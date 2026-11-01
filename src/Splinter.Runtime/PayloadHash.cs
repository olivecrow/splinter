using System.Buffers;
using System.Security.Cryptography;

namespace Splinter.Runtime;

// Bounded reads amortize immutable-reader admission costs without retaining
// bytes or proofs across operations. Cancellation never returns a digest.
public static class PayloadHash
{
    public static string Sha256(Stream input, CancellationToken ct)
    {
        byte[] buffer = ArrayPool<byte>.Shared.Rent(1024 * 1024);
        try
        {
            using var digest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            while (true)
            {
                ct.ThrowIfCancellationRequested();
                int count = input.Read(buffer.AsSpan(0, 1024 * 1024));
                ct.ThrowIfCancellationRequested();
                if (count == 0) return Convert.ToHexStringLower(digest.GetHashAndReset());
                digest.AppendData(buffer.AsSpan(0, count));
            }
        }
        finally { ArrayPool<byte>.Shared.Return(buffer); }
    }
}
