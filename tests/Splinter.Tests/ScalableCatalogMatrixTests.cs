using System.Diagnostics;
using Splinter.Core;
using Splinter.Runtime;

namespace Splinter.Tests;

public sealed class ScalableCatalogMatrixTests
{
    public static TheoryData<int, string> Inputs
    {
        get
        {
            var data = new TheoryData<int, string>();
            foreach (int count in new[] { 1024, 32769, 262145, 1048576 })
                foreach (string shape in new[] { "flat", "hierarchy", "long-prefix" }) data.Add(count, shape);
            return data;
        }
    }

    [Theory, MemberData(nameof(Inputs))]
    public void InputShapeAndSizeDoNotChangeCursorIdentityOrReconnection(int count, string shape)
    {
        using var scratch = new TemporaryMetadataDirectory(); var ct = TestContext.Current.CancellationToken;
        var watch = Stopwatch.StartNew();
        var first = PagedSnapshotCatalog.Build(scratch.Path, Entry("", true), Input(count, shape), ct: ct);
        long before = Bytes();
        using (var reader = new PagedSnapshotCatalog(scratch.Path, first))
        {
            long seen = 0; string last = "";
            foreach (var record in reader.Walk())
            {
                ct.ThrowIfCancellationRequested(); seen++; last = record.Entry.Path;
                if (!record.Entry.Directory && seen % 65536 == 0)
                    Assert.Equal(record, reader.Find(record.Entry.Path));
            }
            Assert.Equal(count + 1L, seen);
            Assert.NotNull(reader.Find(last)); Assert.Empty(reader.Walk(last));
        }
        var repeated = PagedSnapshotCatalog.Build(scratch.Path, Entry("", true), Input(count, shape), ct: ct, previousCatalog: first);
        Assert.Equal(first, repeated); Assert.Equal(before, Bytes());
        using var reopened = new PagedSnapshotCatalog(scratch.Path, repeated);
        Assert.Equal(count + 1L, reopened.Walk().LongCount());
        Assert.InRange(PagedMetadataStore.CachedMetadataBytes, 0L, StorageResources.MetadataCacheBytes);
        TestContext.Current.TestOutputHelper!.WriteLine($"SCAL-02 shape={shape} entries={count} seconds={watch.Elapsed.TotalSeconds:F6} metadataBytes={before} cacheBytes={PagedMetadataStore.CachedMetadataBytes}");
        long Bytes() => Directory.EnumerateFiles(scratch.Path, "pages-*.bin").Sum(path => new FileInfo(path).Length);
    }

    private static int DirectoryCount(string shape) => shape switch { "flat" => 0, "hierarchy" => 16, _ => 8 };
    private static string LongPart(int index) => "prefix-" + index + "-" + new string('한', 32);
    private static string LongPrefix => string.Join('/', Enumerable.Range(0, 8).Select(LongPart));
    private static string FilePath(int index, string shape) => shape switch
    {
        "flat" => "file-" + index.ToString("D8"),
        "hierarchy" => "dir-" + (index % 16).ToString("D2") + "/file-" + index.ToString("D8"),
        _ => LongPrefix + "/file-" + index.ToString("D8")
    };
    private static IEnumerable<DirectoryMetadataRecord> Input(int count, string shape)
    {
        int dirs = DirectoryCount(shape);
        for (int index = count - dirs - 1; index >= 0; index--)
            yield return new(Entry(FilePath(index, shape), false), new string('a', 64));
        for (int index = dirs - 1; index >= 0; index--)
            yield return new(Entry(shape == "hierarchy" ? "dir-" + index.ToString("D2") : string.Join('/', Enumerable.Range(0, index + 1).Select(LongPart)), true), null);
    }
    private static SnapshotEntry Entry(string path, bool directory) => new(path, directory, directory ? 0 : 7,
        directory ? 16u : 32u, 100, 101, 102, directory ? null : 420);
}
