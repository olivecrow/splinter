using Microsoft.Win32.SafeHandles;
using Splinter.Core;
using Splinter.Runtime;

namespace Splinter.Platform.Windows;

// Original identities partition into disjoint transport units. A renamed
// directory carries unchanged descendants; only exceptions become other units.
internal static class ManagedRestoreTopology
{
    internal sealed record Unit(string BasePath, string? FinalPath, bool Directory);
    internal sealed record Operation(string Kind, int Unit, string Path, Guid EntryId);
    internal sealed record Model(Unit[] Units, Operation[] Operations);
    internal sealed record Member(string Path, OwnedDirectoryItem? Original, DeltaEntry? Added, string Identity, bool Deleted);
    internal static WorkspaceException Conflict() => new("GeneratedRestoreConflict", "Preserve an inconsistent restore topology.");
    internal static string Parent(string path) { int slash = path.LastIndexOf('/'); return slash < 0 ? "" : path[..slash]; }
    internal static int Depth(string path) => path.Length == 0 ? 0 : path.Count(c => c == '/') + 1;
    internal static string At(string root, string relative)
    {
        if (JournalDeltaStore.Normalize(relative) != relative) throw Conflict();
        string path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)); WorkspaceStore.EnsurePlainPath(path); return path;
    }
    internal static string Stage(string root, int unit) => Path.Combine(root, "restore-topology-stage-" + unit.ToString("D8", System.Globalization.CultureInfo.InvariantCulture));

    internal static Model Build(OwnedDirectoryManifest manifest, DeltaRestoreEntry[] entries)
    {
        var originals = manifest.Items.ToDictionary(i => i.Entry.Path, StringComparer.Ordinal);
        var final = new Dictionary<string, DeltaEntry>(StringComparer.Ordinal);
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var ids = new HashSet<Guid>();
        var directories = new HashSet<string>(StringComparer.Ordinal);
        foreach (var delta in entries)
        {
            if (delta?.Entry is null || delta.Ranges is null) throw Conflict();
            var e = delta.Entry;
            if (e.Deleted || e.Length < 0 || e.BaseLength < 0 || e.BaseLength > e.Length || !paths.Add(e.Path) || !ids.Add(e.Id) ||
                JournalDeltaStore.Normalize(e.Path) != e.Path) throw Conflict();
            if (e.BasePath is { } source)
            {
                if (!originals.TryGetValue(source, out var item) || item.Entry.Directory != e.Directory || e.BaseLength > item.Entry.Length || !final.TryAdd(source, e)) throw Conflict();
            }
            else if (e.BaseLength != 0) throw Conflict();
            if (e.Directory)
            {
                if (e.Length != 0 || delta.Ranges.Length != 0) throw Conflict();
                directories.Add(e.Path);
            }
            long end = 0;
            foreach (var range in delta.Ranges)
            {
                if (range is null || range.Offset < end || range.Length <= 0 || range.Offset > e.Length - range.Length) throw Conflict();
                end = range.Offset + range.Length;
            }
        }
        if (!final.TryGetValue("", out var root) || root.Path != "" || !root.Directory ||
            entries.Where(e => e.Entry.Path.Length != 0).Any(e => !directories.Contains(Parent(e.Entry.Path)))) throw Conflict();
        var units = new List<Unit> { new("", "", true) };
        foreach (var item in manifest.Items.Where(i => i.Entry.Path.Length != 0).OrderBy(i => i.Entry.Path, StringComparer.Ordinal))
        {
            string path = item.Entry.Path; var next = final.GetValueOrDefault(path); var parent = final.GetValueOrDefault(Parent(path));
            bool split = next is null ? parent is not null : parent is null || Parent(next.Path) != parent.Path ||
                next.Path[(next.Path.LastIndexOf('/') + 1)..] != path[(path.LastIndexOf('/') + 1)..];
            if (split) units.Add(new(path, next?.Path, item.Entry.Directory));
        }
        var operations = new List<Operation>();
        operations.AddRange(Enumerable.Range(1, units.Count - 1).OrderByDescending(i => Depth(units[i].BasePath)).ThenBy(i => units[i].BasePath, StringComparer.Ordinal)
            .Select(i => new Operation("stage", i, "", Guid.Empty)));
        var installs = Enumerable.Range(1, units.Count - 1).Where(i => units[i].Directory && units[i].FinalPath is not null)
            .Select(i => new Operation("install", i, units[i].FinalPath!, Guid.Empty));
        var creates = entries.Where(e => e.Entry.Directory && e.Entry.BasePath is null)
            .Select(e => new Operation("directory", -1, e.Entry.Path, e.Entry.Id));
        operations.AddRange(installs.Concat(creates).OrderBy(o => Depth(o.Path)).ThenBy(o => o.Path, StringComparer.Ordinal));
        operations.AddRange(Enumerable.Range(1, units.Count - 1).Where(i => !units[i].Directory && units[i].FinalPath is not null)
            .OrderBy(i => units[i].FinalPath, StringComparer.Ordinal).Select(i => new Operation("install", i, units[i].FinalPath!, Guid.Empty)));
        return new(units.ToArray(), operations.ToArray());
    }

    internal static bool Equal(Model left, Model right) => left.Units.SequenceEqual(right.Units) && left.Operations.SequenceEqual(right.Operations);

    // Inventory is a projection of the fixed plan, not a filesystem proof.
    // Validate relative syntax here; the scoped forest/operation proofs verify
    // every actual identity and ordinary path before effects or reclamation.
    private static string Project(string root, string relative)
    {
        if (JournalDeltaStore.Normalize(relative) != relative) throw Conflict();
        return Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
    }

    internal static Dictionary<string, Member> Inventory(Model model, int completed, OwnedDirectoryManifest manifest,
        DeltaRestoreEntry[] entries, Dictionary<string, string> identities, string originalRoot, string receiptRoot,
        IReadOnlyDictionary<Guid, string> newDirectories, IReadOnlyDictionary<Guid, string> newFiles, bool allowReclaimed)
    {
        if (completed < 0 || completed > model.Operations.Length) throw Conflict();
        var units = model.Units.Select(u => Project(originalRoot, u.BasePath)).ToArray();
        var installedNew = new HashSet<Guid>();
        for (int i = 0; i < completed; i++)
        {
            var op = model.Operations[i];
            if (op.Kind == "stage") units[op.Unit] = Stage(receiptRoot, op.Unit);
            else if (op.Kind == "install") units[op.Unit] = Project(originalRoot, op.Path);
            else installedNew.Add(op.EntryId);
        }
        if (!installedNew.SetEquals(newDirectories.Keys)) throw Conflict();
        var unitRoots = model.Units.Select((u, i) => (u.BasePath, Index: i)).ToDictionary(p => p.BasePath, p => p.Index, StringComparer.Ordinal);
        var owners = new Dictionary<string, int>(StringComparer.Ordinal);
        var members = new Dictionary<string, Member>(StringComparer.Ordinal);
        foreach (var item in manifest.Items.OrderBy(i => Depth(i.Entry.Path)).ThenBy(i => i.Entry.Path, StringComparer.Ordinal))
        {
            string relative = item.Entry.Path;
            int owner = unitRoots.TryGetValue(relative, out int own) ? own : owners[Parent(relative)]; owners.Add(relative, owner);
            var unit = model.Units[owner];
            string suffix = relative == unit.BasePath ? "" : unit.BasePath.Length == 0 ? relative : relative[(unit.BasePath.Length + 1)..];
            string actual = Project(units[owner], suffix); bool deleted = unit.FinalPath is null;
            if (allowReclaimed && deleted && !ManagedUnchangedDirectoryRestore.Exists(actual)) continue;
            if (!members.TryAdd(actual, new(actual, item, null, identities[relative], deleted))) throw Conflict();
        }
        foreach (var delta in entries.Where(e => e.Entry.BasePath is null))
        {
            var entry = delta.Entry;
            if (entry.Directory && !installedNew.Contains(entry.Id)) continue;
            var receipts = entry.Directory ? newDirectories : newFiles;
            if (!receipts.TryGetValue(entry.Id, out string? identity))
            { if (entry.Directory) throw Conflict(); continue; }
            string path = Project(originalRoot, entry.Path);
            if (!members.TryAdd(path, new(path, null, entry, identity, false))) throw Conflict();
        }
        return members;
    }

    // A proof is path-bound and exists only after topology settles. Mutable
    // leaves are preflighted by the caller and individually opened exclusively.
    internal sealed class ForestPins : IDisposable
    {
        private readonly Dictionary<string, Member> members;
        private readonly Dictionary<string, SafeFileHandle> directories = new(StringComparer.Ordinal);
        private readonly Dictionary<string, FileStream> files = new(StringComparer.Ordinal);
        private WindowsDirectoryTreeProof? originalProof, receiptProof;
        private SafeFileHandle? receiptHandle;
        internal ForestPins(Dictionary<string, Member> members, string originalRoot, string receiptRoot, SafeFileHandle root,
            IReadOnlySet<string> mutablePaths, bool content, CancellationToken ct, int? hashWorkers = null, Action<string>? beforeHash = null)
        {
            this.members = members;
            int workers = hashWorkers ?? OwnedDirectorySnapshotTree.DefaultWorkers;
            if (workers is < 1 or > 8) throw new ArgumentOutOfRangeException(nameof(hashWorkers));
            try
            {
                var pinnedOriginal = new WindowsDirectoryTreeProof(originalRoot, root); originalProof = pinnedOriginal;
                // Control receipts continue to publish beneath this ordinary
                // parent. Its pinned controls keep it nonempty, while sharing
                // WRITE permits those child renames without sharing DELETE.
                receiptHandle = ManagedRestoreFileAccess.DirectoryHandle(receiptRoot, metadataWrite: false, allowChildMutations: true);
                var pinnedReceipts = new WindowsDirectoryTreeProof(receiptRoot, receiptHandle); receiptProof = pinnedReceipts;
                (WindowsDirectoryTreeProof Proof, string Relative) Resolve(string path)
                {
                    bool underOriginal = WorkspaceStore.SamePath(path, originalRoot) ||
                        path.StartsWith(Path.TrimEndingDirectorySeparator(originalRoot) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
                    string parent = underOriginal ? originalRoot : receiptRoot;
                    if (!WorkspaceStore.SamePath(path, parent) &&
                        !path.StartsWith(Path.TrimEndingDirectorySeparator(parent) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw Conflict();
                    string relative = Path.GetRelativePath(parent, path).Replace('\\', '/');
                    return (underOriginal ? pinnedOriginal : pinnedReceipts, relative == "." ? "" : relative);
                }
                foreach (var member in members.Values.Where(Directory).OrderBy(m => m.Path.Length))
                {
                    ct.ThrowIfCancellationRequested();
                    var location = Resolve(member.Path);
                    var pin = location.Proof.PinDirectory(location.Relative, moving: member.Deleted);
                    directories.Add(member.Path, pin);
                    if (SnapshotInputChecks.FileIdentity(pin, directory: true) != member.Identity) throw Conflict();
                }
                // Freeze both directory maps before workers read them. No
                // shared stream position or dictionary mutation occurs inside
                // the workers; publish ownership only after every worker joins.
                var entries = members.Values.Where(m => !Directory(m) && !mutablePaths.Contains(m.Path)).ToArray();
                var locations = entries.Select(m => Resolve(m.Path).Proof).ToArray();
                var opened = new FileStream?[entries.Length];
                try
                {
                    OwnedDirectorySnapshotTree.ParallelFiles(Enumerable.Range(0, entries.Length), index =>
                    {
                        var member = entries[index];
                        var pin = ManagedRestoreFileAccess.Read(member.Path, moving: member.Deleted, proof: locations[index]);
                        opened[index] = pin; ManagedRestoreFileAccess.Require(pin, member.Identity);
                        if (content && member.Original is { } item)
                        {
                            if (pin.Length != item.Entry.Length) throw Conflict();
                            beforeHash?.Invoke(member.Path);
                            if (OwnedDirectorySnapshotTree.Hash(pin.SafeFileHandle, ct) != item.Hash) throw Conflict();
                        }
                    }, workers, ct);
                    for (int index = 0; index < entries.Length; index++) files.Add(entries[index].Path, opened[index]!);
                }
                catch { foreach (var pin in opened) pin?.Dispose(); throw; }
                RequireMembership();
            }
            catch { Dispose(); throw; }
        }
        private static bool Directory(Member member) => member.Original?.Entry.Directory ?? member.Added!.Directory;
        internal void RequireMembership()
        {
            var expected = members.Keys.ToHashSet(StringComparer.Ordinal);
            var seen = members.Keys.Where(p => !members.ContainsKey(Path.GetDirectoryName(p)!)).ToHashSet(StringComparer.Ordinal);
            foreach (var pair in directories)
            {
                if (pair.Value.IsClosed) continue;
                foreach (string path in System.IO.Directory.EnumerateFileSystemEntries(pair.Key))
                    if (!expected.Contains(path) || !seen.Add(path)) throw Conflict();
            }
            if (seen.Count != expected.Count) throw Conflict();
            foreach (string path in seen.Where(p => !members.ContainsKey(Path.GetDirectoryName(p)!)))
                if (!ManagedUnchangedDirectoryRestore.Exists(path)) throw Conflict();
        }
        internal void Reclaim(Action<string>? boundary, CancellationToken ct)
        {
            RequireMembership();
            foreach (var member in members.Values.Where(m => m.Deleted && !Directory(m)).OrderBy(m => m.Path, StringComparer.Ordinal).ToArray())
            {
                ct.ThrowIfCancellationRequested();
                var pin = files[member.Path]; ManagedRestoreFileAccess.Delete(pin); pin.Dispose(); files.Remove(member.Path); members.Remove(member.Path);
                ManagedUnchangedDirectoryRestore.RequireAbsent(member.Path); boundary?.Invoke("topology.reclaim.file");
            }
            foreach (var member in members.Values.Where(m => m.Deleted && Directory(m)).OrderByDescending(m => m.Path.Length).ToArray())
            {
                ct.ThrowIfCancellationRequested();
                if (System.IO.Directory.EnumerateFileSystemEntries(member.Path).Any()) throw Conflict();
                var pin = directories[member.Path]; ManagedRestoreFileAccess.DeleteDirectory(pin); pin.Dispose(); directories.Remove(member.Path); members.Remove(member.Path);
                ManagedUnchangedDirectoryRestore.RequireAbsent(member.Path); boundary?.Invoke("topology.reclaim.directory");
            }
            RequireMembership();
        }
        public void Dispose()
        {
            foreach (var file in files.Values) file.Dispose(); files.Clear();
            // Reclaimed directory handles may already be closed; the proofs
            // own all remaining descendants, but never the caller's root.
            receiptProof?.Dispose(); originalProof?.Dispose(); receiptHandle?.Dispose(); directories.Clear();
        }
    }
}
