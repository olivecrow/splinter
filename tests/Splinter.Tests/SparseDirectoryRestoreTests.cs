using System.Text.Json.Nodes;
using Splinter.Core;
using Splinter.Platform.Windows;
using Splinter.Runtime;

namespace Splinter.Tests;

public sealed class SparseDirectoryRestoreTests
{
    [Theory]
    [InlineData("same", false)]
    [InlineData("same", true)]
    [InlineData("metadata", false)]
    [InlineData("metadata", true)]
    [InlineData("delete", false)]
    [InlineData("delete", true)]
    public async Task FinalStateUsesOneRootMoveAndOnlyDeletedLeafMoves(string change, bool bounded)
    {
        using var f = new Fixture(change, bounded); int roots = 0, leaves = 0, ordinary = 0;
        await f.Restore(stage =>
        {
            if (stage is "directory.after-move" or "sparse.after-move") roots++;
            if (stage == "sparse.after-stage") leaves++;
            if (stage == "file.after-move") ordinary++;
            if (stage == "sparse.after-proof")
            {
                Assert.Throws<IOException>(() => File.WriteAllText(f.Target("dir/f0002"), "foreign"));
                Assert.Throws<IOException>(() => File.Delete(f.Target("dir/f0002")));
            }
        });
        Assert.Equal(1, roots); Assert.Equal(change == "delete" ? 2 : 0, leaves); Assert.Equal(0, ordinary);
        Assert.False(File.Exists(Path.Combine(f.Root, "restore-cursor.json"))); f.AssertRestored();
    }

    [Theory]
    [InlineData("directory.after-plan")]
    [InlineData("directory.after-move")]
    [InlineData("directory.reclaim.intent")]
    public async Task PositiveSequenceExactStateResumesItsExistingDirectoryPlan(string boundary)
    {
        using var f = new Fixture("same", true); await f.Interrupt(boundary);
        await f.Restore(); f.AssertRestored();
    }

    [Theory]
    [InlineData("metadata", false)]
    [InlineData("metadata", true)]
    [InlineData("range", false)]
    [InlineData("range", true)]
    [InlineData("delete", false)]
    [InlineData("delete", true)]
    public async Task UnchangedPlanCannotDiscardJournalEffectsByReplacingItsSeal(string change, bool bounded)
    {
        using var f = new Fixture("same", bounded); await f.Interrupt("directory.after-plan");
        SnapshotDeltaSeal? replacement = null;
        f.Mutate(journal =>
        {
            var file = journal.Stat("dir/f0002")!;
            if (change == "metadata") journal.SetMetadata(file.Id, file.Attributes, file.CreationTime, file.LastAccessTime, file.LastWriteTime + 1);
            else if (change == "range") journal.Write(file.Id, 1, [99]);
            else journal.Delete(file.Id);
            replacement = journal.Seal();
        });
        string path = Path.Combine(f.Root, "restore-directory-plan.json"); var plan = JsonNode.Parse(File.ReadAllText(path))!;
        plan["seal"] = System.Text.Json.JsonSerializer.SerializeToNode(replacement, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        File.WriteAllText(path, plan.ToJsonString());
        Assert.Equal("GeneratedRestoreConflict", (await Assert.ThrowsAsync<WorkspaceException>(() => f.Restore())).Code);
        Assert.True(Directory.Exists(f.State.Basis.Snapshot.RootPath)); Assert.True(Directory.Exists(f.Request.DeltaPath));
        Assert.False(Directory.Exists(f.State.OriginalPath)); Assert.False(File.Exists(Path.Combine(f.Root, "restore-directory-verified.json")));
        Assert.Equal(replacement, JournalDeltaStore.SealJournal(Path.Combine(f.Request.DeltaPath, "delta.journal"), replacement!.DurableSequence));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnchangedPlanAndJournalStayPinnedThroughFinalProof(bool bounded)
    {
        using var f = new Fixture("same", bounded); int checks = 0;
        await f.Restore(stage =>
        {
            if (stage is not ("directory.after-proof" or "directory.after-verified")) return;
            foreach (string path in new[] { Path.Combine(f.Root, "restore-directory-plan.json"), Path.Combine(f.Request.DeltaPath, "delta.journal") })
                Assert.Throws<IOException>(() => { using var writer = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete); });
            checks++;
        });
        Assert.Equal(2, checks); f.AssertRestored();
    }

    [Fact]
    public async Task PublishedUnchangedPlanIsRevalidatedBeforeRootMove()
    {
        using var f = new Fixture("same", true);
        Assert.Equal("GeneratedRestoreConflict", (await Assert.ThrowsAsync<WorkspaceException>(() => f.Restore(stage =>
        {
            if (stage != "directory.after-plan") return;
            string path = Path.Combine(f.Root, "restore-directory-plan.json"); var plan = JsonNode.Parse(File.ReadAllText(path))!;
            plan["identityHash"] = "foreign"; File.WriteAllText(path, plan.ToJsonString());
        }))).Code);
        Assert.True(Directory.Exists(f.State.Basis.Snapshot.RootPath)); Assert.True(Directory.Exists(f.Request.DeltaPath));
        Assert.False(Directory.Exists(f.State.OriginalPath));
    }

    [Theory]
    [InlineData("sparse.root-unpinned")]
    [InlineData("sparse.after-plan")]
    [InlineData("sparse.before-move")]
    [InlineData("sparse.after-move")]
    [InlineData("sparse.after-proof")]
    [InlineData("sparse.before-stage")]
    [InlineData("sparse.after-stage")]
    [InlineData("sparse.after-verified")]
    [InlineData("sparse.reclaim.intent")]
    [InlineData("sparse.reclaim.file")]
    [InlineData("sparse.journal.intent")]
    [InlineData("sparse.journal.journal-removed")]
    [InlineData("sparse.after-reclaim")]
    public async Task InterruptedMovesAndPartialReclamationResume(string boundary)
    {
        foreach (bool bounded in new[] { false, true })
        {
            using var f = new Fixture("delete", bounded); await f.Interrupt(boundary);
            await f.Restore(); f.AssertRestored();
        }
    }

    [Theory]
    [InlineData("retained-bytes")]
    [InlineData("retained-identity")]
    [InlineData("deleted-identity")]
    [InlineData("foreign")]
    [InlineData("basis")]
    [InlineData("directory")]
    public async Task RenameGapChangesArePreservedBeforeAnyDeletion(string change)
    {
        using var f = new Fixture("delete", true);
        await Assert.ThrowsAsync<WorkspaceException>(() => f.Restore(stage =>
        {
            if (stage != "sparse.after-move") return;
            string target = f.Target(change == "deleted-identity" ? "dir/f0001" : "dir/f0002");
            if (change == "retained-bytes") File.WriteAllBytes(target, [99]);
            else if (change.EndsWith("identity", StringComparison.Ordinal))
            {
                byte[] bytes = File.ReadAllBytes(target); File.Move(target, Path.Combine(f.Root, "foreign-preserved")); File.WriteAllBytes(target, bytes);
            }
            else if (change == "foreign") File.WriteAllText(f.Target("foreign"), "keep");
            else if (change == "basis") Directory.CreateDirectory(f.State.Basis.Snapshot.RootPath);
            else
            {
                string path = f.Target("dir"); Directory.Move(path, Path.Combine(f.Root, "foreign-directory")); Directory.CreateDirectory(path);
                foreach (string file in Directory.GetFiles(Path.Combine(f.Root, "foreign-directory"))) File.Move(file, Path.Combine(path, Path.GetFileName(file)));
            }
        }));
        Assert.True(Directory.Exists(f.Request.DeltaPath));
        Assert.False(File.Exists(Path.Combine(f.Root, "restore-sparse-directory-verified.json")));
        Assert.True(File.Exists(f.Target("dir/f0000")));
    }

    [Theory]
    [InlineData("replace-stage")]
    [InlineData("missing-stage")]
    [InlineData("duplicate-original")]
    [InlineData("foreign-stage")]
    [InlineData("changed-survivor")]
    public async Task InterruptedStagingDoesNotAbsorbForeignChanges(string change)
    {
        using var f = new Fixture("delete", true); await f.Interrupt("sparse.after-stage");
        string staged = Directory.GetFiles(f.Root, "restore-sparse-leaf-*.bin").Single();
        if (change is "replace-stage" or "missing-stage")
        {
            byte[] bytes = File.ReadAllBytes(staged); File.Move(staged, Path.Combine(f.Root, "preserved-stage"));
            if (change == "replace-stage") File.WriteAllBytes(staged, bytes);
        }
        else if (change == "duplicate-original") File.WriteAllText(f.Target("dir/f0000"), "foreign");
        else if (change == "foreign-stage") File.WriteAllText(Path.Combine(f.Root, "restore-sparse-leaf-unknown.bin"), "foreign");
        else File.WriteAllBytes(f.Target("dir/f0002"), [77]);
        await Assert.ThrowsAsync<WorkspaceException>(() => f.Restore());
        Assert.True(Directory.Exists(f.Request.DeltaPath));
        Assert.False(File.Exists(Path.Combine(f.Root, "restore-sparse-staging-reclaim.json")));
    }

    [Theory]
    [InlineData("foreign-stage")]
    [InlineData("foreign-original")]
    public async Task ConcurrentPublicationCannotBecomeADeletedOwnedFile(string change)
    {
        using var f = new Fixture("delete", true); bool injected = false;
        await Assert.ThrowsAsync<WorkspaceException>(() => f.Restore(stage =>
        {
            if (injected || stage != (change == "foreign-stage" ? "sparse.before-stage" : "sparse.after-stage")) return;
            injected = true;
            File.WriteAllText(change == "foreign-stage" ? Path.Combine(f.Root, "restore-sparse-leaf-00000000.bin") : f.Target("dir/f0000"), "foreign");
        }));
        Assert.True(injected); Assert.True(Directory.Exists(f.Request.DeltaPath));
        Assert.Equal("foreign", File.ReadAllText(change == "foreign-stage" ? Path.Combine(f.Root, "restore-sparse-leaf-00000000.bin") : f.Target("dir/f0000")));
        Assert.False(File.Exists(Path.Combine(f.Root, "restore-sparse-staging-reclaim.json")));
    }

    [Fact]
    public async Task DeletedFilePinsCoverProofStagingAndFinalVerification()
    {
        using var f = new Fixture("delete", true); int checks = 0;
        await f.Restore(stage =>
        {
            string? path = stage switch
            {
                "sparse.after-proof" => f.Target("dir/f0000"),
                "sparse.after-stage" => Directory.GetFiles(f.Root, "restore-sparse-leaf-*.bin").First(),
                _ => null
            };
            if (path is null) return;
            Assert.Throws<IOException>(() => File.Move(path, Path.Combine(f.Root, "replacement"))); checks++;
        });
        Assert.Equal(3, checks); f.AssertRestored();
    }

    [Fact]
    public async Task ChangedOriginalAfterPartialStagingReclaimPreservesRemainingOwnedData()
    {
        using var f = new Fixture("delete", true); await f.Interrupt("sparse.reclaim.file");
        string survivor = f.Target("dir/f0002"); File.SetAttributes(survivor, FileAttributes.Normal); File.WriteAllBytes(survivor, [77]);
        string remaining = Directory.GetFiles(f.Root, "restore-sparse-leaf-*.bin").Single();
        await Assert.ThrowsAsync<WorkspaceException>(() => f.Restore());
        Assert.Equal(new byte[] { 77 }, File.ReadAllBytes(survivor)); Assert.True(File.Exists(remaining)); Assert.True(Directory.Exists(f.Request.DeltaPath));
    }

    [Theory]
    [InlineData("version")]
    [InlineData("identity")]
    [InlineData("seal")]
    [InlineData("two-roots")]
    [InlineData("general-plan")]
    [InlineData("directory-plan")]
    [InlineData("general-receipt")]
    public async Task IncompatiblePlansAndSealArePreserved(string change)
    {
        using var f = new Fixture("metadata", true); await f.Interrupt("sparse.after-plan");
        string path = Path.Combine(f.Root, "restore-sparse-directory-plan.json");
        if (change is "version" or "identity")
        {
            var json = JsonNode.Parse(File.ReadAllText(path))!;
            if (change == "version") json["version"] = 0; else json["identityHash"] = "foreign";
            File.WriteAllText(path, json.ToJsonString());
        }
        else if (change == "seal") f.Mutate(j => j.SetLength(j.Stat("dir/f0002")!.Id, 1));
        else if (change == "two-roots") Directory.CreateDirectory(f.State.OriginalPath);
        else File.WriteAllText(Path.Combine(f.Root, change switch
        { "general-plan" => "restore-plan.json", "general-receipt" => "restore-file-foreign.json", _ => "restore-directory-plan.json" }), "{}");
        await Assert.ThrowsAsync<WorkspaceException>(() => f.Restore());
        Assert.True(File.Exists(path)); Assert.True(Directory.Exists(f.Request.DeltaPath));
    }

    [Theory]
    [InlineData("metadata", false)]
    [InlineData("deleted", false)]
    [InlineData("metadata", true)]
    [InlineData("deleted", true)]
    public async Task PlanEffectsMustEqualTheSealedJournalBeforeDestruction(string change, bool sameCall)
    {
        using var f = new Fixture("delete", true);
        void Corrupt()
        {
            string path = Path.Combine(f.Root, "restore-sparse-directory-plan.json"); var json = JsonNode.Parse(File.ReadAllText(path))!;
            if (change == "metadata") json["metadata"]![0]!["lastWriteTime"] = json["metadata"]![0]!["lastWriteTime"]!.GetValue<long>() + 1;
            else json["deleted"]!.AsArray().Add("dir/f0003");
            File.WriteAllText(path, json.ToJsonString());
        }
        if (sameCall)
            await Assert.ThrowsAsync<WorkspaceException>(() => f.Restore(stage => { if (stage == "sparse.after-plan") Corrupt(); }));
        else
        {
            await f.Interrupt("sparse.after-stage"); Corrupt();
            await Assert.ThrowsAsync<WorkspaceException>(() => f.Restore());
        }
        Assert.True(Directory.Exists(f.Request.DeltaPath));
        Assert.False(File.Exists(Path.Combine(f.Root, "restore-sparse-staging-reclaim.json")));
    }

    [Fact]
    public async Task PlanAndJournalRemainPinnedUntilStagedDeletionCompletes()
    {
        using var f = new Fixture("delete", true); int checks = 0;
        await f.Restore(stage =>
        {
            if (stage is not ("sparse.after-proof" or "sparse.reclaim.intent")) return;
            foreach (string path in new[] { Path.Combine(f.Root, "restore-sparse-directory-plan.json"), Path.Combine(f.Request.DeltaPath, "delta.journal") })
                Assert.Throws<IOException>(() => { using var writer = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete); });
            checks++;
        });
        Assert.Equal(2, checks); f.AssertRestored();
    }

    [Theory]
    [InlineData("restore-sparse-directory-verified.json")]
    [InlineData("restore-sparse-staging-reclaim.json")]
    [InlineData("restore-sparse-leaf-00000000.bin")]
    [InlineData("restore-file-foreign.json")]
    public async Task OrphanSparseRecordsDoNotStartAnotherMode(string name)
    {
        using var f = new Fixture("metadata", true); File.WriteAllText(Path.Combine(f.Root, name), "foreign");
        await Assert.ThrowsAsync<WorkspaceException>(() => f.Restore());
        Assert.Equal("foreign", File.ReadAllText(Path.Combine(f.Root, name))); Assert.False(Directory.Exists(f.State.OriginalPath));
    }

    [Theory]
    [InlineData("range")]
    [InlineData("create")]
    [InlineData("rename")]
    [InlineData("remove-directory")]
    public async Task ContentAndTopologyChangesSelectTheirOwnRootRestorePlans(string change)
    {
        using var f = new Fixture("metadata", true);
        f.Mutate(j =>
        {
            if (change == "range") j.Write(j.Stat("dir/f0002")!.Id, 1, [88]);
            else if (change == "create") j.Create("new", false, 32);
            else if (change == "rename") j.Rename(j.Stat("dir/f0002")!.Id, "renamed", false);
            else { for (int i = 0; i < 4; i++) j.Delete(j.Stat($"dir/f{i:D4}")!.Id); j.Delete(j.Stat("dir")!.Id); }
        });
        var manifest = new OwnedDirectorySnapshotProvider(f.Root, f.State.Basis.OwnerId).Read(f.State.Basis.Snapshot);
        DeltaRestoreEntry[] expected;
        var bytes = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        var identities = new Dictionary<string, string>(StringComparer.Ordinal);
        using (var tree = new OwnedDirectorySnapshotTree(manifest, f.State.Basis.Snapshot))
        using (var journal = JournalDeltaStore.OpenReadOnly(f.Request.DeltaPath, f.Request.Identity, tree))
        {
            expected = journal.DescribeRestore();
            var topology = ManagedRestoreTopology.Build(manifest, expected);
            long survivingFiles = expected.LongCount(delta => !delta.Entry.Directory && delta.Entry.BasePath is not null);
            long transportMoves = 1;
            for (int unit = 1; unit < topology.Units.Length; unit++) transportMoves += topology.Units[unit].FinalPath is null ? 1 : 2;
            Assert.Equal(change == "remove-directory" ? 0L : 4L, survivingFiles);
            Assert.Equal(change is "rename" or "remove-directory" ? 2 : 1, topology.Units.Length);
            Assert.Equal(change == "remove-directory" ? 2L : change == "rename" ? 3L : 1L, transportMoves);
            Assert.Equal(change is "range" or "create", ManagedTopologyDirectoryRestore.PreferFresh(manifest, expected));
            foreach (var delta in expected.Where(delta => !delta.Entry.Directory))
            {
                var entry = delta.Entry; byte[] payload = new byte[checked((int)entry.Length)];
                Assert.Equal(payload.Length, journal.Read(entry.Id, 0, payload)); bytes.Add(entry.Path, payload);
                if (entry.BasePath is not { } source) continue;
                using var pin = ManagedRestoreFileAccess.ReadHandle(Path.Combine(f.State.Basis.Snapshot.RootPath, source));
                identities.Add(entry.Path, SnapshotInputChecks.FileIdentity(pin));
            }
        }
        await f.Restore();
        bool rootDelta = change is "range" or "create";
        // Removing all leaves has no surviving-file budget. Renaming one of
        // four survivors also exceeds G=1 with two topology cursor writes.
        // Both stay general; a sufficiently large carried subtree still fits.
        Assert.Equal(rootDelta, File.Exists(Path.Combine(f.Root, "restore-root-delta-plan.json")));
        Assert.False(File.Exists(Path.Combine(f.Root, "restore-topology-plan.json")));
        Assert.Equal(!rootDelta, File.Exists(Path.Combine(f.Root, "restore-plan.json")));
        Assert.False(File.Exists(Path.Combine(f.Root, "restore-sparse-directory-plan.json")));
        foreach (var delta in expected)
        {
            var entry = delta.Entry; string path = f.Target(entry.Path);
            Assert.Equal(entry.Attributes, (uint)File.GetAttributes(path));
            Assert.Equal(entry.CreationTime, File.GetCreationTimeUtc(path).ToFileTimeUtc());
            Assert.Equal(entry.LastWriteTime, File.GetLastWriteTimeUtc(path).ToFileTimeUtc());
            if (entry.Directory) continue;
            Assert.Equal(bytes[entry.Path], File.ReadAllBytes(path));
            if (!identities.TryGetValue(entry.Path, out string? identity)) continue;
            using var pin = ManagedRestoreFileAccess.ReadHandle(path); Assert.Equal(identity, SnapshotInputChecks.FileIdentity(pin));
        }
        Assert.Equal(expected.Length - 1, Directory.EnumerateFileSystemEntries(f.State.OriginalPath, "*", SearchOption.AllDirectories).Count());
        Assert.False(Directory.Exists(f.State.Basis.Snapshot.RootPath)); Assert.False(Directory.Exists(f.Request.DeltaPath));
    }

    [Fact]
    public async Task MetadataOnlyEmptyLibraryAndVerifiedRetryKeepFinalMetadata()
    {
        using var f = new Fixture("metadata", true, count: 0); await f.Interrupt("sparse.after-verified");
        Directory.SetLastWriteTimeUtc(f.State.OriginalPath, DateTime.UnixEpoch);
        await f.Restore(); f.AssertRestored();
    }

    [Fact]
    public async Task DeletingEveryLeafKeepsItsEmptyParentDirectory()
    {
        using var f = new Fixture("delete", true, count: 2); await f.Restore(); f.AssertRestored();
        Assert.True(Directory.Exists(f.Target("dir"))); Assert.Empty(Directory.GetFiles(f.State.OriginalPath, "*", SearchOption.AllDirectories));
    }

    private sealed class Fixture : IDisposable
    {
        private readonly ManagedDirectoryRestoreOptimizationTests.Fixture inner;
        private readonly bool bounded;
        private readonly Dictionary<string, string> identities = new(StringComparer.Ordinal);
        private DeltaRestoreEntry[] expected = [];
        internal ManagedDirectoryState State => inner.State;
        internal OverlayServeRequest Request => inner.Request;
        internal string Root => inner.Root;
        internal string Target(string path) => Path.Combine(State.OriginalPath, path);
        internal Fixture(string change, bool bounded, int count = 4)
        {
            this.bounded = bounded;
            inner = new(count, !bounded, unchangedDirectory: true, bounded: bounded);
            var manifest = new OwnedDirectorySnapshotProvider(Root, State.Basis.OwnerId).Read(State.Basis.Snapshot);
            foreach (var item in manifest.Items)
            {
                string path = Path.Combine(State.Basis.Snapshot.RootPath, item.Entry.Path);
                using var pin = item.Entry.Directory ? ManagedRestoreFileAccess.DirectoryHandle(path) : ManagedRestoreFileAccess.ReadHandle(path);
                identities[item.Entry.Path] = SnapshotInputChecks.FileIdentity(pin, item.Entry.Directory);
            }
            Mutate(j =>
            {
                var root = j.Stat("")!;
                j.SetMetadata(root.Id, root.Attributes, root.CreationTime, root.LastAccessTime, root.LastWriteTime + 10000);
                if (change == "same") j.SetMetadata(root.Id, root.Attributes, root.CreationTime, root.LastAccessTime, root.LastWriteTime);
                if (change == "delete") { j.Delete(j.Stat("dir/f0000")!.Id); j.Delete(j.Stat("dir/f0001")!.Id); }
                if (change != "same" && count > 2)
                {
                    var file = j.Stat("dir/f0002")!;
                    j.SetMetadata(file.Id, file.Attributes | 1, file.CreationTime, file.LastAccessTime, file.LastWriteTime + 20000);
                }
            });
            using var tree = new OwnedDirectorySnapshotTree(manifest, State.Basis.Snapshot);
            using var journal = JournalDeltaStore.OpenReadOnly(Request.DeltaPath, Request.Identity, tree);
            expected = journal.DescribeRestore(); Assert.True(journal.Seal().DurableSequence > 0);
        }
        internal void Mutate(Action<JournalDeltaStore> action)
        {
            var manifest = new OwnedDirectorySnapshotProvider(Root, State.Basis.OwnerId).Read(State.Basis.Snapshot);
            using var tree = new OwnedDirectorySnapshotTree(manifest, State.Basis.Snapshot);
            using var journal = new JournalDeltaStore(Request.DeltaPath, Request.Identity, tree,
                optimizeJournal: !bounded, boundedLogBytes: bounded ? 8192 : null);
            action(journal);
        }
        internal Task Restore(Action<string>? boundary = null) => inner.Restore(boundary);
        internal async Task Interrupt(string boundary)
        {
            bool hit = false;
            await Assert.ThrowsAsync<IOException>(() => Restore(stage => { if (stage == boundary) { hit = true; throw new IOException(boundary); } }));
            Assert.True(hit);
        }
        internal void AssertRestored()
        {
            foreach (var delta in expected)
            {
                var entry = delta.Entry; string path = Target(entry.Path);
                using var pin = entry.Directory ? ManagedRestoreFileAccess.DirectoryHandle(path) : ManagedRestoreFileAccess.ReadHandle(path);
                Assert.Equal(identities[entry.Path], SnapshotInputChecks.FileIdentity(pin, entry.Directory));
                Assert.Equal(entry.Attributes, (uint)File.GetAttributes(path));
                Assert.Equal(entry.CreationTime, File.GetCreationTimeUtc(path).ToFileTimeUtc());
                Assert.Equal(entry.LastWriteTime, File.GetLastWriteTimeUtc(path).ToFileTimeUtc());
                if (!entry.Directory)
                {
                    int index = int.Parse(Path.GetFileName(path).AsSpan(1), System.Globalization.CultureInfo.InvariantCulture);
                    Assert.Equal(Enumerable.Range(0, 64).Select(i => (byte)(index + i)), File.ReadAllBytes(path));
                }
            }
            Assert.Equal(expected.Length - 1, Directory.EnumerateFileSystemEntries(State.OriginalPath, "*", SearchOption.AllDirectories).Count());
            Assert.False(Directory.Exists(State.Basis.Snapshot.RootPath)); Assert.False(Directory.Exists(Request.DeltaPath));
            Assert.Empty(Directory.GetFiles(Root, "restore-sparse-leaf-*.bin"));
        }
        public void Dispose() => inner.Dispose();
    }
}
