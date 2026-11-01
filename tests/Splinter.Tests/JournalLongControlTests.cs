using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Splinter.Core;
using Splinter.Runtime;
using Splinter.Platform.Windows;

namespace Splinter.Tests;

public sealed class JournalLongControlTests
{
    [Fact]
    public void ChunkedPackingAndFinalInventoryHandleARealExtendedNtfsRootBeyondOneJsonPage()
    {
        if (!OperatingSystem.IsWindows()) Assert.Skip("Exact extended NTFS control paths and native ownership.");
        using var root = new TestDirectory(); var basis = new SnapshotOverlayTests.FakeSnapshotTree(); var pins = new WindowsJournalFilePins();
        string path = root.Path;
        for (int index = 0; index < 60; index++) path = Path.Combine(path, new string('\uAC00', 200) + index.ToString("d2"));
        string control = @"\\?\" + path; Directory.CreateDirectory(control);
        Assert.InRange(control.Length, 12000, 32700);
        string delta = Path.Combine(control, "delta"), receipt = Path.Combine(root.Path, "reclaim.bin");
        var identity = new OverlayIdentity(3, "long-owned-control", basis.Snapshot, long.MaxValue, 65536);
        Guid id; SnapshotDeltaSeal seal;
        using (var source = new JournalDeltaStore(delta, identity, basis, sourceFilePins: pins))
        {
            id = source.Create("kept", false, 32).Id; source.Write(id, 0, Enumerable.Repeat((byte)41, 8192).ToArray()); source.SetLength(id, 16); seal = source.Seal();
            Assert.Throws<IOException>(() => source.MaintainLiveStorage(ct: TestContext.Current.CancellationToken, collectSegments: true, boundary: point =>
            {
                if (point != "segment-pack.intent") return;
                var job = typeof(JournalDeltaStore).GetMethod("PendingPacking", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(source, null)!;
                Assert.True(JsonSerializer.SerializeToUtf8Bytes(job, job.GetType(), new JsonSerializerOptions(JsonSerializerDefaults.Web)).Length > 65536);
                VerifyCodecOnlyMaximumRoot(source, job);
                throw new IOException("resume the same long-root packing request");
            }));
        }
        using (var source = new JournalDeltaStore(delta, identity, basis, sourceFilePins: pins))
        {
            source.MaintainLiveStorage(ct: TestContext.Current.CancellationToken, collectSegments: true); Assert.Equal(seal, source.Seal());
            byte[] observed = new byte[16]; Assert.Equal(16, source.Read(id, 0, observed)); Assert.All(observed, value => Assert.Equal((byte)41, value));
        }
        Assert.Throws<IOException>(() => SegmentedJournalReclamation.ReclaimDirectory(delta, seal, ["identity.json"], receipt, point =>
        {
            if (point != "intent-flushed") return;
            using var pending = new BinaryReader(File.OpenRead(receipt + ".pending")); pending.ReadBytes(5); Assert.True(pending.ReadInt32() > 65536);
            throw new IOException("long inventory response lost");
        }, filePins: pins));
        SegmentedJournalReclamation.ReclaimDirectory(delta, seal, ["identity.json"], receipt, filePins: pins);
        Assert.False(Directory.Exists(delta));
    }
    private static void VerifyCodecOnlyMaximumRoot(JournalDeltaStore source, object original)
    {
        // Codec qualification at approximately 32K UTF-16 path units. The
        // physical NTFS test above uses a distinct, real 12K root; this helper
        // does not claim that an entire CLI/Unity home ran at maximum length.
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web); Type type = original.GetType();
        var value = JsonSerializer.SerializeToNode(original, type, json)!.AsObject();
        string logical = @"\\?\C:\";
        while (logical.Length + 202 < 31900) logical += new string('\uAC00', 200) + "\\";
        value["directory"] = logical + "job"; value["indexDirectory"] = logical + "index"; value["stage"] = logical + "job\\data.tmp"; value["digest"] = "";
        object changed = value.Deserialize(type, json)!;
        Type integrity = typeof(JournalDeltaStore).Assembly.GetType("Splinter.Runtime.JournalControlIntegrity")!;
        string digest = (string)integrity.GetMethod("Digest", BindingFlags.Static | BindingFlags.NonPublic)!.MakeGenericMethod(type).Invoke(null, [changed])!;
        type.GetProperty("Digest")!.SetValue(changed, digest);
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var rootField = typeof(JournalDeltaStore).GetField("packingRoot", flags)!;
        var prior = (MetadataTreeRoot)rootField.GetValue(source)!;
        var pages = (PagedMetadataStore)typeof(JournalDeltaStore).GetField("pagedIndex", flags)!.GetValue(source)!;
        try
        {
            typeof(JournalDeltaStore).GetMethod("StorePackingJob", flags)!.Invoke(source, [changed]);
            var record = pages.Open((MetadataTreeRoot)rootField.GetValue(source)!).FindEntry("request")!;
            Assert.Equal(36, record.Value.Length); Assert.True(pages.Open(record.Child!).FindEntry("control")!.Child!.Count > 1);
            object read = typeof(JournalDeltaStore).GetMethod("ReadPackingJob", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [pages, record])!;
            Assert.Equal(type.GetProperty("Directory")!.GetValue(changed), type.GetProperty("Directory")!.GetValue(read));
        }
        finally { rootField.SetValue(source, prior); }
    }
}
