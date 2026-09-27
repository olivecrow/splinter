using System.Text.Json;
using System.Text;
using System.Security.Cryptography;
using Splinter.Core;

namespace Splinter.SourceControl.Git;

public sealed record OverlayGitReceipt(string OwnerId, string Project, string Path, string Branch, string Revision, string GitDirectory, string Pointer);

// This adapter registers an existing immutable tree without checking out file
// payloads. Its receipt remains outside the mount throughout interruption.
public static class GitOverlayRegistration
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static Task<string> Git(string cwd, string[] args, CancellationToken ct) => GitWorkspaceProvider.Required(cwd, args, ct);
    public static async Task<OverlayGitReceipt> PrepareAsync(string project, string path, string branch, string revision, string ownerId, string receiptPath, CancellationToken ct, bool capturedSource = false)
    {
        if (File.Exists(receiptPath))
        {
            var prior = await ReadAsync(receiptPath, ct);
            if (prior.OwnerId != ownerId || !Same(prior.Path, path) || !Same(prior.Project, project) || prior.Branch != branch || prior.Revision != revision)
                throw new WorkspaceException("GitOverlayIdentityMismatch", "Reuse the exact registration request.");
            await VerifyAsync(prior, ct);
            // A response may have been lost after the receipt but before the
            // pointer-only staging directory was removed.
            await FinishPreparation(prior, ct); return prior;
        }
        if (File.Exists(receiptPath + ".intent"))
        {
            using var intent = JsonDocument.Parse(await File.ReadAllTextAsync(receiptPath + ".intent", ct));
            var i = intent.RootElement;
            if (i.GetProperty("ownerId").GetString() != ownerId || i.GetProperty("branch").GetString() != branch || i.GetProperty("revision").GetString() != revision ||
                !Same(i.GetProperty("project").GetString()!, project) || !Same(i.GetProperty("path").GetString()!, path))
                throw new WorkspaceException("GitOverlayIdentityMismatch", "Registration intent belongs to different inputs.");
            string common = await Git(project, ["rev-parse", "--path-format=absolute", "--git-common-dir"], ct);
            string registrations = Path.Combine(common, "worktrees");
            foreach (string directory in Directory.Exists(registrations) ? Directory.EnumerateDirectories(registrations) : [])
            {
                Plain(directory);
                string pointerFile = Path.Combine(directory, "gitdir");
                if (!File.Exists(pointerFile) || !Same((await File.ReadAllTextAsync(pointerFile, ct)).Trim(), Path.Combine(path, ".git"))) continue;
                var recovered = new OverlayGitReceipt(ownerId, project, path, branch, revision, directory, "gitdir: " + directory.Replace('\\', '/') + "\n");
                await VerifyAsync(recovered, ct);
                if (await Git(project, ["rev-parse", "refs/heads/" + branch], ct) != revision)
                    throw new WorkspaceException("GitOverlayReconcileRequired", "Registered branch changed during creation; preserve it.");
                if (File.Exists(Path.Combine(path, ".git"))) recovered = recovered with { Pointer = await File.ReadAllTextAsync(Path.Combine(path, ".git"), ct) };
                await Save(receiptPath, recovered, ct); await FinishPreparation(recovered, ct); return recovered;
            }
            if (Directory.Exists(path) || File.Exists(path))
                throw new WorkspaceException("GitOverlayReconcileRequired", "Unregistered files remain after creation; preserve them.");
        }
        Plain(project); Plain(path); Plain(receiptPath);
        if (Directory.Exists(path) || File.Exists(path) || !Path.IsPathFullyQualified(path) || Same(project, path))
            throw new WorkspaceException("GitOverlayPathExists", "Register at a new absolute path separate from the source.");
        if (!Same(await Git(project, ["rev-parse", "--show-toplevel"], ct), project)) throw new WorkspaceException("GitProjectRootMismatch", "Use the repository root.");
        if (!capturedSource && (await Git(project, ["status", "--porcelain"], ct)).Length != 0) throw new WorkspaceException("GitSourceDirty", "Prepare a recorded source checkpoint first.");
        Directory.CreateDirectory(Path.GetDirectoryName(receiptPath)!);
        await Save(receiptPath + ".intent", new { project, path, branch, revision, ownerId }, ct);
        await Git(project, ["worktree", "add", "--no-checkout", "-b", branch, path, revision], ct);
        string pointer = await File.ReadAllTextAsync(System.IO.Path.Combine(path, ".git"), ct);
        string gitDirectory = await Git(path, ["rev-parse", "--path-format=absolute", "--git-dir"], ct);
        var receipt = new OverlayGitReceipt(ownerId, project, path, branch, revision, gitDirectory, pointer);
        await Save(receiptPath, receipt, ct);
        await FinishPreparation(receipt, ct);
        return receipt;
    }
    private static async Task FinishPreparation(OverlayGitReceipt receipt, CancellationToken ct)
    {
        string project = receipt.Project, path = receipt.Path;
        if (!File.Exists(Path.Combine(receipt.GitDirectory, "locked")))
            await Git(project, ["worktree", "lock", "--reason", "Splinter overlay " + receipt.OwnerId, path], ct);
        // The only file created by --no-checkout is the Git pointer. Never
        // recursively delete a mount or source tree when preparing this path.
        if (!Directory.Exists(path)) return;
        if (Directory.EnumerateFileSystemEntries(path).Count() > 1) return;
        // An already mounted overlay must never be inspected as preparation.
        if (!File.Exists(System.IO.Path.Combine(path, ".git")) || Directory.EnumerateFileSystemEntries(path).Count() != 1)
            throw new WorkspaceException("GitOverlayPathChanged", "Unexpected files appeared; preserve them.");
        if (await File.ReadAllTextAsync(System.IO.Path.Combine(path, ".git"), ct) != receipt.Pointer)
            throw new WorkspaceException("GitOverlayPointerMismatch", "Preparation pointer changed.");
        File.Delete(System.IO.Path.Combine(path, ".git")); Directory.Delete(path, false);
    }
    public static async Task<OverlayGitReceipt> ReadAsync(string path, CancellationToken ct) =>
        JsonSerializer.Deserialize<OverlayGitReceipt>(await File.ReadAllTextAsync(path, ct), Json) ?? throw new WorkspaceException("GitOverlayReceiptMissing", path);
    public static async Task<object> AttachAsync(OverlayGitReceipt receipt, CancellationToken ct, bool allowSourceChanges = false)
    {
        await VerifyAsync(receipt, ct);
        string pointerPath = System.IO.Path.Combine(receipt.Path, ".git");
        if (File.Exists(pointerPath) && await File.ReadAllTextAsync(pointerPath, ct) != receipt.Pointer)
            throw new WorkspaceException("GitOverlayPointerMismatch", "The mount has another Git identity.");
        if (!File.Exists(pointerPath)) await File.WriteAllTextAsync(pointerPath, receipt.Pointer, ct);
        if (!File.Exists(System.IO.Path.Combine(receipt.GitDirectory, "index")))
        {
            await Git(receipt.Path, ["read-tree", receipt.Revision], ct);
        }
        // read-tree may have finished before an interrupted refresh. An index
        // existing alone is not evidence that its stat cache was populated.
        // Git retains any outstanding lock; never remove it automatically.
        string indexLock = Path.Combine(receipt.GitDirectory, "index.lock");
        if (File.Exists(indexLock)) throw new WorkspaceException("GitCommandFailed", "Preserve the outstanding Git index lock.");
        await GitWorkspaceProvider.RefreshOverlayIndex(receipt.Path, allowSourceChanges, ct);
        if (File.Exists(indexLock)) throw new WorkspaceException("GitCommandFailed", "A concurrent Git index writer remains; preserve its lock.");
        return new { receipt, head = await Git(receipt.Path, ["rev-parse", "HEAD"], ct),
            status = await Git(receipt.Path, ["status", "--porcelain"], ct), initialPayloadCopiedBytes = 0 };
    }
    public static async Task VerifyAsync(OverlayGitReceipt r, CancellationToken ct)
    {
        Plain(r.Project); Plain(r.GitDirectory);
        string common = await Git(r.Project, ["rev-parse", "--path-format=absolute", "--git-common-dir"], ct);
        if (!Path.GetFullPath(r.GitDirectory).StartsWith(Path.Combine(Path.GetFullPath(common), "worktrees") + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
            !Same((await File.ReadAllTextAsync(Path.Combine(r.GitDirectory, "gitdir"), ct)).Trim(), Path.Combine(r.Path, ".git")) ||
            (await File.ReadAllTextAsync(Path.Combine(r.GitDirectory, "HEAD"), ct)).Trim() != "ref: refs/heads/" + r.Branch)
            throw new WorkspaceException("GitOverlayOwnerMismatch", "Registered Git directory does not match the receipt.");
    }

    // Read-only qualification for offline candidate publication. A caller must
    // still stop applications and exclusively open the source journal. The
    // proof authorizes projecting this pointer from a new candidate, not
    // deleting, unregistering or editing the worker or the source repository.
    public static async Task<CandidateSourceProjection> QualifyCandidateSourceAsync(
        OverlayGitReceipt receipt, OverlayIdentity identity, CancellationToken ct)
    {
        if (identity.Version != 1 || receipt.OwnerId != identity.Id ||
            !Same(receipt.Project, identity.Snapshot.SourcePath) ||
            identity.ExcludedTopLevel?.Contains(".git", StringComparer.OrdinalIgnoreCase) != true)
            throw new WorkspaceException("GitOverlayIdentityMismatch", "The candidate must use this registration's owner, source and excluded Git metadata.");
        await VerifyAsync(receipt, ct);
        string common = await Git(receipt.Project, ["rev-parse", "--path-format=absolute", "--git-common-dir"], ct);
        string commonPointer = Path.Combine(receipt.GitDirectory, "commondir"), ownership = Path.Combine(receipt.GitDirectory, "locked");
        Plain(commonPointer); Plain(ownership);
        if (!Same(Path.GetDirectoryName(Path.GetFullPath(receipt.GitDirectory))!, Path.Combine(common, "worktrees")) ||
            !File.Exists(commonPointer) || !File.Exists(ownership) ||
            !Same(Path.GetFullPath(Path.Combine(receipt.GitDirectory, (await File.ReadAllTextAsync(commonPointer, ct)).Trim())), common) ||
            (await File.ReadAllTextAsync(ownership, ct)).TrimEnd('\r', '\n') != "Splinter overlay " + receipt.OwnerId)
            throw new WorkspaceException("GitOverlayOwnerMismatch", "The registration must retain this owner's lock and original common directory.");
        string pointer = receipt.Pointer;
        if (string.IsNullOrEmpty(pointer) || !pointer.StartsWith("gitdir: ", StringComparison.Ordinal))
            throw new WorkspaceException("GitOverlayPointerMismatch", "A qualified registration pointer is required.");
        string target = pointer[8..].TrimEnd('\r', '\n');
        if (target.Any(char.IsControl) || !Path.IsPathFullyQualified(target) || !Same(target, receipt.GitDirectory))
            throw new WorkspaceException("GitOverlayPointerMismatch", "The pointer must identify the exact registered Git directory.");
        foreach (string name in new[] { "index.lock", "HEAD.lock", "gitdir.lock", "commondir.lock" })
            if (File.Exists(Path.Combine(receipt.GitDirectory, name)) || Directory.Exists(Path.Combine(receipt.GitDirectory, name)))
                throw new WorkspaceException("CandidateRegistrationBusy", "Preserve the outstanding Git writer lock before qualifying a candidate.");
        byte[] bytes = Encoding.UTF8.GetBytes(pointer);
        if (bytes.Length is < 1 or > 4096)
            throw new WorkspaceException("GitOverlayPointerMismatch", "The bounded pointer exceeds the candidate metadata contract.");
        string hash = Convert.ToHexStringLower(SHA256.HashData(bytes));
        string evidence = Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(receipt, Json)));
        return new(1, identity.Id, identity.Snapshot, evidence, [new(".git", bytes.Length, hash)]);
    }
    public static async Task<object> PreserveAsync(OverlayGitReceipt r, CancellationToken ct)
    {
        await VerifyAsync(r, ct);
        await Git(r.Path, ["add", "-A", "--", ".", ":(exclude)Assets/__Splinter__", ":(exclude)Assets/__Splinter__.meta"], ct);
        string status = await Git(r.Path, ["status", "--porcelain", "--untracked-files=all"], ct);
        if ((await Git(r.Path, ["diff", "--cached", "--name-only"], ct)).Length != 0)
            await Git(r.Path, ["-c", "commit.gpgsign=false", "commit", "-m", "Preserve owned snapshot overlay experiment result"], ct);
        string revision = await Git(r.Path, ["rev-parse", "HEAD"], ct);
        string resultRef = "refs/splinter/snapshot-results/" + r.OwnerId;
        await Git(r.Project, ["update-ref", resultRef, revision], ct);
        await Git(r.Project, ["cat-file", "-e", revision + "^{tree}"], ct);
        return new { revision, resultRef, independentOfSnapshot = true, beforeStatus = status };
    }
    public static async Task<object> ReleaseAsync(OverlayGitReceipt r, string expectedRevision, CancellationToken ct)
    {
        if (Directory.Exists(r.Path) || File.Exists(r.Path)) throw new WorkspaceException("OverlayStillMounted", "Unmount and prove the path absent before removing registration.");
        string saved = await Git(r.Project, ["rev-parse", "refs/splinter/snapshot-results/" + r.OwnerId], ct);
        if (saved != expectedRevision) throw new WorkspaceException("GitOverlayResultMismatch", "Preserved result ref does not match.");
        if (Directory.Exists(r.GitDirectory))
        {
            await VerifyAsync(r, ct);
            if (await Git(r.Project, ["rev-parse", "refs/heads/" + r.Branch], ct) != saved)
                throw new WorkspaceException("GitOverlayResultChanged", "The task branch changed after preservation.");
            if (File.Exists(Path.Combine(r.GitDirectory, "locked"))) await Git(r.Project, ["worktree", "unlock", r.Path], ct);
            await Git(r.Project, ["worktree", "remove", "--force", r.Path], ct);
        }
        if (Directory.Exists(r.GitDirectory)) throw new WorkspaceException("GitOverlayReleaseIncomplete", "Registration remains.");
        return new { registrationRemoved = true, resultRevision = saved, resultBranch = r.Branch };
    }
    private static async Task Save(string path, object value, CancellationToken ct)
    {
        await using (var stream = new FileStream(path + ".new", FileMode.Create, FileAccess.Write, FileShare.None))
        { await JsonSerializer.SerializeAsync(stream, value, Json, ct); stream.Flush(true); }
        File.Move(path + ".new", path, true);
    }
    private static bool Same(string a, string b) => string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(a)), Path.TrimEndingDirectorySeparator(Path.GetFullPath(b)), StringComparison.OrdinalIgnoreCase);
    private static void Plain(string path)
    {
        for (string? p = Path.GetFullPath(path); p != null; p = Path.GetDirectoryName(p))
            if ((File.Exists(p) || Directory.Exists(p)) && (File.GetAttributes(p) & FileAttributes.ReparsePoint) != 0)
                throw new WorkspaceException("UnsafePath", "Unexpected reparse point: " + p);
    }
}
