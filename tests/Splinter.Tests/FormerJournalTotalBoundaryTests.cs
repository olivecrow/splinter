using System.Buffers.Binary;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Splinter.Core;
using Splinter.Platform.Windows;
using Splinter.Runtime;

namespace Splinter.Tests;

// This models a durable product checkpoint with streamed binary metadata. It
// qualifies actual reopen/lookup/range mutation past the former totals, not a
// million individual durable ACKs or a native million-file project lifecycle.
public sealed class FormerJournalTotalBoundaryTests
{
    [Fact]
    public void ProductCheckpointReopensBeyondFormerNodeWhiteoutAndExtentTotals()
    {
        const int files = 250001, ranges = 1000001;
        using var root = new TestDirectory(); var basis = new SnapshotOverlayTests.FakeSnapshotTree(); var pins = new WindowsJournalFilePins();
        var identity = new OverlayIdentity(3, "former-total-boundaries", basis.Snapshot, long.MaxValue, 65536);
        string delta = Path.Combine(root.Path, "delta"), captureId = new('a', 24), capturePath = Path.Combine(root.Path, "captures", captureId);
        Guid last = FileId(files - 1); long lastOffset = 2L * (ranges - 1);
        using (var writer = new JournalDeltaStore(delta, identity, basis, sourceFilePins: pins))
        {
            var seed = writer.Create("seed", false, 32); writer.Write(seed.Id, 0, "x"u8); writer.CaptureSource(capturePath, captureId, []);
            CapturedPrivateSourceRange raw;
            using (var capture = new SourceCaptureTree(capturePath, basis, pins)) raw = Assert.Single(capture.GetFileRanges("seed")!.PrivateRanges);
            long physical = raw.Backing!.Offset, segment = long.Parse(Path.GetFileName(raw.Backing.Path).AsSpan(8, 16), System.Globalization.NumberStyles.HexNumber);
            SourceCaptureTree.Complete(capturePath, pins); writer.CompleteSourceCaptureRequest(captureId);
            var pages = (PagedMetadataStore)Field("pagedIndex").GetValue(writer)!; var rootEntry = writer.Stat("")!;
            var extentRoot = pages.Build(Enumerable.Range(0, ranges).Select(index =>
                new MetadataEntry((2L * index).ToString("x16"), Extent(2L * index, physical, segment))), TestContext.Current.CancellationToken);
            IEnumerable<MetadataEntry> Components()
            {
                for (int index = 0; index < files; index++)
                {
                    TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
                    foreach (string path in new[] { FileName(index), HiddenName(index) })
                    {
                        byte[] name = Encoding.UTF8.GetBytes(path), bytes = new byte[33 + name.Length]; bytes[0] = 1; name.CopyTo(bytes, 33);
                        yield return new(Convert.ToHexStringLower(NameId(path)), bytes);
                    }
                }
            }
            var names = pages.Build(MetadataExternalSort.Order(Components(), root.Path, TestContext.Current.CancellationToken, pins), TestContext.Current.CancellationToken);
            IEnumerable<MetadataEntry> Nodes()
            {
                bool emittedRoot = false; string rootKey = rootEntry.Id.ToString("N");
                for (int index = 0; index < files; index++)
                {
                    string key = FileId(index).ToString("N");
                    if (!emittedRoot && StringComparer.Ordinal.Compare(rootKey, key) < 0)
                    { emittedRoot = true; yield return new(rootKey, Node(rootEntry, [])); }
                    var entry = new DeltaEntry(FileId(index), FileName(index), false, index == files - 1 ? lastOffset + 1 : 0, 32, 0, 0, 0, null, 0);
                    yield return new(key, Node(entry, NameId(entry.Path)), index == files - 1 ? extentRoot : null);
                }
                if (!emittedRoot) yield return new(rootKey, Node(rootEntry, []));
            }
            var nodes = pages.Build(Nodes(), TestContext.Current.CancellationToken);
            var children = pages.Build(Enumerable.Range(0, files).Select(index => new MetadataEntry(FileName(index).ToUpperInvariant(),
                Namespace(FileName(index), FileId(index), false, 1))), TestContext.Current.CancellationToken);
            var paths = pages.Set(pages.Empty, "!", Namespace("", rootEntry.Id, true, files + 1L), children);
            IEnumerable<MetadataEntry> Hidden(bool changes)
            {
                for (int index = 0; index < files; index++)
                {
                    string path = HiddenName(index); byte[] bytes = new byte[changes ? 44 : 36];
                    BinaryPrimitives.WriteInt32LittleEndian(bytes, -32); NameId(path).CopyTo(bytes, 4);
                    if (changes) BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(36), writer.Observe().DurableSequence);
                    yield return new("p" + Convert.ToHexStringLower(NameId(path.ToUpperInvariant())), bytes);
                }
            }
            // Hidden keys are component hashes, so sorting is disk-backed.
            var hidden = pages.Build(MetadataExternalSort.Order(Hidden(false), root.Path, TestContext.Current.CancellationToken, pins), TestContext.Current.CancellationToken);
            long sequence = writer.Observe().DurableSequence;
            IEnumerable<MetadataEntry> Changes()
            {
                for (int index = 0; index < files; index++)
                {
                    string path = HiddenName(index); byte[] bytes = new byte[44]; BinaryPrimitives.WriteInt32LittleEndian(bytes, -32);
                    NameId(path).CopyTo(bytes, 4); BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(36), sequence);
                    yield return new("p" + Convert.ToHexStringLower(NameId(path.ToUpperInvariant())), bytes);
                }
            }
            var changes = pages.Build(MetadataExternalSort.Order(Changes(), root.Path, TestContext.Current.CancellationToken, pins), TestContext.Current.CancellationToken);
            var durable = pages.Build(Enumerable.Range(0, files).Select(index =>
            {
                string key = "g" + FileId(index).ToString("N"); byte[] text = Encoding.UTF8.GetBytes(key), bytes = new byte[4 + text.Length];
                BinaryPrimitives.WriteInt32LittleEndian(bytes, text.Length); text.CopyTo(bytes, 4); return new MetadataEntry(key, bytes);
            }), TestContext.Current.CancellationToken);
            Root(Field("nodes").GetValue(writer)!, nodes); Root(Field("paths").GetValue(writer)!, paths); Root(Field("hidden").GetValue(writer)!, hidden);
            Root(Field("pathChanges").GetValue(writer)!, changes); Root(Field("pathComponents").GetValue(writer)!, names);
            Root(Field("boundedDurableNodes").GetValue(writer)!, durable); Field("extentCount").SetValue(writer, (long)ranges);
            pages.Flush(); typeof(JournalDeltaStore).GetMethod("RollSegment", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(writer, null);
        }
        using (var reopened = new JournalDeltaStore(delta, identity, basis, sourceFilePins: pins))
        {
            Assert.Equal(files + 1L, reopened.Observe().IndexedNodes); Assert.Null(reopened.Stat(HiddenName(files - 1)));
            Span<byte> value = stackalloc byte[1]; Assert.Equal(1, reopened.Read(last, lastOffset, value)); Assert.Equal((byte)'x', value[0]);
            var next = reopened.Create("after-boundary", false, 32); reopened.Write(next.Id, 0, "z"u8); reopened.Delete(next.Id); reopened.ReleaseUnlinkedFile(next.Id);
            reopened.SetLength(last, lastOffset); reopened.Write(last, lastOffset, "y"u8); Assert.Equal(1, reopened.Read(last, lastOffset, value)); Assert.Equal((byte)'y', value[0]);
        }
        using var final = new JournalDeltaStore(delta, identity, basis, sourceFilePins: pins); byte[] lastByte = new byte[1];
        Assert.Equal(1, final.Read(last, lastOffset, lastByte)); Assert.Equal((byte)'y', lastByte[0]); Assert.Null(final.Stat("after-boundary"));
        Assert.InRange(PagedMetadataStore.CachedMetadataBytes, 0, StorageResources.MetadataCacheBytes);
    }
    private static FieldInfo Field(string name) => typeof(JournalDeltaStore).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!;
    private static void Root(object value, MetadataTreeRoot root)
    {
        if (value.GetType().GetField("Root") is { } field) field.SetValue(value, root);
        else value.GetType().GetProperty("Root")!.SetValue(value, root);
    }
    private static Guid FileId(int value) => Guid.ParseExact((value + 1L).ToString("x32"), "N");
    private static string FileName(int value) => "file-" + value.ToString("d8");
    private static string HiddenName(int value) => "deleted-" + value.ToString("d8");
    private static byte[] NameId(string name) { byte[] bytes = Encoding.UTF8.GetBytes(name), input = new byte[32 + bytes.Length]; bytes.CopyTo(input, 32); return SHA256.HashData(input); }
    private static byte[] Node(DeltaEntry entry, byte[] name)
    {
        using var stream = new MemoryStream(); using var output = new BinaryWriter(stream); output.Write((byte)2); output.Write(entry.Id.ToByteArray());
        output.Write(name.Length == 0 ? new byte[32] : name); output.Write(entry.Directory); output.Write(entry.Length); output.Write(entry.Attributes);
        output.Write(entry.CreationTime); output.Write(entry.LastAccessTime); output.Write(entry.LastWriteTime); output.Write(entry.BasePath is not null);
        if (entry.BasePath is not null) output.Write(entry.BasePath.Length == 0 ? new byte[32] : NameId(entry.BasePath));
        output.Write(entry.BaseLength); output.Write(entry.Deleted); output.Write(entry.UnixMode.HasValue); if (entry.UnixMode.HasValue) output.Write(entry.UnixMode.Value); return stream.ToArray();
    }
    private static byte[] Namespace(string name, Guid id, bool directory, long count)
    { byte[] text = Encoding.UTF8.GetBytes(name), bytes = new byte[26 + text.Length]; bytes[0] = 1; bytes[1] = directory ? (byte)1 : (byte)0; id.TryWriteBytes(bytes.AsSpan(2)); BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(18), count); text.CopyTo(bytes, 26); return bytes; }
    private static byte[] Extent(long offset, long physical, long segment)
    { byte[] bytes = new byte[45]; bytes[0] = 3; BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(1), offset); BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(9), 1); BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(13), physical); BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(21), 1); BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(25), 1); BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(37), segment); return bytes; }
}
