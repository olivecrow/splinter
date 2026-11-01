using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Splinter.Core;

namespace Splinter.Runtime;

// The immutable inventory stays outside the source directory. No source index
// or payload disappears before that inventory and the exact owner are durable.
public static class SegmentedJournalReclamation
{
    private const string Guard = "delta.journal.bounded.lock";
    private sealed record Header(int Version, string Directory, OverlayIdentity Identity, SnapshotDeltaSeal Seal);
    private sealed record Proof(string Name, long Length, byte[] Hash, string? Identity);
    private static WorkspaceException Invalid(string message = "The segmented reclamation inventory changed.") => new("ViewReceiptMismatch", message);
    public static bool HasIntent(string receipt) => File.Exists(receipt) || File.Exists(receipt + ".pending");
    private static bool SegmentName(string name) => name.Length == 28 && name.StartsWith("segment-", StringComparison.Ordinal) &&
        name.EndsWith(".sjr", StringComparison.Ordinal) && long.TryParse(name.AsSpan(8, 16), System.Globalization.NumberStyles.HexNumber,
            System.Globalization.CultureInfo.InvariantCulture, out long id) && id >= 0 ||
        name.Length == 27 && name.StartsWith("packed-", StringComparison.Ordinal) && name.EndsWith(".sjr", StringComparison.Ordinal) &&
        long.TryParse(name.AsSpan(7, 16), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out long data) && data >= 0 && data <= long.MaxValue - 1;
    private static long SegmentId(string name) => name.StartsWith("packed-", StringComparison.Ordinal)
        ? checked(-2 - long.Parse(name.AsSpan(7, 16), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture))
        : long.Parse(name.AsSpan(8, 16), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture);
    private static bool Name(string name) => name is "identity.json" or "delta.journal" or Guard or "segment-state.json" || SegmentName(name);
    private static bool Index(string name) => name.StartsWith("index-", StringComparison.Ordinal) && Guid.TryParseExact(name.AsSpan(6), "N", out _);

    public static void ReclaimDirectory(string directory, SnapshotDeltaSeal seal, string[] metadataNames, string receipt,
        Action<string>? boundary = null, Func<string, IJournalDirectoryGuard>? directoryGuardFactory = null, IJournalFilePins? filePins = null)
    {
        directory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory)); receipt = Path.GetFullPath(receipt);
        if (metadataNames.Length != 1 || metadataNames[0] != "identity.json" || WorkspaceStore.SamePath(Path.GetDirectoryName(receipt)!, directory) ||
            receipt.StartsWith(directory + Path.DirectorySeparatorChar, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)) throw Invalid();
        WorkspaceStore.EnsurePlainPath(directory); WorkspaceStore.EnsurePlainPath(receipt); WorkspaceStore.EnsurePlainPath(receipt + ".pending");
        bool published = File.Exists(receipt), pending = File.Exists(receipt + ".pending"); if (published && pending) throw Invalid();
        // A pending inventory cannot have authorized deletion. Reconstruct it
        // from the still-complete sealed source and match its surviving prefix
        // before continuing, including an interrupted header/checksum write.
        Header? header = published ? ReadHeader(receipt) : null;
        if (header is not null && (!WorkspaceStore.SamePath(header.Directory, directory) || header.Seal != seal || header.Identity.Version != 3)) throw Invalid();
        if (!Directory.Exists(directory))
        { if (header is null || pending) throw Invalid("A missing journal has no completed reclamation inventory."); foreach (var _ in Read(receipt, header)) { } return; }
        if (File.Exists(directory)) throw Invalid();
        string guardPath = Path.Combine(directory, Guard);
        using var fence = File.Exists(guardPath) ? new FileStream(guardPath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read | FileShare.Delete) : null;
        if (fence is null && header is null || fence is not null && fence.Length != 0) throw Invalid("The stopped journal writer guard differs.");
        IJournalDirectoryGuard? ancestry = fence is null ? null : directoryGuardFactory?.Invoke(directory);
        try
        {
            if (ancestry is not null && !WorkspaceStore.SamePath(ancestry.DirectoryPath, directory)) throw Invalid();
            if (header is null)
            {
                using var input = File.OpenRead(Path.Combine(directory, "identity.json"));
                var identity = JsonSerializer.Deserialize<OverlayIdentity>(input) ?? throw Invalid();
                if (identity.Version != 3 || !JournalDeltaStore.IsSegmentedJournal(Path.Combine(directory, "delta.journal")) ||
                    JournalDeltaStore.SealSegmentedJournal(Path.Combine(directory, "delta.journal"), seal.DurableSequence) != seal)
                    throw new WorkspaceException("ViewSourceChanged", "The stopped journal differs from its sealed result; preserve its storage.");
                if (JournalDeltaStore.HasPendingSourcePacking(Path.Combine(directory, "delta.journal")))
                    throw new WorkspaceException("SourceMaintenancePending", "Resume the same owned packing request before reclaiming its source journal and external plan.");
                header = new(2, directory, identity, seal);
                if (pending)
                {
                    using (var compare = new JournalDeltaStore.BoundedCandidatePrefixVerifier(receipt + ".pending"))
                    { Write(compare, header, fence!, filePins); compare.Complete(); }
                    File.Delete(receipt + ".pending");
                }
                using (var output = new FileStream(receipt + ".pending", FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read))
                { Write(output, header, fence!, filePins); output.Flush(true); }
                boundary?.Invoke("intent-flushed"); pending = true;
            }
            string inventory = published ? receipt : receipt + ".pending";
            ValidateNamespace(directory, header, inventory, allowIndices: true);
            ValidateFiles(directory, header, inventory, fence, filePins);
            if (pending) { File.Move(inventory, receipt); inventory = receipt; }
            using var receiptFence = new FileStream(receipt, FileMode.Open, FileAccess.Read, FileShare.Read);
            foreach (var _ in Read(receipt, header)) { }
            boundary?.Invoke("intent");
            // Source capture protection is inspected by index reclamation before
            // the first page deletion. Pending captures and readers block here.
            JournalDeltaStore.ReclaimStoppedPagedIndices(directory, header.Identity, filePins, boundary,
                new(header.Seal, receipt, () =>
                {
                    ValidateNamespace(directory, header, receipt, allowIndices: true);
                    ValidateFiles(directory, header, receipt, fence, filePins);
                }));
            ValidateNamespace(directory, header, receipt, allowIndices: false);
            foreach (var proof in Read(receipt, header))
            {
                string path = Path.Combine(directory, proof.Name); if (!File.Exists(path)) continue;
                Verify(path, proof, proof.Name == Guard ? fence : null, filePins);
                if (proof.Name == Guard) { ancestry?.Dispose(); ancestry = null; }
                if (proof.Identity is not null) filePins!.Unpin(path, proof.Identity); else File.Delete(path);
                boundary?.Invoke(proof.Name == "delta.journal" ? "journal-removed" : proof.Name == Guard ? "guard-removed" :
                    SegmentName(proof.Name) ? "segment-removed" : "metadata-removed");
            }
            fence?.Dispose();
            if (Directory.EnumerateFileSystemEntries(directory).Any()) throw Invalid("An unowned journal entry remains.");
            Directory.Delete(directory); boundary?.Invoke("directory-removed");
        }
        finally { ancestry?.Dispose(); }
    }
    private static IEnumerable<MetadataEntry> Names(string directory)
    {
        foreach (string path in Directory.EnumerateFileSystemEntries(directory))
        {
            WorkspaceStore.EnsurePlainPath(path); string name = Path.GetFileName(path);
            if (Directory.Exists(path)) { if (!Index(name)) throw Invalid(); continue; }
            if (name.EndsWith(".reclaim", StringComparison.Ordinal) && Index(name[..^8]) ||
                name.EndsWith(".reclaim.pending", StringComparison.Ordinal) && Index(name[..^16])) continue;
            if (!File.Exists(path) || !Name(name)) throw Invalid("An unknown or unfinished segment generation remains.");
            if (name != Guard) yield return new(name, ReadOnlyMemory<byte>.Empty);
        }
    }
    private static void Write(Stream output, Header header, FileStream guard, IJournalFilePins? pins)
    {
        using var hashed = new InventoryHashWriter(output);
        using var writer = new BinaryWriter(hashed, Encoding.UTF8, true); writer.Write("SJRC2"u8);
        byte[] control = JsonSerializer.SerializeToUtf8Bytes(header); if (control.Length > JournalControlIntegrity.MaximumBytes) throw Invalid(); writer.Write(control.Length); writer.Write(control);
        using var stateInput = File.OpenRead(Path.Combine(header.Directory, "segment-state.json"));
        var state = JsonSerializer.Deserialize<JournalSegmentState>(stateInput, new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? throw Invalid();
        using var index = new PagedMetadataStore(state.Roots.Directory); long seen = 0;
        void Record(string name)
        {
            string path = Path.Combine(header.Directory, name);
            var proof = name == Guard ? new Proof(name, 0, SHA256.HashData(Array.Empty<byte>()), pins?.IdentifyOpened(guard)) : Observe(path, name, pins);
            if (SegmentName(name))
            {
                var entry = index.Open(state.Roots.Segments).Find(SegmentId(name).ToString("x16")) ?? throw Invalid();
                var segment = JournalDeltaStore.ReadCapturedSegment(entry);
                if (segment.Length != proof.Length || !Convert.FromHexString(segment.Hash).AsSpan().SequenceEqual(proof.Hash) ||
                    segment.NativeIdentity is not null && proof.Identity != segment.NativeIdentity) throw Invalid("A sealed segment was replaced."); seen++;
            }
            else if (name == "delta.journal" && state.Head.NativeIdentity is not null && proof.Identity != state.Head.NativeIdentity) throw Invalid();
            byte[] bytes = Encoding.UTF8.GetBytes(name); writer.Write((ushort)bytes.Length); writer.Write(bytes); writer.Write(proof.Length); writer.Write(proof.Hash);
            byte[] native = Encoding.UTF8.GetBytes(proof.Identity ?? ""); writer.Write(checked((ushort)native.Length)); writer.Write(native);
        }
        foreach (var name in MetadataExternalSort.Order(Names(header.Directory), Path.GetTempPath(), default, pins)) Record(name.Key);
        if (seen != state.Roots.Segments.Count) throw Invalid("The segment catalog membership changed.");
        Record(Guard); writer.Write((ushort)0); writer.Flush();
        output.Write(hashed.Hash());
    }
    private sealed class InventoryHashWriter(Stream output) : Stream
    {
        private readonly IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        public byte[] Hash() => hash.GetHashAndReset();
        public override void Write(ReadOnlySpan<byte> bytes) { output.Write(bytes); hash.AppendData(bytes); }
        public override void Write(byte[] bytes, int offset, int count) => Write(bytes.AsSpan(offset, count));
        public override void Flush() => output.Flush();
        public override bool CanRead => false;
        public override bool CanWrite => true;
        public override bool CanSeek => false;
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override long Length => throw new NotSupportedException();
        public override int Read(byte[] bytes, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long length) => throw new NotSupportedException();
        protected override void Dispose(bool disposing) { if (disposing) hash.Dispose(); base.Dispose(disposing); }
    }
    private static Header ReadHeader(string path)
    {
        using var reader = new BinaryReader(File.OpenRead(path), Encoding.UTF8); if (!reader.ReadBytes(5).AsSpan().SequenceEqual("SJRC2"u8)) throw Invalid();
        int length = reader.ReadInt32(); if (length < 1 || length > JournalControlIntegrity.MaximumBytes || length > reader.BaseStream.Length - reader.BaseStream.Position) throw Invalid();
        try { using var document = JsonDocument.Parse(reader.ReadBytes(length)); if (!JsonPropertyValidation.HasUniqueProperties(document.RootElement)) throw Invalid();
            var value = document.RootElement.Deserialize<Header>() ?? throw Invalid(); if (value.Version != 2 || value.Identity.Version != 3) throw Invalid(); return value; }
        catch (JsonException) { throw Invalid(); }
    }
    private static IEnumerable<Proof> Read(string path, Header expected)
    {
        if (JsonSerializer.Serialize(ReadHeader(path)) != JsonSerializer.Serialize(expected)) throw Invalid();
        using var reader = new BinaryReader(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read), Encoding.UTF8);
        long dataLength = reader.BaseStream.Length - 32; if (dataLength < 11) throw Invalid();
        using (var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
        {
            byte[] block = new byte[65536]; long left = dataLength;
            while (left > 0) { int count = (int)Math.Min(block.Length, left); reader.BaseStream.ReadExactly(block.AsSpan(0, count)); hash.AppendData(block.AsSpan(0, count)); left -= count; }
            byte[] expectedHash = reader.ReadBytes(32); if (!CryptographicOperations.FixedTimeEquals(hash.GetHashAndReset(), expectedHash)) throw Invalid();
        }
        reader.BaseStream.Position = 5; int control = reader.ReadInt32(); reader.BaseStream.Position += control; string? previous = null; bool guard = false;
        while (true)
        {
            if (dataLength - reader.BaseStream.Position < 2) throw Invalid(); int length = reader.ReadUInt16(); if (length == 0) break;
            if (guard || length > 128 || length > reader.BaseStream.Length - reader.BaseStream.Position - 42) throw Invalid();
            string name = Encoding.UTF8.GetString(reader.ReadBytes(length)); long bytes = reader.ReadInt64(); byte[] hash = reader.ReadBytes(32);
            int nativeLength = reader.ReadUInt16(); if (nativeLength > 4096 || nativeLength > reader.BaseStream.Length - reader.BaseStream.Position) throw Invalid();
            string? native = nativeLength == 0 ? null : Encoding.UTF8.GetString(reader.ReadBytes(nativeLength));
            if (!Name(name) || bytes < 0 || hash.Length != 32 || name != Guard && previous is not null && StringComparer.Ordinal.Compare(name, previous) <= 0) throw Invalid();
            previous = name; guard = name == Guard; yield return new(name, bytes, hash, native);
        }
        if (!guard || reader.BaseStream.Position != dataLength) throw Invalid();
    }
    private static Proof Observe(string path, string name, IJournalFilePins? pins)
    {
        WorkspaceStore.EnsurePlainPath(path); using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        string? identity = pins?.IdentifyOpened(input); long bytes = input.Length; byte[] hash = SHA256.HashData(input);
        if (input.Length != bytes) throw Invalid(); return new(name, bytes, hash, identity);
    }
    private static void Verify(string path, Proof proof, FileStream? guard, IJournalFilePins? pins)
    {
        if (proof.Identity is not null && pins is null) throw Invalid("Resume with the original native file identity adapter.");
        var observed = guard is null ? Observe(path, proof.Name, pins) : new(proof.Name, guard.Length, SHA256.HashData(Array.Empty<byte>()), pins?.IdentifyOpened(guard));
        if (observed.Length != proof.Length || observed.Identity != proof.Identity || !observed.Hash.AsSpan().SequenceEqual(proof.Hash)) throw Invalid("A remaining source file differs from its exact inventory.");
    }
    private static void ValidateFiles(string directory, Header header, string receipt, FileStream? guard, IJournalFilePins? pins)
    { foreach (var proof in Read(receipt, header)) { string path = Path.Combine(directory, proof.Name); if (File.Exists(path)) Verify(path, proof, proof.Name == Guard ? guard : null, pins); } }
    private static void ValidateNamespace(string directory, Header header, string receipt, bool allowIndices)
    {
        using var names = MetadataExternalSort.Order(Names(directory), Path.GetTempPath(), default).GetEnumerator(); bool has = names.MoveNext();
        foreach (var proof in Read(receipt, header))
        { if (proof.Name == Guard) continue; if (!has) continue; int order = StringComparer.Ordinal.Compare(names.Current.Key, proof.Name); if (order < 0) throw Invalid(); if (order == 0) has = names.MoveNext(); }
        if (has || !allowIndices && Directory.EnumerateDirectories(directory).Any()) throw Invalid("A foreign source entry cannot be reclaimed.");
    }
}
