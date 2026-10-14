using System.Buffers.Binary;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Splinter.BoundedLogProbe;

// Development evidence only. This intentionally changes SJL4's latest-only
// contract: superseded ranges can remain in the capped WAL until checkpoint.
// There is no group commit: every returned Write follows its own durable flush.
internal sealed class BoundedWal : IDisposable
{
    internal const int FileHeaderBytes = 96, FrameHeaderBytes = 96, FrameTailBytes = 36;
    private const int MaxPayload = 1024 * 1024;
    private readonly string root;
    private readonly ProbeIdentity identity;
    private readonly byte[] identityHash;
    private readonly FileStream ownerLock;
    private readonly Action<string>? boundary;
    private FileStream wal = null!;
    private Manifest manifest = null!;
    private bool poisoned;
    private readonly SortedDictionary<int, List<Range>> current = [];
    private sealed record Range(long Offset, byte[] Bytes);
    private sealed record Manifest(int Version, string IdentityHash, int Generation, long Sequence, string CheckpointHash);
    private sealed record Intent(int Version, string IdentityHash, int Previous, int Next, long Sequence);
    internal long Sequence { get; private set; }
    internal long FlushCount { get; private set; }
    internal long FlushTicks { get; private set; }
    internal long WrittenBytes { get; private set; }
    internal long CheckpointBytes { get; private set; }
    internal int CheckpointCount { get; private set; }
    internal long PeakOwnedBytes { get; private set; }
    internal long PeakWalBytes { get; private set; }
    internal long IncompleteTailBytes { get; private set; }
    internal long CurrentPrivateBytes => current.Values.Sum(ranges => ranges.Sum(range => (long)range.Bytes.Length));
    internal int CurrentRanges => current.Values.Sum(ranges => ranges.Count);

    internal BoundedWal(string root, ProbeIdentity identity, bool create, Action<string>? boundary = null)
    {
        this.root = Path.GetFullPath(root); this.identity = identity; this.boundary = boundary;
        identity.RequireValid(); identityHash = SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(identity));
        if (create)
        {
            if (Directory.Exists(root) && Directory.EnumerateFileSystemEntries(root).Any()) throw Error("Create target is not empty.");
            Directory.CreateDirectory(root);
        }
        RequirePlainDirectory(this.root);
        ownerLock = new FileStream(PathOf("writer.lock"), create ? FileMode.CreateNew : FileMode.Open,
            FileAccess.ReadWrite, FileShare.Read);
        try
        {
            if (create) Initialize();
            else
            {
                byte[] owner = File.ReadAllBytes(PathOf("owner.json"));
                if (!owner.AsSpan().SequenceEqual(JsonSerializer.SerializeToUtf8Bytes(identity))) throw Error("Owner or immutable basis differs.");
            }
            manifest = ReadJson<Manifest>("current.json"); RequireManifest(manifest);
            Intent? intent = File.Exists(PathOf("checkpoint.intent")) ? ReadJson<Intent>("checkpoint.intent") : null;
            RequireInventory(intent);
            LoadCheckpoint();
            wal = new FileStream(PathOf(WalName(manifest.Generation)), FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
            ReadHeader(wal, "BPWAL001", manifest.Generation, manifest.Sequence, 0);
            ReplayWal();
            // Never reclaim either side of a published intent before all
            // currently referenced bytes have independently validated.
            if (intent is not null) RecoverCheckpoint(intent);
            ObserveSize();
        }
        catch { wal?.Dispose(); ownerLock.Dispose(); throw; }
    }

    private void Initialize()
    {
        WriteNew("owner.json", JsonSerializer.SerializeToUtf8Bytes(identity));
        WriteNew(CheckpointName(0), Header("BPCPT001", 0, 0, 0));
        WriteNew(WalName(0), Header("BPWAL001", 0, 0, 0));
        WriteNew("current.json", JsonSerializer.SerializeToUtf8Bytes(new Manifest(1,
            Convert.ToHexStringLower(identityHash), 0, 0, HashFile(PathOf(CheckpointName(0))))));
    }

    internal void Write(int file, long offset, ReadOnlySpan<byte> bytes)
    {
        Healthy(); ValidateRange(file, offset, bytes.Length);
        if (bytes.IsEmpty) throw Error("Empty mutation is outside this probe's scope.");
        byte[] frame = EncodeFrame(Sequence + 1, file, offset, bytes);
        if (frame.Length + FileHeaderBytes > identity.WalCapBytes) throw Error("One frame exceeds the WAL cap.");
        try
        {
            if (wal.Length + frame.Length > identity.WalCapBytes) Checkpoint();
            wal.Position = wal.Length;
            wal.Write(frame.AsSpan(0, FrameHeaderBytes)); Visit("wal.after-header");
            int first = bytes.Length / 2;
            wal.Write(frame.AsSpan(FrameHeaderBytes, first)); Visit("wal.after-prefix");
            wal.Write(frame.AsSpan(FrameHeaderBytes + first)); WrittenBytes += frame.Length;
            Visit("wal.before-flush"); Durable(wal); Visit("wal.after-flush");
            Apply(file, offset, bytes); Sequence++;
            ObserveSize(); Visit("wal.before-ack");
        }
        catch { poisoned = true; throw; }
    }

    internal void Checkpoint()
    {
        Healthy();
        int next = checked(manifest.Generation + 1);
        var intent = new Intent(1, Convert.ToHexStringLower(identityHash), manifest.Generation, next, Sequence);
        try
        {
            WriteNew("checkpoint.intent", JsonSerializer.SerializeToUtf8Bytes(intent)); Visit("checkpoint.after-intent");
            string checkpoint = PathOf(CheckpointName(next));
            using (var output = new FileStream(checkpoint, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
            {
                output.Write(Header("BPCPT001", next, Sequence, CurrentRanges));
                Visit("checkpoint.after-header");
                foreach (var pair in current)
                    foreach (var range in pair.Value)
                    {
                        byte[] frame = EncodeFrame(Sequence, pair.Key, range.Offset, range.Bytes);
                        output.Write(frame); WrittenBytes += frame.Length; CheckpointBytes += frame.Length;
                    }
                Durable(output);
            }
            Visit("checkpoint.after-data-flush");
            WriteNew(WalName(next), Header("BPWAL001", next, Sequence, 0)); Visit("checkpoint.after-new-wal");
            var following = new Manifest(1, intent.IdentityHash, next, Sequence, HashFile(checkpoint));
            WriteNew("current.pending", JsonSerializer.SerializeToUtf8Bytes(following)); ObserveSize();
            Visit("checkpoint.before-publication");
            File.Replace(PathOf("current.pending"), PathOf("current.json"), null);
            Visit("checkpoint.after-publication");
            wal.Dispose();
            wal = new FileStream(PathOf(WalName(next)), FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
            manifest = following;
            // Reads still use the same RAM generation. Only private bytes were
            // checkpointed; no immutable basis file was read or copied here.
            RecoverCheckpoint(intent); CheckpointCount++; ObserveSize();
        }
        catch { poisoned = true; throw; }
    }

    internal int Read(int file, long offset, Span<byte> output, BasisFixture basis)
    {
        Healthy(); ValidateRange(file, offset, output.Length);
        long cursor = offset, end = offset + output.Length;
        if (current.TryGetValue(file, out var ranges))
            foreach (var range in ranges)
            {
                long start = Math.Max(offset, range.Offset), stop = Math.Min(end, range.Offset + range.Bytes.Length);
                if (start >= stop) continue;
                if (cursor < start) basis.Read(file, cursor, output.Slice((int)(cursor - offset), (int)(start - cursor)));
                range.Bytes.AsSpan((int)(start - range.Offset), (int)(stop - start)).CopyTo(output[(int)(start - offset)..]);
                cursor = stop;
            }
        if (cursor < end) basis.Read(file, cursor, output[(int)(cursor - offset)..]);
        return output.Length;
    }

    private void Apply(int file, long offset, ReadOnlySpan<byte> bytes)
    {
        long end = offset + bytes.Length;
        var next = new List<Range>();
        if (current.TryGetValue(file, out var ranges))
            foreach (var range in ranges)
            {
                long stop = range.Offset + range.Bytes.Length;
                if (stop <= offset || range.Offset >= end) next.Add(range);
                else
                {
                    if (range.Offset < offset) next.Add(new(range.Offset, range.Bytes[..(int)(offset - range.Offset)]));
                    if (stop > end) next.Add(new(end, range.Bytes[(int)(end - range.Offset)..]));
                }
            }
        next.Add(new(offset, bytes.ToArray())); next.Sort((a, b) => a.Offset.CompareTo(b.Offset)); current[file] = next;
    }

    private void LoadCheckpoint()
    {
        string path = PathOf(CheckpointName(manifest.Generation));
        if (HashFile(path) != manifest.CheckpointHash) throw Error("Current checkpoint checksum differs.");
        using var input = File.OpenRead(path);
        int count = ReadHeader(input, "BPCPT001", manifest.Generation, manifest.Sequence, null);
        if (count < 0 || count > 1_000_000) throw Error("Checkpoint range count is invalid.");
        int previousFile = -1; long previousEnd = 0;
        for (int i = 0; i < count; i++)
        {
            var frame = ReadFrame(input, false) ?? throw Error("Checkpoint ended early.");
            if (frame.Sequence != manifest.Sequence || frame.File < previousFile ||
                frame.File == previousFile && frame.Offset < previousEnd) throw Error("Checkpoint range inventory differs.");
            Apply(frame.File, frame.Offset, frame.Bytes); previousFile = frame.File; previousEnd = frame.Offset + frame.Bytes.Length;
        }
        if (input.Position != input.Length) throw Error("Checkpoint has trailing bytes.");
        Sequence = manifest.Sequence;
    }

    private void ReplayWal()
    {
        if (wal.Length > identity.WalCapBytes) throw Error("WAL cap exceeded.");
        while (wal.Position < wal.Length)
        {
            long before = wal.Position;
            var frame = ReadFrame(wal, true);
            if (frame is null)
            {
                IncompleteTailBytes = wal.Length - before; wal.SetLength(before); Durable(wal); break;
            }
            if (frame.Sequence != Sequence + 1) throw Error("WAL sequence is not consecutive.");
            Apply(frame.File, frame.Offset, frame.Bytes); Sequence = frame.Sequence;
        }
    }

    private sealed record DecodedFrame(long Sequence, int File, long Offset, byte[] Bytes);
    private DecodedFrame? ReadFrame(FileStream input, bool allowTail)
    {
        long remaining = input.Length - input.Position;
        if (remaining < FrameHeaderBytes)
        {
            byte[] prefix = new byte[remaining]; input.ReadExactly(prefix);
            if (!allowTail || !prefix.AsSpan(0, Math.Min(4, prefix.Length)).SequenceEqual("BWF1"u8[..Math.Min(4, prefix.Length)]))
                throw Error("Incomplete frame has an invalid prefix.");
            return null;
        }
        byte[] header = new byte[FrameHeaderBytes]; input.ReadExactly(header);
        if (!header.AsSpan(0, 4).SequenceEqual("BWF1"u8) || BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(4)) != 1 ||
            !header.AsSpan(32, 32).SequenceEqual(identityHash) || !SHA256.HashData(header.AsSpan(0, 64)).AsSpan().SequenceEqual(header.AsSpan(64)))
            throw Error("Frame header identity or checksum differs.");
        int length = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(28));
        int file = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(16));
        long offset = BinaryPrimitives.ReadInt64LittleEndian(header.AsSpan(20)); ValidateRange(file, offset, length);
        if (length == 0) throw Error("Zero-length frame.");
        if (input.Length - input.Position < length + FrameTailBytes)
        { if (allowTail) return null; throw Error("Checkpoint frame is incomplete."); }
        byte[] bytes = new byte[length], tail = new byte[FrameTailBytes]; input.ReadExactly(bytes); input.ReadExactly(tail);
        uint crc = Crc32(header, bytes);
        if (BinaryPrimitives.ReadUInt32LittleEndian(tail) != crc) throw Error("Frame CRC differs.");
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(header); hash.AppendData(bytes); hash.AppendData(tail.AsSpan(0, 4));
        if (!hash.GetHashAndReset().AsSpan().SequenceEqual(tail.AsSpan(4))) throw Error("Frame content checksum differs.");
        return new(BinaryPrimitives.ReadInt64LittleEndian(header.AsSpan(8)), file, offset, bytes);
    }

    private byte[] EncodeFrame(long sequence, int file, long offset, ReadOnlySpan<byte> bytes)
    {
        byte[] result = new byte[FrameHeaderBytes + bytes.Length + FrameTailBytes]; var header = result.AsSpan(0, FrameHeaderBytes);
        "BWF1"u8.CopyTo(header); BinaryPrimitives.WriteInt32LittleEndian(header[4..], 1);
        BinaryPrimitives.WriteInt64LittleEndian(header[8..], sequence); BinaryPrimitives.WriteInt32LittleEndian(header[16..], file);
        BinaryPrimitives.WriteInt64LittleEndian(header[20..], offset); BinaryPrimitives.WriteInt32LittleEndian(header[28..], bytes.Length);
        identityHash.CopyTo(header[32..]); SHA256.HashData(header[..64], header[64..]); bytes.CopyTo(result.AsSpan(FrameHeaderBytes));
        int crcAt = FrameHeaderBytes + bytes.Length;
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(crcAt), Crc32(header, bytes));
        SHA256.HashData(result.AsSpan(0, crcAt + 4), result.AsSpan(crcAt + 4)); return result;
    }

    private byte[] Header(string magic, int generation, long sequence, int count)
    {
        byte[] bytes = new byte[FileHeaderBytes]; Encoding.ASCII.GetBytes(magic).CopyTo(bytes, 0);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(8), 1); BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(12), generation);
        BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(16), sequence); BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(24), count);
        identityHash.CopyTo(bytes, 32); SHA256.HashData(bytes.AsSpan(0, 64), bytes.AsSpan(64)); return bytes;
    }
    private int ReadHeader(FileStream input, string magic, int generation, long sequence, int? count)
    {
        byte[] bytes = new byte[FileHeaderBytes]; input.ReadExactly(bytes);
        int actualCount = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(24));
        if (!bytes.AsSpan(0, 8).SequenceEqual(Encoding.ASCII.GetBytes(magic)) ||
            BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(8)) != 1 || BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(12)) != generation ||
            BinaryPrimitives.ReadInt64LittleEndian(bytes.AsSpan(16)) != sequence || count is not null && count != actualCount ||
            BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(28)) != 0 || !bytes.AsSpan(32, 32).SequenceEqual(identityHash) ||
            !SHA256.HashData(bytes.AsSpan(0, 64)).AsSpan().SequenceEqual(bytes.AsSpan(64))) throw Error("File header differs.");
        return actualCount;
    }

    private void RequireManifest(Manifest value)
    {
        if (value.Version != 1 || value.IdentityHash != Convert.ToHexStringLower(identityHash) || value.Generation < 0 || value.Sequence < 0 ||
            value.CheckpointHash.Length != 64 || !value.CheckpointHash.All(char.IsAsciiHexDigit)) throw Error("Current manifest differs.");
    }
    private void RequireInventory(Intent? intent)
    {
        var allowed = new HashSet<string>(StringComparer.Ordinal) { "owner.json", "writer.lock", "current.json",
            CheckpointName(manifest.Generation), WalName(manifest.Generation) };
        if (intent is not null)
        {
            if (intent.Version != 1 || intent.IdentityHash != Convert.ToHexStringLower(identityHash) || intent.Previous < 0 ||
                intent.Next != (long)intent.Previous + 1 || intent.Sequence < 0 ||
                manifest.Generation != intent.Previous && manifest.Generation != intent.Next ||
                manifest.Generation == intent.Next && manifest.Sequence != intent.Sequence ||
                manifest.Generation == intent.Previous && manifest.Sequence > intent.Sequence) throw Error("Checkpoint intent differs.");
            allowed.UnionWith(["checkpoint.intent", "current.pending", CheckpointName(intent.Previous), WalName(intent.Previous),
                CheckpointName(intent.Next), WalName(intent.Next)]);
        }
        foreach (string path in Directory.EnumerateFileSystemEntries(root))
            if (!allowed.Contains(Path.GetFileName(path)) || (File.GetAttributes(path) & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0)
                throw Error("Unknown or non-plain workspace member; preserve it.");
    }
    private void RecoverCheckpoint(Intent intent)
    {
        // If the old manifest won publication, discard only exact, intent-owned
        // unfinished output. If the new one won, retire only the prior files.
        if (manifest.Generation == intent.Previous && Sequence != intent.Sequence) throw Error("Old generation does not reach checkpoint intent.");
        int remove = manifest.Generation == intent.Previous ? intent.Next : intent.Previous;
        foreach (string name in new[] { CheckpointName(remove), WalName(remove), "current.pending" })
        { string path = PathOf(name); if (File.Exists(path)) File.Delete(path); }
        Visit("checkpoint.after-old-retirement"); File.Delete(PathOf("checkpoint.intent"));
    }
    private void ValidateRange(int file, long offset, int count)
    {
        if (file < 0 || file >= identity.Objects || offset < 0 || count < 0 || count > MaxPayload ||
            offset > identity.FileLength(file) - count) throw Error("Range is outside its immutable logical object.");
    }
    private void Healthy() { ObjectDisposedException.ThrowIf(disposed, this); if (poisoned) throw Error("Failed writer must reconnect."); }
    private void Visit(string name) => boundary?.Invoke(name);
    private void Durable(FileStream stream)
    { long before = Stopwatch.GetTimestamp(); try { stream.Flush(true); } finally { FlushTicks += Stopwatch.GetTimestamp() - before; FlushCount++; } }
    private void WriteNew(string name, byte[] bytes)
    { using var file = new FileStream(PathOf(name), FileMode.CreateNew, FileAccess.Write, FileShare.Read); file.Write(bytes); WrittenBytes += bytes.Length; Durable(file); }
    private T ReadJson<T>(string name) => JsonSerializer.Deserialize<T>(File.ReadAllBytes(PathOf(name))) ?? throw Error("Null manifest.");
    private void ObserveSize()
    { PeakWalBytes = Math.Max(PeakWalBytes, wal.Length); PeakOwnedBytes = Math.Max(PeakOwnedBytes, Directory.EnumerateFiles(root).Sum(p => new FileInfo(p).Length)); }
    private string PathOf(string name)
    {
        string path = Path.Combine(root, name);
        if (File.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw Error("Reparse point in owned store.");
        return path;
    }
    private static string CheckpointName(int generation) => $"checkpoint-{generation:d8}.bin";
    private static string WalName(int generation) => $"wal-{generation:d8}.bin";
    private static void RequirePlainDirectory(string directory)
    {
        for (DirectoryInfo? at = new(directory); at is not null; at = at.Parent)
            if ((at.Attributes & FileAttributes.ReparsePoint) != 0) throw Error("Reparse point in store ancestry.");
    }
    internal static string HashFile(string path) { using var file = File.OpenRead(path); return Convert.ToHexStringLower(SHA256.HashData(file)); }
    private static readonly uint[] CrcTable = Enumerable.Range(0, 256).Select(value =>
    { uint crc = (uint)value; for (int bit = 0; bit < 8; bit++) crc = (crc >> 1) ^ ((crc & 1) != 0 ? 0xedb88320u : 0); return crc; }).ToArray();
    private static uint Crc32(ReadOnlySpan<byte> first, ReadOnlySpan<byte> second)
    { uint crc = uint.MaxValue; foreach (byte b in first) crc = (crc >> 8) ^ CrcTable[(crc ^ b) & 255]; foreach (byte b in second) crc = (crc >> 8) ^ CrcTable[(crc ^ b) & 255]; return ~crc; }
    private static InvalidDataException Error(string message) => new(message);
    private bool disposed;
    public void Dispose() { if (disposed) return; disposed = true; wal?.Dispose(); ownerLock.Dispose(); }
}
