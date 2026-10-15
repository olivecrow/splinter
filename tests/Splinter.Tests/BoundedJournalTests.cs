using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Splinter.Core;
using Splinter.Runtime;

namespace Splinter.Tests;

// File-backed Runtime qualification; these tests do not claim WinFsp, Unity,
// process termination, or OS power-loss coverage.
public sealed class BoundedJournalTests
{
    private const long Cap = 8192;
    private static OverlayIdentity Identity(ISnapshotTree tree) =>
        new(2, "bounded-journal-tests", tree.Snapshot, 64L * 1048576, 65536);

    [Fact]
    public void CheckpointPreservesMetadataRangesRenamedLazyChildrenAndLiveUnlinkedIdentities()
    {
        using var root = new TestDirectory(); var tree = new SnapshotOverlayTests.FakeSnapshotTree();
        byte[] original = Bytes(8192, 1), replaced = Bytes(128, 2), deleted = Bytes(64, 3);
        byte[] retainedReplacement = replaced.ToArray(), retainedDeletion = deleted.ToArray();
        new byte[] { 55, 56 }.CopyTo(retainedReplacement, 17);
        new byte[] { 65, 66, 67 }.CopyTo(retainedDeletion, 8);
        tree.Files["Assets/base"] = original; tree.Files["Assets/replaced"] = replaced;
        tree.Files["Assets/deleted"] = deleted; tree.Files["Assets/lazy/deeper"] = [21, 22];
        var identity = Identity(tree); string delta = Path.Combine(root.Path, "delta");
        Guid file, oldTarget, removed, clock; DeltaEntry expectedEntry; long sequence;
        byte[] expected = original[..7000];
        new byte[] { 71, 72, 73 }.CopyTo(expected, 1023);
        byte[] patch = Bytes(4096, 4); patch.AsSpan(0, 904).CopyTo(expected.AsSpan(4096));
        Array.Clear(expected, 5000, 2000);
        using (var store = new JournalDeltaStore(delta, identity, tree, boundedLogBytes: Cap))
        {
            Assert.Equal("bounded", store.JournalStorageMode);
            file = store.Stat("Assets/base")!.Id; oldTarget = store.Stat("Assets/replaced")!.Id;
            removed = store.Stat("Assets/deleted")!.Id;
            store.Write(oldTarget, 17, [55, 56]); store.Write(removed, 8, [65, 66, 67]);
            store.Write(file, 1023, [71, 72, 73]); store.Write(file, 4096, patch);
            store.SetLength(file, 5000); store.SetLength(file, 7000);
            store.SetMetadata(file, 33, 11, 22, 33); store.SetUnixMode(file, 0x1ed);
            store.Create("Assets/new-dir", true, 16);
            Guid added = store.Create("Assets/new-dir/new", false, 32).Id; store.Write(added, 0, [41, 42]);
            store.Rename(file, "Assets/replaced", true); store.Delete(removed);
            store.Rename(store.Stat("Assets")!.Id, "Moved", false);
            clock = store.Create("clock", false, 32).Id;
            ForceCheckpoint(store, clock);
            // Repacking current private ranges must not hydrate the original.
            Assert.Equal(0, tree.ReadBytes);
            expectedEntry = store.Get(file);
            Assert.Equal("Moved/replaced", expectedEntry.Path);
            Assert.Equal(33u, expectedEntry.Attributes); Assert.Equal(0x1edu, expectedEntry.UnixMode);
            Assert.Equal((11L, 22L, 33L), (expectedEntry.CreationTime, expectedEntry.LastAccessTime, expectedEntry.LastWriteTime));
            Verify(store); sequence = store.Observe().DurableSequence;
        }
        using (var resumed = new JournalDeltaStore(delta, identity, tree))
        {
            Assert.Equal("bounded", resumed.JournalStorageMode); Assert.Equal(sequence, resumed.Observe().DurableSequence);
            Assert.Equal(expectedEntry, resumed.Get(file)); Verify(resumed);
            var changes = resumed.ObserveChanges([]);
            resumed.ReleaseUnlinkedFile(oldTarget); resumed.ReleaseUnlinkedFile(removed);
            Assert.Equal(sequence, resumed.Observe().DurableSequence);
            Assert.Equal(changes.Changes, resumed.ObserveChanges([]).Changes);
            Assert.Throws<FileNotFoundException>(() => resumed.Get(oldTarget));
            Assert.Throws<FileNotFoundException>(() => resumed.Get(removed));
            resumed.ReleaseUnlinkedFile(oldTarget);
        }
        using (var replayed = new JournalDeltaStore(delta, identity, tree))
        {
            Assert.Throws<FileNotFoundException>(() => replayed.Get(oldTarget));
            Assert.Throws<FileNotFoundException>(() => replayed.Get(removed));
            Assert.Equal(expected, Read(replayed, file));
        }
        Assert.Equal(original, tree.Files["Assets/base"]); Assert.Equal(replaced, tree.Files["Assets/replaced"]);
        Assert.Equal(deleted, tree.Files["Assets/deleted"]); Assert.Equal(new byte[] { 21, 22 }, tree.Files["Assets/lazy/deeper"]);

        void Verify(JournalDeltaStore store)
        {
            Assert.Null(store.Stat("Assets")); Assert.Null(store.Stat("Moved/base")); Assert.Null(store.Stat("Moved/deleted"));
            Assert.Equal(new[] { "Moved/lazy", "Moved/new-dir", "Moved/replaced" }, store.List("Moved").Select(e => e.Path));
            Assert.Equal(new byte[] { 21, 22 }, Read(store, store.Stat("Moved/lazy/deeper")!.Id));
            Assert.Equal(new byte[] { 41, 42 }, Read(store, store.Stat("Moved/new-dir/new")!.Id));
            Assert.Equal(expected, Read(store, file)); Assert.Equal(retainedReplacement, Read(store, oldTarget));
            Assert.Equal(retainedDeletion, Read(store, removed)); Assert.True(store.Get(oldTarget).Deleted);
            Assert.True(store.Get(removed).Deleted);
        }
    }

    [Fact]
    public void RepeatedOverwritesBoundHistoryAndKeepOneDurableFlushPerAcknowledgedMutation()
    {
        using var root = new TestDirectory(); var tree = new SnapshotOverlayTests.FakeSnapshotTree();
        var observer = new Interrupt(); var identity = Identity(tree); Guid id; byte[] expected = [];
        using (var store = new JournalDeltaStore(root.Path, identity, tree, observer, boundedLogBytes: Cap))
        {
            id = store.Create("current", false, 32).Id;
            long lastSequence = store.Observe().DurableSequence;
            for (int i = 0; i < 24; i++)
            {
                expected = Bytes(4096, 100 + i); long flushes = observer.FlushedMutations;
                store.Write(id, 0, expected);
                Assert.Equal(flushes + 1, observer.FlushedMutations);
                Assert.Equal(++lastSequence, store.Observe().DurableSequence);
                Assert.InRange(store.BoundedLogBytes, 0, Cap);
                Assert.InRange(store.BoundedLogFrames, 0, JournalDeltaStore.DefaultBoundedLogFrames);
            }
            Assert.True(store.BoundedCheckpointCount >= 2);
            Assert.True(store.Observe().JournalBytes < 32768, "Obsolete overwrite history must not grow with the 24 acknowledged writes.");
            Assert.Equal(0, tree.ReadBytes); Assert.Equal(expected, Read(store, id));
            Assert.Equal(0, store.Observe().InitialPayloadCopiedBytes); Assert.Equal(0, store.Observe().ReadCacheBytes);
        }
        using var replayed = new JournalDeltaStore(root.Path, identity, tree);
        Assert.Equal(expected, Read(replayed, id));
    }

    [Fact]
    public void MetadataOnlyTrafficAlsoCheckpointsAtTheFrameBound()
    {
        using var root = new TestDirectory(); var tree = new SnapshotOverlayTests.FakeSnapshotTree();
        using var store = new JournalDeltaStore(root.Path, Identity(tree), tree,
            boundedLogBytes: JournalDeltaStore.DefaultBoundedLogBytes);
        Guid id = store.Create("metadata", false, 32).Id;
        for (int i = 0; i <= JournalDeltaStore.DefaultBoundedLogFrames; i++)
            store.SetMetadata(id, 32, 1, 2, 100 + i);
        Assert.True(store.BoundedCheckpointCount >= 1);
        Assert.InRange(store.BoundedLogFrames, 0, 2);
        Assert.Equal(100L + JournalDeltaStore.DefaultBoundedLogFrames, store.Get(id).LastWriteTime);
    }

    [Fact]
    public async Task InFlightReadKeepsCapturedPrivateBytesWhileCheckpointChangesTheirPhysicalGeneration()
    {
        using var root = new TestDirectory(); var ct = TestContext.Current.CancellationToken;
        using var tree = new GatedTree(ct); byte[] original = Bytes(8192, 300);
        tree.Inner.Files["base"] = original;
        using var store = new JournalDeltaStore(root.Path, Identity(tree), tree, boundedLogBytes: Cap);
        Guid id = store.Stat("base")!.Id, clock = store.Create("clock", false, 32).Id;
        byte[] previous = Bytes(4096, 301), next = Bytes(4096, 302), expected = original.ToArray();
        store.Write(id, 0, previous); previous.CopyTo(expected, 0);
        tree.Arm(); Task<byte[]> reader = Task.Run(() => Read(store, id), ct);
        Task? writer = null;
        try
        {
            Assert.True(tree.Entered.Wait(TimeSpan.FromSeconds(5), ct), "The reader must be inside immutable base I/O.");
            writer = Task.Run(() => { ForceCheckpoint(store, clock); store.Write(id, 0, next); }, ct);
            await writer.WaitAsync(TimeSpan.FromSeconds(5), ct);
        }
        finally
        {
            tree.Continue.Set();
            await reader.WaitAsync(TimeSpan.FromSeconds(10), ct);
            if (writer is not null) await writer.WaitAsync(TimeSpan.FromSeconds(10), ct);
        }
        Assert.Equal(expected, await reader);
        next.CopyTo(expected, 0); Assert.Equal(expected, Read(store, id));
        Assert.Equal(original, tree.Inner.Files["base"]);
    }

    [Theory]
    [InlineData("bounded.checkpoint.intent")]
    [InlineData("bounded.checkpoint.candidate-flushed")]
    [InlineData("bounded.checkpoint.prepared")]
    [InlineData("bounded.checkpoint.published")]
    [InlineData("bounded.checkpoint.reopened")]
    [InlineData("bounded.checkpoint.previous-deleted")]
    [InlineData("bounded.checkpoint.completed")]
    public void InterruptedCheckpointPreservesEveryAcknowledgedWriteAndPoisonsTheFailedWriter(string boundary)
    {
        using var root = new TestDirectory(); var tree = new SnapshotOverlayTests.FakeSnapshotTree();
        for (int i = 0; i < 15; i++) tree.Files[$"lookup/item-{i:D2}"] = [(byte)i, 111];
        var identity = Identity(tree); var observer = new Interrupt(); Guid id; long acknowledgedSequence;
        byte[] acknowledged = Bytes(4096, 20);
        using (var store = new JournalDeltaStore(root.Path, identity, tree, observer, boundedLogBytes: Cap))
        {
            id = store.Create("file", false, 32).Id; store.Write(id, 0, acknowledged);
            Assert.Equal(15, store.List("lookup").Count);
            foreach (string path in tree.Files.Keys) Assert.NotNull(store.Stat(path));
            Assert.Equal(0, tree.ReadBytes);
            acknowledgedSequence = store.Observe().DurableSequence; observer.Boundary = boundary;
            bool interrupted = false;
            for (int i = 0; i < 8 && !interrupted; i++)
            {
                byte[] next = Bytes(4096, 21 + i);
                try { store.Write(id, 0, next); acknowledged = next; acknowledgedSequence = store.Observe().DurableSequence; }
                catch (IOException error) when (error.Message == Interrupt.Message) { interrupted = true; }
            }
            Assert.True(interrupted, "The requested checkpoint boundary must actually execute.");
            Assert.Equal("DeltaReconnectRequired", Assert.Throws<WorkspaceException>(() => store.Write(id, 0, [99])).Code);
        }
        SnapshotDeltaSeal recoveredSeal;
        using (var resumed = new JournalDeltaStore(root.Path, identity, tree))
        {
            Assert.Equal(acknowledgedSequence, resumed.Observe().DurableSequence);
            Assert.Equal(acknowledged, Read(resumed, id)); recoveredSeal = resumed.Seal();
            Assert.Equal(15, resumed.List("lookup").Count);
            foreach (var pair in tree.Files) Assert.Equal(pair.Value, Read(resumed, resumed.Stat(pair.Key)!.Id));
        }
        using (var second = new JournalDeltaStore(root.Path, identity, tree))
            Assert.Equal(recoveredSeal, second.Seal());
        Assert.False(File.Exists(Path.Combine(root.Path, "bounded-checkpoint.json")));
        Assert.False(File.Exists(Path.Combine(root.Path, "delta.journal.bounded-previous")));
        Assert.False(File.Exists(Path.Combine(root.Path, "delta.journal.bounded-next")));
    }

    [Fact]
    public void ReadOnlyOpenIdentitiesAndCachedRenamedChildrenSurviveCheckpointAndLaterPromotion()
    {
        using var root = new TestDirectory(); var tree = new SnapshotOverlayTests.FakeSnapshotTree(); var identity = Identity(tree);
        tree.Files["dir/cached"] = [1, 2, 3, 4]; tree.Files["dir/lazy"] = [5, 6]; tree.Files["observed"] = [7, 8];
        Guid cached, observed, clock;
        using (var store = new JournalDeltaStore(root.Path, identity, tree, boundedLogBytes: Cap))
        {
            cached = store.Stat("dir/cached")!.Id; observed = store.Stat("observed")!.Id;
            Assert.Equal(2, store.List("dir").Count); clock = store.Create("clock", false, 32).Id;
            ForceCheckpoint(store, clock);
            Assert.Equal("dir/cached", store.Get(cached).Path); Assert.Equal(new byte[] { 7, 8 }, Read(store, observed));
            store.Rename(store.Stat("dir")!.Id, "moved", false); ForceCheckpoint(store, clock);
            Assert.Equal("moved/cached", store.Get(cached).Path); Assert.Equal(new byte[] { 1, 2, 3, 4 }, Read(store, cached));
            store.SetMetadata(observed, 33, 101, 102, 103); ForceCheckpoint(store, clock);
        }
        using (var resumed = new JournalDeltaStore(root.Path, identity, tree))
        {
            Assert.Null(resumed.Stat("dir")); Assert.Equal(cached, resumed.Stat("moved/cached")!.Id);
            Assert.Equal(new byte[] { 1, 2, 3, 4 }, Read(resumed, cached));
            Assert.Equal(new byte[] { 5, 6 }, Read(resumed, resumed.Stat("moved/lazy")!.Id));
            Assert.Equal(observed, resumed.Stat("observed")!.Id);
            Assert.Equal((33u, 101L, 102L, 103L), (resumed.Get(observed).Attributes, resumed.Get(observed).CreationTime,
                resumed.Get(observed).LastAccessTime, resumed.Get(observed).LastWriteTime));
            resumed.Write(cached, 1, [99]); ForceCheckpoint(resumed, clock);
        }
        using var verified = new JournalDeltaStore(root.Path, identity, tree);
        Assert.Equal(cached, verified.Stat("moved/cached")!.Id); Assert.Equal(new byte[] { 1, 99, 3, 4 }, Read(verified, cached));
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, tree.Files["dir/cached"]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ImmutableReplacementTargetClosesWithoutAFrameOrPromotesItsLaterPrivateWrite(bool writeAfterUnlink)
    {
        using var root = new TestDirectory(); var tree = new SnapshotOverlayTests.FakeSnapshotTree(); var identity = Identity(tree);
        byte[] original = Bytes(256, 310), retained = original.ToArray(); tree.Files["target"] = original;
        var observer = new Interrupt(); Guid oldTarget, replacement;
        using (var store = new JournalDeltaStore(root.Path, identity, tree, observer, boundedLogBytes: Cap))
        {
            oldTarget = store.Stat("target")!.Id;
            replacement = store.Create("incoming", false, 32).Id; store.Write(replacement, 0, [21, 22]);
            store.Rename(replacement, "target", true); Guid clock = store.Create("clock", false, 32).Id;
            ForceCheckpoint(store, clock);
            Assert.True(store.Get(oldTarget).Deleted); Assert.Equal(original, Read(store, oldTarget));
            if (writeAfterUnlink)
            {
                store.Write(oldTarget, 17, [77, 78]); retained[17] = 77; retained[18] = 78;
                ForceCheckpoint(store, clock); Assert.Equal(retained, Read(store, oldTarget));
            }
            long sequence = store.Observe().DurableSequence, flushes = observer.FlushedMutations;
            var before = store.Seal(); store.ReleaseUnlinkedFile(oldTarget);
            Assert.Equal(sequence, store.Observe().DurableSequence);
            Assert.Equal(flushes + (writeAfterUnlink ? 1 : 0), observer.FlushedMutations);
            if (!writeAfterUnlink) Assert.Equal(before, store.Seal());
            Assert.Throws<FileNotFoundException>(() => store.Get(oldTarget));
            Assert.Equal(replacement, store.Stat("target")!.Id); Assert.Equal(new byte[] { 21, 22 }, Read(store, replacement));
        }
        using var resumed = new JournalDeltaStore(root.Path, identity, tree);
        Assert.Throws<FileNotFoundException>(() => resumed.Get(oldTarget));
        Assert.Equal(replacement, resumed.Stat("target")!.Id); Assert.Equal(new byte[] { 21, 22 }, Read(resumed, replacement));
        Assert.Equal(original, tree.Files["target"]);
    }

    [Theory]
    [InlineData("bounded.initialize.intent-flushed")]
    [InlineData("bounded.initialize.identity")]
    [InlineData("bounded.initialize.published")]
    public void InitializationRetryRetainsTheSavedBoundAndOwner(string boundary)
    {
        using var root = new TestDirectory(); var tree = new SnapshotOverlayTests.FakeSnapshotTree(); var identity = Identity(tree);
        Assert.Throws<IOException>(() => new JournalDeltaStore(root.Path, identity, tree,
            new Interrupt { Boundary = boundary }, boundedLogBytes: Cap));
        using var resumed = new JournalDeltaStore(root.Path, identity, tree, optimizeJournal: false, boundedLogBytes: 16384);
        Assert.Equal("bounded", resumed.JournalStorageMode); Assert.Equal(Cap, resumed.BoundedLogByteLimit);
        Assert.Equal(0, resumed.Observe().DurableSequence);
        Guid id = resumed.Create("file", false, 32).Id; resumed.Write(id, 0, [1, 2, 3]);
        Assert.Equal(new byte[] { 1, 2, 3 }, Read(resumed, id));
    }

    [Theory]
    [InlineData("commit.after-header")]
    [InlineData("commit.after-metadata")]
    [InlineData("commit.after-payload-prefix")]
    public void IncompleteAppendKeepsTheAcknowledgedPrefixAndRequiresReconnect(string boundary)
    {
        using var root = new TestDirectory(); var tree = new SnapshotOverlayTests.FakeSnapshotTree(); var identity = Identity(tree);
        var observer = new Interrupt(); Guid id; byte[] expected = Bytes(4096, 30); long sequence;
        using (var store = new JournalDeltaStore(root.Path, identity, tree, observer, boundedLogBytes: 65536))
        {
            id = store.Create("file", false, 32).Id; store.Write(id, 0, expected); sequence = store.Observe().DurableSequence;
            observer.Boundary = boundary;
            Assert.Throws<IOException>(() => store.Write(id, 0, Bytes(4096, 31)));
            Assert.Equal("DeltaReconnectRequired", Assert.Throws<WorkspaceException>(() => store.Read(id, 0, new byte[1])).Code);
        }
        var incomplete = Inventory(root.Path);
        Assert.Equal("CandidateIncomplete", Assert.Throws<WorkspaceException>(() =>
            JournalDeltaStore.OpenReadOnly(root.Path, identity, tree)).Code);
        AssertInventory(incomplete, root.Path);
        using var resumed = new JournalDeltaStore(root.Path, identity, tree);
        Assert.Equal(sequence, resumed.Observe().DurableSequence); Assert.Equal(expected, Read(resumed, id));
    }

    [Fact]
    public void CompleteFrameCorruptionIsPreservedAndRejectedInsteadOfTruncated()
    {
        using var root = new TestDirectory(); var tree = new SnapshotOverlayTests.FakeSnapshotTree(); var identity = Identity(tree);
        using (var store = new JournalDeltaStore(root.Path, identity, tree, boundedLogBytes: Cap))
            store.Write(store.Create("file", false, 32).Id, 0, Bytes(4096, 40));
        string journal = Path.Combine(root.Path, "delta.journal"); byte[] bytes = File.ReadAllBytes(journal);
        bytes[^1] ^= 1; File.WriteAllBytes(journal, bytes); var before = Inventory(root.Path);
        Assert.Equal("DeltaCorrupt", Assert.Throws<WorkspaceException>(() => new JournalDeltaStore(root.Path, identity, tree)).Code);
        AssertInventory(before, root.Path);
    }

    [Theory]
    [InlineData("owner")]
    [InlineData("candidate")]
    [InlineData("previous")]
    public void ChangedCheckpointRecoveryInputsArePreservedAndRejected(string damage)
    {
        using var root = new TestDirectory(); var tree = new SnapshotOverlayTests.FakeSnapshotTree(); var identity = Identity(tree);
        var observer = new Interrupt();
        using (var store = new JournalDeltaStore(root.Path, identity, tree, observer, boundedLogBytes: Cap))
        {
            Guid id = store.Create("file", false, 32).Id; store.Write(id, 0, Bytes(4096, 50));
            observer.Boundary = damage == "previous" ? "bounded.checkpoint.published" : "bounded.checkpoint.prepared";
            Assert.Throws<IOException>(() => ForceCheckpoint(store, id));
        }
        if (damage == "owner")
        {
            string intent = Path.Combine(root.Path, "bounded-checkpoint.json");
            var json = JsonNode.Parse(File.ReadAllText(intent))!; json["Identity"]!["Id"] = "foreign-owner";
            File.WriteAllText(intent, json.ToJsonString());
        }
        else
        {
            string path = Path.Combine(root.Path, damage == "candidate" ? "delta.journal.bounded-next" : "delta.journal.bounded-previous");
            byte[] bytes = File.ReadAllBytes(path); bytes[^1] ^= 1; File.WriteAllBytes(path, bytes);
        }
        var before = Inventory(root.Path);
        Assert.Throws<WorkspaceException>(() => new JournalDeltaStore(root.Path, identity, tree));
        AssertInventory(before, root.Path);
    }

    [Fact]
    public void ForeignIdentityAndUnownedCheckpointFileCannotBeAdopted()
    {
        using var root = new TestDirectory(); var tree = new SnapshotOverlayTests.FakeSnapshotTree(); var identity = Identity(tree);
        using (var store = new JournalDeltaStore(root.Path, identity, tree, boundedLogBytes: Cap))
            store.Write(store.Create("file", false, 32).Id, 0, [1, 2, 3]);
        var before = Inventory(root.Path);
        Assert.Equal("OverlayIdentityMismatch", Assert.Throws<WorkspaceException>(() =>
            new JournalDeltaStore(root.Path, identity with { Id = "peer" }, tree)).Code);
        AssertInventory(before, root.Path);
        File.WriteAllText(Path.Combine(root.Path, "delta.journal.bounded-next"), "foreign staging file");
        before = Inventory(root.Path);
        Assert.Throws<WorkspaceException>(() => new JournalDeltaStore(root.Path, identity, tree));
        AssertInventory(before, root.Path);
    }

    [Theory]
    [InlineData("checksum")]
    [InlineData("owner")]
    public void UnpreparedCandidateCorruptionAndForeignOwnerAreNotDeletedDuringRecovery(string damage)
    {
        using var root = new TestDirectory(); var tree = new SnapshotOverlayTests.FakeSnapshotTree(); var identity = Identity(tree);
        var observer = new Interrupt();
        using (var store = new JournalDeltaStore(root.Path, identity, tree, observer, boundedLogBytes: Cap))
        {
            Guid id = store.Create("file", false, 32).Id; store.Write(id, 0, Bytes(4096, 70));
            observer.Boundary = "bounded.checkpoint.candidate-flushed";
            Assert.Throws<IOException>(() => ForceCheckpoint(store, id));
        }
        string candidate = Path.Combine(root.Path, "delta.journal.bounded-next");
        byte[] bytes = File.ReadAllBytes(candidate);
        if (damage == "checksum") bytes[^1] ^= 1;
        else
        {
            int metadataLength = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(4));
            var metadata = JsonNode.Parse(bytes.AsSpan(JournalDeltaStore.FrameHeaderBytes, metadataLength))!;
            byte[] owner = Encoding.UTF8.GetBytes(metadata["Bounded"]!["IdentityHash"]!.GetValue<string>());
            int offset = bytes.AsSpan(JournalDeltaStore.FrameHeaderBytes, metadataLength).IndexOf(owner);
            Assert.True(offset >= 0); offset += JournalDeltaStore.FrameHeaderBytes;
            bytes[offset] = bytes[offset] == (byte)'0' ? (byte)'1' : (byte)'0';
            int checksumOffset = JournalDeltaStore.FrameHeaderBytes + metadataLength;
            SHA256.HashData(bytes.AsSpan(0, checksumOffset), bytes.AsSpan(checksumOffset, 32));
        }
        File.WriteAllBytes(candidate, bytes); var before = Inventory(root.Path);
        Assert.Throws<WorkspaceException>(() => new JournalDeltaStore(root.Path, identity, tree));
        AssertInventory(before, root.Path);
    }

    [Fact]
    public void MatchingIncompleteCandidateCanBeReclaimedWithoutLosingAcknowledgedData()
    {
        using var root = new TestDirectory(); var tree = new SnapshotOverlayTests.FakeSnapshotTree(); var identity = Identity(tree);
        var observer = new Interrupt(); byte[] expected = Bytes(4096, 80); Guid id; long sequence;
        using (var store = new JournalDeltaStore(root.Path, identity, tree, observer, boundedLogBytes: Cap))
        {
            id = store.Create("file", false, 32).Id; store.Write(id, 0, expected); sequence = store.Observe().DurableSequence;
            observer.Boundary = "bounded.checkpoint.candidate-flushed";
            Assert.Throws<IOException>(() => store.Write(id, 0, Bytes(4096, 81)));
        }
        string candidate = Path.Combine(root.Path, "delta.journal.bounded-next");
        using (var partial = new FileStream(candidate, FileMode.Open, FileAccess.Write, FileShare.Read))
        { partial.SetLength(partial.Length / 2); partial.Flush(true); }
        using var resumed = new JournalDeltaStore(root.Path, identity, tree);
        Assert.Equal(sequence, resumed.Observe().DurableSequence); Assert.Equal(expected, Read(resumed, id));
        Assert.False(File.Exists(candidate));
    }

    [Theory]
    [InlineData(null, "bounded", false)]
    [InlineData(false, "append", false)]
    [InlineData(true, "latest", false)]
    [InlineData(null, "bounded", true)]
    [InlineData(false, "append", true)]
    [InlineData(true, "latest", true)]
    public void CompactRetainsPersistedBoundUnlessAnExplicitFormatChangeWasRequested(bool? choice, string mode, bool providerWhileCompleting)
    {
        using var root = new TestDirectory(); var tree = new SnapshotOverlayTests.FakeSnapshotTree(); var identity = Identity(tree);
        byte[] expected = Bytes(4096, 90); Guid id;
        using (var store = new JournalDeltaStore(root.Path, identity, tree, boundedLogBytes: Cap))
        { id = store.Create("file", false, 32).Id; store.Write(id, 0, expected); }
        var compacted = JournalDeltaStore.CompactDirectory(root.Path, identity, tree, revertBaseRanges: choice);
        string journal = Path.Combine(root.Path, "delta.journal"); var published = Inventory(root.Path);
        Assert.Contains(journal, JournalDeltaStore.JournalStorageFiles(journal));
        Assert.Equal(compacted.After, JournalDeltaStore.SealJournal(journal, compacted.After.DurableSequence));
        AssertInventory(published, root.Path);
        if (!providerWhileCompleting) JournalDeltaStore.CompleteCompaction(root.Path, identity);
        using var resumed = new JournalDeltaStore(root.Path, identity, tree, optimizeJournal: false, boundedLogBytes: 16384);
        Assert.Equal(mode, resumed.JournalStorageMode); Assert.Equal(expected, Read(resumed, id));
        Assert.Equal(choice is null ? Cap : (long?)null, resumed.BoundedLogByteLimit);
        // The product reconnects its provider before reclaiming the previous
        // generation. Keep that writer open across CompleteCompaction.
        if (providerWhileCompleting) JournalDeltaStore.CompleteCompaction(root.Path, identity);
        resumed.Write(id, 127, [77, 78]); expected[127] = 77; expected[128] = 78;
        Assert.Equal(expected, Read(resumed, id));
    }

    [Fact]
    public void InterruptedExplicitConversionCleanupResumesAfterRetiringTheBoundedGuard()
    {
        using var root = new TestDirectory(); var tree = new SnapshotOverlayTests.FakeSnapshotTree(); var identity = Identity(tree);
        byte[] expected = Bytes(4096, 95); Guid id;
        using (var store = new JournalDeltaStore(root.Path, identity, tree, boundedLogBytes: Cap))
        { id = store.Create("file", false, 32).Id; store.Write(id, 0, expected); }
        var compacted = JournalDeltaStore.CompactDirectory(root.Path, identity, tree, revertBaseRanges: false);
        Assert.Throws<IOException>(() => JournalDeltaStore.CompleteCompaction(root.Path, identity, at =>
        { if (at == "before-previous-delete") throw new IOException(Interrupt.Message); }));
        Assert.False(File.Exists(Path.Combine(root.Path, "delta.journal.bounded.lock")));
        Assert.Equal(compacted.After, JournalDeltaStore.SealJournal(Path.Combine(root.Path, "delta.journal"), compacted.After.DurableSequence));
        JournalDeltaStore.CompleteCompaction(root.Path, identity);
        using var resumed = new JournalDeltaStore(root.Path, identity, tree);
        Assert.Equal("append", resumed.JournalStorageMode); Assert.Equal(expected, Read(resumed, id));
    }

    [Fact]
    public void ReadOnlySealAndInventoryStayStableAndCandidateSurvivesSourceJournalTeardown()
    {
        using var root = new TestDirectory(); var tree = new SnapshotOverlayTests.FakeSnapshotTree();
        byte[] original = Bytes(8192, 60); tree.Files["source"] = original;
        var identity = Identity(tree); string delta = Path.Combine(root.Path, "delta"), candidate = Path.Combine(root.Path, "candidate");
        byte[] expected = original.ToArray(); new byte[] { 91, 92, 93 }.CopyTo(expected, 123);
        SnapshotDeltaSeal seal;
        using (var store = new JournalDeltaStore(delta, identity, tree, boundedLogBytes: Cap))
        {
            store.Write(store.Stat("source")!.Id, 123, [91, 92, 93]);
            ForceCheckpoint(store, store.Create("clock", false, 32).Id);
            store.SetMetadata(store.Stat("source")!.Id, 33, 101, 102, 103); seal = store.Seal();
        }
        var before = Inventory(delta);
        using (var reader = JournalDeltaStore.OpenReadOnly(delta, identity, tree))
        {
            Assert.Equal("bounded", reader.JournalStorageMode); Assert.Equal(seal, reader.Seal());
            Assert.Equal(expected, Read(reader, reader.Stat("source")!.Id));
            reader.Flush(); Assert.Equal(seal, reader.Seal());
            Assert.Equal("CandidateReadOnly", Assert.Throws<WorkspaceException>(() =>
                reader.Write(reader.Stat("source")!.Id, 0, [0])).Code);
        }
        AssertInventory(before, delta);
        string journal = Path.Combine(delta, "delta.journal");
        string[] storage = JournalDeltaStore.JournalStorageFiles(journal).Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(new[] { journal, journal + ".bounded.lock" }.Order(StringComparer.Ordinal), storage);
        Assert.Equal(0, new FileInfo(journal + ".bounded.lock").Length);
        Assert.Equal(seal, JournalDeltaStore.SealJournal(journal, seal.DurableSequence));
        var published = JournalDeltaStore.ExportCandidate(delta, identity, tree, candidate, "independent-bounded", []);
        Assert.Equal(seal, published.SourceSeal); AssertInventory(before, delta);
        // Every deleted path is from the exact sealed owned inventory, not a recursive sweep.
        foreach (string path in storage) File.Delete(path);
        File.Delete(Path.Combine(delta, "identity.json")); Directory.Delete(delta, false);
        using var independent = new CandidateSnapshotTree(candidate, tree);
        byte[] actual = new byte[expected.Length]; Assert.Equal(actual.Length, independent.Read("source", 0, actual));
        Assert.Equal(expected, actual); Assert.Equal(33u, independent.Stat("source")!.Attributes);
        Assert.Equal(103, independent.Stat("source")!.LastWriteTime); Assert.Equal(original, tree.Files["source"]);
    }

    private static void ForceCheckpoint(JournalDeltaStore store, Guid id)
    {
        long before = store.BoundedCheckpointCount;
        for (int i = 0; i < 16 && store.BoundedCheckpointCount == before; i++) store.Write(id, 0, Bytes(4096, 200 + i));
        Assert.True(store.BoundedCheckpointCount > before, "The fixture must actually cross the bounded log threshold.");
    }

    private static byte[] Read(JournalDeltaStore store, Guid id)
    {
        byte[] bytes = new byte[checked((int)store.Get(id).Length)];
        Assert.Equal(bytes.Length, store.Read(id, 0, bytes)); return bytes;
    }

    private static byte[] Bytes(int count, int seed)
    { var bytes = new byte[count]; new Random(seed).NextBytes(bytes); return bytes; }

    private static SortedDictionary<string, string> Inventory(string directory) => new(
        Directory.GetFiles(directory, "*", SearchOption.AllDirectories).ToDictionary(
            path => Path.GetRelativePath(directory, path), path => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)))), StringComparer.Ordinal);

    private static void AssertInventory(SortedDictionary<string, string> expected, string directory)
    {
        var actual = Inventory(directory); Assert.Equal(expected.Keys, actual.Keys);
        foreach (var pair in expected) Assert.Equal(pair.Value, actual[pair.Key]);
    }

    private sealed class Interrupt : IJournalMutationObserver
    {
        public const string Message = "Owned bounded journal interruption.";
        public string? Boundary;
        public long FlushedMutations;
        public void Observe(JournalMutationBoundary at)
        {
            if (at.Boundary == "commit.after-flush") FlushedMutations++;
            if (at.Boundary == Boundary) throw new IOException(Message);
        }
    }

    private sealed class GatedTree(CancellationToken cancellationToken) : ISnapshotTree, IDisposable
    {
        public SnapshotOverlayTests.FakeSnapshotTree Inner { get; } = new();
        public ManualResetEventSlim Entered { get; } = new();
        public ManualResetEventSlim Continue { get; } = new();
        private int armed;
        public SnapshotReference Snapshot => Inner.Snapshot;
        public bool SupportsConcurrentReads => true;
        public void Arm() => Interlocked.Exchange(ref armed, 1);
        public void EnsureAvailable() => Inner.EnsureAvailable();
        public SnapshotEntry? Stat(string path) => Inner.Stat(path);
        public IReadOnlyList<SnapshotEntry> List(string path) => Inner.List(path);
        public int Read(string path, long offset, Span<byte> destination)
        {
            if (Interlocked.Exchange(ref armed, 0) == 1)
            {
                Entered.Set();
                if (!Continue.Wait(TimeSpan.FromSeconds(15), cancellationToken))
                    throw new TimeoutException("The owned immutable-read gate was not released.");
            }
            return Inner.Read(path, offset, destination);
        }
        public void Dispose() { Entered.Dispose(); Continue.Dispose(); }
    }
}
