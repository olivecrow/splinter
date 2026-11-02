using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using Splinter.Core;

namespace Splinter.Runtime;

public sealed record ImmutableFilePrivateRange(long Offset, int Length, string Hash, bool RawRange, int ChunkOffset = 0, int ChunkLength = 0)
{
    public int ContentLength => ChunkLength == 0 ? Length : ChunkLength;
}
public sealed record ImmutableFileContentDescriptor(long Length, string? BasisHash, long BasisObjectLength, long BasisVisibleLength, MetadataTreeRoot Ranges,
    bool ExternalBasis = false, bool BasisRawRange = false);
public sealed record ImmutableContentReference(string Hash, long Length, bool RawRange);

// A full-file identity is verified over a composed stream. Only fixed basis
// content, zero holes and private chunks are retained; no full new version copy.
public sealed partial class ResultFileContentStore
{
    private readonly string home, control, directory;
    private readonly WorkspaceRecord workspace;
    private readonly IJournalFilePins pins;
    private readonly OwnedContentStore whole;
    private readonly OwnedRangeContentStore raw;
    private readonly MetadataTreeRoot? fixedRoot;
    private readonly Func<string, long?, Stream>? externalBasis;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    { RespectRequiredConstructorParameters = true, RespectNullableAnnotations = true, UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow };
    private sealed record Catalog(int Version, string OwnerId, string RequestDigest, MetadataTreeRoot Files);
    public ResultFileContentStore(string home, string control, WorkspaceRecord workspace, IJournalFilePins pins,
        OwnedRangeContentStore raw, MetadataTreeRoot? fixedRoot = null, Func<string, long?, Stream>? externalBasis = null)
    {
        this.home = home; this.control = control; this.workspace = workspace; this.pins = pins; this.raw = raw; this.fixedRoot = fixedRoot; this.externalBasis = externalBasis;
        directory = Path.Combine(control, "files", "result-ranges"); whole = new(home, pins);
    }
    public string MetadataDirectory => directory;
    public MetadataTreeRoot? CurrentRoot
    {
        get
        {
            if (!File.Exists(Path.Combine(directory, "catalog.json"))) return null;
            using var pages = new PagedMetadataStore(directory); return Load(pages).Files;
        }
    }
    public MetadataTreeRoot Preserve(string hash, long length, string? basisHash, long basisObjectLength, long basisVisibleLength,
        IEnumerable<ImmutableFilePrivateRange> ranges, CancellationToken ct = default, bool useExternalBasis = false, bool useRawBasis = false)
    {
        using var batch = BeginPreservation();
        var result = batch.Preserve(hash, length, basisHash, basisObjectLength, basisVisibleLength, ranges, ct, useExternalBasis, useRawBasis);
        batch.Complete(); return result;
    }
    public PreservationBatch BeginPreservation() => new(this);
    // A failed group leaves its last published prefix intact. Dispose never
    // publishes pending roots. Call Complete before acknowledging the capture.
    public sealed class PreservationBatch : IDisposable
    {
        private readonly ResultFileContentStore owner;
        private readonly PagedMetadataStore pages;
        private Catalog catalog;
        private int pending;
        private bool closed;
        internal PreservationBatch(ResultFileContentStore owner)
        {
            this.owner = owner; pages = new(owner.directory, true, filePins: owner.pins);
            try { catalog = owner.Load(pages); } catch { pages.Dispose(); throw; }
        }
        public MetadataTreeRoot Preserve(string hash, long length, string? basisHash, long basisObjectLength, long basisVisibleLength,
            IEnumerable<ImmutableFilePrivateRange> ranges, CancellationToken ct = default, bool useExternalBasis = false, bool useRawBasis = false)
        {
            ObjectDisposedException.ThrowIf(closed, this);
            var root = owner.PreserveCore(pages, catalog, hash, length, basisHash, basisObjectLength, basisVisibleLength, ranges, ct, useExternalBasis, useRawBasis);
            if (root != catalog.Files) { catalog = catalog with { Files = root }; if (++pending == 64) Flush(); }
            return root;
        }
        private void Flush() { if (pending == 0) return; pages.Flush(); owner.Publish(catalog); pending = 0; }
        public void Complete() { ObjectDisposedException.ThrowIf(closed, this); Flush(); }
        public void Dispose() { if (closed) return; closed = true; pages.Dispose(); }
    }
    private MetadataTreeRoot PreserveCore(PagedMetadataStore pages, Catalog catalog, string hash, long length, string? basisHash,
        long basisObjectLength, long basisVisibleLength, IEnumerable<ImmutableFilePrivateRange> ranges, CancellationToken ct, bool useExternalBasis, bool useRawBasis)
    {
        ValidateHash(hash); if (basisHash is not null) ValidateHash(basisHash);
        if (length < 0 || basisObjectLength < 0 || basisVisibleLength < 0 || basisVisibleLength > basisObjectLength || basisVisibleLength > length ||
            basisHash is null && (basisObjectLength != 0 || basisVisibleLength != 0 || useRawBasis) ||
            useRawBasis && (useExternalBasis || basisObjectLength is < 1 or > int.MaxValue)) throw Invalid("Invalid immutable basis visibility.");
        var prior = pages.Open(catalog.Files).FindEntry(hash);
        if (prior is not null)
        {
            var retained = Decode(prior, pages.Empty); if (retained.Length != length) throw Invalid("A full-file identity has another declared length.");
            using var previous = OpenDescriptor(retained, pages, ct); VerifyFile(previous, hash, length, ct); return catalog.Files;
        }
        IEnumerable<MetadataEntry> Entries()
        {
            long end = 0;
            foreach (var range in ranges)
            {
                ct.ThrowIfCancellationRequested(); ValidateHash(range.Hash);
                if (range.Offset < end || range.Length < 1 || range.Offset < 0 || range.Offset > length - range.Length)
                    throw Invalid("Private immutable ranges overlap or exceed the file.");
                end = checked(range.Offset + range.Length); yield return EncodeRange(range);
            }
        }
        var rangeRoot = pages.Build(Entries(), ct);
        var descriptor = new ImmutableFileContentDescriptor(length, basisHash, basisObjectLength, basisVisibleLength, rangeRoot, useExternalBasis, useRawBasis);
        using (var composed = OpenDescriptor(descriptor, pages, ct)) VerifyFile(composed, hash, length, ct);
        return pages.Set(catalog.Files, hash, Encode(descriptor), rangeRoot);
    }
    public bool Contains(string hash, long length)
    {
        ValidateHash(hash); if (fixedRoot is null && !File.Exists(Path.Combine(directory, "catalog.json"))) return false;
        using var pages = new PagedMetadataStore(directory); var root = fixedRoot ?? Load(pages).Files;
        var entry = pages.Open(root).FindEntry(hash); if (entry is null) return false;
        if (Decode(entry, pages.Empty).Length != length) throw Invalid("A composed file has another length."); return true;
    }
    public bool UsesRawBasis(string hash, long length)
    {
        ValidateHash(hash); if (fixedRoot is null && !File.Exists(Path.Combine(directory, "catalog.json"))) return false;
        using var pages = new PagedMetadataStore(directory); var root = fixedRoot ?? Load(pages).Files;
        var entry = pages.Open(root).FindEntry(hash); if (entry is null) return false;
        var descriptor = Decode(entry, pages.Empty);
        if (descriptor.Length != length) throw Invalid("An immutable raw basis has another full-file length.");
        return descriptor.BasisRawRange && descriptor.BasisHash == hash && descriptor.BasisObjectLength == length &&
            descriptor.BasisVisibleLength == length && descriptor.Ranges.Count == 0;
    }
    public Stream Open(string hash, long? length = null, CancellationToken ct = default)
    {
        ValidateHash(hash);
        if (fixedRoot is not null || File.Exists(Path.Combine(directory, "catalog.json")))
        {
            var pages = new PagedMetadataStore(directory);
            try
            {
                var entry = pages.Open(fixedRoot ?? Load(pages).Files).FindEntry(hash);
                if (entry is not null)
                {
                    var descriptor = Decode(entry, pages.Empty);
                    if (length is not null && descriptor.Length != length) throw Invalid("The composed file length changed.");
                    return new Composite(this, descriptor, pages, ownsPages: true, ct);
                }
            }
            catch { pages.Dispose(); throw; }
            pages.Dispose();
        }
        if (externalBasis is not null && !File.Exists(Path.Combine(control, "files", "objects", hash))) return externalBasis(hash, length);
        long size = length ?? whole.LengthOf(workspace, control, hash, ct); return whole.Open(workspace, control, hash, size, ct);
    }
    public IEnumerable<ImmutableContentReference> EnumerateReferences(MetadataTreeRoot root)
    {
        using var pages = new PagedMetadataStore(directory);
        foreach (var file in pages.Open(root).Enumerate())
        {
            var descriptor = Decode(file, pages.Empty);
            if (descriptor.BasisHash is not null && !descriptor.ExternalBasis) yield return new(descriptor.BasisHash, descriptor.BasisObjectLength, descriptor.BasisRawRange);
            foreach (var entry in pages.Open(descriptor.Ranges).Enumerate())
            { var range = DecodeRange(entry); yield return new(range.Hash, range.ContentLength, range.RawRange); }
        }
    }
    public IEnumerable<ImmutableContentReference> ReferencesForFiles(MetadataTreeRoot root, IEnumerable<ImmutableContentReference> files)
    {
        using var pages = new PagedMetadataStore(directory); var contents = pages.Open(root);
        foreach (var file in files)
        {
            var entry = contents.FindEntry(file.Hash);
            if (entry is null) { if (externalBasis is null) yield return file; continue; }
            var descriptor = Decode(entry, pages.Empty);
            if (descriptor.Length != file.Length) throw Invalid("A retained file identity has another length.");
            if (descriptor.BasisHash is not null && !descriptor.ExternalBasis) yield return new(descriptor.BasisHash, descriptor.BasisObjectLength, descriptor.BasisRawRange);
            foreach (var extent in pages.Open(descriptor.Ranges).Enumerate())
            { var range = DecodeRange(extent); yield return new(range.Hash, range.ContentLength, range.RawRange); }
        }
    }
    private Stream OpenDescriptor(ImmutableFileContentDescriptor descriptor, PagedMetadataStore pages, CancellationToken ct) => new Composite(this, descriptor, pages, false, ct);
    private Stream OpenChunk(ImmutableFilePrivateRange range, CancellationToken ct, ImmutableReadProofs? proofs = null) =>
        OpenReference(new(range.Hash, range.ContentLength, range.RawRange), proofs, ct);
    private Stream OpenReference(ImmutableContentReference reference, ImmutableReadProofs? proofs, CancellationToken ct) => reference.RawRange
        ? proofs is null ? raw.Open(workspace, control, reference.Hash, checked((int)reference.Length), ct)
            : raw.OpenVerified(workspace, control, reference.Hash, checked((int)reference.Length), proofs, ct)
        : proofs is null ? whole.Open(workspace, control, reference.Hash, reference.Length, ct)
            : whole.OpenVerified(workspace, control, reference.Hash, reference.Length, proofs, ct);
    private static void VerifyFile(Stream input, string hash, long length, CancellationToken ct)
    {
        byte[] bytes = new byte[65536]; long read = 0; using var digest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        for (int count; (count = input.Read(bytes)) > 0;) { ct.ThrowIfCancellationRequested(); digest.AppendData(bytes.AsSpan(0, count)); read = checked(read + count); }
        if (read != length || Convert.ToHexStringLower(digest.GetHashAndReset()) != hash) throw Invalid("The composed file differs from its frozen full-file identity.");
    }
    private Catalog Load(PagedMetadataStore pages, ImmutableReadProofs? proofs = null)
    {
        string path = Path.Combine(directory, "catalog.json"); ImmutableReadProofs.EnsurePlainPath(path, proofs);
        if (!File.Exists(path))
        {
            if (Directory.EnumerateFiles(directory, "pages-*.bin").Any(file => new FileInfo(file).Length != 48))
                throw Invalid("Preserve composed-content metadata without its catalog owner root.");
            var initial = new Catalog(1, workspace.Id, workspace.RequestDigest!, pages.Empty); Publish(initial); return initial;
        }
        Catalog value;
        try
        {
            using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read); using var document = JsonDocument.Parse(input);
            if (!JsonPropertyValidation.HasUniqueProperties(document.RootElement)) throw Invalid("The composed-content catalog repeats a control field.");
            value = document.RootElement.Deserialize<Catalog>(Json) ?? throw Invalid("The composed-content catalog is empty.");
        }
        catch (JsonException) { throw Invalid("Preserve a malformed composed-content catalog."); }
        if (value.Version != 1 || value.OwnerId != workspace.Id || value.RequestDigest != workspace.RequestDigest) throw Invalid("The composed-content catalog belongs to another task.");
        _ = pages.Open(value.Files); return value;
    }
    private void Publish(Catalog catalog)
    {
        string path = Path.Combine(directory, "catalog.json"), temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        WorkspaceStore.EnsurePlainPath(temporary);
        using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { JsonSerializer.Serialize(output, catalog, Json); output.Flush(true); }
        AtomicFilePublication.Publish(temporary, path);
    }
    private static byte[] Encode(ImmutableFileContentDescriptor descriptor)
    {
        byte[] bytes = new byte[26 + (descriptor.BasisHash is null ? 0 : 32)]; bytes[0] = 3;
        bytes[1] = (byte)((descriptor.BasisHash is null ? 0 : 1) | (descriptor.ExternalBasis ? 2 : 0) | (descriptor.BasisRawRange ? 4 : 0));
        BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(2), descriptor.Length); BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(10), descriptor.BasisObjectLength);
        BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(18), descriptor.BasisVisibleLength); if (descriptor.BasisHash is not null) Convert.FromHexString(descriptor.BasisHash).CopyTo(bytes, 26); return bytes;
    }
    private static ImmutableFileContentDescriptor Decode(MetadataEntry entry, MetadataTreeRoot empty)
    {
        var bytes = entry.Value.Span;
        if (bytes.Length < 26 || bytes[0] != 3 || bytes[1] > 7 || (bytes[1] & 6) == 6 || bytes.Length != 26 + (bytes[1] & 1) * 32) throw Invalid("Malformed composed-file metadata.");
        long length = BinaryPrimitives.ReadInt64LittleEndian(bytes[2..]), basis = BinaryPrimitives.ReadInt64LittleEndian(bytes[10..]), visible = BinaryPrimitives.ReadInt64LittleEndian(bytes[18..]);
        string? hash = (bytes[1] & 1) == 0 ? null : Convert.ToHexStringLower(bytes.Slice(26, 32));
        bool raw = (bytes[1] & 4) != 0;
        if (length < 0 || basis < 0 || visible < 0 || visible > basis || visible > length || hash is null && (basis != 0 || visible != 0 || raw) ||
            raw && (basis is < 1 or > int.MaxValue)) throw Invalid("Invalid composed-file visibility.");
        return new(length, hash, basis, visible, entry.Child ?? empty, (bytes[1] & 2) != 0, raw);
    }
    private static MetadataEntry EncodeRange(ImmutableFilePrivateRange range)
    {
        if (range.ChunkOffset < 0 || range.ContentLength < range.Length || range.ChunkOffset > range.ContentLength - range.Length)
            throw Invalid("Invalid private chunk slice.");
        byte[] bytes = new byte[54]; bytes[0] = 2; bytes[1] = range.RawRange ? (byte)1 : (byte)0;
        BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(2), range.Offset); BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(10), range.Length); Convert.FromHexString(range.Hash).CopyTo(bytes, 14);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(46), range.ChunkOffset); BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(50), range.ContentLength);
        return new(range.Offset.ToString("x16", System.Globalization.CultureInfo.InvariantCulture), bytes);
    }
    private static ImmutableFilePrivateRange DecodeRange(MetadataEntry entry)
    {
        var bytes = entry.Value.Span; if (bytes.Length != 54 || bytes[0] != 2 || bytes[1] > 1 || entry.Child is not null) throw Invalid("Malformed private-content range.");
        long offset = BinaryPrimitives.ReadInt64LittleEndian(bytes[2..]); int length = BinaryPrimitives.ReadInt32LittleEndian(bytes[10..]);
        if (offset < 0 || length < 1 || offset > long.MaxValue - length || entry.Key != offset.ToString("x16", System.Globalization.CultureInfo.InvariantCulture)) throw Invalid("Invalid private-content range bounds.");
        int chunkOffset = BinaryPrimitives.ReadInt32LittleEndian(bytes[46..]), chunkLength = BinaryPrimitives.ReadInt32LittleEndian(bytes[50..]);
        if (chunkOffset < 0 || chunkLength < length || chunkOffset > chunkLength - length) throw Invalid("Invalid private-content slice.");
        return new(offset, length, Convert.ToHexStringLower(bytes.Slice(14, 32)), bytes[1] == 1, chunkOffset, chunkLength);
    }
    private sealed class Composite(ResultFileContentStore owner, ImmutableFileContentDescriptor descriptor, PagedMetadataStore pages, bool ownsPages, CancellationToken ct,
        ImmutableReadProofs? proofs = null) : Stream
    {
        private long position;
        private Stream? basis;
        private Stream? privateChunk;
        private ImmutableFilePrivateRange? activeRange;
        public override int Read(Span<byte> output)
        {
            ct.ThrowIfCancellationRequested(); int count = (int)Math.Min(output.Length, descriptor.Length - position); if (count <= 0) return 0; output = output[..count]; output.Clear();
            long end = position + count;
            void ReadBasis(long from, long to, Span<byte> target)
            {
                if (descriptor.BasisHash is null || from >= descriptor.BasisVisibleLength || to <= from) return;
                basis ??= descriptor.ExternalBasis ? (owner.externalBasis ?? throw Invalid("The protected external basis reader is missing."))(descriptor.BasisHash, descriptor.BasisObjectLength)
                    : owner.OpenReference(new(descriptor.BasisHash, descriptor.BasisObjectLength, descriptor.BasisRawRange), proofs, ct);
                basis.Position = from; int take = (int)(Math.Min(to, descriptor.BasisVisibleLength) - from);
                ReadExactly(basis, target.Slice((int)(from - position), take));
            }
            var ranges = pages.Open(descriptor.Ranges); string key = position.ToString("x16", System.Globalization.CultureInfo.InvariantCulture); var floor = ranges.Floor(key);
            IEnumerable<MetadataEntry> Entries() { if (floor is not null) yield return floor; foreach (var entry in ranges.Enumerate(floor?.Key ?? key)) yield return entry; }
            long previous = -1, cursor = position;
            foreach (var entry in Entries())
            {
                var range = DecodeRange(entry); if (range.Offset >= end) break;
                if (previous > range.Offset || range.Offset > descriptor.Length - range.Length) throw Invalid("Composed ranges overlap or exceed the file."); previous = range.Offset + range.Length;
                long from = Math.Max(position, range.Offset), to = Math.Min(end, range.Offset + range.Length); if (to <= from) continue;
                ReadBasis(cursor, from, output);
                if (activeRange != range)
                { privateChunk?.Dispose(); privateChunk = owner.OpenChunk(range, ct, proofs); activeRange = range; }
                privateChunk!.Position = from - range.Offset + range.ChunkOffset;
                ReadExactly(privateChunk, output.Slice((int)(from - position), (int)(to - from)));
                cursor = to;
            }
            ReadBasis(cursor, end, output);
            position = end; return count;
        }
        private static void ReadExactly(Stream source, Span<byte> target) { while (!target.IsEmpty) { int count = source.Read(target); if (count <= 0) throw Invalid("A retained content range ended early."); target = target[count..]; } }
        public override int Read(byte[] bytes, int offset, int count) => Read(bytes.AsSpan(offset, count));
        public override long Length => descriptor.Length; public override long Position { get => position; set { if (value < 0 || value > Length) throw new ArgumentOutOfRangeException(nameof(value)); position = value; } }
        public override bool CanRead => true; public override bool CanSeek => true; public override bool CanWrite => false;
        public override long Seek(long value, SeekOrigin origin) { Position = origin switch { SeekOrigin.Begin => value, SeekOrigin.Current => checked(position + value), SeekOrigin.End => checked(Length + value), _ => throw new ArgumentOutOfRangeException(nameof(origin)) }; return position; }
        protected override void Dispose(bool disposing) { if (disposing) { basis?.Dispose(); privateChunk?.Dispose(); if (ownsPages) pages.Dispose(); } base.Dispose(disposing); }
        public override void Flush() { } public override void SetLength(long value) => throw new NotSupportedException(); public override void Write(byte[] bytes, int offset, int count) => throw new NotSupportedException();
    }
    private static void ValidateHash(string hash) { if (hash.Length != 64 || hash.Any(c => c is not (>= '0' and <= '9' or >= 'a' and <= 'f'))) throw Invalid("Invalid immutable full-file identity."); }
    private static WorkspaceException Invalid(string message) => new("ResultContentInvalid", message);
}
