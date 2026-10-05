using Splinter.Core;

namespace Splinter.SourceControl.Files;

public sealed partial class FileWorkspaceProvider
{
    private IOriginalFilePublisher OriginalPublisher(WorkspaceRecord w)
    {
        Require(w);
        if (originalPublisher is null || !(w.SourceKind == "none" && (w.ApplicationKind == "none" || w.StorageMode == "project-snapshot" && projectAccess is not null) ||
            w.SourceKind == "plastic" && w.StorageMode == "plastic-snapshot" && originalGuard is not null))
            throw new WorkspaceException("SourceIntegrationUnsupported", "Conditional original-apply requires the publisher and the qualified editorless application mode.");
        return originalPublisher;
    }
    private static OriginalFileVersion VersionOf(FileSourceEntry e) => new(e.Kind, e.Length, e.Hash, e.Attributes, e.LastWriteUtcTicks);
    private static string[]? Selection(WorkspaceRecord w, string[]? paths)
    {
        if (paths is null) return null;
        var selected = paths.Order(StringComparer.Ordinal).ToArray();
        if (!OriginalApplyState.ValidSelection(selected) || selected.Any(p => !ValidPath(p) || w.ManagedPaths.Any(m =>
            Managed(p, [m]))))
            throw new WorkspaceException("OriginalApplyRequestInvalid", "Select explicit distinct relative source paths, excluding managed and VCS data.");
        return selected;
    }
    private static void RequireSelection(OriginalApplyPlan plan, string[]? paths)
    {
        if (!(plan.SelectedPaths ?? []).SequenceEqual(paths ?? [], StringComparer.Ordinal))
            throw new WorkspaceException("OriginalApplyRequestConflict", "Resume the same fixed full or selected original-apply scope.");
    }
    public Task<OriginalApplyPlan> PlanOriginalAsync(WorkspaceRecord w, CancellationToken ct = default, string[]? paths = null)
        => PlanOriginalCore(w, ct, paths, false);
    private Task<OriginalApplyPlan> PlanOriginalCore(WorkspaceRecord w, CancellationToken ct, string[]? paths, bool projectLeaseHeld)
    {
        var publisher = OriginalPublisher(w);
        using var projectLease = w.StorageMode == "project-snapshot" && !projectLeaseHeld ? projectAccess!.Acquire(w, true, ct) : null;
        Owner(w, Control(w)); VerifyIndependentResult(w, Control(w));
        var selected = Selection(w, paths);
        if (w.OriginalApply is { } existing) { existing.RequireMatch(w); RequireSelection(existing.Plan, selected); return Task.FromResult(existing.Plan); }
        // Admission checks are still explicit; unrelated original content is
        // not required to equal the snapshot or a repository-wide clean tree.
        if (w.SourceKind == "none") PreflightAsync(w, ct).GetAwaiter().GetResult();
        var baseline = ReadManifest(w, FilePath(Control(w), "baseline.json"));
        var result = ReadManifest(w, FilePath(Control(w), "result.json"));
        var before = baseline.Entries.ToDictionary(e => e.Path, StringComparer.OrdinalIgnoreCase);
        var after = result.Entries.ToDictionary(e => e.Path, StringComparer.OrdinalIgnoreCase);
        var included = IncludedChanges(before, after, selected);
        // Structural parents are shared; source companions are supplied by
        // the exact application policy and pinned in the reviewed write set.
        foreach (string path in included.ToArray())
        {
            for (string? parent = Parent(path); parent is not null; parent = Parent(parent))
                if (!before.ContainsKey(parent) && after.ContainsKey(parent)) included.Add(parent);
        }
        var closure = (applicationPolicies ?? ApplicationProjectPolicies.None).Get(w.ApplicationKind)
            .CloseSelection(included, before.ContainsKey, after.ContainsKey);
        if (closure.Paths.Any(p => !ValidPath(p) || Managed(p, w.ManagedPaths)) ||
            closure.Companions.Any(p => !closure.Paths.Contains(p, StringComparer.OrdinalIgnoreCase)) ||
            included.Any(p => !closure.Paths.Contains(p, StringComparer.OrdinalIgnoreCase)))
            throw new WorkspaceException("ApplicationSelectionInvalid", "The application policy must retain the requested source paths and exclude managed data.");
        included = closure.Paths.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var companions = closure.Companions.ToHashSet(StringComparer.OrdinalIgnoreCase);
        using var sourceLease = w.SourceKind == "plastic" ? originalGuard!.Acquire(w, included.Order(StringComparer.Ordinal).ToArray(), ct) : null;
        var originalEntries = Scan(w.ProjectPath, w.ManagedPaths, ct, metadataOnly: true, mount: w.StorageMode == "project-snapshot" ? w.ProjectPath : null, omitPlasticRoot: w.SourceKind == "plastic");
        var originalNames = originalEntries.ToDictionary(e => e.Path, StringComparer.OrdinalIgnoreCase);
        var retainedParents = RetainedParents(originalEntries, before, after, included);
        var changes = new List<OriginalApplyChange>(); var conflicts = new List<string>();
        string rootIdentity = publisher.RootIdentity(w.ProjectPath);
        foreach (string path in included.Order(StringComparer.Ordinal))
        {
            ct.ThrowIfCancellationRequested();
            before.TryGetValue(path, out var b); after.TryGetValue(path, out var a);
            if (b is not null && a is not null && b.Path != a.Path)
            { conflicts.Add(path + ": case-only rename is unsupported"); continue; }
            var bv = b is null ? null : VersionOf(b); var av = a is null ? null : VersionOf(a);
            if (bv?.SameContent(av) == true)
            {
                if (bv.RelevantAttributes != av!.RelevantAttributes) conflicts.Add(path + ": metadata-only change is unsupported");
                if (!companions.Contains(path)) continue;
            }
            if (bv is not null && av is not null && bv.Kind != av.Kind ||
                bv is not null && av is not null && bv.RelevantAttributes != av.RelevantAttributes ||
                (av?.Attributes & 1) == 1 || (bv?.Attributes & 1) == 1 || path.Split('/').Any(p => p.StartsWith(".splinter-apply-", StringComparison.OrdinalIgnoreCase)))
            { conflicts.Add(path + ": unsupported type or metadata change"); continue; }
            var observed = publisher.Observe(w.ProjectPath, path, ct);
            // Recorded revisions synthesize archive/directory attributes and
            // do not encode NTFS indexing policy. Preserve that original bit
            // in both conditional versions; all other metadata guards remain.
            if (w.SourceKind == "plastic" && w.StorageMode == "plastic-snapshot" && observed is not null)
            {
                int indexed = observed.Version.Attributes & (int)FileAttributes.NotContentIndexed;
                if (bv is not null) bv = bv with { Attributes = bv.Attributes | indexed };
                if (av is not null) av = av with { Attributes = av.Attributes | indexed };
            }
            if (originalNames.TryGetValue(path, out var named) && named.Path != path)
                conflicts.Add(path + ": original path casing changed");
            bool sameBasis = bv is null ? observed is null : bv.SameContent(observed?.Version) && bv.RelevantAttributes == observed!.Version.RelevantAttributes;
            bool sameResult = av is null ? observed is null : av.SameContent(observed?.Version) && av.RelevantAttributes == observed!.Version.RelevantAttributes;
            if (!sameBasis && !sameResult) conflicts.Add(path + ": original and result diverged");
            if (bv?.Kind == "directory" && av is null && retainedParents.Contains(path))
                conflicts.Add(path + ": directory contains retained or foreign children");
            changes.Add(new(path, bv, av, observed, sameResult));
        }
        var ordered = changes.OrderBy(c => c.After?.Kind == "directory" ? 0 : c.Before?.Kind == "directory" ? 2 : 1)
            .ThenBy(c => c.Before?.Kind == "directory" && c.After is null ? -c.Path.Count(x => x == '/') : c.Path.Count(x => x == '/'))
            .ThenBy(c => c.Path, StringComparer.Ordinal).ToArray();
        var plan = new OriginalApplyPlan(1, w.Id, w.RequestDigest!, w.BaseRevision!, w.ResultRevision!, w.ProjectPath,
            rootIdentity, ordered, conflicts.Distinct().ToArray(), "") { SourceIdentity = sourceLease?.Identity, SelectedPaths = selected };
        return Task.FromResult(plan with { Digest = plan.ComputeDigest() });
    }
    private static HashSet<string> IncludedChanges(Dictionary<string, FileSourceEntry> before,
        Dictionary<string, FileSourceEntry> after, string[]? selected)
    {
        var selection = selected?.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var included = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string path in before.Keys.Union(after.Keys, StringComparer.OrdinalIgnoreCase))
        {
            bool matches = selection is null;
            if (selection is not null)
                for (string? ancestor = path; ancestor is not null; ancestor = Parent(ancestor))
                    if (selection.Contains(ancestor)) { found.Add(ancestor); matches = true; }
            if (matches && (!before.TryGetValue(path, out var b) || !after.TryGetValue(path, out var a) ||
                !VersionOf(b).SameContent(VersionOf(a)) || b.Attributes != a.Attributes || b.Path != a.Path)) included.Add(path);
        }
        if (selection is not null && found.Count != selection.Count)
            throw new WorkspaceException("OriginalApplyRequestInvalid", "A selected source path is absent from both basis and result.");
        return included;
    }
    private static HashSet<string> RetainedParents(IEnumerable<FileSourceEntry> original,
        Dictionary<string, FileSourceEntry> before, Dictionary<string, FileSourceEntry> after, HashSet<string> included)
    {
        var parents = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in original)
            if (after.ContainsKey(entry.Path) || !before.ContainsKey(entry.Path) || !included.Contains(entry.Path))
                for (string? parent = Parent(entry.Path); parent is not null; parent = Parent(parent))
                    if (!parents.Add(parent)) break; // Its ancestors were already indexed.
        return parents;
    }
    private static string? Parent(string path) => path.LastIndexOf('/') is var slash && slash >= 0 ? path[..slash] : null;
    public async Task<IntegrationResult> ApplyOriginalAsync(WorkspaceRecord w, string intentKey, string expectedPlanDigest, CancellationToken ct = default, string[]? paths = null)
    {
        var publisher = OriginalPublisher(w);
        using var projectLease = w.StorageMode == "project-snapshot" ? projectAccess!.Acquire(w, true, ct) : null;
        Owner(w, Control(w)); VerifyIndependentResult(w, Control(w));
        var selected = Selection(w, paths);
        if (string.IsNullOrWhiteSpace(intentKey) || intentKey.Length > 128)
            throw new WorkspaceException("OriginalApplyRequestInvalid", "Supply the same explicit intent key and exact reviewed plan digest.");
        if (w.OriginalApply is { } existing)
        {
            existing.RequireMatch(w);
            RequireSelection(existing.Plan, selected);
            if (existing.IntentKey != intentKey || existing.Plan.Digest != expectedPlanDigest)
                throw new WorkspaceException("OriginalApplyRequestConflict", "Preserve and resume the exact original-apply intent.");
            if (existing.Completed) return new(w.ResultRevision!, true);
        }
        else
        {
            var plan = await PlanOriginalCore(w, ct, selected, true);
            if (plan.Digest != expectedPlanDigest) throw new WorkspaceException("OriginalApplyPlanChanged", "The original changed after planning; review the latest plan before any write.");
            if (plan.Conflicts.Length != 0) throw new WorkspaceException("OriginalApplyConflict", "The original has conflicts; no original writes were performed.", plan);
            // Re-acquire below before saving intent: no original or durable
            // publication state is written if source/locks changed.
            using var admission = w.SourceKind == "plastic" ? originalGuard!.Acquire(w, plan.Changes.Select(c => c.Path).ToArray(), ct, publishing: true) : null;
            if (admission?.Identity != plan.SourceIdentity) throw new WorkspaceException("OriginalApplyPlanChanged", "The original VCS identity changed after planning.");
            w.OriginalApply = new() { Plan = plan, IntentKey = intentKey, Steps = plan.Changes.Select(_ => new OriginalApplyStep()).ToArray() };
            w.Phase = "integrating";
            store.Save(w, "original-apply-intent");
        }
        using var sourceLease = w.SourceKind == "plastic" ? originalGuard!.Acquire(w, w.OriginalApply.Plan.Changes.Select(c => c.Path).ToArray(), ct, publishing: true) : null;
        if (sourceLease?.Identity != w.OriginalApply.Plan.SourceIdentity) throw new WorkspaceException("OriginalApplyPlanChanged", "Preserve the fixed intent; original VCS identity changed.");
        publisher.Publish(w.OriginalApply, version =>
        {
            string path = FilePath(Control(w), "objects/" + version.Hash);
            // The opened payload is independently hashed by the publisher before
            // publication; do not hydrate the workspace or consult the VSS.
            return Open(path);
        }, () => store.Save(w, "original-apply-progress"), ct);
        w.OriginalApply.RequireMatch(w);
        if (!w.OriginalApply.Completed) throw new WorkspaceException("OriginalApplyPending", "Publication did not complete; preserve its intent and payloads.");
        return new(w.ResultRevision!, false);
    }
}
