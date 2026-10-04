using System.Text.Json;
using Splinter.Core;
using Splinter.Platform.Windows;
using Splinter.Runtime;
using Splinter.SourceControl.Git;

namespace Splinter.Workflows.Windows;

// Ordinary-user composition. Privileged VSS deletion stays in the existing
// fixed service protocol with a separate explicit-discard authorization.
public sealed class WindowsWorkspaceDiscardProvider(ISourceDiscardProvider source, ISnapshotWorkspaceBackend snapshots) : IWorkspaceDiscardProvider
{
    public Task<string> ObserveSourceRevisionAsync(WorkspaceRecord w, string control, CancellationToken ct) =>
        source.ObserveDiscardRevisionAsync(w, control, ct);
    public Task DiscardSourceAsync(WorkspaceRecord w, string control, CancellationToken ct) => source.DiscardSourceAsync(w, control, ct);

    public async Task DiscardDeltaAsync(WorkspaceRecord w, string control, CancellationToken ct)
    {
        w.Discard!.RequireMatch(w);
        if (w.Discard.Step != "delta" || w.Discard.Seal is null || Directory.Exists(w.Path) || File.Exists(w.Path) ||
            await snapshots.ObserveAsync(w, control, ct) is not { Mounted: false, ProviderAlive: false, OwnershipVerified: true,
                OpenHandles: 0, WorkingSetBytes: 0, PrivateBytes: 0 })
            throw new WorkspaceException("OverlayCleanupIncomplete", "Verify stopped owned resources and the fixed delta discard stage.");
        await RequireSourceAbsentAsync(w, control, ct);
        string delta = Path.Combine(control, "delta"), ownerPath = Path.Combine(control, "discard-delta-owner.json");
        WorkspaceStore.EnsurePlainPath(delta);
        var prior = ProductState.Read<WorkspaceDiscardState>(ownerPath);
        if (prior is not null && (prior.Intent != w.Discard.Intent || prior.Seal != w.Discard.Seal || prior.Step != "delta"))
            throw new WorkspaceException("DiscardIntentMismatch", "Preserve the different delta discard intent.");
        string identityPath = Path.Combine(delta, "identity.json");
        if (File.Exists(identityPath))
        {
            WorkspaceStore.EnsurePlainPath(identityPath);
            var identity = ProductState.Read<OverlayIdentity>(identityPath, journal: true);
            var expected = NtfsSnapshotWorkspaceBackend.Identity(w);
            if (JsonSerializer.Serialize(identity) != JsonSerializer.Serialize(expected))
                throw new WorkspaceException("OverlayIdentityMismatch", "The delta identity changed; preserve its files.");
        }
        else if (prior is null) throw new WorkspaceException("DiscardOwnershipUnknown", "No owned delta identity or retained discard intent.");
        if (prior is null) ProductState.Save(ownerPath, w.Discard);
        try
        {
            JournalReclamation.Reclaim(delta, w.Discard.Seal, ["identity.json"], Path.Combine(control, "discard-delta-reclaim.json"));
        }
        catch (WorkspaceException ex) when (ex.Code.StartsWith("Validation", StringComparison.Ordinal))
        { throw new WorkspaceException("DiscardDeltaChanged", ex.Message); }
    }

    public async Task<SnapshotReclamation> DiscardSnapshotAsync(WorkspaceRecord w, string control, CancellationToken ct)
    {
        w.Discard!.RequireMatch(w);
        if (w.Discard.Step != "snapshot" || w.Discard.Seal is null ||
            Directory.Exists(Path.Combine(control, "delta")) || File.Exists(Path.Combine(control, "delta")) ||
            Directory.Exists(w.Path) || File.Exists(w.Path))
            throw new WorkspaceException("SnapshotInUse", "Prove the owned source, mount and delta absent before discard.");
        await RequireSourceAbsentAsync(w, control, ct);
        ProductState.Save(Path.Combine(control, "snapshot", "discard-authorized.json"), w.Discard);
        return await WorkspaceSnapshotBasis.Provider(w.Snapshot!.Reference!, Path.Combine(control, "snapshot"), w.Id)
            .ReleaseAsync(w.Snapshot!.Reference!, ct, w.Snapshot.Reclamation?.AttemptId);
    }

    private Task RequireSourceAbsentAsync(WorkspaceRecord w, string control, CancellationToken ct)
    {
        return source.VerifyDiscardedAsync(w, control, ct);
    }
}
