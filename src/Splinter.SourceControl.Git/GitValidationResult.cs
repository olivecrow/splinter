using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Splinter.Core;

namespace Splinter.SourceControl.Git;

// Builds independent objects directly from the frozen logical source. No checkout,
// mutable index, hydration directory, filters or worker branch is used.
public static class GitValidationResult
{
    public static async Task<(string Reference, string Hash)> PreserveAsync(string project, string baseRevision,
        string operationId, ISnapshotTree tree, string[] changedPaths, string[] managedPaths, CancellationToken ct)
    {
        var policy = WorkspacePathPolicy.For(tree.Snapshot);
        string listing = await Run(project, ["ls-tree", "-r", "-z", baseRevision], null, ct);
        var baseline = new Dictionary<string, (string Mode, string Hash)>(StringComparer.Ordinal);
        foreach (string row in listing.Split('\0', StringSplitOptions.RemoveEmptyEntries))
        {
            int tab = row.IndexOf('\t'); string[] fields = row[..tab].Split(' ');
            if (fields[0] is not ("100644" or "100755"))
                throw new WorkspaceException("SnapshotInputUnsupported", "Independent validation results require ordinary source files.");
            baseline.Add(row[(tab + 1)..], (fields[0], fields[2]));
        }
        bool Changed(string path) => changedPaths.Any(p => path.Equals(p, policy.Comparison) || policy.IsChild(path, p));
        async Task<string> Build(string directory)
        {
            var records = new List<(string Name, string Row)>();
            foreach (var entry in tree.List(directory))
            {
                ct.ThrowIfCancellationRequested();
                if (policy.Managed(entry.Path, managedPaths)) continue;
                string name = entry.Path.Split('/')[^1];
                if (name is "." or ".." || name.Contains('\0') || name.Contains('\n'))
                    throw new WorkspaceException("SnapshotInputUnsupported", "Unsupported source entry name.");
                string hash, mode;
                if (entry.Directory)
                {
                    if (entry.UnixMode is not null)
                        throw new WorkspaceException("GitPermissionsUnsupported", "Git trees cannot preserve directory permission metadata; retain the candidate.");
                    hash = await Build(entry.Path); mode = "040000";
                }
                else
                {
                    bool retained = baseline.TryGetValue(entry.Path, out var original);
                    mode = entry.UnixMode switch
                    {
                        0x1A4 => "100644",
                        0x1ED => "100755",
                        null => retained ? original.Mode : "100644",
                        _ => throw new WorkspaceException("GitPermissionsUnsupported", "Git results can preserve regular 0644/0755 modes only; retain unsupported permission metadata in the candidate.")
                    };
                    hash = retained && !Changed(entry.Path) ? original.Hash : (await Run(project, ["hash-object", "-w", "--stdin"], async output =>
                    {
                        byte[] buffer = new byte[1024 * 1024]; long offset = 0;
                        while (offset < entry.Length)
                        {
                            int read = tree.Read(entry.Path, offset, buffer.AsSpan(0, (int)Math.Min(buffer.Length, entry.Length - offset)));
                            if (read <= 0) throw new WorkspaceException("SnapshotReadFailed", "Frozen source ended before its declared length.");
                            await output.WriteAsync(buffer.AsMemory(0, read), ct); offset += read;
                        }
                    }, ct)).Trim();
                }
                records.Add((name + (entry.Directory ? "/" : ""), $"{mode} {(entry.Directory ? "tree" : "blob")} {hash}\t{name}\0"));
            }
            byte[] input = Encoding.UTF8.GetBytes(string.Concat(records.OrderBy(r => r.Name, StringComparer.Ordinal).Select(r => r.Row)));
            return (await Run(project, ["mktree", "-z"], stream => stream.WriteAsync(input, ct).AsTask(), ct)).Trim();
        }
        string root = await Build("");
        byte[] message = Encoding.UTF8.GetBytes($"Splinter validation result {operationId}\n");
        string revision = (await Run(project, ["commit-tree", root, "-p", baseRevision],
            stream => stream.WriteAsync(message, ct).AsTask(), ct, deterministicCommit: true)).Trim();
        string reference = "refs/splinter/validation-results/" + operationId;
        string existing = await Run(project, ["for-each-ref", "--format=%(objectname)", reference], null, ct);
        if (existing.Trim().Length != 0 && existing.Trim() != revision)
            throw new WorkspaceException("ValidationResultChanged", "Keep the existing independent result; its identity differs.");
        if (existing.Trim().Length == 0)
            await Run(project, ["update-ref", reference, revision, new string('0', revision.Length)], null, ct);
        return (reference, await VerifyAsync(project, reference, ct));
    }

    public static async Task<string> VerifyAsync(string project, string reference, CancellationToken ct)
    {
        if (!reference.StartsWith("refs/splinter/validation-results/", StringComparison.Ordinal) ||
            reference["refs/splinter/validation-results/".Length..].Any(c => !char.IsAsciiHexDigit(c)))
            throw new WorkspaceException("ValidationResultInvalid", "A retained validation result reference is required.");
        string commit = await Run(project, ["cat-file", "commit", reference], null, ct);
        string objects = await Run(project, ["rev-list", "--objects", "--missing=print", reference], null, ct);
        if (objects.Split('\n').Any(line => line.StartsWith('?')))
            throw new WorkspaceException("ValidationResultInvalid", "Independent result objects are missing.");
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(commit)));
    }

    private static async Task<string> Run(string project, string[] args, Func<Stream, Task>? input,
        CancellationToken ct, bool deterministicCommit = false)
    {
        var start = new ProcessStartInfo("git") { WorkingDirectory = project, UseShellExecute = false,
            CreateNoWindow = true, RedirectStandardInput = input is not null, RedirectStandardOutput = true, RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8 };
        foreach (string key in start.Environment.Keys.Where(k => k.StartsWith("GIT_", StringComparison.OrdinalIgnoreCase)).ToArray()) start.Environment.Remove(key);
        start.Environment["GIT_TERMINAL_PROMPT"] = "0"; start.Environment["GIT_OPTIONAL_LOCKS"] = "0";
        if (deterministicCommit)
            foreach (string who in new[] { "AUTHOR", "COMMITTER" })
            {
                start.Environment[$"GIT_{who}_NAME"] = "Splinter";
                start.Environment[$"GIT_{who}_EMAIL"] = "splinter@localhost";
                start.Environment[$"GIT_{who}_DATE"] = "2000-01-01T00:00:00Z";
            }
        foreach (string arg in args) start.ArgumentList.Add(arg);
        using var process = Process.Start(start) ?? throw new WorkspaceException("GitUnavailable", "Git did not start.");
        var stdout = process.StandardOutput.ReadToEndAsync(ct); var stderr = process.StandardError.ReadToEndAsync(ct);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromMinutes(10));
        try
        {
            if (input is not null) { await input(process.StandardInput.BaseStream); process.StandardInput.Close(); }
            await process.WaitForExitAsync(timeout.Token);
            string output = await stdout, error = await stderr;
            if (process.ExitCode != 0) throw new WorkspaceException("GitCommandFailed", error.Trim());
            return output;
        }
        catch { if (!process.HasExited) process.Kill(entireProcessTree: true); await process.WaitForExitAsync(CancellationToken.None); throw; }
    }
}
