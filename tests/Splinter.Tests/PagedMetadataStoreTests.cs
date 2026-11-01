using System.Security.Cryptography;
using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using Splinter.Core;
using Splinter.Runtime;

namespace Splinter.Tests;

public sealed class PagedMetadataStoreTests
{
    [Fact]
    public void RebuildingFromTheSameRootReusesPagesAndOneChangedLeafKeepsUnchangedSubtrees()
    {
        using var files = new TestDirectory(); string path = Path.Combine(files.Path, "metadata");
        using var store = new PagedMetadataStore(path, true); var ct = TestContext.Current.CancellationToken;
        var first = store.Build(Entries(1024), ct); store.Flush(); long before = PageBytes();
        var repeated = store.Build(Entries(1024), ct, first); store.Flush();
        Assert.Equal(first, repeated); Assert.Equal(before, PageBytes());
        var changed = store.Build(Entries(1024).Select(entry => entry.Key == "00000500" ? entry with { Value = "changed"u8.ToArray() } : entry), ct, repeated);
        store.Flush(); Assert.NotEqual(first.ContentHash, changed.ContentHash);
        Assert.True(PageBytes() - before < before / 4);
        Assert.Equal("value-500", Encoding.UTF8.GetString(store.Open(first).Find("00000500")!));
        Assert.Equal("changed", Encoding.UTF8.GetString(store.Open(changed).Find("00000500")!));
        long PageBytes() => Directory.EnumerateFiles(path, "pages-*.bin").Sum(file => new FileInfo(file).Length);
    }

    [Fact]
    public void BulkAndDifferentInsertionOrdersHaveTheSameLogicalIdentity()
    {
        using var files = new TestDirectory();
        using var store = new PagedMetadataStore(Path.Combine(files.Path, "metadata"), writable: true);
        var entries = Entries(257).ToArray();
        var bulk = store.Build(entries, TestContext.Current.CancellationToken);
        var forward = store.Empty; var reverse = store.Empty;
        foreach (var entry in entries) forward = store.Set(forward, entry.Key, entry.Value.Span);
        foreach (var entry in entries.Reverse()) reverse = store.Set(reverse, entry.Key, entry.Value.Span);
        Assert.Equal(bulk.ContentHash, forward.ContentHash); Assert.Equal(bulk.ContentHash, reverse.ContentHash);
        Assert.Equal(257, bulk.Count); Assert.Equal(entries.Select(e => e.Key), store.Open(bulk).Enumerate().Select(e => e.Key));
        Assert.Equal(forward, store.Set(forward, entries[0].Key, entries[0].Value.Span));
    }

    [Fact]
    public void OldRootsAndCursorReadsSurviveLaterEditsAndReopening()
    {
        using var files = new TestDirectory(); string directory = Path.Combine(files.Path, "metadata");
        MetadataTreeRoot first, second;
        using (var store = new PagedMetadataStore(directory, writable: true))
        {
            first = store.Build(Entries(1024), TestContext.Current.CancellationToken); second = store.Set(first, "00000500", "later"u8); store.Flush();
            Assert.Equal("value-500", Encoding.UTF8.GetString(store.Open(first).Find("00000500")!));
        }
        using var reader = new PagedMetadataStore(directory);
        Assert.Equal("value-500", Encoding.UTF8.GetString(reader.Open(first).Find("00000500")!));
        Assert.Equal("later", Encoding.UTF8.GetString(reader.Open(second).Find("00000500")!));
        Assert.Equal(Entries(1024).Skip(501).Select(e => e.Key), reader.Open(first).Enumerate("00000500").Select(e => e.Key));
        Assert.Null(reader.Open(first).Find("missing"));
    }

    [Theory]
    [InlineData(32769)]
    [InlineData(262145)]
    [InlineData(1048576)]
    public void LargeSortedInputsAreBuiltAndLookedUpWithoutWholeInputMaterialization(int count)
    {
        using var files = new TestDirectory(); string directory = Path.Combine(files.Path, "metadata");
        MetadataTreeRoot root;
        using (var store = new PagedMetadataStore(directory, writable: true))
        {
            root = store.Build(Entries(count), TestContext.Current.CancellationToken); store.Flush();
            Assert.Equal(count, root.Count);
        }
        using var reader = new PagedMetadataStore(directory); var tree = reader.Open(root);
        foreach (int index in new[] { 0, count / 2, count - 1 })
            Assert.Equal("value-" + index, Encoding.UTF8.GetString(tree.Find(index.ToString("D8"))!));
        Assert.Equal(10, tree.Enumerate((count - 11).ToString("D8")).Count());
        long enumerated = 0;
        foreach (var entry in tree.Enumerate())
        {
            TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
            Assert.Equal(enumerated.ToString("D8"), entry.Key); enumerated++;
        }
        Assert.Equal(count, enumerated);
        Assert.InRange(PagedMetadataStore.CachedMetadataBytes, 0, 32L * 1024 * 1024);
        Assert.DoesNotContain(Directory.EnumerateFiles(directory), p => p.EndsWith(".pending", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("checksum")] [InlineData("truncate")] [InlineData("wrong-store")] [InlineData("wrong-version")]
    [InlineData("wrong-hash")] [InlineData("bad-length")] [InlineData("bad-count")]
    public void CorruptRootsAndPagesAreRefusedWithoutRewritingData(string damage)
    {
        using var files = new TestDirectory(); string directory = Path.Combine(files.Path, "metadata");
        MetadataTreeRoot root;
        using (var store = new PagedMetadataStore(directory, writable: true))
        { root = store.Build(Entries(17), TestContext.Current.CancellationToken); store.Flush(); }
        string container = Directory.EnumerateFiles(directory, "pages-*.bin").Single();
        if (damage is "checksum" or "truncate")
        {
            using var output = new FileStream(container, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            if (damage == "truncate") output.SetLength(root.Page!.Offset + root.Page.Bytes - 1);
            else { output.Position = root.Page!.Offset + root.Page.Bytes - 1; output.WriteByte(0xff); }
        }
        root = damage switch
        {
            "wrong-store" => root with { StoreId = new string('0', 24) },
            "wrong-version" => root with { Version = 0 },
            "wrong-hash" => root with { Page = root.Page! with { ContentHash = new string('0', 64) } },
            "bad-length" => root with { Page = root.Page! with { Bytes = int.MaxValue } },
            "bad-count" => root with { Page = root.Page! with { Count = root.Count + 1 } },
            _ => root
        };
        byte[] before = SHA256.HashData(File.ReadAllBytes(container));
        using var reader = new PagedMetadataStore(directory);
        Assert.Equal("MetadataStoreInvalid", Assert.Throws<WorkspaceException>(() => reader.Open(root)).Code);
        Assert.Equal(before, SHA256.HashData(File.ReadAllBytes(container)));
    }

    [Fact]
    public void DuplicateUnsortedAndOversizedRecordsAreRejectedBeforePublication()
    {
        using var files = new TestDirectory();
        using var store = new PagedMetadataStore(Path.Combine(files.Path, "metadata"), writable: true);
        Assert.Throws<WorkspaceException>(() => store.Build([new("same", new byte[1]), new("same", new byte[1])], TestContext.Current.CancellationToken));
        Assert.Throws<WorkspaceException>(() => store.Build([new("z", new byte[1]), new("a", new byte[1])], TestContext.Current.CancellationToken));
        Assert.Throws<WorkspaceException>(() => store.Set(store.Empty, "huge", new byte[65536]));
        var valid = store.Build(Entries(10), TestContext.Current.CancellationToken); store.Flush(); Assert.Equal(10, store.Open(valid).Root.Count);
    }

    [Fact]
    public void AnUnpublishedTailCannotChangeAnEarlierDurableRoot()
    {
        using var files = new TestDirectory(); string directory = Path.Combine(files.Path, "metadata");
        MetadataTreeRoot root;
        using (var store = new PagedMetadataStore(directory, writable: true))
        {
            root = store.Build(Entries(50), TestContext.Current.CancellationToken); store.Flush();
            _ = store.Set(root, "unpublished", "do not publish this root"u8);
        }
        using var reader = new PagedMetadataStore(directory);
        Assert.Null(reader.Open(root).Find("unpublished")); Assert.Equal(50, reader.Open(root).Enumerate().Count());
    }

    [Fact]
    public void ReturnedValueBuffersCannotMutateAnImmutablePageCacheEntry()
    {
        using var files = new TestDirectory();
        using var store = new PagedMetadataStore(Path.Combine(files.Path, "metadata"), writable: true);
        var root = store.Build(Entries(10), TestContext.Current.CancellationToken); store.Flush();
        byte[] returned = store.Open(root).Find("00000000")!; returned[0] = 0;
        Assert.Equal("value-0", Encoding.UTF8.GetString(store.Open(root).Find("00000000")!));
    }

    [Fact]
    public void CursorIsBoundToTheLogicalRootAndPagesDoNotSkipEntries()
    {
        using var files = new TestDirectory();
        using var store = new PagedMetadataStore(Path.Combine(files.Path, "metadata"), writable: true);
        var root = store.Build(Entries(1030), TestContext.Current.CancellationToken); store.Flush();
        var tree = store.Open(root); MetadataCursor? cursor = null; int count = 0;
        do
        {
            var page = tree.ReadPage(cursor, 17); count += page.Entries.Length; cursor = page.NextCursor;
        } while (cursor is not null);
        Assert.Equal(1030, count);
        Assert.Equal("MetadataCursorInvalid", Assert.Throws<WorkspaceException>(() =>
            tree.ReadPage(new(new string('0', 64), "00000001"))).Code);
        tree.Verify(TestContext.Current.CancellationToken);
    }

    [Theory]
    [InlineData("metadata.identity-prefix-durable")]
    [InlineData("metadata.identity-durable")]
    [InlineData("metadata.identity-published")]
    public void InterruptedInitializationResumesTheOwnedPrefixWithoutTruncation(string point)
    {
        using var files = new TestDirectory(); string directory = Path.Combine(files.Path, "metadata");
        Assert.Throws<IOException>(() => new PagedMetadataStore(directory, writable: true,
            boundary: reached => { if (reached == point) throw new IOException("Injected interruption"); }));
        using var store = new PagedMetadataStore(directory, writable: true);
        var root = store.Build(Entries(17), TestContext.Current.CancellationToken); store.Flush();
        Assert.Equal(17, store.Open(root).Enumerate().Count());
        Assert.False(File.Exists(Path.Combine(directory, "identity.bin.pending")));
    }

    [Fact]
    public void FailedUnpublishedPageAppendDoesNotRequireDiscardingAnOlderRoot()
    {
        using var files = new TestDirectory(); string directory = Path.Combine(files.Path, "metadata");
        MetadataTreeRoot root; bool fail = false;
        using (var store = new PagedMetadataStore(directory, writable: true,
            boundary: reached => { if (fail && reached == "metadata.page-header-written") throw new IOException("Injected interruption"); }))
        {
            root = store.Build(Entries(50), TestContext.Current.CancellationToken); store.Flush(); fail = true;
            Assert.Throws<IOException>(() => store.Set(root, "unpublished", "later"u8));
        }
        using var recovered = new PagedMetadataStore(directory, writable: true);
        Assert.Equal(50, recovered.Open(root).Enumerate().Count());
        var next = recovered.Set(root, "next", "after recovery"u8); recovered.Flush();
        Assert.Equal(51, recovered.Open(next).Enumerate().Count()); Assert.Null(recovered.Open(root).Find("next"));
    }

    [Fact]
    public void PersistentDeletionHasTheSameIdentityAsRebuildingSurvivors()
    {
        using var files = new TestDirectory();
        using var store = new PagedMetadataStore(Path.Combine(files.Path, "metadata"), writable: true);
        var original = store.Build(Entries(500), TestContext.Current.CancellationToken); var removed = original;
        for (int index = 0; index < 500; index += 3) removed = store.Remove(removed, index.ToString("D8"));
        var rebuilt = store.Build(Entries(500).Where((_, index) => index % 3 != 0), TestContext.Current.CancellationToken); store.Flush();
        Assert.Equal(rebuilt.ContentHash, removed.ContentHash); Assert.Equal(500, original.Count);
        Assert.Equal(original, store.Remove(original, "not-present"));
        foreach (var entry in Entries(500)) removed = store.Remove(removed, entry.Key);
        Assert.Equal(store.Empty, removed);
    }

    [Theory]
    [InlineData("{\"Version\":1,\"StoreId\":\"000000000000000000000000\"}")]
    [InlineData("{\"Version\":1,\"StoreId\":\"000000000000000000000000\",\"Page\":null,\"legacy\":true}")]
    public void MissingOrUnknownRootFieldsCannotSilentlyBecomeAnEmptyResult(string json)
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<MetadataTreeRoot>(json));
    }

    [Fact]
    public void ARejectedForeignContainerCannotEnterTheReaderHandleCache()
    {
        using var files = new TestDirectory(); string directory = Path.Combine(files.Path, "metadata");
        MetadataTreeRoot root;
        using (var store = new PagedMetadataStore(directory, writable: true))
        { root = store.Build(Entries(10), TestContext.Current.CancellationToken); store.Flush(); }
        string path = Directory.EnumerateFiles(directory, "pages-*.bin").Single();
        using (var output = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            byte[] header = new byte[48]; output.ReadExactly(header); header[4] ^= 1;
            SHA256.HashData(header.AsSpan(0, 16), header.AsSpan(16)); output.Position = 0; output.Write(header); output.Flush(true);
        }
        using var reader = new PagedMetadataStore(directory);
        Assert.Throws<WorkspaceException>(() => reader.Open(root));
        Assert.Throws<WorkspaceException>(() => reader.Open(root));
        // The failed proof must also close its handle, rather than block an
        // explicit repair/inspection of the retained foreign file.
        using var probe = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Assert.True(probe.Length > 48);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void AdversarialDeepTreesSpillTheirBuildAndReadSpinesWithoutAStackLimit(bool decreasing)
    {
        // An increasing/decreasing subsequence of priorities creates a real
        // one-sided Cartesian tree, instead of hoping random input is deep.
        const int candidates = 8192;
        var priority = new ulong[candidates]; var previous = new int[candidates]; var tails = new List<int>();
        for (int index = 0; index < candidates; index++)
        {
            ulong value = BinaryPrimitives.ReadUInt64BigEndian(SHA256.HashData(Encoding.UTF8.GetBytes(index.ToString("D8"))));
            priority[index] = decreasing ? ulong.MaxValue - value : value;
            int low = 0, high = tails.Count;
            while (low < high)
            { int middle = (low + high) / 2; if (priority[tails[middle]] < priority[index]) low = middle + 1; else high = middle; }
            previous[index] = low == 0 ? -1 : tails[low - 1];
            if (low == tails.Count) tails.Add(index); else tails[low] = index;
        }
        var selected = new List<int>();
        for (int index = tails[^1]; index >= 0; index = previous[index]) selected.Add(index);
        selected.Reverse(); Assert.True(selected.Count > 64);
        using var files = new TestDirectory(); string directory = Path.Combine(files.Path, "metadata");
        using var store = new PagedMetadataStore(directory, writable: true);
        var root = store.Build(selected.Select(i => new MetadataEntry(i.ToString("D8"), Encoding.UTF8.GetBytes("value"))), TestContext.Current.CancellationToken);
        store.Flush(); Assert.Equal(selected.Select(i => i.ToString("D8")), store.Open(root).Enumerate().Select(e => e.Key));
        Assert.Empty(Directory.EnumerateFiles(directory, "*.pending"));
    }

    private static IEnumerable<MetadataEntry> Entries(int count)
    {
        for (int index = 0; index < count; index++)
            yield return new(index.ToString("D8"), Encoding.UTF8.GetBytes("value-" + index));
    }
}
