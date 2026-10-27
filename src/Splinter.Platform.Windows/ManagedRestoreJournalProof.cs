using Splinter.Core;
using Splinter.Runtime;

namespace Splinter.Platform.Windows;

// Replay only namespace/metadata against the fixed manifest after its payload
// root may have moved. The stopped journal stays pinned until its own reclaim
// inventory takes over; this tree can never supply unproved payload bytes.
internal static class ManagedRestoreJournalProof
{
    internal static JournalDeltaStore Open(OverlayServeRequest request, OwnedDirectoryManifest manifest)
        => JournalDeltaStore.OpenReadOnly(request.DeltaPath, request.Identity, new MetadataTree(manifest));

    private sealed class MetadataTree : ISnapshotTree
    {
        private readonly Dictionary<string, SnapshotEntry> entries;
        private readonly Dictionary<string, SnapshotEntry[]> children;
        public SnapshotReference Snapshot { get; }
        internal MetadataTree(OwnedDirectoryManifest manifest)
        {
            Snapshot = manifest.Snapshot;
            entries = manifest.Items.ToDictionary(i => i.Entry.Path, i => i.Entry, StringComparer.OrdinalIgnoreCase);
            children = manifest.Items.Where(i => i.Entry.Path.Length != 0).GroupBy(i => Parent(i.Entry.Path), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Select(i => i.Entry).ToArray(), StringComparer.OrdinalIgnoreCase);
        }
        private static string Parent(string path) { int slash = path.LastIndexOf('/'); return slash < 0 ? "" : path[..slash]; }
        public void EnsureAvailable() { }
        public SnapshotEntry? Stat(string path) => entries.GetValueOrDefault(JournalDeltaStore.Normalize(path));
        public IReadOnlyList<SnapshotEntry> List(string path) => children.GetValueOrDefault(JournalDeltaStore.Normalize(path)) ?? [];
        public int Read(string path, long offset, Span<byte> destination)
            => throw new WorkspaceException("GeneratedRestoreConflict", "A metadata-only restoration proof cannot supply payload bytes.");
    }
}
