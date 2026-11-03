using System.Security.Cryptography;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Splinter.Core;

namespace Splinter.Runtime;

// Platform-owned file identities and hard links. A pin must survive writer
// replacement and process restart; a remembered path/open handle is insufficient.
public interface IJournalFilePins
{
    IRequestPathGuard? PinMetadataPaths(string directory) => null;
    string Identify(string path);
    string IdentifyOpened(FileStream input) { string identity = Identify(input.Name); Verify(input, identity); return identity; }
    void Verify(FileStream input, string expectedIdentity);
    void VerifyPath(FileStream input, string path, string expectedIdentity)
    { Verify(input, expectedIdentity); if (!WorkspaceStore.SamePath(input.Name, path)) throw new WorkspaceException("ContentReclaimUnsupported", "Compose an opened native name proof after directory preservation."); }
    void Pin(string source, string destination, string expectedIdentity);
    bool DetachBeforeTruncate(string path, long length);
    int? LinkCount(string path) => null;
    long AllocatedBytes(FileStream input) => input.Length;
    void Unpin(string path, string expectedIdentity) => throw new WorkspaceException("ContentReclaimUnsupported", "Compose exact native alias deletion before reclaiming immutable content.");
    string IdentifyDirectory(string path) => throw new WorkspaceException("ContentReclaimUnsupported", "Compose native directory identity for opaque metadata preservation.");
    IDisposable PinDirectory(string path, string expectedIdentity) => throw new WorkspaceException("ContentReclaimUnsupported", "Compose native directory preservation fences.");
    IDisposable CreateDirectory(string path, out string identity) => throw new WorkspaceException("ContentReclaimUnsupported", "Compose exact native scratch-directory creation ownership.");
    IDisposable MoveDirectory(string path, string destination, string expectedIdentity, Action? beforeRename = null) => throw new WorkspaceException("ContentReclaimUnsupported", "Compose handle-relative directory preservation.");
    void UnpinDirectory(string path, string expectedIdentity) => throw new WorkspaceException("ContentReclaimUnsupported", "Compose exact empty-directory deletion.");
}

public sealed record CapturedBacking(string Source, string Name, string Identity, long MinimumLength, string? Hash = null);
public sealed record CapturedExtent(long Start, int Length, int Backing, long Physical,
    WorkspaceJournalPayload Payload, int BlockOffset);
public sealed record CapturedNode(DeltaEntry Entry, CapturedExtent[] Extents);
public sealed record JournalSourceCapture(int Version, string Id, OverlayIdentity Identity, long Sequence,
    string[] ManagedPaths, CapturedNode[] Nodes, string[] Hidden, CapturedBacking[] Backings, string[] ChangedPaths, string Digest,
    PagedJournalCapture? Paged = null, string? ReaderName = null, string? ReaderIdentity = null);
public sealed record PagedJournalCapture(string Directory, MetadataTreeRoot Nodes, MetadataTreeRoot Paths, MetadataTreeRoot Hidden, MetadataTreeRoot Changes, MetadataTreeRoot Names,
    MetadataTreeRoot? Segments = null);
public sealed record SourceCaptureTiming(long StopwatchFrequency, long GateWaitTicks, long MutationPauseTicks);

public sealed partial class JournalDeltaStore
{
    private IJournalFilePins? sourceFilePins;
    public SourceCaptureTiming? LastSourceCaptureTiming { get; private set; }

    public JournalSourceCapture CaptureSource(string destination, string id, string[] managedPaths, bool deferBackingPins = false)
    {
        if (segments is not null) return CaptureSegmentedSource(destination, id, managedPaths, deferBackingPins);
        if (deferBackingPins) throw new WorkspaceException("SourceCaptureUnsupported", "Only a durable segmented root can defer backing admission.");
        long requestedAt = Stopwatch.GetTimestamp();
        using (EnterGate(mutation: true))
        {
            long enteredAt = Stopwatch.GetTimestamp(); bool durableIntent = false, pinned = false;
            try
            {
            Check();
            if (readOnly || sourceFilePins is null)
                throw new WorkspaceException("SourceCaptureUnsupported", "Compose durable backing-file pins for live source capture.");
            WorkspaceStore.EnsurePlainPath(destination);
            string manifest = Path.Combine(destination, "capture.json");
            if (File.Exists(manifest))
            {
                durableIntent = true;
                var existing = SourceCaptureTree.ReadCapture(destination, verifyPagedRoots: false);
                if (existing.Id != id || !SourceCaptureTree.SameIdentity(existing.Identity, Identity) || !existing.ManagedPaths.SequenceEqual(managedPaths))
                    throw new WorkspaceException("SourceCaptureRequestConflict", "Resume the exact frozen capture request.");
                if (File.Exists(Path.Combine(destination, "materialized.json"))) SourceCaptureTree.RequireMaterialized(destination, existing);
                else SourceCaptureTree.EnsurePins(destination, existing, sourceFilePins);
                pinned = true;
                return existing;
            }
            if (Directory.Exists(destination))
                throw new WorkspaceException("SourceCaptureIncomplete", "Preserve the unfinished capture and resume its published intent.");
            FlushPendingWrites();
            if (pagedIndex is not null && latest is null)
            {
                var pagedResult = CapturePagedSource(destination, id, managedPaths);
                durableIntent = true;
                Visit("capture.intent-published", "capture", sequence, 0, 0, 0);
                SourceCaptureTree.EnsurePins(destination, pagedResult, sourceFilePins);
                pinned = true;
                Visit("capture.backings-pinned", "capture", sequence, 0, 0, 0);
                return pagedResult;
            }
            // No basis enumeration, payload read/hash, or independent-result
            // copy is performed under this mutation boundary.
            var backings = new List<CapturedBacking>();
            var indices = new Dictionary<string, int>(StringComparer.Ordinal);
            var captured = new List<CapturedNode>();
            foreach (var node in nodes.Values.Where(n => !n.Entry.Deleted && !SourceCaptureTree.Managed(n.Entry.Path, managedPaths)))
            {
                var extents = new List<CapturedExtent>();
                foreach (var extent in node.Extents)
                {
                    string path; long physical;
                    if (latest is null) { path = journal.Name; physical = extent.JournalOffset; }
                    else
                    {
                        long block = extent.JournalOffset / LatestBlockStride;
                        using var input = latest.OpenBlock(block);
                        path = input.Name; physical = latest.BlockOffset(block) + extent.JournalOffset % LatestBlockStride;
                    }
                    if (!indices.TryGetValue(path, out int index))
                    {
                        index = backings.Count; indices.Add(path, index);
                        backings.Add(new(path, index.ToString("x8") + ".pin", sourceFilePins.Identify(path), 0));
                    }
                    backings[index] = backings[index] with { MinimumLength = Math.Max(backings[index].MinimumLength, checked(physical + extent.Payload.StoredBytes)) };
                    extents.Add(new(extent.Start, extent.Length, index, physical, extent.Payload, extent.BlockOffset));
                }
                captured.Add(new(node.Entry, extents.ToArray()));
            }
            var result = new JournalSourceCapture(1, id, Identity, sequence, managedPaths.ToArray(),
                captured.OrderBy(n => n.Entry.Id).ToArray(), hidden.Where(p => !SourceCaptureTree.Managed(p, managedPaths)).Order(StringComparer.Ordinal).ToArray(), backings.ToArray(),
                pathChanges.Keys.Where(p => !SourceCaptureTree.Managed(p, managedPaths)).Order(StringComparer.Ordinal).ToArray(), "");
            result = result with { Digest = SourceCaptureTree.Digest(result) };
            Directory.CreateDirectory(destination);
            SourceCaptureTree.Publish(manifest, result);
            durableIntent = true;
            Visit("capture.intent-published", "capture", sequence, 0, 0, 0);
            // Recovery completes these pins BEFORE opening/recovering the live
            // writer, so a crash here cannot let retirement erase a generation.
            SourceCaptureTree.EnsurePins(destination, result, sourceFilePins);
            pinned = true;
            Visit("capture.backings-pinned", "capture", sequence, 0, 0, 0);
            return result;
            }
            catch { if (durableIntent && !pinned) Poison(); throw; }
            finally { LastSourceCaptureTiming = new(Stopwatch.Frequency, enteredAt - requestedAt, Stopwatch.GetTimestamp() - enteredAt); }
        }
    }
}

public sealed class SourceCaptureTree : ISourceCaptureTree, ISourceCaptureRanges
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, RespectRequiredConstructorParameters = true, RespectNullableAnnotations = true };
    private readonly ISnapshotTree basis;
    public ISnapshotTree Basis => basis;
    private readonly Dictionary<string, CapturedNode> nodes = null!;
    private readonly HashSet<string> hidden = null!;
    private readonly FileStream[] files = [];
    private readonly WorkspacePathPolicy policy = null!;
    private readonly ISourceCaptureTree? paged;
    private readonly FileStream? readerLease;
    public JournalSourceCapture Capture { get; }
    public SourceCaptureIdentity CaptureIdentity => new(Capture.Id, Capture.Sequence, Capture.Digest);
    public string[]? ChangedPaths => paged is null ? Capture.ChangedPaths : null;
    public IEnumerable<string>? EnumerateChangedPaths() => paged?.EnumerateChangedPaths() ?? Capture.ChangedPaths;
    public SnapshotReference Snapshot => basis.Snapshot;
    public SnapshotGraft? Graft => basis.Graft;
    public bool SupportsConcurrentReads => basis.SupportsConcurrentReads;

    public SourceCaptureTree(string directory, ISnapshotTree basis, IJournalFilePins pins, ICapturedContentReader? capturedContentReader = null)
    {
        Capture = ReadCapture(directory); this.basis = basis;
        if (File.Exists(Path.Combine(directory, "materialized.json")))
            throw new WorkspaceException("SourceCaptureMaterialized", "Read the independently retained result after source capture completion.");
        if (Capture.Identity.Snapshot != basis.Snapshot)
            throw new WorkspaceException("SourceCaptureIdentityMismatch", "Open the exact captured immutable basis.");
        if (Capture.ReaderName is { } reader)
        {
            readerLease = new(Path.Combine(directory, reader), FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            try
            {
                pins.Verify(readerLease, Capture.ReaderIdentity ?? throw Invalid("Missing capture reader identity.")); if (readerLease.Length != 0) throw Invalid("The capture reader marker changed.");
                if (File.Exists(Path.Combine(directory, "materialized.json"))) throw new WorkspaceException("SourceCaptureMaterialized", "Read the independently retained result after source capture completion.");
            }
            catch { readerLease.Dispose(); throw; }
        }
        if (Capture.Paged is not null)
        { try { paged = JournalDeltaStore.OpenPagedCapture(directory, basis, pins, Capture, capturedContentReader); return; } catch { readerLease?.Dispose(); throw; } }
        policy = WorkspacePathPolicy.For(basis.Snapshot);
        nodes = Capture.Nodes.Where(n => !n.Entry.Deleted).ToDictionary(n => n.Entry.Path, policy.Comparer);
        hidden = Capture.Hidden.ToHashSet(policy.Comparer);
        var opened = new List<FileStream>();
        try
        {
            foreach (var backing in Capture.Backings)
            {
                string path = Path.Combine(directory, backing.Name); WorkspaceStore.EnsurePlainPath(path);
                var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 1, FileOptions.RandomAccess);
                opened.Add(file);
                pins.Verify(file, backing.Identity);
                if (file.Length < backing.MinimumLength) throw Invalid("A frozen backing range is missing.");
            }
            files = opened.ToArray();
        }
        catch { foreach (var file in opened) file.Dispose(); readerLease?.Dispose(); throw; }
    }
    public void EnsureAvailable() => basis.EnsureAvailable();
    public void VerifyReadBoundary() => basis.VerifyReadBoundary();
    internal static bool Managed(string path, string[] managed) => managed.Any(m => m.StartsWith('*')
        ? path.EndsWith(m[1..], StringComparison.OrdinalIgnoreCase)
        : path.Equals(m, StringComparison.OrdinalIgnoreCase) || path.StartsWith(m + "/", StringComparison.OrdinalIgnoreCase));
    private (SnapshotEntry? Entry, string? Base) Resolve(string path)
    {
        path = policy.Normalize(path);
        if (Managed(path, Capture.ManagedPaths) || Capture.Identity.ExcludedTopLevel?.Contains(path.Split('/')[0], policy.Comparer) == true) return (null, null);
        if (nodes.TryGetValue(path, out var node))
        {
            var e = node.Entry;
            return (new(e.Path, e.Directory, e.Length, e.Attributes, e.CreationTime, e.LastAccessTime, e.LastWriteTime, e.UnixMode), e.BasePath);
        }
        if (hidden.Contains(path)) return (null, null);
        int slash = path.LastIndexOf('/'); string parent = slash < 0 ? "" : path[..slash];
        if (path.Length == 0) return (basis.Stat(""), "");
        var p = Resolve(parent);
        if (p.Entry is not { Directory: true } || p.Base is null) return (null, null);
        string original = p.Base.Length == 0 ? path[(slash + 1)..] : p.Base + "/" + path[(slash + 1)..];
        var entry = basis.Stat(original);
        return (entry is null ? null : entry with { Path = path }, original);
    }
    public SnapshotEntry? Stat(string path) { if (paged is not null) return paged.Stat(path); EnsureAvailable(); return Resolve(path).Entry; }
    public IReadOnlyList<SnapshotEntry> List(string path)
    {
        if (paged is not null) return paged.List(path);
        EnsureAvailable(); var directory = Resolve(path);
        if (directory.Entry is not { Directory: true }) throw new DirectoryNotFoundException(path);
        string prefix = path.Length == 0 ? "" : path + "/";
        var names = new HashSet<string>(policy.Comparer);
        if (directory.Base is { } original)
            foreach (var entry in basis.List(original)) names.Add(prefix + entry.Path[(entry.Path.LastIndexOf('/') + 1)..]);
        foreach (string child in nodes.Keys)
            if (child.StartsWith(prefix, policy.Comparison) && child.Length > prefix.Length && !child.AsSpan(prefix.Length).Contains('/')) names.Add(child);
        return names.Select(n => Resolve(n).Entry).Where(e => e is not null).Cast<SnapshotEntry>().OrderBy(e => e.Path, policy.Comparer).ToArray();
    }
    public int Read(string path, long offset, Span<byte> destination)
    {
        if (paged is not null) return paged.Read(path, offset, destination);
        EnsureAvailable(); var resolved = Resolve(path);
        var entry = resolved.Entry ?? throw new FileNotFoundException(path);
        if (offset < 0 || entry.Directory) throw new ArgumentOutOfRangeException(nameof(offset));
        int count = (int)Math.Min(destination.Length, Math.Max(0, entry.Length - offset));
        var output = destination[..count]; output.Clear();
        if (!nodes.TryGetValue(path, out var node))
        {
            return resolved.Base is null ? count : basis.Read(resolved.Base, offset, output);
        }
        long baseEnd = Math.Min(node.Entry.BaseLength, offset + count);
        if (node.Entry.BasePath is { } basePath && offset < baseEnd)
        {
            int done = 0, length = (int)(baseEnd - offset);
            while (done < length)
            {
                int n = basis.Read(basePath, offset + done, output.Slice(done, length - done));
                if (n <= 0) throw Invalid("The immutable base ended before its captured range."); done += n;
            }
        }
        foreach (var extent in node.Extents)
        {
            long from = Math.Max(offset, extent.Start), to = Math.Min(offset + count, extent.Start + extent.Length);
            if (to <= from) continue;
            int length = (int)(to - from), blockOffset = checked(extent.BlockOffset + (int)(from - extent.Start));
            var target = output.Slice((int)(from - offset), length);
            ReadExactly(files[extent.Backing], extent.Physical + blockOffset, target);
        }
        return count;
    }
    private static void ReadExactly(FileStream input, long offset, Span<byte> output)
    {
        while (!output.IsEmpty)
        { int n = RandomAccess.Read(input.SafeFileHandle, output, offset); if (n <= 0) throw Invalid("A captured private range ended early."); offset += n; output = output[n..]; }
    }
    public IEnumerable<SnapshotEntry> EnumerateChildren(string path) => paged?.EnumerateChildren(path) ?? List(path);
    public CapturedFileRangeLayout? GetFileRanges(string relativePath)
    {
        if (paged is ISourceCaptureRanges ranges) return ranges.GetFileRanges(relativePath);
        EnsureAvailable(); string path = policy.Normalize(relativePath); var resolved = Resolve(path);
        if (resolved.Entry is not { Directory: false } entry) return null;
        if (!nodes.TryGetValue(path, out var node)) return new(resolved.Base, entry.Length, Array.Empty<CapturedPrivateSourceRange>());
        IEnumerable<CapturedPrivateSourceRange> Private()
        {
            foreach (var extent in node.Extents)
            {
                var backing = Capture.Backings[extent.Backing];
                string identity = RangeIdentity(backing.Identity, extent.Physical, extent.BlockOffset, extent.Length, extent.Payload);
                // Legacy append files can be reopened for writing. Their byte
                // slices are read through the capture tree instead of granting
                // a sealed-backing adoption capability.
                yield return new(extent.Start, extent.Length, identity, BlockIdentity: BlockIdentity(backing.Identity, extent.Physical, extent.Payload), BlockOffset: extent.BlockOffset);
            }
        }
        return new(node.Entry.BasePath, Math.Min(node.Entry.BaseLength, node.Entry.Length), Private(), node.Entry.Id);
    }
    internal static string RangeIdentity(string backingIdentity, long physical, int offset, int length, WorkspaceJournalPayload payload) =>
        "sha256:" + Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { backingIdentity, physical, offset, length, payload }, Json)));
    internal static string BlockIdentity(string backingIdentity, long physical, WorkspaceJournalPayload payload) =>
        Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { backingIdentity, physical, payload }, Json)));
    internal static string ContentBlockIdentity(string hash, bool raw, int length) =>
        Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { hash, raw, length }, Json)));
    public void Dispose() { try { paged?.Dispose(); foreach (var file in files) file.Dispose(); } finally { readerLease?.Dispose(); } }
    internal static string Digest(JournalSourceCapture capture) => Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(capture with { Digest = "" }, Json)));
    public static bool SameIdentity(OverlayIdentity a, OverlayIdentity b) => JsonSerializer.Serialize(a, Json) == JsonSerializer.Serialize(b, Json);
    public static JournalSourceCapture ReadCapture(string directory, bool verifyPagedRoots = true)
    {
        string path = Path.Combine(directory, "capture.json"); WorkspaceStore.EnsurePlainPath(path);
        using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (input.Length > 256L * 1024 * 1024) throw Invalid("The capture control record exceeds its bound.");
        JournalSourceCapture c;
        try { c = JsonSerializer.Deserialize<JournalSourceCapture>(input, Json) ?? throw Invalid("Missing capture manifest."); }
        catch (JsonException) { throw Invalid("Malformed source capture; preserve its pins."); }
        if (c.Version == 2)
        {
            bool materialized = File.Exists(Path.Combine(directory, "materialized.json"));
            JournalDeltaStore.ValidatePagedCapture(directory, c, verifyPagedRoots && !materialized);
            if (materialized) ValidateMaterialized(directory, c);
            return c;
        }
        if (c.Version != 1 || c.Identity is null || string.IsNullOrWhiteSpace(c.Id) || c.Sequence < 0 ||
            c.Nodes.Length > MaxNodes || c.Hidden.Length > MaxNodes || c.ChangedPaths.Length > MaxNodes || c.Digest != Digest(c) ||
            c.Backings.Where((b, i) => b is null || b.Name != i.ToString("x8") + ".pin" || b.MinimumLength < 0 || !Path.IsPathFullyQualified(b.Source) || string.IsNullOrWhiteSpace(b.Identity)).Any() ||
            c.Nodes.Any(n => n is null || n.Entry is null || n.Extents is null || n.Extents.Any(e => e is null || e.Payload is null ||
                e.Backing < 0 || e.Backing >= c.Backings.Length || e.Length <= 0 || e.Start < 0 || e.Physical < 0 ||
                e.Payload.StoredBytes <= 0 || e.Payload.StoredBytes != e.Payload.DecodedBytes || e.Payload.DecodedBytes > 16777216 ||
                e.BlockOffset < 0 || e.BlockOffset > e.Payload.DecodedBytes - e.Length ||
                e.Start > n.Entry.Length - e.Length || e.Physical > c.Backings[e.Backing].MinimumLength - e.Payload.StoredBytes)))
            throw Invalid("The frozen source identity/ranges differ from their manifest.");
        var policy = WorkspacePathPolicy.For(c.Identity.Snapshot);
        try
        {
            string delta = Path.GetFullPath(Path.Combine(directory, "..", "..", "delta")) + Path.DirectorySeparatorChar;
            if (c.Nodes.Any(n => n.Entry.Deleted || n.Entry.Path != policy.Normalize(n.Entry.Path) || n.Entry.Length < 0 ||
                n.Entry.BaseLength < 0 || n.Entry.BasePath is { } b && b != policy.Normalize(b) ||
                n.Entry.Directory && n.Extents.Length != 0) ||
                c.Nodes.Select(n => n.Entry.Id).Distinct().Count() != c.Nodes.Length ||
                c.Nodes.Select(n => n.Entry.Path).Distinct(policy.Comparer).Count() != c.Nodes.Length ||
                c.Hidden.Any(p => string.IsNullOrEmpty(p) || p != policy.Normalize(p)) ||
                c.Hidden.Distinct(policy.Comparer).Count() != c.Hidden.Length ||
                c.ChangedPaths.Any(p => p is null || p != policy.Normalize(p)) ||
                c.ChangedPaths.Distinct(policy.Comparer).Count() != c.ChangedPaths.Length ||
                c.ManagedPaths.Any(string.IsNullOrWhiteSpace) ||
                c.Backings.Any(b => !Path.GetFullPath(b.Source).StartsWith(delta,
                    OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)))
                throw Invalid("The frozen namespace is inconsistent.");
            foreach (var n in c.Nodes)
            {
                long end = 0;
                foreach (var extent in n.Extents)
                { if (extent.Start < end) throw Invalid("Frozen source extents overlap or are reordered."); end = extent.Start + extent.Length; }
            }
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException) { throw Invalid("The frozen namespace has an unsupported path."); }
        return c;
    }
    private const int MaxNodes = 250000;
    internal static void EnsurePins(string directory, JournalSourceCapture capture, IJournalFilePins pins)
    {
        foreach (var backing in EnumerateBackings(capture))
            pins.Pin(backing.Source, Path.Combine(directory, backing.Name), backing.Identity);
    }
    internal static IEnumerable<CapturedBacking> EnumerateBackings(JournalSourceCapture capture)
    {
        if (capture.Paged?.Segments is not { } segments)
        { foreach (var backing in capture.Backings) yield return backing; yield break; }
        using var index = new PagedMetadataStore(capture.Paged.Directory);
        string delta = Path.GetDirectoryName(capture.Paged.Directory)!;
        foreach (var item in index.Open(segments).Enumerate())
        {
            var segment = JournalDeltaStore.ReadCapturedSegment(item.Value.Span);
            if (item.Key != segment.Id.ToString("x16") || segment.NativeIdentity is null) throw Invalid("The frozen segment ownership differs.");
            string name = Path.GetFileNameWithoutExtension(JournalSegmentStorage.FileName(segment.Id));
            yield return new(Path.Combine(delta, name + ".sjr"), name + ".pin", segment.NativeIdentity, segment.Length, segment.Hash);
        }
    }
    public static void RecoverPins(string captures, IJournalFilePins pins)
    {
        if (!Directory.Exists(captures)) return;
        WorkspaceStore.EnsurePlainPath(captures);
        foreach (string directory in Directory.EnumerateDirectories(captures))
        {
            WorkspaceStore.EnsurePlainPath(directory);
            if (File.Exists(Path.Combine(directory, "materialized.json")))
            { Complete(directory, pins); continue; }
            if (File.Exists(Path.Combine(directory, "capture.json"))) EnsurePins(directory, ReadCapture(directory), pins);
            else if (File.Exists(Path.Combine(directory, "capture.json.pending")))
            {
                File.Move(Path.Combine(directory, "capture.json.pending"), Path.Combine(directory, "capture.json"));
                EnsurePins(directory, ReadCapture(directory), pins);
            }
        }
    }
    public static void Complete(string directory, IJournalFilePins pins)
    {
        var capture = ReadCapture(directory); string marker = Path.Combine(directory, "materialized.json");
        using var readerFence = capture.ReaderName is { } name
            ? new FileStream(Path.Combine(directory, name), FileMode.Open, FileAccess.ReadWrite, FileShare.None) : null;
        if (readerFence is not null)
        { pins.Verify(readerFence, capture.ReaderIdentity ?? throw Invalid("Missing capture reader identity.")); if (readerFence.Length != 0) throw Invalid("The capture reader marker changed."); }
        if (!File.Exists(marker)) Publish(marker, new { capture.Id, capture.Digest });
        ValidateMaterialized(directory, capture);
        // Completed captures whose index has been reclaimed have no remaining
        // aliases. Their small materialization receipt remains authoritative.
        if (capture.Paged?.Segments is not null && !Directory.Exists(capture.Paged.Directory))
        {
            if (Directory.EnumerateFiles(directory, "*.pin").Any()) throw Invalid("Retain aliases whose ownership catalog is unavailable.");
            return;
        }
        foreach (var backing in EnumerateBackings(capture))
        {
            string path = Path.Combine(directory, backing.Name); WorkspaceStore.EnsurePlainPath(path);
            if (!File.Exists(path)) continue;
            if (pins.Identify(path) != backing.Identity) throw Invalid("Preserve a replaced capture alias.");
            pins.Unpin(path, backing.Identity);
        }
    }
    private sealed record Materialized(string Id, string Digest);
    private static void ValidateMaterialized(string directory, JournalSourceCapture capture)
    {
        try
        {
            string marker = Path.Combine(directory, "materialized.json"); WorkspaceStore.EnsurePlainPath(marker);
            using var input = new FileStream(marker, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (input.Length > 4096) throw Invalid("The materialization receipt exceeds its bound.");
            var receipt = JsonSerializer.Deserialize<Materialized>(input, Json);
            if (receipt is null || receipt.Id != capture.Id || receipt.Digest != capture.Digest) throw Invalid("The materialized capture receipt changed.");
        }
        catch (JsonException) { throw Invalid("Preserve a damaged materialization receipt and backing pins."); }
    }
    internal static void RequireMaterialized(string directory, JournalSourceCapture capture) => ValidateMaterialized(directory, capture);
    internal static void Publish<T>(string path, T value)
    {
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(value, Json);
        string pending = path + ".pending";
        if (File.Exists(pending))
        {
            byte[] prefix = File.ReadAllBytes(pending);
            if (prefix.Length > bytes.Length || !prefix.AsSpan().SequenceEqual(bytes.AsSpan(0, prefix.Length))) throw Invalid("Preserve the different interrupted capture intent.");
            using var output = new FileStream(pending, FileMode.Open, FileAccess.Write, FileShare.Read); output.Position = prefix.Length;
            output.Write(bytes.AsSpan(prefix.Length)); output.Flush(true);
        }
        else { using var output = new FileStream(pending, FileMode.CreateNew, FileAccess.Write, FileShare.Read); output.Write(bytes); output.Flush(true); }
        File.Move(pending, path);
    }
    internal static void SerializeControl(Stream output, JournalSourceCapture value) => JsonSerializer.Serialize(output, value, Json);
    internal static JournalSourceCapture DeserializeControl(Stream input)
    {
        try { return JsonSerializer.Deserialize<JournalSourceCapture>(input, Json) ?? throw Invalid("Missing frozen capture control."); }
        catch (JsonException) { throw Invalid("Damaged frozen capture control."); }
    }
    private static WorkspaceException Invalid(string message) => new("SourceCaptureInvalid", message);
}
