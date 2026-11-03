using Splinter.Core;
using Splinter.Runtime;

namespace Splinter.Tests;

public sealed class PagedSnapshotCatalogTests
{
    [Fact]
    public void RepeatedDirectoryCatalogSharesUnchangedPhysicalPages()
    {
        using var files = new TestDirectory(); string path = Path.Combine(files.Path, "metadata"); var ct = TestContext.Current.CancellationToken;
        DirectoryMetadataRecord[] entries = [new(Entry("A", true), null), new(Entry("A/one"), null), new(Entry("A/two"), null),
            new(Entry("B", true), null), new(Entry("B/one"), null)];
        var first = PagedSnapshotCatalog.Build(path, Entry("", true), entries, ct: ct);
        long bytes = PageBytes(); var repeated = PagedSnapshotCatalog.Build(path, Entry("", true), entries.Reverse(), ct: ct, previousCatalog: first);
        Assert.Equal(first, repeated); Assert.Equal(bytes, PageBytes());
        var next = PagedSnapshotCatalog.Build(path, Entry("", true), entries.Select(entry => entry.Entry.Path == "A/two"
            ? entry with { Entry = entry.Entry with { LastWriteTime = 104 } } : entry), ct: ct, previousCatalog: first);
        using var old = new PagedSnapshotCatalog(path, first); using var current = new PagedSnapshotCatalog(path, next);
        Assert.Equal(old.Find("B")!.Children, current.Find("B")!.Children);
        Assert.NotEqual(old.Find("A")!.Children, current.Find("A")!.Children);
        long PageBytes() => Directory.EnumerateFiles(path, "pages-*.bin").Sum(file => new FileInfo(file).Length);
    }

    private static SnapshotEntry Entry(string path, bool directory = false) => new(path, directory, directory ? 0 : 7,
        (uint)(directory ? FileAttributes.Directory : FileAttributes.Archive), 100, 101, 102, directory ? null : 0x1A4);

    [Fact]
    public void ArbitraryOrderHierarchyPreservesComponentIdentityReferencesAndCase()
    {
        using var scratch = new TemporaryMetadataDirectory();
        DirectoryMetadataRecord[] input =
        [
            new(Entry("A/Deep/File.txt"), "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", SourceReference: "fixed-blob"),
            new(Entry("A.txt"), null, SourceReference: "other"),
            new(Entry("A/Deep", true), null, SourceReference: "deep-tree"),
            new(Entry("A", true), null, SourceReference: "a-tree"),
            new(Entry("Empty", true), null)
        ];
        var root = PagedSnapshotCatalog.Build(scratch.Path, Entry("", true), input, ct: TestContext.Current.CancellationToken);
        using var catalog = new PagedSnapshotCatalog(scratch.Path, root);
        Assert.Equal(6, root.Count);
        Assert.Equal("A/Deep/File.txt", catalog.Find("a/deep/file.txt")!.Entry.Path);
        Assert.Equal("fixed-blob", catalog.Find("a/deep/file.txt")!.SourceReference);
        Assert.Equal("deep-tree", catalog.Find("A/Deep")!.SourceReference);
        Assert.Equal(["A", "A.txt", "Empty"], catalog.EnumerateChildren("").Select(e => e.Entry.Path));
        Assert.Equal(["A/Deep"], catalog.EnumerateChildren("a").Select(e => e.Entry.Path));
        Assert.Empty(catalog.EnumerateChildren("Empty"));
        Assert.Equal(6, catalog.Walk().LongCount()); catalog.Verify(TestContext.Current.CancellationToken);
    }

    [Theory]
    [InlineData("missing/file", "other")]
    [InlineData("Alias/file", "alias")]
    public void MissingOrAliasedParentsAreRejected(string file, string directory)
    {
        using var scratch = new TestDirectory();
        Assert.Throws<WorkspaceException>(() => PagedSnapshotCatalog.Build(Path.Combine(scratch.Path, "index"), Entry("", true),
            [new(Entry(file), null), new(Entry(directory, true), null)], ct: TestContext.Current.CancellationToken));
    }
}
