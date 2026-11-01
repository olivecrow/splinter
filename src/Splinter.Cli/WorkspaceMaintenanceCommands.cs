using System.CommandLine;
using System.CommandLine.Parsing;
using System.Text.Json;
using Splinter.Protocol;
using Splinter.Runtime;

namespace Splinter.Cli;

internal static class WorkspaceMaintenanceCommands
{
    internal static void Add(Command workspace, ContractCatalog contracts, Func<ParseResult, WorkspaceCoordinator> runtime,
        Func<ParseResult, string> home, Action<string> select, Action<ApiResponse> respond, CancellationToken ct)
    {
        var command = new Command("maintain", contracts.Find("workspace.maintain")!.Description);
        var id = new Option<string>("--id") { Required = true };
        var request = new Option<string>("--request-key") { Required = true, Description = "Stable metadata maintenance request; retries preserve its original scope and roots." };
        var scope = new Option<string>("--scope") { Description = "workspace, shared, content, ranges, or all (default)." };
        command.Options.Add(id); command.Options.Add(request); command.Options.Add(scope); workspace.Subcommands.Add(command);
        command.SetAction(async parse =>
        {
            const string name = "workspace.maintain"; select(name);
            var input = new { id = parse.GetValue(id)!, requestKey = parse.GetValue(request)!, scope = parse.GetValue(scope) ?? "all" };
            var validation = contracts.Validate(contracts.Find(name)!.InputSchema, JsonSerializer.SerializeToElement(input, ApiJson.Options));
            if (!validation.IsValid)
            { respond(ApiResponse.Failure(name, "SchemaValidationFailed", "Arguments do not match the fixed metadata maintenance request.", validation)); return; }
            var result = await runtime(parse).MaintainMetadataAsync(input.id, input.requestKey, input.scope, ct);
            respond(ApiResponse.Success(name, new { id = input.id, maintenance = result }) with
            { NextActions = [new("workspace.status", new { id = input.id, home = home(parse) })] });
        });
    }
}
