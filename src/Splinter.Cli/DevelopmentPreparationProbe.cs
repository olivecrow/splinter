using System.CommandLine;
using System.Diagnostics;
using System.Text.Json;
using Splinter.Core;
using Splinter.Protocol;
using Splinter.Runtime;

namespace Splinter.Cli;

// Explicit development entrypoint; ordinary workspace commands have no probe.
internal sealed class DevelopmentPreparationProbe(string boundary, string signalPath) : IWorkspacePreparationObserver
{
    internal static readonly string[] Boundaries = ["request.before-record", "request.after-record",
        "source.before-effect", "source.after-effect", "source.after-record",
        "capture.before-effect", "capture.after-effect", "capture.after-record",
        "registration.before-effect", "git-registration.after-effect", "git-registration.after-record",
        "registration.after-effect", "registration.after-record", "delta.before-effect", "delta.after-record",
        "provider.before-effect", "provider.after-effect", "provider.after-record",
        "mount.before-effect", "mount.after-effect", "mount.after-record", "attach.before-effect", "attach.after-effect",
        "ready.before-record", "ready.after-record", "response.before-publish"];
    private bool reached;

    public async Task ReachedAsync(string point, WorkspaceRecord workspace, CancellationToken ct)
    {
        if (point != boundary || reached) return;
        reached = true;
        WorkspaceStore.EnsurePlainPath(signalPath);
        using var self = Process.GetCurrentProcess();
        var signal = new { version = 1, boundary, workspaceId = workspace.Id, workspace.RequestDigest,
            processId = self.Id, processStartTimeUtcTicks = self.StartTime.ToUniversalTime().Ticks,
            projectPath = workspace.ProjectPath, workspacePath = workspace.Path,
            snapshotId = workspace.Snapshot?.Reference?.Id, step = workspace.Snapshot?.Step,
            observedAt = DateTimeOffset.UtcNow };
        string temporary = signalPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        await using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        { await JsonSerializer.SerializeAsync(file, signal, ApiJson.Options, ct); file.Flush(true); }
        File.Move(temporary, signalPath); // Never replace another run's evidence.
        // External harness either terminates this exact PID/start identity or
        // supplies an explicit continuation. Timeout is cancellation, not cleanup.
        long started = Stopwatch.GetTimestamp();
        string continuation = signalPath + ".continue";
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            WorkspaceStore.EnsurePlainPath(continuation);
            if (File.Exists(continuation)) return;
            if (Stopwatch.GetElapsedTime(started) > TimeSpan.FromMinutes(5))
                throw new WorkspaceException("Cancelled", "Development probe deadline reached; preserve and observe the same task.");
            await Task.Delay(50, ct);
        }
    }

    internal static void Add(RootCommand root, ContractCatalog contracts, Action<ApiResponse> respond, CancellationToken ct)
    {
        var command = new Command("preparation-probe", "Development-only gate in the product NTFS workspace ensure path; never releases resources.");
        var input = new Option<string>("--input") { Required = true }; command.Options.Add(input); root.Subcommands.Add(command);
        command.SetAction(async parse =>
        {
            await using var stream = File.OpenRead(parse.GetValue(input)!);
            if (stream.Length > 1024 * 1024) throw new WorkspaceException("InputTooLarge", "Probe input exceeds 1 MiB.");
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            if (!contracts.Validate("urn:splinter:v1:preparation-probe-request", doc.RootElement).IsValid)
                throw new WorkspaceException("InvalidProbeRequest", "Use the current development preparation request schema.");
            var r = doc.RootElement;
            string point = r.GetProperty("boundary").GetString()!, signal = Path.GetFullPath(r.GetProperty("signalPath").GetString()!);
            WorkspaceStore.EnsurePlainPath(signal);
            if (File.Exists(signal) || Directory.Exists(signal) || File.Exists(signal + ".continue"))
                throw new WorkspaceException("ProbeEvidenceExists", "Use a new signal path; existing evidence is retained.");
            string project = WorkspaceStore.CanonicalProject(r.GetProperty("project").GetString()!);
            string home = Path.GetFullPath(r.GetProperty("home").GetString()!);
            string work = Path.Combine(home, "workspaces");
            foreach (string tree in new[] { project, work })
                if (WorkspaceStore.SamePath(signal, tree) || signal.StartsWith(tree + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    throw new WorkspaceException("UnsafePath", "Keep probe evidence outside source and workspace trees.");
            var observer = new DevelopmentPreparationProbe(point, signal);
            string[] args = ["workspace", "ensure", "--project", project, "--home", home, "--key", r.GetProperty("key").GetString()!,
                "--base", r.GetProperty("baseReference").GetString()!, "--storage", "ntfs-snapshot"];
            using var writer = new StringWriter();
            await Program.RunAsync(args, writer, ct, observer);
            respond(JsonSerializer.Deserialize<ApiResponse>(writer.ToString(), ApiJson.Options)!);
        });
    }
}
