using System.Buffers;
using System.Security.Cryptography;
using Splinter.Core;
using static Splinter.SourceControl.Files.PlasticRevisionClient;

namespace Splinter.SourceControl.Files;

// One revision proof (at most 4 MiB), one 64 KiB chunk and requested staging.
// Reopening for backward/interleaved reads may retransmit; no disk hydration.
public sealed class PlasticRevisionSnapshotTree : ISnapshotTree, IDisposable
{
    public SnapshotReference Snapshot { get; }
    private readonly PlasticRevisionManifest manifest; private readonly IPlasticRevisionReader client; private readonly Action health;
    private readonly IDisposable? session;
    private readonly IDisposable? metadataLease;
    private readonly Dictionary<string, SnapshotEntry> entries = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, PlasticRevisionEntry> files = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<SnapshotEntry>> children = new(StringComparer.OrdinalIgnoreCase);
    private readonly byte[] chunk = new byte[65536]; private readonly object gate = new();
    private byte[]? proof; private string? proved; private Stream? stream; private long position, chunkOffset = -1; private int chunkLength; private bool disposed;
    public long DecodedRevisionBytes { get; private set; } public int RevisionStreamsStarted { get; private set; }
    public PlasticRevisionSnapshotTree(SnapshotReference snapshot, PlasticRevisionManifest manifest, IPlasticRevisionClient client, Action? health = null, IDisposable? metadataLease = null)
    {
        Snapshot = snapshot; this.manifest = manifest; this.health = health ?? (() => { }); this.metadataLease = metadataLease;
        if (manifest?.Entries is null || manifest.Entries.Length > 32768 || manifest.Entries.Any(e => e is null || string.IsNullOrEmpty(e.Path) || string.IsNullOrEmpty(e.ContentHash)) ||
            manifest.Entries.Sum(e => (long)System.Text.Encoding.UTF8.GetByteCount(e.Path)) > 1024 * 1024)
            throw Failure("PlasticBasisInvalid", "Retain absent, oversized or incomplete recorded entries.");
        long timestamp = snapshot.CapturedAt.UtcDateTime.ToFileTimeUtc();
        Add(new("", true, 0, (uint)FileAttributes.Directory, timestamp, timestamp, timestamp));
        var revisionIdentities = new Dictionary<string, (long Length, string Hash)>();
        foreach (var e in manifest.Entries.OrderBy(e => e.Path.Count(c => c == '/')).ThenBy(e => e.Path, StringComparer.Ordinal))
        {
            if (!ValidRecordedPath(e.Path) || e.Length < 0 || e.Length > 8L * 1024 * 1024 * 1024 || !Numeric(e.Revision) ||
                !ValidHash(e.ContentHash) || e.Directory && e.Length != 0 || !e.Directory && e.Length == 0 && e.ContentHash != "1B2M2Y8AsgTpgAmY7PhCfg==")
                throw Failure("PlasticBasisInvalid", "Retain unsupported recorded source entries.");
            string parent = Parent(e.Path);
            if (!entries.TryGetValue(parent, out var ancestor) || !ancestor.Directory || ancestor.Path != parent)
                throw Failure("PlasticBasisInvalid", "An ambiguous or missing parent cannot be a recorded directory.");
            Add(new(e.Path, e.Directory, e.Length, (uint)(e.Directory ? FileAttributes.Directory : FileAttributes.Archive), e.Timestamp, e.Timestamp, e.Timestamp));
            if (!e.Directory) files.Add(e.Path, e);
            if (!e.Directory)
            {
                if (revisionIdentities.TryGetValue(e.Revision, out var prior) && prior != (e.Length, e.ContentHash))
                    throw Failure("PlasticBasisInvalid", "A revision cannot name different recorded content.");
                revisionIdentities[e.Revision] = (e.Length, e.ContentHash);
            }
        }
        this.client = client.CreateReadSession(); session = ReferenceEquals(client, this.client) ? null : this.client as IDisposable;
    }
    private static string Parent(string path) => path.Contains('/') ? path[..path.LastIndexOf('/')] : "";
    internal static bool ValidRecordedPath(string path) => FileWorkspaceProvider.ValidPath(path) && path.Split('/').All(part =>
        !part.EndsWith(' ') && !part.EndsWith('.') && !part.Any(c => char.IsControl(c) || c is '<' or '>' or '"' or '|' or '?' or '*') &&
        !new[] { "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9" }.Contains(part.Split('.')[0], StringComparer.OrdinalIgnoreCase));
    private static bool ValidHash(string hash)
    { try { var data = Convert.FromBase64String(hash); return data.Length == 16 && Convert.ToBase64String(data) == hash; } catch (FormatException) { return false; } }
    private void Add(SnapshotEntry entry)
    {
        if (!entries.TryAdd(entry.Path, entry)) throw Failure("PlasticBasisInvalid", "Case aliases or duplicate paths are unsupported.");
        if (entry.Directory) children.Add(entry.Path, []);
        if (entry.Path.Length != 0) children[Parent(entry.Path)].Add(entry);
    }
    public void EnsureAvailable() { ObjectDisposedException.ThrowIf(disposed, this); health(); }
    public SnapshotEntry? Stat(string path) { EnsureAvailable(); return entries.GetValueOrDefault(Normalize(path)); }
    public IReadOnlyList<SnapshotEntry> List(string path)
    { EnsureAvailable(); path = Normalize(path); return children.TryGetValue(path, out var list) ? list.ToArray() : throw new DirectoryNotFoundException(path); }
    private static string Normalize(string path)
    {
        path = path.Replace('\\', '/').Trim('/');
        // Runtime probes for absent nested VCS directories are ordinary paths.
        // Only recorded entries must exclude administration at every level.
        if (path.Length != 0 && (Path.IsPathRooted(path) || path.Contains(':') || path.Contains('\0') ||
            path.Split('/').Any(part => part is "" or "." or ".." || part.EndsWith('.') || part.EndsWith(' '))))
            throw Failure("PlasticBasisInvalid", "Unsupported source path.");
        return path;
    }
    public int Read(string path, long offset, Span<byte> destination)
    {
        EnsureAvailable(); path = Normalize(path);
        if (offset < 0 || destination.Length > 1024 * 1024) throw new ArgumentOutOfRangeException(nameof(offset));
        if (!files.TryGetValue(path, out var e)) throw new FileNotFoundException(path);
        int count = offset >= e.Length ? 0 : (int)Math.Min(destination.Length, e.Length - offset); if (count == 0) return 0;
        lock (gate)
        {
            EnsureAvailable(); byte[] staged = ArrayPool<byte>.Shared.Rent(count);
            try
            {
                if (proved != e.Revision)
                {
                    Reset(); byte[] candidate = new byte[checked((int)(((e.Length - 1) / chunk.Length + 1) * 32))];
                    Begin(e); using var hash = IncrementalHash.CreateHash(HashAlgorithmName.MD5);
                    while (position < e.Length)
                    {
                        int n = (int)Math.Min(chunk.Length, e.Length - position); stream!.ReadExactly(chunk.AsSpan(0, n)); DecodedRevisionBytes += n;
                        hash.AppendData(chunk.AsSpan(0, n)); SHA256.HashData(chunk.AsSpan(0, n), candidate.AsSpan((int)(position / chunk.Length) * 32, 32));
                        long start = Math.Max(position, offset), end = Math.Min(position + n, offset + count);
                        if (end > start) chunk.AsSpan((int)(start - position), (int)(end - start)).CopyTo(staged.AsSpan((int)(start - offset)));
                        chunkOffset = position; chunkLength = n; position += n;
                    }
                    Complete();
                    if (!hash.GetHashAndReset().AsSpan().SequenceEqual(Convert.FromBase64String(e.ContentHash)))
                        throw Failure("PlasticBasisInvalid", "Fixed revision content failed its recorded hash; requested bytes remain unpublished.");
                    proof = candidate; proved = e.Revision;
                }
                else
                {
                    for (long at = offset; at < offset + count;)
                    {
                        Load(e, at); int n = (int)Math.Min(offset + count - at, chunkOffset + chunkLength - at);
                        chunk.AsSpan((int)(at - chunkOffset), n).CopyTo(staged.AsSpan((int)(at - offset))); at += n;
                    }
                }
                staged.AsSpan(0, count).CopyTo(destination); return count;
            }
            catch (Exception ex) when (ex is IOException or WorkspaceException or InvalidOperationException)
            { Reset(); throw ex is WorkspaceException ? ex : Failure("PlasticBasisInvalid", "The fixed revision stream is incomplete; preserve the task."); }
            finally { ArrayPool<byte>.Shared.Return(staged); }
        }
    }
    private void Begin(PlasticRevisionEntry e)
    { stream?.Dispose(); stream = client.OpenRevision(manifest.TreeSpec, e.Revision); RevisionStreamsStarted++; position = 0; }
    private void Complete()
    { if (stream!.ReadByte() != -1) throw Failure("PlasticBasisInvalid", "Revision length changed."); stream.Dispose(); stream = null; }
    private void Load(PlasticRevisionEntry e, long at)
    {
        if (at >= chunkOffset && at < chunkOffset + chunkLength) return;
        if (stream is null || at < position) Begin(e);
        Span<byte> digest = stackalloc byte[32];
        do
        {
            int n = (int)Math.Min(chunk.Length, e.Length - position); stream!.ReadExactly(chunk.AsSpan(0, n)); DecodedRevisionBytes += n;
            SHA256.HashData(chunk.AsSpan(0, n), digest);
            if (!digest.SequenceEqual(proof!.AsSpan((int)(position / chunk.Length) * 32, 32))) throw Failure("PlasticBasisInvalid", "Reopened chunk differs from the fully verified revision.");
            chunkOffset = position; chunkLength = n; position += n;
        } while (at >= position);
        if (position == e.Length) Complete();
    }
    private void Reset() { stream?.Dispose(); stream = null; proof = null; proved = null; chunkOffset = -1; chunkLength = 0; position = 0; }
    public void Dispose() { lock (gate) { if (disposed) return; try { Reset(); session?.Dispose(); } finally { metadataLease?.Dispose(); disposed = true; } } }
}
