using System.Buffers.Binary;
using System.Globalization;
using Splinter.Core;

namespace Splinter.Runtime;

public sealed partial class PagedMetadataStore
{
    // The caller freezes/pins this root set before leaving its short write
    // boundary. Reachability, duplicate removal and relocation then use disk
    // cursors. Neither the reachable-page set nor relocation map lives in RAM.
    // Returned roots preserve logical identity while using destination offsets.
    public IEnumerable<MetadataTreeRoot> CopyRootsTo(PagedMetadataStore destination, IEnumerable<MetadataTreeRoot> roots,
        string workingDirectory, CancellationToken ct = default)
        => CopyRootsToCore(destination, roots, workingDirectory, ct, false, null);

    private IEnumerable<MetadataTreeRoot> CopyRootsToCore(PagedMetadataStore destination, IEnumerable<MetadataTreeRoot> roots,
        string workingDirectory, CancellationToken ct, bool sameIdentity, Action<MetadataPageReference, MetadataPageReference>? copied)
    {
        if (ReferenceEquals(this, destination) || StoreId == destination.StoreId && !sameIdentity) throw Invalid("Repacking requires a separately owned metadata store.");
        string scope = Path.Combine(Path.GetFullPath(workingDirectory), "repack-" + Guid.NewGuid().ToString("N"));
        WorkspaceStore.EnsurePlainPath(scope); Directory.CreateDirectory(scope); bool completed = false;
        if (filePins is not null) MetadataScratchOwnership.MarkScope(scope, "metadata-repack");
        FileStream NewSpool(string path, bool unbuffered = false) => NewGraphSpool(path, unbuffered, filePins);
        string rootFile = Path.Combine(scope, "roots.bin"), frontier = Path.Combine(scope, "frontier-0000000000000000.bin"), visited = Path.Combine(scope, "visited-0000000000000000.bin");
        string mapping = Path.Combine(scope, "mapping.bin");
        try
        {
            using (var rootOutput = NewSpool(rootFile))
            using (var first = NewSpool(frontier))
            using (NewSpool(visited))
                foreach (var root in roots)
                {
                    ct.ThrowIfCancellationRequested(); CheckRoot(root); rootOutput.WriteByte(root.Page is null ? (byte)0 : (byte)1);
                    if (root.Page is null) continue;
                    var canonical = ResolveReference(root.Page); WriteReference(rootOutput, canonical); WriteReference(first, canonical);
                }
            long phase = 0;
            while (new FileInfo(frontier).Length != 0)
            {
                ct.ThrowIfCancellationRequested(); phase = checked(phase + 1);
                string nextVisited = Path.Combine(scope, "visited-" + phase.ToString("x16") + ".bin"), nextFrontier = Path.Combine(scope, "frontier-" + phase.ToString("x16") + ".bin");
                using (var output = NewSpool(nextVisited))
                using (var children = NewSpool(nextFrontier))
                using (var prior = ReadReferences(visited).GetEnumerator())
                {
                    bool hasPrior = prior.MoveNext(); MetadataPageReference? previous = null;
                    foreach (var sorted in MetadataExternalSort.Order(ReadReferences(frontier).Select(ResolveReference).Select(r => new MetadataEntry(PhysicalKey(r), EncodeReference(r))), scope, ct, filePins))
                    {
                        var reference = DecodeReference(sorted.Value.Span); CheckReference(reference);
                        if (previous is not null && PhysicalOrder(previous, reference) == 0)
                        { if (previous != reference) throw Invalid("One metadata address has conflicting immutable identities."); continue; }
                        previous = reference;
                        while (hasPrior && PhysicalOrder(prior.Current, reference) < 0)
                        { WriteReference(output, prior.Current); hasPrior = prior.MoveNext(); }
                        if (hasPrior && PhysicalOrder(prior.Current, reference) == 0)
                        {
                            if (prior.Current != reference) throw Invalid("A retained metadata address changed its identity.");
                            WriteReference(output, prior.Current); hasPrior = prior.MoveNext(); continue;
                        }
                        var node = ReadNode(reference); WriteReference(output, reference);
                        if (node.Left is not null) WriteReference(children, node.Left);
                        if (node.Right is not null) WriteReference(children, node.Right);
                        if (node.Data is not null) WriteReference(children, node.Data);
                    }
                    while (hasPrior) { WriteReference(output, prior.Current); hasPrior = prior.MoveNext(); }
                }
                MetadataScratchOwnership.Remove(visited, filePins); MetadataScratchOwnership.Remove(frontier, filePins); visited = nextVisited; frontier = nextFrontier;
            }
            using var map = NewSpool(mapping, unbuffered: true); long rows = 0;
            MetadataPageReference? Relocate(MetadataPageReference? source)
            {
                if (source is null) return null;
                source = ResolveReference(source); long low = 0, high = rows; byte[] entry = new byte[76];
                while (low < high)
                {
                    long middle = low + (high - low) / 2; ReadMapping(map, checked(middle * 76), entry);
                    long container = BinaryPrimitives.ReadInt64LittleEndian(entry), offset = BinaryPrimitives.ReadInt64LittleEndian(entry.AsSpan(8));
                    int order = container != source.Container ? container.CompareTo(source.Container) : offset.CompareTo(source.Offset);
                    if (order < 0) low = middle + 1;
                    else if (order > 0) high = middle;
                    else
                    {
                        var target = DecodeReference(entry.AsSpan(16));
                        if (target.ContentHash != source.ContentHash || target.Count != source.Count) throw Invalid("A relocation map changed logical metadata identity.");
                        return target;
                    }
                }
                throw Invalid("An immutable metadata child is absent from its relocation map.");
            }
            foreach (var reference in ReadReferences(visited))
            {
                ct.ThrowIfCancellationRequested(); var node = ReadNode(reference);
                var relocated = node with { Left = Relocate(node.Left), Right = Relocate(node.Right), Data = Relocate(node.Data) };
                MetadataPageReference next;
                lock (destination.gate) { destination.CheckWriter(); next = destination.WriteNode(relocated); }
                if (next.Count != reference.Count || next.ContentHash != reference.ContentHash) throw Invalid("Repacked pages changed their logical identity.");
                byte[] entry = new byte[76]; BinaryPrimitives.WriteInt64LittleEndian(entry, reference.Container); BinaryPrimitives.WriteInt64LittleEndian(entry.AsSpan(8), reference.Offset);
                EncodeReference(next).CopyTo(entry, 16); RandomAccess.Write(map.SafeFileHandle, entry, checked(rows * 76)); rows = checked(rows + 1);
                copied?.Invoke(reference, next);
            }
            destination.Flush();
            using (var input = new FileStream(rootFile, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                while (true)
                {
                    ct.ThrowIfCancellationRequested(); int kind = input.ReadByte(); if (kind < 0) break;
                    if (kind == 0) { yield return destination.Empty; continue; }
                    if (kind != 1) throw Invalid("The owned repack root spool is corrupt.");
                    byte[] bytes = new byte[60]; input.ReadExactly(bytes); var relocated = Relocate(DecodeReference(bytes));
                    yield return new(1, destination.StoreId, relocated);
                }
            }
            completed = true;
        }
        finally
        {
            if (filePins is not null) _ = MetadataScratchOwnership.TryReclaimScope(scope, "metadata-repack", filePins);
            else if (completed)
            {
                foreach (string path in Directory.EnumerateFileSystemEntries(scope))
                {
                    WorkspaceStore.EnsurePlainPath(path); string name = Path.GetFileName(path);
                    if (!File.Exists(path) || !(name is "roots.bin" or "mapping.bin" || name.StartsWith("frontier-", StringComparison.Ordinal) || name.StartsWith("visited-", StringComparison.Ordinal)))
                        throw Invalid("Preserve unexpected files in the owned repack workspace.");
                    File.Delete(path);
                }
                Directory.Delete(scope);
            }
        }
    }
    private static FileStream NewGraphSpool(string path, bool unbuffered, IJournalFilePins? filePins)
    { WorkspaceStore.EnsurePlainPath(path); return MetadataScratchOwnership.Create(path, FileAccess.ReadWrite, FileShare.Read, unbuffered ? 1 : 65536, unbuffered ? FileOptions.RandomAccess : FileOptions.SequentialScan, filePins); }
    private static string PhysicalKey(MetadataPageReference r) => r.Container.ToString("x16", CultureInfo.InvariantCulture) + r.Offset.ToString("x16", CultureInfo.InvariantCulture);
    private static int PhysicalOrder(MetadataPageReference a, MetadataPageReference b) => a.Container != b.Container ? a.Container.CompareTo(b.Container) : a.Offset.CompareTo(b.Offset);
    private static byte[] EncodeReference(MetadataPageReference r)
    {
        byte[] bytes = new byte[60]; BinaryPrimitives.WriteInt64LittleEndian(bytes, r.Container); BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(8), r.Offset);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(16), r.Bytes); BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(20), r.Count);
        Convert.FromHexString(r.ContentHash).CopyTo(bytes, 28); return bytes;
    }
    private static MetadataPageReference DecodeReference(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != 60) throw Invalid("An owned metadata reference spool is truncated.");
        return new(BinaryPrimitives.ReadInt64LittleEndian(bytes), BinaryPrimitives.ReadInt64LittleEndian(bytes[8..]),
            BinaryPrimitives.ReadInt32LittleEndian(bytes[16..]), BinaryPrimitives.ReadInt64LittleEndian(bytes[20..]), Convert.ToHexStringLower(bytes[28..]));
    }
    private static void WriteReference(Stream output, MetadataPageReference reference) => output.Write(EncodeReference(reference));
    private static IEnumerable<MetadataPageReference> ReadReferences(string path)
    {
        using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.SequentialScan);
        if (input.Length % 60 != 0) throw Invalid("An owned reachability spool is truncated.");
        byte[] bytes = new byte[60]; while (input.Position < input.Length) { input.ReadExactly(bytes); yield return DecodeReference(bytes); }
    }
    private static void ReadMapping(FileStream input, long offset, Span<byte> bytes)
    {
        while (!bytes.IsEmpty) { int n = RandomAccess.Read(input.SafeFileHandle, bytes, offset); if (n <= 0) throw Invalid("An owned relocation map ended early."); offset += n; bytes = bytes[n..]; }
    }
}
