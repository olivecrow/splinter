using Splinter.Core;
using Splinter.SourceControl.Git;

namespace Splinter.Tests;

public sealed class GitProgressReadTests
{
    [Fact]
    public void ProgressingPartialReadsCanExceedTheTotalThirtySecondDuration()
    {
        var time = new ManualTime(); bool aborted = false;
        using var input = new AdvancingStream(time, TimeSpan.FromSeconds(20));
        using var reader = new GitProgressReadStream(input, () => aborted = true, time: time);
        byte[] output = new byte[8]; reader.ReadExactly(output);
        Assert.Equal(Enumerable.Range(1, 8).Select(i => (byte)i).ToArray(), output);
        Assert.Equal(TimeSpan.FromSeconds(160), time.Elapsed); Assert.False(aborted);
        time.Advance(TimeSpan.FromMinutes(1)); Assert.False(aborted); // No blocked I/O while hashing/idle.
    }
    [Fact]
    public void AStalledReadAbortsOnlyItsReaderAndPreservesTheTimeoutCode()
    {
        var time = new ManualTime(); int aborts = 0;
        using var input = new AdvancingStream(time, TimeSpan.FromSeconds(31));
        using var reader = new GitProgressReadStream(input, () => aborts++, time: time);
        Assert.Equal("GitBasisReadTimeout", Assert.Throws<WorkspaceException>(() => reader.ReadByte()).Code);
        Assert.Equal(1, aborts);
        Assert.Equal("GitBasisReadTimeout", Assert.Throws<WorkspaceException>(() => reader.ReadByte()).Code);
        reader.Dispose(); time.Advance(TimeSpan.FromMinutes(1)); Assert.Equal(1, aborts);
    }
    private sealed class AdvancingStream(ManualTime time, TimeSpan perRead) : MemoryStream
    {
        private byte next = 1;
        public override int Read(Span<byte> buffer) { time.Advance(perRead); buffer[0] = next++; return 1; }
    }
    private sealed class ManualTime : TimeProvider
    {
        private readonly List<ManualTimer> timers = [];
        public TimeSpan Elapsed { get; private set; }
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => Elapsed.Ticks;
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = new ManualTimer(this, callback, state); timers.Add(timer); timer.Change(dueTime, period); return timer;
        }
        public void Advance(TimeSpan amount)
        {
            Elapsed += amount;
            foreach (var timer in timers.ToArray()) timer.Fire();
        }
        private sealed class ManualTimer(ManualTime time, TimerCallback callback, object? state) : ITimer
        {
            private TimeSpan? due;
            private bool disposed;
            public bool Change(TimeSpan dueTime, TimeSpan period)
            { if (disposed) return false; due = dueTime == Timeout.InfiniteTimeSpan ? null : time.Elapsed + dueTime; return true; }
            public void Fire() { if (!disposed && due is { } when && when <= time.Elapsed) { due = null; callback(state); } }
            public void Dispose() { disposed = true; due = null; }
            public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
        }
    }
}
