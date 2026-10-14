using System.Buffers;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Splinter.Core;
namespace Splinter.Runtime;

// One authoritative capsule per mutation: data and metadata share ONE durable
// flush. ReplaceFile moves the old head to an owned backup without copying it.
internal sealed class LatestJournalStorage
{
    internal sealed record Root(int Version, int NodeFormat, string IdentityHash, string Objects, long Sequence, long NextBlock,
        long FileBytes, int FileCount, long PayloadBytes, long LogicalBytes, string[] ProjectionUnsafe);
    internal sealed record Put(string Name, string? BeforeHash, byte[] Bytes);
    internal sealed record Remove(string Name, string Hash);
    private sealed record Item(string Name, int Length, string Hash, long Offset, byte[]? Metadata);
    private sealed record Retire(string Name, string Hash, long KeepBytes, string? PrefixHash);
    private sealed record CommitRecord(int Version, long Generation, Root After, Item[] Items, string[] Removes, Retire[] Retires);
    private sealed record Location(string Path, long Offset, int Length, byte[]? Metadata);
    private sealed record Frame(string Path, byte[] Header, CommitRecord Commit, byte[] Metadata, long DataEnd, long Length);
    private readonly record struct BlockAddress(long Id, long Offset, int Length, bool Page);
    private static readonly JsonSerializerOptions Json = new() { RespectRequiredConstructorParameters = true, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
    internal const int BlockHeaderBytes = 80;
    private const int HeaderBytes = 128;
    private const long MaxFrame = 256L * 1024 * 1024;
    private const string Pending = "transaction.pending";
    // Reconnect/seal keeps addresses, never payloads, until generation-ordered
    // replay. Bound the temporary cache; unusually dense inputs retain the
    // ordinary same-content index pass once this budget is exhausted.
    private const int MaxBufferedBlockAddresses = 131072;
    private readonly string rootPath, directory, owner;
    private readonly byte[] ownerBytes;
    private readonly long limit;
    private readonly IJournalDirectoryGuard? directoryGuard;
    private readonly Dictionary<string, Location> objects = new(StringComparer.Ordinal);
    private readonly Dictionary<string, HashSet<string>> objectsByPath = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Frame> frames = new(StringComparer.Ordinal);
    private Frame? head;
    private long generation;
    // Size accounting for the exclusive writer, rebuilt from the full inventory
    // on reconnect. Explicit observations and seals still inspect actual files.
    private long committedBytes;
    private readonly SealHash? sealing;
    private sealed class SealHash(string root) : IDisposable
    {
        internal readonly IncrementalHash Hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        internal readonly List<string> Files = [];
        internal long Bytes;
        internal long Begin(FileStream file)
        {
            long length = file.Length; Files.Add(file.Name); Bytes = checked(Bytes + length);
            if (file.Name != root)
            {
                byte[] name = Encoding.UTF8.GetBytes(Path.GetFileName(file.Name)); Span<byte> size = stackalloc byte[8];
                BinaryPrimitives.WriteInt64LittleEndian(size, name.Length); Hash.AppendData(size); Hash.AppendData(name);
                BinaryPrimitives.WriteInt64LittleEndian(size, length); Hash.AppendData(size);
            }
            return length;
        }
        internal void AddFile(string path)
        {
            using var file = Open(path); AddFile(file);
        }
        internal void AddFile(FileStream file)
        {
            file.Position = 0; long length = Begin(file); byte[] buffer = new byte[65536];
            for (int n; (n = file.Read(buffer)) != 0;) Hash.AppendData(buffer.AsSpan(0, n));
            if (file.Position != length || file.Length != length) throw Corrupt("A journal file changed during seal.");
        }
        public void Dispose() => Hash.Dispose();
    }
    internal Action<long>? FlushObserver;
    internal Root Current { get; private set; }
    internal string ObjectDirectory => directory;
    internal long Bytes => new FileInfo(rootPath).Length + Directory.EnumerateFiles(directory).Sum(p => new FileInfo(p).Length);
    internal long CommittedBytes => committedBytes;
    private static FileStream Open(string path) => new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
    internal static bool IsLatest(string path) { using var f = Open(path); Span<byte> b = stackalloc byte[4]; return f.Read(b) == 4 && b[..3].SequenceEqual("SJL"u8); }
    internal LatestJournalStorage(string path, OverlayIdentity identity, bool readOnly, IJournalDirectoryGuard? directoryGuard = null,
        int bufferedBlockAddressLimit = MaxBufferedBlockAddresses)
        : this(path, identity, readOnly, null, directoryGuard, bufferedBlockAddressLimit) { }
    private LatestJournalStorage(string path, OverlayIdentity identity, bool readOnly, SealHash? sealing, IJournalDirectoryGuard? directoryGuard,
        int bufferedBlockAddressLimit)
    {
        if (bufferedBlockAddressLimit is < 0 or > MaxBufferedBlockAddresses) throw new ArgumentOutOfRangeException(nameof(bufferedBlockAddressLimit));
        this.sealing = sealing;
        this.directoryGuard = directoryGuard;
        rootPath = path; owner = Hash(JsonSerializer.SerializeToUtf8Bytes(identity)); ownerBytes = Convert.FromHexString(owner); limit = identity.MaxJournalBytes;
        using var root = Open(path);
        Current = ReadRoot(root); if (Current.IdentityHash != owner) throw Corrupt("The journal has another identity.");
        directory = Path.Combine(Path.GetDirectoryName(path)!, Current.Objects); WorkspaceStore.EnsurePlainPath(directory);
        if (directoryGuard is not null && !WorkspaceStore.SamePath(directoryGuard.DirectoryPath, directory))
            throw Corrupt("The directory proof belongs to another object generation.");
        if (!Directory.Exists(directory)) throw Corrupt("The owned object directory is missing.");
        List<BlockAddress>? headAddresses = null;
        if (Capsule(root)) { head = ReadFrame(path, root, out headAddresses, addressLimit: bufferedBlockAddressLimit); generation = head.Commit.Generation; }
        else sealing?.AddFile(root);
        // Do not hold the root across recovery/publication on Windows.
        root.Dispose();
        int remainingAddresses = bufferedBlockAddressLimit - (headAddresses?.Count ?? 0);
        var parsedFrames = new List<(Frame Frame, List<BlockAddress>? Addresses)>();
        var generations = new HashSet<long>();
        IEnumerable<string> inventory = Directory.EnumerateFileSystemEntries(directory);
        if (sealing is not null) inventory = inventory.OrderBy(Path.GetFileName, StringComparer.Ordinal);
        foreach (string entry in inventory)
        {
            EnsureObjectPath(entry); string name = Path.GetFileName(entry);
            if (!File.Exists(entry) || !Plain(name) && !Backup(name) && name != Pending) throw Corrupt("An unowned object is present.");
            if (Plain(name)) { SetLocation(name, new(entry, 0, checked((int)new FileInfo(entry).Length), null)); sealing?.AddFile(entry); }
            else if (Backup(name))
            {
                using var file = Open(entry);
                if (!Capsule(file))
                {
                    var plan = head?.Commit.Retires.SingleOrDefault(r => r.Name == name && r.KeepBytes == 0);
                    if (plan is null || HashFile(file, sealing: sealing) != plan.Hash) throw Corrupt("An unowned seed backup is present.");
                    continue;
                }
                var frame = ReadFrame(entry, file, out var addresses, allowPrefix: true, remainingAddresses);
                if (head is null || frame.Commit.Generation >= generation || !generations.Add(frame.Commit.Generation))
                    throw Corrupt("A backup has an unexpected generation.");
                frames.Add(entry, frame);
                parsedFrames.Add((frame, addresses)); remainingAddresses -= addresses?.Count ?? 0;
            }
        }
        foreach (var parsed in parsedFrames.OrderBy(f => f.Frame.Commit.Generation)) Index(parsed.Frame, parsed.Addresses);
        if (head is not null) { Index(head, headAddresses); frames[rootPath] = head; }
        string pending = PathOf(Pending);
        if (File.Exists(pending)) { if (readOnly) throw new WorkspaceException("DeltaReconnectRequired", "The owned writer must finish its capsule."); Recover(pending); }
        if (!readOnly) Finish();
        committedBytes = Inventory();
    }
    private static bool Capsule(FileStream f) { f.Position = 0; Span<byte> b = stackalloc byte[8]; bool result = f.Read(b) == 8 && b[..4].SequenceEqual("SJL4"u8) && BinaryPrimitives.ReadInt32LittleEndian(b[4..]) == 4; f.Position = 0; return result; }
    internal static Root ReadRoot(string path)
    {
        using var f = Open(path); return ReadRoot(f);
    }
    private static Root ReadRoot(FileStream f)
    {
        Span<byte> header = stackalloc byte[HeaderBytes]; int read = f.Read(header);
        if (read < 8 || !header[..4].SequenceEqual("SJL4"u8)) throw new WorkspaceException("OverlayFormatUnsupported", "Only SJL4 is supported; preserve earlier roots.");
        Root root;
        if (BinaryPrimitives.ReadInt32LittleEndian(header[4..]) == 4)
        {
            if (read != HeaderBytes) throw Corrupt("A current head is incomplete.");
            long data = BinaryPrimitives.ReadInt64LittleEndian(header[16..]); int count = BinaryPrimitives.ReadInt32LittleEndian(header[24..]);
            if (data < 0 || count < 1 || HeaderBytes + data + count + 32 > MaxFrame || f.Length != HeaderBytes + data + count + 32) throw Corrupt("The current head footer is incomplete.");
            f.Position = HeaderBytes + data; byte[] meta = new byte[count]; f.ReadExactly(meta);
            try { root = JsonSerializer.Deserialize<CommitRecord>(meta, Json)?.After ?? throw Corrupt("The current head is empty."); }
            catch (JsonException) { throw Corrupt("Current head metadata is invalid."); }
        }
        else { f.Position = 0; root = Decode<Root>(ReadShared(f, 65536), "SJL4", 65536); }
        ValidateRoot(root); return root;
    }
    private static void ValidateRoot(Root r)
    {
        if (r.Version != 4 || r.NodeFormat != 3 || r.IdentityHash.Length != 64 || !r.IdentityHash.All(Uri.IsHexDigit) || r.Objects.Length != 39 ||
            !r.Objects.StartsWith("latest-", StringComparison.Ordinal) || !Guid.TryParseExact(r.Objects[7..], "N", out _) ||
            r.Sequence < 0 || r.NextBlock < 1 || r.NextBlock >= long.MaxValue / JournalDeltaStore.LatestBlockStride || r.FileBytes < 0 ||
            r.FileCount < 0 || r.PayloadBytes < 0 || r.LogicalBytes < 0 || r.PayloadBytes > r.FileBytes || r.ProjectionUnsafe is null) throw Corrupt("Current root fields are invalid.");
    }
    internal static byte[] Encode<T>(T value, string magic)
    {
        byte[] json = JsonSerializer.SerializeToUtf8Bytes(value), b = new byte[checked(44 + json.Length)]; Encoding.ASCII.GetBytes(magic, b);
        BinaryPrimitives.WriteInt64LittleEndian(b.AsSpan(4), json.Length); json.CopyTo(b, 12); SHA256.HashData(b.AsSpan(0, b.Length - 32), b.AsSpan(b.Length - 32)); return b;
    }
    internal static T Decode<T>(byte[] b, string magic, long bound)
    {
        if (b.Length < 44 || b.Length > bound || !b.AsSpan(0, 4).SequenceEqual(Encoding.ASCII.GetBytes(magic)) || BinaryPrimitives.ReadInt64LittleEndian(b.AsSpan(4)) != b.Length - 44 ||
            !CryptographicOperations.FixedTimeEquals(SHA256.HashData(b.AsSpan(0, b.Length - 32)), b.AsSpan(b.Length - 32))) throw Corrupt("A complete record is damaged.");
        try { return JsonSerializer.Deserialize<T>(b.AsSpan(12, b.Length - 44), Json) ?? throw Corrupt("Metadata is empty."); } catch (JsonException) { throw Corrupt("Metadata is invalid."); }
    }
    private static byte[] ReadShared(string path, long bound) { using var f = Open(path); return ReadShared(f, bound); }
    private static byte[] ReadShared(FileStream f, long bound) { if (f.Length > bound) throw Corrupt("A record exceeds its bound."); byte[] b = new byte[checked((int)f.Length)]; f.ReadExactly(b); return b; }
    internal static string Hash(byte[] b) => Convert.ToHexStringLower(SHA256.HashData(b));
    private static string HashFile(string path, long? prefix = null, SealHash? sealing = null)
    {
        using var f = Open(path); return HashFile(f, prefix, sealing);
    }
    private static string HashFile(FileStream f, long? prefix = null, SealHash? sealing = null)
    {
        if (sealing is not null && prefix is not null) throw Corrupt("A composite seal requires complete physical files.");
        f.Position = 0; using var h = IncrementalHash.CreateHash(HashAlgorithmName.SHA256); byte[] b = new byte[65536];
        long length = sealing?.Begin(f) ?? f.Length;
        long left = prefix ?? length;
        while (left != 0) { int n = f.Read(b, 0, (int)Math.Min(left, b.Length)); if (n == 0) throw Corrupt("A file is short."); h.AppendData(b.AsSpan(0, n)); sealing?.Hash.AppendData(b.AsSpan(0, n)); left -= n; }
        if (sealing is not null && (f.Length != length || f.Position != length)) throw Corrupt("A journal file changed during seal.");
        return Convert.ToHexStringLower(h.GetHashAndReset());
    }
    private static (string Full, string Prefix) HashRetainedFile(string path, long prefix)
    {
        using var file = Open(path);
        long length = file.Length;
        if (prefix <= 0 || prefix >= length) throw Corrupt("A retained prefix is outside its file.");
        using var full = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        using var retained = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] buffer = new byte[65536]; long left = length, prefixLeft = prefix;
        while (left != 0)
        {
            int count = file.Read(buffer, 0, (int)Math.Min(left, buffer.Length));
            if (count == 0) throw Corrupt("A retirement target is short.");
            full.AppendData(buffer.AsSpan(0, count));
            int prefixCount = (int)Math.Min(prefixLeft, count);
            if (prefixCount != 0) retained.AppendData(buffer.AsSpan(0, prefixCount));
            prefixLeft -= prefixCount; left -= count;
        }
        if (file.Length != length) throw Corrupt("A retirement target changed length during hashing.");
        return (Convert.ToHexStringLower(full.GetHashAndReset()), Convert.ToHexStringLower(retained.GetHashAndReset()));
    }
    private static bool Plain(string n) => n.Length < 80 && n.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '.') &&
        ((n.StartsWith("node-", StringComparison.Ordinal) || n.StartsWith("bucket-", StringComparison.Ordinal)) && n.EndsWith(".json", StringComparison.Ordinal) || DataName(n) && n.EndsWith(".bin", StringComparison.Ordinal) || n == "owner.lock");
    private static bool DataName(string n) => n.StartsWith("block-", StringComparison.Ordinal) || n.StartsWith("page-", StringComparison.Ordinal);
    private static bool Backup(string n) => n.Length == 125 && n.StartsWith("commit-", StringComparison.Ordinal) && n[23] == '-' && n[56] == '-' && n.EndsWith(".bin", StringComparison.Ordinal) && n.AsSpan(7, 16).ToString().All(Uri.IsHexDigit) && Guid.TryParseExact(n.AsSpan(24, 32), "N", out _) && n.AsSpan(57, 64).ToString().All(Uri.IsHexDigit);
    private string BackupName() => $"commit-{generation:x16}-{Guid.NewGuid():N}-{(head is null ? HashFile(rootPath) : Hash(head.Header))}.bin";
    private void EnsureObjectPath(string path)
    {
        if (directoryGuard is null) WorkspaceStore.EnsurePlainPath(path);
        else directoryGuard.EnsureFile(path);
    }
    private string PathOf(string name) { if (!Plain(name) && !Backup(name) && name != Pending) throw Corrupt("An object is outside its owned namespace."); string p = Path.Combine(directory, name); EnsureObjectPath(p); return p; }
    internal IEnumerable<string> Names() => objects.Keys;
    internal long FileLength(string n) => objects.TryGetValue(n, out var l) ? l.Length : throw Corrupt("An object is missing.");
    internal byte[] Read(string n)
    {
        if (!objects.TryGetValue(n, out var l)) throw Corrupt("A current object is missing.");
        if (l.Metadata is not null) { Footer(frames[l.Path]); return (byte[])l.Metadata.Clone(); }
        using var f = Open(l.Path); f.Position = l.Offset; byte[] b = new byte[l.Length]; f.ReadExactly(b); return b;
    }
    internal string? FileHash(string n)
    {
        if (!objects.TryGetValue(n, out var location)) return null;
        if (location.Metadata is null) return Hash(Read(n));
        Footer(frames[location.Path]);
        return Hash(location.Metadata);
    }
    internal FileStream OpenBlock(long id) { if (!objects.TryGetValue(BlockName(id), out var l)) throw Corrupt("A private block is missing."); var f = new FileStream(l.Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 4096, FileOptions.RandomAccess); f.Position = l.Offset; return f; }
    internal long BlockOffset(long id) => objects[BlockName(id)].Offset + BlockHeaderBytes;
    internal static string BlockName(long id) => $"block-{id:x16}.bin";
    internal static string PageName(long id) => $"page-{id:x16}.bin";
    internal static byte[] EncodePage(long id, string owner, ReadOnlySpan<byte> payload)
    {
        byte[] bytes = EncodeBlock(id, owner, payload); "SJM2"u8.CopyTo(bytes); return bytes;
    }
    internal static byte[] EncodeBlock(long id, string owner, ReadOnlySpan<byte> payload)
    {
        byte[] b = new byte[checked(BlockHeaderBytes + payload.Length)]; "SJB1"u8.CopyTo(b); BinaryPrimitives.WriteInt64LittleEndian(b.AsSpan(4), id); BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(12), payload.Length);
        Convert.FromHexString(owner).CopyTo(b, 16); SHA256.HashData(payload, b.AsSpan(48, 32)); payload.CopyTo(b.AsSpan(BlockHeaderBytes)); return b;
    }
    internal void ValidateBlock(long id, int count) { using var f = OpenBlock(id); byte[] h = new byte[BlockHeaderBytes]; f.ReadExactly(h); BlockHeader(h, id, count); }
    internal void ValidatePage(long id, int count)
    {
        if (!objects.TryGetValue(PageName(id), out var location)) throw Corrupt("A metadata page is missing.");
        using var file = Open(location.Path); file.Position = location.Offset;
        byte[] header = new byte[BlockHeaderBytes]; file.ReadExactly(header); BlockHeader(header, id, count, page: true);
    }
    private void BlockHeader(byte[] b, long id, int count, bool page = false) { if (b.Length != BlockHeaderBytes || !b.AsSpan(0, 4).SequenceEqual(page ? "SJM2"u8 : "SJB1"u8) || BinaryPrimitives.ReadInt64LittleEndian(b.AsSpan(4)) != id || BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(12)) != count || id < 1 || count <= 0 || count > 1048576 || !b.AsSpan(16, 32).SequenceEqual(ownerBytes)) throw Corrupt("A private block has another owner or address."); }
    private static void Footer(Frame frame)
    {
        using var f = Open(frame.Path);
        if (f.Length != frame.Length) throw Corrupt("Current metadata changed.");
        f.Position = frame.DataEnd;
        byte[] buffer = ArrayPool<byte>.Shared.Rent(Math.Min(65536, frame.Metadata.Length));
        try
        {
            for (int offset = 0; offset < frame.Metadata.Length;)
            {
                int count = Math.Min(65536, frame.Metadata.Length - offset);
                f.ReadExactly(buffer.AsSpan(0, count));
                if (!buffer.AsSpan(0, count).SequenceEqual(frame.Metadata.AsSpan(offset, count)))
                    throw Corrupt("Current metadata changed.");
                offset += count;
            }
            if (f.Length != frame.Length) throw Corrupt("Current metadata changed.");
        }
        finally { ArrayPool<byte>.Shared.Return(buffer); }
    }
    private Frame ReadFrame(string path, bool allowPrefix = false)
    {
        using var f = Open(path);
        return ReadFrame(path, f, out _, allowPrefix, addressLimit: 0);
    }
    private Frame ReadFrame(string path, FileStream f, out List<BlockAddress>? addresses, bool allowPrefix = false,
        int addressLimit = MaxBufferedBlockAddresses)
    {
        addresses = addressLimit == 0 ? null : [];
        byte[] buffer = ArrayPool<byte>.Shared.Rent(65536);
        try
        {
            long physicalLength = sealing?.Begin(f) ?? f.Length;
            byte[] header = new byte[HeaderBytes]; f.ReadExactly(header); sealing?.Hash.AppendData(header);
            if (allowPrefix && (!Backup(Path.GetFileName(path)) || Hash(header) != Path.GetFileName(path).Substring(57, 64)))
                throw Corrupt("A retained capsule header differs from its owned name.");
            if (!header.AsSpan(0, 4).SequenceEqual("SJL4"u8) || BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(4)) != 4 || !header.AsSpan(32, 32).SequenceEqual(ownerBytes)) throw Corrupt("A capsule has another owner or format.");
            long id = BinaryPrimitives.ReadInt64LittleEndian(header.AsSpan(8)), data = BinaryPrimitives.ReadInt64LittleEndian(header.AsSpan(16)); int count = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(24));
            long end = checked(HeaderBytes + data), expected = checked(end + count + 32L);
            if (id < 1 || data < 0 || count < 1 || expected > MaxFrame || BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(28)) != 0) throw Corrupt("Capsule bounds are invalid.");
            if (allowPrefix && physicalLength < end)
            {
                // A durable retirement may remove a suffix of metadata-page blocks
                // while leaving the earlier private bytes in place. The immutable
                // header is bound to the owned backup name; validate every retained
                // block's owner, complete boundary and payload checksum afresh.
                if (physicalLength <= HeaderBytes) throw Corrupt("A retained capsule is empty.");
                while (f.Position < physicalLength)
                {
                    byte[] block = new byte[BlockHeaderBytes];
                    if (physicalLength - f.Position < block.Length) throw Corrupt("A retained block header is partial.");
                    long offset = f.Position;
                    f.ReadExactly(block); sealing?.Hash.AppendData(block);
                    int length = BinaryPrimitives.ReadInt32LittleEndian(block.AsSpan(12));
                    long blockId = BinaryPrimitives.ReadInt64LittleEndian(block.AsSpan(4)); bool page = block.AsSpan(0, 4).SequenceEqual("SJM2"u8);
                    BlockHeader(block, blockId, length, page);
                    if (length > physicalLength - f.Position) throw Corrupt("A retained block is partial.");
                    AddAddress(ref addresses, addressLimit, new(blockId, offset, BlockHeaderBytes + length, page));
                    using var payloadHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                    for (int leftBytes = length; leftBytes != 0;)
                    {
                        int n = f.Read(buffer, 0, Math.Min(leftBytes, buffer.Length));
                        if (n == 0) throw Corrupt("A retained block is short.");
                        payloadHash.AppendData(buffer.AsSpan(0, n)); sealing?.Hash.AppendData(buffer.AsSpan(0, n)); leftBytes -= n;
                    }
                    if (!payloadHash.GetHashAndReset().AsSpan().SequenceEqual(block.AsSpan(48, 32))) throw Corrupt("A retained block checksum differs.");
                }
                if (f.Length != physicalLength) throw Corrupt("A retained capsule changed during validation.");
                return new(path, header, new(1, id, Current, [], [], []), [], physicalLength, physicalLength);
            }
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256); hash.AppendData(header); using var dh = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            // Interpret addresses from the exact bytes contributing to the frame
            // checksum, instead of reopening every capsule to reread its headers.
            // Complete-frame hashes still cover all block headers and payloads.
            while (f.Position < end)
            {
                long offset = f.Position;
                if (end - offset < BlockHeaderBytes) throw Corrupt("A block header exceeds its capsule.");
                byte[] block = new byte[BlockHeaderBytes]; f.ReadExactly(block);
                long blockId = BinaryPrimitives.ReadInt64LittleEndian(block.AsSpan(4)); int length = BinaryPrimitives.ReadInt32LittleEndian(block.AsSpan(12));
                bool page = block.AsSpan(0, 4).SequenceEqual("SJM2"u8); BlockHeader(block, blockId, length, page);
                if (length > end - f.Position) throw Corrupt("A block exceeds its capsule.");
                AddAddress(ref addresses, addressLimit, new(blockId, offset, BlockHeaderBytes + length, page));
                hash.AppendData(block); dh.AppendData(block); sealing?.Hash.AppendData(block);
                for (int left = length; left != 0;)
                {
                    int n = f.Read(buffer, 0, Math.Min(left, buffer.Length)); if (n == 0) throw Corrupt("Capsule data is short.");
                    hash.AppendData(buffer.AsSpan(0, n)); dh.AppendData(buffer.AsSpan(0, n)); sealing?.Hash.AppendData(buffer.AsSpan(0, n)); left -= n;
                }
            }
            if (f.Length != physicalLength) throw Corrupt("A capsule changed during validation.");
            if (!dh.GetHashAndReset().AsSpan().SequenceEqual(header.AsSpan(96, 32))) throw Corrupt("Capsule data checksum differs.");
            if (allowPrefix && physicalLength == end && data > 0)
            {
                if (f.Length != physicalLength) throw Corrupt("A capsule changed during validation.");
                return new(path, header, new(1, id, Current, [], [], []), [], end, end);
            }
            if (f.Length != expected) throw Corrupt("A committed capsule footer is incomplete.");
            byte[] m = new byte[count]; f.ReadExactly(m); hash.AppendData(m); byte[] checksum = new byte[32]; f.ReadExactly(checksum); if (!hash.GetHashAndReset().AsSpan().SequenceEqual(checksum)) throw Corrupt("A complete capsule is damaged.");
            sealing?.Hash.AppendData(m); sealing?.Hash.AppendData(checksum);
            if (f.Length != physicalLength) throw Corrupt("A capsule changed during validation.");
            CommitRecord c; try { c = JsonSerializer.Deserialize<CommitRecord>(m, Json) ?? throw Corrupt("A capsule is empty."); } catch (JsonException) { throw Corrupt("Capsule metadata is invalid."); }
            ValidateRoot(c.After); if (c.Version != 1 || c.Generation != id || c.After.IdentityHash != owner || c.After.Objects != Current.Objects || c.Items is null || c.Removes is null || c.Retires is null || c.Items.Select(i => i.Name).Distinct(StringComparer.Ordinal).Count() != c.Items.Length) throw Corrupt("Capsule identity differs.");
            return new(path, header, c, m, end, expected);
        }
        finally { ArrayPool<byte>.Shared.Return(buffer); }
    }
    private static void AddAddress(ref List<BlockAddress>? addresses, int limit, BlockAddress address)
    {
        if (addresses is null) return;
        if (addresses.Count == limit) { addresses = null; return; }
        addresses.Add(address);
    }
    private void Index(Frame frame, List<BlockAddress>? addresses, FileStream? validatedFile = null)
    {
        if (addresses is not null)
        {
            foreach (var block in addresses) SetLocation(block.Page ? PageName(block.Id) : BlockName(block.Id), new(frame.Path, block.Offset, block.Length, null));
        }
        else if (validatedFile is not null) IndexBlocks(frame, validatedFile);
        else { using var file = Open(frame.Path); IndexBlocks(frame, file); }
        foreach (var i in frame.Commit.Items)
        {
            if (!Plain(i.Name) || i.Name == "owner.lock" || i.Length < 0) throw Corrupt("A capsule item is outside its namespace.");
            if (i.Metadata is { } m) { if (i.Length != m.Length || Hash(m) != i.Hash || DataName(i.Name)) throw Corrupt("Metadata checksum differs."); SetLocation(i.Name, new(frame.Path, 0, i.Length, m)); }
            else if (!objects.TryGetValue(i.Name, out var loc) || loc.Path != frame.Path || loc.Offset != i.Offset || loc.Length != i.Length) throw Corrupt("Payload references differ.");
        }
        foreach (string n in frame.Commit.Removes) { if (!Plain(n) || n == "owner.lock") throw Corrupt("A removed name is invalid."); RemoveLocation(n); }
        if (frame.Path == rootPath) Current = frame.Commit.After;
    }
    private void IndexBlocks(Frame frame, FileStream file)
    {
        long at = HeaderBytes;
        while (at < frame.DataEnd) { file.Position = at; byte[] h = new byte[BlockHeaderBytes]; file.ReadExactly(h); long id = BinaryPrimitives.ReadInt64LittleEndian(h.AsSpan(4)); int n = BinaryPrimitives.ReadInt32LittleEndian(h.AsSpan(12)); bool page = h.AsSpan(0, 4).SequenceEqual("SJM2"u8); BlockHeader(h, id, n, page); if (at + BlockHeaderBytes + n > frame.DataEnd) throw Corrupt("A block exceeds its capsule."); SetLocation(page ? PageName(id) : BlockName(id), new(frame.Path, at, BlockHeaderBytes + n, null)); at += BlockHeaderBytes + n; }
    }
    private void SetLocation(string name, Location location)
    {
        if (objects.TryGetValue(name, out var prior) && prior.Path != location.Path)
            RemoveMembership(prior.Path, name);
        objects[name] = location;
        if (!objectsByPath.TryGetValue(location.Path, out var names))
            objectsByPath.Add(location.Path, names = new(StringComparer.Ordinal));
        names.Add(name);
    }
    private void RemoveLocation(string name)
    {
        if (objects.Remove(name, out var prior)) RemoveMembership(prior.Path, name);
    }
    private void RemoveMembership(string path, string name)
    {
        var names = objectsByPath[path];
        names.Remove(name);
        if (names.Count == 0) objectsByPath.Remove(path);
    }
    private KeyValuePair<string, Location>[] Members(string path) =>
        objectsByPath.TryGetValue(path, out var names)
            ? names.Select(name => new KeyValuePair<string, Location>(name, objects[name])).ToArray() : [];
    internal void Commit(Root after, Put[] writes, Remove[] removes, Action<string> boundary)
    {
        boundary("latest.before-storage-prepare");
        ValidateRoot(after); if (head is not null) Footer(head);
        if (after.IdentityHash != owner || after.Objects != Current.Objects || after.Sequence < Current.Sequence || after.Sequence - Current.Sequence > 1) throw Corrupt("The mutation root differs.");
        foreach (var p in writes) if (FileHash(p.Name) != p.BeforeHash) throw Corrupt("A replaced object changed."); foreach (var r in removes) if (FileHash(r.Name) != r.Hash) throw Corrupt("A reclaimed object changed.");
        var puts = writes.ToDictionary(p => p.Name, StringComparer.Ordinal); var removed = removes.Select(r => r.Name).ToHashSet(StringComparer.Ordinal);
        var touched = writes.Select(p => objects.GetValueOrDefault(p.Name)?.Path).Concat(removes.Select(r => objects[r.Name].Path)).Where(p => p is not null).Cast<string>().ToHashSet(StringComparer.Ordinal);
        foreach (string p in touched.ToArray())
        {
            if (!frames.ContainsKey(p)) continue; var entries = Members(p);
            bool mt = entries.Any(v => v.Value.Metadata is not null && (puts.ContainsKey(v.Key) || removed.Contains(v.Key)));
            long cut = entries.Where(v => v.Value.Metadata is null && (puts.ContainsKey(v.Key) || removed.Contains(v.Key)))
                .Select(v => v.Value.Offset).DefaultIfEmpty(long.MaxValue).Min();
            foreach (var v in entries)
                if (!puts.ContainsKey(v.Key) && !removed.Contains(v.Key) &&
                    (v.Value.Metadata is not null ? mt || cut != long.MaxValue : v.Value.Offset >= cut))
                {
                    // Capture and hash the same current bytes. Carried objects
                    // enter after the explicit replacement checks above; their
                    // before hash never justified a second independent read.
                    byte[] bytes = Read(v.Key);
                    puts.Add(v.Key, new(v.Key, Hash(bytes), bytes));
                }
        }
        string oldHead = BackupName(); touched.Add(rootPath); var retires = new List<Retire>();
        foreach (string p in touched)
        {
            string name = p == rootPath ? oldHead : Path.GetFileName(p); var remaining = Members(p).Where(v => !puts.ContainsKey(v.Key) && !removed.Contains(v.Key)).ToArray();
            if (remaining.Any(v => v.Value.Metadata is not null)) continue;
            if (remaining.Length == 0) retires.Add(new(name, HashFile(p), 0, null));
            else if (frames.TryGetValue(p, out var frame))
            {
                long keep = remaining.Max(v => v.Value.Offset + v.Value.Length);
                if (keep < frame.Length)
                {
                    var hashes = HashRetainedFile(p, keep);
                    retires.Add(new(name, hashes.Full, keep, hashes.Prefix));
                }
            }
        }
        boundary("latest.after-retirement-plan");
        long id = checked(generation + 1), offset = HeaderBytes;
        var data = puts.Values.Where(p => DataName(p.Name)).OrderBy(p => p.Name.StartsWith("page-", StringComparison.Ordinal) ? 1 : 0).ToArray(); var items = new List<Item>();
        foreach (var p in data) { items.Add(new(p.Name, p.Bytes.Length, Hash(p.Bytes), offset, null)); offset += p.Bytes.Length; } foreach (var p in puts.Values.Except(data)) items.Add(new(p.Name, p.Bytes.Length, Hash(p.Bytes), 0, p.Bytes));
        var record = new CommitRecord(1, id, after, [.. items], [.. removed], [.. retires]); byte[] meta = JsonSerializer.SerializeToUtf8Bytes(record); long expected = checked(offset + meta.Length + 32);
        if (expected > MaxFrame || committedBytes + expected - retires.Sum(r => new FileInfo(r.Name == oldHead ? rootPath : PathOf(r.Name)).Length - r.KeepBytes) > limit) throw new WorkspaceException("DeltaFull", "The current capsule exceeds its budget; preserve it.");
        byte[] header = new byte[HeaderBytes]; "SJL4"u8.CopyTo(header); BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(4), 4); BinaryPrimitives.WriteInt64LittleEndian(header.AsSpan(8), id); BinaryPrimitives.WriteInt64LittleEndian(header.AsSpan(16), offset - HeaderBytes); BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(24), meta.Length);
        Convert.FromHexString(owner).CopyTo(header, 32); Convert.FromHexString(Hash(Encode(Current, "SJL4"))).CopyTo(header, 64); using (var h = IncrementalHash.CreateHash(HashAlgorithmName.SHA256)) { foreach (var p in data) h.AppendData(p.Bytes); h.GetHashAndReset().CopyTo(header, 96); }
        string pending = PathOf(Pending); if (File.Exists(pending)) throw Corrupt("A pending capsule needs reconnect."); boundary("latest.before-intent");
        using (var f = new FileStream(pending, FileMode.CreateNew, FileAccess.Write, FileShare.Read)) using (var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
        {
            f.Write(header.AsSpan(0, 12)); boundary("latest.after-intent-header"); f.Write(header.AsSpan(12)); hash.AppendData(header);
            foreach (var p in data) { f.Write(p.Bytes.AsSpan(0, BlockHeaderBytes)); hash.AppendData(p.Bytes.AsSpan(0, BlockHeaderBytes)); boundary("latest.after-block-header"); int mid = Math.Max(BlockHeaderBytes, p.Bytes.Length / 2); f.Write(p.Bytes.AsSpan(BlockHeaderBytes, mid - BlockHeaderBytes)); hash.AppendData(p.Bytes.AsSpan(BlockHeaderBytes, mid - BlockHeaderBytes)); boundary("latest.after-block-prefix"); boundary("latest.after-intent-prefix"); f.Write(p.Bytes.AsSpan(mid)); hash.AppendData(p.Bytes.AsSpan(mid)); }
            if (data.Length == 0) { int mid = meta.Length / 2; f.Write(meta.AsSpan(0, mid)); hash.AppendData(meta.AsSpan(0, mid)); boundary("latest.after-intent-prefix"); f.Write(meta.AsSpan(mid)); hash.AppendData(meta.AsSpan(mid)); }
            else { boundary("latest.after-intent-write"); f.Write(meta); hash.AppendData(meta); }
            f.Write(hash.GetHashAndReset()); boundary("latest.before-block-flush"); boundary("latest.before-intent-flush"); long began = System.Diagnostics.Stopwatch.GetTimestamp(); try { f.Flush(true); } finally { FlushObserver?.Invoke(System.Diagnostics.Stopwatch.GetTimestamp() - began); } boundary("latest.after-block-flush"); boundary("latest.after-intent-flush");
        }
        boundary("latest.after-staging"); Publish(pending, oldHead, boundary); Finish(boundary); boundary("latest.after-intent-reclamation");
    }
    private void Publish(string pending, string backup, Action<string> boundary)
    {
        boundary("latest.before-root-publication"); string oldPath = PathOf(backup);
        for (int retry = 0;;retry++) { try { File.Replace(pending, rootPath, oldPath); break; } catch (IOException ex) when (OperatingSystem.IsWindows() && retry < 20 && (ex.HResult & 65535) is 32 or 33 or 1175 && File.Exists(pending)) { Thread.Sleep(50); } }
        boundary("latest.after-object-publication");
        var oldFrame = head; frames.Remove(rootPath);
        if (oldFrame is not null)
        {
            frames[oldPath] = oldFrame with { Path = oldPath };
            foreach (var entry in Members(rootPath)) SetLocation(entry.Key, entry.Value with { Path = oldPath });
        }
        // Validate and index the exact published file through one short-lived
        // handle. On Windows its share mode also prevents a write or namespace
        // replacement between the checksum pass and the block/reference pass.
        // Dispose before reclamation or the next publication; retain every
        // existing capsule and index check without caching payload bytes.
        using (var published = new FileStream(rootPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            head = ReadFrame(rootPath, published, out var addresses);
            boundary("latest.after-root-validation");
            committedBytes += head.Length; generation = head.Commit.Generation; frames[rootPath] = head;
            Index(head, addresses, published);
        }
        boundary("latest.after-root-publication");
    }
    private void Recover(string path)
    {
        byte[] b = ReadShared(path, MaxFrame);
        if (b.Length < HeaderBytes) { int n = Math.Min(4, b.Length); if (!b.AsSpan(0, n).SequenceEqual("SJL4"u8[..n])) throw Corrupt("An incomplete capsule is unsupported."); File.Delete(path); return; }
        if (!b.AsSpan(0, 4).SequenceEqual("SJL4"u8) || BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(4)) != 4 || !b.AsSpan(32, 32).SequenceEqual(Convert.FromHexString(owner)) || !b.AsSpan(64, 32).SequenceEqual(Convert.FromHexString(Hash(Encode(Current, "SJL4")))) || BinaryPrimitives.ReadInt64LittleEndian(b.AsSpan(8)) != generation + 1) throw Corrupt("A pending capsule has another owner or previous state.");
        long data = BinaryPrimitives.ReadInt64LittleEndian(b.AsSpan(16)); int meta = BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(24)); long expected = checked(HeaderBytes + data + meta + 32L); if (data < 0 || meta < 1 || expected > MaxFrame) throw Corrupt("Pending bounds are invalid.");
        if (b.Length < expected) { File.Delete(path); return; } var frame = ReadFrame(path);
        using (var f = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) f.Flush(true);
        // The exact backup name was persisted in the retirement inventory when
        // required. If the old head remains current, it has no retirement entry.
        string backup = frame.Commit.Retires.FirstOrDefault(r => r.Name.StartsWith($"commit-{generation:x16}-", StringComparison.Ordinal))?.Name ?? BackupName();
        Publish(path, backup, _ => { });
    }
    private void Finish(Action<string>? boundary = null)
    {
        if (head is null) return;
        foreach (var r in head.Commit.Retires)
        {
            string p = PathOf(r.Name);
            if (File.Exists(p))
            {
                long priorLength = new FileInfo(p).Length;
                if (r.KeepBytes > 0 && priorLength == r.KeepBytes) { if (HashFile(p) != r.PrefixHash) throw Corrupt("A retained prefix changed."); }
                else { if (HashFile(p) != r.Hash) throw Corrupt("A retired file changed outside its writer."); if (r.KeepBytes == 0) File.Delete(p); else using (var f = new FileStream(p, FileMode.Open, FileAccess.Write, FileShare.Read)) f.SetLength(r.KeepBytes); committedBytes -= priorLength - r.KeepBytes; }
            }
            else if (r.KeepBytes != 0) throw Corrupt("A retained prefix disappeared.");
            if (r.KeepBytes == 0) frames.Remove(p); else if (frames.TryGetValue(p, out var frame)) frames[p] = frame with { Metadata = [], Commit = frame.Commit with { Items = [], Removes = [], Retires = [] }, DataEnd = r.KeepBytes, Length = r.KeepBytes };
            boundary?.Invoke("latest.after-object-reclamation");
        }
    }
    private long Inventory()
    {
        long bytes = Bytes;
        if (objects.Count != Current.FileCount || objects.Values.Sum(l => (long)l.Length) != Current.FileBytes || bytes > limit || !objects.TryGetValue("owner.lock", out var o) || o.Length != 0) throw Corrupt("Current inventory differs.");
        var payloadByPath = new Dictionary<string, long>(StringComparer.Ordinal);
        var referencedPaths = new HashSet<string>(StringComparer.Ordinal);
        foreach (var location in objects.Values)
        {
            referencedPaths.Add(location.Path);
            if (location.Metadata is null)
                payloadByPath[location.Path] = checked(payloadByPath.GetValueOrDefault(location.Path) + location.Length);
        }
        foreach (var frame in frames.Values)
        {
            if (frame.Commit.Items.Any(i => i.Metadata is not null && (!objects.TryGetValue(i.Name, out var l) || l.Path != frame.Path))) throw Corrupt("A capsule retains superseded metadata.");
            if (payloadByPath.GetValueOrDefault(frame.Path) != frame.DataEnd - HeaderBytes) throw Corrupt("A capsule retains superseded payload.");
        }
        foreach (string p in Directory.EnumerateFiles(directory).Where(p => Plain(Path.GetFileName(p)))) if (!referencedPaths.Contains(p)) throw Corrupt("An obsolete seed file remains; reconnect its writer.");
        return bytes;
    }
    internal static SnapshotDeltaSeal Seal(string path, Func<string, IJournalDirectoryGuard>? directoryGuardFactory = null,
        int bufferedBlockAddressLimit = MaxBufferedBlockAddresses)
    {
        path = Path.GetFullPath(path);
        var identity = JsonSerializer.Deserialize<OverlayIdentity>(ReadShared(Path.Combine(Path.GetDirectoryName(path)!, "identity.json"), 1048576)) ?? throw Corrupt("The seal identity is absent.");
        // The proof pins namespace ancestry only. Every file's content, the
        // current head and final inventory still belong to this fresh seal.
        using var guard = directoryGuardFactory?.Invoke(Path.Combine(Path.GetDirectoryName(path)!, ReadRoot(path).Objects));
        // Preserve the composite byte format while hashing the exact bytes
        // already read for capsule validation, in physical-name order.
        using var sealing = new SealHash(path);
        var store = new LatestJournalStorage(path, identity, true, sealing, guard, bufferedBlockAddressLimit);
        using (var current = Open(path))
        {
            byte[] header = new byte[store.head?.Header.Length ?? checked((int)current.Length)]; current.ReadExactly(header);
            if (store.head is not null && !header.AsSpan().SequenceEqual(store.head.Header) || store.head is null && !header.AsSpan().SequenceEqual(Encode(store.Current, "SJL4")))
                throw Corrupt("Current generation changed during seal.");
        }
        if (!sealing.Files.SequenceEqual(Files(path), StringComparer.Ordinal) || File.Exists(Path.Combine(store.directory, Pending))) throw Corrupt("Journal changed during seal.");
        return new(store.Current.Sequence, sealing.Bytes, Convert.ToHexStringLower(sealing.Hash.GetHashAndReset()));
    }
    internal static string[] Files(string path) { if (!IsLatest(path)) return [path]; var r = ReadRoot(path); string d = Path.Combine(Path.GetDirectoryName(path)!, r.Objects); WorkspaceStore.EnsurePlainPath(d); return [path, .. Directory.EnumerateFiles(d).OrderBy(Path.GetFileName, StringComparer.Ordinal)]; }
    internal static WorkspaceException Corrupt(string m) => new("DeltaCorrupt", m + " Preserve current journal storage.");
}
