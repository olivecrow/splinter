using System.Text.Json;
using Splinter.Core;

namespace Splinter.Platform.Windows;

public sealed partial class VssSnapshotProvider
{
    private string VerificationPath(string name) => Path.Combine(receiptDirectory, name);

    private async Task<T?> ReadVerificationAsync<T>(string name, CancellationToken ct)
    {
        string path = VerificationPath(name);
        try
        {
            WindowsProcessWorkspaceProvider.CheckPath(path);
            if (File.Exists(path + ".new") || Directory.Exists(receiptDirectory) && Directory.EnumerateFiles(receiptDirectory, name + ".*.tmp").Any())
                throw new WorkspaceException("SnapshotObservationIncomplete", "Preserve the interrupted observation and retry only after reconciling its writer.");
            return await WindowsProcessWorkspaceProvider.ReadAsync<T>(path, ct);
        }
        catch (JsonException) { throw new WorkspaceException("SnapshotObservationCorrupt", "Preserve the invalid observation; it cannot authorize snapshot access."); }
        catch (IOException) { throw new WorkspaceException("SnapshotObservationUnknown", "The observation record cannot be read; retain the workspace."); }
        catch (UnauthorizedAccessException) { throw new WorkspaceException("SnapshotObservationUnknown", "The observation record cannot be read; retain the workspace."); }
    }

    private async Task<SnapshotObservation> ObserveLiveAsync(SnapshotReference snapshot, Receipt receipt, CancellationToken ct)
    {
        try
        {
            if (!CurrentFormat(receipt))
                return new(snapshot.Id, null, "SnapshotObservationCorrupt", null, null);
            var request = await ReadVerificationAsync<VssVerificationRequest>("verification-request.json", ct);
            var live = await ReadVerificationAsync<VssLiveObservation>("live-observation.json", ct);
            return VssSnapshotVerification.Evaluate(snapshot, ownerId, receipt.CaptureIdentity, request, live, await observeBoot(ct), observePath);
        }
        catch (WorkspaceException ex) { return new(snapshot.Id, null, ex.Code, null, null); }
    }

    private async Task PrepareVerificationAsync(string? snapshotId, CancellationToken ct)
    {
        WindowsProcessWorkspaceProvider.CheckPath(receiptDirectory);
        Directory.CreateDirectory(receiptDirectory);
        var boot = await observeBoot(ct) ?? throw new WorkspaceException("SnapshotObservationUnknown", "The current OS boot identity is unavailable.");
        var current = await ReadVerificationAsync<VssVerificationRequest>("verification-request.json", ct);
        if (current is not null && (current.OwnerId != ownerId || current.SnapshotId is not null && current.SnapshotId != snapshotId))
            throw new WorkspaceException("SnapshotIdentityMismatch", "The observation request belongs to another capture.");
        if (current is null || current.BootTimeUtc != boot)
            await WindowsProcessWorkspaceProvider.WriteAsync(VerificationPath("verification-request.json"),
                new VssVerificationRequest(Guid.NewGuid().ToString("N"), ownerId, snapshotId, DateTimeOffset.UtcNow, boot), ct);
    }

    public async Task<SnapshotReadAccess> ResolveReadAccessAsync(SnapshotReference snapshot, string? consumedVerificationId, CancellationToken ct = default)
    {
        var receipt = await Read(ct);
        if (receipt is null || !Matches(receipt, snapshot) || receipt.Released)
            throw new WorkspaceException("SnapshotIdentityMismatch", "Read access requires the exact owned, unreleased capture receipt.");
        RequireCurrentFormat(receipt);
        Directory.CreateDirectory(receiptDirectory);
        using var operation = new FileStream(VerificationPath("verification.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        var observed = await ObserveLiveAsync(snapshot, receipt, ct);
        if (observed.FailureCode is "SnapshotIdentityMismatch" or "SnapshotObservationCorrupt" or "SnapshotObservationIncomplete")
            throw new WorkspaceException(observed.FailureCode, "Preserve conflicting or damaged snapshot evidence.");
        if (observed.Available == false) throw new WorkspaceException("SnapshotMissing", "The original VSS ID is absent; do not recapture this task.");
        if (observed.ReadAccess is { } ready && ready.VerificationId != consumedVerificationId) return ready;
        await PrepareVerificationAsync(snapshot.Id, ct);
        var request = (await ReadVerificationAsync<VssVerificationRequest>("verification-request.json", ct))!;
        if (request.RequestId == consumedVerificationId)
        {
            request = request with { RequestId = Guid.NewGuid().ToString("N"), RequestedAt = DateTimeOffset.UtcNow };
            await WindowsProcessWorkspaceProvider.WriteAsync(VerificationPath("verification-request.json"), request, ct);
        }
        await Run("Observe", snapshot.SourcePath, ct);
        observed = await ObserveLiveAsync(snapshot, receipt, ct);
        if (observed.ReadAccess is not { } access)
            throw new WorkspaceException(observed.FailureCode ?? "SnapshotObservationUnknown", "The exact snapshot could not be verified; preserve its delta.");
        return access;
    }
}
