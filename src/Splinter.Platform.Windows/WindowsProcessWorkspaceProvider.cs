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
        if (lease.Group is null) throw new WorkspaceException("ProcessOwnershipUnknown", "The launch has no owned process group. Preserve its processes until ownership can be proven.");
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
        try
        {
            for (int retry = 0; ; retry++)
            {
                try { AtomicFilePublication.Publish(temporary, path); break; }
                // A brief observer without delete sharing must not tear down a
                // live mount. Retry the same flushed file; never remove the
                // destination, change attributes or copy over visible state.
                catch (IOException ex) when (OperatingSystem.IsWindows() && retry < 20 &&
                    (ex.HResult & 0xffff) is 32 or 33 or 1175 && File.Exists(temporary))
                { await Task.Delay(50, ct); }
            }
        }
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
        var sources = new List<FileStream>(names.Length);
        var digests = new List<byte[]>(names.Length);
        try
        {
            // Keep read-only source handles until publication and validation
            // finish. Writers/replacements cannot mix builds between hash/copy.
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            foreach (string name in names)
            {
                var source = new FileStream(Path.Combine(directory, name), FileMode.Open, FileAccess.Read,
                    FileShare.Read, 4096, FileOptions.SequentialScan);
                sources.Add(source);
                byte[] digest = SHA256.HashData(source);
                digests.Add(digest); hash.AppendData(digest);
            }
            string target = Path.Combine(Path.GetFullPath(cache), Convert.ToHexStringLower(hash.GetHashAndReset()));
            CheckPath(target);
            if (!Directory.Exists(target))
            {
                Directory.CreateDirectory(cache);
                string temporary = target + "." + Guid.NewGuid().ToString("N"); Directory.CreateDirectory(temporary);
                for (int i = 0; i < names.Length; i++)
                {
                    sources[i].Position = 0;
                    using var output = new FileStream(Path.Combine(temporary, names[i]), FileMode.CreateNew, FileAccess.Write, FileShare.None);
                    sources[i].CopyTo(output, 65536);
                }
                try { Directory.Move(temporary, target); }
                catch (IOException) when (Directory.Exists(target))
                {
                    // Only files just written in our exact temporary directory.
                    foreach (string name in names) File.Delete(Path.Combine(temporary, name));
                    Directory.Delete(temporary);
                }
            }
            for (int i = 0; i < names.Length; i++)
            {
                string file = Path.Combine(target, names[i]); CheckPath(file);
                using var cached = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.SequentialScan);
                if (cached.Length != sources[i].Length || !SHA256.HashData(cached).AsSpan().SequenceEqual(digests[i]))
                    throw new WorkspaceException("RunnerChanged", "Cached supervisor bundle changed; preserve it for inspection.");
            }
            return Path.Combine(target, names[0]);
        }
        finally
        {
            foreach (var source in sources) source.Dispose();
        }
    }
    internal static async Task<T?> ReadAsync<T>(string path, CancellationToken ct = default, bool waitForPublication = true)
    {
        long started = Stopwatch.GetTimestamp();
        FileStream stream;
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                // Both the path guard and Open can race Replace on Windows.
                // File.Exists also hides sharing/access failures as absence.
                CheckPath(path);
                stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                break;
            }
            catch (IOException ex) when (ex is FileNotFoundException or DirectoryNotFoundException ||
                OperatingSystem.IsWindows() && (ex.HResult & 0xffff) is 32 or 33)
            {
                bool missing = ex is FileNotFoundException or DirectoryNotFoundException;
                TimeSpan remaining = TimeSpan.FromSeconds(1) - Stopwatch.GetElapsedTime(started);
                // Only repeatable request polling can treat a missing file as
                // "no request yet" immediately. Observation/ownership readers
                // must allow an in-flight publication to become visible first.
                if (missing && (!waitForPublication || !OperatingSystem.IsWindows() || remaining <= TimeSpan.Zero)) return default;
                if (remaining <= TimeSpan.Zero) throw;
                await Task.Delay(remaining < TimeSpan.FromMilliseconds(25) ? remaining : TimeSpan.FromMilliseconds(25), ct);
            }
        }
        await using var ownedStream = stream;
        // A complete but invalid receipt is corruption, not a publication race.
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
