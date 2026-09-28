using System.Diagnostics;
using System.ComponentModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using Splinter.Core;

namespace Splinter.Platform.Windows;

// The normal caller never self-elevates. A fixed storage-authority request and
// its durable receipt let a fresh unprivileged CLI continue without elevating
// the CLI, Git or Unity.
public sealed partial class VssSnapshotProvider : ISourceSnapshotProvider
{
    private readonly string receiptDirectory, ownerId;
    private readonly Func<SnapshotReference, SnapshotObservation> observePath;
    private readonly Func<CancellationToken, Task<DateTimeOffset?>> observeBoot;
    private readonly Func<string, string, CancellationToken, Task>? runHelper;
    public VssSnapshotProvider(string receiptDirectory, string ownerId)
        : this(receiptDirectory, ownerId, SnapshotAvailabilityProbe.Observe, ct => CurrentBoot.Value.WaitAsync(ct)) { }
    private static readonly Lazy<Task<DateTimeOffset?>> CurrentBoot = new(() => ObserveBootAsync(CancellationToken.None));
    internal VssSnapshotProvider(string receiptDirectory, string ownerId,
        Func<SnapshotReference, SnapshotObservation> observePath, Func<CancellationToken, Task<DateTimeOffset?>> observeBoot,
        Func<string, string, CancellationToken, Task>? runHelper = null)
    { this.receiptDirectory = receiptDirectory; this.ownerId = ownerId; this.observePath = observePath; this.observeBoot = observeBoot; this.runHelper = runHelper; }
    private string ReceiptPath => Path.Combine(receiptDirectory, "snapshot.json");
    public async Task<SnapshotReference> CaptureAsync(SnapshotCapture request, CancellationToken cancellationToken = default)
    {
        if (request.OwnerId != ownerId || Path.GetFullPath(request.ReceiptDirectory) != Path.GetFullPath(receiptDirectory))
            throw new WorkspaceException("SnapshotOwnerMismatch", "Capture must use the persisted owner and receipt directory.");
        var receipt = await Read(cancellationToken);
        if (receipt is null)
        {
            await PrepareVerificationAsync(null, cancellationToken);
            await Run("Capture", request.SourcePath, cancellationToken);
            receipt = await Read(cancellationToken);
        }
        if (receipt is null || receipt.Released || receipt.OwnerId != ownerId ||
            !string.Equals(Path.GetFullPath(receipt.Snapshot.SourcePath), Path.GetFullPath(request.SourcePath), StringComparison.OrdinalIgnoreCase) ||
            !receipt.NoAutoRelease || !string.Equals(receipt.NativeProviderId, "{B5946137-7B9F-4925-AF80-51ABD60B20D5}", StringComparison.OrdinalIgnoreCase))
            throw new WorkspaceException("SnapshotIdentityMismatch", "Capture receipt does not describe the requested persistent system snapshot.");
        var access = await ResolveReadAccessAsync(receipt.Snapshot, null, cancellationToken);
        using var tree = new VssSnapshotTree(receipt.Snapshot, access);
        return receipt.Snapshot;
    }
    public async Task<SnapshotObservation> ObserveAsync(SnapshotReference snapshot, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var observation = observePath(snapshot);
        var receipt = await Read(cancellationToken);
        if (receipt is null) return new(snapshot.Id, null, "SnapshotVerificationRequired", null, null);
        if (!Matches(receipt, snapshot)) return new(snapshot.Id, null, "SnapshotIdentityMismatch", null, null);
        if (!CurrentFormat(receipt)) return new(snapshot.Id, null, "SnapshotObservationCorrupt", null, null);
        if (!receipt.Released) return await ObserveLiveAsync(snapshot, receipt, cancellationToken);
        // A device path can be recycled after deletion. Never call its new tree
        // the original snapshot merely because the old receipt says released.
        if (observation.Available == true || observation.FailureCode == "SnapshotIdentityMismatch")
            return new(snapshot.Id, null, "SnapshotIdentityMismatch", null, null);
        if (observation.Available == false) return observation;
        // ERROR_NOT_READY also means inaccessible/offline, not absent. Only the
        // exact helper's VSS-ID absence proof from this boot can resolve it.
        var proof = await WindowsProcessWorkspaceProvider.ReadAsync<ReleaseObservation>(Path.Combine(receiptDirectory, "observation.json"), cancellationToken);
        if (proof is { Available: false, Released: true, BootTimeUtc: not null } && proof.OwnerId == ownerId &&
            proof.Snapshot == snapshot && await observeBoot(cancellationToken) is { } boot && boot == proof.BootTimeUtc)
            return new(snapshot.Id, false, "SnapshotMissing", null, null);
        return observation;
    }
    public async Task<SnapshotReclamation> ReleaseAsync(SnapshotReference snapshot, CancellationToken cancellationToken = default,
        string? previousAttemptId = null)
    {
        var receipt = await Read(cancellationToken);
        if (receipt is null || !Matches(receipt, snapshot))
            throw new WorkspaceException("SnapshotIdentityMismatch", "Release must match the original snapshot receipt.");
        RequireCurrentFormat(receipt);
        VssReleaseRequest? request = null;
        if (!receipt.Released)
        {
            request = await PrepareReleaseAsync(snapshot, previousAttemptId, cancellationToken);
            // A privileged helper can finish between CLI invocations. Consume
            // its exact attempt once; the next explicit retry gets a new ID.
            var deferred = await ReadDeferredReleaseAsync(snapshot, request, cancellationToken);
            if (deferred is not null) return deferred;
            try { await Run("Release", snapshot.SourcePath, cancellationToken); }
            catch (WorkspaceException ex) when (ex.Code == "SnapshotReleaseIsolationRequired")
            {
                return await ReadDeferredReleaseAsync(snapshot, request, cancellationToken)
                    ?? throw new WorkspaceException("SnapshotReclamationInvalid", "The helper did not publish a matching deferred release observation.");
            }
        }
        var observation = await ObserveAsync(snapshot, cancellationToken);
        if (observation is { Available: null, FailureCode: "SnapshotObservationUnknown" })
        {
            // Missing current-boot evidence requires a fresh read-only VSS query.
            // The regular CLI uses the installed authority without elevating.
            await Run("Observe", snapshot.SourcePath, cancellationToken);
            observation = await ObserveAsync(snapshot, cancellationToken);
        }
        if (observation.Available != false)
            throw new WorkspaceException(observation.Available == true ? "SnapshotReleaseIncomplete" : observation.FailureCode ?? "SnapshotObservationUnknown",
                "The exact owned snapshot must be observed absent before release completes.");
        return new(snapshot.Id, request?.RequestId ?? Guid.NewGuid().ToString("N"), "released", DateTimeOffset.UtcNow, []);
    }
    private bool Matches(Receipt receipt, SnapshotReference snapshot) => receipt.Snapshot == snapshot && receipt.OwnerId == ownerId &&
        receipt.NoAutoRelease && string.Equals(receipt.NativeProviderId, "{B5946137-7B9F-4925-AF80-51ABD60B20D5}", StringComparison.OrdinalIgnoreCase);

    private static async Task<DateTimeOffset?> ObserveBootAsync(CancellationToken ct)
    {
        if (!OperatingSystem.IsWindows()) return null;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        var start = new ProcessStartInfo("pwsh") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (string arg in new[] { "-NoProfile", "-NonInteractive", "-Command",
            "$ErrorActionPreference='Stop'; (Get-CimInstance Win32_OperatingSystem -Property LastBootUpTime).LastBootUpTime.ToUniversalTime().ToString('O')" }) start.ArgumentList.Add(arg);
        using var process = new Process { StartInfo = start };
        try
        {
            process.Start();
            var output = process.StandardOutput.ReadToEndAsync(timeout.Token);
            var error = process.StandardError.ReadToEndAsync(timeout.Token);
            await process.WaitForExitAsync(timeout.Token);
            string value = await output; await error;
            return process.ExitCode == 0 && DateTimeOffset.TryParse(value.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var boot) ? boot : null;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return null; }
        catch (Exception ex) when (ex is Win32Exception or IOException) { return null; }
        finally
        {
            try { if (!process.HasExited) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(CancellationToken.None); } }
            catch (InvalidOperationException) { }
        }
    }
    private async Task<Receipt?> Read(CancellationToken ct) => await WindowsProcessWorkspaceProvider.ReadAsync<Receipt>(ReceiptPath, ct);
    private async Task Run(string action, string source, CancellationToken ct)
    {
        if (runHelper is not null) { await runHelper(action, source, ct); return; }
        if (!OperatingSystem.IsWindows()) throw new WorkspaceException("PlatformUnsupported", "The NTFS snapshot backend requires Windows.");
        await RunAuthorityAsync(action, source, ct);
    }
    private static bool CurrentFormat(Receipt receipt) => receipt.Version == 2 && receipt.CaptureIdentity is not null;
    private static void RequireCurrentFormat(Receipt receipt)
    {
        if (!CurrentFormat(receipt))
            throw new WorkspaceException("SnapshotObservationCorrupt", "A version 2 receipt with capture identity is required. Preserve unsupported or incomplete state.");
    }
    private sealed record Receipt(string OwnerId, SnapshotReference Snapshot, string NativeProviderId, bool NoAutoRelease, bool Released,
        int Version, VssCaptureIdentity? CaptureIdentity);
    private sealed record ReleaseObservation(string? OwnerId, SnapshotReference Snapshot, bool? Available, bool Released, DateTimeOffset? BootTimeUtc);
}
