using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Splinter.Core;
using Splinter.Platform.Windows;
using Splinter.Runtime;

namespace Splinter.Tests;

public sealed class RootDeltaDirectoryRestoreTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ChangedRangesLengthsAndNewLeavesKeepOriginalIdentitiesWithOneRootMove(bool bounded)
    {
        using var f = new RootDeltaRestoreFixture(bounded);
        var stages = new List<string>();
        await f.Restore(stages.Add);
        f.AssertRestored();
        Assert.Equal(1, stages.Count(s => s == "root-delta.after-move"));
        Assert.DoesNotContain("file.after-move", stages);
        Assert.True(File.Exists(f.PlanPath));
        Assert.False(File.Exists(Path.Combine(f.Root, "restore-plan.json")));
        f.AllowCleanup();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeleteAndCreateAtTheSamePathDoesNotReuseTheDeletedFileIdentity(bool bounded)
    {
        using var f = new RootDeltaRestoreFixture(bounded, "replacement");
        string before = RootDeltaRestoreFixture.Identity(f.BasisFile(RootDeltaRestoreFixture.FileName(7)));
        await f.Restore(); f.AssertRestored();
        Assert.NotEqual(before, RootDeltaRestoreFixture.Identity(f.Target(RootDeltaRestoreFixture.FileName(7))));
        f.AllowCleanup();
    }

    [Theory]
    [InlineData("root-delta.after-plan")]
    [InlineData("root-delta.root-unpinned")]
    [InlineData("root-delta.before-move")]
    [InlineData("root-delta.after-move")]
    [InlineData("root-delta.after-proof")]
    [InlineData("root-delta.before-stage")]
    [InlineData("root-delta.after-stage")]
    [InlineData("root-delta.file.before-create")]
    [InlineData("root-delta.file.after-create-receipt")]
    [InlineData("root-delta.file.after-write-intent")]
    [InlineData("root-delta.file.before-ranges-write")]
    [InlineData("root-delta.file.after-range-write")]
    [InlineData("root-delta.file.after-ranges-flush")]
    [InlineData("root-delta.file.after-complete")]
    [InlineData("root-delta.after-verified")]
    [InlineData("root-delta.reclaim.intent")]
    [InlineData("root-delta.reclaim.file")]
    [InlineData("root-delta.journal.intent")]
    [InlineData("root-delta.journal.journal-removed")]
    [InlineData("root-delta.after-reclaim")]
    public async Task ExactIntentResumesEveryPublishedMoveWriteAndReclaimBoundary(string boundary)
    {
        foreach (bool bounded in new[] { false, true })
        {
            using var f = new RootDeltaRestoreFixture(bounded);
            await f.Interrupt(boundary); await f.Restore(); f.AssertRestored(); f.AllowCleanup();
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InterruptedPrivateRangeIsRedoneWithoutRereadingTheMutatedBasis(bool bounded)
    {
        using var f = new RootDeltaRestoreFixture(bounded, "large-range", payloadBytes: 262144);
        string name = RootDeltaRestoreFixture.FileName(0);
        byte[] before = File.ReadAllBytes(f.BasisFile(name));
        await f.Interrupt("root-delta.file.after-range-write");
        byte[] partial = File.ReadAllBytes(f.Target(name));
        Assert.NotEqual(before, partial); Assert.NotEqual(f.ExpectedBytes[name], partial);
        Assert.Equal(before.AsSpan(0, 17).ToArray(), partial.AsSpan(0, 17).ToArray());
        f.AssertJournalPreserved(); await f.Restore(); f.AssertRestored(); f.AllowCleanup();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RetainedByteChangeBlocksRedoBeforeReadOnlyAttributesAreCleared(bool afterPartialWrite)
    {
        using var f = new RootDeltaRestoreFixture(true, "range");
        await f.Interrupt(afterPartialWrite ? "root-delta.file.after-range-write" : "root-delta.after-move");
        string path = f.Target(RootDeltaRestoreFixture.FileName(0));
        byte[] bytes = File.ReadAllBytes(path); bytes[0] ^= 1;
        File.SetAttributes(path, FileAttributes.Normal); File.WriteAllBytes(path, bytes); File.SetAttributes(path, FileAttributes.ReadOnly);
        await Assert.ThrowsAsync<WorkspaceException>(() => f.Restore());
        Assert.Equal(bytes, File.ReadAllBytes(path)); Assert.Equal(FileAttributes.ReadOnly, File.GetAttributes(path));
        f.AssertJournalPreserved(); f.AllowCleanup();
    }

    [Fact]
    public async Task CompletedPrivateBytesCannotBeSilentlyRedoneAfterTheirReceipt()
    {
        using var f = new RootDeltaRestoreFixture(true, "range");
        await f.Interrupt("root-delta.file.after-complete");
        string path = f.Target(RootDeltaRestoreFixture.FileName(0));
        byte[] bytes = File.ReadAllBytes(path); bytes[18] ^= 1; // Inside the private range, not the retained proof.
        File.SetAttributes(path, FileAttributes.Normal); File.WriteAllBytes(path, bytes);
        await Assert.ThrowsAsync<WorkspaceException>(() => f.Restore());
        Assert.Equal(bytes, File.ReadAllBytes(path)); f.AssertJournalPreserved(); f.AllowCleanup();
    }

    [Fact]
    public async Task PrivateRangeChangeBeforeWriteIntentIsNotMistakenForOwnedPartialWork()
    {
        using var f = new RootDeltaRestoreFixture(true, "range"); await f.Interrupt("root-delta.after-move");
        string path = f.Target(RootDeltaRestoreFixture.FileName(0)); byte[] bytes = File.ReadAllBytes(path); bytes[18] ^= 1;
        File.SetAttributes(path, FileAttributes.Normal); File.WriteAllBytes(path, bytes); File.SetAttributes(path, FileAttributes.ReadOnly);
        await Assert.ThrowsAsync<WorkspaceException>(() => f.Restore());
        Assert.Equal(bytes, File.ReadAllBytes(path)); Assert.Equal(FileAttributes.ReadOnly, File.GetAttributes(path));
        f.AssertJournalPreserved(); f.AllowCleanup();
    }

    [Fact]
    public async Task TruncateThenExtendResumesFromTheAdmittedShortIntermediateLength()
    {
        using var f = new RootDeltaRestoreFixture(true);
        // f00000 and f00001 precede the truncate-then-extend f00002.
        await f.Interrupt("root-delta.file.after-write-intent", occurrence: 3);
        string path = f.Target(RootDeltaRestoreFixture.FileName(2));
        using (var file = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.None))
        { file.SetLength(17); file.Flush(true); }
        await f.Restore(); f.AssertRestored(); f.AllowCleanup();
    }

    [Theory]
    [InlineData("root-delta.after-move", 0)]
    [InlineData("root-delta.after-move", 3)]
    [InlineData("root-delta.file.after-complete", 0)]
    [InlineData("root-delta.after-verified", 0)]
    public async Task MatchingByteReplacementOfAnExistingFileIsPreserved(string boundary, int index)
    {
        using var f = new RootDeltaRestoreFixture(true, index == 3 ? "mixed" : "range"); await f.Interrupt(boundary);
        string path = f.Target(RootDeltaRestoreFixture.FileName(index)); byte[] bytes = File.ReadAllBytes(path);
        File.Move(path, Path.Combine(f.Root, "preserved-original-file")); File.WriteAllBytes(path, bytes);
        string foreign = RootDeltaRestoreFixture.Identity(path);
        await Assert.ThrowsAsync<WorkspaceException>(() => f.Restore());
        Assert.Equal(foreign, RootDeltaRestoreFixture.Identity(path)); Assert.Equal(bytes, File.ReadAllBytes(path));
        f.AssertJournalPreserved(); f.AllowCleanup();
    }

    [Theory]
    [InlineData("foreign-file")]
    [InlineData("foreign-directory")]
    [InlineData("second-basis")]
    [InlineData("changed-directory-identity")]
    public async Task RootMoveGapDoesNotAbsorbForeignTopology(string change)
    {
        using var f = new RootDeltaRestoreFixture(true);
        bool injected = false;
        await Assert.ThrowsAsync<WorkspaceException>(() => f.Restore(stage =>
        {
            if (stage != "root-delta.after-move") return; injected = true;
            if (change == "foreign-file") File.WriteAllText(f.Target("foreign"), "preserve");
            else if (change == "foreign-directory") Directory.CreateDirectory(f.Target("foreign"));
            else if (change == "second-basis") Directory.CreateDirectory(f.State.Basis.Snapshot.RootPath);
            else
            {
                string saved = Path.Combine(f.Root, "preserved-files"); Directory.Move(f.Target("files"), saved); Directory.CreateDirectory(f.Target("files"));
                foreach (string path in Directory.GetFiles(saved)) File.Move(path, f.Target("files/" + Path.GetFileName(path)));
            }
        }));
        Assert.True(injected); f.AssertJournalPreserved();
        Assert.False(File.Exists(Path.Combine(f.Root, "restore-root-delta-verified.json")));
        Assert.True(File.Exists(f.Target(RootDeltaRestoreFixture.FileName(5))));
        if (change == "foreign-file") Assert.Equal("preserve", File.ReadAllText(f.Target("foreign")));
        f.AllowCleanup();
    }

    [Theory]
    [InlineData("root-delta.file.after-create-before-receipt")]
    [InlineData("root-delta.file.after-create-receipt")]
    public async Task NewLeafWithoutReceiptOrWithForeignIdentityIsPreserved(string boundary)
    {
        using var f = new RootDeltaRestoreFixture(true); await f.Interrupt(boundary);
        string path = f.Target("files/new");
        if (boundary.EndsWith("after-create-receipt", StringComparison.Ordinal)) File.Move(path, Path.Combine(f.Root, "preserved-created-file"));
        File.WriteAllBytes(path, [77]); string foreign = RootDeltaRestoreFixture.Identity(path);
        await Assert.ThrowsAsync<WorkspaceException>(() => f.Restore());
        Assert.Equal(new byte[] { 77 }, File.ReadAllBytes(path)); Assert.Equal(foreign, RootDeltaRestoreFixture.Identity(path));
        f.AssertJournalPreserved(); f.AllowCleanup();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NewLeafInAnExistingEmptyDirectoryPreservesOrdinaryCreationAndMetadata(bool bounded)
    {
        using var f = new RootDeltaRestoreFixture(bounded, "empty-parent");
        await f.Restore(); f.AssertRestored(); f.AllowCleanup();
    }

    [Theory]
    [InlineData("junction")]
    [InlineData("occupied-target")]
    public async Task NewLeafPublicationIsRelativeAndExclusiveAtThePinnedParentBoundary(string change)
    {
        using var f = new RootDeltaRestoreFixture(true, "empty-parent");
        string outside = RootTopologyDirectoryRestoreTests.PrepareOutside(f.DirectoryPath);
        string outsideBefore = RootTopologyDirectoryRestoreTests.OutsideInventory(outside);
        Microsoft.Win32.SafeHandles.SafeFileHandle? injected = null;
        int hits = 0, error = int.MinValue; string? parentIdentity = null, foreignIdentity = null;
        string parent = f.Target("empty-parent"), target = f.Target("empty-parent/new");
        Exception? failure;
        try
        {
            failure = await Record.ExceptionAsync(() => f.Restore(stage =>
            {
                if (stage != "root-delta.file.before-create" || hits != 0) return; hits++;
                Assert.Empty(Directory.GetFileSystemEntries(parent)); parentIdentity = RootDeltaRestoreFixture.Identity(parent, true);
                if (change == "junction")
                {
                    error = RootTopologyDirectoryRestoreTests.InjectJunctionKeepingExactHandle(parent, outside, 0x100, out injected);
                    TestContext.Current.TestOutputHelper?.WriteLine($"A existing empty parent; access=0x100; junction result={error}.");
                }
                else { File.WriteAllBytes(target, [77]); foreignIdentity = RootDeltaRestoreFixture.Identity(target); }
            }));
        }
        finally
        {
            if (injected is not null)
            { try { RootTopologyDirectoryRestoreTests.ClearJunction(injected); } finally { injected.Dispose(); } }
        }
        Assert.Equal(1, hits); if (change == "junction") Assert.Equal(0, error);
        Assert.IsType<WorkspaceException>(failure);
        Assert.Equal(outsideBefore, RootTopologyDirectoryRestoreTests.OutsideInventory(outside));
        Assert.Equal(parentIdentity, RootDeltaRestoreFixture.Identity(parent, true));
        Assert.Empty(Directory.GetFiles(f.Root, "restore-root-delta-file-*.json"));
        Assert.Empty(Directory.GetFiles(f.Root, "restore-root-delta-write-*.json"));
        Assert.False(File.Exists(Path.Combine(f.Root, "restore-root-delta-verified.json"))); f.AssertJournalPreserved();
        foreach (var item in f.Manifest.Items.Where(item => !item.Entry.Directory))
            Assert.Equal(item.Hash, RootDeltaRestoreFixture.Hash(File.ReadAllBytes(f.Target(item.Entry.Path))));
        if (File.Exists(target))
        {
            byte[] bytes = File.ReadAllBytes(target); string identity = RootDeltaRestoreFixture.Identity(target);
            if (change == "occupied-target") { Assert.Equal(new byte[] { 77 }, bytes); Assert.Equal(foreignIdentity, identity); }
            await Assert.ThrowsAsync<WorkspaceException>(() => f.Restore());
            Assert.Equal(bytes, File.ReadAllBytes(target)); Assert.Equal(identity, RootDeltaRestoreFixture.Identity(target));
        }
        f.AllowCleanup();
    }

    [Theory]
    [InlineData("replace")]
    [InlineData("missing")]
    [InlineData("unknown")]
    [InlineData("duplicate-original")]
    public async Task DeletedLeafStagingRequiresItsExactRecordedIdentityAndMembership(string change)
    {
        using var f = new RootDeltaRestoreFixture(true); await f.Interrupt("root-delta.after-stage");
        string staged = Assert.Single(Directory.GetFiles(f.Root, "restore-root-delta-leaf-*.bin"));
        if (change is "replace" or "missing")
        {
            byte[] bytes = File.ReadAllBytes(staged); File.Move(staged, Path.Combine(f.Root, "preserved-stage"));
            if (change == "replace") File.WriteAllBytes(staged, bytes);
        }
        else if (change == "unknown") File.WriteAllText(Path.Combine(f.Root, "restore-root-delta-leaf-unknown.bin"), "foreign");
        else File.WriteAllText(f.Target(RootDeltaRestoreFixture.FileName(5)), "foreign");
        await Assert.ThrowsAsync<WorkspaceException>(() => f.Restore()); f.AssertJournalPreserved();
        Assert.False(File.Exists(Path.Combine(f.Root, "restore-root-delta-staging-reclaim.json")));
        f.AllowCleanup();
    }

    [Fact]
    public async Task FinalTreeChangeAfterOneStagedDeletionPreservesRemainingStagedData()
    {
        using var f = new RootDeltaRestoreFixture(true); await f.Interrupt("root-delta.reclaim.file");
        string staged = Assert.Single(Directory.GetFiles(f.Root, "restore-root-delta-leaf-*.bin"));
        string survivor = f.Target(RootDeltaRestoreFixture.FileName(8)); File.WriteAllBytes(survivor, [77]);
        await Assert.ThrowsAsync<WorkspaceException>(() => f.Restore());
        Assert.True(File.Exists(staged)); Assert.Equal(new byte[] { 77 }, File.ReadAllBytes(survivor)); f.AssertJournalPreserved(); f.AllowCleanup();
    }

    [Theory]
    [InlineData("seal")]
    [InlineData("plan-hash")]
    [InlineData("missing-binding-after-move")]
    [InlineData("general-plan")]
    [InlineData("sparse-plan")]
    public async Task ChangedSealPlanBindingOrConflictingModeIsPreserved(string change)
    {
        using var f = new RootDeltaRestoreFixture(true);
        await f.Interrupt(change == "missing-binding-after-move" ? "root-delta.after-move" : "root-delta.after-plan");
        if (change == "seal") f.Mutate(j => j.Write(j.Stat(RootDeltaRestoreFixture.FileName(8))!.Id, 0, [99]));
        else if (change == "plan-hash") File.AppendAllText(f.PlanPath, " ");
        else if (change == "missing-binding-after-move") File.Move(Path.Combine(f.Root, "restore-root-delta-intent.json"), Path.Combine(f.Root, "preserved-intent.json"));
        else File.WriteAllText(Path.Combine(f.Root, change == "general-plan" ? "restore-plan.json" : "restore-sparse-directory-plan.json"), "{}");
        await Assert.ThrowsAsync<WorkspaceException>(() => f.Restore()); Assert.True(File.Exists(f.PlanPath)); f.AssertJournalPreserved(); f.AllowCleanup();
    }

    [Theory]
    [InlineData("metadata")]
    [InlineData("range")]
    [InlineData("length")]
    [InlineData("deleted")]
    [InlineData("replaced-seal")]
    public async Task PlanEffectsMustExactlyMatchTheJournalEvenWithAnUpdatedPlanBinding(string change)
    {
        using var f = new RootDeltaRestoreFixture(true); await f.Interrupt("root-delta.after-plan");
        var plan = JsonNode.Parse(File.ReadAllText(f.PlanPath))!;
        var files = plan["files"]!.AsArray();
        var delta = files.Single(n => n!["delta"]!["entry"]!["path"]!.GetValue<string>() == RootDeltaRestoreFixture.FileName(0))!["delta"]!;
        if (change == "metadata") delta["entry"]!["lastWriteTime"] = delta["entry"]!["lastWriteTime"]!.GetValue<long>() + 1;
        else if (change == "range") delta["ranges"]![0]!["offset"] = delta["ranges"]![0]!["offset"]!.GetValue<long>() + 1;
        else if (change == "length") delta["entry"]!["length"] = delta["entry"]!["length"]!.GetValue<long>() + 1;
        else if (change == "deleted")
        {
            var removed = files.Single(n => n!["delta"]!["entry"]!["path"]!.GetValue<string>() == RootDeltaRestoreFixture.FileName(8));
            files.Remove(removed);
            var deleted = plan["deleted"]!.AsArray().Select(n => n!.GetValue<string>()).Append(RootDeltaRestoreFixture.FileName(8)).Order(StringComparer.Ordinal);
            plan["deleted"] = new JsonArray(deleted.Select(p => (JsonNode?)JsonValue.Create(p)).ToArray());
        }
        else
        {
            SnapshotDeltaSeal changed = f.Seal;
            f.Mutate(j => { j.Write(j.Stat(RootDeltaRestoreFixture.FileName(8))!.Id, 0, [99]); changed = j.Seal(); });
            plan["seal"] = JsonSerializer.SerializeToNode(changed, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        }
        File.WriteAllText(f.PlanPath, plan.ToJsonString());
        string binding = Path.Combine(f.Root, "restore-root-delta-intent.json");
        var intent = JsonNode.Parse(File.ReadAllText(binding))!;
        intent["planHash"] = ManagedDirectoryBasis.HashFile(f.PlanPath); File.WriteAllText(binding, intent.ToJsonString());
        byte[] fixedPlan = File.ReadAllBytes(f.PlanPath), fixedBinding = File.ReadAllBytes(binding);
        await Assert.ThrowsAsync<WorkspaceException>(() => f.Restore());
        Assert.Equal(fixedPlan, File.ReadAllBytes(f.PlanPath)); Assert.Equal(fixedBinding, File.ReadAllBytes(binding));
        Assert.False(Directory.Exists(f.State.OriginalPath)); Assert.True(File.Exists(f.BasisFile(RootDeltaRestoreFixture.FileName(8))));
        f.AssertJournalPreserved(); f.AllowCleanup();
    }

    [Fact]
    public async Task MissingBindingCanBeReconstructedOnlyWhileEveryOriginalByteStillExists()
    {
        using var f = new RootDeltaRestoreFixture(true); await f.Interrupt("root-delta.after-plan");
        File.Move(Path.Combine(f.Root, "restore-root-delta-intent.json"), Path.Combine(f.Root, "preserved-intent.json"));
        await f.Restore(); f.AssertRestored(); Assert.True(File.Exists(Path.Combine(f.Root, "restore-root-delta-intent.json"))); f.AllowCleanup();
    }

    [Fact]
    public async Task PartialPrivateWriteWithoutItsDurableWriteIntentIsPreserved()
    {
        using var f = new RootDeltaRestoreFixture(true, "large-range", payloadBytes: 262144);
        await f.Interrupt("root-delta.file.after-range-write");
        string receipt = Assert.Single(Directory.GetFiles(f.Root, "restore-root-delta-write-*.json"));
        File.Move(receipt, Path.Combine(f.Root, "preserved-write-intent.json"));
        string target = f.Target(RootDeltaRestoreFixture.FileName(0)); byte[] partial = File.ReadAllBytes(target);
        await Assert.ThrowsAsync<WorkspaceException>(() => f.Restore());
        Assert.Equal(partial, File.ReadAllBytes(target)); f.AssertJournalPreserved(); f.AllowCleanup();
    }

    [Theory]
    [InlineData("restore-root-delta-intent.json")]
    [InlineData("restore-root-delta-verified.json")]
    [InlineData("restore-root-delta-staging-reclaim.json")]
    [InlineData("restore-root-delta-leaf-00000000.bin")]
    [InlineData("restore-root-delta-file-00000000000000000000000000000001.json")]
    [InlineData("restore-root-delta-write-00000000000000000000000000000001.json")]
    [InlineData("restore-root-delta-complete-00000000000000000000000000000001.json")]
    public async Task OrphanRecordsCannotSelectOrInitializeAnotherRestoreMode(string name)
    {
        using var f = new RootDeltaRestoreFixture(true); string path = Path.Combine(f.Root, name); File.WriteAllText(path, "foreign");
        await Assert.ThrowsAsync<WorkspaceException>(() => f.Restore());
        Assert.Equal("foreign", File.ReadAllText(path)); Assert.True(Directory.Exists(f.State.Basis.Snapshot.RootPath));
        Assert.False(Directory.Exists(f.State.OriginalPath)); f.AssertJournalPreserved(); f.AllowCleanup();
    }

    [Fact]
    public async Task PlanJournalAndActiveFileStayPinnedDuringMutationAndReclamation()
    {
        using var f = new RootDeltaRestoreFixture(true); int checkedJournal = 0, checkedFile = 0;
        await f.Restore(stage =>
        {
            if (stage is "root-delta.after-proof" or "root-delta.reclaim.intent")
            {
                foreach (string path in new[] { f.PlanPath, Path.Combine(f.Request.DeltaPath, "delta.journal") })
                    Assert.Throws<IOException>(() => { using var write = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete); });
                checkedJournal++;
            }
            if (stage == "root-delta.file.before-ranges-write" && checkedFile == 0)
            {
                Assert.Throws<IOException>(() => File.Move(f.Target(RootDeltaRestoreFixture.FileName(0)), Path.Combine(f.Root, "foreign-replacement")));
                checkedFile++;
            }
        });
        Assert.Equal(2, checkedJournal); Assert.Equal(1, checkedFile); f.AssertRestored(); f.AllowCleanup();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancelledCompletionPublicationPreservesItsTemporaryAndResumesWithFreshCancellation(bool bounded)
    {
        using var f = new RootDeltaRestoreFixture(bounded, "range");
        using var cancelled = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        bool interrupted = false;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ManagedDirectoryRestore.ExecuteAsync(f.State, f.Request, cancelled.Token, stage =>
        { if (stage == "root-delta.file.after-ranges-flush") { interrupted = true; cancelled.Cancel(); } }));
        Assert.True(interrupted);
        string temporary = Assert.Single(Directory.GetFiles(f.Root, "restore-root-delta-complete-*.json.*.tmp"));
        byte[] before = File.ReadAllBytes(temporary); string identity = RootDeltaRestoreFixture.Identity(temporary);
        f.AssertJournalPreserved(); await f.Restore(); f.AssertRestored();
        Assert.Equal(before, File.ReadAllBytes(temporary)); Assert.Equal(identity, RootDeltaRestoreFixture.Identity(temporary)); f.AllowCleanup();
    }

    [Fact]
    public async Task UnpublishedPlanTemporaryIsNeverAdoptedOrDeletedWhenStartingFresh()
    {
        using var f = new RootDeltaRestoreFixture(true);
        string temporary = f.PlanPath + ".0123456789abcdef0123456789abcdef.tmp";
        File.WriteAllText(temporary, "{unpublished partial plan"); byte[] before = File.ReadAllBytes(temporary);
        string identity = RootDeltaRestoreFixture.Identity(temporary);
        await f.Restore(); f.AssertRestored();
        Assert.Equal(before, File.ReadAllBytes(temporary)); Assert.Equal(identity, RootDeltaRestoreFixture.Identity(temporary)); f.AllowCleanup();
    }

    [Theory]
    [InlineData("unknown-stem")]
    [InlineData("bad-guid")]
    [InlineData("directory")]
    [InlineData("reparse")]
    public async Task TemporaryLookingForeignArtifactsAreStillPreservedAndRefused(string change)
    {
        using var f = new RootDeltaRestoreFixture(true);
        string path = Path.Combine(f.Root, change == "unknown-stem" ? "restore-root-delta-foreign.json.0123456789abcdef0123456789abcdef.tmp" :
            change == "bad-guid" ? "restore-root-delta-plan.json.invalid.tmp" : "restore-root-delta-plan.json.0123456789abcdef0123456789abcdef.tmp");
        if (change is "directory" or "reparse") Directory.CreateDirectory(path); else File.WriteAllText(path, "foreign");
        if (change == "reparse")
        { string outside = Path.Combine(f.DirectoryPath, "outside"); Directory.CreateDirectory(outside); File.WriteAllText(Path.Combine(outside, "marker"), "preserve"); OwnedDirectoryLifetimeTests.SetJunction(path, outside); }
        try
        {
            await Assert.ThrowsAsync<WorkspaceException>(() => f.Restore()); f.AssertJournalPreserved();
            Assert.True(Directory.Exists(f.State.Basis.Snapshot.RootPath)); Assert.False(Directory.Exists(f.State.OriginalPath));
            if (change == "reparse") Assert.Equal("preserve", File.ReadAllText(Path.Combine(f.DirectoryPath, "outside", "marker")));
            else if (change != "directory") Assert.Equal("foreign", File.ReadAllText(path));
        }
        finally { if (change == "reparse" && Directory.Exists(path)) Directory.Delete(path); }
        f.AllowCleanup();
    }
}

// Shared by the opt-in cost fixture. Uses only existing product APIs so its
// test assembly can run against a separately frozen old product distribution.
internal sealed class RootDeltaRestoreFixture : IDisposable
{
    private readonly TestDirectory directory = new();
    private readonly bool bounded;
    private readonly int payloadBytes;
    private bool allowCleanup;
    private readonly Dictionary<string, string> originalIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, byte[]> expectedBytes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DeltaEntry> expectedEntries = new(StringComparer.OrdinalIgnoreCase);
    internal static readonly DateTime FixedTime = new(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
    internal static long FinalTime => FixedTime.AddDays(1).ToFileTimeUtc();
    internal string Root => State.Basis.ReceiptDirectory;
    internal string DirectoryPath => directory.Path;
    internal string PlanPath => Path.Combine(Root, "restore-root-delta-plan.json");
    internal ManagedDirectoryState State { get; }
    internal OverlayServeRequest Request { get; }
    internal OwnedDirectoryManifest Manifest { get; }
    internal SnapshotDeltaSeal Seal { get; private set; } = null!;
    internal IReadOnlyDictionary<string, DeltaEntry> ExpectedEntries => expectedEntries;
    internal IReadOnlyDictionary<string, byte[]> ExpectedBytes => expectedBytes;
    internal string InputHash { get; }
    internal string ExpectedHash { get; private set; } = "";
    internal static string FileName(int index) => $"files/f{index:D5}";
    internal string Target(string path) => Path.Combine(State.OriginalPath, path.Replace('/', Path.DirectorySeparatorChar));
    internal string BasisFile(string path) => Path.Combine(State.Basis.Snapshot.RootPath, path.Replace('/', Path.DirectorySeparatorChar));

    internal RootDeltaRestoreFixture(bool bounded, string profile = "mixed", int count = 12, int payloadBytes = 4096)
    {
        if (!OperatingSystem.IsWindows()) Assert.Skip("Ordinary NTFS identity and root transfer contract.");
        if (count < 10 || payloadBytes < 1024) throw new ArgumentOutOfRangeException(nameof(count));
        this.bounded = bounded; this.payloadBytes = payloadBytes;
        string root = Path.Combine(directory.Path, "owned"), basis = Path.Combine(root, "base"), project = Path.Combine(directory.Path, "original");
        Directory.CreateDirectory(Path.Combine(basis, "files")); Directory.CreateDirectory(project);
        if (profile == "empty-parent") Directory.CreateDirectory(Path.Combine(basis, "empty-parent"));
        for (int i = 0; i < count; i++)
        {
            string path = Path.Combine(basis, FileName(i)); File.WriteAllBytes(path, OriginalBytes(i));
            File.SetCreationTimeUtc(path, FixedTime); File.SetLastAccessTimeUtc(path, FixedTime); File.SetLastWriteTimeUtc(path, FixedTime);
            File.SetAttributes(path, i == 0 ? FileAttributes.ReadOnly : FileAttributes.Normal);
        }
        foreach (string path in new[] { Path.Combine(basis, "files"), basis }.Concat(profile == "empty-parent" ? new[] { Path.Combine(basis, "empty-parent") } : Array.Empty<string>()))
        { Directory.SetCreationTimeUtc(path, FixedTime); Directory.SetLastAccessTimeUtc(path, FixedTime); Directory.SetLastWriteTimeUtc(path, FixedTime); }
        var snapshot = new SnapshotReference("012345678901234567890123", OwnedDirectorySnapshotTree.ProviderName, basis, basis,
            DateTimeOffset.UnixEpoch, true, true, "stopped-original-owned-directory");
        Manifest = OwnedDirectorySnapshotTree.Capture(snapshot, snapshot.Id, Path.Combine(root, OwnedDirectoryManifest.MetadataDirectoryName));
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        File.WriteAllText(Path.Combine(root, "manifest.json"), JsonSerializer.Serialize(Manifest, json));
        string hash = ManagedDirectoryBasis.HashFile(Path.Combine(root, "manifest.json"));
        File.WriteAllText(Path.Combine(root, "proof.json"), JsonSerializer.Serialize(new OwnedDirectoryProof(1, snapshot, snapshot.Id, hash,
            Manifest.Items.Sum(i => i.Entry.Length)), json));
        State = new() { ApplicationKind = "unity", DirectoryKind = "Library", Key = "root-delta", Phase = "restoring",
            OriginalPath = Path.Combine(project, "Library"), CompatibilityKey = "root-delta",
            Basis = new(snapshot.Id, project, "Library", snapshot, root, snapshot.Id, hash) };
        Request = new(new(2, snapshot.Id, snapshot, 64L * 1048576, 65536), Path.Combine(root, "delta"), State.OriginalPath,
            Path.Combine(root, "overlay"), "token", new(snapshot.Id, basis, hash), root, snapshot.Id);
        foreach (var item in Manifest.Items)
            originalIds.Add(item.Entry.Path, Identity(BasisFile(item.Entry.Path), item.Entry.Directory));
        Mutate(j =>
        {
            if (profile == "range") j.Write(j.Stat(FileName(0))!.Id, 17, [91, 92, 93, 94]);
            else if (profile == "large-range")
            {
                byte[] changed = Enumerable.Range(0, 131089).Select(i => (byte)(i * 7)).ToArray();
                for (int offset = 0; offset < changed.Length; offset += 65536)
                    j.Write(j.Stat(FileName(0))!.Id, 17 + offset, changed.AsSpan(offset, Math.Min(65536, changed.Length - offset)));
            }
            else if (profile == "cost")
            {
                for (int i = 0; i < 4; i++) j.Write(j.Stat(FileName(i))!.Id, 0, Enumerable.Repeat((byte)(80 + i), 1024).ToArray());
                j.SetLength(j.Stat(FileName(4))!.Id, 17);
                j.SetLength(j.Stat(FileName(5))!.Id, 2049); j.Write(j.Stat(FileName(5))!.Id, 2046, [51, 52, 53]);
                j.Delete(j.Stat(FileName(6))!.Id);
                Guid created = j.Create("files/new", false, 32).Id; j.Write(created, 0, [9]); j.SetLength(created, 2049); j.Write(created, 2048, [6]);
            }
            else if (profile == "empty-parent") j.Write(j.Create("empty-parent/new", false, 32).Id, 0, [9, 6]);
            else
            {
                j.Write(j.Stat(FileName(0))!.Id, 17, [91, 92, 93, 94]);
                j.SetLength(j.Stat(FileName(1))!.Id, 17);
                Guid regrown = j.Stat(FileName(2))!.Id; j.SetLength(regrown, 17); j.SetLength(regrown, payloadBytes + 17); j.Write(regrown, payloadBytes + 14, [5, 6, 7]);
                j.Write(j.Stat(FileName(3))!.Id, 0, Enumerable.Repeat((byte)77, payloadBytes).ToArray());
                j.SetLength(j.Stat(FileName(4))!.Id, 0);
                j.Delete(j.Stat(FileName(5))!.Id); j.Delete(j.Stat(FileName(6))!.Id);
                Guid created = j.Create("files/new", false, 32).Id; j.Write(created, 0, [9]); j.SetLength(created, 17); j.Write(created, 16, [6]);
                j.Create("files/new-empty", false, 32);
                if (profile == "replacement")
                {
                    j.Delete(j.Stat(FileName(7))!.Id);
                    j.Write(j.Create(FileName(7), false, 32).Id, 0, [44, 45, 46]);
                }
            }
            var changedPaths = profile == "empty-parent" ? new[] { "empty-parent/new" } : profile is "range" or "large-range" ? new[] { FileName(0) } :
                Enumerable.Range(0, profile == "cost" ? 6 : 5).Select(FileName).Append("files/new")
                    .Concat(profile == "cost" ? [] : new[] { "files/new-empty" })
                    .Concat(profile == "replacement" ? [FileName(7)] : []).ToArray();
            foreach (string path in changedPaths)
            {
                var entry = j.Stat(path)!;
                j.SetMetadata(entry.Id, entry.Path == FileName(0) ? 33u : 32u, FinalTime, FinalTime, FinalTime);
            }
            foreach (string path in new[] { "files", "" }.Concat(profile == "empty-parent" ? new[] { "empty-parent" } : Array.Empty<string>()))
            { var entry = j.Stat(path)!; j.SetMetadata(entry.Id, entry.Attributes, FinalTime, FinalTime, FinalTime); }
        });
        InputHash = Hash(JsonSerializer.SerializeToUtf8Bytes(new { profile, count, payloadBytes, Manifest.Items }, json));
        CaptureExpected();
    }

    private byte[] OriginalBytes(int index) => Enumerable.Range(0, payloadBytes).Select(i => (byte)(index * 13 + i * 17)).ToArray();
    internal void Mutate(Action<JournalDeltaStore> action)
    {
        using var tree = new OwnedDirectorySnapshotTree(Manifest, State.Basis.Snapshot);
        using var journal = new JournalDeltaStore(Request.DeltaPath, Request.Identity, tree,
            optimizeJournal: !bounded, boundedLogBytes: bounded ? 1048576 : null);
        action(journal);
    }
    private void CaptureExpected()
    {
        using var tree = new OwnedDirectorySnapshotTree(Manifest, State.Basis.Snapshot);
        using var journal = JournalDeltaStore.OpenReadOnly(Request.DeltaPath, Request.Identity, tree);
        Seal = journal.Seal(); Assert.True(Seal.DurableSequence > 0);
        foreach (var delta in journal.DescribeRestore())
        {
            var entry = delta.Entry; expectedEntries.Add(entry.Path, entry);
            if (entry.Directory) continue;
            byte[] bytes = new byte[checked((int)entry.Length)]; int offset = 0;
            while (offset < bytes.Length)
            {
                int read = journal.Read(entry.Id, offset, bytes.AsSpan(offset, Math.Min(Request.Identity.MaxTransferBytes, bytes.Length - offset)));
                Assert.True(read > 0); offset += read;
            }
            expectedBytes.Add(entry.Path, bytes);
        }
        ExpectedHash = Hash(JsonSerializer.SerializeToUtf8Bytes(expectedEntries.Values.OrderBy(e => e.Path, StringComparer.Ordinal)
            .Select(e => new { e.Path, e.Directory, e.Length, e.Attributes, e.CreationTime, e.LastAccessTime, e.LastWriteTime,
                hash = e.Directory ? null : Hash(expectedBytes[e.Path]) })));
    }
    internal Task Restore(Action<string>? boundary = null) => ManagedDirectoryRestore.ExecuteAsync(State, Request,
        TestContext.Current.CancellationToken, boundary);
    internal async Task Interrupt(string stage, int occurrence = 1)
    {
        int observed = 0;
        await Assert.ThrowsAsync<IOException>(() => Restore(value =>
        { if (value == stage && ++observed == occurrence) throw new IOException("Fixed root-delta interruption: " + stage); }));
        Assert.Equal(occurrence, observed);
    }
    internal void AssertRestored(bool rootMoved = true)
    {
        foreach (var entry in expectedEntries.Values)
        {
            string path = Target(entry.Path);
            Assert.Equal(entry.Attributes, (uint)File.GetAttributes(path));
            Assert.Equal(entry.CreationTime, File.GetCreationTimeUtc(path).ToFileTimeUtc());
            Assert.Equal(entry.LastAccessTime, File.GetLastAccessTimeUtc(path).ToFileTimeUtc());
            Assert.Equal(entry.LastWriteTime, File.GetLastWriteTimeUtc(path).ToFileTimeUtc());
            if (entry.BasePath is { } basisPath && (!entry.Directory || rootMoved))
                Assert.Equal(originalIds[basisPath], Identity(path, entry.Directory));
            if (!entry.Directory)
            { Assert.Equal(entry.Length, new FileInfo(path).Length); Assert.Equal(expectedBytes[entry.Path], File.ReadAllBytes(path)); }
        }
        Assert.Equal(expectedEntries.Keys.Order(StringComparer.Ordinal),
            Directory.EnumerateFileSystemEntries(State.OriginalPath, "*", SearchOption.AllDirectories)
                .Select(p => Path.GetRelativePath(State.OriginalPath, p).Replace('\\', '/')).Prepend("").Order(StringComparer.Ordinal));
        Assert.False(Directory.Exists(State.Basis.Snapshot.RootPath)); Assert.False(Directory.Exists(Request.DeltaPath));
        Assert.Empty(Directory.GetFiles(Root, "restore-root-delta-leaf-*.bin"));
        Assert.Equal(0, State.InitialPayloadCopiedBytes);
    }
    internal void AssertJournalPreserved()
    { Assert.True(Directory.Exists(Request.DeltaPath)); Assert.True(File.Exists(Path.Combine(Request.DeltaPath, "delta.journal"))); }
    internal bool OriginalDirectoryIdentitiesPreserved() => expectedEntries.Values.Where(e => e.Directory && e.BasePath is not null)
        .All(e => originalIds[e.BasePath!] == Identity(Target(e.Path), directory: true));
    internal static string Identity(string path, bool directory = false)
        => RootTopologyDirectoryRestoreTests.ReadNativeIdentity(path, directory);
    internal static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    internal void AllowCleanup() => allowCleanup = true;
    public void Dispose()
    {
        if (allowCleanup) directory.Dispose();
        else TestContext.Current.TestOutputHelper?.WriteLine("Preserved unexpected root-delta fixture failure at " + directory.Path);
    }
}
