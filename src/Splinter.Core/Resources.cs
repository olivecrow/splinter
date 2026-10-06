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
