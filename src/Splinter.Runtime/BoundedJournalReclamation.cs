using System.Security.Cryptography;
using Splinter.Core;

namespace Splinter.Runtime;

// A stopped bounded writer owns exactly three files. The external receipt is
// durable before any unlink and remains usable after the mode frame is gone.
public static class BoundedJournalReclamation
{
    public const string GuardName = "delta.journal.bounded.lock";
    private static readonly string[] Names = ["delta.journal", "identity.json", GuardName];
    private sealed record FileProof(long Length, string Hash);
    private sealed record Plan(int Version, string Directory, SnapshotDeltaSeal Seal, Dictionary<string, FileProof> Files);
    private static WorkspaceException Conflict(string message) => new("ViewReceiptMismatch", message + " Preserve bounded journal storage.");
    public static bool HasIntent(string receipt) => File.Exists(receipt) || File.Exists(receipt + ".pending");

    public static void ReclaimDirectory(string directory, SnapshotDeltaSeal seal, string[] metadataNames, string receipt,
        Action<string>? boundary = null, Func<string, IJournalDirectoryGuard>? directoryGuardFactory = null)
    {
        directory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory)); receipt = Path.GetFullPath(receipt);
        if (metadataNames.Length != 1 || metadataNames[0] != "identity.json")
            throw new ArgumentException("Bounded reclamation requires exactly identity.json metadata.", nameof(metadataNames));
        if (WorkspaceStore.SamePath(Path.GetDirectoryName(receipt)!, directory) ||
            receipt.StartsWith(directory + Path.DirectorySeparatorChar, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            throw Conflict("The reclamation receipt must remain outside the owned directory.");
        WorkspaceStore.EnsurePlainPath(directory); WorkspaceStore.EnsurePlainPath(receipt); WorkspaceStore.EnsurePlainPath(receipt + ".pending");
        if (File.Exists(directory)) throw Conflict("A file replaced the owned directory.");
        bool published = File.Exists(receipt), pending = File.Exists(receipt + ".pending");
        if (published && pending) throw Conflict("Two reclamation inventories remain.");
        Plan? plan = published || pending ? Read(published ? receipt : receipt + ".pending") : null;
        if (plan is not null) ValidatePlan(plan, directory, seal);
        if (!Directory.Exists(directory))
        {
            if (plan is null || pending) throw Conflict("An absent directory has no published reclamation inventory.");
            return;
        }

        var fences = new Dictionary<string, FileStream>(StringComparer.Ordinal);
        IJournalDirectoryGuard? guard = null;
        FileStream? receiptFence = null;
        void Fence(string name, FileAccess access)
        {
            string path = Path.Combine(directory, name); WorkspaceStore.EnsurePlainPath(path);
            fences.Add(name, new FileStream(path, FileMode.Open, access, FileShare.Read | FileShare.Delete));
        }
        try
        {
            // Open, never create: a live writer or stopped reader holds this
            // guard without write sharing. Keep it until every unlink finishes.
            if (File.Exists(Path.Combine(directory, GuardName)))
            {
                Fence(GuardName, FileAccess.ReadWrite);
                if (fences[GuardName].Length != 0) throw Conflict("The bounded writer guard is not empty.");
                guard = directoryGuardFactory?.Invoke(directory);
                if (guard is not null && !WorkspaceStore.SamePath(guard.DirectoryPath, directory))
                    throw Conflict("The ancestry proof belongs to another directory.");
            }
            else if (plan is null) throw Conflict("A fresh reclaim requires its existing bounded writer guard.");
            foreach (string name in Names.Where(n => n != GuardName))
                if (File.Exists(Path.Combine(directory, name))) Fence(name, FileAccess.Read);
            if (plan is null)
            {
                RequireInventory(directory, null, guard);
                if (fences.Count != Names.Length || !JournalDeltaStore.IsBoundedJournal(Path.Combine(directory, "delta.journal")) ||
                    JournalDeltaStore.SealJournal(Path.Combine(directory, "delta.journal"), seal.DurableSequence) != seal)
                    throw new WorkspaceException("ViewSourceChanged", "The stopped bounded generation differs from its retained seal.");
                plan = new(1, directory, seal, Names.ToDictionary(n => n,
                    n => n == "delta.journal" ? new FileProof(seal.JournalBytes, seal.JournalHash) : Proof(fences[n]), StringComparer.Ordinal));
                ValidatePlan(plan, directory, seal);
                byte[] bytes = LatestJournalStorage.Encode(plan, "SBR1");
                using (var output = new FileStream(receipt + ".pending", FileMode.CreateNew, FileAccess.Write, FileShare.Read))
                { output.Write(bytes); output.Flush(true); }
                boundary?.Invoke("intent-flushed"); pending = true;
            }
            ValidateRemaining(plan, directory, fences, guard);
            if (pending) File.Move(receipt + ".pending", receipt);
            receiptFence = new FileStream(receipt, FileMode.Open, FileAccess.Read, FileShare.Read);
            ValidatePlan(Read(receipt), directory, seal);
            boundary?.Invoke("intent");
            foreach (string name in Names)
            {
                // The platform proof pins its existing marker without deletion
                // sharing. Hand off only at the last marker, while its Runtime
                // fence still excludes a new writer and remains delete-pending.
                if (name == GuardName) { guard?.Dispose(); guard = null; }
                RequireInventory(directory, plan, guard);
                string path = Path.Combine(directory, name);
                if (File.Exists(path))
                {
                    if (!fences.ContainsKey(name) || PathProof(path) != plan.Files[name])
                        throw Conflict("A file changed before its conditional deletion.");
                    File.Delete(path);
                }
                boundary?.Invoke(name == "delta.journal" ? "journal-removed" : name == GuardName ? "guard-removed" : "metadata-removed");
            }
            WorkspaceStore.EnsurePlainPath(directory);
            if (Directory.EnumerateFileSystemEntries(directory).Any()) throw Conflict("An unowned entry appeared before directory removal.");
            Directory.Delete(directory, false);
            boundary?.Invoke("directory-removed");
        }
        finally
        {
            receiptFence?.Dispose(); guard?.Dispose();
            foreach (var fence in fences.Values) fence.Dispose();
        }
    }

    private static Plan Read(string receipt)
    {
        WorkspaceStore.EnsurePlainPath(receipt);
        if (new FileInfo(receipt).Length is < 1 or > 65536) throw Conflict("The reclamation inventory has invalid bounds.");
        return LatestJournalStorage.Decode<Plan>(File.ReadAllBytes(receipt), "SBR1", 65536);
    }
    private static void ValidatePlan(Plan plan, string directory, SnapshotDeltaSeal seal)
    {
        if (plan.Version != 1 || string.IsNullOrWhiteSpace(plan.Directory) || !WorkspaceStore.SamePath(plan.Directory, directory) || plan.Seal != seal ||
            seal.DurableSequence < 0 || seal.JournalBytes <= 0 ||
            plan.Files is null || plan.Files.Count != Names.Length || Names.Any(n => !plan.Files.ContainsKey(n)) ||
            plan.Files.Values.Any(p => p is null || p.Length < 0 || p.Hash is not { Length: 64 } || p.Hash.Any(c => !Uri.IsHexDigit(c))) ||
            plan.Files["delta.journal"] != new FileProof(seal.JournalBytes, seal.JournalHash) ||
            plan.Files[GuardName] != new FileProof(0, Convert.ToHexStringLower(SHA256.HashData(Array.Empty<byte>()))))
            throw Conflict("Only the same exact sealed bounded inventory may resume reclamation.");
    }
    private static void RequireInventory(string directory, Plan? plan, IJournalDirectoryGuard? guard)
    {
        foreach (string entry in Directory.EnumerateFileSystemEntries(directory))
        {
            if (guard is null) WorkspaceStore.EnsurePlainPath(entry); else guard.EnsureFile(entry);
            string name = Path.GetFileName(entry);
            if (!File.Exists(entry) || !Names.Contains(name, StringComparer.Ordinal) || plan is not null && !plan.Files.ContainsKey(name))
                throw Conflict("An unknown or pending entry remains in the bounded directory.");
        }
    }
    private static void ValidateRemaining(Plan plan, string directory, Dictionary<string, FileStream> fences, IJournalDirectoryGuard? guard)
    {
        RequireInventory(directory, plan, guard);
        foreach (string name in Names)
        {
            string path = Path.Combine(directory, name);
            if (File.Exists(path) && (!fences.ContainsKey(name) || PathProof(path) != plan.Files[name]))
                throw Conflict("Changed or replaced files are preserved during reclamation.");
        }
    }
    private static FileProof PathProof(string path)
    {
        WorkspaceStore.EnsurePlainPath(path);
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        return Proof(file);
    }
    private static FileProof Proof(FileStream file)
    {
        file.Position = 0; long length = file.Length; string hash = Convert.ToHexStringLower(SHA256.HashData(file));
        if (file.Length != length) throw Conflict("A fenced file changed length.");
        return new(length, hash);
    }
}
