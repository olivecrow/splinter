using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text.Json;
using Microsoft.Win32.SafeHandles;
using Splinter.Core;

namespace Splinter.Platform.Windows;

public sealed record StorageServiceStatus(string ServiceName, string State, int ProtocolVersion,
    string InstallPath, string StatePath, bool CallerElevated, string? FailureCode);

public static class StorageServiceInspection
{
    public static StorageServiceStatus Observe()
    {
        if (!OperatingSystem.IsWindows()) return Result("unsupported", false, "PlatformUnsupported");
        using var identity = WindowsIdentity.GetCurrent();
        bool elevated = new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        try { var status = StorageAuthorityScm.Observe(); return Result(status.State == 4 ? "running" : "stopped", elevated, status.State == 4 ? null : "StorageServiceUnavailable"); }
        catch (WorkspaceException ex) { return Result("unavailable", elevated, ex.Code); }
        static StorageServiceStatus Result(string state, bool elevated, string? code) => new(StorageAuthorityProtocol.ServiceName,
            state, 1, StorageAuthorityProtocol.InstallPath, StorageAuthorityProtocol.StatePath, elevated, code);
    }
}

internal sealed class StorageAuthorityClient : IStorageAuthorityTransport
{
    internal static async Task PublishReceiptsAsync(StorageAuthorityResponse response,
        Func<string, JsonElement, CancellationToken, Task> publish, CancellationToken ct)
    {
        // snapshot.json is the client's commit marker. A partial successful
        // reply must still resume Capture/Release with its original request,
        // rather than advance to Observe while the authority retains that intent.
        foreach (string name in StorageAuthorityProtocol.ReceiptNames.Where(name => name != "snapshot.json"))
            if (response.Receipts.TryGetValue(name, out var receipt)) await publish(name, receipt, ct);
        if (response.ErrorCode is null or "SnapshotReleaseIsolationRequired" && response.Receipts.TryGetValue("snapshot.json", out var snapshot))
            await publish("snapshot.json", snapshot, ct);
    }

    public async Task<StorageAuthorityResponse> SendAsync(StorageAuthorityRequest request, CancellationToken ct)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
        StorageAuthorityProtocol.Validate(request);
        var status = StorageAuthorityScm.Observe();
        if (status.State != 4) throw StorageAuthorityProtocol.Error("StorageServiceUnavailable", "Start or repair the installed storage authority; no automatic elevation is attempted.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromMinutes(3));
        using var pipe = new NamedPipeClientStream(".", StorageAuthorityProtocol.PipeName, PipeAccessRights.ReadWrite | PipeAccessRights.Synchronize,
            PipeOptions.Asynchronous, TokenImpersonationLevel.Impersonation, HandleInheritability.None);
        try
        {
            await pipe.ConnectAsync(10000, timeout.Token);
            if (!GetNamedPipeServerProcessId(pipe.SafePipeHandle, out uint pid)) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            StorageAuthorityScm.RequirePeer(pid);
            await StorageAuthorityServer.WriteFrameAsync(pipe, JsonSerializer.SerializeToUtf8Bytes(request, StorageAuthorityProtocol.Json), timeout.Token);
            var response = StorageAuthorityProtocol.Decode<StorageAuthorityResponse>(await StorageAuthorityServer.ReadFrameAsync(pipe, StorageAuthorityProtocol.MaxResponseBytes, timeout.Token));
            if (response.Version != 1) throw StorageAuthorityProtocol.Error("StorageServiceVersionMismatch", "Install a service matching this client protocol.");
            if (response.RequestId != request.RequestId || response.Operation != request.Operation ||
                response.Receipts.Keys.Any(name => !StorageAuthorityProtocol.ReceiptNames.Contains(name, StringComparer.Ordinal)))
                throw StorageAuthorityProtocol.Error("StorageServiceIdentityMismatch", "The authority reply does not match the exact request.");
            return response;
        }
        catch (Exception ex) when (ex is IOException or TimeoutException || ex is OperationCanceledException && !ct.IsCancellationRequested)
        { throw StorageAuthorityProtocol.Error("StorageOperationUncertain", "Connection or response was interrupted. Preserve the request and retry the same workspace operation."); }
    }
    [DllImport("kernel32", SetLastError = true)] private static extern bool GetNamedPipeServerProcessId(SafePipeHandle pipe, out uint processId);
}

public sealed partial class VssSnapshotProvider
{
    private async Task RunAuthorityAsync(string action, string source, CancellationToken ct)
    {
        string directory = Path.GetFullPath(receiptDirectory);
        var control = Directory.GetParent(directory);
        var records = control?.Parent;
        string? home = records?.Parent?.FullName;
        if (control?.Name != ownerId || records?.Name != "records" || Path.GetFileName(directory) != "snapshot" || home is null)
            throw StorageAuthorityProtocol.Error("StorageScopeDenied", "Normal authority requests require home/records/owner/snapshot; use the explicit diagnostic helper for development-only layouts.");
        if (action == "Release") await PrepareVerificationAsync((await Read(ct))?.Snapshot.Id, ct);
        var verification = await ReadVerificationAsync<VssVerificationRequest>("verification-request.json", ct)
            ?? throw StorageAuthorityProtocol.Error("StorageRequestInvalid", "Missing durable observation intent.");
        var release = action == "Release" ? await WindowsProcessWorkspaceProvider.ReadAsync<StorageReleaseRequest>(Path.Combine(directory, "release-request.json"), ct) : null;
        if (release is not null)
        {
            var discard = await WindowsProcessWorkspaceProvider.ReadAsync<WorkspaceDiscardState>(Path.Combine(directory, "discard-authorized.json"), ct);
            if (discard is not null) release = release with { Discard = discard };
        }
        string? snapshotId = action == "Capture" ? null : (await Read(ct))?.Snapshot.Id;
        var request = new StorageAuthorityRequest(1, release?.RequestId ?? verification.RequestId, action, ownerId,
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(source)), home, snapshotId, verification, release);
        var response = await new StorageAuthorityClient().SendAsync(request, ct);
        // Only the unelevated client writes its own receipt tree. A SYSTEM
        // process never opens an attacker-controlled destination for writing.
        await StorageAuthorityClient.PublishReceiptsAsync(response,
            (name, receipt, token) => WindowsProcessWorkspaceProvider.WriteAsync(Path.Combine(directory, name), receipt, token), ct);
        if (response.ErrorCode is { } code)
            throw new WorkspaceException(code, "The storage authority retained the operation evidence. Retry only this owned workspace after resolving the reported condition.",
                new { action, ownerId, requestId = request.RequestId });
    }
}
