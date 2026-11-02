namespace Splinter.Core;

public sealed record WorkspaceRangeWrite(Guid Id, long Offset, ReadOnlyMemory<byte> Data);

// Ordered writes share a durability barrier. Success means every write is
// durable. Failure may leave an unacknowledged prefix; this is not rollback.
// The caller owns unchanged input buffers until this synchronous call returns.
public interface IWorkspaceWriteBatch
{
    const int MaximumWrites = 64;
    const int MaximumBytes = 16 * 1024 * 1024;
    void WriteBatch(IReadOnlyList<WorkspaceRangeWrite> writes);
}

// Platform callbacks supply one synchronous metadata mutation each. The
// caller retains their state until return; no success precedes the shared
// durable barrier. Failure can leave an unacknowledged replayable prefix.
public interface IWorkspaceMetadataBatch
{
    void ApplyMetadataBatch(IReadOnlyList<Action> mutations);
}

// Optional single-store composition of synchronous range and metadata effects.
// The caller bounds callbacks/chunks and staged bytes by IWorkspaceWriteBatch's
// limits and retains every input until return. One durable barrier precedes all
// successful returns; failure can leave a replayable, unacknowledged prefix.
public interface IWorkspaceMutationBatch
{
    void ApplyMutationBatch(IReadOnlyList<Action> mutations);
}
