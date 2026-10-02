using System.ComponentModel;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Splinter.Core;

namespace Splinter.Application.Unity;

public sealed class UnityWorkspaceProvider(IProcessWorkspaceProvider? processes = null) : IApplicationWorkspaceProvider
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public string[] ManagedPaths => ["Assets/__Splinter__", "Assets/__Splinter__.meta", "Library", "Temp", "Logs", "UserSettings", "obj", ".vs", ".idea", "*.csproj", "*.sln", "*.slnx"];
    public string[] CacheInputs => ["Packages", "ProjectSettings"];
    public string CompatibilityKey(string project)
    {
        using var resource = typeof(UnityWorkspaceProvider).Assembly.GetManifestResourceStream("Splinter.Editor.WorkspaceBridge.cs")!;
        using var reader = new StreamReader(resource);
        string version = string.Join('\n', File.ReadAllLines(Path.Combine(project, "ProjectSettings", "ProjectVersion.txt")).Select(line => line.Trim()));
        return Hash(version + "\n" + reader.ReadToEnd().Replace("\r\n", "\n", StringComparison.Ordinal) + "\nWindows");
    }

    public async Task<ApplicationLease> StartAsync(WorkspaceRecord workspace, string controlPath, CancellationToken ct = default)
    {
        string root = ValidateRoot(workspace);
        PlainPath(controlPath);
        Directory.CreateDirectory(controlPath);
        var prior = await Read<ApplicationLease>(controlPath, "launch-receipt.json", ct);
        if (prior is not null)
        {
            if (OwnedProcess(prior)) return prior;
            await Processes.WaitForExitAsync(prior, ct);
        }
        if (prior is null && File.Exists(Path.Combine(controlPath, "launch-intent.json")))
            throw new WorkspaceException("LaunchUncertain", "A launch intent has no receipt. Preserve it and inspect the process before starting another Editor.");
        string version = File.ReadLines(Path.Combine(root, "ProjectSettings", "ProjectVersion.txt"))
            .FirstOrDefault(line => line.StartsWith("m_EditorVersion:", StringComparison.Ordinal))?.Split(':', 2)[1].Trim()
            ?? throw new WorkspaceException("UnityVersionMissing", "ProjectVersion.txt has no Editor version.");
        string executable = ResolveEditor(version);
        if (workspace.StorageMode == "ntfs-snapshot")
        {
            // Remove only captured process addresses in the private overlay.
            // Unity may otherwise terminate the original compiler helper.
            foreach (string relative in new[] { "Library/ilpp.pid", "Library/Pipeline/.unity-pipeline-port" })
            {
                string file = Path.Combine(root, relative); PlainPath(file, workspace);
                if (File.Exists(file)) File.Delete(file);
            }
            string runState = Path.Combine(root, "Library", "MCPForUnity", "RunState"); PlainPath(runState, workspace);
            if (Directory.Exists(runState))
                foreach (string file in Directory.EnumerateFiles(runState, "mcp_http_*.pid")) { PlainPath(file, workspace); File.Delete(file); }
        }
        string infrastructure = Path.Combine(root, "Assets", "__Splinter__");
        string markerPath = Path.Combine(infrastructure, "workspace.marker");
        if (Directory.Exists(infrastructure)) VerifyInfrastructure(workspace, infrastructure);
        else if (File.Exists(infrastructure) || File.Exists(infrastructure + ".meta"))
            throw new WorkspaceException("ManagedPathConflict", "The reserved bridge path already exists.");
        Directory.CreateDirectory(Path.Combine(infrastructure, "Editor"));
        using Stream resource = typeof(UnityWorkspaceProvider).Assembly.GetManifestResourceStream("Splinter.Editor.WorkspaceBridge.cs")
            ?? throw new WorkspaceException("BridgeMissing", "The embedded Unity bridge is missing.");
        using var reader = new StreamReader(resource);
        string code = await reader.ReadToEndAsync(ct);
        await File.WriteAllTextAsync(Path.Combine(infrastructure, "Editor", "WorkspaceBridge.cs"), code, ct);
        await File.WriteAllTextAsync(markerPath, JsonSerializer.Serialize(new InfrastructureMarker(workspace.Id, Hash(code)), Json), ct);
        string token = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
        await Atomic(controlPath, "launch-intent.json", new { workspace.Id, token, executable, projectPath = root }, ct);
        foreach (string name in new[] { "heartbeat.json", "stop-request.json", "stop-result.json" })
        {
            string path = Path.Combine(controlPath, name);
            PlainPath(path);
            if (File.Exists(path)) File.Delete(path);
        }
        return await Processes.StartAsync(new ProcessLaunch(executable,
            ["-projectPath", root, "-logFile", Path.Combine(controlPath, "editor.log"),
             "-splinterControlPath", Path.GetFullPath(controlPath), "-splinterToken", token], root, token), controlPath, ct);
    }

    public async Task<ApplicationState> ObserveAsync(WorkspaceRecord workspace, string controlPath, CancellationToken ct = default)
    {
        var lease = await Read<ApplicationLease>(controlPath, "launch-receipt.json", ct);
        if (lease is null)
        {
            bool uncertain = File.Exists(Path.Combine(controlPath, "launch-intent.json"));
            return new(false, false, uncertain ? "launch-uncertain" : "not-started", Resources: new(!uncertain, false, 0, 0, 0));
        }
        var resources = processes is null ? new ProcessResources(false, OwnedProcess(lease), 0, 0, 0) : await processes.ObserveAsync(lease, ct);
        if (!OwnedProcess(lease)) return new(false, false, resources.ActiveProcesses > 0 ? "draining" : "exited", lease, Resources: resources);
        var heartbeat = await Read<Heartbeat>(controlPath, "heartbeat.json", ct);
        bool valid = heartbeat is not null && heartbeat.Token == lease.Token && heartbeat.Pid == lease.ProcessId &&
            heartbeat.StartTimeUtcTicks == lease.StartTimeUtcTicks && SamePath(heartbeat.ProjectPath, workspace.Path) &&
            DateTimeOffset.UtcNow - heartbeat.ObservedAt < TimeSpan.FromSeconds(15);
        return valid ? new(true, heartbeat!.Ready, heartbeat.Phase, lease, Resources: resources)
            : new(true, false, "starting-or-unresponsive; inspect editor.log", lease, Resources: resources);
    }

    public async Task StopAsync(WorkspaceRecord workspace, string controlPath, CancellationToken ct = default)
    {
        var lease = await Read<ApplicationLease>(controlPath, "launch-receipt.json", ct);
        if (lease is null)
        {
            if (File.Exists(Path.Combine(controlPath, "launch-intent.json")))
                throw new WorkspaceException("LaunchUncertain", "Cannot prove the launch is stopped; preserve the workspace.");
            return;
        }
        if (!OwnedProcess(lease)) { await Processes.WaitForExitAsync(lease, ct); return; }
        string requestId = Guid.NewGuid().ToString("N");
        await Atomic(controlPath, "stop-request.json", new { requestId, token = lease.Token }, ct);
        await WaitForNormalExitAsync(() => OwnedProcess(lease), async token =>
        {
            var refusal = await Read<StopResult>(controlPath, "stop-result.json", token);
            return refusal?.RequestId == requestId ? refusal.Error : null;
        }, TimeProvider.System, Task.Delay, ct);
        await Processes.WaitForExitAsync(lease, ct);
    }

    internal static async Task WaitForNormalExitAsync(Func<bool> ownedProcess,
        Func<CancellationToken, Task<string?>> refusal, TimeProvider clock,
        Func<TimeSpan, CancellationToken, Task> delay, CancellationToken ct)
    {
        // Real cold-cache shutdown can take over 30 seconds while Unity saves
        // and drains helpers. Keep a bounded monotonic deadline; never kill or
        // release resources merely because it expires.
        var budget = TimeSpan.FromMinutes(2);
        long start = clock.GetTimestamp();
        while (ownedProcess())
        {
            ct.ThrowIfCancellationRequested();
            if (await refusal(ct) is { } reason)
                throw new WorkspaceException("EditorStopBlocked", reason);
            TimeSpan remaining = budget - clock.GetElapsedTime(start);
            if (remaining <= TimeSpan.Zero)
                throw new WorkspaceException("EditorStopTimeout", "Owned Editor did not exit within two minutes. Worktree and results are retained; inspect its log before retrying.");
            await delay(remaining < TimeSpan.FromMilliseconds(250) ? remaining : TimeSpan.FromMilliseconds(250), ct);
        }
    }

    public async Task RemoveInfrastructureAsync(WorkspaceRecord workspace, string controlPath, CancellationToken ct = default)
    {
        var state = await ObserveAsync(workspace, controlPath, ct);
        if (state.Running || state.Resources is not { OwnershipVerified: true, ActiveProcesses: 0 })
            throw new WorkspaceException("EditorStillRunning", "Stop the Editor before releasing its files.");
        PlainPath(workspace.Path, workspace);
        string infrastructure = Path.Combine(workspace.Path, "Assets", "__Splinter__");
        if (!Directory.Exists(infrastructure)) return;
        VerifyInfrastructure(workspace, infrastructure);
        string lockFile = Path.Combine(workspace.Path, "Temp", "UnityLockfile");
        if (File.Exists(lockFile))
        {
            try { using var probe = new FileStream(lockFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None); }
            catch (IOException) { throw new WorkspaceException("EditorLockHeld", "An Editor still holds the workspace lock."); }
        }
        // VerifyInfrastructure checks the exact target and every descendant before deletion.
        Directory.Delete(infrastructure, recursive: true);
        string meta = infrastructure + ".meta";
        PlainPath(meta, workspace);
        if (File.Exists(meta)) File.Delete(meta);
    }

    private static void VerifyInfrastructure(WorkspaceRecord workspace, string path)
    {
        string expected = Path.GetFullPath(Path.Combine(workspace.Path, "Assets", "__Splinter__"));
        if (!SamePath(path, expected)) throw new WorkspaceException("UnsafePath", "Infrastructure path mismatch.");
        PlainPath(path, workspace);
        string markerPath = Path.Combine(path, "workspace.marker");
        PlainPath(markerPath, workspace);
        if (!File.Exists(markerPath)) throw new WorkspaceException("ManagedPathConflict", "Existing bridge directory has no ownership marker.");
        var marker = JsonSerializer.Deserialize<InfrastructureMarker>(File.ReadAllText(markerPath), Json);
        string bridge = Path.Combine(path, "Editor", "WorkspaceBridge.cs");
        PlainPath(bridge, workspace);
        if (marker?.WorkspaceId != workspace.Id || !File.Exists(bridge) || marker.CodeHash != Hash(File.ReadAllText(bridge)))
            throw new WorkspaceException("ManagedFilesChanged", "Bridge ownership or code changed; preserve it for inspection.");
        var allowed = new HashSet<string>(StringComparer.Ordinal)
        { "workspace.marker", "workspace.marker.meta", "Editor.meta", "Editor/WorkspaceBridge.cs", "Editor/WorkspaceBridge.cs.meta" };
        var pending = new Stack<string>(); pending.Push(path);
        while (pending.Count != 0)
        {
            string directory = pending.Pop();
            foreach (string entry in Directory.EnumerateFileSystemEntries(directory))
            {
                if ((File.GetAttributes(entry) & FileAttributes.ReparsePoint) != 0)
                    throw new WorkspaceException("UnsafePath", "Infrastructure contains a reparse point.");
                string relative = Path.GetRelativePath(path, entry).Replace('\\', '/');
                if (Directory.Exists(entry))
                {
                    if (relative != "Editor") throw new WorkspaceException("ManagedFilesChanged", "Unexpected bridge directory: " + relative);
                    pending.Push(entry);
                }
                else if (!allowed.Contains(relative)) throw new WorkspaceException("ManagedFilesChanged", "Unexpected bridge file: " + relative);
            }
        }
    }

    private static string ValidateRoot(WorkspaceRecord workspace)
    {
        string full = Path.GetFullPath(workspace.Path); PlainPath(full, workspace);
        if (!Directory.Exists(Path.Combine(full, "Assets")) || !File.Exists(Path.Combine(full, "ProjectSettings", "ProjectVersion.txt")))
            throw new WorkspaceException("InvalidUnityProject", "The workspace is not a Unity project root.");
        return full;
    }
    private IProcessWorkspaceProvider Processes => processes ?? throw new WorkspaceException("ProcessProviderMissing", "Compose a process ownership provider before launching Unity.");
    private static string ResolveEditor(string version)
    {
        string? configured = Environment.GetEnvironmentVariable("SPLINTER_UNITY_EDITOR");
        string executable = configured ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Unity", "Hub", "Editor", version, "Editor", "Unity.exe");
        executable = Path.GetFullPath(executable);
        if (!File.Exists(executable) || Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(executable))) != version)
            throw new WorkspaceException("UnityEditorMissing", "Exact Unity patch " + version + " was not found. Set SPLINTER_UNITY_EDITOR to its executable.");
        return executable;
    }
    private static bool OwnedProcess(ApplicationLease lease)
    {
        try
        {
            using var process = Process.GetProcessById(lease.ProcessId);
            if (process.HasExited || process.StartTime.ToUniversalTime().Ticks != lease.StartTimeUtcTicks) return false;
            if (!SamePath(process.MainModule?.FileName ?? "", lease.ExecutablePath))
                throw new WorkspaceException("ProcessOwnershipMismatch", "Process executable no longer matches its lease.");
            return true;
        }
        catch (ArgumentException) { return false; }
        catch (InvalidOperationException) { return false; }
        catch (Win32Exception ex) { throw new WorkspaceException("ProcessOwnershipUnknown", ex.Message); }
    }
    private static bool SamePath(string left, string right) => string.Equals(Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar),
        Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar), OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    private static string Hash(string code) => Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(code)));
    private static void PlainPath(string path, WorkspaceRecord? workspace = null)
    {
        string? ownedMountRoot = WorkspaceMountAccess.ObservedRoot(workspace);
        for (string? p = Path.GetFullPath(path); p is not null; p = Path.GetDirectoryName(p))
            if (ownedMountRoot is null || !SamePath(p, ownedMountRoot))
            if ((File.Exists(p) || Directory.Exists(p)) && (File.GetAttributes(p) & FileAttributes.ReparsePoint) != 0)
                throw new WorkspaceException("UnsafePath", "Reparse point: " + p);
    }
    private static async Task<T?> Read<T>(string root, string name, CancellationToken ct) where T : class
    {
        string path = Path.Combine(root, name); PlainPath(path);
        if (!File.Exists(path)) return null;
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        return await JsonSerializer.DeserializeAsync<T>(stream, Json, ct);
    }
    private static async Task Atomic(string root, string name, object value, CancellationToken ct)
    {
        string path = Path.Combine(root, name); PlainPath(path);
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(value, Json), ct);
        File.Move(temporary, path, overwrite: true);
    }
    private sealed record InfrastructureMarker(string WorkspaceId, string CodeHash);
    private sealed record Heartbeat(string Token, int Pid, long StartTimeUtcTicks, string ProjectPath,
        string Phase, bool Ready, DateTimeOffset ObservedAt);
    private sealed record StopResult(string RequestId, string Error);
}
