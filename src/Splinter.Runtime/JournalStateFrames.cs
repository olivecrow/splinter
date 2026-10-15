using System.Security.Cryptography;
using System.Text.Json;
using Splinter.Core;

namespace Splinter.Runtime;

public sealed partial class JournalDeltaStore
{
    private bool hasStatePrefix;
    // A sealed current-state prefix preserves identities and path sequences
    // when exporting to a standalone SJR2 candidate or explicitly selecting OFF.
    // It is initialization, never an acknowledged mutation history.
    private long WriteStateFrames(List<LiveRange> live, Stream? output, IReadOnlySet<string>? projectedFiles, BoundedLogSettings? bounded = null)
    {
        long bytes = 0;
        void Frame(Mutation mutation, ReadOnlySpan<byte> data)
        {
            byte[] metadata = JsonSerializer.SerializeToUtf8Bytes(mutation);
            if (metadata.Length > MaxMetadataBytes) throw new WorkspaceException("OverlayMetadataLimit", "Current-state metadata exceeds its frame bound.");
            byte[]? encoded = EncodePayload(data);
            ReadOnlySpan<byte> stored = encoded is null ? data : encoded;
            var descriptor = new WorkspaceJournalPayload(stored.Length, data.Length);
            long length = FrameHeaderBytes + (long)metadata.Length + stored.Length + FrameChecksumBytes;
            bytes = checked(bytes + length);
            if (output is null) return;
            Span<byte> header = stackalloc byte[FrameHeaderBytes]; WriteHeader(header, metadata.Length, descriptor);
            using var digest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            digest.AppendData(header); digest.AppendData(metadata); digest.AppendData(stored);
            output.Write(header); output.Write(metadata); output.Write(stored); output.Write(digest.GetHashAndReset());
        }
        var root = nodes[paths[""]].Entry;
        if (bounded is not null) Frame(new(0, "state-bounded", root, Bounded: bounded), []);
        var ranges = live.GroupBy(r => r.NodeId).ToDictionary(g => g.Key, g => g.OrderBy(r => r.Logical).ToArray());
        foreach (var node in (bounded is null ? nodes.Values : BoundedNodes()).OrderBy(n => n.Entry.Path, pathPolicy.Comparer).ThenBy(n => n.Entry.Id))
        {
            if (projectedFiles?.Contains(node.Entry.Path) == true) continue;
            Frame(new(0, "state-node", node.Entry), []);
            foreach (var range in ranges.GetValueOrDefault(node.Entry.Id) ?? [])
            {
                byte[] data = new byte[range.Length];
                ReadPayload(range.Physical, range.Payload, range.BlockOffset, data);
                Frame(new(0, "state-write", node.Entry, range.Logical), data);
            }
        }
        foreach (string path in hidden.Order(pathPolicy.Comparer))
            if (projectedFiles?.Contains(path) != true) Frame(new(0, "state-hidden", root with { Path = path }), []);
        foreach (var change in pathChanges.OrderBy(p => p.Key, pathPolicy.Comparer))
            Frame(new(change.Value, "state-change", root with { Path = change.Key }), []);
        foreach (string path in projectionUnsafe.Order(pathPolicy.Comparer))
            Frame(new(0, "state-projection-unsafe", root with { Path = path }), []);
        Frame(new(sequence, "state-complete", root), []);
        return bytes;
    }
    private void ApplyState(Mutation mutation, long physical, WorkspaceJournalPayload payload)
    {
        RequireAllowed(mutation.Entry.Path);
        if (mutation.Entry.BasePath is { } basis) RequireAllowed(basis);
        switch (mutation.Operation)
        {
            case "state-bounded":
                RequireBoundedSettings(mutation.Bounded!, Identity);
                if (boundedSettings is not null || mutation.Entry.Id != nodes[paths[""]].Entry.Id ||
                    mutation.Entry.Path.Length != 0 || !mutation.Entry.Directory || mutation.Entry.Deleted)
                    throw LatestJournalStorage.Corrupt("The bounded initial mode or root identity differs.");
                boundedSettings = mutation.Bounded;
                break;
            case "state-node":
                if (nodes.TryGetValue(mutation.Entry.Id, out var prior))
                {
                    if (mutation.Entry.Path.Length != 0 || prior.Entry.Path.Length != 0 || prior.Extents.Count != 0)
                        throw LatestJournalStorage.Corrupt("A current-state node is duplicated.");
                    RemovePath(prior.Entry.Path);
                }
                else RequireNodeCapacity();
                var node = new Node(mutation.Entry); nodes[mutation.Entry.Id] = node;
                if (boundedSettings is not null) boundedDurableNodes.Add(mutation.Entry.Id);
                if (!node.Entry.Deleted)
                {
                    if (paths.ContainsKey(node.Entry.Path)) throw LatestJournalStorage.Corrupt("A current-state path is duplicated.");
                    IndexPath(node.Entry.Path, node.Entry.Id);
                }
                break;
            case "state-write":
                if (!nodes.TryGetValue(mutation.Entry.Id, out var written) || written.Entry != mutation.Entry ||
                    written.Extents.Count != 0 && written.Extents[^1].Start + written.Extents[^1].Length > mutation.Offset)
                    throw LatestJournalStorage.Corrupt("Current-state ranges lack their matching node or overlap.");
                if (++extentCount > MaxIndexedExtents) throw LatestJournalStorage.Corrupt("The current-state range index is full.");
                written.Extents.Add(new(mutation.Offset, payload.DecodedBytes, physical, payload));
                payloadBytes += payload.StoredBytes; logicalPayloadBytes += payload.DecodedBytes;
                break;
            case "state-hidden":
                if (!hidden.Add(mutation.Entry.Path) || hidden.Count > MaxHiddenPaths)
                    throw LatestJournalStorage.Corrupt("The current-state whiteout index is invalid.");
                break;
            case "state-change":
                if (mutation.Sequence < 1 || !pathChanges.TryAdd(mutation.Entry.Path, mutation.Sequence) || pathChanges.Count > MaxHiddenPaths)
                    throw LatestJournalStorage.Corrupt("The current-state sequence index is invalid.");
                break;
            case "state-complete":
                if (mutation.Sequence < 0 || pathChanges.Any(p => p.Value > mutation.Sequence) || mutation.Entry != nodes[paths[""]].Entry)
                    throw LatestJournalStorage.Corrupt("The current-state footer sequence is invalid.");
                sequence = mutation.Sequence;
                break;
            case "state-projection-unsafe":
                if (Identity.ExcludedTopLevel?.Contains(mutation.Entry.Path, pathPolicy.Comparer) != true || !projectionUnsafe.Add(mutation.Entry.Path))
                    throw LatestJournalStorage.Corrupt("A current-state projection guard is invalid.");
                break;
            default: throw LatestJournalStorage.Corrupt("Unknown current-state initialization operation.");
        }
    }
}
