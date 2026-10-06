using Splinter.Core;

namespace Splinter.Cli;

internal sealed class SelectedApplicationProvider(IApplicationWorkspaceProvider unity) : IApplicationWorkspaceProvider
{
    public string[] ManagedPaths => unity.ManagedPaths;

    public string CompatibilityKey(string p) => unity.CompatibilityKey(p);
    public Task<ApplicationLease> StartAsync(WorkspaceRecord w, string c, CancellationToken ct = default) => unity.StartAsync(w, c, ct);
    private static void None(WorkspaceRecord w)
    { if (w.Application is not null) throw new WorkspaceException("ProcessOwnershipUnknown", "An ordinary-folder record unexpectedly owns an application lease."); }
    public Task<ApplicationState> ObserveAsync(WorkspaceRecord w, string c, CancellationToken ct = default)
    {
        if (w.ApplicationKind == "unity") return unity.ObserveAsync(w, c, ct);
        None(w); return Task.FromResult(new ApplicationState(false, false, "not-selected", Resources: new(true, false, 0, 0, 0)));
    }
    public Task StopAsync(WorkspaceRecord w, string c, CancellationToken ct = default)
    { if (w.ApplicationKind == "unity") return unity.StopAsync(w, c, ct); None(w); return Task.CompletedTask; }
    public Task RemoveInfrastructureAsync(WorkspaceRecord w, string c, CancellationToken ct = default)
    { if (w.ApplicationKind == "unity") return unity.RemoveInfrastructureAsync(w, c, ct); None(w); return Task.CompletedTask; }
}
