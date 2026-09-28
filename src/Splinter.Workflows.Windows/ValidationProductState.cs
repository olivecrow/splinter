using System.Text.Json;
using System.Text.Json.Serialization;
using Splinter.Core;
using Splinter.Runtime;
using Splinter.Platform.Windows;

namespace Splinter.Workflows.Windows;

public sealed record ValidationProductRequest(int Version, string Action, string Project,
    ValidationOwner Owner, string RequestKey, string? WorkspaceId = null, string? ViewId = null,
    string? CandidateId = null, string? GenerationId = null, ValidationTarget? Target = null,
    long MaxJournalBytes = SnapshotWorkspaceState.DefaultJournalBytes);
internal sealed record WorkerBinding(int Version, string ProjectId, string WorkspaceId, ValidationOwner Owner,
    string CompatibilityKey, string BaseRevision);
internal sealed record ViewBinding(int Version, string ProjectId, ValidationOwner Owner, ValidationCacheIdentity Cache,
    string MountPath, bool Closed = false);
internal sealed record ViewGeneration(int Version, ValidationSelectionReceipt Selection, string WorkerId,
    string BaseRevision, WorkspaceRecord Workspace, OverlayServeRequest Request);
internal sealed record ActiveGeneration(int Version, string GenerationId);
internal sealed record ValidationInvocation(int Version, string RequestId, string Fingerprint, long SourceSequence);
internal sealed record ValidationObservation(int Version, ApplicationValidation Validation, long SourceSequence);
internal sealed record WorkerPreservation(int Version, string WorkspaceId, ValidationOwner Owner, SnapshotDeltaSeal Seal);

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
            Unique(document.RootElement);
            var value = document.RootElement.Deserialize<T>(journal ? new JsonSerializerOptions(Json) { PropertyNamingPolicy = null } : Json)
                ?? throw new JsonException("Empty receipt.");
            if (document.RootElement.TryGetProperty(journal ? "Version" : "version", out var version) &&
                version.GetInt32() != (typeof(T) == typeof(ValidationCandidate) ? 2 : 1)) throw new JsonException("Unsupported version.");
            return value;
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException) { return null; }
        catch (JsonException) { throw new WorkspaceException("ValidationReceiptInvalid", "Preserve unsupported or corrupt validation state: " + path); }
    }
    internal static T Required<T>(string path, bool journal = false) where T : class =>
        Read<T>(path, journal) ?? throw new WorkspaceException("ValidationReceiptMissing", "Retain and reconcile missing state: " + path);
    internal static void Save<T>(string path, T value)
    {
        WorkspaceStore.EnsurePlainPath(path); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        { JsonSerializer.Serialize(output, value, Json); output.Flush(true); }
        AtomicFilePublication.Publish(temporary, path);
    }
    private static void Unique(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var p in element.EnumerateObject()) { if (!names.Add(p.Name)) throw new JsonException("Duplicate property."); Unique(p.Value); }
        }
        else if (element.ValueKind == JsonValueKind.Array) foreach (var item in element.EnumerateArray()) Unique(item);
    }
    internal static void Require(bool condition, string code, string message)
    { if (!condition) throw new WorkspaceException(code, message); }
}
