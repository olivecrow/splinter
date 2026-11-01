using System.Text;
using Splinter.Core;
using Splinter.Runtime;
using Splinter.SourceControl.Git;
using Splinter.Platform.Windows;
using Splinter.SourceControl.Files;

namespace Splinter.Tests;

public sealed class StorageThroughputTests
{
    [Fact]
    public void PayloadHashChecksCancellationAfterEachBoundedRead()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        using var input = new CancelingPayload(cancellation);
        Assert.ThrowsAny<OperationCanceledException>(() => PayloadHash.Sha256(input, cancellation.Token));
        Assert.Equal(1, input.Reads);
    }
    private sealed class CancelingPayload(CancellationTokenSource cancellation) : MemoryStream(new byte[2 * 1024 * 1024])
    {
        public int Reads;
        public override int Read(Span<byte> buffer)
        { Reads++; Assert.InRange(buffer.Length, 1, 1024 * 1024); int count = base.Read(buffer); cancellation.Cancel(); return count; }
    }

    [Fact(Timeout = 30000)]
    public void SortedGitPathsCanBePartiallyReadAndRepeatedAfterSpilling()
    {
        var ct = TestContext.Current.CancellationToken;
        var rows = Enumerable.Range(0, 140).Reverse().Select(i => new MetadataEntry("path-" + i.ToString("D4"), new byte[32768]));
        using var paths = new GitWorkspaceProvider.PagedStrings(rows, ct);
        Assert.Equal("path-0000", Assert.Single(paths.Paths().Take(1)));
        Assert.Equal(140, paths.Entries().Count());
        Assert.Equal("path-0139", paths.Paths().Last());
        // Dispose must reclaim all operation-owned scratch after partial reads.
    }

    [Fact(Timeout = 10000)]
    public async Task StopAwareWorkIgnoresForeignRequestsAndDrainsBeforeReturning()
    {
        using var root = new TestDirectory();
        File.WriteAllText(Path.Combine(root.Path, "stop.json"), "{\"token\":\"foreign\"}");
        using var entered = new ManualResetEventSlim(); using var release = new ManualResetEventSlim(); bool drained = false;
        Task work = WinFspOverlayServer.RunStopAwareAsync(root.Path, "owned", ct =>
        {
            try { Assert.False(ct.IsCancellationRequested); entered.Set(); release.Wait(TestContext.Current.CancellationToken); Assert.True(ct.WaitHandle.WaitOne(TimeSpan.FromSeconds(2))); ct.ThrowIfCancellationRequested(); }
            finally { drained = true; }
        });
        Assert.True(entered.Wait(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken));
        try { await WindowsProcessWorkspaceProvider.WriteAsync(Path.Combine(root.Path, "stop.json"), new { token = "owned" }, TestContext.Current.CancellationToken); }
        finally { release.Set(); }
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => work); Assert.True(drained);
    }

    [Fact]
    public void OriginalComparisonMergesOneRowPerPathAndPreservesCaseOnlyChanges()
    {
        using var root = new TestDirectory(); using var index = new FileApplyIndex(root.Path);
        FileSourceEntry Entry(string path, string hash) => new(path, "file", 1, hash, 0, 32);
        string hash = new('a', 64);
        var before = index.Build([Entry("same", hash), Entry("OldCase", hash), Entry("deleted", hash)], TestContext.Current.CancellationToken);
        var after = index.Build([Entry("same", hash), Entry("oldcase", hash), Entry("added", hash)], TestContext.Current.CancellationToken);
        var rows = index.Compare(before, after).ToArray(); Assert.Equal(4, rows.Length);
        Assert.Single(rows, row => row.Before?.Path == "OldCase" && row.After?.Path == "oldcase");
        Assert.Single(rows, row => row.Before?.Path == "deleted" && row.After is null);
        Assert.Single(rows, row => row.Before is null && row.After?.Path == "added");
        var paths = index.BuildImportedPaths(rows.Select(row => (row.After ?? row.Before)!.Path), TestContext.Current.CancellationToken);
        Assert.Equal(4, paths.Count); Assert.True(index.ContainsExactPath(paths, "oldcase"));
        var closure = index.BuildPaths(["a", "a", "Assets/item", "Assets/item"], TestContext.Current.CancellationToken, coalesce: true);
        Assert.Equal(2, closure.Count);
    }
    [Fact]
    public void SmallSpoolAvoidsFilesAndSpilledCursorsKeepTheirOwnSnapshot()
    {
        using var root = new TestDirectory(); using var spool = new MetadataSpool(root.Path);
        spool.Append(new("first", "one"u8.ToArray()));
        Assert.Empty(Directory.EnumerateFiles(root.Path));
        using var small = spool.Read().GetEnumerator(); Assert.True(small.MoveNext());
        var large = new byte[70_000]; large[65_535] = 123;
        spool.Append(new("large", large));
        Assert.False(small.MoveNext()); Assert.Single(Directory.EnumerateFiles(root.Path));
        using var first = spool.Read().GetEnumerator(); using var second = spool.Read().GetEnumerator();
        Assert.True(first.MoveNext()); Assert.True(second.MoveNext());
        spool.Append(new("last", "end"u8.ToArray()));
        Assert.True(first.MoveNext()); Assert.Equal(large, first.Current.Value.ToArray()); Assert.False(first.MoveNext());
        Assert.True(second.MoveNext()); Assert.Equal(large, second.Current.Value.ToArray()); Assert.False(second.MoveNext());
        Assert.Equal(new[] { "first", "large", "last" }, spool.Read().Select(entry => entry.Key));
        spool.Dispose(); Assert.Empty(Directory.EnumerateFiles(root.Path));
    }

    [Fact]
    public void DurableSmallSpoolSpillsAndRetainsWriterFence()
    {
        using var root = new TestDirectory(); using var spool = new MetadataSpool(root.Path);
        spool.Append(new("key", "value"u8.ToArray())); spool.FlushDurable();
        string path = Assert.Single(Directory.EnumerateFiles(root.Path));
        // The writer excludes foreign writes while the spool is live.
        Assert.Throws<IOException>(() => new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete));
        Assert.Equal("value"u8.ToArray(), Assert.Single(spool.Read()).Value.ToArray());
    }

    [Fact]
    public void GitAttributeBatchesPreserveNamesWithinCountAndUtf8Budgets()
    {
        string[] paths = Enumerable.Range(0, 5000).Select(i => "공백 이름/" + i.ToString("D6") + new string('a', 80)).ToArray();
        var batches = GitWorkspaceProvider.AttributeBatches(paths).ToArray();
        Assert.Equal(paths, batches.SelectMany(batch => batch)); Assert.InRange(batches.Length, 2, 3);
        Assert.All(batches, batch => { Assert.InRange(batch.Length, 1, 4096); Assert.InRange(batch.Sum(path => Encoding.UTF8.GetByteCount(path) + 1), 1, 256 * 1024); });
        Assert.Throws<WorkspaceException>(() => GitWorkspaceProvider.AttributeBatches(["a\0b"]).ToArray());
    }
}
