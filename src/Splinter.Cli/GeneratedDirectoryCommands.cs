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
        AddGroup(false); AddGroup(true);
        void AddGroup(bool projectBasis)
        {
        string groupName = projectBasis ? "project" : "library";
        var group = new Command(groupName, "Explicit original basis preparation, observation and restoration."); root.Subcommands.Add(group);
        foreach (string action in new[] { "prepare", "status", "restore" })
        {
            string command = groupName + "." + action;
            var child = new Command(action, contracts.Find(command)!.Description);
            var project = new Option<string>("--project") { Required = action == "prepare" };
            var key = new Option<string>("--key") { Required = action == "prepare" };
            var id = new Option<string>("--id") { Required = action != "prepare" };
            var application = new Option<string>("--application") { Description = "none (default) or unity; fixed during this project basis lifecycle." };
            if (projectBasis && action == "prepare") child.Options.Add(application);
            if (action == "prepare") { child.Options.Add(project); child.Options.Add(key); } else child.Options.Add(id);
            group.Subcommands.Add(child);
            child.SetAction(async parse =>
            {
                select(command);
                object input = action == "prepare" ? projectBasis ? new { project = parse.GetValue(project), key = parse.GetValue(key), application = parse.GetValue(application) ?? "none" } : (object)new { project = parse.GetValue(project), key = parse.GetValue(key) } : new { id = parse.GetValue(id) };
                if (!contracts.Validate(contracts.Find(command)!.InputSchema, System.Text.Json.JsonSerializer.SerializeToElement(input)).IsValid)
                {
                    respond(ApiResponse.Failure(command, "InvalidArguments", "Use the command's exact current schema.")); return;
                }
                var manager = new ManagedDirectoryBasis(new WorkspaceStore(home(parse)), projectBasis);
                ManagedDirectoryState state;
                if (action == "prepare")
                {
                    string path = manager.NormalizeProject(parse.GetValue(project)!);
                    string compatibility = projectBasis && (parse.GetValue(application) ?? "none") == "none" ? "plain-project-v1" : new UnityWorkspaceProvider(new WindowsProcessWorkspaceProvider(Path.Combine(home(parse), "runners"))).CompatibilityKey(path);
                    state = await manager.PrepareAsync(path, parse.GetValue(key)!, compatibility, ct);
                }
                else state = action == "restore" ? await manager.RestoreAsync(parse.GetValue(id)!, ct) : await manager.GetAsync(parse.GetValue(id)!, ct);
                respond(ApiResponse.Success(command, new { basis = state, home = home(parse) }));
            });
        }
        }
    }
}
