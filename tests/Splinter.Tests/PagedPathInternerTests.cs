using System.Text;
using Splinter.Runtime;

namespace Splinter.Tests;

public sealed class PagedPathInternerTests
{
    [Fact]
    public void AFilesystemLengthUnicodePathUsesBoundedComponentRecords()
    {
        using var files = new TestDirectory(); using var pages = new PagedMetadataStore(Path.Combine(files.Path, "pages"), true);
        var paths = new PagedPathInterner(pages);
        string path = string.Join('/', Enumerable.Range(0, 110).Select(i => new string('한', 200) + i)) + "/file.bin";
        Assert.True(Encoding.UTF8.GetByteCount(path) > PagedMetadataStore.MaximumPageBytes);
        string identity = paths.Intern(path); pages.Flush();
        Assert.Equal(PagedPathInterner.Reference(path), identity); Assert.Equal(path, paths.Resolve(identity));
        Assert.Equal(111, paths.Root.Count);
        var before = paths.Root; Assert.Equal(identity, paths.Intern(path)); Assert.Equal(before, paths.Root);
    }
    [Fact]
    public void BulkInterningWritesOnlyTheFinalGraphAndReusesOldPages()
    {
        using var files = new TestDirectory(); string directory = Path.Combine(files.Path, "pages");
        using var pages = new PagedMetadataStore(directory, true); var paths = new PagedPathInterner(pages); var ct = TestContext.Current.CancellationToken;
        IEnumerable<string> Input() => Enumerable.Range(0, 32769).Select(i => "Assets/Common/file-" + i.ToString("D7"));
        paths.Import(Input(), ct); pages.Flush(); long bytes = Size(); var first = paths.Root;
        Assert.Equal(32771, first.Count); Assert.Equal("Assets/Common/file-0032768", paths.Resolve(PagedPathInterner.Reference("Assets/Common/file-0032768")));
        paths.Import(Input().Reverse(), ct); pages.Flush(); Assert.Equal(first, paths.Root); Assert.Equal(bytes, Size());
        Assert.True(bytes < 32771L * 350);
        long Size() => Directory.EnumerateFiles(directory, "pages-*.bin").Sum(path => new FileInfo(path).Length);
    }
}
