using System.ComponentModel;
using System.Diagnostics;
using Splinter.Core;

namespace Splinter.SourceControl.Git;

public sealed class GitRepositoryInspector : IRepositoryInspector
{
    public async Task<RepositoryInspection?> InspectAsync(string path, CancellationToken cancellationToken = default)
    {
        var root = await RunAsync(path, ["rev-parse", "--show-toplevel"], cancellationToken);
        if (root.ExitCode != 0)
        {
            if (root.Error.Contains("not a git repository", StringComparison.OrdinalIgnoreCase))
                return null;
            throw new InspectionException("GitInspectionFailed", root.Error.Trim());
        }

        string rootPath = Path.GetFullPath(root.Output.TrimEnd('\r', '\n'));
        string gitDirectory = await RequiredAsync(path, ["rev-parse", "--absolute-git-dir"], cancellationToken);
        string commonDirectory = await RequiredAsync(path,
            ["rev-parse", "--path-format=absolute", "--git-common-dir"], cancellationToken);
        var head = await RunAsync(path, ["rev-parse", "--verify", "HEAD"], cancellationToken);
        var branch = await RunAsync(path, ["symbolic-ref", "--quiet", "--short", "HEAD"], cancellationToken);
        if (head.ExitCode != 0 && branch.ExitCode != 0)
            throw new InspectionException("GitHeadUnavailable", "Git HEAD is neither a commit nor an unborn branch.");

        string worktrees = await RequiredAsync(path, ["worktree", "list", "--porcelain", "-z"], cancellationToken);
        int count = worktrees.Split('\0').Count(field => field.StartsWith("worktree ", StringComparison.Ordinal));
        var comparer = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return new("git", rootPath, Path.GetFullPath(commonDirectory),
            head.ExitCode == 0 ? head.Output.Trim() : null,
            new Dictionary<string, string?>
            {
                ["gitDirectory"] = Path.GetFullPath(gitDirectory),
                ["commonDirectory"] = Path.GetFullPath(commonDirectory),
                ["branch"] = branch.ExitCode == 0 ? branch.Output.Trim() : null,
                ["isLinkedWorktree"] = (!string.Equals(Path.GetFullPath(gitDirectory),
                    Path.GetFullPath(commonDirectory), comparer)).ToString().ToLowerInvariant(),
                ["worktreeCount"] = count.ToString(System.Globalization.CultureInfo.InvariantCulture)
            });
    }

    private static async Task<string> RequiredAsync(string path, string[] args, CancellationToken cancellationToken)
    {
        var result = await RunAsync(path, args, cancellationToken);
        if (result.ExitCode != 0)
            throw new InspectionException("GitInspectionFailed", result.Error.Trim());
        return result.Output.TrimEnd('\r', '\n');
    }

    private static async Task<GitResult> RunAsync(string path, string[] args, CancellationToken cancellationToken)
    {
        var start = new ProcessStartInfo("git")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = path
        };
        foreach (var name in start.Environment.Keys.Where(key => key.StartsWith("GIT_", StringComparison.OrdinalIgnoreCase)).ToArray())
            start.Environment.Remove(name);
        start.Environment["GIT_OPTIONAL_LOCKS"] = "0";
        start.Environment["GIT_TERMINAL_PROMPT"] = "0";
        start.Environment["LC_ALL"] = "C";
        start.ArgumentList.Add("-C");
        start.ArgumentList.Add(path);
        foreach (string arg in args)
            start.ArgumentList.Add(arg);

        using var process = new Process { StartInfo = start };
        try { process.Start(); }
        catch (Win32Exception ex) { throw new InspectionException("GitUnavailable", ex.Message); }
        Task<string> output = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> error = process.StandardError.ReadToEndAsync(cancellationToken);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(10));
        try { await process.WaitForExitAsync(deadline.Token); }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(CancellationToken.None);
            cancellationToken.ThrowIfCancellationRequested();
            throw new InspectionException("GitInspectionTimeout", "Git inspection exceeded 10 seconds.");
        }
        return new(process.ExitCode, await output, await error);
    }

    private sealed record GitResult(int ExitCode, string Output, string Error);
}
