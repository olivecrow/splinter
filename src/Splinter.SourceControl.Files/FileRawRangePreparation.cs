using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Splinter.Core;
using Splinter.Runtime;

namespace Splinter.SourceControl.Files;

public sealed partial class FileWorkspaceProvider
{
    private sealed record RawPreparationRow(string Path, long Offset, int Length, string Hash, CapturedImmutableRangeBacking Backing);
    internal sealed class PreparedRawRanges : IDisposable
    {
        private readonly TemporaryMetadataDirectory scratch = new();
        private readonly PagedMetadataStore pages;
        private MetadataTreeRoot root;
        public PreparedRawRanges() { pages = new(scratch.Path, true); root = pages.Empty; }
        public string Directory => scratch.Path;
        public void Build(IEnumerable<MetadataEntry> entries, CancellationToken ct) => root = pages.Build(MetadataSort.Order(entries, scratch.Path, ct), ct);
        private static string Key(string path, long offset) => PagedPathInterner.Reference(path) + ":" + offset.ToString("x16");
        public static MetadataEntry Encode(string path, long offset, int length, string hash, bool raw)
        {
            byte[] bytes = new byte[37]; BinaryPrimitives.WriteInt32LittleEndian(bytes, length); bytes[4] = raw ? (byte)1 : (byte)0;
            Convert.FromHexString(hash).CopyTo(bytes, 5); return new(Key(path, offset), bytes);
        }
        public (string Hash, bool Raw) Find(string path, long offset, int length)
        {
            var bytes = pages.Open(root).Find(Key(path, offset));
            if (bytes is null || bytes.Length != 37 || bytes[4] > 1 || BinaryPrimitives.ReadInt32LittleEndian(bytes) != length)
                throw Error("A prepared raw range differs from its fixed file interval.");
            return (Convert.ToHexStringLower(bytes.AsSpan(5)), bytes[4] == 1);
        }
        public void Dispose() { pages.Dispose(); scratch.Dispose(); }
    }

    internal static PreparedRawRanges PrepareRawRanges(WorkspaceRecord w, string control, SourceCaptureIdentity capture,
        ISourceCaptureRanges ranges, ISnapshotTree tree, FileSourceManifest result, OwnedRangeContentStore raw,
        OwnedContentStore whole, CancellationToken ct)
    {
        var prepared = new PreparedRawRanges();
        try
        {
            using var wholeReferences = whole.BeginExistingReferenceScope(w, control);
            IEnumerable<MetadataEntry> Requests()
            {
                foreach (var entry in result.EnumerateEntries())
                {
                    ct.ThrowIfCancellationRequested(); if (entry.Kind != "file") continue;
                    var layout = ranges.GetFileRanges(entry.Path) ?? throw Error("A frozen file has no range layout.");
                    foreach (var range in layout.PrivateRanges)
                    {
                        if (range.PreservedHash is not null || range.Backing?.ExpectedBackingHash is null) continue;
                        var backing = range.Backing;
                        if (backing.CaptureId != capture.Id || backing.CaptureDigest != capture.Digest ||
                            range.Offset < 0 || range.Length < 1 || range.Offset > entry.Length - range.Length)
                            throw Error("A raw range differs from the fixed capture.");
                        string group = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(backing.FileIdentity + "\n" + backing.ExpectedBackingHash)));
                        for (int at = 0; at < range.Length;)
                        {
                            ct.ThrowIfCancellationRequested(); int length = Math.Min(1048576, range.Length - at); string hash;
                            if (at == 0 && range.Offset == 0 && length == entry.Length) hash = entry.Hash!;
                            else
                            {
                                byte[] bytes = new byte[length]; int done = 0;
                                while (done < length)
                                {
                                    ct.ThrowIfCancellationRequested(); int read = tree.Read(entry.Path, range.Offset + at + done, bytes.AsSpan(done));
                                    if (read <= 0 || read > length - done) throw Error("A fixed private range ended early."); done += read;
                                }
                                hash = Convert.ToHexStringLower(SHA256.HashData(bytes));
                            }
                            var row = new RawPreparationRow(entry.Path, checked(range.Offset + at), length, hash,
                                backing with { Offset = checked(backing.Offset + at) });
                            yield return new(group + ":" + row.Backing.Offset.ToString("x16") + ":" + PagedPathInterner.Reference(entry.Path) + ":" + row.Offset.ToString("x16"),
                                JsonSerializer.SerializeToUtf8Bytes(row, Json));
                            at += length;
                        }
                    }
                }
            }
            IEnumerable<MetadataEntry> Preserve()
            {
                // The fixed capture remains leased throughout both passes.
                // Group by physical backing so its full hash/fence is admitted
                // once, regardless of file order or the number of segments.
                var pending = new List<RawPreparationRow>(OwnedRangeContentStore.MaximumPreservationBatch);
                IEnumerable<MetadataEntry> Publish()
                {
                    var existing = wholeReferences.TryReferenceBatch(
                        pending.Select(row => new OwnedContentStore.ExistingReference(row.Hash, row.Length)).ToArray(), ct);
                    var missing = pending.Where((_, index) => !existing[index]).ToArray();
                    if (missing.Length > 0) raw.PreserveBatch(w, control,
                        missing.Select(row => new OwnedRangeContentStore.Preservation(row.Hash, row.Length, row.Backing)).ToArray(), ct);
                    for (int index = 0; index < pending.Count; index++)
                    {
                        var row = pending[index];
                        yield return PreparedRawRanges.Encode(row.Path, row.Offset, row.Length, row.Hash, !existing[index]);
                    }
                    pending.Clear();
                }
                foreach (var item in MetadataSort.Order(Requests(), prepared.Directory, ct))
                {
                    ct.ThrowIfCancellationRequested();
                    var row = JsonSerializer.Deserialize<RawPreparationRow>(item.Value.Span, Json) ?? throw Error("Invalid raw preparation row.");
                    pending.Add(row);
                    if (pending.Count == OwnedRangeContentStore.MaximumPreservationBatch)
                        foreach (var entry in Publish()) yield return entry;
                }
                if (pending.Count > 0) foreach (var entry in Publish()) yield return entry;
            }
            prepared.Build(Preserve(), ct); return prepared;
        }
        catch { prepared.Dispose(); throw; }
    }
}
