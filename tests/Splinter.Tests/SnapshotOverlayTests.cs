using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Splinter.Core;
using Splinter.Runtime;
using Splinter.Platform.Windows;

namespace Splinter.Tests;

public sealed class SnapshotOverlayTests
{
    [Fact]
    public void SourceChangeBarrierIgnoresManagedWritesAndSurvivesJournalReplayWithNewEpoch()
    {
        using var d = new TestDirectory(); var tree = new FakeSnapshotTree();
        tree.Files["dir/code.cs"] = [1, 2]; tree.Files["Library/cache"] = [3];
        SourceChangeView beforeReplay;
        using (var store = New(d, tree))
        {
            Assert.Equal(0, store.ObserveChanges(["Library"]).Sequence);
            var root = store.Stat("")!;
            store.SetMetadata(root.Id, root.Attributes, root.CreationTime, root.LastAccessTime, root.LastWriteTime + 1);
            Assert.Equal(0, store.ObserveChanges(["Library"]).Sequence);
            store.Write(store.Stat("Library/cache")!.Id, 0, new byte[] { 4 });
            Assert.Equal(0, store.ObserveChanges(["Library"]).Sequence);
            store.Write(store.Stat("dir/code.cs")!.Id, 0, new byte[] { 5 });
            store.Rename(store.Stat("dir")!.Id, "moved", false);
            beforeReplay = store.ObserveChanges(["Library"]);
            Assert.Equal(4, beforeReplay.Sequence);
            Assert.Contains(beforeReplay.Changes, p => p.Path == "dir");
            Assert.Contains(beforeReplay.Changes, p => p.Path == "moved");
            Assert.DoesNotContain(beforeReplay.Changes, p => p.Path.StartsWith("Library"));
            var metrics = store.Observe().Metrics!;
            Assert.Equal(4, metrics.CommitCount); Assert.True(metrics.FlushCount >= 4);
            Assert.Equal(0, metrics.BaseReadBytes); Assert.Equal(0, metrics.ReplayBytes);
        }
        using var resumed = New(d, tree);
        var current = resumed.ObserveChanges(["Library"]);
        Assert.NotEqual(beforeReplay.Epoch, current.Epoch);
        Assert.Equal(beforeReplay.Sequence, current.Sequence); Assert.Equal(beforeReplay.Changes, current.Changes);
        Assert.True(resumed.Observe().Metrics!.ReplayBytes > 0);
        Assert.Equal(0, resumed.Observe().Metrics!.CommitCount);
    }

    [Fact]
    public void RepeatedWritesRetainJournalAllocationAndCountersDoNotConfuseLiveContentWithHistory()
    {
        using var d = new TestDirectory(); var tree = new FakeSnapshotTree(); tree.Files["large"] = new byte[16 * 1024 * 1024];
        using var store = New(d, tree); var file = store.Stat("large")!;
        byte[] block = new byte[4096];
        for (int i = 0; i < 100; i++) { block[0] = (byte)i; store.Write(file.Id, 8192, block); }
        var before = store.Observe(); Assert.Equal(409600, before.PayloadBytes);
        Assert.Equal(100, before.Metrics!.CommitCount); Assert.Equal(100, before.Metrics.FlushCount);
        store.SetLength(file.Id, 4096); store.Delete(file.Id);
        var after = store.Observe(); Assert.Equal(before.PayloadBytes, after.PayloadBytes);
        Assert.True(after.JournalBytes > before.JournalBytes); Assert.Equal(0, after.ReadCacheBytes);
        Assert.Equal(after.JournalBytes, after.Metrics!.JournalAppendedBytes);
        Assert.True(after.Metrics.LockAcquisitions > 100);
    }

    [Fact]
    public void VerifiedReadLocationCanMoveWhileJournalIdentityAndAcknowledgedBytesStayFixed()
    {
        using var d = new TestDirectory();
        var snapshot = VssLiveIdentityTests.Snapshot;
        byte[] source = RandomNumberGenerator.GetBytes(1024 * 1024), patch = RandomNumberGenerator.GetBytes(4096);
        var locations = new Dictionary<string, byte[]> { [snapshot.RootPath] = source };
        var first = new FakeSnapshotTree { Snapshot = snapshot };
        first.Files["file"] = locations[VssSnapshotTree.ResolveReadRoot(snapshot, null)];
        long sequence;
        using (var store = New(d, first))
        {
            store.Write(store.Stat("file")!.Id, 17, patch); store.Flush();
            sequence = store.Observe().DurableSequence;
        }
        string identity = Path.Combine(d.Path, "delta", "identity.json"), journal = Path.Combine(d.Path, "delta", "delta.journal");
        byte[] identityBefore = File.ReadAllBytes(identity), journalBefore = File.ReadAllBytes(journal);
        var access = VssSnapshotVerification.Evaluate(snapshot, "owner", VssLiveIdentityTests.Capture,
            VssLiveIdentityTests.Request, VssLiveIdentityTests.Live, VssLiveIdentityTests.Request.BootTimeUtc,
            s => new(s.Id, true, null, null, null)).ReadAccess!;
        locations.Remove(snapshot.RootPath); locations[access.RootPath] = source;
        var resumedTree = new FakeSnapshotTree { Snapshot = snapshot };
        resumedTree.Files["file"] = locations[VssSnapshotTree.ResolveReadRoot(snapshot, access)];
        using var resumed = New(d, resumedTree);
        byte[] actual = new byte[source.Length], expected = (byte[])source.Clone(); patch.CopyTo(expected, 17);
        resumed.Read(resumed.Stat("file")!.Id, 0, actual);
        Assert.Equal(SHA256.HashData(expected), SHA256.HashData(actual));
        Assert.Equal(sequence, resumed.Observe().DurableSequence);
        Assert.Equal(4096, resumed.Observe().PayloadBytes); Assert.Equal(0, resumed.Observe().ReadCacheBytes);
        resumed.Dispose();
        Assert.Equal(identityBefore, File.ReadAllBytes(identity)); Assert.Equal(journalBefore, File.ReadAllBytes(journal));
        Assert.Equal(snapshot, resumedTree.Snapshot);
        Assert.Equal("SnapshotIdentityMismatch", Assert.Throws<WorkspaceException>(() =>
            VssSnapshotTree.ResolveReadRoot(snapshot, access with { RootPath = access.RootPath + "\\other" })).Code);
    }

    [Fact]
    public void RepeatedWholeFileReadsNeverWriteJournalOrHydrate()
    {
        using var d = new TestDirectory(); var tree = new FakeSnapshotTree();
        byte[] source = RandomNumberGenerator.GetBytes(1024 * 1024); tree.Files["large"] = source;
        using var store = New(d, tree); var file = store.Stat("large")!;
        var before = store.Observe(); byte[] actual = new byte[source.Length];
        for (int i = 0; i < 3; i++) { Assert.Equal(source.Length, store.Read(file.Id, 0, actual)); Assert.Equal(source, actual); }
        var after = store.Observe();
        Assert.Equal(before.JournalBytes, after.JournalBytes); Assert.Equal(0, after.PayloadBytes);
        Assert.Equal(0, after.ReadCacheBytes); Assert.Equal(3L * source.Length, after.ReadBytes);
    }

    [Theory]
    [InlineData(1024 * 1024)]
    [InlineData(16 * 1024 * 1024)]
    public void SmallBoundaryWritesPersistOnlyTheWrittenRanges(int size)
    {
        using var d = new TestDirectory(); var tree = new FakeSnapshotTree();
        byte[] source = RandomNumberGenerator.GetBytes(size); tree.Files["file"] = source;
        byte[] expected = (byte[])source.Clone(), first = RandomNumberGenerator.GetBytes(4096), second = RandomNumberGenerator.GetBytes(4096);
        Guid id; long payload;
        using (var store = New(d, tree))
        {
            id = store.Stat("file")!.Id; store.Write(id, 4095, first); store.Write(id, 6144, second);
            first.CopyTo(expected, 4095); second.CopyTo(expected, 6144);
            var observed = store.Observe(); payload = observed.PayloadBytes;
            Assert.Equal(8192, payload); Assert.InRange(observed.JournalBytes, 8192, 12000);
            Assert.Equal(0, tree.ReadBytes); // writing never reads the rest of a file
        }
        using var resumed = New(d, tree); byte[] actual = new byte[size];
        Assert.Equal(id, resumed.Stat("file")!.Id); Assert.Equal(size, resumed.Read(id, 0, actual));
        Assert.Equal(expected, actual); Assert.Equal(payload, resumed.Observe().PayloadBytes);
        Assert.Equal(source, tree.Files["file"]);
    }

    [Fact]
    public void TruncateThenExtendDoesNotResurrectBaseOrDeltaTail()
    {
        using var d = new TestDirectory(); var tree = new FakeSnapshotTree(); tree.Files["file"] = Enumerable.Repeat((byte)71, 10000).ToArray();
        using (var store = New(d, tree))
        {
            var id = store.Stat("file")!.Id; store.Write(id, 4000, Enumerable.Repeat((byte)99, 2000).ToArray());
            store.SetLength(id, 4500); store.SetLength(id, 12000);
        }
        using var resumed = New(d, tree); byte[] actual = new byte[12000]; resumed.Read(resumed.Stat("file")!.Id, 0, actual);
        Assert.All(actual[..4000], b => Assert.Equal(71, b)); Assert.All(actual[4000..4500], b => Assert.Equal(99, b));
        Assert.All(actual[4500..], b => Assert.Equal(0, b));
    }

    [Fact]
    public void RenameDirectoriesPreservesLazyBasePathsAndDeletionAcrossReconnect()
    {
        using var d = new TestDirectory(); var tree = new FakeSnapshotTree(); tree.Files["dir/a"] = [1, 2]; tree.Files["dir/b"] = [3, 4];
        using (var store = New(d, tree))
        {
            store.Delete(store.Stat("dir/a")!.Id); store.Rename(store.Stat("dir")!.Id, "moved", false);
            Assert.Null(store.Stat("dir")); Assert.Null(store.Stat("moved/a")); Assert.NotNull(store.Stat("moved/b"));
        }
        using var resumed = New(d, tree); Assert.Null(resumed.Stat("dir")); Assert.Null(resumed.Stat("moved/a"));
        byte[] data = new byte[2]; resumed.Read(resumed.Stat("moved/b")!.Id, 0, data); Assert.Equal(new byte[] { 3, 4 }, data);
        Assert.Single(resumed.List("moved"));
    }

    [Fact]
    public void ReplaceDoesNotChangeAnOpenHandleToTheOldFile()
    {
        using var d = new TestDirectory(); var tree = new FakeSnapshotTree(); tree.Files["original"] = [1, 2];
        using var store = New(d, tree); var old = store.Stat("original")!;
        var temp = store.Create("temp", false, 32); store.Write(temp.Id, 0, new byte[] { 7, 8 }); store.Rename(temp.Id, "original", true);
        byte[] bytes = new byte[2]; store.Read(old.Id, 0, bytes); Assert.Equal(new byte[] { 1, 2 }, bytes);
        store.Read(store.Stat("original")!.Id, 0, bytes); Assert.Equal(new byte[] { 7, 8 }, bytes);
        Assert.Null(store.Stat("temp"));
    }

    [Fact]
    public void BudgetFailureAndIncompleteAppendKeepAcknowledgedData()
    {
        using var d = new TestDirectory(); var tree = new FakeSnapshotTree(); tree.Files["file"] = [1, 2];
        using (var store = New(d, tree, 4500))
        {
            var id = store.Stat("file")!.Id; store.Write(id, 0, new byte[] { 7, 8 });
            long before = store.Observe().JournalBytes;
            Assert.Equal("DeltaFull", Assert.Throws<WorkspaceException>(() => store.Write(id, 0, new byte[4096])).Code);
            Assert.Equal(before, store.Observe().JournalBytes);
        }
        string journal = Path.Combine(d.Path, "delta", "delta.journal");
        using (var append = new FileStream(journal, FileMode.Append)) append.Write(new byte[] { 10, 0, 0 });
        using var resumed = New(d, tree, 4500); byte[] data = new byte[2]; resumed.Read(resumed.Stat("file")!.Id, 0, data);
        Assert.Equal(new byte[] { 7, 8 }, data); Assert.Equal(1, resumed.Observe().DurableSequence);
    }

    [Fact]
    public void CorruptionAndSnapshotLossFailClosedWithoutDeletingTheDelta()
    {
        using var d = new TestDirectory(); var tree = new FakeSnapshotTree(); tree.Files["file"] = [1, 2];
        using (var store = New(d, tree))
        {
            var id = store.Stat("file")!.Id; store.Write(id, 0, new byte[] { 9 }); tree.Available = false;
            Assert.Equal("SnapshotMissing", Assert.Throws<WorkspaceException>(() => store.Read(id, 0, new byte[2])).Code);
            store.Flush(); // Preserve acknowledged private bytes even when the base disappears.
        }
        tree.Available = true; string path = Path.Combine(d.Path, "delta", "delta.journal");
        byte[] corrupted = File.ReadAllBytes(path); corrupted[^1] ^= 1; File.WriteAllBytes(path, corrupted);
        Assert.Equal("DeltaCorrupt", Assert.Throws<WorkspaceException>(() => New(d, tree)).Code);
        Assert.Equal(corrupted, File.ReadAllBytes(path));
    }

    [Fact]
    public void ReconnectRejectsDifferentSnapshotAndConcurrentWriter()
    {
        using var d = new TestDirectory(); var tree = new FakeSnapshotTree();
        using (var store = New(d, tree)) Assert.Throws<IOException>(() => New(d, tree));
        tree.Snapshot = tree.Snapshot with { Id = "new-time" };
        Assert.Equal("OverlayIdentityMismatch", Assert.Throws<WorkspaceException>(() => New(d, tree)).Code);
    }

    [Theory]
    [InlineData("json")]
    [InlineData("entry")]
    [InlineData("path")]
    [InlineData("range")]
    public void CompleteInvalidMetadataPreservesTheJournalAndReturnsCorruption(string fault)
    {
        using var d = new TestDirectory(); var tree = new FakeSnapshotTree(); tree.Files["file"] = [1, 2];
        using (var store = New(d, tree)) store.Write(store.Stat("file")!.Id, 0, new byte[] { 9 });
        string journal = Path.Combine(d.Path, "delta", "delta.journal");
        byte[] frame = File.ReadAllBytes(journal);
        int metadataLength = BinaryPrimitives.ReadInt32LittleEndian(frame);
        var metadata = JsonNode.Parse(frame.AsSpan(8, metadataLength))!;
        switch (fault)
        {
            case "entry": metadata["Entry"] = null; break;
            case "path": metadata["Entry"]!["Path"] = "../outside"; break;
            case "range": metadata["Offset"] = long.MaxValue; break;
        }
        byte[] encoded = fault == "json" ? "{"u8.ToArray() : System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(metadata);
        byte[] replacement = new byte[8 + encoded.Length + 1 + 32];
        BinaryPrimitives.WriteInt32LittleEndian(replacement, encoded.Length);
        BinaryPrimitives.WriteInt32LittleEndian(replacement.AsSpan(4), 1);
        encoded.CopyTo(replacement, 8); replacement[8 + encoded.Length] = 9;
        SHA256.HashData(replacement.AsSpan(0, replacement.Length - 32), replacement.AsSpan(replacement.Length - 32));
        File.WriteAllBytes(journal, replacement);
        Assert.Equal("DeltaCorrupt", Assert.Throws<WorkspaceException>(() => New(d, tree)).Code);
        Assert.Equal(replacement, File.ReadAllBytes(journal)); Assert.Equal(new byte[] { 1, 2 }, tree.Files["file"]);
    }

    [Fact]
    public void CorruptIdentityReturnsStableFailureWithoutChangingEitherFile()
    {
        using var d = new TestDirectory(); var tree = new FakeSnapshotTree(); tree.Files["file"] = [1, 2];
        using (var store = New(d, tree)) store.Write(store.Stat("file")!.Id, 0, new byte[] { 9 });
        string identity = Path.Combine(d.Path, "delta", "identity.json"), journal = Path.Combine(d.Path, "delta", "delta.journal");
        byte[] bytes = File.ReadAllBytes(journal); File.WriteAllText(identity, "{\"Version\":");
        Assert.Equal("DeltaCorrupt", Assert.Throws<WorkspaceException>(() => New(d, tree)).Code);
        Assert.Equal("{\"Version\":", File.ReadAllText(identity)); Assert.Equal(bytes, File.ReadAllBytes(journal));
    }

    [Fact]
    public void ExclusionsHideOnlyTheLowerTreeAndPersistByValueOnReconnect()
    {
        using var d = new TestDirectory(); var tree = new FakeSnapshotTree(); tree.Files[".git/config"] = [1]; tree.Files["Library/cache"] = [2];
        OverlayIdentity Identity() => new(1, "excluded", tree.Snapshot, 100000, 4096, [".git", "Temp"]);
        string delta = Path.Combine(d.Path, "delta");
        using (var store = new JournalDeltaStore(delta, Identity(), tree))
        {
            Assert.Null(store.Stat(".git")); Assert.NotNull(store.Stat("Library/cache"));
            var pointer = store.Create(".git", false, 32); store.Write(pointer.Id, 0, new byte[] { 9, 8 });
        }
        using var resumed = new JournalDeltaStore(delta, Identity(), tree);
        byte[] bytes = new byte[2]; Assert.Equal(2, resumed.Read(resumed.Stat(".git")!.Id, 0, bytes));
        Assert.Equal(new byte[] { 9, 8 }, bytes); Assert.Null(resumed.Stat(".git/config"));
        Assert.Equal(new byte[] { 1 }, tree.Files[".git/config"]);
    }

    [Fact]
    public void ReclamationRequiresCurrentIndependentResultAndNoOwnedResourcesOrDependents()
    {
        var storage = new OverlayStorageObservation("a", "snapshot", 0, 100, 20, 80, 0, 0, 4096, 1, 3);
        var receipt = new OverlayResultReceipt("a", "snapshot", 3, "result", "verified-hash", true);
        var clear = new OverlayMountObservation(false, true, 0, 0, 0, null);
        Assert.Throws<WorkspaceException>(() => OverlayReleaseGate.RequireSafe(storage, receipt with { DurableSequence = 2 }, clear, 0));
        Assert.Throws<WorkspaceException>(() => OverlayReleaseGate.RequireSafe(storage, receipt, clear with { OpenHandles = 1 }, 0));
        Assert.Throws<WorkspaceException>(() => OverlayReleaseGate.RequireSafe(storage, receipt, clear, 1));
        OverlayReleaseGate.RequireSafe(storage, receipt, clear, 0);
    }

    private static JournalDeltaStore New(TestDirectory d, FakeSnapshotTree tree, long budget = 100 * 1024 * 1024) =>
        new(Path.Combine(d.Path, "delta"), new(1, "test", tree.Snapshot, budget, 1024 * 1024), tree);

    private sealed class FakeSnapshotTree : ISnapshotTree
    {
        public SnapshotReference Snapshot { get; set; } = new("fake-t0", "fake-memory", "mutable-source", "fake-root", DateTimeOffset.UnixEpoch, true, true, "fixture-only");
        public Dictionary<string, byte[]> Files { get; } = new(StringComparer.OrdinalIgnoreCase);
        public bool Available { get; set; } = true;
        public long ReadBytes { get; private set; }
        public void EnsureAvailable() { if (!Available) throw new WorkspaceException("SnapshotMissing", "Injected snapshot loss."); }
        public SnapshotEntry? Stat(string relativePath)
        {
            EnsureAvailable(); relativePath = JournalDeltaStore.Normalize(relativePath);
            if (Files.TryGetValue(relativePath, out var data)) return new(relativePath, false, data.Length, 32, 1, 1, 1);
            return relativePath == "" || Files.Keys.Any(p => p.StartsWith(relativePath + "/", StringComparison.OrdinalIgnoreCase))
                ? new(relativePath, true, 0, 16, 1, 1, 1) : null;
        }
        public IReadOnlyList<SnapshotEntry> List(string relativeDirectory)
        {
            EnsureAvailable(); string prefix = relativeDirectory.Length == 0 ? "" : relativeDirectory + "/";
            return Files.Keys.Where(p => p.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .Select(p => prefix + p[prefix.Length..].Split('/')[0]).Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(p => Stat(p)!).ToArray();
        }
        public int Read(string relativePath, long offset, Span<byte> destination)
        {
            EnsureAvailable(); byte[] data = Files[relativePath]; int count = (int)Math.Min(destination.Length, Math.Max(0, data.Length - offset));
            data.AsSpan((int)offset, count).CopyTo(destination); ReadBytes += count; return count;
        }
    }
}
