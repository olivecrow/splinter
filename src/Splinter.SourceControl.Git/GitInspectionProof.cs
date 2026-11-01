using Splinter.Core;

namespace Splinter.SourceControl.Git;

public sealed partial class GitWorkspaceProvider
{
    public async Task<ProvedSourceInspection> InspectWithProofAsync(WorkspaceRecord workspace, CancellationToken ct = default)
    {
        // Match ordinary InspectAsync admission before making any proof.
        await EnsureRegistered(workspace, ct);
        SourceProof? proof = null;
        try
        {
        var state = await InspectSourceAsync(workspace, ct, value => proof = value);
        if (proof is { CompleteMembership: true } && !auditSource)
        {
            var result = new ProvedSourceInspection(state, new InspectionProof(this, workspace.Id, state, proof));
            proof = null; return result;
        }
        return new(state, null);
        }
        finally { proof?.Dispose(); }
    }

    private sealed class InspectionProof(GitWorkspaceProvider provider, string id, SourceState source, SourceProof proof) : ISourceInspectionProof
    {
        private int consumed;
        private int disposed;
        public void Dispose() { if (Interlocked.Exchange(ref disposed, 1) == 0) proof.Dispose(); }
        public async Task<SourceState> RevalidateAsync(WorkspaceRecord workspace, CancellationToken ct = default)
        {
            try
            {
            ct.ThrowIfCancellationRequested();
            if (Volatile.Read(ref disposed) != 0 || Interlocked.Exchange(ref consumed, 1) != 0 || workspace.Id != id)
                throw Error("SourceChanged", "Source inspection proof is single-use and belongs to one workspace operation.");
            await provider.VerifyStagedSourceAsync(workspace, proof, ct, staging: false);
            return source;
            }
            finally { Dispose(); }
        }
    }
}
