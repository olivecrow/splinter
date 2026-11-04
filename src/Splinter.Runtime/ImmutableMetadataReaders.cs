namespace Splinter.Runtime;

public sealed partial class ImmutableReadProofs
{
    // Reuse immutable page readers, never a mutable ownership/catalog header.
    // Callers still load the current root under their ordinary owner lock.
    // Identity/location fences and the store reader lease exclude replacement
    // and reclamation; peer writers may continue to append new containers.
    private readonly Dictionary<string, MetadataReader> metadata = new(StringComparer.Ordinal);
    private IJournalFilePins MetadataPins => pins;
    internal static void EnsurePlainPath(string path, ImmutableReadProofs? proofs)
    {
        if (proofs is not null)
        {
            lock (proofs.gate)
            {
                ObjectDisposedException.ThrowIf(proofs.disposed, proofs);
                foreach (var reader in proofs.metadata.Values)
                    if (reader.Paths?.TryEnsurePlainPath(path) == true) return;
            }
        }
        WorkspaceStore.EnsurePlainPath(path);
    }
    internal static MetadataLease OpenMetadata(string directory, ImmutableReadProofs? proofs)
    {
        if (proofs is null) return new(new PagedMetadataStore(directory), null);
        string path = Path.GetFullPath(directory);
        lock (proofs.gate)
        {
            ObjectDisposedException.ThrowIf(proofs.disposed, proofs);
            if (proofs.metadata.TryGetValue(path, out var existing)) return new(existing.Pages, proofs);
            // This is a request budget, not a limit on accepted input. Overflow
            // uses the ordinary short-lived reader and full validation.
            if (proofs.metadata.Count >= 4) return new(new PagedMetadataStore(path), null);
            var reader = new MetadataReader(path, proofs.MetadataPins);
            proofs.metadata.Add(path, reader); return new(reader.Pages, proofs);
        }
    }
    internal sealed class MetadataLease(PagedMetadataStore pages, ImmutableReadProofs? owner) : IDisposable
    {
        internal PagedMetadataStore Pages { get; } = pages;
        public void Dispose() { if (owner is null) Pages.Dispose(); }
    }
    private sealed class MetadataReader : IDisposable
    {
        private readonly FileStream identity;
        private readonly FileStream? locationHeader;
        internal IRequestPathGuard? Paths { get; }
        internal PagedMetadataStore Pages { get; }
        internal MetadataReader(string directory, IJournalFilePins pins)
        {
            WorkspaceStore.EnsurePlainPath(directory);
            using var admission = PagedMetadataStore.AcquireReaderAdmission(directory);
            identity = new FileStream(Path.Combine(directory, "identity.bin"), FileMode.Open, FileAccess.Read, FileShare.Read);
            try
            {
                Paths = pins.PinMetadataPaths(directory);
                string locations = Path.Combine(directory, "locations.json");
                if (File.Exists(locations)) locationHeader = new FileStream(locations, FileMode.Open, FileAccess.Read, FileShare.Read);
                Pages = new(directory, filePins: pins, pathGuard: Paths);
            }
            catch { Paths?.Dispose(); locationHeader?.Dispose(); identity.Dispose(); throw; }
        }
        public void Dispose()
        {
            try { try { Paths?.Dispose(); } finally { try { locationHeader?.Dispose(); } finally { identity.Dispose(); } } }
            finally { Pages.Dispose(); }
        }
    }
}
