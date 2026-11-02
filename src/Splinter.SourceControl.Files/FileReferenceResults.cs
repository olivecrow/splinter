using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Splinter.Core;
using Splinter.Runtime;

namespace Splinter.SourceControl.Files;

public sealed partial class FileWorkspaceProvider
{
    // A result retains changed before/after payloads and earlier retained payloads,
    // not every unchanged file in the comparison catalog. The full catalog still
    // owns path/metadata comparisons. Retention is bound by the generation receipt.
    private void BindRetainedPayloads(WorkspaceRecord w, FileSourceManifest baseline, FileSourceManifest result, ISourceCaptureRanges ranges, CancellationToken ct)
    {
        using var previous = w.Results.LastOrDefault() is { } last
            ? ReadManifest(w, GenerationPath(Control(w), last.Id, "result.json")) : null;
        IEnumerable<MetadataEntry> Required()
        {
            foreach (var (left, right) in ChangedPayloadEntries(baseline, result, ct))
            {
                ct.ThrowIfCancellationRequested();
                if (left == right) continue;
                if (left is { Kind: "file" }) yield return EncodeRetained(left);
                if (right is { Kind: "file" })
                {
                    yield return EncodeRetained(right);
                    var layout = ranges.GetFileRanges(right.Path) ?? throw Error("A changed file has no frozen layout.");
                    if (layout.BasisPath is { } path && baseline.Find(path) is { Kind: "file" } dependency)
                        yield return EncodeRetained(dependency);
                }
            }
        }
        using var pool = new SharedMetadataPool(store.Root, "capture-retention", ct, contentPins);
        using var pages = new PagedMetadataStore(pool.Directory, true, filePins: contentPins);
        // This is an append-only logical dependency set. Path-copy only new
        // before/after dependencies; never sort/rebuild all historical hashes.
        // The complete resulting set is still drained by independent verification.
        var retained = previous?.RetainedPayloads ?? pages.Empty;
        foreach (var entry in Required())
        {
            ct.ThrowIfCancellationRequested(); var prior = pages.Open(retained).Find(entry.Key);
            if (prior is not null)
            { if (!prior.AsSpan().SequenceEqual(entry.Value.Span)) throw Error("One retained payload has different lengths."); continue; }
            retained = pages.Set(retained, entry.Key, entry.Value.Span);
        }
        result.RetainedPayloads = retained; pages.Flush();
        result.RetentionLease = pool.Anchor(result.Catalog!, result.RetainedPayloads, result.PayloadPaths!);
    }
    // Compare directory roots directly: identical subtrees need no temporary
    // project-wide comparison index, and unchanged leaves need no payload work.
    private static IEnumerable<(FileSourceEntry? Before, FileSourceEntry? After)> ChangedPayloadEntries(
        FileSourceManifest baseline, FileSourceManifest result, CancellationToken ct)
    {
        using var leftPages = new PagedMetadataStore(baseline.Directory!);
        using var rightPages = new PagedMetadataStore(result.Directory!);
        bool sensitive = baseline.Catalog!.CaseSensitive;
        if (sensitive != result.Catalog!.CaseSensitive) throw Error("Capture path policies differ.");
        var leftTree = new DirectoryMetadataTree(leftPages, sensitive); var rightTree = new DirectoryMetadataTree(rightPages, sensitive);
        string Key(string path) => sensitive ? path : path.ToUpperInvariant();
        string JoinPath(string parent, string name) => parent.Length == 0 ? name : parent + "/" + name;
        FileSourceEntry? FileEntry(DirectoryMetadataRecord? record, string prefix) => record is null || record.Entry.Directory ? null :
            new(JoinPath(prefix, record.Entry.Path), "file", record.Entry.Length, record.ContentHash,
                DateTime.FromFileTimeUtc(record.Entry.LastWriteTime).Ticks, (int)record.Entry.Attributes, record.Entry.UnixMode);
        var pending = new Stack<(MetadataTreeRoot? Left, MetadataTreeRoot? Right, string LeftPath, string RightPath)>();
        pending.Push((baseline.Catalog.Children, result.Catalog.Children, "", ""));
        while (pending.TryPop(out var pair))
        {
            ct.ThrowIfCancellationRequested();
            if (pair.Left?.ContentHash == pair.Right?.ContentHash && pair.LeftPath == pair.RightPath) continue;
            using var left = (pair.Left is null ? [] : leftTree.Enumerate(pair.Left)).GetEnumerator();
            using var right = (pair.Right is null ? [] : rightTree.Enumerate(pair.Right)).GetEnumerator();
            bool a = left.MoveNext(), b = right.MoveNext();
            while (a || b)
            {
                ct.ThrowIfCancellationRequested();
                int order = !a ? 1 : !b ? -1 : StringComparer.Ordinal.Compare(Key(left.Current.Entry.Path), Key(right.Current.Entry.Path));
                var l = order <= 0 ? left.Current : null; var r = order >= 0 ? right.Current : null;
                var before = FileEntry(l, pair.LeftPath); var after = FileEntry(r, pair.RightPath);
                if (before != after) yield return (before, after);
                if (l?.Entry.Directory == true || r?.Entry.Directory == true)
                    pending.Push((l?.Children, r?.Children, l is null ? pair.LeftPath : JoinPath(pair.LeftPath, l.Entry.Path),
                        r is null ? pair.RightPath : JoinPath(pair.RightPath, r.Entry.Path)));
                if (order <= 0) a = left.MoveNext(); if (order >= 0) b = right.MoveNext();
            }
        }
    }
    private static MetadataEntry EncodeRetained(FileSourceEntry entry)
    {
        if (!ValidHash(entry.Hash) || entry.Length < 0) throw Error("Invalid retained payload identity.");
        byte[] size = new byte[8]; BinaryPrimitives.WriteInt64LittleEndian(size, entry.Length); return new(entry.Hash!, size);
    }
    private static string GenerationFingerprint(FileSourceManifest manifest) => manifest.RetainedPayloads is null ? manifest.Identity
        : "sha256:" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes("reference-result-v4\n" + manifest.Identity + "\n" +
            manifest.RetainedPayloads.ContentHash + "\n" + manifest.ContentFiles?.ContentHash)));

    private static IEnumerable<FileSourceEntry> RequiredBasisPayloads(FileSourceManifest baseline, FileSourceManifest result, CancellationToken ct)
    {
        using var pages = new PagedMetadataStore(baseline.Directory!); var payloads = pages.Open(baseline.PayloadIndex!);
        using var reader = baseline.OpenReader();
        foreach (var entry in result.EnumerateRetainedPayloads(ct))
            if (payloads.Find(entry.Hash!) is not null) yield return reader.FindPayload(entry.Hash!, entry.Length);
    }

    // A current logical payload union is insufficient by itself: an older
    // full-file hash can use different physical whole/raw dependencies. Prove
    // both retained membership and descriptor equivalence before omitting its
    // contribution to logical storage accounting. Never used by GC/pruning.
    internal static bool ReferencesCoveredBy(FileSourceManifest prior, FileSourceManifest current, string control, CancellationToken ct,
        PagedMetadataStore? retainedPages = null, PagedMetadataStore? descriptorPages = null)
    {
        if (prior.RetainedPayloads is null || current.RetainedPayloads is null ||
            !WorkspaceStore.SamePath(prior.Directory!, current.Directory!)) return false;
        using var ownedMetadata = retainedPages is null ? new PagedMetadataStore(current.Directory!) : null;
        var metadata = retainedPages ?? ownedMetadata!;
        if (metadata.EntriesNotIdentical(prior.RetainedPayloads, current.RetainedPayloads, ct).Any()) return false;
        if (prior.ContentFiles is null && current.ContentFiles is null) return true;
        using var ownedContents = descriptorPages is null ? new PagedMetadataStore(FilePath(control, "result-ranges")) : null;
        var contents = descriptorPages ?? ownedContents!;
        var before = prior.ContentFiles ?? contents.Empty; var after = current.ContentFiles ?? contents.Empty;
        var required = metadata.Open(prior.RetainedPayloads);
        foreach (var entry in contents.EntriesNotIdentical(before, after, ct))
            if (required.Find(entry.Key) is not null) return false;
        // Also reject a formerly whole object now represented by a new range
        // descriptor; equal full-file hashes do not imply equal physical sets.
        foreach (var entry in contents.EntriesNotIdentical(after, before, ct))
            if (required.Find(entry.Key) is not null) return false;
        return true;
    }
}
