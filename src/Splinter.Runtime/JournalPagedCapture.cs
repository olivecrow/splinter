using System.Buffers.Binary;
using System.Text;
using Splinter.Core;

namespace Splinter.Runtime;

public sealed partial class JournalDeltaStore
{
    private JournalSourceCapture CapturePagedSource(string destination, string id, string[] managedPaths)
    {
        // The append/bounded writer has a single exact backing. A hard-link pin
        // protects replacement/truncation independently of future journal work.
        pagedIndex!.Flush();
        var backing = new CapturedBacking(journal.Name, "00000000.pin", sourceFilePins!.Identify(journal.Name), journal.Length);
        var roots = new PagedJournalCapture(pagedIndexDirectory!, NodeRoot!, PathRoot!, HiddenRoot!, ChangeRoot!, pathComponents!.Root);
        var capture = new JournalSourceCapture(2, id, Identity, sequence, managedPaths.ToArray(), [], [], [backing], [], "", roots);
        capture = capture with { Digest = SourceCaptureTree.Digest(capture) };
        pagedIndexCaptured = true;
        Directory.CreateDirectory(destination); SourceCaptureTree.Publish(Path.Combine(destination, "capture.json"), capture);
        return capture;
    }

    internal static void ValidatePagedCapture(string directory, JournalSourceCapture capture, bool verifyRoots = true)
    {
        var roots = capture.Paged;
        string delta = Path.GetFullPath(Path.Combine(directory, "..", "..", "delta"));
        if (roots is null || capture.Identity.Version != 3 || string.IsNullOrWhiteSpace(capture.Id) || capture.Sequence < 0 ||
            capture.Nodes.Length != 0 || capture.Hidden.Length != 0 || capture.ChangedPaths.Length != 0 ||
            (roots.Segments is null ? capture.Backings.Length != 1 : capture.Backings.Length != 0) ||
            capture.Digest != SourceCaptureTree.Digest(capture) ||
            Path.GetDirectoryName(Path.GetFullPath(roots.Directory)) != delta ||
            !IndexName(Path.GetFileName(roots.Directory)) ||
            roots.Segments is not null && (capture.ReaderName is not { Length: 45 } reader || !reader.StartsWith("readers-", StringComparison.Ordinal) ||
                !reader.EndsWith(".lock", StringComparison.Ordinal) || !Guid.TryParseExact(reader.AsSpan(8, 32), "N", out _) || string.IsNullOrEmpty(capture.ReaderIdentity)) ||
            roots.Segments is null && (capture.Backings[0] is null || capture.Backings[0].Name != "00000000.pin" || capture.Backings[0].MinimumLength < 0 ||
            Path.GetDirectoryName(Path.GetFullPath(capture.Backings[0].Source)) != delta) ||
            capture.ManagedPaths.Any(string.IsNullOrWhiteSpace))
            throw PagedInvalid();
        if (!verifyRoots) return;
        using var index = new PagedMetadataStore(roots.Directory);
        foreach (var root in new[] { roots.Nodes, roots.Paths, roots.Hidden, roots.Changes, roots.Names }) index.Open(root).Verify();
        if (roots.Segments is { } segments) index.Open(segments).Verify();
    }

    internal static ISourceCaptureTree OpenPagedCapture(string directory, ISnapshotTree basis, IJournalFilePins pins, JournalSourceCapture capture, ICapturedContentReader? capturedContentReader = null) =>
        new PagedCaptureTree(directory, basis, pins, capture, capturedContentReader);

    private sealed class PagedCaptureTree : ISourceCaptureTree, ISourceCaptureRanges
    {
        private readonly JournalSourceCapture capture;
        private readonly PagedJournalCapture roots;
        private readonly PagedMetadataStore index;
        private readonly FileStream? payload;
        private readonly string directory;
        private readonly IJournalFilePins pins;
        private readonly ICapturedContentReader? capturedContentReader;
        private readonly WorkspacePathPolicy policy;
        private readonly PathComponents components;
        private readonly PagedPathNamespace namesIndex;
        private readonly string scratch;
        private readonly CaptureBackingReaders backingReaders;
        public ISnapshotTree Basis { get; }
        public SnapshotReference Snapshot => Basis.Snapshot;
        public SnapshotGraft? Graft => Basis.Graft;
        public SourceCaptureIdentity CaptureIdentity => new(capture.Id, capture.Sequence, capture.Digest);
        public bool SupportsConcurrentReads => Basis.SupportsConcurrentReads;

        internal PagedCaptureTree(string directory, ISnapshotTree basis, IJournalFilePins pins, JournalSourceCapture capture, ICapturedContentReader? capturedContentReader)
        {
            this.capture = capture; this.directory = directory; this.pins = pins;
            this.capturedContentReader = capturedContentReader;
            roots = capture.Paged!; Basis = basis; policy = WorkspacePathPolicy.For(basis.Snapshot);
            index = new(roots.Directory); scratch = Path.Combine(Path.GetTempPath(), "splinter-metadata-sort");
            backingReaders = new(directory, pins);
            components = new(index) { Root = roots.Names }; namesIndex = new(index, policy, _ => throw new NotSupportedException()) { Root = roots.Paths };
            try
            {
                if (roots.Segments is null)
                {
                    payload = new(Path.Combine(directory, capture.Backings[0].Name), FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 1, FileOptions.RandomAccess);
                    pins.Verify(payload, capture.Backings[0].Identity);
                    if (payload.Length < capture.Backings[0].MinimumLength) throw PagedInvalid();
                }
            }
            catch { index.Dispose(); throw; }
        }
        public void EnsureAvailable() => Basis.EnsureAvailable();
        public void VerifyReadBoundary() => Basis.VerifyReadBoundary();
        private string Key(string path) => PathComponents.Key(path, policy);
        private (DeltaEntry Entry, MetadataTreeRoot Extents)? Node(string path)
        {
            if (!namesIndex.TryGetValue(path, out Guid id)) return null;
            var node = index.Open(roots.Nodes).FindEntry(id.ToString("N")) ?? throw PagedInvalid();
            var entry = DecodeEntry(node.Value.ToArray(), components, roots.Names);
            if (entry.Deleted || !policy.Comparer.Equals(entry.Path, path)) throw PagedInvalid();
            return (entry, node.Child ?? index.Empty);
        }
        private bool Excluded(string path) => SourceCaptureTree.Managed(path, capture.ManagedPaths) ||
            capture.Identity.ExcludedTopLevel?.Contains(path.Split('/')[0], policy.Comparer) == true ||
            capture.Identity.DeniedTopLevel?.Contains(path.Split('/')[0], policy.Comparer) == true ||
            capture.Identity.IncludedTopLevel is { } included && path.Length != 0 && !included.Contains(path.Split('/')[0], policy.Comparer);
        private (SnapshotEntry? Entry, string? Base) Resolve(string path)
        {
            path = policy.Normalize(path); if (Excluded(path)) return (null, null);
            var node = Node(path);
            if (node is { } found) return (Convert(found.Entry), found.Entry.BasePath);
            if (index.Open(roots.Hidden).Find(Key(path)) is not null) return (null, null);
            if (path.Length == 0) return (Basis.Stat(""), "");
            var parent = Resolve(Parent(path));
            if (parent.Entry is not { Directory: true } || parent.Base is null) return (null, null);
            string original = Join(parent.Base, Name(path)); var entry = Basis.Stat(original);
            return (entry is null ? null : entry with { Path = path }, original);
        }
        public SnapshotEntry? Stat(string path) { EnsureAvailable(); return Resolve(path).Entry; }
        public IReadOnlyList<SnapshotEntry> List(string path) => EnumerateChildren(path).ToArray();
        public IEnumerable<SnapshotEntry> EnumerateChildren(string path)
        {
            path = policy.Normalize(path); EnsureAvailable(); var resolved = Resolve(path);
            if (resolved.Entry is not { Directory: true }) throw new DirectoryNotFoundException(path);
            IEnumerable<MetadataEntry> Names()
            {
                if (resolved.Base is { } original)
                    foreach (var child in Basis.EnumerateChildren(original))
                    {
                        string name = Name(child.Path);
                        if (child.Path != Join(original, name)) throw PagedInvalid();
                        yield return new(name, EncodeListedMetadata(child));
                    }
                foreach (var child in namesIndex.Children(path)) yield return new(Name(child.Key), EncodeListedMetadata(null));
            }
            string? previous = null;
            foreach (var item in MetadataExternalSort.Order(Names(), scratch, default, keyComparer: policy.Comparer))
            {
                string key = policy.IdentityPath(item.Key); if (key == previous) continue; previous = key;
                string child = Join(path, item.Key); if (Excluded(child)) continue;
                if (Node(child) is { } node) { yield return Convert(node.Entry); continue; }
                if (index.Open(roots.Hidden).Find(Key(child)) is not null || resolved.Base is null) continue;
                var entry = DecodeListedMetadata(child, item.Value.Span) ?? throw PagedInvalid();
                Basis.EnsureAvailable(); yield return entry;
            }
        }
        public IEnumerable<string>? EnumerateChangedPaths()
        {
            foreach (var entry in index.Open(roots.Changes).Enumerate())
            {
                var stored = UnpackPath(entry.Value.ToArray(), components, roots.Names);
                if (stored.Value.Length != 8 || BinaryPrimitives.ReadInt64LittleEndian(stored.Value) > capture.Sequence) throw PagedInvalid();
                if (stored.Path.Length != 0 && !SourceCaptureTree.Managed(stored.Path, capture.ManagedPaths)) yield return stored.Path;
            }
        }
        public CapturedFileRangeLayout? GetFileRanges(string relativePath)
        {
            EnsureAvailable(); string path = policy.Normalize(relativePath); var resolved = Resolve(path);
            if (resolved.Entry is not { Directory: false } entry) return null;
            var found = Node(path);
            if (found is null) return new(resolved.Base, entry.Length, Array.Empty<CapturedPrivateSourceRange>());
            var node = found.Value.Entry; var fixedRanges = found.Value.Extents;
            IEnumerable<CapturedPrivateSourceRange> Private()
            {
                foreach (var item in index.Open(fixedRanges).Enumerate())
                {
                    var extent = DecodeExtent(item.Value.ToArray());
                    extent.Payload.RequireValid(capture.Identity.MaxTransferBytes); extent.Payload.RequireFragment(extent.BlockOffset, extent.Length);
                    if (extent.ContentHash is { } content)
                    {
                        string block = extent.OriginBlock ?? SourceCaptureTree.ContentBlockIdentity(content, extent.RawContent, extent.Payload.DecodedBytes);
                        string slice = SourceCaptureTree.RangeIdentity(block, 0, extent.BlockOffset, extent.Length, extent.Payload);
                        yield return new(extent.Start, extent.Length, slice, BlockIdentity: block, BlockOffset: ProofOffset(extent),
                            PreservedHash: content, PreservedRaw: extent.RawContent, PreservedOffset: extent.BlockOffset, PreservedLength: extent.Payload.DecodedBytes);
                        continue;
                    }
                    var backing = Backing(extent);
                    if (extent.Start < 0 || extent.Start > node.Length - extent.Length || extent.JournalOffset < 0 || extent.JournalOffset > backing.MinimumLength - extent.Payload.StoredBytes) throw PagedInvalid();
                    string identity = SourceCaptureTree.RangeIdentity(backing.Identity, extent.JournalOffset, extent.BlockOffset, extent.Length, extent.Payload);
                    CapturedImmutableRangeBacking? raw = null;
                    // Only the segmented producer can grant the sealed-file
                    // capability. The transitional bounded-prefix writer uses
                    // streamed chunk preservation until that path is connected.
                    if (backing.Hash is not null)
                        raw = new(Path.Combine(directory, backing.Name), backing.Identity, checked(extent.JournalOffset + extent.BlockOffset), backing.MinimumLength,
                            capture.Id, capture.Digest, backing.Hash);
                    yield return new(extent.Start, extent.Length, identity, raw,
                        extent.OriginBlock ?? SourceCaptureTree.BlockIdentity(backing.Identity, extent.JournalOffset, extent.Payload), ProofOffset(extent));
                }
            }
            return new(node.BasePath, Math.Min(node.BaseLength, node.Length), Private(), node.Id);
        }
        public int Read(string path, long offset, Span<byte> destination)
        {
            EnsureAvailable(); path = policy.Normalize(path); var resolved = Resolve(path);
            var entry = resolved.Entry ?? throw new FileNotFoundException(path);
            if (offset < 0 || entry.Directory) throw new ArgumentOutOfRangeException(nameof(offset));
            int length = (int)Math.Min(destination.Length, Math.Max(0, entry.Length - offset)); var output = destination[..length]; output.Clear();
            var found = Node(path);
            if (found is null) return resolved.Base is null ? length : Basis.Read(resolved.Base, offset, output);
            var node = found.Value.Entry; long cursor = offset, readEnd = checked(offset + length);
            void ReadBasis(long from, long to, Span<byte> target)
            {
                to = Math.Min(to, node.BaseLength);
                if (node.BasePath is not { } original || to <= from) return;
                int done = 0, count = checked((int)(to - from)), start = checked((int)(from - offset));
                while (done < count) { int n = Basis.Read(original, from + done, target.Slice(start + done, count - done)); if (n <= 0) throw PagedInvalid(); done += n; }
            }
            var extents = index.Open(found.Value.Extents); string key = offset.ToString("x16", System.Globalization.CultureInfo.InvariantCulture);
            var floor = extents.Floor(key);
            IEnumerable<MetadataEntry> Ranges()
            { if (floor is not null) yield return floor; foreach (var range in extents.Enumerate(floor?.Key ?? key)) yield return range; }
            foreach (var item in Ranges())
            {
                var extent = DecodeExtent(item.Value.ToArray()); if (extent.Start >= offset + length) break;
                long from = Math.Max(offset, extent.Start), to = Math.Min(offset + length, checked(extent.Start + extent.Length)); if (to <= from) continue;
                ReadBasis(cursor, from, output); cursor = to;
                extent.Payload.RequireValid(capture.Identity.MaxTransferBytes);
                int skip = checked(extent.BlockOffset + (int)(from - extent.Start)), count = checked((int)(to - from));
                extent.Payload.RequireFragment(skip, count); var target = output.Slice(checked((int)(from - offset)), count);
                if (extent.ContentHash is { } content)
                {
                    if (capturedContentReader is null) throw new WorkspaceException("ContentReaderMissing", "Compose the preserved-range content reader.");
                    int done = 0;
                    while (done < count)
                    { int n = capturedContentReader.Read(content, extent.RawContent, skip + done, target[done..]); if (n <= 0 || n > count - done) throw PagedInvalid(); done += n; }
                    continue;
                }
                var backing = Backing(extent);
                if (extent.JournalOffset < 0 || extent.JournalOffset > backing.MinimumLength - extent.Payload.StoredBytes ||
                    extent.Start < 0 || extent.Start > node.Length - extent.Length) throw PagedInvalid();
                using var segment = roots.Segments is null ? null : backingReaders.Acquire(backing);
                var input = segment?.Stream ?? payload!;
                ReadExactly(input, checked(extent.JournalOffset + skip), target);
            }
            ReadBasis(cursor, readEnd, output);
            return length;
        }
        private static void ReadExactly(FileStream input, long offset, Span<byte> output)
        { while (!output.IsEmpty) { int n = RandomAccess.Read(input.SafeFileHandle, output, offset); if (n <= 0) throw PagedInvalid(); offset += n; output = output[n..]; } }
        private static SnapshotEntry Convert(DeltaEntry e) => new(e.Path, e.Directory, e.Length, e.Attributes, e.CreationTime, e.LastAccessTime, e.LastWriteTime, e.UnixMode);
        private CapturedBacking Backing(Extent extent)
        {
            if (roots.Segments is null) return capture.Backings[0];
            if (extent.Segment == -1 || extent.ContentHash is not null) throw PagedInvalid();
            var data = index.Open(roots.Segments).Find(extent.Segment.ToString("x16")) ?? throw PagedInvalid();
            var segment = DecodeSegment(data); if (segment.Id != extent.Segment || segment.NativeIdentity is null) throw PagedInvalid();
            string name = Path.GetFileNameWithoutExtension(JournalSegmentStorage.FileName(segment.Id));
            return new(Path.Combine(Path.GetDirectoryName(roots.Directory)!, name + ".sjr"), name + ".pin", segment.NativeIdentity, segment.Length, segment.Hash);
        }
        public void Dispose() { backingReaders.Dispose(); payload?.Dispose(); index.Dispose(); }
    }
    private static (string Path, byte[] Value) UnpackPath(byte[] bytes, PathComponents components, MetadataTreeRoot fixedNames)
    {
        if (bytes.Length < 4) throw PagedInvalid(); int length = BinaryPrimitives.ReadInt32LittleEndian(bytes);
        if (length != -32 || bytes.Length < 36) throw PagedInvalid();
        return (components.Resolve(bytes.AsSpan(4, 32), fixedNames), bytes.AsSpan(36).ToArray());
    }
}
