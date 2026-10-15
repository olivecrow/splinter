using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Splinter.Core;
using Splinter.Runtime;

namespace Splinter.BoundedLogProbe;

internal sealed record ProbeIdentity(int Version, string Owner, string BasisHash, long BasisBytes,
    int Objects, int HotObjects, int HotFileBytes, int ColdFileBytes, long WalCapBytes)
{
    internal long FileLength(int file) => file < HotObjects ? HotFileBytes : ColdFileBytes;
    internal long FileOffset(int file) => file < HotObjects ? (long)file * HotFileBytes : (long)HotObjects * HotFileBytes + (long)(file - HotObjects) * ColdFileBytes;
    internal void RequireValid()
    {
        if (Version != 1 || string.IsNullOrWhiteSpace(Owner) || Owner.Length > 100 || BasisHash.Length != 64 || !BasisHash.All(char.IsAsciiHexDigit) ||
            Objects < 1 || Objects > 20000 || HotObjects < 1 || HotObjects > Objects || HotObjects > 256 || HotFileBytes < 4096 || HotFileBytes > 1048576 ||
            ColdFileBytes < 1 || ColdFileBytes > 4096 || BasisBytes != (long)HotObjects * HotFileBytes + (long)(Objects - HotObjects) * ColdFileBytes ||
            BasisBytes > 128L * 1048576 || WalCapBytes < 8192 || WalCapBytes > 64L * 1048576) throw new InvalidDataException("Unsupported probe identity or bounds.");
    }
}

internal sealed class BasisFixture : ISnapshotTree, IDisposable
{
    private readonly FileStream input;
    internal ProbeIdentity Identity { get; }
    internal string Path { get; }
    internal long BytesRead { get; private set; }
    public SnapshotReference Snapshot { get; }
    public bool SupportsConcurrentReads => false;
    internal BasisFixture(string path, ProbeIdentity identity)
    {
        Path = System.IO.Path.GetFullPath(path); Identity = identity; identity.RequireValid();
        // Pin the original against writes/deletion for the complete run.
        input = new FileStream(Path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (input.Length != identity.BasisBytes || Convert.ToHexStringLower(SHA256.HashData(input)) != identity.BasisHash)
        { input.Dispose(); throw new InvalidDataException("Immutable original differs."); }
        Snapshot = new("basis-" + identity.BasisHash, "bounded-log-probe", Path, Path,
            new DateTimeOffset(2026, 10, 15, 0, 0, 0, TimeSpan.Zero), true, true, "physical-read-only-fixture");
    }
    internal static ProbeIdentity Create(string path, string owner, int objects, int hot, int hotBytes, long cap)
    {
        const int cold = 64;
        long length = (long)hot * hotBytes + (long)(objects - hot) * cold;
        new ProbeIdentity(1, owner, new string('0', 64), length, objects, hot, hotBytes, cold, cap).RequireValid();
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        using (var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
        {
            byte[] buffer = new byte[65536]; long at = 0;
            while (at < length)
            {
                int count = (int)Math.Min(buffer.Length, length - at);
                for (int i = 0; i < count; i++) buffer[i] = (byte)((at + i) * 37 + 19);
                output.Write(buffer, 0, count); at += count;
            }
            output.Flush(true);
        }
        var identity = new ProbeIdentity(1, owner, BoundedWal.HashFile(path), length, objects, hot, hotBytes, cold, cap);
        identity.RequireValid(); return identity;
    }
    public void EnsureAvailable() { if (input.Length != Identity.BasisBytes) throw new InvalidDataException("Pinned original length changed."); }
    internal static string Name(int file) => $"f{file:d5}.bin";
    private int FileId(string name)
    {
        if (name.Length != 10 || name[0] != 'f' || !name.EndsWith(".bin", StringComparison.Ordinal) ||
            !int.TryParse(name.AsSpan(1, 5), out int id) || id < 0 || id >= Identity.Objects) return -1;
        return id;
    }
    public SnapshotEntry? Stat(string relativePath)
    {
        long stamp = Snapshot.CapturedAt.UtcDateTime.ToFileTimeUtc();
        if (relativePath == "") return new("", true, 0, 16, stamp, stamp, stamp);
        int file = FileId(relativePath);
        return file < 0 ? null : new(relativePath, false, Identity.FileLength(file), 32, stamp, stamp, stamp);
    }
    public IReadOnlyList<SnapshotEntry> List(string relativeDirectory) => relativeDirectory == ""
        ? Enumerable.Range(0, Identity.Objects).Select(i => Stat(Name(i))!).ToArray() : [];
    public int Read(string relativePath, long offset, Span<byte> destination)
    {
        int file = FileId(relativePath); if (file < 0) throw new FileNotFoundException(relativePath);
        return Read(file, offset, destination);
    }
    internal int Read(int file, long offset, Span<byte> destination)
    {
        if (file < 0 || file >= Identity.Objects || offset < 0 || offset > Identity.FileLength(file) - destination.Length)
            throw new ArgumentOutOfRangeException(nameof(offset));
        int total = 0;
        while (total < destination.Length)
        {
            int count = RandomAccess.Read(input.SafeFileHandle, destination[total..], Identity.FileOffset(file) + offset + total);
            if (count == 0) throw new EndOfStreamException(); total += count;
        }
        BytesRead += total; return total;
    }
    public void Dispose() => input.Dispose();
}

internal interface IProbeStore : IDisposable
{
    long Sequence { get; }
    void Write(int file, long offset, byte[] bytes);
    int Read(int file, long offset, Span<byte> bytes);
    object Metrics();
}
internal sealed class WalProbeStore(string root, BasisFixture basis, bool create, Action<string>? boundary = null) : IProbeStore
{
    internal BoundedWal Store { get; } = new(root, basis.Identity, create, boundary);
    public long Sequence => Store.Sequence;
    public void Write(int file, long offset, byte[] bytes) => Store.Write(file, offset, bytes);
    public int Read(int file, long offset, Span<byte> bytes) => Store.Read(file, offset, bytes, basis);
    public object Metrics() => new { Store.Sequence, Store.FlushCount, Store.FlushTicks, Store.WrittenBytes,
        Store.CheckpointBytes, Store.CheckpointCount, Store.CurrentPrivateBytes, Store.CurrentRanges,
        Store.PeakOwnedBytes, Store.PeakWalBytes, Store.IncompleteTailBytes };
    public void Dispose() => Store.Dispose();
}
internal sealed class SjlProbeStore : IProbeStore
{
    private readonly JournalDeltaStore store;
    private readonly Guid[] ids;
    internal SjlProbeStore(string root, BasisFixture basis, Action<string>? boundary = null, bool bounded = false)
    {
        var identity = new OverlayIdentity(2, basis.Identity.Owner, basis.Snapshot, 512L * 1048576, 1048576);
        long? cap = bounded ? basis.Identity.WalCapBytes : null;
        store = boundary is null ? new(root, identity, basis, boundedLogBytes: cap)
            : new(root, identity, basis, new Observer(boundary), boundedLogBytes: cap);
        ids = Enumerable.Range(0, basis.Identity.Objects).Select(i => store.Stat(BasisFixture.Name(i))!.Id).ToArray();
    }
    public long Sequence => store.Observe().DurableSequence;
    public void Write(int file, long offset, byte[] bytes) => store.Write(ids[file], offset, bytes);
    public int Read(int file, long offset, Span<byte> bytes) => store.Read(ids[file], offset, bytes);
    public object Metrics() => new { storage = store.Observe(), store.BoundedCheckpointCount,
        store.BoundedCheckpointWrittenBytes, store.BoundedCheckpointTicks };
    public void Dispose() => store.Dispose();
    private sealed class Observer(Action<string> boundary) : IJournalMutationObserver
    { public void Observe(JournalMutationBoundary value) => boundary(value.Boundary); }
}

internal sealed record Mutation(int File, int Offset, byte[] Bytes);
internal static class Workload
{
    internal static Mutation At(int number, ProbeIdentity identity, int seedPerFile)
    {
        int length = Math.Min(seedPerFile, new[] { 4096, 8192, 16384 }[number % 3]);
        int slots = (seedPerFile - length) / 4096 + 1;
        int file = number % identity.HotObjects;
        int offset = (int)((uint)(number * 2654435761L) % slots) * 4096;
        return new(file, offset, Payload(number + 100000, file, length));
    }
    internal static IEnumerable<Mutation> Seeds(ProbeIdentity identity, int seedPerFile)
    {
        for (int file = 0; file < identity.HotObjects; file++)
            for (int offset = 0; offset < seedPerFile; offset += 16384)
                yield return new(file, offset, Payload(offset / 16384, file, Math.Min(16384, seedPerFile - offset)));
    }
    internal static byte[] Payload(int sequence, int file, int length)
    {
        byte[] result = new byte[length]; uint value = unchecked((uint)(sequence * 104729L + file * 13007L + 1));
        for (int i = 0; i < result.Length; i++) { value ^= value << 13; value ^= value >> 17; value ^= value << 5; result[i] = (byte)value; }
        return result;
    }
}

internal static class ResultArchive
{
    internal static string Export(string path, IProbeStore store, ProbeIdentity identity)
    {
        using (var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
        {
            output.Write("BPRSLT01"u8); byte[] description = JsonSerializer.SerializeToUtf8Bytes(identity);
            Span<byte> number = stackalloc byte[8]; BinaryPrimitives.WriteInt64LittleEndian(number, description.Length);
            output.Write(number); output.Write(description);
            byte[] buffer = new byte[65536];
            for (int file = 0; file < identity.Objects; file++)
            {
                BinaryPrimitives.WriteInt64LittleEndian(number, identity.FileLength(file)); output.Write(number);
                for (long offset = 0; offset < identity.FileLength(file);)
                {
                    int count = (int)Math.Min(buffer.Length, identity.FileLength(file) - offset);
                    if (store.Read(file, offset, buffer.AsSpan(0, count)) != count) throw new EndOfStreamException();
                    output.Write(buffer, 0, count); offset += count;
                }
            }
            output.Flush(true);
        }
        return BoundedWal.HashFile(path);
    }
    // Reads only the independent archive. No snapshot/store paths are accepted.
    internal static long Verify(string path, string expectedHash, ProbeIdentity expectedIdentity)
    {
        if (BoundedWal.HashFile(path) != expectedHash) throw new InvalidDataException("Independent result changed.");
        using var input = File.OpenRead(path); byte[] magic = new byte[8]; input.ReadExactly(magic);
        if (!magic.AsSpan().SequenceEqual("BPRSLT01"u8)) throw new InvalidDataException("Result format differs.");
        input.ReadExactly(magic); long length = BinaryPrimitives.ReadInt64LittleEndian(magic);
        if (length < 1 || length > 65536) throw new InvalidDataException("Result header bound differs.");
        byte[] description = new byte[length]; input.ReadExactly(description);
        if (JsonSerializer.Deserialize<ProbeIdentity>(description) != expectedIdentity) throw new InvalidDataException("Result owner differs.");
        long payload = 0; byte[] scratch = new byte[65536];
        for (int file = 0; file < expectedIdentity.Objects; file++)
        {
            input.ReadExactly(magic); length = BinaryPrimitives.ReadInt64LittleEndian(magic);
            if (length != expectedIdentity.FileLength(file)) throw new InvalidDataException("Result inventory differs.");
            payload += length;
            while (length != 0) { int count = (int)Math.Min(scratch.Length, length); input.ReadExactly(scratch.AsSpan(0, count)); length -= count; }
        }
        if (input.Position != input.Length) throw new InvalidDataException("Result has trailing bytes.");
        return payload;
    }
}
