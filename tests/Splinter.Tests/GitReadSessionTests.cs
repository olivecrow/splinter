using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using Splinter.Core;
using Splinter.SourceControl.Git;

namespace Splinter.Tests;

public sealed class GitReadSessionTests
{
    private const int Chunk = 65536;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InterleavedForwardReadsKeepBothDecodersAndOnlyDecodeNewChunks(bool packed)
    {
        using var f = await Fixture.Create(packed); using var tree = f.Open();
        foreach (string name in new[] { "a.bin", "b.bin" }) Read(f, tree, name, 0);
        long before = tree.DecodedBlobBytes;
        for (int chunk = 0; chunk < 4; chunk++)
            foreach (string name in new[] { "a.bin", "b.bin" }) Read(f, tree, name, chunk * Chunk);
        Assert.Equal(8L * Chunk, tree.DecodedBlobBytes - before);
        Assert.Equal(2, tree.ReaderProcessStarts); Assert.Equal(2, tree.ActiveReaderProcesses);
        Assert.Equal(2, tree.PeakReaderProcesses); Assert.Equal(4 * Chunk, tree.CachedPayloadBytes);
        Assert.Equal(2, tree.FullBlobProofs);
        Assert.Empty(Directory.GetFiles(f.Receipt, "*.bin", SearchOption.AllDirectories));
        int[] pids = tree.ReaderProcessIds;
        tree.Dispose(); AssertExited(pids);
        Assert.Equal(0, tree.ActiveReaderProcesses); Assert.Equal(0, tree.CachedPayloadBytes);
        Assert.Equal(0, tree.CachedProofBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BackwardReadsWithinTwoChunksReuseBytesAndOlderReadsRestartTheOwnedDecoder(bool packed)
    {
        using var f = await Fixture.Create(packed); using var tree = f.Open();
        Read(f, tree, "a.bin", 0); // Full blob proof, retaining its tail.
        Read(f, tree, "a.bin", 0); Read(f, tree, "a.bin", Chunk);
        long before = tree.DecodedBlobBytes;
        Read(f, tree, "alias.bin", 0);
        Assert.Equal(before, tree.DecodedBlobBytes); Assert.Equal(1, tree.ReaderProcessStarts);
        Read(f, tree, "a.bin", 2 * Chunk);
        int[] retired = tree.ReaderProcessIds;
        before = tree.DecodedBlobBytes;
        Read(f, tree, "a.bin", 0); // Outside the retained window.
        Assert.Equal(Chunk, tree.DecodedBlobBytes - before);
        Assert.Equal(2, tree.ReaderProcessStarts); Assert.Equal(1, tree.PeakReaderProcesses);
        AssertExited(retired); Assert.Equal(1, tree.FullBlobProofs);
        // Requests crossing chunks still stage the entire request correctly.
        byte[] crossing = new byte[Chunk + 32];
        Assert.Equal(crossing.Length, tree.Read("a.bin", Chunk - 16, crossing));
        Assert.Equal(f.Data("a.bin").AsSpan(Chunk - 16, crossing.Length).ToArray(), crossing);
    }

    [Fact]
    public async Task SessionEvictionClosesTheLruChildAndPreservesOnlyBoundedBuffers()
    {
        using var f = await Fixture.Create(); using var tree = f.Open();
        Read(f, tree, "a.bin", 0); int a = Assert.Single(tree.ReaderProcessIds);
        Read(f, tree, "b.bin", 0); int b = Assert.Single(tree.ReaderProcessIds.Except([a]));
        Read(f, tree, "b.bin", 0); // Make the LRU victim a paused decoder.
        Read(f, tree, "a.bin", 0); Read(f, tree, "c.bin", 0);
        AssertExited([b]); Assert.Contains(a, tree.ReaderProcessIds);
        Assert.Equal(2, tree.ActiveReaderProcesses); Assert.Equal(2, tree.PeakReaderProcesses);
        Assert.Equal(4 * Chunk, tree.CachedPayloadBytes); Assert.Equal(3, tree.CachedProofCount);
        long before = tree.DecodedBlobBytes;
        Read(f, tree, "b.bin", 0);
        Assert.Equal(Chunk, tree.DecodedBlobBytes - before); Assert.Equal(3, tree.FullBlobProofs);
        Assert.Equal(4, tree.ReaderProcessStarts); Assert.Equal(2, tree.PeakReaderProcesses);
    }

    [Fact]
    public async Task DistinctSmallBlobsReuseDrainedBatchReadersWithoutProcessChurn()
    {
        using var f = await Fixture.Create(); using var tree = f.Open();
        foreach (int i in Enumerable.Range(0, 12))
        {
            byte[] actual = new byte[16];
            Assert.Equal(16, tree.Read($"small-{i}.bin", 0, actual));
            Assert.All(actual, b => Assert.Equal((byte)i, b));
        }
        Assert.Equal(12, tree.FullBlobProofs); Assert.Equal(12 * 16, tree.DecodedBlobBytes);
        Assert.Equal(2, tree.ReaderProcessStarts); Assert.Equal(2, tree.ActiveReaderProcesses);
        Assert.Equal(4 * Chunk, tree.CachedPayloadBytes);
    }

    [Fact]
    public async Task ProofEvictionAlsoRetiresItsDecoderAndRetainedPayload()
    {
        using var f = await Fixture.Create(); using var tree = f.Open(proofEntries: 1);
        Read(f, tree, "a.bin", 0); Read(f, tree, "a.bin", 0);
        int[] retired = tree.ReaderProcessIds;
        Read(f, tree, "b.bin", 0);
        AssertExited(retired); Assert.Equal(1, tree.ActiveReaderProcesses);
        Assert.Equal(2 * Chunk, tree.CachedPayloadBytes); Assert.Equal(1, tree.CachedProofCount);
        Assert.Equal(1, tree.PeakReaderProcesses);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1048576)]
    public async Task FailedChunkPublishesNoPartialRequestAndClosesEveryReader(int payloadBytes)
    {
        using var f = await Fixture.Create(); using var tree = f.Open(payloadBytes: payloadBytes);
        Read(f, tree, "a.bin", 0); Read(f, tree, "b.bin", 0); Read(f, tree, "b.bin", 0);
        int[] pids = tree.ReaderProcessIds;
        string id = await TestDirectory.Git(f.Source, "rev-parse", "HEAD:a.bin");
        string path = Path.Combine(f.Source, ".git", "objects", id[..2], id[2..]);
        File.SetAttributes(path, FileAttributes.Normal);
        byte[] corrupt = f.Data("a.bin").ToArray(); corrupt[Chunk + 4] ^= 1;
        using (var output = File.Create(path))
        using (var zipped = new ZLibStream(output, CompressionLevel.Fastest))
        { zipped.Write(Encoding.ASCII.GetBytes($"blob {corrupt.Length}\0")); zipped.Write(corrupt); }
        byte[] destination = Enumerable.Repeat((byte)217, Chunk + 32).ToArray();
        Assert.Equal("GitBasisObjectInvalid", Assert.Throws<WorkspaceException>(() => tree.Read("a.bin", 0, destination)).Code);
        Assert.All(destination, b => Assert.Equal((byte)217, b)); AssertExited(pids);
        Assert.Equal(0, tree.ActiveReaderProcesses); Assert.Equal(0, tree.CachedPayloadBytes);
        Assert.Equal(0, tree.CachedProofBytes); Assert.Equal(0, tree.CachedProofCount);
        Read(f, tree, "b.bin", 0); Assert.Equal(3, tree.FullBlobProofs);
    }

    private static void Read(Fixture f, GitObjectSnapshotTree tree, string name, int offset)
    {
        byte[] bytes = new byte[16];
        Assert.Equal(bytes.Length, tree.Read(name, offset, bytes));
        Assert.Equal(f.Data(name).AsSpan(offset, bytes.Length).ToArray(), bytes);
    }
    private static void AssertExited(IEnumerable<int> pids)
    {
        foreach (int pid in pids)
        {
            try { using var process = Process.GetProcessById(pid); Assert.True(process.HasExited, $"Owned reader {pid} remains alive."); }
            catch (ArgumentException) { }
        }
    }
    private sealed class Fixture : IDisposable
    {
        private readonly TestDirectory directory = new();
        public string Source => Path.Combine(directory.Path, "source");
        public string Receipt => Path.Combine(directory.Path, "receipt");
        private SnapshotReference snapshot = null!;
        private string tree = "";
        private readonly Dictionary<string, byte[]> data = new();
        private Fixture()
        {
            byte[] bytes = Enumerable.Range(0, 16 * Chunk).Select(i => (byte)(i * 31 + i / Chunk)).ToArray();
            data.Add("a.bin", bytes); data.Add("alias.bin", bytes);
            foreach (string name in new[] { "b.bin", "c.bin" })
            { byte[] different = bytes.ToArray(); different[0] = (byte)name[0]; data.Add(name, different); }
        }
        public byte[] Data(string name) => data[name];
        // Decoder-window regressions deliberately bypass the optional RAM LRU.
        public GitObjectSnapshotTree Open(int proofEntries = 4096, int payloadBytes = 0) =>
            new(snapshot, tree, "", "", () => new(snapshot.Id, true, null, 0, null), proofEntryLimit: proofEntries, payloadByteLimit: payloadBytes);
        public static async Task<Fixture> Create(bool packed = false)
        {
            var f = new Fixture(); Directory.CreateDirectory(f.Source);
            await TestDirectory.Git(f.Source, "init", "-b", "development");
            await TestDirectory.Git(f.Source, "config", "core.autocrlf", "false");
            await TestDirectory.Git(f.Source, "config", "user.name", "Fixture");
            await TestDirectory.Git(f.Source, "config", "user.email", "fixture@example.invalid");
            foreach (string name in new[] { "a.bin", "alias.bin", "b.bin", "c.bin" }) File.WriteAllBytes(Path.Combine(f.Source, name), f.Data(name));
            foreach (int i in Enumerable.Range(0, 12)) File.WriteAllBytes(Path.Combine(f.Source, $"small-{i}.bin"), Enumerable.Repeat((byte)i, 16).ToArray());
            await TestDirectory.Git(f.Source, "add", "."); await TestDirectory.Git(f.Source, "commit", "-m", "basis");
            string revision = await TestDirectory.Git(f.Source, "rev-parse", "HEAD");
            f.tree = await TestDirectory.Git(f.Source, "rev-parse", "HEAD^{tree}");
            var provider = new GitObjectSnapshotProvider(f.Receipt, "reader-sessions", revision);
            f.snapshot = await provider.CaptureAsync(new(f.Source, "reader-sessions", f.Receipt), TestContext.Current.CancellationToken);
            if (packed) await TestDirectory.Git(f.Source, "gc");
            return f;
        }
        public void Dispose() => directory.Dispose();
    }
}
