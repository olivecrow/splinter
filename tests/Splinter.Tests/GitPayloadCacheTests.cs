using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Splinter.Core;
using Splinter.SourceControl.Git;

namespace Splinter.Tests;

public sealed class GitPayloadCacheTests
{
    private const int Chunk = 65536;

    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task ProofReadAheadServesSequentialAndAliasReadsWithoutAnotherDecode(bool packed)
    {
        using var f = await Fixture.Create(packed); using var tree = f.Open(64 * Chunk, readAhead: 32 * Chunk);
        Read(f, tree, "a.bin", 0, 16);
        Assert.Equal(f.Data["a.bin"].Length, tree.CachedChunkPayloadBytes);
        long decoded = tree.DecodedBlobBytes;
        for (int at = 0; at < f.Data["a.bin"].Length; at += Chunk)
            Read(f, tree, "alias.bin", at, Math.Min(Chunk, f.Data["a.bin"].Length - at));
        Assert.Equal(decoded, tree.DecodedBlobBytes); Assert.Equal(1, tree.FullBlobProofs);
        tree.Dispose(); Assert.Equal(0, tree.CachedPayloadBytes); Assert.Equal(0, tree.ActiveReaderProcesses);
    }

    [Fact]
    public async Task ProofReadAheadIsBoundedForLargeBlobsAndSurvivesSessionReassignment()
    {
        using var f = await Fixture.Create(); using var tree = f.Open(8 * Chunk, readAhead: 4 * Chunk, diagnostics: true);
        foreach (string name in new[] { "a.bin", "b.bin", "c.bin" }) Read(f, tree, name, 0, 16);
        Assert.Equal(8 * Chunk, tree.PeakChunkPayloadBytes);
        long decoded = tree.DecodedBlobBytes;
        Read(f, tree, "b.bin", Chunk, Chunk); // Session remains reusable; full window survives.
        Assert.Equal(decoded, tree.DecodedBlobBytes);
        Read(f, tree, "a.bin", Chunk, Chunk); // Its window was evicted, proof remains valid.
        Assert.True(tree.DecodedBlobBytes > decoded); Assert.Equal(3, tree.FullBlobProofs);
        Read(f, tree, "c.bin", 8 * Chunk, Chunk); // Beyond the speculative window.
        Assert.InRange(tree.PeakChunkPayloadBytes, 1, 8 * Chunk);
        var observed = Assert.IsType<GitReadObservation>(tree.ObserveReads());
        Assert.Equal(6, observed.Requests); Assert.Equal(48 + 3 * Chunk, observed.RequestedBytes);
        Assert.Equal(0, observed.FailedRequests);
        Assert.Equal(tree.DecodedBlobBytes, observed.FullProofDecodedBytes + observed.ProvedReadDecodedBytes);
        Assert.True(observed.ProvedReadDecodedBytes > 0);
        Assert.True(observed.UnusedReadAheadEvictedBytes > 0);
        Assert.InRange(observed.UnusedReadAheadEvictedBytes, 1, observed.CapacityEvictedBytes);
        Assert.True(observed.FullProofProcessingTicks > 0); Assert.True(observed.ProvedReadProcessingTicks > 0);
        Assert.Equal(0, observed.ActiveReaders); Assert.Equal(0, observed.WaitingReaders);
    }

    [Fact]
    public async Task FailedFullProofNeverPublishesReadAheadAndDrainsItsCache()
    {
        using var f = await Fixture.Create(); using var tree = f.Open(64 * Chunk, readAhead: 32 * Chunk, diagnostics: true);
        await f.Corrupt("a.bin", f.Data["a.bin"].Length - 1);
        byte[] destination = Enumerable.Repeat((byte)217, 16).ToArray();
        Assert.Equal("GitBasisObjectInvalid", Assert.Throws<WorkspaceException>(() => tree.Read("a.bin", 0, destination)).Code);
        Assert.All(destination, b => Assert.Equal((byte)217, b));
        Assert.Equal(f.Data["a.bin"].Length, tree.PeakChunkPayloadBytes);
        Assert.Equal(0, tree.CachedPayloadBytes); Assert.Equal(0, tree.CachedProofCount);
        Assert.Equal(0, tree.ActiveReaderProcesses);
        var observed = Assert.IsType<GitReadObservation>(tree.ObserveReads());
        Assert.Equal(1, observed.Requests); Assert.Equal(1, observed.FailedRequests);
        Assert.Equal(0, observed.FullBlobProofs); Assert.Equal(0, observed.PayloadBytes);
    }

    [Fact]
    public async Task ConsumedReadAheadIsNotCountedAsUnusedOnCapacityEviction()
    {
        using var f = await Fixture.Create(); using var tree = f.Open(8 * Chunk, readAhead: 4 * Chunk, diagnostics: true);
        Read(f, tree, "a.bin", 0, 4 * Chunk);
        Read(f, tree, "b.bin", 0, 4 * Chunk);
        Read(f, tree, "c.bin", 0, 4 * Chunk);
        var observed = Assert.IsType<GitReadObservation>(tree.ObserveReads());
        Assert.True(observed.CapacityEvictedBytes > 0);
        Assert.Equal(0, observed.UnusedReadAheadEvictedBytes);
    }

    [Fact]
    public async Task ReadAheadChunkStillRequiresItsCurrentHashBeforePublication()
    {
        using var f = await Fixture.Create(); using var tree = f.Open(64 * Chunk, readAhead: 32 * Chunk);
        Read(f, tree, "a.bin", 0, 16);
        var cache = (System.Collections.IEnumerable)typeof(GitObjectSnapshotTree)
            .GetField("payloadLru", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(tree)!;
        var chunk = cache.Cast<object>().Single(x => (long)x.GetType().GetProperty("Offset")!.GetValue(x)! == Chunk);
        ((byte[])chunk.GetType().GetProperty("Bytes")!.GetValue(chunk)!)[0] ^= 1;
        byte[] destination = Enumerable.Repeat((byte)217, 16).ToArray();
        Assert.Equal("GitBasisObjectInvalid", Assert.Throws<WorkspaceException>(() => tree.Read("a.bin", Chunk, destination)).Code);
        Assert.All(destination, b => Assert.Equal((byte)217, b));
        Assert.Equal(0, tree.CachedPayloadBytes); Assert.Equal(0, tree.CachedProofCount);
    }

    [Fact]
    public async Task ProofEvictionDiscardsItsEntireReadAheadWindow()
    {
        using var f = await Fixture.Create(); using var tree = f.Open(64 * Chunk, proofEntries: 1, readAhead: 32 * Chunk);
        Read(f, tree, "a.bin", 0, 16); Read(f, tree, "b.bin", 0, 16);
        Assert.Equal(f.Data["b.bin"].Length, tree.CachedChunkPayloadBytes);
        long decoded = tree.DecodedBlobBytes;
        Read(f, tree, "a.bin", Chunk, Chunk);
        Assert.Equal(f.Data["a.bin"].Length, tree.DecodedBlobBytes - decoded);
        Assert.Equal(3, tree.FullBlobProofs);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FirstRequestRetainsCompleteUsefulChunksForCrossingAndAliasReads(bool packed)
    {
        using var f = await Fixture.Create(packed); using var tree = f.Open();
        Read(f, tree, "a.bin", 123, Chunk + 7);
        Assert.Equal(f.Data["a.bin"].Length, tree.DecodedBlobBytes);
        Assert.Equal(2 * Chunk, tree.CachedChunkPayloadBytes);
        long before = tree.DecodedBlobBytes;
        Read(f, tree, "alias.bin", 0, 2 * Chunk);
        Assert.Equal(before, tree.DecodedBlobBytes);
        Assert.Equal(2, tree.PayloadCacheHits); Assert.Equal(1, tree.FullBlobProofs);
        Assert.Equal(1, tree.ReaderProcessStarts);
        Read(f, tree, "a.bin", f.Data["a.bin"].Length - 13, 13);
        tree.Dispose();
        Assert.Equal(0, tree.CachedPayloadBytes); Assert.Equal(0, tree.CachedChunkCount);
        Assert.Equal(0, tree.ActiveReaderProcesses);
    }

    [Fact]
    public async Task ChunkLruSurvivesSessionEvictionAndEvictsBeforeExceedingItsBudget()
    {
        using var f = await Fixture.Create(); using var tree = f.Open(3 * Chunk);
        foreach (string name in new[] { "a.bin", "b.bin", "c.bin" }) Read(f, tree, name, 0, 16);
        long before = tree.DecodedBlobBytes;
        Read(f, tree, "a.bin", 0, 16); // Reassigned session, cached object payload.
        Assert.Equal(before, tree.DecodedBlobBytes);
        Read(f, tree, "d.bin", 0, 16); // Evict b's payload, retaining its proof.
        before = tree.DecodedBlobBytes;
        Read(f, tree, "a.bin", 0, 16);
        Assert.Equal(before, tree.DecodedBlobBytes);
        Read(f, tree, "b.bin", 0, 16);
        Assert.Equal(Chunk, tree.DecodedBlobBytes - before);
        Assert.Equal(4, tree.FullBlobProofs); Assert.Equal(2, tree.PeakReaderProcesses);
        Assert.Equal(3 * Chunk, tree.CachedChunkPayloadBytes);
        Assert.Equal(3 * Chunk, tree.PeakChunkPayloadBytes);
        Assert.Equal(7 * Chunk, tree.CachedPayloadBytes);
        Assert.Equal(3, tree.CachedChunkCount);
    }

    [Fact]
    public async Task ProofEvictionAlsoRemovesItsPayloadAndForcesAFreshFullProof()
    {
        using var f = await Fixture.Create(); using var tree = f.Open(proofEntries: 1);
        Read(f, tree, "a.bin", 0, 2 * Chunk);
        Assert.Equal(2 * Chunk, tree.CachedChunkPayloadBytes);
        Read(f, tree, "b.bin", 0, 16);
        Assert.Equal(Chunk, tree.CachedChunkPayloadBytes); Assert.Equal(1, tree.CachedChunkCount);
        long before = tree.DecodedBlobBytes;
        Read(f, tree, "a.bin", 0, 16);
        Assert.Equal(f.Data["a.bin"].Length, tree.DecodedBlobBytes - before);
        Assert.Equal(3, tree.FullBlobProofs); Assert.Equal(0, tree.PayloadCacheHits);
    }

    [Fact]
    public async Task WholeBlobFailureDiscardsPendingChunksAndNeverPublishesTheRequestedPrefix()
    {
        using var f = await Fixture.Create(); using var tree = f.Open();
        await f.Corrupt("a.bin", f.Data["a.bin"].Length - 1);
        byte[] destination = Enumerable.Repeat((byte)217, 16).ToArray();
        Assert.Equal("GitBasisObjectInvalid", Assert.Throws<WorkspaceException>(() => tree.Read("a.bin", 0, destination)).Code);
        Assert.All(destination, b => Assert.Equal((byte)217, b));
        Assert.Equal(Chunk, tree.PeakChunkPayloadBytes); // Prefix staged before final full hash failed.
        Assert.Equal(0, tree.CachedPayloadBytes); Assert.Equal(0, tree.CachedChunkCount);
        Assert.Equal(0, tree.CachedProofCount); Assert.Equal(0, tree.ActiveReaderProcesses);
    }

    [Fact]
    public async Task RetainedBytesStayImmutableButAnUncachedReadStillRejectsChangedObjectBytes()
    {
        using var f = await Fixture.Create(); using var tree = f.Open();
        Read(f, tree, "a.bin", 0, 16);
        await f.Corrupt("a.bin", 0);
        Read(f, tree, "alias.bin", 0, 16); // Fixed verified bytes; not a fresh object-store audit.
        Assert.Equal(1, tree.PayloadCacheHits);
        Assert.Equal(f.Data["a.bin"].Length, tree.DecodedBlobBytes);
        byte[] destination = Enumerable.Repeat((byte)217, 16).ToArray();
        Assert.Equal("GitBasisObjectInvalid", Assert.Throws<WorkspaceException>(() => tree.Read("a.bin", 2 * Chunk, destination)).Code);
        Assert.All(destination, b => Assert.Equal((byte)217, b));
        Assert.Equal(0, tree.CachedPayloadBytes); Assert.Equal(0, tree.CachedProofCount);
    }

    [Fact]
    public async Task HealthFailureRejectsCachedBytesAndRetiresAllPayload()
    {
        using var f = await Fixture.Create(); using var tree = f.Open();
        Read(f, tree, "a.bin", 0, 16);
        tree.ReadObserver = (_, stage) => { if (stage == "verified") f.Available = false; };
        byte[] destination = Enumerable.Repeat((byte)217, 16).ToArray();
        Assert.Equal("GitBasisUnavailable", Assert.Throws<WorkspaceException>(() => tree.Read("a.bin", 0, destination)).Code);
        Assert.All(destination, b => Assert.Equal((byte)217, b));
        Assert.Equal(1, tree.PayloadCacheHits);
        Assert.Equal(0, tree.CachedPayloadBytes); Assert.Equal(0, tree.ActiveReaderProcesses);
    }

    [Fact]
    public async Task RequestLargerThanCacheBudgetStillVerifiesEveryByteWithBoundedRetention()
    {
        using var f = await Fixture.Create(); using var tree = f.Open(Chunk);
        Read(f, tree, "a.bin", 31, 4 * Chunk);
        Assert.Equal(Chunk, tree.CachedChunkPayloadBytes);
        Assert.Equal(Chunk, tree.PeakChunkPayloadBytes);
        Read(f, tree, "alias.bin", 31, 4 * Chunk);
        Assert.Equal(Chunk, tree.PeakChunkPayloadBytes);
        Assert.Equal(1, tree.FullBlobProofs);
    }

    [Fact]
    public async Task RepeatedPackedBackwardReadCost()
    {
        string? output = Environment.GetEnvironmentVariable("SPLINTER_GIT_PAYLOAD_COST_OUTPUT");
        if (string.IsNullOrWhiteSpace(output)) { Assert.Skip("Set SPLINTER_GIT_PAYLOAD_COST_OUTPUT to a new evidence JSON path."); return; }
        Assert.False(File.Exists(output), "Preserve existing measurement evidence.");
        using var f = await Fixture.Create(packed: true, length: 4 * 1024 * 1024);
        var results = new List<object>();
        foreach (int budget in new[] { 0, 16 * 1024 * 1024 })
        {
            using var tree = f.Open(budget);
            var initial = Stopwatch.StartNew();
            Read(f, tree, "a.bin", 0, 4096); Read(f, tree, "b.bin", 0, 4096);
            initial.Stop(); long initialDecoded = tree.DecodedBlobBytes;
            var timer = Stopwatch.StartNew();
            for (int pass = 0; pass < 8; pass++)
                foreach (int chunk in new[] { 0, 12, 1, 11, 2, 10, 3, 9 })
                    foreach (string name in new[] { "a.bin", "b.bin" }) Read(f, tree, name, chunk * Chunk + 7, 4096);
            timer.Stop();
            var measured = new
            {
                payloadBudgetBytes = budget,
                initialSeconds = initial.Elapsed.TotalSeconds,
                initialDecodedBytes = initialDecoded,
                repeatedSeconds = timer.Elapsed.TotalSeconds,
                repeatedDecodedBytes = tree.DecodedBlobBytes - initialDecoded,
                tree.PayloadCacheHits, tree.PayloadCacheMisses, tree.ReaderProcessStarts,
                tree.FullBlobProofs, tree.PeakReaderProcesses, tree.PeakChunkPayloadBytes, tree.CachedPayloadBytes
            };
            Assert.Equal(2, tree.FullBlobProofs); Assert.InRange(tree.PeakChunkPayloadBytes, 0, budget);
            tree.Dispose(); Assert.Equal(0, tree.CachedPayloadBytes); Assert.Equal(0, tree.ActiveReaderProcesses);
            results.Add(measured);
        }
        string json = JsonSerializer.Serialize(new
        {
            scenario = "two-4MiB-packed-blobs-repeated-backward-4KiB-ranges",
            includes = "Read and exact returned-byte assertion; two initial full proofs reported separately",
            limitations = "One cache-off then cache-on observation; no OS cache clearing, whole-process RAM or Unity qualification",
            results
        }, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(output, json, TestContext.Current.CancellationToken);
        TestContext.Current.TestOutputHelper!.WriteLine(json);
    }

    private static void Read(Fixture f, GitObjectSnapshotTree tree, string path, int offset, int count)
    {
        byte[] actual = new byte[count];
        Assert.Equal(count, tree.Read(path, offset, actual));
        Assert.Equal(f.Data[path].AsSpan(offset, count).ToArray(), actual);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly TestDirectory directory = new();
        private string Source => Path.Combine(directory.Path, "source");
        private SnapshotReference snapshot = null!;
        private string tree = "";
        internal readonly Dictionary<string, byte[]> Data = new();
        internal bool Available = true;
        internal GitObjectSnapshotTree Open(int payloadBytes = 16 * 1024 * 1024, int proofEntries = 4096, int readAhead = 0, bool diagnostics = false) =>
            new(snapshot, tree, "", "", () => new(snapshot.Id, Available, Available ? null : "GitBasisUnavailable", 0, null),
                proofEntryLimit: proofEntries, payloadByteLimit: payloadBytes, proofReadAheadBytes: readAhead, diagnostics: diagnostics);
        internal static async Task<Fixture> Create(bool packed = false, int length = 16 * Chunk + 113)
        {
            var f = new Fixture(); Directory.CreateDirectory(f.Source);
            await TestDirectory.Git(f.Source, "init", "-b", "development");
            await TestDirectory.Git(f.Source, "config", "core.autocrlf", "false");
            await TestDirectory.Git(f.Source, "config", "user.name", "Fixture");
            await TestDirectory.Git(f.Source, "config", "user.email", "fixture@example.invalid");
            foreach (string name in new[] { "a.bin", "b.bin", "c.bin", "d.bin" })
            {
                byte[] bytes = new byte[length]; new Random(name[0]).NextBytes(bytes);
                f.Data.Add(name, bytes); File.WriteAllBytes(Path.Combine(f.Source, name), bytes);
            }
            f.Data.Add("alias.bin", f.Data["a.bin"]); File.WriteAllBytes(Path.Combine(f.Source, "alias.bin"), f.Data["a.bin"]);
            await TestDirectory.Git(f.Source, "add", "."); await TestDirectory.Git(f.Source, "commit", "-m", "basis");
            string revision = await TestDirectory.Git(f.Source, "rev-parse", "HEAD");
            f.tree = await TestDirectory.Git(f.Source, "rev-parse", "HEAD^{tree}");
            string receipt = Path.Combine(f.directory.Path, "receipt");
            var provider = new GitObjectSnapshotProvider(receipt, "payload-cache", revision);
            f.snapshot = await provider.CaptureAsync(new(f.Source, "payload-cache", receipt), TestContext.Current.CancellationToken);
            if (packed) await TestDirectory.Git(f.Source, "gc");
            return f;
        }
        internal async Task Corrupt(string name, int offset)
        {
            string id = await TestDirectory.Git(Source, "rev-parse", "HEAD:" + name);
            string path = Path.Combine(Source, ".git", "objects", id[..2], id[2..]);
            File.SetAttributes(path, FileAttributes.Normal);
            byte[] corrupt = Data[name].ToArray(); corrupt[offset] ^= 1;
            using var file = File.Create(path); using var zipped = new ZLibStream(file, CompressionLevel.Fastest);
            zipped.Write(Encoding.ASCII.GetBytes($"blob {corrupt.Length}\0")); zipped.Write(corrupt);
        }
        public void Dispose() => directory.Dispose();
    }
}
