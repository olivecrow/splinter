using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

// Identical bounded experiment wrapper for both frozen candidates. Cancellation
// is delivered to the product; it never kills a writing CLI or provider.
internal static class ScalabilityTimedCommand
{
    public static async Task<int> Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--stop") return await ScalabilityProviderStop.Main(args[1..]);
        if (args.Length < 3) throw new ArgumentException("CLI assembly, metrics file, CLI arguments required.");
        string assembly = Path.GetFullPath(args[0]), directory = Path.GetDirectoryName(assembly)!;
        AssemblyLoadContext.Default.Resolving += (context, name) =>
        { string file = Path.Combine(directory, name.Name + ".dll"); return File.Exists(file) ? context.LoadFromAssemblyPath(file) : null; };
        var cli = AssemblyLoadContext.Default.LoadFromAssemblyPath(assembly);
        var run = cli.GetType("Splinter.Cli.Program", true)!.GetMethod("RunAsync", BindingFlags.Public | BindingFlags.Static)!;
        const int deadlineSeconds = 30;
        double remaining = deadlineSeconds;
        if (DateTimeOffset.TryParse(Environment.GetEnvironmentVariable("SPLINTER_VERIFY_DEADLINE_UTC"), out var trialDeadline))
            remaining = Math.Max(0.001, Math.Min(remaining, (trialDeadline - DateTimeOffset.UtcNow).TotalSeconds));
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(remaining));
        ConsoleCancelEventHandler cancel = (_, e) => { e.Cancel = true; cancellation.Cancel(); };
        Console.CancelKeyPress += cancel;
        long start = Stopwatch.GetTimestamp(); int exitCode = -1;
        try { exitCode = await (Task<int>)run.Invoke(null, [args[2..], Console.Out, cancellation.Token, null, false])!; return exitCode; }
        finally
        {
            Console.CancelKeyPress -= cancel;
            using var output = new FileStream(args[1], FileMode.CreateNew, FileAccess.Write, FileShare.Read);
            JsonSerializer.Serialize(output, new { version = 1, cliAssembly = assembly, command = args[2..], exitCode,
                secondsInsideEntryPoint = Stopwatch.GetElapsedTime(start).TotalSeconds, deadlineSeconds,
                cancellationRequested = cancellation.IsCancellationRequested, observedAt = DateTimeOffset.UtcNow });
            output.Flush(true);
        }
    }
}
