using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Splinter.Core;
using Splinter.Platform.Windows;
using Splinter.Runtime;
using Splinter.SourceControl.Files;

namespace Splinter.Tests;

public sealed class MetadataEncodingTests
{
    private static readonly long Ticks = DateTime.FromFileTimeUtc(123).Ticks;
    private static OriginalFileVersion FileVersion => new("file", 12, new string('a', 64), 32, Ticks, 420);

    [Fact(Timeout = 10000)]
    public void SmallPageCostRecordsTheRemainingOneKeyPerNodeBoundary()
    {
        using var files = new TestDirectory(); int writes = 0;
        using var pages = new PagedMetadataStore(files.Path, true, point => { if (point == "metadata.page-header-written") writes++; });
        var root = pages.Build(Enumerable.Range(0, 64).Select(i => new MetadataEntry(i.ToString("d4"), new byte[16])), TestContext.Current.CancellationToken);
        pages.Flush(); Assert.Equal(64, writes); pages.Open(root).Verify(TestContext.Current.CancellationToken);
        long bytes = Directory.EnumerateFiles(files.Path, "pages-*.bin").Sum(path => new FileInfo(path).Length) - 48;
        // 40-byte checksummed header + 9-byte lengths/flags + key/value,
        // and 63 non-null 61-byte child references. Null links are omitted.
        Assert.Equal(64 * (40 + 9 + 4 + 16) + 63 * 61, bytes);
        TestContext.Current.TestOutputHelper!.WriteLine($"remaining tree cost: 64 keys, {writes} pages, {bytes} page bytes, 1280 key/value bytes");
    }

    [Fact(Timeout = 10000)]
    public void PublicationDeltasRetainEveryExpectedFieldAndHistoricalPlan()
    {
        using var files = new TestDirectory(); var ct = TestContext.Current.CancellationToken;
        var before = FileVersion;
        var after = before with { Length = 13, Hash = new string('b', 64), ModifiedUtcTicks = Ticks + 1 };
        var expected = before with { Attributes = 32 | 8192 };
        var change = new OriginalApplyChange("Assets/한글.asset", before, after, new("native-file", expected), false);
        var raw = OriginalApplyPages.EncodeChange(change); Assert.Equal(change, OriginalApplyPages.DecodeChange(raw));
        Assert.True(raw.Length < LegacyChange(change).Length);
        var template = new OriginalApplyPlan(3, new string('a', 24), "request", "base", "result", files.Path, "root", [], [], "");
        var first = OriginalApplyPages.Create(template, [change], files.Path, ct); OriginalApplyPages.SavePlan(first, files.Path);
        var second = OriginalApplyPages.Create(template, [change with { Expected = new("native-file", after) }], files.Path, ct);
        Assert.NotEqual(first.Digest, second.Digest);
        Assert.Equal(change, OriginalApplyPages.ReadPlan(files.Path, first.Digest).ChangeAt(0));
        Assert.Equal(expected.Attributes, first.ChangeAt(0).Expected!.Version.Attributes);
        TestContext.Current.TestOutputHelper!.WriteLine($"publication inline bytes: {LegacyChange(change).Length} -> {raw.Length}");
    }

    [Fact(Timeout = 10000)]
    public void PublicationDeltasCoverNullsKindsAllFieldsAndReferenceChoices()
    {
        var file = FileVersion; var directory = new OriginalFileVersion("directory", 0, null, 16, Ticks);
        OriginalFileVersion?[] versions = [null, file, file with { UnixMode = null }, file with { UnixMode = 0 },
            file with { Length = 0, Hash = new string('b', 64), Attributes = 0, ModifiedUtcTicks = Ticks + 7, UnixMode = 511 }, directory];
        foreach (var before in versions)
        foreach (var after in versions)
        foreach (var observed in versions)
        {
            TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
            var change = new OriginalApplyChange("p", before, after, observed is null ? null : new("native", observed), true);
            Assert.Equal(change, OriginalApplyPages.DecodeChange(OriginalApplyPages.EncodeChange(change)));
        }
        // A small record can reference After when Before is absent.
        var added = new OriginalApplyChange("p", null, file, new("native", file), false);
        Assert.Equal(added, OriginalApplyPages.DecodeChange(OriginalApplyPages.EncodeChange(added)));
    }

    [Fact]
    public void PublicationDeltasRejectOldTruncatedUnknownAndMissingReferences()
    {
        var change = new OriginalApplyChange("p", null, FileVersion, new("native", FileVersion), false);
        byte[] raw = OriginalApplyPages.EncodeChange(change);
        for (int n = 0; n < raw.Length; n++) Assert.Throws<WorkspaceException>(() => OriginalApplyPages.DecodeChange(raw.AsSpan(0, n)));
        Assert.Throws<WorkspaceException>(() => OriginalApplyPages.DecodeChange([.. raw, 0]));
        foreach (byte tag in new byte[] { 1, 3, 255 })
        { byte[] bad = raw.ToArray(); bad[0] = tag; Assert.Throws<WorkspaceException>(() => OriginalApplyPages.DecodeChange(bad)); }
        foreach (byte tag in new byte[] { 2, 3, 255 })
        { byte[] bad = raw.ToArray(); bad[7] = tag; Assert.Throws<WorkspaceException>(() => OriginalApplyPages.DecodeChange(bad)); }
        var reference = OriginalApplyPages.EncodeChange(change with { Before = FileVersion });
        // Before is 58 bytes including Unix mode; After then has reference + mask.
        reference[65] = 128; Assert.Throws<WorkspaceException>(() => OriginalApplyPages.DecodeChange(reference));
    }

    [Theory(Timeout = 10000)]
    [InlineData(0, 0)] [InlineData(10, 0)] [InlineData(0, 20)] [InlineData(10, 20)]
    public void DirectoryTimesUseCanonicalDefaultFlagsAndPreserveAllMetadata(long creation, long access)
    {
        using var files = new TestDirectory(); using var pages = new PagedMetadataStore(files.Path, true);
        foreach (bool sensitive in new[] { false, true })
        foreach (uint? mode in new uint?[] { null, 0, 420 })
        {
            TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
            var codec = new DirectoryMetadataTree(pages, sensitive);
            var entry = new DirectoryMetadataRecord(new("MiXeD한글", false, 12, 32, creation, access, 30, mode), new string('a', 64), SourceReference: "revision");
            var raw = codec.Encode(entry); Assert.Equal(entry, codec.Decode(raw));
            int legacy = 46 + Encoding.UTF8.GetByteCount(entry.Entry.Path) + 32 + (mode is null ? 0 : 4) + 4 + 8;
            Assert.Equal(legacy - 20 + (creation == 0 ? 0 : 8) + (access == 0 ? 0 : 8), raw.Value.Length);
            var first = codec.Set(codec.Empty, entry); var second = codec.Set(first, entry with { Entry = entry.Entry with { CreationTime = 99 } }); pages.Flush();
            Assert.NotEqual(first.ContentHash, second.ContentHash); Assert.Equal(entry, codec.Find(first, entry.Entry.Path));
            for (int n = 0; n < raw.Value.Length; n++)
                Assert.Throws<WorkspaceException>(() => codec.Decode(raw with { Value = raw.Value[..n] }));
            var bad = raw.Value.ToArray(); bad[0] = 1; Assert.Throws<WorkspaceException>(() => codec.Decode(raw with { Value = bad }));
            bad[0] = 2; bad[1] |= 128; Assert.Throws<WorkspaceException>(() => codec.Decode(raw with { Value = bad }));
        }
    }

    [Fact(Timeout = 10000)]
    public void TemporaryComparisonRowsHaveOnePathAndRoundTripThroughDisk()
    {
        using var files = new TestDirectory(); using var spool = new MetadataSpool(files.Path, 0, new ScratchBudget(0));
        string path = string.Join('/', Enumerable.Repeat("한글" + new string('x', 120), 90));
        FileSourceEntry[] entries = [new(path, "file", 12, new string('a', 64), Ticks, 32, 420),
            new("Mixed/file", "file", 0, null, 0, 0, 0), new("Mixed/dir", "directory", 0, null, Ticks, 16)];
        foreach (var entry in entries) { TestContext.Current.CancellationToken.ThrowIfCancellationRequested(); spool.Append(new(entry.Path, FileApplyIndex.EncodeTemporary(entry))); }
        spool.Append(new("gone", new byte[] { 0 }));
        var read = spool.Read().ToArray(); Assert.Equal(entries, read.Take(3).Select(row => FileApplyIndex.DecodeTemporary(row)!));
        Assert.Null(FileApplyIndex.DecodeTemporary(read[3])); Assert.Equal(57, read[0].Value.Length);
        foreach (var row in read.Take(3))
        {
            for (int n = 0; n < row.Value.Length; n++) Assert.Throws<WorkspaceException>(() => FileApplyIndex.DecodeTemporary(row with { Value = row.Value[..n] }));
            Assert.Throws<WorkspaceException>(() => FileApplyIndex.DecodeTemporary(row with { Value = new byte[] { 255 } }));
            Assert.Throws<WorkspaceException>(() => FileApplyIndex.DecodeTemporary(row with { Key = "../foreign" }));
        }
        var oldRow = new MetadataEntry(path, JsonSerializer.SerializeToUtf8Bytes(entries[0]));
        TestContext.Current.TestOutputHelper!.WriteLine($"comparison value bytes, long path omitted: {oldRow.Value.Length} -> {read[0].Value.Length}; checksummed spool record: {RecordBytes(oldRow)} -> {RecordBytes(read[0])}");
    }

    [Fact(Timeout = 10000)]
    public void OriginalSpellingSortSurvivesMultipleMergeLevelsWithoutASecondName()
    {
        using var files = new TestDirectory(); var ct = TestContext.Current.CancellationToken;
        string[] names = Enumerable.Range(0, 140).Select(i => (i % 2 == 0 ? "mIxEd한글" : "MIXed") + i.ToString("d4"))
            .Concat(["a", "[", "_", "A", "é", "É", "𐐨", "𐐀"]).Reverse().ToArray();
        var rows = names.Select(name => new MetadataEntry(name, new byte[1000]));
        var sorted = MetadataExternalSort.Order(rows, files.Path, ct, null, 1800, new ScratchBudget(8192), StringComparer.OrdinalIgnoreCase).ToArray();
        Assert.Equal(names.OrderBy(name => name.ToUpperInvariant(), StringComparer.Ordinal).Select(name => name.ToUpperInvariant()), sorted.Select(row => row.Key.ToUpperInvariant()));
        Assert.Equal(names.Order(StringComparer.Ordinal), sorted.Select(row => row.Key).Order(StringComparer.Ordinal));
        Assert.Empty(Directory.EnumerateDirectories(files.Path, "sort-*"));
    }

    [Fact(Timeout = 10000)]
    public void BinaryListingPreservesFixedMetadataOverlayCaseAndAvailability()
    {
        using var files = new TestDirectory(); var snapshot = new ListingTree();
        snapshot.Entries.Add(new("MiXeD", false, 12, 32, 10, 20, 30, 420));
        snapshot.Entries.Add(new("hidden", false, 0, 32, 0, 0, 0));
        snapshot.Entries.Add(new("untouched", false, 12, 32, 10, 20, 30, 420));
        using var journal = new JournalDeltaStore(files.Path, new(3, "binary-listing", snapshot.Snapshot, 4096, 65536), snapshot);
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        var id = journal.Stat("MiXeD")!.Id; journal.SetLength(id, 21); journal.Delete(journal.Stat("hidden")!.Id);
        var fixedOverlay = journal.Get(id);
        var held = journal.EnumerateChildren(""); journal.Create("later", false, 32);
        var result = held.ToArray(); Assert.Equal(2, result.Length);
        var listed = Assert.Single(result, row => row.Path == "MiXeD"); Assert.Equal(fixedOverlay, listed);
        var untouched = Assert.Single(result, row => row.Path == "untouched");
        Assert.Equal((uint)420, untouched.UnixMode); Assert.Equal(10, untouched.CreationTime); Assert.Equal(20, untouched.LastAccessTime); Assert.Equal(30, untouched.LastWriteTime);
        snapshot.Available = false; Assert.Throws<WorkspaceException>(() => journal.EnumerateChildren("").ToArray());
        var basis = snapshot.Entries[0]; byte[] raw = JournalDeltaStore.EncodeListedMetadata(basis);
        Assert.Equal(basis, JournalDeltaStore.DecodeListedMetadata(basis.Path, raw));
        for (int n = 0; n < raw.Length; n++) Assert.Throws<WorkspaceException>(() => JournalDeltaStore.DecodeListedMetadata(basis.Path, raw.AsSpan(0, n)));
        Assert.Null(JournalDeltaStore.DecodeListedMetadata("overlay", [0]));
        Assert.Throws<WorkspaceException>(() => JournalDeltaStore.DecodeListedMetadata(basis.Path, [.. raw, 0]));
        int oldBytes = JsonSerializer.SerializeToUtf8Bytes(new { Name = "MiXeD", Entry = basis }).Length;
        TestContext.Current.TestOutputHelper!.WriteLine($"listing value bytes (name now only key): {oldBytes} -> {raw.Length}");
    }

    [Theory(Timeout = 10000)]
    [InlineData(false)] [InlineData(true)]
    public void PreviousMetadataStoreIsRefusedBeforeAppendOrCollection(bool writable)
    {
        using var files = new TestDirectory(); var pins = new WindowsJournalFilePins();
        using (var pages = new PagedMetadataStore(files.Path, true, filePins: pins)) { _ = pages.Set(pages.Empty, "a", "old"u8); pages.Flush(); }
        string identity = Path.Combine(files.Path, "identity.bin"); byte[] header = System.IO.File.ReadAllBytes(identity);
        "SPM1"u8.CopyTo(header); SHA256.HashData(header.AsSpan(0, 16), header.AsSpan(16)); System.IO.File.WriteAllBytes(identity, header);
        var before = Directory.EnumerateFiles(files.Path).ToDictionary(p => Path.GetFileName(p)!, p => SHA256.HashData(System.IO.File.ReadAllBytes(p)));
        Assert.Equal("MetadataStoreInvalid", Assert.Throws<WorkspaceException>(() => { using var ignored = new PagedMetadataStore(files.Path, writable); }).Code);
        Assert.Throws<WorkspaceException>(() => PagedMetadataStore.Collect(files.Path, [], "old", pins, ct: TestContext.Current.CancellationToken));
        Assert.Equal(before.Keys.Order(), Directory.EnumerateFiles(files.Path).Select(Path.GetFileName).Order());
        foreach (var pair in before) Assert.Equal(pair.Value, SHA256.HashData(System.IO.File.ReadAllBytes(Path.Combine(files.Path, pair.Key!))));
    }

    private sealed class ListingTree : ISnapshotTree
    {
        internal readonly List<SnapshotEntry> Entries = [];
        internal bool Available = true;
        public SnapshotReference Snapshot { get; } = new("fixed-listing", "fake", "basis", "root", DateTimeOffset.UnixEpoch, true, true, "fixture");
        public void EnsureAvailable() { if (!Available) throw new WorkspaceException("SnapshotMissing", "injected"); }
        public SnapshotEntry? Stat(string path) { EnsureAvailable(); return path == "" ? new("", true, 0, 16, 0, 0, 0) : Entries.SingleOrDefault(entry => entry.Path.Equals(path, StringComparison.OrdinalIgnoreCase)); }
        public IReadOnlyList<SnapshotEntry> List(string path) { EnsureAvailable(); return Entries; }
        public int Read(string path, long offset, Span<byte> target) { EnsureAvailable(); return 0; }
    }

    private static long RecordBytes(MetadataEntry record)
    { using var output = new MemoryStream(); MetadataExternalSort.Write(output, record); return output.Length; }

    // Exact old value codec used only to measure this fixed small input.
    private static byte[] LegacyChange(OriginalApplyChange change)
    {
        using var memory = new MemoryStream(); using var writer = new BinaryWriter(memory, Encoding.UTF8, true);
        void Text(string text) { byte[] bytes = Encoding.UTF8.GetBytes(text); writer.Write(bytes.Length); writer.Write(bytes); }
        void Version(OriginalFileVersion? v)
        {
            writer.Write(v is null ? (byte)0 : v.Kind == "directory" ? (byte)1 : (byte)2); if (v is null) return;
            writer.Write(v.Length); writer.Write(v.Attributes); writer.Write(v.ModifiedUtcTicks);
            if (v.Kind == "file") writer.Write(Convert.FromHexString(v.Hash!));
            writer.Write(v.UnixMode is not null); if (v.UnixMode is { } mode) writer.Write(mode);
        }
        writer.Write((byte)1); Text(change.Path); Version(change.Before); Version(change.After);
        writer.Write(change.Expected is not null); if (change.Expected is { } observed) { Text(observed.Identity); Version(observed.Version); }
        writer.Write(change.AlreadyMatches); return memory.ToArray();
    }
}
