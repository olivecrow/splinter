using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Splinter.Core;
using Splinter.Runtime;

namespace Splinter.Platform.Windows;

// Private, current-version Windows adapter protocol. No caller-supplied code,
// executable, privileged destination, or snapshot receipt crosses this boundary.
internal sealed record StorageAuthorityRequest(int Version, string RequestId, string Operation,
    string OwnerId, string SourcePath, string HomePath, string? SnapshotId,
    VssVerificationRequest Verification, StorageReleaseRequest? Release);
internal sealed record StorageReleaseRequest(string RequestId, string OwnerId, SnapshotReference Snapshot,
    DateTimeOffset RequestedAt, DateTimeOffset? BootTimeUtc,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] WorkspaceDiscardState? Discard = null);
internal sealed record StorageAuthorityResponse(int Version, string RequestId, string Operation,
    string? ErrorCode, Dictionary<string, JsonElement> Receipts);
internal sealed record StorageGrant(string UserSid, string SourcePath, string HomePath,
    string SourceVolume, string SourceIdentity, string HomeIdentity);
internal sealed record StorageAuthorityConfiguration(int Version, string PowerShellPath, StorageGrant[] Grants);
internal sealed record StorageAuthorityOwner(int Version, string UserSid, string OwnerId, string SourcePath, string HomePath);

internal interface IStorageAuthorityTransport
{
    Task<StorageAuthorityResponse> SendAsync(StorageAuthorityRequest request, CancellationToken ct);
}
internal interface IStorageAuthorityOperations
{
    Task<string?> ExecuteAsync(string operation, string protectedReceipts, string source, string owner, CancellationToken ct);
}

internal static partial class StorageAuthorityProtocol
{
    internal const int Version = 1;
    internal const int MaxRequestBytes = 128 * 1024;
    internal const int MaxResponseBytes = 2 * 1024 * 1024;
    internal const string ServiceName = "SplinterStorageAuthority";
    internal const string PipeName = "Splinter.StorageAuthority.v1";
    internal static string InstallPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Splinter", "StorageAuthority");
    internal static string StatePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "SplinterStorageAuthority");
    internal static readonly string[] ReceiptNames = ["snapshot.json", "live-observation.json", "observation.json", "release-deferred.json", "release-isolation.json"];
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = false,
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    internal static T Decode<T>(ReadOnlySpan<byte> bytes)
    {
        using var doc = JsonDocument.Parse(bytes.ToArray(), new() { MaxDepth = 32 });
        if (!JsonPropertyValidation.HasUniqueProperties(doc.RootElement))
            throw new JsonException("Duplicate property.");
        return doc.RootElement.Deserialize<T>(Json) ?? throw new JsonException("Empty storage authority message.");
    }
    internal static void Validate(StorageAuthorityRequest r)
    {
        if (r.Version != Version) throw Error("StorageServiceVersionMismatch", "Install the matching storage service; preserve existing state.");
        if (r.Operation is not ("Capture" or "Observe" or "Release") || !Id().IsMatch(r.OwnerId ?? "") ||
            !RequestId().IsMatch(r.RequestId ?? "") || r.Verification is null ||
            !RequestId().IsMatch(r.Verification.RequestId ?? "") || r.Verification.OwnerId != r.OwnerId ||
            r.Verification.BootTimeUtc is null || r.Verification.RequestedAt == default ||
            r.SnapshotId is not null && !Guid.TryParse(r.SnapshotId, out _) ||
            r.Verification.SnapshotId is not null && r.Verification.SnapshotId != r.SnapshotId ||
            (r.Operation == "Capture" ? r.SnapshotId is not null : r.SnapshotId is null) ||
            (r.Operation == "Release" ? r.Release is null : r.Release is not null))
            throw Error("StorageRequestInvalid", "Only complete capture, observe and release requests are accepted.");
        string? key = r.Operation == "Release" ? r.Release!.RequestId : r.Verification.RequestId;
        if (key != r.RequestId || r.Release is { } release && (release.OwnerId != r.OwnerId ||
            release.Snapshot.Id != r.SnapshotId || release.Snapshot.SourcePath != r.SourcePath ||
            release.BootTimeUtc != r.Verification.BootTimeUtc || release.RequestedAt == default))
            throw Error("StorageRequestInvalid", "Request, snapshot and observation identities must agree.");
        PlainAbsolute(r.SourcePath); PlainAbsolute(r.HomePath);
        if (r.Release?.Discard is { } discard && (discard.Step != "snapshot" || discard.Seal is null ||
            discard.Intent.Version != 1 || discard.Intent.WorkspaceId != r.OwnerId || discard.Intent.Snapshot != r.Release.Snapshot ||
            discard.Intent.ProjectPath != r.SourcePath || string.IsNullOrWhiteSpace(discard.Intent.IntentKey) ||
            discard.Intent.IntentKey.Length > 128 || string.IsNullOrWhiteSpace(discard.Intent.RequestDigest)))
            throw Error("StorageDiscardNotAuthorized", "A fixed explicit-discard intent and seal must match the owned snapshot.");
    }
    internal static void PlainAbsolute(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path) || path.StartsWith("\\\\", StringComparison.Ordinal) ||
            Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar) != path ||
            path.Skip(2).Contains(':') || path.Split(Path.DirectorySeparatorChar).Any(p => p.EndsWith(' ') || p.EndsWith('.')))
            throw Error("StoragePathRejected", "Use a canonical local path without device aliases or traversal.");
    }
    internal static WorkspaceException Error(string code, string message) => new(code, message);
    [GeneratedRegex("^[A-Za-z0-9_-]{1,128}$", RegexOptions.CultureInvariant)] private static partial Regex Id();
    [GeneratedRegex("^[0-9a-f]{32}$", RegexOptions.CultureInvariant)] private static partial Regex RequestId();
}
