using System.Security.Cryptography;
using Splinter.Core;
using Splinter.Runtime;

namespace Splinter.Tests;

public sealed class DirectoryMetadataTreeTests
{
    [Fact]
    public void DirectoryAndChildIdentitiesDoNotDependOnStoreOrPhysicalOffsets()
    {
        using var files = new TestDirectory();
        string Build(string name, bool padding)
        {
            using var store = new PagedMetadataStore(Path.Combine(files.Path, name), writable: true);
            if (padding) _ = store.Set(store.Empty, "unrelated", "different physical offsets"u8);
            var tree = new DirectoryMetadataTree(store);
            var child = tree.Build([File("Hero.png"), File("Hero.png.meta")], TestContext.Current.CancellationToken);
            var root = tree.Build([Folder("Assets", child)], TestContext.Current.CancellationToken); store.Flush();
            Assert.Equal(new[] { "Assets", "Assets/Hero.png", "Assets/Hero.png.meta" }, tree.Walk(root).Select(e => e.Entry.Path));
            return root.ContentHash;
        }
        Assert.Equal(Build("first", false), Build("second", true));
    }

    [Fact]
    public void ACaseInsensitiveDirectoryRejectsCollidingNamesAndRetainsOriginalCasing()
    {
        using var files = new TestDirectory();
        using var store = new PagedMetadataStore(Path.Combine(files.Path, "metadata"), writable: true);
        var tree = new DirectoryMetadataTree(store);
        Assert.Throws<WorkspaceException>(() => tree.Build([File("Hero.png"), File("hero.png")], TestContext.Current.CancellationToken));
        var root = tree.Build([File("Hero.png")], TestContext.Current.CancellationToken); store.Flush();
        Assert.Equal("Hero.png", tree.Find(root, "HERO.PNG")!.Entry.Path);
    }

    [Fact]
    public void LargeUnsortedDirectoryUsesExternalRunsAndRetainsEarlierChildren()
    {
        using var files = new TestDirectory(); string directory = Path.Combine(files.Path, "metadata");
        using var store = new PagedMetadataStore(directory, writable: true);
        var tree = new DirectoryMetadataTree(store);
        var child = tree.Build(Files(100001), TestContext.Current.CancellationToken);
        var first = tree.Build([Folder("Assets", child)], TestContext.Current.CancellationToken);
        var secondChild = tree.Set(child, File("addition.txt"));
        var second = tree.Set(first, Folder("Assets", secondChild)); store.Flush();
        Assert.Null(tree.Find(tree.Find(first, "Assets")!.Children!, "addition.txt"));
        Assert.NotNull(tree.Find(tree.Find(second, "Assets")!.Children!, "addition.txt"));
        Assert.Equal(100001, child.Count); Assert.Equal(100002, secondChild.Count);
        Assert.Empty(Directory.EnumerateDirectories(directory, "sort-*"));
    }

    [Fact]
    public void MetadataRetainsAllTimesAttributesAndOptionalUnixMode()
    {
        using var files = new TestDirectory();
        using var store = new PagedMetadataStore(Path.Combine(files.Path, "metadata"), writable: true);
        var tree = new DirectoryMetadataTree(store, caseSensitive: true);
        var file = File("settings.asset") with { Entry = new("settings.asset", false, 12, 32, 10, 20, 30, 420) };
        var root = tree.Build([file], TestContext.Current.CancellationToken); store.Flush();
        Assert.Equal(file, tree.Find(root, "settings.asset"));
        Assert.Null(tree.Find(root, "SETTINGS.ASSET"));
    }

    [Fact]
    public void AReaderCannotReinterpretPersistedNamesUnderAnotherPathPolicy()
    {
        using var files = new TestDirectory();
        using var store = new PagedMetadataStore(Path.Combine(files.Path, "metadata"), writable: true);
        var sensitive = new DirectoryMetadataTree(store, caseSensitive: true);
        var insensitive = new DirectoryMetadataTree(store, caseSensitive: false);
        var first = sensitive.Build([File("FILE")], TestContext.Current.CancellationToken);
        var second = insensitive.Build([File("FILE")], TestContext.Current.CancellationToken); store.Flush();
        Assert.NotEqual(first.ContentHash, second.ContentHash);
        Assert.Throws<Splinter.Core.WorkspaceException>(() => insensitive.Find(first, "file"));
    }

    private static IEnumerable<DirectoryMetadataRecord> Files(int count)
    {
        for (int index = count - 1; index >= 0; index--) yield return File("file-" + index.ToString("D8"));
    }
    private static DirectoryMetadataRecord File(string name) => new(new(name, false, 12, 32, 10, 20, 30),
        Convert.ToHexStringLower(SHA256.HashData("same content"u8)));
    private static DirectoryMetadataRecord Folder(string name, MetadataTreeRoot child) => new(new(name, true, 0, 16, 10, 20, 30), null, child);
}
