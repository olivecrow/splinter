using System.Buffers;
using Splinter.Core;

namespace Splinter.Runtime;

// Replays the deterministic candidate stream against every existing byte before
// appending anything. A mismatch never truncates, overwrites, or extends the file.
internal sealed class CandidatePrefixWriter : Stream
{
    private readonly FileStream file;
    private readonly long prefixLength;
    public CandidatePrefixWriter(string path)
    {
        file = new(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 65536);
        prefixLength = file.Length;
    }
    public override void Write(ReadOnlySpan<byte> buffer)
    {
        int compare = (int)Math.Min(buffer.Length, prefixLength - Math.Min(prefixLength, file.Position));
        if (compare > 0)
        {
            byte[] saved = ArrayPool<byte>.Shared.Rent(compare);
            try
            {
                file.ReadExactly(saved.AsSpan(0, compare));
                if (!buffer[..compare].SequenceEqual(saved.AsSpan(0, compare)))
                    throw new WorkspaceException("CandidatePartialMismatch", "The partial candidate is not an exact prefix; preserve it without alteration.");
            }
            finally { ArrayPool<byte>.Shared.Return(saved); }
        }
        file.Write(buffer[compare..]);
    }
    public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));
    public override void Flush() => file.Flush(true);
    public override long Length => file.Length;
    public override long Position { get => file.Position; set => throw new NotSupportedException(); }
    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    protected override void Dispose(bool disposing) { if (disposing) file.Dispose(); base.Dispose(disposing); }
}
