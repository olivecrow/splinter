using System.Security.Cryptography;
using System.Text.Json;
using Splinter.Core;
using Splinter.Platform.Windows;
using Splinter.Runtime;

namespace Splinter.Tests;

public sealed class JournalRangeAdoptionTests
{
    [Fact(Timeout = 60000)]
    public void AdoptionCancellationKeepsDurablePrefixAndResumesWithoutReplacingLaterWrites()
    {
        using var root = new TestDirectory(); var basis = new SnapshotOverlayTests.FakeSnapshotTree();
        var pins = new WindowsJournalFilePins(); var content = new Content();
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var identity = new OverlayIdentity(3, "adoption-batches", basis.Snapshot, long.MaxValue, 65536);
        int durable = 0; bool interrupt = false;
        using var writer = new JournalDeltaStore(Path.Combine(root.Path, "delta"), identity, basis,
            new Boundary(point => { if (interrupt && point.Operation == "adopt-range" && point.Boundary == "commit.after-flush" && ++durable == 1) cancel.Cancel(); }),
            sourceFilePins: pins, capturedContentReader: content);
        var ids = new Guid[130];
        for (int i = 0; i < ids.Length; i++) { ids[i] = writer.Create("file-" + i.ToString("D3"), false, 32).Id; writer.Write(ids[i], 0, [(byte)i]); }
        string id = new('a', 24), directory = Path.Combine(root.Path, "captures", id); var capture = writer.CaptureSource(directory, id, []);
        var rows = new List<CapturedRangeAdoptionEntry>();
        using (var frozen = new SourceCaptureTree(directory, basis, pins, content))
            for (int i = 0; i < ids.Length; i++)
            {
                var range = Assert.Single(frozen.GetFileRanges("file-" + i.ToString("D3"))!.PrivateRanges);
                rows.Add(new(ids[i], 0, 1, range.BlockIdentity!, range.BlockOffset, content.Add([(byte)i]), 0, false));
            }
        CapturedRangeAdoptionStore.Publish(root.Path, new(id, capture.Sequence, capture.Digest), rows, pins, TestContext.Current.CancellationToken);
        var seal = writer.Seal(); long flushes = writer.Observe().Metrics!.FlushCount; interrupt = true;
        Assert.ThrowsAny<OperationCanceledException>(() => writer.AdoptCapturedRanges(root.Path, id, cancel.Token));
        Assert.Equal(64, durable); Assert.Equal(seal, writer.Seal()); Assert.False(File.Exists(Path.Combine(directory, "materialized.json")));
        Assert.Equal(flushes + 1, writer.Observe().Metrics!.FlushCount);
        interrupt = false; writer.Write(ids[0], 0, [250]);
        writer.AdoptCapturedRanges(root.Path, id, TestContext.Current.CancellationToken);
        Assert.InRange(writer.Observe().Metrics!.FlushCount - flushes, 3, 10);
        byte[] value = new byte[1]; writer.Read(ids[0], 0, value); Assert.Equal((byte)250, value[0]);
        using (var frozen = new SourceCaptureTree(directory, basis, pins, content))
        { frozen.Read("file-000", 0, value); Assert.Equal((byte)0, value[0]); }
        SourceCaptureTree.Complete(directory, pins);
    }
    [Fact]
    public void PreliminaryHeaderRoutingStillRequiresTheConstructorFullRootProofBeforeEmptyAdoption()
    {
        using var root = new TestDirectory(); var basis = new SnapshotOverlayTests.FakeSnapshotTree(); var pins = new WindowsJournalFilePins(); var content = new Content();
        var identity = new OverlayIdentity(3, "adoption-root-proof", basis.Snapshot, long.MaxValue, 65536);
        using var source = new JournalDeltaStore(Path.Combine(root.Path, "delta"), identity, basis, sourceFilePins: pins, capturedContentReader: content);
        var file = source.Create("source", false, 32); source.Write(file.Id, 0, new byte[] { 4 }); string id = new('f', 24), path = Path.Combine(root.Path, "captures", id);
        var capture = source.CaptureSource(path, id, []); var names = capture.Paged!.Names;
        var changed = capture with { Paged = capture.Paged with { Names = names with { Page = names.Page! with { ContentHash = new string('0', 64) } } }, Digest = "" };
        changed = changed with { Digest = SourceCaptureTree.Digest(changed) };
        File.WriteAllText(Path.Combine(path, "capture.json"), JsonSerializer.Serialize(changed, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        Assert.Equal(changed.Digest, SourceCaptureTree.ReadCapture(path, verifyPagedRoots: false).Digest);
        CapturedRangeAdoptionStore.Publish(root.Path, new(id, changed.Sequence, changed.Digest), [], pins, TestContext.Current.CancellationToken);
        var before = source.Seal(); Assert.Throws<WorkspaceException>(() => source.AdoptCapturedRanges(root.Path, id, TestContext.Current.CancellationToken)); Assert.Equal(before, source.Seal());
        Assert.False(File.Exists(Path.Combine(path, "materialized.json"))); byte[] value = new byte[1]; Assert.Equal(1, source.Read(file.Id, 0, value)); Assert.Equal((byte)4, value[0]);
    }

    [Fact]
    public void AcceptedCaptureRecoversMissingOrTornControlBeforeAnyGarbageCollection()
    {
        using var root = new TestDirectory(); var basis = new SnapshotOverlayTests.FakeSnapshotTree(); var pins = new WindowsJournalFilePins();
        var identity = new OverlayIdentity(3, "capture-recovery", basis.Snapshot, long.MaxValue, 65536);
        string delta = Path.Combine(root.Path, "delta"), id = new('b', 24), captures = Path.Combine(root.Path, "captures"), path = Path.Combine(captures, id); Guid file;
        using (var writer = new JournalDeltaStore(delta, identity, basis, new Boundary(point =>
            { if (point.Boundary == "capture.freeze-accepted") throw new IOException("storage full before control publication"); }), sourceFilePins: pins))
        {
            file = writer.Create("source", false, 32).Id; writer.Write(file, 0, "frozen"u8);
            Assert.Throws<IOException>(() => writer.CaptureSource(path, id, [])); Assert.False(File.Exists(Path.Combine(path, "capture.json")));
        }
        File.WriteAllText(Path.Combine(path, "capture.json.pending"), "{\"version\":2,\"id\":\"");
        using var resumed = new JournalDeltaStore(delta, identity, basis, sourceFilePins: pins);
        resumed.RecoverSourceCaptures(captures); var original = SourceCaptureTree.ReadCapture(path);
        resumed.Write(file, 0, "latest"u8); Assert.Equal(original.Digest, resumed.CaptureSource(path, id, []).Digest);
        resumed.MaintainLiveStorage(true, TestContext.Current.CancellationToken);
        using var frozen = new SourceCaptureTree(path, basis, pins); byte[] bytes = new byte[6];
        Assert.Equal(6, frozen.Read("source", 0, bytes)); Assert.Equal("frozen"u8.ToArray(), bytes);
    }

    [Fact]
    public void PendingCapturedCanonicalRangesStayRequiredAndActiveReadersBlockCompletion()
    {
        using var root = new TestDirectory(); var basis = new SnapshotOverlayTests.FakeSnapshotTree(); var pins = new WindowsJournalFilePins(); var content = new Content();
        var identity = new OverlayIdentity(3, "capture-readers", basis.Snapshot, long.MaxValue, 65536); string delta = Path.Combine(root.Path, "delta");
        using var writer = new JournalDeltaStore(delta, identity, basis, sourceFilePins: pins, capturedContentReader: content);
        var file = writer.Create("source", false, 32); writer.Write(file.Id, 0, "captured"u8);
        string first = new('c', 24), firstPath = Path.Combine(root.Path, "captures", first); var firstCapture = writer.CaptureSource(firstPath, first, []); CapturedPrivateSourceRange range;
        using (var frozen = new SourceCaptureTree(firstPath, basis, pins, content)) range = Assert.Single(frozen.GetFileRanges("source")!.PrivateRanges);
        string hash = content.Add("captured"u8.ToArray());
        CapturedRangeAdoptionStore.Publish(root.Path, new(first, firstCapture.Sequence, firstCapture.Digest),
            [new(file.Id, range.Offset, range.Length, range.BlockIdentity!, range.BlockOffset, hash, 0, false, range.Length)], pins, TestContext.Current.CancellationToken);
        writer.AdoptCapturedRanges(root.Path, first, TestContext.Current.CancellationToken); SourceCaptureTree.Complete(firstPath, pins); writer.CompleteSourceCaptureRequest(first);
        string second = new('d', 24), secondPath = Path.Combine(root.Path, "captures", second); writer.CaptureSource(secondPath, second, []);
        writer.Write(file.Id, 0, "replaced"u8);
        using var view = writer.ObserveChanges([]); Assert.Contains(JournalDeltaStore.EnumerateRequiredCapturedContent(view), item => item.Hash == hash && item.Length == 8);
        using (var reader = new SourceCaptureTree(secondPath, basis, pins, content))
        {
            Assert.ThrowsAny<IOException>(() => SourceCaptureTree.Complete(secondPath, pins)); Assert.False(File.Exists(Path.Combine(secondPath, "materialized.json")));
            byte[] bytes = new byte[8]; Assert.Equal(8, reader.Read("source", 0, bytes)); Assert.Equal("captured"u8.ToArray(), bytes);
        }
        SourceCaptureTree.Complete(secondPath, pins); writer.CompleteSourceCaptureRequest(second);
        using var completed = writer.ObserveChanges([]);
        Assert.DoesNotContain(JournalDeltaStore.EnumerateRequiredCapturedContent(completed), item => item.Hash == hash);
    }

    [Theory]
    [InlineData("metadata.repack-roots-durable")]
    [InlineData("metadata.repack-payload-durable")]
    [InlineData("metadata.repack-before-publication")]
    [InlineData("metadata.repack-published")]
    [InlineData("metadata.repack-reclaimed")]
    public void LiveIndexCollectionResumesTheSameDurableRootAfterInterruption(string boundary)
    {
        using var root = new TestDirectory(); var basis = new SnapshotOverlayTests.FakeSnapshotTree(); var pins = new WindowsJournalFilePins();
        var identity = new OverlayIdentity(3, "index-recovery", basis.Snapshot, long.MaxValue, 65536); string delta = Path.Combine(root.Path, "delta");
        Guid id; SnapshotDeltaSeal seal;
        using (var writer = new JournalDeltaStore(delta, identity, basis, sourceFilePins: pins))
        {
            id = writer.Create("kept", false, 32).Id; writer.Write(id, 0, "preserved"u8); seal = writer.Seal();
            Assert.Throws<IOException>(() => writer.CollectLiveMetadata(true, TestContext.Current.CancellationToken,
                point => { if (point == boundary) throw new IOException("fixed metadata interruption"); }));
        }
        using var resumed = new JournalDeltaStore(delta, identity, basis, sourceFilePins: pins);
        resumed.CollectLiveMetadata(true, TestContext.Current.CancellationToken); Assert.Equal(seal, resumed.Seal());
        byte[] bytes = new byte[9]; Assert.Equal(9, resumed.Read(id, 0, bytes)); Assert.Equal("preserved"u8.ToArray(), bytes);
        Assert.False(File.Exists(Path.Combine(delta, "live-repack.json")));
    }

    [Theory]
    [InlineData("segments.reclaim-intent")]
    [InlineData("segments.reclaim-roots")]
    [InlineData("segments.reclaim-file")]
    [InlineData("segments.reclaim-complete")]
    public void SegmentRetirementResumesTheSameNativeInventoryAfterInterruption(string boundary)
    {
        using var root = new TestDirectory(); var basis = new SnapshotOverlayTests.FakeSnapshotTree(); var pins = new WindowsJournalFilePins();
        var identity = new OverlayIdentity(3, "segment-recovery", basis.Snapshot, long.MaxValue, 65536); string delta = Path.Combine(root.Path, "delta"); Guid id; SnapshotDeltaSeal seal;
        using (var writer = new JournalDeltaStore(delta, identity, basis, boundedLogBytes: 4096, sourceFilePins: pins))
        {
            id = writer.Create("kept", false, 32).Id; writer.Write(id, 0, new byte[8192]); writer.Write(id, 0, Enumerable.Repeat((byte)7, 8192).ToArray()); seal = writer.Seal();
            Assert.Throws<IOException>(() => writer.MaintainLiveStorage(false, TestContext.Current.CancellationToken, true,
                point => { if (point == boundary) throw new IOException("fixed segment interruption"); }));
        }
        using var resumed = new JournalDeltaStore(delta, identity, basis, sourceFilePins: pins);
        resumed.MaintainLiveStorage(false, TestContext.Current.CancellationToken, true); Assert.Equal(seal, resumed.Seal());
        byte[] bytes = new byte[8192]; Assert.Equal(bytes.Length, resumed.Read(id, 0, bytes)); Assert.All(bytes, value => Assert.Equal((byte)7, value));
        Assert.False(File.Exists(Path.Combine(delta, "segment-reclaim.json")));
    }

    [Fact]
    public void FinalPreAndPostSealsSurviveCaptureAdoptionAndStorageRelocationButDetectNewAcceptedWrites()
    {
        using var root = new TestDirectory(); var basis = new SnapshotOverlayTests.FakeSnapshotTree(); var pins = new WindowsJournalFilePins(); var content = new Content();
        var identity = new OverlayIdentity(3, "final-seal", basis.Snapshot, long.MaxValue, 65536);
        string delta = Path.Combine(root.Path, "delta"), id = new('f', 24), path = Path.Combine(root.Path, "captures", id);
        using var writer = new JournalDeltaStore(delta, identity, basis, sourceFilePins: pins, capturedContentReader: content);
        var file = writer.Create("source", false, 32); writer.Write(file.Id, 0, "final-value"u8); var before = writer.Seal();
        var capture = writer.CaptureSource(path, id, []); CapturedPrivateSourceRange range;
        using (var frozen = new SourceCaptureTree(path, basis, pins, content)) range = Assert.Single(frozen.GetFileRanges("source")!.PrivateRanges);
        string hash = content.Add("final-value"u8.ToArray());
        CapturedRangeAdoptionStore.Publish(root.Path, new(id, capture.Sequence, capture.Digest),
            [new(file.Id, range.Offset, range.Length, range.BlockIdentity!, range.BlockOffset, hash, 0, false, range.Length)], pins, TestContext.Current.CancellationToken);
        writer.AdoptCapturedRanges(root.Path, id, TestContext.Current.CancellationToken); SourceCaptureTree.Complete(path, pins); writer.MaintainLiveStorage(force: true, ct: TestContext.Current.CancellationToken);
        Assert.Equal(before, writer.Seal()); Assert.Equal(before, JournalDeltaStore.SealJournal(Path.Combine(delta, "delta.journal"), before.DurableSequence));
        Assert.Equal(before.JournalBytes, writer.Observe().SealJournalBytes);
        writer.Write(file.Id, 0, "final-value"u8); Assert.NotEqual(before, writer.Seal());
    }

    [Fact]
    public void StableChunkAdoptionPreservesLaterSlicesRenameAndCollectsTheOldSegment()
    {
        using var root = new TestDirectory(); var basis = new SnapshotOverlayTests.FakeSnapshotTree();
        var pins = new WindowsJournalFilePins(); var content = new Content();
        var identity = new OverlayIdentity(3, "range-adoption", basis.Snapshot, long.MaxValue, 65536);
        string delta = Path.Combine(root.Path, "delta"), id = new('d', 24), capturePath = Path.Combine(root.Path, "captures", id); Guid file;
        using (var writer = new JournalDeltaStore(delta, identity, basis, sourceFilePins: pins, capturedContentReader: content))
        {
            file = writer.Create("source", false, 32).Id; writer.Write(file, 0, "first-value"u8);
            var capture = writer.CaptureSource(capturePath, id, []); CapturedPrivateSourceRange range;
            using (var tree = new SourceCaptureTree(capturePath, basis, pins, content))
            {
                var layout = tree.GetFileRanges("source")!; Assert.Equal(file, layout.FileId); range = Assert.Single(layout.PrivateRanges);
                Assert.NotNull(range.Backing); Assert.NotNull(range.BlockIdentity);
            }
            string hash = content.Add("first-value"u8.ToArray());
            CapturedRangeAdoptionStore.Publish(root.Path, new(id, capture.Sequence, capture.Digest),
                [new(file, range.Offset, range.Length, range.BlockIdentity!, range.BlockOffset, hash, 0, false, range.Length)], pins, TestContext.Current.CancellationToken);
            writer.Write(file, 2, "XY"u8); writer.Rename(file, "continued", false);
            writer.AdoptCapturedRanges(root.Path, id, TestContext.Current.CancellationToken); SourceCaptureTree.Complete(capturePath, pins);
            writer.MaintainLiveStorage(collectSegments: true, ct: TestContext.Current.CancellationToken);
            byte[] actual = new byte[11]; Assert.Equal(actual.Length, writer.Read(file, 0, actual)); Assert.Equal("fiXYt-value"u8.ToArray(), actual);
            Assert.False(File.Exists(Path.Combine(delta, "segment-0000000000000000.sjr")));
            Assert.Contains(writer.EnumerateRequiredCapturedContent(), item => item.Hash == hash && !item.Raw);
            var before = writer.Seal(); writer.AdoptCapturedRanges(root.Path, id, TestContext.Current.CancellationToken); Assert.Equal(before, writer.Seal());
            string next = new('e', 24), nextPath = Path.Combine(root.Path, "captures", next); writer.CaptureSource(nextPath, next, []);
            using var nextTree = new SourceCaptureTree(nextPath, basis, pins, content);
            Assert.Contains(nextTree.GetFileRanges("continued")!.PrivateRanges, item => item.PreservedHash == hash && item.PreservedLength == 11);
            Assert.Equal(actual.Length, nextTree.Read("continued", 0, actual)); Assert.Equal("fiXYt-value"u8.ToArray(), actual);
        }
        using var reopened = new JournalDeltaStore(delta, identity, basis, sourceFilePins: pins, capturedContentReader: content);
        byte[] result = new byte[11]; Assert.Equal(result.Length, reopened.Read(file, 0, result)); Assert.Equal("fiXYt-value"u8.ToArray(), result);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void LiveIndexCatchUpRetainsAdmissionsAndCompletedAdoptionAcrossInterruptedCopy(bool reopen, bool missingProof)
    {
        using var root = new TestDirectory(); var basis = new SnapshotOverlayTests.FakeSnapshotTree();
        basis.Files["Assets/observed"] = "basis"u8.ToArray(); basis.Files["replaced"] = "old"u8.ToArray();
        var pins = new WindowsJournalFilePins(); var content = new Content();
        var identity = new OverlayIdentity(3, "adoption-copy-tail", basis.Snapshot, long.MaxValue, 65536);
        string delta = Path.Combine(root.Path, "delta"), id = new('e', 24), capturePath = Path.Combine(root.Path, "captures", id);
        using var writer = new JournalDeltaStore(delta, identity, basis, sourceFilePins: pins, capturedContentReader: content);
        var file = writer.Create("source", false, 32); writer.Write(file.Id, 0, "kept"u8);
        var displaced = writer.Stat("replaced")!; var incoming = writer.Create("incoming", false, 32); writer.Write(incoming.Id, 0, "next"u8);
        var capture = writer.CaptureSource(capturePath, id, []); CapturedPrivateSourceRange range;
        using (var frozen = new SourceCaptureTree(capturePath, basis, pins, content)) range = Assert.Single(frozen.GetFileRanges("source")!.PrivateRanges);
        CapturedRangeAdoptionStore.Publish(root.Path, new(id, capture.Sequence, capture.Digest),
            [new(file.Id, 0, 4, range.BlockIdentity!, range.BlockOffset, content.Add("kept"u8.ToArray()), 0, false, 4)], pins, TestContext.Current.CancellationToken);
        Guid observed = default; bool changed = false; var seal = writer.Seal();
        void Boundary(string point)
        {
            if (point != "metadata.repack-payload-durable" || changed) return;
            changed = true; observed = writer.Stat("Assets/observed")!.Id;
            writer.Rename(incoming.Id, "replaced", true); writer.ReleaseUnlinkedFile(displaced.Id);
            writer.AdoptCapturedRanges(root.Path, id, TestContext.Current.CancellationToken);
            seal = writer.Seal();
            if (reopen) throw new IOException("interrupted after durable adoption and basis admission");
        }
        if (reopen) Assert.Throws<IOException>(() => writer.CollectLiveMetadata(true, TestContext.Current.CancellationToken, Boundary));
        else writer.CollectLiveMetadata(true, TestContext.Current.CancellationToken, Boundary);
        Assert.True(changed);
        writer.Dispose();
        using var resumed = new JournalDeltaStore(delta, identity, basis, sourceFilePins: pins, capturedContentReader: content);
        if (missingProof)
        {
            string artifact = CapturedRangeAdoptionStore.Artifact(root.Path, id), held = Path.Combine(root.Path, "held-owned-proof");
            string intent = Path.Combine(delta, "live-repack.json"); byte[] exactRequest = File.ReadAllBytes(intent);
            File.Move(artifact, held);
            Assert.Throws<WorkspaceException>(() => resumed.CollectLiveMetadata(true, TestContext.Current.CancellationToken));
            Assert.Equal(exactRequest, File.ReadAllBytes(intent)); Assert.Equal(seal, resumed.Seal());
            File.Move(held, artifact);
        }
        resumed.CollectLiveMetadata(true, TestContext.Current.CancellationToken);
        Assert.Equal(seal, resumed.Seal()); Assert.Equal("Assets/observed", resumed.Get(observed).Path);
        Assert.Equal(incoming.Id, resumed.Stat("replaced")!.Id);
        Assert.Throws<FileNotFoundException>(() => resumed.Get(displaced.Id));
        byte[] bytes = new byte[4]; Assert.Equal(4, resumed.Read(file.Id, 0, bytes)); Assert.Equal("kept"u8.ToArray(), bytes);
        using (var frozen = new SourceCaptureTree(capturePath, basis, pins, content))
        { Assert.Equal(4, frozen.Read("source", 0, bytes)); Assert.Equal("kept"u8.ToArray(), bytes); }
        Assert.False(File.Exists(Path.Combine(delta, "live-repack.json")));
        using var state = JsonDocument.Parse(File.ReadAllText(Path.Combine(delta, "segment-state.json")));
        Assert.Equal(1, state.RootElement.GetProperty("roots").GetProperty("adoptions").GetProperty("page").GetProperty("count").GetInt64());
    }

    [Fact]
    public void PendingLiveIndexCopyDoesNotRepeatPreparationWhileACursorPreventsPublication()
    {
        using var root = new TestDirectory(); var basis = new SnapshotOverlayTests.FakeSnapshotTree(); var pins = new WindowsJournalFilePins();
        var identity = new OverlayIdentity(3, "pending-cursor", basis.Snapshot, long.MaxValue, 65536);
        string delta = Path.Combine(root.Path, "delta"); var ct = TestContext.Current.CancellationToken;
        using var writer = new JournalDeltaStore(delta, identity, basis, sourceFilePins: pins);
        var file = writer.Create("before", false, 32); writer.Write(file.Id, 0, "old"u8);
        IEnumerable<DeltaEntry>? held = null;
        writer.CollectLiveMetadata(true, ct, stage => { if (stage == "metadata.repack-payload-durable") held = writer.EnumerateChildren(""); });
        Assert.NotNull(held); string intent = Path.Combine(delta, "live-repack.json"); Assert.True(File.Exists(intent));
        byte[] before = File.ReadAllBytes(intent); writer.Write(file.Id, 0, "new"u8); var later = writer.Create("later", false, 32);
        var seal = writer.Seal(); bool entered = false;
        writer.CollectLiveMetadata(true, ct, _ => entered = true);
        Assert.False(entered); Assert.Equal(before, File.ReadAllBytes(intent));
        Assert.Equal(new[] { "before" }, held.Select(entry => entry.Path));
        writer.CollectLiveMetadata(true, ct); Assert.False(File.Exists(intent)); Assert.Equal(seal, writer.Seal());
        Assert.Equal("later", writer.Get(later.Id).Path); byte[] bytes = new byte[3];
        Assert.Equal(3, writer.Read(file.Id, 0, bytes)); Assert.Equal("new"u8.ToArray(), bytes);
    }

    [Fact]
    public void LiveIndexCopyRetainsFrozenRootsAndAdmittedIdsWhileHeldCursorDefersPreparation()
    {
        using var root = new TestDirectory(); var basis = new SnapshotOverlayTests.FakeSnapshotTree(); basis.Files["Assets/observed"] = "basis"u8.ToArray();
        var pins = new WindowsJournalFilePins(); var identity = new OverlayIdentity(3, "live-index", basis.Snapshot, long.MaxValue, 65536);
        string delta = Path.Combine(root.Path, "delta"), id = new('a', 24), capturePath = Path.Combine(root.Path, "captures", id);
        using var writer = new JournalDeltaStore(delta, identity, basis, boundedLogBytes: 4096, sourceFilePins: pins);
        var file = writer.Create("Assets/current", false, 32); writer.Write(file.Id, 0, "frozen"u8);
        var capture = writer.CaptureSource(capturePath, id, []); string oldIndex = capture.Paged!.Directory;
        var held = writer.EnumerateChildren("Assets");
        bool prepared = false;
        writer.CollectLiveMetadata(force: true, ct: TestContext.Current.CancellationToken, boundary: _ => prepared = true);
        Assert.False(prepared); Assert.False(File.Exists(Path.Combine(delta, "live-repack.json")));
        var observed = writer.Stat("Assets/observed")!; writer.Write(file.Id, 0, "latest"u8); writer.Rename(file.Id, "Assets/later", false);
        Assert.Equal(new[] { "Assets/current", "Assets/observed" }, held.Select(item => item.Path));
        writer.CollectLiveMetadata(force: true, ct: TestContext.Current.CancellationToken);
        var state = JsonSerializer.Deserialize<JournalSegmentState>(File.ReadAllText(Path.Combine(delta, "segment-state.json")), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.NotEqual(oldIndex, state.Roots.Directory); Assert.False(File.Exists(Path.Combine(delta, "live-repack.json")));
        Assert.Equal("Assets/observed", writer.Get(observed.Id).Path); Assert.Equal("Assets/later", writer.Get(file.Id).Path);
        byte[] bytes = new byte[6]; Assert.Equal(6, writer.Read(file.Id, 0, bytes)); Assert.Equal("latest"u8.ToArray(), bytes);
        using (var frozen = new SourceCaptureTree(capturePath, basis, pins))
        { Assert.Equal(6, frozen.Read("Assets/current", 0, bytes)); Assert.Equal("frozen"u8.ToArray(), bytes); }
        Assert.True(Directory.Exists(oldIndex)); SourceCaptureTree.Complete(capturePath, pins);
        writer.MaintainLiveStorage(force: true, ct: TestContext.Current.CancellationToken); Assert.False(Directory.Exists(oldIndex));
        Assert.Equal(6, writer.Read(file.Id, 0, bytes)); Assert.Equal("latest"u8.ToArray(), bytes);
    }
    private sealed class Content : ICapturedContentReader
    {
        private readonly Dictionary<string, byte[]> bytes = new(StringComparer.Ordinal);
        public string Add(byte[] value) { string hash = Convert.ToHexStringLower(SHA256.HashData(value)); bytes.Add(hash, value); return hash; }
        public long GetLength(string hash, bool raw) => bytes[hash].Length;
        public int Read(string hash, bool raw, long offset, Span<byte> destination)
        { var data = bytes[hash]; int count = (int)Math.Min(destination.Length, Math.Max(0, data.Length - offset)); data.AsSpan((int)offset, count).CopyTo(destination); return count; }
    }
    private sealed class Boundary(Action<JournalMutationBoundary> action) : IJournalMutationObserver
    { public void Observe(JournalMutationBoundary value) => action(value); }
}
