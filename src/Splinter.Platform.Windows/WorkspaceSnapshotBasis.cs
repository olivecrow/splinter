using Splinter.Core;
using Splinter.SourceControl.Git;

namespace Splinter.Platform.Windows;

// Ordinary-user composition of immutable readers. Unknown providers cannot
// fall back to the source filesystem or request a replacement VSS capture.
public static class WorkspaceSnapshotBasis
{
    public static ISourceSnapshotProvider Provider(SnapshotReference snapshot, string directory, string owner) => snapshot.Provider switch
    {
        GitObjectSnapshotProvider.ProviderName => new GitObjectSnapshotProvider(directory, owner),
        OwnedDirectorySnapshotTree.ProviderName => new OwnedDirectorySnapshotProvider(directory, owner),
        "windows-vss-system" => new VssSnapshotProvider(directory, owner),
        _ => throw new WorkspaceException("SnapshotProviderUnsupported", "Retain the unsupported immutable basis.")
    };
    public static Task<SnapshotReadAccess> ResolveAsync(SnapshotReference snapshot, string directory, string owner, string? consumed, CancellationToken ct) =>
        snapshot.Provider switch
        {
            GitObjectSnapshotProvider.ProviderName => new GitObjectSnapshotProvider(directory, owner).ResolveReadAccessAsync(snapshot, ct),
            OwnedDirectorySnapshotTree.ProviderName => new OwnedDirectorySnapshotProvider(directory, owner).ResolveReadAccessAsync(snapshot, ct),
            "windows-vss-system" => new VssSnapshotProvider(directory, owner).ResolveReadAccessAsync(snapshot, consumed, ct),
            _ => throw new WorkspaceException("SnapshotProviderUnsupported", "Retain the unsupported immutable basis.")
        };
    public static IDisposable AcquireReadLease(SnapshotReference snapshot, string directory, string owner) => snapshot.Provider switch
    {
        GitObjectSnapshotProvider.ProviderName => new GitObjectSnapshotProvider(directory, owner).AcquireReadLease(),
        OwnedDirectorySnapshotTree.ProviderName => new OwnedDirectorySnapshotProvider(directory, owner).AcquireReadLease(),
        "windows-vss-system" => new VssSnapshotProvider(directory, owner).AcquireReadLease(),
        _ => throw new WorkspaceException("SnapshotProviderUnsupported", "Retain the unsupported immutable basis.")
    };
    public static DisposableSnapshotTree OpenTree(SnapshotReference snapshot, SnapshotReadAccess access, string directory, string owner,
        Func<SnapshotObservation>? health = null) => new(snapshot.Provider switch
        {
            GitObjectSnapshotProvider.ProviderName => new GitObjectSnapshotProvider(directory, owner).OpenTree(snapshot, access, health),
            OwnedDirectorySnapshotTree.ProviderName => new OwnedDirectorySnapshotTree(new OwnedDirectorySnapshotProvider(directory, owner).Read(snapshot), snapshot),
            "windows-vss-system" => new VssSnapshotTree(snapshot, access, health),
            _ => throw new WorkspaceException("SnapshotProviderUnsupported", "Retain the unsupported immutable basis.")
        });
    public sealed class DisposableSnapshotTree(ISnapshotTree tree) : ISnapshotTree, IDisposable
    {
        public SnapshotReference Snapshot => tree.Snapshot;
        public SnapshotGraft? Graft => tree.Graft;
        public void EnsureAvailable() => tree.EnsureAvailable();
        public SnapshotEntry? Stat(string path) => tree.Stat(path);
        public IReadOnlyList<SnapshotEntry> List(string path) => tree.List(path);
        public int Read(string path, long offset, Span<byte> destination) => tree.Read(path, offset, destination);
        public void Dispose() => (tree as IDisposable)?.Dispose();
    }
    public sealed class GeneratedTreeScope : ISnapshotTree, IDisposable
    {
        private readonly IDisposable? lease;
        private readonly DisposableSnapshotTree? generated;
        private readonly ISnapshotTree tree;
        public GeneratedTreeScope(ISnapshotTree source, OverlayServeRequest request)
        {
            if (request.Identity.Graft is not { } graft) { tree = source; return; }
            try
            {
                lease = AcquireReadLease(graft.Basis.Snapshot, graft.Basis.ReceiptDirectory, graft.Basis.OwnerId);
                generated = OpenTree(graft.Basis.Snapshot, request.GeneratedSnapshot!.ReadAccess, graft.Basis.ReceiptDirectory, graft.Basis.OwnerId);
                tree = new Splinter.Runtime.GraftedSnapshotTree(source, generated, graft);
            }
            catch { Dispose(); throw; }
        }
        public SnapshotReference Snapshot => tree.Snapshot;
        public SnapshotGraft? Graft => tree.Graft;
        public void EnsureAvailable() => tree.EnsureAvailable();
        public SnapshotEntry? Stat(string path) => tree.Stat(path);
        public IReadOnlyList<SnapshotEntry> List(string path) => tree.List(path);
        public int Read(string path, long offset, Span<byte> destination) => tree.Read(path, offset, destination);
        public void Dispose() { generated?.Dispose(); lease?.Dispose(); }
    }
}
