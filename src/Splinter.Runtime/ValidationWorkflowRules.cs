using System.Text.RegularExpressions;
using Splinter.Core;

namespace Splinter.Runtime;

internal static partial class ValidationWorkflowRules
{
    internal static void Require(bool condition, string code, string message)
    { if (!condition) throw new WorkspaceException(code, message); }
    internal static void Name(string? value) => Require(value is not null && NamePattern().IsMatch(value),
        "ValidationRequestInvalid", "Use a bounded opaque identifier, not a filesystem path.");
    internal static void Text(string? value) => Require(!string.IsNullOrWhiteSpace(value) && value.Length <= 4096,
        "ValidationRequestInvalid", "A bounded nonempty identity value is required.");
    internal static void Owner(ValidationOwner owner)
    {
        if (owner is null) throw new WorkspaceException("ValidationRequestInvalid", "Owner is required.");
        Name(owner.Id); Require(owner.Generation > 0, "ValidationRequestInvalid", "Owner generation must be positive.");
    }
    internal static void Seal(SnapshotDeltaSeal seal) => Require(seal is not null && seal.DurableSequence >= 0 && seal.JournalBytes >= 0 &&
        seal.JournalHash is not null && HashPattern().IsMatch(seal.JournalHash), "ValidationReceiptInvalid", "A complete durable seal is required.");
    internal static void Snapshot(SnapshotReference snapshot)
    {
        if (snapshot is null) throw new WorkspaceException("ValidationRequestInvalid", "Snapshot identity is required.");
        Text(snapshot.Id); Text(snapshot.Provider); Text(snapshot.SourcePath); Text(snapshot.RootPath); Text(snapshot.Consistency);
        Require(snapshot.Persistent && snapshot.Differential && snapshot.CapturedAt != default,
            "ValidationRequestInvalid", "The original persistent differential snapshot identity is required.");
    }
    internal static void Cache(ValidationCacheIdentity cache)
    {
        Name(cache.ViewId); Name(cache.CacheId); Snapshot(cache.Snapshot); Text(cache.CompatibilityKey); Text(cache.PartitionKey);
        Require(cache.MaxJournalBytes >= 4096, "ValidationRequestInvalid", "The cache budget must be at least 4096 bytes.");
    }
    internal static void Target(ValidationTarget target)
    { Name(target.Id); Require(target.Kind is "candidate" or "source-generation", "ValidationRequestInvalid", "Unknown artifact kind."); }
    internal static void Request(ValidationWorkflowRequest request)
    {
        if (request is null) throw new WorkspaceException("ValidationRequestInvalid", "A complete request is required.");
        Require(request.Version == 1, "ValidationContractUnsupported", "Only the current validation workflow contract is supported.");
        Name(request.ProjectId); Name(request.RequestKey); Owner(request.Owner);
        bool w = request.Worker is not null, c = request.CandidateId is not null, v = request.Cache is not null,
            g = request.GenerationId is not null, t = request.Target is not null;
        Require(request.Operation switch
        {
            "publish" => w && !c && !v && !g && !t,
            "select" => !w && c && v && !g && !t,
            "resume" or "observe-validation" => !w && !c && !v && g && !t,
            "preserve" or "release" => !w && !c && !v && !g && t,
            _ => false
        }, "ValidationRequestInvalid", "Supply exactly the payload for this operation.");
        if (request.Worker is { } worker)
        {
            Name(worker.WorkspaceId); Owner(worker.Owner); Snapshot(worker.Snapshot); Seal(worker.SourceSeal);
            Require(worker.Owner == request.Owner, "ValidationOwnerMismatch", "The request must name the worker's exact owner generation.");
        }
        if (request.CandidateId is { } candidate) Name(candidate);
        if (request.Cache is { } cache) Cache(cache);
        if (request.GenerationId is { } generation) Name(generation);
        if (request.Target is { } target) Target(target);
    }
    internal static void Quiescent(ValidationResources resources)
    {
        Require(resources.OwnershipVerified && resources.Applications is >= 0 && resources.Writers is >= 0 &&
            resources.OpenHandles is >= 0 && resources.Mounted is not null,
            "ValidationResourcesUnknown", "Unknown ownership or resources must be observed; they are not zero.");
        Require(resources.Applications == 0 && resources.Writers == 0 && resources.OpenHandles == 0 && resources.Mounted == false,
            "ValidationResourcesBusy", "Stop owned applications/writers and unmount before this operation.");
    }
    internal static string OperationId(ValidationWorkflowRequest request) =>
        WorkspaceStore.Key(FormattableString.Invariant($"validation:{request.ProjectId.Length}:{request.ProjectId}:{request.RequestKey}"));
    internal static void Publication(CandidatePublicationReceipt receipt, CandidatePublicationIntent intent)
    {
        Require(receipt.Intent == intent, "ValidationReceiptMismatch", "Publication receipt belongs to another request or source.");
        Seal(receipt.CandidateSeal); Text(receipt.ReceiptId);
        Require(receipt.CandidateSeal.DurableSequence == intent.Worker.SourceSeal.DurableSequence,
            "ValidationReceiptMismatch", "Candidate publication must preserve the sealed source sequence.");
    }
    internal static void Selection(ValidationSelectionReceipt receipt, ValidationSelectionIntent intent)
    { Require(receipt.Intent == intent, "ValidationReceiptMismatch", "Selection receipt belongs to another generation."); Text(receipt.ReceiptId); }
    internal static void Result(ValidationIndependentResult result, ValidationPreservationIntent intent)
    {
        Require(result.Intent == intent && result.IndependentOfSnapshot && result.ContentHash is not null && HashPattern().IsMatch(result.ContentHash),
            "ValidationResultInvalid", "Require an independently readable result for the exact frozen artifact.");
        Text(result.Reference);
    }
    internal static void Evidence(CandidateValidationEvidence evidence, ValidationSelectionReceipt selection)
    {
        Text(evidence.ValidationId); Seal(evidence.SourceOutputSeal);
        Require(evidence.GenerationId == selection.Intent.GenerationId && evidence.CandidateId == selection.Intent.Candidate.Intent.CandidateId &&
            evidence.CandidateSeal == selection.Intent.Candidate.CandidateSeal && evidence.CompletedAt != default,
            "ValidationEvidenceMismatch", "Validation evidence must identify this candidate seal and source generation.");
    }
    private static void Response(ValidationWorkflowResponse response, ValidationWorkflowRequest request)
    {
        bool c = response.Candidate is not null, s = response.Selection is not null, e = response.Evidence is not null,
            r = response.Result is not null, d = response.Released is not null;
        Require(response.Operation switch
        {
            "publish" => c && !s && !e && !r && !d && response.Candidate!.Intent.Worker == request.Worker,
            "select" => !c && s && !e && !r && !d && response.Selection!.Intent.Cache == request.Cache &&
                response.Selection.Intent.Candidate.Intent.CandidateId == request.CandidateId && response.Selection.Intent.Owner == request.Owner,
            "resume" => !c && s && !e && !r && !d && response.Selection!.Intent.GenerationId == request.GenerationId,
            "observe-validation" => !c && s && e && !r && !d && response.Selection!.Intent.GenerationId == request.GenerationId,
            "preserve" => !c && !s && !e && r && !d && response.Result!.Intent.Artifact.Target == request.Target,
            "release" => !c && !s && !e && r && d && response.Released == request.Target && response.Result!.Intent.Artifact.Target == request.Target,
            _ => false
        }, "ValidationStateCorrupt", "The response does not match its operation payload.");
        if (response.Evidence is { } evidence) Evidence(evidence, response.Selection!);
    }

    internal static void State(ValidationWorkflowState state, string projectId)
    {
        Require(state.Version == 1 && state.ProjectId == projectId && state.Revision > 0 && state.Candidates is not null &&
            state.Generations is not null && state.Views is not null && state.Operations is not null,
            "ValidationStateCorrupt", "Workflow identity/version/collections do not match.");
        foreach (var (key, op) in state.Operations!)
        {
            Request(op.Request);
            Require(key == op.Request.RequestKey && op.Request.ProjectId == projectId && op.Id == OperationId(op.Request),
                "ValidationStateCorrupt", "Operation identity mismatch.");
            if (op.Response is { } response)
            {
                Require(response.Version == 1 && response.OperationId == op.Id && response.Operation == op.Request.Operation && !response.Replayed,
                    "ValidationStateCorrupt", "Saved response identity mismatch.");
                Response(response, op.Request);
            }
            if (op.Artifact is { } artifact)
            {
                Target(artifact.Target); Owner(artifact.Owner); Snapshot(artifact.Snapshot); Seal(artifact.Seal);
                Require(op.Request.Operation == "preserve" && artifact.Target == op.Request.Target && artifact.Owner == op.Request.Owner,
                    "ValidationStateCorrupt", "Frozen artifact has no matching preservation intent.");
            }
        }
        foreach (var (key, candidate) in state.Candidates!)
        {
            var intent = candidate.Publication.Intent;
            Require(key == intent.CandidateId && key == intent.OperationId && intent.ProjectId == projectId && intent.ManagedPathPolicy == "source-only-v1" &&
                state.Operations.Values.Any(o => o.Id == intent.OperationId && o.Request.Operation == "publish" &&
                    o.Request.Worker == intent.Worker && o.Response?.Candidate == candidate.Publication),
                "ValidationStateCorrupt", "Candidate has no matching publication.");
            Publication(candidate.Publication, intent);
            if (candidate.Result is { } result)
            {
                Result(result, new(result.Intent.OperationId, projectId, new(new("candidate", key), intent.Worker.Owner, intent.Worker.Snapshot, candidate.Publication.CandidateSeal)));
                Require(state.Operations.Values.Any(o => o.Id == result.Intent.OperationId && o.Response?.Result == result),
                    "ValidationStateCorrupt", "Independent result has no completed preservation operation.");
            }
            Require(candidate.Released == state.Operations.Values.Any(o => o.Request.Operation == "release" && o.Response?.Released == new ValidationTarget("candidate", key)) &&
                (!candidate.Released || candidate.Result is not null), "ValidationStateCorrupt", "Candidate release has no matching completed operation/result.");
        }
        foreach (var (key, generation) in state.Generations!)
        {
            var intent = generation.Selection.Intent;
            Require(key == intent.GenerationId && key == intent.OperationId && intent.ProjectId == projectId && state.Candidates.TryGetValue(intent.Candidate.Intent.CandidateId, out var candidate) &&
                candidate.Publication == intent.Candidate && (!candidate.Released || generation.Released) &&
                state.Operations.Values.Any(o => o.Id == intent.OperationId && o.Request.Operation == "select" && o.Response?.Selection == generation.Selection),
                "ValidationStateCorrupt", "Generation has no matching publication/selection.");
            Cache(intent.Cache); Owner(intent.Owner); Selection(generation.Selection, intent);
            Require(intent.Cache.Snapshot == intent.Candidate.Intent.Worker.Snapshot, "ValidationStateCorrupt", "Cache snapshot mismatch.");
            Require(state.Views!.TryGetValue(intent.Cache.ViewId, out var retainedView) &&
                retainedView.Cache == intent.Cache && retainedView.Owner == intent.Owner,
                "ValidationStateCorrupt", "Every source generation must retain its view and cache ownership.");
            if (generation.Evidence is { } evidence) Evidence(evidence, generation.Selection);
            if (generation.FrozenSeal is { } seal)
            {
                Seal(seal);
                Require(state.Operations.Values.Any(o => o.Artifact is { Target.Kind: "source-generation" } artifact && artifact.Target.Id == key && artifact.Seal == seal),
                    "ValidationStateCorrupt", "Source freeze has no durable preservation intent.");
            }
            if (generation.Result is { } result)
            {
                Require(generation.FrozenSeal is not null, "ValidationStateCorrupt", "Result has no frozen source seal.");
                Result(result, new(result.Intent.OperationId, projectId, new(new("source-generation", key), intent.Owner, intent.Cache.Snapshot, generation.FrozenSeal!)));
                Require(state.Operations.Values.Any(o => o.Id == result.Intent.OperationId && o.Response?.Result == result),
                    "ValidationStateCorrupt", "Independent source result has no completed preservation operation.");
            }
            Require(generation.Released == state.Operations.Values.Any(o => o.Request.Operation == "release" && o.Response?.Released == new ValidationTarget("source-generation", key)) &&
                (!generation.Released || generation.Result is not null), "ValidationStateCorrupt", "Source release has no completed operation/result.");
        }
        foreach (var (key, view) in state.Views!)
        {
            Cache(view.Cache); Owner(view.Owner);
            Require(key == view.Cache.ViewId && (view.CurrentGenerationId is null ||
                state.Generations.TryGetValue(view.CurrentGenerationId, out var generation) && !generation.Released &&
                generation.Selection.Intent.Cache == view.Cache && generation.Selection.Intent.Owner == view.Owner),
                "ValidationStateCorrupt", "View identity/current generation mismatch.");
            Require(state.Generations.Values.Any(g => g.Selection.Intent.Cache == view.Cache && g.Selection.Intent.Owner == view.Owner),
                "ValidationStateCorrupt", "A view must retain its original selection identity.");
        }
        foreach (var op in state.Operations.Values)
        {
            if (op.Response?.Candidate is { } publication)
                Require(state.Candidates.TryGetValue(publication.Intent.CandidateId, out var publishedCandidate) && publishedCandidate.Publication == publication,
                    "ValidationStateCorrupt", "A publication response cannot lose its retained candidate and dependency.");
            if (op.Response?.Selection is { } selection)
                Require(state.Generations.TryGetValue(selection.Intent.GenerationId, out var generation) && generation.Selection == selection,
                    "ValidationStateCorrupt", "A response cannot reinterpret the selected generation.");
            if (op.Response?.Result is { } result)
            {
                var target = result.Intent.Artifact.Target;
                bool matches = target.Kind == "candidate"
                    ? state.Candidates.TryGetValue(target.Id, out var candidate) && candidate.Result == result
                    : state.Generations.TryGetValue(target.Id, out var generation) && generation.Result == result;
                Require(matches, "ValidationStateCorrupt", "A response cannot substitute an independent result.");
            }
        }
        Require(state.Views.Values.Select(v => v.Cache.CacheId).Distinct().Count() == state.Views.Count,
            "ValidationStateCorrupt", "A cache cannot belong to multiple views.");
    }
    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._:-]{0,127}$", RegexOptions.CultureInvariant)]
    private static partial Regex NamePattern();
    [GeneratedRegex("^[a-f0-9]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex HashPattern();
}
