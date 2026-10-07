using System.Diagnostics;
using Splinter.Core;
using Splinter.Runtime;

namespace Splinter.Tests;

public sealed class JournalInventoryScalingTests
{
    [Theory]
    [InlineData(128)]
    [InlineData(512)]
    public void ManySmallCapsulesReconnectWithIdenticalSealAndContent(int count)
    {
        using var root = new TestDirectory(); var tree = new SnapshotOverlayTests.FakeSnapshotTree();
        var identity = new OverlayIdentity(2, "inventory", tree.Snapshot, 64L * 1048576, 1048576);
        SnapshotDeltaSeal seal;
        using (var writer = new JournalDeltaStore(root.Path, identity, tree))
        {
            for (int i = 0; i < count; i++) writer.Write(writer.Create($"file-{i}", false, 32).Id, 0, [(byte)i]);
            seal = writer.Seal();
        }
        var timer = Stopwatch.StartNew();
        using var resumed = JournalDeltaStore.OpenReadOnly(root.Path, identity, tree);
        Assert.Equal(seal, resumed.Seal());
        TestContext.Current.TestOutputHelper!.WriteLine($"private-files={count}; reconnect-and-seal-ms={timer.Elapsed.TotalMilliseconds:F3}");
        for (int i = 0; i < count; i++)
        { byte[] read = new byte[1]; Assert.Equal(1, resumed.Read(resumed.Stat($"file-{i}")!.Id, 0, read)); Assert.Equal((byte)i, read[0]); }
    }

    [Fact]
    public void DuplicateGenerationStillRejectsWithoutRemovingEitherCapsule()
    {
        using var root = new TestDirectory(); var tree = new SnapshotOverlayTests.FakeSnapshotTree();
        var identity = new OverlayIdentity(2, "inventory", tree.Snapshot, 4 * 1048576, 1048576);
        using (var writer = new JournalDeltaStore(root.Path, identity, tree))
        {
            for (int i = 0; i < 4; i++) writer.Write(writer.Create($"file-{i}", false, 32).Id, 0, [(byte)i]);
        }
        string directory = Directory.GetDirectories(root.Path, "latest-*").Single();
        string original = Directory.GetFiles(directory, "commit-*").First();
        string duplicate = Path.Combine(directory, Path.GetFileName(original)[..24] + Guid.NewGuid().ToString("N") + ".bin");
        File.Copy(original, duplicate);
        byte[] before = File.ReadAllBytes(original);
        Assert.Equal("DeltaCorrupt", Assert.Throws<WorkspaceException>(() => JournalDeltaStore.OpenReadOnly(root.Path, identity, tree)).Code);
        Assert.Equal(before, File.ReadAllBytes(original)); Assert.Equal(before, File.ReadAllBytes(duplicate));
    }
}
