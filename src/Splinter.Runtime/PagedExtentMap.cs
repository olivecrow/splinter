using System.Buffers.Binary;
using System.Globalization;
using Splinter.Core;

namespace Splinter.Runtime;

// Slices refer to immutable content objects; a partial overwrite changes the
// interval index, never the retained object's bytes or the previous map root.
public sealed record PagedExtent(long Start, int Length, string ContentHash, int ObjectLength, int ObjectOffset = 0)
{
    public long End => checked(Start + Length);
}

public sealed class PagedExtentMap(PagedMetadataStore store)
{
    public MetadataTreeRoot Empty => store.Empty;
    public MetadataTreeRoot Build(IEnumerable<PagedExtent> extents, CancellationToken ct = default)
    {
        IEnumerable<MetadataEntry> Entries()
        {
            long end = 0;
            foreach (var extent in extents)
            {
                ct.ThrowIfCancellationRequested(); Validate(extent);
                if (extent.Start < end) throw Invalid("Extent input must be ordered and non-overlapping.");
                end = extent.End; yield return Encode(extent);
            }
        }
        return store.Build(Entries(), ct);
    }
    public IEnumerable<PagedExtent> Enumerate(MetadataTreeRoot root)
    {
        long end = 0;
        foreach (var entry in store.Open(root).Enumerate())
        {
            var extent = Decode(entry);
            if (extent.Start < end) throw Invalid("Immutable extent intervals overlap.");
            end = extent.End; yield return extent;
        }
    }
    public IEnumerable<PagedExtent> Overlaps(MetadataTreeRoot root, long start, long length)
    {
        if (start < 0 || length < 0 || start > long.MaxValue - length) throw new ArgumentOutOfRangeException(nameof(start));
        if (length == 0) yield break;
        long end = start + length; var tree = store.Open(root); var floor = tree.Floor(Key(start));
        if (floor is not null)
        {
            var extent = Decode(floor);
            if (extent.End > start) yield return extent;
        }
        foreach (var entry in tree.Enumerate(floor?.Key ?? Key(start)))
        {
            var extent = Decode(entry);
            if (extent.Start >= end) yield break;
            if (extent.End > start) yield return extent;
        }
    }
    public MetadataTreeRoot Replace(MetadataTreeRoot root, PagedExtent incoming, CancellationToken ct = default)
    {
        Validate(incoming); var next = root;
        foreach (var prior in Overlaps(root, incoming.Start, incoming.Length))
        {
            ct.ThrowIfCancellationRequested(); next = store.Remove(next, Key(prior.Start));
            if (prior.Start < incoming.Start)
            {
                var prefix = prior with { Length = checked((int)(incoming.Start - prior.Start)) };
                var encoded = Encode(prefix); next = store.Set(next, encoded.Key, encoded.Value.Span);
            }
            if (prior.End > incoming.End)
            {
                int skip = checked((int)(incoming.End - prior.Start));
                var suffix = prior with { Start = incoming.End, Length = prior.Length - skip, ObjectOffset = checked(prior.ObjectOffset + skip) };
                var encoded = Encode(suffix); next = store.Set(next, encoded.Key, encoded.Value.Span);
            }
        }
        var replacement = Encode(incoming); return store.Set(next, replacement.Key, replacement.Value.Span);
    }
    public MetadataTreeRoot Truncate(MetadataTreeRoot root, long length, CancellationToken ct = default)
    {
        if (length < 0) throw new ArgumentOutOfRangeException(nameof(length));
        var next = root; var tree = store.Open(root); var floor = tree.Floor(Key(length));
        if (floor is not null)
        {
            var prior = Decode(floor);
            if (prior.End > length)
            {
                next = store.Remove(next, floor.Key);
                if (prior.Start < length)
                {
                    var prefix = Encode(prior with { Length = checked((int)(length - prior.Start)) });
                    next = store.Set(next, prefix.Key, prefix.Value.Span);
                }
            }
        }
        foreach (var entry in tree.Enumerate(floor?.Key ?? Key(length)))
        { ct.ThrowIfCancellationRequested(); next = store.Remove(next, entry.Key); }
        return next;
    }
    private static string Key(long start) => start.ToString("x16", CultureInfo.InvariantCulture);
    private static MetadataEntry Encode(PagedExtent extent)
    {
        Validate(extent); byte[] bytes = new byte[45]; bytes[0] = 1;
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(1), extent.Length);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(5), extent.ObjectLength);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(9), extent.ObjectOffset);
        Convert.FromHexString(extent.ContentHash).CopyTo(bytes, 13); return new(Key(extent.Start), bytes);
    }
    private static PagedExtent Decode(MetadataEntry entry)
    {
        var bytes = entry.Value.Span;
        if (bytes.Length != 45 || bytes[0] != 1 || entry.Child is not null || entry.Key.Length != 16 ||
            !long.TryParse(entry.Key, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long start) || start < 0 || entry.Key != Key(start))
            throw Invalid("An extent record has an invalid format or offset.");
        var extent = new PagedExtent(start, BinaryPrimitives.ReadInt32LittleEndian(bytes[1..]), Convert.ToHexStringLower(bytes[13..]),
            BinaryPrimitives.ReadInt32LittleEndian(bytes[5..]), BinaryPrimitives.ReadInt32LittleEndian(bytes[9..]));
        Validate(extent); return extent;
    }
    private static void Validate(PagedExtent extent)
    {
        if (extent.Start < 0 || extent.Length <= 0 || extent.Start > long.MaxValue - extent.Length || extent.ObjectLength <= 0 ||
            extent.ObjectOffset < 0 || extent.ObjectOffset > extent.ObjectLength - extent.Length || extent.ContentHash is not { Length: 64 } ||
            extent.ContentHash.Any(c => c is not (>= '0' and <= '9' or >= 'a' and <= 'f')))
            throw Invalid("An extent requires valid 64-bit bounds and an immutable object slice.");
    }
    private static WorkspaceException Invalid(string message) => new("PagedExtentInvalid", message);
}
