using System.Security.Cryptography;
using Splinter.Core;

namespace Splinter.Runtime;

// Request-local content proofs. Ownership is checked by the caller on EVERY
// open. Only byte hashing can be reused under an uninterrupted native fence.
public sealed partial class ImmutableReadProofs(IJournalFilePins pins) : IDisposable
{
    private readonly object gate = new();
    private readonly Dictionary<(string Path, string Identity, long Length), FileStream> fences = [];
    private readonly HashSet<(string Path, string Identity, long FileLength, long Offset, long Length, string Hash)> ranges = [];
    private bool disposed;
    internal long HashAdmissions { get; private set; }
    internal long HashReuses { get; private set; }
    public void Verify(FileStream input, string identity, long offset, long length, string hash, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (offset < 0 || length < 0 || offset > input.Length - length)
            throw new WorkspaceException("ResultContentInvalid", "The immutable proof range exceeds its backing.");
        var file = (input.Name, identity, input.Length);
        var range = (input.Name, identity, input.Length, offset, length, hash);
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (ranges.Contains(range)) { HashReuses++; return; }
        }
        // The caller already proved input's native identity and denies writes
        // and deletion. Hash outside the cache lock; duplicate admissions are OK.
        using var digest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] buffer = new byte[65536]; long position = offset, remaining = length;
        while (remaining > 0)
        {
            ct.ThrowIfCancellationRequested();
            int count = RandomAccess.Read(input.SafeFileHandle, buffer.AsSpan(0, (int)Math.Min(buffer.Length, remaining)), position);
            if (count <= 0) throw new WorkspaceException("ResultContentInvalid", "An immutable proof range ended early.");
            digest.AppendData(buffer.AsSpan(0, count)); position += count; remaining -= count;
        }
        if (Convert.ToHexStringLower(digest.GetHashAndReset()) != hash)
            throw new WorkspaceException("ResultContentInvalid", "The independently retained bytes changed.");
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this); HashAdmissions++;
            if (ranges.Count >= 4096) return;
            if (!fences.ContainsKey(file))
            {
                if (fences.Count >= 64) return;
                var fence = new FileStream(input.Name, FileMode.Open, FileAccess.Read, FileShare.Read, 1, FileOptions.RandomAccess);
                try { pins.Verify(fence, identity); if (fence.Length != input.Length) throw new IOException("Immutable backing length changed."); fences.Add(file, fence); }
                catch { fence.Dispose(); throw; }
            }
            ranges.Add(range);
        }
    }
    public void Dispose()
    {
        lock (gate)
        {
            if (disposed) return; disposed = true;
            foreach (var reader in metadata.Values) reader.Dispose(); metadata.Clear();
            foreach (var fence in fences.Values) fence.Dispose(); fences.Clear(); ranges.Clear();
        }
    }
}
