namespace Splinter.Core;

// Application and VCS details stay behind the providers. Records are durable,
// versioned state, not proof that an external side effect has completed.
public sealed class WorkspaceRecord
{
    public const int CurrentVersion = 2;
    [System.Text.Json.Serialization.JsonRequired]
    public int Version { get; set; } = CurrentVersion;
    [System.Text.Json.Serialization.JsonRequired]
    public string SourceKind { get; set; } = "git";
    [System.Text.Json.Serialization.JsonRequired]
    public string ApplicationKind { get; set; } = "unity";
    public required string Id { get; set; }
    public required string RequestKey { get; set; }
    public required string ProjectPath { get; set; }
    public required string Path { get; set; }
    public required string SourceReference { get; set; }
    public required string BaseReference { get; set; }
    public string? BaseRevision { get; set; }
    public string Phase { get; set; } = "creating";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string StorageMode { get; set; } = "git-checkout";
    public string Purpose { get; set; } = "task";
    public string[] ManagedPaths { get; set; } = [];
    public ApplicationLease? Application { get; set; }
    // Retain earlier evidence in the current durable format; no longer produced or used as a gate.
    public string? PendingValidationId { get; set; }
    public string? PendingFingerprint { get; set; }
    public ApplicationValidation? Validation { get; set; }
    public string? ResultRevision { get; set; }
    public string? IntegratedRevision { get; set; }
    public string? LastError { get; set; }
    public string? BaselineId { get; set; }
    public CloneStatistics? CloneMetrics { get; set; }
    public ProcessResources? ReleasedResources { get; set; }
    public string? RequestDigest { get; set; }
    public SnapshotWorkspaceState? Snapshot { get; set; }
    public WorkspaceDiscardState? Discard { get; set; }
    public SourceResultStorage? ResultStorage { get; set; }
}

// Logical payload bytes of explicit result preservation, separate from the
// workspace journal and physical allocation/VSS accounting.
public sealed record SourceResultStorage(long BasisLogicalBytes, long ResultLogicalBytes, long UniquePayloadBytes, int PayloadObjects);

public sealed record SourceWorkspace(string Path, string Reference, string BaseRevision, CloneStatistics? CloneMetrics = null);
public sealed record SourceState(string Revision, string Fingerprint, bool HasChanges, string[] ChangedPaths)
{
    public SourceInspectionMetrics? Inspection { get; init; }
}
public sealed record IntegrationResult(string Revision, bool AlreadyIntegrated);
public sealed record ApplicationLease(int ProcessId, long StartTimeUtcTicks, string Token, string ExecutablePath,
    ProcessGroupLease? Group = null);
// Historical evidence in WorkspaceRecord only; application observations do not publish it.
public sealed record ApplicationValidation(string RequestId, string Fingerprint, bool Success,
    DateTimeOffset CompletedAt, string[] Errors);
public sealed record ApplicationState(bool Running, bool Ready, string Phase,
    ApplicationLease? Lease = null, ProcessResources? Resources = null);

public interface ISourceWorkspaceProvider
{
    Task<string> ResolveRevisionAsync(string project, string reference, CancellationToken cancellationToken = default);
    Task<string> SnapshotKeyAsync(string project, string revision, string[] paths, CancellationToken cancellationToken = default);
    Task<SourceWorkspace> CreateAsync(WorkspaceRecord workspace, CancellationToken cancellationToken = default);
    Task<SourceState> InspectAsync(WorkspaceRecord workspace, CancellationToken cancellationToken = default);
    // Returns an opaque preserved-source identity, not necessarily a commit.
    Task<string> CheckpointAsync(WorkspaceRecord workspace, string message, CancellationToken cancellationToken = default);
    Task<IntegrationResult> IntegrateAsync(WorkspaceRecord workspace, string targetReference, CancellationToken cancellationToken = default);
    Task ReleaseAsync(WorkspaceRecord workspace, CancellationToken cancellationToken = default);
}

public interface ISourceDiscardProvider
{
    Task<string> ObserveDiscardRevisionAsync(WorkspaceRecord workspace, string control, CancellationToken ct = default);
    Task DiscardSourceAsync(WorkspaceRecord workspace, string control, CancellationToken ct = default);
    Task VerifyDiscardedAsync(WorkspaceRecord workspace, string control, CancellationToken ct = default);
}

public interface IApplicationWorkspaceProvider
{
    string[] ManagedPaths { get; }
    string[] CacheInputs { get; }
    string CompatibilityKey(string project);
    Task<ApplicationLease> StartAsync(WorkspaceRecord workspace, string controlPath, CancellationToken cancellationToken = default);
    Task<ApplicationState> ObserveAsync(WorkspaceRecord workspace, string controlPath, CancellationToken cancellationToken = default);
    Task StopAsync(WorkspaceRecord workspace, string controlPath, CancellationToken cancellationToken = default);
    Task RemoveInfrastructureAsync(WorkspaceRecord workspace, string controlPath, CancellationToken cancellationToken = default);
}

public sealed record WorkspaceRecoveryAction(string Command, object Arguments, bool RequiresUserAction = false);
public sealed class WorkspaceException(string code, string message, object? details = null,
    WorkspaceRecoveryAction[]? recoveryActions = null) : Exception(message)
{
    public string Code { get; } = code;
    public object? Details { get; } = details;
    public WorkspaceRecoveryAction[]? RecoveryActions { get; } = recoveryActions;
}
