using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Splinter.Core;
using Splinter.Runtime;

namespace Splinter.Platform.Windows;

public sealed class WindowsProcessWorkspaceProvider(string? runnerCache = null) : IProcessWorkspaceProvider
{
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public async Task<ApplicationLease> StartAsync(ProcessLaunch launch, string controlPath, CancellationToken ct = default)
    {
        if (!OperatingSystem.IsWindows()) throw new WorkspaceException("PlatformUnsupported", "Process ownership requires Windows.");
        CheckPath(controlPath); Directory.CreateDirectory(controlPath);
        string request = Path.Combine(controlPath, "process-request.json");
        await WriteAsync(request, launch, ct);
        string runner = PrepareRunner(runnerCache ?? Path.Combine(controlPath, "runner"));
        string dotnet = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ??
            (Path.GetFileNameWithoutExtension(Environment.ProcessPath) == "dotnet" ? Environment.ProcessPath! : "dotnet");
        using var supervisor = WindowsProcessGroup.StartSupervisor(dotnet, [runner, "supervise", request], controlPath);
        for (int attempt = 0; attempt < 100; attempt++)
        {
            var receipt = await ReadAsync<ApplicationLease>(Path.Combine(controlPath, "launch-receipt.json"), ct);
            if (receipt?.Token == launch.Token) return receipt;
            var error = await ReadAsync<LaunchError>(Path.Combine(controlPath, "process-error.json"), ct);
            if (error?.Token == launch.Token) throw new WorkspaceException("SupervisorLaunchFailed", error.Message);
            if (supervisor.HasExited) throw new WorkspaceException("SupervisorLaunchFailed", "Supervisor exited before its durable receipt; inspect process-error.json.");
            await Task.Delay(100, ct);
        }
        throw new WorkspaceException("LaunchUncertain", "Supervisor has not published its receipt. Preserve state and query status before retrying.");
    }
    public async Task<ProcessResources> ObserveAsync(ApplicationLease lease, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (lease.Group is not { } group)
            return new ProcessResources(false, WindowsProcessGroup.IdentityAlive(lease.ProcessId, lease.StartTimeUtcTicks), 0, 0, 0);
        using var job = WindowsProcessGroup.Open(group.Name);
        bool keeper = WindowsProcessGroup.IdentityAlive(group.SupervisorId, group.SupervisorStartTimeUtcTicks);
        var resources = job?.Observe(lease) ?? new ProcessResources(true, false, 0, 0, 0);
        if (job is null && WindowsProcessGroup.IdentityAlive(lease.ProcessId, lease.StartTimeUtcTicks))
            throw new WorkspaceException("ProcessOwnershipUnknown", "Editor is alive without its owning group.");
        if (job is null && !keeper)
        {
            var proof = group.ControlPath is null ? null : await ReadAsync<ExitProof>(Path.Combine(group.ControlPath, "process-exit.json"), ct);
            resources = resources with { OwnershipVerified = proof?.Token == lease.Token && proof.ActiveJobProcesses == 0 };
        }
        if (keeper)
        {
            try
            {
                using var process = Process.GetProcessById(group.SupervisorId);
                resources = resources with { ActiveProcesses = resources.ActiveProcesses + 1,
                    WorkingSetBytes = resources.WorkingSetBytes + process.WorkingSet64, PrivateBytes = resources.PrivateBytes + process.PrivateMemorySize64 };
            }
            catch (ArgumentException) { }
            catch (InvalidOperationException) { }
        }
        return resources;
    }
    public async Task WaitForExitAsync(ApplicationLease lease, CancellationToken ct = default)
    {
        if (lease.Group is null) throw new WorkspaceException("LegacyProcessOwnership", "This older launch has no process group. Inspect its descendants before migrating; cleanup is not proven.");
        for (int attempt = 0; attempt < 120; attempt++)
        {
            var state = await ObserveAsync(lease, ct);
            if (!state.OwnershipVerified) throw new WorkspaceException("ProcessOwnershipUnknown", "Supervisor exit has no cleanup proof. Preserve the workspace for recovery.");
            if (state.ActiveProcesses == 0) return;
            await Task.Delay(100, ct);
        }
        throw new WorkspaceException("ProcessCleanupIncomplete", "Owned processes remain. Preserve the workspace and retry status/stop.");
    }
    internal static async Task WriteAsync<T>(string path, T data, CancellationToken ct = default)
    {
        CheckPath(path);
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            await JsonSerializer.SerializeAsync(stream, data, Json, ct); stream.Flush(true);
        }
        try { AtomicFilePublication.Publish(temporary, path); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ex.Data["publicationPath"] = path;
            ex.Data["publicationTemporaryPath"] = temporary;
            throw;
        }
    }
    public static string PrepareRunner(string cache)
    {
        // A running helper must not lock the development build output. Reuse one
        // small immutable bundle per binary version across all workspace Editors.
        string assembly = typeof(WindowsProcessWorkspaceProvider).Assembly.Location;
        string directory = Path.GetDirectoryName(assembly)!;
        string[] names = ["Splinter.Platform.Windows.dll", "Splinter.Platform.Windows.deps.json",
            "Splinter.Platform.Windows.runtimeconfig.json", "Splinter.Core.dll", "Splinter.Runtime.dll", "winfsp-msil.dll"];
        var contents = names.ToDictionary(name => name, name => File.ReadAllBytes(Path.Combine(directory, name)));
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (string name in names) hash.AppendData(SHA256.HashData(contents[name]));
        string target = Path.Combine(Path.GetFullPath(cache), Convert.ToHexStringLower(hash.GetHashAndReset()));
        CheckPath(target);
        if (!Directory.Exists(target))
        {
            Directory.CreateDirectory(cache);
            string temporary = target + "." + Guid.NewGuid().ToString("N"); Directory.CreateDirectory(temporary);
            foreach (string name in names) File.WriteAllBytes(Path.Combine(temporary, name), contents[name]);
            try { Directory.Move(temporary, target); }
            catch (IOException) when (Directory.Exists(target))
            {
                // Only files just written in our exact temporary directory.
                foreach (string name in names) File.Delete(Path.Combine(temporary, name));
                Directory.Delete(temporary);
            }
        }
        foreach (string name in names)
        {
            string file = Path.Combine(target, name); CheckPath(file);
            if (!File.ReadAllBytes(file).AsSpan().SequenceEqual(contents[name]))
                throw new WorkspaceException("RunnerChanged", "Cached supervisor bundle changed; preserve it for inspection.");
        }
        return Path.Combine(target, names[0]);
    }
    internal static async Task<T?> ReadAsync<T>(string path, CancellationToken ct = default)
    {
        CheckPath(path); if (!File.Exists(path)) return default;
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        return await JsonSerializer.DeserializeAsync<T>(stream, Json, ct);
    }
    internal static void CheckPath(string path)
    {
        for (string? p = Path.GetFullPath(path); p is not null; p = Path.GetDirectoryName(p))
            if ((Directory.Exists(p) || File.Exists(p)) && (File.GetAttributes(p) & FileAttributes.ReparsePoint) != 0)
                throw new WorkspaceException("UnsafePath", "Reparse points are not supported: " + p);
    }
    internal sealed record LaunchError(string Token, string Message);
    private sealed record ExitProof(string Token, int ActiveJobProcesses);
}
