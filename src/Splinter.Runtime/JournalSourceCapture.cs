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
    string Identify(string path);
    void Verify(FileStream input, string expectedIdentity);
    void Pin(string source, string destination, string expectedIdentity);
    bool DetachBeforeTruncate(string path, long length);
}

public sealed record CapturedBacking(string Source, string Name, string Identity, long MinimumLength);
public sealed record CapturedExtent(long Start, int Length, int Backing, long Physical,
    WorkspaceJournalPayload Payload, int BlockOffset);
public sealed record CapturedNode(DeltaEntry Entry, CapturedExtent[] Extents);
public sealed record JournalSourceCapture(int Version, string Id, OverlayIdentity Identity, long Sequence,
    string[] ManagedPaths, CapturedNode[] Nodes, string[] Hidden, CapturedBacking[] Backings, string[] ChangedPaths, string Digest);
public sealed record SourceCaptureTiming(long StopwatchFrequency, long GateWaitTicks, long MutationPauseTicks);

public sealed partial class JournalDeltaStore
{
    private IJournalFilePins? sourceFilePins;
    public SourceCaptureTiming? LastSourceCaptureTiming { get; private set; }

    public JournalSourceCapture CaptureSource(string destination, string id, string[] managedPaths)
    {
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
                var existing = SourceCaptureTree.ReadCapture(destination);
                if (existing.Id != id || !SourceCaptureTree.SameIdentity(existing.Identity, Identity) || !existing.ManagedPaths.SequenceEqual(managedPaths))
                    throw new WorkspaceException("SourceCaptureRequestConflict", "Resume the exact frozen capture request.");
                SourceCaptureTree.EnsurePins(destination, existing, sourceFilePins);
                pinned = true;
                return existing;
            }
            if (Directory.Exists(destination))
                throw new WorkspaceException("SourceCaptureIncomplete", "Preserve the unfinished capture and resume its published intent.");
            FlushPendingWrites();
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

public sealed class SourceCaptureTree : ISourceCaptureTree
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, RespectRequiredConstructorParameters = true, RespectNullableAnnotations = true };
    private readonly ISnapshotTree basis;
    public ISnapshotTree Basis => basis;
    private readonly Dictionary<string, CapturedNode> nodes;
    private readonly HashSet<string> hidden;
    private readonly FileStream[] files;
    private readonly WorkspacePathPolicy policy;
    private readonly BrotliWorkspacePayloadReader codec = new();
    public JournalSourceCapture Capture { get; }
    public string[] ChangedPaths => Capture.ChangedPaths;
    public SnapshotReference Snapshot => basis.Snapshot;
    public SnapshotGraft? Graft => basis.Graft;
    public bool SupportsConcurrentReads => basis.SupportsConcurrentReads;

    public SourceCaptureTree(string directory, ISnapshotTree basis, IJournalFilePins pins)
    {
        Capture = ReadCapture(directory); this.basis = basis;
        if (Capture.Identity.Snapshot != basis.Snapshot)
            throw new WorkspaceException("SourceCaptureIdentityMismatch", "Open the exact captured immutable basis.");
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
        catch { foreach (var file in opened) file.Dispose(); throw; }
    }
    public void EnsureAvailable() => basis.EnsureAvailable();
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
    public SnapshotEntry? Stat(string path) { EnsureAvailable(); return Resolve(path).Entry; }
    public IReadOnlyList<SnapshotEntry> List(string path)
    {
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
            if (!extent.Payload.Encoded) ReadExactly(files[extent.Backing], extent.Physical + blockOffset, target);
            else
            {
                byte[] encoded = new byte[extent.Payload.StoredBytes]; ReadExactly(files[extent.Backing], extent.Physical, encoded);
                if (codec.Read(encoded, blockOffset, target) != length) throw Invalid("A captured encoded block is incomplete.");
            }
        }
        return count;
    }
    private static void ReadExactly(FileStream input, long offset, Span<byte> output)
    {
        while (!output.IsEmpty)
        { int n = RandomAccess.Read(input.SafeFileHandle, output, offset); if (n <= 0) throw Invalid("A captured private range ended early."); offset += n; output = output[n..]; }
    }
    public void Dispose() { foreach (var file in files) file.Dispose(); }
    internal static string Digest(JournalSourceCapture capture) => Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(capture with { Digest = "" }, Json)));
    public static bool SameIdentity(OverlayIdentity a, OverlayIdentity b) => JsonSerializer.Serialize(a, Json) == JsonSerializer.Serialize(b, Json);
    public static JournalSourceCapture ReadCapture(string directory)
    {
        string path = Path.Combine(directory, "capture.json"); WorkspaceStore.EnsurePlainPath(path);
        using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (input.Length > 256L * 1024 * 1024) throw Invalid("The capture manifest exceeds its bound.");
        JournalSourceCapture c;
        try { c = JsonSerializer.Deserialize<JournalSourceCapture>(input, Json) ?? throw Invalid("Missing capture manifest."); }
        catch (JsonException) { throw Invalid("Malformed source capture; preserve its pins."); }
        if (c.Version != 1 || c.Identity is null || string.IsNullOrWhiteSpace(c.Id) || c.Sequence < 0 ||
            c.Nodes.Length > MaxNodes || c.Hidden.Length > MaxNodes || c.ChangedPaths.Length > MaxNodes || c.Digest != Digest(c) ||
            c.Backings.Where((b, i) => b is null || b.Name != i.ToString("x8") + ".pin" || b.MinimumLength < 0 || !Path.IsPathFullyQualified(b.Source) || string.IsNullOrWhiteSpace(b.Identity)).Any() ||
            c.Nodes.Any(n => n is null || n.Entry is null || n.Extents is null || n.Extents.Any(e => e is null || e.Payload is null ||
                e.Backing < 0 || e.Backing >= c.Backings.Length || e.Length <= 0 || e.Start < 0 || e.Physical < 0 ||
                e.Payload.StoredBytes <= 0 || e.Payload.StoredBytes > e.Payload.DecodedBytes || e.Payload.DecodedBytes > 16777216 ||
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
        foreach (var backing in capture.Backings)
            pins.Pin(backing.Source, Path.Combine(directory, backing.Name), backing.Identity);
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
        if (!File.Exists(marker)) Publish(marker, new { capture.Id, capture.Digest });
        ValidateMaterialized(directory, capture);
        foreach (var backing in capture.Backings)
        {
            string path = Path.Combine(directory, backing.Name); WorkspaceStore.EnsurePlainPath(path);
            if (!File.Exists(path)) continue;
            if (pins.Identify(path) != backing.Identity) throw Invalid("Preserve a replaced capture alias.");
            File.Delete(path);
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
    internal static void Publish<T>(string path, T value)
    {
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(value, Json);
        string pending = path + ".pending";
        if (File.Exists(pending))
        { if (!File.ReadAllBytes(pending).AsSpan().SequenceEqual(bytes)) throw Invalid("Preserve the different interrupted capture intent."); }
        else { using var output = new FileStream(pending, FileMode.CreateNew, FileAccess.Write, FileShare.Read); output.Write(bytes); output.Flush(true); }
        File.Move(pending, path);
    }
    private static WorkspaceException Invalid(string message) => new("SourceCaptureInvalid", message);
}
