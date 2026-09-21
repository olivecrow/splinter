using System.Diagnostics;
using System.Text.Json;
using Fsp;
using Splinter.Core;
using Splinter.Runtime;

namespace Splinter.Platform.Windows;

public sealed record OverlayServeRequest(OverlayIdentity Identity, string DeltaPath, string MountPath,
    string ControlPath, string Token);

public static class WinFspOverlayServer
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public static async Task<int> RunAsync(string requestPath)
    {
        OverlayServeRequest request = JsonSerializer.Deserialize<OverlayServeRequest>(await File.ReadAllTextAsync(requestPath), Json)
            ?? throw new ArgumentException("Missing overlay request.");
        WindowsProcessWorkspaceProvider.CheckPath(request.ControlPath);
        Directory.CreateDirectory(request.ControlPath);
        string statePath = Path.Combine(request.ControlPath, "state.json");
        int failures = 0;
        string? lastFailureCode = null;
        using var self = Process.GetCurrentProcess();
        try
        {
            if (string.IsNullOrWhiteSpace(request.Token) || request.Identity.Id.Length == 0) throw new ArgumentException("An owned request identity is required.");
            string mountPath = Path.GetFullPath(request.MountPath), deltaPath = Path.GetFullPath(request.DeltaPath);
            foreach (string other in new[] { deltaPath, request.ControlPath, request.Identity.Snapshot.SourcePath })
            {
                string normalized = Path.TrimEndingDirectorySeparator(Path.GetFullPath(other));
                if (mountPath.Equals(normalized, StringComparison.OrdinalIgnoreCase) ||
                    mountPath.StartsWith(normalized + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                    normalized.StartsWith(mountPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    throw new WorkspaceException("UnsafePath", "Mount, delta, control and mutable source must be separate trees.");
            }
            WindowsProcessWorkspaceProvider.CheckPath(mountPath); WindowsProcessWorkspaceProvider.CheckPath(deltaPath);
            if (Directory.Exists(mountPath) && Directory.EnumerateFileSystemEntries(mountPath).Any())
                throw new WorkspaceException("MountPathNotEmpty", "A mount must never hide an existing tree.");
            // WinFsp creates the mount directory itself. Passing an existing
            // empty directory returns STATUS_OBJECT_NAME_COLLISION.
            if (Directory.Exists(mountPath)) Directory.Delete(mountPath, false);
            using var snapshot = new VssSnapshotTree(request.Identity.Snapshot);
            using var store = new JournalDeltaStore(deltaPath, request.Identity, snapshot);
            var fileSystem = new WinFspOverlayFileSystem(store, ex =>
            {
                int count = Interlocked.Increment(ref failures);
                if (ex is WorkspaceException failure) Volatile.Write(ref lastFailureCode, failure.Code);
                if (count <= 100)
                    lock (Json) File.AppendAllText(Path.Combine(request.ControlPath, "io-errors.jsonl"),
                        JsonSerializer.Serialize(new { at = DateTimeOffset.UtcNow, type = ex.GetType().Name, code = (ex as WorkspaceException)?.Code, ex.Message }, Json) + "\n");
            });
            using (var host = new FileSystemHost(fileSystem))
            {
                string? lastFlushRequest = null;
                int status = host.MountEx(mountPath, 8, null, false, 0);
                if (status < 0) throw new WorkspaceException("OverlayMountFailed", $"WinFsp mount returned 0x{status:x8}.");
                while (true)
                {
                    self.Refresh();
                    await WindowsProcessWorkspaceProvider.WriteAsync(statePath, new
                    {
                        version = 1, request.Token, request.Identity.Id,
                        phase = Directory.Exists(request.Identity.Snapshot.RootPath) ? "mounted" : "snapshot-missing", observedAt = DateTimeOffset.UtcNow,
                        snapshotId = request.Identity.Snapshot.Id, mountPath, deltaPath,
                        processId = self.Id, processStartTimeUtcTicks = self.StartTime.ToUniversalTime().Ticks,
                        openHandles = fileSystem.OpenHandles, retainedFileContexts = fileSystem.RetainedFileContexts,
                        workingSetBytes = self.WorkingSet64, privateBytes = self.PrivateMemorySize64,
                        ioFailures = Volatile.Read(ref failures), lastFailureCode = Volatile.Read(ref lastFailureCode), storage = store.Observe()
                    });
                    var stop = await WindowsProcessWorkspaceProvider.ReadAsync<StopRequest>(Path.Combine(request.ControlPath, "stop.json"));
                    if (stop?.Token == request.Token)
                    {
                        if (fileSystem.OpenHandles == 0) { store.Flush(); break; }
                        // A normal stop is deferred while clients own handles.
                        // Throwing here would dispose the host and forcibly unmount it.
                        await WindowsProcessWorkspaceProvider.WriteAsync(Path.Combine(request.ControlPath, "stop-blocked.json"),
                            new { request.Token, error = "OverlayHandlesRemain", openHandles = fileSystem.OpenHandles, observedAt = DateTimeOffset.UtcNow });
                    }
                    var flush = await WindowsProcessWorkspaceProvider.ReadAsync<FlushRequest>(Path.Combine(request.ControlPath, "flush.json"));
                    if (flush?.Token == request.Token && flush.RequestId != lastFlushRequest)
                    {
                        store.Flush();
                        await WindowsProcessWorkspaceProvider.WriteAsync(Path.Combine(request.ControlPath, "flush-result.json"),
                            new { request.Token, flush.RequestId, observedAt = DateTimeOffset.UtcNow, storage = store.Observe() });
                        lastFlushRequest = flush.RequestId;
                    }
                    await Task.Delay(1000);
                }
            }
            await WindowsProcessWorkspaceProvider.WriteAsync(statePath, new
            {
                version = 1, request.Token, request.Identity.Id, phase = "unmounted", observedAt = DateTimeOffset.UtcNow,
                snapshotId = request.Identity.Snapshot.Id, mountPath, deltaPath, processId = self.Id,
                processStartTimeUtcTicks = self.StartTime.ToUniversalTime().Ticks, openHandles = fileSystem.OpenHandles,
                retainedFileContexts = fileSystem.RetainedFileContexts,
                // The external owner must verify process exit and memory zero.
                storage = store.Observe(), ioFailures = Volatile.Read(ref failures)
            });
            return 0;
        }
        catch (Exception ex)
        {
            await WindowsProcessWorkspaceProvider.WriteAsync(statePath, new
            {
                version = 1, request.Token, request.Identity.Id, phase = "failed", observedAt = DateTimeOffset.UtcNow,
                processId = self.Id, processStartTimeUtcTicks = self.StartTime.ToUniversalTime().Ticks,
                error = ex is WorkspaceException w ? w.Code : ex.GetType().Name, ex.Message,
                preservedDeltaPath = request.DeltaPath, snapshotId = request.Identity.Snapshot.Id
            });
            return 1;
        }
    }
    private sealed record StopRequest(string Token);
    private sealed record FlushRequest(string Token, string RequestId);
}
