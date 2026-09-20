using System.Text.Json;
using System.Text.Json.Nodes;
using Splinter.Cli;
using Splinter.Protocol;

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
