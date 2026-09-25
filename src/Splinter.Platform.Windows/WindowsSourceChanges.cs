using Splinter.Core;

namespace Splinter.Platform.Windows;

// An explicit request/response barrier, never the periodic state.json cache.
// The short-lived CLI must hear from the exact provider named by its lease.
public sealed class WindowsSourceChanges(Func<string, string> controlPath) : ISourceChangeProvider
{
    public string CachePath(WorkspaceRecord workspace) => Path.Combine(controlPath(workspace.Id), "source-fingerprint.json");

    public async Task<SourceChangeView> ObserveChangesAsync(WorkspaceRecord workspace, CancellationToken ct = default)
    {
        var mount = workspace.Snapshot?.Mount ?? throw new WorkspaceException("OverlayLeaseMissing", "Reconnect the owned provider before inspecting source.");
        string control = Path.Combine(controlPath(workspace.Id), "overlay");
        var request = await WinFspOverlayClient.ReadRequestAsync(Path.Combine(control, "request.json"), ct);
        if (request.Identity.Id != workspace.Id || request.Identity.Snapshot != workspace.Snapshot!.Reference ||
            request.Identity.Snapshot != mount.Snapshot ||
            !string.Equals(Path.GetFullPath(request.MountPath), Path.GetFullPath(workspace.Path), StringComparison.OrdinalIgnoreCase))
            throw new WorkspaceException("SnapshotIdentityMismatch", "Source observation must use this workspace's original snapshot.");
        await RequireProvider();
        string id = Guid.NewGuid().ToString("N");
        await WindowsProcessWorkspaceProvider.WriteAsync(Path.Combine(control, "source-query.json"),
            new SourceChangeRequest(request.Token, id, workspace.ManagedPaths), ct);
        for (int attempt = 0; attempt < 150; attempt++)
        {
            var response = await WindowsProcessWorkspaceProvider.ReadAsync<SourceChangeResponse>(Path.Combine(control, "source-response.json"), ct);
            if (response?.Token == request.Token && response.RequestId == id)
            {
                await RequireProvider();
                if (response.ProcessId != mount.ProviderProcessId || response.ProcessStartTimeUtcTicks != mount.ProviderStartTimeUtcTicks)
                    throw new WorkspaceException("OverlayLeaseStale", "Source response belongs to another provider generation.");
                if (response.Error is { } error) throw new WorkspaceException(error, "Source change observation failed; preserve the workspace.");
                var view = response.View ?? throw new WorkspaceException("SourceObservationInvalid", "The provider returned no source generation.");
                if (view.WorkspaceId != workspace.Id || view.SnapshotId != mount.Snapshot.Id)
                    throw new WorkspaceException("SnapshotIdentityMismatch", "The source generation belongs to another snapshot.");
                return view;
            }
            await Task.Delay(100, ct);
        }
        throw new WorkspaceException("SourceObservationUncertain", "No fresh source response; preserve and retry the same workspace.");

        async Task RequireProvider()
        {
            var observed = await WinFspOverlayClient.ObserveAsync(request, ct);
            if (!observed.GetProperty("processAlive").GetBoolean() || !observed.TryGetProperty("state", out var state) ||
                observed.GetProperty("phase").GetString() != "mounted" ||
                state.GetProperty("processId").GetInt32() != mount.ProviderProcessId ||
                state.GetProperty("processStartTimeUtcTicks").GetInt64() != mount.ProviderStartTimeUtcTicks ||
                !Directory.Exists(workspace.Path))
                throw new WorkspaceException("SourceObservationUncertain", "The owned source provider is not available; reconnect before verification.");
        }
    }
}

public sealed record SourceChangeRequest(string Token, string RequestId, string[] ManagedPaths);
public sealed record SourceChangeResponse(string Token, string RequestId, int ProcessId, long ProcessStartTimeUtcTicks,
    SourceChangeView? View, string? Error);
