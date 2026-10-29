using Splinter.Core;
using Splinter.Platform.Windows;

namespace Splinter.Tests;

public sealed class RootTopologyRestoreSelectionTests
{
    [Theory]
    [InlineData(8, 8, false)]
    [InlineData(8, 7, false)]
    [InlineData(8, 1, false)]
    [InlineData(8, 3, false)]
    [InlineData(7, 3, false)]
    [InlineData(1000, 499, false)]
    [InlineData(1000, 4, true)]
    [InlineData(1000, 5, false)]
    [InlineData(128, 1, false)]
    [InlineData(129, 1, true)]
    [InlineData(1, 0, false)]
    [InlineData(2, 0, true)]
    public void FreshSelectionRequiresFewerMovesAndKeepsLeafCursorWritesWithinTheGeneralBudget(int files, int renamed, bool preferred)
    {
        var items = new List<(string Path, bool Directory, string? Final)> { ("", true, "") };
        items.AddRange(Enumerable.Range(0, files).Select(i => ($"f{i}", false, (string?)(i < renamed ? $"moved{i}" : $"f{i}"))));
        var (manifest, entries) = Input(items);
        var model = ManagedRestoreTopology.Build(manifest, entries);
        Assert.Equal(renamed + 1, model.Units.Length);
        Assert.Equal(renamed * 2, model.Operations.Length);
        Assert.Equal(((long)files, 1L + renamed * 2, renamed * 2L, 1L + (files - 1) / 128), Costs(manifest, entries));
        Assert.Equal(preferred, ManagedTopologyDirectoryRestore.PreferFresh(manifest, entries));
    }

    [Theory]
    [InlineData(5, false)]
    [InlineData(10, false)]
    [InlineData(384, false)]
    [InlineData(385, true)]
    public void NestedTransportUnitsCountTheirOwnMovesWithoutCountingCarriedDescendantsAgain(int files, bool preferred)
    {
        var items = new List<(string Path, bool Directory, string? Final)>
        {
            ("", true, ""), ("bulk", true, "renamed"), ("bulk/nested", true, "renamed/extracted"),
            ("bulk/direct", false, "renamed/direct")
        };
        items.AddRange(Enumerable.Range(1, files - 1).Select(i => ($"bulk/nested/f{i}", false, (string?)$"renamed/extracted/f{i}")));
        var (manifest, entries) = Input(items);
        var model = ManagedRestoreTopology.Build(manifest, entries);
        var sourcePaths = new List<string>();
        foreach (var unit in model.Units) { sourcePaths.Add(unit.BasePath); Assert.True(unit.Directory); }
        Assert.Equal(new[] { "", "bulk", "bulk/nested" }, sourcePaths);
        Assert.Equal(4, model.Operations.Length);
        Assert.Equal(((long)files, 5L, 4L, 1L + (files - 1) / 128), Costs(manifest, entries));
        Assert.Equal(preferred, ManagedTopologyDirectoryRestore.PreferFresh(manifest, entries));
    }

    [Theory]
    [InlineData(4, false)]
    [InlineData(8, false)]
    [InlineData(256, false)]
    [InlineData(257, true)]
    public void DeletedSubtreeCostsOneStageAndNewFilesDoNotIncreaseTheSurvivingSourceDenominator(int surviving, bool preferred)
    {
        var items = new List<(string Path, bool Directory, string? Final)>
        {
            ("", true, ""), ("bulk", true, "renamed"), ("doomed", true, null), ("doomed/nested", true, null)
        };
        items.AddRange(Enumerable.Range(0, surviving).Select(i => ($"bulk/f{i}", false, (string?)$"renamed/f{i}")));
        items.AddRange(Enumerable.Range(0, 3).Select(i => ($"doomed/nested/f{i}", false, (string?)null)));
        var (manifest, entries) = Input(items);
        var added = Enumerable.Range(0, 20).Select(i => new DeltaRestoreEntry(
            new DeltaEntry(Guid.NewGuid(), $"new{i}", false, 0, 32, 1, 1, 1, null, 0), []));
        entries = entries.Concat(added).ToArray();
        var model = ManagedRestoreTopology.Build(manifest, entries);
        Assert.Equal(3, model.Units.Length);
        var deleted = new List<string>();
        foreach (var unit in model.Units) if (unit.FinalPath is null) deleted.Add(unit.BasePath);
        Assert.Equal("doomed", Assert.Single(deleted));
        Assert.Equal(3, model.Operations.Length);
        Assert.Equal(((long)surviving, 4L, 3L, 1L + (surviving - 1) / 128), Costs(manifest, entries));
        Assert.Equal(preferred, ManagedTopologyDirectoryRestore.PreferFresh(manifest, entries));
    }

    [Theory]
    [InlineData(384, false)]
    [InlineData(385, true)]
    public void NewDirectoryConsumesBothCursorAndIdentityPublicationWhileNewFilesAddNoBudget(int surviving, bool preferred)
    {
        var items = new List<(string Path, bool Directory, string? Final)> { ("", true, ""), ("bulk", true, "renamed") };
        items.AddRange(Enumerable.Range(0, surviving).Select(i => ($"bulk/f{i}", false, (string?)$"renamed/f{i}")));
        var (manifest, entries) = Input(items);
        var additions = new List<DeltaRestoreEntry> { new(new DeltaEntry(Guid.NewGuid(), "new-directory", true, 0, 16, 1, 1, 1, null, 0), []) };
        additions.AddRange(Enumerable.Range(0, 1000).Select(i => new DeltaRestoreEntry(
            new DeltaEntry(Guid.NewGuid(), $"new-directory/f{i}", false, 0, 32, 1, 1, 1, null, 0), [])));
        entries = entries.Concat(additions).ToArray();
        Assert.Equal(((long)surviving, 3L, 4L, 1L + (surviving - 1) / 128), Costs(manifest, entries));
        Assert.Equal(preferred, ManagedTopologyDirectoryRestore.PreferFresh(manifest, entries));
    }

    [Theory]
    [InlineData(24, false)]
    [InlineData(128, false)]
    [InlineData(129, true)]
    public async Task FreshBulkRoutingUsesThePublicationBudgetAndStillRestoresExactFiles(int files, bool topology)
    {
        using var f = new RootTopologyRestoreFixture(true, "bulk", count: files, payloadBytes: 1024);
        var stages = new List<string>(); await f.Restore(stages.Add);
        f.AssertRestored(requireOriginalDirectoryIdentities: topology);
        Assert.Equal(topology, File.Exists(f.PlanPath));
        Assert.Equal(!topology, File.Exists(Path.Combine(f.Root, "restore-plan.json")));
        Assert.Equal(topology ? 1 : 0, stages.Count(stage => stage == "topology.after-root-move"));
        Assert.Equal(topology ? 0 : files, stages.Count(stage => stage == "file.after-move"));
        f.AllowCleanup();
    }

    [Fact]
    public async Task RejectedFreshLeafWorkloadUsesTheGeneralRouteAndRestoresTheSameFiles()
    {
        using var f = new RootTopologyRestoreFixture(true, "leaf-cost", count: 20, payloadBytes: 1024);
        var stages = new List<string>(); await f.Restore(stages.Add);
        f.AssertRestored(requireOriginalDirectoryIdentities: false);
        Assert.False(File.Exists(f.PlanPath)); Assert.True(File.Exists(Path.Combine(f.Root, "restore-plan.json")));
        Assert.DoesNotContain("topology.after-root-move", stages);
        Assert.Equal(f.OriginalFileCount, stages.Count(stage => stage == "file.after-move"));
        f.AllowCleanup();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExistingCurrentTopologyPlanResumesEvenWhenTheFreshPolicyRejectsItsInput(bool bounded)
    {
        using var f = new RootTopologyRestoreFixture(bounded, "leaf-cost", count: 20, payloadBytes: 1024);
        await f.SeedTopologyPlanAsync(); byte[] plan = File.ReadAllBytes(f.PlanPath);
        var stages = new List<string>(); await f.Restore(stages.Add); f.AssertRestored();
        Assert.Equal(plan, File.ReadAllBytes(f.PlanPath)); Assert.False(File.Exists(Path.Combine(f.Root, "restore-plan.json")));
        Assert.Equal(1, stages.Count(stage => stage == "topology.after-root-move"));
        Assert.Equal(f.OriginalFileCount, stages.Count(stage => stage == "topology.after-stage"));
        Assert.Equal(f.OriginalFileCount, stages.Count(stage => stage == "topology.after-install"));
        Assert.Contains("topology.after-reclaim", stages); f.AllowCleanup();
    }

    private static (long Files, long Moves, long Publications, long GeneralBudget) Costs(
        OwnedDirectoryManifest manifest, DeltaRestoreEntry[] entries)
    {
        var model = ManagedRestoreTopology.Build(manifest, entries);
        long files = entries.LongCount(delta => !delta.Entry.Directory && delta.Entry.BasePath is not null), moves = 1, publications = 0;
        for (int i = 1; i < model.Units.Length; i++) moves += model.Units[i].FinalPath is null ? 1 : 2;
        foreach (var operation in model.Operations) publications += operation.Kind == "directory" ? 2 : 1;
        return (files, moves, publications, files == 0 ? 0 : 1L + (files - 1) / 128);
    }

    private static (OwnedDirectoryManifest Manifest, DeltaRestoreEntry[] Entries) Input(
        IEnumerable<(string Path, bool Directory, string? Final)> definition)
    {
        var source = definition.ToArray();
        var snapshot = new SnapshotReference("planner", OwnedDirectorySnapshotTree.ProviderName, "unused", "unused",
            DateTimeOffset.UnixEpoch, true, true, "stopped-original-owned-directory");
        var items = source.Select(item => new OwnedDirectoryItem(new SnapshotEntry(item.Path, item.Directory,
            item.Directory ? 0 : 1, item.Directory ? 16u : 32u, 1, 1, 1), item.Directory ? null : new string('0', 64))).ToArray();
        var entries = source.Where(item => item.Final is not null).Select(item => new DeltaRestoreEntry(
            new DeltaEntry(Guid.NewGuid(), item.Final!, item.Directory, item.Directory ? 0 : 1,
                item.Directory ? 16u : 32u, 1, 1, 1, item.Path, item.Directory ? 0 : 1), [])).ToArray();
        return (new OwnedDirectoryManifest(1, snapshot, "planner", items), entries);
    }
}
