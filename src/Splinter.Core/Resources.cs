namespace Splinter.Core;

// Platform-neutral ownership: the supervisor outlives a calling CLI, and its
// process group contains descendants from the first instruction of the root.
public sealed record ProcessGroupLease(string Name, int SupervisorId, long SupervisorStartTimeUtcTicks, string? ControlPath = null);
public sealed record ProcessLaunch(string Executable, string[] Arguments, string WorkingDirectory, string Token);
public sealed record ProcessResources(bool OwnershipVerified, bool RootRunning, int ActiveProcesses,
    long WorkingSetBytes, long PrivateBytes);

public interface IProcessWorkspaceProvider
{
    Task<ApplicationLease> StartAsync(ProcessLaunch launch, string controlPath, CancellationToken cancellationToken = default);
    Task<ProcessResources> ObserveAsync(ApplicationLease lease, CancellationToken cancellationToken = default);
    Task WaitForExitAsync(ApplicationLease lease, CancellationToken cancellationToken = default);
}

public sealed record CloneStatistics(long LogicalBytes, long ClonedBytes, long CopiedBytes, int Files,
    long ElapsedMilliseconds);
public sealed record StorageProbe(string Mode, string Volume, string FileSystem, long ClusterBytes);

public interface IWorkspaceStorageProvider
{
    StorageProbe Probe(string directory);
    Task<CloneStatistics> CloneAsync(string source, string destination, string[] excludedTopLevel,
        CancellationToken cancellationToken = default);
}

public sealed record WorkspaceBaseline(string Id, string ProjectPath, string BaseRevision, string CompatibilityKey,
    string Path, DateTimeOffset SealedAt, CloneStatistics CloneMetrics, string ManifestHash);
public interface IWorkspaceBaselineReader
{
    WorkspaceBaseline GetBaseline(string id);
}
