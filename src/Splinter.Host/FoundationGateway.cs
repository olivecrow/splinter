using System.Text.Json;
using Splinter.Core;
using Splinter.Protocol;

namespace Splinter.Host;

// Only read-only services are hosted in-process in this foundation milestone.
// Persistent operations will require the planned daemon and journal before exposure.
public sealed class FoundationGateway(
    ContractCatalog contracts,
    IRepositoryInspector repositories,
    IApplicationInspector applications)
{
    public ApiResponse Capabilities() => ApiResponse.Success("capabilities", new
    {
        implementationStage = "foundation",
        commands = contracts.Commands,
        unavailable = new[] { "plan.submit", "workspace.create", "workspace.release", "storage.cow", "unity.execute", "agent.start" },
        persistentHost = false
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
