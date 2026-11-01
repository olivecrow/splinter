using System.Text;
using Splinter.Core;
using Splinter.Runtime;
using Splinter.SourceControl.Files;

namespace Splinter.Tests;

public sealed class ResourceThroughputTests
{
    [Fact(Timeout = 10000)]
    public void InMemorySortObservesCancellationBetweenReturnedRows()
    {
        using var files = new TestDirectory(); using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var budget = new ScratchBudget(1024 * 1024);
        using var cursor = MetadataExternalSort.Order([new("b", "2"u8.ToArray()), new("a", "1"u8.ToArray())],
            files.Path, stop.Token, null, 1024 * 1024, budget).GetEnumerator();
        Assert.True(cursor.MoveNext()); Assert.Equal("a", cursor.Current.Key); stop.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => cursor.MoveNext());
        Assert.Equal(0, budget.Used); Assert.Empty(Directory.EnumerateFileSystemEntries(files.Path));
    }

    [Fact(Timeout = 10000)]
    public void FileInspectionMergeRetainsAddDeleteCaseAndMetadataChanges()
    {
        using var files = new TestDirectory(); var ct = TestContext.Current.CancellationToken;
        FileSourceEntry Entry(string path) => new(path, "file", 1, new string('a', 64), 0, 32);
        FileSourceEntry[] before = [Entry("same"), Entry("deleted"), Entry("meta"), Entry("Case")];
        FileSourceEntry[] after = [Entry("same"), Entry("added"), Entry("meta") with { LastWriteUtcTicks = 1 }, Entry("case")];
        var result = FileWorkspaceProvider.Changed(before, after, files.Path, ct);
        Assert.Equal(4, result.Count); Assert.Equal(new[] { "added", "case", "deleted", "meta" }, result.Preview.Order(StringComparer.Ordinal));
        Assert.Equal(0, FileWorkspaceProvider.Changed(before, before, files.Path, ct).Count);
    }

    [Fact]
    public void ResourceCeilingsLeaveHeadroomAndScratchDoesNotReserveMemory()
    {
        Assert.Equal(4L << 30, StorageResources.Target(64L << 30));
        Assert.Equal(512L << 20, StorageResources.Target(4L << 30));
        Assert.InRange(StorageResources.MetadataCacheBytes + StorageResources.ScratchBytes, 1, StorageResources.WorkingTargetBytes);
        Assert.InRange(StorageResources.HashWorkers, 1, 32); Assert.InRange(StorageResources.IoWorkers, 8, 32);
        var budget = new ScratchBudget(1024); using var first = budget.Open(); using var second = budget.Open();
        Assert.Equal(0, budget.Used); Assert.True(first.TryResize(768)); Assert.False(second.TryResize(512));
        Assert.True(first.TryResize(128)); Assert.True(second.TryResize(512)); Assert.Equal(640, budget.Used);
        first.Dispose(); second.Dispose(); Assert.Equal(0, budget.Used);
        Assert.Equal(4096, StorageResources.PublicationStagingBytes(4096, 0));
        Assert.Equal(long.MaxValue, StorageResources.PublicationStagingBytes(long.MaxValue, long.MaxValue));
        Assert.True(StorageResources.PublicationStagingBytes(32, 1024 * 1024) > 32);
        TestContext.Current.TestOutputHelper!.WriteLine($"target={StorageResources.WorkingTargetBytes}; metadata={StorageResources.MetadataCacheBytes}; scratch={StorageResources.ScratchBytes}; hash={StorageResources.HashWorkers}; io={StorageResources.IoWorkers}; readers={StorageResources.MetadataReaders}");
    }

    [Fact(Timeout = 10000)]
    public void SpoolsUseMemoryOnDemandAndSpillWhenTheAggregateBudgetIsBusy()
    {
        using var files = new TestDirectory(); var budget = new ScratchBudget(1024 * 1024); var ct = TestContext.Current.CancellationToken;
        using var first = new MetadataSpool(files.Path, 1024 * 1024, budget);
        using var second = new MetadataSpool(files.Path, 1024 * 1024, budget);
        for (int i = 0; i < 8; i++) { ct.ThrowIfCancellationRequested(); first.Append(new(i.ToString(), new byte[32768])); }
        Assert.Empty(Directory.EnumerateFiles(files.Path)); Assert.InRange(budget.Used, 1, 1024 * 1024);
        for (int i = 0; i < 24; i++) { ct.ThrowIfCancellationRequested(); second.Append(new(i.ToString(), new byte[32768])); }
        Assert.Single(Directory.EnumerateFiles(files.Path, "metadata-spool-*.bin"));
        Assert.Equal(8, first.Read().Count()); Assert.Equal(24, second.Read().Count());
        Assert.Equal(24, second.Read().Count()); first.Dispose(); second.Dispose();
        Assert.Equal(0, budget.Used); Assert.Empty(Directory.EnumerateFiles(files.Path));
    }

    [Fact(Timeout = 15000)]
    public void MoreSortMemoryEliminatesRunFilesForTheSameSmallInput()
    {
        using var files = new TestDirectory(); var ct = TestContext.Current.CancellationToken;
        (int Runs, string[] Keys) Run(string name, int limit)
        {
            string path = Path.Combine(files.Path, name); Directory.CreateDirectory(path); int runs = 0;
            IEnumerable<MetadataEntry> Input()
            {
                for (int i = 5999; i >= 0; i--)
                {
                    ct.ThrowIfCancellationRequested();
                    if (i == 0) runs = Directory.EnumerateFiles(path, "run-*.bin", SearchOption.AllDirectories).Count();
                    yield return new(i.ToString("D6"), new byte[1024]);
                }
            }
            var budget = new ScratchBudget(64L * 1024 * 1024);
            var keys = MetadataExternalSort.Order(Input(), path, ct, null, limit, budget).Select(entry => entry.Key).ToArray();
            Assert.Equal(0, budget.Used); Assert.Empty(Directory.EnumerateFileSystemEntries(path)); return (runs, keys);
        }
        var before = Run("small", 4 * 1024 * 1024); var after = Run("larger", 32 * 1024 * 1024);
        Assert.Equal(before.Keys, after.Keys); Assert.True(before.Runs > 0); Assert.Equal(0, after.Runs);
        TestContext.Current.TestOutputHelper!.WriteLine($"6000 records / 1KiB values: runs before final row {before.Runs} -> {after.Runs}");
    }

    [Fact(Timeout = 10000)]
    public void NestedSortWithAnExhaustedPoolGroupsRecordsWithoutWaiting()
    {
        using var files = new TestDirectory(); var budget = new ScratchBudget(1024); using var held = budget.Open();
        Assert.True(held.TryResize(1024)); int runs = 0; var ct = TestContext.Current.CancellationToken;
        IEnumerable<MetadataEntry> Input()
        {
            for (int i = 255; i >= 0; i--)
            {
                ct.ThrowIfCancellationRequested();
                if (i == 0) runs = Directory.EnumerateFiles(files.Path, "run-*.bin", SearchOption.AllDirectories).Count();
                yield return new(i.ToString("D4"), "x"u8.ToArray());
            }
        }
        var rows = MetadataExternalSort.Order(Input(), files.Path, ct, null, 1024 * 1024, budget).ToArray();
        Assert.Equal(256, rows.Length); Assert.InRange(runs, 1, 4); Assert.Equal(1024, budget.Used);
    }

    [Fact(Timeout = 10000)]
    public void InsertBatchWritesOnlySurvivingPagesAndMatchesSequentialRoots()
    {
        using var files = new TestDirectory(); int pages = 0; var ct = TestContext.Current.CancellationToken;
        using var store = new PagedMetadataStore(files.Path, true, point => { if (point == "metadata.page-header-written") pages++; });
        var entries = Enumerable.Range(0, 64).Select(i => new MetadataEntry(i.ToString("D4"), Encoding.UTF8.GetBytes("value-" + i))).ToArray();
        var serial = store.Empty;
        foreach (var entry in entries) { ct.ThrowIfCancellationRequested(); serial = store.Set(serial, entry.Key, entry.Value.Span); }
        int before = pages; pages = 0; var batch = store.SetBatch(store.Empty, entries);
        Assert.Equal(serial.ContentHash, batch.ContentHash); Assert.Equal(64, pages); Assert.True(before > pages * 2);
        TestContext.Current.TestOutputHelper!.WriteLine($"64 inserted keys: page writes {before} -> {pages}");
        var mixed = Enumerable.Range(0, 64).Select(i => new MetadataEntry((i * 2).ToString("D4"), "changed"u8.ToArray())).ToArray();
        var expected = batch;
        foreach (var entry in mixed) expected = store.Set(expected, entry.Key, entry.Value.Span);
        Assert.Equal(expected.ContentHash, store.SetBatch(batch, mixed).ContentHash);
        Assert.Equal("value-0", Encoding.UTF8.GetString(store.Open(batch).Find("0000")!));
    }

    [Fact(Timeout = 10000)]
    public void PipelineDoesNotWaitForASlowFirstFileAndDisposalDrainsIt()
    {
        var ct = TestContext.Current.CancellationToken; int active = 0; bool inputDisposed = false;
        using var first = new ManualResetEventSlim();
        IEnumerable<int> Input() { try { for (int i = 0; i < 64; i++) yield return i; } finally { inputDisposed = true; } }
        int Map(int value, CancellationToken token)
        {
            Interlocked.Increment(ref active);
            try
            {
                if (value == 0) { first.Set(); token.WaitHandle.WaitOne(); token.ThrowIfCancellationRequested(); }
                Assert.True(first.Wait(TimeSpan.FromSeconds(3), token)); return value;
            }
            finally { Interlocked.Decrement(ref active); }
        }
        int result = BoundedParallel.Select(Input(), Map, 4, ct).First();
        Assert.NotEqual(0, result); Assert.Equal(0, active); Assert.True(inputDisposed);
    }

    [Theory(Timeout = 10000)]
    [InlineData(false)] [InlineData(true)]
    public void PipelineFailureKeepsTheCauseAndJoinsAdmittedWorkers(bool inputFailure)
    {
        var ct = TestContext.Current.CancellationToken; int active = 0; bool disposed = false;
        IEnumerable<int> Input()
        {
            try { for (int i = 0; i < 32; i++) { if (inputFailure && i == 4) throw new WorkspaceException("InputFailure", "injected"); yield return i; } }
            finally { disposed = true; }
        }
        int Map(int value, CancellationToken token)
        {
            Interlocked.Increment(ref active);
            try { if (!inputFailure && value == 4) throw new WorkspaceException("WorkerFailure", "injected"); token.ThrowIfCancellationRequested(); return value; }
            finally { Interlocked.Decrement(ref active); }
        }
        Assert.Equal(inputFailure ? "InputFailure" : "WorkerFailure",
            Assert.Throws<WorkspaceException>(() => BoundedParallel.Select(Input(), Map, 4, ct).ToArray()).Code);
        Assert.Equal(0, active); Assert.True(disposed);
    }

    [Fact(Timeout = 10000)]
    public void PagedListingUsesItsExistingMetadataInsteadOfStattingEveryFile()
    {
        using var files = new TestDirectory(); var basis = new CountingTree(); var ct = TestContext.Current.CancellationToken;
        for (int i = 0; i < 32; i++) basis.Inner.Files["file-" + i.ToString("D3")] = [];
        using var journal = new JournalDeltaStore(files.Path, new(3, "listing-cost", basis.Snapshot, 4096, 65536), basis);
        basis.Stats = 0; ct.ThrowIfCancellationRequested();
        Assert.Equal(32, journal.EnumerateChildren("").Count()); Assert.Equal(0, basis.Stats);
        basis.Inner.Available = false;
        Assert.Throws<WorkspaceException>(() => journal.EnumerateChildren("").ToArray());
    }
    private sealed class CountingTree : ISnapshotTree
    {
        internal readonly SnapshotOverlayTests.FakeSnapshotTree Inner = new();
        internal int Stats;
        public SnapshotReference Snapshot => Inner.Snapshot;
        public void EnsureAvailable() => Inner.EnsureAvailable();
        public SnapshotEntry? Stat(string path) { Stats++; return Inner.Stat(path); }
        public IReadOnlyList<SnapshotEntry> List(string path) => Inner.List(path);
        public int Read(string path, long offset, Span<byte> target) => Inner.Read(path, offset, target);
    }
}
