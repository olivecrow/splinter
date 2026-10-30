using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Splinter.Core;

namespace Splinter.SourceControl.Git;

// Pins existing Git administration against replacement while publishing only
// working-tree files. No index refresh, staging, commit, ref update or merge.
public sealed class GitOriginalPublicationGuard : IOriginalSourceLease
{
    private readonly List<FileStream> handles = [];
    public string Identity { get; }
    public GitOriginalPublicationGuard(string project, CancellationToken ct)
    {
        try
        {
            string git = Run(project, ["rev-parse", "--absolute-git-dir"], ct);
            string common = Run(project, ["rev-parse", "--path-format=absolute", "--git-common-dir"], ct);
            string head = Run(project, ["rev-parse", "HEAD"], ct);
            string branch = Run(project, ["symbolic-ref", "-q", "HEAD"], ct, allowDetached: true);
            var proofs = new SortedDictionary<string, string>(StringComparer.Ordinal);
            var paths = new List<string> { Path.Combine(git, "HEAD"), Path.Combine(git, "index"), Path.Combine(common, "packed-refs") };
            if (branch.Length > 0) paths.Add(Path.Combine(common, branch.Replace('/', Path.DirectorySeparatorChar)));
            if (File.Exists(Path.Combine(project, ".git"))) paths.Add(Path.Combine(project, ".git"));
            foreach (string path in paths.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (Path.Exists(path + ".lock"))
                    throw new WorkspaceException("GitOriginalBusy", "Preserve the in-progress Git administration request before publishing files.");
                for (string? ancestor = path; ancestor is not null; ancestor = Path.GetDirectoryName(ancestor))
                    if (Path.Exists(ancestor) && (File.GetAttributes(ancestor) & FileAttributes.ReparsePoint) != 0)
                        throw new WorkspaceException("GitOriginalIdentityChanged", "Preserve reparse Git administration.");
                if (!File.Exists(path)) { proofs.Add(path, "absent"); continue; }
                var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read); handles.Add(input);
                proofs.Add(path, Convert.ToHexStringLower(SHA256.HashData(input)));
            }
            if (Run(project, ["rev-parse", "HEAD"], ct) != head || Run(project, ["symbolic-ref", "-q", "HEAD"], ct, true) != branch)
                throw new WorkspaceException("GitOriginalIdentityChanged", "Git administration changed during original admission.");
            Identity = "sha256:" + Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { git, common, head, branch, proofs })));
        }
        catch { Dispose(); throw; }
    }
    public void Dispose() { foreach (var input in handles) input.Dispose(); handles.Clear(); }
    private static string Run(string project, string[] arguments, CancellationToken ct, bool allowDetached = false)
    {
        ct.ThrowIfCancellationRequested();
        var start = new ProcessStartInfo("git") { WorkingDirectory = project, UseShellExecute = false,
            CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, StandardOutputEncoding = Encoding.UTF8 };
        foreach (string key in start.Environment.Keys.Where(k => k.StartsWith("GIT_", StringComparison.OrdinalIgnoreCase)).ToArray()) start.Environment.Remove(key);
        start.Environment["GIT_OPTIONAL_LOCKS"] = "0";
        foreach (string argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new WorkspaceException("GitOriginalUnavailable", "Cannot observe original Git administration.");
        var output = process.StandardOutput.ReadToEndAsync(ct); var error = process.StandardError.ReadToEndAsync(ct);
        process.WaitForExitAsync(ct).GetAwaiter().GetResult(); Task.WhenAll(output, error).GetAwaiter().GetResult();
        if (process.ExitCode != 0 && !(allowDetached && process.ExitCode == 1))
            throw new WorkspaceException("GitOriginalUnavailable", "Cannot establish exact original Git identity.");
        return output.Result.Trim();
    }
}
