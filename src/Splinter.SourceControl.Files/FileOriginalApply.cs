using Splinter.Core;
using Splinter.Runtime;
using Splinter.SourceControl.Git;

namespace Splinter.SourceControl.Files;

public sealed partial class FileWorkspaceProvider
{
    private IOriginalFilePublisher OriginalPublisher(WorkspaceRecord w)
    {
        Require(w);
        if (originalPublisher is null || !(w.SourceKind == "none" && (w.ApplicationKind == "none" || w.StorageMode == "project-snapshot" && projectAccess is not null) ||
            w.SourceKind == "plastic" && w.StorageMode == "plastic-snapshot" && originalGuard is not null ||
            w.SourceKind == "git" && w.StorageMode == "git-snapshot" && w.Results.Count > 0))
            throw new WorkspaceException("SourceIntegrationUnsupported", "Conditional original-apply requires the publisher and the qualified editorless application mode.");
        return originalPublisher;
    }
    private static OriginalFileVersion VersionOf(FileSourceEntry e) => new(e.Kind, e.Length, e.Hash, e.Attributes, e.LastWriteUtcTicks, e.UnixMode);
    private SelectionScope? Selection(WorkspaceRecord w, string[]? paths)
    {
        if (paths is null) return null;
        if (paths.Length == 0 || paths.Any(p => !OriginalApplyState.ValidSelectionPath(p) || !ValidPath(p) || Managed(p, w.ManagedPaths)))
            throw new WorkspaceException("OriginalApplyRequestInvalid", "Select explicit distinct relative source paths, excluding managed and VCS data.");
        var work = new FileApplyIndex(FilePath(Control(w), "selection-work"));
        try { return new(work, work.BuildPaths(paths)); }
        catch (WorkspaceException error) when (error.Code is "MetadataStoreInvalid" or "PathIndexInvalid")
        { work.Dispose(); throw new WorkspaceException("OriginalApplyRequestInvalid", "Select distinct supported relative paths; preserve failed selection artifacts."); }
        catch { work.Dispose(); throw; }
    }
    private sealed class SelectionScope(FileApplyIndex work, MetadataTreeRoot root) : IDisposable
    {
        public long Count => root.Count;
        public IEnumerable<string> Paths => work.Paths(root);
        public bool Contains(string path) => work.Contains(root, path);
        public bool ContainsExact(string path) => work.ContainsExactPath(root, path);
        public void Dispose() => work.Dispose();
    }
    private static void RequireSelection(OriginalApplyPlan plan, SelectionScope? paths)
    {
        if (paths is not null && (plan.SelectionCount != paths.Count || plan.EnumerateSelection().Any(path => !paths.ContainsExact(path))))
            throw new WorkspaceException("OriginalApplyRequestConflict", "Resume the same fixed full or selected original-apply scope.");
    }
    public Task<OriginalApplyPlan> PlanOriginalAsync(WorkspaceRecord w, CancellationToken ct = default, string[]? paths = null)
        => PlanOriginalCore(w, ct, paths, false);
    private Task<OriginalApplyPlan> PlanOriginalCore(WorkspaceRecord w, CancellationToken ct, string[]? paths, bool publicationLeasesHeld,
        GenerationReadScope? proof = null)
    {
        var diagnostics = w.Results.LastOrDefault() is { } generation ? ResultGenerationDiagnostics.Open(Control(w), generation.Id) : null;
        using var totalTiming = diagnostics?.Measure("plan.total");
        var publisher = OriginalPublisher(w);
        using var projectLease = w.StorageMode == "project-snapshot" && !publicationLeasesHeld ? projectAccess!.Acquire(w, true, ct) : null;
        using (diagnostics?.Measure("plan.verify-generation")) VerifyOriginalGeneration(w, ct, proof);
        // Apply holds the stopped Editor's exclusive reservation through publication. A
        // live permit still needs a fresh identity and expiry check at the plan boundary.
        using var applicationLease = w.SourceKind == "git" && (!publicationLeasesHeld || w.LiveOriginal is not null)
            ? (applicationPolicies ?? ApplicationProjectPolicies.None).Get(w.ApplicationKind).AcquireOriginal(w) : null;
        using var selected = Selection(w, paths);
        if (w.OriginalApply is { } existing && (w.Results.Count == 0 || !existing.Completed))
        { existing.RequireMatch(w); RequireSelection(existing.Plan, selected); return Task.FromResult(existing.Plan); }
        // Admission checks are still explicit; unrelated original content is
        // not required to equal the snapshot or a repository-wide clean tree.
        if (w.SourceKind == "none") PreflightOriginalRoot(w);
        using var baseline = ReadManifest(w, FilePath(Control(w), w.Results.Count == 0 ? "baseline.json" : "generation-basis.json"));
        using var result = w.Results.Count == 0 ? ReadManifest(w, FilePath(Control(w), "result.json")) : GenerationManifest(w);
        using var work = new FileApplyIndex(FilePath(Control(w), "plan-work"));
        // A selected existing file has no descendants. Use the immutable catalogs
        // directly for that leaf, its parents and application-selected companions.
        // Structural changes and differently spelled selections use the full merge.
        bool single = paths is { Length: 1 } && baseline.Catalog?.CaseSensitive == false && result.Catalog?.CaseSensitive == false;
        using var basisReader = single ? baseline.OpenReader(contentPins) : null;
        using var resultReader = single ? result.OpenReader(contentPins) : null;
        using var history = single && w.AppliedPaths is not null ? new AppliedPathReader(Control(w), w, ct) : null;
        var before = work.Empty;
        var after = work.Empty;
        FileSourceEntry? Before(string path)
        {
            if (!single) return work.Find(before, path);
            return history?.Find(path) is { } applied ? applied.ToEntry() : basisReader!.Find(path);
        }
        FileSourceEntry? After(string path) => single ? resultReader!.Find(path) : work.Find(after, path);
        FileSourceEntry? selectedBefore = single ? Before(paths![0]) : null, selectedAfter = single ? After(paths![0]) : null;
        single = single && selectedBefore?.Kind == "file" && selectedAfter?.Kind == "file" &&
            selectedBefore.Path == paths![0] && selectedAfter.Path == paths[0];
        void IndexFullSources()
        {
            before = work.Build(baseline.EnumerateEntries(), ct);
            before = work.ApplyVersions(before, EnumerateAppliedPaths(Control(w), w.AppliedPaths, w.AppliedPathNames), ct);
            after = work.Build(result.EnumerateEntries(), ct);
        }
        if (!single) IndexFullSources();
        var found = work.Empty;
        IEnumerable<string> DirectChanges()
        {
        IEnumerable<(FileSourceEntry? Before, FileSourceEntry? After)> comparison = single
            ? [(selectedBefore, selectedAfter)] : work.Compare(before, after);
        foreach (var (b, a) in comparison)
        {
            ct.ThrowIfCancellationRequested(); var entry = a ?? b!;
            bool matches = selected is null;
            if (selected is not null)
                for (string? ancestor = entry.Path; ancestor is not null; ancestor = Parent(ancestor))
                    if (selected.Contains(ancestor)) { found = work.AddPath(found, ancestor); matches = true; }
            if (matches && (b is null || a is null || !VersionOf(b).SameContent(VersionOf(a)) || b.Attributes != a.Attributes || b.UnixMode != a.UnixMode || b.Path != a.Path))
                yield return a?.Path ?? b!.Path;
        }
        }
        var included = single ? work.BuildPaths(DirectChanges(), ct) : work.BuildImportedPaths(DirectChanges(), ct);
        if (selected is not null && found.Count != selected.Count)
            throw new WorkspaceException("OriginalApplyRequestInvalid", "A selected source path is absent from both basis and result.");
        var direct = included;
        foreach (string path in work.Paths(direct))
            for (string? parent = Parent(path); parent is not null; parent = Parent(parent))
                if (Before(parent) is null && After(parent) is { } structural) included = work.AddPath(included, structural.Path);
        var closure = (applicationPolicies ?? ApplicationProjectPolicies.None).Get(w.ApplicationKind)
            .CloseSelection(work.Paths(included), path => Before(path) is not null, path => After(path) is not null);
        IEnumerable<string> CheckedClosure()
        {
            foreach (var path in closure.Paths)
            {
                ct.ThrowIfCancellationRequested();
                if (!ValidPath(path) || Managed(path, w.ManagedPaths)) throw new WorkspaceException("ApplicationSelectionInvalid", "Application selection includes managed data.");
                yield return path;
            }
        }
        var closed = work.BuildPaths(CheckedClosure(), ct, coalesce: true);
        IEnumerable<string> CheckedCompanions()
        {
            foreach (var path in closure.Companions)
            {
                ct.ThrowIfCancellationRequested();
                if (!work.Contains(closed, path)) throw new WorkspaceException("ApplicationSelectionInvalid", "A companion is absent from the fixed source selection.");
                yield return path;
            }
        }
        var companions = work.BuildPaths(CheckedCompanions(), ct, coalesce: true);
        if (work.Paths(included).Any(path => !work.Contains(closed, path))) throw new WorkspaceException("ApplicationSelectionInvalid", "The application selection dropped a source path.");
        included = closed;
        using var sourceLease = AcquireOriginalSource(w, work.Paths(included), ct);
        var originalEntries = Scan(w.ProjectPath, w.ManagedPaths, ct, metadataOnly: true, mount: w.StorageMode == "project-snapshot" ? w.ProjectPath : null,
            omitPlasticRoot: w.SourceKind == "plastic", omitGitRoot: w.SourceKind == "git");
        IEnumerable<FileSourceEntry> CheckedOriginalEntries()
        {
            foreach (var entry in originalEntries)
            {
                if (w.SourceKind == "none") RequireNoApplyResources(entry);
                yield return entry;
            }
        }
        bool deletesDirectory = work.Paths(included).Any(path => Before(path)?.Kind == "directory" && After(path) is null);
        bool selectedNames = single && included.Count <= 64 && !deletesDirectory;
        var originalNames = selectedNames ? work.SelectedOriginalNames(CheckedOriginalEntries(), included, ct) : work.Build(CheckedOriginalEntries(), ct);
        var retainedParents = work.Empty;
        if (deletesDirectory)
        {
            // A policy may expand even a single leaf to a directory deletion.
            if (single) IndexFullSources();
            retainedParents = work.RetainedParents(originalNames, before, after, included, ct);
        }
        var conflicts = work.Empty;
        void Conflict(string message) => conflicts = work.AddText(conflicts, message);
        IOriginalFileObservationScope? observation;
        using (diagnostics?.Measure("plan.root-identity"))
            observation = (publisher as IOriginalFileObservationProvider)?.OpenObservation(w.ProjectPath, ct);
        using var observationLifetime = observation;
        string rootIdentity = observation?.ProjectIdentity ?? publisher.RootIdentity(w.ProjectPath);
        IEnumerable<OriginalApplyChange> Changes()
        {
        foreach (string path in work.Paths(included))
        {
            ct.ThrowIfCancellationRequested();
            var b = Before(path); var a = After(path);
            if (b is not null && a is not null && b.Path != a.Path)
            { Conflict(path + ": case-only rename is unsupported"); continue; }
            var bv = b is null ? null : VersionOf(b); var av = a is null ? null : VersionOf(a);
            // NTFS indexing policy belongs to the target path. Atomic saves
            // can drop this bit in a workspace; it is not a source permission
            // change. Preserve every other source metadata comparison below.
            const int indexing = (int)FileAttributes.NotContentIndexed;
            if (bv is not null && av is not null)
                av = av with { Attributes = (av.Attributes & ~indexing) | (bv.Attributes & indexing) };
            if (b is not null && a is not null && b.UnixMode != a.UnixMode || b is null && a?.UnixMode is not (null or 0x1A4))
            { Conflict(path + ": original file publication does not apply Git/Unix mode changes"); continue; }
            if (bv?.SameContent(av) == true)
            {
                if (bv.RelevantAttributes != av!.RelevantAttributes) Conflict(path + ": metadata-only change is unsupported");
                if (!work.Contains(companions, path)) continue;
            }
            if (bv is not null && av is not null && bv.Kind != av.Kind ||
                bv is not null && av is not null && bv.RelevantAttributes != av.RelevantAttributes ||
                (av?.Attributes & 1) == 1 || (bv?.Attributes & 1) == 1 || path.Split('/').Any(p => p.StartsWith(".splinter-apply-", StringComparison.OrdinalIgnoreCase)))
            { Conflict(path + ": unsupported type or metadata change"); continue; }
            OriginalFileObservation? observed;
            using (diagnostics?.Measure("plan.observe-target")) observed = observation is null
                ? publisher.Observe(w.ProjectPath, path, ct) : observation.Observe(path, ct);
            // Bind both versions to the observed target policy, including an
            // explicit clear. Expected identity/attributes remain exact in the
            // fixed digest and conditional publisher guard.
            if (observed is not null)
            {
                int indexed = observed.Version.Attributes & indexing;
                if (bv is not null) bv = bv with { Attributes = (bv.Attributes & ~indexing) | indexed };
                if (av is not null) av = av with { Attributes = (av.Attributes & ~indexing) | indexed };
            }
            bool casingChanged = selectedNames
                ? work.Contains(originalNames, path) && !work.ContainsExactPath(originalNames, path)
                : work.Find(originalNames, path) is { } named && named.Path != path;
            if (casingChanged)
                Conflict(path + ": original path casing changed");
            bool sameBasis = bv is null ? observed is null : bv.SameContent(observed?.Version) && bv.RelevantAttributes == observed!.Version.RelevantAttributes;
            bool sameResult = av is null ? observed is null : av.SameContent(observed?.Version) && av.RelevantAttributes == observed!.Version.RelevantAttributes;
            if (!sameBasis && !sameResult) Conflict(path + ": original and result diverged");
            if (bv?.Kind == "directory" && av is null && work.Contains(retainedParents, path))
                Conflict(path + ": directory contains retained or foreign children");
            yield return new(path, bv, av, observed, sameResult);
        }
        }
        var template = new OriginalApplyPlan(3, w.Id, w.RequestDigest!, w.BaseRevision!, w.ResultRevision!, w.ProjectPath,
            rootIdentity, [], [], "") { SourceIdentity = sourceLease?.Identity,
                ResultId = w.Results.LastOrDefault()?.Id, PreviousApplyDigest = w.OriginalApplies.LastOrDefault()?.Plan.Digest,
                LiveOriginalIdentity = w.LiveOriginal?.Identity };
        var plan = OriginalApplyPages.Create(template, OriginalApplyPages.OrderChanges(Changes(), Control(w), ct), Control(w), ct, contentPins, selected?.Paths);
        plan = OriginalApplyPages.WithConflicts(plan, work.Texts(conflicts), Control(w), contentPins);
        using (diagnostics?.Measure("plan.observation-boundary")) observation?.VerifyBoundary(ct);
        OriginalApplyPages.SavePlan(plan, Control(w));
        return Task.FromResult(plan);
    }
    private static string? Parent(string path) => path.LastIndexOf('/') is var slash && slash >= 0 ? path[..slash] : null;
    public async Task<IntegrationResult> ApplyOriginalAsync(WorkspaceRecord w, string intentKey, string expectedPlanDigest, CancellationToken ct = default, string[]? paths = null)
    {
        var diagnostics = w.Results.LastOrDefault() is { } generation ? ResultGenerationDiagnostics.Open(Control(w), generation.Id) : null;
        using var totalTiming = diagnostics?.Measure("apply.total");
        var publisher = OriginalPublisher(w);
        using var projectLease = w.StorageMode == "project-snapshot" ? projectAccess!.Acquire(w, true, ct) : null;
        using var proof = w.Results.LastOrDefault() is { } latest ? new GenerationReadScope(this, w, Control(w), latest, contentPins, ct) : null;
        using (diagnostics?.Measure("apply.verify-generation")) VerifyOriginalGeneration(w, ct, proof);
        using var applicationLease = w.SourceKind == "git" ? (applicationPolicies ?? ApplicationProjectPolicies.None).Get(w.ApplicationKind).AcquireOriginal(w, true) : null;
        using var selected = Selection(w, paths);
        if (string.IsNullOrWhiteSpace(intentKey) || intentKey.Length > 128)
            throw new WorkspaceException("OriginalApplyRequestInvalid", "Supply the same explicit intent key and exact reviewed plan digest.");
        var historic = w.OriginalApplies.Find(intentKey, a => a.IntentKey);
        if (historic is not null)
        {
            historic.RequireMatch(w); RequireSelection(historic.Plan, selected);
            if (historic.Plan.Digest != expectedPlanDigest) throw new WorkspaceException("OriginalApplyRequestConflict", "A completed intent fixes its exact plan.");
            return new(historic.Plan.Result, true);
        }
        if (w.OriginalApply is { } existing && (w.Results.Count == 0 || !existing.Completed || existing.IntentKey == intentKey))
        {
            existing.RequireMatch(w);
            RequireSelection(existing.Plan, selected);
            if (existing.IntentKey != intentKey || existing.Plan.Digest != expectedPlanDigest)
                throw new WorkspaceException("OriginalApplyRequestConflict", "Preserve and resume the exact original-apply intent.");
            if (existing.Completed) { CompleteApplyHistory(w); return new(existing.Plan.Result, true); }
        }
        else
        {
            var plan = await PlanOriginalCore(w, ct, paths, true, proof);
            if (plan.Digest != expectedPlanDigest) throw new WorkspaceException("OriginalApplyPlanChanged", "The original changed after planning; review the latest plan before any write.");
            if (plan.ConflictCount != 0) throw new WorkspaceException("OriginalApplyConflict", "The original has conflicts; no original writes were performed.", plan);
            // Re-acquire below before saving intent: no original or durable
            // publication state is written if source/locks changed.
            using var admission = AcquireOriginalSource(w, plan.EnumerateChanges().Select(c => c.Path), ct, true);
            if (admission?.Identity != plan.SourceIdentity) throw new WorkspaceException("OriginalApplyPlanChanged", "The original VCS identity changed after planning.");
            w.OriginalApply = OriginalApplyPages.Initialize(plan, intentKey, Control(w), contentPins);
            w.Phase = "integrating";
            store.Save(w, "original-apply-intent");
        }
        if (w.OriginalApply!.Plan.LiveOriginalIdentity != w.LiveOriginal?.Identity)
            throw new WorkspaceException("LiveOriginalPermitMismatch", "Resume publication with the same externally coordinated Editor identity.");
        using var sourceLease = AcquireOriginalSource(w, w.OriginalApply.Plan.EnumerateChanges().Select(c => c.Path), ct, true);
        if (sourceLease?.Identity != w.OriginalApply.Plan.SourceIdentity) throw new WorkspaceException("OriginalApplyPlanChanged", "Preserve the fixed intent; original VCS identity changed.");
        using var independentManifest = proof is null ? ReadManifest(w, FilePath(Control(w), "result.json")) : null;
        var payloadManifest = proof?.Manifest ?? independentManifest!;
        using var independentReader = proof is null ? ContentReader(w, Control(w), payloadManifest.ContentFiles) : null;
        var payloadReader = proof?.Content ?? independentReader;
        using (diagnostics?.Measure("apply.publish"))
        publisher.Publish(w.OriginalApply, version =>
        {
            if (w.SourceKind == "git" && w.Results.Last().GitReference is not null)
            {
                var entry = payloadManifest!.FindPayload(version.Hash!, version.Length);
                return GitSourceResult.OpenPayload(w.ProjectPath, w.Results.Last().GitReference!, entry.Path);
            }
            string path = FilePath(Control(w), "objects/" + version.Hash);
            // The opened payload is independently hashed by the publisher before
            // publication; do not hydrate the workspace or consult the source basis.
            return (payloadReader?.Reader ?? Open)(path);
        }, () =>
        {
            if (w.LiveOriginal is not null)
            {
                using var currentEditor = (applicationPolicies ?? ApplicationProjectPolicies.None).Get(w.ApplicationKind)
                    .AcquireOriginal(w, true, w.StorageMode == "project-snapshot" ? w.ProjectPath : null);
            }
            // Completion and the applied baseline advance in the SAME durable
            // record as the final publication receipt; no crash window permits
            // a newer capture to forget a just-published path.
            if (w.Results.Count > 0 && w.OriginalApply.Completed && w.OriginalApplies.Find(w.OriginalApply.IntentKey, a => a.IntentKey) is null)
            { SaveAppliedPaths(w); w.OriginalApplies.Add(w.OriginalApply); }
            store.Save(w, "original-apply-progress");
        }, ct);
        payloadReader?.VerifyBoundary();
        w.OriginalApply.RequireMatch(w);
        if (!w.OriginalApply.Completed) throw new WorkspaceException("OriginalApplyPending", "Publication did not complete; preserve its intent and payloads.");
        CompleteApplyHistory(w);
        return new(w.ResultRevision!, false);
    }
    private void VerifyOriginalGeneration(WorkspaceRecord w, CancellationToken ct, GenerationReadScope? proof = null)
    {
        Owner(w, Control(w));
        if (proof is not null) proof.VerifyAsync(ct).GetAwaiter().GetResult();
        else if (w.Results.Count == 0) { using var reader = ContentReader(w, Control(w)); VerifyIndependentResult(w, Control(w), w.ResultRevision, reader.Reader); }
        else VerifyGenerationAsync(w, Control(w), w.Results.Last(), ct).GetAwaiter().GetResult();
    }
    private IOriginalSourceLease? AcquireOriginalSource(WorkspaceRecord w, IEnumerable<string> paths, CancellationToken ct, bool publishing = false) =>
        w.SourceKind switch { "plastic" => originalGuard!.Acquire(w, paths, ct, publishing), "git" => new GitOriginalPublicationGuard(w.ProjectPath, ct), _ => null };
    private void CompleteApplyHistory(WorkspaceRecord w)
    {
        if (w.Results.Count == 0 || w.OriginalApply is not { Completed: true } current || w.OriginalApplies.Find(current.IntentKey, a => a.IntentKey) is not null) return;
        SaveAppliedPaths(w); w.OriginalApplies.Add(current); store.Save(w, "original-generation-completed");
    }
}
