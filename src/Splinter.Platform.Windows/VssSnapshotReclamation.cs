using Splinter.Core;

namespace Splinter.Platform.Windows;

public sealed partial class VssSnapshotProvider
{
    private sealed record VssReleaseRequest(string RequestId, string OwnerId, SnapshotReference Snapshot,
        DateTimeOffset RequestedAt, DateTimeOffset BootTimeUtc);
    private sealed record VssDeferredRelease(int Version, string RequestId, string OwnerId, SnapshotReference Snapshot,
        DateTimeOffset BootTimeUtc, DateTimeOffset ObservedAt, string Disposition, bool? DeletionAttempted,
        string[] BeforeIds, string[] BlockingIds, string[] MissingOtherIds, SnapshotSharedStorage SharedStorage);

    private async Task<VssReleaseRequest> PrepareReleaseAsync(SnapshotReference snapshot, string? previousAttemptId, CancellationToken ct)
    {
        var boot = await observeBoot(ct) ?? throw new WorkspaceException("SnapshotObservationUnknown", "The current boot is required before snapshot reclamation.");
        var request = await ReadVerificationAsync<VssReleaseRequest>("release-request.json", ct);
        if (request is not null && (request.OwnerId != ownerId || request.Snapshot != snapshot ||
            string.IsNullOrWhiteSpace(request.RequestId) || request.RequestedAt == default || request.RequestedAt > DateTimeOffset.UtcNow.AddMinutes(1)))
            throw new WorkspaceException("SnapshotReclamationInvalid", "Preserve the conflicting release request.");
        if (request is null || request.BootTimeUtc != boot || request.RequestId == previousAttemptId)
        {
            request = new(Guid.NewGuid().ToString("N"), ownerId, snapshot, DateTimeOffset.UtcNow, boot);
            await WindowsProcessWorkspaceProvider.WriteAsync(VerificationPath("release-request.json"), request, ct);
        }
        return request;
    }

    private async Task<SnapshotReclamation?> ReadDeferredReleaseAsync(SnapshotReference snapshot, VssReleaseRequest request, CancellationToken ct)
    {
        var proof = await ReadVerificationAsync<VssDeferredRelease>("release-deferred.json", ct);
        if (proof is null || proof.RequestId != request.RequestId) return null;
        if (proof.Version != 1 || proof.OwnerId != ownerId || proof.Snapshot != snapshot ||
            proof.BootTimeUtc != request.BootTimeUtc || proof.ObservedAt < request.RequestedAt ||
            proof.Disposition != "deferred" || proof.DeletionAttempted is not false || proof.SharedStorage is null ||
            proof.BeforeIds is null || !proof.BeforeIds.Contains(snapshot.Id, StringComparer.OrdinalIgnoreCase) ||
            proof.BeforeIds.Distinct(StringComparer.OrdinalIgnoreCase).Count() != proof.BeforeIds.Length ||
            proof.BlockingIds is null || proof.BlockingIds.Any(id => !proof.BeforeIds.Contains(id, StringComparer.OrdinalIgnoreCase)) ||
            proof.MissingOtherIds is not { Length: 0 })
            throw new WorkspaceException("SnapshotReclamationInvalid", "The deferred release evidence does not match its request or preservation gates.");
        var receipt = (await Read(ct))!;
        var identity = receipt.CaptureIdentity;
        if (identity is null || !string.Equals(identity.SourceVolume, proof.SharedStorage.VolumeId, StringComparison.OrdinalIgnoreCase))
            throw new WorkspaceException("SnapshotReclamationInvalid", "Shared storage must describe the captured source volume.");
        var result = new SnapshotReclamation(snapshot.Id, request.RequestId, "deferred", proof.ObservedAt, proof.BlockingIds, proof.SharedStorage);
        result.RequireMatch(snapshot);
        return result;
    }
}
