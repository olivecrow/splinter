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
        // These are independent read-only queries (optional index writes are
        // disabled by Run). Bound fan-out to four operations and join every
        // child before returning a failure; later fixed-source proofs remain.
        var clean = Task.Run(() => EnsureClean(w.ProjectPath, ct), ct);
        var selected = Task.Run(() => ResolveRevisionAsync(w.ProjectPath, w.BaseReference, ct), ct);
        var flags = Task.Run(() => GitRecords(w.ProjectPath, ["ls-files", "-v", "-z"], ct)
            .Any(entry => !entry.StartsWith("H ", StringComparison.Ordinal)), ct);
        var supported = Task.Run(() => CheckSnapshotGitFiles(w.ProjectPath, ct), ct);
        try { await Task.WhenAll(clean, selected, flags, supported); }
        catch
        {
            // Keep the previous preflight error precedence after all owned
            // processes/pipes have drained, including cancellation/failure.
            await clean; await selected;
        }
        string revision = await selected;
        if (w.StorageMode != "git-snapshot" && await Required(w.ProjectPath, ["rev-parse", "HEAD"], ct) != revision)
            throw Error("SnapshotCheckpointMismatch", "The requested base must equal the source checkout HEAD.");
        if (await flags)
            throw Error("SnapshotSourceUnsupported", "Skip-worktree, assume-unchanged and unresolved index entries cannot prove a clean captured source.");
        await supported;
        if (w.StorageMode == "git-snapshot")
        {
            if (await Required(w.ProjectPath, ["rev-parse", "HEAD"], ct) != revision)
                throw Error("GitBasisUnsupported", "The first Git basis requires a clean checkout at the requested commit before preparation.");
            // No working-tree conversion is performed by the object reader.
            string autoCrlf = (await Run(w.ProjectPath, ["config", "--get", "core.autocrlf"], ct)).Output.Trim();
            if (autoCrlf is not ("" or "false" or "input"))
                throw Error("GitBasisUnsupported", "Raw committed bytes require core.autocrlf=false or input; no materializing conversion fallback.");
        }
        return revision;
    }

    private static async Task CheckSnapshotGitFiles(string path, CancellationToken ct)
    {
        foreach (string entry in GitRecords(path, ["ls-files", "--stage", "-z"], ct))
            if (!entry.StartsWith("100644 ", StringComparison.Ordinal) && !entry.StartsWith("100755 ", StringComparison.Ordinal))
                throw Error("SnapshotSourceUnsupported", "Submodules, symbolic links and unresolved index entries are not supported.");
        using var paths = new PagedStrings(GitRecords(path, ["ls-files", "--cached", "--others", "--exclude-standard", "-z"], ct), ct);
        foreach (bool cached in new[] { false, true })
        {
            foreach (string[] batch in paths.Paths().Chunk(256))
            await AttributeOutputAsync(path, batch, cached, all: false, output =>
            {
                string[] values = SplitZero(output);
                for (int i = 2; i < values.Length; i += 3)
                    if (values[i] is not ("unspecified" or "unset"))
                        throw Error("SnapshotFilterUnsupported", "Materializing filters/LFS and working-tree-encoding need separate qualification: " + values[i - 2]);
            }, ct);
        }
    }

    public async Task RegisterAsync(WorkspaceRecord w, string control, CancellationToken ct = default)
    {
        if (w.Snapshot?.Reference is null || w.BaseRevision is null) throw Error("SnapshotNotReady", "Capture a fixed checkpoint first.");
        // Once captured, changes to the original are explicitly allowed.
        await GitOverlayRegistration.PrepareAsync(w.ProjectPath, w.Path, w.SourceReference, w.BaseRevision, w.Id,
            ReceiptPath(control), ct, capturedSource: true,
            preparationPoint: boundary => preparationObserver?.ReachedAsync(boundary, w, ct) ?? Task.CompletedTask);
    }
    public async Task AttachAsync(WorkspaceRecord w, string control, CancellationToken ct = default)
    {
        var observation = Environment.GetEnvironmentVariable("SPLINTER_GIT_PREPARATION_DIAGNOSTICS") == "1"
            ? new GitPreparationObservation() : null;
        await RunPreparationAttemptAsync(w, control, observation, Attach);

        async Task Attach()
        {
            var receipt = observation is null ? await Receipt(w, control, ct)
                : await observation.Measure("load-receipt", () => Receipt(w, control, ct));
            string expectedHead = w.Results.Count > 0 ? w.BaseRevision! : w.ResultRevision ?? w.BaseRevision!;
            if (await Required(w.ProjectPath, ["rev-parse", "refs/heads/" + w.SourceReference], ct) != expectedHead)
                throw Error("GitOverlayResultChanged", "The owned task branch changed; preserve its index, branch and recovered source.");
            initialContentAdmissions.TryRemove(w.Id, out _);
            // The coordinator has already verified this owned mount and replayed
            // its original journal. Reattachment preserves private edits; the
            // first admission keeps its clean check and immutable warmup.
            await GitOverlayRegistration.AttachAsync(receipt, ct, w.Snapshot?.Attached == true, null, observation);
            if (observation is null) await BeginInitialContentInspectionAsync(w, ct);
            else await observation.Measure("initial-content-admission", () => BeginInitialContentInspectionAsync(w, ct));
        }
    }

    internal static Task RunPreparationAttemptAsync(WorkspaceRecord w, string control,
        GitPreparationObservation? observation, Func<Task> operation)
        => observation is null ? operation() : observation.Run(operation, async snapshot =>
        {
            string path = Path.Combine(control, "git-preparation.json"), temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            PlainPath(path); PlainPath(temporary);
            // Diagnostic publication is outside measured stages and is not a
            // durable proof, recovery input or capture timestamp. Cancellation
            // of attach must not suppress its final cancelled observation.
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                await System.Text.Json.JsonSerializer.SerializeAsync(stream, new
                { workspaceId = w.Id, workspacePath = w.Path, requestDigest = w.RequestDigest,
                    baseRevision = w.BaseRevision, preparation = snapshot }, Json, CancellationToken.None);
            File.Move(temporary, path, true);
        });

    public async Task<string> PreserveAsync(WorkspaceRecord w, string control, CancellationToken ct = default)
    {
        await Receipt(w, control, ct);
        await CheckSnapshotGitFiles(w.Path, ct);
        var source = await InspectAsync(w, ct);
        if (source.HasChanges || source.Revision != w.ResultRevision)
            throw Error("UncollectedChanges", "Checkpoint current changes before preserving a source result.");
        foreach (string ignored in await IgnoredSourceFiles(w, ct))
            if (!Managed(ignored, w.ManagedPaths)) throw Error("UnownedIgnoredFiles", "Unclassified ignored result must be preserved: " + ignored);
        string reference = ResultReference(w);
        await Required(w.ProjectPath, ["update-ref", reference, source.Revision], ct);
        await Required(w.ProjectPath, ["cat-file", "-e", source.Revision + "^{tree}"], ct);
        return reference;
    }

    public bool SupportsCheckpointAndPreserve(WorkspaceRecord w) => WorkspaceStorage.HasRangeJournal(w.StorageMode);

    public async Task<SnapshotSourceCheckpoint> CheckpointAndPreserveAsync(WorkspaceRecord w, string control,
        string message, CancellationToken ct = default)
    {
        if (!WorkspaceStorage.HasRangeJournal(w.StorageMode))
            throw Error("UnsupportedStorageMode", "Sealed checkpoint preservation requires a range-journal workspace.");
        await Receipt(w, control, ct);
        string reference = ResultReference(w);
        var source = await CheckpointSourceAsync(w, message, async revision =>
        {
            foreach (string ignored in await IgnoredSourceFiles(w, ct))
                if (!Managed(ignored, w.ManagedPaths))
                    throw Error("UnownedIgnoredFiles", "Unclassified ignored result must be preserved: " + ignored);
            await Required(w.ProjectPath, ["update-ref", reference, revision], ct);
            await Required(w.ProjectPath, ["cat-file", "-e", revision + "^{tree}"], ct);
        }, ct);
        if (await Required(w.ProjectPath, ["rev-parse", reference], ct) != source.Revision ||
            await Required(w.ProjectPath, ["rev-parse", "refs/heads/" + w.SourceReference], ct) != source.Revision)
            throw Error("GitOverlayResultMismatch", "The preserved checkpoint reference changed; keep the source and result.");
        return new(source, reference);
    }

    private static Task<IEnumerable<string>> IgnoredSourceFiles(WorkspaceRecord w, CancellationToken ct)
    {
        // Wildcard policies retain the existing per-path classification. Literal
        // managed roots can be pruned before Git traverses generated subtrees.
        string magic = OperatingSystem.IsWindows() ? ":(top,exclude,literal,icase)" : ":(top,exclude,literal)";
        var exclusions = w.ManagedPaths.Where(root => !root.Contains('*') && !root.Contains('\\') &&
            !Path.IsPathRooted(root) && root.TrimEnd('/').Split('/').All(part => part is not ("" or "." or "..")))
            .Select(root => magic + root.TrimEnd('/')).ToArray();
        return Task.FromResult(GitRecords(w.Path,
            ["ls-files", "--others", "--ignored", "--exclude-standard", "-z", "--", ".", .. exclusions], ct));
    }
    public async Task VerifyResultAsync(WorkspaceRecord w, string control, CancellationToken ct = default)
    {
        if (w.Results.LastOrDefault() is { } generation)
        {
            if (w.Snapshot?.Result?.Revision != generation.Revision || generation.GitReference is not null &&
                await GitSourceResult.VerifyAsync(w.ProjectPath, generation.GitReference, ct) != generation.GitFingerprint)
                throw Error("GitOverlayResultMismatch", "The current independent generation or final closure differs.");
            if (generation.GitReference is null) { using var basis = GitRetainedResultBasis.Open(w, control); basis.VerifyReadBoundary(); }
            return;
        }
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
    public async Task UnregisterAsync(WorkspaceRecord w, string control, CancellationToken ct = default,
        Func<CancellationToken, Task>? verifyGeneration = null)
    {
        if (verifyGeneration is null) await VerifyResultAsync(w, control, ct);
        else
        {
            var verifiedGeneration = w.Results.LastOrDefault();
            if (verifiedGeneration is null || w.Snapshot?.Result?.Revision != verifiedGeneration.Revision)
                throw Error("GitOverlayResultMismatch", "The current independent generation or final closure differs.");
            // The router's full file-generation proof also opens/verifies the
            // retained Git basis or hashes the Git result. Do not reconstruct
            // and verify that same basis a second time inside the project lock.
            await verifyGeneration(ct);
            if (w.Results.LastOrDefault() != verifiedGeneration || w.Snapshot?.Result?.Revision != verifiedGeneration.Revision)
                throw Error("GitOverlayResultMismatch", "The verified generation changed before registration removal.");
        }
        var receipt = await Receipt(w, control, ct, verify: false);
        if (w.Results.LastOrDefault() is { } generation)
        {
            string reference = generation.GitReference ?? GitRetainedResultBasis.Reference(w);
            await GitOverlayRegistration.ReleaseGenerationAsync(receipt, w.BaseRevision!, reference,
                await Required(w.ProjectPath, ["rev-parse", reference], ct), ct);
        }
        else await GitOverlayRegistration.ReleaseAsync(receipt, w.ResultRevision!, ct);
    }
    private static string ReceiptPath(string control) => Path.Combine(control, "git-overlay.json");
    private static string ResultReference(WorkspaceRecord w) => "refs/splinter/snapshot-results/" + w.Id;
    internal static async Task<OverlayGitReceipt> Receipt(WorkspaceRecord w, string control, CancellationToken ct, bool verify = true)
    {
        var r = await GitOverlayRegistration.ReadAsync(ReceiptPath(control), ct);
        if (r.OwnerId != w.Id || !SamePath(r.Path, w.Path) || !SamePath(r.Project, w.ProjectPath) || r.Branch != w.SourceReference || r.Revision != w.BaseRevision)
            throw Error("GitOverlayIdentityMismatch", "Registration receipt and workspace identity differ.");
        if (verify) await GitOverlayRegistration.VerifyAsync(r, ct);
        return r;
    }
}
