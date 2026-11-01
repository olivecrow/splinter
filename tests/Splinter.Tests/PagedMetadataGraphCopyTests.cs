using Splinter.Core;
using Splinter.Runtime;

namespace Splinter.Tests;

public sealed class PagedMetadataGraphCopyTests
{
    [Fact]
    public void GraphCopyDropsUnreachablePagesAndPreservesSharedImmutableChildren()
    {
        using var root = new TestDirectory(); var ct = TestContext.Current.CancellationToken;
        string originalPath = Path.Combine(root.Path, "source"), targetPath = Path.Combine(root.Path, "destination");
        MetadataTreeRoot first, final;
        using (var original = new PagedMetadataStore(originalPath, writable: true))
        using (var destination = new PagedMetadataStore(targetPath, writable: true))
        {
            var shared = original.Build(Enumerable.Range(0, 1024).Select(i => new MetadataEntry(i.ToString("d6"), BitConverter.GetBytes(i))), ct);
            first = original.Set(original.Empty, "a", "first"u8, shared);
            var live = original.Set(first, "b", "second"u8, shared);
            for (int i = 0; i < 250; i++) live = original.Set(live, "b", BitConverter.GetBytes(i), shared);
            original.Flush(); long before = Bytes(originalPath);
            var copied = original.CopyRootsTo(destination, [first, live, original.Empty], root.Path, ct).ToArray();
            Assert.Equal(first.ContentHash, copied[0].ContentHash); Assert.Equal(live.ContentHash, copied[1].ContentHash); Assert.Equal(0, copied[2].Count);
            Assert.Equal(BitConverter.GetBytes(249), destination.Open(copied[1]).Find("b"));
            var a = destination.Open(copied[1]).FindEntry("a")!;
            var b = destination.Open(copied[1]).FindEntry("b")!;
            Assert.Equal(a.Child, b.Child); destination.Open(a.Child!).Verify(ct);
            Assert.True(Bytes(targetPath) < before); final = copied[1];
        }
        using var reopened = new PagedMetadataStore(targetPath); Assert.Equal(2, reopened.Open(final).Enumerate().LongCount());
        Assert.Empty(Directory.EnumerateDirectories(root.Path, "repack-*"));
    }
    [Fact]
    public void GraphCopyStreamsAMillionEntriesWithoutAnInMemoryRelocationDictionary()
    {
        using var root = new TestDirectory(); var ct = TestContext.Current.CancellationToken;
        using var source = new PagedMetadataStore(Path.Combine(root.Path, "source"), writable: true);
        using var target = new PagedMetadataStore(Path.Combine(root.Path, "target"), writable: true);
        const int count = 1048576;
        var tree = source.Build(Enumerable.Range(0, count).Select(i => new MetadataEntry(i.ToString("d8"), BitConverter.GetBytes(i))), ct); source.Flush();
        var result = source.CopyRootsTo(target, [tree], root.Path, ct).Single();
        Assert.Equal(tree.ContentHash, result.ContentHash); Assert.Equal(count, result.Count);
        Assert.Equal(BitConverter.GetBytes(count - 1), target.Open(result).Find((count - 1).ToString("d8")));
        Assert.Equal(count, target.Open(result).Enumerate().LongCount()); Assert.True(PagedMetadataStore.CachedMetadataBytes <= StorageResources.MetadataCacheBytes);
    }
    private static long Bytes(string directory) => Directory.EnumerateFiles(directory, "pages-*.bin").Sum(p => new FileInfo(p).Length);
}
