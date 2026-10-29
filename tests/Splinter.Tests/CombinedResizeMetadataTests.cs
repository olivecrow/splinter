using System.Security.Cryptography;
using Splinter.Core;
using Splinter.Runtime;

namespace Splinter.Tests;

// Small file-backed journal checks. No provider, native mount, child process,
// power-loss, or performance qualification is implied by observer injection.
public sealed class CombinedResizeMetadataTests
{
    [Theory]
    [InlineData("append")]
    [InlineData("bounded")]
    public void ZeroLengthAndMetadataUseOneDurableFrameAndReplayTogether(string mode)
    {
        using var f = new Fixture(mode); DeltaEntry expected; long sequence;
        using (var store = f.Open())
        {
            Guid id = store.Stat("file")!.Id;
            store.Write(id, 2, [91, 92, 93]); store.SetUnixMode(id, 0x1a4);
            var old = store.Get(id); var before = store.Observe(); f.Observer.Events.Clear();
            store.SetLengthAndMetadata(id, 0, 33, 101, 102, 103);
            expected = old with { Length = 0, BaseLength = 0, Attributes = 33, CreationTime = 101, LastAccessTime = 102, LastWriteTime = 103 };
            Assert.Equal(expected, store.Get(id)); Assert.Equal(0, store.Read(id, 0, new byte[8]));
            var after = store.Observe(); sequence = after.DurableSequence;
            Assert.Equal(before.DurableSequence + 1, sequence);
            Assert.Equal(before.Metrics!.CommitCount + 1, after.Metrics!.CommitCount);
            Assert.Equal(before.Metrics.FlushCount + 1, after.Metrics.FlushCount);
            var flush = Assert.Single(f.Observer.Events, e => e.Boundary == "commit.after-flush");
            Assert.Equal("resize", flush.Operation); Assert.Equal(0, flush.PayloadBytes);
            Assert.Equal(0, f.Tree.ReadBytes);
        }
        using var replay = f.Open();
        Assert.Equal(expected, replay.Get(expected.Id)); Assert.Equal(sequence, replay.Observe().DurableSequence);
        Assert.Equal(0, replay.Read(expected.Id, 0, new byte[8]));
    }

    [Theory]
    [InlineData("append")]
    [InlineData("bounded")]
    [InlineData("latest")]
    public void ClippedOriginalAndPrivateTailsNeverReturnAfterExtensionOrReconnect(string mode)
    {
        using var f = new Fixture(mode); Guid id; DeltaEntry expected;
        byte[] bytes = f.Original.ToArray(); new byte[] { 91, 92, 93, 94, 95, 96 }.CopyTo(bytes, 2);
        Array.Clear(bytes, 5, bytes.Length - 5);
        using (var store = f.Open())
        {
            id = store.Stat("file")!.Id;
            store.Write(id, 2, [91, 92, 93, 94, 95, 96]);
            store.Write(id, 12, [81, 82, 83]);
            store.SetLengthAndMetadata(id, 5, 33, 101, 102, 103);
            store.SetLengthAndMetadata(id, f.Original.Length, 32, 201, 202, 203);
            expected = store.Get(id); Assert.Equal(5, expected.BaseLength);
            Assert.Equal(0, f.Tree.ReadBytes); Assert.Equal(bytes, Read(store, id));
        }
        using var replay = f.Open();
        Assert.Equal(expected, replay.Get(id)); Assert.Equal(bytes, Read(replay, id));
        Assert.Equal(f.Original, f.Tree.Files["file"]);
    }

    [Theory]
    [InlineData("append")]
    [InlineData("bounded")]
    public void EmptyFileMetadataOnlyAndExactNoOpKeepTheirExistingCommitCounts(string mode)
    {
        using var f = new Fixture(mode); Guid id; DeltaEntry expected;
        using (var store = f.Open())
        {
            id = store.Create("empty", false, 32).Id;
            var old = store.Get(id); var before = store.Observe(); f.Observer.Events.Clear();
            store.SetLengthAndMetadata(id, 0, old.Attributes, old.CreationTime, old.LastAccessTime, old.LastWriteTime);
            Assert.Equal(before.DurableSequence, store.Observe().DurableSequence); Assert.Empty(f.Observer.Events);
            store.SetLengthAndMetadata(id, 0, 33, 101, 102, 103);
            expected = store.Get(id);
            Assert.Equal(before.DurableSequence + 1, store.Observe().DurableSequence);
            Assert.Equal(before.Metrics!.FlushCount + 1, store.Observe().Metrics!.FlushCount);
            Assert.Equal("metadata", Assert.Single(f.Observer.Events, e => e.Boundary == "commit.after-flush").Operation);
            f.Observer.Events.Clear();
            store.SetLengthAndMetadata(id, 0, 33, 101, 102, 103);
            Assert.Empty(f.Observer.Events); Assert.Equal(before.DurableSequence + 1, store.Observe().DurableSequence);
        }
        using var replay = f.Open(); Assert.Equal(expected, replay.Get(id));
    }

    [Theory]
    [InlineData("append")]
    [InlineData("bounded")]
    [InlineData("latest")]
    public void UnknownDirectoryAndNegativeLengthAreRejectedBeforeAnyMutation(string mode)
    {
        using var f = new Fixture(mode); using var store = f.Open();
        Guid id = store.Stat("file")!.Id, directory = store.Create("directory", true, 16).Id;
        var before = store.Observe(); var entry = store.Get(id); var parent = store.Get(directory);
        byte[] journal = HashJournal(f.Path); f.Observer.Events.Clear();
        Assert.Throws<FileNotFoundException>(() => store.SetLengthAndMetadata(Guid.NewGuid(), 0, 33, 101, 102, 103));
        Assert.Throws<IOException>(() => store.SetLengthAndMetadata(directory, 0, 33, 101, 102, 103));
        Assert.Throws<ArgumentOutOfRangeException>(() => store.SetLengthAndMetadata(id, -1, 33, 101, 102, 103));
        Assert.Empty(f.Observer.Events); Assert.Equal(entry, store.Get(id)); Assert.Equal(parent, store.Get(directory));
        Assert.Equal(before.DurableSequence, store.Observe().DurableSequence);
        Assert.Equal(before.Metrics!.FlushCount, store.Observe().Metrics!.FlushCount);
        Assert.Equal(journal, HashJournal(f.Path));
    }

    [Fact]
    public void ExhaustedAppendBudgetKeepsTheLastWholeEntryAndJournal()
    {
        using var f = new Fixture("append", budget: 4096); DeltaEntry? last = null; long sequence = -1;
        using (var store = f.Open())
        {
            Guid id = store.Stat("file")!.Id; bool rejected = false;
            for (int attempt = 0; attempt < 32; attempt++)
            {
                var before = store.Observe(); var entry = store.Get(id); byte[] journal = HashJournal(f.Path);
                try { store.SetLengthAndMetadata(id, attempt % 2, 33, 101, 102, 1000 + attempt); }
                catch (WorkspaceException ex) when (ex.Code == "DeltaFull")
                {
                    Assert.Equal(entry, store.Get(id)); Assert.Equal(journal, HashJournal(f.Path));
                    Assert.Equal(before.DurableSequence, store.Observe().DurableSequence);
                    Assert.Equal(before.Metrics!.FlushCount, store.Observe().Metrics!.FlushCount);
                    last = entry; sequence = before.DurableSequence; rejected = true; break;
                }
            }
            Assert.True(rejected, "The fixed 4KiB append budget must actually reject a combined mutation.");
        }
        using var replay = f.Open(); Assert.Equal(last, replay.Stat("file")); Assert.Equal(sequence, replay.Observe().DurableSequence);
    }

    [Theory]
    [InlineData("append", "commit.after-header", false)]
    [InlineData("bounded", "commit.after-header", false)]
    [InlineData("append", "commit.after-metadata", false)]
    [InlineData("bounded", "commit.after-metadata", false)]
    [InlineData("append", "commit.after-flush", true)]
    [InlineData("bounded", "commit.after-flush", true)]
    [InlineData("append", "commit.after-apply", true)]
    [InlineData("bounded", "commit.after-apply", true)]
    public void InterruptedCombinedFrameReplaysOldOrNewEntryWithoutMixedMetadata(string mode, string boundary, bool survives)
    {
        using var f = new Fixture(mode); DeltaEntry old, expected; long before;
        using (var store = f.Open())
        {
            Guid id = store.Stat("file")!.Id; store.Write(id, 2, [91, 92, 93]);
            old = store.Get(id); before = store.Observe().DurableSequence;
            expected = old with { Length = 0, BaseLength = 0, Attributes = 33, CreationTime = 101, LastAccessTime = 102, LastWriteTime = 103 };
            f.Observer.FailAt = boundary;
            Assert.Throws<IOException>(() => store.SetLengthAndMetadata(id, 0, 33, 101, 102, 103));
            Assert.Equal("DeltaReconnectRequired", Assert.Throws<WorkspaceException>(() => store.Read(id, 0, new byte[16])).Code);
        }
        f.Observer.FailAt = null;
        using var replay = f.Open();
        Assert.Equal(survives ? expected : old, replay.Get(old.Id));
        Assert.Equal(before + (survives ? 1 : 0), replay.Observe().DurableSequence);
        if (survives) Assert.Equal(0, replay.Read(old.Id, 0, new byte[16]));
        else
        {
            byte[] content = f.Original.ToArray(); new byte[] { 91, 92, 93 }.CopyTo(content, 2);
            Assert.Equal(content, Read(replay, old.Id));
        }
    }

    [Fact]
    public async Task CombinedCallCannotReturnBeforeItsDurableFlushBarrier()
    {
        using var f = new Fixture("bounded"); using var store = f.Open();
        using var entered = new ManualResetEventSlim(); using var proceed = new ManualResetEventSlim();
        Guid id = store.Stat("file")!.Id;
        f.Observer.Events.Clear();
        f.Observer.Callback = boundary =>
        {
            if (boundary.Boundary != "commit.before-flush") return;
            entered.Set();
            if (!proceed.Wait(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken)) throw new TimeoutException("Flush gate was not released.");
        };
        var writer = Task.Run(() => store.SetLengthAndMetadata(id, 0, 33, 101, 102, 103), TestContext.Current.CancellationToken);
        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
            Assert.False(writer.IsCompleted);
        }
        finally { proceed.Set(); await writer; f.Observer.Callback = null; }
        Assert.Equal(0, store.Get(id).Length);
        Assert.Single(f.Observer.Events, e => e.Boundary == "commit.after-flush");
        var expected = store.Get(id); store.Dispose();
        using var replay = f.Open(); Assert.Equal(expected, replay.Get(id));
    }

    [Fact]
    public void LatestModeKeepsTwoSeparateDurableMutationPublications()
    {
        using var f = new Fixture("latest"); using var store = f.Open();
        Guid id = store.Stat("file")!.Id; var before = store.Observe(); f.Observer.Events.Clear();
        store.SetLengthAndMetadata(id, 0, 33, 101, 102, 103);
        Assert.Equal(before.DurableSequence + 2, store.Observe().DurableSequence);
        Assert.Equal(before.Metrics!.FlushCount + 2, store.Observe().Metrics!.FlushCount);
        Assert.Equal(new[] { "resize", "metadata" }, f.Observer.Events.Where(e => e.Boundary == "latest.after-ack-ready").Select(e => e.Operation));
        var expected = store.Get(id); store.Dispose();
        using var replay = f.Open(); Assert.Equal(expected, replay.Get(id));
    }

    [Fact]
    public void LatestMetadataFailurePreservesTheAlreadyDurableResizeBoundary()
    {
        using var f = new Fixture("latest"); Guid id; DeltaEntry old; long sequence;
        using (var store = f.Open())
        {
            id = store.Stat("file")!.Id; old = store.Get(id); sequence = store.Observe().DurableSequence;
            f.Observer.FailAt = "latest.before-intent"; f.Observer.FailOperation = "metadata";
            Assert.Throws<IOException>(() => store.SetLengthAndMetadata(id, 0, 33, 101, 102, 103));
            Assert.Equal("DeltaReconnectRequired", Assert.Throws<WorkspaceException>(() => store.Get(id)).Code);
        }
        f.Observer.FailAt = null;
        using var replay = f.Open(); var resized = replay.Get(id);
        Assert.Equal(sequence + 1, replay.Observe().DurableSequence);
        Assert.Equal(0, resized.Length); Assert.Equal(0, resized.BaseLength);
        Assert.Equal(old.Attributes, resized.Attributes); Assert.Equal(old.CreationTime, resized.CreationTime);
        Assert.Equal(old.LastAccessTime, resized.LastAccessTime); Assert.NotEqual(103, resized.LastWriteTime);
        replay.SetLengthAndMetadata(id, 0, 33, 101, 102, 103);
        Assert.Equal(sequence + 2, replay.Observe().DurableSequence);
        Assert.Equal((33u, 101L, 102L, 103L), (replay.Get(id).Attributes, replay.Get(id).CreationTime, replay.Get(id).LastAccessTime, replay.Get(id).LastWriteTime));
    }

    [Fact]
    public void SourceViewForwardsCombinedChangesToTheExistingSourceAndCacheOwners()
    {
        using var root = new TestDirectory(); var tree = new SnapshotOverlayTests.FakeSnapshotTree();
        tree.Files["file"] = [1, 2, 3, 4, 5, 6];
        var identity = new OverlayIdentity(2, "combined-view", tree.Snapshot, 64L * 1048576, 4096);
        string worker = System.IO.Path.Combine(root.Path, "worker"), candidate = System.IO.Path.Combine(root.Path, "candidate");
        string source = System.IO.Path.Combine(root.Path, "source"), cache = System.IO.Path.Combine(root.Path, "cache");
        using (var writer = new JournalDeltaStore(worker, identity, tree, optimizeJournal: false))
            writer.SetMetadata(writer.Stat("file")!.Id, 32, 1, 1, 2);
        JournalDeltaStore.ExportCandidate(worker, identity, tree, candidate, "combined", ["Library"]);
        var selection = new SourceViewSelection(candidate, cache, ["Library"]);
        DeltaEntry expectedSource, expectedCache;
        using (var view = new SourceViewDeltaStore(source, identity, tree, selection))
        {
            Guid a = view.Stat("file")!.Id;
            view.Create("Library", true, 16); Guid b = view.Create("Library/data", false, 32).Id;
            view.Write(b, 0, [91, 92, 93]);
            view.SetLengthAndMetadata(a, 4, 32, 101, 102, 103);
            view.SetLengthAndMetadata(b, 0, 33, 201, 202, 203);
            expectedSource = view.Get(a); expectedCache = view.Get(b);
        }
        using var replay = new SourceViewDeltaStore(source, identity, tree, selection);
        Assert.Equal(expectedSource, replay.Stat("file")); Assert.Equal(expectedCache, replay.Stat("Library/data"));
        byte[] actual = new byte[4]; Assert.Equal(4, replay.Read(expectedSource.Id, 0, actual));
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, actual); Assert.Equal(0, replay.Read(expectedCache.Id, 0, new byte[3]));
    }

    private static byte[] Read(JournalDeltaStore store, Guid id)
    {
        byte[] bytes = new byte[checked((int)store.Get(id).Length)];
        Assert.Equal(bytes.Length, store.Read(id, 0, bytes)); return bytes;
    }
    private static byte[] HashJournal(string directory)
    {
        using var input = new FileStream(System.IO.Path.Combine(directory, "delta.journal"), FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        return SHA256.HashData(input);
    }
    private sealed class Observer : IJournalMutationObserver
    {
        internal List<JournalMutationBoundary> Events { get; } = [];
        internal string? FailAt { get; set; }
        internal string? FailOperation { get; set; }
        internal Action<JournalMutationBoundary>? Callback { get; set; }
        public void Observe(JournalMutationBoundary boundary)
        {
            Events.Add(boundary); Callback?.Invoke(boundary);
            if (boundary.Boundary == FailAt && (FailOperation is null || boundary.Operation == FailOperation)) throw new IOException("Injected combined mutation interruption.");
        }
    }
    private sealed class Fixture : IDisposable
    {
        private readonly TestDirectory directory = new();
        private readonly string mode;
        private readonly OverlayIdentity identity;
        internal string Path => directory.Path;
        internal SnapshotOverlayTests.FakeSnapshotTree Tree { get; } = new();
        internal Observer Observer { get; } = new();
        internal byte[] Original { get; } = Enumerable.Range(1, 16).Select(i => (byte)i).ToArray();
        internal Fixture(string mode, long budget = 64L * 1048576)
        {
            this.mode = mode; Tree.Files["file"] = Original.ToArray();
            identity = new(2, "combined-resize-metadata", Tree.Snapshot, budget, 4096);
        }
        internal JournalDeltaStore Open() => new(Path, identity, Tree, Observer, optimizeJournal: mode == "latest",
            boundedLogBytes: mode == "bounded" ? 8192 : null);
        public void Dispose() => directory.Dispose();
    }
}
