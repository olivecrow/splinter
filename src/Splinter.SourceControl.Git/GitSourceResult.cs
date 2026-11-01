using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Splinter.Core;
using Splinter.Runtime;

namespace Splinter.SourceControl.Git;

// Builds independent objects directly from the frozen logical source. No checkout,
// mutable index, hydration directory, filters or worker branch is used.
public static partial class GitSourceResult
{
    public static async Task<(string Reference, string Hash)> PreserveAsync(string project, string baseRevision,
        string operationId, ISnapshotTree tree, IEnumerable<string> changedPaths, string[] managedPaths, CancellationToken ct)
    {
        var policy = WorkspacePathPolicy.For(tree.Snapshot);
        string common = (await Run(project, ["rev-parse", "--path-format=absolute", "--git-common-dir"], null, ct)).Trim();
        using var baselineDirectory = new TemporaryMetadataDirectory();
        IEnumerable<DirectoryMetadataRecord> Baseline()
        {
            foreach (string row in GitObjectSnapshotTree.EnumerateListing(common, baseRevision))
            {
                ct.ThrowIfCancellationRequested();
                int tab = row.IndexOf('\t'); if (tab < 0) throw new WorkspaceException("GitBasisObjectInvalid", "The fixed result baseline listing is invalid.");
                string[] fields = row[..tab].Split(' ', StringSplitOptions.RemoveEmptyEntries); string path = row[(tab + 1)..];
                if (fields.Length != 4) throw new WorkspaceException("GitBasisObjectInvalid", "The fixed result baseline listing is invalid.");
                bool directory = fields[0] == "040000" && fields[1] == "tree" && fields[3] == "-";
                long length = 0;
                if (!directory && (fields[0] is not ("100644" or "100755") || fields[1] != "blob" ||
                    !long.TryParse(fields[3], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out length) || length < 0))
                    throw new WorkspaceException("SnapshotInputUnsupported", "Independent source results require ordinary source files.");
                yield return new(new(path, directory, length, (uint)(directory ? FileAttributes.Directory : FileAttributes.Archive), 0, 0, 0), null,
                    SourceReference: fields[0] + "|" + fields[2]);
            }
        }
        var baselineRoot = PagedSnapshotCatalog.Build(baselineDirectory.Path, new("", true, 0, (uint)FileAttributes.Directory, 0, 0, 0), Baseline(), policy.CaseSensitive, ct);
        using var baseline = new PagedSnapshotCatalog(baselineDirectory.Path, baselineRoot);
        using var changesDirectory = new TemporaryMetadataDirectory();
        var changeRecords = SnapshotCatalogInput.WithPathMarkers(changedPaths.Select(path => new MetadataEntry(
            policy.IdentityPath(policy.Normalize(path)), ReadOnlyMemory<byte>.Empty)), changesDirectory.Path, policy.CaseSensitive, ct);
        var changesRoot = PagedSnapshotCatalog.Build(changesDirectory.Path, new("", true, 0, 16, 0, 0, 0), changeRecords, policy.CaseSensitive, ct);
        using var changes = new PagedSnapshotCatalog(changesDirectory.Path, changesRoot);
        bool Changed(string path)
        {
            path = policy.IdentityPath(path);
            while (true)
            {
                if (changes.Find(path)?.SourceReference is not null) return true;
                int slash = path.LastIndexOf('/'); if (slash < 0) return false;
                path = path[..slash];
            }
        }
        async Task<string> Build(string directory)
        {
            using var records = new FileStream(Path.Combine(Path.GetTempPath(), "splinter-git-tree-" + Guid.NewGuid().ToString("N") + ".bin"),
                FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 65536, FileOptions.DeleteOnClose | FileOptions.SequentialScan);
            foreach (var entry in tree.EnumerateChildren(directory))
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
                    var original = baseline.Find(entry.Path);
                    bool retained = original is { Entry.Directory: false, SourceReference: not null } && original.Entry.Path == entry.Path;
                    string? originalReference = retained ? original!.SourceReference : null;
                    string? originalMode = originalReference is null ? null : originalReference[..originalReference.IndexOf('|')];
                    string? originalHash = originalReference is null ? null : originalReference[(originalReference.IndexOf('|') + 1)..];
                    mode = entry.UnixMode switch
                    {
                        0x1A4 => "100644",
                        0x1ED => "100755",
                        null => retained ? originalMode! : "100644",
                        _ => throw new WorkspaceException("GitPermissionsUnsupported", "Git results can preserve regular 0644/0755 modes only; retain unsupported permission metadata in the candidate.")
                    };
                    hash = retained && !Changed(entry.Path) ? originalHash! : (await Run(project, ["hash-object", "-w", "--stdin"], async (output, token) =>
                    {
                        byte[] buffer = new byte[1024 * 1024]; long offset = 0;
                        while (offset < entry.Length)
                        {
                            token.ThrowIfCancellationRequested();
                            int read = tree.Read(entry.Path, offset, buffer.AsSpan(0, (int)Math.Min(buffer.Length, entry.Length - offset)));
                            if (read <= 0) throw new WorkspaceException("SnapshotReadFailed", "Frozen source ended before its declared length.");
                            await output.WriteAsync(buffer.AsMemory(0, read), token); offset += read;
                        }
                    }, ct)).Trim();
                }
                await records.WriteAsync(Encoding.UTF8.GetBytes($"{mode} {(entry.Directory ? "tree" : "blob")} {hash}\t{name}\0"), ct);
            }
            records.Position = 0;
            return (await Run(project, ["mktree", "-z"], (stream, token) => records.CopyToAsync(stream, token), ct)).Trim();
        }
        string root = await Build("");
        byte[] message = Encoding.UTF8.GetBytes($"Splinter source result {operationId}\n");
        string revision = (await Run(project, ["commit-tree", root, "-p", baseRevision],
            (stream, token) => stream.WriteAsync(message, token).AsTask(), ct, deterministicCommit: true)).Trim();
        string reference = "refs/splinter/validation-results/" + operationId;
        tree.VerifyReadBoundary();
        string existing = await Run(project, ["for-each-ref", "--count=1", "--format=%(objectname)", reference], null, ct);
        if (existing.Trim().Length != 0 && existing.Trim() != revision)
            throw new WorkspaceException("ViewResultChanged", "Keep the existing independent result; its identity differs.");
        if (existing.Trim().Length == 0)
            await Run(project, ["update-ref", reference, revision, new string('0', revision.Length)], null, ct);
        return (reference, await VerifyAsync(project, reference, ct));
    }

    public static async Task<string> VerifyAsync(string project, string reference, CancellationToken ct)
    {
        if (!reference.StartsWith("refs/splinter/validation-results/", StringComparison.Ordinal) ||
            reference["refs/splinter/validation-results/".Length..].Any(c => !char.IsAsciiHexDigit(c)))
            throw new WorkspaceException("ViewResultInvalid", "A retained source result reference is required.");
        string commit = await Run(project, ["cat-file", "commit", reference], null, ct);
        using (var process = Process.Start(StartInfo(project, ["rev-list", "--objects", "--missing=print", reference], false, false))
            ?? throw new WorkspaceException("GitUnavailable", "Git did not start."))
        {
            var error = process.StandardError.ReadToEndAsync(ct);
            try
            {
                for (string? line; (line = await process.StandardOutput.ReadLineAsync(ct)) is not null;)
                    if (line.StartsWith('?')) throw new WorkspaceException("ViewResultInvalid", "Independent result objects are missing.");
                await process.WaitForExitAsync(ct);
                if (process.ExitCode != 0) throw new WorkspaceException("GitCommandFailed", (await error).Trim());
            }
            finally { if (!process.HasExited) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(CancellationToken.None); } }
        }
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(commit)));
    }

    private static async Task<string> Run(string project, string[] args, Func<Stream, CancellationToken, Task>? input,
        CancellationToken ct, bool deterministicCommit = false)
    {
        var start = StartInfo(project, args, input is not null, deterministicCommit);
        using var process = Process.Start(start) ?? throw new WorkspaceException("GitUnavailable", "Git did not start.");
        var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
        using var lifetime = new GitCommandLifetime(args, ct);
        await lifetime.WaitAsync(process, input is null ? null : SendInput, stdout, stderr);
        string output = await stdout, error = await stderr;
        if (process.ExitCode != 0) throw new WorkspaceException("GitCommandFailed", error.Trim());
        return output;

        async Task SendInput(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            await input!(process.StandardInput.BaseStream, token); process.StandardInput.Close();
        }
    }
    private static ProcessStartInfo StartInfo(string project, string[] args, bool input, bool deterministicCommit)
    {
        var start = new ProcessStartInfo("git") { WorkingDirectory = project, UseShellExecute = false,
            CreateNoWindow = true, RedirectStandardInput = input, RedirectStandardOutput = true, RedirectStandardError = true,
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
        return start;
    }
}
