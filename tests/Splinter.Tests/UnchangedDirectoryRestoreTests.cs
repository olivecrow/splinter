using System.Text.Json.Nodes;
using Splinter.Core;
using Splinter.Platform.Windows;
using Splinter.Runtime;
using Fixture = Splinter.Tests.ManagedDirectoryRestoreOptimizationTests.Fixture;

namespace Splinter.Tests;

public sealed class UnchangedDirectoryRestoreTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    public async Task WholeDirectoryPreservesIdentityMetadataAndPinnedContent(int count)
    {
        using var f = new Fixture(count, true, unchangedDirectory: true);
        var manifest = new OwnedDirectorySnapshotProvider(f.Root, f.State.Basis.OwnerId).Read(f.State.Basis.Snapshot);
        string rootIdentity;
        using (var pin = ManagedRestoreFileAccess.DirectoryHandle(f.State.Basis.Snapshot.RootPath))
            rootIdentity = SnapshotInputChecks.FileIdentity(pin, true);
        int moves = 0, files = 0;
        await f.Restore(stage =>
        {
            if (stage == "directory.after-move") moves++;
            if (stage == "file.after-move") files++;
            if (stage == "directory.after-proof" && count > 1)
            {
                string path = Path.Combine(f.State.OriginalPath, "dir", "f0001");
                Assert.Throws<IOException>(() => File.Delete(path));
                Assert.Throws<IOException>(() => File.WriteAllBytes(path, [7]));
            }
        });
        Assert.Equal(1, moves); Assert.Equal(0, files);
        using (var pin = ManagedRestoreFileAccess.DirectoryHandle(f.State.OriginalPath))
            Assert.Equal(rootIdentity, SnapshotInputChecks.FileIdentity(pin, true));
        foreach (var item in manifest.Items)
        {
            string path = Path.Combine(f.State.OriginalPath, item.Entry.Path);
            Assert.Equal(item.Entry.Attributes, (uint)File.GetAttributes(path));
            Assert.Equal(item.Entry.CreationTime, File.GetCreationTimeUtc(path).ToFileTimeUtc());
            Assert.Equal(item.Entry.LastWriteTime, File.GetLastWriteTimeUtc(path).ToFileTimeUtc());
        }
        Assert.False(File.Exists(Path.Combine(f.Root, "restore-cursor.json"))); f.AssertRestored();
    }

    [Theory]
    [InlineData("directory.after-plan")]
    [InlineData("directory.before-move")]
    [InlineData("directory.after-move")]
    [InlineData("directory.after-verified")]
    [InlineData("directory.reclaim.intent")]
    [InlineData("directory.reclaim.journal-removed")]
    [InlineData("directory.after-reclaim")]
    public async Task InterruptedDirectoryAndReclamationEffectsResume(string hook)
    {
        using var f = new Fixture(3, true, unchangedDirectory: true);
        await Assert.ThrowsAsync<IOException>(() => f.Restore(stage => { if (stage == hook) throw new IOException(hook); }));
        await f.Restore(); f.AssertRestored();
    }

    [Theory]
    [InlineData("bytes")]
    [InlineData("file-identity")]
    [InlineData("directory-identity")]
    [InlineData("foreign")]
    [InlineData("basis-replacement")]
    public async Task RenameGapChangesArePreservedWithoutReclaim(string change)
    {
        using var f = new Fixture(3, true, unchangedDirectory: true);
        await Assert.ThrowsAsync<WorkspaceException>(() => f.Restore(stage =>
        {
            if (stage != "directory.after-move") return;
            string path = Path.Combine(f.State.OriginalPath, "dir", "f0001");
            if (change == "bytes") File.WriteAllBytes(path, [4, 5]);
            else if (change == "file-identity")
            {
                byte[] bytes = File.ReadAllBytes(path); File.Move(path, Path.Combine(f.Root, "retained-file")); File.WriteAllBytes(path, bytes);
            }
            else if (change == "directory-identity")
            {
                string dir = Path.GetDirectoryName(path)!; Directory.Move(dir, Path.Combine(f.Root, "retained-dir")); Directory.CreateDirectory(dir);
                foreach (string file in Directory.GetFiles(Path.Combine(f.Root, "retained-dir"))) File.Move(file, Path.Combine(dir, Path.GetFileName(file)));
            }
            else if (change == "foreign") File.WriteAllText(Path.Combine(f.State.OriginalPath, "foreign"), "retain me");
            else Directory.CreateDirectory(f.State.Basis.Snapshot.RootPath);
        }));
        Assert.True(Directory.Exists(f.Request.DeltaPath)); Assert.True(Directory.Exists(f.State.OriginalPath));
        Assert.False(File.Exists(Path.Combine(f.Root, "restore-directory-verified.json")));
    }

    [Theory]
    [InlineData("root")]
    [InlineData("file")]
    [InlineData("seal")]
    [InlineData("identity")]
    [InlineData("two-roots")]
    public async Task ResumeRejectsChangedIdentitySealOrAmbiguousLocation(string change)
    {
        using var f = new Fixture(3, true, unchangedDirectory: true);
        await Assert.ThrowsAsync<IOException>(() => f.Restore(stage => { if (stage == "directory.after-plan") throw new IOException("stop"); }));
        string basis = f.State.Basis.Snapshot.RootPath;
        if (change == "root")
        {
            Directory.Move(basis, Path.Combine(f.Root, "retained-base")); Directory.CreateDirectory(basis);
            Directory.Move(Path.Combine(f.Root, "retained-base", "dir"), Path.Combine(basis, "dir"));
        }
        else if (change == "file")
        {
            string file = Path.Combine(basis, "dir", "f0001"); byte[] bytes = File.ReadAllBytes(file);
            File.Move(file, Path.Combine(f.Root, "retained-file")); File.WriteAllBytes(file, bytes);
        }
        else if (change == "seal")
        {
            var manifest = new OwnedDirectorySnapshotProvider(f.Root, f.State.Basis.OwnerId).Read(f.State.Basis.Snapshot);
            using var tree = new OwnedDirectorySnapshotTree(manifest, f.State.Basis.Snapshot);
            using var journal = new JournalDeltaStore(f.Request.DeltaPath, f.Request.Identity, tree);
            var entry = journal.Stat("dir/f0001")!; journal.SetMetadata(entry.Id, entry.Attributes, entry.CreationTime, entry.LastAccessTime, entry.LastWriteTime + 1);
        }
        else if (change == "identity")
        {
            string plan = Path.Combine(f.Root, "restore-directory-plan.json"); var json = JsonNode.Parse(File.ReadAllText(plan))!;
            json["identityHash"] = "different"; File.WriteAllText(plan, json.ToJsonString());
        }
        else Directory.CreateDirectory(f.State.OriginalPath);
        await Assert.ThrowsAsync<WorkspaceException>(() => f.Restore());
        Assert.True(Directory.Exists(f.Request.DeltaPath)); Assert.True(Directory.Exists(basis));
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task OtherJournalsAndAnyMutationKeepGeneralRestore(bool optimized, bool unchanged)
    {
        using var f = new Fixture(2, optimized, unchangedDirectory: unchanged);
        int moves = 0; await f.Restore(stage => { if (stage == "file.after-move") moves++; });
        Assert.Equal(2, moves); Assert.False(File.Exists(Path.Combine(f.Root, "restore-directory-plan.json"))); f.AssertRestored();
    }

    [Fact]
    public async Task ForeignTargetAndOrphanCursorNeverStartNewIntent()
    {
        using var f = new Fixture(2, true, unchangedDirectory: true);
        Directory.CreateDirectory(f.State.OriginalPath);
        await Assert.ThrowsAsync<WorkspaceException>(() => f.Restore());
        Assert.False(File.Exists(Path.Combine(f.Root, "restore-directory-plan.json")));
        Directory.Delete(f.State.OriginalPath);
        File.WriteAllText(Path.Combine(f.Root, "restore-cursor.json"), "{\"planHash\":\"unknown\",\"completedFiles\":0}");
        await Assert.ThrowsAsync<WorkspaceException>(() => f.Restore());
        Assert.False(File.Exists(Path.Combine(f.Root, "restore-plan.json")));
        Assert.False(File.Exists(Path.Combine(f.Root, "restore-directory-plan.json")));
    }

    [Theory]
    [InlineData("directory.after-plan")]
    [InlineData("directory.after-verified")]
    public async Task ReplacedJournalIdentityCannotBecomeOwnedCleanup(string hook)
    {
        using var f = new Fixture(2, true, unchangedDirectory: true);
        await Assert.ThrowsAsync<IOException>(() => f.Restore(stage => { if (stage == hook) throw new IOException("stop"); }));
        string path = Path.Combine(f.Request.DeltaPath, "identity.json");
        var json = JsonNode.Parse(File.ReadAllText(path))!; json["Id"] = "another-owner"; File.WriteAllText(path, json.ToJsonString());
        await Assert.ThrowsAsync<WorkspaceException>(() => f.Restore());
        Assert.True(File.Exists(path)); Assert.False(File.Exists(Path.Combine(f.Root, "restore-directory-reclaim.bin")));
    }

    [Fact]
    public async Task PairedManifestProofRewriteCannotChangeFixedRestoreMetadata()
    {
        using var f = new Fixture(2, true, unchangedDirectory: true);
        await Assert.ThrowsAsync<IOException>(() => f.Restore(stage => { if (stage == "directory.after-plan") throw new IOException("stop"); }));
        string path = Path.Combine(f.Root, "manifest.json"); var json = JsonNode.Parse(File.ReadAllText(path))!;
        json["items"]![0]!["entry"]!["lastWriteTime"] = 123L; File.WriteAllText(path, json.ToJsonString());
        string proofPath = Path.Combine(f.Root, "proof.json"); var proof = JsonNode.Parse(File.ReadAllText(proofPath))!;
        proof["manifestHash"] = ManagedDirectoryBasis.HashFile(path); File.WriteAllText(proofPath, proof.ToJsonString());
        await Assert.ThrowsAsync<WorkspaceException>(() => f.Restore());
        Assert.True(Directory.Exists(f.Request.DeltaPath)); Assert.False(Directory.Exists(f.State.OriginalPath));
    }

    [Fact]
    public async Task VerifiedRetryRestoresMetadataAgainUnderTheFinalPins()
    {
        using var f = new Fixture(2, true, unchangedDirectory: true);
        var manifest = new OwnedDirectorySnapshotProvider(f.Root, f.State.Basis.OwnerId).Read(f.State.Basis.Snapshot);
        await Assert.ThrowsAsync<IOException>(() => f.Restore(stage => { if (stage == "directory.after-verified") throw new IOException("stop"); }));
        string path = Path.Combine(f.State.OriginalPath, "dir", "f0001");
        File.SetLastWriteTimeUtc(path, DateTime.UnixEpoch); Directory.SetLastWriteTimeUtc(f.State.OriginalPath, DateTime.UnixEpoch);
        await f.Restore();
        Assert.Equal(manifest.Items.Single(i => i.Entry.Path == "dir/f0001").Entry.LastWriteTime, File.GetLastWriteTimeUtc(path).ToFileTimeUtc());
        Assert.Equal(manifest.Items.Single(i => i.Entry.Path == "").Entry.LastWriteTime, Directory.GetLastWriteTimeUtc(f.State.OriginalPath).ToFileTimeUtc());
        f.AssertRestored();
    }
}
