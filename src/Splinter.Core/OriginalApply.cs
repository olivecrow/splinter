using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Splinter.Core;

public interface IOriginalSourceLease : IDisposable { string Identity { get; } }

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record OriginalFileVersion([property: JsonRequired] string Kind, [property: JsonRequired] long Length,
    [property: JsonRequired] string? Hash, [property: JsonRequired] int Attributes, [property: JsonRequired] long ModifiedUtcTicks,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] uint? UnixMode = null)
{
    // Archive and timestamps change as a consequence of content writes. They
    // are not permission to overwrite a later original content change.
    public bool SameContent(OriginalFileVersion? other) => other is not null &&
        Kind == other.Kind && Length == other.Length && Hash == other.Hash;
    [JsonIgnore] public int RelevantAttributes => Attributes & ~(32 | 128 | 16);
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record OriginalFileObservation([property: JsonRequired] string Identity, [property: JsonRequired] OriginalFileVersion Version);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record OriginalApplyChange([property: JsonRequired] string Path, [property: JsonRequired] OriginalFileVersion? Before,
    [property: JsonRequired] OriginalFileVersion? After, [property: JsonRequired] OriginalFileObservation? Expected, [property: JsonRequired] bool AlreadyMatches);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record OriginalApplyPlan([property: JsonRequired] int Version, [property: JsonRequired] string OwnerId,
    [property: JsonRequired] string RequestDigest, [property: JsonRequired] string Basis, [property: JsonRequired] string Result,
    [property: JsonRequired] string ProjectPath, [property: JsonRequired] string ProjectIdentity,
    [property: JsonRequired] OriginalApplyChange[] Changes, [property: JsonRequired] string[] Conflicts, [property: JsonRequired] string Digest)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? SourceIdentity { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string[]? SelectedPaths { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? ResultId { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? PreviousApplyDigest { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? LiveOriginalIdentity { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public MetadataTreeRoot? ChangesRoot { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public MetadataTreeRoot? PathsRoot { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public MetadataTreeRoot? PathNamesRoot { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public MetadataTreeRoot? ConflictsRoot { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public MetadataTreeRoot? SelectionRoot { get; init; }
    public long SelectionCount => SelectionRoot?.Count ?? SelectedPaths?.LongLength ?? 0;
    public long ConflictCount => ConflictsRoot?.Count ?? Conflicts.LongLength;
    [JsonIgnore] public IOriginalApplyPlanReader? PageReader { get; set; }
    public long ChangeCount => ChangesRoot?.Count ?? Changes.LongLength;
    public IEnumerable<OriginalApplyChange> EnumerateChanges() => ChangesRoot is null ? Changes :
        (PageReader ?? throw new WorkspaceException("OriginalApplyStateInvalid", "Bind the immutable plan pages before reading publication entries.")).Enumerate();
    public OriginalApplyChange? FindChange(string path) => ChangesRoot is null ? Changes.FirstOrDefault(c => c.Path.Equals(path, StringComparison.OrdinalIgnoreCase)) :
        (PageReader ?? throw new WorkspaceException("OriginalApplyStateInvalid", "The publication page reader is missing.")).Find(path);
    public OriginalApplyChange ChangeAt(long index) => ChangesRoot is null ? Changes[checked((int)index)] :
        (PageReader ?? throw new WorkspaceException("OriginalApplyStateInvalid", "The publication page reader is missing.")).At(index);
    public IEnumerable<string> EnumerateSelection() => SelectionRoot is null ? SelectedPaths ?? [] :
        (PageReader ?? throw new WorkspaceException("OriginalApplyStateInvalid", "The publication selection reader is missing.")).Selection();
    public string ComputeDigest()
    {
        var content = new Dictionary<string, object?> { [nameof(Version)] = Version, [nameof(OwnerId)] = OwnerId,
            [nameof(RequestDigest)] = RequestDigest, [nameof(Basis)] = Basis, [nameof(Result)] = Result,
            [nameof(ProjectPath)] = ProjectPath, [nameof(ProjectIdentity)] = ProjectIdentity, [nameof(Changes)] = Changes, [nameof(Conflicts)] = Conflicts };
        if (SourceIdentity is not null) content.Add(nameof(SourceIdentity), SourceIdentity);
        if (SelectionRoot is not null) content.Add(nameof(SelectedPaths), new { root = SelectionRoot.ContentHash, count = SelectionRoot.Count });
        else if (SelectedPaths is not null) content.Add(nameof(SelectedPaths), SelectedPaths);
        if (ResultId is not null) content.Add(nameof(ResultId), ResultId);
        if (PreviousApplyDigest is not null) content.Add(nameof(PreviousApplyDigest), PreviousApplyDigest);
        if (LiveOriginalIdentity is not null) content.Add(nameof(LiveOriginalIdentity), LiveOriginalIdentity);
        if (ChangesRoot is not null)
        {
            content[nameof(Changes)] = new { root = ChangesRoot.ContentHash, count = ChangesRoot.Count };
            content[nameof(PathsRoot)] = PathsRoot?.ContentHash;
            content[nameof(PathNamesRoot)] = PathNamesRoot?.ContentHash;
            content[nameof(Conflicts)] = ConflictsRoot is null ? Conflicts : new { root = ConflictsRoot.ContentHash, count = ConflictsRoot.Count };
        }
        return "sha256:" + Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(content)));
    }
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class OriginalApplyStep
{
    [JsonRequired] public string Phase { get; set; } = "pending";
    [JsonRequired] public string? StagedIdentity { get; set; }
    [JsonRequired] public long StagedBytes { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? StageNonce { get; set; }
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class OriginalApplyState
{
    public required OriginalApplyPlan Plan { get; init; }
    public required string IntentKey { get; init; }
    public required OriginalApplyStep[] Steps { get; init; }
    [JsonRequired] public bool Completed { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public MetadataTreeRoot? StepsRoot { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public MetadataTreeRoot? PreservedScratchRoot { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public MetadataTreeRoot? ScratchNamesRoot { get; set; }
    [JsonIgnore] public IOriginalApplyProgress? PageProgress { get; set; }
    public OriginalApplyStep StepAt(long index) => StepsRoot is null ? Steps[checked((int)index)] :
        (PageProgress ?? throw new WorkspaceException("OriginalApplyStateInvalid", "Bind publication progress pages before resuming.")).Get(index);
    public void SaveStep(long index, OriginalApplyStep step)
    {
        if (StepsRoot is null) { Steps[checked((int)index)] = step; return; }
        StepsRoot = (PageProgress ?? throw new WorkspaceException("OriginalApplyStateInvalid", "Publication progress pages are missing.")).Save(index, step);
    }
    public void RequireMatch(WorkspaceRecord w)
    {
        bool plain = w.SourceKind == "none" && w.StorageMode == "project-snapshot";
        bool plastic = w.SourceKind == "plastic" && w.StorageMode == "plastic-snapshot" && w.ApplicationKind is "none" or "unity";
        bool git = w.SourceKind == "git" && w.StorageMode == "git-snapshot" && w.Results.Count > 0;
        if (Plan is null || Plan.Version != (Plan.ChangesRoot is not null ? 3 : Plan.ResultId is null ? 1 : 2) || w.Purpose != "task" || !plain && !plastic && !git ||
            (plastic || git) && (Plan.SourceIdentity is not { Length: 71 } || !Plan.SourceIdentity.StartsWith("sha256:", StringComparison.Ordinal) ||
                Plan.SourceIdentity[7..].Any(c => c is not (>= '0' and <= '9' or >= 'a' and <= 'f'))) ||
            plain && Plan.SourceIdentity is not null ||
            (Plan.SelectionRoot is null ? Plan.SelectedPaths is { } selected && !ValidSelection(selected) :
                Plan.ChangesRoot is null || Plan.SelectionRoot.Count == 0 || Plan.SelectedPaths is null || Plan.SelectedPaths.Length > 100 || Plan.SelectedPaths.Any(path => !ValidSelectionPath(path))) ||
            Plan.OwnerId != w.Id || Plan.RequestDigest != w.RequestDigest || Plan.Basis != w.BaseRevision ||
            (Plan.ResultId is null ? Plan.Result != w.ResultRevision : !w.Results.Any(r => r.Id == Plan.ResultId && r.Revision == Plan.Result)) || Plan.ProjectPath != w.ProjectPath ||
            string.IsNullOrWhiteSpace(IntentKey) || IntentKey.Length > 128 || Plan.Changes is null ||
            Plan.Conflicts is null || Plan.ConflictCount != 0 ||
            string.IsNullOrWhiteSpace(Plan.ProjectIdentity) || Plan.Digest != Plan.ComputeDigest() || Steps is null ||
            (Plan.ChangesRoot is null ? StepsRoot is not null || Steps.Length != Plan.Changes.Length :
                StepsRoot is null || ChangesInlineInvalid() || PageProgress is null || Plan.PageReader is null))
            throw new WorkspaceException("OriginalApplyStateInvalid", "Preserve the mismatched or incomplete original-apply record.");
        long index = 0;
        // The persistent plan reader verifies ordinal keys and membership. Inline
        // fixtures retain their compatibility path; production paged plans never
        // create a project-sized duplicate-name set here.
        HashSet<string>? names = Plan.ChangesRoot is null ? new(StringComparer.OrdinalIgnoreCase) : null;
        using var progress = PageProgress?.Enumerate().GetEnumerator();
        bool hasProgress = progress?.MoveNext() == true;
        foreach (var change in Plan.EnumerateChanges())
        {
            OriginalApplyStep step;
            if (StepsRoot is null) step = Steps[checked((int)index)];
            else if (hasProgress && progress!.Current.Key == index)
            { step = progress.Current.Value; hasProgress = progress.MoveNext(); }
            else step = new();
            if (hasProgress && progress!.Current.Key < index)
                throw new WorkspaceException("OriginalApplyStateInvalid", "Publication progress keys are duplicated or out of order.");
            index++;
            if (!Valid(change) || names is not null && !names.Add(change.Path) || step is null ||
                step.Phase is not ("pending" or "staging" or "staged" or "retired" or "published" or "done") ||
                step.StagedBytes < 0 || step.StagedIdentity is not null && string.IsNullOrWhiteSpace(step.StagedIdentity) ||
                step.StageNonce is not null && (step.StageNonce.Length != 32 || step.StageNonce.Any(c => c is not (>= '0' and <= '9' or >= 'a' and <= 'f'))) ||
                Completed && step.Phase != "done")
                throw new WorkspaceException("OriginalApplyStateInvalid", "Preserve malformed publication entries or progress.");
            if (change.After is null && (step.StagedIdentity is not null || step.StagedBytes != 0 || step.Phase is "staging" or "staged" or "published"))
                throw new WorkspaceException("OriginalApplyStateInvalid", "Preserve impossible deletion publication receipts.");
            if ((step.Phase is "staged" or "published" || step.Phase == "done" && !change.AlreadyMatches) && change.After is not null &&
                (step.StagedIdentity is null || step.StagedBytes != change.After.Length))
                throw new WorkspaceException("OriginalApplyStateInvalid", "Preserve incomplete payload publication receipts.");
        }
        if (index != Plan.ChangeCount || hasProgress || StepsRoot is not null && StepsRoot.Count > Plan.ChangeCount)
            throw new WorkspaceException("OriginalApplyStateInvalid", "Publication membership and progress differ.");
        bool ChangesInlineInvalid() => Plan.Changes.Length != 0 || Steps.Length != 0;
    }
    public static bool ValidSelectionPath(string p) => !string.IsNullOrWhiteSpace(p) && p.Length <= 32767 && !p.Contains('\\') && !p.Contains(':') && !p.Contains('*') && !p.Contains('?') &&
            p.Split('/').All(s => s.Length > 0 && s is not ("." or "..") && !s.StartsWith(".splinter-apply-", StringComparison.OrdinalIgnoreCase) &&
                !new[] { ".git", ".plastic", ".hg", ".svn" }.Contains(s, StringComparer.OrdinalIgnoreCase));
    public static bool ValidSelection(string[] paths) => paths.Length > 0 && paths.All(ValidSelectionPath) &&
        paths.Distinct(StringComparer.OrdinalIgnoreCase).Count() == paths.Length && paths.SequenceEqual(paths.Order(StringComparer.Ordinal));
    private static bool Valid(OriginalApplyChange? c) => c is not null && !string.IsNullOrWhiteSpace(c.Path) &&
        !c.Path.Contains('\\') && !c.Path.Contains(':') && c.Path.Split('/').All(p => p.Length > 0 && p is not ("." or "..") &&
            !p.StartsWith(".splinter-apply-", StringComparison.OrdinalIgnoreCase)) &&
        (c.Before is not null || c.After is not null) && VersionValid(c.Before) && VersionValid(c.After) &&
        (c.Expected is null || !string.IsNullOrWhiteSpace(c.Expected.Identity) && c.Expected.Version is not null && VersionValid(c.Expected.Version));
    private static bool VersionValid(OriginalFileVersion? v) => v is null || v.ModifiedUtcTicks >= DateTime.FromFileTimeUtc(0).Ticks &&
        v.ModifiedUtcTicks <= DateTime.MaxValue.Ticks && v.Length >= 0 && v.UnixMode is not > 0xFFF && (v.Attributes & (1 | 0x400 | 0x800 | 0x4000 | 0x200)) == 0 &&
        (v.Kind == "directory" && v.Length == 0 && v.Hash is null && (v.Attributes & 16) != 0 ||
         v.Kind == "file" && (v.Attributes & 16) == 0 && v.Hash is { Length: 64 } && v.Hash.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f'));
    public void RequireReclaimable(WorkspaceRecord w)
    {
        RequireMatch(w);
        if (!Completed) throw new WorkspaceException("OriginalApplyPending", "Resume the exact apply intent before reclaiming its workspace or publication resources.");
    }
}
public interface IOriginalApplyProvider
{
    Task<OriginalApplyPlan> PlanOriginalAsync(WorkspaceRecord workspace, CancellationToken ct = default, string[]? paths = null);
    Task<IntegrationResult> ApplyOriginalAsync(WorkspaceRecord workspace, string intentKey, string expectedPlanDigest, CancellationToken ct = default, string[]? paths = null);
}
// Native path/handle operations live behind this port, never in Core.
public interface IOriginalFilePublisher
{
    string RootIdentity(string project);
    OriginalFileObservation? Observe(string project, string relativePath, CancellationToken ct = default);
    void Publish(OriginalApplyState state, Func<OriginalFileVersion, Stream> openPayload,
        Action persist, CancellationToken ct = default);
}

public interface IOriginalApplyPlanReader
{
    IEnumerable<OriginalApplyChange> Enumerate();
    OriginalApplyChange? Find(string path);
    OriginalApplyChange At(long index);
    IEnumerable<string> Selection() => [];
}
public interface IOriginalApplyProgress
{
    IEnumerable<KeyValuePair<long, OriginalApplyStep>> Enumerate();
    OriginalApplyStep Get(long index);
    MetadataTreeRoot Save(long index, OriginalApplyStep step);
    MetadataTreeRoot PreserveScratch(string path, string? observedIdentity, string reason);
}

public sealed record PreservedPublicationScratch(string Path, string? ObservedIdentity, string Reason);
