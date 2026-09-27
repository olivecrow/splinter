using System.Diagnostics;
using System.Text.Json;
using Splinter.Core;

namespace Splinter.Platform.Windows;

// Experimental process control. Request identity is immutable; reconnect reuses
// the original request and journal, never a new source capture.
public static class WinFspOverlayClient
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    internal static async Task<string?> ConsumedVerificationAsync(OverlayServeRequest request, CancellationToken ct)
    {
        var intent = await WindowsProcessWorkspaceProvider.ReadAsync<ProviderIntent>(Path.Combine(request.ControlPath, "provider-launch-intent.json"), ct);
        return intent?.VerificationId;
    }
    internal static async Task<SnapshotObservation> ObserveSnapshotAsync(OverlayServeRequest request, CancellationToken ct = default)
    {
        if (request.SnapshotReceiptDirectory is null && request.SnapshotOwnerId is null && request.ReadAccess is null)
            return SnapshotAvailabilityProbe.Observe(request.Identity.Snapshot); // Separate legacy experiment contract.
        if (request.ReadAccess is null || request.SnapshotReceiptDirectory is null || request.SnapshotOwnerId is null)
            return new(request.Identity.Snapshot.Id, null, "SnapshotVerificationRequired", null, null);
        request.ReadAccess.RequireMatch(request.Identity.Snapshot);
        var proof = await new VssSnapshotProvider(request.SnapshotReceiptDirectory, request.SnapshotOwnerId).ObserveAsync(request.Identity.Snapshot, ct);
        if (proof.Available == true && proof.ReadAccess != request.ReadAccess)
            return new(request.Identity.Snapshot.Id, null, "SnapshotIdentityMismatch", null, null);
        return proof;
    }
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
        if (!knownLaunch)
            return JsonSerializer.SerializeToElement(new { phase = "launch-uncertain", processAlive = alive || launchAlive, ownershipVerified = false }, Json);
        if (launchAlive && (state.GetProperty("processId").GetInt32() != launch!.ProcessId ||
            ticks.ValueKind == JsonValueKind.Undefined || ticks.GetInt64() != launch.StartTimeUtcTicks))
            return JsonSerializer.SerializeToElement(new { phase = "starting", processAlive = true, ownershipVerified = true }, Json);
        var snapshot = await ObserveSnapshotAsync(request, ct);
        string? phase = state.GetProperty("phase").GetString();
        return JsonSerializer.SerializeToElement(new { phase = alive && snapshot.Available != true ? "snapshot-" + snapshot.Availability : phase, processAlive = alive,
            snapshotAvailable = snapshot.Available, snapshotAvailability = snapshot.Availability, failureCode = snapshot.FailureCode,
            mountPresent = Directory.Exists(request.MountPath), state }, Json);
    }
    public static async Task<JsonElement> StartAsync(string requestPath, string runnerCache, CancellationToken ct = default)
    {
        var request = await ReadRequestAsync(requestPath, ct);
        WindowsProcessWorkspaceProvider.CheckPath(request.ControlPath); Directory.CreateDirectory(request.ControlPath);
        using var operation = new FileStream(Path.Combine(request.ControlPath, "client.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        var before = await ObserveAsync(request, ct);
        string intentPath = Path.Combine(request.ControlPath, "provider-launch-intent.json");
        string receiptPath = Path.Combine(request.ControlPath, "provider-launch-receipt.json");
        var priorLaunch = await WindowsProcessWorkspaceProvider.ReadAsync<ProviderLaunch>(receiptPath, ct);
        var priorIntent = await WindowsProcessWorkspaceProvider.ReadAsync<ProviderIntent>(intentPath, ct);
        if (before.GetProperty("processAlive").GetBoolean())
        {
            if (priorLaunch is null || priorIntent?.Generation != priorLaunch.Generation)
                throw new WorkspaceException("OverlayStartUncertain", "Preserve the live provider without a matching launch receipt.");
            return await AwaitMountedAsync(() => ObserveAsync(request, ct), priorLaunch.ProcessId,
                priorLaunch.StartTimeUtcTicks, TimeSpan.FromMinutes(2), ct);
        }
        if ((priorIntent is not null && (priorLaunch is null || priorIntent.Generation != priorLaunch.Generation)) || priorLaunch is not null &&
            WindowsProcessGroup.IdentityAlive(priorLaunch.ProcessId, priorLaunch.StartTimeUtcTicks))
            throw new WorkspaceException("OverlayStartUncertain", "A provider launch is not reconciled. Preserve its receipt and inspect that exact process before retrying.");
        if (Directory.Exists(request.MountPath) && (File.GetAttributes(request.MountPath) & FileAttributes.ReparsePoint) != 0)
            throw new WorkspaceException("MountReconcileRequired", "A stale mount remains. Preserve its journal and inspect ownership before reconnecting.");
        var verified = await ObserveSnapshotAsync(request, ct);
        if (verified.Available != true) throw new WorkspaceException(verified.FailureCode ?? "SnapshotObservationUnknown", "Verify snapshot access before starting a provider.");
        using (var snapshot = new VssSnapshotTree(request.Identity.Snapshot, request.ReadAccess)) snapshot.EnsureAvailable();
        string stop = Path.Combine(request.ControlPath, "stop.json"); WindowsProcessWorkspaceProvider.CheckPath(stop);
        if (File.Exists(stop)) File.Delete(stop);
        string runner = WindowsProcessWorkspaceProvider.PrepareRunner(runnerCache);
        string generation = Guid.NewGuid().ToString("N");
        await WindowsProcessWorkspaceProvider.WriteAsync(intentPath, new ProviderIntent(request.Token, request.Identity.Id, generation, DateTimeOffset.UtcNow, request.ReadAccess?.VerificationId), ct);
        using var process = WindowsProcessGroup.StartSupervisor("dotnet", [runner, "overlay-serve", Path.GetFullPath(requestPath)], request.ControlPath);
        await WindowsProcessWorkspaceProvider.WriteAsync(receiptPath, new ProviderLaunch(request.Token, process.Id, process.StartTime.ToUniversalTime().Ticks, generation), CancellationToken.None);
        return await AwaitMountedAsync(() => ObserveAsync(request, ct), process.Id,
            process.StartTime.ToUniversalTime().Ticks, TimeSpan.FromMinutes(2), ct);
    }

    internal static async Task<JsonElement> AwaitMountedAsync(Func<Task<JsonElement>> observe, int processId,
        long startTimeUtcTicks, TimeSpan timeout, CancellationToken ct)
    {
        long started = Stopwatch.GetTimestamp();
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            var current = await observe();
            if (current.TryGetProperty("ownershipVerified", out var ownership) && !ownership.GetBoolean())
                throw new WorkspaceException("OverlayStartUncertain", "Provider ownership is unresolved; preserve the existing launch.");
            if (current.TryGetProperty("state", out var state))
            {
                // State from an exited preceding generation may still be on disk
                // before the new process publishes its first state.
                bool sameProcess = state.TryGetProperty("processId", out var pid) && pid.GetInt32() == processId &&
                    state.TryGetProperty("processStartTimeUtcTicks", out var ticks) && ticks.GetInt64() == startTimeUtcTicks;
                if (!sameProcess) throw new WorkspaceException("OverlayStartUncertain", "The observed provider generation changed; preserve both receipts.");
                string? phase = current.GetProperty("phase").GetString();
                if (phase == "failed")
                    throw new WorkspaceException(state.TryGetProperty("error", out var error) ? error.GetString() ?? "OverlayStartFailed" : "OverlayStartFailed",
                        "Provider failed before readiness. Inspect its state; journal and launch records are preserved.");
                if (phase == "mounted" && current.GetProperty("processAlive").GetBoolean() &&
                    current.TryGetProperty("mountPresent", out var mount) && mount.GetBoolean() &&
                    state.TryGetProperty("observedAt", out var at) &&
                    DateTimeOffset.UtcNow - at.GetDateTimeOffset() is { TotalSeconds: >= -1 and <= 5 }) return current;
            }
            if (!current.GetProperty("processAlive").GetBoolean())
                throw new WorkspaceException("OverlayStartFailed", "Provider exited before readiness. Preserve its state and journal.");
            if (Stopwatch.GetElapsedTime(started) >= timeout)
                throw new WorkspaceException("OverlayStartUncertain", "Provider is still preparing or its mount is unconfirmed. Preserve it and retry this same request.");
            await Task.Delay(100, ct);
        }
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
    private sealed record ProviderIntent(string Token, string Id, string? Generation, DateTimeOffset RequestedAt, string? VerificationId = null);
    private sealed record ProviderLaunch(string Token, int ProcessId, long StartTimeUtcTicks, string? Generation = null);
}
