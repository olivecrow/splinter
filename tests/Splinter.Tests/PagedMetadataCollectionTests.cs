using System.Text;
using System.Text.Json.Nodes;
using Splinter.Core;
using Splinter.Platform.Windows;
using Splinter.Runtime;

namespace Splinter.Tests;

public sealed class PagedMetadataCollectionTests
{
    [Theory]
    [InlineData("maximum")]
    [InlineData("phase")]
    [InlineData("old-version")]
    [InlineData("missing-hash")]
    [InlineData("duplicate")]
    public void CorruptOrPreviousCollectionControlsArePreservedAndRefusedBeforeDeletion(string fault)
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Requires native metadata identities.");
        using var files = new TestDirectory(); string directory = Path.Combine(files.Path, "metadata"); var pins = new WindowsJournalFilePins();
        var ct = TestContext.Current.CancellationToken; MetadataTreeRoot root;
        using (var writer = new PagedMetadataStore(directory, true, filePins: pins)) { root = writer.Build(Entries(32), ct); writer.Flush(); }
        Assert.Throws<IOException>(() => PagedMetadataStore.Collect(directory, [root], "fixed", pins, point =>
        { if (point == "metadata.collection-roots-durable") throw new IOException("lost response"); }, ct));
        string active = Path.Combine(directory, "collection.json"); var node = JsonNode.Parse(File.ReadAllText(active))!;
        switch (fault)
        {
            case "maximum": node["maximumContainer"] = 100; break;
            case "phase": node["phase"] = "cleanup"; break;
            case "old-version": node["version"] = 1; break;
            case "missing-hash": node.AsObject().Remove("stateHash"); break;
        }
        string changed = fault == "duplicate" ? node.ToJsonString().Replace("\"version\":2", "\"version\":1,\"version\":2", StringComparison.Ordinal) : node.ToJsonString();
        File.WriteAllText(active, changed); byte[] control = File.ReadAllBytes(active); long bytes = PageBytes(directory);
        Assert.Equal("MetadataStoreInvalid", Assert.Throws<WorkspaceException>(() => PagedMetadataStore.Collect(directory, [], "fixed", pins, ct: ct)).Code);
        Assert.Equal(control, File.ReadAllBytes(active)); Assert.Equal(bytes, PageBytes(directory));
        using var reader = new PagedMetadataStore(directory); Assert.Equal(32, reader.Open(root).Enumerate().Count());
    }

    [Fact]
    public void CopiedPageCorruptionIsRefusedBeforeLocationPublicationAndOldContainerDeletion()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Requires native metadata identities.");
        using var files = new TestDirectory(); string directory = Path.Combine(files.Path, "metadata"); var pins = new WindowsJournalFilePins();
        var ct = TestContext.Current.CancellationToken; MetadataTreeRoot root;
        using (var writer = new PagedMetadataStore(directory, true, filePins: pins)) { root = writer.Build(Entries(32), ct); writer.Flush(); }
        Assert.Throws<IOException>(() => PagedMetadataStore.Collect(directory, [root], "fixed", pins, point =>
        { if (point == "metadata.collection-copy-prepared") throw new IOException("lost response"); }, ct));
        string work = Directory.EnumerateDirectories(directory, "collection-work-*").Single(); string copied = Directory.EnumerateFiles(work, "pages-*.bin").Single();
        byte[] damaged = File.ReadAllBytes(copied); damaged[^1] ^= 1; File.WriteAllBytes(copied, damaged);
        long original = PageBytes(directory);
        Assert.Equal("MetadataStoreInvalid", Assert.Throws<WorkspaceException>(() => PagedMetadataStore.Collect(directory, [], "fixed", pins, ct: ct)).Code);
        Assert.False(File.Exists(Path.Combine(directory, "locations.json")));
        Assert.True(PageBytes(directory) >= original);
        using var reader = new PagedMetadataStore(directory); Assert.Equal(32, reader.Open(root).Enumerate().Count());
    }
    [Fact]
    public void SharedPoolInitializationResumesAfterSpaceFailureBeforeFirstNativeReceiptWithoutAdoptingTheGap()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Requires native metadata identities.");
        using var files = new TestDirectory(); string home = Path.Combine(files.Path, "home"); var pins = new WindowsJournalFilePins();
        bool injected = false;
        Assert.Throws<IOException>(() => new SharedMetadataPool(home, "source", filePins: pins, boundary: point =>
        {
            if (!injected && point == "metadata.container-created-before-owner")
            { injected = true; throw new IOException("injected ENOSPC", unchecked((int)0x80070070)); }
        }));
        string directory = Path.Combine(home, "metadata-store"), gap = Directory.EnumerateFiles(directory, "pages-*.bin").Single();
        string gapId = pins.Identify(gap); byte[] original = File.ReadAllBytes(gap);
        Assert.False(File.Exists(gap + ".owner")); Assert.False(File.Exists(Path.Combine(directory, "owner.json")));
        using (var recovered = new SharedMetadataPool(home, "source", filePins: pins))
        {
            Assert.True(File.Exists(Path.Combine(directory, "owner.json")));
            Assert.Equal(gapId, pins.Identify(gap)); Assert.Equal(original, File.ReadAllBytes(gap)); Assert.False(File.Exists(gap + ".owner"));
            var result = recovered.Collect("initial-gap", TestContext.Current.CancellationToken);
            Assert.Equal(1, result.PreservedEntries); Assert.NotNull(result.PreservedRoot);
        }
        Assert.Equal(gapId, pins.Identify(gap)); Assert.Equal(original, File.ReadAllBytes(gap));
    }

    [Fact]
    public void SharedPoolInitializationRefusesForeignIdentityAfterAnOwnershipCreationGap()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Requires native metadata identities.");
        using var files = new TestDirectory(); string home = Path.Combine(files.Path, "home"); var pins = new WindowsJournalFilePins();
        Assert.Throws<IOException>(() => new SharedMetadataPool(home, "source", filePins: pins, boundary: point =>
        { if (point == "metadata.container-created-before-owner") throw new IOException("injected before native receipt"); }));
        string directory = Path.Combine(home, "metadata-store"), identity = Path.Combine(directory, "identity.bin"), replacement = identity + ".replacement";
        File.Copy(identity, replacement); string foreignId = pins.Identify(replacement); File.Move(replacement, identity, true);
        Assert.Throws<WorkspaceException>(() => new SharedMetadataPool(home, "source", filePins: pins));
        Assert.Equal(foreignId, pins.Identify(identity)); Assert.False(File.Exists(Path.Combine(directory, "owner.json")));
    }

    [Fact]
    public void CollectionSpaceFailureBeforeContainerOwnershipResumesAndReportsProtectedScratch()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Requires native metadata identities.");
        using var files = new TestDirectory(); string directory = Path.Combine(files.Path, "metadata"); var pins = new WindowsJournalFilePins();
        var ct = TestContext.Current.CancellationToken; MetadataTreeRoot root;
        using (var writer = new PagedMetadataStore(directory, true, filePins: pins)) { root = writer.Build(Entries(32), ct); writer.Flush(); }
        Assert.Throws<IOException>(() => PagedMetadataStore.Collect(directory, [root], "space", pins, point =>
        { if (point == "metadata.container-created-before-owner") throw new IOException("injected ENOSPC", unchecked((int)0x80070070)); }, ct));
        string work = Directory.EnumerateDirectories(directory, "collection-work-*").Single();
        string gap = Directory.EnumerateFiles(work, "pages-*.bin").Single(); string identity = pins.Identify(gap);
        var recovered = PagedMetadataStore.Collect(directory, [], "space", pins, ct: ct);
        Assert.Equal(1, recovered.PreservedEntries); Assert.Equal(identity, pins.Identify(gap)); Assert.False(File.Exists(gap + ".owner"));
        using var reader = new PagedMetadataStore(directory); Assert.Equal(32, reader.Open(root).Enumerate().Count());
    }
    [Fact]
    public void CollectionPreservesPublishedReferencesAndReclaimsOnlyUnreachablePagesAcrossRepeatedRuns()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Requires actual native file ownership.");
        using var files = new TestDirectory(); string directory = Path.Combine(files.Path, "metadata");
        var pins = new WindowsJournalFilePins(); var ct = TestContext.Current.CancellationToken;
        MetadataTreeRoot first, latest;
        using (var store = new PagedMetadataStore(directory, true, filePins: pins))
        {
            first = store.Build(Entries(256), ct); latest = first;
            for (int version = 0; version < 40; version++) latest = store.Set(latest, "00000128", Encoding.UTF8.GetBytes("v" + version));
            store.Flush();
        }
        long before = PageBytes(directory);
        var result = PagedMetadataStore.Collect(directory, [first, latest], "first", pins, ct: ct);
        Assert.True(result.ReclaimedBytes > result.RetainedBytes);
        Assert.True(PageBytes(directory) < before); AssertRoots();
        Assert.True(PagedMetadataStore.Collect(directory, [], "first", pins, ct: ct).AlreadyCompleted);
        using (var writer = new PagedMetadataStore(directory, true, filePins: pins))
        { latest = writer.Set(latest, "00000128", "after-collection"u8); writer.Flush(); }
        PagedMetadataStore.Collect(directory, [first, latest], "second", pins, ct: ct); AssertRoots();
        PagedMetadataStore.Collect(directory, [first, latest], "third", pins, ct: ct); AssertRoots();
        Assert.Single(Directory.EnumerateFiles(directory, "locations-*.bin"));
        Assert.Empty(Directory.EnumerateDirectories(directory, "collection-work-*"));
        void AssertRoots()
        {
            using var reader = new PagedMetadataStore(directory);
            Assert.Equal("value-128", Encoding.UTF8.GetString(reader.Open(first).Find("00000128")!));
            Assert.NotEqual("value-128", Encoding.UTF8.GetString(reader.Open(latest).Find("00000128")!));
            Assert.Equal(256, reader.Open(first).Enumerate().Count());
            Assert.Equal(256, reader.Open(latest).Enumerate().Count());
        }
    }

    [Theory]
    [InlineData("metadata.collection-roots-durable")]
    [InlineData("metadata.container-owner-durable")]
    [InlineData("metadata.page-header-written")]
    [InlineData("metadata.collection-payload-durable")]
    [InlineData("metadata.collection-copy-prepared")]
    [InlineData("metadata.collection-container-installed")]
    [InlineData("metadata.collection-location-published")]
    [InlineData("metadata.collection-reclaim-intent-durable")]
    [InlineData("metadata.collection-container-unlinked")]
    [InlineData("metadata.collection-container-reclaimed")]
    [InlineData("metadata.collection-cleanup-durable")]
    [InlineData("metadata.collection-root-spool-reclaimed")]
    [InlineData("metadata.collection-completed")]
    public void InterruptedCollectionResumesItsFrozenRootsAndRequest(string stop)
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Requires actual native file ownership.");
        using var files = new TestDirectory(); string directory = Path.Combine(files.Path, "metadata");
        var pins = new WindowsJournalFilePins(); var ct = TestContext.Current.CancellationToken; MetadataTreeRoot root;
        using (var writer = new PagedMetadataStore(directory, true, filePins: pins))
        { root = writer.Build(Entries(128), ct); _ = writer.Set(root, "unpublished", "unused"u8); writer.Flush(); }
        bool injected = false;
        Assert.Throws<IOException>(() => PagedMetadataStore.Collect(directory, [root], "fixed", pins, boundary =>
        { if (!injected && boundary == stop) { injected = true; throw new IOException("injected storage interruption"); } }, ct));
        Assert.True(injected);
        if (stop != "metadata.collection-completed")
        {
            Assert.Equal("MetadataCollectionPending", Assert.Throws<WorkspaceException>(() => new PagedMetadataStore(directory, true, filePins: pins)).Code);
            Assert.Throws<WorkspaceException>(() => PagedMetadataStore.Collect(directory, [root], "bypass", pins, ct: ct));
        }
        // Deliberately omit the roots on retry: the accepted sealed root set is
        // recovered instead of accepting a new caller-provided generation.
        PagedMetadataStore.Collect(directory, [], "fixed", pins, ct: ct);
        using var reader = new PagedMetadataStore(directory);
        Assert.Equal("value-127", Encoding.UTF8.GetString(reader.Open(root).Find("00000127")!));
        Assert.Equal(128, reader.Open(root).Enumerate().Count());
        Assert.Empty(Directory.EnumerateDirectories(directory, "collection-work-*"));
    }

    [Fact]
    public void ActiveReaderAndForeignEqualByteContainerPreventCollection()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Requires actual native file ownership.");
        using var files = new TestDirectory(); string directory = Path.Combine(files.Path, "metadata");
        var pins = new WindowsJournalFilePins(); var ct = TestContext.Current.CancellationToken; MetadataTreeRoot root;
        using (var writer = new PagedMetadataStore(directory, true, filePins: pins))
        { root = writer.Build(Entries(128), ct); writer.Flush(); }
        using (var reader = new PagedMetadataStore(directory))
            Assert.Throws<IOException>(() => PagedMetadataStore.Collect(directory, [root], "reader", pins, ct: ct));
        string container = Directory.EnumerateFiles(directory, "pages-*.bin").Single(), replacement = container + ".replacement";
        File.Copy(container, replacement); string replacementIdentity = pins.Identify(replacement); File.Move(replacement, container, true);
        Assert.Throws<WorkspaceException>(() => PagedMetadataStore.Collect(directory, [root], "foreign", pins, ct: ct));
        Assert.Equal(replacementIdentity, pins.Identify(container));
        Assert.False(File.Exists(Path.Combine(directory, "collection.json")));
    }

    [Fact]
    public void CompletedHistoricalRequestCannotChangeANewerPendingCollection()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Requires actual native file ownership.");
        using var files = new TestDirectory(); string directory = Path.Combine(files.Path, "metadata");
        var pins = new WindowsJournalFilePins(); var ct = TestContext.Current.CancellationToken; MetadataTreeRoot root;
        using (var writer = new PagedMetadataStore(directory, true, filePins: pins))
        { root = writer.Build(Entries(64), ct); writer.Flush(); }
        PagedMetadataStore.Collect(directory, [root], "old", pins, ct: ct);
        Assert.Throws<IOException>(() => PagedMetadataStore.Collect(directory, [root], "new", pins,
            boundary => { if (boundary == "metadata.collection-roots-durable") throw new IOException("lost response"); }, ct));
        string active = Path.Combine(directory, "collection.json"), seal = active + ".roots";
        byte[] activeBytes = File.ReadAllBytes(active), sealBytes = File.ReadAllBytes(seal);
        Assert.True(PagedMetadataStore.Collect(directory, [], "old", pins, ct: ct).AlreadyCompleted);
        Assert.Equal(activeBytes, File.ReadAllBytes(active)); Assert.Equal(sealBytes, File.ReadAllBytes(seal));
        PagedMetadataStore.Collect(directory, [], "new", pins, ct: ct);
        using var reader = new PagedMetadataStore(directory);
        Assert.Equal(64, reader.Open(root).Enumerate().Count());
    }

    [Fact]
    public void ContainersWithoutCreationOwnershipArePreservedInsteadOfAdopted()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Requires actual native file ownership.");
        using var files = new TestDirectory(); string directory = Path.Combine(files.Path, "metadata");
        var pins = new WindowsJournalFilePins(); var ct = TestContext.Current.CancellationToken; MetadataTreeRoot root;
        using (var writer = new PagedMetadataStore(directory, true)) { root = writer.Build(Entries(32), ct); writer.Flush(); }
        long before = PageBytes(directory);
        Assert.ThrowsAny<IOException>(() => PagedMetadataStore.Collect(directory, [root], "unowned", pins, ct: ct));
        Assert.Equal(before, PageBytes(directory));
    }

    [Fact]
    public void UnreferencedUnknownScratchAndHeaderGapsArePreservedAndIndexedInTheReceipt()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Requires actual native file ownership.");
        using var files = new TestDirectory(); string directory = Path.Combine(files.Path, "metadata");
        var pins = new WindowsJournalFilePins(); var ct = TestContext.Current.CancellationToken; MetadataTreeRoot root;
        using (var writer = new PagedMetadataStore(directory, true, filePins: pins))
        { root = writer.Build(Entries(32), ct); writer.Flush(); }
        string gap = Path.Combine(directory, "pages-0000000000000001.bin"); File.WriteAllBytes(gap, []);
        string work = Path.Combine(directory, "collection-work-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(work);
        string unknown = Path.Combine(work, "unknown.bin"); File.WriteAllText(unknown, "preserve foreign scratch");
        string gapId = pins.Identify(gap), unknownId = pins.Identify(unknown);
        var result = PagedMetadataStore.Collect(directory, [root], "preserve", pins, ct: ct);
        Assert.Equal(2, result.PreservedEntries); Assert.NotNull(result.PreservedDirectory); Assert.NotNull(result.PreservedRoot);
        using (var inventory = new PagedMetadataStore(Path.Combine(directory, result.PreservedDirectory)))
        {
            var rows = inventory.Open(result.PreservedRoot);
            Assert.NotNull(rows.Find("header/" + Path.GetFileName(gap)));
            Assert.NotNull(rows.Find("work/" + Path.GetFileName(work)));
        }
        Assert.Equal(gapId, pins.Identify(gap)); Assert.Equal(unknownId, pins.Identify(unknown));
        var before = Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories).ToDictionary(path => path, File.ReadAllBytes);
        var repeated = PagedMetadataStore.Collect(directory, [], "preserve", pins, ct: ct);
        Assert.True(repeated.AlreadyCompleted); Assert.Equal(2, repeated.PreservedEntries);
        foreach (var pair in before) Assert.Equal(pair.Value, File.ReadAllBytes(pair.Key));
        using var reader = new PagedMetadataStore(directory); Assert.Equal(32, reader.Open(root).Enumerate().Count());
    }

    [Fact]
    public void EqualByteReplacementOfTheActiveLocationMapIsRefusedBeforeAnyContainerDeletion()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Requires actual native file ownership.");
        using var files = new TestDirectory(); string directory = Path.Combine(files.Path, "metadata");
        var pins = new WindowsJournalFilePins(); var ct = TestContext.Current.CancellationToken; MetadataTreeRoot root;
        using (var writer = new PagedMetadataStore(directory, true, filePins: pins)) { root = writer.Build(Entries(32), ct); writer.Flush(); }
        PagedMetadataStore.Collect(directory, [root], "first", pins, ct: ct);
        string path = Directory.EnumerateFiles(directory, "locations-*.bin").Single(), replacement = path + ".replacement";
        File.Copy(path, replacement); string foreignId = pins.Identify(replacement); File.Move(replacement, path, true);
        long before = PageBytes(directory);
        Assert.Throws<WorkspaceException>(() => PagedMetadataStore.Collect(directory, [root], "second", pins, ct: ct));
        Assert.Equal(foreignId, pins.Identify(path)); Assert.Equal(before, PageBytes(directory));
        Assert.False(File.Exists(Path.Combine(directory, "collection.json")));
    }

    private static long PageBytes(string directory) => Directory.EnumerateFiles(directory, "pages-*.bin").Sum(path => new FileInfo(path).Length);
    private static IEnumerable<MetadataEntry> Entries(int count)
    { for (int index = 0; index < count; index++) yield return new(index.ToString("D8"), Encoding.UTF8.GetBytes("value-" + index)); }
}
