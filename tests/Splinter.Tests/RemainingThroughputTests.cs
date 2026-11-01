using Splinter.Core;
using Splinter.Runtime;
using Splinter.Platform.Windows;
using Splinter.SourceControl.Files;

namespace Splinter.Tests;

public sealed class RemainingThroughputTests
{
    [Fact(Timeout = 10000)]
    public async Task NativeCreatesShareDurabilityBeforeRetainingHandles()
    {
        using var files = new TestDirectory(); var basis = new SnapshotOverlayTests.FakeSnapshotTree(); var ct = TestContext.Current.CancellationToken;
        using var pause = new CreatePause(); var identity = new OverlayIdentity(3, "create-queue", basis.Snapshot, 4096, 65536);
        using var writer = new JournalDeltaStore(files.Path, identity, basis, pause); var fs = new WinFspOverlayFileSystem(writer);
        long flushes = writer.Observe().Metrics!.FlushCount; pause.Armed = true;
        Task<(object Node, object Desc)> Call(int i) => Task.Factory.StartNew(() =>
        {
            Assert.Equal(0, fs.Create("file-" + i, 0, 0, 32, [], 0, out var node, out var desc, out _, out _));
            return (node, desc);
        }, ct, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        var first = Call(0); Task<(object Node, object Desc)>[] others = [];
        try
        {
            Assert.True(pause.Entered.Wait(TimeSpan.FromSeconds(3), ct)); others = Enumerable.Range(1, 8).Select(Call).ToArray();
            Assert.True(SpinWait.SpinUntil(() => fs.PendingMetadataCount == 8, TimeSpan.FromSeconds(3)));
            Assert.Equal(0, fs.OpenHandles); Assert.Equal(0, fs.RetainedFileContexts);
        }
        finally { pause.Proceed.Set(); await first; await Task.WhenAll(others); }
        Assert.Equal(flushes + 2, writer.Observe().Metrics!.FlushCount); Assert.Equal(9, fs.OpenHandles);
        Assert.NotEqual(0, fs.Create("file-0", 0, 0, 32, [], 0, out _, out _, out _, out _)); Assert.Equal(9, fs.OpenHandles);
        foreach (var item in others.Prepend(first)) { var handle = await item; fs.Close(handle.Node, handle.Desc); }
        Assert.Equal(0, fs.OpenHandles); Assert.Equal(0, fs.RetainedFileContexts);
        TestContext.Current.TestOutputHelper!.WriteLine("9 native create callbacks: 2 durable flushes; no pre-ACK handles; all contexts released");
    }

    [Fact(Timeout = 10000)]
    public void FailedNativeCreateDoesNotRetainUnacknowledgedHandles()
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        using var files = new TestDirectory(); var basis = new SnapshotOverlayTests.FakeSnapshotTree(); using var observer = new CreatePause { Fail = true };
        using var writer = new JournalDeltaStore(files.Path, new(3, "create-failure", basis.Snapshot, 4096, 65536), basis, observer);
        var fs = new WinFspOverlayFileSystem(writer); observer.Armed = true;
        Assert.Throws<IOException>(() => fs.Create("failed", 0, 0, 32, [], 0, out _, out _, out _, out _));
        Assert.Equal(0, fs.OpenHandles); Assert.Equal(0, fs.RetainedFileContexts);
        Assert.Equal("DeltaReconnectRequired", Assert.Throws<WorkspaceException>(() => writer.Stat("failed")).Code);
    }

    private sealed class CreatePause : IJournalMutationObserver, IDisposable
    {
        internal bool Armed, Fail;
        internal readonly ManualResetEventSlim Entered = new(), Proceed = new();
        public void Observe(JournalMutationBoundary point)
        {
            if (!Armed || point.Boundary != "commit.before-flush") return;
            Armed = false; if (Fail) throw new IOException("create-flush-failed");
            Entered.Set(); if (!Proceed.Wait(TimeSpan.FromSeconds(3))) throw new TimeoutException("Release the small create check.");
        }
        public void Dispose() { Entered.Dispose(); Proceed.Dispose(); }
    }

    [Theory(Timeout = 10000)]
    [InlineData(false)] [InlineData(true)]
    public async Task PublicationGroupsWaitForDurabilityAndPoisonFailures(bool fail)
    {
        using var files = new TestDirectory(); var ct = TestContext.Current.CancellationToken;
        var template = new OriginalApplyPlan(3, new string('a', 24), "request", "basis", "result", files.Path, "root", [], [], "");
        var plan = OriginalApplyPages.Create(template, Enumerable.Range(0, 9).Select(i => new OriginalApplyChange("file-" + i,
            null, new("file", 0, new string('a', 64), 32, 0), null, false)), files.Path, ct);
        var state = OriginalApplyPages.Initialize(plan, "fixed", files.Path); using var publication = OriginalApplyPages.BeginPublication(state);
        using var firstEntered = new ManualResetEventSlim(); using var releaseFirst = new ManualResetEventSlim();
        using var secondEntered = new ManualResetEventSlim(); using var releaseSecond = new ManualResetEventSlim();
        int saves = 0; var durableRoot = state.StepsRoot; var sizes = new List<int>();
        var queue = new OriginalPublicationCommitQueue(state, () =>
        {
            int current = Interlocked.Increment(ref saves); var entered = current == 1 ? firstEntered : secondEntered;
            var release = current == 1 ? releaseFirst : releaseSecond; entered.Set();
            Assert.True(release.Wait(TimeSpan.FromSeconds(3), ct));
            if (current == 2 && fail) throw new IOException("persist-failed");
            durableRoot = state.StepsRoot;
        }, count => sizes.Add(count));
        Task Call(int index) => Task.Factory.StartNew(() => queue.Commit(index, new() { Phase = "done" }),
            ct, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        Task first = Call(0); Task[] rest = [];
        try
        {
            Assert.True(firstEntered.Wait(TimeSpan.FromSeconds(3), ct)); rest = Enumerable.Range(1, 8).Select(Call).ToArray();
            Assert.True(SpinWait.SpinUntil(() => queue.PendingCount == 8, TimeSpan.FromSeconds(3)));
            Assert.False(first.IsCompleted); Assert.All(rest, task => Assert.False(task.IsCompleted));
            releaseFirst.Set(); Assert.True(secondEntered.Wait(TimeSpan.FromSeconds(3), ct));
            Assert.All(rest, task => Assert.False(task.IsCompleted));
        }
        finally
        {
            releaseFirst.Set(); releaseSecond.Set(); await first;
            if (fail) foreach (var task in rest) Assert.Equal("persist-failed", (await Assert.ThrowsAsync<IOException>(() => task)).Message);
            else await Task.WhenAll(rest);
        }
        Assert.Equal(2, saves);
        if (fail) Assert.Throws<IOException>(() => queue.Commit(1, new() { Phase = "done" }));
        else Assert.Equal(new[] { 1, 8 }, sizes);
        publication.Dispose();
        var recovered = new OriginalApplyState { Plan = plan, IntentKey = "fixed", Steps = [], StepsRoot = durableRoot };
        OriginalApplyPages.Bind(recovered, files.Path);
        Assert.Equal("done", recovered.StepAt(0).Phase);
        for (int i = 1; i < 9; i++) Assert.Equal(fail ? "pending" : "done", recovered.StepAt(i).Phase);
        TestContext.Current.TestOutputHelper!.WriteLine($"9 progress boundaries: persist calls=2; injectedFailure={fail}");
    }

    [Fact(Timeout = 10000)]
    public void InitialNamespaceBatchReducesPagesAndKeepsNestedReadsAndReplay()
    {
        using var files = new TestDirectory(); var basis = new SnapshotOverlayTests.FakeSnapshotTree(); var ct = TestContext.Current.CancellationToken;
        long Run(string name, bool batch)
        {
            string path = Path.Combine(files.Path, name); var identity = new OverlayIdentity(3, "namespace-cost", basis.Snapshot, 4096, 65536);
            using (var writer = new JournalDeltaStore(path, identity, basis))
            {
                var actions = new List<Action> { () => writer.Create("new", true, 16) };
                for (int i = 0; i < 32; i++)
                {
                    string child = "new/file-" + i;
                    actions.Add(() => { ct.ThrowIfCancellationRequested(); var created = writer.Create(child, false, 32); Assert.Equal(created.Id, writer.Stat(child)!.Id); });
                }
                if (batch) writer.ApplyMetadataBatch(actions); else foreach (var action in actions) action();
                Assert.Equal(32, writer.List("new").Count);
                writer.ApplyMetadataBatch([() => writer.Create("other", true, 16), () => writer.Rename(writer.Stat("new/file-0")!.Id, "other/moved", false),
                    () => writer.Delete(writer.Stat("new/file-1")!.Id), () => writer.Create("new/replacement", false, 32)]);
                Assert.NotNull(writer.Stat("other/moved")); Assert.Null(writer.Stat("new/file-1"));
            }
            using (var replay = new JournalDeltaStore(path, identity, basis))
            { Assert.Equal(31, replay.List("new").Count); Assert.NotNull(replay.Stat("other/moved")); Assert.NotNull(replay.Stat("new/replacement")); }
            return Directory.EnumerateFiles(path, "pages-*.bin", SearchOption.AllDirectories).Sum(file => new FileInfo(file).Length);
        }
        long serial = Run("serial", false), batched = Run("batched", true);
        Assert.True(batched < serial * 0.85, $"serial={serial}, batched={batched}");
        TestContext.Current.TestOutputHelper!.WriteLine($"32 initial files/nested rename/delete/replay: metadata bytes {serial} -> {batched}");
    }

    [Theory(Timeout = 10000)]
    [InlineData(3)] [InlineData(129)]
    public void NamespaceRootExportInsideBatchKeepsItsCursorAndDeepPathFallback(int depth)
    {
        using var files = new TestDirectory(); var basis = new SnapshotOverlayTests.FakeSnapshotTree(); var ct = TestContext.Current.CancellationToken;
        string parent = string.Join('/', Enumerable.Range(0, depth).Select(i => "d" + i)); basis.Files[parent + "/basis"] = [];
        var identity = new OverlayIdentity(3, "namespace-export", basis.Snapshot, 4096, 65536);
        using (var writer = new JournalDeltaStore(files.Path, identity, basis))
        {
            IEnumerable<DeltaEntry>? fixedListing = null;
            writer.ApplyMetadataBatch([() => writer.Create(parent + "/first", false, 32),
                () => { ct.ThrowIfCancellationRequested(); fixedListing = writer.EnumerateChildren(parent); },
                () => writer.Create(parent + "/later", false, 32)]);
            Assert.Equal(new[] { parent + "/basis", parent + "/first" }, fixedListing!.Select(entry => entry.Path).Order());
            Assert.Equal(3, writer.List(parent).Count);
        }
        using var replay = new JournalDeltaStore(files.Path, identity, basis);
        Assert.NotNull(replay.Stat(parent + "/first")); Assert.NotNull(replay.Stat(parent + "/later"));
    }

    [Theory(Timeout = 10000)]
    [InlineData(0)] [InlineData(1024)] [InlineData(1048576)]
    public void GraphRepackKeepsLogicalRootsWithMemoryExhaustionAndDiskFallback(int memoryLimit)
    {
        using var files = new TestDirectory(); var ct = TestContext.Current.CancellationToken;
        int passes = 0, memory = 0, diskReads = 0, cacheHits = 0;
        using var source = new PagedMetadataStore(Path.Combine(files.Path, "source"), true, point =>
        {
            if (point == "metadata.repack.reachability-disk-pass") passes++;
            if (point == "metadata.repack.reachability-memory") memory++;
            if (point == "metadata.repack.mapping-disk-read") diskReads++;
            if (point == "metadata.repack.mapping-memory-hit") cacheHits++;
        });
        using var target = new PagedMetadataStore(Path.Combine(files.Path, "target"), true);
        var shared = source.Build(Enumerable.Range(0, 128).Select(i => new MetadataEntry(i.ToString("D4"), BitConverter.GetBytes(i))), ct);
        var first = source.Set(source.Empty, "first", "one"u8, shared); var last = source.Set(first, "last", "two"u8, shared); source.Flush();
        var roots = source.CopyRootsToBounded(target, [first, last, first, source.Empty], files.Path, ct, memoryLimit).ToArray();
        Assert.Equal(new[] { first.ContentHash, last.ContentHash, first.ContentHash, source.Empty.ContentHash }, roots.Select(root => root.ContentHash));
        target.Open(roots[1]).Verify(ct);
        Assert.Equal(target.Open(roots[0]).FindEntry("first")!.Child, target.Open(roots[1]).FindEntry("last")!.Child);
        if (memoryLimit == 1048576) { Assert.Equal(1, memory); Assert.Equal(0, passes); Assert.Equal(0, diskReads); Assert.True(cacheHits > 100); }
        else { Assert.Equal(0, memory); Assert.True(passes > 1); Assert.True(diskReads > 100); }
        Assert.Empty(Directory.EnumerateDirectories(files.Path, "repack-*"));
        TestContext.Current.TestOutputHelper!.WriteLine($"128 shared rows; budget={memoryLimit}; reachability passes={passes}; mapping disk reads={diskReads}; memory hits={cacheHits}");
    }

    [Theory(Timeout = 10000)]
    [InlineData(4)] [InlineData(96)]
    public void AppliedHistoryMergeAndRetainedParentSweepMatchReferenceRules(int count)
    {
        using var files = new TestDirectory(); int writes = 0;
        using var work = new FileApplyIndex(files.Path, point => { if (point == "metadata.page-header-written") writes++; });
        var ct = TestContext.Current.CancellationToken;
        FileSourceEntry Entry(string path) => new(path, "file", 1, new string('a', 64), 0, 32);
        var source = Enumerable.Range(0, 128).Select(i => Entry("dir/nested/file-" + i)).ToArray();
        var before = work.Build(source, ct); var changed = Enumerable.Range(0, count).Select(i => new AppliedFilePath("dir/nested/file-" + i,
            i % 3 == 0 ? null : new("file", 2, new string('b', 64), 32, 7))).Append(new("other/new", new("file", 0, new string('c', 64), 32, 0))).ToArray();
        // Equalize imported path names to isolate index-update page costs.
        _ = work.BuildPaths(changed.Select(item => item.Path), ct); writes = 0; var expected = before;
        foreach (var item in changed) expected = item.Version is null ? work.Remove(expected, item.Path) : work.Set(expected,
            new(item.Path, item.Version.Kind, item.Version.Length, item.Version.Hash, item.Version.ModifiedUtcTicks, item.Version.Attributes));
        int serialWrites = writes; writes = 0;
        var after = work.ApplyVersions(before, changed, ct); int mergedWrites = writes; Assert.Equal(expected.ContentHash, after.ContentHash);
        if (count == 96) Assert.True(mergedWrites < serialWrites / 2, $"serial={serialWrites}, merged={mergedWrites}");
        TestContext.Current.TestOutputHelper!.WriteLine($"128 basis files / {changed.Length} applied entries: page writes {serialWrites} -> {mergedWrites}");
        var original = work.Build(source.Append(Entry("foreign/child")), ct);
        var included = work.BuildPaths(source.Take(32).Select(entry => entry.Path), ct);
        var wanted = new HashSet<string>();
        foreach (var entry in work.Entries(original))
            if (work.Contains(after, entry.Path) || !work.Contains(before, entry.Path) || !work.Contains(included, entry.Path))
            { string path = entry.Path; for (int slash; (slash = path.LastIndexOf('/')) >= 0;) { path = path[..slash]; wanted.Add(path); } }
        Assert.Equal(wanted.Order(), work.Paths(work.RetainedParents(original, before, after, included, ct)).Order());
        Assert.Equal(128, work.Entries(before).Count());
    }
}
