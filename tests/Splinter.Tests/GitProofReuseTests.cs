using System.IO.Compression;
using System.Text;
using Splinter.Core;
using Splinter.SourceControl.Git;

namespace Splinter.Tests;

public sealed class GitProofReuseTests
{
    [Fact]
    public async Task InsufficientProofBudgetRejectsBeforePayloadDecoding()
    {
        using var f = await Fixture.Create(); using var tree = f.Open(511);
        byte[] bytes = Enumerable.Repeat((byte)217, 16).ToArray();
        Assert.Equal("GitBasisUnsupported", Assert.Throws<WorkspaceException>(() => tree.Read("a.bin", 0, bytes)).Code);
        Assert.All(bytes, b => Assert.Equal((byte)217, b));
        Assert.Equal(0, tree.DecodedBlobBytes); Assert.Equal(0, tree.CachedProofBytes);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AlternatingBlobsReuseOnlyProofsAndAliasesShareTheirProof(bool packed)
    {
        using var f = await Fixture.Create(packed);
        using var tree = f.Open();
        byte[] bytes = new byte[16];
        tree.Read("a.bin", 0, bytes); tree.Read("b.bin", 0, bytes);
        long before = tree.DecodedBlobBytes;
        tree.Read("a.bin", 0, bytes);
        Assert.Equal(f.A[..16], bytes);
        Assert.Equal(65536, tree.DecodedBlobBytes - before);
        Assert.Equal(2, tree.FullBlobProofs); Assert.Equal(2, tree.CachedProofCount);
        Assert.Equal(1024, tree.CachedProofBytes);
        before = tree.DecodedBlobBytes;
        tree.Read("alias.bin", 0, bytes);
        Assert.Equal(f.A[..16], bytes); Assert.Equal(before, tree.DecodedBlobBytes);
        Assert.Equal(0, tree.Read("a.bin", f.A.Length, bytes));
        Assert.Equal(2, tree.CachedProofCount);
        Assert.Empty(Directory.GetFiles(f.Receipt, "*.bin", SearchOption.AllDirectories));
    }

    [Theory]
    [InlineData(1024, 4096)]
    [InlineData(4194304, 2)]
    public async Task EvictionIsLruAndInflightProofStaysWithinTheByteBudget(int bytes, int entries)
    {
        using var f = await Fixture.Create(); using var tree = f.Open(bytes, entries);
        foreach (string name in new[] { "a.bin", "b.bin", "a.bin", "c.bin" }) tree.Read(name, 0, new byte[16]);
        Assert.Equal(3, tree.FullBlobProofs);
        tree.Read("a.bin", 0, new byte[16]); Assert.Equal(3, tree.FullBlobProofs);
        tree.Read("b.bin", 0, new byte[16]); Assert.Equal(4, tree.FullBlobProofs);
        Assert.InRange(tree.CachedProofCount, 1, 2); Assert.InRange(tree.PeakProofBytes, 512, bytes);
        tree.Dispose(); Assert.Equal(0, tree.CachedProofBytes); Assert.Equal(0, tree.CachedProofCount);
    }

    [Fact]
    public async Task CorruptCachedBlobAfterSwitchPublishesNothingAndInvalidatesEveryProof()
    {
        using var f = await Fixture.Create(); using var tree = f.Open();
        tree.Read("a.bin", 0, new byte[16]); tree.Read("b.bin", 0, new byte[16]);
        string id = await TestDirectory.Git(f.Source, "rev-parse", "HEAD:a.bin");
        string path = Path.Combine(f.Source, ".git", "objects", id[..2], id[2..]);
        File.SetAttributes(path, FileAttributes.Normal);
        byte[] corrupt = f.A.ToArray(); corrupt[65540] ^= 1;
        using (var output = File.Create(path))
        using (var zipped = new ZLibStream(output, CompressionLevel.Fastest))
        { zipped.Write(Encoding.ASCII.GetBytes($"blob {corrupt.Length}\0")); zipped.Write(corrupt); }
        byte[] destination = Enumerable.Repeat((byte)217, 70000).ToArray();
        Assert.Equal("GitBasisObjectInvalid", Assert.Throws<WorkspaceException>(() => tree.Read("a.bin", 0, destination)).Code);
        Assert.All(destination, b => Assert.Equal((byte)217, b));
        Assert.Equal(0, tree.CachedProofBytes); Assert.Equal(0, tree.CachedProofCount);
        tree.Read("b.bin", 0, new byte[16]); Assert.Equal(3, tree.FullBlobProofs);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly TestDirectory directory = new();
        public string Source => Path.Combine(directory.Path, "source");
        public string Receipt => Path.Combine(directory.Path, "receipt");
        public byte[] A { get; } = Enumerable.Range(0, 1048576).Select(i => (byte)(i * 31)).ToArray();
        private SnapshotReference snapshot = null!;
        private string tree = "";
        public GitObjectSnapshotTree Open(int bytes = 4194304, int entries = 4096) =>
            new(snapshot, tree, "", "", () => new(snapshot.Id, true, null, 0, null), bytes, entries);
        public static async Task<Fixture> Create(bool packed = false)
        {
            var f = new Fixture(); Directory.CreateDirectory(f.Source);
            await TestDirectory.Git(f.Source, "init", "-b", "development");
            await TestDirectory.Git(f.Source, "config", "core.autocrlf", "false");
            await TestDirectory.Git(f.Source, "config", "user.name", "Fixture");
            await TestDirectory.Git(f.Source, "config", "user.email", "fixture@example.invalid");
            foreach (string name in new[] { "a.bin", "alias.bin", "b.bin", "c.bin" })
            {
                byte[] data = f.A.ToArray(); if (name is "b.bin" or "c.bin") data[0] = (byte)name[0];
                File.WriteAllBytes(Path.Combine(f.Source, name), data);
            }
            await TestDirectory.Git(f.Source, "add", "."); await TestDirectory.Git(f.Source, "commit", "-m", "basis");
            string revision = await TestDirectory.Git(f.Source, "rev-parse", "HEAD");
            f.tree = await TestDirectory.Git(f.Source, "rev-parse", "HEAD^{tree}");
            var provider = new GitObjectSnapshotProvider(f.Receipt, "proof-reuse", revision);
            f.snapshot = await provider.CaptureAsync(new(f.Source, "proof-reuse", f.Receipt), TestContext.Current.CancellationToken);
            if (packed) await TestDirectory.Git(f.Source, "gc");
            return f;
        }
        public void Dispose() => directory.Dispose();
    }
}
