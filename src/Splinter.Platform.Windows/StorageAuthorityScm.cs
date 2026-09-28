using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Splinter.Platform.Windows;

// Native SCM plumbing keeps the privileged adapter free of a service-host
// framework, command shell, dependency injection configuration or plugin loader.
internal static class StorageAuthorityScm
{
    private static readonly ServiceMain MainCallback = ServiceEntryPoint;
    private static readonly Handler HandlerCallback = Control;
    private static readonly CancellationTokenSource Stop = new();
    private static nint statusHandle;
    private static uint checkpoint;
    internal static int Run()
    {
        ServiceEntry[] table = [new() { Name = StorageAuthorityProtocol.ServiceName, Main = MainCallback }, new()];
        if (!StartServiceCtrlDispatcherW(table)) throw new Win32Exception(Marshal.GetLastWin32Error());
        return 0;
    }
    private static void ServiceEntryPoint(uint count, nint arguments)
    {
        statusHandle = RegisterServiceCtrlHandlerExW(StorageAuthorityProtocol.ServiceName, HandlerCallback, 0);
        if (statusHandle == 0) return;
        Report(2);
        uint error = 0;
        try { StorageAuthorityServer.RunAsync(() => Report(4), Stop.Token).GetAwaiter().GetResult(); }
        catch (OperationCanceledException) when (Stop.IsCancellationRequested) { }
        catch (Exception ex)
        {
            error = 1066;
            try { StorageAuthorityProcessor.Write(Path.Combine(StorageAuthorityProtocol.StatePath, "last-error.json"), new { code = ex.GetType().Name, message = ex.Message, at = DateTimeOffset.UtcNow }); }
            catch (Exception) { /* A broken protected ledger cannot be repaired by deleting it. */ }
        }
        Report(1, error);
    }
    private static uint Control(uint control, uint eventType, nint eventData, nint context)
    {
        if (control is 1 or 5) { Report(3); Stop.Cancel(); }
        return 0;
    }
    private static void Report(uint state, uint error = 0)
    {
        var status = new ServiceStatus { Type = 0x10, State = state, Accepted = state == 4 ? 5u : 0,
            Error = error, ServiceError = error == 0 ? 0u : 1u, Checkpoint = state is 2 or 3 ? ++checkpoint : 0, WaitHint = 20000 };
        SetServiceStatus(statusHandle, ref status);
    }
    internal static (uint State, uint ProcessId) Observe()
    {
        using var manager = OpenSCManagerW(null, null, 1);
        if (manager.IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error());
        using var service = OpenServiceW(manager, StorageAuthorityProtocol.ServiceName, 4 | 1); // QUERY_STATUS | QUERY_CONFIG
        if (service.IsInvalid)
        {
            int error = Marshal.GetLastWin32Error();
            if (error == 1060) throw StorageAuthorityProtocol.Error("StorageServiceMissing", "Install the storage authority once before normal NTFS workspace operations.");
            throw new Win32Exception(error);
        }
        if (!QueryServiceStatusEx(service, 0, out var status, Marshal.SizeOf<ServiceProcessStatus>(), out _))
            throw new Win32Exception(Marshal.GetLastWin32Error());
        _ = QueryServiceConfigW(service, 0, 0, out uint needed);
        if (Marshal.GetLastWin32Error() != 122 || needed == 0 || needed > 65536)
            throw new Win32Exception(Marshal.GetLastWin32Error());
        nint buffer = Marshal.AllocHGlobal(checked((int)needed));
        try
        {
            if (!QueryServiceConfigW(service, buffer, needed, out _)) throw new Win32Exception(Marshal.GetLastWin32Error());
            var config = Marshal.PtrToStructure<ServiceConfiguration>(buffer);
            RequireConfiguration(config.Type, Marshal.PtrToStringUni(config.BinaryPath), Marshal.PtrToStringUni(config.Account));
        }
        finally { Marshal.FreeHGlobal(buffer); }
        return (status.Status.State, status.ProcessId);
    }
    internal static void RequireConfiguration(uint type, string? image, string? account)
    {
        string expected = "\"" + Path.Combine(StorageAuthorityProtocol.InstallPath, "Splinter.Platform.Windows.exe") + "\" storage-service";
        if (type != 0x10 || account != "LocalSystem" || !StorageAuthorityProcessor.Same(image ?? "", expected))
            throw StorageAuthorityProtocol.Error("StorageServiceIdentityMismatch", "SCM must run the exact protected service image as LocalSystem.");
    }
    internal static void RequirePeer(uint pid)
    {
        var observed = Observe();
        if (observed.State != 4 || observed.ProcessId == 0 || observed.ProcessId != pid)
            throw StorageAuthorityProtocol.Error("StorageServiceIdentityMismatch", "The pipe peer is not the running SCM-owned authority.");
    }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct ServiceEntry { [MarshalAs(UnmanagedType.LPWStr)] public string? Name; public ServiceMain? Main; }
    [StructLayout(LayoutKind.Sequential)] private struct ServiceStatus { public uint Type, State, Accepted, Error, ServiceError, Checkpoint, WaitHint; }
    [StructLayout(LayoutKind.Sequential)] private struct ServiceProcessStatus { public ServiceStatus Status; public uint ProcessId, Flags; }
    [StructLayout(LayoutKind.Sequential)] private struct ServiceConfiguration
    { public uint Type, StartType, ErrorControl; public nint BinaryPath, LoadOrderGroup; public uint TagId; public nint Dependencies, Account, DisplayName; }
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void ServiceMain(uint count, nint arguments);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate uint Handler(uint control, uint eventType, nint eventData, nint context);
    private sealed class ServiceHandle : SafeHandleZeroOrMinusOneIsInvalid
    { public ServiceHandle() : base(true) { } protected override bool ReleaseHandle() => CloseServiceHandle(handle); }
    [DllImport("advapi32", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool StartServiceCtrlDispatcherW([In] ServiceEntry[] table);
    [DllImport("advapi32", CharSet = CharSet.Unicode, SetLastError = true)] private static extern nint RegisterServiceCtrlHandlerExW(string name, Handler handler, nint context);
    [DllImport("advapi32", SetLastError = true)] private static extern bool SetServiceStatus(nint handle, ref ServiceStatus status);
    [DllImport("advapi32", CharSet = CharSet.Unicode, SetLastError = true)] private static extern ServiceHandle OpenSCManagerW(string? machine, string? database, uint access);
    [DllImport("advapi32", CharSet = CharSet.Unicode, SetLastError = true)] private static extern ServiceHandle OpenServiceW(ServiceHandle manager, string name, uint access);
    [DllImport("advapi32", SetLastError = true)] private static extern bool QueryServiceStatusEx(ServiceHandle service, int level, out ServiceProcessStatus status, int size, out int needed);
    [DllImport("advapi32", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool QueryServiceConfigW(ServiceHandle service, nint config, uint size, out uint needed);
    [DllImport("advapi32")] private static extern bool CloseServiceHandle(nint handle);
}
