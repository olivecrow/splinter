using System.ComponentModel;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Splinter.Core;

namespace Splinter.SourceControl.Git;

public sealed partial class GitWorkspaceProvider(ISourceChangeProvider? sourceChanges = null, bool auditSource = false,
    IWorkspacePreparationObserver? preparationObserver = null) : ISourceWorkspaceProvider, ISnapshotSourceWorkspaceProvider, ISourceDiscardProvider
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public Task<string> ResolveRevisionAsync(string project, string reference, CancellationToken ct = default) =>
        Required(project, ["rev-parse", "--verify", "--end-of-options", reference + "^{commit}"], ct);
    public async Task<SourceWorkspace> CreateAsync(WorkspaceRecord workspace, CancellationToken ct = default)
    {
        if (workspace.StorageMode != "git-checkout")
            throw Error("UnsupportedStorageMode", "Only explicit diagnostic git-checkout creates a linked checkout; snapshot workspaces use their own registration path.");
        CheckPaths(workspace);
        string root = await Required(workspace.ProjectPath, ["rev-parse", "--show-toplevel"], ct);
        if (!SamePath(root, workspace.ProjectPath)) throw Error("GitProjectRootMismatch", "The alpha requires the Unity project at its repository root.");
        var registered = await Worktrees(workspace.ProjectPath, ct);
        var found = registered.FirstOrDefault(item => SamePath(item.Path, workspace.Path));
        if (found is not null)
        {
            await EnsureRegistered(workspace, ct);
            string head = await Required(workspace.Path, ["rev-parse", "HEAD"], ct);
            return new(workspace.Path, workspace.SourceReference, workspace.BaseRevision ?? head);
        }
        await EnsureClean(workspace.ProjectPath, ct);
        if (Directory.Exists(workspace.Path) || File.Exists(workspace.Path))
            throw Error("GitWorkspacePathExists", "An unregistered path already exists. Preserve it for inspection.");
        if (registered.Any(item => item.Branch == workspace.SourceReference))
            throw Error("GitWorkspaceMismatch", "The task branch is already checked out elsewhere.");
        string revision = workspace.BaseRevision ?? await Required(workspace.ProjectPath,
            ["rev-parse", "--verify", "--end-of-options", workspace.BaseReference + "^{commit}"], ct);
        Directory.CreateDirectory(Path.GetDirectoryName(workspace.Path)!);
        await Required(workspace.ProjectPath, ["worktree", "add", "-b", workspace.SourceReference, workspace.Path, revision], ct);
        return new(workspace.Path, workspace.SourceReference, revision);
    }

    public async Task<SourceState> InspectAsync(WorkspaceRecord workspace, CancellationToken ct = default)
    {
        await EnsureRegistered(workspace, ct);
        return await InspectSourceAsync(workspace, ct);
    }

    public async Task<string> CheckpointAsync(WorkspaceRecord workspace, string message, CancellationToken ct = default)
    {
        if (WorkspaceStorage.HasRangeJournal(workspace.StorageMode)) await CheckSnapshotGitFiles(workspace.Path, ct);
        SourceState before = await InspectAsync(workspace, ct);
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
        if (WorkspaceStorage.HasRangeJournal(workspace.StorageMode))
            foreach (string[] batch in before.ChangedPaths.Where(path => tracked.Contains(path, StringComparer.Ordinal) &&
                File.Exists(Path.Combine(workspace.Path, path))).Select(path => ":(literal)" + path).Chunk(50))
                await Required(workspace.Path, ["add", "--renormalize", "--", .. batch], ct);
        var stagedPaths = SplitZero(await Required(workspace.Path, ["ls-files", "--cached", "-z"], ct));
        if (WorkspaceStorage.HasRangeJournal(workspace.StorageMode)) await CheckSnapshotGitFiles(workspace.Path, ct);
        if (stagedPaths.Any(path => Managed(path, workspace.ManagedPaths)))
            throw Error("ManagedPathTracked", "Generated files changed while staging. No commit was made; preserve and unstage them before retrying.");
        SourceState staged = await InspectAsync(workspace, ct);
        if (before.Fingerprint != staged.Fingerprint)
            throw Error("SourceChanged", "Source changed during collection. No commit was made; preserve the current source and retry.");
        await Required(workspace.Path, ["-c", "commit.gpgsign=false", "commit", "-m", message], ct);
        SourceState committed = await InspectAsync(workspace, ct);
        if (committed.HasChanges || committed.Fingerprint != before.Fingerprint)
            throw Error("SourceChanged", "Source changed while committing. The commit and later edits are preserved; recollect the stable source.");
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
    private static bool Managed(string path, string[] roots) => SourcePathPolicy.Managed(path.Replace('\\', '/'), roots);
    private static void CheckPaths(WorkspaceRecord workspace)
    {
        if (!Path.IsPathFullyQualified(workspace.Path) || !Path.IsPathFullyQualified(workspace.ProjectPath) || SamePath(workspace.Path, workspace.ProjectPath))
            throw Error("GitInvalidPath", "Independent absolute project and workspace paths are required.");
        if (Path.GetFullPath(workspace.ProjectPath).StartsWith(Path.TrimEndingDirectorySeparator(Path.GetFullPath(workspace.Path)) + Path.DirectorySeparatorChar, Comparison))
            throw Error("GitInvalidPath", "The workspace must not contain the original repository.");
        PlainPath(workspace.ProjectPath); PlainPath(workspace.Path, WorkspaceMountAccess.ObservedRoot(workspace));
    }
    private static void PlainPath(string path, string? ownedMountRoot = null)
    {
        for (string? p = Path.GetFullPath(path); p is not null; p = Path.GetDirectoryName(p))
            // A directory-mounted filesystem is itself a reparse point. Only
            // its observed, identity-bound root is allowed; ancestors and
            // source entries inside it still must be plain paths.
            if (ownedMountRoot is null || !SamePath(p, ownedMountRoot))
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
    internal static async Task RefreshOverlayIndex(string cwd, bool allowSourceChanges, CancellationToken ct)
    {
        var result = await Run(cwd, ["update-index", "--refresh"], ct);
        // Exit 1 with no diagnostic means tracked files need update. A sealed
        // validation candidate intentionally differs from its baseline index.
        if (result.ExitCode != 0 && !(allowSourceChanges && result.ExitCode == 1 && result.Error.Length == 0))
            throw Error("GitCommandFailed", result.Error.Trim());
    }
    private static async Task<CommandResult> Run(string cwd, string[] args, CancellationToken ct, string? input = null)
    {
        var start = new ProcessStartInfo("git") { WorkingDirectory = cwd, UseShellExecute = false,
            RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true,
            RedirectStandardInput = input is not null };
        if (input is not null) start.StandardInputEncoding = new UTF8Encoding(false);
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
            // Workspace operations can hash multi-GiB assets through an overlay
            // or create Git objects. Keep a bounded deadline without treating
            // ordinary large-project work as a hung metadata query.
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromMinutes(10));
            try
            {
                Task send = input is null ? Task.CompletedTask : SendInput();
                await Task.WhenAll(process.WaitForExitAsync(timeout.Token), send);
            }
            catch (OperationCanceledException)
            {
                if (!process.HasExited) process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(CancellationToken.None);
                if (ct.IsCancellationRequested) throw;
                throw Error("GitCommandTimeout", "Git exceeded the 10-minute workspace operation limit; inspect the persisted operation and remaining locks before retrying.");
            }
            catch
            {
                if (!process.HasExited) process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(CancellationToken.None);
                throw;
            }
            return new(process.ExitCode, await stdout, await stderr);

            async Task SendInput()
            {
                await process.StandardInput.WriteAsync(input.AsMemory(), timeout.Token);
                process.StandardInput.Close();
            }
        }
    }
    private static string[] SplitZero(string text) => text.Split('\0', StringSplitOptions.RemoveEmptyEntries);
    private static bool SamePath(string a, string b) => string.Equals(Path.GetFullPath(a).TrimEnd(Path.DirectorySeparatorChar), Path.GetFullPath(b).TrimEnd(Path.DirectorySeparatorChar), Comparison);
    private static StringComparison Comparison => OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
    private static WorkspaceException Error(string code, string message) => new(code, message);
    private sealed record RegisteredWorktree(string Path, string Branch);
    private sealed record CommandResult(int ExitCode, string Output, string Error);
}
