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
    public GitOriginalPublicationGuard(string project, CancellationToken ct) : this(project, ct, null) { }
    public static async Task<GitOriginalPublicationGuard> AcquireWorkspaceAsync(WorkspaceRecord target, string control, CancellationToken ct)
    {
        var receipt = await GitWorkspaceProvider.Receipt(target, control, ct);
        return new(target.Path, ct, receipt);
    }
    private GitOriginalPublicationGuard(string project, CancellationToken ct, OverlayGitReceipt? peer)
    {
        try
        {
            if (peer is not null)
            {
                string pointer = Path.Combine(project, ".git");
                if ((File.GetAttributes(pointer) & (FileAttributes.ReparsePoint | FileAttributes.Directory)) != 0)
                    throw new WorkspaceException("GitOverlayPointerMismatch", "The destination Git pointer is no longer an ordinary file.");
                var file = new FileStream(pointer, FileMode.Open, FileAccess.Read, FileShare.Read);
                handles.Add(file);
                using var reader = new StreamReader(file, leaveOpen: true);
                if (reader.ReadToEnd() != peer.Pointer)
                    throw new WorkspaceException("GitOverlayPointerMismatch", "The destination no longer owns this Git pointer.");
            }
            string git = Run(project, ["rev-parse", "--absolute-git-dir"], ct);
            string common = Run(project, ["rev-parse", "--path-format=absolute", "--git-common-dir"], ct);
            string head = Run(project, ["rev-parse", "HEAD"], ct);
            string branch = Run(project, ["symbolic-ref", "-q", "HEAD"], ct, allowDetached: true);
            var proofs = new SortedDictionary<string, string>(StringComparer.Ordinal);
            var paths = new List<string> { Path.Combine(git, "HEAD"), Path.Combine(git, "index"), Path.Combine(common, "packed-refs") };
            if (peer is not null)
            {
                if (!Path.GetFullPath(git).Equals(Path.GetFullPath(peer.GitDirectory), StringComparison.OrdinalIgnoreCase) ||
                    !Path.GetFullPath(common).Equals(Path.GetFullPath(Run(peer.Project, ["rev-parse", "--path-format=absolute", "--git-common-dir"], ct)), StringComparison.OrdinalIgnoreCase) ||
                    branch != "refs/heads/" + peer.Branch || head != peer.Revision)
                    throw new WorkspaceException("GitOverlayOwnerMismatch", "The destination branch or registered Git directory changed.");
                paths.AddRange(new[] { "gitdir", "commondir", "locked" }.Select(name => Path.Combine(git, name)));
            }
            if (branch.Length > 0) paths.Add(Path.Combine(common, branch.Replace('/', Path.DirectorySeparatorChar)));
            if (File.Exists(Path.Combine(project, ".git"))) paths.Add(Path.Combine(project, ".git"));
            foreach (string path in paths.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (Path.Exists(path + ".lock"))
                    throw new WorkspaceException("GitOriginalBusy", "Preserve the in-progress Git administration request before publishing files.");
                for (string? ancestor = path; ancestor is not null; ancestor = Path.GetDirectoryName(ancestor))
                    if (!(peer is not null && ancestor.Equals(project, StringComparison.OrdinalIgnoreCase)) &&
                        Path.Exists(ancestor) && (File.GetAttributes(ancestor) & FileAttributes.ReparsePoint) != 0)
                        throw new WorkspaceException("GitOriginalIdentityChanged", "Preserve reparse Git administration.");
                if (!File.Exists(path)) { proofs.Add(path, "absent"); continue; }
                var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read); handles.Add(input);
                proofs.Add(path, Convert.ToHexStringLower(SHA256.HashData(input)));
            }
            if (peer is not null && (File.ReadAllText(Path.Combine(git, "locked")).TrimEnd('\r', '\n') != "Splinter overlay " + peer.OwnerId ||
                !Path.GetFullPath(File.ReadAllText(Path.Combine(git, "gitdir")).Trim()).Equals(Path.Combine(project, ".git"), StringComparison.OrdinalIgnoreCase) ||
                !Path.GetFullPath(Path.Combine(git, File.ReadAllText(Path.Combine(git, "commondir")).Trim())).Equals(Path.GetFullPath(common), StringComparison.OrdinalIgnoreCase)))
                throw new WorkspaceException("GitOverlayOwnerMismatch", "The destination registration changed during admission.");
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
