using System.Buffers.Binary;
using System.Diagnostics;
using System.Text.Json;
using Splinter.Core;
using Splinter.Platform.Windows;
using Splinter.Runtime;

namespace Splinter.Tests;

public sealed class CombinedOverwriteCostTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OverwriteCallbackPublishesOneCompleteResize(bool replace)
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Windows callback contract.");
        using var temp = new TestDirectory(); var tree = new SnapshotOverlayTests.FakeSnapshotTree();
        tree.Files["file"] = [1, 2, 3];
        var identity = new OverlayIdentity(2, "combined-overwrite", tree.Snapshot, 1048576, 4096);
        using var store = new JournalDeltaStore(temp.Path, identity, tree, boundedLogBytes: 65536);
        var old = store.Stat("file")!; long sequence = store.Observe().DurableSequence;
        var fs = new WinFspOverlayFileSystem(store);
        Assert.Equal(0, fs.Overwrite(old.Id, null!, (uint)FileAttributes.Hidden, replace, 4096, out var info));
        Assert.Equal(sequence + 1, store.Observe().DurableSequence);
        var result = store.Get(old.Id);
        Assert.Equal(0, result.Length); Assert.Equal(0, result.BaseLength);
        Assert.Equal(replace ? 2u : 34u, result.Attributes);
        Assert.Equal(result.Attributes, info.FileAttributes); Assert.Equal(0UL, info.FileSize);
        Assert.Equal(old.CreationTime, result.CreationTime); Assert.Equal(old.LastAccessTime, result.LastAccessTime);
        Assert.True(result.LastWriteTime > old.LastWriteTime);
        Assert.Equal(new byte[] { 1, 2, 3 }, tree.Files["file"]);
    }

    [Fact]
    public void CompareOverwriteAndRefillWithDurableJournal()
    {
        string? output = Environment.GetEnvironmentVariable("SPLINTER_COMBINED_OVERWRITE_COST");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(output), "Opt-in local durable-write cost comparison.");
        Assert.True(Path.IsPathFullyQualified(output!)); Assert.False(File.Exists(output));
        string root = output + ".data"; Assert.False(Directory.Exists(root)); Directory.CreateDirectory(root);
        const int files = 64, rounds = 128, bytes = 4096;
        var rows = new List<object>(); int run = 0;
        foreach (bool combined in new[] { false, true, true, false })
        {
            var tree = new SnapshotOverlayTests.FakeSnapshotTree();
            for (int i = 0; i < files; i++) tree.Files[$"file-{i}"] = Enumerable.Repeat((byte)7, bytes).ToArray();
            var identity = new OverlayIdentity(2, "combined-overwrite-cost", tree.Snapshot, 256 * 1048576L, bytes);
            string delta = Path.Combine(root, (++run).ToString());
            double seconds; long mutations; SnapshotDeltaSeal seal;
            byte[] payload = Enumerable.Repeat((byte)7, bytes).ToArray();
            using (var store = new JournalDeltaStore(delta, identity, tree, boundedLogBytes: 16 * 1048576))
            {
                Guid[] ids = Enumerable.Range(0, files).Select(i => store.Stat($"file-{i}")!.Id).ToArray();
                long sequence = store.Observe().DurableSequence, start = Stopwatch.GetTimestamp();
                for (int round = 0; round < rounds; round++)
                for (int i = 0; i < files; i++)
                {
                    TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
                    if (combined) store.SetLengthAndMetadata(ids[i], 0, 32, 1, 1, 10000 + round);
                    else { store.SetLength(ids[i], 0); store.SetMetadata(ids[i], 32, 1, 1, 10000 + round); }
                    BinaryPrimitives.WriteInt64LittleEndian(payload, round * files + i);
                    store.Write(ids[i], 0, payload);
                }
                store.Flush(); seconds = Stopwatch.GetElapsedTime(start).TotalSeconds;
                seal = store.Seal(); mutations = store.Observe().DurableSequence - sequence;
                Assert.Equal(files * rounds * (combined ? 2 : 3), mutations);
            }
            using (var reopened = new JournalDeltaStore(delta, identity, tree, boundedLogBytes: 16 * 1048576))
            {
                Assert.Equal(seal, reopened.Seal());
                byte[] actual = new byte[bytes];
                for (int i = 0; i < files; i++)
                {
                    var entry = reopened.Stat($"file-{i}")!;
                    Assert.Equal(bytes, entry.Length); Assert.Equal(0, entry.BaseLength);
                    Assert.Equal(bytes, reopened.Read(entry.Id, 0, actual));
                    BinaryPrimitives.WriteInt64LittleEndian(payload, (rounds - 1) * files + i);
                    Assert.Equal(payload, actual);
                }
            }
            rows.Add(new { combined, seconds, mutations, seal, replayExact = true });
        }
        using var stream = new FileStream(output!, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        JsonSerializer.Serialize(stream, new
        {
            scope = "local SJR2 overwrite+refill loop and final flush; no mount or Unity lifecycle",
            files, rounds, bytes, saves = files * rounds, historicalUnityOverwrites = 260,
            historicalFrequencyAmplification = files * rounds / 260.0,
            sameBuildExistingAndCombinedPaths = true, setupAndReopenProofOutsideTiming = true, rows
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true });
    }
}
