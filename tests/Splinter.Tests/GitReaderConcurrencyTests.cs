using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Security.Cryptography;
using System.Text.Json;
using Splinter.Core;
using Splinter.SourceControl.Git;

namespace Splinter.Tests;

public sealed class GitReaderConcurrencyTests
{
    private const int Chunk = 65536;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DifferentBlobsProgressWhileTheOtherOwnedDecoderIsBlocked(bool packed)
    {
        using var f = await Fixture.Create(packed); using var tree = f.Open();
        using var entered = new ManualResetEventSlim(); using var resume = new ManualResetEventSlim();
        tree.ReadObserver = (id, stage) =>
        {
            if (id != f.Blobs["a.bin"] || stage != "header") return;
            entered.Set(); Wait(resume);
        };
        var a = ReadTask(f, tree, "a.bin");
        try
        {
            await Until(() => entered.IsSet);
            await ReadTask(f, tree, "b.bin").WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            Assert.False(a.IsCompleted);
            Assert.Equal(1, tree.ActiveReadCount);
            Assert.Equal(2, tree.PeakReaderProcesses);
            Assert.Equal(4 * Chunk, tree.CachedPayloadBytes);
            Assert.Equal(576, tree.CachedProofBytes); // Both in-flight and completed proofs.
        }
        finally { resume.Set(); await a; }
        Assert.Equal(2, tree.FullBlobProofs);
        Assert.Equal(16L * Chunk, tree.DecodedBlobBytes);
    }

    [Fact]
    public async Task BlobAliasesWaitForTheSameProofAndReaderLease()
    {
        using var f = await Fixture.Create(); using var tree = f.Open();
        using var entered = new ManualResetEventSlim(); using var resume = new ManualResetEventSlim();
        int headers = 0;
        tree.ReadObserver = (_, stage) =>
        {
            if (stage != "header" || Interlocked.Increment(ref headers) != 1) return;
            entered.Set(); Wait(resume);
        };
        var a = ReadTask(f, tree, "a.bin"); Task? alias = null;
        try
        {
            await Until(() => entered.IsSet);
            alias = ReadTask(f, tree, "alias.bin");
            await Until(() => tree.WaitingReadCount == 1);
            Assert.Equal(1, tree.ActiveReadCount);
            Assert.Equal(2 * Chunk, tree.CachedPayloadBytes);
            Assert.Equal(288, tree.CachedProofBytes);
            Assert.False(alias.IsCompleted);
        }
        finally { resume.Set(); await a; if (alias is not null) await alias; }
        Assert.Equal(1, tree.FullBlobProofs);
        Assert.Equal(1, tree.PeakReaderProcesses);
    }

    [Fact]
    public async Task ThirdBlobCannotEvictAnActiveReaderOrExceedThePayloadBudget()
    {
        using var f = await Fixture.Create(); using var tree = f.Open();
        using var aEntered = new ManualResetEventSlim(); using var bEntered = new ManualResetEventSlim();
        using var resume = new ManualResetEventSlim();
        tree.ReadObserver = (id, stage) =>
        {
            if (stage != "header" || id == f.Blobs["c.bin"]) return;
            (id == f.Blobs["a.bin"] ? aEntered : bEntered).Set(); Wait(resume);
        };
        Task a = ReadTask(f, tree, "a.bin"), b = ReadTask(f, tree, "b.bin"); Task? c = null;
        try
        {
            await Until(() => aEntered.IsSet && bEntered.IsSet);
            c = ReadTask(f, tree, "c.bin");
            await Until(() => tree.WaitingReadCount == 1);
            Assert.Equal(2, tree.ActiveReadCount);
            Assert.Equal(4 * Chunk, tree.CachedPayloadBytes);
            Assert.Equal(576, tree.CachedProofBytes);
            Assert.False(c.IsCompleted);
        }
        finally { resume.Set(); await Task.WhenAll(a, b); if (c is not null) await c; }
        Assert.Equal(3, tree.FullBlobProofs);
        Assert.Equal(2, tree.PeakReaderProcesses);
    }

    [Theory]
    [InlineData(288, 4096)]
    [InlineData(4194304, 1)]
    public async Task PendingProofReservationsApplyBothBudgetsBeforeAllocation(int bytes, int entries)
    {
        using var f = await Fixture.Create(); using var tree = f.Open(bytes, entries);
        using var entered = new ManualResetEventSlim(); using var resume = new ManualResetEventSlim();
        tree.ReadObserver = (id, stage) =>
        {
            if (id != f.Blobs["a.bin"] || stage != "header") return;
            entered.Set(); Wait(resume);
        };
        var a = ReadTask(f, tree, "a.bin"); Task? b = null;
        try
        {
            await Until(() => entered.IsSet);
            b = ReadTask(f, tree, "b.bin"); await Until(() => tree.WaitingReadCount == 1);
            Assert.Equal(288, tree.CachedProofBytes);
            Assert.Equal(2 * Chunk, tree.CachedPayloadBytes);
            Assert.Equal(1, tree.ActiveReadCount);
        }
        finally { resume.Set(); await a; if (b is not null) await b; }
        Assert.Equal(288, tree.PeakProofBytes);
        Assert.Equal(1, tree.CachedProofCount);
        Assert.Equal(1, tree.PeakReaderProcesses);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1048576)]
    public async Task FailedReaderDrainsTheOtherLeaseBeforeResetAndBlocksItsStagedPublication(int payloadBytes)
    {
        using var f = await Fixture.Create(); using var tree = f.Open(payloadBytes: payloadBytes);
        if (payloadBytes != 0) await ReadTask(f, tree, "a.bin");
        using var verified = new ManualResetEventSlim(); using var resume = new ManualResetEventSlim();
        tree.ReadObserver = (id, stage) =>
        {
            if (id != f.Blobs["a.bin"] || stage != "verified") return;
            verified.Set(); Wait(resume);
        };
        byte[] aBytes = Enumerable.Repeat((byte)217, 16).ToArray();
        byte[] bBytes = aBytes.ToArray();
        Task<Exception?> a = Task.Run(() => Record.Exception(() => tree.Read("a.bin", 0, aBytes)), TestContext.Current.CancellationToken);
        Task<Exception?>? b = null;
        try
        {
            await Until(() => verified.IsSet);
            f.RemoveBlob("b.bin");
            b = Task.Run(() => Record.Exception(() => tree.Read("b.bin", 0, bBytes)), TestContext.Current.CancellationToken);
            await Until(() => tree.IsResetting && tree.ActiveReadCount == 1);
            Assert.False(b.IsCompleted);
            Assert.All(aBytes, value => Assert.Equal((byte)217, value));
        }
        finally { resume.Set(); }
        Assert.Equal("GitBasisObjectInvalid", Assert.IsType<WorkspaceException>(await a).Code);
        Assert.NotNull(b);
        Assert.Equal("GitBasisObjectInvalid", Assert.IsType<WorkspaceException>(await b).Code);
        Assert.All(aBytes, value => Assert.Equal((byte)217, value));
        Assert.All(bBytes, value => Assert.Equal((byte)217, value));
        Assert.Equal(0, tree.ActiveReadCount); Assert.Equal(0, tree.ActiveReaderProcesses);
        Assert.Equal(0, tree.CachedProofBytes); Assert.Equal(0, tree.CachedPayloadBytes);
        tree.ReadObserver = null; await ReadTask(f, tree, "c.bin");
        Assert.Equal(payloadBytes == 0 ? 1 : 2, tree.FullBlobProofs);
        if (payloadBytes != 0) Assert.Equal(1, tree.PayloadCacheHits);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1048576)]
    public async Task EveryDisposeWaitsForBothOwnedLeasesAndNoBytesPublishAfterCloseStarts(int payloadBytes)
    {
        using var f = await Fixture.Create(); using var tree = f.Open(payloadBytes: payloadBytes);
        if (payloadBytes != 0)
        { await ReadTask(f, tree, "a.bin"); await ReadTask(f, tree, "b.bin"); }
        using var aEntered = new ManualResetEventSlim(); using var bEntered = new ManualResetEventSlim();
        using var resumeA = new ManualResetEventSlim(); using var resumeB = new ManualResetEventSlim();
        using var secondEntered = new ManualResetEventSlim();
        tree.ReadObserver = (id, stage) =>
        {
            if (stage != (payloadBytes == 0 ? "header" : "verified")) return;
            bool isA = id == f.Blobs["a.bin"];
            (isA ? aEntered : bEntered).Set(); Wait(isA ? resumeA : resumeB);
        };
        byte[] aBytes = Enumerable.Repeat((byte)217, 16).ToArray(), bBytes = aBytes.ToArray();
        var a = Task.Run(() => CaptureReadFailure(tree, "a.bin", aBytes), TestContext.Current.CancellationToken);
        var b = Task.Run(() => CaptureReadFailure(tree, "b.bin", bBytes), TestContext.Current.CancellationToken);
        Task? first = null, second = null; int[] pids = [];
        try
        {
            await Until(() => aEntered.IsSet && bEntered.IsSet); pids = tree.ReaderProcessIds;
            first = Task.Run(tree.Dispose, TestContext.Current.CancellationToken);
            await Until(() => tree.IsClosing);
            second = Task.Run(() => { secondEntered.Set(); tree.Dispose(); }, TestContext.Current.CancellationToken);
            await Until(() => secondEntered.IsSet);
            Assert.Throws<ObjectDisposedException>(() => tree.Read("c.bin", 0, new byte[16]));
            Assert.Equal(2, tree.ActiveReaderProcesses);
            resumeA.Set(); Assert.IsType<ObjectDisposedException>((await a).Error);
            Assert.False(first.IsCompleted); Assert.False(second.IsCompleted);
            Assert.Equal(1, tree.ActiveReadCount);
        }
        finally { resumeA.Set(); resumeB.Set(); }
        Assert.IsType<ObjectDisposedException>((await b).Error);
        if (first is not null) await first; if (second is not null) await second;
        Assert.False((await a).GateLeaked); Assert.False((await b).GateLeaked);
        Assert.All(aBytes, value => Assert.Equal((byte)217, value));
        Assert.All(bBytes, value => Assert.Equal((byte)217, value));
        Assert.Equal(0, tree.ActiveReadCount); Assert.Equal(0, tree.ActiveReaderProcesses);
        Assert.Equal(0, tree.CachedProofBytes); Assert.Equal(0, tree.CachedPayloadBytes);
        if (payloadBytes != 0) Assert.Equal(2, tree.PayloadCacheHits);
        foreach (int pid in pids)
        {
            try { using var process = Process.GetProcessById(pid); Assert.True(process.HasExited); }
            catch (ArgumentException) { }
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1048576)]
    public async Task UnclassifiedCompletionFailureUnwindsTheGateAndRetiresItsLease(int payloadBytes)
    {
        using var f = await Fixture.Create(); using var tree = f.Open(payloadBytes: payloadBytes);
        tree.ReadObserver = (_, stage) => { if (stage == "verified") f.ObservationFailure = new ApplicationException("Injected completion failure"); };
        byte[] destination = Enumerable.Repeat((byte)217, 16).ToArray();
        var failure = CaptureReadFailure(tree, "a.bin", destination);
        Assert.IsType<ApplicationException>(failure.Error);
        Assert.False(failure.GateLeaked);
        Assert.Equal(0, tree.ActiveReadCount);
        Assert.Equal(0, tree.CachedChunkPayloadBytes);
        Assert.All(destination, value => Assert.Equal((byte)217, value));
        f.ObservationFailure = null; tree.ReadObserver = null;
        await ReadTask(f, tree, "a.bin");
        Assert.Equal(1, tree.FullBlobProofs);
    }

    [Fact]
    public async Task OverlappingSmallRangeReadsKeepExactHashesAndBoundedLinearDecoding()
    {
        using var f = await Fixture.Create(packed: true); using var tree = f.Open();
        using var barrier = new Barrier(2);
        var stopwatch = Stopwatch.StartNew();
        async Task ReadAll(string name)
        {
            byte[] actual = await Task.Run(() =>
            {
                byte[] bytes = new byte[f.Data[name].Length];
                for (int offset = 0; offset < bytes.Length; offset += 4096)
                {
                    Assert.True(barrier.SignalAndWait(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken));
                    Assert.Equal(4096, tree.Read(name, offset, bytes.AsSpan(offset, 4096)));
                }
                return bytes;
            }, TestContext.Current.CancellationToken);
            Assert.Equal(SHA256.HashData(f.Data[name]), SHA256.HashData(actual));
        }
        await Task.WhenAll(ReadAll("a.bin"), ReadAll("b.bin"));
        stopwatch.Stop();
        Assert.Equal(2, tree.FullBlobProofs); Assert.Equal(2, tree.ReaderProcessStarts);
        Assert.InRange(tree.DecodedBlobBytes, 16L * Chunk, 32L * Chunk);
        Assert.Equal(4 * Chunk, tree.CachedPayloadBytes); Assert.Equal(576, tree.PeakProofBytes);
        TestContext.Current.TestOutputHelper!.WriteLine(JsonSerializer.Serialize(new
        {
            scenario = "two-packed-blobs-overlapping-4k-ranges",
            elapsedSeconds = stopwatch.Elapsed.TotalSeconds,
            sourceBytes = 16L * Chunk,
            tree.DecodedBlobBytes,
            tree.ReaderProcessStarts,
            tree.FullBlobProofs,
            tree.PeakReaderProcesses,
            tree.CachedPayloadBytes,
            tree.PeakProofBytes
        }));
    }

    [Theory]
    [InlineData(256)]
    [InlineData(3)]
    public void HeaderReadsAreBoundedAndLeaveThePayloadUntouched(int maximumRead)
    {
        string id = new('a', 40), header = id + " blob 12345\n";
        using var stream = new HeaderStream(Encoding.ASCII.GetBytes(header + "payload"), maximumRead);
        GitObjectSnapshotTree.ReadBlobHeader(stream, id, 12345);
        Assert.Equal(header.Length, stream.Position);
        Assert.Equal((header.Length + maximumRead - 1) / maximumRead, stream.ReadCalls);
        Assert.Equal((byte)'p', stream.ReadByte());
    }

    [Fact]
    public void MissingOrMalformedHeadersFailBeforeReadingPastTheirNewline()
    {
        string id = new('a', 40);
        foreach (string header in new[] { id + " missing\n", "bad\n", id + " tree 12345\n", id + " blob 1234\n" })
        {
            using var stream = new HeaderStream(Encoding.ASCII.GetBytes(header), 256, failAtEnd: true);
            Assert.Equal("GitBasisObjectInvalid", Assert.Throws<WorkspaceException>(() => GitObjectSnapshotTree.ReadBlobHeader(stream, id, 12345)).Code);
            Assert.Equal(1, stream.ReadCalls);
        }
    }

    private static (Exception? Error, bool GateLeaked) CaptureReadFailure(GitObjectSnapshotTree tree, string path, byte[] bytes)
    {
        object gate = typeof(GitObjectSnapshotTree).GetField("readGate", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(tree)!;
        Exception? error;
        try { tree.Read(path, 0, bytes); error = null; }
        catch (Exception ex) { error = ex; }
        bool leaked = Monitor.IsEntered(gate);
        // A regression must fail promptly, not strand both Dispose callers.
        // Release only an accidentally retained monitor owned by this thread.
        while (Monitor.IsEntered(gate)) Monitor.Exit(gate);
        return (error, leaked);
    }

    private sealed class HeaderStream(byte[] bytes, int maximumRead, bool failAtEnd = false) : MemoryStream(bytes)
    {
        internal int ReadCalls;
        public override int Read(Span<byte> buffer)
        {
            ReadCalls++;
            if (failAtEnd && Position == Length) throw new InvalidOperationException("Read waited beyond the batch response.");
            return base.Read(buffer[..Math.Min(buffer.Length, maximumRead)]);
        }
    }
    private static Task ReadTask(Fixture f, GitObjectSnapshotTree tree, string name) => Task.Run(() =>
    {
        byte[] bytes = new byte[16];
        Assert.Equal(bytes.Length, tree.Read(name, 0, bytes));
        Assert.Equal(f.Data[name].AsSpan(0, bytes.Length).ToArray(), bytes);
    }, TestContext.Current.CancellationToken);
    private static void Wait(ManualResetEventSlim ready) => Assert.True(ready.Wait(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken));
    private static async Task Until(Func<bool> predicate)
    {
        var time = Stopwatch.StartNew();
        while (!predicate())
        {
            Assert.True(time.Elapsed < TimeSpan.FromSeconds(10), "The expected reader boundary was not reached.");
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }
    }
    private sealed class Fixture : IDisposable
    {
        private readonly TestDirectory directory = new();
        private string Source => Path.Combine(directory.Path, "source");
        private string Receipt => Path.Combine(directory.Path, "receipt");
        private SnapshotReference snapshot = null!;
        private string tree = "";
        internal Dictionary<string, string> Blobs { get; } = new();
        internal Dictionary<string, byte[]> Data { get; } = new();
        internal Exception? ObservationFailure;
        internal GitObjectSnapshotTree Open(int bytes = 4194304, int entries = 4096, int payloadBytes = 0) =>
            new(snapshot, tree, "", "", () => ObservationFailure is { } failure ? throw failure : new(snapshot.Id, true, null, 0, null), bytes, entries,
                payloadByteLimit: payloadBytes);
        internal static async Task<Fixture> Create(bool packed = false)
        {
            var f = new Fixture(); Directory.CreateDirectory(f.Source);
            await TestDirectory.Git(f.Source, "init", "-b", "development");
            await TestDirectory.Git(f.Source, "config", "core.autocrlf", "false");
            await TestDirectory.Git(f.Source, "config", "user.name", "Fixture");
            await TestDirectory.Git(f.Source, "config", "user.email", "fixture@example.invalid");
            foreach (string name in new[] { "a.bin", "b.bin", "c.bin", "alias.bin" })
            {
                int seed = name == "alias.bin" ? 'a' : name[0];
                byte[] data = Enumerable.Range(0, 8 * Chunk).Select(i => (byte)(i * 31 + i / Chunk + seed)).ToArray();
                f.Data.Add(name, data); File.WriteAllBytes(Path.Combine(f.Source, name), data);
            }
            await TestDirectory.Git(f.Source, "add", "."); await TestDirectory.Git(f.Source, "commit", "-m", "basis");
            string revision = await TestDirectory.Git(f.Source, "rev-parse", "HEAD");
            f.tree = await TestDirectory.Git(f.Source, "rev-parse", "HEAD^{tree}");
            foreach (string name in f.Data.Keys) f.Blobs.Add(name, await TestDirectory.Git(f.Source, "rev-parse", "HEAD:" + name));
            var provider = new GitObjectSnapshotProvider(f.Receipt, "reader-concurrency", revision);
            f.snapshot = await provider.CaptureAsync(new(f.Source, "reader-concurrency", f.Receipt), TestContext.Current.CancellationToken);
            if (packed) await TestDirectory.Git(f.Source, "gc");
            return f;
        }
        internal void RemoveBlob(string name)
        {
            string id = Blobs[name];
            string path = Path.GetFullPath(Path.Combine(Source, ".git", "objects", id[..2], id[2..]));
            Assert.StartsWith(Path.GetFullPath(directory.Path) + Path.DirectorySeparatorChar, path);
            File.SetAttributes(path, FileAttributes.Normal); File.Delete(path);
        }
        public void Dispose() => directory.Dispose();
    }
}
