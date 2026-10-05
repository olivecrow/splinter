using System.Buffers.Binary;
using System.Diagnostics;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.Json;
using Splinter.Core;

namespace Splinter.Platform.Windows;

internal static class StorageAuthorityServer
{
    internal static async Task RunAsync(Action ready, CancellationToken ct)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
        using var identity = WindowsIdentity.GetCurrent();
        if (identity.User?.Value != "S-1-5-18" || !StorageAuthorityProcessor.Same(Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory), StorageAuthorityProtocol.InstallPath))
            throw StorageAuthorityProtocol.Error("StorageServiceUnsafe", "Run only the installed SCM service as LocalSystem.");
        using var pinned = new StorageAuthorityPaths();
        pinned.Pin(StorageAuthorityProtocol.InstallPath); pinned.Pin(StorageAuthorityProtocol.StatePath);
        StorageAuthorityPaths.RequireProtectedDirectory(StorageAuthorityProtocol.InstallPath);
        StorageAuthorityPaths.RequireProtectedDirectory(StorageAuthorityProtocol.StatePath);
        var config = StorageAuthorityProcessor.Read<StorageAuthorityConfiguration>(Path.Combine(StorageAuthorityProtocol.StatePath, "configuration.json"))
            ?? throw new IOException("Missing authority configuration.");
        if (config.Version != 1 || config.Grants.Length == 0) throw StorageAuthorityProtocol.Error("StorageServiceVersionMismatch", "Repair the current service configuration.");
        string expectedPowerShell = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "PowerShell", "7", "pwsh.exe");
        if (!StorageAuthorityProcessor.Same(config.PowerShellPath, expectedPowerShell)) throw new IOException("Only the installed PowerShell 7 host is permitted.");
        pinned.Pin(Path.GetDirectoryName(config.PowerShellPath)!); pinned.Pin(config.PowerShellPath, directory: false);
        StorageAuthorityPaths.RequireProtectedDirectory(Path.GetDirectoryName(config.PowerShellPath)!, allowWindowsInstaller: true);
        var processor = new StorageAuthorityProcessor(StorageAuthorityProtocol.StatePath, config, new Operations(config.PowerShellPath));
        var security = PipeSecurityFor(config.Grants.Select(g => g.UserSid));
        bool first = true;
        while (!ct.IsCancellationRequested)
        {
            // One instance serializes authority mutations. FirstPipeInstance
            // rejects an already occupied name rather than joining a fake server.
            using var pipe = NamedPipeServerStreamAcl.Create(StorageAuthorityProtocol.PipeName, PipeDirection.InOut, 1,
                PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.FirstPipeInstance, 65536, 65536, security);
            if (first) { ready(); first = false; }
            await pipe.WaitForConnectionAsync(ct);
            StorageAuthorityRequest? request = null;
            try
            {
                request = StorageAuthorityProtocol.Decode<StorageAuthorityRequest>(await ReadRequestAsync(pipe, ct));
                string? sid = null;
                pipe.RunAsClient(() => { if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
                    using var peer = WindowsIdentity.GetCurrent(true); sid = peer?.User?.Value; });
                if (sid is null) throw StorageAuthorityProtocol.Error("StoragePeerDenied", "A local authenticated user token is required.");
                IDisposable Authorize(StorageGrant grant, StorageAuthorityRequest r)
                {
                    StorageAuthorityPaths? guard = null;
                    pipe.RunAsClient(() => guard = StorageAuthorityPaths.Authorize(grant, r));
                    return guard ?? throw new UnauthorizedAccessException();
                }
                using var operation = CancellationTokenSource.CreateLinkedTokenSource(ct); operation.CancelAfter(TimeSpan.FromMinutes(3));
                var response = await processor.ProcessAsync(sid, request, Authorize, operation.Token);
                using var reply = CancellationTokenSource.CreateLinkedTokenSource(ct); reply.CancelAfter(TimeSpan.FromSeconds(1));
                await WriteFrameAsync(pipe, JsonSerializer.SerializeToUtf8Bytes(response, StorageAuthorityProtocol.Json), reply.Token);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or WorkspaceException or System.ComponentModel.Win32Exception or OperationCanceledException or ArgumentException or InvalidOperationException or KeyNotFoundException or NullReferenceException)
            {
                // Keep the server available after a rejected/disconnected client.
                // No failure turns an uncertain privileged side effect into success.
                string code = ex is WorkspaceException w ? w.Code : ex is JsonException or ArgumentException ? "StorageRequestInvalid" : "StorageOperationUncertain";
                var failure = new StorageAuthorityResponse(1, request?.RequestId ?? "", request?.Operation ?? "", code, []);
                try { using var reply = CancellationTokenSource.CreateLinkedTokenSource(ct); reply.CancelAfter(TimeSpan.FromSeconds(1));
                    await WriteFrameAsync(pipe, JsonSerializer.SerializeToUtf8Bytes(failure, StorageAuthorityProtocol.Json), reply.Token); }
                catch (Exception send) when (send is IOException or OperationCanceledException) { }
            }
        }
    }
    internal static async Task<byte[]> ReadRequestAsync(Stream stream, CancellationToken ct)
    {
        using var receive = CancellationTokenSource.CreateLinkedTokenSource(ct);
        receive.CancelAfter(TimeSpan.FromSeconds(1));
        return await ReadFrameAsync(stream, StorageAuthorityProtocol.MaxRequestBytes, receive.Token);
    }
    internal static PipeSecurity PipeSecurityFor(IEnumerable<string> userSids)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
        var security = new PipeSecurity(); security.SetAccessRuleProtection(true, false);
        security.AddAccessRule(new(new SecurityIdentifier(WellKnownSidType.NetworkSid, null), PipeAccessRights.FullControl, AccessControlType.Deny));
        security.AddAccessRule(new(new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null), PipeAccessRights.FullControl, AccessControlType.Allow));
        foreach (string sid in userSids.Distinct(StringComparer.Ordinal))
            security.AddAccessRule(new(new SecurityIdentifier(sid), PipeAccessRights.ReadWrite | PipeAccessRights.Synchronize, AccessControlType.Allow));
        return security;
    }
    internal static async Task<byte[]> ReadFrameAsync(Stream stream, int limit, CancellationToken ct)
    {
        var header = new byte[4]; await stream.ReadExactlyAsync(header, ct);
        int length = BinaryPrimitives.ReadInt32LittleEndian(header);
        if (length is <= 0 || length > limit) throw new JsonException("Invalid authority frame length.");
        var bytes = new byte[length]; await stream.ReadExactlyAsync(bytes, ct); return bytes;
    }
    internal static async Task WriteFrameAsync(Stream stream, byte[] bytes, CancellationToken ct)
    {
        var header = new byte[4]; BinaryPrimitives.WriteInt32LittleEndian(header, bytes.Length);
        await stream.WriteAsync(header, ct); await stream.WriteAsync(bytes, ct); await stream.FlushAsync(ct);
    }
    private sealed class Operations(string powershell) : IStorageAuthorityOperations
    {
        public async Task<string?> ExecuteAsync(string operation, string receipts, string source, string owner, CancellationToken ct)
        {
            // The helper and working directory are installed/protected. Only
            // fixed semantic parameters enter ArgumentList-equivalent quoting.
            string script = Path.Combine(StorageAuthorityProtocol.InstallPath, "vss-snapshot.ps1");
            using var resource = typeof(VssSnapshotProvider).Assembly.GetManifestResourceStream("Splinter.VssSnapshot.ps1")!;
            using var reader = new StreamReader(resource);
            if (await reader.ReadToEndAsync(ct) != await File.ReadAllTextAsync(script, ct)) throw new IOException("Installed helper mismatch.");
            using var job = WindowsProcessGroup.Create("Local\\Splinter.StorageAuthority." + Guid.NewGuid().ToString("N"));
            using var child = job.Launch(new(powershell, ["-NoProfile", "-NonInteractive", "-File", script,
                "-Action", operation, "-ReceiptDirectory", receipts, "-SourcePath", source, "-OwnerId", owner],
                StorageAuthorityProtocol.InstallPath, Guid.NewGuid().ToString("N")));
            using var process = Process.GetProcessById(child.Id); child.Resume();
            await process.WaitForExitAsync(ct);
            if (process.ExitCode == 0) return null;
            var outcome = StorageAuthorityProcessor.Read<JsonElement?>(Path.Combine(receipts, "operation-result.json"));
            string? error = outcome?.GetProperty("error").GetString();
            return new[] { "SnapshotHeadroomRequired", "SnapshotIdentityMismatch", "SnapshotObservationIncomplete", "CaptureReconcileRequired",
                "SnapshotReleaseIsolationRequired", "SnapshotReclamationInvalid", "SnapshotCollateralLoss", "WorkspaceBusy", "SnapshotInUse", "StorageDiscardNotAuthorized" }
                .FirstOrDefault(code => error?.Contains(code, StringComparison.Ordinal) == true) ?? "SnapshotOperationIncomplete";
        }
    }
}
