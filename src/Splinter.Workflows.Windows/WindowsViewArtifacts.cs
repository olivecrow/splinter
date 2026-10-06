using System.Security.Cryptography;
using Splinter.Core;
using Splinter.Runtime;
using Splinter.SourceControl.Git;
using static Splinter.Workflows.Windows.ProductState;

namespace Splinter.Workflows.Windows;

internal sealed partial class WindowsViewProvider
{
    internal sealed class ArtifactTree(VssSnapshotTreeOwner owner, CandidateSnapshotTree candidate, JournalDeltaStore? source,
        string[] changes, string baseRevision) : ISnapshotTree, IDisposable
    {
        public SnapshotReference Snapshot => candidate.Snapshot;
        internal string[] Changes => changes;
        internal string BaseRevision => baseRevision;
        internal SnapshotDeltaSeal Seal => source?.Seal() ?? candidate.Candidate.Seal;
        public void EnsureAvailable() => candidate.EnsureAvailable();
        public SnapshotEntry? Stat(string path) => source is null ? candidate.Stat(path) : source.Stat(path) is { } entry ? Convert(entry) : null;
        public IReadOnlyList<SnapshotEntry> List(string path) => source is null ? candidate.List(path) : source.List(path).Select(Convert).ToArray();
        public int Read(string path, long offset, Span<byte> destination) => source is null ? candidate.Read(path, offset, destination) :
            source.Read((source.Stat(path) ?? throw new FileNotFoundException(path)).Id, offset, destination);
        private static SnapshotEntry Convert(DeltaEntry e) => new(e.Path, e.Directory, e.Length, e.Attributes, e.CreationTime, e.LastAccessTime, e.LastWriteTime);
        public void Dispose() { source?.Dispose(); candidate.Dispose(); owner.Dispose(); }
    }
    // Keeps disposal ownership explicit when construction of a layered tree fails.
    internal sealed record VssSnapshotTreeOwner(Splinter.Platform.Windows.VssSnapshotTree Tree) : IDisposable
    { public void Dispose() => Tree.Dispose(); }

    internal async Task<ArtifactTree> OpenArtifact(ViewTarget target, CancellationToken ct)
    {
        ViewGeneration? generation = target.Kind == "source-generation" ? Generation(target.Id) : null;
        string path = CandidatePath(generation?.Selection.Intent.Candidate.Intent.CandidateId ?? target.Id);
        var metadata = Required<ImmutableCandidate>(Path.Combine(path, "candidate.json"), true);
        var worker = Worker(metadata.Identity.Id);
        var basis = await Basis(worker.WorkspaceId, metadata.Identity.Snapshot, ct);
        CandidateSnapshotTree? candidate = null; JournalDeltaStore? source = null;
        try
        {
            candidate = new(path, basis);
            var changes = candidate.ObserveChanges(Managed).Changes.Select(c => c.Path);
            if (generation is not null)
            {
                Require(generation.Selection.Intent.Candidate.CandidateSeal == candidate.Candidate.Seal &&
                    generation.Request.Identity.Snapshot == basis.Snapshot, "ViewReceiptMismatch", "Generation candidate changed.");
                source = JournalDeltaStore.OpenReadOnly(generation.Request.DeltaPath,
                    generation.Request.Identity with { Snapshot = candidate.Snapshot }, candidate);
                changes = changes.Concat(source.ObserveChanges(Managed).Changes.Select(c => c.Path));
            }
            return new(new(basis), candidate, source, changes.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(), worker.BaseRevision);
        }
        catch { source?.Dispose(); candidate?.Dispose(); basis.Dispose(); throw; }
    }
    public async Task<SnapshotDeltaSeal> SealSourceAsync(ViewSelectionReceipt selection, CancellationToken ct)
    {
        Quiescent((await ObserveViewAsync(selection.Intent.ProjectId, selection.Intent.Cache.ViewId, ct)).Resources);
        using var tree = await OpenArtifact(new("source-generation", selection.Intent.GenerationId), ct);
        return tree.Seal;
    }
    internal string ResultPath(string operation) => Path.Combine(Root, "results", WorkspaceStore.Key(operation) + ".json");
    public async Task<ViewIndependentResult?> FindResultAsync(ViewPreservationIntent intent, CancellationToken ct)
    {
        var result = Read<ViewIndependentResult>(ResultPath(intent.OperationId));
        if (result is null) return null;
        Require(result.Intent == intent, "ViewReceiptMismatch", "Independent result intent differs.");
        await VerifyResultAsync(result, ct); return result;
    }
    public async Task<ViewIndependentResult> PreserveAsync(ViewPreservationIntent intent, CancellationToken ct)
    {
        using var tree = await OpenArtifact(intent.Artifact.Target, ct);
        Require(tree.Seal == intent.Artifact.Seal, "ViewSourceChanged", "The frozen artifact changed before preservation.");
        var saved = await GitSourceResult.PreserveAsync(project, tree.BaseRevision, intent.OperationId, tree, tree.Changes, Managed, ct);
        Require(tree.Seal == intent.Artifact.Seal, "ViewSourceChanged", "The frozen artifact changed during preservation.");
        var result = new ViewIndependentResult(intent, saved.Reference, saved.Hash, true);
        Save(ResultPath(intent.OperationId), result); return result;
    }
    public async Task VerifyResultAsync(ViewIndependentResult result, CancellationToken ct)
    {
        Require(result.Intent.ProjectId == ProjectId && result.IndependentOfSnapshot &&
            await GitSourceResult.VerifyAsync(project, result.Reference, ct) == result.ContentHash,
            "ViewResultInvalid", "The exact independent result must remain readable without VSS.");
    }
    public async Task ReleaseAsync(ViewReleaseIntent intent, CancellationToken ct)
    {
        await VerifyResultAsync(intent.Result, ct);
        bool candidate = intent.Artifact.Target.Kind == "candidate";
        string directory = candidate ? CandidatePath(intent.Artifact.Target.Id) : Generation(intent.Artifact.Target.Id).Request.DeltaPath;
        string receiptPath = Path.Combine(Root, "releases", WorkspaceStore.Key(intent.Artifact.Target.Kind + ":" + intent.Artifact.Target.Id) + ".json");
        var prior = Read<ViewReleaseIntent>(receiptPath);
        Require(prior is null || prior.Artifact == intent.Artifact && prior.Result == intent.Result,
            "ViewReceiptMismatch", "Release must retain the same frozen artifact and result.");
        if (File.Exists(Path.Combine(directory, "delta.journal")))
        {
            using (var tree = await OpenArtifact(intent.Artifact.Target, ct))
                Require(tree.Seal == intent.Artifact.Seal, "ViewSourceChanged", "The frozen artifact changed before release.");
        }
        if (prior is null) Save(receiptPath, intent);
        JournalReclamation.Reclaim(directory, intent.Artifact.Seal,
            candidate ? ["candidate.json", "candidate-export.json", "identity.json"] : ["identity.json"], receiptPath + ".journal.json");
    }
}
