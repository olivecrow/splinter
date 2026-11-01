using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

// Experiment control only: stop one already owned, Editor-free provider via
// its existing authenticated protocol. No task/basis/identity is recreated.
internal static class ScalabilityProviderStop
{
    public static async Task<int> Main(string[] args)
    {
        if (args.Length is not (6 or 7)) throw new ArgumentException("Expected runtime, request, owner ID, PID, start ticks, evidence path, optional restart.");
        bool restart = args.Length == 7 && args[6] == "restart";
        string runtime = Path.GetFullPath(args[0]), requestPath = Path.GetFullPath(args[1]);
        string owner = args[2], evidence = Path.GetFullPath(args[5]); int pid = int.Parse(args[3]); long ticks = long.Parse(args[4]);
        AssemblyLoadContext.Default.Resolving += (context, name) =>
        {
            string file = Path.Combine(runtime, name.Name + ".dll"); return File.Exists(file) ? context.LoadFromAssemblyPath(file) : null;
        };
        var core = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(runtime, "Splinter.Core.dll"));
        var platform = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(runtime, "Splinter.Platform.Windows.dll"));
        Type requestType = platform.GetType("Splinter.Platform.Windows.OverlayServeRequest", throwOnError: true)!;
        object request = JsonSerializer.Deserialize(File.ReadAllText(requestPath), requestType, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        object identity = requestType.GetProperty("Identity")!.GetValue(request)!;
        if ((string)identity.GetType().GetProperty("Id")!.GetValue(identity)! != owner) throw new InvalidOperationException("Wrong exact owned view.");
        string control = (string)requestType.GetProperty("ControlPath")!.GetValue(request)!;
        using var state = JsonDocument.Parse(File.ReadAllText(Path.Combine(control, "state.json")));
        if (state.RootElement.GetProperty("processId").GetInt32() != pid || state.RootElement.GetProperty("processStartTimeUtcTicks").GetInt64() != ticks)
            throw new InvalidOperationException("Persisted provider lease changed.");
        bool oldAlive;
        try { using var process = Process.GetProcessById(pid); oldAlive = process.StartTime.ToUniversalTime().Ticks == ticks; }
        catch (ArgumentException) { oldAlive = false; }
        if (restart ? oldAlive : !oldAlive) throw new InvalidOperationException("Observe the exact preceding provider lifetime before this transition.");
        Type client = platform.GetType("Splinter.Platform.Windows.WinFspOverlayClient", throwOnError: true)!;
        MethodInfo method = client.GetMethod(restart ? "StartAsync" : "StopAsync", BindingFlags.Public | BindingFlags.Static)!;
        using var deadline = new CancellationTokenSource(restart ? TimeSpan.FromMinutes(3) : TimeSpan.FromSeconds(45));
        object[] parameters = restart ? [requestPath, Path.Combine(Path.GetDirectoryName(control)!, "scal-provider-runners"), deadline.Token, null!]
            : [request, deadline.Token];
        JsonElement result = await (Task<JsonElement>)method.Invoke(null, parameters)!;
        bool alive = result.GetProperty("processAlive").GetBoolean();
        int? currentPid = null; long? currentTicks = null;
        if (result.TryGetProperty("state", out var current))
        {
            if (current.TryGetProperty("processId", out var id)) currentPid = id.GetInt32();
            if (current.TryGetProperty("processStartTimeUtcTicks", out var started)) currentTicks = started.GetInt64();
        }
        using var output = new FileStream(evidence, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        JsonSerializer.Serialize(output, new { owner, pid, startTicks = ticks, processAlive = alive,
            mountExists = Directory.Exists((string)requestType.GetProperty("MountPath")!.GetValue(request)!),
            currentPid, currentStartTicks = currentTicks,
            transition = restart ? "restart-same-request" : "stop-through-owned-protocol", observedAt = DateTimeOffset.UtcNow }); output.Flush(true);
        return restart ? alive ? 0 : 2 : alive ? 2 : 0;
    }
}
