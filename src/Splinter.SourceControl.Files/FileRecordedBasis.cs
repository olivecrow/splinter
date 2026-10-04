using System.Security.Cryptography;
using Splinter.Core;
using Splinter.Runtime;

namespace Splinter.SourceControl.Files;

public sealed partial class FileWorkspaceProvider
{
    private (PlasticRevisionSnapshotTree Tree, IDisposable Lease) RecordedTree(WorkspaceRecord w, string control)
    {
        var basis = w.Snapshot?.Reference ?? throw Error("A fixed recorded basis is required.");
        var provider = new PlasticRevisionSnapshotProvider(Path.Combine(control, "snapshot"), w.Id, client: revisionClient);
        var access = provider.ResolveReadAccessAsync(basis).GetAwaiter().GetResult();
        var lease = provider.AcquireReadLease();
        try { return (provider.OpenTree(basis, access), lease); }
        catch { lease.Dispose(); throw; }
    }
    private FileSourceManifest RecordedManifest(WorkspaceRecord w, string control, CancellationToken ct)
    {
        var owned = RecordedTree(w, control); using var lease = owned.Lease; using var tree = owned.Tree;
        var entries = new List<FileSourceEntry>(); var pending = new Stack<string>(); pending.Push(""); byte[] scratch = new byte[65536];
        while (pending.TryPop(out var directory))
            foreach (var entry in tree.List(directory))
            {
                ct.ThrowIfCancellationRequested(); if (Managed(entry.Path, w.ManagedPaths)) continue;
                string? hash = null;
                if (entry.Directory) pending.Push(entry.Path);
                else
                {
                    using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                    for (long position = 0; position < entry.Length;)
                    {
                        int n = tree.Read(entry.Path, position, scratch.AsSpan(0, (int)Math.Min(scratch.Length, entry.Length - position)));
                        if (n == 0) throw Error("The fixed recorded payload ended early."); sha.AppendData(scratch.AsSpan(0, n)); position += n;
                    }
                    hash = Convert.ToHexStringLower(sha.GetHashAndReset());
                }
                entries.Add(new(entry.Path, entry.Directory ? "directory" : "file", entry.Length, hash,
                    DateTime.FromFileTimeUtc(entry.LastWriteTime).Ticks, (int)entry.Attributes));
            }
        return Manifest(w, entries.OrderBy(e => e.Path, StringComparer.Ordinal).ToArray());
    }
    private void PreserveRecordedPayloads(WorkspaceRecord w, string control, FileSourceManifest manifest, CancellationToken ct)
    {
        var owned = RecordedTree(w, control); using var lease = owned.Lease; using var tree = owned.Tree; byte[] scratch = new byte[65536];
        foreach (var entry in manifest.Entries.Where(e => e.Kind == "file"))
        {
            ct.ThrowIfCancellationRequested(); string objectPath = FilePath(control, "objects/" + entry.Hash);
            WorkspaceStore.EnsurePlainPath(objectPath);
            if (Directory.Exists(objectPath)) throw Error("The content object has an unexpected type.");
            if (File.Exists(objectPath)) { VerifyObject(objectPath, entry, ct); continue; }
            Directory.CreateDirectory(Path.GetDirectoryName(objectPath)!); string pending = objectPath + ".pending"; WorkspaceStore.EnsurePlainPath(pending);
            if (!File.Exists(pending))
            {
                using var output = new FileStream(pending, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                for (long position = 0; position < entry.Length;)
                {
                    ct.ThrowIfCancellationRequested(); int n = tree.Read(entry.Path, position, scratch.AsSpan(0, (int)Math.Min(scratch.Length, entry.Length - position)));
                    if (n == 0) throw Error("The fixed payload ended early during explicit preservation."); output.Write(scratch, 0, n); position += n;
                }
                output.Flush(true);
            }
            VerifyObject(pending, entry, ct); File.Move(pending, objectPath);
        }
    }
}
