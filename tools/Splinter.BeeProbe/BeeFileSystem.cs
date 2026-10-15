using System.Buffers.Binary;
using System.Text;
using Fsp;
using Splinter.Core;
using Splinter.Platform.Windows;
using Splinter.Runtime;
using FileInfo = Fsp.Interop.FileInfo;
using VolumeInfo = Fsp.Interop.VolumeInfo;

namespace Splinter.BeeProbe;

// Development-only namespace experiment. The product filesystem is unchanged.
// Native Bee files deliberately use ordinary NTFS durability, outside the journal.
internal sealed class BeeFileSystem : FileSystemBase
{
    private const string Bee = "Library/Bee";
    private const uint Tag = 0xA0000003; // IO_REPARSE_TAG_MOUNT_POINT
    private readonly WinFspOverlayFileSystem inner;
    private readonly IWorkspaceDeltaStore store;
    private readonly NativePin? target;
    private readonly byte[] reparse = [];
    private readonly object admission = new();
    private readonly Dictionary<Guid, int> retainedContexts = [];
    private bool stopping;
    public int OpenHandles => inner.OpenHandles;
    public int RetainedFileContexts => inner.RetainedFileContexts;
    internal ulong RootIndexNumber => BitConverter.ToUInt64(store.Stat("")!.Id.ToByteArray(), 0);

    public BeeFileSystem(IWorkspaceDeltaStore store, NativePin? target, Action<Exception> report)
    {
        this.store = store; this.target = target;
        inner = new WinFspOverlayFileSystem(store, report);
        if (target is null) return;
        if (store.Stat(Bee) is not { Directory: true }) throw new InvalidOperationException("Basis must contain an empty Library/Bee directory.");
        if (store.List(Bee).Count != 0) throw new InvalidOperationException("Bee seed must be empty; initial copying is not part of this probe.");
        string native = "\\??\\" + target.Path;
        byte[] substitute = Encoding.Unicode.GetBytes(native), display = Encoding.Unicode.GetBytes(target.Path);
        reparse = new byte[16 + substitute.Length + 2 + display.Length + 2];
        BinaryPrimitives.WriteUInt32LittleEndian(reparse, Tag);
        BinaryPrimitives.WriteUInt16LittleEndian(reparse.AsSpan(4), checked((ushort)(reparse.Length - 8)));
        BinaryPrimitives.WriteUInt16LittleEndian(reparse.AsSpan(10), checked((ushort)substitute.Length));
        BinaryPrimitives.WriteUInt16LittleEndian(reparse.AsSpan(12), checked((ushort)(substitute.Length + 2)));
        BinaryPrimitives.WriteUInt16LittleEndian(reparse.AsSpan(14), checked((ushort)display.Length));
        substitute.CopyTo(reparse, 16); display.CopyTo(reparse, 18 + substitute.Length);
    }
    private static bool Exact(string path) => JournalDeltaStore.Normalize(path).Equals(Bee, StringComparison.OrdinalIgnoreCase);
    private bool Protected(string path)
    {
        if (target is null) return false;
        path = JournalDeltaStore.Normalize(path);
        return path.Length == 0 || path.Equals("Library", StringComparison.OrdinalIgnoreCase) ||
            path.Equals(Bee, StringComparison.OrdinalIgnoreCase) || path.StartsWith(Bee + "/", StringComparison.OrdinalIgnoreCase);
    }
    private bool Protected(object node) => node is Guid id && Protected(store.Get(id).Path);
    private void Decorate(string path, ref FileInfo info)
    { if (target is not null && Exact(path)) { target.Verify(); info.FileAttributes |= 0x400; info.ReparseTag = Tag; } }
    private void Decorate(object node, ref FileInfo info)
    { if (node is Guid id) Decorate(store.Get(id).Path, ref info); }
    public override int Init(object host)
    { int status = inner.Init(host); ((FileSystemHost)host).ReparsePoints = target is not null; return status; }
    public void BeginStop()
    {
        lock (admission)
        {
            if (OpenHandles != 0 || RetainedFileContexts != 0) throw new IOException("Owned overlay handles or contexts remain; preserve mount.");
            stopping = true;
        }
    }
    public void Resume() { lock (admission) stopping = false; }
    internal IDisposable? TryFenceNewOpens(int ownedOpenHandles)
    {
        lock (admission)
        {
            if (stopping || OpenHandles != ownedOpenHandles) return null;
            stopping = true; return new AdmissionFence(this);
        }
    }
    internal string[] RetainedLinkedPaths()
    {
        lock (admission)
            return retainedContexts.Keys.Select(store.Get).Where(entry => !entry.Deleted)
                .Select(entry => "\\" + entry.Path.Replace('/', '\\')).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }
    public object[] RetainedContextDiagnostics()
    {
        lock (admission)
            return retainedContexts.Select(pair =>
            {
                try
                {
                    var entry = store.Get(pair.Key);
                    return (object)new { id = pair.Key, contexts = pair.Value, path = entry.Path, entry.Deleted, entry.Directory };
                }
                catch (Exception error) { return new { id = pair.Key, contexts = pair.Value, unavailable = error.Message }; }
            }).ToArray();
    }
    internal bool WaitForContextDrain(TimeSpan timeout)
    {
        long started = System.Diagnostics.Stopwatch.GetTimestamp();
        lock (admission)
        {
            while (RetainedFileContexts != 0 || retainedContexts.Count != 0)
            {
                TimeSpan remaining = timeout - System.Diagnostics.Stopwatch.GetElapsedTime(started);
                if (remaining <= TimeSpan.Zero) return false;
                Monitor.Wait(admission, remaining);
            }
            return OpenHandles == 0;
        }
    }
    private void Retain(object node)
    { var id = (Guid)node; retainedContexts[id] = retainedContexts.GetValueOrDefault(id) + 1; }
    private sealed class AdmissionFence(BeeFileSystem owner) : IDisposable
    {
        private BeeFileSystem? owner = owner;
        public void Dispose() => Interlocked.Exchange(ref owner, null)?.Resume();
    }
    public override int ExceptionHandler(Exception ex) => inner.ExceptionHandler(ex);
    public override int GetVolumeInfo(out VolumeInfo info) => inner.GetVolumeInfo(out info);
    public override int GetSecurityByName(string name, out uint attributes, ref byte[] security)
    {
        if (target is not null && !Exact(name) && FindReparsePoint(name, out attributes)) return STATUS_REPARSE;
        int status = inner.GetSecurityByName(name, out attributes, ref security);
        if (status >= 0 && target is not null && Exact(name)) { target.Verify(); attributes |= 0x400; }
        return status;
    }
    public override int GetReparsePointByName(string name, bool directory, ref byte[] data)
    {
        lock (admission)
        {
            if (stopping) return STATUS_ACCESS_DENIED;
            if (target is null || !Exact(name)) return STATUS_NOT_A_REPARSE_POINT;
            target.Verify(); data = reparse; return STATUS_SUCCESS;
        }
    }
    public override int GetReparsePoint(object node, object desc, string name, ref byte[] data) => GetReparsePointByName(name, true, ref data);
    public override int SetReparsePoint(object node, object desc, string name, byte[] data) => STATUS_ACCESS_DENIED;
    public override int DeleteReparsePoint(object node, object desc, string name, byte[] data) => STATUS_ACCESS_DENIED;
    public override int GetSecurity(object node, object desc, ref byte[] security) => inner.GetSecurity(node, desc, ref security);
    public override int Create(string name, uint options, uint access, uint attributes, byte[] security, ulong allocation,
        out object node, out object desc, out FileInfo info, out string normalized)
    {
        lock (admission)
        {
            if (stopping || Protected(name))
            {
                node = desc = null!; info = default; normalized = null!;
                // Recursive mkdir callers probe every parent, including existing
                // protected directories. Preserve the normal collision status:
                // this does not create, replace, or modify the graft namespace.
                return !stopping && store.Stat(name) is { Directory: true }
                    ? STATUS_OBJECT_NAME_COLLISION : STATUS_ACCESS_DENIED;
            }
            int status = inner.Create(name, options, access, attributes, security, allocation, out node, out desc, out info, out normalized);
            if (status >= 0) Retain(node); return status;
        }
    }
    public override int Open(string name, uint options, uint access, out object node, out object desc, out FileInfo info, out string normalized)
    {
        lock (admission)
        {
            if (stopping) { node = desc = null!; info = default; normalized = null!; return STATUS_ACCESS_DENIED; }
            int status = inner.Open(name, options, access, out node, out desc, out info, out normalized);
            if (status >= 0) { Retain(node); Decorate(name, ref info); } return status;
        }
    }
    public override int Overwrite(object node, object desc, uint attributes, bool replace, ulong allocation, out FileInfo info)
    { if (Protected(node)) { info = default; return STATUS_ACCESS_DENIED; } return inner.Overwrite(node, desc, attributes, replace, allocation, out info); }
    public override void Cleanup(object node, object desc, string name, uint flags)
    {
        // CanDelete rejects the immutable graft stub and its ancestors. Do not
        // allow an unexpected delete cleanup to mutate the underlying journal.
        inner.Cleanup(node, desc, name, Protected(node) ? flags & ~CleanupDelete : flags);
    }
    public override void Close(object node, object desc)
    {
        lock (admission)
        {
            try { inner.Close(node, desc); }
            finally
            {
                var id = (Guid)node;
                if (retainedContexts.TryGetValue(id, out int count))
                { if (count > 1) retainedContexts[id] = count - 1; else retainedContexts.Remove(id); }
                Monitor.PulseAll(admission);
            }
        }
    }
    public override int Read(object node, object desc, IntPtr buffer, ulong offset, uint length, out uint transferred) => inner.Read(node, desc, buffer, offset, length, out transferred);
    public override int Write(object node, object desc, IntPtr buffer, ulong offset, uint length, bool end, bool constrained, out uint transferred, out FileInfo info)
    { if (Protected(node)) { transferred = 0; info = default; return STATUS_ACCESS_DENIED; } return inner.Write(node, desc, buffer, offset, length, end, constrained, out transferred, out info); }
    public override int Flush(object node, object desc, out FileInfo info)
    { int status = inner.Flush(node, desc, out info); if (status >= 0) Decorate(node, ref info); return status; }
    public override int GetFileInfo(object node, object desc, out FileInfo info)
    { int status = inner.GetFileInfo(node, desc, out info); if (status >= 0) Decorate(node, ref info); return status; }
    public override int SetBasicInfo(object node, object desc, uint attributes, ulong creation, ulong access, ulong write, ulong change, out FileInfo info)
    { if (Protected(node)) { info = default; return STATUS_ACCESS_DENIED; } return inner.SetBasicInfo(node, desc, attributes, creation, access, write, change, out info); }
    public override int SetFileSize(object node, object desc, ulong size, bool allocation, out FileInfo info)
    { if (Protected(node)) { info = default; return STATUS_ACCESS_DENIED; } return inner.SetFileSize(node, desc, size, allocation, out info); }
    public override int CanDelete(object node, object desc, string name) => Protected(name) ? STATUS_ACCESS_DENIED : inner.CanDelete(node, desc, name);
    public override int Rename(object node, object desc, string name, string destination, bool replace)
        => Protected(name) || Protected(destination) ? STATUS_ACCESS_DENIED : inner.Rename(node, desc, name, destination, replace);
    public override bool ReadDirectoryEntry(object node, object desc, string pattern, string marker, ref object context, out string name, out FileInfo info)
    {
        bool found = inner.ReadDirectoryEntry(node, desc, pattern, marker, ref context, out name, out info);
        if (found && name is not "." and not "..") Decorate(store.Get((Guid)node).Path + "/" + name, ref info);
        return found;
    }
    public override int GetDirInfoByName(object node, object desc, string name, out string normalized, out FileInfo info)
    {
        int status = inner.GetDirInfoByName(node, desc, name, out normalized, out info);
        if (status >= 0 && name is not "." and not "..") Decorate(store.Get((Guid)node).Path + "/" + name, ref info);
        return status;
    }
}
