using System.Diagnostics;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using Splinter.Core;

namespace Splinter.Platform.Windows;

// The normal caller never self-elevates. A narrow, inspectable helper request
// is returned when capture/release require an administrator. Its durable receipt
// lets a fresh unprivileged CLI continue without running Unity as administrator.
public sealed class VssSnapshotProvider(string receiptDirectory, string ownerId) : ISourceSnapshotProvider
{
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
    public Task<SnapshotObservation> ObserveAsync(SnapshotReference snapshot, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        bool available = Directory.Exists(snapshot.RootPath);
        return Task.FromResult(new SnapshotObservation(snapshot.Id, available, available ? null : "SnapshotMissing", null, null));
    }
    public async Task ReleaseAsync(SnapshotReference snapshot, CancellationToken cancellationToken = default)
    {
        var receipt = await Read(cancellationToken);
        if (receipt is null || receipt.Snapshot != snapshot || receipt.OwnerId != ownerId)
            throw new WorkspaceException("SnapshotIdentityMismatch", "Release must match the original snapshot receipt.");
        if (!receipt.Released) await Run("Release", snapshot.SourcePath, cancellationToken);
        if (Directory.Exists(snapshot.RootPath)) throw new WorkspaceException("SnapshotReleaseIncomplete", "The owned snapshot is still available.");
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
}
