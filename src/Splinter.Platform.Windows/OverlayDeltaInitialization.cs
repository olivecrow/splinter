using Splinter.Core;
using Splinter.Runtime;

namespace Splinter.Platform.Windows;

// A fresh journal needs only the fixed root metadata. The provider, not this
// short-lived client preparation, owns complete payload verification/fencing
// before publishing a mount. This tree cannot be used for payload access.
internal static class OverlayDeltaInitialization
{
    internal static void Create(OverlayServeRequest request, IJournalMutationObserver? observer = null)
    {
        WindowsSnapshotPolicy.Require(request);
        WinFspOverlayClient.ValidateCacheSnapshot(request);
        if (request.SourceView is not null)
            throw new WorkspaceException("OverlayInitializationUnsupported", "Source views retain their own initialization lifecycle.");
        using var source = RootProof.Open(request.Identity.Snapshot, request.ReadAccess,
            request.SnapshotReceiptDirectory, request.SnapshotOwnerId);
        using var generated = request.Identity.Graft is { } graft
            ? RootProof.Open(graft.Basis.Snapshot, request.GeneratedSnapshot!.ReadAccess,
                graft.Basis.ReceiptDirectory, graft.Basis.OwnerId) : null;
        // Preserve the common graft admission before any delta is created:
        // exact policy/path and no source entry shadowed by generated data.
        if (request.Identity.Graft is { } admitted)
            _ = new GraftedSnapshotTree(source, generated!, admitted);
        var tree = new InitializationRoot(request.Identity, source, generated);
        using var delta = observer is null
            ? new JournalDeltaStore(request.DeltaPath, request.Identity, tree,
                boundedLogBytes: JournalDeltaStore.DefaultBoundedLogBytes)
            : new JournalDeltaStore(request.DeltaPath, request.Identity, tree, observer,
                boundedLogBytes: JournalDeltaStore.DefaultBoundedLogBytes);
    }

    private sealed class InitializationRoot(OverlayIdentity identity, RootProof source, RootProof? generated) : ISnapshotTree
    {
        public SnapshotReference Snapshot => identity.Snapshot;
        public SnapshotGraft? Graft => identity.Graft;
        public void EnsureAvailable() { source.EnsureAvailable(); generated?.EnsureAvailable(); }
        public SnapshotEntry? Stat(string path)
        {
            EnsureAvailable();
            return path.Length == 0 ? source.Root : throw Unsupported();
        }
        public IReadOnlyList<SnapshotEntry> List(string path) => throw Unsupported();
        public int Read(string path, long offset, Span<byte> destination) => throw Unsupported();
        private static WorkspaceException Unsupported() => new("OverlayInitializationUnsupported",
            "The initialization proof admits root metadata only; open a fully verified provider reader for other access.");
    }

    private sealed class RootProof : ISnapshotTree, IDisposable
    {
        private readonly List<IDisposable> resources = [];
        private Action? available;
        private Func<string, SnapshotEntry?>? stat;
        public SnapshotReference Snapshot { get; private set; } = null!;
        internal SnapshotEntry Root { get; private set; } = null!;

        internal static RootProof Open(SnapshotReference snapshot, SnapshotReadAccess access, string directory, string owner)
        {
            access.RequireMatch(snapshot);
            var result = new RootProof { Snapshot = snapshot };
            try
            {
                if (snapshot.Provider == OwnedDirectorySnapshotTree.ProviderName)
                {
                    var provider = ProjectDirectorySnapshotProvider.Present(directory)
                        ? new ProjectDirectorySnapshotProvider(directory, owner).Shared(snapshot)
                        : new OwnedDirectorySnapshotProvider(directory, owner);
                    result.resources.Add(provider.AcquireReadLease());
                    var observation = provider.ObserveAsync(snapshot).GetAwaiter().GetResult();
                    if (observation.Available != true || observation.ReadAccess != access)
                        throw new WorkspaceException("SnapshotIdentityMismatch", "Initialization requires the exact owned manifest/read proof.");
                    var manifest = provider.Read(snapshot);
                    if (manifest.Version != 1 || !snapshot.Persistent || !snapshot.Differential ||
                        snapshot.Consistency != "stopped-original-owned-directory" || manifest.Items.Length > JournalDeltaStore.MaxIndexedNodes ||
                        manifest.Items.Count(item => item.Entry.Path.Length == 0) != 1)
                        throw new WorkspaceException("GeneratedBasisCorrupt", "Initialization requires a supported manifest with one root.");
                    result.Root = manifest.Items.Single(item => item.Entry.Path.Length == 0).Entry;
                    if (!result.Root.Directory || result.Root.Length != 0 || (result.Root.Attributes & 16) == 0)
                        throw new WorkspaceException("GeneratedBasisCorrupt", "The fixed manifest root must be a directory.");
                    SnapshotInputChecks.RejectUnsupportedAttributes(result.Root.Attributes);
                    var items = manifest.Items.ToDictionary(item => item.Entry.Path, StringComparer.OrdinalIgnoreCase);
                    result.stat = path => items.GetValueOrDefault(JournalDeltaStore.Normalize(path))?.Entry;
                    var pin = new WindowsOwnedDirectoryLease(snapshot.RootPath);
                    result.resources.Add(pin);
                    result.available = pin.EnsureAvailable;
                }
                else
                {
                    result.resources.Add(WorkspaceSnapshotBasis.AcquireReadLease(snapshot, directory, owner));
                    var tree = WorkspaceSnapshotBasis.OpenTree(snapshot, access, directory, owner);
                    result.resources.Add(tree);
                    result.Root = tree.Stat("") ?? throw new WorkspaceException("SnapshotMissing", "The fixed source root is unavailable.");
                    result.stat = tree.Stat;
                    result.available = tree.EnsureAvailable;
                }
                result.EnsureAvailable();
                return result;
            }
            catch { result.Dispose(); throw; }
        }

        public void EnsureAvailable()
        {
            ObjectDisposedException.ThrowIf(available is null, this);
            available();
        }
        public SnapshotEntry? Stat(string path) { EnsureAvailable(); return stat!(path); }
        public IReadOnlyList<SnapshotEntry> List(string path) => throw new NotSupportedException("Initialization admission does not enumerate a live basis.");
        public int Read(string path, long offset, Span<byte> destination) => throw new NotSupportedException("Initialization admission never reads payload.");
        public void Dispose()
        {
            available = null; stat = null;
            for (int i = resources.Count - 1; i >= 0; i--) resources[i].Dispose();
            resources.Clear();
        }
    }
}
