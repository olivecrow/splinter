using System.Text.Json.Serialization;

namespace Splinter.Core;

public sealed record WorkspaceImportRequest(string Id, string From, string Result, bool Plan,
    string? IntentKey = null, string? ExpectedPlanDigest = null, string[]? Paths = null);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PeerSource([property: JsonRequired] string Id, [property: JsonRequired] string RequestDigest);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record WorkspaceImportReceipt([property: JsonRequired] OriginalApplyState Publication,
    [property: JsonRequired] MetadataTreeRoot AppliedPaths, [property: JsonRequired] MetadataTreeRoot AppliedPathNames);

// The backend pins the exact destination volume for the entire operation.
public sealed record WorkspacePublicationTarget(IOriginalFilePublisher Publisher, IDisposable MountLease) : IDisposable
{
    public void Dispose() => MountLease.Dispose();
}
public interface IWorkspacePublicationTargetProvider
{
    WorkspacePublicationTarget OpenPublicationTarget(WorkspaceRecord workspace);
}
public interface IWorkspaceImportSource
{
    Task<OriginalApplyPlan> ImportAsync(WorkspaceRecord source, WorkspaceResultGeneration generation, WorkspaceRecord target,
        IOriginalFilePublisher publisher, WorkspaceImportRequest request, LiveOriginalEditorPermit? liveTarget, CancellationToken ct);
}
