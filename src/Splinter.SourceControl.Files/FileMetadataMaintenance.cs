using System.Text.Json;
using Splinter.Core;
using Splinter.Runtime;

namespace Splinter.SourceControl.Files;

public sealed record MaintenanceResidualEntry(string Path, string Reason);
public sealed record MaintenanceResidualPage(MetadataTreeRoot Root, MaintenanceResidualEntry[] Entries, MetadataCursor? NextCursor);

public sealed partial class FileWorkspaceProvider
{
    public bool SupportsMetadataMaintenance => contentPins is not null;
    private static string MaintenanceActive(string control) => Path.Combine(control, "maintenance-active.json");
    private static string MaintenanceReceipt(string control, string key) => Path.Combine(control, "maintenance", WorkspaceStore.Key(key) + ".json");
    public static MaintenanceResidualPage ReadMaintenanceResiduals(WorkspaceRecord w, string control, string requestKey,
        string storeScope, MetadataCursor? cursor = null, int pageSize = 128)
    {
        if (pageSize is < 1 or > 1024 || string.IsNullOrWhiteSpace(requestKey) || requestKey.Length > 128)
            throw new WorkspaceException("MetadataCursorInvalid", "Use the exact maintenance request and a bounded page size.");
        control = Path.GetFullPath(control);
        string records = Path.GetDirectoryName(control) ?? throw Error("The maintenance control directory has no registry.");
        string home = Path.GetDirectoryName(records) ?? throw Error("The maintenance registry has no home.");
        if (Path.GetFileName(records) != "records" || !WorkspaceStore.SamePath(control, Path.Combine(home, "records", w.Id)))
            throw Error("Read only the selected workspace's maintenance inventory.");
        string path = MaintenanceReceipt(control, requestKey);
        if (!File.Exists(path)) path = MaintenanceActive(control);
        var state = Read<WorkspaceMaintenanceResult>(path); ValidateMaintenance(w, state, home, control);
        if (state.RequestKey != requestKey) throw new WorkspaceException("MetadataCursorInvalid", "This request has no recorded maintenance inventory.");
        var stage = state.Stages.SingleOrDefault(value => value.Scope == storeScope)
            ?? throw new WorkspaceException("MetadataCursorInvalid", "Choose one of the fixed store scopes owned by this maintenance request.");
        if (stage.PreservedEntries == 0 || stage.PreservedDirectory is null || stage.PreservedRoot is null)
            throw new WorkspaceException("MetadataCursorInvalid", "This store has no protected metadata residue.");
        if (Path.GetFileName(stage.PreservedDirectory) != stage.PreservedDirectory || !stage.PreservedDirectory.StartsWith("collection-preserved-", StringComparison.Ordinal))
            throw Error("Preserve a malformed protected maintenance location.");
        string directory = Path.Combine(stage.Store!, stage.PreservedDirectory);
        using var pages = new PagedMetadataStore(directory); var page = pages.Open(stage.PreservedRoot).ReadPage(cursor, pageSize);
        return new(page.Root, page.Entries.Select(entry => new MaintenanceResidualEntry(entry.Key,
            new System.Text.UTF8Encoding(false, true).GetString(entry.Value.Span))).ToArray(), page.NextCursor);
    }
    public WorkspaceMaintenanceResult? ObserveMetadataMaintenance(WorkspaceRecord w, string control)
    {
        RequireMaintenanceOwner(w, control); string path = MaintenanceActive(control);
        if (!File.Exists(path)) return null;
        var value = Read<WorkspaceMaintenanceResult>(path); ValidateMaintenance(w, value); return value;
    }
    public void RequireMetadataMaintenanceComplete(WorkspaceRecord w, string control)
    {
        if (ObserveMetadataMaintenance(w, control) is { } pending) throw MaintenancePending(w, pending);
        RequireNoForeignCollection(w, control);
    }
    public async Task<WorkspaceMaintenanceResult?> MaintainMetadataIfNeededAsync(WorkspaceRecord w, string control, string requestKey, CancellationToken ct = default)
    {
        RequireMaintenanceOwner(w, control);
        if (ObserveMetadataMaintenance(w, control) is { } pending)
        {
            if (pending.RequestKey != requestKey || pending.Scope != "all") throw MaintenancePending(w, pending);
            return await MaintainMetadataAsync(w, control, requestKey, "all", ct);
        }
        bool needed = false;
        foreach (string scope in new[] { "workspace-history", "workspace-original", "workspace-applied", "workspace-results", "shared", "content", "ranges" })
        {
            string directory = MaintenanceDirectory(control, scope);
            if (!Directory.Exists(directory)) continue;
            if (PagedMetadataStore.PendingCollection(directory) is not null || PagedMetadataStore.AppendedBytesSinceCollection(directory) >= 4L * 1024 * 1024)
            { needed = true; break; }
        }
        return needed ? await MaintainMetadataAsync(w, control, requestKey, "all", ct) : null;
    }
    public Task<WorkspaceMaintenanceResult> MaintainMetadataAsync(WorkspaceRecord w, string control, string requestKey,
        string scope = "all", CancellationToken ct = default)
    {
        RequireMaintenanceOwner(w, control);
        if (contentPins is null) throw new WorkspaceException("MetadataMaintenanceUnsupported", "Compose native container ownership before reclaiming metadata.");
        if (string.IsNullOrWhiteSpace(requestKey) || requestKey.Length > 128 || scope is not ("workspace" or "shared" or "content" or "ranges" or "all"))
            throw new WorkspaceException("MetadataMaintenanceInvalid", "Use a stable request key and workspace, shared, content, ranges or all scope.");
        string receipt = MaintenanceReceipt(control, requestKey), active = MaintenanceActive(control);
        if (File.Exists(receipt))
        {
            var completed = Read<WorkspaceMaintenanceResult>(receipt); ValidateMaintenance(w, completed);
            if (completed.RequestKey != requestKey || completed.Scope != scope || completed.Phase != "completed")
                throw new WorkspaceException("RequestKeyConflict", "This maintenance key belongs to another fixed scope.");
            return Task.FromResult(completed);
        }
        var state = ObserveMetadataMaintenance(w, control);
        if (state is not null && (state.RequestKey != requestKey || state.Scope != scope)) throw MaintenancePending(w, state);
        if (state is null)
        {
            RequireNoForeignCollection(w, control);
            string[] names = scope switch
            { "workspace" => ["workspace-history", "workspace-original", "workspace-applied", "workspace-results"], "all" => ["workspace-history", "workspace-original", "workspace-applied", "workspace-results", "shared", "content", "ranges"], _ => [scope] };
            state = new(1, w.Id, w.RequestDigest!, requestKey, scope, "pending",
                names.Select(name => new WorkspaceMaintenanceStage(name, "pending", MaintenanceDirectory(control, name),
                    w.Id + "/" + WorkspaceStore.Key(requestKey) + "/" + name)).ToArray());
            SaveMaintenance(active, state);
        }
        try
        {
            for (int index = 0; index < state.Stages.Length; index++)
            {
                ct.ThrowIfCancellationRequested(); var stage = state.Stages[index];
                if (stage.Phase is "completed" or "absent") continue;
                MetadataCollectionResult? result;
                if (stage.Scope == "shared")
                {
                    if (!Directory.Exists(stage.Store)) result = null;
                    else { using var pool = new SharedMetadataPool(store.Root, "maintenance", ct, contentPins); result = pool.Collect(stage.RequestKey, ct); }
                }
                else if (stage.Scope == "content") result = new OwnedContentStore(store.Root, contentPins).CollectMetadata(stage.RequestKey, ct);
                else if (stage.Scope == "ranges")
                { using var ranges = new OwnedRangeContentStore(store.Root, contentPins); result = ranges.CollectMetadata(stage.RequestKey, ct); }
                else result = CollectWorkspaceMetadata(w, control, stage, ct);
                state.Stages[index] = new(stage.Scope, result is null ? "absent" : "completed", stage.Store, stage.RequestKey,
                    result?.ReclaimedBytes ?? 0, result?.RetainedBytes ?? 0, result?.PreservedEntries ?? 0,
                    result?.PreservedDirectory, result?.PreservedRoot);
                state = state with { FailureCode = null }; SaveMaintenance(active, state);
            }
            state = state with { Phase = "completed", FailureCode = null }; SaveMaintenance(active, state);
            Directory.CreateDirectory(Path.GetDirectoryName(receipt)!); AtomicFilePublication.Publish(active, receipt);
            return Task.FromResult(state);
        }
        catch (Exception error) when (error is IOException or WorkspaceException or OperationCanceledException or JsonException)
        {
            state = state with { FailureCode = error is WorkspaceException failure ? failure.Code : error is OperationCanceledException ? "Cancelled" : error is JsonException ? "MetadataMaintenanceInvalid" : "MetadataMaintenanceIO" };
            SaveMaintenance(active, state);
            throw MaintenancePending(w, state, error.Message);
        }
    }
    private MetadataCollectionResult? CollectWorkspaceMetadata(WorkspaceRecord w, string control, WorkspaceMaintenanceStage stage, CancellationToken ct)
    {
        string directory = stage.Store!;
        if (!Directory.Exists(directory)) return null;
        var completed = PagedMetadataStore.CollectionReceipt(directory, stage.RequestKey); if (completed is not null) return completed;
        string storeId; using (var pages = new PagedMetadataStore(directory)) storeId = pages.StoreId;
        IEnumerable<MetadataTreeRoot> Roots()
        {
            if (stage.Scope == "workspace-history")
            {
                if (w.ResultHistory.Root is { } results) yield return results;
                if (w.OriginalApplyHistory.Root is { } applies) yield return applies;
            }
            else if (stage.Scope == "workspace-original")
            {
                if (w.OriginalApply is { } current) foreach (var root in MetadataMaintenance.Roots(current)) yield return root;
                foreach (var apply in w.OriginalApplies) foreach (var root in MetadataMaintenance.Roots(apply)) yield return root;
                string plans = Path.Combine(control, "original-plans");
                if (Directory.Exists(plans))
                    foreach (string path in Directory.EnumerateFiles(plans))
                    {
                        if (IsReviewScratch(Path.GetFileName(path))) continue;
                        if (!path.EndsWith(".json", StringComparison.Ordinal))
                            throw Error("Preserve an unknown retained original-plan artifact.");
                        string name = Path.GetFileName(path), digest = name[..^5];
                        var plan = Read<OriginalApplyPlan>(path);
                        if (digest.Length != 64 || digest.Any(c => !char.IsAsciiHexDigit(c) || char.IsUpper(c)) || plan.Version != 3 ||
                            plan.OwnerId != w.Id || plan.RequestDigest != w.RequestDigest || plan.Digest != "sha256:" + digest || plan.ComputeDigest() != plan.Digest)
                            throw Error("Preserve a malformed or foreign retained original-plan artifact.");
                        foreach (var root in MetadataMaintenance.Roots(plan)) yield return root;
                    }
            }
            else if (stage.Scope == "workspace-applied")
            {
                if (w.AppliedPaths is { } applied) yield return applied;
                if (w.AppliedPathNames is { } names) yield return names;
            }
            else if (stage.Scope == "workspace-results")
            {
                foreach (string path in MaintenanceFileHeaders(control))
                {
                    using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
                    using var document = JsonDocument.Parse(input);
                    if (!JsonPropertyValidation.HasUniqueProperties(document.RootElement)) throw Error("Preserve duplicated result metadata root properties.");
                    if (!document.RootElement.TryGetProperty("contentFiles", out _) || !document.RootElement.TryGetProperty("catalog", out _)) continue;
                    var manifest = document.RootElement.Deserialize<FileSourceManifest>(Json) ?? throw Error("A retained result header is empty.");
                    if (manifest.Version != 3 || manifest.OwnerId != w.Id || manifest.RequestDigest != w.RequestDigest ||
                        manifest.SnapshotId != w.Snapshot?.Reference?.Id || manifest.Catalog is null || manifest.Identity != "sha256:" + manifest.Catalog.Children.ContentHash)
                        throw Error("Preserve a foreign or malformed retained result header.");
                    if (manifest.ContentFiles is { } content)
                    {
                        if (content.StoreId != storeId) throw Error("A retained result refers to another range metadata store.");
                        yield return content;
                    }
                }
                string catalog = Path.Combine(directory, "catalog.json");
                if (File.Exists(catalog))
                {
                    using var input = new FileStream(catalog, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
                    using var document = JsonDocument.Parse(input);
                    if (!JsonPropertyValidation.HasUniqueProperties(document.RootElement) || document.RootElement.GetProperty("version").GetInt32() != 1 ||
                        document.RootElement.GetProperty("ownerId").GetString() != w.Id || document.RootElement.GetProperty("requestDigest").GetString() != w.RequestDigest)
                        throw Error("Preserve a foreign composed-content catalog owner.");
                    foreach (var root in MetadataMaintenance.Roots(document.RootElement))
                    { if (root.StoreId != storeId) throw Error("The composed-content catalog refers to another store."); yield return root; }
                }
            }
        }
        IEnumerable<MetadataTreeRoot> OwnedRoots()
        {
            foreach (var root in Roots())
            {
                if (root.StoreId == storeId) yield return root;
                else throw Error("A private maintenance root belongs to another page store.");
            }
        }
        IEnumerable<MetadataEntry> Scratch()
        {
            if (stage.Scope != "workspace-original") yield break;
            string plans = Path.Combine(control, "original-plans"); if (!Directory.Exists(plans)) yield break;
            foreach (string path in Directory.EnumerateFiles(plans))
                if (IsReviewScratch(Path.GetFileName(path))) yield return new("external-original-plans/" + Path.GetFileName(path), "unpublished-review-control-scratch"u8.ToArray());
        }
        return MetadataMaintenance.Collect(directory, OwnedRoots(), stage.RequestKey, contentPins!, ct, Scratch());
    }
    private static bool IsReviewScratch(string name) => System.Text.RegularExpressions.Regex.IsMatch(name,
        "^[a-f0-9]{64}\\.json(?:\\.pending|\\.[a-f0-9]{32}\\.tmp)$");
    private string MaintenanceDirectory(string control, string scope) => MaintenanceDirectory(store.Root, control, scope);
    private static string MaintenanceDirectory(string home, string control, string scope) => scope switch
    {
        "workspace-history" => Path.Combine(control, "history-pages"), "workspace-original" => Path.Combine(control, "original-pages"),
        "workspace-applied" => FilePath(control, "applied-pages"), "workspace-results" => FilePath(control, "result-ranges"),
        "shared" => SharedMetadataPool.Location(control), "content" => Path.Combine(home, "content-store", "pages"),
        "ranges" => Path.Combine(home, "range-store", "pages"), _ => throw Error("Unsupported metadata maintenance scope.")
    };
    private void RequireNoForeignCollection(WorkspaceRecord w, string control)
    {
        foreach (string scope in new[] { "workspace-history", "workspace-original", "workspace-applied", "workspace-results", "shared", "content", "ranges" })
        {
            string directory = MaintenanceDirectory(control, scope);
            if (!Directory.Exists(directory) || PagedMetadataStore.PendingCollection(directory) is not { } pending) continue;
            string[] key = pending.RequestKey.Split('/');
            if (key.Length == 3 && key[0].Length == 24 && key[0].All(char.IsAsciiHexDigit))
            {
                var owner = store.Get(key[0]); string ownerControl = store.ControlPath(owner.Id);
                if (ObserveMetadataMaintenance(owner, ownerControl) is { } request && request.Stages.Any(stage => stage.RequestKey == pending.RequestKey && stage.Store is not null && WorkspaceStore.SamePath(stage.Store, directory)))
                    throw MaintenancePending(owner, request);
            }
            throw new WorkspaceException("MetadataCollectionPending", "Preserve and resume the exact recorded metadata collection before publishing new roots.",
                new { scope, pending.RequestKey, pending.Phase, directory });
        }
    }
    private static IEnumerable<string> MaintenanceFileHeaders(string control)
    {
        string files = Path.Combine(control, "files"); if (!Directory.Exists(files)) yield break;
        foreach (string path in Directory.EnumerateFiles(files, "*.json")) yield return path;
        string generations = Path.Combine(files, "generations");
        if (Directory.Exists(generations)) foreach (string directory in Directory.EnumerateDirectories(generations))
            foreach (string path in Directory.EnumerateFiles(directory, "*.json")) yield return path;
    }
    private void RequireMaintenanceOwner(WorkspaceRecord w, string control)
    {
        if (w.Version != WorkspaceRecord.CurrentVersion || w.Purpose != "task" || string.IsNullOrWhiteSpace(w.RequestDigest) ||
            !WorkspaceStore.SamePath(control, store.ControlPath(w.Id)) || !WorkspaceStorage.HasRangeJournal(w.StorageMode))
            throw Error("Metadata maintenance requires the exact current range workspace owner.");
    }
    private void ValidateMaintenance(WorkspaceRecord w, WorkspaceMaintenanceResult value) => ValidateMaintenance(w, value, store.Root, store.ControlPath(w.Id));
    private static void ValidateMaintenance(WorkspaceRecord w, WorkspaceMaintenanceResult value, string home, string control)
    {
        if (value.Version != 1 || value.OwnerId != w.Id || value.RequestDigest != w.RequestDigest ||
            value.Scope is not ("workspace" or "shared" or "content" or "ranges" or "all") || value.Phase is not ("pending" or "completed") ||
            string.IsNullOrWhiteSpace(value.RequestKey) || value.RequestKey.Length > 128 || value.Stages is null || value.Stages.Length > 7 ||
            value.Stages.Any(stage => stage is null || stage.Phase is not ("pending" or "completed" or "absent")) ||
            value.Phase == "completed" && value.Stages.Any(stage => stage.Phase == "pending"))
            throw Error("Preserve a malformed or foreign metadata maintenance request.");
        string[] expected = value.Scope switch
        { "workspace" => ["workspace-history", "workspace-original", "workspace-applied", "workspace-results"], "all" => ["workspace-history", "workspace-original", "workspace-applied", "workspace-results", "shared", "content", "ranges"], _ => [value.Scope] };
        if (value.Stages.Length != expected.Length) throw Error("Preserve an incomplete maintenance scope plan.");
        for (int index = 0; index < expected.Length; index++)
        {
            var stage = value.Stages[index]; string directory = MaintenanceDirectory(home, control, expected[index]);
            if (stage.Scope != expected[index] || stage.Store is null || !WorkspaceStore.SamePath(stage.Store, directory) ||
                stage.RequestKey != w.Id + "/" + WorkspaceStore.Key(value.RequestKey) + "/" + stage.Scope ||
                stage.ReclaimedBytes < 0 || stage.RetainedBytes < 0 || stage.PreservedEntries < 0 ||
                stage.PreservedEntries == 0 && (stage.PreservedDirectory is not null || stage.PreservedRoot is not null) ||
                stage.PreservedEntries != 0 && (stage.PreservedDirectory is null || stage.PreservedRoot?.Count != stage.PreservedEntries))
                throw Error("Preserve a changed maintenance scope, path, request identity or inventory.");
        }
    }
    private WorkspaceException MaintenancePending(WorkspaceRecord w, WorkspaceMaintenanceResult value, string? reason = null) =>
        new("WorkspaceMaintenancePending", "Resume this exact metadata maintenance request before starting another mutation." + (reason is null ? "" : " " + reason),
            new { value.RequestKey, value.Scope, value.FailureCode }, [new("workspace.maintain", new { id = w.Id, requestKey = value.RequestKey, scope = value.Scope, home = store.Root })]);
    private static void SaveMaintenance(string path, WorkspaceMaintenanceResult value)
    {
        WorkspaceStore.EnsurePlainPath(path); string pending = path + "." + Guid.NewGuid().ToString("N") + ".pending";
        using (var output = new FileStream(pending, FileMode.CreateNew, FileAccess.Write, FileShare.Read)) { JsonSerializer.Serialize(output, value, Json); output.Flush(true); }
        AtomicFilePublication.Publish(pending, path);
    }
}
