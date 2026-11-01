using System.Buffers.Binary;
using System.Security.Cryptography;
using Splinter.Core;

namespace Splinter.Runtime;

public sealed partial class PagedMetadataStore
{
    internal static PagedMetadataStore OpenStoppedAudit(string directory, IJournalFilePins pins) =>
        new(directory, false, null, pins, maintenance: true, containerStart: null);
    // This verifies references, never ownership of an otherwise unknown file.
    // The caller already excludes index readers/writers. Maintenance readers
    // intentionally borrow that outer fence instead of reopening writer.lock.
    internal static void VerifyStoppedGraph(string directory, IJournalFilePins pins, IEnumerable<MetadataTreeRoot> roots)
    {
        using var store = new PagedMetadataStore(directory, false, null, pins, maintenance: true, containerStart: null);
        using var pending = new TraversalSpine();
        foreach (var root in roots)
        {
            store.CheckRoot(root); if (root.Page is { } page) pending.Push(page);
            while (pending.TryPop(out var reference))
            {
                // Enumerate checks every page hash, subtree count, key order,
                // uniqueness and final count. Entry-owned trees are separate
                // ordered namespaces and receive the same complete check.
                foreach (var entry in store.Open(new(1, store.StoreId, reference)).Enumerate())
                    if (entry.Child?.Page is { } child) pending.Push(child);
            }
        }
    }

    // A narrow stopped-replay recovery may preserve this complete container
    // as opaque evidence. It must not adopt it, read it as an owned graph, or
    // delete it. A random 49-byte prefix is deliberately not eligible.
    internal static void VerifyUnownedStoppedContainer(string path, string storeId)
    {
        using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1, FileOptions.RandomAccess);
        if (input.Length <= ContainerHeaderBytes || File.Exists(path + ".owner")) throw Invalid("Preserve an unsupported unowned metadata container.");
        Span<byte> prefix = stackalloc byte[ContainerHeaderBytes]; input.ReadExactly(prefix);
        if (!prefix[..4].SequenceEqual(FormatMagic) || Convert.ToHexStringLower(prefix.Slice(4, 12)) != storeId ||
            !SHA256.HashData(prefix[..16]).AsSpan().SequenceEqual(prefix[16..])) throw Invalid("Preserve a foreign unowned metadata container.");
        long cursor = ContainerHeaderBytes; long number = ParseContainer(path);
        Span<byte> header = stackalloc byte[PageHeaderBytes];
        while (cursor < input.Length)
        {
            if (input.Length - cursor < PageHeaderBytes) throw Invalid("Preserve an incomplete opaque metadata page.");
            ReadAt(input, header, cursor); int length = BinaryPrimitives.ReadInt32LittleEndian(header[4..]);
            if (!header[..4].SequenceEqual("SPN1"u8) || length < 9 || length > MaximumPageBytes - PageHeaderBytes ||
                input.Length - cursor - PageHeaderBytes < length) throw Invalid("Preserve an invalid opaque metadata page.");
            byte[] body = new byte[length]; ReadAt(input, body, cursor + PageHeaderBytes);
            if (!SHA256.HashData(body).AsSpan().SequenceEqual(header[8..])) throw Invalid("Preserve a damaged opaque metadata page.");
            var node = Decode(body);
            foreach (var child in new[] { node.Left, node.Right, node.Data })
                if (child is not null && (child.Container > number || child.Container == number && child.Offset >= cursor))
                    throw Invalid("Preserve a cyclic opaque metadata page.");
            cursor = checked(cursor + PageHeaderBytes + length);
        }
    }
}
