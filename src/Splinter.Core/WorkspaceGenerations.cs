using System.Text.Json.Serialization;

namespace Splinter.Core;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LiveOriginalEditorPermit(int Version, string ProjectPath, int ProcessId, long StartTimeUtcTicks,
    string ResultId, string CooperationKey, DateTimeOffset SavedAt, DateTimeOffset ObservedAt, DateTimeOffset ExpiresAt, bool ImportsDeferred)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string Identity => "sha256:" + Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(
        System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new { Version, ProjectPath, ProcessId, StartTimeUtcTicks, ResultId, CooperationKey })));
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record WorkspaceResultGeneration([property: JsonRequired] string Id, [property: JsonRequired] long Sequence,
    [property: JsonRequired] string RequestKey, [property: JsonRequired] string Message,
    [property: JsonRequired] string Revision, [property: JsonRequired] string Fingerprint,
    [property: JsonRequired] string ResultReference, [property: JsonRequired] string CaptureDigest,
    [property: JsonRequired] long SourceSequence, [property: JsonRequired] DateTimeOffset CreatedAt,
    string? GitReference = null, string? GitFingerprint = null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record WorkspaceCaptureIntent([property: JsonRequired] string Id, [property: JsonRequired] long Sequence,
    [property: JsonRequired] string RequestKey, [property: JsonRequired] string Message);
public sealed record SourceCaptureIdentity(string Id, long Sequence, string Digest);
public interface ISourceCaptureTree : ISnapshotTree, IDisposable
{
    ISnapshotTree Basis { get; }
    string[]? ChangedPaths => null;
    IEnumerable<string>? EnumerateChangedPaths() => ChangedPaths;
}
public interface ISourceGenerationBackend
{
    Task<SourceCaptureIdentity> CaptureSourceAsync(WorkspaceRecord workspace, string control, string id, CancellationToken ct = default);
    Task<ISourceCaptureTree> OpenSourceCaptureAsync(WorkspaceRecord workspace, string control, string id, CancellationToken ct = default);
    Task CompleteSourceCaptureAsync(WorkspaceRecord workspace, string control, string id, CancellationToken ct = default);
}
public interface IWorkspaceGenerationSource
{
    Task<WorkspaceResultGeneration> PreserveGenerationAsync(WorkspaceRecord workspace, string control,
        WorkspaceCaptureIntent intent, SourceCaptureIdentity capture, ISourceCaptureTree tree, CancellationToken ct = default);
    Task VerifyGenerationAsync(WorkspaceRecord workspace, string control, WorkspaceResultGeneration generation, CancellationToken ct = default);
}
public interface IWorkspaceGenerationBranchSource
{
    Task<IntegrationResult> IntegrateGenerationAsync(WorkspaceRecord workspace, WorkspaceResultGeneration result,
        string target, CancellationToken ct = default);
}
