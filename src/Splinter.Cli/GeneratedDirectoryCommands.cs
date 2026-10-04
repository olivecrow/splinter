using System.CommandLine;
using System.CommandLine.Parsing;
using Splinter.Application.Unity;
using Splinter.Platform.Windows;
using Splinter.Protocol;
using Splinter.Runtime;

namespace Splinter.Cli;

internal static class GeneratedDirectoryCommands
{
    public static void Add(RootCommand root, ContractCatalog contracts, Func<ParseResult, string> home, Action<string> select,
        Action<ApiResponse> respond, CancellationToken ct)
    {
        var group = new Command("library", "Explicit original Library basis preparation, observation and restoration."); root.Subcommands.Add(group);
        foreach (string action in new[] { "prepare", "status", "restore" })
        {
            string command = "library." + action;
            var child = new Command(action, contracts.Find(command)!.Description);
            var project = new Option<string>("--project") { Required = action == "prepare" };
            var key = new Option<string>("--key") { Required = action == "prepare" };
            var id = new Option<string>("--id") { Required = action != "prepare" };
            if (action == "prepare") { child.Options.Add(project); child.Options.Add(key); } else child.Options.Add(id);
            group.Subcommands.Add(child);
            child.SetAction(async parse =>
            {
                select(command);
                object input = action == "prepare" ? new { project = parse.GetValue(project), key = parse.GetValue(key) } : new { id = parse.GetValue(id) };
                if (!contracts.Validate(contracts.Find(command)!.InputSchema, System.Text.Json.JsonSerializer.SerializeToElement(input)).IsValid)
                {
                    respond(ApiResponse.Failure(command, "InvalidArguments", "Use the command's exact current schema.")); return;
                }
                var manager = new ManagedDirectoryBasis(new WorkspaceStore(home(parse)));
                ManagedDirectoryState state;
                if (action == "prepare")
                {
                    string path = WorkspaceStore.CanonicalProject(parse.GetValue(project)!);
                    state = await manager.PrepareAsync(path, parse.GetValue(key)!, new UnityWorkspaceProvider(new WindowsProcessWorkspaceProvider(Path.Combine(home(parse), "runners"))).CompatibilityKey(path), ct);
                }
                else state = action == "restore" ? await manager.RestoreAsync(parse.GetValue(id)!, ct) : await manager.GetAsync(parse.GetValue(id)!, ct);
                respond(ApiResponse.Success(command, new { basis = state, home = home(parse) }));
            });
        }
    }
}
