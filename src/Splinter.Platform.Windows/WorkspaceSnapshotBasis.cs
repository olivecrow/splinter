using Splinter.Core;
using Splinter.SourceControl.Git;
using Splinter.SourceControl.Files;

namespace Splinter.Platform.Windows;

// Ordinary-user composition of immutable readers. Unknown providers cannot
// fall back to the source filesystem or request a replacement VSS capture.
public static class WorkspaceSnapshotBasis
{
    public static ISourceSnapshotProvider Provider(SnapshotReference snapshot, string directory, string owner) => snapshot.Provider switch
    {
        GitObjectSnapshotProvider.ProviderName => new GitObjectSnapshotProvider(directory, owner),
        PlasticRevisionSnapshotProvider.ProviderName => new PlasticRevisionSnapshotProvider(directory, owner),
        OwnedDirectorySnapshotTree.ProviderName => ProjectDirectorySnapshotProvider.Present(directory) ? new ProjectDirectorySnapshotProvider(directory, owner) : new OwnedDirectorySnapshotProvider(directory, owner),
        "windows-vss-system" => new VssSnapshotProvider(directory, owner),
        _ => throw new WorkspaceException("SnapshotProviderUnsupported", "Retain the unsupported immutable basis.")
    };
    public static Task<SnapshotReadAccess> ResolveAsync(SnapshotReference snapshot, string directory, string owner, string? consumed, CancellationToken ct) =>
        snapshot.Provider switch
        {
            GitObjectSnapshotProvider.ProviderName => new GitObjectSnapshotProvider(directory, owner).ResolveReadAccessAsync(snapshot, ct),
            PlasticRevisionSnapshotProvider.ProviderName => new PlasticRevisionSnapshotProvider(directory, owner).ResolveReadAccessAsync(snapshot, ct),
            OwnedDirectorySnapshotTree.ProviderName => ProjectDirectorySnapshotProvider.Present(directory) ? new ProjectDirectorySnapshotProvider(directory, owner).ResolveReadAccessAsync(snapshot, ct) : new OwnedDirectorySnapshotProvider(directory, owner).ResolveReadAccessAsync(snapshot, ct),
            "windows-vss-system" => new VssSnapshotProvider(directory, owner).ResolveReadAccessAsync(snapshot, consumed, ct),
            _ => throw new WorkspaceException("SnapshotProviderUnsupported", "Retain the unsupported immutable basis.")
        };
    public static IDisposable AcquireReadLease(SnapshotReference snapshot, string directory, string owner) => snapshot.Provider switch
    {
        GitObjectSnapshotProvider.ProviderName => new GitObjectSnapshotProvider(directory, owner).AcquireReadLease(),
        PlasticRevisionSnapshotProvider.ProviderName => new PlasticRevisionSnapshotProvider(directory, owner).AcquireReadLease(),
        OwnedDirectorySnapshotTree.ProviderName => ProjectDirectorySnapshotProvider.Present(directory) ? new ProjectDirectorySnapshotProvider(directory, owner).AcquireReadLease(snapshot) : new OwnedDirectorySnapshotProvider(directory, owner).AcquireReadLease(),
        "windows-vss-system" => new VssSnapshotProvider(directory, owner).AcquireReadLease(),
        _ => throw new WorkspaceException("SnapshotProviderUnsupported", "Retain the unsupported immutable basis.")
    };
    public static DisposableSnapshotTree OpenTree(SnapshotReference snapshot, SnapshotReadAccess access, string directory, string owner,
        Func<SnapshotObservation>? health = null)
    {
        WindowsSnapshotPolicy.Require(snapshot);
        return new(snapshot.Provider switch
        {
            GitObjectSnapshotProvider.ProviderName => new GitObjectSnapshotProvider(directory, owner).OpenTree(snapshot, access, health),
            PlasticRevisionSnapshotProvider.ProviderName => new PlasticRevisionSnapshotProvider(directory, owner).OpenTree(snapshot, access),
            OwnedDirectorySnapshotTree.ProviderName => new OwnedDirectorySnapshotTree((ProjectDirectorySnapshotProvider.Present(directory) ? new ProjectDirectorySnapshotProvider(directory, owner).Shared(snapshot) : new OwnedDirectorySnapshotProvider(directory, owner)).Read(snapshot), snapshot),
            "windows-vss-system" => new VssSnapshotTree(snapshot, access, health),
            _ => throw new WorkspaceException("SnapshotProviderUnsupported", "Retain the unsupported immutable basis.")
        });
    }
    public sealed class DisposableSnapshotTree(ISnapshotTree tree) : ISnapshotTree, ISnapshotContentSummaryProvider, IDisposable
    {
        public SnapshotReference Snapshot => tree.Snapshot;
        public SnapshotGraft? Graft => tree.Graft;
        public bool SupportsConcurrentReads => tree.SupportsConcurrentReads;
        public void EnsureAvailable() => tree.EnsureAvailable();
        public SnapshotEntry? Stat(string path) => tree.Stat(path);
        public IReadOnlyList<SnapshotEntry> List(string path) => tree.List(path);
        public int Read(string path, long offset, Span<byte> destination) => tree.Read(path, offset, destination);
        public SnapshotContentSummary? GetVerifiedContentSummary(string path) =>
            tree is ISnapshotContentSummaryProvider provider ? provider.GetVerifiedContentSummary(path) : null;
        public void Dispose() => (tree as IDisposable)?.Dispose();
    }
}
