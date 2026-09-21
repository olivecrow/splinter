using Splinter.Core;

namespace Splinter.SourceControl.Git;

public sealed partial class GitWorkspaceProvider
{
    public async Task<string> PreflightAsync(WorkspaceRecord w, CancellationToken ct = default)
    {
        CheckPaths(w);
        if (!Directory.Exists(Path.Combine(w.ProjectPath, ".git")) ||
            !SamePath(await Required(w.ProjectPath, ["rev-parse", "--show-toplevel"], ct), w.ProjectPath))
            throw Error("SnapshotSourceUnsupported", "The first NTFS alpha accepts repository roots with a local .git directory.");
        await EnsureClean(w.ProjectPath, ct);
        string revision = await ResolveRevisionAsync(w.ProjectPath, w.BaseReference, ct);
        if (await Required(w.ProjectPath, ["rev-parse", "HEAD"], ct) != revision)
            throw Error("SnapshotCheckpointMismatch", "The requested base must equal the source checkout HEAD.");
        if (SplitZero(await Required(w.ProjectPath, ["ls-files", "-v", "-z"], ct)).Any(entry => !entry.StartsWith("H ", StringComparison.Ordinal)))
            throw Error("SnapshotSourceUnsupported", "Skip-worktree, assume-unchanged and unresolved index entries cannot prove a clean captured source.");
        await CheckSnapshotGitFiles(w.ProjectPath, ct);
        return revision;
    }

    private static async Task CheckSnapshotGitFiles(string path, CancellationToken ct)
    {
        foreach (string entry in SplitZero(await Required(path, ["ls-files", "--stage", "-z"], ct)))
            if (!entry.StartsWith("100644 ", StringComparison.Ordinal) && !entry.StartsWith("100755 ", StringComparison.Ordinal))
                throw Error("SnapshotSourceUnsupported", "Submodules, symbolic links and unresolved index entries are not supported.");
        string[] paths = SplitZero(await Required(path, ["ls-files", "--cached", "--others", "--exclude-standard", "-z"], ct)).Distinct(StringComparer.Ordinal).ToArray();
        foreach (string[] batch in paths.Chunk(50))
        {
            foreach (string[] options in new[] { Array.Empty<string>(), new[] { "--cached" } })
            {
                string[] values = SplitZero(await Required(path, ["check-attr", .. options, "-z", "filter", "working-tree-encoding", "--", .. batch], ct));
                for (int i = 2; i < values.Length; i += 3)
                    if (values[i] is not ("unspecified" or "unset"))
                        throw Error("SnapshotFilterUnsupported", "Materializing filters/LFS and working-tree-encoding need separate qualification: " + values[i - 2]);
            }
        }
    }

    public async Task RegisterAsync(WorkspaceRecord w, string control, CancellationToken ct = default)
    {
        if (w.Snapshot?.Reference is null || w.BaseRevision is null) throw Error("SnapshotNotReady", "Capture a fixed checkpoint first.");
        // Once captured, changes to the original are explicitly allowed.
        await GitOverlayRegistration.PrepareAsync(w.ProjectPath, w.Path, w.SourceReference, w.BaseRevision, w.Id,
            ReceiptPath(control), ct, capturedSource: true);
    }
    public async Task AttachAsync(WorkspaceRecord w, string control, CancellationToken ct = default) =>
        await GitOverlayRegistration.AttachAsync(await Receipt(w, control, ct), ct);

    public async Task<string> PreserveAsync(WorkspaceRecord w, string control, CancellationToken ct = default)
    {
        await Receipt(w, control, ct);
        await CheckSnapshotGitFiles(w.Path, ct);
        var source = await InspectAsync(w, ct);
        if (source.HasChanges || source.Revision != w.ResultRevision)
            throw Error("UncollectedChanges", "Checkpoint current changes before preserving a source result.");
        foreach (string ignored in SplitZero(await Required(w.Path, ["ls-files", "--others", "--ignored", "--exclude-standard", "-z"], ct)))
            if (!Managed(ignored, w.ManagedPaths)) throw Error("UnownedIgnoredFiles", "Unclassified ignored result must be preserved: " + ignored);
        string reference = ResultReference(w);
        await Required(w.ProjectPath, ["update-ref", reference, source.Revision], ct);
        await Required(w.ProjectPath, ["cat-file", "-e", source.Revision + "^{tree}"], ct);
        return reference;
    }
    public async Task VerifyResultAsync(WorkspaceRecord w, string control, CancellationToken ct = default)
    {
        var result = w.Snapshot?.Result;
        if (result is null || !result.IndependentOfSnapshot || result.SnapshotId != w.Snapshot!.Reference?.Id ||
            result.Revision != w.ResultRevision || result.ResultReference != ResultReference(w))
            throw Error("OverlayResultRequired", "A current independent result manifest is required.");
        if (await Required(w.ProjectPath, ["rev-parse", result.ResultReference], ct) != result.Revision ||
            await Required(w.ProjectPath, ["rev-parse", "refs/heads/" + w.SourceReference], ct) != result.Revision)
            throw Error("GitOverlayResultMismatch", "The preserved result reference changed; keep the delta.");
        await Required(w.ProjectPath, ["cat-file", "-e", result.Revision + "^{tree}"], ct);
        if ((await Required(w.ProjectPath, ["rev-list", "--objects", "--missing=print", result.Revision], ct))
            .Split('\n').Any(line => line.StartsWith('?')))
            throw Error("GitResultIncomplete", "The preserved result has missing objects; retain its delta and snapshot.");
    }
    public async Task UnregisterAsync(WorkspaceRecord w, string control, CancellationToken ct = default)
    {
        await VerifyResultAsync(w, control, ct);
        await GitOverlayRegistration.ReleaseAsync(await Receipt(w, control, ct, verify: false), w.ResultRevision!, ct);
    }
    private static string ReceiptPath(string control) => Path.Combine(control, "git-overlay.json");
    private static string ResultReference(WorkspaceRecord w) => "refs/splinter/snapshot-results/" + w.Id;
    private static async Task<OverlayGitReceipt> Receipt(WorkspaceRecord w, string control, CancellationToken ct, bool verify = true)
    {
        var r = await GitOverlayRegistration.ReadAsync(ReceiptPath(control), ct);
        if (r.OwnerId != w.Id || !SamePath(r.Path, w.Path) || !SamePath(r.Project, w.ProjectPath) || r.Branch != w.SourceReference || r.Revision != w.BaseRevision)
            throw Error("GitOverlayIdentityMismatch", "Registration receipt and workspace identity differ.");
        if (verify) await GitOverlayRegistration.VerifyAsync(r, ct);
        return r;
    }
}
