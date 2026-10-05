using System.Text.Json;
using System.Diagnostics;
using Splinter.Core;

namespace Splinter.Platform.Windows;

public sealed class WinFspOverlayMountProvider(string requestPath, string runnerCache, Func<string, Task>? preparationPoint = null) : IOverlayMountProvider
{
    public async Task<OverlayMount> MountAsync(OverlayIdentity identity, string deltaPath, string mountPath, CancellationToken cancellationToken = default)
    {
        WindowsSnapshotPolicy.Require(identity.Snapshot);
        var request = await WinFspOverlayClient.ReadRequestAsync(requestPath, cancellationToken);
        if (JsonSerializer.Serialize(request.Identity) != JsonSerializer.Serialize(identity) || !Same(request.DeltaPath, deltaPath) || !Same(request.MountPath, mountPath))
            throw new WorkspaceException("OverlayIdentityMismatch", "Mount must use the persisted request identity and paths.");
        var result = await WinFspOverlayClient.StartAsync(requestPath, runnerCache, cancellationToken, preparationPoint);
        if (result.GetProperty("phase").GetString() != "mounted") throw new WorkspaceException("OverlayNotReady", result.GetProperty("phase").GetString()!);
        var state = result.GetProperty("state");
        return new(identity.Id, mountPath, deltaPath, identity.Snapshot, state.GetProperty("processId").GetInt32(), state.GetProperty("processStartTimeUtcTicks").GetInt64());
    }
    public async Task<OverlayMount> ReconnectAsync(OverlayMount mount, CancellationToken cancellationToken = default)
    {
        var request = await Request(mount, cancellationToken);
        return await MountAsync(request.Identity, mount.DeltaPath, mount.Path, cancellationToken);
    }
    public async Task FlushAsync(OverlayMount mount, CancellationToken cancellationToken = default)
    {
        var observed = await ObserveAsync(mount, cancellationToken);
        if (!observed.Mounted || !observed.OwnershipVerified) throw new WorkspaceException("OverlayNotReady", "Flush requires the current owned provider.");
        var request = await Request(mount, cancellationToken);
        string id = Guid.NewGuid().ToString("N");
        await WindowsProcessWorkspaceProvider.WriteAsync(Path.Combine(request.ControlPath, "flush.json"), new { request.Token, requestId = id }, cancellationToken);
        for (int i = 0; i < 100; i++)
        {
            var receipt = await WindowsProcessWorkspaceProvider.ReadAsync<JsonElement>(Path.Combine(request.ControlPath, "flush-result.json"), cancellationToken);
            if (receipt.ValueKind == JsonValueKind.Object && receipt.GetProperty("token").GetString() == request.Token && receipt.GetProperty("requestId").GetString() == id) return;
            await Task.Delay(100, cancellationToken);
        }
        throw new WorkspaceException("OverlayFlushUncertain", "Preserve the journal and inspect the provider before retrying.");
    }
    public async Task<OverlayMountObservation> ObserveAsync(OverlayMount mount, CancellationToken cancellationToken = default)
    {
        var request = await Request(mount, cancellationToken);
        var result = await WinFspOverlayClient.ObserveAsync(request, cancellationToken);
        bool alive = result.GetProperty("processAlive").GetBoolean(), present = Directory.Exists(mount.Path);
        if (!result.TryGetProperty("state", out _))
        {
            bool known = result.GetProperty("ownershipVerified").GetBoolean() && !alive && !present;
            return new(present, known, known ? 0 : null, known ? 0 : null, known ? 0 : null,
                known ? "OverlayNotStarted" : "OverlayStartUncertain");
        }
        var state = result.GetProperty("state");
        if (alive && (state.GetProperty("processId").GetInt32() != mount.ProviderProcessId || state.GetProperty("processStartTimeUtcTicks").GetInt64() != mount.ProviderStartTimeUtcTicks))
            throw new WorkspaceException("OverlayLeaseStale", "Reconnect to the current provider generation before controlling it.");
        long working = 0, memory = 0;
        if (alive)
        {
            // Failed/unmounted state can precede process exit and omit memory
            // fields. Read the actual process; never infer zero from omission.
            try { using var process = Process.GetProcessById(mount.ProviderProcessId); working = process.WorkingSet64; memory = process.PrivateMemorySize64; }
            catch (ArgumentException) { alive = false; }
            catch (InvalidOperationException) { alive = false; }
        }
        return new(present, !alive && !present || alive, alive ? state.TryGetProperty("openHandles", out var handles) ? handles.GetInt32() : null : 0,
            working, memory,
            state.TryGetProperty("error", out var error) ? error.GetString() : result.GetProperty("failureCode").GetString());
    }
    public async Task UnmountAsync(OverlayMount mount, CancellationToken cancellationToken = default)
    { await ObserveAsync(mount, cancellationToken); await WinFspOverlayClient.StopAsync(await Request(mount, cancellationToken), cancellationToken); }
    private async Task<OverlayServeRequest> Request(OverlayMount mount, CancellationToken ct)
    {
        var request = await WinFspOverlayClient.ReadRequestAsync(requestPath, ct);
        if (request.Identity.Id != mount.Id || request.Identity.Snapshot != mount.Snapshot || !Same(request.MountPath, mount.Path) || !Same(request.DeltaPath, mount.DeltaPath))
            throw new WorkspaceException("OverlayIdentityMismatch", "Mount lease does not match the persisted snapshot and delta.");
        return request;
    }
    private static bool Same(string a, string b) => string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);
}
