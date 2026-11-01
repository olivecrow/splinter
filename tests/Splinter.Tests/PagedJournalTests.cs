using Splinter.Core;
using Splinter.Platform.Windows;
using Splinter.Runtime;

namespace Splinter.Tests;

public sealed class PagedJournalTests
{
    private static OverlayIdentity Identity(ISnapshotTree basis) => new(3, "paged-journal", basis.Snapshot, 4096, 65536);

    [Fact]
    public void MaterializedCaptureReceiptsRemainUsableAfterTheirIndexIsReclaimed()
    {
        using var root = new TestDirectory(); var basis = new SnapshotOverlayTests.FakeSnapshotTree(); var pins = new WindowsJournalFilePins();
        string delta = Path.Combine(root.Path, "delta"), capturePath = Path.Combine(root.Path, "captures", new string('c', 24)); var identity = Identity(basis);
        JournalSourceCapture capture;
        using (var writer = new JournalDeltaStore(delta, identity, basis, sourceFilePins: pins))
        {
            var file = writer.Create("source", false, 32); writer.Write(file.Id, 0, "result"u8);
            capture = writer.CaptureSource(capturePath, new string('c', 24), []); SourceCaptureTree.Complete(capturePath, pins);
        }
        JournalDeltaStore.ReclaimPagedIndices(delta, identity, pins);
        Assert.False(Directory.Exists(capture.Paged!.Directory));
        Assert.Equal(capture.Digest, SourceCaptureTree.ReadCapture(capturePath).Digest);
        SourceCaptureTree.Complete(capturePath, pins); SourceCaptureTree.RecoverPins(Path.GetDirectoryName(capturePath)!, pins);
    }

    [Fact]
    public void UnicodePathBeyondOnePageUsesComponentsAndReplays()
    {
        using var root = new TestDirectory(); var basis = new SnapshotOverlayTests.FakeSnapshotTree();
        string parent = string.Join('/', Enumerable.Range(0, 85).Select(i => i.ToString("d3") + new string('한', 190)));
        string path = parent + "/file"; basis.Files[path] = [];
        string delta = Path.Combine(root.Path, "delta"); var identity = Identity(basis); Guid id;
        using (var store = new JournalDeltaStore(delta, identity, basis))
        { id = store.Stat(path)!.Id; store.Write(id, 0, "large-name"u8); Assert.Equal(path, store.Get(id).Path); }
        using var reopened = new JournalDeltaStore(delta, identity, basis); byte[] result = new byte[10];
        Assert.Equal(path, reopened.Get(id).Path); Assert.Equal(10, reopened.Read(id, 0, result)); Assert.Equal("large-name"u8.ToArray(), result);
    }

    [Fact]
    public void ExplicitPagedCompactionRevertsBaseEqualRangesAndCompletesExactInventory()
    {
        using var root = new TestDirectory(); var basis = new SnapshotOverlayTests.FakeSnapshotTree();
        byte[] original = Enumerable.Repeat((byte)17, 8192).ToArray(); basis.Files["Assets/file"] = original;
        string delta = Path.Combine(root.Path, "delta"); var identity = Identity(basis); Guid id;
        using (var writer = new JournalDeltaStore(delta, identity, basis))
        { id = writer.Stat("Assets/file")!.Id; writer.Write(id, 0, original); writer.Write(id, 4096, [18]); }
        var compacted = JournalDeltaStore.CompactDirectory(delta, identity, basis, revertBaseRanges: true);
        Assert.True(compacted.RevertedPayloadBytes >= 4096); Assert.False(compacted.JournalOptimizationEnabled);
        using (var reader = JournalDeltaStore.OpenReadOnly(delta, identity, basis))
        {
            byte[] result = new byte[8192]; Assert.Equal(result.Length, reader.Read(id, 0, result));
            Assert.Equal((byte)18, result[4096]); Assert.All(result[..4096], b => Assert.Equal((byte)17, b));
            Assert.Equal(compacted.After, reader.Seal());
        }
        JournalDeltaStore.CompleteCompaction(delta, identity);
        var seal = compacted.After;
        SegmentedJournalReclamation.ReclaimDirectory(delta, seal, ["identity.json"], Path.Combine(root.Path, "reclaim.bin"));
        Assert.False(Directory.Exists(delta));
    }

    [Fact]
    public void PagedIndicesPreserveSlicesRenameTruncateAndReconnectWithoutJournalQuota()
    {
        using var root = new TestDirectory(); var basis = new SnapshotOverlayTests.FakeSnapshotTree();
        basis.Files["Assets/base"] = "basis"u8.ToArray(); var identity = Identity(basis); string delta = Path.Combine(root.Path, "delta");
        Guid file;
        using (var writer = new JournalDeltaStore(delta, identity, basis))
        {
            Assert.Equal("segmented", writer.JournalStorageMode);
            file = writer.Create("Assets/private", false, 32).Id;
            writer.Write(file, 0, new byte[32768]); writer.Write(file, 3, "fixed"u8); writer.SetLength(file, 18);
            writer.Rename(writer.Stat("Assets")!.Id, "Moved", false);
            Assert.Null(writer.Stat("Assets")); Assert.Equal("Moved/private", writer.Get(file).Path);
            byte[] bytes = new byte[18]; Assert.Equal(18, writer.Read(file, 0, bytes)); Assert.Equal("fixed"u8.ToArray(), bytes[3..8]);
            Assert.True(writer.GetJournalBytes() > 4096); Assert.Equal(new[] { "Moved/base", "Moved/private" }, writer.EnumerateChildren("Moved").Select(e => e.Path));
        }
        using var replay = new JournalDeltaStore(delta, identity, basis);
        Assert.Equal("Moved/private", replay.Get(file).Path); Assert.Null(replay.Stat("Assets"));
        byte[] result = new byte[18]; Assert.Equal(18, replay.Read(file, 0, result)); Assert.Equal("fixed"u8.ToArray(), result[3..8]);
        Assert.Equal(new[] { "Moved/base", "Moved/private" }, replay.EnumerateChildren("Moved").Select(e => e.Path));
    }

    [Fact]
    public void DirectoryCursorKeepsOriginalMembershipDuringLaterWrites()
    {
        using var root = new TestDirectory(); var basis = new SnapshotOverlayTests.FakeSnapshotTree();
        basis.Files["Assets/source.txt"] = [];
        using var writer = new JournalDeltaStore(Path.Combine(root.Path, "delta"), Identity(basis), basis);
        var first = writer.Create("Assets/first", false, 32); var second = writer.Create("Assets/second", false, 32);
        var cursor = writer.EnumerateChildren("Assets");
        writer.Rename(first.Id, "Assets/moved", false); writer.Delete(second.Id); writer.Create("Assets/later", false, 32);
        Assert.Equal(new[] { "Assets/first", "Assets/second", "Assets/source.txt" }, cursor.Select(e => e.Path));
    }

    [Fact]
    public void FrozenPagedRootsSurviveOverwriteCheckpointRenameAndWriterReconnect()
    {
        if (!OperatingSystem.IsWindows()) Assert.Skip("NTFS durable backing aliases.");
        using var root = new TestDirectory(); var basis = new SnapshotOverlayTests.FakeSnapshotTree(); var pins = new WindowsJournalFilePins();
        basis.Files["Assets/source.txt"] = [];
        var identity = Identity(basis); string delta = Path.Combine(root.Path, "delta"), directory = Path.Combine(root.Path, "captures", new string('b', 24));
        Guid file;
        using (var writer = new JournalDeltaStore(delta, identity, basis, boundedLogBytes: 8192, sourceFilePins: pins))
        {
            file = writer.Create("Assets/private", false, 32).Id; writer.Write(file, 0, "frozen"u8);
            var capture = writer.CaptureSource(directory, new string('b', 24), []);
            Assert.Equal(2, capture.Version); Assert.NotNull(capture.Paged); Assert.Empty(capture.Nodes); Assert.Empty(capture.ChangedPaths);
            var repeated = writer.CaptureSource(directory, new string('b', 24), []); Assert.Equal(capture.Digest, repeated.Digest);
            for (int i = 0; i < 50; i++) writer.Write(file, 0, Enumerable.Repeat((byte)i, 128).ToArray());
            writer.Rename(file, "Assets/later", false); Assert.True(writer.BoundedCheckpointCount > 0);
            using var frozen = new SourceCaptureTree(directory, basis, pins); byte[] result = new byte[6];
            Assert.Equal(6, frozen.Read("Assets/private", 0, result)); Assert.Equal("frozen"u8.ToArray(), result);
            Assert.Contains("Assets/private", frozen.EnumerateChangedPaths()!); Assert.Null(frozen.Stat("Assets/later"));
        }
        SourceCaptureTree.RecoverPins(Path.Combine(root.Path, "captures"), pins);
        using (var writer = new JournalDeltaStore(delta, identity, basis, sourceFilePins: pins)) writer.SetLength(file, 0);
        using (var frozen = new SourceCaptureTree(directory, basis, pins))
        { byte[] result = new byte[6]; Assert.Equal(6, frozen.Read("Assets/private", 0, result)); Assert.Equal("frozen"u8.ToArray(), result); }
        SourceCaptureTree.Complete(directory, pins); Assert.Empty(Directory.EnumerateFiles(directory, "*.pin"));
    }
}
