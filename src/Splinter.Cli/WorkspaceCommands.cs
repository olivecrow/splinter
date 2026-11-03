using System.CommandLine;
using System.CommandLine.Parsing;
using System.Text.Json;
using Splinter.Application.Unity;
using Splinter.Core;
using Splinter.Protocol;
using Splinter.Runtime;
using Splinter.SourceControl.Git;
using Splinter.SourceControl.Files;
using Splinter.Platform.Windows;
using Splinter.Workflows.Windows;

namespace Splinter.Cli;

internal static class WorkspaceCommands
{
    public static void Add(RootCommand root, ContractCatalog contracts, Action<string> select, Action<ApiResponse> respond,
        CancellationToken ct, Action<NextAction[]> selectRecovery, IWorkspacePreparationObserver? preparationObserver = null, bool development = false)
    {
        var home = new Option<string>("--home") { Recursive = true, Description = "Durable Splinter state root; defaults to SPLINTER_HOME or local application data/Splinter." };
        root.Options.Add(home);
        string Home(ParseResult parse) => Path.GetFullPath(parse.GetValue(home) ?? Environment.GetEnvironmentVariable("SPLINTER_HOME")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Splinter"));
        if (development) ViewCommands.Add(root, contracts, Home, select, respond, ct);
        GeneratedDirectoryCommands.Add(root, contracts, Home, select, respond, ct);
        WorkspaceCoordinator Runtime(ParseResult parse, bool auditSource = false)
        {
            selectRecovery([new("workspace.list", new { home = Home(parse) })]);
            var store = new WorkspaceStore(Home(parse), new WindowsJournalFilePins());
            var git = new GitWorkspaceProvider(new WindowsSourceChanges(store.ControlPath), auditSource, preparationObserver);
            var policies = new ApplicationProjectPolicies(NoApplicationProjectPolicy.Instance, new UnityProjectPolicy(new WindowsOriginalEditorIdentityVerifier()));
            var projects = new ManagedDirectoryBasis(store, policies, projectBasis: true);
            var generated = new ManagedDirectoryBasis(store, policies);
            var snapshots = new NtfsSnapshotWorkspaceBackend(Path.Combine(store.Root, "runners"), preparationObserver, generated, projects);
            var sources = new SourceWorkspaceRouter(git, new FileWorkspaceProvider(store, new WindowsOriginalFilePublisher(managedRoot: projects.AcquireOriginal, allowGitAdministration: true), projectAccess: projects,
                    applicationPolicies: policies, sourceChanges: auditSource ? null : new WindowsSourceChanges(store.ControlPath), preparationObserver: preparationObserver, contentPins: new WindowsJournalFilePins()),
                new PlasticWorkspaceProvider(store, new PlasticWorkspaceClient(), originalPublisher: new WindowsOriginalFilePublisher(allowPlasticAdministration: true),
                    applicationPolicies: policies, sourceChanges: auditSource ? null : new WindowsSourceChanges(store.ControlPath), contentPins: new WindowsJournalFilePins()));
            return new(store, sources,
                new SelectedApplicationProvider(new UnityWorkspaceProvider(new WindowsProcessWorkspaceProvider(Path.Combine(store.Root, "runners")))),
                snapshots, preparationObserver, new WindowsWorkspaceDiscardProvider(sources, snapshots, generated, projects));
        }

        var workspace = new Command("workspace", "Create, resume, collect, integrate, release or explicitly discard durable isolated workspaces.");
        var unity = new Command("unity", "Start and stop only the selected workspace's private Unity Editor.");
        root.Subcommands.Add(workspace);
        root.Subcommands.Add(unity);
        WorkspaceReadCommands.Add(workspace, contracts, Home, select, respond, ct);
        WorkspaceMaintenanceCommands.Add(workspace, contracts, parse => Runtime(parse), Home, select, respond, ct);
        foreach (var group in new[] { workspace, unity })
            group.SetAction(_ => { select("cli"); respond(ApiResponse.Failure("cli", "InvalidArguments",
                "Select a subcommand; use capabilities to discover its contract.", nextActions: [new("capabilities", new { })])); });
        var ensure = new Command("ensure", contracts.Find("workspace.ensure")!.Description);
        var project = new Option<string>("--project") { Required = true };
        var key = new Option<string>("--key") { Required = true, Description = "Stable unique task key; reuse to resume across turns." };
        var basis = new Option<string>("--base") { Description = "Source branch/ref; default development." };
        var source = new Option<string>("--source") { Description = "git (default), none or plastic. Selection is durable; failures never fall back." };
        var application = new Option<string>("--application") { Description = "unity (default) or none for an ordinary folder without an Editor." };
        var storage = new Option<string>("--storage") { Required = true, Description = "git-snapshot/plastic-snapshot read recorded project versions; project-snapshot reads a prepared project basis. All store private ranges. git-checkout is an explicit development diagnostic path." };
        var editor = new Option<bool>("--editor") { Description = "Also start the private Editor when this task needs it." };
        var generatedBasis = new Option<string>("--library-basis") { Description = "Exact prepared original Library basis id; fixed for this task." };
        ensure.Options.Add(project); ensure.Options.Add(key); ensure.Options.Add(basis); ensure.Options.Add(storage); ensure.Options.Add(editor);
        ensure.Options.Add(source); ensure.Options.Add(application);
        var projectBasis = new Option<string>("--project-basis") { Description = "Exact prepared managed project basis id; requires source none and project-snapshot." };
        ensure.Options.Add(generatedBasis); ensure.Options.Add(projectBasis);
        workspace.Subcommands.Add(ensure);
        ensure.SetAction(async parse =>
        {
            const string command = "workspace.ensure";
            select(command);
            if (parse.GetValue(storage) == "ntfs-snapshot")
                throw new WorkspaceException("StorageModeRetired", "ntfs-snapshot/VSS is retired. Preserve existing state and independent results; select a supported project basis for new work.");
            string sourceKind = parse.GetValue(source) ?? "git";
            string applicationKind = parse.GetValue(application) ?? (sourceKind == "none" ? "none" : "unity");
            string baseReference = parse.GetValue(basis) ?? (sourceKind is "none" or "plastic" ? "snapshot" : "development");
            var input = new { project = parse.GetValue(project), key = parse.GetValue(key), baseReference, source = sourceKind, application = applicationKind, storage = parse.GetValue(storage), editor = parse.GetValue(editor), libraryBasis = parse.GetValue(generatedBasis), projectBasis = parse.GetValue(projectBasis) };
            if (!Validate(command, input)) return;
            var view = await Runtime(parse).EnsureAsync(input.project!, input.key!, baseReference, input.storage!, input.editor, ct,
                sourceKind: sourceKind, applicationKind: applicationKind, generatedDirectoryId: input.projectBasis ?? input.libraryBasis);
            respond(Result(command, view, input.editor && view.Application is { Running: true, Ready: false }, Home(parse)));
        });
        var list = new Command("list", contracts.Find("workspace.list")!.Description);
        var filter = new Option<string>("--project"); list.Options.Add(filter); workspace.Subcommands.Add(list);
        list.SetAction(parse =>
        {
            select("workspace.list");
            respond(ApiResponse.Success("workspace.list", new
            {
                workspaces = Runtime(parse).List(parse.GetValue(filter)).Select(Summary).ToArray(),
                observation = "persisted; use workspace status for live process and provider state"
            }));
        });
        Bind(workspace, "status", null, (runtime, id, _) => runtime.StatusAsync(id, ct));
        var compact = new Command("compact", contracts.Find("workspace.compact")!.Description);
        var compactId = new Option<string>("--id") { Required = true };
        var maxEstimatedJournalOverlapBytes = new Option<long?>("--max-estimated-journal-overlap-bytes")
        {
            Description = "Optional ceiling for the estimated sum of old and candidate journal lengths; this is not a physical workspace peak limit."
        };
        var revertBaseRanges = new Option<bool?>("--revert-base-ranges")
        { Description = "Select current-state journal storage: reclaim superseded private payload after each durable write. Explicit compaction also removes base-equal ranges. New workspaces default ON; existing journals, reconnect and omitted selections retain the saved mode. Explicit false selects OFF." };
        compact.Options.Add(compactId); compact.Options.Add(maxEstimatedJournalOverlapBytes); compact.Options.Add(revertBaseRanges);
        workspace.Subcommands.Add(compact);
        compact.SetAction(async parse =>
        {
            const string command = "workspace.compact";
            select(command);
            string id = parse.GetValue(compactId)!;
            long? budget = parse.GetValue(maxEstimatedJournalOverlapBytes);
            bool? revert = parse.GetValue(revertBaseRanges);
            if (!Validate(command, new { id, maxEstimatedJournalOverlapBytes = budget, revertBaseRanges = revert })) return;
            WorkspaceView view;
            try { view = await Runtime(parse).CompactAsync(id, ct, budget, revert); }
            catch (WorkspaceException ex) when (ex.Code == "CompactionOverlapBudgetExceeded")
            {
                throw new WorkspaceException(ex.Code, ex.Message, ex.Details,
                    [new("workspace.status", new { id, home = Home(parse) })]);
            }
            respond(Result(command, view, false, Home(parse)));
        });
        Bind(workspace, "checkpoint", "--message", (runtime, id, extra) => runtime.CheckpointAsync(id, extra!, ct));
        var integrate = new Command("integrate", contracts.Find("workspace.integrate")!.Description);
        var integrateId = new Option<string>("--id") { Required = true };
        var integrateTarget = new Option<string>("--target"); var planOnly = new Option<bool>("--plan");
        var applyIntent = new Option<string>("--intent-key"); var planDigest = new Option<string>("--expected-plan-digest");
        var integrateResult = new Option<string>("--result") { Description = "Exact immutable source result generation." };
        var liveOriginal = new Option<string>("--live-original") { Description = "Path to the fresh external-tool live-original cooperation receipt." };
        var applyPaths = new Option<string[]>("--path") { Description = "Select relative source paths; repeat for multiple selections. Omit for all changes.", AllowMultipleArgumentsPerToken = false };
        integrate.Options.Add(integrateId); integrate.Options.Add(integrateTarget); integrate.Options.Add(planOnly);
        integrate.Options.Add(applyIntent); integrate.Options.Add(planDigest); workspace.Subcommands.Add(integrate);
        integrate.Options.Add(applyPaths);
        integrate.Options.Add(integrateResult); integrate.Options.Add(liveOriginal);
        integrate.SetAction(async parse =>
        {
            const string command = "workspace.integrate"; select(command);
            string id = parse.GetValue(integrateId)!; string? target = parse.GetValue(integrateTarget);
            bool plan = parse.GetValue(planOnly); string? intent = parse.GetValue(applyIntent), digest = parse.GetValue(planDigest);
            string[]? paths = parse.GetValue(applyPaths); if (paths is { Length: 0 }) paths = null;
            string? resultId = parse.GetValue(integrateResult), permitPath = parse.GetValue(liveOriginal);
            if (!Validate(command, new { id, target, plan, intentKey = intent, expectedPlanDigest = digest, paths, result = resultId, liveOriginal = permitPath })) return;
            var record = new WorkspaceStore(Home(parse)).Get(id); var runtime = Runtime(parse);
            WorkspaceView view;
            if (target == "original")
            {
                if (resultId is null || !plan && (intent is null || digest is null))
                    throw new WorkspaceException("OriginalApplyRequestInvalid", "Use --target original --plan, then the explicit intent key and exact returned plan digest.");
                LiveOriginalEditorPermit? permit = null;
                if (permitPath is not null)
                {
                    WorkspaceStore.EnsurePlainPath(permitPath);
                    using var input = new FileStream(permitPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                    if (input.Length > 65536) throw new WorkspaceException("LiveOriginalPermitInvalid", "The external cooperation receipt exceeds its bound.");
                    using var document = JsonDocument.Parse(input);
                    if (!contracts.Validate("urn:splinter:v1:live-original-editor-permit", document.RootElement).IsValid)
                        throw new WorkspaceException("LiveOriginalPermitInvalid", "The external cooperation receipt does not match the current schema.");
                    permit = document.RootElement.Deserialize<LiveOriginalEditorPermit>(ApiJson.Options) ?? throw new WorkspaceException("LiveOriginalPermitInvalid", "Missing external cooperation receipt.");
                }
                view = plan ? await runtime.PlanOriginalAsync(id, resultId, permit, ct, paths)
                    : await runtime.ApplyOriginalAsync(id, resultId, intent!, digest!, permit, ct, paths);
            }
            else
            {
                if (plan || intent is not null || digest is not null || paths is not null || permitPath is not null)
                    throw new WorkspaceException("OriginalApplyRequestInvalid", "Original-apply options belong to the selected plain-folder source only.");
                if (resultId is not null && record.Results.LastOrDefault()?.Id != resultId)
                    throw new WorkspaceException("ResultNotReady", "Select the latest immutable result for a new branch integration.");
                view = await runtime.IntegrateAsync(id, target ?? "development", ct);
            }
            respond(Result(command, view, false, Home(parse)));
        });
        Bind(workspace, "release", null, (runtime, id, _) => runtime.ReleaseAsync(id, ct));
        var discardCommand = new Command("discard", contracts.Find("workspace.discard")!.Description);
        var discardId = new Option<string>("--id") { Required = true };
        var intentKey = new Option<string>("--intent-key") { Required = true, Description = "Explicit, stable discard request key. Reuse it to resume." };
        var expectedDigest = new Option<string>("--expected-request-digest") { Required = true, Description = "Confirm the requestDigest returned by workspace status/list." };
        discardCommand.Options.Add(discardId); discardCommand.Options.Add(intentKey); discardCommand.Options.Add(expectedDigest);
        workspace.Subcommands.Add(discardCommand);
        discardCommand.SetAction(async parse =>
        {
            const string command = "workspace.discard"; select(command);
            var request = new WorkspaceDiscardRequest(parse.GetValue(discardId)!, parse.GetValue(intentKey)!, parse.GetValue(expectedDigest)!);
            if (!Validate(command, request)) return;
            respond(Result(command, await Runtime(parse).DiscardAsync(request, ct), false, Home(parse)));
        });
        Bind(unity, "start", null, (runtime, id, _) => runtime.StartAsync(id, ct));
        Bind(unity, "stop", null, (runtime, id, _) => runtime.StopAsync(id, ct));

        void Bind(Command group, string name, string? extraName, Func<WorkspaceCoordinator, string, string?, Task<WorkspaceView>> action)
        {
            string commandId = group.Name + "." + name;
            var command = new Command(name, contracts.Find(commandId)!.Description);
            var idOption = new Option<string>("--id") { Required = true };
            command.Options.Add(idOption);
            Option<string>? extra = null;
            Option<bool>? verifySource = null, auditSource = null, releaseAfterCheckpoint = null;
            Option<string>? checkpointKey = null;
            if (commandId == "workspace.status")
            {
                verifySource = new Option<bool>("--verify-source") { Description = "Read source contents to verify the current fingerprint; ordinary polls only inspect process and provider state." };
                command.Options.Add(verifySource);
                auditSource = new Option<bool>("--audit-source") { Description = "Read all source and rebuild the fingerprint manifest; preserve a corrupt manifest before replacement. Implies --verify-source." };
                command.Options.Add(auditSource);
            }
            if (commandId == "workspace.checkpoint")
            {
                checkpointKey = new Option<string>("--request-key") { Required = true, Description = "Stable idempotent result-generation request key." };
                command.Options.Add(checkpointKey);
                releaseAfterCheckpoint = new Option<bool>("--release")
                { Description = "Preserve the source and immediately reclaim this range-journal workspace under one task lock. Retain the independent result; never integrate it automatically." };
                command.Options.Add(releaseAfterCheckpoint);
            }
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
                if (verifySource is not null) parameters["verifySource"] = parse.GetValue(verifySource);
                if (auditSource is not null) parameters["auditSource"] = parse.GetValue(auditSource);
                if (releaseAfterCheckpoint is not null) parameters["release"] = parse.GetValue(releaseAfterCheckpoint);
                if (checkpointKey is not null) parameters["requestKey"] = parse.GetValue(checkpointKey);
                if (!Validate(commandId, parameters)) return;
                bool audit = auditSource is not null && parse.GetValue(auditSource);
                WorkspaceView view;
                try
                {
                    view = verifySource is not null
                        ? await Runtime(parse, audit).StatusAsync(id, ct, audit || parse.GetValue(verifySource))
                        : releaseAfterCheckpoint is not null && parse.GetValue(releaseAfterCheckpoint)
                        ? await Runtime(parse).CheckpointAndReleaseAsync(id, value!, parse.GetValue(checkpointKey!)!, ct)
                        : checkpointKey is not null ? await Runtime(parse).CheckpointAsync(id, value!, parse.GetValue(checkpointKey)!, ct)
                        : await action(Runtime(parse), id, value);
                }
                catch (WorkspaceException ex) when (ex.Code == "SourceCacheRevalidationRequired")
                {
                    throw new WorkspaceException(ex.Code, ex.Message, ex.Details,
                        [new("workspace.status", new { id, home = Home(parse), auditSource = true })]);
                }
                bool accepted = commandId == "unity.start" && view.Application is { Running: true, Ready: false };
                respond(Result(commandId, view, accepted, Home(parse)));
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

    internal static ApiResponse Result(string command, WorkspaceView view, bool accepted, string home)
    {
        bool deferred = command == "workspace.release" && view.Workspace.Phase == "reclaim-pending" ||
            command == "workspace.discard" && view.Workspace.Phase == "discard-reclaim-pending";
        var next = WorkspaceActions.Next(view, home, accepted);
        string nextCommand = next.Command;
        object data = new
        {
            workspace = Summary(view.Workspace),
            source = view.Source is null ? null : new
            {
                view.Source.Revision, view.Source.Fingerprint, view.Source.HasChanges,
                changedPaths = view.Source.ChangedPaths.Take(100).ToArray(), changedPathCount = view.Source.ChangedPathCount, inspection = view.Source.Inspection
            },
            application = view.Application is null ? null : new
            {
                view.Application.Running, view.Application.Ready, view.Application.Phase,
                processId = view.Application.Lease?.ProcessId, startTimeUtcTicks = view.Application.Lease?.StartTimeUtcTicks, view.Application.Resources
            },
            view.ControlPath, nextCommand, view.Recovery, view.SourceVerification, view.OriginalApplyPlan, view.ResultGeneration, view.OriginalApplyReceipt, view.MetadataMaintenance,
            isolation = "private-project-and-library; shared user preferences and external services are not sandboxed"
        };
        var response = ApiResponse.Success(command, data);
        return response with
        {
            Status = deferred || accepted ? "Accepted" : "Succeeded", Terminal = !(deferred || accepted),
            RetryDisposition = deferred ? "requires-user-action" : "not-needed",
            NextActions = [new(next.Command, next.Arguments, next.RequiresUserAction)]
        };
    }

    private static object Summary(WorkspaceRecord record) => new
    {
        record.Version, record.Id, record.RequestKey, record.ProjectPath, record.Path, record.SourceKind, record.ApplicationKind, record.SourceReference, record.BaseReference,
        record.BaseRevision, record.Phase, record.CreatedAt, record.UpdatedAt, record.StorageMode, record.Purpose, record.ReleasedResources,
        record.RequestDigest, record.Snapshot, record.Discard, record.ResultStorage, record.OriginalApply,
        resultCount = record.Results.Count, latestResult = record.Results.LastOrDefault(), record.ResultHistory, record.CaptureIntent,
        originalApplyCount = record.OriginalApplies.Count, record.OriginalApplyHistory, record.AppliedPaths, record.AppliedPathNames,
        processId = record.Application?.ProcessId,
        record.ResultRevision, record.IntegratedRevision, record.LastError
    };


}
