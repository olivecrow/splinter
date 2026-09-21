using System.Diagnostics;
using Splinter.Core;

namespace Splinter.Platform.Windows;

internal static class Program
{
    // Private helper protocol. A single supervisor exits when its Editor group
    // finishes; this is neither the product Host nor a machine-wide daemon.
    public static async Task<int> Main(string[] args)
    {
        if (!OperatingSystem.IsWindows() || args.Length != 2 || args[0] != "supervise") return 2;
        string request = Path.GetFullPath(args[1]); string control = Path.GetDirectoryName(request)!;
        ProcessLaunch? launch = null;
        try
        {
            launch = await WindowsProcessWorkspaceProvider.ReadAsync<ProcessLaunch>(request)
                ?? throw new WorkspaceException("InvalidLaunch", "Missing process request.");
            using var self = Process.GetCurrentProcess();
            var group = new ProcessGroupLease("Local\\Splinter." + Guid.NewGuid().ToString("N"), self.Id, self.StartTime.ToUniversalTime().Ticks, control);
            using var job = WindowsProcessGroup.Create(group.Name);
            using var suspended = job.Launch(launch);
            using var root = Process.GetProcessById(suspended.Id);
            var lease = new ApplicationLease(root.Id, root.StartTime.ToUniversalTime().Ticks, launch.Token, launch.Executable, group);
            await WindowsProcessWorkspaceProvider.WriteAsync(Path.Combine(control, "launch-receipt.json"), lease);
            suspended.Resume();
            await root.WaitForExitAsync();
            // Unity has completed its own graceful exit. Any surviving members
            // belong to this exact group; shared external/original processes do not.
            for (int attempt = 0; attempt < 20 && job.ActiveCount != 0; attempt++) await Task.Delay(100);
            if (job.ActiveCount != 0) job.TerminateRemainder();
            for (int attempt = 0; attempt < 100 && job.ActiveCount != 0; attempt++) await Task.Delay(100);
            if (job.ActiveCount != 0) throw new WorkspaceException("ProcessCleanupIncomplete", "Job members have not exited.");
            await WindowsProcessWorkspaceProvider.WriteAsync(Path.Combine(control, "process-exit.json"),
                new { launch.Token, observedAt = DateTimeOffset.UtcNow, rootExitCode = root.ExitCode,
                    activeJobProcesses = 0, workingSetBytes = 0, privateBytes = 0 });
            return 0;
        }
        catch (Exception ex)
        {
            await WindowsProcessWorkspaceProvider.WriteAsync(Path.Combine(control, "process-error.json"),
                new WindowsProcessWorkspaceProvider.LaunchError(launch?.Token ?? "", ex.Message));
            return 1;
        }
    }
}
