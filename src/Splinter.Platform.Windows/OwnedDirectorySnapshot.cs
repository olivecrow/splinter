using System.Security.Cryptography;
using System.Buffers;
using System.Text.Json;
using Microsoft.Win32.SafeHandles;
using Splinter.Core;
using Splinter.Runtime;

namespace Splinter.Platform.Windows;

public sealed record OwnedDirectoryItem(SnapshotEntry Entry, string? Hash);
public sealed record OwnedDirectoryManifest(int Version, SnapshotReference Snapshot, string OwnerId, OwnedDirectoryItem[] Items);
public sealed record OwnedDirectoryProof(int Version, SnapshotReference Snapshot, string OwnerId, string ManifestHash, long LogicalBytes);

// The original has been renamed into an owned directory, and every existing
// file is fenced against writes/deletion before exposing a reader. Metadata is
// fixed in a manifest. No payload cache or mutable-project fallback exists.
public sealed class OwnedDirectorySnapshotTree : ISnapshotTree, IDisposable
{
    public const string ProviderName = "owned-directory";
    public SnapshotReference Snapshot { get; }
    private readonly Dictionary<string, OwnedDirectoryItem> items;
    private readonly Dictionary<string, SafeFileHandle> files = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, SnapshotEntry[]> children;
    private bool disposed;
    public OwnedDirectoryManifest Manifest { get; }
    public OwnedDirectorySnapshotTree(OwnedDirectoryManifest manifest, SnapshotReference snapshot, bool verifyContent = true)
    {
        if (manifest.Version != 1 || manifest.Snapshot != snapshot || snapshot.Provider != ProviderName || !snapshot.Persistent || !snapshot.Differential ||
            snapshot.Consistency != "stopped-original-owned-directory")
            throw new WorkspaceException("GeneratedBasisMismatch", "Preserve an unsupported or changed generated basis.");
        Manifest = manifest; Snapshot = snapshot;
        items = manifest.Items.ToDictionary(i => i.Entry.Path, StringComparer.OrdinalIgnoreCase);
        children = manifest.Items.Where(i => i.Entry.Path.Length != 0).GroupBy(i => Parent(i.Entry.Path), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Select(i => i.Entry).ToArray(), StringComparer.OrdinalIgnoreCase);
        if (items.Count > JournalDeltaStore.MaxIndexedNodes || !items.ContainsKey("")) throw new WorkspaceException("GeneratedBasisCorrupt", "Invalid generated metadata bounds/root.");
        try
        {
            foreach (var item in manifest.Items)
            {
                string path = Resolve(item.Entry.Path); var attrs = File.GetAttributes(path);
                if ((attrs & FileAttributes.ReparsePoint) != 0 || ((attrs & FileAttributes.Directory) != 0) != item.Entry.Directory)
                    throw new WorkspaceException("GeneratedBasisChanged", "Generated basis type or path changed.");
                if (item.Entry.Directory) continue;
                var handle = File.OpenHandle(path, FileMode.Open, FileAccess.Read, FileShare.Read, FileOptions.RandomAccess);
                files.Add(item.Entry.Path, handle);
                if (RandomAccess.GetLength(handle) != item.Entry.Length || verifyContent && Hash(handle) != item.Hash)
                    throw new WorkspaceException("GeneratedBasisChanged", "Generated content differs from its frozen manifest.");
            }
        }
        catch { Dispose(); throw; }
    }
    public static OwnedDirectoryManifest Capture(SnapshotReference snapshot, string owner)
    {
        WorkspaceStore.EnsurePlainPath(snapshot.RootPath);
        var items = new List<OwnedDirectoryItem>();
        void Walk(string relative)
        {
            string path = Path.Combine(snapshot.RootPath, relative.Replace('/', Path.DirectorySeparatorChar));
            WorkspaceStore.EnsurePlainPath(path); var attrs = File.GetAttributes(path);
            bool directory = (attrs & FileAttributes.Directory) != 0;
            FileSystemInfo info = directory ? new DirectoryInfo(path) : new FileInfo(path);
            var entry = new SnapshotEntry(relative, directory, directory ? 0 : ((FileInfo)info).Length, (uint)attrs,
                info.CreationTimeUtc.ToFileTimeUtc(), info.LastAccessTimeUtc.ToFileTimeUtc(), info.LastWriteTimeUtc.ToFileTimeUtc());
            string? hash = null;
            if (!directory) { using var h = File.OpenHandle(path, FileMode.Open, FileAccess.Read, FileShare.Read); hash = Hash(h); }
            if (items.Count >= JournalDeltaStore.MaxIndexedNodes) throw new WorkspaceException("OverlayMetadataLimit", "Generated basis exceeds the metadata limit.");
            items.Add(new(entry, hash));
            if (directory) foreach (string child in Directory.EnumerateFileSystemEntries(path).Order(StringComparer.OrdinalIgnoreCase))
                Walk(relative.Length == 0 ? Path.GetFileName(child) : relative + "/" + Path.GetFileName(child));
        }
        Walk(""); return new(1, snapshot, owner, items.ToArray());
    }
    public static string Hash(SafeFileHandle handle)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256); byte[] buffer = ArrayPool<byte>.Shared.Rent(65536); long offset = 0;
        try
        {
            for (int read; (read = RandomAccess.Read(handle, buffer.AsSpan(0, 65536), offset)) != 0; offset += read) hash.AppendData(buffer, 0, read);
            return Convert.ToHexStringLower(hash.GetHashAndReset());
        }
        finally { ArrayPool<byte>.Shared.Return(buffer); }
    }
    public void EnsureAvailable() { ObjectDisposedException.ThrowIf(disposed, this); if (!Directory.Exists(Snapshot.RootPath)) throw new WorkspaceException("SnapshotMissing", "Retain the missing generated basis."); }
    public SnapshotEntry? Stat(string path) { EnsureAvailable(); return items.GetValueOrDefault(JournalDeltaStore.Normalize(path))?.Entry; }
    public IReadOnlyList<SnapshotEntry> List(string path)
    {
        path = JournalDeltaStore.Normalize(path); EnsureAvailable();
        if (Stat(path) is not { Directory: true }) throw new DirectoryNotFoundException(path);
        return children.GetValueOrDefault(path) ?? [];
    }
    public int Read(string path, long offset, Span<byte> destination) { EnsureAvailable(); return RandomAccess.Read(files[JournalDeltaStore.Normalize(path)], destination, offset); }
    private string Resolve(string path)
    {
        if (JournalDeltaStore.Normalize(path) != path) throw new WorkspaceException("GeneratedBasisCorrupt", "Invalid manifest path.");
        string result = Path.Combine(Snapshot.RootPath, path.Replace('/', Path.DirectorySeparatorChar)); WorkspaceStore.EnsurePlainPath(result); return result;
    }
    public void Dispose() { disposed = true; foreach (var file in files.Values) file.Dispose(); files.Clear(); }
    private static string Parent(string path) { int slash = path.LastIndexOf('/'); return slash < 0 ? "" : path[..slash]; }
}

public sealed class OwnedDirectorySnapshotProvider(string directory, string owner) : ISourceSnapshotProvider
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public OwnedDirectoryManifest Read(SnapshotReference snapshot)
    {
        WorkspaceStore.EnsurePlainPath(directory);
        var manifest = JsonSerializer.Deserialize<OwnedDirectoryManifest>(File.ReadAllText(Path.Combine(directory, "manifest.json")), Json)
            ?? throw new WorkspaceException("GeneratedBasisCorrupt", "Missing generated manifest.");
        if (manifest.Snapshot != snapshot || manifest.OwnerId != owner) throw new WorkspaceException("GeneratedBasisMismatch", "Generated owner/reference mismatch.");
        if (ManagedDirectoryBasis.HashFile(Path.Combine(directory, "manifest.json")) != Proof(snapshot).ManifestHash)
            throw new WorkspaceException("GeneratedBasisChanged", "Retain a changed generated manifest.");
        return manifest;
    }
    private OwnedDirectoryProof Proof(SnapshotReference snapshot)
    {
        var proof = JsonSerializer.Deserialize<OwnedDirectoryProof>(File.ReadAllText(Path.Combine(directory, "proof.json")), Json);
        if (proof is null || proof.Version != 1 || proof.Snapshot != snapshot || proof.OwnerId != owner || proof.ManifestHash.Length != 64)
            throw new WorkspaceException("GeneratedBasisMismatch", "Retain a mismatched generated proof.");
        return proof;
    }
    public IDisposable AcquireReadLease() => new ReadLease(directory);
    private sealed class ReadLease : IDisposable
    {
        private readonly List<FileStream> handles = [];
        public ReadLease(string directory)
        {
            try { foreach (string name in new[] { "readers.lock", "manifest.json", "proof.json" }) handles.Add(new(Path.Combine(directory, name), FileMode.Open, FileAccess.Read, FileShare.Read)); }
            catch { Dispose(); throw; }
        }
        public void Dispose() { foreach (var handle in handles) handle.Dispose(); handles.Clear(); }
    }
    public Task<SnapshotReference> CaptureAsync(SnapshotCapture request, CancellationToken ct = default) => throw new WorkspaceException("GeneratedBasisPreparationRequired", "Use the explicit generated-directory preparation lifecycle.");
    public Task<SnapshotObservation> ObserveAsync(SnapshotReference snapshot, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested(); var proof = Proof(snapshot);
        if (!Directory.Exists(snapshot.RootPath)) return Task.FromResult(new SnapshotObservation(snapshot.Id, false, "SnapshotMissing", null, null));
        return Task.FromResult(new SnapshotObservation(snapshot.Id, true, null, proof.LogicalBytes, null) { ReadAccess = new(snapshot.Id, snapshot.RootPath, proof.ManifestHash) });
    }
    public async Task<SnapshotReadAccess> ResolveReadAccessAsync(SnapshotReference snapshot, CancellationToken ct = default) =>
        (await ObserveAsync(snapshot, ct)).ReadAccess ?? throw new WorkspaceException("SnapshotMissing", "The original generated basis is missing.");
    public Task<SnapshotReclamation> ReleaseAsync(SnapshotReference snapshot, CancellationToken ct = default, string? previousAttemptId = null) =>
        throw new WorkspaceException("GeneratedBasisRestoreRequired", "Restore the original through its explicit lifecycle after all dependents are absent.");
}
