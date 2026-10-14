using Splinter.Core;
using System.Text.Json.Serialization;

namespace Splinter.Platform.Windows;

// An explicit request/response barrier, never the periodic state.json cache.
// The short-lived CLI must hear from the exact provider named by its lease.
public sealed class WindowsSourceChanges(Func<string, string> controlPath) : ISourceChangeProvider, ISourceContentSummaryProvider
{
    public string CachePath(WorkspaceRecord workspace) => Path.Combine(controlPath(workspace.Id), "source-fingerprint.json");

    public async Task<SourceChangeView> ObserveChangesAsync(WorkspaceRecord workspace, CancellationToken ct = default)
        => (await ObserveAsync(workspace, null, null, ct)).View!;

    public async Task<SourceContentSummaryView?> ObserveInitialContentAsync(WorkspaceRecord workspace,
        SourceChangeView expected, string[] paths, CancellationToken ct = default)
        => (await ObserveAsync(workspace, expected, paths, ct)).InitialContent;

    private async Task<SourceChangeResponse> ObserveAsync(WorkspaceRecord workspace,
        SourceChangeView? expected, string[]? paths, CancellationToken ct)
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
            new SourceChangeRequest(request.Token, id, workspace.ManagedPaths, expected, paths), ct);
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
                if (response.InitialContent is { } content && (expected is null || content.Generation is null ||
                    content.Generation.Changes is null || view.Changes is null || content.Files is null ||
                    content.Snapshot != workspace.Snapshot.Reference ||
                    content.Generation.WorkspaceId != view.WorkspaceId || content.Generation.SnapshotId != view.SnapshotId ||
                    content.Generation.Epoch != view.Epoch || content.Generation.Sequence != view.Sequence ||
                    !content.Generation.Changes.SequenceEqual(view.Changes)))
                    throw new WorkspaceException("SourceObservationInvalid", "Initial content evidence is not bound to this provider response.");
                return response;
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

public sealed record SourceChangeRequest(string Token, string RequestId, string[] ManagedPaths,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] SourceChangeView? ExpectedGeneration = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string[]? ContentPaths = null);
public sealed record SourceChangeResponse(string Token, string RequestId, int ProcessId, long ProcessStartTimeUtcTicks,
    SourceChangeView? View, string? Error,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] SourceContentSummaryView? InitialContent = null);
