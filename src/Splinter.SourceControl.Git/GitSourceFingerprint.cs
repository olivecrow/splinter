using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Splinter.Core;

namespace Splinter.SourceControl.Git;

public sealed partial class GitWorkspaceProvider
{
    private sealed record FileHash(string Path, string Hash, string? GitHash);
    private sealed record SourceManifest(int Version, string WorkspaceId, SnapshotReference Snapshot,
        string Policy, string Epoch, long Sequence, FileHash[] Files, string Interpretation);
    private sealed record SourceManifestEnvelope(SourceManifest Value, string Sha256);
    private const long MaxManifestBytes = 64 * 1024 * 1024;

    private async Task<SourceState> InspectSourceAsync(WorkspaceRecord workspace, CancellationToken ct)
    {
        var clock = Stopwatch.StartNew();
        var tracker = workspace.StorageMode == "ntfs-snapshot" ? sourceChanges : null;
        var before = tracker is null ? null : await tracker.ObserveChangesAsync(workspace, ct);
        if (before is not null) RequireView(workspace, before);
        string revision = await Required(workspace.Path, ["rev-parse", "HEAD"], ct);
        string[] changes = (await InspectionChanges(workspace, before is not null, ct)).Where(p => !Managed(p, workspace.ManagedPaths)).ToArray();
        string indexState = before is null ? "" : await Required(workspace.Path, ["ls-files", "--stage", "-z"], ct);
        string[] files = await SourceFiles(workspace, ct);
        if (before is not null) await CheckSnapshotGitFiles(workspace.Path, ct);
        string interpretation = before is null ? "" : await Interpretation(workspace, files, ct);
        string policy = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(
            "source-fingerprint-v1\0" + string.Join('\0', workspace.ManagedPaths))));
        string? cachePath = tracker?.CachePath(workspace);
        SourceManifest? prior = null;
        bool corrupt = false;
        if (cachePath is not null)
        {
            PlainPath(cachePath);
            try { prior = await ReadManifest(cachePath, ct); }
            catch (WorkspaceException ex) when (ex.Code == "SourceCacheRevalidationRequired" && auditSource) { corrupt = true; }
        }
        bool reusable = !auditSource && prior is not null && before is not null && prior.Version == 2 &&
            prior.WorkspaceId == workspace.Id && prior.Snapshot == workspace.Snapshot!.Reference &&
            prior.Policy == policy && prior.Epoch == before.Epoch && prior.Sequence <= before.Sequence;
        var cached = reusable ? prior!.Files.ToDictionary(f => f.Path, StringComparer.Ordinal) : [];
        var invalidated = reusable ? before!.Changes.Where(p => p.Sequence > prior!.Sequence).Select(p => p.Path).ToArray() : [];
        var hashes = new List<FileHash>(files.Length);
        long payload = 0, gitHashInput = 0;
        int readFiles = 0, reusedFiles = 0;
        using var fingerprint = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (string relative in files)
        {
            ct.ThrowIfCancellationRequested();
            string full = Path.GetFullPath(Path.Combine(workspace.Path, relative));
            if (!full.StartsWith(Path.TrimEndingDirectorySeparator(workspace.Path) + Path.DirectorySeparatorChar, Comparison))
                throw Error("GitUnsafePath", "Source path escapes the workspace.");
            PlainPath(full, WorkspaceMountAccess.ObservedRoot(workspace));
            if (!File.Exists(full)) continue;
            string fileHash;
            bool unchanged = cached.TryGetValue(relative, out var item) && !invalidated.Any(path => SourcePathPolicy.Affects(path, relative));
            if (unchanged)
            { fileHash = item!.Hash; reusedFiles++; }
            else
            {
                await using var stream = File.OpenRead(full);
                fileHash = Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, ct));
                payload += stream.Position; readFiles++;
            }
            string? gitHash = null;
            if (before is not null)
            {
                if (unchanged && prior!.Interpretation == interpretation) gitHash = item!.GitHash;
                else
                {
                    // Git's stat cache may report clean after same-size/mtime
                    // writes. Hash content through Git's qualified EOL policy,
                    // independently of that cache, without writing an object.
                    gitHash = await Required(workspace.Path, ["hash-object", "--path=" + relative, "--", relative], ct);
                    gitHashInput += new FileInfo(full).Length;
                }
            }
            hashes.Add(new(relative, fileHash, gitHash));
            byte[] name = Encoding.UTF8.GetBytes(relative);
            fingerprint.AppendData(BitConverter.GetBytes(name.Length)); fingerprint.AppendData(name);
            fingerprint.AppendData([1]); fingerprint.AppendData(Convert.FromHexString(fileHash));
        }
        // Git's index/HEAD live outside the overlay. Check membership as well as
        // the journal barrier; neither mtime nor git status is a content oracle.
        string[] contentChanges = [];
        if (before is not null)
        {
            var head = SplitZero(await Required(workspace.Path, ["ls-tree", "-r", "-z", "HEAD"], ct))
                .Select(entry => entry.Split('\t', 2)).Where(entry => !Managed(entry[1], workspace.ManagedPaths))
                .ToDictionary(entry => entry[1], entry => entry[0].Split(' ')[^1], StringComparer.Ordinal);
            var actual = hashes.ToDictionary(entry => entry.Path, entry => entry.GitHash, StringComparer.Ordinal);
            contentChanges = head.Keys.Concat(actual.Keys).Distinct(StringComparer.Ordinal)
                .Where(path => !head.TryGetValue(path, out var expected) || !actual.TryGetValue(path, out var current) || expected != current)
                .Order(StringComparer.Ordinal).ToArray();
            string finalRevision = await Required(workspace.Path, ["rev-parse", "HEAD"], ct);
            string[] finalFiles = await SourceFiles(workspace, ct);
            string[] finalChanges = (await InspectionChanges(workspace, true, ct)).Where(p => !Managed(p, workspace.ManagedPaths)).ToArray();
            if (revision != finalRevision || !files.SequenceEqual(finalFiles, StringComparer.Ordinal) ||
                !changes.SequenceEqual(finalChanges, StringComparer.Ordinal) ||
                indexState != await Required(workspace.Path, ["ls-files", "--stage", "-z"], ct) ||
                interpretation != await Interpretation(workspace, finalFiles, ct))
                throw Error("SourceChanged", "Source membership or revision changed during verification; retry the current source.");
            var after = await tracker!.ObserveChangesAsync(workspace, ct);
            RequireView(workspace, after);
            if (before.Epoch != after.Epoch || before.Sequence != after.Sequence || !before.Changes.SequenceEqual(after.Changes))
                throw Error("SourceChanged", "Source changed during verification; preserve edits and validate the new generation.");
        }
        string value = Convert.ToHexStringLower(fingerprint.GetHashAndReset());
        long manifestBytes = 0;
        if (cachePath is not null && before is not null)
        {
            var manifest = new SourceManifest(2, workspace.Id, workspace.Snapshot!.Reference!, policy, before.Epoch, before.Sequence, hashes.ToArray(), interpretation);
            byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(new SourceManifestEnvelope(manifest, ManifestHash(manifest)), Json);
            if (bytes.Length > MaxManifestBytes) throw Error("SourceManifestLimit", "Source metadata exceeds the bounded manifest size; preserve the workspace.");
            string temporary = cachePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            PlainPath(temporary);
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { stream.Write(bytes); stream.Flush(true); }
            if (corrupt && File.Exists(cachePath)) File.Move(cachePath, cachePath + ".rejected-" + Guid.NewGuid().ToString("N"));
            File.Move(temporary, cachePath, true);
            manifestBytes = bytes.Length;
        }
        string mode = before is null ? "full" : auditSource ? "audit" : reusable ? "incremental" : prior is null ? "initial" : "rebuild";
        var inspection = new SourceInspectionMetrics(mode, payload, readFiles, reusedFiles, manifestBytes,
            clock.Elapsed.TotalMilliseconds, before?.Epoch, before?.Sequence, gitHashInput);
        if (cachePath is not null)
        {
            string measurements = Path.Combine(Path.GetDirectoryName(cachePath)!, "source-inspections.jsonl");
            PlainPath(measurements);
            await File.AppendAllTextAsync(measurements, JsonSerializer.Serialize(new { workspaceId = workspace.Id,
                fingerprint = value, measuredAt = DateTimeOffset.UtcNow, inspection }, Json) + "\n", ct);
        }
        string[] allChanges = changes.Concat(contentChanges).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        return new(revision, value, allChanges.Length != 0, allChanges)
        {
            Inspection = inspection
        };
    }

    private static async Task<string[]> SourceFiles(WorkspaceRecord workspace, CancellationToken ct) =>
        SplitZero(await Required(workspace.Path, ["ls-files", "--cached", "--others", "--exclude-standard", "-z"], ct))
            .Where(path => !Managed(path, workspace.ManagedPaths)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();

    private static async Task<string[]> InspectionChanges(WorkspaceRecord workspace, bool journalBacked, CancellationToken ct) =>
        journalBacked
            ? SplitZero(await Required(workspace.Path, ["diff", "--cached", "--name-only", "--no-renames", "-z", "HEAD", "--"], ct))
            : await ChangedPaths(workspace.Path, ct);

    private static string ManifestHash(SourceManifest manifest) =>
        Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(manifest, Json)));

    private static async Task<string> Interpretation(WorkspaceRecord workspace, string[] files, CancellationToken ct)
    {
        using var digest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        // Only a digest is persisted: config may contain private values.
        digest.AppendData(Encoding.UTF8.GetBytes(await Required(workspace.Path, ["config", "--null", "--list"], ct)));
        foreach (string[] batch in files.Chunk(50))
            digest.AppendData(Encoding.UTF8.GetBytes(await Required(workspace.Path, ["check-attr", "--all", "-z", "--", .. batch], ct)));
        return Convert.ToHexStringLower(digest.GetHashAndReset());
    }

    private static async Task<SourceManifest?> ReadManifest(string path, CancellationToken ct)
    {
        if (!File.Exists(path)) return null;
        try
        {
            if (new FileInfo(path).Length > MaxManifestBytes) throw new JsonException();
            var envelope = JsonSerializer.Deserialize<SourceManifestEnvelope>(await File.ReadAllTextAsync(path, ct), Json);
            if (envelope?.Value is not { } value || envelope.Sha256 != ManifestHash(value) || value.Version != 2 ||
                value.Snapshot is null || string.IsNullOrWhiteSpace(value.WorkspaceId) || string.IsNullOrWhiteSpace(value.Epoch) ||
                value.Sequence < 0 || string.IsNullOrEmpty(value.Interpretation) || value.Files is null || value.Files.Any(f => f is null || string.IsNullOrEmpty(f.Path) ||
                    f.Hash is null || f.Hash.Length != 64 || !f.Hash.All(char.IsAsciiHexDigit) ||
                    f.GitHash is null || f.GitHash.Length is not (40 or 64) || !f.GitHash.All(char.IsAsciiHexDigit)) ||
                value.Files.Select(f => f.Path).Distinct(StringComparer.Ordinal).Count() != value.Files.Length)
                throw new JsonException();
            return value;
        }
        catch (JsonException)
        {
            throw new WorkspaceException("SourceCacheRevalidationRequired",
                "Preserve the invalid source manifest and run workspace status --audit-source to rebuild it by reading all source.", new { path });
        }
    }

    private static void RequireView(WorkspaceRecord workspace, SourceChangeView view)
    {
        if (view.WorkspaceId != workspace.Id || view.SnapshotId != workspace.Snapshot?.Reference?.Id ||
            string.IsNullOrWhiteSpace(view.Epoch) || view.Sequence < 0 || view.Changes is null ||
            view.Changes.Any(c => c is null || string.IsNullOrEmpty(c.Path) || c.Path.Contains('\\') ||
                c.Path.StartsWith('/') || c.Path.Split('/').Any(p => p is ".." or "." or "") || c.Sequence <= 0 || c.Sequence > view.Sequence) ||
            view.Sequence != (view.Changes.Length == 0 ? 0 : view.Changes.Max(c => c.Sequence)) ||
            view.Changes.Select(c => c.Path).Distinct(StringComparer.OrdinalIgnoreCase).Count() != view.Changes.Length)
            throw Error("SourceObservationInvalid", "Source change history is incomplete or belongs to another workspace; preserve and reconnect.");
    }
}
