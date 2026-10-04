using System.Text.Json;
using System.Text.Json.Nodes;
using Splinter.Cli;
using Splinter.Protocol;
using Splinter.Core;
using Splinter.Runtime;

namespace Splinter.Tests;

public sealed class ContractAndCliTests
{
    [Fact]
    public async Task PlainFolderDiscoveryAndDoctorHaveNoGitOrUnityPrerequisite()
    {
        var catalog = new ContractCatalog();
        var input = JsonSerializer.SerializeToElement(new { project = "folder", key = "task", storage = "ntfs-snapshot", source = "none", application = "none", baseReference = "snapshot" });
        Assert.True(catalog.Validate(catalog.Find("workspace.ensure")!.InputSchema, input).IsValid);
        Assert.False(catalog.Validate(catalog.Find("workspace.ensure")!.InputSchema,
            JsonSerializer.SerializeToElement(new { project = "folder", key = "task", storage = "ntfs-snapshot", source = "none", application = "none", editor = true })).IsValid);
        using var directory = new TestDirectory(); using var output = new StringWriter();
        Assert.Equal(0, await Program.RunAsync(["doctor", "--project", directory.Path, "--source", "none"], output, TestContext.Current.CancellationToken));
        using var response = JsonDocument.Parse(output.ToString());
        Assert.Empty(response.RootElement.GetProperty("data").GetProperty("missingPrerequisites").EnumerateArray());
        Assert.False(response.RootElement.GetProperty("data").TryGetProperty("repository", out _));
        Assert.False(response.RootElement.GetProperty("data").TryGetProperty("application", out _));
    }
    private readonly ContractCatalog contracts = new(development: true);

    [Fact]
    public async Task ProductDiscoveryExcludesDevelopmentAndCompileControl()
    {
        using var output = new StringWriter();
        Assert.Equal(0, await Program.RunAsync(["capabilities"], output, TestContext.Current.CancellationToken));
        using var response = JsonDocument.Parse(output.ToString());
        var catalog = new ContractCatalog();
        Assert.True(catalog.Validate("urn:splinter:v1:response", response.RootElement).IsValid, output.ToString());
        var data = response.RootElement.GetProperty("data");
        Assert.Equal(new[] { "ntfs-snapshot", "git-snapshot" }, data.GetProperty("storageModes").EnumerateArray().Select(item => item.GetString()));
        foreach (string command in new[] { "unity.refresh", "validation.execute", "view.execute", "snapshot.spike", "preparation.probe", "baseline.prepare", "storage.probe" })
            Assert.Null(catalog.Find(command));
        Assert.False(data.TryGetProperty("validationWorkflowContract", out _));
        Assert.Contains("external-tools", data.GetProperty("projectValidation").GetString());
    }

    [Theory]
    [InlineData("unity", "refresh")]
    [InlineData("baseline", "prepare")]
    [InlineData("validation", "")]
    [InlineData("view", "")]
    [InlineData("snapshot-spike", "")]
    [InlineData("preparation-probe", "")]
    public async Task ProductRejectsRemovedCommandsBeforeCreatingState(string command, string subcommand)
    {
        using var directory = new TestDirectory(); string home = Path.Combine(directory.Path, "untouched");
        string[] arguments = string.IsNullOrEmpty(subcommand) ? [command, "--home", home] : [command, subcommand, "--home", home];
        using var output = new StringWriter();
        Assert.Equal(2, await Program.RunAsync(arguments, output, TestContext.Current.CancellationToken));
        using var response = JsonDocument.Parse(output.ToString());
        Assert.Equal("InvalidArguments", response.RootElement.GetProperty("error").GetProperty("code").GetString());
        Assert.False(Directory.Exists(home));
    }

    [Fact]
    public async Task ProductRejectsReFsBeforeOpeningSourceOrState()
    {
        using var directory = new TestDirectory(); string home = Path.Combine(directory.Path, "untouched");
        using var output = new StringWriter();
        Assert.Equal(2, await Program.RunAsync(["workspace", "ensure", "--project", "missing-source", "--key", "task",
            "--storage", "refs-cow", "--home", home], output, TestContext.Current.CancellationToken));
        using var response = JsonDocument.Parse(output.ToString());
        Assert.Equal("SchemaValidationFailed", response.RootElement.GetProperty("error").GetProperty("code").GetString());
        Assert.False(Directory.Exists(home));
    }

    [Fact]
    public async Task StorageAuthorityDiscoveryIsReadOnlyAndKeepsTheHostInProcess()
    {
        var (exit, response) = await Run("capabilities");
        Assert.Equal(0, exit); Assert.False(response.GetProperty("data").GetProperty("persistentHost").GetBoolean());
        var authority = response.GetProperty("data").GetProperty("storageAuthority");
        Assert.Equal("unelevated", authority.GetProperty("normalCaller").GetString());
        foreach (var schema in authority.GetProperty("schemas").EnumerateArray()) Assert.Contains(schema.GetString()!, contracts.SchemaIds);
        var (statusExit, status) = await Run("storage", "service", "status");
        Assert.Equal(0, statusExit); Assert.Equal("SplinterStorageAuthority", status.GetProperty("data").GetProperty("serviceName").GetString());
    }

    [Fact]
    public async Task ManagedCandidateExportRequiresAReceiptAndRemainsAnExperiment()
    {
        var input = new JsonObject { ["action"] = "candidate-source-export", ["overlayRequestPath"] = "overlay.json", ["candidatePath"] = "new-candidate" };
        Assert.False(contracts.Validate("urn:splinter:v1:snapshot-spike-request", JsonSerializer.SerializeToElement(input)).IsValid);
        input["receiptPath"] = "owned-git-receipt.json";
        Assert.False(contracts.Validate("urn:splinter:v1:snapshot-spike-request", JsonSerializer.SerializeToElement(input)).IsValid);
        input["publication"] = new JsonObject { ["version"] = 1, ["requestKey"] = "candidate-a" };
        Assert.True(contracts.Validate("urn:splinter:v1:snapshot-spike-request", JsonSerializer.SerializeToElement(input)).IsValid);
        var (exit, response) = await Run("capabilities");
        Assert.Equal(0, exit);
        var capability = response.GetProperty("data").GetProperty("validationViewExperiment");
        Assert.False(capability.GetProperty("integratedWorkspaceMode").GetBoolean());
        Assert.Contains("candidate-source-export", capability.GetProperty("candidate").GetString());
        Assert.Contains("stable-request-key", capability.GetProperty("publication").GetString());
        Assert.Contains("urn:splinter:v1:candidate-source-projection", contracts.SchemaIds);
        Assert.Contains("urn:splinter:v1:candidate-export-request", contracts.SchemaIds);
        Assert.Contains("urn:splinter:v1:candidate-export-intent", contracts.SchemaIds);
    }

    [Theory]
    [InlineData("candidate-export")]
    [InlineData("candidate-source-export")]
    public async Task CandidateExportWithoutAStableRequestIsRejectedBeforeOpeningTheOverlay(string action)
    {
        using var dir = new TestDirectory(); string path = Path.Combine(dir.Path, "request.json");
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(new {
            action, overlayRequestPath = "missing-overlay.json", candidatePath = "candidate", receiptPath = "missing-git.json"
        }), TestContext.Current.CancellationToken);
        var (exit, response) = await Run("snapshot-spike", "--input", path);
        Assert.Equal(2, exit); Assert.Equal("InvalidSpikeRequest", response.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task ValidationProductRejectsAmbiguousPayloadsAndExposesItsResultContract()
    {
        var request = new JsonObject { ["version"] = 1, ["action"] = "select", ["project"] = "C:/fixture",
            ["owner"] = new JsonObject { ["id"] = "view-owner", ["generation"] = 1 }, ["requestKey"] = "select-a",
            ["viewId"] = "view", ["candidateId"] = "candidate" };
        Assert.True(contracts.Validate("urn:splinter:v1:validation-product-request", JsonSerializer.SerializeToElement(request)).IsValid);
        request["workspaceId"] = "ambiguous-worker";
        Assert.False(contracts.Validate("urn:splinter:v1:validation-product-request", JsonSerializer.SerializeToElement(request)).IsValid);
        request.Remove("workspaceId"); request["owner"]!["generation"] = 0;
        Assert.False(contracts.Validate("urn:splinter:v1:validation-product-request", JsonSerializer.SerializeToElement(request)).IsValid);
        var (_, schema) = await Run("schema", "view.execute");
        Assert.Equal("urn:splinter:v1:validation-product-request", schema.GetProperty("data").GetProperty("input").GetProperty("$id").GetString());
        Assert.Contains("urn:splinter:v1:validation-product-data", contracts.SchemaIds);
        using var directory = new TestDirectory(); string file = Path.Combine(directory.Path, "duplicate.json");
        request["owner"]!["generation"] = 1;
        File.WriteAllText(file, request.ToJsonString().Replace("\"requestKey\":", "\"requestKey\":\"duplicate\",\"requestKey\":"));
        var (invalidExit, invalid) = await Run("view", "--input", file, "--home", Path.Combine(directory.Path, "state"));
        Assert.Equal(2, invalidExit); Assert.Equal("ValidationRequestInvalid", invalid.GetProperty("error").GetProperty("code").GetString());
        Assert.False(Directory.Exists(Path.Combine(directory.Path, "state")));
    }

    [Fact]
    public async Task ValidationWorkflowContractsExposeTheConcreteProductCommand()
    {
        var (exit, response) = await Run("capabilities");
        Assert.Equal(0, exit);
        var contract = response.GetProperty("data").GetProperty("validationWorkflowContract");
        Assert.Equal("development-storage-view", contract.GetProperty("stage").GetString());
        Assert.True(contract.GetProperty("integratedWorkspaceMode").GetBoolean());
        Assert.True(contract.GetProperty("cliAvailable").GetBoolean());
        foreach (var schema in contract.GetProperty("schemas").EnumerateArray())
            Assert.Contains(schema.GetString()!, contracts.SchemaIds);
        Assert.Contains(contracts.Commands, command => command.Id == "view.execute");
        var enabled = JsonNode.Parse(response.GetRawText())!;
        enabled["data"]!["validationWorkflowContract"]!["cliAvailable"] = false;
        Assert.False(contracts.Validate("urn:splinter:v1:response", JsonSerializer.SerializeToElement(enabled)).IsValid);
    }

    [Fact]
    public void DeferredReleaseIsAcceptedAndNonterminalWhileStatusOnlyReportsItsObservation()
    {
        using var d = new TestDirectory(); var store = new WorkspaceStore(d.Path);
        string id = WorkspaceStore.Key("deferred-contract");
        var workspace = new WorkspaceRecord { Id = id, RequestKey = "deferred", RequestDigest = id, ProjectPath = d.Path,
            Path = store.WorkPath(id), BaseReference = "development", SourceReference = "task", StorageMode = "ntfs-snapshot",
            Phase = "reclaim-pending", Snapshot = new() { Step = "release-snapshot",
                Result = new("snapshot", "revision", "fingerprint", "result-ref", new(1, 100, "hash"), true, SnapshotResult.StableJournalProtocol),
                Observation = new(true, false, false, true, 0, 0, 0, DeltaPresent: false),
                Reclamation = new("snapshot", "attempt", "deferred", DateTimeOffset.UtcNow, ["older"],
                    new("volume", DateTimeOffset.UtcNow, null, null, null, "SnapshotStorageUnknown")) } };
        var view = new WorkspaceView(workspace, null, null, store.ControlPath(id), "workspace release");
        var response = WorkspaceCommands.Result("workspace.release", view, false, d.Path);
        Assert.Equal("Accepted", response.Status); Assert.False(response.Terminal);
        Assert.Equal("requires-user-action", response.RetryDisposition);
        Assert.Equal("workspace.release", Assert.Single(response.NextActions).Command);
        Assert.True(contracts.Validate("urn:splinter:v1:response", JsonSerializer.SerializeToElement(response, ApiJson.Options)).IsValid);
        Assert.False(contracts.Validate("urn:splinter:v1:response", JsonSerializer.SerializeToElement(response with { Status = "Succeeded", Terminal = true }, ApiJson.Options)).IsValid);
        var status = WorkspaceCommands.Result("workspace.status", view, false, d.Path);
        Assert.Equal("Succeeded", status.Status);
        Assert.True(contracts.Validate("urn:splinter:v1:response", JsonSerializer.SerializeToElement(status, ApiJson.Options)).IsValid);
        var node = JsonSerializer.SerializeToNode(response, ApiJson.Options)!;
        var shared = node["data"]!["workspace"]!["snapshot"]!["reclamation"]!["sharedStorage"]!;
        Assert.Null(shared["usedBytes"]);
        shared["usedBytes"] = 0;
        Assert.False(contracts.Validate("urn:splinter:v1:response", JsonSerializer.SerializeToElement(node)).IsValid);
    }

    [Fact]
    public void DeferredDiscardRequiresExplicitSameIntentRetryWhileStatusOnlyObserves()
    {
        using var d = new TestDirectory(); var store = new WorkspaceStore(d.Path);
        string id = WorkspaceStore.Key("deferred-discard-contract");
        var snapshot = new SnapshotReference("snapshot", "fake", d.Path, "snapshot-root", DateTimeOffset.UtcNow, true, true, "fake");
        var workspace = new WorkspaceRecord { Id = id, RequestKey = "discard", RequestDigest = id, ProjectPath = d.Path,
            Path = store.WorkPath(id), BaseReference = "snapshot", BaseRevision = "basis", SourceReference = "task",
            StorageMode = "ntfs-snapshot", Phase = "discard-reclaim-pending",
            Snapshot = new() { Step = "discard-snapshot", Reference = snapshot,
                Observation = new(true, false, false, true, 0, 0, 0, DeltaPresent: false),
                Reclamation = new("snapshot", "attempt", "deferred", DateTimeOffset.UtcNow, ["older"]) },
            Discard = new() { Step = "snapshot", Seal = new(1, 100, "hash"),
                Intent = new(1, id, "discard-this-task", id, d.Path, store.WorkPath(id), "task", "basis", "source", snapshot, DateTimeOffset.UtcNow) } };
        var view = new WorkspaceView(workspace, null, null, store.ControlPath(id), "workspace discard");
        var response = WorkspaceCommands.Result("workspace.discard", view, false, d.Path);
        Assert.Equal("Accepted", response.Status); Assert.False(response.Terminal);
        Assert.Equal("requires-user-action", response.RetryDisposition);
        var next = Assert.Single(response.NextActions);
        Assert.Equal("workspace.discard", next.Command); Assert.True(next.RequiresUserAction);
        var args = JsonSerializer.SerializeToElement(next.Arguments, ApiJson.Options);
        Assert.Equal("discard-this-task", args.GetProperty("intentKey").GetString());
        Assert.Equal(id, args.GetProperty("expectedRequestDigest").GetString());
        Assert.True(contracts.Validate("urn:splinter:v1:response", JsonSerializer.SerializeToElement(response, ApiJson.Options)).IsValid);
        Assert.False(contracts.Validate("urn:splinter:v1:response", JsonSerializer.SerializeToElement(response with { RetryDisposition = "not-needed" }, ApiJson.Options)).IsValid);
        Assert.False(contracts.Validate("urn:splinter:v1:response", JsonSerializer.SerializeToElement(response with { Status = "Succeeded", Terminal = true }, ApiJson.Options)).IsValid);
        var status = WorkspaceCommands.Result("workspace.status", view, false, d.Path);
        Assert.Equal("Succeeded", status.Status); Assert.Equal("not-needed", status.RetryDisposition);
        Assert.True(contracts.Validate("urn:splinter:v1:response", JsonSerializer.SerializeToElement(status, ApiJson.Options)).IsValid);
    }

    [Fact]
    public async Task StatusPollDoesNotOpenGitSourceFilesAndExplicitVerificationDetectsEdits()
    {
        using var directory = new TestDirectory(); var ct = TestContext.Current.CancellationToken;
        string project = Path.Combine(directory.Path, "project"), home = Path.Combine(directory.Path, "home");
        Directory.CreateDirectory(project);
        await TestDirectory.Git(project, "init", "-b", "development");
        await TestDirectory.Git(project, "config", "user.name", "Fixture");
        await TestDirectory.Git(project, "config", "user.email", "fixture@example.invalid");
        await File.WriteAllTextAsync(Path.Combine(project, "asset.bin"), "original", ct);
        await TestDirectory.Git(project, "add", "."); await TestDirectory.Git(project, "commit", "-m", "fixture");
        var (created, response) = await Run("workspace", "ensure", "--project", project, "--key", "poll", "--storage", "git-checkout", "--home", home);
        Assert.Equal(0, created);
        string id = response.GetProperty("data").GetProperty("workspace").GetProperty("id").GetString()!;
        var store = new WorkspaceStore(home); var record = store.Get(id);
        record.Validation = new("fixture", response.GetProperty("data").GetProperty("source").GetProperty("fingerprint").GetString()!, true, DateTimeOffset.UtcNow, []);
        store.Save(record, "fixture-validation");
        string file = Path.Combine(record.Path, "asset.bin");
        await File.WriteAllTextAsync(file, "edited", ct);
        // This exclusive handle would make the old polling fingerprint fail.
        using (var locked = new FileStream(file, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            var (polled, status) = await Run("workspace", "status", "--id", id, "--home", home);
            Assert.Equal(0, polled);
            Assert.Equal("not-checked", status.GetProperty("data").GetProperty("sourceVerification").GetString());
            Assert.False(status.GetProperty("data").GetProperty("workspace").TryGetProperty("validation", out _));
            Assert.NotNull(store.Get(id).Validation);
        }
        var (verified, fresh) = await Run("workspace", "status", "--id", id, "--home", home, "--verify-source");
        Assert.Equal(0, verified);
        Assert.Equal("verified", fresh.GetProperty("data").GetProperty("sourceVerification").GetString());
        Assert.True(fresh.GetProperty("data").GetProperty("source").GetProperty("hasChanges").GetBoolean());
        Assert.False(fresh.GetProperty("data").TryGetProperty("validationCurrent", out _));
        Assert.NotNull(store.Get(id).Validation);
        var node = JsonNode.Parse(fresh.GetRawText())!;
        node["data"]!["workspace"]!["validation"] = JsonSerializer.SerializeToNode(record.Validation, ApiJson.Options);
        Assert.False(contracts.Validate("urn:splinter:v1:response", JsonSerializer.SerializeToElement(node)).IsValid);
        node["data"]!["workspace"]!.AsObject().Remove("validation");
        var inspection = node["data"]!["source"]!["inspection"]!;
        Assert.Equal(0, inspection["gitHashProcesses"]!.GetValue<int>()); // git-checkout uses the existing full inspection path.
        inspection["gitHashProcesses"] = -1;
        Assert.False(contracts.Validate("urn:splinter:v1:response", JsonSerializer.SerializeToElement(node)).IsValid);
        inspection["gitHashProcesses"] = null;
        Assert.False(contracts.Validate("urn:splinter:v1:response", JsonSerializer.SerializeToElement(node)).IsValid);
        inspection.AsObject().Remove("gitHashProcesses");
        Assert.False(contracts.Validate("urn:splinter:v1:response", JsonSerializer.SerializeToElement(node)).IsValid);
        Assert.Throws<JsonException>(() => inspection.Deserialize<SourceInspectionMetrics>(ApiJson.Options));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StatusOptionAndSourceFreshnessAreMachineReadable(bool verifySource)
    {
        using var directory = new TestDirectory();
        var store = new WorkspaceStore(directory.Path);
        string id = WorkspaceStore.Key("status-cli");
        store.Save(new() { Id = id, RequestKey = "status-cli", ProjectPath = Path.Combine(directory.Path, "project"),
            Path = store.WorkPath(id), SourceReference = "task", BaseReference = "development" }, "fixture");
        string[] args = ["workspace", "status", "--id", id, "--home", directory.Path];
        if (verifySource) args = [.. args, "--verify-source"];
        var (exitCode, response) = await Run(args);
        Assert.Equal(0, exitCode);
        var data = response.GetProperty("data");
        Assert.Equal("not-checked", data.GetProperty("sourceVerification").GetString());
        Assert.False(data.TryGetProperty("source", out _));
        Assert.False(data.TryGetProperty("validationCurrent", out _));
        var node = JsonNode.Parse(response.GetRawText())!;
        node["data"]!["validationCurrent"] = true;
        Assert.False(contracts.Validate("urn:splinter:v1:response", JsonSerializer.SerializeToElement(node)).IsValid);
        node["data"]!.AsObject().Remove("validationCurrent");
        node["data"]!["sourceVerification"] = "verified";
        Assert.False(contracts.Validate("urn:splinter:v1:response", JsonSerializer.SerializeToElement(node)).IsValid);
    }

    [Theory]
    [InlineData("complete", true)] [InlineData("missing-tuple", false)]
    [InlineData("no-owner", false)] [InlineData("no-directory", false)] [InlineData("no-access", false)]
    public void OverlayReadAccessRequiresTheWholeVerificationTuple(string variant, bool valid)
    {
        var request = JsonSerializer.SerializeToNode(new Splinter.Platform.Windows.OverlayServeRequest(
            new(2, "workspace", VssLiveIdentityTests.Snapshot, 1048576, 1048576), "delta", "mount", "control", new string('a', 64),
            new(VssLiveIdentityTests.Snapshot.Id, VssLiveIdentityTests.Snapshot.RootPath, "verified"), "receipt", "owner"), ApiJson.Options)!;
        if (variant is "no-owner" or "missing-tuple") request.AsObject().Remove("snapshotOwnerId");
        if (variant is "no-directory" or "missing-tuple") request.AsObject().Remove("snapshotReceiptDirectory");
        if (variant is "no-access" or "missing-tuple") request.AsObject().Remove("readAccess");
        Assert.Equal(valid, contracts.Validate("urn:splinter:v1:overlay-serve-request", JsonSerializer.SerializeToElement(request)).IsValid);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PayloadCodecDiscoveryAdvertisesScopedJournalIntegrationOnlyInDevelopment(bool development)
    {
        using var output = new StringWriter();
        Assert.Equal(0, await Program.RunAsync(["capabilities"], output, TestContext.Current.CancellationToken, development: development));
        using var response = JsonDocument.Parse(output.ToString());
        var catalog = new ContractCatalog(development);
        Assert.True(catalog.Validate("urn:splinter:v1:response", response.RootElement).IsValid, output.ToString());
        var data = response.RootElement.GetProperty("data");
        if (development)
        {
            var codec = data.GetProperty("developmentPayloadCodec");
            Assert.Equal("workspace-internal-alpha", codec.GetProperty("stage").GetString());
            Assert.Equal("docs/journal-payload-compression.md", codec.GetProperty("specification").GetString());
            Assert.Equal(new Splinter.Runtime.BrotliWorkspacePayloadCodec().CodecId, codec.GetProperty("codec").GetString());
            Assert.Equal(Splinter.Runtime.BrotliWorkspacePayloadCodec.MaximumBlockBytes, codec.GetProperty("maxDecodedBytes").GetInt32());
            Assert.Equal(Splinter.Runtime.BrotliWorkspacePayloadCodec.BlockOverheadBytes, codec.GetProperty("blockOverheadBytes").GetInt32());
            var unsupported = JsonNode.Parse(response.RootElement.GetRawText())!;
            unsupported["data"]!["developmentPayloadCodec"]!["stage"] = "standalone-unqualified";
            Assert.False(catalog.Validate("urn:splinter:v1:response", JsonSerializer.SerializeToElement(unsupported)).IsValid);
        }
        else Assert.True(!data.TryGetProperty("developmentPayloadCodec", out var hidden) || hidden.ValueKind == JsonValueKind.Null);
        Assert.DoesNotContain(catalog.Commands, command => command.Id.Contains("codec", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CapabilitiesOnlyAdvertiseImplementedCommands()
    {
        var (exitCode, response) = await Run("capabilities");
        Assert.Equal(0, exitCode);
        Assert.False(response.GetProperty("data").GetProperty("persistentHost").GetBoolean());
        var commands = response.GetProperty("data").GetProperty("commands").EnumerateArray()
            .Select(item => item.GetProperty("id").GetString()).ToArray();
        Assert.Equal(contracts.Commands.Select(command => command.Id), commands);
        Assert.DoesNotContain("workspace.create", commands);
        Assert.DoesNotContain("plan.validate", commands);
        Assert.DoesNotContain("urn:splinter:v1:plan-request", contracts.SchemaIds);
        var unavailable = response.GetProperty("data").GetProperty("unavailable").EnumerateArray().Select(item => item.GetString()).ToArray();
        Assert.Contains("workspace.discard", commands);
        Assert.DoesNotContain("workspace.discard", unavailable);
        Assert.DoesNotContain("plan.submit", unavailable);
        Assert.DoesNotContain("agent.start", unavailable);
        foreach (var descriptor in response.GetProperty("data").GetProperty("commands").EnumerateArray())
            Assert.Contains(descriptor.GetProperty("sideEffects").GetString(), contracts.Commands.Select(x => x.SideEffects));
    }

    [Fact]
    public void AcceptedWorkspaceResponseMatchesContract()
    {
        using var document = JsonDocument.Parse("""
        {"schemaVersion":"1.0","requestId":"00000000000000000000000000000000","command":"workspace.ensure","status":"Accepted","terminal":false,"retryDisposition":"not-needed","nextActions":[{"command":"workspace.status","arguments":{"id":"aaaaaaaaaaaaaaaaaaaaaaaa"},"requiresUserAction":false}],"data":{"workspace":{"version":2,"sourceKind":"git","applicationKind":"unity","id":"aaaaaaaaaaaaaaaaaaaaaaaa","requestKey":"task","projectPath":"C:/project","path":"C:/workspace","sourceReference":"development","baseReference":"development","phase":"creating","createdAt":"2026-09-21T00:00:00Z","updatedAt":"2026-09-21T00:00:00Z","storageMode":"git-checkout"},"controlPath":"C:/control","nextCommand":"workspace.status","isolation":"private"}}
        """);
        Assert.True(contracts.Validate("urn:splinter:v1:response", document.RootElement).IsValid);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [InlineData(null)]
    public void SnapshotSchemaRequiresIdentityAndKeepsUnknownCountersAbsent(bool? available)
    {
        var node = JsonSerializer.SerializeToNode(ApiResponse.Success("workspace.status", new
        {
            workspace = new { version = WorkspaceRecord.CurrentVersion, sourceKind = "git", applicationKind = "unity", id = "aaaaaaaaaaaaaaaaaaaaaaaa", requestKey = "task", projectPath = "C:/project", path = "C:/work",
                sourceReference = "task", baseReference = "development", phase = "creating", createdAt = DateTimeOffset.UtcNow,
                updatedAt = DateTimeOffset.UtcNow, storageMode = "ntfs-snapshot", requestDigest = "aaaaaaaaaaaaaaaaaaaaaaaa",
                snapshot = new SnapshotWorkspaceState { Observation = new(available, false, false, false, null, null, null,
                    FailureCode: "OverlayStartUncertain", SnapshotStorage: new SnapshotObservation("snapshot", available, null, null, null)
                    { ReadAccess = available == true ? new("snapshot", "verified-read-root", "verification-id") : null }) } },
            controlPath = "C:/control", nextCommand = "workspace.status", isolation = "private",
            recovery = new SnapshotRecoveryDecision("preserve", "OverlayStartUncertain", "workspace.status")
        }), ApiJson.Options)!;
        Assert.True(contracts.Validate("urn:splinter:v1:response", JsonSerializer.SerializeToElement(node)).IsValid);
        Assert.Null(node["data"]!["workspace"]!["snapshot"]!["observation"]!["privateBytes"]);
        var snapshot = node["data"]!["workspace"]!["snapshot"]!;
        snapshot["result"] = JsonSerializer.SerializeToNode(new SnapshotResult("snapshot", "revision", "fingerprint", "result-ref",
            new(1, 100, "hash"), true, SnapshotResult.StableJournalProtocol), ApiJson.Options);
        Assert.True(contracts.Validate("urn:splinter:v1:response", JsonSerializer.SerializeToElement(node)).IsValid);
        snapshot["result"]!["collectionProtocol"] = "unverified";
        Assert.False(contracts.Validate("urn:splinter:v1:response", JsonSerializer.SerializeToElement(node)).IsValid);
        snapshot["result"]!.AsObject().Remove("collectionProtocol");
        Assert.False(contracts.Validate("urn:splinter:v1:response", JsonSerializer.SerializeToElement(node)).IsValid);
        Assert.Throws<JsonException>(() => snapshot["result"]!.Deserialize<SnapshotResult>(ApiJson.Options));
        snapshot["result"]!["collectionProtocol"] = SnapshotResult.StableJournalProtocol;
        var observation = node["data"]!["workspace"]!["snapshot"]!["observation"]!;
        if (available is null)
        {
            Assert.Null(observation["snapshotAvailable"]);
            observation["snapshotAvailable"] = false;
            Assert.False(contracts.Validate("urn:splinter:v1:response", JsonSerializer.SerializeToElement(node)).IsValid);
            observation.AsObject().Remove("snapshotAvailable");
        }
        node["data"]!["workspace"]!.AsObject().Remove("snapshot");
        Assert.False(contracts.Validate("urn:splinter:v1:response", JsonSerializer.SerializeToElement(node)).IsValid);
    }

    [Theory]
    [InlineData("mounted", "available", true)]
    [InlineData("snapshot-missing", "missing", true)]
    [InlineData("snapshot-unknown", "unknown", true)]
    [InlineData("mounted", "unknown", false)]
    [InlineData("snapshot-missing", "unknown", false)]
    [InlineData("snapshot-unknown", "missing", false)]
    public void OverlayStateCannotReportUnknownSnapshotAsMountedOrMissing(string phase, string availability, bool valid)
    {
        var observation = JsonSerializer.SerializeToElement(new
        {
            version = 1, token = "test", id = "test", phase, observedAt = DateTimeOffset.UtcNow,
            processId = 1, snapshotId = "snapshot", snapshotAvailability = availability
        });
        Assert.Equal(valid, contracts.Validate("urn:splinter:v1:overlay-observation", observation).IsValid);
    }

    [Fact]
    public void OverlayReplayStateAndMetricsMatchThePublishedObservationContract()
    {
        using var dir = new TestDirectory(); var tree = new SnapshotOverlayTests.FakeSnapshotTree();
        var identity = new OverlayIdentity(2, "replay-contract", tree.Snapshot, 65536, 4096);
        using var store = new JournalDeltaStore(Path.Combine(dir.Path, "delta"), identity, tree);
        store.Create("live-cost-contract", false, 32);
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var node = JsonSerializer.SerializeToNode(new
        {
            version = 1, token = "test", id = identity.Id, phase = "replaying", observedAt = DateTimeOffset.UtcNow,
            processId = 1, snapshotId = tree.Snapshot.Id
        }, json)!;
        bool Valid() => contracts.Validate("urn:splinter:v1:overlay-observation", JsonSerializer.SerializeToElement(node)).IsValid;
        Assert.True(Valid());
        node["phase"] = "mounted";
        node["preparation"] = JsonSerializer.SerializeToNode(new { storeReadyTicks = 1, stopwatchFrequency = 1000 });
        node["storage"] = JsonSerializer.SerializeToNode(store.Observe(), json);
        Assert.True(Valid());
        var operationSchema = contracts.GetSchema("urn:splinter:v1:overlay-observation").GetProperty("properties")
            .GetProperty("storage").GetProperty("properties").GetProperty("metrics").GetProperty("properties").GetProperty("replayIndexOperations");
        var responseSchema = contracts.GetSchema("urn:splinter:v1:response").GetProperty("$defs")
            .GetProperty("overlayIoMetrics").GetProperty("properties").GetProperty("replayIndexOperations");
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(operationSchema.GetRawText()), JsonNode.Parse(responseSchema.GetRawText())));
        var rangeSchema = contracts.GetSchema("urn:splinter:v1:overlay-observation").GetProperty("properties")
            .GetProperty("storage").GetProperty("properties").GetProperty("metrics").GetProperty("properties").GetProperty("replayWriteRangeIndex");
        var rangeResponseSchema = contracts.GetSchema("urn:splinter:v1:response").GetProperty("$defs")
            .GetProperty("overlayIoMetrics").GetProperty("properties").GetProperty("replayWriteRangeIndex");
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(rangeSchema.GetRawText()), JsonNode.Parse(rangeResponseSchema.GetRawText())));
        var liveSchema = contracts.GetSchema("urn:splinter:v1:overlay-observation").GetProperty("properties")
            .GetProperty("storage").GetProperty("properties").GetProperty("metrics").GetProperty("properties").GetProperty("liveJournalWrites");
        var liveResponseSchema = contracts.GetSchema("urn:splinter:v1:response").GetProperty("$defs")
            .GetProperty("overlayIoMetrics").GetProperty("properties").GetProperty("liveJournalWrites");
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(liveSchema.GetRawText()), JsonNode.Parse(liveResponseSchema.GetRawText())));
        var readSchema = contracts.GetSchema("urn:splinter:v1:overlay-observation").GetProperty("properties")
            .GetProperty("storage").GetProperty("properties").GetProperty("metrics").GetProperty("properties").GetProperty("readRangeIndex");
        var readResponseSchema = contracts.GetSchema("urn:splinter:v1:response").GetProperty("$defs")
            .GetProperty("overlayIoMetrics").GetProperty("properties").GetProperty("readRangeIndex");
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(readSchema.GetRawText()), JsonNode.Parse(readResponseSchema.GetRawText())));
        node["storage"]!["metrics"]!["readRangeIndex"]!["searchProbes"] = -1;
        Assert.False(Valid());
        node["storage"]!["metrics"]!["readRangeIndex"]!["searchProbes"] = 0;
        Assert.True(Valid());
        node["storage"]!["metrics"]!["readRangeIndex"] = null;
        Assert.False(Valid());
        node["storage"]!["metrics"]!["readRangeIndex"] = JsonSerializer.SerializeToNode(store.Observe().Metrics!.ReadRangeIndex, json);
        node["storage"]!["metrics"]!["liveJournalWrites"]!["operations"]![0]!["waitingMutationsAtFlushStart"]![0] = -1;
        Assert.False(Valid());
        node["storage"]!["metrics"]!["liveJournalWrites"]!["operations"]![0]!["waitingMutationsAtFlushStart"]![0] = 1;
        Assert.True(Valid());
        node["storage"]!["metrics"]!["replayWriteRangeIndex"]!["priorExtentBuckets"]![0] = -1;
        Assert.False(Valid());
        node["storage"]!["metrics"]!["replayWriteRangeIndex"]!["priorExtentBuckets"]![0] = 0;
        Assert.True(Valid());
        node["storage"]!["metrics"]!["replayIndexOperations"] = JsonNode.Parse("""[{"operation":"rename","frames":1,"applyTicks":0}]""");
        Assert.True(Valid());
        node["storage"]!["metrics"]!["replayIndexOperations"]![0]!["operation"] = "unknown";
        Assert.False(Valid());
        node["storage"]!["metrics"]!["replayIndexOperations"]![0]!["operation"] = "write";
        node["storage"]!["metrics"]!["replayIndexOperations"]![0]!["frames"] = 0;
        Assert.False(Valid());
        node["storage"]!["metrics"]!["replayIndexOperations"] = null;
        Assert.False(Valid());
        node["storage"]!["metrics"]!["replayIndexOperations"] = new JsonArray();
        node["storage"]!["metrics"]!["replayReadCalls"] = -1;
        Assert.False(Valid());
        node["storage"]!["metrics"] = JsonSerializer.SerializeToNode(store.Observe().Metrics, json);
        foreach (string field in new[] { "replayFrames", "replayReadCalls", "replayReadTicks", "replayValidationTicks", "replayIndexTicks", "replayBufferBytes", "replayIndexOperations", "replayWriteRangeIndex", "liveJournalWrites", "readRangeIndex" })
        {
            var complete = node["storage"]!["metrics"]!.DeepClone();
            node["storage"]!["metrics"]!.AsObject().Remove(field);
            Assert.False(Valid());
            Assert.Throws<JsonException>(() => node["storage"]!["metrics"]!.Deserialize<OverlayIoMetrics>(json));
            node["storage"]!["metrics"] = complete;
        }
        Assert.True(Valid());
        node["preparation"]!["storeReadyTicks"] = -1;
        Assert.False(Valid());
    }

    [Theory]
    [InlineData("ntfs-snapshot", 4096, true)]
    [InlineData("ntfs-snapshot", 4095, false)]
    [InlineData("ntfs-snapshot", 1099511627777, false)]
    [InlineData("git-checkout", 4096, false)]
    public void JournalBudgetBelongsOnlyToSnapshotRequests(string storage, long maxJournalBytes, bool valid)
    {
        Assert.Equal(valid, contracts.Validate("urn:splinter:v1:workspace-development-ensure-request",
            JsonSerializer.SerializeToElement(new { project = "C:/project", key = "task", storage, maxJournalBytes })).IsValid);
    }

    [Fact]
    public async Task PendingSnapshotStatusReturnsAnAbsoluteResumeHomeAndNoInventedStorage()
    {
        using var d = new TestDirectory(); var store = new WorkspaceStore(d.Path);
        string id = WorkspaceStore.Key("pending-snapshot");
        store.Save(new WorkspaceRecord { Id = id, RequestKey = "task", RequestDigest = id, ProjectPath = Path.Combine(d.Path, "source"),
            Path = store.WorkPath(id), BaseReference = "development", SourceReference = "splinter/task", StorageMode = "ntfs-snapshot", Snapshot = new() }, "fixture-pending");
        var (exit, response) = await Run("workspace", "status", "--id", id, "--home", Path.GetRelativePath(Environment.CurrentDirectory, d.Path));
        Assert.Equal(0, exit);
        Assert.Equal(d.Path, response.GetProperty("nextActions")[0].GetProperty("arguments").GetProperty("home").GetString());
        var observation = response.GetProperty("data").GetProperty("workspace").GetProperty("snapshot").GetProperty("observation");
        Assert.False(observation.GetProperty("snapshotAvailable").GetBoolean());
        Assert.False(observation.GetProperty("deltaPresent").GetBoolean());
        Assert.False(observation.TryGetProperty("storage", out _));
        Assert.Equal("resume-ensure", response.GetProperty("data").GetProperty("recovery").GetProperty("disposition").GetString());
        Assert.Equal(response.GetProperty("data").GetProperty("nextCommand").GetString(), response.GetProperty("nextActions")[0].GetProperty("command").GetString());
    }

    [Theory]
    [InlineData("capabilities")]
    [InlineData("schema")]
    [InlineData("doctor")]
    [InlineData("workspace.ensure")]
    public async Task SchemasAreAvailableWithoutAnyHostOrWorkingDirectoryState(string command)
    {
        var (exitCode, response) = await Run("schema", command);
        Assert.Equal(0, exitCode);
        Assert.Equal(command, response.GetProperty("data").GetProperty("descriptor").GetProperty("id").GetString());
        Assert.True(response.GetProperty("data").GetProperty("input").TryGetProperty("$schema", out _));
    }

    [Theory]
    [InlineData("--unknown")]
    [InlineData("workspace")]
    [InlineData("doctor")]
    public async Task InvalidArgumentsRemainOneStructuredResponse(string argument)
    {
        var (exitCode, response) = await Run(argument);
        Assert.Equal(2, exitCode);
        Assert.Equal("InvalidArguments", response.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task HelpIsDiscoverableJsonAndDoesNotNeedRequiredArguments()
    {
        var (exitCode, response) = await Run("doctor", "--help", "--json");
        Assert.Equal(0, exitCode);
        Assert.Equal("capabilities", response.GetProperty("command").GetString());
    }

    [Fact]
    public async Task RemovedTaskGraphEntryPointsReturnStructuredErrors()
    {
        var (exitCode, response) = await Run("plan", "validate", "--input", "must-not-be-opened.json");
        Assert.Equal(2, exitCode);
        Assert.Equal("InvalidArguments", response.GetProperty("error").GetProperty("code").GetString());
        var (schemaExit, schemaResponse) = await Run("schema", "plan.validate");
        Assert.Equal(9, schemaExit);
        Assert.Equal("UnknownCommand", schemaResponse.GetProperty("error").GetProperty("code").GetString());
    }

    [Theory]
    [InlineData("{bad json", "InvalidJson")]
    [InlineData("{}", "ValidationRequestInvalid")]
    public async Task InvalidInputCannotLeakParserTextIntoStdout(string json, string error)
    {
        using var directory = new TestDirectory();
        string file = Path.Combine(directory.Path, "怨꾪쉷 with spaces.json");
        await File.WriteAllTextAsync(file, json, TestContext.Current.CancellationToken);
        var (exitCode, response) = await Run("view", "--input", file);
        Assert.Equal(2, exitCode);
        Assert.Equal(error, response.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task UnknownSchemaProvidesRecoveryAction()
    {
        var (exitCode, response) = await Run("schema", "workspace.create");
        Assert.Equal(9, exitCode);
        Assert.Equal("capabilities", response.GetProperty("nextActions")[0].GetProperty("command").GetString());
    }

    [Fact]
    public void ResponseSchemaRejectsFalseExecutionSuccessAndMissingErrors()
    {
        using var invalid = JsonDocument.Parse("""
            {"schemaVersion":"1.0","requestId":"00000000000000000000000000000000",
             "command":"doctor","status":"Succeeded","terminal":true,
             "retryDisposition":"not-needed","nextActions":[],
             "data":{"path":"x","hasCommit":true,"readyForExecution":true,
                     "checksNotRun":[],"missingPrerequisites":[]}}
            """);
        Assert.False(contracts.Validate("urn:splinter:v1:response", invalid.RootElement).IsValid);
    }

    [Fact]
    public void CompactResponseCostMustMatchTheWorkspaceContract()
    {
        var response = JsonNode.Parse("""
            {"schemaVersion":"1.0","requestId":"00000000000000000000000000000000",
             "command":"workspace.compact","status":"Succeeded","terminal":true,
             "retryDisposition":"not-needed","nextActions":[],
             "data":{"workspace":{"version":2,"sourceKind":"git","applicationKind":"unity","id":"000000000000000000000000","requestKey":"test",
                 "projectPath":"P:","path":"W:","sourceReference":"development",
                 "baseReference":"development","phase":"ready","createdAt":"2026-09-26T00:00:00Z",
                 "updatedAt":"2026-09-26T00:00:00Z","storageMode":"ntfs-snapshot","requestDigest":"test",
                 "snapshot":{"maxJournalBytes":4096,"attached":false,"step":"ready",
                     "compaction":{"attemptId":"test","startedAt":"2026-09-26T00:00:00Z",
                         "before":{"durableSequence":1,"journalBytes":4096,"journalHash":"before"},
                         "after":{"durableSequence":1,"journalBytes":2048,"journalHash":"after"},
                         "cost":{"stopwatchFrequency":10000000,"totalTicks":100,
                             "stages":[{"stage":"candidate-write","elapsedTicks":50,
                                 "readBytes":4096,"writtenBytes":2048,"flushCount":0}]}}}},
                 "controlPath":"control","nextCommand":"workspace.status","isolation":"snapshot"}}
            """)!;
        using (var valid = JsonDocument.Parse(response.ToJsonString()))
            Assert.True(contracts.Validate("urn:splinter:v1:response", valid.RootElement).IsValid);

        var snapshot = response["data"]!["workspace"]!["snapshot"]!;
        snapshot["step"] = "compact-write";
        snapshot["pendingCompactionMaxEstimatedJournalOverlapBytes"] = 1024;
        using (var pending = JsonDocument.Parse(response.ToJsonString()))
            Assert.True(contracts.Validate("urn:splinter:v1:response", pending.RootElement).IsValid);
        snapshot["pendingCompactionMaxEstimatedJournalOverlapBytes"] = 0;
        using (var invalidPending = JsonDocument.Parse(response.ToJsonString()))
            Assert.False(contracts.Validate("urn:splinter:v1:response", invalidPending.RootElement).IsValid);
        snapshot["pendingCompactionMaxEstimatedJournalOverlapBytes"] = 1024;
        response["data"]!["workspace"]!["snapshot"]!["compaction"]!["cost"]!["stages"]![0]!["readBytes"] = -1;
        using var invalid = JsonDocument.Parse(response.ToJsonString());
        Assert.False(contracts.Validate("urn:splinter:v1:response", invalid.RootElement).IsValid);
    }

    [Fact]
    public async Task CompactCliValidatesEstimatedOverlapBudgetBeforeOpeningWorkspace()
    {
        const string id = "000000000000000000000000";
        using var directory = new TestDirectory();
        var (invalidExit, invalid) = await Run("workspace", "compact", "--id", id,
            "--max-estimated-journal-overlap-bytes", "0", "--home", directory.Path);
        Assert.Equal(2, invalidExit);
        Assert.Equal("SchemaValidationFailed", invalid.GetProperty("error").GetProperty("code").GetString());

        var (validExit, valid) = await Run("workspace", "compact", "--id", id,
            "--max-estimated-journal-overlap-bytes", "12345", "--home", directory.Path);
        Assert.Equal(2, validExit);
        Assert.NotEqual("SchemaValidationFailed", valid.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task DiscardCliRequiresExplicitIntentAndConfirmedDigest()
    {
        var (schemaExit, schema) = await Run("schema", "workspace.discard");
        Assert.Equal(0, schemaExit); Assert.Equal("Succeeded", schema.GetProperty("status").GetString());
        using var directory = new TestDirectory(); string id = new('a', 24);
        var (missingExit, missing) = await Run("workspace", "discard", "--id", id, "--home", directory.Path);
        Assert.Equal(2, missingExit); Assert.Equal("InvalidArguments", missing.GetProperty("error").GetProperty("code").GetString());
        var (invalidExit, invalid) = await Run("workspace", "discard", "--id", id, "--intent-key", "discard", "--expected-request-digest", "wrong", "--home", directory.Path);
        Assert.Equal(2, invalidExit); Assert.Equal("SchemaValidationFailed", invalid.GetProperty("error").GetProperty("code").GetString());
        var (validExit, valid) = await Run("workspace", "discard", "--id", id, "--intent-key", "discard", "--expected-request-digest", new string('b', 24), "--home", directory.Path);
        Assert.Equal(2, validExit); Assert.Equal("WorkspaceNotFound", valid.GetProperty("error").GetProperty("code").GetString());
    }

    private async Task<(int ExitCode, JsonElement Response)> Run(params string[] args)
    {
        using var output = new StringWriter();
        int exitCode = await Program.RunAsync(args, output, TestContext.Current.CancellationToken, development: true);
        string text = output.ToString().TrimEnd('\r', '\n');
        Assert.DoesNotContain('\n', text);
        using var document = JsonDocument.Parse(text);
        var response = document.RootElement.Clone();
        Assert.True(contracts.Validate("urn:splinter:v1:response", response).IsValid, text);
        return (exitCode, response);
    }
}
