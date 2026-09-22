using System.Text.RegularExpressions;
using Splinter.Core;

namespace Splinter.Platform.Windows;

// Provider-specific evidence stays out of Core and out of immutable delta identity.
internal sealed record VssCaptureIdentity(string SnapshotId, string NativeProviderId, string SourceVolume,
    string SourceRelativePath, DateTimeOffset CapturedAt, bool Persistent, bool Differential, bool NoAutoRelease,
    string Provenance = "capture");
internal sealed record VssVerificationRequest(string RequestId, string OwnerId, string? SnapshotId,
    DateTimeOffset RequestedAt, DateTimeOffset? BootTimeUtc);
internal sealed record VssLiveObservation(string RequestId, string OwnerId, string SnapshotId,
    DateTimeOffset? BootTimeUtc, DateTimeOffset ObservedAt, bool? Available, string? FailureCode,
    VssCaptureIdentity? Identity, string? DeviceObject);

internal static partial class VssSnapshotVerification
{
    internal const string SystemProvider = "{B5946137-7B9F-4925-AF80-51ABD60B20D5}";
    [GeneratedRegex(@"^\\\\\?\\GLOBALROOT\\Device\\HarddiskVolumeShadowCopy[0-9]+$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DevicePattern();
    [GeneratedRegex(@"^\\\\\?\\Volume\{[0-9a-fA-F-]{36}\}\\$", RegexOptions.CultureInvariant)]
    private static partial Regex VolumePattern();

    internal static SnapshotObservation Evaluate(SnapshotReference snapshot, string owner,
        VssCaptureIdentity? captured, VssVerificationRequest? request, VssLiveObservation? live,
        DateTimeOffset? boot, Func<SnapshotReference, SnapshotObservation> observePath)
    {
        SnapshotObservation Unknown(string code = "SnapshotVerificationRequired") => new(snapshot.Id, null, code, null, null);
        if (request is not null && (request.OwnerId != owner || request.SnapshotId is not null && request.SnapshotId != snapshot.Id) ||
            live is not null && (live.OwnerId != owner || live.SnapshotId != snapshot.Id)) return Unknown("SnapshotIdentityMismatch");
        if (request is null || live is null || boot is null ||
            live.RequestId != request.RequestId || string.IsNullOrWhiteSpace(request.RequestId) ||
            live.BootTimeUtc != boot || request.BootTimeUtc != boot || live.ObservedAt < request.RequestedAt)
            return Unknown();
        if (live.Available is null) return Unknown(live.FailureCode ?? "SnapshotObservationUnknown");
        if (live.Available == false) return new(snapshot.Id, false, "SnapshotMissing", null, null);
        if (live.FailureCode is not null || captured is null || live.Identity is null) return Unknown();
        var observed = live.Identity;
        if (!ValidIdentity(snapshot, captured) || !ValidIdentity(snapshot, observed) ||
            !Same(captured.NativeProviderId, observed.NativeProviderId) || !Same(captured.SourceVolume, observed.SourceVolume) ||
            captured.SourceRelativePath != observed.SourceRelativePath || captured.CapturedAt != observed.CapturedAt)
            return Unknown("SnapshotIdentityMismatch");
        if (live.DeviceObject is null || !DevicePattern().IsMatch(live.DeviceObject)) return Unknown("SnapshotIdentityMismatch");
        string root = live.DeviceObject + (captured.SourceRelativePath.Length == 0 ? "\\" : "\\" + captured.SourceRelativePath);
        var path = observePath(snapshot with { RootPath = root });
        if (path.Available != true)
            return Unknown(path.FailureCode == "SnapshotIdentityMismatch" ? path.FailureCode : "SnapshotObservationUnknown");
        return new(snapshot.Id, true, null, null, null) { ReadAccess = new(snapshot.Id, root, request.RequestId) };
    }

    internal static bool ValidIdentity(SnapshotReference snapshot, VssCaptureIdentity identity) =>
        snapshot.Provider == "windows-vss-system" && snapshot.Persistent && snapshot.Differential &&
        identity.SnapshotId == snapshot.Id && Same(identity.NativeProviderId, SystemProvider) &&
        identity.CapturedAt == snapshot.CapturedAt && identity.Persistent && identity.Differential && identity.NoAutoRelease &&
        identity.SourceVolume is not null && identity.SourceRelativePath is not null &&
        VolumePattern().IsMatch(identity.SourceVolume) && ValidRelative(identity.SourceRelativePath) &&
        TryRelative(snapshot.RootPath, out string relative) && relative == identity.SourceRelativePath;

    internal static bool TryRelative(string root, out string relative)
    {
        const string prefix = @"\\?\GLOBALROOT\Device\";
        relative = "";
        if (root is null || !root.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return false;
        int separator = root.IndexOf('\\', prefix.Length);
        string device = separator < 0 ? root : root[..separator];
        if (!DevicePattern().IsMatch(device)) return false;
        relative = separator < 0 ? "" : root[(separator + 1)..];
        return ValidRelative(relative);
    }
    private static bool ValidRelative(string relative) => relative.Length == 0 ||
        !relative.Contains('/') && !relative.Contains(':') && !relative.Contains('\0') &&
        relative.Split('\\').All(part => part.Length > 0 && part is not ("." or "..") && !part.EndsWith('.') && !part.EndsWith(' '));
    private static bool Same(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}
