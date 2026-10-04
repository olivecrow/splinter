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
        "windows-vss-system" => new VssSnapshotProvider(directory, owner),
        _ => throw new WorkspaceException("SnapshotProviderUnsupported", "Retain the unsupported immutable basis.")
    };
    public static Task<SnapshotReadAccess> ResolveAsync(SnapshotReference snapshot, string directory, string owner, string? consumed, CancellationToken ct) =>
        snapshot.Provider switch
        {
            GitObjectSnapshotProvider.ProviderName => new GitObjectSnapshotProvider(directory, owner).ResolveReadAccessAsync(snapshot, ct),
            "windows-vss-system" => new VssSnapshotProvider(directory, owner).ResolveReadAccessAsync(snapshot, consumed, ct),
            _ => throw new WorkspaceException("SnapshotProviderUnsupported", "Retain the unsupported immutable basis.")
        };
    public static IDisposable AcquireReadLease(SnapshotReference snapshot, string directory, string owner) => snapshot.Provider switch
    {
        GitObjectSnapshotProvider.ProviderName => new GitObjectSnapshotProvider(directory, owner).AcquireReadLease(),
        "windows-vss-system" => new VssSnapshotProvider(directory, owner).AcquireReadLease(),
        _ => throw new WorkspaceException("SnapshotProviderUnsupported", "Retain the unsupported immutable basis.")
    };
    public static DisposableSnapshotTree OpenTree(SnapshotReference snapshot, SnapshotReadAccess access, string directory, string owner,
        Func<SnapshotObservation>? health = null) => new(snapshot.Provider switch
        {
            GitObjectSnapshotProvider.ProviderName => new GitObjectSnapshotProvider(directory, owner).OpenTree(snapshot, access, health),
            "windows-vss-system" => new VssSnapshotTree(snapshot, access, health),
            _ => throw new WorkspaceException("SnapshotProviderUnsupported", "Retain the unsupported immutable basis.")
        });
    public sealed class DisposableSnapshotTree(ISnapshotTree tree) : ISnapshotTree, IDisposable
    {
        public SnapshotReference Snapshot => tree.Snapshot;
        public void EnsureAvailable() => tree.EnsureAvailable();
        public SnapshotEntry? Stat(string path) => tree.Stat(path);
        public IReadOnlyList<SnapshotEntry> List(string path) => tree.List(path);
        public int Read(string path, long offset, Span<byte> destination) => tree.Read(path, offset, destination);
        public void Dispose() => (tree as IDisposable)?.Dispose();
    }
}
