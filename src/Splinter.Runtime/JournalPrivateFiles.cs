using System.Security.Cryptography;
using System.Text.Json;
using Splinter.Core;

namespace Splinter.Runtime;

public sealed partial class JournalDeltaStore
{
    internal sealed record PrivateRange(long Offset, byte[] Bytes);
    internal sealed record PrivateFile(DeltaEntry Entry, PrivateRange[] Ranges, string Hash);

    internal PrivateFile ReadPrivateFile(Guid id)
    {
        using (EnterGate())
        {
            Check(); var node = RequiredFile(id);
            if (node.Entry.BaseLength != 0 || node.Extents.Sum(e => (long)e.Length) > 64L * 1024 * 1024 || node.Extents.Count > 8192)
                throw new WorkspaceException("ViewPartitionBoundary", "Only wholly private file ranges up to 64 MiB and 8192 extents can cross view partitions; no base-file copy-up.");
            using var digest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            digest.AppendData(JsonSerializer.SerializeToUtf8Bytes(node.Entry with { Path = "", BasePath = null, Deleted = false }));
            var ranges = new List<PrivateRange>();
            foreach (var extent in node.Extents)
            {
                byte[] data = new byte[extent.Length]; ReadExactly(extent.JournalOffset, data);
                digest.AppendData(JsonSerializer.SerializeToUtf8Bytes(new { Offset = extent.Start, Length = extent.Length }));
                digest.AppendData(data); ranges.Add(new(extent.Start, data));
            }
            return new(node.Entry, [.. ranges], Convert.ToHexStringLower(digest.GetHashAndReset()));
        }
    }

    internal DeltaEntry? FindPrivateIdentity(Guid id)
    { using (EnterGate()) { Check(); return nodes.TryGetValue(id, out var node) ? node.Entry : null; } }

    internal void CreatePrivateTransferTarget(PrivateFile file, string staging)
    {
        using (EnterGate())
        {
            Check(); staging = Normalize(staging);
            if (nodes.TryGetValue(file.Entry.Id, out var prior) && !prior.Entry.Deleted || Lookup(staging) is not null)
                throw new WorkspaceException("ViewTransferCollision", "Preserve existing file identities at the transfer destination.");
            RequireParent(staging); RequireNodeCapacity();
            Commit(new(sequence + 1, "create", file.Entry with { Path = staging, BasePath = null, BaseLength = 0, Deleted = false }), []);
        }
    }

    internal void FillPrivateTransferTarget(PrivateFile file)
    {
        using (EnterGate())
        {
            Check(); SetLength(file.Entry.Id, 0); SetLength(file.Entry.Id, file.Entry.Length);
            foreach (var range in file.Ranges)
                for (int at = 0; at < range.Bytes.Length; at += Identity.MaxTransferBytes)
                    Write(file.Entry.Id, range.Offset + at, range.Bytes.AsSpan(at, Math.Min(Identity.MaxTransferBytes, range.Bytes.Length - at)));
            SetMetadata(file.Entry.Id, file.Entry.Attributes, file.Entry.CreationTime, file.Entry.LastAccessTime, file.Entry.LastWriteTime);
        }
    }
}
