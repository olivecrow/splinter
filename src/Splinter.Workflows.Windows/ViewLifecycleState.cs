using System.Text.Json;
using System.Text.Json.Serialization;
using Splinter.Core;
using Splinter.Runtime;
using Splinter.Platform.Windows;

namespace Splinter.Workflows.Windows;

public sealed record ViewLifecycleRequest(int Version, string Action, string Project,
    ViewOwner Owner, string RequestKey, string? WorkspaceId = null, string? ViewId = null,
    string? CandidateId = null, string? GenerationId = null, ViewTarget? Target = null,
    long MaxJournalBytes = SnapshotWorkspaceState.DefaultJournalBytes, long? MaxEstimatedJournalOverlapBytes = null);
internal sealed record WorkerBinding(int Version, string ProjectId, string WorkspaceId, ViewOwner Owner,
    string CompatibilityKey, string BaseRevision);
internal sealed record ViewBinding(int Version, string ProjectId, ViewOwner Owner, ViewCacheIdentity Cache,
    string MountPath, [property: JsonRequired] string CacheWorkerId, bool Closed = false);
internal sealed record ViewGeneration(int Version, ViewSelectionReceipt Selection, string WorkerId,
    string BaseRevision, WorkspaceRecord Workspace, OverlayServeRequest Request);
internal sealed record ActiveGeneration(int Version, string GenerationId);
internal sealed record CacheMaintenance(int Version, string RequestKey, ViewCacheIdentity Cache,
    long MaxEstimatedJournalOverlapBytes, SnapshotDeltaSeal BeforeSeal, JournalPrivateUsage Before,
    SnapshotJournalCompaction? Compaction = null, JournalPrivateUsage? After = null, bool Completed = false);

internal static class ProductState
{
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true, PropertyNameCaseInsensitive = false, RespectRequiredConstructorParameters = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };
    internal static T? Read<T>(string path, bool journal = false) where T : class
    {
        WorkspaceStore.EnsurePlainPath(path);
        try
        {
            using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            if (input.Length > 16 * 1024 * 1024) throw new JsonException("Receipt exceeds its bound.");
            using var document = JsonDocument.Parse(input);
            if (!JsonPropertyValidation.HasUniqueProperties(document.RootElement))
                throw new JsonException("Duplicate property.");
            var value = document.RootElement.Deserialize<T>(journal ? new JsonSerializerOptions(Json) { PropertyNamingPolicy = null } : Json)
                ?? throw new JsonException("Empty receipt.");
            if (document.RootElement.TryGetProperty(journal ? "Version" : "version", out var version) &&
                version.GetInt32() != (typeof(T) == typeof(ImmutableCandidate) ? 3 : typeof(T) == typeof(OverlayIdentity) ? 2 : 1)) throw new JsonException("Unsupported version.");
            return value;
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException) { return null; }
        catch (JsonException) { throw new WorkspaceException("ViewReceiptInvalid", "Preserve unsupported or corrupt view state: " + path); }
    }
    internal static T Required<T>(string path, bool journal = false) where T : class =>
        Read<T>(path, journal) ?? throw new WorkspaceException("ViewReceiptMissing", "Retain and reconcile missing state: " + path);
    internal static void Save<T>(string path, T value)
    {
        WorkspaceStore.EnsurePlainPath(path); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        { JsonSerializer.Serialize(output, value, Json); output.Flush(true); }
        AtomicFilePublication.Publish(temporary, path);
    }
    internal static void Require(bool condition, string code, string message)
    { if (!condition) throw new WorkspaceException(code, message); }
}
