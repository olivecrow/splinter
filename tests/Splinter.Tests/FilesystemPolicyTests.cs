using System.Text.Json;
using Splinter.Core;
using Splinter.Runtime;
using Splinter.Platform.Windows;

namespace Splinter.Tests;

public sealed class FilesystemPolicyTests
{
    [Fact]
    public void CaseDistinctPathsPermissionsAndWhiteoutsSurviveReplayCompactionAndCandidate()
    {
        using var directory = new TestDirectory(); var basis = new MemoryTree();
        basis.Files["Tree/run"] = ([1, 2, 3], 0x1ED);
        basis.Files["tree/run"] = ([4, 5, 6], 0x1A4);
        basis.Files["Tree/gone"] = ([7], null);
        var identity = new OverlayIdentity(2, "posix", basis.Snapshot, 1_000_000, 4096);
        string delta = Path.Combine(directory.Path, "delta"), candidatePath = Path.Combine(directory.Path, "candidate");
        Guid upper, lower;
        using (var writer = new JournalDeltaStore(delta, identity, basis))
        {
            upper = writer.Stat("Tree/run")!.Id; lower = writer.Stat("tree/run")!.Id;
            Assert.NotEqual(upper, lower);
            writer.Write(upper, 1, [9]); writer.SetUnixMode(upper, 0x1C0);
            writer.SetMetadata(upper, 32, 10, 20, 30); // Windows metadata changes retain independent Unix metadata.
            writer.Delete(writer.Stat("Tree/gone")!.Id);
            writer.Rename(writer.Stat("Tree")!.Id, "Moved", false);
            var created = writer.Create("tool: .", false, 32); writer.SetUnixMode(created.Id, 0x1ED);
            writer.Write(created.Id, 0, [8]);
            Assert.Throws<ArgumentOutOfRangeException>(() => writer.SetUnixMode(upper, 0x1000));
            Assert.Equal(new[] { "Moved/run" }, writer.List("Moved").Select(e => e.Path));
        }
        void Check(IWorkspaceDeltaStore store)
        {
            Assert.Equal(upper, store.Stat("Moved/run")!.Id); Assert.Equal(lower, store.Stat("tree/run")!.Id);
            Assert.Null(store.Stat("Tree")); Assert.Null(store.Stat("Moved/gone")); Assert.Null(store.Stat("TREE/run"));
            Assert.Equal(0x1C0u, store.Stat("Moved/run")!.UnixMode); Assert.Equal(0x1A4u, store.Stat("tree/run")!.UnixMode);
            Assert.Equal(0x1EDu, store.Stat("tool: .")!.UnixMode);
            byte[] bytes = new byte[3]; Assert.Equal(3, store.Read(upper, 0, bytes)); Assert.Equal(new byte[] { 1, 9, 3 }, bytes);
        }
        using (var replay = new JournalDeltaStore(delta, identity, basis)) Check(replay);
        JournalDeltaStore.CompactDirectory(delta, identity, basis);
        using (var compacted = new JournalDeltaStore(delta, identity, basis)) Check(compacted);
        JournalDeltaStore.ExportCandidate(delta, identity, basis, candidatePath, "fixed", []);
        using var candidate = new CandidateSnapshotTree(candidatePath, basis);
        Assert.Equal(WorkspacePathPolicy.PosixId, candidate.Snapshot.PathPolicy);
        Assert.Equal(0x1C0u, candidate.Stat("Moved/run")!.UnixMode);
        Assert.Equal(0x1A4u, candidate.Stat("tree/run")!.UnixMode);
        using var stopped = JournalDeltaStore.OpenReadOnly(delta, identity, basis);
        Assert.Equal(0x1C0u, Assert.Single(stopped.DescribeRestore(), e => e.Entry.Path == "Moved/run").Entry.UnixMode);
        Assert.Equal(new byte[] { 1, 2, 3 }, basis.Files["Tree/run"].Bytes);
    }

    [Fact]
    public void PathPolicyCannotChangeOnReconnectAndLegacyWindowsDefaultsRemainOmitted()
    {
        using var directory = new TestDirectory(); var basis = new MemoryTree(); basis.Files["a"] = ([1], null);
        var identity = new OverlayIdentity(2, "pinned", basis.Snapshot, 100_000, 4096);
        using (var writer = new JournalDeltaStore(directory.Path, identity, basis)) writer.Write(writer.Stat("a")!.Id, 0, [2]);
        byte[] journal = File.ReadAllBytes(Path.Combine(directory.Path, "delta.journal"));
        var windows = new MemoryTree { Snapshot = basis.Snapshot with { PathPolicy = null } }; windows.Files["a"] = ([1], null);
        Assert.Equal("OverlayIdentityMismatch", Assert.Throws<WorkspaceException>(() =>
            new JournalDeltaStore(directory.Path, identity with { Snapshot = windows.Snapshot }, windows)).Code);
        Assert.Equal("SnapshotIdentityMismatch", Assert.Throws<WorkspaceException>(() =>
            new JournalDeltaStore(directory.Path, identity with { Snapshot = windows.Snapshot }, basis)).Code);
        var unsupported = identity with { Snapshot = basis.Snapshot with { PathPolicy = "unknown" } };
        Assert.Equal("PathPolicyUnsupported", Assert.Throws<WorkspaceException>(() => new JournalDeltaStore(directory.Path, unsupported, basis)).Code);
        Assert.Equal(journal, File.ReadAllBytes(Path.Combine(directory.Path, "delta.journal")));
        Assert.Equal("PathPolicyUnsupported", Assert.Throws<WorkspaceException>(() => WindowsSnapshotPolicy.Require(basis.Snapshot)).Code);
        WindowsSnapshotPolicy.Require(windows.Snapshot);
        Assert.DoesNotContain("PathPolicy", JsonSerializer.Serialize(windows.Snapshot));
        Assert.DoesNotContain("UnixMode", JsonSerializer.Serialize(new SnapshotEntry("a", false, 1, 32, 1, 1, 1)));
        Assert.Equal("dir/file", JournalDeltaStore.Normalize("DIR\\file").ToLowerInvariant());
        Assert.Throws<ArgumentException>(() => JournalDeltaStore.Normalize("tool: ."));
    }

    [Fact]
    public void FiltersAndGraftsUseTheirFixedTreePolicy()
    {
        using var directory = new TestDirectory(); var basis = new MemoryTree(); basis.Files["cache/base"] = ([1], null);
        var identity = new OverlayIdentity(2, "filtered", basis.Snapshot, 100_000, 4096, ["Cache"], DeniedTopLevel: ["Admin"]);
        string delta = Path.Combine(directory.Path, "delta");
        using (var writer = new JournalDeltaStore(delta, identity, basis))
        {
            Assert.NotNull(writer.Stat("cache/base")); Assert.Null(writer.Stat("Cache/base"));
            writer.Create("admin", false, 32);
            Assert.Throws<UnauthorizedAccessException>(() => writer.Create("Admin", false, 32));
            writer.Write(writer.Stat("cache/base")!.Id, 0, [2]);
        }
        string candidatePath = Path.Combine(directory.Path, "candidate");
        JournalDeltaStore.ExportCandidate(delta, identity, basis, candidatePath, "candidate", ["Cache"]);
        using var candidate = new CandidateSnapshotTree(candidatePath, basis);
        Assert.NotNull(candidate.Stat("cache/base")); Assert.Null(candidate.Stat("Cache/base"));
        var generated = new MemoryTree { Snapshot = basis.Snapshot with { Id = "generated" } }; generated.Files["run"] = ([3], 0x1ED);
        var reference = new GeneratedDirectoryReference("basis", "project", "Cache", generated.Snapshot, "receipt", "owner", "manifest");
        var graft = new GraftedSnapshotTree(basis, generated, new("Cache", reference));
        Assert.Equal(0x1EDu, graft.Stat("Cache/run")!.UnixMode); Assert.NotNull(graft.Stat("cache/base"));
        Assert.Null(graft.Stat("CACHE/run"));
        var other = new MemoryTree { Snapshot = generated.Snapshot with { PathPolicy = null } };
        Assert.Equal("GeneratedBasisMismatch", Assert.Throws<WorkspaceException>(() =>
            new GraftedSnapshotTree(basis, other, new("Cache", reference with { Snapshot = other.Snapshot }))).Code);
    }

    private sealed class MemoryTree : ISnapshotTree
    {
        public SnapshotReference Snapshot { get; init; } = new("memory", "fixture", "source", "memory", DateTimeOffset.UnixEpoch,
            true, true, "fixture-only", WorkspacePathPolicy.PosixId);
        public Dictionary<string, (byte[] Bytes, uint? Mode)> Files { get; } = new(StringComparer.Ordinal);
        private WorkspacePathPolicy Policy => WorkspacePathPolicy.For(Snapshot);
        public void EnsureAvailable() { }
        public SnapshotEntry? Stat(string path)
        {
            path = Policy.Normalize(path);
            if (Files.TryGetValue(path, out var file)) return new(path, false, file.Bytes.Length, 32, 1, 1, 1, file.Mode);
            return path.Length == 0 || Files.Keys.Any(p => Policy.IsChild(p, path)) ? new(path, true, 0, 16, 1, 1, 1) : null;
        }
        public IReadOnlyList<SnapshotEntry> List(string directory)
        {
            string prefix = directory.Length == 0 ? "" : directory + "/";
            return Files.Keys.Where(p => p.StartsWith(prefix, Policy.Comparison))
                .Select(p => prefix + p[prefix.Length..].Split('/')[0]).Distinct(Policy.Comparer).Select(p => Stat(p)!).ToArray();
        }
        public int Read(string path, long offset, Span<byte> destination)
        {
            var data = Files[path].Bytes; int count = (int)Math.Min(destination.Length, Math.Max(0, data.Length - offset));
            data.AsSpan((int)offset, count).CopyTo(destination); return count;
        }
    }
}
