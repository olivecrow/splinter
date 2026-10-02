namespace Splinter.Core;

// A frame stores one bounded payload. Extents address logical fragments inside
// that payload, never byte offsets inside its compressed representation.
public sealed record WorkspaceJournalPayload(int StoredBytes, int DecodedBytes)
{
    public bool Encoded => StoredBytes != DecodedBytes;
    public void RequireValid(int maximum)
    {
        if (DecodedBytes < 0 || DecodedBytes > maximum || StoredBytes < 0 ||
            StoredBytes > DecodedBytes || (DecodedBytes > 0 && StoredBytes == 0))
            throw new WorkspaceException("DeltaCorrupt", "Invalid bounded journal payload lengths; preserve the frame.");
    }
    public void RequireFragment(int offset, int length)
    {
        if (offset < 0 || length < 0 || offset > DecodedBytes - length)
            throw new WorkspaceException("DeltaCorrupt", "A logical fragment exceeds its journal payload.");
    }
}
