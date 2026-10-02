using Splinter.Core;

namespace Splinter.SourceControl.Files;

// Selection is durable. An adapter failure is propagated, never reinterpreted
// as a request for another source mode.
public sealed class SourceWorkspaceRouter(ISourceWorkspaceProvider git, FileWorkspaceProvider files) :
    ISourceWorkspaceProvider, ISnapshotSourceWorkspaceProvider, ISourceDiscardProvider
{
    private ISourceWorkspaceProvider For(WorkspaceRecord w) => w.SourceKind switch
    { "git" => git, "none" => files, _ => throw new WorkspaceException("SourceSelectionUnsupported", "Unknown durable source kind.") };
    private ISnapshotSourceWorkspaceProvider Snapshot(WorkspaceRecord w) => (ISnapshotSourceWorkspaceProvider)For(w);
    private ISourceDiscardProvider Discard(WorkspaceRecord w) => (ISourceDiscardProvider)For(w);
    public Task<string> ResolveRevisionAsync(string p, string r, CancellationToken ct = default) => git.ResolveRevisionAsync(p, r, ct);
    public Task<string> SnapshotKeyAsync(string p, string r, string[] paths, CancellationToken ct = default) => git.SnapshotKeyAsync(p, r, paths, ct);
    public Task<SourceWorkspace> CreateAsync(WorkspaceRecord w, CancellationToken ct = default) => For(w).CreateAsync(w, ct);
    public Task<SourceState> InspectAsync(WorkspaceRecord w, CancellationToken ct = default) => For(w).InspectAsync(w, ct);
    public Task<string> CheckpointAsync(WorkspaceRecord w, string message, CancellationToken ct = default) => For(w).CheckpointAsync(w, message, ct);
    public Task<IntegrationResult> IntegrateAsync(WorkspaceRecord w, string target, CancellationToken ct = default) => For(w).IntegrateAsync(w, target, ct);
    public Task ReleaseAsync(WorkspaceRecord w, CancellationToken ct = default) => For(w).ReleaseAsync(w, ct);
    public Task<string> PreflightAsync(WorkspaceRecord w, CancellationToken ct = default) => Snapshot(w).PreflightAsync(w, ct);
    public Task RegisterAsync(WorkspaceRecord w, string c, CancellationToken ct = default) => Snapshot(w).RegisterAsync(w, c, ct);
    public Task AttachAsync(WorkspaceRecord w, string c, CancellationToken ct = default) => Snapshot(w).AttachAsync(w, c, ct);
    public Task<string> EstablishBaselineAsync(WorkspaceRecord w, string c, CancellationToken ct = default) => Snapshot(w).EstablishBaselineAsync(w, c, ct);
    public Task<string> PreserveAsync(WorkspaceRecord w, string c, CancellationToken ct = default) => Snapshot(w).PreserveAsync(w, c, ct);
    public Task VerifyResultAsync(WorkspaceRecord w, string c, CancellationToken ct = default) => Snapshot(w).VerifyResultAsync(w, c, ct);
    public Task UnregisterAsync(WorkspaceRecord w, string c, CancellationToken ct = default) => Snapshot(w).UnregisterAsync(w, c, ct);
    public Task<string> ObserveDiscardRevisionAsync(WorkspaceRecord w, string c, CancellationToken ct = default) => Discard(w).ObserveDiscardRevisionAsync(w, c, ct);
    public Task DiscardSourceAsync(WorkspaceRecord w, string c, CancellationToken ct = default) => Discard(w).DiscardSourceAsync(w, c, ct);
    public Task VerifyDiscardedAsync(WorkspaceRecord w, string c, CancellationToken ct = default) => Discard(w).VerifyDiscardedAsync(w, c, ct);
}
