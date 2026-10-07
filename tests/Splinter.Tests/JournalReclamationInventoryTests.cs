using Splinter.Core;
using Splinter.Runtime;

namespace Splinter.Tests;

public sealed class JournalReclamationInventoryTests
{
    private static SnapshotDeltaSeal Populate(string directory, int count)
    {
        var tree = new SnapshotOverlayTests.FakeSnapshotTree();
        var identity = new OverlayIdentity(2, "reclamation-inventory", tree.Snapshot, 64L * 1048576, 1048576);
        using var store = new JournalDeltaStore(directory, identity, tree);
        for (int i = 0; i < count; i++)
        {
            Guid id = store.Create($"file-{i}", false, 32).Id;
            store.Write(id, 0, new byte[] { (byte)i, 2, 3 });
        }
        return store.Seal();
    }

    [Theory]
    [InlineData("intent")]
    [InlineData("object-removed")]
    public void ManyFileInventoryResumesWithoutDeletingForeignFiles(string interruption)
    {
        using var root = new TestDirectory();
        string delta = Path.Combine(root.Path, "delta"), receipt = Path.Combine(root.Path, "receipt");
        SnapshotDeltaSeal seal = Populate(delta, 128);
        Assert.Throws<IOException>(() => LatestJournalReclamation.ReclaimDirectory(delta, seal, ["identity.json"], receipt,
            at => { if (at == interruption) throw new IOException("Retain partial inventory."); }));
        string foreign = Path.Combine(delta, "foreign.txt");
        File.WriteAllText(foreign, "retain");
        string[] remaining = Directory.GetFiles(delta, "*", SearchOption.AllDirectories);
        var error = Assert.Throws<WorkspaceException>(() => LatestJournalReclamation.ReclaimDirectory(delta, seal, ["identity.json"], receipt));
        Assert.Equal("ViewReceiptMismatch", error.Code);
        Assert.Equal("retain", File.ReadAllText(foreign));
        Assert.All(remaining, path => Assert.True(File.Exists(path)));
        File.Delete(foreign);
        LatestJournalReclamation.ReclaimDirectory(delta, seal, ["identity.json"], receipt);
        Assert.False(Directory.Exists(delta));
        LatestJournalReclamation.ReclaimDirectory(delta, seal, ["identity.json"], receipt);
    }

    [Fact]
    public void FreshInventoryFencesEveryOwnedFileAgainstWriters()
    {
        if (!OperatingSystem.IsWindows()) Assert.Skip("Windows file-sharing fence contract.");
        using var root = new TestDirectory();
        string delta = Path.Combine(root.Path, "delta"), receipt = Path.Combine(root.Path, "receipt");
        SnapshotDeltaSeal seal = Populate(delta, 128);
        int checkedFiles = 0;
        LatestJournalReclamation.ReclaimDirectory(delta, seal, ["identity.json"], receipt, at =>
        {
            if (at != "intent") return;
            foreach (string path in Directory.GetFiles(delta, "*", SearchOption.AllDirectories))
            {
                Assert.Throws<IOException>(() => { using var writer = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete); });
                checkedFiles++;
            }
        });
        Assert.True(checkedFiles >= 128);
        Assert.False(Directory.Exists(delta));
    }
}
