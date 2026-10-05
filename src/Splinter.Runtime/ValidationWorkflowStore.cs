using System.Text.Json;
using System.Text.Json.Serialization;
using Splinter.Core;

namespace Splinter.Runtime;

public sealed record ValidationCandidateState(CandidatePublicationReceipt Publication,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] ValidationIndependentResult? Result, bool Released);
public sealed record ValidationGenerationState(ValidationSelectionReceipt Selection,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] CandidateValidationEvidence? Evidence,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] SnapshotDeltaSeal? FrozenSeal,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] ValidationIndependentResult? Result, bool Released);
public sealed record ValidationViewState(ValidationCacheIdentity Cache, ValidationOwner Owner,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] string? CurrentGenerationId,
    [property: JsonRequired] bool CacheReleased = false);
public sealed record ValidationOperationState(string Id, ValidationWorkflowRequest Request,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] ValidationArtifact? Artifact,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] ValidationWorkflowResponse? Response);
public sealed record ValidationWorkflowState(int Version, string ProjectId, long Revision,
    Dictionary<string, ValidationCandidateState> Candidates,
    Dictionary<string, ValidationGenerationState> Generations,
    Dictionary<string, ValidationViewState> Views,
    Dictionary<string, ValidationOperationState> Operations);

// Separate ledger for candidate/view operations, also read by workspace reclamation.
// One flushed pending generation can be completed by an explicit same-project
// operation. Reads never publish it; malformed/ambiguous state is preserved.
public sealed class ValidationWorkflowStore(WorkspaceStore workspaces)
{
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = false,
        RespectRequiredConstructorParameters = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public string StatePath(string projectId) => Path.Combine(workspaces.Root, "validation-workflows",
        WorkspaceStore.Key(projectId), "state.json");
    public Task<FileStream> LockAsync(string projectId, CancellationToken ct) =>
        workspaces.LockAsync(WorkspaceStore.Key("validation-workflow:" + projectId), ct);

    public ValidationWorkflowState Read(string projectId, bool recoverPending = false)
    {
        string path = StatePath(projectId), pending = path + ".pending";
        WorkspaceStore.EnsurePlainPath(path); WorkspaceStore.EnsurePlainPath(pending);
        try
        {
            var current = ReadFile(path, projectId);
            var next = ReadFile(pending, projectId);
            if (next is not null)
            {
                if (!recoverPending)
                    throw new WorkspaceException("ValidationStateRecoveryRequired", "Retry the same operation to reconcile the flushed state generation.");
                if (next.Revision != (current?.Revision ?? 0) + 1)
                    throw new WorkspaceException("ValidationStateCorrupt", "Pending state does not follow the visible generation. Preserve both files.");
                if (current is not null) RequireContinuation(current, next);
                AtomicFilePublication.Publish(pending, path);
                return next;
            }
            if (current is not null) return current;
            if (Directory.Exists(Path.GetDirectoryName(path)))
                throw new WorkspaceException("ValidationStateRecoveryRequired", "Orphaned preparation has no complete state. Preserve it.");
            return new(1, projectId, 0, [], [], [], []);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { throw new WorkspaceException("ValidationStateUnavailable", "Cannot inspect or publish state; preserve it and restore access before retrying."); }
    }

    private static ValidationWorkflowState? ReadFile(string path, string projectId)
    {
        try
        {
            using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var document = JsonDocument.Parse(input);
            if (!JsonPropertyValidation.HasUniqueProperties(document.RootElement))
                throw new JsonException("Duplicate state field.");
            var state = document.RootElement.Deserialize<ValidationWorkflowState>(Json)
                ?? throw new JsonException("Empty state.");
            ValidationWorkflowRules.State(state, projectId);
            return state;
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException) { return null; }
        catch (Exception ex) when (ex is JsonException or ArgumentException or NullReferenceException || ex is WorkspaceException)
        { throw new WorkspaceException("ValidationStateCorrupt", "Unsupported, incomplete or inconsistent workflow state. Preserve all generations."); }
    }

    private static void RequireContinuation(ValidationWorkflowState before, ValidationWorkflowState after)
    {
        // A flushed next generation may complete an intent, never forget one or
        // reinterpret an existing source/result. Both files remain on rejection.
        bool valid = before.Operations.All(p => after.Operations.TryGetValue(p.Key, out var next) &&
                p.Value.Id == next.Id && p.Value.Request == next.Request &&
                (p.Value.Artifact is null || p.Value.Artifact == next.Artifact) &&
                (p.Value.Response is null || p.Value.Response == next.Response)) &&
            before.Candidates.All(p => after.Candidates.TryGetValue(p.Key, out var next) &&
                p.Value.Publication == next.Publication && (!p.Value.Released || next.Released) &&
                (p.Value.Result is null || p.Value.Result == next.Result)) &&
            before.Generations.All(p => after.Generations.TryGetValue(p.Key, out var next) &&
                p.Value.Selection == next.Selection && (!p.Value.Released || next.Released) &&
                (p.Value.FrozenSeal is null || p.Value.FrozenSeal == next.FrozenSeal) &&
                (p.Value.Result is null || p.Value.Result == next.Result)) &&
            before.Views.All(p => after.Views.TryGetValue(p.Key, out var next) && p.Value.Cache == next.Cache && p.Value.Owner == next.Owner &&
                (!p.Value.CacheReleased || next.CacheReleased));
        if (!valid) throw new WorkspaceException("ValidationStateCorrupt", "Pending state rewrites existing ownership, intent or preserved output.");
    }

    internal ValidationWorkflowState Save(ValidationWorkflowState state)
    {
        var next = state with { Revision = checked(state.Revision + 1) };
        ValidationWorkflowRules.State(next, next.ProjectId);
        string path = StatePath(next.ProjectId), pending = path + ".pending";
        WorkspaceStore.EnsurePlainPath(path); WorkspaceStore.EnsurePlainPath(pending);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using (var output = new FileStream(pending, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { JsonSerializer.Serialize(output, next, Json); output.Flush(true); }
            AtomicFilePublication.Publish(pending, path);
            return next;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { throw new WorkspaceException("ValidationStateWriteIncomplete", "Keep the state and pending file; retry the same operation after restoring access or space."); }
    }
}
