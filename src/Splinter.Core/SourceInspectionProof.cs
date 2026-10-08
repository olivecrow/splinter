namespace Splinter.Core;

// Optional, operation-local evidence. Never serialize this proof, reconstruct
// it from metrics, or carry it across commands/provider restarts.
public sealed record ProvedSourceInspection(SourceState Source, ISourceInspectionProof? Proof);

public interface ISourceInspectionProof
{
    // Recheck identity, complete source generation and interpretation before
    // returning the inspected state. Changed/unknown evidence must be refused.
    Task<SourceState> RevalidateAsync(WorkspaceRecord workspace, CancellationToken ct = default);
}

public interface ISourceInspectionProofProvider
{
    Task<ProvedSourceInspection> InspectWithProofAsync(WorkspaceRecord workspace, CancellationToken ct = default);
}
