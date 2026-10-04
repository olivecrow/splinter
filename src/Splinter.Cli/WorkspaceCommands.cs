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
        if (development) ValidationCommands.Add(root, contracts, Home, select, respond, ct);
        GeneratedDirectoryCommands.Add(root, contracts, Home, select, respond, ct);
        WorkspaceCoordinator Runtime(ParseResult parse, bool auditSource = false)
        {
            selectRecovery([new("workspace.list", new { home = Home(parse) })]);
            var store = new WorkspaceStore(Home(parse)); IWorkspaceStorageProvider? storageProvider = development ? new RefsWorkspaceStorageProvider() : null;
            var git = new GitWorkspaceProvider(storageProvider, store, new WindowsSourceChanges(store.ControlPath), auditSource, preparationObserver);
            var snapshots = new NtfsSnapshotWorkspaceBackend(Path.Combine(store.Root, "runners"), preparationObserver, new ManagedDirectoryBasis(store));
            var sources = new SourceWorkspaceRouter(git, new FileWorkspaceProvider(store, new WindowsOriginalFilePublisher()),
                new PlasticWorkspaceProvider(store, new PlasticWorkspaceClient()));
            return new(store, sources,
                new SelectedApplicationProvider(new UnityWorkspaceProvider(new WindowsProcessWorkspaceProvider(Path.Combine(store.Root, "runners")))), storageProvider,
                snapshots, preparationObserver, new WindowsWorkspaceDiscardProvider(sources, snapshots));
        }

        var workspace = new Command("workspace", "Create, resume, collect, integrate, release or explicitly discard durable isolated workspaces.");
        var unity = new Command("unity", "Start and stop only the selected workspace's private Unity Editor.");
        root.Subcommands.Add(workspace);
        root.Subcommands.Add(unity);
        foreach (var group in new[] { workspace, unity })
            group.SetAction(_ => { select("cli"); respond(ApiResponse.Failure("cli", "InvalidArguments",
                "Select a subcommand; use capabilities to discover its contract.", nextActions: [new("capabilities", new { })])); });
        var ensure = new Command("ensure", contracts.Find("workspace.ensure")!.Description);
        var project = new Option<string>("--project") { Required = true };
        var key = new Option<string>("--key") { Required = true, Description = "Stable unique task key; reuse to resume across turns." };
        var basis = new Option<string>("--base") { Description = "Source branch/ref; default development." };
        var source = new Option<string>("--source") { Description = "git (default), none or plastic. Selection is durable; failures never fall back." };
        var application = new Option<string>("--application") { Description = "unity (default) or none for an ordinary folder without an Editor." };
        var storage = new Option<string>("--storage") { Required = true, Description = "git-snapshot reads a recorded project commit without VSS or checkout; ntfs-snapshot uses VSS. Both store private ranges. refs-cow/git-checkout are development paths." };
        var journalBudget = new Option<long?>("--max-journal-bytes") { Description = "Immutable NTFS delta budget; defaults to 16 GiB. A changed limit requires a new task key." };
        var baselineOption = new Option<string>("--baseline") { Description = "Sealed baseline id required for refs-cow." };
        var editor = new Option<bool>("--editor") { Description = "Also start the private Editor when this task needs it." };
        var generatedBasis = new Option<string>("--library-basis") { Description = "Exact prepared original Library basis id; fixed for this task." };
        ensure.Options.Add(project); ensure.Options.Add(key); ensure.Options.Add(basis); ensure.Options.Add(storage); ensure.Options.Add(editor); if (development) ensure.Options.Add(baselineOption); ensure.Options.Add(journalBudget);
        ensure.Options.Add(source); ensure.Options.Add(application);
        ensure.Options.Add(generatedBasis);
        workspace.Subcommands.Add(ensure);
        ensure.SetAction(async parse =>
        {
            const string command = "workspace.ensure";
            select(command);
            string sourceKind = parse.GetValue(source) ?? "git";
            string applicationKind = parse.GetValue(application) ?? (sourceKind == "none" ? "none" : "unity");
            string baseReference = parse.GetValue(basis) ?? (sourceKind is "none" or "plastic" ? "snapshot" : "development");
            var input = new { project = parse.GetValue(project), key = parse.GetValue(key), baseReference, source = sourceKind, application = applicationKind, storage = parse.GetValue(storage), editor = parse.GetValue(editor), baseline = development ? parse.GetValue(baselineOption) : null, maxJournalBytes = parse.GetValue(journalBudget), libraryBasis = parse.GetValue(generatedBasis) };
            if (!Validate(command, input)) return;
            var view = await Runtime(parse).EnsureAsync(input.project!, input.key!, baseReference, input.storage!, input.editor, ct, input.baseline,
                maxJournalBytes: input.maxJournalBytes ?? SnapshotWorkspaceState.DefaultJournalBytes, sourceKind: sourceKind, applicationKind: applicationKind, generatedDirectoryId: input.libraryBasis);
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
        compact.Options.Add(compactId); compact.Options.Add(maxEstimatedJournalOverlapBytes);
        workspace.Subcommands.Add(compact);
        compact.SetAction(async parse =>
        {
            const string command = "workspace.compact";
            select(command);
            string id = parse.GetValue(compactId)!;
            long? budget = parse.GetValue(maxEstimatedJournalOverlapBytes);
            if (!Validate(command, new { id, maxEstimatedJournalOverlapBytes = budget })) return;
            WorkspaceView view;
            try { view = await Runtime(parse).CompactAsync(id, ct, budget); }
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
        integrate.Options.Add(integrateId); integrate.Options.Add(integrateTarget); integrate.Options.Add(planOnly);
        integrate.Options.Add(applyIntent); integrate.Options.Add(planDigest); workspace.Subcommands.Add(integrate);
        integrate.SetAction(async parse =>
        {
            const string command = "workspace.integrate"; select(command);
            string id = parse.GetValue(integrateId)!; string? target = parse.GetValue(integrateTarget);
            bool plan = parse.GetValue(planOnly); string? intent = parse.GetValue(applyIntent), digest = parse.GetValue(planDigest);
            if (!Validate(command, new { id, target, plan, intentKey = intent, expectedPlanDigest = digest })) return;
            var record = new WorkspaceStore(Home(parse)).Get(id); var runtime = Runtime(parse);
            WorkspaceView view;
            if (record.SourceKind == "none")
            {
                if (target != "original" || !plan && (intent is null || digest is null))
                    throw new WorkspaceException("OriginalApplyRequestInvalid", "Use --target original --plan, then the explicit intent key and exact returned plan digest.");
                view = plan ? await runtime.PlanOriginalAsync(id, ct) : await runtime.ApplyOriginalAsync(id, intent!, digest!, ct);
            }
            else
            {
                if (plan || intent is not null || digest is not null)
                    throw new WorkspaceException("OriginalApplyRequestInvalid", "Original-apply options belong to the selected plain-folder source only.");
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

        var storageGroup = new Command("storage", "Inspect a candidate CoW volume."); root.Subcommands.Add(storageGroup);
        var serviceGroup = new Command("service", "Inspect the installed VSS authority; installation is an explicit machine operation.");
        var serviceStatus = new Command("status", "Read SCM status without elevation or VSS side effects.");
        serviceGroup.Subcommands.Add(serviceStatus); storageGroup.Subcommands.Add(serviceGroup);
        serviceStatus.SetAction(_ => { select("storage.service.status"); respond(ApiResponse.Success("storage.service.status", StorageServiceInspection.Observe())); });
        serviceGroup.SetAction(_ => { select("cli"); respond(ApiResponse.Failure("cli", "InvalidArguments", "Use storage service status.")); });
        storageGroup.SetAction(_ => { select("cli"); respond(ApiResponse.Failure("cli", "InvalidArguments", "Use storage service status.")); });
        if (development)
        {
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
                respond(Result("baseline.prepare", view, view.Application is { Ready: false }, Home(parse)));
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
        }

        void Bind(Command group, string name, string? extraName, Func<WorkspaceCoordinator, string, string?, Task<WorkspaceView>> action)
        {
            string commandId = group.Name + "." + name;
            var command = new Command(name, contracts.Find(commandId)!.Description);
            var idOption = new Option<string>("--id") { Required = true };
            command.Options.Add(idOption);
            Option<string>? extra = null;
            Option<bool>? verifySource = null, auditSource = null;
            if (commandId == "workspace.status")
            {
                verifySource = new Option<bool>("--verify-source") { Description = "Read source contents to verify the current fingerprint; ordinary polls only inspect process and provider state." };
                command.Options.Add(verifySource);
                auditSource = new Option<bool>("--audit-source") { Description = "Read all source and rebuild the fingerprint manifest; preserve a corrupt manifest before replacement. Implies --verify-source." };
                command.Options.Add(auditSource);
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
                if (!Validate(commandId, parameters)) return;
                bool audit = auditSource is not null && parse.GetValue(auditSource);
                WorkspaceView view;
                try
                {
                    view = verifySource is not null
                        ? await Runtime(parse, audit).StatusAsync(id, ct, audit || parse.GetValue(verifySource))
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
        string nextCommand = accepted ? "workspace.status" : view.NextCommand.Split(" --", 2)[0].Replace(' ', '.');
        if (!accepted && view.Recovery is { Disposition: not "ready" } recovery) nextCommand = recovery.NextCommand;
        if (view.OriginalApplyPlan is not null || view.Workspace.OriginalApply is { Completed: false }) nextCommand = "workspace.integrate";
        object data = new
        {
            workspace = Summary(view.Workspace),
            source = view.Source is null ? null : new
            {
                view.Source.Revision, view.Source.Fingerprint, view.Source.HasChanges,
                changedPaths = view.Source.ChangedPaths.Take(100).ToArray(), changedPathCount = view.Source.ChangedPaths.Length, inspection = view.Source.Inspection
            },
            application = view.Application is null ? null : new
            {
                view.Application.Running, view.Application.Ready, view.Application.Phase,
                processId = view.Application.Lease?.ProcessId, view.Application.Resources
            },
            view.ControlPath, nextCommand, view.Recovery, view.SourceVerification, view.OriginalApplyPlan,
            isolation = "private-project-and-library; shared user preferences and external services are not sandboxed"
        };
        var response = ApiResponse.Success(command, data);
        object nextArguments = nextCommand == "workspace.integrate" && view.Workspace.SourceKind == "none"
            ? view.Workspace.OriginalApply is { } apply
                ? new { id = view.Workspace.Id, target = "original", apply.IntentKey, expectedPlanDigest = apply.Plan.Digest, home }
                : (object)new { id = view.Workspace.Id, target = "original", plan = true, home }
            : nextCommand == "workspace.ensure"
            ? new { project = view.Workspace.ProjectPath, key = view.Workspace.RequestKey, baseReference = view.Workspace.BaseReference,
                source = view.Workspace.SourceKind, application = view.Workspace.ApplicationKind,
                storage = view.Workspace.StorageMode, baseline = view.Workspace.BaselineId, maxJournalBytes = view.Workspace.Snapshot?.MaxJournalBytes, home }
            : nextCommand == "workspace.discard" && view.Workspace.Discard is { } discard
                ? new { id = view.Workspace.Id, discard.Intent.IntentKey, expectedRequestDigest = discard.Intent.RequestDigest, home }
                : nextCommand == "workspace.list" ? new { home } : (object)new { id = view.Workspace.Id, home };
        return response with
        {
            Status = deferred || accepted ? "Accepted" : "Succeeded", Terminal = !(deferred || accepted),
            RetryDisposition = deferred ? "requires-user-action" : "not-needed",
            NextActions = view.Workspace.Purpose == "baseline" && view.Workspace.Phase == "released"
                ? [new("schema", new { command = "workspace.ensure" })]
                : [new(nextCommand, nextArguments, view.Recovery is { Disposition: "observe" or "preserve" or "deferred-reclamation" or "deferred-discard-reclamation" })]
        };
    }

    private static object Summary(WorkspaceRecord record) => new
    {
        record.Version, record.Id, record.RequestKey, record.ProjectPath, record.Path, record.SourceKind, record.ApplicationKind, record.SourceReference, record.BaseReference,
        record.BaseRevision, record.Phase, record.CreatedAt, record.UpdatedAt, record.StorageMode, record.Purpose, record.BaselineId, record.CloneMetrics, record.ReleasedResources,
        record.RequestDigest, record.Snapshot, record.Discard, record.ResultStorage, record.OriginalApply,
        processId = record.Application?.ProcessId,
        record.ResultRevision, record.IntegratedRevision, record.LastError
    };


}
