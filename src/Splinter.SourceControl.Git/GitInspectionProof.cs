using Splinter.Core;

namespace Splinter.SourceControl.Git;

public sealed partial class GitWorkspaceProvider
{
    public async Task<ProvedSourceInspection> InspectWithProofAsync(WorkspaceRecord workspace, CancellationToken ct = default)
    {
        // Match ordinary InspectAsync admission before making any proof.
        await EnsureRegistered(workspace, ct);
        SourceProof? proof = null;
        var state = await InspectSourceAsync(workspace, ct, value => proof = value);
        return new(state, proof is { CompleteMembership: true } && !auditSource
            ? new InspectionProof(this, workspace.Id, state, proof) : null);
    }

    private sealed class InspectionProof(GitWorkspaceProvider provider, string id, SourceState source, SourceProof proof) : ISourceInspectionProof
    {
        private int consumed;
        public async Task<SourceState> RevalidateAsync(WorkspaceRecord workspace, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            if (Interlocked.Exchange(ref consumed, 1) != 0 || workspace.Id != id)
                throw Error("SourceChanged", "Source inspection proof is single-use and belongs to one workspace operation.");
            await provider.VerifyStagedSourceAsync(workspace, proof, ct, staging: false);
            return source;
        }
    }
}
