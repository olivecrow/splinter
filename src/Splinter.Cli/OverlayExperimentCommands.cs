using System.CommandLine;
using System.Security.Cryptography;
using System.Text.Json;
using Splinter.Application.Unity;
using Splinter.Core;
using Splinter.Platform.Windows;
using Splinter.Protocol;
using Splinter.Runtime;
using Splinter.SourceControl.Git;

namespace Splinter.Cli;

// Bounded spike entrypoint, deliberately separate from workspace ensure and its
// ReFS lifecycle. No source capture, driver install or fallback is implicit.
internal static class OverlayExperimentCommands
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public static void Add(RootCommand root, ContractCatalog contracts, Action<string> select, Action<ApiResponse> respond, CancellationToken ct)
    {
        var command = new Command("snapshot-spike", "Operate an explicitly prepared snapshot-overlay experiment.");
        var input = new Option<string>("--input") { Required = true }; command.Options.Add(input); root.Subcommands.Add(command);
        command.SetAction(async parse =>
        {
            select("snapshot.spike");
            await using var stream = File.OpenRead(parse.GetValue(input)!);
            if (stream.Length > 1024 * 1024) throw new WorkspaceException("InputTooLarge", "Spike request exceeds 1 MiB.");
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            if (!contracts.Validate("urn:splinter:v1:snapshot-spike-request", document.RootElement).IsValid)
                throw new WorkspaceException("InvalidSpikeRequest", "Request does not match snapshot.spike schema.");
            var request = document.RootElement.Deserialize<SpikeRequest>(Json)!;
            var overlay = await WinFspOverlayClient.ReadRequestAsync(request.OverlayRequestPath, ct);
            if (!contracts.Validate("urn:splinter:v1:overlay-serve-request", JsonSerializer.SerializeToElement(overlay, Json)).IsValid)
                throw new WorkspaceException("InvalidOverlayRequest", "Persisted overlay request is invalid.");
            object result = await Execute(request, overlay, ct);
            respond(ApiResponse.Success("snapshot.spike", new { request.Action, overlayId = overlay.Identity.Id, result }));
        });
    }
    private static async Task<object> Execute(SpikeRequest input, OverlayServeRequest overlay, CancellationToken ct)
    {
        string control = Path.Combine(overlay.ControlPath, "unity");
        var processes = new WindowsProcessWorkspaceProvider(Path.Combine(overlay.ControlPath, "runners"));
        var unity = new UnityWorkspaceProvider(processes);
        var workspace = new WorkspaceRecord { Id = overlay.Identity.Id, RequestKey = overlay.Identity.Id,
            Path = overlay.MountPath, ProjectPath = overlay.Identity.Snapshot.SourcePath,
            BaseReference = input.Revision ?? "", SourceReference = input.Branch ?? "", StorageMode = "snapshot-overlay-spike" };
        switch (input.Action)
        {
            case "candidate-export":
                if (overlay.ValidationView is not null)
                    throw new WorkspaceException("CandidateSourceRequired", "Export an editorless worker, not a validation output generation.");
                string destination = Path.GetFullPath(input.CandidatePath ??
                    throw new WorkspaceException("CandidatePathRequired", "Use a new candidate directory."));
                for (string? part = destination; part is not null; part = Path.GetDirectoryName(part))
                    if ((File.Exists(part) || Directory.Exists(part)) && (File.GetAttributes(part) & FileAttributes.ReparsePoint) != 0)
                        throw new WorkspaceException("UnsafePath", "Candidate publication cannot traverse a reparse point.");
                foreach (string path in new[] { overlay.Identity.Snapshot.SourcePath, overlay.DeltaPath, overlay.MountPath, overlay.ControlPath })
                {
                    string other = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
                    if (destination.Equals(other, StringComparison.OrdinalIgnoreCase) ||
                        destination.StartsWith(other + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                        other.StartsWith(Path.TrimEndingDirectorySeparator(destination) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                        throw new WorkspaceException("UnsafePath", "Publish a candidate in a separate tree from source, worker, mount and control.");
                }
                var state = await WinFspOverlayClient.ObserveAsync(overlay, ct);
                if (state.GetProperty("processAlive").GetBoolean() || Directory.Exists(overlay.MountPath) ||
                    state.TryGetProperty("ownershipVerified", out var ownership) && !ownership.GetBoolean())
                    throw new WorkspaceException("CandidateSourceBusy", "Stop and unmount the owned worker before publishing a candidate.");
                if (Directory.Exists(control))
                {
                    var sourceApp = await unity.ObserveAsync(workspace, control, ct);
                    if (sourceApp.Running || sourceApp.Resources is not { OwnershipVerified: true, ActiveProcesses: 0 })
                        throw new WorkspaceException("ApplicationStillRunning", "The candidate source must have no live application resources.");
                }
                using (var tree = new VssSnapshotTree(overlay.Identity.Snapshot, overlay.ReadAccess))
                    return JournalDeltaStore.ExportCandidate(overlay.DeltaPath, overlay.Identity, tree,
                        destination,
                        Guid.NewGuid().ToString("N"), [".git", "Library", "Temp", "Logs", "UserSettings", "obj"]);
            case "mount":
                IOverlayMountProvider mountProvider = new WinFspOverlayMountProvider(input.OverlayRequestPath, Path.Combine(overlay.ControlPath, "runners"));
                await mountProvider.MountAsync(overlay.Identity, overlay.DeltaPath, overlay.MountPath, ct);
                return await WinFspOverlayClient.ObserveAsync(overlay, ct);
            case "status": return await WinFspOverlayClient.ObserveAsync(overlay, ct);
            case "unmount":
                if (Directory.Exists(control))
                {
                    var app = await unity.ObserveAsync(workspace, control, ct);
                    if (app.Running || app.Resources is not { OwnershipVerified: true, ActiveProcesses: 0 })
                        throw new WorkspaceException("OverlayClientsRemain", "Stop the owned application and prove its process group absent first.");
                }
                return await WinFspOverlayClient.StopAsync(overlay, ct);
            case "git-prepare":
                if (input.ReceiptPath is null || input.Branch is null || input.Revision is null) throw new WorkspaceException("GitArgumentsRequired", "receiptPath, branch and revision are required.");
                return await GitOverlayRegistration.PrepareAsync(workspace.ProjectPath, workspace.Path, input.Branch, input.Revision, workspace.Id, input.ReceiptPath, ct);
            case "git-release":
                if (input.Revision is null) throw new WorkspaceException("ResultRevisionRequired", "Supply the independently preserved result revision.");
                return await GitOverlayRegistration.ReleaseAsync(await Receipt(input, overlay, ct), input.Revision, ct);
            case "unity-status": return await unity.ObserveAsync(workspace, control, ct);
            case "unity-stop": await unity.StopAsync(workspace, control, ct); return await unity.ObserveAsync(workspace, control, ct);
        }
        var mount = await WinFspOverlayClient.ObserveAsync(overlay, ct);
        if (mount.GetProperty("phase").GetString() != "mounted" || !mount.GetProperty("processAlive").GetBoolean() ||
            DateTimeOffset.UtcNow - mount.GetProperty("state").GetProperty("observedAt").GetDateTimeOffset() > TimeSpan.FromSeconds(10))
            throw new WorkspaceException("OverlayNotReady", "A fresh owned mount is required.");
        switch (input.Action)
        {
            case "git-attach": return await GitOverlayRegistration.AttachAsync(await Receipt(input, overlay, ct), ct, allowSourceChanges: true);
            case "unity-start":
                var prior = await unity.ObserveAsync(workspace, control, ct);
                if (prior.Running) return prior;
                if (prior.Phase == "launch-uncertain") throw new WorkspaceException("LaunchUncertain", "Inspect persisted launch intent before retrying.");
                if (prior.Resources is not { OwnershipVerified: true, ActiveProcesses: 0 }) throw new WorkspaceException("ProcessCleanupIncomplete", "Prior application processes must exit first.");
                // Captured caches include live-process addresses. Unity treats
                // ilpp.pid as a lingering process and kills it before compiling.
                // Whiteout only these ephemeral files in this private overlay;
                // retain the source's files and every reusable Library artifact.
                foreach (string relative in new[] { "Library/ilpp.pid", "Library/Pipeline/.unity-pipeline-port" })
                {
                    string ephemeral = Path.Combine(workspace.Path, relative);
                    if (File.Exists(ephemeral)) File.Delete(ephemeral);
                }
                string mcpRunState = Path.Combine(workspace.Path, "Library", "MCPForUnity", "RunState");
                if (Directory.Exists(mcpRunState))
                    foreach (string ephemeral in Directory.EnumerateFiles(mcpRunState, "mcp_http_*.pid")) File.Delete(ephemeral);
                string infrastructure = Path.Combine(workspace.Path, "Assets", "__Splinter__");
                string marker = Path.Combine(infrastructure, "workspace.marker");
                using (var resource = typeof(UnityWorkspaceProvider).Assembly.GetManifestResourceStream("Splinter.Editor.WorkspaceBridge.cs")!)
                using (var reader = new StreamReader(resource))
                {
                    string code = await reader.ReadToEndAsync(ct);
                    var identity = new { workspaceId = workspace.Id, codeHash = Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(code))) };
                    string markerText = JsonSerializer.Serialize(identity, Json);
                    if (Directory.Exists(infrastructure))
                    {
                        string priorBridge = Path.Combine(infrastructure, "Editor", "WorkspaceBridge.cs");
                        if (!File.Exists(marker) || !File.Exists(priorBridge)) throw new WorkspaceException("ManagedPathConflict", "Existing bridge ownership is incomplete.");
                        using var saved = JsonDocument.Parse(await File.ReadAllTextAsync(marker, ct));
                        if (saved.RootElement.GetProperty("workspaceId").GetString() != workspace.Id || saved.RootElement.GetProperty("codeHash").GetString() !=
                            Convert.ToHexStringLower(SHA256.HashData(await File.ReadAllBytesAsync(priorBridge, ct))))
                            throw new WorkspaceException("ManagedPathConflict", "Existing bridge content changed outside its owner.");
                    }
                    Directory.CreateDirectory(Path.Combine(infrastructure, "Editor"));
                    await File.WriteAllTextAsync(Path.Combine(infrastructure, "Editor", "WorkspaceBridge.cs"), code, ct);
                    await File.WriteAllTextAsync(marker, markerText, ct);
                }
                string version = File.ReadLines(Path.Combine(workspace.Path, "ProjectSettings", "ProjectVersion.txt"))
                    .First(line => line.StartsWith("m_EditorVersion:", StringComparison.Ordinal)).Split(':', 2)[1].Trim();
                string executable = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Unity", "Hub", "Editor", version, "Editor", "Unity.exe");
                if (!File.Exists(executable)) throw new WorkspaceException("UnityEditorMissing", executable);
                Directory.CreateDirectory(control);
                foreach (string name in new[] { "heartbeat.json", "stop-request.json", "stop-result.json", "validation-request.json", "pending.json", "validation.json", "snapshot-probe.json" })
                    if (File.Exists(Path.Combine(control, name))) File.Delete(Path.Combine(control, name));
                string token = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
                await File.WriteAllTextAsync(Path.Combine(control, "launch-intent.json"), JsonSerializer.Serialize(new { workspace.Id, token, executable, projectPath = workspace.Path }, Json), ct);
                await processes.StartAsync(new ProcessLaunch(executable, ["-projectPath", workspace.Path, "-logFile", Path.Combine(control, "editor.log"),
                    "-splinterControlPath", control, "-splinterToken", token, "-splinterIncrementalCompilation", "1"], workspace.Path, token), control, ct);
                return await unity.ObserveAsync(workspace, control, ct);
            case "unity-refresh":
                string requestId = Guid.NewGuid().ToString("N");
                string fingerprint = Convert.ToHexStringLower(SHA256.HashData(await File.ReadAllBytesAsync(Path.Combine(workspace.Path, "Assets", "SplinterSnapshotProbe.cs"), ct)));
                await unity.RequestValidationAsync(workspace, control, requestId, fingerprint, ct);
                return new { requestId, fingerprint, phase = "accepted", completed = false };
            case "preserve":
                var state = await unity.ObserveAsync(workspace, control, ct);
                if (state.Running || state.Resources is not { OwnershipVerified: true, ActiveProcesses: 0 }) throw new WorkspaceException("OverlayClientsRemain", "Preserve source after application writes have stopped.");
                return await GitOverlayRegistration.PreserveAsync(await Receipt(input, overlay, ct), ct);
            default: throw new WorkspaceException("UnknownSpikeAction", input.Action);
        }
    }
    private static async Task<OverlayGitReceipt> Receipt(SpikeRequest input, OverlayServeRequest overlay, CancellationToken ct)
    {
        if (input.ReceiptPath is null) throw new WorkspaceException("GitReceiptRequired", "A persisted registration receipt is required.");
        var r = await GitOverlayRegistration.ReadAsync(input.ReceiptPath, ct);
        if (r.OwnerId != overlay.Identity.Id || Path.GetFullPath(r.Path) != Path.GetFullPath(overlay.MountPath) || Path.GetFullPath(r.Project) != Path.GetFullPath(overlay.Identity.Snapshot.SourcePath))
            throw new WorkspaceException("GitOverlayOwnerMismatch", "Registration must belong to the selected overlay.");
        if (overlay.Identity.ExcludedTopLevel?.Contains(".git", StringComparer.OrdinalIgnoreCase) != true)
            throw new WorkspaceException("GitSourceMetadataVisible", "The lower .git directory must be excluded.");
        return r;
    }
    private sealed record SpikeRequest(string Action, string OverlayRequestPath, string? ReceiptPath, string? Branch, string? Revision,
        string? CandidatePath);
}
