namespace Splinter.Core;

// Optional, operation-local evidence. Never serialize this proof, reconstruct
// it from metrics, or carry it across commands/provider restarts.
public sealed record ProvedSourceInspection(SourceState Source, ISourceInspectionProof? Proof) : IDisposable
{
    private ISourceInspectionProof? ownedProof = Proof;
    public ISourceInspectionProof? DetachProof() => Interlocked.Exchange(ref ownedProof, null);
    public void Dispose() => Interlocked.Exchange(ref ownedProof, null)?.Dispose();
}

public interface ISourceInspectionProof : IDisposable
{
    // Older/fake proofs carry no resource. Native reader-backed proofs override
    // disposal; callers still own the returned proof on every success/failure.
    void IDisposable.Dispose() { }
    // Recheck identity, complete source generation and interpretation before
    // returning the inspected state. Changed/unknown evidence must be refused.
    Task<SourceState> RevalidateAsync(WorkspaceRecord workspace, CancellationToken ct = default);
}

public interface ISourceInspectionProofProvider
{
    Task<ProvedSourceInspection> InspectWithProofAsync(WorkspaceRecord workspace, CancellationToken ct = default);
}
