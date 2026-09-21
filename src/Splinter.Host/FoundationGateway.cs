using System.Text.Json;
using Splinter.Core;
using Splinter.Protocol;

namespace Splinter.Host;

// Discovery and inspection remain read-only. Runtime mutations are composed by
// the separate durable workspace coordinator, never by this inspection gateway.
public sealed class FoundationGateway(
    ContractCatalog contracts,
    IRepositoryInspector repositories,
    IApplicationInspector applications)
{
    public ApiResponse Capabilities() => ApiResponse.Success("capabilities", new
    {
        implementationStage = "internal-alpha",
        commands = contracts.Commands,
        unavailable = new[] { "plan.submit", "unity.execute", "unity.play", "unity.tests", "agent.start" },
        persistentHost = false,
        persistentState = true,
        stateBackend = "atomic-json-with-operation-locks",
        storageModes = new[] { "refs-cow", "git-checkout" },
        snapshotOverlay = new
        {
            stage = "experimental-storage-spike",
            integratedWorkspaceMode = false,
            sourceSnapshot = "windows-vss-system",
            deltaStore = "bounded-checksummed-range-journal",
            requestSchema = "urn:splinter:v1:overlay-serve-request",
            observationSchema = "urn:splinter:v1:overlay-observation",
            entrypoints = new[] { "scripts/setup-snapshot-overlay.ps1", "scripts/vss-snapshot.ps1",
                "dotnet Splinter.Platform.Windows.dll overlay-serve <request.json>",
                "scripts/test-snapshot-overlay-io.ps1", "scripts/test-snapshot-overlay-concurrency.ps1",
                "scripts/measure-overlay-small-writes.ps1", "scripts/invoke-snapshot-spike.ps1",
                "scripts/measure-snapshot-overlay.ps1", "scripts/test-snapshot-overlay-faults.ps1",
                "scripts/prepare-snapshot-overlay-test.ps1", "scripts/test-snapshot-overlay-compatibility.ps1",
                "scripts/test-overlay-journal-recovery.ps1", "scripts/test-owned-snapshot-loss.ps1",
                "scripts/preserve-snapshot-test-results.ps1", "scripts/measure-overlay-journal.py",
                "scripts/preserve-snapshot-overlay-unity.ps1",
                "scripts/reclaim-snapshot-overlay-test.ps1", "scripts/release-snapshot-overlay-test.ps1" },
            qualification = "Experimental fixture evidence is recorded in docs/verification/snapshot-overlay.md; workspace integration and reboot recovery are not qualified"
        }
    });

    public ApiResponse Schema(string command)
    {
        var descriptor = contracts.Find(command);
        return descriptor is null
            ? ApiResponse.Failure("schema", "UnknownCommand", $"No implemented command '{command}'.",
                nextActions: [new("capabilities", new { })])
            : ApiResponse.Success("schema", new
            {
                descriptor,
                input = contracts.GetSchema(descriptor.InputSchema),
                output = contracts.GetSchema(descriptor.OutputSchema)
            });
    }

    public async Task<ApiResponse> DoctorAsync(string path, CancellationToken cancellationToken = default)
    {
        string fullPath = Path.GetFullPath(path);
        if (!Directory.Exists(fullPath))
            return ApiResponse.Failure("doctor", "ProjectPathNotFound", "The project path must be an existing directory.");
        var repository = await repositories.InspectAsync(fullPath, cancellationToken);
        var application = applications.Inspect(fullPath);
        return ApiResponse.Success("doctor", new
        {
            path = fullPath,
            repository,
            application,
            hasCommit = repository?.Revision is not null,
            readyForExecution = false,
            checksNotRun = new[] { "refs-block-cloning", "unity-editor", "agent-authentication", "host-recovery" },
            missingPrerequisites = repository is null ? new[] { "GitRepository" }
                : repository.Revision is null ? new[] { "InitialCommit" } : Array.Empty<string>()
        });
    }

    public ApiResponse ValidatePlan(JsonElement input)
    {
        var descriptor = contracts.Find("plan.validate")!;
        var schemaResult = contracts.Validate(descriptor.InputSchema, input);
        if (!schemaResult.IsValid)
            return ApiResponse.Failure("plan.validate", "SchemaValidationFailed", "The plan does not match the input contract.",
                schemaResult, [new("schema", new { command = "plan.validate" })]);

        var tasks = input.GetProperty("tasks").EnumerateArray().Select(task => new PlanTask(
            task.GetProperty("taskId").GetString()!,
            task.GetProperty("dependsOn").EnumerateArray().Select(item => item.GetString()!).ToArray())).ToArray();
        var result = PlanGraph.Validate(tasks);
        if (!result.IsValid)
            return ApiResponse.Failure("plan.validate", "InvalidTaskGraph", "Task dependencies are invalid.", result.Issues);

        return ApiResponse.Success("plan.validate", new
        {
            taskCount = tasks.Length,
            executionLayers = result.ExecutionLayers,
            validationScope = "schema-and-dependency-graph",
            readyForExecution = false,
            checksNotRun = new[] { "policy-authorization", "snapshot-existence", "runtime-capabilities", "resource-budget" }
        });
    }
}
