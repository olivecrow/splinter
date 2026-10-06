using Splinter.Core;
using Splinter.Runtime;

namespace Splinter.Tests;

public sealed class JournalDuplicateWriteTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(65530, 12)]
    [InlineData(131071, 1)]
    public void ChangedLibraryEnvelopeReusesPrivateEdgesAcrossExtentsAndReplay(int offset, int count)
    {
        using var root = new TestDirectory(); var tree = new SnapshotOverlayTests.FakeSnapshotTree();
        var identity = new OverlayIdentity(2, "edges", tree.Snapshot, 4 * 1024 * 1024, 131072);
        byte[] expected = Enumerable.Repeat((byte)7, 131072).ToArray(); DeltaEntry current;
        using (var store = new JournalDeltaStore(root.Path, identity, tree, optimizeJournal: false))
        {
            store.Create("Library", true, 16); store.Create("Library/Bee", true, 16);
            Guid id = store.Create("Library/Bee/cache", false, 32).Id;
            store.Write(id, 0, expected.AsSpan(0, 65536)); store.Write(id, 65536, expected.AsSpan(65536));
            var before = store.Observe(); Array.Fill(expected, (byte)9, offset, count);
            store.Write(id, 0, expected); var after = store.Observe();
            Assert.Equal(before.PayloadBytes + count, after.PayloadBytes);
            Assert.Equal(before.DurableSequence + 1, after.DurableSequence);
            Assert.Equal(before.Metrics!.FlushCount + 1, after.Metrics!.FlushCount);
            Assert.Equal(0, after.Metrics.BaseReadBytes); Assert.Equal(0, tree.ReadBytes);
            Assert.Equal(0, store.ObserveChanges(["Library"]).Sequence);
            current = store.Get(id);
        }
        using var replay = new JournalDeltaStore(root.Path, identity, tree, optimizeJournal: false);
        Assert.Equal(current, replay.Stat(current.Path));
        byte[] actual = new byte[expected.Length]; Assert.Equal(actual.Length, replay.Read(current.Id, 0, actual));
        Assert.Equal(expected, actual); Assert.Equal(0, tree.ReadBytes);
    }

    [Theory]
    [InlineData("commit.after-header", false)]
    [InlineData("commit.after-flush", true)]
    public void InterruptedTrimmedWriteRecoversOneAcknowledgedEnvelope(string boundary, bool committed)
    {
        using var root = new TestDirectory(); var tree = new SnapshotOverlayTests.FakeSnapshotTree();
        var observer = new Observer(); var identity = new OverlayIdentity(2, "edge-interrupted", tree.Snapshot, 100000, 32768);
        byte[] expected = new byte[32768]; expected[16000] = 1;
        using (var store = new JournalDeltaStore(root.Path, identity, tree, observer, optimizeJournal: false))
        {
            Guid id = store.Create("file", false, 32).Id; store.Write(id, 0, new byte[expected.Length]);
            observer.FailAt = boundary; Assert.Throws<IOException>(() => store.Write(id, 0, expected));
            Assert.Equal("write", observer.Reached!.Operation); Assert.Equal(1, observer.Reached.PayloadBytes);
            Assert.Equal("DeltaReconnectRequired", Assert.Throws<WorkspaceException>(() => store.Write(id, 0, [9])).Code);
        }
        using var replay = new JournalDeltaStore(root.Path, identity, tree, optimizeJournal: false);
        Assert.Equal(committed ? 3 : 2, replay.Observe().DurableSequence);
        Assert.Equal(expected.Length + (committed ? 1 : 0), replay.ObservePrivateUsage().RetainedLogicalPayloadBytes);
        byte[] actual = new byte[expected.Length]; replay.Read(replay.Stat("file")!.Id, 0, actual);
        if (!committed) expected[16000] = 0;
        Assert.Equal(expected, actual); Assert.Equal(0, tree.ReadBytes);
    }

    [Fact]
    public void RepeatedPrivateContentKeepsPayloadAndStillCommitsMetadataAcrossReplay()
    {
        using var root = new TestDirectory(); var tree = new SnapshotOverlayTests.FakeSnapshotTree();
        var identity = new OverlayIdentity(2, "duplicate", tree.Snapshot, 4 * 1024 * 1024, 65536);
        byte[] expected = Enumerable.Repeat((byte)1, 65536).ToArray();
        Array.Fill(expected, (byte)2, 16384, 16384);
        DeltaEntry current; SourceChangeView changes; long payload;
        using (var store = new JournalDeltaStore(root.Path, identity, tree, optimizeJournal: false))
        {
            Guid id = store.Create("file", false, 32).Id;
            store.Write(id, 0, Enumerable.Repeat((byte)1, expected.Length).ToArray());
            store.Write(id, 16384, expected.AsSpan(16384, 16384));
            payload = store.Observe().PayloadBytes;
            store.SetMetadata(id, 32, 1, 1, 1);
            for (int i = 0; i < 16; i++) store.Write(id, 0, expected);
            var observed = store.Observe();
            Assert.Equal(payload, observed.PayloadBytes);
            Assert.Equal(20, observed.DurableSequence);
            Assert.Equal(20, observed.Metrics!.FlushCount); Assert.Equal(20, observed.Metrics.CommitCount);
            Assert.Equal(0, observed.ReadBytes); Assert.Equal(0, observed.Metrics.BaseReadBytes);
            Assert.Equal(0, observed.Metrics.ReadRangeIndex!.ReadCalls);
            current = store.Stat("file")!; Assert.True(current.LastWriteTime > 1);
            changes = store.ObserveChanges([]); Assert.Equal(observed.DurableSequence, changes.Sequence);
        }
        using (var replay = new JournalDeltaStore(root.Path, identity, tree, optimizeJournal: false))
        {
            Assert.Equal(current, replay.Stat("file"));
            Assert.Equal(changes.Changes, replay.ObserveChanges([]).Changes);
            byte[] actual = new byte[expected.Length]; Assert.Equal(actual.Length, replay.Read(current.Id, 0, actual));
            Assert.Equal(expected, actual);
            replay.Write(current.Id, 4096, expected.AsSpan(4096, 32768));
            Assert.Equal(payload, replay.Observe().PayloadBytes);
            Assert.Equal(21, replay.Observe().DurableSequence);
        }
        Assert.Equal(0, tree.ReadBytes);
    }

    [Theory]
    [InlineData("base")]
    [InlineData("hole")]
    [InlineData("different")]
    [InlineData("extension")]
    public void UncoveredOrDifferentContentWritesTheRequestedPayloadWithoutBaseComparison(string scenario)
    {
        using var root = new TestDirectory(); var tree = new SnapshotOverlayTests.FakeSnapshotTree();
        byte[] incoming = new byte[4096]; tree.Files["base"] = incoming.ToArray();
        var identity = new OverlayIdentity(2, "different", tree.Snapshot, 100000, 4096);
        using var store = new JournalDeltaStore(root.Path, identity, tree, optimizeJournal: false);
        Guid id = scenario == "base" ? store.Stat("base")!.Id : store.Create("file", false, 32).Id;
        if (scenario == "hole") { store.SetLength(id, incoming.Length); store.Write(id, 0, [0]); store.Write(id, 4095, [0]); }
        if (scenario is "different" or "extension") store.Write(id, 0, incoming);
        if (scenario == "different") incoming[^1] = 1;
        long offset = scenario == "extension" ? 1 : 0;
        long before = store.ObservePrivateUsage().RetainedLogicalPayloadBytes;
        store.Write(id, offset, incoming);
        Assert.Equal(before + incoming.Length, store.ObservePrivateUsage().RetainedLogicalPayloadBytes);
        Assert.Equal(0, tree.ReadBytes);
        byte[] actual = new byte[incoming.Length]; Assert.Equal(actual.Length, store.Read(id, offset, actual));
        Assert.Equal(incoming, actual); Assert.Equal(0, tree.ReadBytes);
    }

    [Fact]
    public void SmallJournalBudgetAdmitsDuplicateButStillRejectsDifferentPayload()
    {
        using var root = new TestDirectory(); var tree = new SnapshotOverlayTests.FakeSnapshotTree();
        var identity = new OverlayIdentity(2, "budget", tree.Snapshot, 40000, 32768);
        using var store = new JournalDeltaStore(root.Path, identity, tree, optimizeJournal: false);
        Guid id = store.Create("file", false, 32).Id; byte[] bytes = new byte[32768]; new Random(537).NextBytes(bytes);
        byte[] original = bytes.ToArray();
        store.Write(id, 0, bytes); store.Write(id, 0, bytes);
        Assert.Equal(bytes.Length, store.Observe().PayloadBytes);
        Assert.Equal(3, store.Observe().DurableSequence);
        long length = store.Observe().JournalBytes; new Random(538).NextBytes(bytes);
        Assert.Equal("DeltaFull", Assert.Throws<WorkspaceException>(() => store.Write(id, 0, bytes)).Code);
        Assert.Equal(length, store.Observe().JournalBytes); Assert.Equal(3, store.Observe().DurableSequence);
        byte[] actual = new byte[bytes.Length]; store.Read(id, 0, actual); Assert.Equal(original, actual);
    }

    [Theory]
    [InlineData("commit.after-header", false)]
    [InlineData("commit.after-flush", true)]
    public void FailedDuplicateCommitPreservesPriorPayloadAndRecoversItsMetadataBoundary(string boundary, bool committed)
    {
        using var root = new TestDirectory(); var tree = new SnapshotOverlayTests.FakeSnapshotTree();
        var observer = new Observer(); var identity = new OverlayIdentity(2, "interrupted", tree.Snapshot, 100000, 4096);
        using (var store = new JournalDeltaStore(root.Path, identity, tree, observer, optimizeJournal: false))
        {
            Guid id = store.Create("file", false, 32).Id; store.Write(id, 0, [1, 2, 3, 4]);
            store.SetMetadata(id, 32, 1, 1, 1); observer.FailAt = boundary;
            Assert.Throws<IOException>(() => store.Write(id, 0, [1, 2, 3, 4]));
            Assert.Equal("metadata", observer.Reached!.Operation); Assert.Equal(0, observer.Reached.PayloadBytes);
            Assert.Equal("DeltaReconnectRequired", Assert.Throws<WorkspaceException>(() => store.Write(id, 0, [9])).Code);
        }
        using var replay = new JournalDeltaStore(root.Path, identity, tree, optimizeJournal: false);
        var entry = replay.Stat("file")!;
        Assert.Equal(committed ? 4 : 3, replay.Observe().DurableSequence);
        Assert.Equal(4, replay.Observe().PayloadBytes);
        if (committed) Assert.True(entry.LastWriteTime > 1); else Assert.Equal(1, entry.LastWriteTime);
        byte[] actual = new byte[4]; replay.Read(entry.Id, 0, actual); Assert.Equal(new byte[] { 1, 2, 3, 4 }, actual);
        Assert.Equal(0, tree.ReadBytes);
    }

    private sealed class Observer : IJournalMutationObserver
    {
        public string? FailAt; public JournalMutationBoundary? Reached;
        public void Observe(JournalMutationBoundary boundary)
        { if (boundary.Boundary == FailAt) { Reached = boundary; throw new IOException("Duplicate metadata publication cut."); } }
    }
}
