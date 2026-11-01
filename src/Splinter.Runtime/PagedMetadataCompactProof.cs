using System.Buffers.Binary;
using System.Security.Cryptography;
using Splinter.Core;

namespace Splinter.Runtime;

public sealed partial class PagedMetadataStore
{
    // An immutable flat bulk tree has nothing to collect when its graph covers
    // every owned container byte. Prove that fact, not merely a byte/count hint.
    // Unknown work, relocation or an unfinished collection uses ordinary GC.
    public static bool VerifyCompactFlatTree(string directory, MetadataTreeRoot root,
        IJournalFilePins pins, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (File.Exists(Path.Combine(directory, "collection.json")) || File.Exists(Path.Combine(directory, "locations.json")) ||
            Directory.Exists(Path.Combine(directory, "collections"))) return false;
        // Read-only certification permits readers but excludes writers and GC.
        // The owner retains its publication lock across this call and fallback.
        using var exclusive = new FileStream(Path.Combine(directory, "writer.lock"), FileMode.Open, FileAccess.Read, FileShare.Read);
        foreach (string path in Directory.EnumerateFileSystemEntries(directory))
        {
            ct.ThrowIfCancellationRequested(); WorkspaceStore.EnsurePlainPath(path);
            string name = Path.GetFileName(path);
            if (Directory.Exists(path)) return false;
            if (name is "identity.bin" or "writer.lock") continue;
            if (name.StartsWith("pages-", StringComparison.Ordinal) && name.EndsWith(".bin", StringComparison.Ordinal)) continue;
            if (name.StartsWith("pages-", StringComparison.Ordinal) && name.EndsWith(".bin.owner", StringComparison.Ordinal) && File.Exists(path[..^6])) continue;
            return false;
        }
        using var source = new PagedMetadataStore(directory, false, null, pins, maintenance: true, containerStart: null);
        foreach (var entry in source.Open(root).Enumerate())
        { ct.ThrowIfCancellationRequested(); if (entry.Child is not null) return false; }
        using var scratch = new TemporaryMetadataDirectory();
        IEnumerable<MetadataEntry> References()
        {
            using var pending = new TraversalSpine();
            if (root.Page is { } first) pending.Push(first);
            while (pending.TryPop(out var reference))
            {
                ct.ThrowIfCancellationRequested(); var node = source.ReadNode(reference);
                if (node.Data is not null) throw Invalid("A compact flat tree contains an owned child.");
                yield return new(PhysicalKey(reference), EncodeReference(reference));
                if (node.Left is { } left) pending.Push(left);
                if (node.Right is { } right) pending.Push(right);
            }
        }
        using var references = MetadataSort.Order(References(), scratch.Path, ct).GetEnumerator();
        bool hasReference = references.MoveNext();
        var containers = Directory.EnumerateFiles(directory, "pages-*.bin").Select(path => new MetadataEntry(Path.GetFileName(path), ReadOnlyMemory<byte>.Empty));
        foreach (var file in MetadataSort.Order(containers, scratch.Path, ct))
        {
            ct.ThrowIfCancellationRequested(); string path = Path.Combine(directory, file.Key); long number = ParseContainer(path);
            using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1, FileOptions.RandomAccess);
            try { source.VerifyContainerOwner(input, number); }
            catch (Exception error) when (input.Length <= ContainerHeaderBytes && error is IOException or WorkspaceException)
            { return false; } // Ordinary GC preserves incomplete ownership admission.
            source.CheckContainer(input); long cursor = ContainerHeaderBytes;
            while (hasReference)
            {
                var reference = DecodeReference(references.Current.Value.Span);
                if (reference.Container > number) break;
                if (reference.Container != number || reference.Offset != cursor) return false;
                if (cursor > input.Length - reference.Bytes) throw Invalid("A compact tree page was truncated.");
                byte[] bytes = new byte[reference.Bytes]; ReadAt(input, bytes, cursor);
                var header = bytes.AsSpan(0, PageHeaderBytes); var body = bytes.AsSpan(PageHeaderBytes);
                if (!header[..4].SequenceEqual("SPN1"u8) || BinaryPrimitives.ReadInt32LittleEndian(header[4..]) != body.Length ||
                    !SHA256.HashData(body).AsSpan().SequenceEqual(header[8..])) throw Invalid("A compact tree page changed.");
                var node = Decode(body);
                if (reference.ContentHash != Convert.ToHexStringLower(LogicalHash(node)) ||
                    reference.Count != checked(1 + (node.Left?.Count ?? 0) + (node.Right?.Count ?? 0))) throw Invalid("A compact tree logical identity changed.");
                cursor = checked(cursor + reference.Bytes); hasReference = references.MoveNext();
            }
            if (cursor != input.Length) return false; // Unreachable/torn tail: preserve and use normal collection.
        }
        return !hasReference;
    }
}
