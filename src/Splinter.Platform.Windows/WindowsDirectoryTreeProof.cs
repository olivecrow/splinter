using Microsoft.Win32.SafeHandles;
using Splinter.Core;
using Splinter.Runtime;

namespace Splinter.Platform.Windows;

// A scoped, root-first namespace proof. The caller owns the immutable root and
// its ancestor lease; this proof pins every descendant directory before any
// child is opened. A disposed proof is never reused across a restore move/gap.
internal sealed class WindowsDirectoryTreeProof : IDisposable
{
    private readonly string root;
    private readonly SafeFileHandle rootHandle;
    private readonly bool metadataWrite;
    private readonly Dictionary<string, SafeFileHandle> directories = new(StringComparer.OrdinalIgnoreCase);
    private bool disposed;

    internal WindowsDirectoryTreeProof(string root, SafeFileHandle rootHandle, bool metadataWrite = true)
    {
        this.root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)); this.rootHandle = rootHandle; this.metadataWrite = metadataWrite;
        _ = SnapshotInputChecks.FileIdentity(rootHandle, directory: true);
        WindowsOwnedDirectoryLease.RequirePath(rootHandle, this.root);
        directories.Add(this.root, rootHandle);
    }

    internal string Resolve(string relative)
    {
        ObjectDisposedException.ThrowIf(disposed || rootHandle.IsClosed, this);
        if (JournalDeltaStore.Normalize(relative) != relative)
            throw new WorkspaceException("GeneratedBasisCorrupt", "Invalid generated manifest path.");
        return relative.Length == 0 ? root : Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
    }

    internal SafeFileHandle PinDirectory(string relative)
    {
        string path = Resolve(relative);
        if (directories.TryGetValue(path, out var existing)) return existing;
        var handle = ManagedRestoreFileAccess.DirectoryHandle(path, proof: this, metadataWrite: metadataWrite);
        try { directories.Add(path, handle); return handle; }
        catch { handle.Dispose(); throw; }
    }

    internal FileStream Read(string relative) => ManagedRestoreFileAccess.Read(Resolve(relative), proof: this);
    internal SafeFileHandle ReadHandle(string relative) => ManagedRestoreFileAccess.ReadHandle(Resolve(relative), proof: this, metadataWrite: metadataWrite);

    internal void RequireParent(string path)
    {
        ObjectDisposedException.ThrowIf(disposed || rootHandle.IsClosed, this);
        string? parent = Path.GetDirectoryName(Path.GetFullPath(path));
        if (parent is null || !directories.TryGetValue(parent, out var handle) || handle.IsClosed || handle.IsInvalid)
            throw new WorkspaceException("GeneratedBasisCorrupt", "Generated entries require a pinned ordinary parent directory.");
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        foreach (var handle in directories.Values.Reverse()) if (!ReferenceEquals(handle, rootHandle)) handle.Dispose();
        directories.Clear();
    }
}
