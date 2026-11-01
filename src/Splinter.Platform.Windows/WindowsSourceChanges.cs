using Splinter.Core;
using Splinter.Runtime;
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
    {
        var response = await ObserveAsync(workspace, expected, paths, ct);
        using var owner = response.View;
        if (response.InitialContent is not { } summary) return null;
        var generation = summary.Generation;
        // A summary carries a comparison stamp, not a hidden native reader.
        return summary with { Generation = new(generation.WorkspaceId, generation.SnapshotId, generation.Epoch,
            generation.Sequence, generation.Changes) { Index = generation.Index } };
    }

    private async Task<SourceChangeResponse> ObserveAsync(WorkspaceRecord workspace,
        SourceChangeView? expected, string[]? paths, CancellationToken ct)
    {
        // A shared request filename has one in-flight reader. Keep the response
        // bound before another caller can replace its request or response.
        string workspaceControl = controlPath(workspace.Id);
        var operationStore = new WorkspaceStore(Path.GetDirectoryName(Path.GetDirectoryName(workspaceControl)!)!);
        using var queryLease = await operationStore.LockAsync(WorkspaceStore.Key("source-query:" + workspace.Id), ct);
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
        for (long attempt = 0; ; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            var response = await WindowsProcessWorkspaceProvider.ReadAsync<SourceChangeResponse>(Path.Combine(control, "source-response.json"), ct);
            if (response?.Token == request.Token && response.RequestId == id)
            {
                await RequireProvider();
                if (response.ProcessId != mount.ProviderProcessId || response.ProcessStartTimeUtcTicks != mount.ProviderStartTimeUtcTicks)
                    throw new WorkspaceException("OverlayLeaseStale", "Source response belongs to another provider generation.");
                if (response.Error is { } error) throw new WorkspaceException(error, "Source change observation failed; preserve the workspace.");
                var view = response.View ?? throw new WorkspaceException("SourceObservationInvalid", "The provider returned no source generation.");
                try
                {
                if (view.WorkspaceId != workspace.Id || view.SnapshotId != mount.Snapshot.Id)
                    throw new WorkspaceException("SnapshotIdentityMismatch", "The source generation belongs to another snapshot.");
                if (request.Identity.Version == 3)
                {
                    if (view.Index is null || !view.Index.ManagedPaths.SequenceEqual(workspace.ManagedPaths))
                        throw new WorkspaceException("SourceObservationInvalid", "Current source responses require the exact managed-path scope and paged roots.");
                    view = JournalDeltaStore.BindChangeView(view, request.DeltaPath, request.Identity);
                }
                if (response.InitialContent is { } content && (expected is null || content.Generation is null ||
                    content.Generation.Changes is null || view.Changes is null || content.Files is null ||
                    content.Snapshot != workspace.Snapshot.Reference ||
                    content.Generation.WorkspaceId != view.WorkspaceId || content.Generation.SnapshotId != view.SnapshotId ||
                    content.Generation.Epoch != view.Epoch || content.Generation.Sequence != view.Sequence ||
                    (content.Generation.Index is null || view.Index is null ? !content.Generation.Changes.SequenceEqual(view.Changes) : content.Generation.ContentIdentity != view.ContentIdentity)))
                    throw new WorkspaceException("SourceObservationInvalid", "Initial content evidence is not bound to this provider response.");
                return response with { View = view, InitialContent = response.InitialContent is { } summary ? summary with { Generation = view } : null };
                }
                catch { view.Dispose(); throw; }
            }
            if (attempt % 10 == 0) await RequireProvider();
            await Task.Delay(100, ct);
        }

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
