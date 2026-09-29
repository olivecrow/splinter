using System.Text.Json.Serialization;

namespace Splinter.Core;

// Provider-neutral contracts. Concrete adapters must fence direct writers as well
// as CLI calls; Windows product composition is outside Core and the read-only Host.
public sealed record ValidationOwner(string Id, long Generation);
public sealed record ValidationWorkerIdentity(string WorkspaceId, ValidationOwner Owner,
    SnapshotReference Snapshot, SnapshotDeltaSeal SourceSeal);
public sealed record ValidationCacheIdentity(string ViewId, string CacheId, SnapshotReference Snapshot,
    string CompatibilityKey, string PartitionKey, long MaxJournalBytes,
    [property: JsonRequired] bool SnapshotBacked = true);
public sealed record ValidationResources(bool OwnershipVerified, int? Applications, int? Writers,
    int? OpenHandles, bool? Mounted);
public sealed record ValidationWorkerObservation(ValidationWorkerIdentity Worker,
    ValidationResources Resources, bool? SnapshotAvailable, string ManagedPathPolicy);
public sealed record ValidationViewObservation(ValidationOwner Owner, ValidationResources Resources);

// Exactly one operation-specific payload is accepted; Runtime and JSON Schema
// both enforce it. IDs are opaque and never interpreted as caller-supplied paths.
public sealed record ValidationWorkflowRequest(int Version, string Operation, string ProjectId,
    string RequestKey, ValidationOwner Owner)
{
    public ValidationWorkerIdentity? Worker { get; init; }
    public string? CandidateId { get; init; }
    public ValidationCacheIdentity? Cache { get; init; }
    public string? GenerationId { get; init; }
    public ValidationTarget? Target { get; init; }
}

public sealed record ValidationTarget(string Kind, string Id);
public sealed record CandidatePublicationIntent(string OperationId, string ProjectId,
    string CandidateId, ValidationWorkerIdentity Worker, string ManagedPathPolicy);
public sealed record CandidatePublicationReceipt(CandidatePublicationIntent Intent,
    SnapshotDeltaSeal CandidateSeal, string ReceiptId);
public sealed record ValidationSelectionIntent(string OperationId, string ProjectId,
    string GenerationId, ValidationOwner Owner, ValidationCacheIdentity Cache,
    CandidatePublicationReceipt Candidate);
public sealed record ValidationSelectionReceipt(ValidationSelectionIntent Intent, string ReceiptId);
public sealed record CandidateValidationEvidence(string ValidationId, string GenerationId,
    string CandidateId, SnapshotDeltaSeal CandidateSeal, SnapshotDeltaSeal SourceOutputSeal,
    bool Success, DateTimeOffset CompletedAt);

// A source-generation artifact includes generated source, not the shared cache.
// A candidate alone still depends on its original snapshot until preserved.
public sealed record ValidationArtifact(ValidationTarget Target, ValidationOwner Owner,
    SnapshotReference Snapshot, SnapshotDeltaSeal Seal);
public sealed record ValidationPreservationIntent(string OperationId, string ProjectId,
    ValidationArtifact Artifact);
public sealed record ValidationIndependentResult(ValidationPreservationIntent Intent,
    string Reference, string ContentHash, bool IndependentOfSnapshot);
public sealed record ValidationReleaseIntent(string OperationId, string ProjectId,
    ValidationArtifact Artifact, ValidationIndependentResult Result);

public sealed record ValidationWorkflowResponse(int Version, string OperationId, string Operation,
    bool Replayed, CandidatePublicationReceipt? Candidate = null,
    ValidationSelectionReceipt? Selection = null, CandidateValidationEvidence? Evidence = null,
    ValidationIndependentResult? Result = null, ValidationTarget? Released = null, string? ClosedViewId = null);

public interface IValidationWorkflowProvider
{
    // Verify the current authoritative owner generation, including on retries.
    Task VerifyOwnerAsync(string projectId, string resourceId, ValidationOwner owner, CancellationToken ct);
    Task<ValidationWorkerObservation> ObserveWorkerAsync(string projectId, string workspaceId, CancellationToken ct);
    // Null means positively absent; unknown/corrupt/unreadable receipts must throw.
    Task<CandidatePublicationReceipt?> FindCandidateAsync(CandidatePublicationIntent intent, CancellationToken ct);
    // Fence writers, recheck the source seal/resources, remove no source data, and
    // publish one immutable source-only receipt. Never silently omit user files.
    Task<CandidatePublicationReceipt> PublishAsync(CandidatePublicationIntent intent, CancellationToken ct);
    Task<ValidationViewObservation> ObserveViewAsync(string projectId, string viewId, CancellationToken ct);
    Task<ValidationSelectionReceipt?> FindSelectionAsync(ValidationSelectionIntent intent, CancellationToken ct);
    // Select offline, preserving old source generations. This is not Editor-ready
    // or validation success. Recheck ownership/quiescence within the side effect.
    Task<ValidationSelectionReceipt> SelectAsync(ValidationSelectionIntent intent, CancellationToken ct);
    Task<CandidateValidationEvidence> ReadValidationAsync(ValidationSelectionReceipt selection, CancellationToken ct);
    Task<SnapshotDeltaSeal> SealSourceAsync(ValidationSelectionReceipt selection, CancellationToken ct);
    Task<ValidationIndependentResult?> FindResultAsync(ValidationPreservationIntent intent, CancellationToken ct);
    // Preserve the exact frozen seal, including failed validation outputs.
    Task<ValidationIndependentResult> PreserveAsync(ValidationPreservationIntent intent, CancellationToken ct);
    Task VerifyResultAsync(ValidationIndependentResult result, CancellationToken ct);
    // Idempotent for this intent, including a lost response after deletion. Delete
    // only the named private artifact after rechecking its frozen seal, current
    // owner and resources; never a shared cache or source snapshot.
    Task ReleaseAsync(ValidationReleaseIntent intent, CancellationToken ct);
    // Explicit cache closure after all dependent source generations were saved
    // and released. Return only after positive absence; retry a lost response
    // through the same owned reclamation receipt. Never release its base here.
    Task CloseCacheAsync(string projectId, ValidationCacheIdentity cache, ValidationOwner owner, CancellationToken ct);
}
