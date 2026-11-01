using System.Buffers.Binary;
using System.Security.Cryptography;
using Splinter.Core;

namespace Splinter.SourceControl.Git;

// The RAM proof cache is a processing budget, not a blob-size limit. Larger
// hash tables live in a private delete-on-close metadata file; no blob bytes
// are hydrated. One exclusive blob lease owns construction and publication.
internal sealed class GitChunkProof : IDisposable
{
    private const int HeaderBytes = 12;
    private readonly byte[]? memory;
    private readonly FileStream? disk;
    public long Length { get; }
    public int Charge { get; }
    public bool DiskBacked => disk is not null;
    internal string? BackingPath => disk?.Name;
    public GitChunkProof(long length, int memoryLimit)
    {
        if (length < 32 || length % 32 != 0 || memoryLimit < 1) throw new ArgumentOutOfRangeException(nameof(length));
        Length = length;
        if (length <= memoryLimit) { memory = new byte[(int)length]; Charge = memory.Length; return; }
        Charge = Math.Min(256, memoryLimit);
        disk = new(Path.Combine(Path.GetTempPath(), "splinter-git-proof-" + Guid.NewGuid().ToString("N") + ".bin"),
            FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.RandomAccess | FileOptions.DeleteOnClose);
        Span<byte> header = stackalloc byte[HeaderBytes]; "SGP1"u8.CopyTo(header); BinaryPrimitives.WriteInt64LittleEndian(header[4..], length);
        try { disk.Write(header); } catch { disk.Dispose(); throw; }
    }
    public void Write(long offset, ReadOnlySpan<byte> digest)
    {
        RequireRange(offset, digest.Length);
        if (memory is not null) digest.CopyTo(memory.AsSpan((int)offset));
        else RandomAccess.Write(disk!.SafeFileHandle, digest, checked(HeaderBytes + offset));
    }
    public bool Matches(long offset, ReadOnlySpan<byte> digest)
    {
        RequireRange(offset, digest.Length);
        if (memory is not null) return digest.SequenceEqual(memory.AsSpan((int)offset, digest.Length));
        Span<byte> expected = stackalloc byte[32]; Read(offset, expected); return digest.SequenceEqual(expected);
    }
    public void WriteRawHash(ReadOnlySpan<byte> digest)
    {
        Write(Length - 32, digest);
        if (disk is not null)
        { Span<byte> checksum = stackalloc byte[32]; SHA256.HashData(digest, checksum); RandomAccess.Write(disk.SafeFileHandle, checksum, checked(HeaderBytes + Length)); }
    }
    public byte[] ReadRawHash()
    {
        byte[] raw = new byte[32]; Read(Length - 32, raw);
        if (disk is not null)
        {
            Span<byte> checksum = stackalloc byte[32], stored = stackalloc byte[32];
            ReadExactly(disk.SafeFileHandle, stored, checked(HeaderBytes + Length)); SHA256.HashData(raw, checksum);
            if (!checksum.SequenceEqual(stored)) throw Invalid();
        }
        return raw;
    }
    public void Flush() => disk?.Flush(true);
    private void Read(long offset, Span<byte> bytes)
    {
        RequireRange(offset, bytes.Length);
        if (memory is not null) memory.AsSpan((int)offset, bytes.Length).CopyTo(bytes);
        else
        {
            Span<byte> header = stackalloc byte[HeaderBytes]; ReadExactly(disk!.SafeFileHandle, header, 0);
            if (!header[..4].SequenceEqual("SGP1"u8) || BinaryPrimitives.ReadInt64LittleEndian(header[4..]) != Length) throw Invalid();
            ReadExactly(disk.SafeFileHandle, bytes, checked(HeaderBytes + offset));
        }
    }
    private static void ReadExactly(Microsoft.Win32.SafeHandles.SafeFileHandle file, Span<byte> bytes, long offset)
    {
        int at = 0;
        while (at < bytes.Length)
        { int read = RandomAccess.Read(file, bytes[at..], offset + at); if (read == 0) throw Invalid(); at += read; }
    }
    private void RequireRange(long offset, int bytes)
    { if (offset < 0 || offset % 32 != 0 || bytes != 32 || offset > Length - bytes) throw Invalid(); }
    public void Dispose() => disk?.Dispose();
    private static WorkspaceException Invalid() => new("GitBasisObjectInvalid", "The owned Git chunk proof is invalid; no requested bytes may be published.");
}
