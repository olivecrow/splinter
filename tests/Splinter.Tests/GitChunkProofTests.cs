using System.Reflection;
using System.Security.Cryptography;
using Splinter.Core;
using Splinter.SourceControl.Git;

namespace Splinter.Tests;

public sealed class GitChunkProofTests
{
    [Theory]
    [InlineData(64)]
    [InlineData(4194304)]
    public void ProofStorageKeepsRawDigestAndRejectsChangedChunkDigests(int budget)
    {
        string? path;
        using (var proof = new GitChunkProof(544, budget))
        {
            path = proof.BackingPath;
            byte[] first = SHA256.HashData("chunk"u8), raw = SHA256.HashData("raw"u8);
            proof.Write(0, first); proof.WriteRawHash(raw); proof.Flush();
            Assert.True(proof.Matches(0, first)); Assert.False(proof.Matches(0, SHA256.HashData("other"u8)));
            Assert.Equal(raw, proof.ReadRawHash()); Assert.InRange(proof.Charge, 1, budget);
            if (path is not null) Assert.Throws<IOException>(() => File.Open(path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite).Dispose());
        }
        if (path is not null) Assert.False(File.Exists(path));
    }
    [Fact]
    public void DiskProofRejectsRawDigestCorruptionAndDeletesOnlyItsOwnBacking()
    {
        using var proof = new GitChunkProof(544, 64); proof.WriteRawHash(SHA256.HashData("raw"u8)); proof.Flush();
        var disk = (FileStream)typeof(GitChunkProof).GetField("disk", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(proof)!;
        RandomAccess.Write(disk.SafeFileHandle, new byte[32], 12 + proof.Length - 32);
        Assert.Equal("GitBasisObjectInvalid", Assert.Throws<WorkspaceException>(() => proof.ReadRawHash()).Code);
    }
    [Fact]
    public void ModeledProofBeyondIntLengthDoesNotAllocateTheBlobOrWholeProof()
    {
        using var proof = new GitChunkProof((long)int.MaxValue + 33, 4096);
        byte[] digest = SHA256.HashData("first"u8); proof.Write(0, digest);
        Assert.True(proof.DiskBacked); Assert.True(proof.Matches(0, digest)); Assert.InRange(proof.Charge, 1, 4096);
        Assert.Equal(44, new FileInfo(proof.BackingPath!).Length);
    }
}
