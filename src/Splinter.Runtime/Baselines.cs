using System.Security.Cryptography;
using System.Text;
using Splinter.Core;

namespace Splinter.Runtime;

public sealed partial class WorkspaceCoordinator
{
    private IWorkspaceStorageProvider Storage => storage ?? throw new WorkspaceException("CoWUnsupported", "No CoW storage provider is configured.");
    public async Task<WorkspaceView> PrepareBaselineAsync(string project, string reference, CancellationToken ct = default)
    {
        Storage.Probe(store.Root);
        project = WorkspaceStore.CanonicalProject(project);
        string revision = await sources.ResolveRevisionAsync(project, reference, ct);
        string compatibility = applications.CompatibilityKey(project);
        string key = "baseline-" + WorkspaceStore.Key(revision + "\n" + compatibility);
        using var preparation = await store.LockAsync(WorkspaceStore.Key(WorkspaceStore.ProjectKey(project) + ":" + key), ct);
        var records = store.List(project);
        string signature = await sources.SnapshotKeyAsync(project, revision, applications.CacheInputs, ct);
        WorkspaceBaseline? seed = null;
        foreach (var record in records.Where(w => w.Purpose == "baseline" && w.Phase == "released" && Directory.Exists(store.BaselinePath(w.Id))).OrderByDescending(w => w.CreatedAt))
        {
            var candidate = store.GetBaseline(record.Id);
            if (candidate.CompatibilityKey != compatibility) continue;
            if (candidate.BaseRevision == revision)
                return new(record, null, null, store.ControlPath(record.Id), "workspace ensure --storage refs-cow --baseline " + record.Id);
            if (seed is null && await sources.SnapshotKeyAsync(project, candidate.BaseRevision, applications.CacheInputs, ct) == signature) seed = candidate;
        }
        int generation = records.Count(w => w.Purpose == "baseline" && w.RequestKey.StartsWith(key + "-", StringComparison.Ordinal)
            && w.Phase == "released" && !Directory.Exists(store.BaselinePath(w.Id)));
        string request = key + "-" + generation;
        var previous = records.FirstOrDefault(w => w.RequestKey == request);
        var view = await EnsureAsync(project, request, revision, previous?.StorageMode ?? (seed is null ? "git-checkout" : "refs-cow"), true, ct,
            previous?.BaselineId ?? (previous is null ? seed?.Id : null), purpose: "baseline");
        if (view.Workspace.Phase == "released")
        {
            store.GetBaseline(view.Workspace.Id);
            return view with { NextCommand = "workspace ensure --storage refs-cow --baseline " + view.Workspace.Id };
        }
        return view;
    }
    public async Task<WorkspaceBaseline> SealBaselineAsync(string id, CancellationToken ct = default)
    {
        WorkspaceBaseline baseline;
        using (var gate = await store.LockAsync(id, ct))
        using (var baselineGate = await store.LockAsync(WorkspaceStore.Key("baseline:" + id), ct))
        {
            var record = store.Get(id);
            if (record.Purpose != "baseline") throw new WorkspaceException("BaselineMismatch", "Only a dedicated baseline preparation workspace can be sealed.");
            string destination = store.BaselinePath(id);
            if (File.Exists(Path.Combine(Path.GetDirectoryName(destination)!, "baseline.json"))) baseline = store.GetBaseline(id);
            else
            {
                RequireLive(record);
                var view = await ViewAsync(record, ct);
                if (view.Source!.HasChanges || view.Source.Revision != record.BaseRevision || record.Validation is not { Success: true } validation || validation.Fingerprint != view.Source.Fingerprint)
                    throw new WorkspaceException("BaselineValidationRequired", "Baseline source must be unchanged and successfully compiled. Refresh, query status, then seal.");
                await applications.StopAsync(record, store.ControlPath(id), ct);
                var stopped = await applications.ObserveAsync(record, store.ControlPath(id), ct);
                if (stopped.Resources is not { OwnershipVerified: true, ActiveProcesses: 0 })
                    throw new WorkspaceException("ProcessCleanupIncomplete", "Baseline sealing requires a stopped process group.");
                await applications.RemoveInfrastructureAsync(record, store.ControlPath(id), ct);
                if ((await sources.InspectAsync(record, ct)).HasChanges)
                    throw new WorkspaceException("BaselineSourceChanged", "Editor exit changed source. Review it in a separate source commit and prepare a new baseline.");
                if (Directory.Exists(destination)) throw new WorkspaceException("BaselineSealIncomplete", "A partial baseline is retained. Inspect it before removing only that derived cache and retrying seal.");
                string[] excluded = [".git", "Temp", "Logs", "UserSettings", "obj", ".vs", ".idea",
                    .. Directory.EnumerateFiles(record.Path).Select(Path.GetFileName).OfType<string>().Where(name => name.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".sln", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase))];
                var clone = await Storage.CloneAsync(record.Path, destination, excluded, ct);
                baseline = new(id, record.ProjectPath, record.BaseRevision!, applications.CompatibilityKey(record.Path), destination,
                    DateTimeOffset.UtcNow, clone, BaselineManifest.Hash(destination));
                store.SaveBaseline(baseline);
            }
        }
        // Separate operation lock; a retry recovers if cache sealing succeeded
        // but Git removal was interrupted. No second Library import is needed.
        await ReleaseAsync(id, ct);
        return baseline;
    }
    public async Task ReleaseBaselineAsync(string id, CancellationToken ct = default)
    {
        using var gate = await store.LockAsync(WorkspaceStore.Key("baseline:" + id), ct);
        string path = store.BaselinePath(id);
        if (!Directory.Exists(path)) return;
        if (store.List().Any(w => w.Phase != "released" && (w.BaselineId == id || w.Id == id)))
            throw new WorkspaceException("BaselineInUse", "Release its preparation and consuming workspaces before removing this reusable cache.");
        var record = store.GetBaseline(id); // Hash and path ownership check before deletion.
        if (!WorkspaceStore.SamePath(path, record.Path)) throw new WorkspaceException("UnsafePath", "Baseline deletion path mismatch.");
        Directory.Delete(path, true);
        // Keep the small baseline record for audit; no source or process remains.
    }
}

internal static class BaselineManifest
{
    public static string Hash(string root)
    {
        WorkspaceStore.EnsurePlainPath(root);
        var pending = new Stack<string>(); pending.Push(root); var files = new List<string>();
        while (pending.TryPop(out string? directory))
        {
            foreach (string path in Directory.EnumerateFileSystemEntries(directory))
            {
                WorkspaceStore.EnsurePlainPath(path);
                if (Directory.Exists(path)) pending.Push(path); else files.Add(path);
            }
        }
        using var digest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (string file in files.Order(StringComparer.Ordinal))
        {
            digest.AppendData(Encoding.UTF8.GetBytes(Path.GetRelativePath(root, file).Replace('\\', '/') + "\0"));
            using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
            digest.AppendData(SHA256.HashData(stream));
        }
        return Convert.ToHexStringLower(digest.GetHashAndReset());
    }
}
