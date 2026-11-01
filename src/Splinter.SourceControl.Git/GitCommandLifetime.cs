using System.Diagnostics;
using Splinter.Core;

namespace Splinter.SourceControl.Git;

// A data operation may legitimately remain silent while hashing or refreshing
// a large index. Neither elapsed time nor missing stdout proves it is stuck.
// Only fixed control queries have a deadline; all operations remain cancellable
// and cancellation drains their own process and pipe/producer tasks.
internal sealed class GitCommandLifetime : IDisposable
{
    private readonly CancellationToken caller;
    private readonly CancellationTokenSource cancellation;
    internal CancellationToken Token => cancellation.Token;

    internal GitCommandLifetime(string[] arguments, CancellationToken ct, TimeSpan? controlDeadline = null)
    {
        caller = ct; cancellation = CancellationTokenSource.CreateLinkedTokenSource(ct);
        if (IsControl(arguments)) cancellation.CancelAfter(controlDeadline ?? TimeSpan.FromMinutes(10));
    }

    private static bool IsControl(string[] arguments)
    {
        int i = 0;
        while (i + 1 < arguments.Length && arguments[i] == "-c") i += 2;
        if (i >= arguments.Length) return false;
        return arguments[i] switch
        {
            "rev-parse" or "symbolic-ref" or "config" or "update-ref" or "version" or "--version" => true,
            "branch" => arguments.Length == i + 2 && arguments[i + 1] == "--show-current",
            "cat-file" => arguments.Length == i + 3 && arguments[i + 1] == "-e",
            "for-each-ref" => arguments.Skip(i + 1).Contains("--count=1", StringComparer.Ordinal),
            _ => false
        };
    }

    internal async Task WaitAsync(Process process, Func<CancellationToken, Task>? send, Task stdout, Task stderr)
    {
        Task input = Task.CompletedTask;
        try
        {
            input = send?.Invoke(Token) ?? Task.CompletedTask;
            Task exit = process.WaitForExitAsync(Token);
            Task first = await Task.WhenAny(exit, input);
            // A failed producer must not leave Git waiting forever for stdin.
            if (first.IsFaulted || first.IsCanceled) await first;
            await Task.WhenAll(exit, input);
        }
        catch (Exception failure)
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
            catch (InvalidOperationException) when (process.HasExited) { }
            await process.WaitForExitAsync(CancellationToken.None);
            // Killing the owned child closes its pipe ends. Drain every task
            // before returning, retaining the original failure as authority.
            try { await Task.WhenAll(input, stdout, stderr); } catch { }
            if (failure is OperationCanceledException && !caller.IsCancellationRequested && Token.IsCancellationRequested)
                throw new WorkspaceException("GitCommandTimeout", "A bounded Git control query timed out; inspect the persisted operation before retrying.");
            throw;
        }
    }
    public void Dispose() => cancellation.Dispose();
}
