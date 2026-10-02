using Splinter.Core;

namespace Splinter.SourceControl.Files;

public sealed partial class FileWorkspaceProvider
{
    private IOriginalFilePublisher OriginalPublisher(WorkspaceRecord w)
    {
        Require(w);
        if (originalPublisher is null || w.ApplicationKind != "none")
            throw new WorkspaceException("SourceIntegrationUnsupported", "Conditional original-apply requires the publisher and the qualified editorless application mode.");
        return originalPublisher;
    }
    private static OriginalFileVersion VersionOf(FileSourceEntry e) => new(e.Kind, e.Length, e.Hash, e.Attributes, e.LastWriteUtcTicks);
    public Task<OriginalApplyPlan> PlanOriginalAsync(WorkspaceRecord w, CancellationToken ct = default)
    {
        var publisher = OriginalPublisher(w);
        Owner(w, Control(w)); VerifyIndependentResult(w, Control(w));
        if (w.OriginalApply is { } existing) { existing.RequireMatch(w); return Task.FromResult(existing.Plan); }
        // Admission checks are still explicit; unrelated original content is
        // not required to equal the snapshot or a repository-wide clean tree.
        PreflightAsync(w, ct).GetAwaiter().GetResult();
        var baseline = ReadManifest(w, FilePath(Control(w), "baseline.json"));
        var result = ReadManifest(w, FilePath(Control(w), "result.json"));
        var before = baseline.Entries.ToDictionary(e => e.Path, StringComparer.OrdinalIgnoreCase);
        var after = result.Entries.ToDictionary(e => e.Path, StringComparer.OrdinalIgnoreCase);
        var originalEntries = Scan(w.ProjectPath, [], ct, metadataOnly: true);
        var originalNames = originalEntries.ToDictionary(e => e.Path, StringComparer.OrdinalIgnoreCase);
        var changes = new List<OriginalApplyChange>(); var conflicts = new List<string>();
        string rootIdentity = publisher.RootIdentity(w.ProjectPath);
        foreach (string path in before.Keys.Union(after.Keys, StringComparer.OrdinalIgnoreCase).Order(StringComparer.Ordinal))
        {
            ct.ThrowIfCancellationRequested();
            before.TryGetValue(path, out var b); after.TryGetValue(path, out var a);
            if (b is not null && a is not null && b.Path != a.Path)
            { conflicts.Add(path + ": case-only rename is unsupported"); continue; }
            var bv = b is null ? null : VersionOf(b); var av = a is null ? null : VersionOf(a);
            if (bv?.SameContent(av) == true)
            {
                if (bv.RelevantAttributes != av!.RelevantAttributes) conflicts.Add(path + ": metadata-only change is unsupported");
                continue;
            }
            if (bv is not null && av is not null && bv.Kind != av.Kind ||
                bv is not null && av is not null && bv.RelevantAttributes != av.RelevantAttributes ||
                (av?.Attributes & 1) == 1 || (bv?.Attributes & 1) == 1 || path.Split('/').Any(p => p.StartsWith(".splinter-apply-", StringComparison.OrdinalIgnoreCase)))
            { conflicts.Add(path + ": unsupported type or metadata change"); continue; }
            var observed = publisher.Observe(w.ProjectPath, path, ct);
            if (originalNames.TryGetValue(path, out var named) && named.Path != path)
                conflicts.Add(path + ": original path casing changed");
            bool sameBasis = bv is null ? observed is null : bv.SameContent(observed?.Version) && bv.RelevantAttributes == observed!.Version.RelevantAttributes;
            bool sameResult = av is null ? observed is null : av.SameContent(observed?.Version) && av.RelevantAttributes == observed!.Version.RelevantAttributes;
            if (!sameBasis && !sameResult) conflicts.Add(path + ": original and result diverged");
            if (bv?.Kind == "directory" && av is null && originalEntries.Any(e =>
                e.Path.StartsWith(path + "/", StringComparison.OrdinalIgnoreCase) &&
                (after.ContainsKey(e.Path) || !before.ContainsKey(e.Path))))
                conflicts.Add(path + ": directory contains retained or foreign children");
            changes.Add(new(path, bv, av, observed, sameResult));
        }
        var ordered = changes.OrderBy(c => c.After?.Kind == "directory" ? 0 : c.Before?.Kind == "directory" ? 2 : 1)
            .ThenBy(c => c.Before?.Kind == "directory" && c.After is null ? -c.Path.Count(x => x == '/') : c.Path.Count(x => x == '/'))
            .ThenBy(c => c.Path, StringComparer.Ordinal).ToArray();
        var plan = new OriginalApplyPlan(1, w.Id, w.RequestDigest!, w.BaseRevision!, w.ResultRevision!, w.ProjectPath,
            rootIdentity, ordered, conflicts.Distinct().ToArray(), "");
        return Task.FromResult(plan with { Digest = plan.ComputeDigest() });
    }
    public async Task<IntegrationResult> ApplyOriginalAsync(WorkspaceRecord w, string intentKey, string expectedPlanDigest, CancellationToken ct = default)
    {
        var publisher = OriginalPublisher(w);
        Owner(w, Control(w)); VerifyIndependentResult(w, Control(w));
        if (string.IsNullOrWhiteSpace(intentKey) || intentKey.Length > 128)
            throw new WorkspaceException("OriginalApplyRequestInvalid", "Supply the same explicit intent key and exact reviewed plan digest.");
        if (w.OriginalApply is { } existing)
        {
            existing.RequireMatch(w);
            if (existing.IntentKey != intentKey || existing.Plan.Digest != expectedPlanDigest)
                throw new WorkspaceException("OriginalApplyRequestConflict", "Preserve and resume the exact original-apply intent.");
            if (existing.Completed) return new(w.ResultRevision!, true);
        }
        else
        {
            var plan = await PlanOriginalAsync(w, ct);
            if (plan.Digest != expectedPlanDigest) throw new WorkspaceException("OriginalApplyPlanChanged", "The original changed after planning; review the latest plan before any write.");
            if (plan.Conflicts.Length != 0) throw new WorkspaceException("OriginalApplyConflict", "The original has conflicts; no original writes were performed.", plan);
            w.OriginalApply = new() { Plan = plan, IntentKey = intentKey, Steps = plan.Changes.Select(_ => new OriginalApplyStep()).ToArray() };
            w.Phase = "integrating";
            store.Save(w, "original-apply-intent");
        }
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
