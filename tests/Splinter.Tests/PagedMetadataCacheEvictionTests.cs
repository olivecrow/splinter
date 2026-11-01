using Splinter.Core;
using Splinter.Platform.Windows;
using Splinter.Runtime;

namespace Splinter.Tests;

public sealed class PagedMetadataCacheEvictionTests
{
    [Fact]
    public void DisposingOneRelocationMapDoesNotInvalidateAnotherMapsVerifiedBlocks()
    {
        RequireWindows(); using var files = new TestDirectory(); var pins = new WindowsJournalFilePins(); var ct = TestContext.Current.CancellationToken;
        var first = Relocated("first"); var second = Relocated("second");
        using var readerA = new PagedMetadataStore(first.Directory, filePins: pins);
        using var readerB = new PagedMetadataStore(second.Directory, filePins: pins);
        Assert.Equal(new byte[] { 7 }, readerA.Open(first.Root).Find("key"));
        Assert.Equal(new byte[] { 7 }, readerB.Open(second.Root).Find("key"));
        long reads = readerB.LocationBlockReads; Assert.True(reads > 0);
        readerA.Dispose();
        for (int index = 0; index < 20; index++) Assert.Equal(new byte[] { 7 }, readerB.Open(second.Root).Find("key"));
        Assert.Equal(reads, readerB.LocationBlockReads); Assert.InRange(PagedMetadataStore.CachedMetadataBytes, 0, StorageResources.MetadataCacheBytes);

        (string Directory, MetadataTreeRoot Root) Relocated(string name)
        {
            string directory = Path.Combine(files.Path, name); MetadataTreeRoot root;
            using (var writer = new PagedMetadataStore(directory, true, filePins: pins))
            { root = writer.Build([new("key", new byte[] { 7 })], ct); writer.Flush(); }
            PagedMetadataStore.Collect(directory, [root], "relocate", pins, ct: ct);
            return (directory, root);
        }
    }

    [Fact]
    public void HotContainersStayResidentAtTheResourceBudgetAndEvictionVisitsOnlyItsPartition()
    {
        RequireWindows(); using var files = new TestDirectory(); var ct = TestContext.Current.CancellationToken;
        var native = new WindowsJournalFilePins(); string directory = Path.Combine(files.Path, "metadata");
        int capacity = PagedMetadataStore.MaximumContainerReaders; var roots = OwnedContainers(directory, native, capacity + 4);
        string unrelatedDirectory = Path.Combine(files.Path, "unrelated"); MetadataTreeRoot unrelatedRoot;
        using (var writer = new PagedMetadataStore(unrelatedDirectory, true, filePins: native))
        { unrelatedRoot = writer.Build(Enumerable.Range(0, 4096).Select(i => new MetadataEntry(i.ToString("d8"), new byte[128])), ct); writer.Flush(); }
        using var unrelated = new PagedMetadataStore(unrelatedDirectory, filePins: native); unrelated.Open(unrelatedRoot).Verify(ct);
        var counted = new CountingPins(native); using var reader = new PagedMetadataStore(directory, filePins: counted);
        for (int index = 0; index < capacity; index++) Assert.Equal(new byte[] { (byte)index }, reader.Open(roots[index]).Find("key"));
        Assert.Equal(capacity, counted.Verifications); long before = reader.CacheInvalidationVisits;
        Assert.Equal(new byte[] { (byte)capacity }, reader.Open(roots[capacity]).Find("key"));
        Assert.InRange(reader.CacheInvalidationVisits - before, 0, 1);
        Assert.Equal(new byte[] { (byte)(capacity + 1) }, reader.Open(roots[capacity + 1]).Find("key"));
        Assert.Equal(capacity + 2, counted.Verifications);
        for (int index = 0; index < 100; index++)
        {
            Assert.Equal(new byte[] { (byte)capacity }, reader.Open(roots[capacity]).Find("key"));
            Assert.Equal(new byte[] { (byte)(capacity + 1) }, reader.Open(roots[capacity + 1]).Find("key"));
        }
        Assert.Equal(capacity + 2, counted.Verifications);
        string hot = Path.Combine(directory, $"pages-{roots[capacity + 1].Page!.Container:x16}.bin");
        Assert.Throws<IOException>(() => { using var denied = new FileStream(hot, FileMode.Open, FileAccess.ReadWrite, FileShare.Read); });
        string cold = Path.Combine(directory, $"pages-{roots[0].Page!.Container:x16}.bin"), retained = cold + ".retained";
        File.Move(cold, retained); File.Copy(retained, cold);
        Assert.Equal("SourceCaptureBackingInvalid", Assert.Throws<WorkspaceException>(() => reader.Open(roots[0]).Find("key")).Code);
        Assert.True(File.Exists(cold)); Assert.NotEqual(native.Identify(retained), native.Identify(cold));
        Assert.InRange(PagedMetadataStore.CachedMetadataBytes, 0, StorageResources.MetadataCacheBytes);
        Assert.Equal(4096, unrelated.Open(unrelatedRoot).Enumerate().LongCount());
        reader.Dispose();
        using var released = new FileStream(hot, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
    }

    private static MetadataTreeRoot[] OwnedContainers(string directory, WindowsJournalFilePins native, int count)
    {
        var roots = new List<MetadataTreeRoot>(); var fences = new List<PagedMetadataStore>();
        try
        {
            for (int index = 0; index < count; index++)
            {
                using (var writer = new PagedMetadataStore(directory, true, filePins: native))
                { roots.Add(writer.Build([new("key", new byte[] { (byte)index })], TestContext.Current.CancellationToken)); writer.Flush(); }
                // An immutable reader forces the next real writer to create
                // another owned container, without a GiB padding allocation.
                var fence = new PagedMetadataStore(directory, filePins: native); fences.Add(fence); _ = fence.Open(roots[^1]).Find("key");
            }
            return roots.ToArray();
        }
        finally { foreach (var fence in fences) fence.Dispose(); }
    }
    private sealed class CountingPins(WindowsJournalFilePins native) : IJournalFilePins
    {
        public int Verifications;
        public string Identify(string path) => native.Identify(path);
        public string IdentifyOpened(FileStream input) => native.IdentifyOpened(input);
        public void Verify(FileStream input, string expectedIdentity) { Verifications++; native.Verify(input, expectedIdentity); }
        public void Pin(string source, string destination, string expectedIdentity) => native.Pin(source, destination, expectedIdentity);
        public bool DetachBeforeTruncate(string path, long length) => native.DetachBeforeTruncate(path, length);
    }
    private static void RequireWindows()
    { if (!OperatingSystem.IsWindows()) Assert.Skip("Native cache fences require Windows."); }
}
