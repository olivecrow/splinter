using Splinter.Core;
using Splinter.Runtime;
using Splinter.SourceControl.Git;

namespace Splinter.SourceControl.Files;

public sealed partial class FileWorkspaceProvider
{
    public async Task<OriginalApplyPlan> ImportAsync(WorkspaceRecord source, WorkspaceResultGeneration generation, WorkspaceRecord target,
        IOriginalFilePublisher publisher, WorkspaceImportRequest request, LiveOriginalEditorPermit? liveTarget, CancellationToken ct)
    {
        string control = Control(target);
        Owner(source, Control(source));
        if (target.SourceKind != "git") Owner(target, control);
        using var proof = new GenerationReadScope(this, source, Control(source), generation, contentPins, ct);
        await proof.VerifyAsync(ct);
        var policy = (applicationPolicies ?? ApplicationProjectPolicies.None).Get(target.ApplicationKind);
        IDisposable? AcquireEditor(bool publish) => policy.AcquirePublication(target.Path, generation.Id, liveTarget, publish, target.Path);
        using var editor = AcquireEditor(!request.Plan);
        using var git = target.SourceKind == "git" ? await GitOriginalPublicationGuard.AcquireWorkspaceAsync(target, control, ct) : null;
        var peer = new PeerSource(source.Id, source.RequestDigest!);
        var previous = target.Imports.LastOrDefault(r => r.Publication.Plan.PeerSource == peer);
        var pending = target.PendingImport;
        OriginalApplyPlan plan;
        if (pending is null)
        {
            var template = new OriginalApplyPlan(3, target.Id, target.RequestDigest!, source.BaseRevision!, generation.Revision,
                target.Path, "", [], [], "") { ResultId = generation.Id, PeerSource = peer, SourceIdentity = git?.Identity,
                    PreviousApplyDigest = previous?.Publication.Plan.Digest, LiveOriginalIdentity = liveTarget?.Identity };
            using var selected = Selection(target, request.Paths);
            plan = BuildPublicationPlan(target, proof.Basis, proof.Manifest, publisher, template, previous?.AppliedPaths,
                previous?.AppliedPathNames, selected, request.Paths, null, null, ct);
            if (request.Plan) return plan;
            if (plan.Digest != request.ExpectedPlanDigest)
                throw new WorkspaceException("WorkspaceImportPlanChanged", "The destination or selection changed. Review a fresh import plan.");
            if (plan.ConflictCount != 0)
                throw new WorkspaceException("WorkspaceImportConflict", "Resolve the conflicting destination paths before importing.", plan);
        }
        else
        {
            plan = pending.Plan;
            if (plan.PeerSource != peer || plan.ResultId != generation.Id || plan.Result != generation.Revision ||
                plan.SourceIdentity != git?.Identity || plan.LiveOriginalIdentity != liveTarget?.Identity ||
                plan.PreviousApplyDigest != previous?.Publication.Plan.Digest ||
                !OriginalApplyPages.MatchesSelection(plan, control, request.Paths) ||
                !request.Plan && (pending.IntentKey != request.IntentKey || plan.Digest != request.ExpectedPlanDigest))
                throw new WorkspaceException("WorkspaceImportPending", "Resume the exact source, result, scope, Editor and intent of the pending import.");
            if (request.Plan) return plan;
        }
        // An older None/Plastic checkpoint may name an unchanged basis file whose bytes were first
        // retained when a later checkpoint changed it. A reversal uses those verified immutable bytes.
        var latest = source.Results.Last();
        using var retained = generation.Id != latest.Id && previous is not null && !RetainsImportPayloads(proof.Manifest, plan, ct)
            ? new GenerationReadScope(this, source, Control(source), latest, contentPins, ct) : null;
        if (retained is not null)
        {
            await retained.VerifyAsync(ct);
            if (!RetainsImportPayloads(retained.Manifest, plan, ct)) throw Error("A required import payload has no independent retention proof.");
        }
        var payload = retained ?? proof;
        if (pending is null)
        {
            pending = OriginalApplyPages.Initialize(plan, request.IntentKey!, control, contentPins);
            target.PendingImport = pending;
            store.Save(target, "workspace-import-intent");
        }
        publisher.Publish(pending, version => OpenPublicationPayload(source, generation, proof.Manifest, payload.Content, version), () =>
        {
            if (liveTarget is not null) { using var currentEditor = AcquireEditor(true); }
            if (pending.Completed)
            {
                proof.Content.VerifyBoundary();
                retained?.Content.VerifyBoundary();
                var (paths, names) = SaveAppliedPaths(control, pending, previous?.AppliedPaths, previous?.AppliedPathNames);
                target.Imports.Add(new(pending, paths, names));
                target.PendingImport = null;
            }
            store.Save(target, "workspace-import-progress");
        }, ct);
        if (!pending.Completed || target.PendingImport is not null)
            throw new WorkspaceException("WorkspaceImportPending", "Resume the destination's fixed import intent.");
        return plan;
    }
    private bool RetainsImportPayloads(FileSourceManifest manifest, OriginalApplyPlan plan, CancellationToken ct)
    {
        if (manifest.RetainedPayloads is null) return true;
        using var pages = new PagedMetadataStore(manifest.Directory!, filePins: contentPins);
        var retained = pages.Open(manifest.RetainedPayloads);
        foreach (var change in plan.EnumerateChanges())
        {
            ct.ThrowIfCancellationRequested();
            if (!change.AlreadyMatches && change.After is { Kind: "file" } file && retained.Find(file.Hash!) is null) return false;
        }
        return true;
    }
}
