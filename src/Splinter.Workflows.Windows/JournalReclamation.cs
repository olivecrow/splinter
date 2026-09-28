using System.Security.Cryptography;
using Splinter.Core;
using Splinter.Runtime;
using static Splinter.Workflows.Windows.ProductState;

namespace Splinter.Workflows.Windows;

internal sealed record JournalReclamationIntent(int Version, string Directory, SnapshotDeltaSeal Seal, Dictionary<string, string> Metadata);

// Windows denies moving a directory containing the open journal fence. Unlink
// only that verified file while holding the fence, then remove verified metadata.
internal static class JournalReclamation
{
    internal static void Reclaim(string directory, SnapshotDeltaSeal seal, string[] metadataNames, string receiptPath, Action<string>? boundary = null)
    {
        WorkspaceStore.EnsurePlainPath(directory);
        Require(!File.Exists(directory), "ValidationReleaseUncertain", "A file replaced the owned artifact directory.");
        var prior = Read<JournalReclamationIntent>(receiptPath);
        Require(prior is null || WorkspaceStore.SamePath(prior.Directory, directory) && prior.Seal == seal &&
            prior.Metadata.Keys.All(metadataNames.Contains), "ValidationReceiptMismatch", "Reclamation must retain its exact directory, seal and metadata.");
        if (!Directory.Exists(directory))
        { Require(prior is not null, "ValidationReleaseUncertain", "Missing artifact has no reclamation intent."); return; }
        string journal = Path.Combine(directory, "delta.journal");
        string[] Files() => Directory.GetFileSystemEntries(directory);
        void CheckFiles(bool allowJournal)
        {
            foreach (string file in Files())
            {
                WorkspaceStore.EnsurePlainPath(file);
                Require(!Directory.Exists(file) && (metadataNames.Contains(Path.GetFileName(file)) || allowJournal && file == journal),
                    "ValidationReleaseUncertain", "Unknown artifact entries remain; preserve them.");
            }
        }
        CheckFiles(allowJournal: true);
        var metadata = Files().Where(p => p != journal).ToDictionary(p => Path.GetFileName(p)!, Hash, StringComparer.Ordinal);
        Require(prior is null || metadata.All(p => prior.Metadata.TryGetValue(p.Key, out var hash) && p.Value == hash),
            "ValidationReceiptMismatch", "Reclamation metadata changed; preserve it.");
        if (File.Exists(journal))
        {
            using var fence = new FileStream(journal, FileMode.Open, FileAccess.Read, FileShare.Delete);
            Require(fence.Length == seal.JournalBytes && Convert.ToHexStringLower(SHA256.HashData(fence)) == seal.JournalHash,
                "ValidationSourceChanged", "The journal changed before its reclamation fence.");
            if (prior is null) Save(receiptPath, new JournalReclamationIntent(1, directory, seal, metadata!));
            boundary?.Invoke("intent");
            File.Delete(journal);
        }
        else Require(prior is not null, "ValidationReleaseUncertain", "Missing journal has no reclamation intent.");
        boundary?.Invoke("journal-removed");
        CheckFiles(allowJournal: false);
        foreach (string file in Files())
        {
            Require(metadata.TryGetValue(Path.GetFileName(file), out var expected) && Hash(file) == expected,
                "ValidationReceiptMismatch", "Metadata changed during reclamation.");
            File.Delete(file);
        }
        Directory.Delete(directory, false);
    }
    private static string Hash(string file)
    {
        using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }
}
