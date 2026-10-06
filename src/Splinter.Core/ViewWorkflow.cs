using System.Text.Json.Serialization;

namespace Splinter.Core;

// Provider-neutral contracts. Concrete adapters must fence direct writers as well
// as CLI calls; Windows product composition is outside Core and the read-only Host.
public sealed record ViewOwner(string Id, long Generation);
public sealed record ViewWorkerIdentity(string WorkspaceId, ViewOwner Owner,
    SnapshotReference Snapshot, SnapshotDeltaSeal SourceSeal);
public sealed record ViewCacheIdentity(string ViewId, string CacheId, SnapshotReference Snapshot,
    string CompatibilityKey, string PartitionKey, long MaxJournalBytes,
    [property: JsonRequired] bool SnapshotBacked = true);
public sealed record ViewResources(bool OwnershipVerified, int? Applications, int? Writers,
    int? OpenHandles, bool? Mounted);
public sealed record ViewWorkerObservation(ViewWorkerIdentity Worker,
    ViewResources Resources, bool? SnapshotAvailable, string ManagedPathPolicy);
public sealed record SourceViewObservation(ViewOwner Owner, ViewResources Resources);

// Exactly one operation-specific payload is accepted; Runtime and JSON Schema
// both enforce it. IDs are opaque and never interpreted as caller-supplied paths.
public sealed record ViewWorkflowRequest(int Version, string Operation, string ProjectId,
    string RequestKey, ViewOwner Owner)
{
    public ViewWorkerIdentity? Worker { get; init; }
    public string? CandidateId { get; init; }
    public ViewCacheIdentity? Cache { get; init; }
    public string? GenerationId { get; init; }
    public ViewTarget? Target { get; init; }
}

public sealed record ViewTarget(string Kind, string Id);
public sealed record CandidatePublicationIntent(string OperationId, string ProjectId,
    string CandidateId, ViewWorkerIdentity Worker, string ManagedPathPolicy);
public sealed record CandidatePublicationReceipt(CandidatePublicationIntent Intent,
    SnapshotDeltaSeal CandidateSeal, string ReceiptId);
public sealed record ViewSelectionIntent(string OperationId, string ProjectId,
    string GenerationId, ViewOwner Owner, ViewCacheIdentity Cache,
    CandidatePublicationReceipt Candidate);
public sealed record ViewSelectionReceipt(ViewSelectionIntent Intent, string ReceiptId);
// A source-generation artifact includes generated source, not the shared cache.
// A candidate alone still depends on its original snapshot until preserved.
public sealed record ViewArtifact(ViewTarget Target, ViewOwner Owner,
    SnapshotReference Snapshot, SnapshotDeltaSeal Seal);
public sealed record ViewPreservationIntent(string OperationId, string ProjectId,
    ViewArtifact Artifact);
public sealed record ViewIndependentResult(ViewPreservationIntent Intent,
    string Reference, string ContentHash, bool IndependentOfSnapshot);
public sealed record ViewReleaseIntent(string OperationId, string ProjectId,
    ViewArtifact Artifact, ViewIndependentResult Result);

public sealed record ViewWorkflowResponse(int Version, string OperationId, string Operation,
    bool Replayed, CandidatePublicationReceipt? Candidate = null,
    ViewSelectionReceipt? Selection = null,
    ViewIndependentResult? Result = null, ViewTarget? Released = null, string? ClosedViewId = null);

public interface IViewWorkflowProvider
{
    // Verify the current authoritative owner generation, including on retries.
    Task VerifyOwnerAsync(string projectId, string resourceId, ViewOwner owner, CancellationToken ct);
    Task<ViewWorkerObservation> ObserveWorkerAsync(string projectId, string workspaceId, CancellationToken ct);
    // Null means positively absent; unknown/corrupt/unreadable receipts must throw.
    Task<CandidatePublicationReceipt?> FindCandidateAsync(CandidatePublicationIntent intent, CancellationToken ct);
    // Fence writers, recheck the source seal/resources, remove no source data, and
    // publish one immutable source-only receipt. Never silently omit user files.
    Task<CandidatePublicationReceipt> PublishAsync(CandidatePublicationIntent intent, CancellationToken ct);
    Task<SourceViewObservation> ObserveViewAsync(string projectId, string viewId, CancellationToken ct);
    Task<ViewSelectionReceipt?> FindSelectionAsync(ViewSelectionIntent intent, CancellationToken ct);
    // Select offline, preserving old source generations. This is not Editor-ready
    // or project success. Recheck ownership/quiescence within the side effect.
    Task<ViewSelectionReceipt> SelectAsync(ViewSelectionIntent intent, CancellationToken ct);
    Task<SnapshotDeltaSeal> SealSourceAsync(ViewSelectionReceipt selection, CancellationToken ct);
    Task<ViewIndependentResult?> FindResultAsync(ViewPreservationIntent intent, CancellationToken ct);
    // Preserve the exact frozen seal, including application-generated source outputs.
    Task<ViewIndependentResult> PreserveAsync(ViewPreservationIntent intent, CancellationToken ct);
    Task VerifyResultAsync(ViewIndependentResult result, CancellationToken ct);
    // Idempotent for this intent, including a lost response after deletion. Delete
    // only the named private artifact after rechecking its frozen seal, current
    // owner and resources; never a shared cache or source snapshot.
    Task ReleaseAsync(ViewReleaseIntent intent, CancellationToken ct);
    // Explicit cache closure after all dependent source generations were saved
    // and released. Return only after positive absence; retry a lost response
    // through the same owned reclamation receipt. Never release its base here.
    Task CloseCacheAsync(string projectId, ViewCacheIdentity cache, ViewOwner owner, CancellationToken ct);
}
