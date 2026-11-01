using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using System.Threading.Tasks;

// Build once as a net10.0 console helper, outside measured product outputs.
// Both ABBA candidates use the same wrapper and runtime. It invokes the exact
// selected CLI assembly; it never substitutes mocked storage or providers.
internal static class ScalabilityCommandProbe
{
    public static async Task<int> Main(string[] args)
    {
        if (args.Length < 3) throw new ArgumentException("Expected CLI assembly, metrics file, then CLI arguments.");
        string assemblyPath = Path.GetFullPath(args[0]), metricsPath = Path.GetFullPath(args[1]);
        string directory = Path.GetDirectoryName(assemblyPath)!;
        AssemblyLoadContext.Default.Resolving += (context, name) =>
        {
            string path = Path.Combine(directory, name.Name + ".dll");
            return File.Exists(path) ? context.LoadFromAssemblyPath(path) : null;
        };
        Assembly cli = AssemblyLoadContext.Default.LoadFromAssemblyPath(assemblyPath);
        MethodInfo main = cli.GetType("Splinter.Cli.Program", throwOnError: true)!.GetMethod("Main", BindingFlags.Public | BindingFlags.Static)!;
        long start = Stopwatch.GetTimestamp(); int exitCode = -1;
        try { exitCode = await (Task<int>)main.Invoke(null, [args[2..]])!; return exitCode; }
        finally
        {
            Assembly? runtime = null;
            foreach (var loaded in AssemblyLoadContext.Default.Assemblies) if (loaded.GetName().Name == "Splinter.Runtime") runtime = loaded;
            Type? store = runtime?.GetType("Splinter.Runtime.PagedMetadataStore");
            long? Read(string name) => store?.GetProperty(name, BindingFlags.Public | BindingFlags.Static)?.GetValue(null) as long?;
            using var process = Process.GetCurrentProcess(); process.Refresh();
            var metric = new
            {
                version = 1, observedAt = DateTimeOffset.UtcNow, cliAssembly = assemblyPath, processId = process.Id,
                startTimeUtcTicks = process.StartTime.ToUniversalTime().Ticks, exitCode,
                secondsInsideEntryPoint = Stopwatch.GetElapsedTime(start).TotalSeconds,
                metadataCacheBytes = Read("CachedMetadataBytes"), peakMetadataCacheBytes = Read("PeakCachedMetadataBytes"),
                privateBytesAtExit = process.PrivateMemorySize64, workingSetBytesAtExit = process.WorkingSet64,
                handlesAtExit = process.HandleCount, allocatedManagedBytes = GC.GetTotalAllocatedBytes(),
                scope = "Same reflection wrapper for both candidates. Cache fields are accounted page-cache charge, not total resident metadata or private memory; null means the candidate has no gauge."
            };
            using var output = new FileStream(metricsPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
            JsonSerializer.Serialize(output, metric); output.Flush(true);
        }
    }
}
