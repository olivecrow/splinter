using Splinter.Core;
using Splinter.Platform.Windows;
using Splinter.Runtime;
using System.Text.Json.Nodes;
using RestoreFixture = Splinter.Tests.ManagedDirectoryRestoreOptimizationTests.Fixture;

namespace Splinter.Tests;

public sealed class BoundedJournalReclamationTests
{
    [Theory]
    [InlineData("intent-flushed")]
    [InlineData("intent")]
    [InlineData("journal-removed")]
    [InlineData("metadata-removed")]
    [InlineData("guard-removed")]
    [InlineData("directory-removed")]
    public void InterruptedExactInventoryResumesWithoutRecreatingRemovedFiles(string stage)
    {
        using var root = new TestDirectory(); var f = Populate(root.Path);
        Assert.Throws<IOException>(() => Reclaim(f, at => { if (at == stage) throw new IOException(stage); }));
        bool markerMissing = !File.Exists(Path.Combine(f.Directory, BoundedJournalReclamation.GuardName));
        Reclaim(f, at =>
        {
            if (markerMissing) Assert.False(File.Exists(Path.Combine(f.Directory, BoundedJournalReclamation.GuardName)));
        });
        Assert.False(Directory.Exists(f.Directory)); Assert.True(File.Exists(f.Receipt));
        Assert.False(File.Exists(f.Receipt + ".pending")); Reclaim(f);
    }

    [Theory]
    [InlineData("foreign")]
    [InlineData("bounded-checkpoint.json")]
    [InlineData("bounded-checkpoint.json.pending")]
    [InlineData("bounded-initialization.json")]
    [InlineData("delta.journal.bounded-next")]
    [InlineData("delta.journal.bounded-previous")]
    [InlineData("directory")]
    public void UnknownAndPendingEntriesArePreservedBeforeAnyDeletion(string name)
    {
        using var root = new TestDirectory(); var f = Populate(root.Path);
        string path = Path.Combine(f.Directory, name);
        if (name == "directory") Directory.CreateDirectory(path); else File.WriteAllText(path, "retain");
        string[] before = Directory.GetFileSystemEntries(f.Directory);
        Assert.Throws<WorkspaceException>(() => Reclaim(f));
        Assert.All(before, p => Assert.True(Path.Exists(p))); Assert.False(BoundedJournalReclamation.HasIntent(f.Receipt));
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("changed-identity")]
    [InlineData("changed-guard")]
    public void PartialReclaimRejectsChangedRemainingInventory(string damage)
    {
        using var root = new TestDirectory(); var f = Populate(root.Path);
        Assert.Throws<IOException>(() => Reclaim(f, at => { if (at == "journal-removed") throw new IOException("stop"); }));
        string path = Path.Combine(f.Directory, damage switch
        { "unknown" => "foreign", "changed-identity" => "identity.json", _ => BoundedJournalReclamation.GuardName });
        File.WriteAllText(path, "preserve this"); string[] before = Directory.GetFiles(f.Directory);
        Assert.Throws<WorkspaceException>(() => Reclaim(f));
        Assert.Equal("preserve this", File.ReadAllText(path)); Assert.All(before, p => Assert.True(File.Exists(p)));
    }

    [Fact]
    public void MissingInitialGuardWrongSealAndForeignReceiptCannotAuthorizeDeletion()
    {
        using var root = new TestDirectory(); var f = Populate(root.Path);
        Assert.Equal("ViewSourceChanged", Assert.Throws<WorkspaceException>(() =>
            Reclaim(f with { Seal = f.Seal with { JournalHash = new string('0', 64) } })).Code);
        Assert.False(BoundedJournalReclamation.HasIntent(f.Receipt));
        string marker = Path.Combine(f.Directory, BoundedJournalReclamation.GuardName); File.Delete(marker);
        Assert.Throws<WorkspaceException>(() => Reclaim(f)); Assert.False(File.Exists(marker));
        Assert.True(File.Exists(Path.Combine(f.Directory, "delta.journal")));
        var other = Populate(Path.Combine(root.Path, "other"));
        Assert.Throws<IOException>(() => Reclaim(other, at => { if (at == "intent") throw new IOException("stop"); }));
        Assert.Throws<WorkspaceException>(() => Reclaim(f with { Receipt = other.Receipt }));
        Assert.True(File.Exists(Path.Combine(f.Directory, "identity.json")));
    }

    [Fact]
    public void LiveWriterAndStoppedReaderFenceReclamation()
    {
        if (!OperatingSystem.IsWindows()) Assert.Skip("Windows file-sharing contract.");
        using var root = new TestDirectory(); var f = Populate(root.Path);
        using (var writer = new JournalDeltaStore(f.Directory, f.Identity, f.Tree))
            Assert.Throws<IOException>(() => Reclaim(f));
        using (var reader = JournalDeltaStore.OpenReadOnly(f.Directory, f.Identity, f.Tree))
            Assert.Throws<IOException>(() => Reclaim(f));
        Assert.False(BoundedJournalReclamation.HasIntent(f.Receipt)); Reclaim(f);
    }

    [Fact]
    public void PublishedInventoryPinsWriterAndAncestryAndRechecksBeforeDeletion()
    {
        if (!OperatingSystem.IsWindows()) Assert.Skip("Windows ancestry and file-sharing contract.");
        using var root = new TestDirectory(); var f = Populate(root.Path);
        Reclaim(f, at =>
        {
            if (at != "intent") return;
            Assert.Throws<IOException>(() => Directory.Move(f.Directory, f.Directory + "-moved"));
            Assert.Throws<IOException>(() => { using var writer = new JournalDeltaStore(f.Directory, f.Identity, f.Tree); });
            foreach (string path in Directory.GetFiles(f.Directory))
                Assert.Throws<IOException>(() => { using var writer = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete); });
        });
        Assert.False(Directory.Exists(f.Directory));
    }

    [Fact]
    public void ForeignFileAfterPublicationPreventsTheFirstUnlink()
    {
        using var root = new TestDirectory(); var f = Populate(root.Path);
        Assert.Throws<WorkspaceException>(() => Reclaim(f, at =>
        { if (at == "intent") File.WriteAllText(Path.Combine(f.Directory, "foreign"), "retain"); }));
        Assert.True(File.Exists(Path.Combine(f.Directory, "delta.journal")));
        Assert.True(File.Exists(Path.Combine(f.Directory, "identity.json")));
        Assert.Equal("retain", File.ReadAllText(Path.Combine(f.Directory, "foreign")));
    }

    [Theory]
    [InlineData("directory.after-plan")]
    [InlineData("directory.after-move")]
    [InlineData("directory.after-verified")]
    [InlineData("directory.reclaim.intent-flushed")]
    [InlineData("directory.reclaim.intent")]
    [InlineData("directory.reclaim.journal-removed")]
    [InlineData("directory.reclaim.metadata-removed")]
    [InlineData("directory.reclaim.guard-removed")]
    public async Task UnchangedBoundedLibraryResumesTheSameDirectoryMoveAndInventory(string stage)
    {
        using var f = new RestoreFixture(3, true, unchangedDirectory: true, bounded: true);
        string originalIdentity;
        using (var pin = ManagedRestoreFileAccess.DirectoryHandle(f.State.Basis.Snapshot.RootPath))
            originalIdentity = SnapshotInputChecks.FileIdentity(pin, true);
        await Assert.ThrowsAsync<IOException>(() => f.Restore(at => { if (at == stage) throw new IOException(stage); }));
        await f.Restore(); f.AssertRestored();
        using var final = ManagedRestoreFileAccess.DirectoryHandle(f.State.OriginalPath);
        Assert.Equal(originalIdentity, SnapshotInputChecks.FileIdentity(final, true));
        Assert.False(File.Exists(Path.Combine(f.Root, "restore-plan.json")));
        Assert.True(File.Exists(Path.Combine(f.Root, "restore-directory-reclaim.bin")));
    }

    [Fact]
    public async Task ChangedBoundedLibraryUsesGeneralRestoreAndExactReclamation()
    {
        using var f = new RestoreFixture(3, true, rename: true, bounded: true);
        int files = 0; await f.Restore(at => { if (at == "file.after-move") files++; });
        Assert.Equal(3, files); f.AssertRestored();
        Assert.False(File.Exists(Path.Combine(f.Root, "restore-directory-plan.json")));
        Assert.True(File.Exists(Path.Combine(f.Root, "bounded-restore-delta-reclaim.bin")));
    }

    [Fact]
    public async Task UnchangedBoundedLibraryPreservesForeignInventoryAfterMove()
    {
        using var f = new RestoreFixture(2, true, unchangedDirectory: true, bounded: true);
        await Assert.ThrowsAsync<IOException>(() => f.Restore(at => { if (at == "directory.reclaim.journal-removed") throw new IOException("stop"); }));
        string path = Path.Combine(f.Request.DeltaPath, "foreign"); File.WriteAllText(path, "retain");
        await Assert.ThrowsAsync<WorkspaceException>(() => f.Restore());
        Assert.Equal("retain", File.ReadAllText(path)); Assert.True(Directory.Exists(f.State.OriginalPath));
        File.Delete(path); await f.Restore(); f.AssertRestored();
    }

    [Fact]
    public void PlatformProofSelectsOnlyAnExistingUnambiguousMarker()
    {
        if (!OperatingSystem.IsWindows()) Assert.Skip("Windows ancestry proof.");
        using var root = new TestDirectory(); string delta = Path.Combine(root.Path, "delta"); Directory.CreateDirectory(delta);
        Assert.Throws<WorkspaceException>(() => new WindowsJournalDirectoryGuard(delta));
        Assert.Empty(Directory.GetFileSystemEntries(delta));
        string marker = Path.Combine(delta, BoundedJournalReclamation.GuardName); File.WriteAllBytes(marker, []);
        using (var guard = new WindowsJournalDirectoryGuard(delta))
        {
            guard.EnsureFile(marker); Assert.Throws<IOException>(() => File.Delete(marker));
            Assert.Throws<IOException>(() => Directory.Move(delta, delta + "-moved"));
        }
        File.WriteAllBytes(Path.Combine(delta, "owner.lock"), []);
        Assert.Throws<WorkspaceException>(() => new WindowsJournalDirectoryGuard(delta));
        Assert.Equal(2, Directory.GetFiles(delta).Length);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GeneralRestoreRejectsAReceiptFromTheOtherJournalMode(bool bounded)
    {
        using var f = new RestoreFixture(2, true, rename: true, bounded: bounded);
        string receipt = Path.Combine(f.Root, bounded ? "latest-restore-delta-reclaim.json" : "bounded-restore-delta-reclaim.bin");
        File.WriteAllText(receipt, "unrelated receipt must be preserved");
        Assert.Equal("UnownedDeltaFiles", (await Assert.ThrowsAsync<WorkspaceException>(() => f.Restore())).Code);
        Assert.Equal("unrelated receipt must be preserved", File.ReadAllText(receipt));
        Assert.True(File.Exists(Path.Combine(f.Request.DeltaPath, "delta.journal")));
        Assert.True(File.Exists(Path.Combine(f.Request.DeltaPath, "identity.json")));
        Assert.True(File.Exists(Path.Combine(f.State.OriginalPath, "renamed", "f0000")));
    }

    [Theory]
    [InlineData("latest")]
    [InlineData("unknown")]
    [InlineData("previous-version")]
    public async Task DirectoryPlanCannotChangeItsRecordedModeOrMigrateAnOldVersion(string change)
    {
        using var f = new RestoreFixture(2, true, unchangedDirectory: true, bounded: true);
        await Assert.ThrowsAsync<IOException>(() => f.Restore(at => { if (at == "directory.after-plan") throw new IOException("stop"); }));
        string path = Path.Combine(f.Root, "restore-directory-plan.json"); var plan = JsonNode.Parse(File.ReadAllText(path))!;
        if (change == "previous-version") plan["version"] = 1; else plan["journalMode"] = change;
        File.WriteAllText(path, plan.ToJsonString());
        await Assert.ThrowsAsync<WorkspaceException>(() => f.Restore());
        Assert.True(Directory.Exists(f.State.Basis.Snapshot.RootPath));
        Assert.True(File.Exists(Path.Combine(f.Request.DeltaPath, "delta.journal")));
        Assert.False(Directory.Exists(f.State.OriginalPath));
    }

    [Fact]
    public void CorruptReceiptCannotAuthorizeACompletedOrPendingReclaim()
    {
        using var root = new TestDirectory(); var f = Populate(root.Path);
        Assert.Throws<IOException>(() => Reclaim(f, at => { if (at == "intent-flushed") throw new IOException("stop"); }));
        byte[] bytes = File.ReadAllBytes(f.Receipt + ".pending"); bytes[0] ^= 1; File.WriteAllBytes(f.Receipt + ".pending", bytes);
        Assert.Throws<WorkspaceException>(() => Reclaim(f));
        Assert.True(File.Exists(Path.Combine(f.Directory, "delta.journal"))); Assert.True(File.Exists(Path.Combine(f.Directory, "identity.json")));
        Assert.Equal(bytes, File.ReadAllBytes(f.Receipt + ".pending"));
    }

    private sealed record Fixture(string Directory, string Receipt, SnapshotDeltaSeal Seal, OverlayIdentity Identity,
        SnapshotOverlayTests.FakeSnapshotTree Tree);
    private static Fixture Populate(string root)
    {
        Directory.CreateDirectory(root);
        string delta = Path.Combine(root, "delta"); var tree = new SnapshotOverlayTests.FakeSnapshotTree();
        var identity = new OverlayIdentity(2, "bounded-reclaim", tree.Snapshot, 4 * 1048576, 65536);
        using var store = new JournalDeltaStore(delta, identity, tree, boundedLogBytes: 8192);
        Guid id = store.Create("result", false, 32).Id; store.Write(id, 0, [1, 2, 3]);
        return new(delta, Path.Combine(root, "reclaim.bin"), store.Seal(), identity, tree);
    }
    private static void Reclaim(Fixture fixture, Action<string>? boundary = null) =>
        BoundedJournalReclamation.ReclaimDirectory(fixture.Directory, fixture.Seal, ["identity.json"], fixture.Receipt, boundary,
            OperatingSystem.IsWindows() ? path => new WindowsJournalDirectoryGuard(path, BoundedJournalReclamation.GuardName) : null);
}
