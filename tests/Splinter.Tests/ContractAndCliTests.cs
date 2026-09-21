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
                    FailureCode: "OverlayStartUncertain", SnapshotStorage: new("snapshot", available, null, null, null)) } },
            controlPath = "C:/control", nextCommand = "workspace.status", isolation = "private",
            recovery = new SnapshotRecoveryDecision("preserve", "OverlayStartUncertain", "workspace.status")
        }), ApiJson.Options)!;
        Assert.True(contracts.Validate("urn:splinter:v1:response", JsonSerializer.SerializeToElement(node)).IsValid);
        Assert.Null(node["data"]!["workspace"]!["snapshot"]!["observation"]!["privateBytes"]);
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
