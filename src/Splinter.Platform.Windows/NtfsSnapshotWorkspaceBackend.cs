using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using Microsoft.Win32;
using Splinter.Core;
using Splinter.Runtime;
using Splinter.SourceControl.Git;
using Splinter.SourceControl.Files;

namespace Splinter.Platform.Windows;

public sealed class NtfsSnapshotWorkspaceBackend(string runnerCache, IWorkspacePreparationObserver? preparationObserver = null,
    IGeneratedDirectoryDependencies? generatedDirectories = null) : ISnapshotWorkspaceBackend
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static string OverlayControl(string control) => Path.Combine(control, "overlay");
    private static string RequestPath(string control) => Path.Combine(OverlayControl(control), "request.json");
    private static string DeltaPath(string control) => Path.Combine(control, "delta");
    private static ISourceSnapshotProvider Provider(WorkspaceRecord w, string control) =>
        WorkspaceSnapshotBasis.Provider(w.Snapshot!.Reference!, Path.Combine(control, "snapshot"), w.Id);
    private WinFspOverlayMountProvider MountProvider(string control, WorkspaceRecord? workspace = null) => new(RequestPath(control), runnerCache,
        workspace is null || preparationObserver is null ? null : boundary => preparationObserver.ReachedAsync(boundary, workspace, CancellationToken.None));
    public static OverlayIdentity Identity(WorkspaceRecord w) => new(2, w.Id, w.Snapshot!.Reference!, w.Snapshot.MaxJournalBytes, 1024 * 1024,
        w.SourceKind is "none" or "plastic" ? Splinter.SourceControl.Files.FileWorkspaceProvider.VcsMetadata : [".git"],
        DeniedTopLevel: w.SourceKind == "plastic" ? Splinter.SourceControl.Files.FileWorkspaceProvider.VcsMetadata : null,
        Graft: w.GeneratedDirectory is { } generated ? new(generated.RelativePath, generated) : null);

    public async Task<SnapshotReference> CaptureAsync(WorkspaceRecord w, string control, CancellationToken ct = default)
    {
        if (!OperatingSystem.IsWindows()) throw new WorkspaceException("PlatformUnsupported", "NTFS snapshots require Windows.");
        if (new DriveInfo(Path.GetPathRoot(w.ProjectPath)!).DriveFormat != "NTFS")
            throw new WorkspaceException("NtfsVolumeRequired", "Keep the source on an existing NTFS volume.");
        if (Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\WinFsp", "InstallDir", null) is not string)
            throw new WorkspaceException("WinFspMissing", "Install a qualified WinFsp provider before capture; no full-copy fallback is available.");
        CheckSpace(control);
        if (w.StorageMode is "git-snapshot" or "plastic-snapshot")
        {
            if (w.GeneratedDirectoryId is { } generatedId)
                w.GeneratedDirectory = await (generatedDirectories ?? throw new WorkspaceException("GeneratedBasisUnsupported", "Generated basis dependencies are unavailable."))
                    .AttachAsync(generatedId, w, ct);
            if (w.StorageMode == "plastic-snapshot")
                return await new PlasticRevisionSnapshotProvider(Path.Combine(control, "snapshot"), w.Id, w.BaseReference)
                    .CaptureAsync(new(w.ProjectPath, w.Id, Path.Combine(control, "snapshot")), ct);
            if (w.SourceKind != "git" || w.BaseRevision is null) throw new WorkspaceException("GitBasisUnsupported", "Fix a Git project commit before capture.");
            return await new GitObjectSnapshotProvider(Path.Combine(control, "snapshot"), w.Id, w.BaseRevision)
                .CaptureAsync(new(w.ProjectPath, w.Id, Path.Combine(control, "snapshot")), ct);
        }
        // Reject unsupported live input before requesting a privileged capture.
        // Repeat on the immutable tree to close the capture-time race.
        if (!File.Exists(Path.Combine(control, "snapshot", "snapshot.json"))) SnapshotInputChecks.VerifyTree(w.ProjectPath, ct, w.SourceKind is "none" or "plastic", w.SourceKind == "plastic");
        var snapshot = await new VssSnapshotProvider(Path.Combine(control, "snapshot"), w.Id).CaptureAsync(new(w.ProjectPath, w.Id, Path.Combine(control, "snapshot")), ct);
        string probePath = Path.Combine(control, "snapshot", "input-check.json");
        var prior = await WindowsProcessWorkspaceProvider.ReadAsync<InputCheck>(probePath, ct);
        if (prior is null || prior.SnapshotId != snapshot.Id || prior.Version != 1)
        {
            var watch = Stopwatch.StartNew();
            var access = await WorkspaceSnapshotBasis.ResolveAsync(snapshot, Path.Combine(control, "snapshot"), w.Id, null, ct);
            int entries = SnapshotInputChecks.VerifyTree(access.RootPath, ct, w.SourceKind is "none" or "plastic", w.SourceKind == "plastic");
            await WindowsProcessWorkspaceProvider.WriteAsync(probePath, new InputCheck(1, snapshot.Id, entries, watch.ElapsedMilliseconds, 0), ct);
        }
        return snapshot;
    }

    public async Task VerifyConnectionAsync(WorkspaceRecord w, string control, CancellationToken ct = default)
    {
        var request = await Request(w, control, ct);
        var provider = Provider(w, control);
        var snapshot = w.Snapshot!.Reference!;
        if (request is not null)
        {
            var observed = await WinFspOverlayClient.ObserveAsync(request, ct);
            if (observed.TryGetProperty("ownershipVerified", out var owned) && !owned.GetBoolean())
                throw new WorkspaceException("OverlayStartUncertain", "Preserve the unresolved provider launch before verifying a new connection.");
            if (observed.GetProperty("processAlive").GetBoolean())
            {
                var proof = await provider.ObserveAsync(snapshot, ct);
                if (proof.ReadAccess is null || proof.ReadAccess != request.ReadAccess)
                    throw new WorkspaceException(proof.FailureCode ?? "SnapshotIdentityMismatch", "Stop the owned application and provider before changing its verified read access.");
                return;
            }
        }
        string? consumed = request is null ? null : await WinFspOverlayClient.ConsumedVerificationAsync(request, ct);
        var access = await WorkspaceSnapshotBasis.ResolveAsync(snapshot, Path.Combine(control, "snapshot"), w.Id, consumed, ct);
        request = request is null
            ? new(Identity(w), DeltaPath(control), w.Path, OverlayControl(control), Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32)),
                access, Path.Combine(control, "snapshot"), w.Id)
            : request with { ReadAccess = access, SnapshotReceiptDirectory = Path.Combine(control, "snapshot"), SnapshotOwnerId = w.Id };
        if (w.GeneratedDirectory is { } generated)
        {
            await (generatedDirectories ?? throw new WorkspaceException("GeneratedBasisUnsupported", "Generated basis dependencies are unavailable.")).AttachAsync(generated.Id, w, ct);
            var generatedAccess = await WorkspaceSnapshotBasis.ResolveAsync(generated.Snapshot, generated.ReceiptDirectory, generated.OwnerId, null, ct);
            request = request with { GeneratedSnapshot = new(generatedAccess, generated.ReceiptDirectory, generated.OwnerId) };
        }
        Directory.CreateDirectory(OverlayControl(control));
        await WindowsProcessWorkspaceProvider.WriteAsync(RequestPath(control), request, ct);
    }

    public async Task<OverlayMount> ConnectAsync(WorkspaceRecord w, string control, CancellationToken ct = default)
    {
        CheckSpace(control);
        var request = await Request(w, control, ct)
            ?? throw new WorkspaceException("SnapshotVerificationRequired", "Verify the connection before mounting its persisted request.");
        return await MountProvider(control, w).MountAsync(request!.Identity, request.DeltaPath, request.MountPath, ct);
    }
    public async Task<SnapshotWorkspaceObservation> ObserveAsync(WorkspaceRecord w, string control, CancellationToken ct = default)
    {
        var snapshotObservation = w.Snapshot?.Reference is { } snapshot ? await Provider(w, control).ObserveAsync(snapshot, ct) : null;
        SnapshotWorkspaceObservation Finish(SnapshotWorkspaceObservation observation) => WithSpace(observation with { SnapshotStorage = snapshotObservation }, w, control);
        bool? available = snapshotObservation is null ? false : snapshotObservation.Available;
        var request = await Request(w, control, ct);
        if (request is null) return Finish(new(available, false, false, true, 0, 0, 0,
            FailureCode: snapshotObservation?.FailureCode ?? (available == true ? null : "SnapshotNotCaptured")));
        var observed = await WinFspOverlayClient.ObserveAsync(request, ct);
        bool alive = observed.GetProperty("processAlive").GetBoolean();
        bool mounted = Directory.Exists(w.Path);
        if (!observed.TryGetProperty("state", out var state))
        {
            bool known = observed.GetProperty("ownershipVerified").GetBoolean() && !alive && !mounted;
            return Finish(new(available, mounted, alive, known, known ? 0 : null, known ? 0 : null, known ? 0 : null,
                FailureCode: snapshotObservation?.FailureCode ?? (known ? "OverlayNotStarted" : "OverlayStartUncertain")));
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
        return Finish(new(available, mounted, alive, fresh && (alive || !mounted), alive ? state.TryGetProperty("openHandles", out var h) ? h.GetInt32() : null : 0,
            working, memory, state.TryGetProperty("storage", out var s) ? s.Deserialize<OverlayStorageObservation>(Json) : null,
            snapshotObservation?.FailureCode ?? (!fresh ? "OverlayUnresponsive" : state.TryGetProperty("error", out var e) ? e.GetString() :
                state.TryGetProperty("lastFailureCode", out var failure) ? failure.GetString() : null)));
    }

    public async Task<SnapshotDeltaSeal> SealAsync(WorkspaceRecord w, string control, CancellationToken ct = default)
    {
        var request = await Request(w, control, ct) ?? throw new WorkspaceException("OverlayRequestMissing", "Preserve the delta without a request.");
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
        long length = stream.Length;
        var timer = Stopwatch.StartNew();
        string hash = Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, ct));
        await RecordRead(control, w, "seal", stream.Position, timer.Elapsed.TotalMilliseconds, ct);
        if (stream.Length != length) throw new WorkspaceException("DeltaChangedAfterCollection", "Journal grew while sealing; preserve it.");
        return new(storage.DurableSequence, length, hash);
    }
    public async Task UnmountAsync(WorkspaceRecord w, string control, CancellationToken ct = default)
    {
        var request = await Request(w, control, ct) ?? throw new WorkspaceException("OverlayRequestMissing", "No owned mount request.");
        await WinFspOverlayClient.StopAsync(request, ct);
    }
    public async Task<SnapshotJournalCompaction> CompactAsync(WorkspaceRecord w, string control, CancellationToken ct = default,
        long? maxEstimatedJournalOverlapBytes = null)
    {
        var observed = await ObserveAsync(w, control, ct);
        if (observed is not { SnapshotAvailable: true, Mounted: false, ProviderAlive: false,
            OwnershipVerified: true, OpenHandles: 0, WorkingSetBytes: 0, PrivateBytes: 0 })
            throw new WorkspaceException("CompactionProviderNotStopped", "Only an observed stopped provider can compact its journal.");
        var request = await Request(w, control, ct) ?? throw new WorkspaceException("OverlayRequestMissing", "Preserve the missing provider request.");
        var health = await Provider(w, control).ObserveAsync(w.Snapshot!.Reference!, ct);
        if (health.Available != true || request.ReadAccess is null)
            throw new WorkspaceException(health.FailureCode ?? "SnapshotVerificationRequired", "Verify the original snapshot read access before compaction.");
        using var tree = WorkspaceSnapshotBasis.OpenTree(request.Identity.Snapshot, request.ReadAccess,
            request.SnapshotReceiptDirectory, request.SnapshotOwnerId, () => health);
        using var generatedLease = request.Identity.Graft is { } g ? WorkspaceSnapshotBasis.AcquireReadLease(g.Basis.Snapshot, g.Basis.ReceiptDirectory, g.Basis.OwnerId) : null;
        using var generatedTree = request.Identity.Graft is { } gt ? WorkspaceSnapshotBasis.OpenTree(gt.Basis.Snapshot, request.GeneratedSnapshot!.ReadAccess, gt.Basis.ReceiptDirectory, gt.Basis.OwnerId) : null;
        ISnapshotTree basisTree = request.Identity.Graft is { } graft ? new GraftedSnapshotTree(tree, generatedTree!, graft) : tree;
        string delta = DeltaPath(control);
        WindowsProcessWorkspaceProvider.CheckPath(delta);
        var result = JournalDeltaStore.CompactDirectory(delta, request.Identity, basisTree,
            maxEstimatedJournalOverlapBytes: maxEstimatedJournalOverlapBytes);
        string journal = Path.Combine(delta, "delta.journal"), previous = Path.Combine(delta, "delta.journal.previous");
        long? oldAllocation = Allocation(previous), newAllocation = Allocation(journal);
        return result with { BeforeAllocatedBytes = oldAllocation, AfterAllocatedBytes = newAllocation,
            PeakTemporaryAllocatedBytes = oldAllocation is not null && newAllocation is not null ? oldAllocation + newAllocation : null };
    }
    public async Task CompleteCompactionAsync(WorkspaceRecord w, string control, CancellationToken ct = default)
    {
        var expected = w.Snapshot?.Compaction?.After ?? throw new WorkspaceException("CompactionIntentMissing", "The prepared compaction receipt is missing.");
        var observed = await ObserveAsync(w, control, ct);
        if (observed is not { SnapshotAvailable: true, Mounted: true, ProviderAlive: true, OwnershipVerified: true, OpenHandles: 0 })
            throw new WorkspaceException("CompactionReconnectIncomplete", "Verify the same mounted provider with no open handles before removing the previous generation.");
        if (await SealAsync(w, control, ct) != expected)
            throw new WorkspaceException("CompactionGenerationMismatch", "The reconnected journal differs from the compacted generation.");
        var request = await Request(w, control, ct) ?? throw new WorkspaceException("OverlayRequestMissing", "The provider request is missing.");
        string delta = DeltaPath(control);
        if (File.Exists(Path.Combine(delta, "compaction-intent.json")))
            JournalDeltaStore.CompleteCompaction(delta, request.Identity);
        else if (File.Exists(Path.Combine(delta, "delta.journal.previous")))
            throw new WorkspaceException("CompactionIntentMissing", "A previous generation remains without its intent.");
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct FileStandardInfo
    {
        public long AllocationSize;
        public long EndOfFile;
        public uint NumberOfLinks;
        [MarshalAs(UnmanagedType.Bool)] public bool DeletePending;
        [MarshalAs(UnmanagedType.Bool)] public bool Directory;
    }
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandleEx(SafeFileHandle file, int fileInformationClass,
        out FileStandardInfo info, uint bufferSize);
    public static long? Allocation(string path)
    {
        if (!File.Exists(path)) return null;
        using var handle = File.OpenHandle(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        return GetFileInformationByHandleEx(handle, 1, out var info, (uint)Marshal.SizeOf<FileStandardInfo>()) ? info.AllocationSize : null;
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
            {
                if (stream.Length != result.Seal.JournalBytes)
                    throw new WorkspaceException("DeltaChangedAfterCollection", "Remaining journal differs from the deletion intent.");
                var timer = Stopwatch.StartNew();
                string hash = Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, ct));
                await RecordRead(control, w, "delta-reclaim-verification", stream.Position, timer.Elapsed.TotalMilliseconds, ct);
                if (hash != result.Seal.JournalHash)
                    throw new WorkspaceException("DeltaChangedAfterCollection", "Remaining journal differs from the deletion intent.");
            }
            File.Delete(journal);
        }
        string identity = Path.Combine(delta, "identity.json");
        if (File.Exists(identity)) File.Delete(identity);
        Directory.Delete(delta, false);
    }
    public async Task<SnapshotReclamation> ReleaseSnapshotAsync(WorkspaceRecord w, string control, CancellationToken ct = default)
    {
        if (Directory.Exists(DeltaPath(control)) || File.Exists(DeltaPath(control)) || Directory.Exists(w.Path) || File.Exists(w.Path) || w.Snapshot?.Result is not { IndependentOfSnapshot: true })
            throw new WorkspaceException("SnapshotInUse", "Owned delta or mount remains, or independent result is missing.");
        string directory = Path.Combine(control, "snapshot");
        await WindowsProcessWorkspaceProvider.WriteAsync(Path.Combine(directory, "release-authorized.json"),
            new { snapshotId = w.Snapshot.Reference!.Id, remainingDependents = 0, resultsPreserved = true }, ct);
        var reclaimed = await Provider(w, control).ReleaseAsync(w.Snapshot.Reference, ct, w.Snapshot.Reclamation?.AttemptId);
        if (reclaimed.Disposition == "released" && w.GeneratedDirectory is { } generated)
            await (generatedDirectories ?? throw new WorkspaceException("GeneratedBasisUnsupported", "Generated basis dependencies are unavailable.")).DetachAsync(generated, w, ct);
        return reclaimed;
    }
    private static async Task<OverlayServeRequest?> Request(WorkspaceRecord w, string control, CancellationToken ct)
    {
        string path = RequestPath(control);
        var request = await WindowsProcessWorkspaceProvider.ReadAsync<OverlayServeRequest>(path, ct);
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
            SnapshotStorage = observed.SnapshotStorage ?? (w.Snapshot?.Reference is { } snapshot ? new(snapshot.Id, observed.SnapshotAvailable,
                observed.SnapshotAvailable switch { true => null, false => "SnapshotMissing", null => "SnapshotObservationUnknown" }, null, null) : null),
            FailureCode = observed.FailureCode ?? (free < 2L * 1024 * 1024 * 1024 ? "SnapshotHeadroomRequired" : null)
        };
    }
    private sealed record InputCheck(int Version, string SnapshotId, int Entries, long ElapsedMilliseconds, long PayloadBytesRead);
    // Diagnostic measurements live outside the delta and are never collection
    // authority. Keep the seal's equality/durability protocol unchanged.
    private static Task RecordRead(string control, WorkspaceRecord workspace, string operation, long bytes,
        double elapsedMilliseconds, CancellationToken ct)
    {
        string path = Path.Combine(control, "storage-reads.jsonl");
        WindowsProcessWorkspaceProvider.CheckPath(path);
        return File.AppendAllTextAsync(path, JsonSerializer.Serialize(new { version = 1, workspaceId = workspace.Id,
            snapshotId = workspace.Snapshot!.Reference!.Id, operation, bytes, elapsedMilliseconds, measuredAt = DateTimeOffset.UtcNow }, Json) + "\n", ct);
    }
}
