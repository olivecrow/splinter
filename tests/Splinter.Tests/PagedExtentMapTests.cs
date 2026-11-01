using System.Security.Cryptography;
using Splinter.Runtime;

namespace Splinter.Tests;

public sealed class PagedExtentMapTests
{
    private static readonly string HashA = Convert.ToHexStringLower(SHA256.HashData("first"u8));
    private static readonly string HashB = Convert.ToHexStringLower(SHA256.HashData("second"u8));
    [Fact]
    public void PartialReplacementPreservesBothOldEdgesAndTheEarlierRoot()
    {
        using var files = new TestDirectory();
        using var store = new PagedMetadataStore(Path.Combine(files.Path, "metadata"), writable: true);
        var map = new PagedExtentMap(store); var ct = TestContext.Current.CancellationToken;
        var first = map.Build([new(0, 1000, HashA, 1000)], ct);
        var second = map.Replace(first, new(250, 300, HashB, 300), ct); store.Flush();
        Assert.Equal(new[] { new PagedExtent(0, 250, HashA, 1000), new(250, 300, HashB, 300), new(550, 450, HashA, 1000, 550) }, map.Enumerate(second));
        Assert.Equal(new PagedExtent(0, 1000, HashA, 1000), Assert.Single(map.Enumerate(first)));
        Assert.Single(map.Overlaps(second, 999, 1)); Assert.Empty(map.Overlaps(second, 1000, 1));
    }
    [Fact]
    public void TruncationAndLargeOffsetsDoNotRequirePayloadAllocationOrWholeExtentArrays()
    {
        using var files = new TestDirectory();
        using var store = new PagedMetadataStore(Path.Combine(files.Path, "metadata"), writable: true);
        var map = new PagedExtentMap(store); var ct = TestContext.Current.CancellationToken;
        const long at = 17L * 1024 * 1024 * 1024;
        var first = map.Build([new(0, 64, HashA, 64), new(at, 1000, HashA, 1000)], ct);
        var second = map.Truncate(first, at + 500, ct); store.Flush();
        Assert.Equal(500, Assert.Single(map.Overlaps(second, at, 1000)).Length);
        Assert.Equal(1000, Assert.Single(map.Overlaps(first, at, 1000)).Length);
        Assert.Equal(store.Empty, map.Truncate(second, 0, ct));
    }
    [Fact]
    public void ReplacingAcrossManyIntervalsLeavesOnlyTheRequiredBoundarySlices()
    {
        using var files = new TestDirectory();
        using var store = new PagedMetadataStore(Path.Combine(files.Path, "metadata"), writable: true);
        var map = new PagedExtentMap(store); var ct = TestContext.Current.CancellationToken;
        var first = map.Build(Enumerable.Range(0, 1000).Select(i => new PagedExtent(i * 10, 10, HashA, 10)), ct);
        var second = map.Replace(first, new(15, 9970, HashB, 9970), ct); store.Flush();
        Assert.Equal(5, map.Enumerate(second).Count()); Assert.Equal(1000, first.Count);
    }
}
