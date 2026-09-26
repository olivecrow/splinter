using System.Diagnostics;
using System.Text.Json;
using System.Security.Cryptography;
using Splinter.Core;

namespace Splinter.Runtime;

public sealed partial class JournalDeltaStore
{
    // The source is opened read-only, denying an active writer. Only live private
    // payload ranges are copied. Neither enumeration nor hydration of the base occurs.
    public static ValidationCandidate ExportCandidate(string sourceDirectory, OverlayIdentity identity,
        ISnapshotTree snapshot, string destination, string id, string[] forbiddenTopLevel)
    {
        long started = Stopwatch.GetTimestamp();
        if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Candidate id is required.");
        if (Directory.Exists(destination) || File.Exists(destination))
            throw new WorkspaceException("CandidateAlreadyExists", "Keep the existing candidate or unfinished publication; use a new destination.");
        using var source = new JournalDeltaStore(sourceDirectory, identity, snapshot, "delta.journal", true);
        if (source.pathChanges.Keys.Any(p => forbiddenTopLevel.Contains(p.Split('/')[0], StringComparer.OrdinalIgnoreCase)))
            throw new WorkspaceException("CandidateContainsManagedPaths", "This experiment accepts editorless source deltas without managed cache or Git writes.");
        var before = source.SealCurrent();
        string state = source.StateHash();
        var live = source.LiveRanges();
        long estimate = source.WalkFrames(live, null);
        if (estimate > identity.MaxJournalBytes)
            throw new WorkspaceException("DeltaFull", "The sealed candidate exceeds the source's journal budget.");
        Directory.CreateDirectory(destination);
        using (var file = new FileStream(Path.Combine(destination, "identity.json"), FileMode.CreateNew, FileAccess.Write, FileShare.None))
        { JsonSerializer.Serialize(file, identity); file.Flush(true); }
        source.WriteCompactedCandidate(Path.Combine(destination, "delta.journal"), before, live, estimate, new CompactionCostBuilder());
        using var check = new JournalDeltaStore(destination, identity, snapshot, "delta.journal", true);
        if (check.StateHash() != state) throw new WorkspaceException("CandidateStateMismatch", "Preserve both generations; candidate replay differs.");
        var result = new ValidationCandidate(1, id, identity, before, check.SealCurrent(), DateTimeOffset.UtcNow,
            Stopwatch.GetTimestamp() - started, Stopwatch.Frequency);
        string pending = Path.Combine(destination, "candidate.json.new");
        using (var file = new FileStream(pending, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        { JsonSerializer.Serialize(file, result); file.Flush(true); }
        AtomicFilePublication.Publish(pending, Path.Combine(destination, "candidate.json"));
        return result;
    }

    internal static JournalDeltaStore OpenCandidate(string directory, ISnapshotTree snapshot, out ValidationCandidate candidate)
    {
        try
        {
            candidate = JsonSerializer.Deserialize<ValidationCandidate>(File.ReadAllText(Path.Combine(directory, "candidate.json")))
                ?? throw new JsonException();
        }
        catch (Exception ex) when (ex is JsonException or FileNotFoundException)
        { throw new WorkspaceException("CandidateUnpublished", "A complete candidate receipt is required; preserve its files."); }
        if (candidate.Version != 1 || string.IsNullOrWhiteSpace(candidate.Id) || candidate.Identity is null ||
            candidate.Seal is null || !ValidSeal(candidate.Seal) || candidate.SourceSeal is null || !ValidSeal(candidate.SourceSeal) ||
            candidate.SourceSeal.DurableSequence != candidate.Seal.DurableSequence || candidate.Identity.Snapshot != snapshot.Snapshot)
            throw new WorkspaceException("CandidateIdentityMismatch", "The candidate does not identify this exact snapshot.");
        var store = new JournalDeltaStore(directory, candidate.Identity, snapshot, "delta.journal", true);
        try
        {
            if (store.SealCurrent() != candidate.Seal)
                throw new WorkspaceException("CandidateSealMismatch", "The published journal changed; preserve it.");
            return store;
        }
        catch { store.Dispose(); throw; }
    }
}

// Holds a read-only handle throughout use, preventing replacement or mutation.
public sealed class CandidateSnapshotTree : ISnapshotTree, IDisposable
{
    private readonly JournalDeltaStore store;
    private readonly ISnapshotTree basis;
    public ValidationCandidate Candidate { get; }
    public SnapshotReference Snapshot { get; }
    public CandidateSnapshotTree(string directory, ISnapshotTree basis)
    {
        this.basis = basis;
        store = JournalDeltaStore.OpenCandidate(directory, basis, out var candidate);
        Candidate = candidate;
        string contentIdentity = System.Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { candidate.Identity, candidate.Seal })));
        Snapshot = basis.Snapshot with { Id = basis.Snapshot.Id + "/candidate/" + contentIdentity,
            Provider = "sealed-range-candidate" };
    }
    public void EnsureAvailable() => basis.EnsureAvailable();
    private static SnapshotEntry Convert(DeltaEntry entry) => new(entry.Path, entry.Directory, entry.Length,
        entry.Attributes, entry.CreationTime, entry.LastAccessTime, entry.LastWriteTime);
    public SnapshotEntry? Stat(string path) => store.Stat(path) is { } entry ? Convert(entry) : null;
    public IReadOnlyList<SnapshotEntry> List(string path) => store.List(path).Select(Convert).ToArray();
    public int Read(string path, long offset, Span<byte> destination) =>
        store.Read((store.Stat(path) ?? throw new FileNotFoundException(path)).Id, offset, destination);
    public void Dispose() => store.Dispose();
}
