using Splinter.Core;
using Splinter.Platform.Windows;
using Splinter.Runtime;

namespace Splinter.Tests;

public sealed class JournalDirectoryListingTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ImmutableListingIndexesChildrenWithoutRepeatingStats(bool optimized)
    {
        using var directory = new TestDirectory();
        var tree = new ListedTree(Enumerable.Range(0, 256).Select(i => File($"f{i:D3}")).Prepend(DirectoryEntry("")));
        using var store = Open(directory.Path, tree, optimized);
        tree.Reset();
        var actual = store.List("");
        Assert.Equal(256, actual.Count);
        Assert.Equal(0, tree.Stats);
        Assert.Equal(1, tree.Lists);
        Assert.Equal(3, tree.HealthChecks); // operation, listing, completion
        Assert.All(actual, e =>
        {
            Assert.Equal(e.Path, e.BasePath);
            Assert.Equal((7L, 11L, 13L, 17L, 0x1A4u),
                (e.Length, e.CreationTime, e.LastAccessTime, e.LastWriteTime, e.UnixMode!.Value));
        });
        Assert.Equal(actual, store.List(""));
        Assert.Equal(0, tree.Stats);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RenamedParentsKeepBaseCoordinatesAndPrivateDeletedAndNewEntries(bool optimized)
    {
        using var directory = new TestDirectory();
        var tree = new ListedTree([DirectoryEntry(""), DirectoryEntry("dir"), File("dir/a"), File("dir/b"), File("dir/c"), File("dir/d")]);
        using var store = Open(directory.Path, tree, optimized);
        store.Rename(store.Stat("dir")!.Id, "renamed", false);
        store.Delete(store.Stat("renamed/a")!.Id);
        store.Rename(store.Stat("renamed/b")!.Id, "renamed/other", false);
        var changed = store.Stat("renamed/c")!;
        store.SetMetadata(changed.Id, 1, 101, 102, 103);
        var created = store.Create("renamed/new", false, 32);
        tree.Reset();
        var actual = store.List("renamed");
        Assert.Equal(new[] { "renamed/c", "renamed/d", "renamed/new", "renamed/other" }, actual.Select(e => e.Path));
        Assert.Equal(0, tree.Stats);
        Assert.Equal("dir/d", actual.Single(e => e.Path == "renamed/d").BasePath);
        Assert.Equal(103, actual.Single(e => e.Id == changed.Id).LastWriteTime);
        Assert.Null(actual.Single(e => e.Id == created.Id).BasePath);
        Assert.Null(store.Stat("dir"));
        Assert.Null(store.Stat("renamed/a"));
        Assert.Null(store.Stat("renamed/b"));
        byte[] bytes = new byte[7];
        Assert.Equal(7, store.Read(actual.Single(e => e.Path == "renamed/d").Id, 0, bytes));
        Assert.Equal("dir/d", tree.LastReadPath);
    }

    [Fact]
    public void TopLevelPolicyStillPrecedesListedEntryReuse()
    {
        using var directory = new TestDirectory();
        var tree = new ListedTree([DirectoryEntry(""), File("allowed"), File("excluded"), File("denied"), File("outside")]);
        var identity = Identity(tree) with
        {
            IncludedTopLevel = ["allowed", "excluded", "denied"],
            ExcludedTopLevel = ["excluded"], DeniedTopLevel = ["denied"]
        };
        using var store = new JournalDeltaStore(directory.Path, identity, tree, optimizeJournal: true);
        tree.Reset();
        Assert.Equal("allowed", Assert.Single(store.List("")).Path);
        Assert.Equal(0, tree.Stats);
        foreach (string path in new[] { "excluded", "denied", "outside" }) Assert.Null(store.Stat(path));
    }

    [Fact]
    public void UnexpectedListedBasePathUsesExactLookupAndLossAfterListingFails()
    {
        using var directory = new TestDirectory();
        var tree = new ListedTree([DirectoryEntry(""), File("a")]) { WrongListedPath = true };
        using var store = Open(directory.Path, tree, true);
        tree.Reset();
        var actual = Assert.Single(store.List(""));
        Assert.Equal("a", actual.BasePath);
        Assert.Equal(7, actual.Length);
        Assert.Equal(1, tree.Stats);
        tree.LoseAfterListing = true;
        Assert.Equal("SnapshotMissing", Assert.Throws<WorkspaceException>(() => store.List("")).Code);
        Assert.Equal("SnapshotMissing", Assert.Throws<WorkspaceException>(() => store.Get(actual.Id)).Code);
    }

    [Fact]
    public void RealOwnedDirectoryListingReusesManifestMetadataAndKeepsPinnedContent()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Requires Windows owned-directory handles.");
        using var directory = new TestDirectory();
        string root = Path.Combine(directory.Path, "basis"); Directory.CreateDirectory(root);
        for (int i = 0; i < 128; i++) System.IO.File.WriteAllBytes(Path.Combine(root, $"f{i:D3}"), [(byte)i, 91, 92]);
        var snapshot = new SnapshotReference("listed-native", OwnedDirectorySnapshotTree.ProviderName, root, root,
            DateTimeOffset.UtcNow, true, true, "stopped-original-owned-directory");
        var manifest = OwnedDirectorySnapshotTree.Capture(snapshot, "owner");
        using var basis = new OwnedDirectorySnapshotTree(manifest, snapshot);
        var tree = new CountedTree(basis);
        using var store = Open(Path.Combine(directory.Path, "delta"), tree, true);
        tree.Stats = 0;
        var entries = store.List("");
        Assert.Equal(128, entries.Count); Assert.Equal(0, tree.Stats);
        foreach (var entry in entries)
        {
            var saved = manifest.Items.Single(i => i.Entry.Path == entry.Path).Entry;
            Assert.Equal((saved.Attributes, saved.CreationTime, saved.LastAccessTime, saved.LastWriteTime),
                (entry.Attributes, entry.CreationTime, entry.LastAccessTime, entry.LastWriteTime));
        }
        byte[] data = new byte[3]; Assert.Equal(3, store.Read(entries[0].Id, 0, data));
        Assert.Equal(new byte[] { 0, 91, 92 }, data);
        Assert.Throws<IOException>(() => System.IO.File.WriteAllText(Path.Combine(root, "f000"), "changed"));
    }

    private static SnapshotEntry DirectoryEntry(string path) => new(path, true, 0, 16, 11, 13, 17);
    private static SnapshotEntry File(string path) => new(path, false, 7, 32, 11, 13, 17, 0x1A4);
    private static OverlayIdentity Identity(ISnapshotTree tree) => new(2, "directory-listing", tree.Snapshot, 8 * 1024 * 1024, 65536);
    private static JournalDeltaStore Open(string path, ISnapshotTree tree, bool optimized) => new(path, Identity(tree), tree, optimizeJournal: optimized);

    private sealed class ListedTree(IEnumerable<SnapshotEntry> entries) : ISnapshotTree
    {
        private readonly Dictionary<string, SnapshotEntry> entries = entries.ToDictionary(e => e.Path, StringComparer.OrdinalIgnoreCase);
        public SnapshotReference Snapshot { get; } = new("listed", "fake-memory", "source", "fixed", DateTimeOffset.UnixEpoch, true, true, "fixture-only");
        public int Stats, Lists, HealthChecks;
        public bool WrongListedPath, LoseAfterListing;
        private bool lost;
        public string? LastReadPath;
        public void Reset() { Stats = Lists = HealthChecks = 0; }
        public void EnsureAvailable()
        {
            HealthChecks++;
            if (lost) throw new WorkspaceException("SnapshotMissing", "Test basis disappeared.");
        }
        public SnapshotEntry? Stat(string path) { Stats++; EnsureAvailable(); return entries.GetValueOrDefault(path); }
        public IReadOnlyList<SnapshotEntry> List(string path)
        {
            Lists++; EnsureAvailable();
            var result = entries.Values.Where(e => e.Path.Length > 0 && Parent(e.Path) == path).ToArray();
            if (WrongListedPath) result = result.Select(e => e with { Path = "unexpected/" + e.Path, Length = 999 }).ToArray();
            if (LoseAfterListing) lost = true;
            return result;
        }
        public int Read(string path, long offset, Span<byte> destination)
        {
            EnsureAvailable(); LastReadPath = path;
            int count = (int)Math.Min(destination.Length, entries[path].Length - offset);
            destination[..count].Fill(42); return count;
        }
        private static string Parent(string path) => path[..Math.Max(0, path.LastIndexOf('/'))];
    }
    private sealed class CountedTree(ISnapshotTree inner) : ISnapshotTree
    {
        public int Stats;
        public SnapshotReference Snapshot => inner.Snapshot;
        public void EnsureAvailable() => inner.EnsureAvailable();
        public SnapshotEntry? Stat(string path) { Stats++; return inner.Stat(path); }
        public IReadOnlyList<SnapshotEntry> List(string path) => inner.List(path);
        public int Read(string path, long offset, Span<byte> output) => inner.Read(path, offset, output);
    }
}
