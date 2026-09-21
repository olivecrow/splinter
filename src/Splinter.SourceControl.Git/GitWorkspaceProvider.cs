using System.ComponentModel;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Splinter.Core;

namespace Splinter.SourceControl.Git;

public sealed class GitWorkspaceProvider(IWorkspaceStorageProvider? storage = null, IWorkspaceBaselineReader? baselines = null) : ISourceWorkspaceProvider
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public Task<string> ResolveRevisionAsync(string project, string reference, CancellationToken ct = default) =>
        Required(project, ["rev-parse", "--verify", "--end-of-options", reference + "^{commit}"], ct);
    public async Task<string> SnapshotKeyAsync(string project, string revision, string[] paths, CancellationToken ct = default) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(await Required(project, ["ls-tree", "-r", "-z", revision, "--", .. paths], ct))));
    public async Task<SourceWorkspace> CreateAsync(WorkspaceRecord workspace, CancellationToken ct = default)
    {
        CheckPaths(workspace);
        string root = await Required(workspace.ProjectPath, ["rev-parse", "--show-toplevel"], ct);
        if (!SamePath(root, workspace.ProjectPath)) throw Error("GitProjectRootMismatch", "The alpha requires the Unity project at its repository root.");
        var registered = await Worktrees(workspace.ProjectPath, ct);
        var found = registered.FirstOrDefault(item => SamePath(item.Path, workspace.Path));
        if (found is not null)
        {
            await EnsureRegistered(workspace, ct);
            string head = await Required(workspace.Path, ["rev-parse", "HEAD"], ct);
            var clone = workspace.StorageMode == "refs-cow" ? await ReadCloneReceipt(workspace, head, ct) : null;
            return new(workspace.Path, workspace.SourceReference, workspace.BaseRevision ?? head, clone);
        }
        await EnsureClean(workspace.ProjectPath, ct);
        if (Directory.Exists(workspace.Path) || File.Exists(workspace.Path))
            throw Error("GitWorkspacePathExists", "An unregistered path already exists. Preserve it for inspection.");
        if (registered.Any(item => item.Branch == workspace.SourceReference))
            throw Error("GitWorkspaceMismatch", "The task branch is already checked out elsewhere.");
        string revision = workspace.BaseRevision ?? await Required(workspace.ProjectPath,
            ["rev-parse", "--verify", "--end-of-options", workspace.BaseReference + "^{commit}"], ct);
        WorkspaceBaseline? baseline = null;
        if (workspace.StorageMode == "refs-cow")
        {
            if (storage is null || baselines is null || workspace.BaselineId is null) throw Error("CoWUnsupported", "Missing CoW provider or baseline.");
            storage.Probe(workspace.Path);
            baseline = baselines.GetBaseline(workspace.BaselineId);
            if (baseline.BaseRevision != revision && workspace.Purpose != "baseline" || !SamePath(baseline.ProjectPath, workspace.ProjectPath))
                throw Error("BaselineMismatch", "Baseline must match the exact source revision and original repository.");
        }
        Directory.CreateDirectory(Path.GetDirectoryName(workspace.Path)!);
        string[] flags = baseline is null ? [] : ["--no-checkout"];
        await Required(workspace.ProjectPath, ["worktree", "add", .. flags, "-b", workspace.SourceReference, workspace.Path, revision], ct);
        CloneStatistics? statistics = null;
        if (baseline is not null)
        {
            statistics = await storage!.CloneAsync(baseline.Path, workspace.Path, [".git"], ct);
            // Populate the independent index without rewriting any large files.
            await Required(workspace.Path, ["read-tree", baseline.BaseRevision], ct);
            await Required(workspace.Path, ["update-index", "--refresh"], ct);
            if (baseline.BaseRevision != revision)
                await Required(workspace.Path, ["read-tree", "-m", "-u", baseline.BaseRevision, revision], ct);
            if ((await InspectAsync(workspace, ct)).HasChanges) throw Error("CloneSourceMismatch", "Cloned source differs from its Git revision. Preserve it for inspection.");
            string receipt = await CloneReceiptPath(workspace, ct);
            await File.WriteAllTextAsync(receipt, JsonSerializer.Serialize(new CloneReceipt(baseline.Id, revision, statistics), Json), CancellationToken.None);
        }
        return new(workspace.Path, workspace.SourceReference, revision, statistics);
    }

    private sealed record CloneReceipt(string BaselineId, string Revision, CloneStatistics Statistics);
    private static async Task<string> CloneReceiptPath(WorkspaceRecord w, CancellationToken ct) =>
        Path.Combine(await Required(w.Path, ["rev-parse", "--path-format=absolute", "--git-dir"], ct), "splinter-clone.json");
    private static async Task<CloneStatistics> ReadCloneReceipt(WorkspaceRecord w, string head, CancellationToken ct)
    {
        string path = await CloneReceiptPath(w, ct);
        if (!File.Exists(path)) throw Error("CloneIncomplete", "A registered workspace has no completed clone receipt. Preserve it for recovery; no full checkout fallback is allowed.");
        var receipt = JsonSerializer.Deserialize<CloneReceipt>(await File.ReadAllTextAsync(path, ct), Json);
        if (receipt is null || receipt.BaselineId != w.BaselineId || receipt.Revision != head)
            throw Error("CloneIncomplete", "Clone receipt does not match the registered workspace.");
        return receipt.Statistics;
    }

    public async Task<SourceState> InspectAsync(WorkspaceRecord workspace, CancellationToken ct = default)
    {
        await EnsureRegistered(workspace, ct);
        string revision = await Required(workspace.Path, ["rev-parse", "HEAD"], ct);
        string[] changes = (await ChangedPaths(workspace.Path, ct)).Where(path => !Managed(path, workspace.ManagedPaths)).ToArray();
        return new(revision, await Fingerprint(workspace, ct), changes.Length != 0, changes);
    }

    public async Task<string> CommitAsync(WorkspaceRecord workspace, string message, CancellationToken ct = default)
    {
        SourceState before = await InspectAsync(workspace, ct);
        if (workspace.Validation is not { Success: true } validation || validation.Fingerprint != before.Fingerprint)
            throw Error("ValidationRequired", "Only the exact successfully validated source can be collected.");
        if (!before.HasChanges) return before.Revision;
        var tracked = SplitZero(await Required(workspace.Path, ["ls-files", "--cached", "-z"], ct));
        if (tracked.Any(path => Managed(path, workspace.ManagedPaths)))
            throw Error("ManagedPathTracked", "Generated infrastructure has been staged/tracked. Preserve it and unstage it explicitly before collecting source.");
        // Excluding an explicitly named ignored directory makes `git add` fail.
        // Only exclude managed paths actually reported by status; ignored caches
        // are already ignored by Git. Literal pathspecs also handle special names.
        string[] exclusions = (await ChangedPaths(workspace.Path, ct)).Where(path => Managed(path, workspace.ManagedPaths))
            .Select(path => ":(exclude,literal)" + path).ToArray();
        await Required(workspace.Path, ["add", "-A", "--", ".", .. exclusions], ct);
        var stagedPaths = SplitZero(await Required(workspace.Path, ["ls-files", "--cached", "-z"], ct));
        if (stagedPaths.Any(path => Managed(path, workspace.ManagedPaths)))
            throw Error("ManagedPathTracked", "Generated files changed while staging. No commit was made; preserve and unstage them before retrying.");
        SourceState staged = await InspectAsync(workspace, ct);
        if (before.Fingerprint != staged.Fingerprint)
            throw Error("SourceChanged", "Source changed during collection. No commit was made; validate the new source.");
        await Required(workspace.Path, ["-c", "commit.gpgsign=false", "commit", "-m", message], ct);
        SourceState committed = await InspectAsync(workspace, ct);
        if (committed.HasChanges || committed.Fingerprint != validation.Fingerprint)
            throw Error("SourceChanged", "Source changed while committing. The commit and later edits are preserved; validate again before collection.");
        return committed.Revision;
    }

    public async Task<IntegrationResult> IntegrateAsync(WorkspaceRecord workspace, string targetReference, CancellationToken ct = default)
    {
        SourceState source = await InspectAsync(workspace, ct);
        if (source.HasChanges) throw Error("GitDirtyWorktree", "Workspace has uncollected changes.");
        if (string.IsNullOrEmpty(workspace.ResultRevision) || source.Revision != workspace.ResultRevision)
            throw Error("GitResultMismatch", "Workspace HEAD must match the collected result.");
        await EnsureClean(workspace.ProjectPath, ct);
        string branch = await Required(workspace.ProjectPath, ["branch", "--show-current"], ct);
        if (branch != targetReference) throw Error("GitIntegrationTargetMismatch", "Original checkout must already be on the requested integration branch.");
        string target = await Required(workspace.ProjectPath, ["rev-parse", "HEAD"], ct);
        if (await IsAncestor(workspace.ProjectPath, source.Revision, target, ct)) return new(target, true);
        try
        {
            var merge = await Run(workspace.ProjectPath, ["-c", "commit.gpgsign=false", "merge", "--no-ff", "--no-edit", source.Revision], ct);
            if (merge.ExitCode != 0) throw Error("IntegrationConflict", merge.Error.Trim());
        }
        catch
        {
            // Only abort the merge we started, and only while its original HEAD is unchanged.
            var mergeHead = await Run(workspace.ProjectPath, ["rev-parse", "--verify", "MERGE_HEAD"], CancellationToken.None);
            var head = await Run(workspace.ProjectPath, ["rev-parse", "HEAD"], CancellationToken.None);
            if (mergeHead.Output.Trim() == source.Revision && head.Output.Trim() == target)
                await Required(workspace.ProjectPath, ["merge", "--abort"], CancellationToken.None);
            throw;
        }
        await EnsureClean(workspace.ProjectPath, ct);
        return new(await Required(workspace.ProjectPath, ["rev-parse", "HEAD"], ct), false);
    }

    public async Task ReleaseAsync(WorkspaceRecord workspace, CancellationToken ct = default)
    {
        CheckPaths(workspace);
        var registered = await Worktrees(workspace.ProjectPath, ct);
        bool exists = registered.Any(item => SamePath(item.Path, workspace.Path));
        if (!exists && !Directory.Exists(workspace.Path)) return;
        if (!exists)
        {
            await ResumePartialRemoval(workspace, ct);
            return;
        }
        await EnsureRegistered(workspace, ct);
        await EnsureClean(workspace.Path, ct);
        string head = await Required(workspace.Path, ["rev-parse", "HEAD"], ct);
        if (workspace.ResultRevision is not null && head != workspace.ResultRevision)
            throw Error("GitResultMismatch", "Current result differs from the collected revision.");
        if (workspace.IntegratedRevision is not null)
        {
            if (!await IsAncestor(workspace.ProjectPath, head, workspace.IntegratedRevision, ct))
                throw Error("GitReleaseResultNotIntegrated", "Result is not included in the recorded integration.");
        }
        else if (head != workspace.BaseRevision)
            throw Error("GitReleaseResultNotIntegrated", "Unintegrated commits must remain available in their workspace.");
        foreach (string ignored in SplitZero(await Required(workspace.Path, ["ls-files", "--others", "--ignored", "--exclude-standard", "-z"], ct)))
            if (!Managed(ignored, workspace.ManagedPaths))
                throw Error("UnownedIgnoredFiles", "Preserving ignored file outside managed generated paths: " + ignored);
        RejectTreeLinks(workspace.Path);
        await Required(workspace.ProjectPath, ["worktree", "remove", workspace.Path], ct);
        // Keep the result branch as a durable recovery reference. It is not a live worktree.
    }

    private static async Task ResumePartialRemoval(WorkspaceRecord workspace, CancellationToken ct)
    {
        if (workspace.Phase != "releasing" || workspace.BaseRevision is null)
            throw Error("GitWorkspaceMismatch", "Unregistered files require a previously recorded release intent; preserve them.");
        string expected = workspace.ResultRevision ?? workspace.BaseRevision;
        string branch = await Required(workspace.ProjectPath,
            ["rev-parse", "--verify", "--end-of-options", "refs/heads/" + workspace.SourceReference + "^{commit}"], ct);
        if (branch != expected)
            throw Error("GitResultMismatch", "The recovery branch changed after release started; preserve the remaining files.");
        if (workspace.IntegratedRevision is not null)
        {
            if (!await IsAncestor(workspace.ProjectPath, expected, workspace.IntegratedRevision, ct))
                throw Error("GitReleaseResultNotIntegrated", "The preserved result is not in the recorded integration.");
        }
        else if (expected != workspace.BaseRevision)
            throw Error("GitReleaseResultNotIntegrated", "Unintegrated results must be retained.");

        // Git may leave source files as well as caches after a failed removal.
        // Every remaining source file must still match its collected Git blob.
        var blobs = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string entry in SplitZero(await Required(workspace.ProjectPath, ["ls-tree", "-r", "-z", expected], ct)))
        {
            int separator = entry.IndexOf('\t');
            if (separator < 0) throw Error("GitStatusInvalid", "Invalid tree entry during release recovery.");
            string[] fields = entry[..separator].Split(' ');
            if (fields.Length == 3 && fields[1] == "blob" && fields[0] is "100644" or "100755")
                blobs.Add(entry[(separator + 1)..], fields[2]);
        }
        RejectTreeLinks(workspace.Path);
        foreach (string file in Directory.EnumerateFiles(workspace.Path, "*", SearchOption.AllDirectories))
        {
            ct.ThrowIfCancellationRequested();
            string relative = Path.GetRelativePath(workspace.Path, file).Replace('\\', '/');
            if (Managed(relative, workspace.ManagedPaths)) continue;
            if (!blobs.TryGetValue(relative, out string? blob) ||
                blob != (await Required(workspace.ProjectPath, ["hash-object", "--path=" + relative, "--", file], ct)).Trim())
                throw Error("UncollectedChanges", "Preserving unexpected or changed file after partial removal: " + relative);
        }
        CheckPaths(workspace);
        RejectTreeLinks(workspace.Path);
        Directory.Delete(workspace.Path, recursive: true);
    }

    private static async Task EnsureRegistered(WorkspaceRecord workspace, CancellationToken ct)
    {
        CheckPaths(workspace);
        if (!(await Worktrees(workspace.ProjectPath, ct)).Any(item => SamePath(item.Path, workspace.Path) && item.Branch == workspace.SourceReference))
            throw Error("GitWorkspaceMismatch", "Path and task branch are not correctly registered together.");
        if (!SamePath(await Required(workspace.Path, ["rev-parse", "--show-toplevel"], ct), workspace.Path))
            throw Error("GitWorkspaceMismatch", "Worktree root does not match registry.");
    }
    private static async Task EnsureClean(string path, CancellationToken ct)
    {
        if ((await ChangedPaths(path, ct)).Length != 0)
            throw Error("GitDirtyWorktree", "Saved and staged input must be clean; existing changes are preserved: " + path);
        foreach (string marker in new[] { "MERGE_HEAD", "CHERRY_PICK_HEAD", "REVERT_HEAD" })
            if ((await Run(path, ["rev-parse", "--verify", marker], ct)).ExitCode == 0)
                throw Error("GitOperationInProgress", "Finish the existing Git operation before using this checkout.");
    }
    private static async Task<string[]> ChangedPaths(string path, CancellationToken ct)
    {
        string[] entries = SplitZero(await Required(path, ["status", "--porcelain=v1", "-z", "--untracked-files=all"], ct));
        var changed = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < entries.Length; i++)
        {
            string entry = entries[i];
            if (entry.Length < 4) throw Error("GitStatusInvalid", "Unexpected Git status record.");
            changed.Add(entry[3..]);
            if ((entry[0] is 'R' or 'C' || entry[1] is 'R' or 'C') && i + 1 < entries.Length) changed.Add(entries[++i]);
        }
        return changed.Order(StringComparer.Ordinal).ToArray();
    }
    private static async Task<string> Fingerprint(WorkspaceRecord workspace, CancellationToken ct)
    {
        string[] files = SplitZero(await Required(workspace.Path, ["ls-files", "--cached", "--others", "--exclude-standard", "-z"], ct));
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (string relative in files.Where(path => !Managed(path, workspace.ManagedPaths)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            string full = Path.GetFullPath(Path.Combine(workspace.Path, relative));
            if (!full.StartsWith(Path.TrimEndingDirectorySeparator(workspace.Path) + Path.DirectorySeparatorChar, Comparison))
                throw Error("GitUnsafePath", "Source path escapes the workspace.");
            PlainPath(full);
            if (!File.Exists(full)) continue;
            byte[] name = Encoding.UTF8.GetBytes(relative);
            hash.AppendData(BitConverter.GetBytes(name.Length)); hash.AppendData(name);
            bool exists = File.Exists(full);
            hash.AppendData([exists ? (byte)1 : (byte)0]);
            if (exists)
            {
                await using var stream = File.OpenRead(full);
                hash.AppendData(await SHA256.HashDataAsync(stream, ct));
            }
        }
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }
    private static bool Managed(string path, string[] roots)
    {
        path = path.Replace('\\', '/');
        return roots.Any(root => root.Contains('*')
            ? !path.Contains('/') && System.IO.Enumeration.FileSystemName.MatchesSimpleExpression(root, path, OperatingSystem.IsWindows())
            : path.Equals(root, Comparison) || path.StartsWith(root.TrimEnd('/') + "/", Comparison));
    }
    private static void CheckPaths(WorkspaceRecord workspace)
    {
        if (!Path.IsPathFullyQualified(workspace.Path) || !Path.IsPathFullyQualified(workspace.ProjectPath) || SamePath(workspace.Path, workspace.ProjectPath))
            throw Error("GitInvalidPath", "Independent absolute project and workspace paths are required.");
        if (Path.GetFullPath(workspace.ProjectPath).StartsWith(Path.TrimEndingDirectorySeparator(Path.GetFullPath(workspace.Path)) + Path.DirectorySeparatorChar, Comparison))
            throw Error("GitInvalidPath", "The workspace must not contain the original repository.");
        PlainPath(workspace.ProjectPath); PlainPath(workspace.Path);
    }
    private static void PlainPath(string path)
    {
        for (string? p = Path.GetFullPath(path); p is not null; p = Path.GetDirectoryName(p))
            if ((File.Exists(p) || Directory.Exists(p)) && (File.GetAttributes(p) & FileAttributes.ReparsePoint) != 0)
                throw Error("GitSymlinkRejected", "Source path contains a reparse point: " + p);
    }
    private static void RejectTreeLinks(string root)
    {
        var remaining = new Stack<string>(); remaining.Push(root);
        while (remaining.Count > 0)
            foreach (string entry in Directory.EnumerateFileSystemEntries(remaining.Pop()))
            {
                if ((File.GetAttributes(entry) & FileAttributes.ReparsePoint) != 0) throw Error("GitSymlinkRejected", entry);
                if (Directory.Exists(entry)) remaining.Push(entry);
            }
    }
    private static async Task<bool> IsAncestor(string path, string ancestor, string descendant, CancellationToken ct)
    {
        var result = await Run(path, ["merge-base", "--is-ancestor", ancestor, descendant], ct);
        return result.ExitCode switch { 0 => true, 1 => false, _ => throw Error("GitCommandFailed", result.Error) };
    }
    private static async Task<List<RegisteredWorktree>> Worktrees(string path, CancellationToken ct)
    {
        string[] lines = (await Required(path, ["worktree", "list", "--porcelain", "-z"], ct)).Split('\0');
        var result = new List<RegisteredWorktree>(); string? directory = null; string branch = "";
        foreach (string line in lines)
        {
            if (line.StartsWith("worktree ", StringComparison.Ordinal)) directory = line[9..];
            else if (line.StartsWith("branch refs/heads/", StringComparison.Ordinal)) branch = line[18..];
            else if (line.Length == 0 && directory is not null) { result.Add(new(directory, branch)); directory = null; branch = ""; }
        }
        return result;
    }
    internal static async Task<string> Required(string cwd, string[] args, CancellationToken ct)
    {
        var result = await Run(cwd, args, ct);
        if (result.ExitCode != 0) throw Error("GitCommandFailed", result.Error.Trim());
        return result.Output.TrimEnd('\r', '\n');
    }
    private static async Task<CommandResult> Run(string cwd, string[] args, CancellationToken ct)
    {
        var start = new ProcessStartInfo("git") { WorkingDirectory = cwd, UseShellExecute = false,
            RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        foreach (string key in start.Environment.Keys.Where(key => key.StartsWith("GIT_", StringComparison.OrdinalIgnoreCase)).ToArray()) start.Environment.Remove(key);
        start.Environment["GIT_TERMINAL_PROMPT"] = "0";
        start.Environment["GIT_OPTIONAL_LOCKS"] = "0";
        start.Environment["LC_ALL"] = "C";
        // Unity PackageCache paths routinely exceed MAX_PATH. Keep this scoped
        // to our command; do not change the user's global or repository config.
        if (OperatingSystem.IsWindows()) { start.ArgumentList.Add("-c"); start.ArgumentList.Add("core.longpaths=true"); }
        foreach (string arg in args) start.ArgumentList.Add(arg);
        Process process;
        try { process = Process.Start(start) ?? throw Error("GitUnavailable", "Git did not start."); }
        catch (Win32Exception ex) { throw Error("GitUnavailable", ex.Message); }
        using (process)
        {
            Task<string> stdout = process.StandardOutput.ReadToEndAsync(); Task<string> stderr = process.StandardError.ReadToEndAsync();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(30));
            try { await process.WaitForExitAsync(timeout.Token); }
            catch (OperationCanceledException)
            {
                if (!process.HasExited) process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(CancellationToken.None);
                if (ct.IsCancellationRequested) throw;
                throw Error("GitCommandTimeout", "Git timed out; inspect the persisted operation before retrying.");
            }
            return new(process.ExitCode, await stdout, await stderr);
        }
    }
    private static string[] SplitZero(string text) => text.Split('\0', StringSplitOptions.RemoveEmptyEntries);
    private static bool SamePath(string a, string b) => string.Equals(Path.GetFullPath(a).TrimEnd(Path.DirectorySeparatorChar), Path.GetFullPath(b).TrimEnd(Path.DirectorySeparatorChar), Comparison);
    private static StringComparison Comparison => OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
    private static WorkspaceException Error(string code, string message) => new(code, message);
    private sealed record RegisteredWorktree(string Path, string Branch);
    private sealed record CommandResult(int ExitCode, string Output, string Error);
}
