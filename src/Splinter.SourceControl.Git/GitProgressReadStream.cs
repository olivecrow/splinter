using Splinter.Core;

namespace Splinter.SourceControl.Git;

// Bound each blocked read, not the total duration of a progressing proof.
// The abort callback owns one specific reader process; a retired timer cannot
// kill a replacement reader or time out hashing between reads.
internal sealed class GitProgressReadStream : Stream
{
    private readonly Stream input;
    private readonly Action abort;
    private readonly TimeProvider time;
    private readonly TimeSpan limit;
    private readonly ITimer timer;
    private readonly object gate = new();
    private long started;
    private bool reading, expired, disposed;

    internal GitProgressReadStream(Stream input, Action abort, TimeSpan? limit = null, TimeProvider? time = null)
    {
        this.input = input; this.abort = abort;
        this.limit = limit ?? TimeSpan.FromSeconds(30); this.time = time ?? TimeProvider.System;
        timer = this.time.CreateTimer(Expire, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }
    private void Expire(object? state)
    {
        lock (gate)
        {
            if (disposed || !reading) return;
            TimeSpan remaining = limit - time.GetElapsedTime(started);
            if (remaining > TimeSpan.Zero) { timer.Change(remaining, Timeout.InfiniteTimeSpan); return; }
            expired = true; reading = false; abort();
        }
    }
    private void Begin()
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (expired) throw TimeoutFailure();
            started = time.GetTimestamp(); reading = true;
            timer.Change(limit, Timeout.InfiniteTimeSpan);
        }
    }
    private void End()
    {
        lock (gate)
        {
            reading = false; timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            if (expired) throw TimeoutFailure();
        }
    }
    private static WorkspaceException TimeoutFailure() => new("GitBasisReadTimeout", "The owned Git reader made no read progress for 30 seconds; preserve the fixed basis and journal.");
    public override int Read(Span<byte> buffer)
    {
        if (buffer.IsEmpty) return 0;
        Begin(); try { return input.Read(buffer); } finally { End(); }
    }
    public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));
    public override int ReadByte()
    {
        Span<byte> one = stackalloc byte[1]; return Read(one) == 0 ? -1 : one[0];
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing) { lock (gate) { disposed = true; reading = false; timer.Dispose(); } }
        base.Dispose(disposing); // The process owns the underlying stream.
    }
    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    public override void Flush() => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
