using System.Buffers.Binary;

namespace Splinter.Runtime;

// Owned by one journal and protected by its gate. This is RAM only: callers
// still read the current encoded bytes through the owned journal on every use.
internal sealed class JournalDecodedPayloadCache
{
    internal const long DefaultBudgetBytes = 64L * 1024 * 1024;
    // Payload accounting includes the in-flight miss before allocating it.
    // Bound metadata separately as well as charging a conservative entry cost;
    // this is a live cache budget, not a GC/process working-set measurement.
    internal const int EntryChargeBytes = 512, MaximumEntries = 4096;
    private readonly long budgetBytes;
    private readonly Dictionary<Key, LinkedListNode<Entry>> entries = [];
    private readonly LinkedList<Entry> lru = [];
    private long accountedBytes, payloadBytes;

    private readonly record struct Key(ulong A, ulong B, ulong C, ulong D, int EncodedBytes, int DecodedBytes)
    {
        internal static Key From(ReadOnlySpan<byte> encoded, int decoded)
        {
            // Validate recomputes this checksum from the current header and
            // compressed bytes first. A physical address alone is never a key.
            var digest = encoded[^32..];
            return new(BinaryPrimitives.ReadUInt64LittleEndian(digest), BinaryPrimitives.ReadUInt64LittleEndian(digest[8..]),
                BinaryPrimitives.ReadUInt64LittleEndian(digest[16..]), BinaryPrimitives.ReadUInt64LittleEndian(digest[24..]),
                encoded.Length, decoded);
        }
    }
    private sealed record Entry(Key Key, byte[] Bytes);

    internal JournalDecodedPayloadCache(long budgetBytes = DefaultBudgetBytes)
    {
        if (budgetBytes < 0) throw new ArgumentOutOfRangeException(nameof(budgetBytes));
        this.budgetBytes = budgetBytes;
    }

    internal long BudgetBytes => budgetBytes;
    internal long PayloadBytes => payloadBytes;
    internal long AccountedBytes => accountedBytes;
    internal int EntryCount => entries.Count;
    internal long PeakAccountedBytes { get; private set; }
    internal long Hits { get; private set; }
    internal long Misses { get; private set; }
    internal long Decodes { get; private set; }
    internal long Evictions { get; private set; }

    internal int Read(BrotliWorkspacePayloadCodec codec, ReadOnlySpan<byte> encoded, int offset, Span<byte> output)
    {
        if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset));
        try
        {
            // The disabled comparison uses the original codec path exactly,
            // including its single checksum pass and bounded scratch buffer.
            if (budgetBytes == 0)
            {
                Misses++;
                if (encoded.Length > 12 && encoded[12] == 1) Decodes++;
                return codec.Read(encoded, offset, output);
            }
            // Retain current corruption/format detection even on a cache hit.
            var block = codec.Validate(encoded);
            var (decoded, _, compressed) = block;
            int count = offset >= decoded ? 0 : Math.Min(output.Length, decoded - offset);
            var key = Key.From(encoded, decoded);
            if (entries.TryGetValue(key, out var cached))
            {
                Hits++; lru.Remove(cached); lru.AddFirst(cached);
                if (count != 0) cached.Value.Bytes.AsSpan(offset, count).CopyTo(output);
                return count;
            }
            Misses++;
            long charge = decoded + EntryChargeBytes;
            if (!compressed || charge > budgetBytes)
            {
                if (compressed) Decodes++;
                return codec.ReadValidated(encoded, offset, output, block);
            }
            while (accountedBytes > budgetBytes - charge || entries.Count >= MaximumEntries)
                EvictLast();
            accountedBytes += charge;
            PeakAccountedBytes = Math.Max(PeakAccountedBytes, accountedBytes);
            bool published = false;
            try
            {
                byte[] bytes = new byte[decoded];
                Decodes++; codec.DecodeForCache(encoded, bytes, block);
                var node = new LinkedListNode<Entry>(new(key, bytes));
                entries.Add(key, node); lru.AddFirst(node);
                payloadBytes += decoded; published = true;
                if (count != 0) bytes.AsSpan(offset, count).CopyTo(output);
                return count;
            }
            finally { if (!published) accountedBytes -= charge; }
        }
        catch { Clear(); throw; }
    }

    private void EvictLast()
    {
        var last = lru.Last!;
        entries.Remove(last.Value.Key); lru.RemoveLast();
        payloadBytes -= last.Value.Bytes.Length;
        accountedBytes -= last.Value.Bytes.Length + EntryChargeBytes;
        Evictions++;
    }

    internal void Clear()
    {
        entries.Clear(); lru.Clear(); payloadBytes = accountedBytes = 0;
    }
}
