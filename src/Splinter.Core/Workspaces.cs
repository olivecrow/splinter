namespace Splinter.Core;

// Application and VCS details stay behind the providers. Records are durable,
// versioned state, not proof that an external side effect has completed.
public sealed class WorkspaceRecord
{
    public int Version { get; set; } = 1;
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
}

public sealed record SourceWorkspace(string Path, string Reference, string BaseRevision, CloneStatistics? CloneMetrics = null);
public sealed record SourceState(string Revision, string Fingerprint, bool HasChanges, string[] ChangedPaths)
{
    public SourceInspectionMetrics? Inspection { get; init; }
}
public sealed record IntegrationResult(string Revision, bool AlreadyIntegrated);
public sealed record ApplicationLease(int ProcessId, long StartTimeUtcTicks, string Token, string ExecutablePath,
    ProcessGroupLease? Group = null);
public sealed record ApplicationValidation(string RequestId, string Fingerprint, bool Success,
    DateTimeOffset CompletedAt, string[] Errors);
public sealed record ApplicationState(bool Running, bool Ready, string Phase,
    ApplicationLease? Lease = null, ApplicationValidation? Validation = null, ProcessResources? Resources = null);

public interface ISourceWorkspaceProvider
{
    Task<string> ResolveRevisionAsync(string project, string reference, CancellationToken cancellationToken = default);
    Task<string> SnapshotKeyAsync(string project, string revision, string[] paths, CancellationToken cancellationToken = default);
    Task<SourceWorkspace> CreateAsync(WorkspaceRecord workspace, CancellationToken cancellationToken = default);
    Task<SourceState> InspectAsync(WorkspaceRecord workspace, CancellationToken cancellationToken = default);
    Task<string> CommitAsync(WorkspaceRecord workspace, string message, CancellationToken cancellationToken = default);
    Task<IntegrationResult> IntegrateAsync(WorkspaceRecord workspace, string targetReference, CancellationToken cancellationToken = default);
    Task ReleaseAsync(WorkspaceRecord workspace, CancellationToken cancellationToken = default);
}

public interface IApplicationWorkspaceProvider
{
    string[] ManagedPaths { get; }
    string[] CacheInputs { get; }
    string CompatibilityKey(string project);
    Task<ApplicationLease> StartAsync(WorkspaceRecord workspace, string controlPath, CancellationToken cancellationToken = default);
    Task<ApplicationState> ObserveAsync(WorkspaceRecord workspace, string controlPath, CancellationToken cancellationToken = default);
    Task RequestValidationAsync(WorkspaceRecord workspace, string controlPath, string requestId, string fingerprint,
        CancellationToken cancellationToken = default);
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
