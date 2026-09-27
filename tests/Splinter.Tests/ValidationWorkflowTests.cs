using System.Text.Json;
using System.Text.Json.Nodes;
using Splinter.Core;
using Splinter.Protocol;
using Splinter.Runtime;

namespace Splinter.Tests;

public sealed class ValidationWorkflowTests
{
    private sealed class Harness : IDisposable
    {
        public readonly TestDirectory Directory = new();
        public readonly ValidationWorkflowWorld World = new();
        public readonly ValidationOwner WorkerOwner = new("worker-owner", 1), ViewOwner = new("view-owner", 1);
        public readonly SnapshotReference Snapshot = new("snapshot", "fake", "source", "immutable", DateTimeOffset.Parse("2026-09-28T00:00:00Z"), true, true, "fixture");
        public ValidationWorkflowStore Store => new(new WorkspaceStore(Path.Combine(Directory.Path, "state")));
        public ValidationWorkflowFake Provider => new(Path.Combine(Directory.Path, "provider"), World);
        public CancellationToken Ct => TestContext.Current.CancellationToken;
        public Harness()
        {
            World.SetWorker("worker-a", WorkerOwner, Snapshot, "A");
            World.SetWorker("worker-b", WorkerOwner, Snapshot, "B");
            World.Owners["view"] = ViewOwner; World.Owners["other-view"] = ViewOwner;
        }
        public ValidationWorkflowCoordinator Runtime(Action<string>? fault = null, ValidationWorkflowFake? provider = null) => new(Store, provider ?? Provider, fault);
        public ValidationWorkflowRequest Publish(string key = "publish-a", string worker = "worker-a") =>
            new(1, "publish", "project", key, WorkerOwner) { Worker = World.Workers[worker].Worker };
        public ValidationWorkflowRequest Select(string candidate, string key = "select-a") =>
            new(1, "select", "project", key, ViewOwner) { CandidateId = candidate, Cache = new("view", "cache", Snapshot, "app-version", "cache-partition", 100_000) };
        public ValidationWorkflowRequest Generation(string action, string generation, string key) =>
            new(1, action, "project", key, ViewOwner) { GenerationId = generation };
        public ValidationWorkflowRequest Artifact(string action, string kind, string id, string key) =>
            new(1, action, "project", key, kind == "candidate" ? WorkerOwner : ViewOwner) { Target = new(kind, id) };
        public Task<ValidationWorkflowResponse> Run(ValidationWorkflowRequest request) => Runtime().ExecuteAsync(request, Ct);
        public async Task<string> Published(string key = "publish-a", string worker = "worker-a") => (await Run(Publish(key, worker))).Candidate!.Intent.CandidateId;
        public async Task<string> Selected(string candidate, string key = "select-a") => (await Run(Select(candidate, key))).Selection!.Intent.GenerationId;
        public void Dispose() => Directory.Dispose();
    }
    private static async Task Error(string expected, Func<Task> action) => Assert.Equal(expected, (await Assert.ThrowsAsync<WorkspaceException>(action)).Code);
    private static Action<string> Crash(string point) => current => { if (current == point) throw new IOException("Interrupted at " + point); };

    [Fact]
    public async Task CandidateSurvivesWorkerEditsAndAbaKeepsEverySourceGenerationSeparate()
    {
        using var h = new Harness();
        var originalRequest = h.Publish();
        string a = await h.Published(), b = await h.Published("publish-b", "worker-b");
        h.World.SetWorker("worker-a", h.WorkerOwner, h.Snapshot, "A-after-publication", 2);
        Assert.True((await h.Run(originalRequest)).Replayed);
        await Error("ValidationRequestKeyConflict", () => h.Run(h.Publish()));
        string a1 = await h.Selected(a), b1 = await h.Selected(b, "select-b"), a2 = await h.Selected(a, "select-a-again");
        Assert.Equal("A|generated:" + a1, h.Provider.SourceContent(a1));
        Assert.Equal("B|generated:" + b1, h.Provider.SourceContent(b1));
        Assert.Equal("A|generated:" + a2, h.Provider.SourceContent(a2));
        await Error("ValidationGenerationMismatch", () => h.Run(h.Generation("resume", a1, "wrong-generation")));
        Assert.Equal(a2, (await h.Run(h.Generation("resume", a2, "resume-current"))).Selection!.Intent.GenerationId);
        var response = await h.Run(h.Generation("observe-validation", a2, "proof"));
        Assert.True(response.Evidence!.Success);
        Assert.Equal(a, response.Evidence.CandidateId);
        Assert.Equal("A-after-publication", h.World.Contents["worker-a"]);
        var state = await h.Runtime().InspectAsync("project", h.Ct);
        Assert.Single(state.Views); Assert.Equal(3, state.Generations.Count);
        Assert.Equal(originalRequest.Worker!.SourceSeal, state.Candidates[a].Publication.Intent.Worker.SourceSeal);
        Assert.DoesNotContain("wrong-generation", state.Operations.Keys);
    }

    [Theory]
    [InlineData("publish:intent")]
    [InlineData("publish:observed")]
    [InlineData("publish:completed")]
    [InlineData("select:intent")]
    [InlineData("select:observed")]
    [InlineData("select:completed")]
    [InlineData("preserve:intent")]
    [InlineData("preserve:frozen")]
    [InlineData("preserve:observed")]
    [InlineData("preserve:completed")]
    [InlineData("release:intent")]
    [InlineData("release:observed")]
    [InlineData("release:completed")]
    public async Task NewCoordinatorReconcilesEveryDurableBoundaryWithoutDuplicatingEffects(string boundary)
    {
        using var h = new Harness();
        string action = boundary.Split(':')[0];
        ValidationWorkflowRequest request;
        if (action == "publish") request = h.Publish();
        else
        {
            string a = await h.Published();
            if (action == "select") request = h.Select(a);
            else
            {
                string generation = await h.Selected(a);
                if (action == "release") await h.Run(h.Artifact("preserve", "source-generation", generation, "preserve-first"));
                request = h.Artifact(action, "source-generation", generation, action + "-target");
            }
        }
        await Assert.ThrowsAsync<IOException>(() => h.Runtime(Crash(boundary)).ExecuteAsync(request, h.Ct));
        var response = await h.Run(request);
        Assert.Equal(1, h.World.Effects[action]);
        Assert.True((await h.Run(request)).Replayed);
        Assert.Equal(action, response.Operation);
        Assert.All((await h.Runtime().InspectAsync("project", h.Ct)).Operations.Values, op => Assert.NotNull(op.Response));
    }

    [Theory]
    [InlineData("publish")]
    [InlineData("select")]
    [InlineData("preserve")]
    [InlineData("release")]
    public async Task ProviderReplyLossUsesDurableReceiptsFromANewProviderInstance(string action)
    {
        using var h = new Harness();
        ValidationWorkflowRequest request;
        if (action == "publish") request = h.Publish();
        else
        {
            string candidate = await h.Published();
            if (action == "select") request = h.Select(candidate);
            else
            {
                if (action == "release") await h.Run(h.Artifact("preserve", "candidate", candidate, "keep"));
                request = h.Artifact(action, "candidate", candidate, "action");
            }
        }
        var provider = h.Provider; provider.LoseReply = action;
        await Assert.ThrowsAsync<IOException>(() => h.Runtime(provider: provider).ExecuteAsync(request, h.Ct));
        await h.Run(request);
        Assert.Equal(1, h.World.Effects[action]);
    }

    [Fact]
    public async Task PendingSelectionBlocksConflictingSwitchButKeepsOtherWorkerPublicationIndependent()
    {
        using var h = new Harness(); string a = await h.Published();
        var selection = h.Select(a);
        await Assert.ThrowsAsync<IOException>(() => h.Runtime(Crash("select:intent")).ExecuteAsync(selection, h.Ct));
        string b = await h.Published("publish-b", "worker-b");
        await Error("ValidationOperationPending", () => h.Run(h.Select(b, "other-selection")));
        await h.Run(selection);
        await h.Selected(b, "other-selection");
        Assert.Equal(2, h.World.Effects["select"]);
    }

    [Fact]
    public async Task PreservationReleaseAndDependencyGatesKeepSnapshotAndOtherWorkerResults()
    {
        using var h = new Harness(); string a = await h.Published(), b = await h.Published("publish-b", "worker-b");
        string generation = await h.Selected(a);
        await Error("ValidationResultRequired", () => h.Run(h.Artifact("release", "candidate", a, "too-early")));
        var resultA = (await h.Run(h.Artifact("preserve", "candidate", a, "keep-a"))).Result!;
        await Error("ValidationDependencyActive", () => h.Run(h.Artifact("release", "candidate", a, "has-dependent")));
        h.World.ValidationSuccess = false;
        Assert.False((await h.Run(h.Generation("observe-validation", generation, "failed-compile"))).Evidence!.Success);
        var generated = (await h.Run(h.Artifact("preserve", "source-generation", generation, "keep-generated"))).Result!;
        await Error("ValidationGenerationFrozen", () => h.Run(h.Generation("resume", generation, "after-preserve")));
        await h.Run(h.Artifact("release", "source-generation", generation, "release-generated"));
        await h.Run(h.Artifact("release", "candidate", a, "release-a"));
        Assert.Equal(["candidate:" + b], await h.Runtime().SnapshotDependenciesAsync("project", h.Snapshot, h.Ct));
        Assert.Equal("A", File.ReadAllText(resultA.Reference));
        Assert.Equal("A|generated:" + generation, File.ReadAllText(generated.Reference));
        Assert.Equal("B", File.ReadAllText(h.Provider.PayloadPath(new("candidate", b))));
        Assert.Equal("A", h.World.Contents["worker-a"]);
        var keptB = (await h.Run(h.Artifact("preserve", "candidate", b, "keep-b"))).Result!;
        await h.Run(h.Artifact("release", "candidate", b, "release-b"));
        Assert.Empty(await h.Runtime().SnapshotDependenciesAsync("project", h.Snapshot, h.Ct));
        Assert.Single((await h.Runtime().InspectAsync("project", h.Ct)).Views); // cache not reclaimed
        h.World.Workers["worker-a"] = h.World.Workers["worker-a"] with { SnapshotAvailable = false };
        await h.Provider.VerifyResultAsync(resultA, h.Ct); await h.Provider.VerifyResultAsync(keptB, h.Ct);
    }

    [Theory]
    [InlineData("snapshot")]
    [InlineData("partition")]
    [InlineData("budget")]
    [InlineData("application")]
    [InlineData("another-view")]
    public async Task CacheMismatchIsRejectedBeforeAnySelectionEffectOrPendingIntent(string mismatch)
    {
        using var h = new Harness(); string candidate = await h.Published(); await h.Selected(candidate);
        var request = h.Select(candidate, "mismatch"); var cache = request.Cache!;
        request = request with { Cache = mismatch switch
        {
            "snapshot" => cache with { Snapshot = cache.Snapshot with { Id = "new-snapshot" } },
            "partition" => cache with { PartitionKey = "different-partition" },
            "budget" => cache with { MaxJournalBytes = 200_000 },
            "application" => cache with { CompatibilityKey = "other-version" },
            _ => cache with { ViewId = "other-view" }
        } };
        await Error("ValidationCacheMismatch", () => h.Run(request));
        Assert.Equal(1, h.World.Effects["select"]);
        Assert.DoesNotContain("mismatch", (await h.Runtime().InspectAsync("project", h.Ct)).Operations.Keys);
    }

    [Theory]
    [InlineData("ownership", "ValidationResourcesUnknown")]
    [InlineData("unknown-handles", "ValidationResourcesUnknown")]
    [InlineData("unknown-mount", "ValidationResourcesUnknown")]
    [InlineData("negative", "ValidationResourcesUnknown")]
    [InlineData("applications", "ValidationResourcesBusy")]
    [InlineData("writers", "ValidationResourcesBusy")]
    [InlineData("handles", "ValidationResourcesBusy")]
    [InlineData("mounted", "ValidationResourcesBusy")]
    public async Task UnknownAndBusyResourcesNeverAuthorizePublicationOrViewSwitch(string kind, string error)
    {
        using var h = new Harness(); string a = await h.Published();
        var quiet = new ValidationResources(true, 0, 0, 0, false);
        var resources = kind switch
        {
            "ownership" => quiet with { OwnershipVerified = false },
            "unknown-handles" => quiet with { OpenHandles = null },
            "unknown-mount" => quiet with { Mounted = null },
            "negative" => quiet with { Writers = -1 },
            "applications" => quiet with { Applications = 1 },
            "writers" => quiet with { Writers = 1 },
            "handles" => quiet with { OpenHandles = 1 },
            _ => quiet with { Mounted = true }
        };
        h.World.ViewResources = resources;
        await Error(error, () => h.Run(h.Select(a)));
        h.World.Workers["worker-b"] = h.World.Workers["worker-b"] with { Resources = resources };
        await Error(error, () => h.Run(h.Publish("b", "worker-b")));
        Assert.Equal(1, h.World.Effects["publish"]); Assert.False(h.World.Effects.ContainsKey("select"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(null)]
    public async Task MissingOrUnobservedSnapshotDoesNotGetReplaced(bool? available)
    {
        using var h = new Harness();
        h.World.Workers["worker-a"] = h.World.Workers["worker-a"] with { SnapshotAvailable = available };
        await Error("ValidationSnapshotUnavailable", () => h.Run(h.Publish()));
        Assert.Empty(h.World.Effects);
    }

    [Fact]
    public async Task ManagedPathSeparationMustBeQualifiedAndPublicationRechecksDirectWriters()
    {
        using var h = new Harness();
        h.World.Workers["worker-a"] = h.World.Workers["worker-a"] with { ManagedPathPolicy = "unqualified" };
        await Error("ValidationManagedPathsUnsupported", () => h.Run(h.Publish()));
        h.World.Workers["worker-a"] = h.World.Workers["worker-a"] with { ManagedPathPolicy = "source-only-v1" };
        var provider = h.Provider;
        provider.BeforePublish = () => h.World.SetWorker("worker-a", h.WorkerOwner, h.Snapshot, "late-write", 2);
        await Error("ValidationSourceChanged", () => h.Runtime(provider: provider).ExecuteAsync(h.Publish(), h.Ct));
        Assert.Empty(h.World.Effects); Assert.Equal("late-write", h.World.Contents["worker-a"]);
        Assert.Single(await h.Runtime().SnapshotDependenciesAsync("project", h.Snapshot, h.Ct));
    }

    [Fact]
    public async Task StaleOwnerCannotReplayAnAlreadyCompletedPublicationOrUseTheView()
    {
        using var h = new Harness(); var request = h.Publish(); string candidate = await h.Published();
        h.World.Owners["worker-a"] = h.WorkerOwner with { Generation = 2 };
        await Error("ValidationOwnerMismatch", () => h.Run(request));
        await h.Selected(candidate);
        h.World.Owners["view"] = h.ViewOwner with { Generation = 2 };
        await Error("ValidationOwnerMismatch", () => h.Run(h.Select(candidate, "stale")));
        Assert.Equal(1, h.World.Effects["select"]);
    }

    [Fact]
    public async Task WrongEvidenceAndMutatedPreservedSourceCannotAuthorizeSuccessOrReclamation()
    {
        using var h = new Harness(); string a = await h.Published(), generation = await h.Selected(a);
        var selection = (await h.Runtime().InspectAsync("project", h.Ct)).Generations[generation].Selection;
        var good = await h.Provider.ReadValidationAsync(selection, h.Ct);
        h.World.EvidenceOverride = good with { GenerationId = "other-generation" };
        var request = h.Generation("observe-validation", generation, "proof");
        await Error("ValidationEvidenceMismatch", () => h.Run(request));
        Assert.Null((await h.Runtime().InspectAsync("project", h.Ct)).Generations[generation].Evidence);
        h.World.EvidenceOverride = good; await h.Run(request);
        await h.Run(h.Artifact("preserve", "source-generation", generation, "keep"));
        File.AppendAllText(h.Provider.PayloadPath(new("source-generation", generation)), "late-generated-write");
        await Error("ValidationSourceChanged", () => h.Run(h.Artifact("release", "source-generation", generation, "release")));
        Assert.False((await h.Runtime().InspectAsync("project", h.Ct)).Generations[generation].Released);
        Assert.False(h.World.Effects.ContainsKey("release"));
    }

    [Fact]
    public async Task CorruptIndependentResultIsNotAReleaseAuthorization()
    {
        using var h = new Harness(); string a = await h.Published();
        var result = (await h.Run(h.Artifact("preserve", "candidate", a, "keep"))).Result!;
        File.WriteAllText(result.Reference, "damaged");
        await Error("ValidationResultInvalid", () => h.Run(h.Artifact("release", "candidate", a, "release")));
        Assert.True(File.Exists(h.Provider.PayloadPath(new("candidate", a))));
    }

    [Fact]
    public async Task ConcurrentIdenticalRequestsPublishOnceAcrossCoordinatorInstances()
    {
        using var h = new Harness(); var request = h.Publish();
        var replies = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => h.Run(request)));
        Assert.Single(replies, r => !r.Replayed); Assert.Equal(1, h.World.Effects["publish"]);
        Assert.Single(replies.Select(r => r.Candidate).Distinct());
    }

    [Theory]
    [InlineData("truncated")]
    [InlineData("unsupported")]
    [InlineData("missing")]
    [InlineData("null")]
    [InlineData("extra")]
    [InlineData("identity")]
    public async Task CorruptOrUnsupportedStateIsPreservedByteForByte(string damage)
    {
        using var h = new Harness(); var request = h.Publish(); await h.Run(request);
        string path = h.Store.StatePath("project"); var node = JsonNode.Parse(File.ReadAllText(path))!;
        switch (damage)
        {
            case "unsupported": node["version"] = 2; break;
            case "missing": node.AsObject().Remove("operations"); break;
            case "null": node["candidates"] = null; break;
            case "extra": node["futureCompatibility"] = true; break;
            case "identity": node["projectId"] = "another-project"; break;
        }
        string content = damage == "truncated" ? "{" : node.ToJsonString(); File.WriteAllText(path, content);
        await Error("ValidationStateCorrupt", () => h.Run(request));
        Assert.Equal(content, File.ReadAllText(path)); Assert.Equal(1, h.World.Effects["publish"]);
    }

    [Fact]
    public async Task ExplicitRetryRecoversACompleteFlushedStateButReadOnlyInspectionDoesNotPublishIt()
    {
        using var h = new Harness(); var request = h.Publish(); await h.Run(request);
        string path = h.Store.StatePath("project"); var node = JsonNode.Parse(File.ReadAllText(path))!;
        node["revision"] = node["revision"]!.GetValue<long>() + 1;
        File.WriteAllText(path + ".pending", node.ToJsonString());
        await Error("ValidationStateRecoveryRequired", () => h.Runtime().InspectAsync("project", h.Ct));
        Assert.True(File.Exists(path + ".pending"));
        Assert.True((await h.Run(request)).Replayed);
        Assert.False(File.Exists(path + ".pending")); Assert.Equal(1, h.World.Effects["publish"]);
    }

    [Fact]
    public async Task FirstFlushedIntentRecoversBeforeTheInitialRenameAndCannotLoseSnapshotDependency()
    {
        using var h = new Harness(); var request = h.Publish();
        await Assert.ThrowsAsync<IOException>(() => h.Runtime(Crash("publish:intent")).ExecuteAsync(request, h.Ct));
        string path = h.Store.StatePath("project"); File.Move(path, path + ".pending");
        await Error("ValidationStateRecoveryRequired", () => h.Runtime().SnapshotDependenciesAsync("project", h.Snapshot, h.Ct));
        var response = await h.Run(request);
        Assert.Single(await h.Runtime().SnapshotDependenciesAsync("project", h.Snapshot, h.Ct));
        Assert.Equal(response.OperationId, response.Candidate!.Intent.CandidateId);
        Assert.Equal(1, h.World.Effects["publish"]);
    }

    [Fact]
    public async Task WrongSnapshotTupleCannotTurnAnExistingDependencyIntoAnEmptySet()
    {
        using var h = new Harness(); await h.Published();
        await Error("SnapshotIdentityMismatch", () => h.Runtime().SnapshotDependenciesAsync("project", h.Snapshot with { RootPath = "different-root" }, h.Ct));
        Assert.Single(await h.Runtime().SnapshotDependenciesAsync("project", h.Snapshot, h.Ct));
    }

    [Fact]
    public async Task DelimitedProjectAndRequestIdsCannotAliasAnotherPublication()
    {
        using var h = new Harness();
        var first = h.Publish("publish") with { ProjectId = "project:scope" };
        var second = h.Publish("scope:publish");
        var a = await h.Run(first);
        var b = await h.Run(second);
        Assert.NotEqual(a.OperationId, b.OperationId);
        Assert.Equal(2, h.World.Effects["publish"]);
        Assert.True((await h.Run(first)).Replayed);
        Assert.True((await h.Run(second)).Replayed);
        Assert.Single(await h.Runtime().SnapshotDependenciesAsync(first.ProjectId, h.Snapshot, h.Ct));
        Assert.Single(await h.Runtime().SnapshotDependenciesAsync(second.ProjectId, h.Snapshot, h.Ct));
    }

    [Theory]
    [InlineData("candidates")]
    [InlineData("views")]
    public async Task MissingRetainedOwnershipCannotHideSnapshotDependencies(string collection)
    {
        using var h = new Harness(); string candidate = await h.Published();
        if (collection == "views") await h.Selected(candidate);
        string path = h.Store.StatePath("project");
        var state = JsonNode.Parse(File.ReadAllText(path))!;
        state[collection] = new JsonObject();
        string damaged = state.ToJsonString(); File.WriteAllText(path, damaged);
        await Error("ValidationStateCorrupt", () => h.Runtime().SnapshotDependenciesAsync("project", h.Snapshot, h.Ct));
        await Error("ValidationStateCorrupt", () => h.Run(h.Publish()));
        Assert.Equal(damaged, File.ReadAllText(path));
        Assert.Equal(1, h.World.Effects["publish"]);
    }

    [Theory]
    [InlineData("rollback")]
    [InlineData("duplicate-field")]
    [InlineData("partial-pending")]
    [InlineData("forged-release")]
    public async Task AmbiguousStateGenerationsAndUnprovenReleaseFlagsArePreserved(string damage)
    {
        using var h = new Harness(); var request = h.Publish(); string id = await h.Published();
        string path = h.Store.StatePath("project"), original = File.ReadAllText(path);
        var node = JsonNode.Parse(original)!;
        if (damage == "rollback")
        {
            node["revision"] = node["revision"]!.GetValue<long>() + 1;
            node["candidates"] = new JsonObject(); node["operations"] = new JsonObject();
            File.WriteAllText(path + ".pending", node.ToJsonString());
        }
        else if (damage == "duplicate-field") File.WriteAllText(path, original.Replace("\"version\": 1,", "\"version\": 1, \"version\": 1,", StringComparison.Ordinal));
        else if (damage == "partial-pending") File.WriteAllText(path + ".pending", "{");
        else { node["candidates"]![id]!["released"] = true; File.WriteAllText(path, node.ToJsonString()); }
        string damaged = File.ReadAllText(path);
        string? pending = File.Exists(path + ".pending") ? File.ReadAllText(path + ".pending") : null;
        await Error("ValidationStateCorrupt", () => h.Run(request));
        Assert.Equal(damaged, File.ReadAllText(path));
        if (pending is not null) Assert.Equal(pending, File.ReadAllText(path + ".pending"));
        Assert.Equal(1, h.World.Effects["publish"]);
    }

    [Fact]
    public async Task PublicationReceiptFromAnotherIntentCannotCompleteAnInterruptedRequest()
    {
        using var h = new Harness(); var request = h.Publish(); var provider = h.Provider; provider.LoseReply = "publish";
        await Assert.ThrowsAsync<IOException>(() => h.Runtime(provider: provider).ExecuteAsync(request, h.Ct));
        string id = (await h.Runtime().InspectAsync("project", h.Ct)).Operations[request.RequestKey].Id;
        string path = Path.Combine(h.Directory.Path, "provider", "candidate-" + id + ".json");
        string original = File.ReadAllText(path);
        var receipt = JsonSerializer.Deserialize<CandidatePublicationReceipt>(original, ApiJson.Options)!;
        File.WriteAllText(path, JsonSerializer.Serialize(receipt with { Intent = receipt.Intent with { CandidateId = "someone-else" } }, ApiJson.Options));
        await Error("ValidationReceiptMismatch", () => h.Run(request));
        Assert.Empty((await h.Runtime().InspectAsync("project", h.Ct)).Candidates);
        File.WriteAllText(path, original);
        Assert.Equal(id, (await h.Run(request)).Candidate!.Intent.CandidateId);
        Assert.Equal(1, h.World.Effects["publish"]);
    }

    [Theory]
    [InlineData("ValidationStateCorrupt", "requires-user-action")]
    [InlineData("ValidationResultInvalid", "requires-user-action")]
    [InlineData("ValidationResourcesUnknown", "requires-user-action")]
    [InlineData("ValidationResourcesBusy", "safe-to-retry")]
    [InlineData("ValidationOperationPending", "requires-user-action")]
    [InlineData("ValidationCacheMismatch", "correct-request")]
    public void SharedFailureResponsesKeepPreservationAndRequestCorrectionDistinct(string code, string disposition)
    {
        Assert.Equal(disposition, ApiResponse.Failure("contract-test", code, "test").RetryDisposition);
    }

    [Fact]
    public async Task EveryRequestResponseAndDurableStateMatchesItsStrictSchema()
    {
        using var h = new Harness(); var catalog = new ContractCatalog();
        void Validate(string schema, object value)
        {
            var json = JsonSerializer.SerializeToElement(value, ApiJson.Options);
            Assert.True(catalog.Validate("urn:splinter:v1:validation-workflow-" + schema, json).IsValid, json.ToString());
        }
        async Task<ValidationWorkflowResponse> Run(ValidationWorkflowRequest request)
        { Validate("request", request); var response = await h.Run(request); Validate("response", response); return response; }
        var published = await Run(h.Publish()); string a = published.Candidate!.Intent.CandidateId;
        string generation = (await Run(h.Select(a))).Selection!.Intent.GenerationId;
        await Run(h.Generation("resume", generation, "resume"));
        await Run(h.Generation("observe-validation", generation, "proof"));
        await Run(h.Artifact("preserve", "source-generation", generation, "keep-source"));
        await Run(h.Artifact("release", "source-generation", generation, "release-source"));
        await Run(h.Artifact("preserve", "candidate", a, "keep-candidate"));
        await Run(h.Artifact("release", "candidate", a, "release-candidate"));
        using var state = JsonDocument.Parse(File.ReadAllText(h.Store.StatePath("project")));
        Assert.True(catalog.Validate("urn:splinter:v1:validation-workflow-state", state.RootElement).IsValid);
        var invalid = JsonSerializer.SerializeToNode(h.Publish(), ApiJson.Options)!;
        invalid["generationId"] = "extraneous-generation";
        Assert.False(catalog.Validate("urn:splinter:v1:validation-workflow-request", JsonSerializer.SerializeToElement(invalid)).IsValid);
        await Error("ValidationRequestInvalid", () => h.Run(h.Publish() with { GenerationId = "extraneous-generation" }));
        var invalidResponse = JsonSerializer.SerializeToNode(published, ApiJson.Options)!;
        invalidResponse.AsObject().Remove("candidate");
        Assert.False(catalog.Validate("urn:splinter:v1:validation-workflow-response", JsonSerializer.SerializeToElement(invalidResponse)).IsValid);
    }
}
