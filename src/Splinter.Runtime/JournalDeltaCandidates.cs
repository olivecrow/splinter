using System.Text.Json;
using System.Security.Cryptography;
using Splinter.Core;

namespace Splinter.Runtime;

public sealed partial class JournalDeltaStore
{
    public static JournalDeltaStore OpenReadOnly(string directory, OverlayIdentity identity, ISnapshotTree basis) =>
        new(directory, identity, basis, "delta.journal", true);

    public SnapshotDeltaSeal Seal() { using (EnterGate()) { Check(); return SealCurrent(); } }
    // The source is opened read-only, denying an active writer. Only live private
    // payload ranges are copied. Neither enumeration nor hydration of the base occurs.
    public static ValidationCandidate ExportCandidate(string sourceDirectory, OverlayIdentity identity,
        ISnapshotTree snapshot, string destination, string id, string[] forbiddenTopLevel) =>
        ExportCandidateCore(sourceDirectory, identity, snapshot, destination, id, forbiddenTopLevel, null, null);

    public static ValidationCandidate ExportSourceCandidate(string sourceDirectory, OverlayIdentity identity,
        ISnapshotTree snapshot, string destination, string id, string[] forbiddenTopLevel,
        CandidateSourceProjection projection, Action<string>? boundary = null)
    {
        if (projection is null) throw new WorkspaceException("CandidateProjectionInvalid", "Managed-file evidence is required.");
        return ExportCandidateCore(sourceDirectory, identity, snapshot, destination, id, forbiddenTopLevel, projection, boundary);
    }

    private static ValidationCandidate ExportCandidateCore(string sourceDirectory, OverlayIdentity identity,
        ISnapshotTree snapshot, string destination, string id, string[] forbiddenTopLevel,
        CandidateSourceProjection? projection, Action<string>? boundary)
    {
        // One-shot callers deliberately cannot adopt any existing directory.
        // Explicit stable requests use PublishCandidate for reconciliation.
        if (Directory.Exists(destination) || File.Exists(destination))
            throw new WorkspaceException("CandidateAlreadyExists", "Keep the existing candidate or unfinished publication; use a new destination.");
        return PublishCandidate(sourceDirectory, identity, snapshot, destination, new(1, id), forbiddenTopLevel, projection, boundary);
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
        if (candidate.Version != 3 || string.IsNullOrWhiteSpace(candidate.Id) || candidate.Identity is null ||
            candidate.Seal is null || !ValidSeal(candidate.Seal) || candidate.SourceSeal is null || !ValidSeal(candidate.SourceSeal) ||
            candidate.SourceSeal.DurableSequence != candidate.Seal.DurableSequence || candidate.Identity.Snapshot != snapshot.Snapshot)
            throw new WorkspaceException("CandidateIdentityMismatch", "The candidate does not identify this exact snapshot.");
        if (candidate.SourceProjection is { } projection) ValidateProjection(projection, candidate.Identity);
        var store = new JournalDeltaStore(directory, candidate.Identity, snapshot, "delta.journal", true);
        try
        {
            if (store.SealCurrent() != candidate.Seal)
                throw new WorkspaceException("CandidateSealMismatch", "The published journal changed; preserve it.");
            if (candidate.SourceProjection is null && store.pathChanges.Keys.Any(path =>
                store.Identity.ExcludedTopLevel?.Contains(path.Split('/')[0], StringComparer.OrdinalIgnoreCase) == true))
                throw new WorkspaceException("CandidateProjectionMismatch", "Excluded-path history requires the retained projection evidence.");
            if (candidate.SourceProjection is { } proof && proof.Files.Any(file => store.Stat(file.Path) is not null))
                throw new WorkspaceException("CandidateProjectionMismatch", "Projected managed files must be absent from the published candidate.");
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
        string contentIdentity = System.Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new {
            candidate.Identity, candidate.SourceSeal, candidate.Seal, candidate.SourceProjection })));
        Snapshot = basis.Snapshot with { Id = basis.Snapshot.Id + "/candidate/" + contentIdentity,
            Provider = "sealed-range-candidate" };
    }
    public void EnsureAvailable() => basis.EnsureAvailable();
    public SourceChangeView ObserveChanges(string[] managedPaths) => store.ObserveChanges(managedPaths);
    private static SnapshotEntry Convert(DeltaEntry entry) => new(entry.Path, entry.Directory, entry.Length,
        entry.Attributes, entry.CreationTime, entry.LastAccessTime, entry.LastWriteTime);
    public SnapshotEntry? Stat(string path) => store.Stat(path) is { } entry ? Convert(entry) : null;
    public IReadOnlyList<SnapshotEntry> List(string path) => store.List(path).Select(Convert).ToArray();
    public int Read(string path, long offset, Span<byte> destination) =>
        store.Read((store.Stat(path) ?? throw new FileNotFoundException(path)).Id, offset, destination);
    public void Dispose() => store.Dispose();
}
