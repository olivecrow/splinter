using System.Text;
using Splinter.Core;
using Splinter.Runtime;
using Splinter.SourceControl.Git;

namespace Splinter.Tests;

public sealed class StructuralThroughputTests
{
    [Fact(Timeout = 10000)]
    public void ProductJournalBatchReducesMetadataBytesForTheSameMutations()
    {
        using var files = new TestDirectory(); var basis = new SnapshotOverlayTests.FakeSnapshotTree();
        string[] paths = Enumerable.Range(0, 32).Select(i => "file-" + i).ToArray();
        foreach (string path in paths) basis.Files[path] = [];
        long Run(string name, bool mutate, bool batch)
        {
            string directory = Path.Combine(files.Path, name);
            using (var writer = new JournalDeltaStore(directory, new(3, "batch-cost", basis.Snapshot, 4096, 65536), basis))
            {
                Guid[] ids = paths.Select(path => writer.Stat(path)!.Id).ToArray();
                var mutations = ids.Select<Guid, Action>(id => () =>
                {
                    TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
                    writer.SetLength(id, 5); writer.SetMetadata(id, 32, 1, 2, 3);
                }).ToArray();
                if (mutate) { if (batch) writer.ApplyMetadataBatch(mutations); else foreach (var mutation in mutations) mutation(); }
            }
            return Directory.EnumerateFiles(directory, "pages-*.bin", SearchOption.AllDirectories).Sum(path => new FileInfo(path).Length);
        }
        long baseline = Run("baseline", false, false), serial = Run("serial", true, false) - baseline, batch = Run("batch", true, true) - baseline;
        TestContext.Current.TestOutputHelper!.WriteLine($"32 files / 64 mutations: serial metadata bytes={serial}, batch metadata bytes={batch}");
        Assert.True(batch > 0 && batch < serial * 0.8, $"serial={serial}, batch={batch}");
    }

    [Fact(Timeout = 10000)]
    public void BatchRewritesSharedAncestorsOnceAndKeepsOldAndNestedRoots()
    {
        using var files = new TestDirectory(); int writes = 0;
        using var store = new PagedMetadataStore(files.Path, true, point => { if (point == "metadata.page-header-written") writes++; });
        var ct = TestContext.Current.CancellationToken;
        var original = store.Build(Enumerable.Range(0, 256).Select(i => new MetadataEntry(i.ToString("D4"), "old"u8.ToArray())), ct);
        var child = store.Set(store.Empty, "child", "nested"u8);
        var updates = Enumerable.Range(0, 64).Select(i => new MetadataEntry((i * 4).ToString("D4"), "new"u8.ToArray(), child)).ToArray();
        writes = 0; var serial = original;
        foreach (var item in updates) serial = store.Set(serial, item.Key, item.Value.Span, item.Child);
        int serialWrites = writes; writes = 0;
        var batch = store.SetBatch(original, updates); int batchWrites = writes;
        TestContext.Current.TestOutputHelper!.WriteLine($"256 nodes / 64 updates: serial pages={serialWrites}, batch pages={batchWrites}");
        Assert.Equal(serial.ContentHash, batch.ContentHash); Assert.True(batchWrites * 2 < serialWrites);
        foreach (var item in updates)
        {
            Assert.Equal("old", Encoding.UTF8.GetString(store.Open(original).Find(item.Key)!));
            Assert.Equal(child, store.Open(batch).FindEntry(item.Key)!.Child);
        }
        writes = 0; Assert.Equal(batch, store.SetBatch(batch, updates)); Assert.Equal(0, writes);
        var inserted = store.SetBatch(batch, [new("new-key", "insert"u8.ToArray()), updates[0]]);
        Assert.Equal("insert", Encoding.UTF8.GetString(store.Open(inserted).Find("new-key")!));
        Assert.Throws<WorkspaceException>(() => store.SetBatch(batch, [updates[0], updates[0]]));
    }

    [Theory(Timeout = 10000)]
    [InlineData(false)] [InlineData(true)]
    public void PagedBatchKeepsReadYourWritesAndDurableReplay(bool fail)
    {
        using var root = new TestDirectory(); var basis = new SnapshotOverlayTests.FakeSnapshotTree();
        var identity = new OverlayIdentity(3, "structural-batch", basis.Snapshot, 4096, 65536);
        Guid[] ids;
        using (var writer = new JournalDeltaStore(root.Path, identity, basis))
        {
            ids = Enumerable.Range(0, 16).Select(i => writer.Create("file-" + i, false, 32).Id).ToArray();
            var mutations = ids.Select<Guid, Action>(id => () =>
            {
                TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
                writer.SetLength(id, 5); Assert.Equal(5, writer.Get(id).Length);
                writer.SetMetadata(id, 32, 1, 2, 3); Assert.Equal(3, writer.Get(id).LastWriteTime);
            }).ToList();
            if (fail) mutations.Add(() => throw new IOException("injected failure"));
            if (fail)
            {
                Assert.Throws<IOException>(() => writer.ApplyMetadataBatch(mutations));
                Assert.Equal("DeltaReconnectRequired", Assert.Throws<WorkspaceException>(() => writer.Get(ids[0])).Code);
            }
            else writer.ApplyMetadataBatch(mutations);
        }
        using var replay = new JournalDeltaStore(root.Path, identity, basis);
        foreach (var id in ids) { Assert.Equal(5, replay.Get(id).Length); Assert.Equal(3, replay.Get(id).LastWriteTime); }
    }

    [Fact(Timeout = 10000)]
    public void InterruptedBatchLeavesThePublishedRootReadableAndRetryable()
    {
        using var files = new TestDirectory(); bool armed = false; MetadataTreeRoot original;
        var ct = TestContext.Current.CancellationToken;
        using (var store = new PagedMetadataStore(files.Path, true, point =>
            { if (armed && point == "metadata.page-header-written") { armed = false; throw new IOException("torn batch tail"); } }))
        {
            original = store.Build(Enumerable.Range(0, 32).Select(i => new MetadataEntry(i.ToString("D4"), "old"u8.ToArray())), ct);
            store.Flush(); armed = true;
            Assert.Throws<IOException>(() => store.SetBatch(original, [new("0000", "new"u8.ToArray()), new("0016", "new"u8.ToArray())]));
        }
        using var reopened = new PagedMetadataStore(files.Path, true);
        Assert.Equal("old", Encoding.UTF8.GetString(reopened.Open(original).Find("0000")!));
        var retried = reopened.SetBatch(original, [new("0000", "new"u8.ToArray()), new("0016", "new"u8.ToArray())]);
        Assert.Equal("new", Encoding.UTF8.GetString(reopened.Open(retried).Find("0016")!));
    }

    [Fact(Timeout = 10000)]
    public void RepeatedPathImportsDoNotRebuildTheTreeAndNewPathsPreserveOldNames()
    {
        using var files = new TestDirectory(); using var store = new PagedMetadataStore(files.Path, true);
        var names = new PagedPathInterner(store); var ct = TestContext.Current.CancellationToken;
        string[] paths = Enumerable.Range(0, 64).Select(i => "dir/file-" + i).ToArray();
        names.Import(paths, ct); var before = names.Root;
        for (int i = 0; i < 3; i++) names.Import(paths.Reverse().Concat(paths), ct);
        Assert.Equal(1, names.ImportBuilds); Assert.Equal(before, names.Root);
        names.Import(["new/child", paths[0]], ct); Assert.Equal(2, names.ImportBuilds);
        foreach (string path in paths.Append("new/child")) Assert.Equal(path, names.Resolve(PagedPathInterner.Reference(path)));
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        Assert.Throws<OperationCanceledException>(() => names.Import(paths, cancelled.Token));
    }

    [Fact(Timeout = 10000)]
    public void GitCachedMergeHandlesComponentOrderingMissingPathsAndOmissions()
    {
        using var files = new TestDirectory(); var ct = TestContext.Current.CancellationToken;
        string[] paths = ["a.txt", "a/child", "b", "z"];
        var input = paths.Select(path => new DirectoryMetadataRecord(new(path, false, 1, 32, 0, 0, 0), new string('a', 64), SourceReference: new string('b', 40) + ":" + new string('c', 64)));
        var descriptor = PagedSnapshotCatalog.Build(files.Path, new("", true, 0, 16, 0, 0, 0),
            SnapshotCatalogInput.WithImplicitDirectories(input, files.Path, ct: ct), ct: ct);
        using var catalog = new PagedSnapshotCatalog(files.Path, descriptor);
        using var cursor = new GitWorkspaceProvider.CachedFileCursor(catalog, files.Path, ct);
        foreach (string path in paths.Take(3)) Assert.Equal(path, cursor.FindNext(path)!.Entry.Path);
        Assert.Null(cursor.FindNext("missing")); Assert.Equal(4, cursor.Complete());
        Assert.Throws<WorkspaceException>(() => cursor.FindNext("a.txt"));
    }
}
