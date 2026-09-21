using System.Diagnostics;
using System.Text.Json;
using Splinter.Core;

namespace Splinter.Platform.Windows;

// Experimental process control. Request identity is immutable; reconnect reuses
// the original request and journal, never a new source capture.
public static class WinFspOverlayClient
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public static async Task<OverlayServeRequest> ReadRequestAsync(string path, CancellationToken ct = default)
    {
        WindowsProcessWorkspaceProvider.CheckPath(path);
        return await WindowsProcessWorkspaceProvider.ReadAsync<OverlayServeRequest>(path, ct)
            ?? throw new WorkspaceException("OverlayRequestMissing", "A persisted overlay request is required.");
    }
    public static async Task<JsonElement> ObserveAsync(OverlayServeRequest request, CancellationToken ct = default)
    {
        JsonElement state = await WindowsProcessWorkspaceProvider.ReadAsync<JsonElement>(Path.Combine(request.ControlPath, "state.json"), ct);
        var launch = await WindowsProcessWorkspaceProvider.ReadAsync<ProviderLaunch>(Path.Combine(request.ControlPath, "provider-launch-receipt.json"), ct);
        var intent = await WindowsProcessWorkspaceProvider.ReadAsync<ProviderIntent>(Path.Combine(request.ControlPath, "provider-launch-intent.json"), ct);
        if (launch is not null && launch.Token != request.Token || intent is not null && (intent.Token != request.Token || intent.Id != request.Identity.Id))
            throw new WorkspaceException("OverlayOwnerMismatch", "Provider launch records belong to another overlay.");
        bool launchAlive = launch is not null && WindowsProcessGroup.IdentityAlive(launch.ProcessId, launch.StartTimeUtcTicks);
        bool knownLaunch = intent is null && launch is null || launch is not null && intent?.Generation == launch.Generation;
        if (state.ValueKind == JsonValueKind.Undefined)
        {
            return JsonSerializer.SerializeToElement(new { phase = launchAlive ? "starting" : !knownLaunch ? "launch-uncertain" : "not-started",
                processAlive = launchAlive, ownershipVerified = knownLaunch }, Json);
        }
        if (state.GetProperty("token").GetString() != request.Token || state.GetProperty("id").GetString() != request.Identity.Id ||
            state.GetProperty("snapshotId").GetString() != request.Identity.Snapshot.Id)
            throw new WorkspaceException("OverlayOwnerMismatch", "State does not belong to this immutable request.");
        bool alive = state.TryGetProperty("processStartTimeUtcTicks", out var ticks) &&
            WindowsProcessGroup.IdentityAlive(state.GetProperty("processId").GetInt32(), ticks.GetInt64());
        if (!knownLaunch || launchAlive && (state.GetProperty("processId").GetInt32() != launch!.ProcessId ||
            ticks.ValueKind == JsonValueKind.Undefined || ticks.GetInt64() != launch.StartTimeUtcTicks))
            return JsonSerializer.SerializeToElement(new { phase = "launch-uncertain", processAlive = alive || launchAlive, ownershipVerified = false }, Json);
        bool available = Directory.Exists(request.Identity.Snapshot.RootPath);
        string? phase = state.GetProperty("phase").GetString();
        return JsonSerializer.SerializeToElement(new { phase = alive && !available ? "snapshot-missing" : phase, processAlive = alive,
            snapshotAvailable = available, failureCode = available ? null : "SnapshotMissing",
            mountPresent = Directory.Exists(request.MountPath), state }, Json);
    }
    public static async Task<JsonElement> StartAsync(string requestPath, string runnerCache, CancellationToken ct = default)
    {
        var request = await ReadRequestAsync(requestPath, ct);
        WindowsProcessWorkspaceProvider.CheckPath(request.ControlPath); Directory.CreateDirectory(request.ControlPath);
        using var operation = new FileStream(Path.Combine(request.ControlPath, "client.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        var before = await ObserveAsync(request, ct);
        if (before.GetProperty("processAlive").GetBoolean()) return before;
        string intentPath = Path.Combine(request.ControlPath, "provider-launch-intent.json");
        string receiptPath = Path.Combine(request.ControlPath, "provider-launch-receipt.json");
        var priorLaunch = await WindowsProcessWorkspaceProvider.ReadAsync<ProviderLaunch>(receiptPath, ct);
        var priorIntent = await WindowsProcessWorkspaceProvider.ReadAsync<ProviderIntent>(intentPath, ct);
        if ((priorIntent is not null && (priorLaunch is null || priorIntent.Generation != priorLaunch.Generation)) || priorLaunch is not null &&
            WindowsProcessGroup.IdentityAlive(priorLaunch.ProcessId, priorLaunch.StartTimeUtcTicks))
            throw new WorkspaceException("OverlayStartUncertain", "A provider launch is not reconciled. Preserve its receipt and inspect that exact process before retrying.");
        if (Directory.Exists(request.MountPath) && (File.GetAttributes(request.MountPath) & FileAttributes.ReparsePoint) != 0)
            throw new WorkspaceException("MountReconcileRequired", "A stale mount remains. Preserve its journal and inspect ownership before reconnecting.");
        using (var snapshot = new VssSnapshotTree(request.Identity.Snapshot)) snapshot.EnsureAvailable();
        string stop = Path.Combine(request.ControlPath, "stop.json"); WindowsProcessWorkspaceProvider.CheckPath(stop);
        if (File.Exists(stop)) File.Delete(stop);
        string runner = WindowsProcessWorkspaceProvider.PrepareRunner(runnerCache);
        string generation = Guid.NewGuid().ToString("N");
        await WindowsProcessWorkspaceProvider.WriteAsync(intentPath, new ProviderIntent(request.Token, request.Identity.Id, generation, DateTimeOffset.UtcNow), ct);
        using var process = WindowsProcessGroup.StartSupervisor("dotnet", [runner, "overlay-serve", Path.GetFullPath(requestPath)], request.ControlPath);
        await WindowsProcessWorkspaceProvider.WriteAsync(receiptPath, new ProviderLaunch(request.Token, process.Id, process.StartTime.ToUniversalTime().Ticks, generation), CancellationToken.None);
        for (int i = 0; i < 150; i++)
        {
            var current = await ObserveAsync(request, ct);
            if (current.GetProperty("processAlive").GetBoolean() && current.TryGetProperty("state", out var currentState) && currentState.GetProperty("processId").GetInt32() == process.Id)
                return current;
            if (process.HasExited) throw new WorkspaceException("OverlayStartFailed", "Provider exited. Inspect state.json; delta is preserved.");
            await Task.Delay(100, ct);
        }
        throw new WorkspaceException("OverlayStartUncertain", "Query the persisted state before retrying.");
    }
    public static async Task<JsonElement> StopAsync(OverlayServeRequest request, CancellationToken ct = default)
    {
        await WindowsProcessWorkspaceProvider.WriteAsync(Path.Combine(request.ControlPath, "stop.json"), new { request.Token }, ct);
        for (int i = 0; i < 150; i++)
        {
            var state = await ObserveAsync(request, ct);
            if (!state.GetProperty("processAlive").GetBoolean() && !Directory.Exists(request.MountPath) &&
                (!state.TryGetProperty("ownershipVerified", out var known) || known.GetBoolean())) return state;
            await Task.Delay(100, ct);
        }
        throw new WorkspaceException("OverlayStopPending", "Clients or provider remain; files and snapshot are retained. Query status and close owned clients.");
    }
    private sealed record ProviderIntent(string Token, string Id, string? Generation, DateTimeOffset RequestedAt);
    private sealed record ProviderLaunch(string Token, int ProcessId, long StartTimeUtcTicks, string? Generation = null);
}
