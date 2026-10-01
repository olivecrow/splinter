using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Splinter.Core;
using Splinter.Runtime;

namespace Splinter.Platform.Windows;

// One serialized authority writer. Receipts and retry intents are exclusively
// service-owned; client files are never adopted as privileged ownership proof.
internal sealed class StorageAuthorityProcessor(string root, StorageAuthorityConfiguration configuration,
    IStorageAuthorityOperations operations)
{
    private sealed record Intent(StorageAuthorityRequest Request, string Digest);

    internal async Task<StorageAuthorityResponse> ProcessAsync(string peerSid, StorageAuthorityRequest request,
        Func<StorageGrant, StorageAuthorityRequest, IDisposable> authorize, CancellationToken ct)
    {
        StorageAuthorityProtocol.Validate(request);
        var grant = configuration.Grants.SingleOrDefault(g => g.UserSid == peerSid &&
            Same(g.SourcePath, request.SourcePath) && Same(g.HomePath, request.HomePath))
            ?? throw StorageAuthorityProtocol.Error("StorageScopeDenied", "The authenticated user, project and home must have an installed grant.");
        // Production supplies a client-token inspection and pins path identities
        // through the side effect. Tests supply an explicit fake authorization.
        using var authorization = authorize(grant, request);
        string key = Hash(peerSid + "\n" + request.HomePath.ToUpperInvariant() + "\n" + request.OwnerId);
        string directory = Path.Combine(root, "owners", key);
        Directory.CreateDirectory(directory);
        var owner = new StorageAuthorityOwner(1, peerSid, request.OwnerId, request.SourcePath, request.HomePath);
        string ownerPath = Path.Combine(directory, "owner.json");
        var existing = Read<StorageAuthorityOwner>(ownerPath);
        if (existing is not null && existing != owner)
            throw StorageAuthorityProtocol.Error("StorageOwnerMismatch", "Preserve the conflicting service ownership record.");
        if (existing is null) Write(ownerPath, owner);

        string receipts = Path.Combine(directory, "receipts");
        Directory.CreateDirectory(receipts);
        var snapshot = Read<JsonElement?>(Path.Combine(receipts, "snapshot.json"));
        if (request.Operation != "Capture" && (snapshot is null ||
            snapshot.Value.GetProperty("ownerId").GetString() != request.OwnerId ||
            snapshot.Value.GetProperty("snapshot").GetProperty("id").GetString() != request.SnapshotId))
            throw StorageAuthorityProtocol.Error("StorageSnapshotNotOwned", "Only a snapshot captured by this service for this owner can be used.");
        if (request.Release is { } release && snapshot is { } stored &&
            stored.GetProperty("snapshot").Deserialize<SnapshotReference>(StorageAuthorityProtocol.Json) != release.Snapshot)
            throw StorageAuthorityProtocol.Error("StorageSnapshotNotOwned", "Release must match the service's immutable snapshot identity.");

        string digest = Hash(JsonSerializer.Serialize(request, StorageAuthorityProtocol.Json));
        string requests = Path.Combine(directory, "requests"); Directory.CreateDirectory(requests);
        string stem = Path.Combine(requests, request.Operation + "-" + request.RequestId);
        var intent = Read<Intent>(stem + ".json");
        if (intent is not null && intent.Digest != digest)
            throw StorageAuthorityProtocol.Error("StorageRequestConflict", "A request ID cannot be reused with different input.");
        string activePath = Path.Combine(directory, "active.json");
        var active = Read<Intent>(activePath);
        if (active is not null && active.Digest != digest)
            throw StorageAuthorityProtocol.Error("StorageRequestPending", "Retry the retained request before starting another operation.");
        var completed = Read<StorageAuthorityResponse>(stem + ".response.json");
        if (completed is not null)
        {
            if (active is not null) File.Delete(activePath);
            return completed;
        }
        intent ??= new(request, digest);
        Write(stem + ".json", intent); Write(activePath, intent);
        Write(Path.Combine(receipts, "verification-request.json"), request.Verification);
        if (request.Release is not null)
        {
            Write(Path.Combine(receipts, "release-request.json"), request.Release);
            if (request.Release.Discard is { } discard)
                Write(Path.Combine(receipts, "discard-authorized.json"), discard);
            else
                Write(Path.Combine(receipts, "release-authorized.json"), new { snapshotId = request.SnapshotId, remainingDependents = 0, resultsPreserved = true });
        }
        string? error = await operations.ExecuteAsync(request.Operation, receipts, grant.SourcePath, request.OwnerId, ct);
        var output = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (string name in StorageAuthorityProtocol.ReceiptNames)
            if (Read<JsonElement?>(Path.Combine(receipts, name)) is { } receipt) output.Add(name, receipt);
        var response = new StorageAuthorityResponse(1, request.RequestId, request.Operation, error, output);
        if (error is null or "SnapshotReleaseIsolationRequired")
        {
            Write(stem + ".response.json", response);
            File.Delete(activePath);
        }
        else Write(stem + ".attempt.json", response);
        return response;
    }
    internal static bool Same(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    internal static string Hash(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    internal static T? Read<T>(string path)
    {
        // Only this serialized service writes the protected ledger. Finish a
        // complete current-format publication after restart; retain malformed
        // bytes rather than deleting them or treating them as absent.
        string pending = path + ".pending";
        if (File.Exists(pending))
        {
            T recovered;
            using (var stream = new FileStream(pending, FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
            {
                using var bytes = new MemoryStream(); stream.CopyTo(bytes);
                try { recovered = StorageAuthorityProtocol.Decode<T>(bytes.ToArray()); }
                catch (JsonException) { throw StorageAuthorityProtocol.Error("StorageStateIncomplete", "Preserve malformed interrupted service state."); }
                stream.Flush(true);
            }
            AtomicFilePublication.Publish(pending, path);
            return recovered;
        }
        if (!File.Exists(path)) return default;
        return StorageAuthorityProtocol.Decode<T>(File.ReadAllBytes(path));
    }
    internal static void Write<T>(string path, T value)
    {
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(value, StorageAuthorityProtocol.Json);
        string pending = path + ".pending";
        if (File.Exists(pending))
        {
            if (!File.ReadAllBytes(pending).AsSpan().SequenceEqual(bytes))
                throw StorageAuthorityProtocol.Error("StorageStateIncomplete", "Preserve the conflicting interrupted publication.");
        }
        else
        {
            using var stream = new FileStream(pending, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            stream.Write(bytes); stream.Flush(true);
        }
        AtomicFilePublication.Publish(pending, path);
    }
}
