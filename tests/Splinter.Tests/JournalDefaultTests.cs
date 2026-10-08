using System.Text.Json.Nodes;
using Splinter.Core;
using Splinter.Runtime;

namespace Splinter.Tests;

public sealed class JournalDefaultTests
{
    private static OverlayIdentity Identity(ISnapshotTree tree) => new(2, "journal-default", tree.Snapshot, 16L * 1048576, 1048576);
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExistingExplicitOffIncludingEmptyJournalSurvivesDefaultReconnect(bool write)
    {
        using var directory = new TestDirectory(); var tree = new SnapshotOverlayTests.FakeSnapshotTree(); var identity = Identity(tree);
        SnapshotDeltaSeal seal;
        using (var writer = new JournalDeltaStore(directory.Path, identity, tree, optimizeJournal: false))
        { Assert.False(writer.JournalOptimizationEnabled); if (write) writer.Write(writer.Create("file", false, 32).Id, 0, [1, 2]); seal = writer.Seal(); }
        using var resumed = new JournalDeltaStore(directory.Path, identity, tree);
        Assert.False(resumed.JournalOptimizationEnabled); Assert.Equal(seal, resumed.Seal());
    }
    [Theory]
    [InlineData("latest.initialize.intent-flushed")]
    [InlineData("latest.initialize.intent")]
    [InlineData("latest.initialize.identity-flushed")]
    [InlineData("latest.initialize.identity")]
    [InlineData("latest.initialize.objects")]
    [InlineData("latest.initialize.root-flushed")]
    [InlineData("latest.initialize.published")]
    public void InterruptedNewJournalResumesItsSavedOnSelection(string boundary)
    {
        using var directory = new TestDirectory(); var tree = new SnapshotOverlayTests.FakeSnapshotTree(); var identity = Identity(tree);
        Assert.Throws<IOException>(() => new JournalDeltaStore(directory.Path, identity, tree, new Interrupt(boundary)));
        using (var resumed = new JournalDeltaStore(directory.Path, identity, tree, optimizeJournal: false))
        {
            Assert.True(resumed.JournalOptimizationEnabled); Assert.Equal(0, resumed.Observe().DurableSequence);
            resumed.Write(resumed.Create("file", false, 32).Id, 0, [1, 2, 3]);
        }
        Assert.False(File.Exists(Path.Combine(directory.Path, "journal-initialization.json")));
        using var verified = new JournalDeltaStore(directory.Path, identity, tree);
        byte[] actual = new byte[3]; verified.Read(verified.Stat("file")!.Id, 0, actual); Assert.Equal(new byte[] { 1, 2, 3 }, actual);
    }
    [Fact]
    public void OmittedCompactRetainsOnAndExplicitOffSurvivesLaterOmission()
    {
        using var directory = new TestDirectory(); var tree = new SnapshotOverlayTests.FakeSnapshotTree(); var identity = Identity(tree);
        using (var writer = new JournalDeltaStore(directory.Path, identity, tree)) writer.Write(writer.Create("file", false, 32).Id, 0, [1, 2, 3]);
        var on = JournalDeltaStore.CompactDirectory(directory.Path, identity, tree);
        Assert.True(on.JournalOptimizationEnabled); JournalDeltaStore.CompleteCompaction(directory.Path, identity);
        var off = JournalDeltaStore.CompactDirectory(directory.Path, identity, tree, revertBaseRanges: false);
        Assert.False(off.JournalOptimizationEnabled); JournalDeltaStore.CompleteCompaction(directory.Path, identity);
        var retained = JournalDeltaStore.CompactDirectory(directory.Path, identity, tree);
        Assert.False(retained.JournalOptimizationEnabled); JournalDeltaStore.CompleteCompaction(directory.Path, identity);
        using var resumed = new JournalDeltaStore(directory.Path, identity, tree);
        Assert.False(resumed.JournalOptimizationEnabled);
        byte[] actual = new byte[3]; resumed.Read(resumed.Stat("file")!.Id, 0, actual); Assert.Equal(new byte[] { 1, 2, 3 }, actual);
    }
    [Theory]
    [InlineData("owner")]
    [InlineData("object")]
    [InlineData("root")]
    public void DamagedInitializationIsPreservedAndRejected(string damage)
    {
        using var directory = new TestDirectory(); var tree = new SnapshotOverlayTests.FakeSnapshotTree(); var identity = Identity(tree);
        Assert.Throws<IOException>(() => new JournalDeltaStore(directory.Path, identity, tree, new Interrupt("latest.initialize.published")));
        string intent = Path.Combine(directory.Path, "journal-initialization.json");
        if (damage == "owner")
        { var json = JsonNode.Parse(File.ReadAllText(intent))!; json["Identity"]!["Id"] = "peer"; File.WriteAllText(intent, json.ToJsonString()); }
        else if (damage == "object") File.WriteAllText(Path.Combine(Directory.GetDirectories(directory.Path).Single(), "peer.txt"), "preserve");
        else File.WriteAllText(Path.Combine(directory.Path, "delta.journal"), "preserve");
        var before = Directory.GetFiles(directory.Path, "*", SearchOption.AllDirectories).ToDictionary(p => p, File.ReadAllBytes);
        Assert.Equal("DeltaCorrupt", Assert.Throws<WorkspaceException>(() => new JournalDeltaStore(directory.Path, identity, tree)).Code);
        Assert.Equal(before.Keys.Order(), Directory.GetFiles(directory.Path, "*", SearchOption.AllDirectories).Order());
        foreach (var entry in before) Assert.Equal(entry.Value, File.ReadAllBytes(entry.Key));
    }
    private sealed class Interrupt(string boundary) : IJournalMutationObserver
    { public void Observe(JournalMutationBoundary at) { if (at.Boundary == boundary) throw new IOException("Owned initialization interruption."); } }
}
