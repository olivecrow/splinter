using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Splinter.Core;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record OriginalFileVersion([property: JsonRequired] string Kind, [property: JsonRequired] long Length,
    [property: JsonRequired] string? Hash, [property: JsonRequired] int Attributes, [property: JsonRequired] long ModifiedUtcTicks)
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
    public string ComputeDigest()
    {
        var content = new Dictionary<string, object?> { [nameof(Version)] = Version, [nameof(OwnerId)] = OwnerId,
            [nameof(RequestDigest)] = RequestDigest, [nameof(Basis)] = Basis, [nameof(Result)] = Result,
            [nameof(ProjectPath)] = ProjectPath, [nameof(ProjectIdentity)] = ProjectIdentity, [nameof(Changes)] = Changes, [nameof(Conflicts)] = Conflicts };
        if (SourceIdentity is not null) content.Add(nameof(SourceIdentity), SourceIdentity);
        if (SelectedPaths is not null) content.Add(nameof(SelectedPaths), SelectedPaths);
        return "sha256:" + Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(content)));
    }
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class OriginalApplyStep
{
    [JsonRequired] public string Phase { get; set; } = "pending";
    [JsonRequired] public string? StagedIdentity { get; set; }
    [JsonRequired] public long StagedBytes { get; set; }
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class OriginalApplyState
{
    public required OriginalApplyPlan Plan { get; init; }
    public required string IntentKey { get; init; }
    public required OriginalApplyStep[] Steps { get; init; }
    [JsonRequired] public bool Completed { get; set; }
    public void RequireMatch(WorkspaceRecord w)
    {
        bool plain = w.SourceKind == "none" && w.ApplicationKind == "none" && w.StorageMode == "ntfs-snapshot";
        bool plastic = w.SourceKind == "plastic" && w.StorageMode == "plastic-snapshot" && w.ApplicationKind is "none" or "unity";
        if (Plan is null || Plan.Version != 1 || w.Purpose != "task" || !plain && !plastic ||
            plastic && (Plan.SourceIdentity is not { Length: 71 } || !Plan.SourceIdentity.StartsWith("sha256:", StringComparison.Ordinal) ||
                Plan.SourceIdentity[7..].Any(c => c is not (>= '0' and <= '9' or >= 'a' and <= 'f'))) ||
            plain && Plan.SourceIdentity is not null || Plan.SelectedPaths is { } selected && !ValidSelection(selected) ||
            Plan.OwnerId != w.Id || Plan.RequestDigest != w.RequestDigest || Plan.Basis != w.BaseRevision ||
            Plan.Result != w.ResultRevision || Plan.ProjectPath != w.ProjectPath ||
            string.IsNullOrWhiteSpace(IntentKey) || IntentKey.Length > 128 || Plan.Changes is null ||
            Plan.Conflicts is null || Plan.Conflicts.Length != 0 || Plan.Changes.Length > 32768 ||
            string.IsNullOrWhiteSpace(Plan.ProjectIdentity) || Plan.Changes.Any(c => !Valid(c)) ||
            Plan.Changes.Select(c => c.Path).Distinct(StringComparer.OrdinalIgnoreCase).Count() != Plan.Changes.Length ||
            Plan.Digest != Plan.ComputeDigest() || Steps is null || Steps.Length != Plan.Changes.Length ||
            Steps.Any(s => s is null || s.Phase is not ("pending" or "staging" or "staged" or "retired" or "published" or "done") ||
                s.StagedBytes < 0 || s.StagedIdentity is not null && string.IsNullOrWhiteSpace(s.StagedIdentity)) ||
            Completed && Steps.Any(s => s.Phase != "done"))
            throw new WorkspaceException("OriginalApplyStateInvalid", "Preserve the mismatched or incomplete original-apply record.");
        for (int i = 0; i < Steps.Length; i++)
        {
            if (Plan.Changes[i].After is null && (Steps[i].StagedIdentity is not null || Steps[i].StagedBytes != 0 || Steps[i].Phase is "staging" or "staged" or "published"))
                throw new WorkspaceException("OriginalApplyStateInvalid", "Preserve impossible deletion publication receipts.");
            if ((Steps[i].Phase is "staged" or "published" || Steps[i].Phase == "done" && !Plan.Changes[i].AlreadyMatches) && Plan.Changes[i].After is not null &&
                (Steps[i].StagedIdentity is null || Steps[i].StagedBytes != Plan.Changes[i].After!.Length))
                throw new WorkspaceException("OriginalApplyStateInvalid", "Preserve incomplete payload publication receipts.");
        }
    }
    public static bool ValidSelection(string[] paths) => paths.Length is > 0 and <= 512 &&
        paths.All(p => !string.IsNullOrWhiteSpace(p) && p.Length <= 1024 && !p.Contains('\\') && !p.Contains(':') && !p.Contains('*') && !p.Contains('?') &&
            p.Split('/').All(s => s.Length > 0 && s is not ("." or "..") && !s.StartsWith(".splinter-apply-", StringComparison.OrdinalIgnoreCase) &&
                !new[] { ".git", ".plastic", ".hg", ".svn" }.Contains(s, StringComparer.OrdinalIgnoreCase))) &&
        paths.Distinct(StringComparer.OrdinalIgnoreCase).Count() == paths.Length && paths.SequenceEqual(paths.Order(StringComparer.Ordinal));
    private static bool Valid(OriginalApplyChange? c) => c is not null && !string.IsNullOrWhiteSpace(c.Path) &&
        !c.Path.Contains('\\') && !c.Path.Contains(':') && c.Path.Split('/').All(p => p.Length > 0 && p is not ("." or "..") &&
            !p.StartsWith(".splinter-apply-", StringComparison.OrdinalIgnoreCase)) &&
        (c.Before is not null || c.After is not null) && VersionValid(c.Before) && VersionValid(c.After) &&
        (c.Expected is null || !string.IsNullOrWhiteSpace(c.Expected.Identity) && c.Expected.Version is not null && VersionValid(c.Expected.Version));
    private static bool VersionValid(OriginalFileVersion? v) => v is null || v.ModifiedUtcTicks >= DateTime.FromFileTimeUtc(0).Ticks &&
        v.ModifiedUtcTicks <= DateTime.MaxValue.Ticks && v.Length >= 0 && (v.Attributes & (1 | 0x400 | 0x800 | 0x4000 | 0x200)) == 0 &&
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
