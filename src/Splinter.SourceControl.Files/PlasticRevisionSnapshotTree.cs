using System.Buffers;
using System.Security.Cryptography;
using Splinter.Core;
using Splinter.Runtime;
using static Splinter.SourceControl.Files.PlasticRevisionClient;

namespace Splinter.SourceControl.Files;

// One disk-backed revision proof, one 64 KiB chunk and requested staging.
// Reopening for backward/interleaved reads may retransmit; no disk hydration.
public sealed class PlasticRevisionSnapshotTree : ISnapshotTree, ISnapshotAvailabilityScope, IDisposable
{
    public SnapshotReference Snapshot { get; }
    private readonly PlasticRevisionManifest manifest; private readonly IPlasticRevisionReader client; private readonly Action health;
    private readonly IDisposable? session;
    private readonly IDisposable? metadataLease;
    private readonly PagedSnapshotCatalog catalog;
    private readonly PlasticManifestObservationScope? observationScope;
    private readonly Action? verifyBoundary;
    private readonly TemporaryMetadataDirectory? scratch;
    private readonly byte[] chunk = new byte[65536]; private readonly object gate = new();
    private FileStream? proof; private string? proved; private Stream? stream; private long position, chunkOffset = -1; private int chunkLength; private bool disposed;
    public long DecodedRevisionBytes { get; private set; } public int RevisionStreamsStarted { get; private set; }
    public long MetadataFenceCount => disposed || observationScope is null ? 0 : checked(observationScope.HandleCount + 2);
    public long MetadataFenceBytes => disposed ? 0 : observationScope?.Bytes ?? 0;
    public PlasticRevisionSnapshotTree(SnapshotReference snapshot, PlasticRevisionManifest manifest, IPlasticRevisionClient client, Action? health = null, IDisposable? metadataLease = null)
        : this(snapshot, manifest, client, health, metadataLease, null, null) { }
    internal PlasticRevisionSnapshotTree(SnapshotReference snapshot, PlasticRevisionManifest manifest, IPlasticRevisionClient client, Action? health,
        IDisposable? metadataLease, PlasticManifestObservationScope? observationScope, Action? verifyBoundary)
    {
        Snapshot = snapshot; this.manifest = manifest; this.health = health ?? (() => { }); this.metadataLease = metadataLease;
        this.observationScope = observationScope; this.verifyBoundary = verifyBoundary;
        if (manifest is null || manifest.Version != 2) throw Failure("PlasticBasisInvalid", "Retain unsupported recorded manifest metadata.");
        try
        {
            if (manifest.Catalog is null)
            {
                scratch = new(); manifest = manifest.WriteCatalog(scratch.Path);
            }
            catalog = manifest.OpenCatalog();
            this.client = client.CreateReadSession(); session = ReferenceEquals(client, this.client) ? null : this.client as IDisposable;
        }
        catch (WorkspaceException ex)
        { try { scratch?.Dispose(); } catch (IOException) { } throw Failure("PlasticBasisInvalid", ex.Message); }
    }
    private static string Parent(string path) => path.Contains('/') ? path[..path.LastIndexOf('/')] : "";
    internal static bool ValidRecordedPath(string path) => FileWorkspaceProvider.ValidPath(path) && path.Split('/').All(part =>
        !part.EndsWith(' ') && !part.EndsWith('.') && !part.Any(c => char.IsControl(c) || c is '<' or '>' or '"' or '|' or '?' or '*') &&
        !new[] { "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9" }.Contains(part.Split('.')[0], StringComparer.OrdinalIgnoreCase));
    private static bool ValidHash(string hash)
    { try { var data = Convert.FromBase64String(hash); return data.Length == 16 && Convert.ToBase64String(data) == hash; } catch (FormatException) { return false; } }
    public void EnsureAvailable() { ObjectDisposedException.ThrowIf(disposed, this); health(); }
    public SnapshotObservation? ObserveAvailability(SnapshotReadAccess expectedAccess)
    { ObjectDisposedException.ThrowIf(disposed, this); return observationScope?.Observe(expectedAccess); }
    public void VerifyReadBoundary() { EnsureAvailable(); if (verifyBoundary is not null) verifyBoundary(); }
    public SnapshotEntry? Stat(string path) { EnsureAvailable(); return catalog.Find(Normalize(path))?.Entry; }
    public IReadOnlyList<SnapshotEntry> List(string path)
    { return EnumerateChildren(path).ToArray(); }
    public IEnumerable<SnapshotEntry> EnumerateChildren(string path)
    { EnsureAvailable(); return catalog.EnumerateChildren(Normalize(path)).Select(e => e.Entry); }
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
        var record = catalog.Find(path);
        if (record is null || record.Entry.Directory) throw new FileNotFoundException(path);
        var e = PlasticRevisionManifest.FromRecord(record);
        int count = offset >= e.Length ? 0 : (int)Math.Min(destination.Length, e.Length - offset); if (count == 0) return 0;
        lock (gate)
        {
            EnsureAvailable(); byte[] staged = ArrayPool<byte>.Shared.Rent(count);
            try
            {
                if (proved != e.Revision)
                {
                    Reset(); FileStream? candidate = new(Path.Combine(Path.GetTempPath(), "splinter-revision-proof-" + Guid.NewGuid().ToString("N") + ".bin"),
                        FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 65536, FileOptions.RandomAccess | FileOptions.DeleteOnClose);
                    try
                    {
                    Begin(e); using var hash = IncrementalHash.CreateHash(HashAlgorithmName.MD5);
                    byte[] digest = new byte[32];
                    while (position < e.Length)
                    {
                        int n = (int)Math.Min(chunk.Length, e.Length - position); stream!.ReadExactly(chunk.AsSpan(0, n)); DecodedRevisionBytes += n;
                        hash.AppendData(chunk.AsSpan(0, n)); SHA256.HashData(chunk.AsSpan(0, n), digest); candidate.Write(digest);
                        long start = Math.Max(position, offset), end = Math.Min(position + n, offset + count);
                        if (end > start) chunk.AsSpan((int)(start - position), (int)(end - start)).CopyTo(staged.AsSpan((int)(start - offset)));
                        chunkOffset = position; chunkLength = n; position += n;
                    }
                    Complete();
                    if (!hash.GetHashAndReset().AsSpan().SequenceEqual(Convert.FromBase64String(e.ContentHash)))
                        throw Failure("PlasticBasisInvalid", "Fixed revision content failed its recorded hash; requested bytes remain unpublished.");
                    candidate.Flush(true); proof = candidate; candidate = null; proved = e.Revision;
                    }
                    finally { candidate?.Dispose(); }
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
    {
        stream?.Dispose();
        if (client is IPlasticRevisionBatchReader batch && e.Length <= 4 * 1024 * 1024 && !batch.HasPreparedRevision(e.Revision))
        {
            var requests = new List<PlasticRevisionReadRequest>(64) { new(e.Revision, e.Length, e.ContentHash) };
            var revisions = new HashSet<string>(StringComparer.Ordinal) { e.Revision }; long bytes = e.Length; int visited = 0;
            // Seek after this entry, never scan the full catalog looking for a
            // particular batch size. Large files retain segmented streaming.
            foreach (var next in catalog.Walk(e.Path))
            {
                if (++visited > 64 || requests.Count == 64) break;
                var item = PlasticRevisionManifest.FromRecord(next);
                if (item.Directory || item.Length == 0 || item.Length > 4 * 1024 * 1024 - bytes || !revisions.Add(item.Revision)) continue;
                requests.Add(new(item.Revision, item.Length, item.ContentHash)); bytes += item.Length;
            }
            batch.PrepareRevisions(manifest.TreeSpec, requests);
        }
        stream = client.OpenRevision(manifest.TreeSpec, e.Revision); RevisionStreamsStarted++; position = 0;
    }
    private void Complete()
    { if (stream!.ReadByte() != -1) throw Failure("PlasticBasisInvalid", "Revision length changed."); stream.Dispose(); stream = null; }
    private void Load(PlasticRevisionEntry e, long at)
    {
        if (at >= chunkOffset && at < chunkOffset + chunkLength) return;
        if (stream is null || at < position) Begin(e);
        Span<byte> digest = stackalloc byte[32], expected = stackalloc byte[32];
        do
        {
            int n = (int)Math.Min(chunk.Length, e.Length - position); stream!.ReadExactly(chunk.AsSpan(0, n)); DecodedRevisionBytes += n;
            SHA256.HashData(chunk.AsSpan(0, n), digest);
            long proofOffset = checked(position / chunk.Length * 32); int proofRead = 0;
            while (proofRead < expected.Length)
            {
                int read = RandomAccess.Read(proof!.SafeFileHandle, expected[proofRead..], proofOffset + proofRead);
                if (read == 0) throw Failure("PlasticBasisInvalid", "The owned revision proof is truncated.");
                proofRead += read;
            }
            if (!digest.SequenceEqual(expected)) throw Failure("PlasticBasisInvalid", "Reopened chunk differs from the fully verified revision.");
            chunkOffset = position; chunkLength = n; position += n;
        } while (at >= position);
        if (position == e.Length) Complete();
    }
    private void Reset() { stream?.Dispose(); stream = null; proof?.Dispose(); proof = null; proved = null; chunkOffset = -1; chunkLength = 0; position = 0; }
    public void Dispose()
    {
        lock (gate)
        {
            if (disposed) return;
            try { Reset(); session?.Dispose(); }
            finally { try { catalog.Dispose(); scratch?.Dispose(); } finally { try { observationScope?.Dispose(); } finally { metadataLease?.Dispose(); disposed = true; } } }
        }
    }
}
