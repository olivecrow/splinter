using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Splinter.Core;

namespace Splinter.Runtime;

// Current-overlay segmented storage. Rolling seals one append unit and publishes
// frozen index roots without copying any earlier private payload. Old bounded
// prefix fixtures remain isolated in their historical overlay contract.
public sealed record JournalIndexRoots(string Directory, MetadataTreeRoot Nodes, MetadataTreeRoot Paths,
    MetadataTreeRoot Hidden, MetadataTreeRoot Changes, MetadataTreeRoot Names, MetadataTreeRoot DurableNodes,
    MetadataTreeRoot Segments, long Sequence, long PayloadBytes, long DecodedPayloadBytes, long ExtentCount,
    MetadataTreeRoot? Adoptions = null, string? SourceMutationHash = null, long SourceMutationBytes = 0,
    MetadataTreeRoot? Captures = null, MetadataTreeRoot? Packing = null, long NextDataSegment = -2,
    long DataSegmentBytes = 0, long DataPayloadBytes = 0);
public sealed record JournalSegment(long Id, long Length, string Hash, string? NativeIdentity, long PayloadBytes = 0);
public sealed record JournalSegmentHead(long Id, string Nonce, string? NativeIdentity);
public sealed record JournalSegmentState(int Version, string OwnerHash, JournalSegmentHead Head, JournalIndexRoots Roots,
    long SealedBytes, long SealedCount, long SealedPayloadBytes = 0, string Digest = "");

public sealed class JournalSegmentStorage : IDisposable
{
    public const int HeaderBytes = 92;
    public const long DefaultAppendBytes = 16L * 1024 * 1024;
    private const string StateName = "segment-state.json", IntentName = "segment-roll.json", GuardName = "delta.journal.bounded.lock";
    private sealed record RollIntent(int Version, JournalSegmentState Before, JournalSegment Sealed, JournalSegmentHead Next,
        JournalIndexRoots Roots, long StartedAtUtcTicks, long RetiredBytes = 0, long RetiredCount = 0, long RetiredPayloadBytes = 0, string Digest = "");
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    { RespectRequiredConstructorParameters = true, RespectNullableAnnotations = true, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
    private readonly string directory;
    private readonly string owner;
    private readonly IJournalFilePins? pins;
    private readonly FileStream lease;
    private readonly Action<string>? boundary;
    private FileStream? active;
    private IncrementalHash? activeHash;
    private long hashedBytes;
    private long activePayloadBytes;
    private readonly bool readOnly;
    private bool disposed;
    public JournalSegmentState State { get; private set; } = null!;
    public FileStream Active => active ?? throw new ObjectDisposedException(nameof(JournalSegmentStorage));
    public long ActiveId => State.Head.Id;
    public string ActivePath => Path.Combine(directory, "delta.journal");
    public long JournalBytes => checked(State.SealedBytes + Active.Length);
    public long PayloadBytes => checked(State.SealedPayloadBytes + activePayloadBytes);
    public void SetActivePayloadBytes(long value) { if (value < 0 || value > Active.Length) throw Invalid(); activePayloadBytes = value; }
    public static string FileName(long id) => id >= 0 ? "segment-" + id.ToString("x16") + ".sjr" : id <= -2 ?
        "packed-" + (-(id + 2)).ToString("x16") + ".sjr" : throw Invalid();
    public string SegmentPath(long id) => Path.Combine(directory, FileName(id));
    private static WorkspaceException Invalid(string message = "Preserve a damaged or foreign journal segment generation.") => new("JournalSegmentCorrupt", message);

    public JournalSegmentStorage(string directory, OverlayIdentity identity, JournalIndexRoots? initialRoots = null,
        IJournalFilePins? pins = null, Action<string>? boundary = null, bool readOnly = false)
    {
        if (identity.Version != 3) throw new WorkspaceException("OverlayFormatUnsupported", "Segmented journals require the current overlay identity.");
        this.directory = Path.GetFullPath(directory); this.pins = pins; this.boundary = boundary; this.readOnly = readOnly;
        owner = Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(identity)));
        WorkspaceStore.EnsurePlainPath(this.directory); Directory.CreateDirectory(this.directory);
        string guard = Path.Combine(this.directory, GuardName); WorkspaceStore.EnsurePlainPath(guard);
        lease = new(guard, readOnly ? FileMode.Open : FileMode.OpenOrCreate, readOnly ? FileAccess.Read : FileAccess.ReadWrite,
            readOnly ? FileShare.ReadWrite : FileShare.Read, 1);
        try
        {
            if (lease.Length != 0) throw Invalid();
            string statePath = Path.Combine(this.directory, StateName);
            if (!File.Exists(statePath))
            {
                if (readOnly) throw Invalid("Recover the exact writable segmented journal before opening it read-only.");
                string initialization = Path.Combine(this.directory, IntentName);
                if (File.Exists(initialization))
                {
                    var saved = Read<RollIntent>(initialization); State = saved.Before;
                    if (saved.Sealed.Id != -1) throw Invalid(); CompleteRoll(saved, initializing: true);
                }
                else
                {
                if (initialRoots is null || File.Exists(ActivePath))
                    throw new WorkspaceException("OverlayFormatUnsupported", "Preserve an older or unfinished non-segmented journal without migration.");
                ValidateRoots(initialRoots);
                var head = new JournalSegmentHead(0, new Guid(Convert.FromHexString(owner).AsSpan(0, 16)).ToString("N"), null);
                State = Sign(new JournalSegmentState(2, owner, head, initialRoots, 0, 0));
                // Initialization is an ordinary recoverable roll from no head.
                var intent = Sign(new RollIntent(2, State, new(-1, 0, EmptyHash, null), head, initialRoots, DateTime.UtcNow.Ticks));
                Publish(Path.Combine(this.directory, IntentName), intent);
                CompleteRoll(intent, initializing: true);
                }
            }
            else
            {
                State = Read<JournalSegmentState>(statePath); ValidateState(State);
                string intentPath = Path.Combine(this.directory, IntentName);
                if (File.Exists(intentPath))
                { if (readOnly) throw Invalid("Recover the pending segmented roll first."); var intent = Read<RollIntent>(intentPath); CompleteRoll(intent, initializing: intent.Sealed.Id == -1); }
            }
            OpenActive();
        }
        catch { active?.Dispose(); lease.Dispose(); throw; }
    }
    public void Rotate(Func<JournalSegment, JournalIndexRoots> freezeRoots, long retiredBytes = 0, long retiredCount = 0, long retiredPayloadBytes = 0)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (readOnly) throw Invalid("An immutable journal cannot rotate.");
        if (File.Exists(Path.Combine(directory, IntentName))) throw Invalid("Recover the exact pending segment roll before accepting more writes.");
        Active.Flush(true); long length = Active.Length;
        if (hashedBytes != length) throw Invalid("The append hash does not cover the complete accepted head.");
        var segment = new JournalSegment(ActiveId, length, Convert.ToHexStringLower(activeHash!.GetCurrentHash()), pins?.IdentifyOpened(Active), activePayloadBytes);
        var roots = freezeRoots(segment); ValidateRoots(roots);
        var next = new JournalSegmentHead(checked(ActiveId + 1), Guid.NewGuid().ToString("N"), null);
        if (retiredBytes < 0 || retiredBytes > State.SealedBytes || retiredCount < 0 || retiredCount > State.SealedCount) throw Invalid();
        var intent = Sign(new RollIntent(2, State, segment, next, roots, DateTime.UtcNow.Ticks, retiredBytes, retiredCount, retiredPayloadBytes));
        Publish(Path.Combine(directory, IntentName), intent); boundary?.Invoke("segment.intent-published");
        active!.Dispose(); active = null;
        CompleteRoll(intent, initializing: false, trustedCurrent: true); OpenActive();
    }
    public void RecordAppend(ReadOnlySpan<byte> header, ReadOnlySpan<byte> metadata, ReadOnlySpan<byte> payload, ReadOnlySpan<byte> checksum)
    {
        if (readOnly || hashedBytes != Active.Length - header.Length - metadata.Length - payload.Length - checksum.Length) throw Invalid();
        activeHash!.AppendData(header); activeHash.AppendData(metadata); activeHash.AppendData(payload); activeHash.AppendData(checksum);
        hashedBytes = Active.Length;
        activePayloadBytes = checked(activePayloadBytes + payload.Length);
    }
    public void ResetActiveHash()
    {
        activeHash?.Dispose(); activeHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256); byte[] buffer = new byte[65536];
        for (long at = 0; at < Active.Length;)
        { int count = RandomAccess.Read(Active.SafeFileHandle, buffer.AsSpan(0, (int)Math.Min(buffer.Length, Active.Length - at)), at); if (count <= 0) throw Invalid(); activeHash.AppendData(buffer.AsSpan(0, count)); at += count; }
        hashedBytes = Active.Length;
    }
    public string ActiveHash => hashedBytes == Active.Length ? Convert.ToHexStringLower(activeHash!.GetCurrentHash()) : throw Invalid();
    private void CompleteRoll(RollIntent intent, bool initializing, bool trustedCurrent = false)
    {
        ValidateState(intent.Before);
        if (intent.Version != 2 || intent.Digest != JournalControlIntegrity.Digest(intent with { Digest = "" }) ||
            intent.Before.OwnerHash != owner || intent.Before.Version != 2 || intent.StartedAtUtcTicks <= 0 ||
            intent.RetiredBytes < 0 || intent.RetiredBytes > intent.Before.SealedBytes || intent.RetiredCount < 0 || intent.RetiredCount > intent.Before.SealedCount ||
            intent.RetiredPayloadBytes < 0 || intent.RetiredPayloadBytes > intent.Before.SealedPayloadBytes ||
            !Guid.TryParseExact(intent.Next.Nonce, "N", out _) || intent.Next.NativeIdentity is not null ||
            (initializing ? intent.Sealed.Id != -1 || intent.Next.Id != 0 : intent.Sealed.Id != intent.Before.Head.Id || intent.Next.Id != checked(intent.Sealed.Id + 1))) throw Invalid();
        ValidateRoots(intent.Roots);
        if (!initializing)
        {
            string sealedPath = SegmentPath(intent.Sealed.Id); WorkspaceStore.EnsurePlainPath(sealedPath);
            if (!File.Exists(sealedPath))
            {
                if (trustedCurrent)
                { if (new FileInfo(ActivePath).Length != intent.Sealed.Length || pins is not null && pins.Identify(ActivePath) != intent.Sealed.NativeIdentity) throw Invalid(); }
                else RequireSegment(ActivePath, intent.Sealed);
                File.Move(ActivePath, sealedPath); boundary?.Invoke("segment.head-renamed");
            }
            else RequireSegment(sealedPath, intent.Sealed);
        }
        byte[] header = Header(intent.Next);
        if (!File.Exists(ActivePath))
        {
            using var created = new FileStream(ActivePath, FileMode.CreateNew, FileAccess.Write, FileShare.Read | FileShare.Delete);
            created.Write(header); created.Flush(true); boundary?.Invoke("segment.new-head-durable");
        }
        else
        {
            using var existing = new FileStream(ActivePath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read | FileShare.Delete);
            // Before state publication this generation cannot have accepted a
            // frame. Match every surviving initialization byte, not a filename.
            if (existing.Length > HeaderBytes) throw Invalid();
            byte[] prefix = new byte[checked((int)existing.Length)]; existing.ReadExactly(prefix);
            if (!prefix.AsSpan().SequenceEqual(header.AsSpan(0, prefix.Length))) throw Invalid();
            existing.Position = prefix.Length; existing.Write(header.AsSpan(prefix.Length)); existing.Flush(true);
        }
        var head = intent.Next with { NativeIdentity = pins?.Identify(ActivePath) };
        var after = Sign(new JournalSegmentState(2, owner, head, intent.Roots,
            initializing ? 0 : checked(intent.Before.SealedBytes + intent.Sealed.Length - intent.RetiredBytes),
            initializing ? 0 : checked(intent.Before.SealedCount + 1 - intent.RetiredCount),
            initializing ? 0 : checked(intent.Before.SealedPayloadBytes + intent.Sealed.PayloadBytes - intent.RetiredPayloadBytes)));
        ValidateState(after);
        string statePath = Path.Combine(directory, StateName);
        if (File.Exists(statePath))
        {
            var saved = Read<JournalSegmentState>(statePath);
            if (!Same(saved, intent.Before) && !Same(saved, after)) throw Invalid();
        }
        Publish(statePath, after); State = after; boundary?.Invoke("segment.state-published");
        string path = Path.Combine(directory, IntentName);
        if (!Same(Read<RollIntent>(path), intent)) throw Invalid();
        File.Delete(path); boundary?.Invoke("segment.intent-removed");
    }
    private void OpenActive()
    {
        ValidateState(State); WorkspaceStore.EnsurePlainPath(ActivePath);
        active = new(ActivePath, FileMode.Open, readOnly ? FileAccess.Read : FileAccess.ReadWrite,
            readOnly ? FileShare.ReadWrite | FileShare.Delete : FileShare.Read | FileShare.Delete, 4096, FileOptions.RandomAccess);
        try
        {
            byte[] header = new byte[HeaderBytes]; active.ReadExactly(header);
            if (!header.AsSpan().SequenceEqual(Header(State.Head)) || pins is not null && pins.Identify(ActivePath) != State.Head.NativeIdentity) throw Invalid();
            active.Position = active.Length;
            activePayloadBytes = 0;
            ResetActiveHash();
        }
        catch { active.Dispose(); active = null; throw; }
    }
    public FileStream OpenSealed(JournalSegment segment)
    {
        ObjectDisposedException.ThrowIf(disposed, this); string path = SegmentPath(segment.Id);
        WorkspaceStore.EnsurePlainPath(path); var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1, FileOptions.RandomAccess);
        try
        {
            if (input.Length != segment.Length || pins is not null && pins.Identify(path) != segment.NativeIdentity) throw Invalid();
            if (Convert.ToHexStringLower(SHA256.HashData(input)) != segment.Hash) throw Invalid();
            input.Position = 0; return input;
        }
        catch { input.Dispose(); throw; }
    }
    private void RequireSegment(string path, JournalSegment segment)
    {
        if (segment.Id == -1 || segment.Length < (segment.Id >= 0 ? HeaderBytes : 1) || segment.Hash is not { Length: 64 } ||
            pins is not null && pins.Identify(path) != segment.NativeIdentity || HashFile(path, segment.Length) != segment.Hash) throw Invalid();
    }
    private static string HashFile(string path, long length)
    {
        WorkspaceStore.EnsurePlainPath(path); using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        if (file.Length != length) throw Invalid(); string hash = Convert.ToHexStringLower(SHA256.HashData(file));
        if (file.Length != length) throw Invalid(); return hash;
    }
    private static string EmptyHash => Convert.ToHexStringLower(SHA256.HashData(Array.Empty<byte>()));
    private byte[] Header(JournalSegmentHead head)
    {
        byte[] bytes = new byte[HeaderBytes]; "SJS1"u8.CopyTo(bytes); BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(4), head.Id);
        Convert.FromHexString(owner).CopyTo(bytes, 12); Guid.ParseExact(head.Nonce, "N").TryWriteBytes(bytes.AsSpan(44, 16));
        SHA256.HashData(bytes.AsSpan(0, 60)).CopyTo(bytes.AsSpan(60)); return bytes;
    }
    private void ValidateState(JournalSegmentState state)
    {
        if (state.Version != 2 || state.Digest != JournalControlIntegrity.Digest(state with { Digest = "" }) ||
            state.OwnerHash != owner || state.Head.Id < 0 || state.SealedBytes < 0 || state.SealedCount < 0 || state.SealedPayloadBytes < 0 || state.SealedPayloadBytes > state.SealedBytes ||
            !Guid.TryParseExact(state.Head.Nonce, "N", out _)) throw Invalid(); ValidateRoots(state.Roots);
    }
    private void ValidateRoots(JournalIndexRoots roots)
    {
        if (roots.Sequence < 0 || roots.PayloadBytes < 0 || roots.DecodedPayloadBytes < roots.PayloadBytes || roots.ExtentCount < 0 ||
            !WorkspaceStore.SamePath(Path.GetDirectoryName(Path.GetFullPath(roots.Directory))!, directory)) throw Invalid();
        using var store = new PagedMetadataStore(roots.Directory);
        foreach (var root in new[] { roots.Nodes, roots.Paths, roots.Hidden, roots.Changes, roots.Names, roots.DurableNodes, roots.Segments }) _ = store.Open(root);
        if (roots.Adoptions is { } adoptions) _ = store.Open(adoptions);
        if (roots.Captures is { } captures) _ = store.Open(captures);
        if (roots.Packing is { } packing) _ = store.Open(packing);
        if (roots.NextDataSegment > -2 || roots.DataSegmentBytes < 0 || roots.DataPayloadBytes < 0 || roots.DataPayloadBytes > roots.DataSegmentBytes) throw Invalid();
    }
    private static T Read<T>(string path)
    {
        return JournalControlIntegrity.Read<T>(path);
    }
    private static JournalSegmentState Sign(JournalSegmentState value) => value with { Digest = JournalControlIntegrity.Digest(value with { Digest = "" }) };
    private static RollIntent Sign(RollIntent value) => value with { Digest = JournalControlIntegrity.Digest(value with { Digest = "" }) };
    private static bool Same<T>(T a, T b) => JsonSerializer.Serialize(a, Json) == JsonSerializer.Serialize(b, Json);
    private static void Publish<T>(string path, T value)
    {
        WorkspaceStore.EnsurePlainPath(path); string pending = path + ".pending"; WorkspaceStore.EnsurePlainPath(pending);
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(value, Json);
        if (File.Exists(pending))
        {
            byte[] prefix = File.ReadAllBytes(pending);
            if (prefix.Length > bytes.Length || !prefix.AsSpan().SequenceEqual(bytes.AsSpan(0, prefix.Length))) throw Invalid();
            using var output = new FileStream(pending, FileMode.Open, FileAccess.Write, FileShare.Read); output.Position = prefix.Length;
            output.Write(bytes.AsSpan(prefix.Length)); output.Flush(true);
        }
        else { using var output = new FileStream(pending, FileMode.CreateNew, FileAccess.Write, FileShare.Read); output.Write(bytes); output.Flush(true); }
        AtomicFilePublication.Publish(pending, path);
    }
    public void Dispose()
    {
        if (disposed) return; disposed = true;
        try { active?.Dispose(); activeHash?.Dispose(); } finally { active = null; lease.Dispose(); }
    }
}
