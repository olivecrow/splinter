using System.Text.Json;
using System.Text.Json.Serialization;
using Splinter.Core;

namespace Splinter.SourceControl.Git;

public sealed partial class GitWorkspaceProvider
{
    internal static async Task<string?> DiscardBranchAsync(OverlayGitReceipt r, CancellationToken ct)
    {
        string reference = "refs/heads/" + r.Branch;
        string output = await Required(r.Project, ["for-each-ref", "--format=%(refname) %(objectname) %(symref)", reference], ct);
        var entry = output.Split('\n').Select(line => line.TrimEnd('\r').Split(' ', 3))
            .SingleOrDefault(parts => parts.Length >= 2 && parts[0] == reference);
        if (entry is { Length: 3 } && entry[2].Length != 0)
            throw new WorkspaceException("DiscardSourceChanged", "A symbolic task reference cannot authorize branch deletion.");
        return entry?[1];
    }
    public async Task<string> ObserveDiscardRevisionAsync(WorkspaceRecord w, string control, CancellationToken ct = default)
    {
        RequireDiscardTask(w);
        var receipt = await Receipt(w, control, ct);
        await GitOverlayRegistration.RequireDiscardOwnerAsync(receipt, ct);
        return await DiscardBranchAsync(receipt, ct)
            ?? throw new WorkspaceException("DiscardSourceChanged", "The exact task branch is missing before discard intent.");
    }

    public async Task DiscardSourceAsync(WorkspaceRecord w, string control, CancellationToken ct = default)
    {
        RequireDiscardTask(w);
        w.Discard!.RequireMatch(w);
        await GitOverlayRegistration.DiscardAsync(await Receipt(w, control, ct, verify: false), w.Discard.Intent,
            Path.Combine(control, "git-discard.json"), ct);
    }

    private static void RequireDiscardTask(WorkspaceRecord w)
    {
        if (w.Purpose != "task" || w.StorageMode != "ntfs-snapshot" || w.SourceReference != "splinter/" + w.Id)
            throw new WorkspaceException("DiscardIntentMismatch", "Discard only the exact branch assigned to this NTFS task.");
    }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record GitDiscardReceipt(
    [property: JsonRequired] WorkspaceDiscardIntent Intent,
    [property: JsonRequired] OverlayGitReceipt Registration,
    [property: JsonRequired] bool Completed);

public static partial class GitOverlayRegistration
{
    internal static async Task RequireDiscardOwnerAsync(OverlayGitReceipt r, CancellationToken ct)
    {
        await VerifyAsync(r, ct);
        string common = await Git(r.Project, ["rev-parse", "--path-format=absolute", "--git-common-dir"], ct);
        if (!Same(Path.GetDirectoryName(r.GitDirectory)!, Path.Combine(common, "worktrees")) ||
            (await File.ReadAllTextAsync(Path.Combine(r.GitDirectory, "locked"), ct)).TrimEnd('\r', '\n') != "Splinter overlay " + r.OwnerId)
            throw new WorkspaceException("GitOverlayOwnerMismatch", "The exact task registration must retain its owner lock.");
        RequireNoGitWriter(r);
    }

    private static void RequireNoGitWriter(OverlayGitReceipt r)
    {
        foreach (string name in new[] { "index.lock", "HEAD.lock", "gitdir.lock", "commondir.lock" })
            if (File.Exists(Path.Combine(r.GitDirectory, name)) || Directory.Exists(Path.Combine(r.GitDirectory, name)))
                throw new WorkspaceException("GitCommandFailed", "Preserve the outstanding Git writer lock.");
    }

    public static async Task DiscardAsync(OverlayGitReceipt r, WorkspaceDiscardIntent intent, string receiptPath, CancellationToken ct)
    {
        Plain(r.Path); Plain(r.GitDirectory); Plain(receiptPath);
        if (intent.Version != 1 || intent.WorkspaceId != r.OwnerId || intent.SourceReference != r.Branch ||
            intent.BaseRevision != r.Revision || !Same(intent.ProjectPath, r.Project) || !Same(intent.Path, r.Path) ||
            Directory.Exists(r.Path) || File.Exists(r.Path))
            throw new WorkspaceException("DiscardIntentMismatch", "Only this intent's absent mount and exact registration may be discarded.");
        var options = new JsonSerializerOptions(Json) { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
        GitDiscardReceipt? prior = null;
        if (File.Exists(receiptPath))
        {
            await using var stream = new FileStream(receiptPath, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
            prior = await JsonSerializer.DeserializeAsync<GitDiscardReceipt>(stream, options, ct)
                ?? throw new WorkspaceException("DiscardIntentMismatch", "Preserve the empty Git discard receipt.");
        }
        if (prior is not null && (prior.Intent != intent || prior.Registration != r))
            throw new WorkspaceException("DiscardIntentMismatch", "Preserve the different Git deletion intent.");
        string? revision = await GitWorkspaceProvider.DiscardBranchAsync(r, ct);
        if (revision is not null && revision != intent.SourceRevision || prior is null && revision is null)
            throw new WorkspaceException("DiscardSourceChanged", "The task branch differs from the frozen discard revision.");
        if (prior?.Completed == true && (Directory.Exists(r.GitDirectory) || revision is not null))
            throw new WorkspaceException("DiscardSourceChanged", "Git resources reappeared after completed discard.");
        if (prior is null)
        {
            await RequireDiscardOwnerAsync(r, ct);
            await SaveDiscardReceipt(receiptPath, new(intent, r, false), ct);
        }
        if (Directory.Exists(r.GitDirectory))
        {
            await VerifyAsync(r, ct); RequireNoGitWriter(r);
            string ownerLock = Path.Combine(r.GitDirectory, "locked");
            if (File.Exists(ownerLock))
            {
                if ((await File.ReadAllTextAsync(ownerLock, ct)).TrimEnd('\r', '\n') != "Splinter overlay " + r.OwnerId)
                    throw new WorkspaceException("GitOverlayOwnerMismatch", "Another owner locked the registration.");
                await Git(r.Project, ["worktree", "unlock", r.Path], ct);
            }
            await Git(r.Project, ["worktree", "remove", "--force", r.Path], ct);
        }
        if (Directory.Exists(r.GitDirectory) || File.Exists(r.GitDirectory))
            throw new WorkspaceException("GitOverlayReleaseIncomplete", "The owned Git registration remains.");
        string listing = await Git(r.Project, ["worktree", "list", "--porcelain"], ct);
        if (listing.Split('\n').Any(line => line.TrimEnd('\r') == "branch refs/heads/" + r.Branch))
            throw new WorkspaceException("DiscardSourceChanged", "The task branch is used by another worktree.");
        // Compare-and-delete this one branch. Independent results and objects
        // remain reachable; no repository GC or shared-ref deletion is performed.
        if (revision is not null) await Git(r.Project, ["update-ref", "--no-deref", "-d", "refs/heads/" + r.Branch, intent.SourceRevision], ct);
        if (await GitWorkspaceProvider.DiscardBranchAsync(r, ct) is not null)
            throw new WorkspaceException("DiscardSourceChanged", "The task branch reappeared; preserve it.");
        await SaveDiscardReceipt(receiptPath, new(intent, r, true), ct);
    }

    private static async Task SaveDiscardReceipt(string path, GitDiscardReceipt receipt, CancellationToken ct)
    {
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(receipt, Json);
        string pending = path + ".new";
        if (File.Exists(pending))
        {
            if (!File.ReadAllBytes(pending).AsSpan().SequenceEqual(bytes))
                throw new WorkspaceException("DiscardIntentMismatch", "Preserve the interrupted Git discard receipt.");
        }
        else
        {
            await using var stream = new FileStream(pending, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            await stream.WriteAsync(bytes, ct); stream.Flush(true);
        }
        if (File.Exists(path)) File.Replace(pending, path, null); else File.Move(pending, path);
    }
}
