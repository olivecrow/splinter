using Splinter.Core;
using Splinter.Runtime;
using Splinter.SourceControl.Git;

namespace Splinter.Tests;

public sealed class GitSourceMembershipTests
{
    [Fact]
    public void ComparisonAndLazyLookupMatchTheMaterializedMarkerCatalog()
    {
        using var files = new TestDirectory(); var ct = TestContext.Current.CancellationToken;
        MetadataEntry[] input = [new("A/한글/leaf", new byte[] { 1, 2, 3 }), new("A", new byte[] { 4 }),
            new("A/한글", new byte[] { 5 }), new("a/leaf", new byte[] { 6 }), new("A", new byte[] { 4 })];
        using var paths = new GitWorkspaceProvider.PagedStrings(input, ct);
        using var reversed = new GitWorkspaceProvider.PagedStrings(input.Reverse(), ct);
        string hash = paths.Hash; Assert.Equal(hash, reversed.Hash); Assert.Equal(4, paths.Count);
        var descriptor = PagedSnapshotCatalog.Build(files.Path, new("", true, 0, 16, 0, 0, 0),
            SnapshotCatalogInput.WithPathMarkers(input.DistinctBy(e => e.Key), files.Path, true, ct), true, ct);
        using var catalog = new PagedSnapshotCatalog(files.Path, descriptor); catalog.Verify(ct);
        foreach (var entry in input)
        {
            Assert.Equal("v:" + Convert.ToBase64String(entry.Value.Span), catalog.Find(entry.Key)!.SourceReference);
            Assert.Equal(entry.Value.ToArray(), paths.Find(entry.Key));
        }
        Assert.Null(paths.Find("a/한글")); Assert.True(paths.Affects("A/한글/leaf/child"));
        Assert.Equal(hash, paths.Hash);
        using var changed = new GitWorkspaceProvider.PagedStrings(input.Select(e => e.Key == "a/leaf" ? e with { Value = new byte[] { 7 } } : e), ct);
        Assert.NotEqual(hash, changed.Hash);
        Assert.Equal(input.Select(e => e.Key).Distinct().Order(StringComparer.Ordinal), paths.Paths());
    }

    [Theory]
    [InlineData("..", 0)]
    [InlineData("a//b", 0)]
    [InlineData("/absolute", 0)]
    [InlineData("", 0)]
    [InlineData("a/", 0)]
    [InlineData("a/../b", 0)]
    [InlineData("parent/leaf", 60000)]
    public void ComparisonOnlyAdmissionRetainsTheCatalogPathAndPageLimits(string path, int bytes)
    {
        using var files = new TestDirectory(); var ct = TestContext.Current.CancellationToken;
        MetadataEntry[] input = [new(path, new byte[bytes])];
        var materialized = Record.Exception(() => PagedSnapshotCatalog.Build(files.Path, new("", true, 0, 16, 0, 0, 0),
            SnapshotCatalogInput.WithPathMarkers(input, files.Path, true, ct), true, ct));
        var comparison = Record.Exception(() => { using var paths = new GitWorkspaceProvider.PagedStrings(input, ct); });
        Assert.NotNull(materialized); Assert.NotNull(comparison); Assert.Equal(materialized.GetType(), comparison.GetType());
        if (materialized is WorkspaceException expected) Assert.Equal(expected.Code, ((WorkspaceException)comparison).Code);
    }

    [Fact]
    public void ConflictingDuplicatesAreRejectedBeforeAComparisonCanBeUsed()
    {
        var ct = TestContext.Current.CancellationToken;
        MetadataEntry[] input = [new("same", new byte[] { 1 }), new("same", new byte[] { 2 })];
        var error = Assert.Throws<WorkspaceException>(() => { using var paths = new GitWorkspaceProvider.PagedStrings(input, ct); });
        Assert.Equal("SourceObservationInvalid", error.Code);
    }
}
