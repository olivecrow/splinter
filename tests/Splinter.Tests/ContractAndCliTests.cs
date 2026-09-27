using System.Text.Json;
using System.Text.Json.Nodes;
using Splinter.Cli;
using Splinter.Protocol;
using Splinter.Core;
using Splinter.Runtime;

namespace Splinter.Tests;

public sealed class ContractAndCliTests
{
    private readonly ContractCatalog contracts = new();

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
            Assert.True(status.GetProperty("data").GetProperty("workspace").GetProperty("validation").GetProperty("success").GetBoolean());
        }
        var (verified, fresh) = await Run("workspace", "status", "--id", id, "--home", home, "--verify-source");
        Assert.Equal(0, verified);
        Assert.Equal("verified", fresh.GetProperty("data").GetProperty("sourceVerification").GetString());
        Assert.True(fresh.GetProperty("data").GetProperty("source").GetProperty("hasChanges").GetBoolean());
        Assert.False(fresh.GetProperty("data").GetProperty("validationCurrent").GetBoolean());
        Assert.Null(store.Get(id).Validation);
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
    [InlineData("complete", true)] [InlineData("legacy", true)]
    [InlineData("no-owner", false)] [InlineData("no-directory", false)] [InlineData("no-access", false)]
    public void OverlayReadAccessRequiresTheWholeVerificationTuple(string variant, bool valid)
    {
        var request = JsonSerializer.SerializeToNode(new Splinter.Platform.Windows.OverlayServeRequest(
            new(1, "workspace", VssLiveIdentityTests.Snapshot, 1048576, 1048576), "delta", "mount", "control", new string('a', 64),
            new(VssLiveIdentityTests.Snapshot.Id, VssLiveIdentityTests.Snapshot.RootPath, "verified"), "receipt", "owner"), ApiJson.Options)!;
        if (variant is "no-owner" or "legacy") request.AsObject().Remove("snapshotOwnerId");
        if (variant is "no-directory" or "legacy") request.AsObject().Remove("snapshotReceiptDirectory");
        if (variant is "no-access" or "legacy") request.AsObject().Remove("readAccess");
        Assert.Equal(valid, contracts.Validate("urn:splinter:v1:overlay-serve-request", JsonSerializer.SerializeToElement(request)).IsValid);
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
        var plan = response.GetProperty("data").GetProperty("commands").EnumerateArray()
            .Single(item => item.GetProperty("id").GetString() == "plan.validate");
        Assert.Equal("splinter plan validate --input <plan.json>", plan.GetProperty("cliUsage").GetString());
        foreach (var descriptor in response.GetProperty("data").GetProperty("commands").EnumerateArray())
            Assert.Contains(descriptor.GetProperty("sideEffects").GetString(), contracts.Commands.Select(x => x.SideEffects));
    }

    [Fact]
    public void AcceptedWorkspaceResponseMatchesContract()
    {
        using var document = JsonDocument.Parse("""
        {"schemaVersion":"1.0","requestId":"00000000000000000000000000000000","command":"workspace.ensure","status":"Accepted","terminal":false,"retryDisposition":"not-needed","nextActions":[{"command":"workspace.status","arguments":{"id":"aaaaaaaaaaaaaaaaaaaaaaaa"},"requiresUserAction":false}],"data":{"workspace":{"id":"aaaaaaaaaaaaaaaaaaaaaaaa","requestKey":"task","projectPath":"C:/project","path":"C:/workspace","sourceReference":"development","baseReference":"development","phase":"creating","createdAt":"2026-09-21T00:00:00Z","updatedAt":"2026-09-21T00:00:00Z","storageMode":"git-checkout"},"controlPath":"C:/control","nextCommand":"workspace.status","isolation":"private"}}
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
            workspace = new { id = "aaaaaaaaaaaaaaaaaaaaaaaa", requestKey = "task", projectPath = "C:/project", path = "C:/work",
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
        Assert.True(contracts.Validate("urn:splinter:v1:response", JsonSerializer.SerializeToElement(node)).IsValid);
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
        var identity = new OverlayIdentity(1, "replay-contract", tree.Snapshot, 65536, 4096);
        using var store = new JournalDeltaStore(Path.Combine(dir.Path, "delta"), identity, tree);
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
        node["storage"]!["metrics"]!["replayIndexOperations"] = JsonNode.Parse("""[{"operation":"rename","frames":1,"applyTicks":0}]""");
        Assert.True(Valid());
        node["storage"]!["metrics"]!["replayIndexOperations"]![0]!["operation"] = "unknown";
        Assert.False(Valid());
        node["storage"]!["metrics"]!["replayIndexOperations"]![0]!["operation"] = "write";
        node["storage"]!["metrics"]!["replayIndexOperations"]![0]!["frames"] = 0;
        Assert.False(Valid());
        node["storage"]!["metrics"]!["replayIndexOperations"] = null;
        Assert.True(Valid());
        node["storage"]!["metrics"]!["replayReadCalls"] = -1;
        Assert.False(Valid());
        foreach (string field in new[] { "replayFrames", "replayReadCalls", "replayReadTicks", "replayValidationTicks", "replayIndexTicks", "replayBufferBytes", "replayIndexOperations" })
            node["storage"]!["metrics"]!.AsObject().Remove(field);
        Assert.True(Valid()); // Legacy observations keep the breakdown unknown.
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
        Assert.Equal(valid, contracts.Validate("urn:splinter:v1:workspace-ensure-request",
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
    [InlineData("plan.validate")]
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
    public async Task ValidPlanDoesNotClaimAuthorizationOrExecutionReadiness()
    {
        var (exitCode, response) = await Run("plan", "validate", "--input", Path.Combine(AppContext.BaseDirectory, "parallel-plan.json"));
        Assert.Equal(0, exitCode);
        var data = response.GetProperty("data");
        Assert.Equal(2, data.GetProperty("taskCount").GetInt32());
        Assert.Equal(2, data.GetProperty("executionLayers")[0].GetArrayLength());
        Assert.False(data.GetProperty("readyForExecution").GetBoolean());
        Assert.Contains("policy-authorization", data.GetProperty("checksNotRun").EnumerateArray().Select(item => item.GetString()));
    }

    [Theory]
    [InlineData("budget")]
    [InlineData("unknown-field")]
    [InlineData("version")]
    [InlineData("empty-goal")]
    public void PlanSchemaRejectsAmbiguousOrUnsupportedRequests(string mutation)
    {
        var node = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "parallel-plan.json")))!;
        switch (mutation)
        {
            case "budget": node["limits"]!["maxWorkers"] = 0; break;
            case "unknown-field": node["bypassApprovals"] = true; break;
            case "version": node["schemaVersion"] = "2.0"; break;
            case "empty-goal": node["tasks"]![0]!["goal"] = "   "; break;
        }
        using var document = JsonDocument.Parse(node.ToJsonString());
        Assert.False(contracts.Validate("urn:splinter:v1:plan-request", document.RootElement).IsValid);
    }

    [Theory]
    [InlineData("{bad json", "InvalidJson")]
    [InlineData("{}", "SchemaValidationFailed")]
    public async Task InvalidInputCannotLeakParserTextIntoStdout(string json, string error)
    {
        using var directory = new TestDirectory();
        string file = Path.Combine(directory.Path, "계획 with spaces.json");
        await File.WriteAllTextAsync(file, json, TestContext.Current.CancellationToken);
        var (exitCode, response) = await Run("plan", "validate", "--input", file);
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

    [Theory]
    [InlineData("missing")]
    [InlineData("cycle")]
    public async Task SchemaValidButInvalidGraphsCannotPassCliValidation(string mutation)
    {
        var plan = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "parallel-plan.json")))!;
        string taskId = plan["tasks"]![0]!["taskId"]!.GetValue<string>();
        plan["tasks"]![0]!["dependsOn"] = new JsonArray(mutation == "cycle" ? taskId : "missing-task");
        using var directory = new TestDirectory();
        string path = Path.Combine(directory.Path, "plan.json");
        await File.WriteAllTextAsync(path, plan.ToJsonString(), TestContext.Current.CancellationToken);
        var (exitCode, response) = await Run("plan", "validate", "--input", path);
        Assert.Equal(2, exitCode);
        Assert.Equal("InvalidTaskGraph", response.GetProperty("error").GetProperty("code").GetString());
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
             "data":{"workspace":{"id":"000000000000000000000000","requestKey":"test",
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

    private async Task<(int ExitCode, JsonElement Response)> Run(params string[] args)
    {
        using var output = new StringWriter();
        int exitCode = await Program.RunAsync(args, output, TestContext.Current.CancellationToken);
        string text = output.ToString().TrimEnd('\r', '\n');
        Assert.DoesNotContain('\n', text);
        using var document = JsonDocument.Parse(text);
        var response = document.RootElement.Clone();
        Assert.True(contracts.Validate("urn:splinter:v1:response", response).IsValid, text);
        return (exitCode, response);
    }
}
