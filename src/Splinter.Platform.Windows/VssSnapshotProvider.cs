using System.Diagnostics;
using System.ComponentModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using Splinter.Core;

namespace Splinter.Platform.Windows;

// The normal caller never self-elevates. A narrow, inspectable helper request
// is returned when capture/release require an administrator. Its durable receipt
// lets a fresh unprivileged CLI continue without running Unity as administrator.
public sealed class VssSnapshotProvider : ISourceSnapshotProvider
{
    private readonly string receiptDirectory, ownerId;
    private readonly Func<SnapshotReference, SnapshotObservation> observePath;
    private readonly Func<CancellationToken, Task<DateTimeOffset?>> observeBoot;
    public VssSnapshotProvider(string receiptDirectory, string ownerId)
        : this(receiptDirectory, ownerId, SnapshotAvailabilityProbe.Observe, ObserveBootAsync) { }
    internal VssSnapshotProvider(string receiptDirectory, string ownerId,
        Func<SnapshotReference, SnapshotObservation> observePath, Func<CancellationToken, Task<DateTimeOffset?>> observeBoot)
    { this.receiptDirectory = receiptDirectory; this.ownerId = ownerId; this.observePath = observePath; this.observeBoot = observeBoot; }
    private string ReceiptPath => Path.Combine(receiptDirectory, "snapshot.json");
    public async Task<SnapshotReference> CaptureAsync(SnapshotCapture request, CancellationToken cancellationToken = default)
    {
        if (request.OwnerId != ownerId || Path.GetFullPath(request.ReceiptDirectory) != Path.GetFullPath(receiptDirectory))
            throw new WorkspaceException("SnapshotOwnerMismatch", "Capture must use the persisted owner and receipt directory.");
        var receipt = await Read(cancellationToken);
        if (receipt is null) { await Run("Capture", request.SourcePath, cancellationToken); receipt = await Read(cancellationToken); }
        if (receipt is null || receipt.Released || receipt.OwnerId != ownerId ||
            !string.Equals(Path.GetFullPath(receipt.Snapshot.SourcePath), Path.GetFullPath(request.SourcePath), StringComparison.OrdinalIgnoreCase) ||
            !receipt.NoAutoRelease || !string.Equals(receipt.NativeProviderId, "{B5946137-7B9F-4925-AF80-51ABD60B20D5}", StringComparison.OrdinalIgnoreCase))
            throw new WorkspaceException("SnapshotIdentityMismatch", "Capture receipt does not describe the requested persistent system snapshot.");
        using var tree = new VssSnapshotTree(receipt.Snapshot);
        return receipt.Snapshot;
    }
    public async Task<SnapshotObservation> ObserveAsync(SnapshotReference snapshot, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var observation = observePath(snapshot);
        var receipt = await Read(cancellationToken);
        if (receipt is null) return observation;
        if (!Matches(receipt, snapshot)) return new(snapshot.Id, null, "SnapshotIdentityMismatch", null, null);
        if (!receipt.Released) return observation;
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
    public async Task ReleaseAsync(SnapshotReference snapshot, CancellationToken cancellationToken = default)
    {
        var receipt = await Read(cancellationToken);
        if (receipt is null || !Matches(receipt, snapshot))
            throw new WorkspaceException("SnapshotIdentityMismatch", "Release must match the original snapshot receipt.");
        if (!receipt.Released) await Run("Release", snapshot.SourcePath, cancellationToken);
        var observation = await ObserveAsync(snapshot, cancellationToken);
        if (observation is { Available: null, FailureCode: "SnapshotObservationUnknown" })
        {
            // Older receipts and a reboot require a fresh read-only VSS query.
            // The regular CLI still returns the exact helper without elevating.
            await Run("Observe", snapshot.SourcePath, cancellationToken);
            observation = await ObserveAsync(snapshot, cancellationToken);
        }
        if (observation.Available != false)
            throw new WorkspaceException(observation.Available == true ? "SnapshotReleaseIncomplete" : observation.FailureCode ?? "SnapshotObservationUnknown",
                "The exact owned snapshot must be observed absent before release completes.");
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
        if (!OperatingSystem.IsWindows()) throw new WorkspaceException("PlatformUnsupported", "The NTFS snapshot backend requires Windows.");
        WindowsProcessWorkspaceProvider.CheckPath(receiptDirectory); Directory.CreateDirectory(receiptDirectory);
        using var resource = typeof(VssSnapshotProvider).Assembly.GetManifestResourceStream("Splinter.VssSnapshot.ps1")!;
        using var reader = new StreamReader(resource);
        string script = await reader.ReadToEndAsync(ct);
        string helper = Path.Combine(receiptDirectory, "vss-" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(script)))[..16] + ".ps1");
        if (File.Exists(helper) && await File.ReadAllTextAsync(helper, ct) != script)
            throw new WorkspaceException("SnapshotHelperChanged", "The recorded privilege helper changed; preserve it for inspection.");
        if (!File.Exists(helper)) await File.WriteAllTextAsync(helper, script, ct);
        string[] args = ["-NoProfile", "-NonInteractive", "-File", helper, "-Action", action, "-SourcePath", source, "-OwnerId", ownerId, "-ReceiptDirectory", receiptDirectory];
        using var identity = WindowsIdentity.GetCurrent();
        if (!new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator))
            throw new WorkspaceException("SnapshotPrivilegeRequired", "Run only the supplied VSS helper with administrator rights, then retry the same workspace command.",
                new { action, receiptDirectory, ownerId }, [new("pwsh", new { argumentList = args }, true)]);
        var start = new ProcessStartInfo("pwsh") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true };
        foreach (string arg in args) start.ArgumentList.Add(arg);
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync(ct); var error = process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);
        string errors = await error; await output;
        if (process.ExitCode != 0)
            throw new WorkspaceException(errors.Contains("SnapshotHeadroomRequired", StringComparison.Ordinal) ? "SnapshotHeadroomRequired" : "SnapshotOperationIncomplete",
                "The VSS helper did not complete. Preserve the receipt and inspect its diagnostic.", new { action, receiptDirectory, diagnostic = errors });
    }
    private sealed record Receipt(string OwnerId, SnapshotReference Snapshot, string NativeProviderId, bool NoAutoRelease, bool Released);
    private sealed record ReleaseObservation(string? OwnerId, SnapshotReference Snapshot, bool? Available, bool Released, DateTimeOffset? BootTimeUtc);
}
