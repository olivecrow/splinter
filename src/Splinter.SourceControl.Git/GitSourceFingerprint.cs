using System.Diagnostics;
using System.Buffers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Splinter.Core;

namespace Splinter.SourceControl.Git;

public sealed partial class GitWorkspaceProvider
{
    private sealed record FileHash(string Path, string Hash, string? GitHash, string Interpretation);
    private sealed record SourceManifest(int Version, string WorkspaceId, SnapshotReference Snapshot,
        string Policy, string Epoch, long Sequence, FileHash[] Files, string Configuration);
    private sealed record SourceManifestEnvelope(SourceManifest Value, string Sha256);
    private sealed record SourceInterpretation(string Configuration, Dictionary<string, string> Paths);
    private static readonly string EmptyPathInterpretation = Convert.ToHexStringLower(SHA256.HashData(ReadOnlySpan<byte>.Empty));
    private const int SourceManifestVersion = 3;
    private const long MaxManifestBytes = 64 * 1024 * 1024;
    private sealed record SourceProof(SourceChangeView View, string Revision, string[] Files, string Interpretation,
        string Index, string Path, string ProjectPath, SnapshotReference Snapshot, string[] ManagedPaths, bool CompleteMembership);

    private async Task<SourceState> InspectSourceAsync(WorkspaceRecord workspace, CancellationToken ct,
        Action<SourceProof>? capture = null)
    {
        var clock = Stopwatch.StartNew();
        initialContentAdmissions.TryRemove(workspace.Id, out var initialAdmission);
        var tracker = WorkspaceStorage.HasRangeJournal(workspace.StorageMode) ? sourceChanges : null;
        var before = tracker is null ? null : await tracker.ObserveChangesAsync(workspace, ct);
        if (before is not null) RequireView(workspace, before);
        string revision = await Required(workspace.Path, ["rev-parse", "HEAD"], ct);
        string[] changes = (await InspectionChanges(workspace, before is not null, ct)).Where(p => !Managed(p, workspace.ManagedPaths)).ToArray();
        string indexState = before is null ? "" : await Required(workspace.Path, ["ls-files", "--stage", "-z"], ct);
        string[] files = await SourceFiles(workspace, ct);
        if (before is not null) await CheckSnapshotGitFiles(workspace.Path, ct);
        GitCleanIdentity? cleanIdentity = null;
        SourceInterpretation? sourceInterpretation = null;
        string interpretation = before is null ? "" : await Interpretation(workspace, files, ct,
            workspace.StorageMode == "git-snapshot" ? value => cleanIdentity = value : null,
            value => sourceInterpretation = value);
        HashAlgorithmName gitAlgorithm = default;
        if (cleanIdentity is not null && files.Any(path => cleanIdentity.Requirement(path) != GitCleanRequirement.None))
            gitAlgorithm = (await Required(workspace.Path, ["rev-parse", "--show-object-format"], ct)) switch
            {
                "sha1" => HashAlgorithmName.SHA1,
                "sha256" => HashAlgorithmName.SHA256,
                _ => throw Error("GitHashOutputInvalid", "The Git object hash format is unsupported.")
            };
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
        bool reusable = !auditSource && prior is not null && before is not null && prior.Version == SourceManifestVersion &&
            prior.WorkspaceId == workspace.Id && prior.Snapshot == workspace.Snapshot!.Reference &&
            prior.Policy == policy && prior.Epoch == before.Epoch && prior.Sequence <= before.Sequence;
        var cached = reusable ? prior!.Files.ToDictionary(f => f.Path, StringComparer.Ordinal) : [];
        var invalidated = reusable ? before!.Changes.Where(p => p.Sequence > prior!.Sequence).Select(p => p.Path).ToArray() : [];
        var initialContent = await InitialContentAsync(workspace, initialAdmission, before, revision, files, prior is not null || corrupt, ct);
        var hashes = new List<FileHash>(files.Length);
        var needsGitHash = new List<string>();
        long payload = 0, gitHashInput = 0;
        int readFiles = 0, reusedFiles = 0;
        var paths = new GitSourcePathInspection(WorkspaceMountAccess.ObservedRoot(workspace), journalBound: before is not null);
        using var fingerprint = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (string relative in files)
        {
            ct.ThrowIfCancellationRequested();
            string full = Path.GetFullPath(Path.Combine(workspace.Path, relative));
            if (!full.StartsWith(Path.TrimEndingDirectorySeparator(workspace.Path) + Path.DirectorySeparatorChar, Comparison))
                throw Error("GitUnsafePath", "Source path escapes the workspace.");
            var attributes = paths.Inspect(full);
            if (attributes is null || (attributes & FileAttributes.Directory) != 0) continue;
            string fileHash;
            string? gitHash = null;
            string pathInterpretation = sourceInterpretation?.Paths.GetValueOrDefault(relative, EmptyPathInterpretation) ?? "";
            bool unchanged = cached.TryGetValue(relative, out var item) && !invalidated.Any(path => SourcePathPolicy.Affects(path, relative));
            if (unchanged)
            { fileHash = item!.Hash; reusedFiles++; }
            else if (initialContent.TryGetValue(relative, out var initial))
            {
                if (new FileInfo(full).Length != initial.Summary.Length)
                    throw Error("SourceChanged", "Initial source length changed after its fixed content proof.");
                fileHash = initial.Summary.Sha256; reusedFiles++;
                if (GitCleanIdentity.IsRawIdentity(cleanIdentity?.Requirement(relative) ?? GitCleanRequirement.None,
                    initial.Summary.Length, initial.Summary.ContainsCarriageReturn, initial.Summary.ContainsNul))
                    gitHash = initial.ObjectId;
            }
            else
            {
                await using var stream = File.OpenRead(full);
                var requirement = cleanIdentity?.Requirement(relative) ?? GitCleanRequirement.None;
                if (requirement != GitCleanRequirement.None)
                {
                    var pair = await GitCleanIdentity.HashAsync(stream, requirement, gitAlgorithm, ct);
                    fileHash = pair.RawHash;
                    gitHash = pair.GitHash;
                }
                else
                    fileHash = tracker is null ? Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, ct)) :
                        await HashRangeSourceAsync(stream, ct);
                payload += stream.Position; readFiles++;
            }
            if (before is not null)
            {
                if (unchanged && prior!.Configuration == sourceInterpretation!.Configuration && item!.Interpretation == pathInterpretation)
                    gitHash = item.GitHash;
                else if (gitHash is null)
                {
                    // Unproved conversion identity (including reused raw hashes
                    // under changed attributes/config) still uses Git's filters.
                    needsGitHash.Add(relative);
                    gitHashInput += new FileInfo(full).Length;
                }
            }
            hashes.Add(new(relative, fileHash, gitHash, pathInterpretation));
            byte[] name = Encoding.UTF8.GetBytes(relative);
            fingerprint.AppendData(BitConverter.GetBytes(name.Length)); fingerprint.AppendData(name);
            fingerprint.AppendData([1]); fingerprint.AppendData(Convert.FromHexString(fileHash));
        }
        int gitHashProcesses = 0;
        if (needsGitHash.Count != 0)
        {
            var gitHashes = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string[] batch in needsGitHash.Chunk(256))
            {
                ct.ThrowIfCancellationRequested();
                string input = string.Join('\n', batch.Select(QuoteGitPath)) + "\n";
                var result = await Run(workspace.Path, ["hash-object", "--stdin-paths"], ct, input);
                gitHashProcesses++;
                if (result.ExitCode != 0) throw Error("GitCommandFailed", result.Error.Trim());
                string[] values = result.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(v => v.TrimEnd('\r')).ToArray();
                if (values.Length != batch.Length || values.Any(v => v.Length is not (40 or 64) || !v.All(char.IsAsciiHexDigit)))
                    throw Error("GitHashOutputInvalid", "Git did not return exactly one content hash per source path.");
                for (int i = 0; i < batch.Length; i++) gitHashes.Add(batch[i], values[i]);
            }
            for (int i = 0; i < hashes.Count; i++)
                if (hashes[i].GitHash is null) hashes[i] = hashes[i] with { GitHash = gitHashes[hashes[i].Path] };
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
            var manifest = new SourceManifest(SourceManifestVersion, workspace.Id, workspace.Snapshot!.Reference!, policy, before.Epoch,
                before.Sequence, hashes.ToArray(), sourceInterpretation!.Configuration);
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
            clock.Elapsed.TotalMilliseconds, before?.Epoch, before?.Sequence, gitHashInput, gitHashProcesses);
        if (cachePath is not null)
        {
            string measurements = Path.Combine(Path.GetDirectoryName(cachePath)!, "source-inspections.jsonl");
            PlainPath(measurements);
            await File.AppendAllTextAsync(measurements, JsonSerializer.Serialize(new { workspaceId = workspace.Id,
                fingerprint = value, measuredAt = DateTimeOffset.UtcNow, inspection }, Json) + "\n", ct);
        }
        string[] allChanges = changes.Concat(contentChanges).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        if (before is not null) capture?.Invoke(new(before with { Changes = [.. before.Changes] }, revision, files, interpretation,
            indexState, workspace.Path, workspace.ProjectPath, workspace.Snapshot!.Reference!, [.. workspace.ManagedPaths], hashes.Count == files.Length));
        return new(revision, value, allChanges.Length != 0, allChanges)
        {
            Inspection = inspection
        };
    }

    private async Task VerifyStagedSourceAsync(WorkspaceRecord workspace, SourceProof proof, CancellationToken ct,
        bool staging = true)
    {
        await EnsureRegistered(workspace, ct);
        RequireView(workspace, proof.View);
        if (workspace.Path != proof.Path || workspace.ProjectPath != proof.ProjectPath || workspace.Snapshot!.Reference != proof.Snapshot ||
            !workspace.ManagedPaths.SequenceEqual(proof.ManagedPaths, StringComparer.Ordinal))
            throw Error("SourceChanged", "Checkpoint source identity or managed-path policy changed.");
        string index = await Required(workspace.Path, ["ls-files", "--stage", "-z"], ct);
        RequireUnmanagedIndex(index, workspace.ManagedPaths);
        await CheckSnapshotGitFiles(workspace.Path, ct);
        string revision = await Required(workspace.Path, ["rev-parse", "HEAD"], ct);
        string[] files = await SourceFiles(workspace, ct);
        string interpretation = await Interpretation(workspace, files, ct);
        string[] finalFiles = await SourceFiles(workspace, ct);
        string finalInterpretation = await Interpretation(workspace, finalFiles, ct);
        if (!staging && index != proof.Index || revision != proof.Revision || !files.SequenceEqual(proof.Files, StringComparer.Ordinal) ||
            interpretation != proof.Interpretation || index != await Required(workspace.Path, ["ls-files", "--stage", "-z"], ct) ||
            revision != await Required(workspace.Path, ["rev-parse", "HEAD"], ct) ||
            !files.SequenceEqual(finalFiles, StringComparer.Ordinal) || interpretation != finalInterpretation)
            throw Error("SourceChanged", "Source membership, interpretation or Git metadata changed while staging; retain the source.");
        var current = await sourceChanges!.ObserveChangesAsync(workspace, ct);
        RequireView(workspace, current);
        if (current.Epoch != proof.View.Epoch || current.Sequence != proof.View.Sequence ||
            !current.Changes.SequenceEqual(proof.View.Changes))
            throw Error("SourceChanged", "Source changed during checkpoint verification; preserve the current source and any retained commit.");
    }

    private static void RequireUnmanagedIndex(string index, string[] managedPaths)
    {
        foreach (string entry in SplitZero(index))
        {
            string[] fields = entry.Split('\t', 2);
            if (fields.Length != 2 || Managed(fields[1], managedPaths))
                throw Error("ManagedPathTracked", "Generated infrastructure entered the source index; preserve and unstage it before checkpointing.");
        }
    }

    private static async Task<string> HashRangeSourceAsync(Stream stream, CancellationToken ct)
    {
        // The snapshot protocol already bounds each transfer to 1MiB. Use that
        // bound for an explicit full-content hash instead of 4KiB OS callbacks.
        byte[] buffer = ArrayPool<byte>.Shared.Rent(1024 * 1024);
        try
        {
            using var digest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            int count;
            while ((count = await stream.ReadAsync(buffer.AsMemory(0, 1024 * 1024), ct)) != 0)
                digest.AppendData(buffer.AsSpan(0, count));
            return Convert.ToHexStringLower(digest.GetHashAndReset());
        }
        finally { ArrayPool<byte>.Shared.Return(buffer); }
    }

    private static string QuoteGitPath(string path)
    {
        // --stdin-paths uses Git's C-style quoted byte strings, not JSON or
        // shell quoting. Octal UTF-8 also preserves newlines and non-ASCII names.
        var quoted = new StringBuilder("\"");
        foreach (byte value in Encoding.UTF8.GetBytes(path))
        {
            if (value is (byte)'"' or (byte)'\\') quoted.Append('\\').Append((char)value);
            else if (value is < 32 or >= 127) quoted.Append('\\').Append(Convert.ToString(value, 8).PadLeft(3, '0'));
            else quoted.Append((char)value);
        }
        return quoted.Append('"').ToString();
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

    private static async Task<string> Interpretation(WorkspaceRecord workspace, string[] files, CancellationToken ct,
        Action<GitCleanIdentity>? captureIdentity = null, Action<SourceInterpretation>? captureInterpretation = null)
    {
        using var digest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        // Only a digest is persisted: config may contain private values.
        string config = await Required(workspace.Path, ["config", "--null", "--list"], ct);
        digest.AppendData(Encoding.UTF8.GetBytes(config));
        var identity = captureIdentity is null ? null : new GitCleanIdentity(config);
        var paths = captureInterpretation is null ? null : new Dictionary<string, string>(StringComparer.Ordinal);
        var membership = paths is null ? null : files.ToHashSet(StringComparer.Ordinal);
        await AttributeOutputAsync(workspace.Path, files, cached: false, all: true,
            output =>
            {
                // Keep the operation-wide digest unchanged for fresh staging
                // and generation barriers. Only persistent reuse is path-scoped.
                digest.AppendData(Encoding.UTF8.GetBytes(output)); identity?.AddAttributes(output);
                if (paths is not null) CapturePathInterpretations(output, membership!, paths);
            }, ct);
        if (identity is not null) captureIdentity!(identity);
        if (paths is not null) captureInterpretation!(new(
            Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(config))), paths));
        return Convert.ToHexStringLower(digest.GetHashAndReset());
    }

    private static void CapturePathInterpretations(string output, HashSet<string> membership, Dictionary<string, string> paths)
    {
        if (output.Length == 0) return;
        string[] fields = output.Split('\0');
        if (fields[^1] != "" || (fields.Length - 1) % 3 != 0)
            throw Error("GitAttributeOutputInvalid", "Git returned an incomplete attribute interpretation; preserve the source.");
        var attributes = new Dictionary<string, SortedDictionary<string, string>>(StringComparer.Ordinal);
        for (int i = 0; i < fields.Length - 1; i += 3)
        {
            string path = fields[i], name = fields[i + 1];
            if (!membership.Contains(path) || name.Length == 0)
                throw Error("GitAttributeOutputInvalid", "Git returned an invalid path or attribute interpretation; preserve the source.");
            if (!attributes.TryGetValue(path, out var values)) attributes.Add(path, values = new(StringComparer.Ordinal));
            if (!values.TryAdd(name, fields[i + 2]))
                throw Error("GitAttributeOutputInvalid", "Git returned a duplicate attribute interpretation; preserve the source.");
        }
        foreach (var entry in attributes)
        {
            using var digest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            foreach (var attribute in entry.Value)
                digest.AppendData(Encoding.UTF8.GetBytes(attribute.Key + "\0" + attribute.Value + "\0"));
            if (!paths.TryAdd(entry.Key, Convert.ToHexStringLower(digest.GetHashAndReset())))
                throw Error("GitAttributeOutputInvalid", "Git repeated a path across attribute batches; preserve the source.");
        }
    }

    private static async Task<SourceManifest?> ReadManifest(string path, CancellationToken ct)
    {
        if (!File.Exists(path)) return null;
        try
        {
            if (new FileInfo(path).Length > MaxManifestBytes) throw new JsonException();
            string text = await File.ReadAllTextAsync(path, ct);
            using (var document = JsonDocument.Parse(text))
            {
                if (document.RootElement.ValueKind != JsonValueKind.Object) throw new JsonException();
                if (document.RootElement.TryGetProperty("value", out var stored) && stored.ValueKind == JsonValueKind.Object &&
                    stored.TryGetProperty("version", out var version) && version.ValueKind == JsonValueKind.Number &&
                    version.TryGetInt32(out int found) && found != SourceManifestVersion)
                    throw new WorkspaceException("SourceCacheVersionUnsupported",
                        "Preserve the source manifest: only version 3 is supported; older formats are not migrated by audit.", new { path, version = found });
            }
            var envelope = JsonSerializer.Deserialize<SourceManifestEnvelope>(text, Json);
            if (envelope?.Value is not { } value || envelope.Sha256 != ManifestHash(value) || value.Version != SourceManifestVersion ||
                value.Snapshot is null || string.IsNullOrWhiteSpace(value.WorkspaceId) || string.IsNullOrWhiteSpace(value.Epoch) ||
                value.Sequence < 0 || !IsInterpretationHash(value.Configuration) || value.Files is null || value.Files.Any(f => f is null || string.IsNullOrEmpty(f.Path) ||
                    f.Hash is null || f.Hash.Length != 64 || !f.Hash.All(char.IsAsciiHexDigit) ||
                    !IsInterpretationHash(f.Interpretation) ||
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

    private static bool IsInterpretationHash(string? value) => value is { Length: 64 } && value.All(char.IsAsciiHexDigit);

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
