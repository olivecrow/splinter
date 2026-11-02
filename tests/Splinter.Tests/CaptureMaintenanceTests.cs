using Splinter.Core;
using Splinter.Platform.Windows;
using Splinter.Runtime;

namespace Splinter.Tests;

public sealed class CaptureMaintenanceTests
{
    [Fact(Timeout = 10000)]
    public void CompactProofDefersUnownedInitializationWithoutAdoptingOrDeletingIt()
    {
        if (!OperatingSystem.IsWindows()) Assert.Skip("Native metadata ownership.");
        using var files = new TestDirectory(); var pins = new WindowsJournalFilePins(); var ct = TestContext.Current.CancellationToken;
        string directory = Path.Combine(files.Path, "pages"); MetadataTreeRoot root;
        using (var writer = new PagedMetadataStore(directory, true, filePins: pins))
        { root = writer.Build([new("a", new byte[] { 1 })], ct); writer.Flush(); }
        string orphan = Path.Combine(directory, "pages-0000000000000001.bin");
        File.WriteAllBytes(orphan, File.ReadAllBytes(Path.Combine(directory, "identity.bin"))[..16]);
        Assert.False(PagedMetadataStore.VerifyCompactFlatTree(directory, root, pins, ct));
        Assert.Equal(16, new FileInfo(orphan).Length); Assert.False(File.Exists(orphan + ".owner"));
        var result = MetadataMaintenance.Collect(directory, [root], "preserve", pins, ct)!;
        Assert.True(result.PreservedEntries > 0); Assert.True(File.Exists(orphan)); Assert.False(File.Exists(orphan + ".owner"));
    }
    [Theory(Timeout = 10000)]
    [InlineData(100000, 100000)] [InlineData(0, 100000)] [InlineData(1100, 100000)] [InlineData(100000, 0)]
    public void MembershipDrainsValidatesAndFallsBackWithoutLosingEntries(int limit, int capacity)
    {
        using var files = new TestDirectory(); var budget = new ScratchBudget(capacity); var ct = TestContext.Current.CancellationToken;
        bool drained = false;
        IEnumerable<MetadataEntry> Entries()
        {
            for (int i = 0; i < 20; i++) yield return new(i.ToString("d3"), new byte[] { (byte)i });
            yield return new("000", new byte[] { 0 }); drained = true;
        }
        using (var set = new MetadataMembership(Entries(), files.Path, limit, budget, ct))
        {
            Assert.True(drained); Assert.Equal(limit < 100000 || capacity == 0, set.DiskBacked);
            for (int i = 0; i < 20; i++) Assert.True(set.Contains(i.ToString("d3")));
            Assert.False(set.Contains("absent"));
            var aggregate = set.Enumerate(ct).ToArray();
            Assert.Equal(20, aggregate.Length); Assert.Equal(190, aggregate.Sum(e => e.Value.Span[0]));
            using var cancelledEnumeration = new CancellationTokenSource(); cancelledEnumeration.Cancel();
            Assert.ThrowsAny<OperationCanceledException>(() => set.Enumerate(cancelledEnumeration.Token).ToArray());
        }
        Assert.Equal(0, budget.Used);
        Assert.Throws<WorkspaceException>(() => new MetadataMembership([new("a", new byte[] { 1 }), new("a", new byte[] { 2 })], files.Path, limit, budget, ct));
        Assert.Equal(0, budget.Used);
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => new MetadataMembership(Entries(), files.Path, limit, budget, cancelled.Token));
        Assert.Equal(0, budget.Used);
    }

    [Theory(Timeout = 10000)]
    [InlineData("metadata.collection-roots-durable")]
    [InlineData("metadata.collection-location-published")]
    public void AdoptionRetryResumesTheSameCollectionAndNeverRecollectsRanges(string stop)
    {
        if (!OperatingSystem.IsWindows()) Assert.Skip("Native metadata ownership.");
        using var files = new TestDirectory(); var pins = new WindowsJournalFilePins(); var ct = TestContext.Current.CancellationToken;
        var capture = new SourceCaptureIdentity(new string('a', 24), 1, new string('b', 64));
        var row = new CapturedRangeAdoptionEntry(Guid.NewGuid(), 0, 1, new string('c', 64), 0, new string('d', 64), 0, false, 1);
        var first = CapturedRangeAdoptionStore.Publish(files.Path, capture, [row], pins, ct);
        Assert.False(File.Exists(Path.Combine(first.Directory, "locations.json")));
        using (var writer = new PagedMetadataStore(first.Directory, true, filePins: pins))
        { _ = writer.Set(first.Root, "orphan", new byte[] { 1 }); writer.Flush(); }
        Assert.Throws<InvalidOperationException>(() => PagedMetadataStore.Collect(first.Directory, [first.Root], "adoption-" + capture.Id,
            pins, at => { if (at == stop) throw new InvalidOperationException("Injected stop."); }, ct));
        IEnumerable<CapturedRangeAdoptionEntry> Unused() { throw new InvalidOperationException("Never recollect."); }
        Assert.Equal(first, CapturedRangeAdoptionStore.Publish(files.Path, capture, UnusedDeferred(), pins, ct));
        IEnumerable<CapturedRangeAdoptionEntry> UnusedDeferred() { foreach (var item in Unused()) yield return item; }
        Assert.NotNull(PagedMetadataStore.CollectionReceipt(first.Directory, "adoption-" + capture.Id));
        Assert.Equal(row, Assert.Single(CapturedRangeAdoptionStore.Enumerate(first)));
    }

    [Theory(Timeout = 10000)]
    [InlineData(0)] [InlineData(32)]
    public void CompactProofChecksEveryByteAndRetainsWriterExclusion(int count)
    {
        if (!OperatingSystem.IsWindows()) Assert.Skip("Native metadata ownership.");
        using var files = new TestDirectory(); var pins = new WindowsJournalFilePins(); var ct = TestContext.Current.CancellationToken;
        string directory = Path.Combine(files.Path, "pages"); MetadataTreeRoot root;
        using (var writer = new PagedMetadataStore(directory, true, filePins: pins))
        { root = writer.Build(Enumerable.Range(0, count).Select(i => new MetadataEntry(i.ToString("d4"), new byte[] { 1 })), ct); writer.Flush(); }
        Assert.True(PagedMetadataStore.VerifyCompactFlatTree(directory, root, pins, ct));
        using (var reader = new PagedMetadataStore(directory))
            Assert.True(PagedMetadataStore.VerifyCompactFlatTree(directory, root, pins, ct));
        using (var writer = new PagedMetadataStore(directory, true, filePins: pins))
            Assert.ThrowsAny<IOException>(() => PagedMetadataStore.VerifyCompactFlatTree(directory, root, pins, ct));
        File.WriteAllText(Path.Combine(directory, "foreign"), "preserve");
        Assert.False(PagedMetadataStore.VerifyCompactFlatTree(directory, root, pins, ct));
        Assert.Equal("preserve", File.ReadAllText(Path.Combine(directory, "foreign")));
        File.Delete(Path.Combine(directory, "foreign"));
        using (var writer = new PagedMetadataStore(directory, true, filePins: pins))
        { _ = writer.Set(root, "later", new byte[] { 2 }); writer.Flush(); }
        Assert.False(PagedMetadataStore.VerifyCompactFlatTree(directory, root, pins, ct));
        _ = MetadataMaintenance.Collect(directory, [root], "same", pins, ct);
        Assert.False(PagedMetadataStore.VerifyCompactFlatTree(directory, root, pins, ct)); // Existing relocation keeps ordinary GC.
        using var reopened = new PagedMetadataStore(directory); Assert.Equal(count, reopened.Open(root).Enumerate().Count());
    }

    [Theory(Timeout = 10000)]
    [InlineData("damage")] [InlineData("truncate")] [InlineData("owner")]
    public void CompactProofRejectsCorruptReachablePagesAndNativeOwnership(string mutation)
    {
        if (!OperatingSystem.IsWindows()) Assert.Skip("Native metadata ownership.");
        using var files = new TestDirectory(); var pins = new WindowsJournalFilePins(); var ct = TestContext.Current.CancellationToken;
        string directory = Path.Combine(files.Path, "pages"); MetadataTreeRoot root;
        using (var writer = new PagedMetadataStore(directory, true, filePins: pins))
        { root = writer.Build([new("a", new byte[] { 1 }), new("b", new byte[] { 2 })], ct); writer.Flush(); }
        string container = Directory.GetFiles(directory, "pages-*.bin").Single();
        if (mutation == "owner") File.WriteAllText(container + ".owner", "{}");
        else using (var output = new FileStream(container, FileMode.Open, FileAccess.ReadWrite))
        { if (mutation == "truncate") output.SetLength(output.Length - 1); else { output.Position = 90; output.WriteByte(255); } }
        Assert.Throws<WorkspaceException>(() => PagedMetadataStore.VerifyCompactFlatTree(directory, root, pins, ct));
        Assert.True(File.Exists(container));
    }
}
