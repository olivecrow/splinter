using Splinter.Core;
using Splinter.Runtime;

namespace Splinter.Platform.Windows;

// Replay only namespace/metadata against the fixed manifest after its payload
// root may have moved. The stopped journal stays pinned until its own reclaim
// inventory takes over; this tree can never supply unproved payload bytes.
internal static class ManagedRestoreJournalProof
{
    internal static JournalDeltaStore Open(OverlayServeRequest request, OwnedDirectoryManifest manifest)
        => JournalDeltaStore.OpenReadOnly(request.DeltaPath, request.Identity, new MetadataTree(manifest), ownsSnapshotTree: true, sourceFilePins: new WindowsJournalFilePins());

    private sealed class MetadataTree : ISnapshotTree, IDisposable
    {
        private readonly OwnedDirectoryItemIndex entries;
        public SnapshotReference Snapshot { get; }
        internal MetadataTree(OwnedDirectoryManifest manifest)
        {
            Snapshot = manifest.Snapshot;
            entries = manifest.OpenIndex(StringComparer.OrdinalIgnoreCase);
        }
        private static string Parent(string path) { int slash = path.LastIndexOf('/'); return slash < 0 ? "" : path[..slash]; }
        public void EnsureAvailable() { }
        public SnapshotEntry? Stat(string path) => entries.GetValueOrDefault(JournalDeltaStore.Normalize(path))?.Entry;
        public IReadOnlyList<SnapshotEntry> List(string path) => EnumerateChildren(path).ToArray();
        public IEnumerable<SnapshotEntry> EnumerateChildren(string path) => entries.EnumerateChildren(JournalDeltaStore.Normalize(path)).Select(item => item.Entry);
        public void Dispose() => entries.Dispose();
        public int Read(string path, long offset, Span<byte> destination)
            => throw new WorkspaceException("GeneratedRestoreConflict", "A metadata-only restoration proof cannot supply payload bytes.");
    }
}
