using Splinter.Core;

namespace Splinter.Runtime;

// A separate receipt namespace prevents an interrupted deletion from being
// reinterpreted after its identifying journal files have already disappeared.
public static class CurrentJournalReclamation
{
    public const string GuardName = BoundedJournalReclamation.GuardName;
    public static bool IsManagedJournal(string path) => JournalDeltaStore.IsSegmentedJournal(path) || JournalDeltaStore.IsBoundedJournal(path);
    public static bool HasIntent(string receipt) => BoundedJournalReclamation.HasIntent(receipt) || SegmentedJournalReclamation.HasIntent(receipt + ".segmented");
    public static void ReclaimDirectory(string directory, SnapshotDeltaSeal seal, string[] metadataNames, string receipt,
        Action<string>? boundary = null, Func<string, IJournalDirectoryGuard>? directoryGuardFactory = null, IJournalFilePins? filePins = null)
    {
        bool old = BoundedJournalReclamation.HasIntent(receipt);
        bool segmented = SegmentedJournalReclamation.HasIntent(receipt + ".segmented");
        string journal = Path.Combine(directory, "delta.journal");
        bool current = File.Exists(journal) && JournalDeltaStore.IsSegmentedJournal(journal);
        if (old && (segmented || current)) throw new WorkspaceException("ViewReceiptMismatch", "Conflicting journal reclamation receipts must be preserved.");
        if (segmented || current)
            SegmentedJournalReclamation.ReclaimDirectory(directory, seal, metadataNames, receipt + ".segmented", boundary, directoryGuardFactory, filePins);
        else BoundedJournalReclamation.ReclaimDirectory(directory, seal, metadataNames, receipt, boundary, directoryGuardFactory, filePins);
    }
}
