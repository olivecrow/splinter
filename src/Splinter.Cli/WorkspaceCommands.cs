using System.CommandLine;
using System.CommandLine.Parsing;
using System.Text.Json;
using Splinter.Application.Unity;
using Splinter.Core;
using Splinter.Protocol;
using Splinter.Runtime;
using Splinter.SourceControl.Git;
using Splinter.Platform.Windows;

namespace Splinter.Cli;

internal static class WorkspaceCommands
{
    public static void Add(RootCommand root, ContractCatalog contracts, Action<string> select, Action<ApiResponse> respond,
        CancellationToken ct)
    {
        var home = new Option<string>("--home") { Recursive = true, Description = "Durable Splinter state root; defaults to SPLINTER_HOME or local application data/Splinter." };
        root.Options.Add(home);
        string Home(ParseResult parse) => parse.GetValue(home) ?? Environment.GetEnvironmentVariable("SPLINTER_HOME")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Splinter");
        WorkspaceCoordinator Runtime(ParseResult parse)
        {
            var store = new WorkspaceStore(Home(parse)); var storageProvider = new RefsWorkspaceStorageProvider();
            return new(store, new GitWorkspaceProvider(storageProvider, store),
                new UnityWorkspaceProvider(new WindowsProcessWorkspaceProvider(Path.Combine(store.Root, "runners"))), storageProvider);
        }

        var workspace = new Command("workspace", "Create, resume, collect, integrate and release durable isolated workspaces.");
        var unity = new Command("unity", "Start, refresh and stop only the selected workspace's private Unity Editor.");
        root.Subcommands.Add(workspace);
        root.Subcommands.Add(unity);
        foreach (var group in new[] { workspace, unity })
            group.SetAction(_ => { select("cli"); respond(ApiResponse.Failure("cli", "InvalidArguments",
                "Select a subcommand; use capabilities to discover its contract.", nextActions: [new("capabilities", new { })])); });
        var ensure = new Command("ensure", contracts.Find("workspace.ensure")!.Description);
        var project = new Option<string>("--project") { Required = true };
        var key = new Option<string>("--key") { Required = true, Description = "Stable unique task key; reuse to resume across turns." };
        var basis = new Option<string>("--base") { Description = "Source branch/ref; default development." };
        var storage = new Option<string>("--storage") { Required = true, Description = "refs-cow uses a sealed baseline. git-checkout is a cold diagnostic mode." };
        var baselineOption = new Option<string>("--baseline") { Description = "Sealed baseline id required for refs-cow." };
        var editor = new Option<bool>("--editor") { Description = "Also start the private Editor when this task needs it." };
        ensure.Options.Add(project); ensure.Options.Add(key); ensure.Options.Add(basis); ensure.Options.Add(storage); ensure.Options.Add(editor); ensure.Options.Add(baselineOption);
        workspace.Subcommands.Add(ensure);
        ensure.SetAction(async parse =>
        {
            const string command = "workspace.ensure";
            select(command);
            string baseReference = parse.GetValue(basis) ?? "development";
            var input = new { project = parse.GetValue(project), key = parse.GetValue(key), baseReference, storage = parse.GetValue(storage), editor = parse.GetValue(editor), baseline = parse.GetValue(baselineOption) };
            if (!Validate(command, input)) return;
            var view = await Runtime(parse).EnsureAsync(input.project!, input.key!, baseReference, input.storage!, input.editor, ct, input.baseline);
            respond(Result(command, view, input.editor && view.Application is { Running: true, Ready: false }));
        });
        var list = new Command("list", contracts.Find("workspace.list")!.Description);
        var filter = new Option<string>("--project"); list.Options.Add(filter); workspace.Subcommands.Add(list);
        list.SetAction(parse =>
        {
            select("workspace.list");
            respond(ApiResponse.Success("workspace.list", new
            {
                workspaces = Runtime(parse).List(parse.GetValue(filter)).Select(Summary).ToArray(),
                observation = "persisted; use workspace status for live process and validation state"
            }));
        });
        Bind(workspace, "status", null, (runtime, id, _) => runtime.StatusAsync(id, ct));
        Bind(workspace, "checkpoint", "--message", (runtime, id, extra) => runtime.CheckpointAsync(id, extra!, ct));
        Bind(workspace, "integrate", "--target", (runtime, id, extra) => runtime.IntegrateAsync(id, extra ?? "development", ct));
        Bind(workspace, "release", null, (runtime, id, _) => runtime.ReleaseAsync(id, ct));
        Bind(unity, "start", null, (runtime, id, _) => runtime.StartAsync(id, ct));
        Bind(unity, "refresh", null, (runtime, id, _) => runtime.RefreshAsync(id, ct));
        Bind(unity, "stop", null, (runtime, id, _) => runtime.StopAsync(id, ct));

        var storageGroup = new Command("storage", "Inspect a candidate CoW volume."); root.Subcommands.Add(storageGroup);
        var probe = new Command("probe", contracts.Find("storage.probe")!.Description);
        var pathOption = new Option<string>("--path") { Required = true }; probe.Options.Add(pathOption); storageGroup.Subcommands.Add(probe);
        probe.SetAction(parse => { select("storage.probe"); var input = new { path = parse.GetValue(pathOption)! };
            if (Validate("storage.probe", input)) respond(ApiResponse.Success("storage.probe", new { storage = new RefsWorkspaceStorageProvider().Probe(input.path) })); });
        var baselineGroup = new Command("baseline", "Warm once, seal and explicitly reclaim a reusable cache."); root.Subcommands.Add(baselineGroup);
        foreach (var group in new[] { storageGroup, baselineGroup })
            group.SetAction(_ => { select("cli"); respond(ApiResponse.Failure("cli", "InvalidArguments", "Select a subcommand; use capabilities.")); });
        var prepare = new Command("prepare", contracts.Find("baseline.prepare")!.Description);
        var baselineProject = new Option<string>("--project") { Required = true }; var baselineBase = new Option<string>("--base");
        prepare.Options.Add(baselineProject); prepare.Options.Add(baselineBase); baselineGroup.Subcommands.Add(prepare);
        prepare.SetAction(async parse =>
        {
            select("baseline.prepare");
            var input = new { project = parse.GetValue(baselineProject)!, baseReference = parse.GetValue(baselineBase) ?? "development" };
            if (!Validate("baseline.prepare", input)) return;
            var view = await Runtime(parse).PrepareBaselineAsync(input.project, input.baseReference, ct);
            respond(Result("baseline.prepare", view, view.Application is { Ready: false }));
        });
        foreach (string name in new[] { "seal", "release" })
        {
            string commandId = "baseline." + name;
            var command = new Command(name, contracts.Find(commandId)!.Description); var idOption = new Option<string>("--id") { Required = true };
            command.Options.Add(idOption); baselineGroup.Subcommands.Add(command);
            command.SetAction(async parse =>
            {
                select(commandId); string id = parse.GetValue(idOption)!;
                if (!Validate(commandId, new { id })) return;
                if (name == "seal") respond(ApiResponse.Success(commandId, new { baseline = await Runtime(parse).SealBaselineAsync(id, ct) }) with
                    { NextActions = [new("schema", new { command = "workspace.ensure" })] });
                else { await Runtime(parse).ReleaseBaselineAsync(id, ct); respond(ApiResponse.Success(commandId, new { id, phase = "released" })); }
            });
        }

        void Bind(Command group, string name, string? extraName, Func<WorkspaceCoordinator, string, string?, Task<WorkspaceView>> action)
        {
            string commandId = group.Name + "." + name;
            var command = new Command(name, contracts.Find(commandId)!.Description);
            var idOption = new Option<string>("--id") { Required = true };
            command.Options.Add(idOption);
            Option<string>? extra = null;
            if (extraName is not null)
            {
                extra = new Option<string>(extraName) { Required = extraName == "--message" };
                command.Options.Add(extra);
            }
            command.SetAction(async parse =>
            {
                select(commandId);
                string id = parse.GetValue(idOption)!;
                string? value = extra is null ? null : parse.GetValue(extra);
                var parameters = new Dictionary<string, object?> { ["id"] = id };
                if (extraName is not null) parameters[extraName[2..]] = value ?? "development";
                if (!Validate(commandId, parameters)) return;
                var view = await action(Runtime(parse), id, value);
                bool accepted = commandId == "unity.refresh" && view.Workspace.PendingValidationId is not null ||
                    commandId == "unity.start" && view.Application is { Running: true, Ready: false };
                respond(Result(commandId, view, accepted));
            });
            group.Subcommands.Add(command);
        }

        bool Validate(string command, object input)
        {
            var validation = contracts.Validate(contracts.Find(command)!.InputSchema, JsonSerializer.SerializeToElement(input, ApiJson.Options));
            if (validation.IsValid) return true;
            respond(ApiResponse.Failure(command, "SchemaValidationFailed", "Arguments do not match the command contract.", validation,
                [new("schema", new { command })]));
            return false;
        }
    }

    private static ApiResponse Result(string command, WorkspaceView view, bool accepted)
    {
        object data = new
        {
            workspace = Summary(view.Workspace),
            source = view.Source is null ? null : new
            {
                view.Source.Revision, view.Source.Fingerprint, view.Source.HasChanges,
                changedPaths = view.Source.ChangedPaths.Take(100).ToArray(), changedPathCount = view.Source.ChangedPaths.Length
            },
            application = view.Application is null ? null : new
            {
                view.Application.Running, view.Application.Ready, view.Application.Phase,
                processId = view.Application.Lease?.ProcessId, validation = ValidationSummary(view.Application.Validation), view.Application.Resources
            },
            view.ControlPath, view.NextCommand,
            isolation = "private-project-and-library; shared user preferences and external services are not sandboxed"
        };
        var response = ApiResponse.Success(command, data);
        string nextCommand = accepted ? "workspace.status" : view.NextCommand.Split(" --", 2)[0].Replace(' ', '.');
        return response with
        {
            Status = accepted ? "Accepted" : "Succeeded", Terminal = !accepted,
            NextActions = view.Workspace.Purpose == "baseline" && view.Workspace.Phase == "released"
                ? [new("schema", new { command = "workspace.ensure" })]
                : [new(nextCommand, nextCommand == "workspace.list" ? new { } : (object)new { id = view.Workspace.Id })]
        };
    }

    private static object Summary(WorkspaceRecord record) => new
    {
        record.Id, record.RequestKey, record.ProjectPath, record.Path, record.SourceReference, record.BaseReference,
        record.BaseRevision, record.Phase, record.CreatedAt, record.UpdatedAt, record.StorageMode, record.Purpose, record.BaselineId, record.CloneMetrics, record.ReleasedResources,
        processId = record.Application?.ProcessId, record.PendingValidationId,
        validation = ValidationSummary(record.Validation),
        record.ResultRevision, record.IntegratedRevision, record.LastError
    };

    private static object? ValidationSummary(ApplicationValidation? validation) => validation is null ? null : new
        {
            validation.RequestId, validation.Fingerprint, validation.Success, validation.CompletedAt,
            errors = validation.Errors.Take(50).ToArray(), errorCount = validation.Errors.Length
        };
}
