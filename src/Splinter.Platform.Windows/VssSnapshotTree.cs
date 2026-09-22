using Splinter.Core;
using Splinter.Runtime;
using Microsoft.Win32.SafeHandles;

namespace Splinter.Platform.Windows;

// Reading an arbitrary directory as a live baseline is deliberately unsupported.
// Capture and provider validation are performed by the elevated VSS requester.
public sealed class VssSnapshotTree : ISnapshotTree, IDisposable
{
    public SnapshotReference Snapshot { get; }
    private readonly string root;
    private readonly bool verifiedAccess;
    private readonly Func<SnapshotObservation>? verifiedHealth;
    private readonly Dictionary<string, SafeFileHandle> handles = new(StringComparer.OrdinalIgnoreCase);
    private readonly Queue<string> handleOrder = new();
    private readonly Dictionary<string, SnapshotEntry?> metadata = new(StringComparer.OrdinalIgnoreCase);
    private readonly Queue<string> metadataOrder = new();
    public const int MaxReadHandles = 32;
    public const int MaxMetadataEntries = 32768;
    public VssSnapshotTree(SnapshotReference snapshot, SnapshotReadAccess? access = null, Func<SnapshotObservation>? verifiedHealth = null)
    {
        string readRoot = ResolveReadRoot(snapshot, access);
        if (!OperatingSystem.IsWindows() || snapshot.Provider != "windows-vss-system" ||
            !snapshot.Persistent || !snapshot.Differential ||
            !VssSnapshotVerification.TryRelative(readRoot, out _))
            throw new WorkspaceException("SnapshotProviderUnsupported", "Only a validated persistent system-provider VSS snapshot is accepted.");
        Snapshot = snapshot; root = readRoot.TrimEnd('\\', '/'); this.verifiedHealth = verifiedHealth; verifiedAccess = access is not null;
        EnsureAvailable();
    }
    internal static string ResolveReadRoot(SnapshotReference snapshot, SnapshotReadAccess? access)
    {
        access?.RequireMatch(snapshot);
        string readRoot = access?.RootPath ?? snapshot.RootPath;
        if (!VssSnapshotVerification.TryRelative(readRoot, out string relative) ||
            !VssSnapshotVerification.TryRelative(snapshot.RootPath, out string original) || relative != original)
            throw new WorkspaceException("SnapshotIdentityMismatch", "Read access must retain the captured source-relative path.");
        return readRoot;
    }
    public void EnsureAvailable()
    {
        var observation = verifiedHealth?.Invoke();
        if (observation is null || observation.Available == true)
        {
            observation = SnapshotAvailabilityProbe.Observe(Snapshot with { RootPath = root });
            if (verifiedAccess && observation.Available == false)
                observation = new(Snapshot.Id, null, "SnapshotObservationUnknown", null, null);
        }
        if (observation.Available != true)
            throw new WorkspaceException(observation.FailureCode ?? "SnapshotObservationUnknown", "The original snapshot cannot be read. Preserve the delta and do not recapture.");
    }
    public SnapshotEntry? Stat(string relativePath)
    {
        EnsureAvailable(); string relative = JournalDeltaStore.Normalize(relativePath);
        lock (metadata) if (metadata.TryGetValue(relative, out var cached)) return cached;
        string path = Resolve(relative);
        FileAttributes attrs;
        try { attrs = File.GetAttributes(path); }
        catch (FileNotFoundException) { Remember(relative, null); return null; }
        catch (DirectoryNotFoundException) { Remember(relative, null); return null; }
        Reject(attrs);
        bool directory = (attrs & FileAttributes.Directory) != 0;
        FileSystemInfo info = directory ? new DirectoryInfo(path) : new FileInfo(path);
        var entry = new SnapshotEntry(relative, directory, directory ? 0 : ((FileInfo)info).Length,
            (uint)attrs, info.CreationTimeUtc.ToFileTimeUtc(), info.LastAccessTimeUtc.ToFileTimeUtc(), info.LastWriteTimeUtc.ToFileTimeUtc());
        Remember(relative, entry); return entry;
    }
    public IReadOnlyList<SnapshotEntry> List(string relativeDirectory)
    {
        EnsureAvailable(); string parent = JournalDeltaStore.Normalize(relativeDirectory);
        var result = new List<SnapshotEntry>();
        foreach (var info in new DirectoryInfo(Resolve(parent)).EnumerateFileSystemInfos())
        {
            if (result.Count >= JournalDeltaStore.MaxIndexedNodes) throw new WorkspaceException("OverlayMetadataLimit", "Directory exceeds the bounded metadata index.");
            Reject(info.Attributes); bool directory = (info.Attributes & FileAttributes.Directory) != 0;
            string relative = parent.Length == 0 ? info.Name : parent + "/" + info.Name;
            var entry = new SnapshotEntry(relative, directory, directory ? 0 : ((FileInfo)info).Length, (uint)info.Attributes,
                info.CreationTimeUtc.ToFileTimeUtc(), info.LastAccessTimeUtc.ToFileTimeUtc(), info.LastWriteTimeUtc.ToFileTimeUtc());
            Remember(relative, entry); result.Add(entry);
        }
        return result;
    }
    private void Remember(string path, SnapshotEntry? entry)
    {
        lock (metadata)
        {
            if (metadata.ContainsKey(path)) return;
            if (metadata.Count == MaxMetadataEntries) metadata.Remove(metadataOrder.Dequeue());
            metadata.Add(path, entry); metadataOrder.Enqueue(path);
        }
    }
    public int Read(string relativePath, long offset, Span<byte> destination)
    {
        EnsureAvailable(); string relative = JournalDeltaStore.Normalize(relativePath);
        // A bounded handle cache contains no file data. The immutable path and
        // each reparse boundary are checked when acquiring a handle.
        lock (handles)
        {
            if (!handles.TryGetValue(relative, out var file))
            {
                string path = Resolve(relative); Reject(File.GetAttributes(path));
                if (handles.Count == MaxReadHandles)
                { string evicted = handleOrder.Dequeue(); handles[evicted].Dispose(); handles.Remove(evicted); }
                file = File.OpenHandle(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, FileOptions.RandomAccess);
                handles.Add(relative, file); handleOrder.Enqueue(relative);
            }
            return RandomAccess.Read(file, destination, offset);
        }
    }
    public void Dispose() { lock (handles) { foreach (var handle in handles.Values) handle.Dispose(); handles.Clear(); handleOrder.Clear(); } }
    private string Resolve(string relativePath)
    {
        string relative = JournalDeltaStore.Normalize(relativePath);
        // Reject reparse points at every component, including directories that
        // could otherwise escape the immutable device into the mutable original.
        string path = root;
        if (relative.Length != 0)
            foreach (string part in relative.Split('/'))
            {
                path += "\\" + part;
                try { Reject(File.GetAttributes(path)); }
                catch (FileNotFoundException) { break; }
                catch (DirectoryNotFoundException) { break; }
            }
        return root + "\\" + relative.Replace('/', '\\');
    }
    private static void Reject(FileAttributes attributes)
    {
        SnapshotInputChecks.RejectUnsupportedAttributes((uint)attributes);
    }
}
