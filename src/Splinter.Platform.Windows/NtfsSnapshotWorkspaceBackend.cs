using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Win32;
using Splinter.Core;

namespace Splinter.Platform.Windows;

public sealed class NtfsSnapshotWorkspaceBackend(string runnerCache) : ISnapshotWorkspaceBackend
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static string OverlayControl(string control) => Path.Combine(control, "overlay");
    private static string RequestPath(string control) => Path.Combine(OverlayControl(control), "request.json");
    private static string DeltaPath(string control) => Path.Combine(control, "delta");
    private static VssSnapshotProvider Provider(WorkspaceRecord w, string control) => new(Path.Combine(control, "snapshot"), w.Id);
    private WinFspOverlayMountProvider MountProvider(string control) => new(RequestPath(control), runnerCache);
    private static OverlayIdentity Identity(WorkspaceRecord w) => new(1, w.Id, w.Snapshot!.Reference!, w.Snapshot.MaxJournalBytes, 1024 * 1024, [".git"]);

    public async Task<SnapshotReference> CaptureAsync(WorkspaceRecord w, string control, CancellationToken ct = default)
    {
        if (!OperatingSystem.IsWindows()) throw new WorkspaceException("PlatformUnsupported", "NTFS snapshots require Windows.");
        if (new DriveInfo(Path.GetPathRoot(w.ProjectPath)!).DriveFormat != "NTFS")
            throw new WorkspaceException("NtfsVolumeRequired", "Keep the source on an existing NTFS volume.");
        if (Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\WinFsp", "InstallDir", null) is not string)
            throw new WorkspaceException("WinFspMissing", "Install a qualified WinFsp provider before capture; no full-copy fallback is available.");
        CheckSpace(control);
        // Reject unsupported live input before requesting a privileged capture.
        // Repeat on the immutable tree to close the capture-time race.
        if (!File.Exists(Path.Combine(control, "snapshot", "snapshot.json"))) SnapshotInputChecks.VerifyTree(w.ProjectPath, ct);
        var snapshot = await Provider(w, control).CaptureAsync(new(w.ProjectPath, w.Id, Path.Combine(control, "snapshot")), ct);
        string probePath = Path.Combine(control, "snapshot", "input-check.json");
        var prior = await WindowsProcessWorkspaceProvider.ReadAsync<InputCheck>(probePath, ct);
        if (prior is null || prior.SnapshotId != snapshot.Id || prior.Version != 1)
        {
            var watch = Stopwatch.StartNew();
            int entries = SnapshotInputChecks.VerifyTree(snapshot.RootPath, ct);
            await WindowsProcessWorkspaceProvider.WriteAsync(probePath, new InputCheck(1, snapshot.Id, entries, watch.ElapsedMilliseconds, 0), ct);
        }
        return snapshot;
    }

    public async Task<OverlayMount> ConnectAsync(WorkspaceRecord w, string control, CancellationToken ct = default)
    {
        CheckSpace(control);
        var request = await Request(w, control, true, ct);
        return await MountProvider(control).MountAsync(request!.Identity, request.DeltaPath, request.MountPath, ct);
    }
    public async Task<SnapshotWorkspaceObservation> ObserveAsync(WorkspaceRecord w, string control, CancellationToken ct = default)
    {
        var snapshotObservation = w.Snapshot?.Reference is { } snapshot ? await Provider(w, control).ObserveAsync(snapshot, ct) : null;
        bool? available = snapshotObservation is null ? false : snapshotObservation.Available;
        var request = await Request(w, control, false, ct);
        if (request is null) return WithSpace(new(available, false, false, true, 0, 0, 0,
            FailureCode: snapshotObservation?.FailureCode ?? (available == true ? null : "SnapshotNotCaptured")), w, control);
        var observed = await WinFspOverlayClient.ObserveAsync(request, ct);
        bool alive = observed.GetProperty("processAlive").GetBoolean();
        bool mounted = Directory.Exists(w.Path);
        if (!observed.TryGetProperty("state", out var state))
        {
            bool known = observed.GetProperty("ownershipVerified").GetBoolean() && !alive && !mounted;
            return WithSpace(new(available, mounted, alive, known, known ? 0 : null, known ? 0 : null, known ? 0 : null,
                FailureCode: snapshotObservation?.FailureCode ?? (known ? "OverlayNotStarted" : "OverlayStartUncertain")), w, control);
        }
        long? working = 0, memory = 0;
        if (alive)
        {
            try
            {
                using var process = Process.GetProcessById(state.GetProperty("processId").GetInt32());
                working = process.WorkingSet64; memory = process.PrivateMemorySize64;
            }
            catch (ArgumentException) { alive = false; }
            catch (InvalidOperationException) { alive = false; }
        }
        bool fresh = !alive || state.TryGetProperty("observedAt", out var timestamp) && DateTimeOffset.UtcNow - timestamp.GetDateTimeOffset() < TimeSpan.FromSeconds(15);
        return WithSpace(new(available, mounted, alive, fresh && (alive || !mounted), alive ? state.TryGetProperty("openHandles", out var h) ? h.GetInt32() : null : 0,
            working, memory, state.TryGetProperty("storage", out var s) ? s.Deserialize<OverlayStorageObservation>(Json) : null,
            snapshotObservation?.FailureCode ?? (!fresh ? "OverlayUnresponsive" : state.TryGetProperty("error", out var e) ? e.GetString() :
                state.TryGetProperty("lastFailureCode", out var failure) ? failure.GetString() : null)), w, control);
    }

    public async Task<SnapshotDeltaSeal> SealAsync(WorkspaceRecord w, string control, CancellationToken ct = default)
    {
        var request = await Request(w, control, false, ct) ?? throw new WorkspaceException("OverlayRequestMissing", "Preserve the delta without a request.");
        var observed = await WinFspOverlayClient.ObserveAsync(request, ct);
        OverlayStorageObservation? storage;
        if (observed.GetProperty("processAlive").GetBoolean())
        {
            if (w.Snapshot?.Mount is null) throw new WorkspaceException("OverlayLeaseMissing", "Reconnect the owned provider before sealing.");
            await MountProvider(control).FlushAsync(w.Snapshot.Mount, ct);
            var flushed = await WindowsProcessWorkspaceProvider.ReadAsync<JsonElement>(Path.Combine(request.ControlPath, "flush-result.json"), ct);
            storage = flushed.GetProperty("storage").Deserialize<OverlayStorageObservation>(Json);
        }
        else storage = observed.GetProperty("state").GetProperty("storage").Deserialize<OverlayStorageObservation>(Json);
        string journal = Path.Combine(request.DeltaPath, "delta.journal");
        WindowsProcessWorkspaceProvider.CheckPath(journal);
        await using var stream = new FileStream(journal, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        if (storage is null || stream.Length != storage.JournalBytes) throw new WorkspaceException("DeltaChangedAfterCollection", "Journal length and flush receipt differ; preserve it.");
        return new(storage.DurableSequence, stream.Length, Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, ct)));
    }
    public async Task UnmountAsync(WorkspaceRecord w, string control, CancellationToken ct = default)
    {
        var request = await Request(w, control, false, ct) ?? throw new WorkspaceException("OverlayRequestMissing", "No owned mount request.");
        await WinFspOverlayClient.StopAsync(request, ct);
    }
    public async Task RemoveDeltaAsync(WorkspaceRecord w, string control, CancellationToken ct = default)
    {
        var result = w.Snapshot?.Result ?? throw new WorkspaceException("OverlayResultRequired", "Independent result is missing.");
        var observed = await ObserveAsync(w, control, ct);
        if (observed is not { Mounted: false, ProviderAlive: false, OwnershipVerified: true, OpenHandles: 0, WorkingSetBytes: 0, PrivateBytes: 0 })
            throw new WorkspaceException("OverlayCleanupIncomplete", "Observe provider and mount absence before delta removal.");
        string delta = DeltaPath(control), intentPath = Path.Combine(control, "delta-reclaim.json");
        WindowsProcessWorkspaceProvider.CheckPath(delta);
        var intent = await WindowsProcessWorkspaceProvider.ReadAsync<SnapshotResult>(intentPath, ct);
        if (intent is null)
        {
            if (await SealAsync(w, control, ct) != result.Seal) throw new WorkspaceException("DeltaChangedAfterCollection", "Journal changed after the result was sealed.");
            await WindowsProcessWorkspaceProvider.WriteAsync(intentPath, result, ct);
        }
        else if (intent != result) throw new WorkspaceException("OverlayResultRequired", "Reclamation intent differs from the retained result.");
        if (File.Exists(delta)) throw new WorkspaceException("UnownedDeltaFiles", "A file replaced the owned delta directory; preserve it.");
        if (!Directory.Exists(delta)) return;
        foreach (string entry in Directory.EnumerateFileSystemEntries(delta))
        {
            WindowsProcessWorkspaceProvider.CheckPath(entry);
            if (Directory.Exists(entry) || Path.GetFileName(entry) is not ("identity.json" or "delta.journal"))
                throw new WorkspaceException("UnownedDeltaFiles", "Unexpected delta files are retained: " + entry);
        }
        string journal = Path.Combine(delta, "delta.journal");
        if (File.Exists(journal))
        {
            await using (var stream = File.OpenRead(journal))
                if (stream.Length != result.Seal.JournalBytes || Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, ct)) != result.Seal.JournalHash)
                    throw new WorkspaceException("DeltaChangedAfterCollection", "Remaining journal differs from the deletion intent.");
            File.Delete(journal);
        }
        string identity = Path.Combine(delta, "identity.json");
        if (File.Exists(identity)) File.Delete(identity);
        Directory.Delete(delta, false);
    }
    public async Task ReleaseSnapshotAsync(WorkspaceRecord w, string control, CancellationToken ct = default)
    {
        if (Directory.Exists(DeltaPath(control)) || File.Exists(DeltaPath(control)) || Directory.Exists(w.Path) || File.Exists(w.Path) || w.Snapshot?.Result is not { IndependentOfSnapshot: true })
            throw new WorkspaceException("SnapshotInUse", "Owned delta or mount remains, or independent result is missing.");
        string directory = Path.Combine(control, "snapshot");
        await WindowsProcessWorkspaceProvider.WriteAsync(Path.Combine(directory, "release-authorized.json"),
            new { snapshotId = w.Snapshot.Reference!.Id, remainingDependents = 0, resultsPreserved = true }, ct);
        await Provider(w, control).ReleaseAsync(w.Snapshot.Reference, ct);
    }
    private static async Task<OverlayServeRequest?> Request(WorkspaceRecord w, string control, bool create, CancellationToken ct)
    {
        string path = RequestPath(control);
        var request = await WindowsProcessWorkspaceProvider.ReadAsync<OverlayServeRequest>(path, ct);
        if (request is null && create)
        {
            Directory.CreateDirectory(OverlayControl(control));
            request = new(Identity(w), DeltaPath(control), w.Path, OverlayControl(control), Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32)));
            await WindowsProcessWorkspaceProvider.WriteAsync(path, request, ct);
        }
        if (request is not null && (JsonSerializer.Serialize(request.Identity) != JsonSerializer.Serialize(Identity(w)) ||
            request.DeltaPath != DeltaPath(control) || request.MountPath != w.Path || request.ControlPath != OverlayControl(control)))
            throw new WorkspaceException("OverlayIdentityMismatch", "Persisted provider request differs from the workspace.");
        return request;
    }
    private static void CheckSpace(string path)
    {
        if (new DriveInfo(Path.GetPathRoot(Path.GetFullPath(path))!).AvailableFreeSpace < 2L * 1024 * 1024 * 1024)
            throw new WorkspaceException("SnapshotHeadroomRequired", "Retain at least 2 GiB of free host space before creating or reconnecting a workspace.");
    }
    private static SnapshotWorkspaceObservation WithSpace(SnapshotWorkspaceObservation observed, WorkspaceRecord w, string control)
    {
        long free = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(control))!).AvailableFreeSpace;
        return observed with
        {
            HostFreeBytes = free,
            DeltaPresent = Directory.Exists(DeltaPath(control)) || File.Exists(DeltaPath(control)),
            JournalRemainingBytes = observed.Storage is { } storage ? Math.Max(0, w.Snapshot!.MaxJournalBytes - storage.JournalBytes) : null,
            SnapshotStorage = w.Snapshot?.Reference is { } snapshot ? new(snapshot.Id, observed.SnapshotAvailable,
                observed.SnapshotAvailable switch { true => null, false => "SnapshotMissing", null => "SnapshotObservationUnknown" }, null, null) : null,
            FailureCode = observed.FailureCode ?? (free < 2L * 1024 * 1024 * 1024 ? "SnapshotHeadroomRequired" : null)
        };
    }
    private sealed record InputCheck(int Version, string SnapshotId, int Entries, long ElapsedMilliseconds, long PayloadBytesRead);
}
