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
