using System.Buffers;
using System.Diagnostics;
using Splinter.Core;

namespace Splinter.Runtime;

public sealed partial class JournalDeltaStore
{
    private const int ReplayReadBufferBytes = 64 * 1024;

    // A bounded, transient journal read buffer. Snapshot payload is never read.
    // Every complete frame is still checksummed and decoded before indexing.
    private sealed class ReplayReader(JournalDeltaStore owner, long length) : IDisposable
    {
        private readonly byte[] buffer = ArrayPool<byte>.Shared.Rent(ReplayReadBufferBytes);
        private long offset;
        private int available, position;

        public void ReadExactly(Span<byte> output)
        {
            while (!output.IsEmpty)
            {
                if (position == available)
                {
                    int requested = (int)Math.Min(ReplayReadBufferBytes, length - offset);
                    long started = Stopwatch.GetTimestamp();
                    try
                    {
                        owner.replayReadCalls++;
                        available = RandomAccess.Read(owner.journal.SafeFileHandle, buffer.AsSpan(0, requested), offset);
                    }
                    finally { owner.replayReadTicks += Stopwatch.GetTimestamp() - started; }
                    if (available == 0) throw new WorkspaceException("DeltaCorrupt", "A replayed journal range is missing; preserve the file.");
                    owner.replayBytes += available; owner.journalReadBytes += available;
                    offset += available; position = 0;
                }
                int count = Math.Min(output.Length, available - position);
                buffer.AsSpan(position, count).CopyTo(output);
                position += count; output = output[count..];
            }
        }

        public void Dispose() => ArrayPool<byte>.Shared.Return(buffer);
    }
}
