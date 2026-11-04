using System.Buffers.Binary;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Splinter.Core;
using Splinter.Runtime;

namespace Splinter.SourceControl.Git;

public sealed partial class GitWorkspaceProvider
{
    private sealed record PagedSourceManifest(int Version, string WorkspaceId, SnapshotReference Snapshot, string Policy,
        string Epoch, long Sequence, SnapshotCatalogDescriptor Catalog, SnapshotCatalogDescriptor ChangedPaths, string Configuration);
    private sealed record PagedSourceEnvelope(PagedSourceManifest Value, string Sha256);
    private sealed record PagedSourceProof(string FilesHash, string IndexHash, string InterpretationHash, string CachePath,
        SnapshotCatalogDescriptor ChangedPaths);
    private sealed record PagedFileHash(string Path, long Length, string Hash, string GitHash, string Interpretation);

    // Product snapshot readers expose paged change roots. The diagnostic
    // checkout/inline-fake contract remains in the separate legacy inspector.
    private async Task<SourceState> InspectPagedSourceAsync(WorkspaceRecord w, SourceChangeView before,
        InitialContentAdmission? admission, CancellationToken ct, Action<SourceProof>? capture)
    {
        var clock = Stopwatch.StartNew(); RequireView(w, before);
        using var membershipBefore = await ObserveSourceMembership(w, ct);
        string revision = membershipBefore.Revision, index = membershipBefore.Index;
        var files = membershipBefore.Files; var staged = membershipBefore.Staged!;
        string policy = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes("source-fingerprint-v1\0" + string.Join('\0', w.ManagedPaths))));
        string cachePath = sourceChanges!.CachePath(w), pages = cachePath + ".pages";
        PagedSourceManifest? prior = null; bool corrupt = false;
        try { prior = await ReadPagedManifest(cachePath, pages, ct); }
        catch (WorkspaceException ex) when (ex.Code == "SourceCacheRevalidationRequired" && auditSource) { corrupt = true; }
        bool reusable = !auditSource && prior is not null && prior.WorkspaceId == w.Id && prior.Snapshot == w.Snapshot!.Reference &&
            prior.Policy == policy && prior.Epoch == before.Epoch && prior.Sequence <= before.Sequence;
        using var invalidated = new PagedStrings(reusable ? before.EnumerateChanges().Where(change => change.Sequence > prior!.Sequence).Select(change => change.Path) : [], ct, ignoreCase: true);
        using var allChanges = new PagedStrings(before.EnumerateChanges().Select(change => change.Path), ct, ignoreCase: true);
        using PagedSnapshotCatalog? cached = reusable ? new(pages, prior!.Catalog) : null;
        bool catalogUnchanged = cached is not null;
        using var scratch = new TemporaryMetadataDirectory();
        using var cachedFiles = cached is null ? null : new CachedFileCursor(cached, scratch.Path, ct);
        using var hashes = new MetadataSpool(scratch.Path);
        long payload = 0, gitInput = 0, hashed = 0, reused = 0, gitProcesses = 0, actualFiles = 0;
        string config = await Required(w.Path, ["config", "--null", "--list"], ct);
        string configuration = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(config)));
        HashAlgorithmName algorithm = (await Required(w.Path, ["rev-parse", "--show-object-format"], ct)) switch
        { "sha1" => HashAlgorithmName.SHA1, "sha256" => HashAlgorithmName.SHA256, _ => throw Error("GitHashOutputInvalid", "The Git object format is unsupported.") };
        using var interpretation = IncrementalHash.CreateHash(HashAlgorithmName.SHA256); interpretation.AppendData(Encoding.UTF8.GetBytes(config));
        using var fingerprint = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var paths = new GitSourcePathInspection(WorkspaceMountAccess.ObservedRoot(w), journalBound: true);
        bool useInitial = admission is not null && !auditSource && prior is null && !corrupt &&
            w.Snapshot?.Attached == false && sourceChanges is ISourceContentSummaryProvider;
        if (useInitial && (admission!.Snapshot != w.Snapshot!.Reference || admission.Mount != w.Snapshot.Mount || admission.Path != w.Path ||
            admission.Revision != w.BaseRevision || revision != admission.Revision || !admission.ManagedPaths.SequenceEqual(w.ManagedPaths) || !SameGeneration(admission.Generation, before)))
            throw Error("SourceChanged", "Initial content admission changed after attach.");
        using var baseline = useInitial ? new PagedStrings(BaselineEntries(w.Path, revision, ct).Where(entry => !Managed(entry.Key, w.ManagedPaths)), ct) : null;
        try
        {
            foreach (string[] batch in AttributeBatches(files.Paths()))
            {
                ct.ThrowIfCancellationRequested();
                var clean = new GitCleanIdentity(config);
                var attributes = new Dictionary<string, string>(StringComparer.Ordinal);
                var membership = batch.ToHashSet(StringComparer.Ordinal);
                await AttributeOutputAsync(w.Path, batch, cached: false, all: true, output =>
                {
                    interpretation.AppendData(Encoding.UTF8.GetBytes(output)); clean.AddAttributes(output);
                    CapturePathInterpretations(output, membership, attributes);
                }, ct);
                var initial = useInitial ? await InitialBatch(w, admission!, before, batch, baseline!, allChanges, ct) : null;
                // Null means this provider lifetime cannot supply the optional
                // proof. An empty (non-null) response only excludes these paths.
                if (useInitial && initial is null) useInitial = false;
                var records = new List<(string Path, long Length, string Hash, string? GitHash, string Interpretation)>(batch.Length);
                var need = new List<string>(batch.Length);
                foreach (string path in batch)
                {
                    ct.ThrowIfCancellationRequested(); string full = Path.GetFullPath(Path.Combine(w.Path, path));
                    if (!full.StartsWith(Path.TrimEndingDirectorySeparator(w.Path) + Path.DirectorySeparatorChar, Comparison)) throw Error("GitUnsafePath", "Source path escapes the workspace.");
                    var fileAttributes = paths.Inspect(full);
                    if (fileAttributes is null || (fileAttributes & FileAttributes.Directory) != 0) continue;
                    long length = new FileInfo(full).Length;
                    string pathInterpretation = attributes.GetValueOrDefault(path, EmptyPathInterpretation);
                    var original = cachedFiles?.FindNext(path);
                    bool unchanged = original is { Entry.Directory: false, ContentHash: not null } && original.Entry.Path == path && !invalidated.Affects(path);
                    string raw; string? gitHash = null;
                    if (unchanged) { raw = original!.ContentHash!; reused++; }
                    else if (initial is not null && initial.TryGetValue(path, out var proved))
                    {
                        if (length != proved.Summary.Length) throw Error("SourceChanged", "Initial source length changed after its fixed content proof.");
                        raw = proved.Summary.Sha256; reused++;
                        if (GitCleanIdentity.IsRawIdentity(clean.Requirement(path), length, proved.Summary.ContainsCarriageReturn, proved.Summary.ContainsNul)) gitHash = proved.ObjectId;
                    }
                    else
                    {
                        await using var stream = File.OpenRead(full); var requirement = clean.Requirement(path);
                        if (requirement != GitCleanRequirement.None)
                        { var pair = await GitCleanIdentity.HashAsync(stream, requirement, algorithm, ct); raw = pair.RawHash; gitHash = pair.GitHash; }
                        else raw = await HashRangeSourceAsync(stream, ct);
                        payload = checked(payload + stream.Position); hashed++;
                    }
                    if (unchanged && prior!.Configuration == configuration && ParseCached(original!).Interpretation == pathInterpretation)
                        gitHash = ParseCached(original!).GitHash;
                    catalogUnchanged &= unchanged && original!.Entry.Length == length &&
                        ParseCached(original).Interpretation == pathInterpretation && ParseCached(original).GitHash == gitHash;
                    if (gitHash is null) { need.Add(path); gitInput = checked(gitInput + length); }
                    records.Add((path, length, raw, gitHash, pathInterpretation));
                    byte[] name = Encoding.UTF8.GetBytes(path); fingerprint.AppendData(BitConverter.GetBytes(name.Length)); fingerprint.AppendData(name);
                    fingerprint.AppendData([1]); fingerprint.AppendData(Convert.FromHexString(raw)); actualFiles++;
                }
                var converted = new Dictionary<string, string>(StringComparer.Ordinal);
                if (need.Count > 0)
                {
                    string input = string.Join('\n', need.Select(QuoteGitPath)) + "\n";
                    var output = await Run(w.Path, ["hash-object", "--stdin-paths"], ct, input); gitProcesses++;
                    if (output.ExitCode != 0) throw Error("GitCommandFailed", output.Error.Trim());
                    string[] values = output.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(value => value.TrimEnd('\r')).ToArray();
                    if (values.Length != need.Count || values.Any(value => value.Length is not (40 or 64) || !value.All(char.IsAsciiHexDigit)))
                        throw Error("GitHashOutputInvalid", "Git did not return one content hash per source path.");
                    for (int i = 0; i < values.Length; i++) converted.Add(need[i], values[i]);
                }
                foreach (var item in records)
                    hashes.Append(EncodePagedHash(new(item.Path, item.Length, item.Hash, item.GitHash ?? converted[item.Path], item.Interpretation)));
            }
            // Every current record matched. Check omissions as well before
            // reusing an immutable catalog; Git visibility can change without
            // an added mounted file (for example an index-only operation).
            if (catalogUnchanged) catalogUnchanged = cachedFiles!.Complete() == actualFiles;
        }
        finally { cached?.Dispose(); }
        string interpretationHash = Convert.ToHexStringLower(interpretation.GetHashAndReset());
        using var head = new PagedStrings(BaselineEntries(w.Path, revision, ct).Where(entry => !Managed(entry.Key, w.ManagedPaths)), ct);
        using var actual = new PagedStrings(hashes.Read().Select(entry =>
        { var file = DecodePagedHash(entry); return new MetadataEntry(file.Path, Encoding.ASCII.GetBytes(file.GitHash)); }), ct);
        using var changes = new PagedStrings(staged.Entries().Concat(Differences(head, actual)), ct);
        var finalPaths = await ObserveSourcePaths(w, ct);
        using var finalFiles = finalPaths.Files; using var finalStaged = finalPaths.Staged;
        if (revision != await Required(w.Path, ["rev-parse", "HEAD"], ct) || files.Hash != finalFiles.Hash || staged.Hash != finalStaged.Hash ||
            index != IndexDigest(w, ct) || interpretationHash != await PagedInterpretation(w.Path, finalFiles.Paths(), config: null, ct))
            throw Error("SourceChanged", "Source membership, interpretation or Git metadata changed during verification.");
        using var after = await sourceChanges.ObserveChangesAsync(w, ct); RequireView(w, after);
        if (!SameGeneration(before, after)) throw Error("SourceChanged", "Source changed during verification; preserve the new generation.");
        string value = Convert.ToHexStringLower(fingerprint.GetHashAndReset());
        var catalog = catalogUnchanged ? prior!.Catalog : PagedSnapshotCatalog.Build(pages, new("", true, 0, 16, 0, 0, 0),
            SnapshotCatalogInput.WithImplicitDirectories(hashes.Read().Select(entry =>
            {
                var file = DecodePagedHash(entry);
                return new DirectoryMetadataRecord(new(file.Path, false, file.Length, 32, 0, 0, 0), file.Hash, SourceReference: file.GitHash + ":" + file.Interpretation);
            }), scratch.Path, ct: ct), ct: ct, previousCatalog: corrupt ? null : prior?.Catalog);
        var changedRoot = PagedSnapshotCatalog.Build(pages, new("", true, 0, 16, 0, 0, 0),
            SnapshotCatalogInput.WithPathMarkers(changes.Entries(), scratch.Path, caseSensitive: true, ct: ct),
                caseSensitive: true, ct: ct, previousCatalog: corrupt ? null : prior?.ChangedPaths);
        var manifest = new PagedSourceManifest(4, w.Id, w.Snapshot!.Reference!, policy, before.Epoch, before.Sequence, catalog, changedRoot, configuration);
        var envelope = new PagedSourceEnvelope(manifest, PagedManifestHash(manifest));
        string temporary = cachePath + "." + Guid.NewGuid().ToString("N") + ".tmp"; PlainPath(temporary);
        using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        { JsonSerializer.Serialize(output, envelope, Json); output.Flush(true); }
        if (corrupt && File.Exists(cachePath)) File.Move(cachePath, cachePath + ".rejected-" + Guid.NewGuid().ToString("N"));
        File.Move(temporary, cachePath, true);
        var preview = changes.Paths().Take(256).ToArray();
        var metrics = new SourceInspectionMetrics(auditSource ? "audit" : reusable ? "incremental" : prior is null ? "initial" : "rebuild",
            payload, hashed, reused, new FileInfo(cachePath).Length, clock.Elapsed.TotalMilliseconds, before.Epoch, before.Sequence, gitInput, gitProcesses);
        var result = new SourceState(revision, value, changes.Count != 0, preview) { ChangedPathCount = changes.Count, Inspection = metrics };
        capture?.Invoke(new(before with { }, revision, [], "", "", w.Path, w.ProjectPath, w.Snapshot.Reference!, [.. w.ManagedPaths], actualFiles == files.Count)
        { Paged = new(files.Hash, index, interpretationHash, cachePath, changedRoot) });
        string measurements = Path.Combine(Path.GetDirectoryName(cachePath)!, "source-inspections.jsonl"); PlainPath(measurements);
        await File.AppendAllTextAsync(measurements, JsonSerializer.Serialize(new { workspaceId = w.Id, fingerprint = value, measuredAt = DateTimeOffset.UtcNow, inspection = metrics }, Json) + "\n", ct);
        return result;
    }

    private async Task VerifyPagedSourceAsync(WorkspaceRecord w, SourceProof proof, bool staging, CancellationToken ct)
    {
        await EnsureRegistered(w, ct); RequireView(w, proof.View);
        if (w.Path != proof.Path || w.ProjectPath != proof.ProjectPath || w.Snapshot!.Reference != proof.Snapshot || !w.ManagedPaths.SequenceEqual(proof.ManagedPaths))
            throw Error("SourceChanged", "The checkpoint source identity changed.");
        string index = IndexDigest(w, ct), revision = await Required(w.Path, ["rev-parse", "HEAD"], ct);
        using var files = new PagedStrings(GitRecords(w.Path, ["ls-files", "--cached", "--others", "--exclude-standard", "-z"], ct).Where(path => !Managed(path, w.ManagedPaths)), ct);
        string interpretation = await PagedInterpretation(w.Path, files.Paths(), null, ct);
        using var finalFiles = new PagedStrings(GitRecords(w.Path, ["ls-files", "--cached", "--others", "--exclude-standard", "-z"], ct).Where(path => !Managed(path, w.ManagedPaths)), ct);
        if (!staging && index != proof.Paged!.IndexHash || revision != proof.Revision || files.Hash != proof.Paged!.FilesHash ||
            interpretation != proof.Paged.InterpretationHash || index != IndexDigest(w, ct) || revision != await Required(w.Path, ["rev-parse", "HEAD"], ct) ||
            files.Hash != finalFiles.Hash || interpretation != await PagedInterpretation(w.Path, finalFiles.Paths(), null, ct))
            throw Error("SourceChanged", "Source membership or interpretation changed while staging.");
        using var current = await sourceChanges!.ObserveChangesAsync(w, ct); RequireView(w, current);
        if (!SameGeneration(current, proof.View)) throw Error("SourceChanged", "Source changed during checkpoint verification.");
    }

    private static async Task<string> PagedInterpretation(string project, IEnumerable<string> files, string? config, CancellationToken ct)
    {
        using var digest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        config ??= await Required(project, ["config", "--null", "--list"], ct); digest.AppendData(Encoding.UTF8.GetBytes(config));
        foreach (string[] batch in AttributeBatches(files))
            await AttributeOutputAsync(project, batch, cached: false, all: true, output => digest.AppendData(Encoding.UTF8.GetBytes(output)), ct);
        return Convert.ToHexStringLower(digest.GetHashAndReset());
    }
    private async Task<Dictionary<string, (SnapshotContentSummary Summary, string ObjectId)>?> InitialBatch(WorkspaceRecord w,
        InitialContentAdmission admission, SourceChangeView before, string[] batch, PagedStrings baseline, PagedStrings changes, CancellationToken ct)
    {
        var result = new Dictionary<string, (SnapshotContentSummary, string)>(StringComparer.Ordinal);
        var observed = await ((ISourceContentSummaryProvider)sourceChanges!).ObserveInitialContentAsync(w, before, batch, ct);
        using var ownedGeneration = ReferenceEquals(observed?.Generation, before) ? null : observed?.Generation;
        if (observed is null) return null;
        if (observed.Snapshot != admission.Snapshot || !SameGeneration(observed.Generation, before) || observed.Files is null || observed.Files.Length > batch.Length)
            throw Error("SourceObservationInvalid", "Initial content evidence identifies another generation.");
        var requested = batch.ToHashSet(StringComparer.Ordinal);
        foreach (var summary in observed.Files)
        {
            if (summary is null || !requested.Remove(summary.Path) || summary.Length < 0 || summary.Sha256 is not { Length: 64 } ||
                !summary.Sha256.All(char.IsAsciiHexDigit) || changes.Affects(summary.Path)) throw Error("SourceObservationInvalid", "Initial content evidence is not an unchanged requested file.");
            byte[]? blob = baseline.Find(summary.Path);
            if (blob is null) throw Error("SourceObservationInvalid", "Initial content evidence is absent from its fixed tree.");
            string[] identity = Encoding.ASCII.GetString(blob).Split('|');
            if (identity.Length != 2 || !long.TryParse(identity[1], NumberStyles.None, CultureInfo.InvariantCulture, out long length) || length != summary.Length)
                throw Error("SourceObservationInvalid", "Initial content evidence differs from its fixed blob.");
            result.Add(summary.Path, (summary, identity[0]));
        }
        return result;
    }
    private static PagedFileHash ParseCached(DirectoryMetadataRecord item)
    {
        int separator = item.SourceReference?.IndexOf(':') ?? -1;
        if (separator is not (40 or 64) || item.SourceReference!.Length != separator + 65 || item.ContentHash is null)
            throw Error("SourceCacheRevalidationRequired", "The cached source identity is incomplete.");
        if (!item.SourceReference[..separator].All(char.IsAsciiHexDigit) || !IsInterpretationHash(item.SourceReference[(separator + 1)..]))
            throw Error("SourceCacheRevalidationRequired", "The cached source identity has invalid hash fields.");
        return new(item.Entry.Path, item.Entry.Length, item.ContentHash, item.SourceReference[..separator], item.SourceReference[(separator + 1)..]);
    }
    private static MetadataEntry EncodePagedHash(PagedFileHash item)
    {
        byte[] git = Convert.FromHexString(item.GitHash), raw = Convert.FromHexString(item.Hash), interpretation = Convert.FromHexString(item.Interpretation);
        if (git.Length is not (20 or 32) || raw.Length != 32 || interpretation.Length != 32 || item.Length < 0) throw Error("SourceObservationInvalid", "A source hash record is invalid.");
        byte[] bytes = new byte[73 + git.Length]; BinaryPrimitives.WriteInt64LittleEndian(bytes, item.Length); raw.CopyTo(bytes, 8);
        bytes[40] = (byte)git.Length; git.CopyTo(bytes, 41); interpretation.CopyTo(bytes, 41 + git.Length); return new(item.Path, bytes);
    }
    private static PagedFileHash DecodePagedHash(MetadataEntry entry)
    {
        var bytes = entry.Value.Span;
        if (bytes.Length < 93 || bytes[40] is not (20 or 32) || bytes.Length != 73 + bytes[40] || BinaryPrimitives.ReadInt64LittleEndian(bytes) < 0)
            throw Error("SourceObservationInvalid", "A source hash spool record is corrupt.");
        return new(entry.Key, BinaryPrimitives.ReadInt64LittleEndian(bytes), Convert.ToHexStringLower(bytes.Slice(8, 32)),
            Convert.ToHexStringLower(bytes.Slice(41, bytes[40])), Convert.ToHexStringLower(bytes.Slice(41 + bytes[40], 32)));
    }
    private static IEnumerable<MetadataEntry> BaselineEntries(string project, string revision, CancellationToken ct)
    {
        foreach (string row in GitRecords(project, ["ls-tree", "-r", "-l", "-z", revision], ct))
        {
            int tab = row.IndexOf('\t'); if (tab < 0) throw Error("GitBasisObjectInvalid", "The fixed source tree is malformed.");
            string[] fields = row[..tab].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length != 4 || fields[0] is not ("100644" or "100755") || fields[1] != "blob" || fields[2].Length is not (40 or 64) ||
                !fields[2].All(char.IsAsciiHexDigit) || !long.TryParse(fields[3], NumberStyles.None, CultureInfo.InvariantCulture, out long length) || length < 0)
                throw Error("GitBasisObjectInvalid", "The fixed initial tree has invalid ordinary blob metadata.");
            yield return new(row[(tab + 1)..], Encoding.ASCII.GetBytes(fields[2] + "|" + fields[3]));
        }
    }
    private static IEnumerable<MetadataEntry> Differences(PagedStrings head, PagedStrings actual)
    {
        using var a = head.Entries().GetEnumerator(); using var b = actual.Entries().GetEnumerator(); bool haveA = a.MoveNext(), haveB = b.MoveNext();
        while (haveA || haveB)
        {
            int order = !haveA ? 1 : !haveB ? -1 : StringComparer.Ordinal.Compare(a.Current.Key, b.Current.Key);
            if (order == 0)
            {
                string expected = Encoding.ASCII.GetString(a.Current.Value.Span).Split('|')[0];
                if (Encoding.ASCII.GetString(b.Current.Value.Span) != expected) yield return new(a.Current.Key, ReadOnlyMemory<byte>.Empty);
                haveA = a.MoveNext(); haveB = b.MoveNext();
            }
            else if (order < 0) { yield return new(a.Current.Key, ReadOnlyMemory<byte>.Empty); haveA = a.MoveNext(); }
            else { yield return new(b.Current.Key, ReadOnlyMemory<byte>.Empty); haveB = b.MoveNext(); }
        }
    }
    private static string IndexDigest(WorkspaceRecord w, CancellationToken ct)
    {
        using var digest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (string row in GitRecords(w.Path, ["ls-files", "--stage", "-z"], ct))
        {
            int tab = row.IndexOf('\t');
            if (tab < 0 || !row.StartsWith("100644 ", StringComparison.Ordinal) && !row.StartsWith("100755 ", StringComparison.Ordinal))
                throw Error("SnapshotSourceUnsupported", "Only ordinary resolved files may enter a source index.");
            if (Managed(row[(tab + 1)..], w.ManagedPaths)) throw Error("ManagedPathTracked", "Generated files entered the source index.");
            digest.AppendData(Encoding.UTF8.GetBytes(row)); digest.AppendData([0]);
        }
        return Convert.ToHexStringLower(digest.GetHashAndReset());
    }
    private static string PagedManifestHash(PagedSourceManifest manifest) => Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(manifest, Json)));
    private static async Task<PagedSourceManifest?> ReadPagedManifest(string path, string pages, CancellationToken ct)
    {
        PlainPath(path); if (!File.Exists(path)) return null;
        try
        {
            await using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var document = await JsonDocument.ParseAsync(input, cancellationToken: ct);
            if (document.RootElement.TryGetProperty("value", out var value) && value.TryGetProperty("version", out var version) &&
                version.TryGetInt32(out int found) && found != 4) throw Error("SourceCacheVersionUnsupported", "Only paged source cache version 4 is supported; preserve older state.");
            var envelope = document.RootElement.Deserialize<PagedSourceEnvelope>(Json);
            if (envelope?.Value is not { } manifest || manifest.Version != 4 || manifest.Snapshot is null || manifest.Sequence < 0 ||
                string.IsNullOrWhiteSpace(manifest.WorkspaceId) || string.IsNullOrWhiteSpace(manifest.Epoch) || !IsInterpretationHash(manifest.Configuration) ||
                envelope.Sha256 != PagedManifestHash(manifest)) throw new JsonException();
            using var catalog = new PagedSnapshotCatalog(pages, manifest.Catalog); long count = 0;
            foreach (var entry in catalog.Walk())
            { ct.ThrowIfCancellationRequested(); count++; if (!entry.Entry.Directory) _ = ParseCached(entry); }
            if (count != manifest.Catalog.Count) throw Error("SourceCacheRevalidationRequired", "The cached source catalog membership changed.");
            using var changed = new PagedSnapshotCatalog(pages, manifest.ChangedPaths); changed.Verify(ct); return manifest;
        }
        catch (Exception ex) when (ex is JsonException || ex is WorkspaceException failure && failure.Code is not "SourceCacheVersionUnsupported")
        { throw Error("SourceCacheRevalidationRequired", "Preserve the invalid source cache and audit the current source."); }
    }
    internal static IEnumerable<string> GitRecords(string project, string[] args, CancellationToken ct)
    {
        var start = new ProcessStartInfo("git") { WorkingDirectory = project, UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (string key in start.Environment.Keys.Where(key => key.StartsWith("GIT_", StringComparison.OrdinalIgnoreCase)).ToArray()) start.Environment.Remove(key);
        start.Environment["GIT_OPTIONAL_LOCKS"] = "0"; start.Environment["GIT_TERMINAL_PROMPT"] = "0";
        if (OperatingSystem.IsWindows()) { start.ArgumentList.Add("-c"); start.ArgumentList.Add("core.longpaths=true"); }
        foreach (string arg in args) start.ArgumentList.Add(arg);
        using var process = Process.Start(start) ?? throw Error("GitUnavailable", "Git did not start.");
        var error = DrainGitError(process.StandardError); process.StandardInput.Close();
        void Abort() { try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { } }
        using var cancel = ct.Register(Abort);
        // Status/diff may inspect an arbitrarily large unchanged tree before
        // emitting their first record (or only EOF). Silence does not identify
        // a stalled protocol read here. Caller cancellation still terminates
        // and drains this exact owned command and its output pipes below.
        using var reader = new StreamReader(process.StandardOutput.BaseStream, new UTF8Encoding(false, true), false, 65536, true);
        var record = new StringBuilder(); char[] buffer = new char[4096];
        try
        {
            for (int n; (n = reader.Read(buffer)) != 0;)
                for (int i = 0; i < n; i++)
                    if (buffer[i] == '\0') { if (record.Length != 0) yield return record.ToString(); record.Clear(); }
                    else
                    {
                        if (record.Length >= 32767 + 1024) throw Error("GitOutputInvalid", "A Git record exceeds the supported filesystem path and protocol header bounds.");
                        record.Append(buffer[i]);
                    }
            ct.ThrowIfCancellationRequested(); if (record.Length != 0) throw Error("GitOutputInvalid", "The Git record stream ended inside a record.");
            process.WaitForExitAsync(ct).GetAwaiter().GetResult();
            if (process.ExitCode != 0) throw Error("GitCommandFailed", error.GetAwaiter().GetResult());
        }
        finally { Abort(); if (!process.HasExited) process.WaitForExit(); _ = error.GetAwaiter().GetResult(); }
    }
    private static async Task<string> DrainGitError(StreamReader reader)
    {
        char[] buffer = new char[4096]; var first = new StringBuilder();
        for (int n; (n = await reader.ReadAsync(buffer)) != 0;)
            if (first.Length < 16384) first.Append(buffer, 0, Math.Min(n, 16384 - first.Length));
        return first.ToString().Trim();
    }
    // The source file list is already ordinal-sorted. Stream the fixed prior
    // catalog once and merge it, rather than reopening its search path for
    // every unchanged file. External sorting preserves bounded memory and
    // handles the different directory/component traversal order.
    internal sealed class CachedFileCursor : IDisposable
    {
        private readonly IEnumerator<MetadataEntry> entries;
        private readonly CancellationToken ct;
        private bool more;
        private long count;
        private string? previous;
        internal CachedFileCursor(PagedSnapshotCatalog catalog, string scratch, CancellationToken ct)
        {
            this.ct = ct;
            IEnumerable<MetadataEntry> Files()
            {
                foreach (var item in catalog.Walk())
                {
                    ct.ThrowIfCancellationRequested(); if (item.Entry.Directory) continue;
                    var parsed = ParseCached(item);
                    yield return EncodePagedHash(new(item.Entry.Path, item.Entry.Length, item.ContentHash!, parsed.GitHash, parsed.Interpretation));
                }
            }
            entries = MetadataExternalSort.Order(Files(), scratch, ct).GetEnumerator();
            try { Advance(); } catch { entries.Dispose(); throw; }
        }
        private void Advance() { ct.ThrowIfCancellationRequested(); more = entries.MoveNext(); if (more) count++; }
        internal DirectoryMetadataRecord? FindNext(string path)
        {
            if (previous is not null && StringComparer.Ordinal.Compare(previous, path) >= 0) throw Error("SourceObservationInvalid", "Cached source merge requires unique sorted paths.");
            previous = path;
            while (more && StringComparer.Ordinal.Compare(entries.Current.Key, path) < 0) Advance();
            if (!more || entries.Current.Key != path) return null;
            var file = DecodePagedHash(entries.Current);
            return new(new(file.Path, false, file.Length, 32, 0, 0, 0), file.Hash, SourceReference: file.GitHash + ":" + file.Interpretation);
        }
        internal long Complete() { while (more) Advance(); return count; }
        public void Dispose() => entries.Dispose();
    }

    internal sealed class PagedStrings : IDisposable
    {
        private readonly TemporaryMetadataDirectory? directory;
        private PagedSnapshotCatalog? catalog;
        private readonly MetadataSpool? ordered;
        private readonly long count;
        private readonly bool ignoreCase;
        private readonly CancellationToken cancellationToken;
        private bool disposed;
        public long Count => count;
        public string Hash { get; } = EmptyPathInterpretation;
        public PagedStrings(IEnumerable<string> paths, CancellationToken ct, bool ignoreCase = false)
            : this(paths.Select(path => new MetadataEntry(ignoreCase ? path.ToUpperInvariant() : path, ReadOnlyMemory<byte>.Empty)), ct) => this.ignoreCase = ignoreCase;
        public PagedStrings(IEnumerable<MetadataEntry> entries, CancellationToken ct)
        {
            cancellationToken = ct;
            using var cursor = entries.GetEnumerator();
            // Empty comparisons need no scratch directory, owner publication
            // or page store. Consume/close the input command before returning;
            // a later boundary still observes its own fresh complete input.
            ct.ThrowIfCancellationRequested();
            if (!cursor.MoveNext()) { ct.ThrowIfCancellationRequested(); return; }
            directory = new();
            ordered = new(directory.Path);
            try
            {
                long uniqueCount = 0; MetadataEntry? prior = null;
                foreach (var entry in MetadataExternalSort.Order(Remaining(), directory.Path, ct))
                {
                    if (prior?.Key == entry.Key)
                    { if (!prior.Value.Span.SequenceEqual(entry.Value.Span)) throw Error("SourceObservationInvalid", "One path names multiple source identities."); continue; }
                    SnapshotCatalogInput.ValidatePathMarker(entry, caseSensitive: true);
                    prior = entry; uniqueCount = checked(uniqueCount + 1); ordered.Append(entry);
                }
                // This hash is private to one inspection/proof; no persisted
                // format changes. Frame checked, ordinal-sorted spool bytes,
                // not a second hierarchy built only to compare two lists.
                using var digest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                Span<byte> framing = stackalloc byte[8];
                foreach (var entry in ordered.Read())
                {
                    ct.ThrowIfCancellationRequested(); byte[] key = Encoding.UTF8.GetBytes(entry.Key);
                    BinaryPrimitives.WriteInt32LittleEndian(framing, key.Length);
                    BinaryPrimitives.WriteInt32LittleEndian(framing[4..], entry.Value.Length);
                    digest.AppendData(framing); digest.AppendData(key); digest.AppendData(entry.Value.Span);
                }
                Hash = Convert.ToHexStringLower(digest.GetHashAndReset()); count = uniqueCount;
            }
            catch { ordered.Dispose(); try { directory.Dispose(); } catch (IOException) { } throw; }
            IEnumerable<MetadataEntry> Remaining()
            { do { yield return cursor.Current; } while (cursor.MoveNext()); }
        }
        public byte[]? Find(string path)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (ordered is null) return null;
            if (catalog is null)
            {
                var descriptor = PagedSnapshotCatalog.Build(directory!.Path, new("", true, 0, 16, 0, 0, 0),
                    SnapshotCatalogInput.WithPathMarkers(Entries(), directory.Path, caseSensitive: true, ct: cancellationToken),
                    caseSensitive: true, ct: cancellationToken);
                catalog = new(directory.Path, descriptor);
            }
            path = ignoreCase ? path.ToUpperInvariant() : path; var entry = catalog.Find(path);
            return entry is not null && entry.SourceReference?.StartsWith("v:", StringComparison.Ordinal) == true && entry.Entry.Path == path
                ? Convert.FromBase64String(entry.SourceReference[2..]) : null;
        }
        public IEnumerable<MetadataEntry> Entries()
        {
            ObjectDisposedException.ThrowIf(disposed, this); cancellationToken.ThrowIfCancellationRequested();
            if (ordered is null) yield break;
            foreach (var entry in ordered.Read()) { cancellationToken.ThrowIfCancellationRequested(); yield return entry; }
        }
        public IEnumerable<string> Paths() => Entries().Select(entry => entry.Key);
        public bool Affects(string path)
        {
            while (true)
            { if (Find(path) is not null) return true; int slash = path.LastIndexOf('/'); if (slash < 0) return false; path = path[..slash]; }
        }
        public void Dispose()
        {
            if (disposed) return; disposed = true;
            try { catalog?.Dispose(); } finally { try { ordered?.Dispose(); } finally { directory?.Dispose(); } }
        }
    }
}
