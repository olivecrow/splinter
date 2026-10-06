using System.Security.Cryptography;
using Splinter.Core;
using Splinter.Runtime;

namespace Splinter.Tests;

public sealed class JournalLatestOnlyTests
{
    private static OverlayIdentity Identity(ISnapshotTree tree) => new(2, "latest-only", tree.Snapshot, 64L * 1048576, 1048576);
    private static void Enable(string directory, OverlayIdentity identity, ISnapshotTree tree)
    {
        using var store = new JournalDeltaStore(directory, identity, tree);
        Assert.True(store.JournalOptimizationEnabled);
    }
    [Fact]
    public void ManyFilesKeepOnlyCurrentPayloadAfterEveryAcknowledgedWriteAndReconnect()
    {
        using var root = new TestDirectory(); var tree = new SnapshotOverlayTests.FakeSnapshotTree();
        var identity = Identity(tree); Enable(root.Path, identity, tree);
        byte[][] current = Enumerable.Range(0, 16).Select(_ => RandomNumberGenerator.GetBytes(16384)).ToArray();
        SourceChangeView changes; SnapshotDeltaSeal seal;
        using (var writer = new JournalDeltaStore(root.Path, identity, tree))
        {
            Assert.True(writer.Observe().JournalOptimizationEnabled);
            Guid[] ids = Enumerable.Range(0, 16).Select(i => writer.Create($"file-{i}", false, 32).Id).ToArray();
            for (int cycle = 0; cycle < 12; cycle++)
            for (int i = 0; i < ids.Length; i++)
            {
                RandomNumberGenerator.Fill(current[i]); writer.Write(ids[i], 0, current[i]);
                Assert.Equal(Math.Min(16, cycle * 16 + i + 1) * 16384, writer.Observe().PayloadBytes);
                Assert.DoesNotContain(Directory.EnumerateFiles(root.Path, "*", SearchOption.AllDirectories), p => p.EndsWith(".pending", StringComparison.Ordinal));
            }
            changes = writer.ObserveChanges([]); seal = writer.Seal();
            Assert.Equal(16 * 16384, writer.Observe().PayloadBytes);
            Assert.Equal(seal.JournalBytes, writer.Observe().JournalBytes);
        }
        using var resumed = new JournalDeltaStore(root.Path, identity, tree);
        Assert.True(resumed.JournalOptimizationEnabled); Assert.Equal(seal, resumed.Seal());
        Assert.Equal(changes.Changes, resumed.ObserveChanges([]).Changes);
        for (int i = 0; i < current.Length; i++)
        { byte[] actual = new byte[16384]; resumed.Read(resumed.Stat($"file-{i}")!.Id, 0, actual); Assert.Equal(current[i], actual); }
        Assert.Equal(0, tree.ReadBytes);
    }
    [Fact]
    public void EachPayloadAndMetadataMutationHasOneDurableFlush()
    {
        using var root = new TestDirectory(); var tree = new SnapshotOverlayTests.FakeSnapshotTree();
        var identity = Identity(tree); Enable(root.Path, identity, tree);
        using var writer = new JournalDeltaStore(root.Path, identity, tree);
        long count = writer.Observe().Metrics!.FlushCount;
        Guid id = writer.Create("file", false, 32).Id;
        Assert.Equal(++count, writer.Observe().Metrics!.FlushCount);
        writer.Write(id, 0, RandomNumberGenerator.GetBytes(131072));
        Assert.Equal(++count, writer.Observe().Metrics!.FlushCount);
        writer.SetMetadata(id, 33, 11, 22, 33);
        Assert.Equal(++count, writer.Observe().Metrics!.FlushCount);
        writer.Write(id, 0, RandomNumberGenerator.GetBytes(131072));
        Assert.Equal(++count, writer.Observe().Metrics!.FlushCount);
    }
    [Fact]
    public void PartialRetirementMovesOnlyStillCurrentPrivateBlocksOfTheCapsule()
    {
        using var root = new TestDirectory(); var tree = new SnapshotOverlayTests.FakeSnapshotTree();
        var identity = Identity(tree); Enable(root.Path, identity, tree);
        var observer = new Failure();
        byte[] expected = RandomNumberGenerator.GetBytes(1572864); Guid id;
        using (var writer = new JournalDeltaStore(root.Path, identity, tree, observer))
        {
            id = writer.Create("file", false, 32).Id; writer.Write(id, 0, expected.AsSpan(0, 1048576));
            writer.Write(id, 524288, expected.AsSpan(524288, 1048576));
            for (int i = 0; i < 8; i++)
            {
                RandomNumberGenerator.Fill(expected.AsSpan(0, 524288)); writer.Write(id, 0, expected.AsSpan(0, 524288));
                Assert.Equal(expected.Length, writer.Observe().PayloadBytes);
                Assert.InRange(writer.Observe().JournalBytes, expected.Length, expected.Length + 32768L);
                Assert.Equal(writer.Observe().JournalBytes, observer.LastAckBytes);
            }
        }
        using var resumed = new JournalDeltaStore(root.Path, identity, tree, observer);
        RandomNumberGenerator.Fill(expected.AsSpan(0, 524288)); resumed.Write(id, 0, expected.AsSpan(0, 524288));
        Assert.Equal(resumed.Observe().JournalBytes, observer.LastAckBytes);
        byte[] actual = new byte[expected.Length];
        resumed.Read(id, 0, actual.AsSpan(0, 1048576)); resumed.Read(id, 1048576, actual.AsSpan(1048576));
        Assert.Equal(expected, actual); Assert.Equal(0, tree.ReadBytes);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PartialReplacementReclaimsOldBlockAndKeepsPrivateSlicesWithoutReadingTheBase(bool compressed)
    {
        using var root = new TestDirectory(); var tree = new SnapshotOverlayTests.FakeSnapshotTree();
        var identity = Identity(tree); Enable(root.Path, identity, tree);
        Guid id; byte[] expected = compressed ? new byte[65536] : RandomNumberGenerator.GetBytes(65536);
        using (var writer = new JournalDeltaStore(root.Path, identity, tree))
        {
            id = writer.Create("file", false, 32).Id; writer.Write(id, 0, expected);
            for (int i = 0; i < 64; i++)
            {
                expected[i * 811] ^= 0x55; writer.Write(id, i * 811, expected.AsSpan(i * 811, 1));
                if (compressed) Assert.True(writer.Observe().PayloadBytes < 65536);
                else Assert.Equal(65536, writer.Observe().PayloadBytes);
                Assert.DoesNotContain(JournalDeltaStore.JournalStorageFiles(Path.Combine(root.Path, "delta.journal")), p => p.EndsWith(".bin", StringComparison.Ordinal));
            }
            writer.SetLength(id, 32768); expected = expected[..32768];
            if (compressed) Assert.True(writer.Observe().PayloadBytes < expected.Length);
            else Assert.Equal(expected.Length, writer.Observe().PayloadBytes);
        }
        using var reader = JournalDeltaStore.OpenReadOnly(root.Path, identity, tree);
        var seal = reader.Seal(); reader.PrepareRestoreReads(seal);
        byte[] actual = new byte[expected.Length]; reader.ReadRestoreRange(id, 0, actual, seal);
        Assert.Equal(expected, actual); Assert.Equal(0, tree.ReadBytes);
    }
    [Fact]
    public void RenameWhiteoutsMetadataAndCurrentStateExportSurviveExplicitOffAndOn()
    {
        using var root = new TestDirectory(); using var candidate = new TestDirectory();
        var tree = new SnapshotOverlayTests.FakeSnapshotTree(); tree.Files["base"] = new byte[8192];
        var identity = Identity(tree); Enable(root.Path, identity, tree);
        DeltaEntry expected; SourceChangeView changes;
        using (var writer = new JournalDeltaStore(root.Path, identity, tree))
        {
            writer.Create("dir", true, 16); Guid id = writer.Create("dir/file", false, 32).Id;
            writer.Write(id, 0, [1, 2, 3]); writer.Rename(writer.Stat("dir")!.Id, "moved", false);
            writer.Delete(writer.Stat("base")!.Id); writer.SetMetadata(id, 33, 11, 22, 33);
            expected = writer.Get(id); changes = writer.ObserveChanges([]);
        }
        string destination = Path.Combine(candidate.Path, "export");
        var published = JournalDeltaStore.ExportCandidate(root.Path, identity, tree, destination, "latest-export", []);
        using (var export = JournalDeltaStore.OpenReadOnly(destination, identity, tree))
        { Assert.Equal(expected, export.Stat("moved/file")); Assert.Equal(changes.Changes, export.ObserveChanges([]).Changes); Assert.Equal(published.Seal, export.Seal()); }
        var off = JournalDeltaStore.CompactDirectory(root.Path, identity, tree, revertBaseRanges: false);
        Assert.False(off.JournalOptimizationEnabled); JournalDeltaStore.CompleteCompaction(root.Path, identity);
        using (var writer = new JournalDeltaStore(root.Path, identity, tree))
        {
            Assert.False(writer.JournalOptimizationEnabled); Assert.Null(writer.Stat("base")); Assert.Null(writer.Stat("dir"));
            Assert.Equal(expected, writer.Stat("moved/file")); Assert.Equal(changes.Changes, writer.ObserveChanges([]).Changes);
            writer.Write(expected.Id, 1, [9]);
        }
        var on = JournalDeltaStore.CompactDirectory(root.Path, identity, tree, revertBaseRanges: true);
        Assert.True(on.JournalOptimizationEnabled); JournalDeltaStore.CompleteCompaction(root.Path, identity);
        using var resumed = new JournalDeltaStore(root.Path, identity, tree);
        byte[] actual = new byte[3]; resumed.Read(expected.Id, 0, actual); Assert.Equal(new byte[] { 1, 9, 3 }, actual);
    }
    public static TheoryData<string, bool> RecoveryBoundaries => new()
    {
        { "latest.before-intent", false }, { "latest.after-intent-header", false }, { "latest.after-intent-prefix", false },
        { "latest.after-intent-write", false }, { "latest.before-intent-flush", true }, { "latest.after-intent-flush", true },
        { "latest.after-block-header", false }, { "latest.after-block-prefix", false },
        { "latest.before-block-flush", true }, { "latest.after-block-flush", true }, { "latest.after-staging", true },
        { "latest.after-object-publication", true },
        { "latest.before-root-publication", true }, { "latest.after-root-publication", true },
        { "latest.after-object-reclamation", true }, { "latest.after-intent-reclamation", true }, { "latest.after-ack-ready", true }
    };
    [Theory]
    [MemberData(nameof(RecoveryBoundaries))]
    public void InterruptedWriteKeepsAcknowledgedBOrCompletesDurableCThenReclaimsB(string boundary, bool completed)
    {
        using var root = new TestDirectory(); var tree = new SnapshotOverlayTests.FakeSnapshotTree();
        var identity = Identity(tree); Enable(root.Path, identity, tree); var observer = new Failure();
        Guid id; byte[] b = RandomNumberGenerator.GetBytes(8192), c = RandomNumberGenerator.GetBytes(8192);
        using (var writer = new JournalDeltaStore(root.Path, identity, tree, observer))
        {
            id = writer.Create("file", false, 32).Id; writer.Write(id, 0, b); observer.Boundary = boundary;
            Assert.Throws<IOException>(() => writer.Write(id, 0, c));
            Assert.Equal("DeltaReconnectRequired", Assert.Throws<WorkspaceException>(() => writer.Write(id, 0, b)).Code);
        }
        using var resumed = new JournalDeltaStore(root.Path, identity, tree);
        byte[] actual = new byte[8192]; resumed.Read(id, 0, actual); Assert.Equal(completed ? c : b, actual);
        Assert.Equal(8192, resumed.Observe().PayloadBytes);
        Assert.DoesNotContain(JournalDeltaStore.JournalStorageFiles(Path.Combine(root.Path, "delta.journal")), p => p.EndsWith(".bin", StringComparison.Ordinal));
        Assert.Equal(completed ? 3 : 2, resumed.Observe().DurableSequence);
    }
    [Fact]
    public void SingleCapsuleContainsPayloadOnceAndCompletesC()
    {
        using var root = new TestDirectory(); var tree = new SnapshotOverlayTests.FakeSnapshotTree();
        var identity = Identity(tree); Enable(root.Path, identity, tree); var observer = new Failure(); Guid id;
        byte[] b = RandomNumberGenerator.GetBytes(524288), c = RandomNumberGenerator.GetBytes(524288);
        using (var writer = new JournalDeltaStore(root.Path, identity, tree, observer))
        {
            id = writer.Create("file", false, 32).Id; writer.Write(id, 0, b); observer.Boundary = "latest.after-intent-flush";
            Assert.Throws<IOException>(() => writer.Write(id, 0, c));
        }
        string pending = Directory.EnumerateFiles(root.Path, "transaction.pending", SearchOption.AllDirectories).Single();
        Assert.InRange(new FileInfo(pending).Length, c.Length, c.Length + 8192);
        using var resumed = new JournalDeltaStore(root.Path, identity, tree);
        byte[] actual = new byte[c.Length]; resumed.Read(id, 0, actual); Assert.Equal(c, actual);
        Assert.Equal(3, resumed.Observe().DurableSequence); Assert.Equal(c.Length, resumed.Observe().PayloadBytes);
        Assert.DoesNotContain(Directory.EnumerateFiles(root.Path, "*", SearchOption.AllDirectories),
            p => p.EndsWith(".pending", StringComparison.Ordinal) || p.EndsWith(".new", StringComparison.Ordinal));
    }
    [Fact]
    public void CompleteDamagedStagedPayloadIsPreservedRatherThanRolledBack()
    {
        using var root = new TestDirectory(); var tree = new SnapshotOverlayTests.FakeSnapshotTree();
        var identity = Identity(tree); Enable(root.Path, identity, tree); var observer = new Failure();
        using (var writer = new JournalDeltaStore(root.Path, identity, tree, observer))
        {
            Guid id = writer.Create("file", false, 32).Id; writer.Write(id, 0, RandomNumberGenerator.GetBytes(8192));
            observer.Boundary = "latest.after-staging";
            Assert.Throws<IOException>(() => writer.Write(id, 0, RandomNumberGenerator.GetBytes(8192)));
        }
        string path = Directory.EnumerateFiles(root.Path, "transaction.pending", SearchOption.AllDirectories).Single();
        byte[] bytes = File.ReadAllBytes(path); bytes[^1] ^= 1; File.WriteAllBytes(path, bytes);
        Assert.Equal("DeltaCorrupt", Assert.Throws<WorkspaceException>(() => new JournalDeltaStore(root.Path, identity, tree)).Code);
        Assert.Equal(bytes, File.ReadAllBytes(path));
    }
    [Fact]
    public void PublishedRootWithMissingCIsPreservedAndRefusesRollbackToB()
    {
        using var root = new TestDirectory(); var tree = new SnapshotOverlayTests.FakeSnapshotTree();
        var identity = Identity(tree); Enable(root.Path, identity, tree); var observer = new Failure();
        using (var writer = new JournalDeltaStore(root.Path, identity, tree, observer))
        {
            Guid id = writer.Create("file", false, 32).Id; writer.Write(id, 0, RandomNumberGenerator.GetBytes(8192));
            observer.Boundary = "latest.after-root-publication";
            Assert.Throws<IOException>(() => writer.Write(id, 0, RandomNumberGenerator.GetBytes(8192)));
        }
        string c = Path.Combine(root.Path, "delta.journal");
        string[] backups = Directory.EnumerateFiles(root.Path, "commit-*.bin", SearchOption.AllDirectories).ToArray();
        byte[][] before = backups.Select(File.ReadAllBytes).ToArray();
        File.Delete(c);
        Assert.Equal("DeltaCorrupt", Assert.Throws<WorkspaceException>(() => new JournalDeltaStore(root.Path, identity, tree)).Code);
        Assert.False(File.Exists(c));
        for (int i = 0; i < backups.Length; i++) Assert.Equal(before[i], File.ReadAllBytes(backups[i]));
    }
    [Fact]
    public void UnknownFileBesideIncompleteCPreservesTheWholePendingTransaction()
    {
        using var root = new TestDirectory(); var tree = new SnapshotOverlayTests.FakeSnapshotTree();
        var identity = Identity(tree); Enable(root.Path, identity, tree); var observer = new Failure();
        using (var writer = new JournalDeltaStore(root.Path, identity, tree, observer))
        {
            Guid id = writer.Create("file", false, 32).Id; writer.Write(id, 0, [1, 2]);
            observer.Boundary = "latest.after-intent-write";
            Assert.Throws<IOException>(() => writer.Write(id, 0, [3, 4]));
        }
        string pending = Directory.EnumerateFiles(root.Path, "transaction.pending", SearchOption.AllDirectories).Single();
        byte[] descriptor = File.ReadAllBytes(pending); string unknown = Path.Combine(Path.GetDirectoryName(pending)!, "foreign.txt");
        File.WriteAllText(unknown, "preserve");
        Assert.Equal("DeltaCorrupt", Assert.Throws<WorkspaceException>(() => new JournalDeltaStore(root.Path, identity, tree)).Code);
        Assert.Equal(descriptor, File.ReadAllBytes(pending)); Assert.Equal("preserve", File.ReadAllText(unknown));
    }
    [Fact]
    public void DamagedCompleteIntentIsPreservedAndRefusesReconnect()
    {
        using var root = new TestDirectory(); var tree = new SnapshotOverlayTests.FakeSnapshotTree();
        var identity = Identity(tree); Enable(root.Path, identity, tree); var observer = new Failure();
        using (var writer = new JournalDeltaStore(root.Path, identity, tree, observer))
        {
            Guid id = writer.Create("file", false, 32).Id; writer.Write(id, 0, [1, 2]); observer.Boundary = "latest.after-intent-flush";
            Assert.Throws<IOException>(() => writer.Write(id, 0, [3, 4]));
        }
        string pending = Directory.EnumerateFiles(root.Path, "transaction.pending", SearchOption.AllDirectories).Single();
        byte[] bytes = File.ReadAllBytes(pending); bytes[^1] ^= 1; File.WriteAllBytes(pending, bytes);
        Assert.Equal("DeltaCorrupt", Assert.Throws<WorkspaceException>(() => new JournalDeltaStore(root.Path, identity, tree)).Code);
        Assert.Equal(bytes, File.ReadAllBytes(pending));
    }
    [Fact]
    public void ChangedPendingTemporaryIsPreservedRatherThanOverwrittenDuringRecovery()
    {
        using var root = new TestDirectory(); var tree = new SnapshotOverlayTests.FakeSnapshotTree();
        var identity = Identity(tree); Enable(root.Path, identity, tree); var observer = new Failure(); Guid id;
        using (var writer = new JournalDeltaStore(root.Path, identity, tree, observer))
        {
            id = writer.Create("file", false, 32).Id; writer.Write(id, 0, [1, 2]); observer.Boundary = "latest.after-intent-flush";
            Assert.Throws<IOException>(() => writer.Write(id, 0, [3, 4]));
        }
        string objects = Directory.EnumerateDirectories(root.Path, "latest-*").Single();
        string temporary = Path.Combine(objects, $"node-{id:N}.json.new"); byte[] foreign = "changed temporary"u8.ToArray(); File.WriteAllBytes(temporary, foreign);
        string pending = Path.Combine(objects, "transaction.pending"); byte[] redo = File.ReadAllBytes(pending);
        Assert.Equal("DeltaCorrupt", Assert.Throws<WorkspaceException>(() => new JournalDeltaStore(root.Path, identity, tree)).Code);
        Assert.Equal(foreign, File.ReadAllBytes(temporary)); Assert.Equal(redo, File.ReadAllBytes(pending));
    }
    [Fact]
    public void ClosedUnlinkedIdentityReclaimsPayloadAndMetadataWithoutChangingPathSequence()
    {
        using var root = new TestDirectory(); var tree = new SnapshotOverlayTests.FakeSnapshotTree();
        var identity = Identity(tree); Enable(root.Path, identity, tree);
        SourceChangeView changes; SnapshotDeltaSeal seal;
        using (var writer = new JournalDeltaStore(root.Path, identity, tree))
        {
            Guid id = writer.Create("file", false, 32).Id; byte[] data = RandomNumberGenerator.GetBytes(8192);
            writer.Write(id, 0, data); writer.Delete(id);
            byte[] actual = new byte[data.Length]; writer.Read(id, 0, actual); Assert.Equal(data, actual);
            changes = writer.ObserveChanges([]); long sequence = writer.Observe().DurableSequence;
            writer.ReleaseUnlinkedFile(id);
            Assert.Equal(0, writer.Observe().PayloadBytes); Assert.Equal(sequence, writer.Observe().DurableSequence);
            Assert.Equal(changes.Changes, writer.ObserveChanges([]).Changes); Assert.Null(writer.Stat("file"));
            Assert.Throws<FileNotFoundException>(() => writer.Get(id)); writer.ReleaseUnlinkedFile(id);
            seal = writer.Seal();
        }
        using var resumed = new JournalDeltaStore(root.Path, identity, tree);
        Assert.Equal(seal, resumed.Seal()); Assert.Equal(changes.Changes, resumed.ObserveChanges([]).Changes); Assert.Null(resumed.Stat("file"));
    }
    [Theory]
    [InlineData("latest.before-root-publication")]
    [InlineData("latest.after-object-reclamation")]
    public void InterruptedUnlinkedCloseFinishesExactReclamationAtTheSameSourceSequence(string boundary)
    {
        using var root = new TestDirectory(); var tree = new SnapshotOverlayTests.FakeSnapshotTree();
        var identity = Identity(tree); Enable(root.Path, identity, tree); var observer = new Failure();
        SourceChangeView changes;
        using (var writer = new JournalDeltaStore(root.Path, identity, tree, observer))
        {
            Guid id = writer.Create("file", false, 32).Id; writer.Write(id, 0, [1, 2]); writer.Delete(id);
            changes = writer.ObserveChanges([]); observer.Boundary = boundary;
            Assert.Throws<IOException>(() => writer.ReleaseUnlinkedFile(id));
        }
        using var resumed = new JournalDeltaStore(root.Path, identity, tree);
        Assert.Equal(0, resumed.Observe().PayloadBytes); Assert.Equal(3, resumed.Observe().DurableSequence);
        Assert.Equal(changes.Changes, resumed.ObserveChanges([]).Changes); Assert.Null(resumed.Stat("file"));
    }
    [Theory]
    [InlineData('1')]
    [InlineData('2')]
    [InlineData('3')]
    public void PreviousCurrentRootMagicIsPreservedAndRejected(char previous)
    {
        using var root = new TestDirectory(); var tree = new SnapshotOverlayTests.FakeSnapshotTree();
        var identity = Identity(tree); Enable(root.Path, identity, tree);
        string path = Path.Combine(root.Path, "delta.journal"); byte[] bytes = File.ReadAllBytes(path); bytes[3] = (byte)previous; File.WriteAllBytes(path, bytes);
        Assert.Equal("OverlayFormatUnsupported", Assert.Throws<WorkspaceException>(() => new JournalDeltaStore(root.Path, identity, tree)).Code);
        Assert.Equal(bytes, File.ReadAllBytes(path));
    }
    [Theory]
    [InlineData("block-")]
    [InlineData("node-")]
    public void DamagedCurrentObjectIsPreservedAndRefusesReconnect(string prefix)
    {
        using var root = new TestDirectory(); var tree = new SnapshotOverlayTests.FakeSnapshotTree();
        var identity = Identity(tree); Enable(root.Path, identity, tree);
        using (var writer = new JournalDeltaStore(root.Path, identity, tree))
        { Guid id = writer.Create("file", false, 32).Id; writer.Write(id, 0, RandomNumberGenerator.GetBytes(8192)); }
        string path = Path.Combine(root.Path, "delta.journal");
        byte[] bytes = File.ReadAllBytes(path); bytes[prefix == "block-" ? 256 : ^1] ^= 1; File.WriteAllBytes(path, bytes);
        Assert.Equal("DeltaCorrupt", Assert.Throws<WorkspaceException>(() => new JournalDeltaStore(root.Path, identity, tree)).Code);
        Assert.Equal(bytes, File.ReadAllBytes(path));
    }
    private sealed class Failure : IJournalMutationObserver
    {
        public string? Boundary;
        public long LastAckBytes;
        public void Observe(JournalMutationBoundary boundary)
        {
            if (boundary.Boundary == "latest.after-ack-ready") LastAckBytes = boundary.FrameBytes;
            if (boundary.Boundary == Boundary) throw new IOException("Owned latest-journal interruption.");
        }
    }
    [Fact]
    public async Task BriefMetadataReaderWithoutDeleteSharingPublishesTheSameFlushedWrite()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var root = new TestDirectory(); var tree = new SnapshotOverlayTests.FakeSnapshotTree();
        var identity = Identity(tree); Enable(root.Path, identity, tree);
        using var writer = new JournalDeltaStore(root.Path, identity, tree);
        Guid id = writer.Create("file", false, 32).Id; writer.Write(id, 0, [1, 2]);
        string path = Path.Combine(root.Path, "delta.journal");
        using var observer = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var write = Task.Run(() => writer.Write(id, 0, new byte[] { 3, 4 }), TestContext.Current.CancellationToken);
        await Task.Delay(150, TestContext.Current.CancellationToken); observer.Dispose(); await write;
        byte[] actual = new byte[2]; writer.Read(id, 0, actual); Assert.Equal(new byte[] { 3, 4 }, actual);
        Assert.Equal(3, writer.Observe().DurableSequence); Assert.Equal(2, writer.Observe().PayloadBytes);
        Assert.DoesNotContain(Directory.EnumerateFiles(root.Path, "*", SearchOption.AllDirectories), p => p.EndsWith(".new", StringComparison.Ordinal) || p.EndsWith(".pending", StringComparison.Ordinal));
    }
    [Theory]
    [InlineData("intent")]
    [InlineData("object-removed")]
    [InlineData("journal-removed")]
    public void ExactCurrentGenerationReclamationResumesAfterPartialDeletion(string boundary)
    {
        using var root = new TestDirectory(); var tree = new SnapshotOverlayTests.FakeSnapshotTree();
        string delta = Path.Combine(root.Path, "delta"), receipt = Path.Combine(root.Path, "reclaim");
        var identity = Identity(tree); Enable(delta, identity, tree); SnapshotDeltaSeal seal;
        using (var store = new JournalDeltaStore(delta, identity, tree))
        { Guid id = store.Create("file", false, 32).Id; store.Write(id, 0, [1, 2, 3]); seal = store.Seal(); }
        Assert.Throws<IOException>(() => LatestJournalReclamation.ReclaimDirectory(delta, seal, ["identity.json"], receipt,
            at => { if (at == boundary) throw new IOException("interrupted reclamation"); }));
        LatestJournalReclamation.ReclaimDirectory(delta, seal, ["identity.json"], receipt);
        Assert.False(Directory.Exists(delta));
        LatestJournalReclamation.ReclaimDirectory(delta, seal, ["identity.json"], receipt);
    }
    [Fact]
    public void CurrentGenerationReclamationRejectsWriterWrongSealAndUnknownFiles()
    {
        using var root = new TestDirectory(); var tree = new SnapshotOverlayTests.FakeSnapshotTree();
        string delta = Path.Combine(root.Path, "delta"), receipt = Path.Combine(root.Path, "reclaim");
        var identity = Identity(tree); Enable(delta, identity, tree); SnapshotDeltaSeal seal;
        using (var store = new JournalDeltaStore(delta, identity, tree))
        {
            Guid id = store.Create("file", false, 32).Id; store.Write(id, 0, [1]); seal = store.Seal();
            Assert.Throws<IOException>(() => LatestJournalReclamation.ReclaimDirectory(delta, seal, ["identity.json"], receipt));
        }
        Assert.False(File.Exists(receipt));
        Assert.Equal("ViewSourceChanged", Assert.Throws<WorkspaceException>(() => LatestJournalReclamation.ReclaimDirectory(delta,
            seal with { JournalHash = new string('0', 64) }, ["identity.json"], receipt)).Code);
        string unknown = Path.Combine(delta, "user.txt"); File.WriteAllText(unknown, "preserve");
        Assert.Throws<WorkspaceException>(() => LatestJournalReclamation.ReclaimDirectory(delta, seal, ["identity.json"], receipt));
        Assert.False(File.Exists(receipt)); Assert.Equal("preserve", File.ReadAllText(unknown));
    }
}
