using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Splinter.Core;

namespace Splinter.Runtime;

public sealed partial class JournalDeltaStore
{
    private const string ExportIntentName = "candidate-export.json";
    private static readonly JsonSerializerOptions PublicationJson = new()
    {
        RespectRequiredConstructorParameters = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    // Only an explicit stable request may continue its durable intent. All
    // publication files are create-new or no-overwrite moves; no repair deletes.
    public static ImmutableCandidate PublishCandidate(string sourceDirectory, OverlayIdentity identity,
        ISnapshotTree snapshot, string destination, CandidateExportRequest request, string[] forbiddenTopLevel,
        CandidateSourceProjection? projection = null, Action<string>? boundary = null)
    {
        long started = Stopwatch.GetTimestamp();
        ValidateExportRequest(request);
        sourceDirectory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(sourceDirectory));
        destination = Path.TrimEndingDirectorySeparator(Path.GetFullPath(destination));
        WorkspaceStore.EnsurePlainPath(sourceDirectory); WorkspaceStore.EnsurePlainPath(destination);
        string separator = Path.DirectorySeparatorChar.ToString();
        if (sourceDirectory.Equals(destination, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) ||
            sourceDirectory.StartsWith(destination + separator, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) ||
            destination.StartsWith(sourceDirectory + separator, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            throw new WorkspaceException("UnsafePath", "Candidate and source must be separate trees.");
        if (identity.Snapshot != snapshot.Snapshot)
            throw new WorkspaceException("SnapshotIdentityMismatch", "Publication requires the exact snapshot tuple.");
        if (projection is not null) ValidateProjection(projection, identity);
        var policy = WorkspacePathPolicy.For(identity.Snapshot);
        string[] forbidden = forbiddenTopLevel.Concat(identity.ExcludedTopLevel ?? [])
            .Select(p => policy.CaseSensitive ? p : p.ToLowerInvariant()).Distinct(policy.Comparer).Order(policy.Comparer).ToArray();
        if (forbidden.Any(p => string.IsNullOrWhiteSpace(p) || policy.Normalize(p) != p || p.Contains('/')))
            throw new WorkspaceException("CandidateRequestInvalid", "Forbidden paths must be nonempty top-level names.");

        string lockPath = destination + ".publication.lock";
        WorkspaceStore.EnsurePlainPath(lockPath);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        using var publicationLock = LockCandidatePublication(lockPath);
        ValidatePublicationFiles(destination);
        CandidateExportIntent? intent = ReadPublicationFile<CandidateExportIntent>(Path.Combine(destination, ExportIntentName))
            ?? ReadPublicationFile<CandidateExportIntent>(Path.Combine(destination, ExportIntentName + ".new"));
        JournalDeltaStore? source = null;
        try
        {
            if (intent is null)
            {
                if (Directory.Exists(destination) || File.Exists(destination))
                    throw new WorkspaceException("CandidatePublicationUnowned", "An existing destination has no complete publication intent; preserve it.");
                source = new(sourceDirectory, identity, snapshot, "delta.journal", true);
                var files = QualifyPublicationSource(source, projection, forbidden);
                var live = source.LiveRanges(files);
                long estimate = source.WalkFrames(live, null, files);
                if (estimate > identity.MaxJournalBytes)
                    throw new WorkspaceException("DeltaFull", "The sealed candidate exceeds the source journal budget.");
                intent = new(1, request, sourceDirectory, destination, identity, forbidden,
                    source.SealCurrent(), source.StateHash(files), estimate, DateTimeOffset.UtcNow, projection);
                Directory.CreateDirectory(destination);
                WritePublicationFile(Path.Combine(destination, ExportIntentName + ".new"), intent);
                boundary?.Invoke("intent-flushed");
            }
            ValidateExportIntent(intent, request, sourceDirectory, destination, identity, forbidden, projection);
            PromotePublicationFile(destination, ExportIntentName);
            boundary?.Invoke("intent-published");

            string identityPath = Path.Combine(destination, "identity.json");
            var savedIdentity = ReadPublicationFile<OverlayIdentity>(identityPath)
                ?? ReadPublicationFile<OverlayIdentity>(identityPath + ".new");
            if (savedIdentity is not null && !SamePublicationValue(savedIdentity, identity))
                throw new WorkspaceException("CandidatePublicationMismatch", "The candidate identity differs from its intent.");

            // A full receipt is authoritative only after replay, state, and seal
            // checks. No current worker handle is needed for a historical retry.
            var receipt = ReadPublicationFile<ImmutableCandidate>(Path.Combine(destination, "candidate.json"))
                ?? ReadPublicationFile<ImmutableCandidate>(Path.Combine(destination, "candidate.json.new"));
            if (receipt is not null)
            {
                ValidatePublicationReceipt(receipt, intent);
                if (savedIdentity is null || !File.Exists(identityPath))
                    throw new WorkspaceException("CandidatePublicationCorrupt", "A receipt requires the published identity; preserve all files.");
                using var complete = VerifyPublicationJournal(destination, "delta.journal", snapshot, intent);
                if (complete.SealCurrent() != receipt.Seal)
                    throw new WorkspaceException("CandidateSealMismatch", "The receipt journal changed; preserve all publication files.");
                PromotePublicationFile(destination, "candidate.json");
                boundary?.Invoke("receipt-published");
                return receipt;
            }

            string journalPath = Path.Combine(destination, "delta.journal"), pendingJournal = journalPath + ".new";
            long pendingLength = File.Exists(pendingJournal) ? new FileInfo(pendingJournal).Length : -1;
            if (pendingLength > intent.JournalBytes)
                throw new WorkspaceException("CandidatePublicationCorrupt", "The partial journal exceeds the frozen publication length.");
            HashSet<string>? projectedFiles = null;
            bool needsSource = !File.Exists(journalPath) && pendingLength != intent.JournalBytes;
            if (needsSource)
            {
                source ??= new(sourceDirectory, identity, snapshot, "delta.journal", true);
                if (source.SealCurrent() != intent.SourceSeal)
                    throw new WorkspaceException("CandidateSourceChanged", "Only the original sealed worker can finish a partial journal; preserve both generations.");
                projectedFiles = QualifyPublicationSource(source, projection, forbidden);
                if (source.StateHash(projectedFiles) != intent.StateHash)
                    throw new WorkspaceException("CandidateSourceChanged", "The worker state differs from the frozen intent.");
            }
            if (savedIdentity is null)
            {
                if (File.Exists(Path.Combine(destination, "delta.journal")) || File.Exists(Path.Combine(destination, "delta.journal.new")))
                    throw new WorkspaceException("CandidatePublicationCorrupt", "A journal without its identity cannot be adopted.");
                WritePublicationFile(identityPath + ".new", identity);
                boundary?.Invoke("identity-flushed");
            }
            PromotePublicationFile(destination, "identity.json");
            boundary?.Invoke("identity-published");

            if (!File.Exists(journalPath))
            {
                if (needsSource)
                {
                    source!.WritePublicationJournal(pendingJournal, intent, projectedFiles);
                    boundary?.Invoke("journal-flushed");
                }
                using (VerifyPublicationJournal(destination, "delta.journal.new", snapshot, intent)) { }
                PromotePublicationFile(destination, "delta.journal");
                boundary?.Invoke("journal-published");
            }
            using var check = VerifyPublicationJournal(destination, "delta.journal", snapshot, intent);
            boundary?.Invoke("candidate-flushed");
            var result = new ImmutableCandidate(3, request.RequestKey, identity, intent.SourceSeal, check.SealCurrent(),
                DateTimeOffset.UtcNow, Stopwatch.GetTimestamp() - started, Stopwatch.Frequency, projection);
            WritePublicationFile(Path.Combine(destination, "candidate.json.new"), result);
            boundary?.Invoke("receipt-flushed");
            PromotePublicationFile(destination, "candidate.json");
            boundary?.Invoke("receipt-published");
            return result;
        }
        finally { source?.Dispose(); }
    }

    private static FileStream LockCandidatePublication(string path)
    {
        FileStream file;
        try { file = new(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
        catch (IOException) { throw new WorkspaceException("CandidatePublicationBusy", "Another publisher or file handle occupies this destination; retry after it closes."); }
        if (file.Length == 0) return file;
        file.Dispose();
        throw new WorkspaceException("CandidatePublicationUnowned", "Preserve a nonempty publication lock file.");
    }

    private static void ValidateExportRequest(CandidateExportRequest request)
    {
        if (request is null || request.Version != 1 || request.RequestKey is not { Length: > 0 and <= 128 } key ||
            !char.IsAsciiLetterOrDigit(key[0]) || key.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not ('.' or '_' or ':' or '-')))
            throw new WorkspaceException("CandidateRequestInvalid", "Use a current request and a stable 1-128 character request key.");
    }

    private static void ValidateExportIntent(CandidateExportIntent intent, CandidateExportRequest request,
        string source, string destination, OverlayIdentity identity, string[] forbidden, CandidateSourceProjection? projection)
    {
        if (intent.Version != 1 || intent.SourceSeal is null || !ValidSeal(intent.SourceSeal) ||
            intent.StateHash is not { Length: 64 } hash || !hash.All(Uri.IsHexDigit) || intent.JournalBytes < 0 ||
            intent.JournalBytes > identity.MaxJournalBytes || intent.StartedAt == default)
            throw new WorkspaceException("CandidatePublicationCorrupt", "Unsupported or incomplete publication intent; preserve it.");
        if (intent.Request != request || intent.SourceDirectory != source || intent.Destination != destination ||
            !SamePublicationValue(intent.Identity, identity) || !SamePublicationValue(intent.ForbiddenTopLevel, forbidden) ||
            !SamePublicationValue(intent.SourceProjection, projection))
            throw new WorkspaceException("CandidatePublicationMismatch", "Retry the exact request, paths, owner, snapshot, limits and projection evidence.");
    }

    private static HashSet<string>? QualifyPublicationSource(JournalDeltaStore source, CandidateSourceProjection? projection, string[] forbidden)
    {
        var files = projection is null ? null : source.QualifyProjection(projection);
        if (source.pathChanges.Keys.Any(p => forbidden.Contains(p.Split('/')[0], source.pathPolicy.Comparer) && files?.Contains(p) != true))
            throw new WorkspaceException("CandidateContainsManagedPaths", "The source contains unqualified managed-file or cache writes.");
        return files;
    }

    private static JournalDeltaStore VerifyPublicationJournal(string directory, string name, ISnapshotTree snapshot, CandidateExportIntent intent)
    {
        var check = new JournalDeltaStore(directory, intent.Identity, snapshot, name, true);
        try
        {
            if (check.journal.Length != intent.JournalBytes || check.sequence != intent.SourceSeal.DurableSequence ||
                check.StateHash() != intent.StateHash || intent.SourceProjection is { } proof && proof.Files.Any(f => check.Stat(f.Path) is not null))
                throw new WorkspaceException("CandidateStateMismatch", "Candidate replay differs from the frozen source state; preserve it.");
            return check;
        }
        catch { check.Dispose(); throw; }
    }

    private static void ValidatePublicationReceipt(ImmutableCandidate receipt, CandidateExportIntent intent)
    {
        if (receipt.Version != 3 || receipt.Id != intent.Request.RequestKey ||
            !SamePublicationValue(receipt.Identity, intent.Identity) || receipt.SourceSeal != intent.SourceSeal ||
            !SamePublicationValue(receipt.SourceProjection, intent.SourceProjection) || receipt.Seal is null || !ValidSeal(receipt.Seal) ||
            receipt.Seal.DurableSequence != intent.SourceSeal.DurableSequence || receipt.Seal.JournalBytes != intent.JournalBytes ||
            receipt.CreatedAt == default || receipt.CreationTicks < 0 || receipt.StopwatchFrequency <= 0)
            throw new WorkspaceException("CandidatePublicationMismatch", "The receipt differs from its publication intent; preserve both.");
    }

    private static bool SamePublicationValue<T>(T left, T right) => JsonSerializer.Serialize(left) == JsonSerializer.Serialize(right);

    private static void ValidatePublicationFiles(string directory)
    {
        if (!Directory.Exists(directory)) return;
        string[] names = [ExportIntentName, "identity.json", "delta.journal", "candidate.json"];
        foreach (string path in Directory.EnumerateFileSystemEntries(directory))
        {
            string name = Path.GetFileName(path);
            WorkspaceStore.EnsurePlainPath(path);
            if (Directory.Exists(path) || !names.Any(n => name == n || name == n + ".new") ||
                name.EndsWith(".new", StringComparison.Ordinal) && File.Exists(path[..^4]))
                throw new WorkspaceException("CandidatePublicationCorrupt", "Unknown or competing publication files must be preserved.");
        }
    }

    private static T? ReadPublicationFile<T>(string path) where T : class
    {
        try
        {
            using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
            if (file.Length > 1024 * 1024) throw new JsonException("Publication metadata exceeds its bound.");
            using var document = JsonDocument.Parse(file);
            if (!JsonPropertyValidation.HasUniqueProperties(document.RootElement))
                throw new JsonException("Duplicate publication field.");
            return document.RootElement.Deserialize<T>(PublicationJson) ?? throw new JsonException("Missing publication metadata.");
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException) { return null; }
        catch (JsonException) { throw new WorkspaceException("CandidatePublicationCorrupt", "Malformed, unsupported or incomplete metadata must be preserved."); }
    }

    private static void WritePublicationFile<T>(string path, T value)
    {
        using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        JsonSerializer.Serialize(output, value); output.Flush(true);
    }

    private static void PromotePublicationFile(string directory, string name)
    {
        string pending = Path.Combine(directory, name + ".new"), path = Path.Combine(directory, name);
        // This lifecycle never replaces a published generation.
        if (File.Exists(pending)) File.Move(pending, path);
    }

    private void WritePublicationJournal(string path, CandidateExportIntent intent, IReadOnlySet<string>? files)
    {
        var live = LiveRanges(files);
        if (WalkFrames(live, null, files) != intent.JournalBytes)
            throw new WorkspaceException("CandidateSourceChanged", "The planned candidate length differs from its intent.");
        long existing = File.Exists(path) ? new FileInfo(path).Length : 0;
        if (new DriveInfo(Path.GetPathRoot(path)!).AvailableFreeSpace < checked(intent.JournalBytes - existing + 2L * 1024 * 1024 * 1024))
            throw new WorkspaceException("CompactionHeadroomRequired", "Publication needs remaining journal space and safety headroom.");
        using (var output = new CandidatePrefixWriter(path))
        {
            long actual = WalkFrames(live, output, files);
            if (actual != intent.JournalBytes || output.Length != actual)
                throw new WorkspaceException("CandidateStateMismatch", "The candidate length changed during publication.");
            output.Flush();
        }
        if (SealCurrent() != intent.SourceSeal)
            throw new WorkspaceException("CandidateSourceChanged", "The worker changed during publication; preserve both generations.");
    }
}
