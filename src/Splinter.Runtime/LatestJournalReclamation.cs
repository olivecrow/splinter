using System.Security.Cryptography;
using Splinter.Core;

namespace Splinter.Runtime;

// Reclaim exact files from a sealed current generation. The durable inventory
// makes deletion of several owned objects resumable without sweeping a folder.
public static class LatestJournalReclamation
{
    private sealed record Plan(int Version, string Root, SnapshotDeltaSeal Seal, string Objects, Dictionary<string, string> Files, bool WholeDirectory);
    public static bool IsLatest(string path) => LatestJournalStorage.IsLatest(path);
    public static bool HasIntent(string receipt) => File.Exists(receipt);
    private static string Hash(string path)
    {
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        return Convert.ToHexStringLower(SHA256.HashData(file));
    }
    private static Plan Read(string receipt) => LatestJournalStorage.Decode<Plan>(File.ReadAllBytes(receipt), "SJM1", int.MaxValue);
    public static bool MatchesPendingGeneration(string root, SnapshotDeltaSeal seal, string receipt)
    {
        if (!File.Exists(receipt)) return false;
        Validate(Read(receipt), root, seal, false); return true;
    }
    public static void ReclaimDirectory(string directory, SnapshotDeltaSeal seal, string[] metadataNames, string receipt, Action<string>? boundary = null) =>
        Reclaim(Path.Combine(directory, "delta.journal"), seal, metadataNames, receipt, true, boundary);
    internal static void ReclaimGeneration(string root, SnapshotDeltaSeal seal, string receipt, Action<string>? boundary = null) =>
        Reclaim(root, seal, [], receipt, false, boundary);
    private static void Reclaim(string root, SnapshotDeltaSeal seal, string[] metadataNames, string receipt, bool wholeDirectory, Action<string>? boundary)
    {
        root = Path.GetFullPath(root); WorkspaceStore.EnsurePlainPath(root); WorkspaceStore.EnsurePlainPath(receipt);
        string directory = Path.GetDirectoryName(root)!;
        Plan? plan = File.Exists(receipt) ? Read(receipt) : null;
        if (!Directory.Exists(directory))
        {
            if (plan is null) throw new WorkspaceException("ViewReleaseUncertain", "A missing owned directory has no reclamation inventory.");
            Validate(plan, root, seal, wholeDirectory); return;
        }
        var fences = new List<FileStream>();
        try
        {
            if (File.Exists(root)) fences.Add(new(root, FileMode.Open, FileAccess.ReadWrite, FileShare.Read | FileShare.Delete));
            if (plan is null)
            {
                if (!File.Exists(root) || JournalDeltaStore.SealJournal(root, seal.DurableSequence) != seal)
                    throw new WorkspaceException("ViewSourceChanged", "The current generation differs from its retained seal.");
                var current = LatestJournalStorage.ReadRoot(root);
                var names = LatestJournalStorage.Files(root).ToList();
                if (wholeDirectory)
                {
                    foreach (string entry in Directory.EnumerateFileSystemEntries(directory))
                    {
                        WorkspaceStore.EnsurePlainPath(entry);
                        string name = Path.GetFileName(entry);
                        if (Directory.Exists(entry))
                        {
                            if (name != current.Objects) throw new WorkspaceException("ViewReleaseUncertain", "An unowned journal directory remains.");
                        }
                        else if (entry != root)
                        {
                            if (!metadataNames.Contains(name) && name != "delta.journal.writer.lock")
                                throw new WorkspaceException("ViewReleaseUncertain", "Unowned journal metadata remains.");
                            if (name.EndsWith(".writer.lock", StringComparison.Ordinal) && new FileInfo(entry).Length != 0)
                                throw new WorkspaceException("ViewReleaseUncertain", "The writer guard is not its owned empty file.");
                            names.Add(entry);
                        }
                    }
                }
                foreach (string file in names.Where(p => p != root)) fences.Add(new(file, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete));
                plan = new(1, root, seal, current.Objects, names.ToDictionary(p => Path.GetRelativePath(directory, p), Hash, StringComparer.Ordinal), wholeDirectory);
                byte[] bytes = LatestJournalStorage.Encode(plan, "SJM1");
                using (var file = new FileStream(receipt, FileMode.CreateNew, FileAccess.Write, FileShare.Read)) { file.Write(bytes); file.Flush(true); }
            }
            Validate(plan, root, seal, wholeDirectory);
            foreach (string name in plan.Files.Keys)
            {
                string file = Path.Combine(directory, name);
                if (File.Exists(file) && !fences.Any(f => WorkspaceStore.SamePath(f.Name, file)))
                    fences.Add(new(file, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete));
            }
            Validate(plan, root, seal, wholeDirectory);
            boundary?.Invoke("intent");
            string objects = Path.Combine(directory, plan.Objects);
            foreach (var pair in plan.Files.Where(p => p.Key.StartsWith(plan.Objects + Path.DirectorySeparatorChar, StringComparison.Ordinal)))
            {
                string file = Path.Combine(directory, pair.Key);
                if (File.Exists(file)) { if (Hash(file) != pair.Value) throw LatestJournalStorage.Corrupt("A reclaimed object changed."); File.Delete(file); }
                boundary?.Invoke("object-removed");
            }
            if (Directory.Exists(objects)) Directory.Delete(objects, false);
            if (File.Exists(root)) File.Delete(root);
            boundary?.Invoke("journal-removed");
            if (wholeDirectory)
            {
                foreach (var pair in plan.Files.Where(p => !p.Key.Contains(Path.DirectorySeparatorChar) && p.Key != Path.GetFileName(root)))
                {
                    string file = Path.Combine(directory, pair.Key);
                    if (File.Exists(file)) { if (Hash(file) != pair.Value) throw LatestJournalStorage.Corrupt("Reclamation metadata changed."); File.Delete(file); }
                }
                Directory.Delete(directory, false);
            }
        }
        finally { foreach (var fence in fences) fence.Dispose(); }
    }
    private static void Validate(Plan plan, string root, SnapshotDeltaSeal seal, bool wholeDirectory)
    {
        if (plan.Version != 1 || !WorkspaceStore.SamePath(plan.Root, root) || plan.Seal != seal || plan.WholeDirectory != wholeDirectory ||
            plan.Objects is not { Length: 39 } || !plan.Objects.StartsWith("latest-", StringComparison.Ordinal) ||
            !Guid.TryParseExact(plan.Objects[7..], "N", out _) || plan.Files is null || !plan.Files.ContainsKey(Path.GetFileName(root)))
            throw new WorkspaceException("ViewReceiptMismatch", "Only the same sealed generation can resume reclamation.");
        string directory = Path.GetDirectoryName(root)!;
        foreach (string name in plan.Files.Keys)
        {
            if (Path.IsPathRooted(name) || name.Split(Path.DirectorySeparatorChar).Any(p => p is "" or "." or "..") ||
                name.Contains(Path.AltDirectorySeparatorChar) && Path.AltDirectorySeparatorChar != Path.DirectorySeparatorChar ||
                name.Contains(Path.DirectorySeparatorChar) && !name.StartsWith(plan.Objects + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                throw new WorkspaceException("ViewReceiptMismatch", "A reclamation inventory path is outside its generation.");
        }
        string objects = Path.Combine(directory, plan.Objects); WorkspaceStore.EnsurePlainPath(objects);
        var remaining = new List<string>();
        if (Directory.Exists(objects))
        {
            foreach (string entry in Directory.EnumerateFileSystemEntries(objects))
            { WorkspaceStore.EnsurePlainPath(entry); if (!File.Exists(entry)) throw LatestJournalStorage.Corrupt("A reclamation object is no longer a file."); remaining.Add(entry); }
        }
        if (wholeDirectory && Directory.Exists(directory))
        {
            foreach (string entry in Directory.EnumerateFileSystemEntries(directory))
            {
                WorkspaceStore.EnsurePlainPath(entry);
                if (entry == objects && Directory.Exists(entry)) continue;
                if (!File.Exists(entry)) throw LatestJournalStorage.Corrupt("An unowned directory appeared during reclamation.");
                remaining.Add(entry);
            }
        }
        else if (File.Exists(root)) remaining.Add(root);
        foreach (string file in remaining)
        {
            string relative = Path.GetRelativePath(directory, file);
            if (!plan.Files.TryGetValue(relative, out string? hash) || Hash(file) != hash)
                throw new WorkspaceException("ViewReceiptMismatch", "Changed or unowned files are preserved during reclamation.");
        }
    }
}
