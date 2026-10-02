using Splinter.Core;
using Splinter.Runtime;

namespace Splinter.Tests;

public sealed partial class SnapshotOverlayTests
{
    [Theory]
    [InlineData(".plastic")]
    [InlineData(".PLASTIC")]
    [InlineData(".git")]
    public void DeniedRootsCannotBeReadCreatedOrRenamedAndDoNotAppendFrames(string root)
    {
        using var d = new TestDirectory(); var tree = new FakeSnapshotTree();
        tree.Files[".plastic/private"] = [1, 2, 3]; tree.Files["source"] = [4];
        var identity = new OverlayIdentity(1, "guard", tree.Snapshot, 100000, 4096, [".plastic", ".git"], DeniedTopLevel: [".plastic", ".git"]);
        string delta = Path.Combine(d.Path, "delta");
        using (var store = new JournalDeltaStore(delta, identity, tree))
        {
            Assert.Null(store.Stat(root)); Assert.Null(store.Stat(root + "/private"));
            Assert.DoesNotContain(store.List(""), e => e.Path.Equals(".plastic", StringComparison.OrdinalIgnoreCase));
            long length = store.Observe().JournalBytes;
            Assert.Throws<UnauthorizedAccessException>(() => store.Create(root, true, 16));
            Assert.Throws<UnauthorizedAccessException>(() => store.Create(root + "/new", false, 32));
            Assert.Throws<UnauthorizedAccessException>(() => store.Rename(store.Stat("source")!.Id, root, true));
            Assert.Equal(length, store.Observe().JournalBytes); Assert.Equal(0, store.Observe().DurableSequence);
            Assert.Equal(0, tree.ReadBytes);
            var file = store.Create("allowed", false, 32); store.Write(file.Id, 0, [9]);
        }
        using var replayed = new JournalDeltaStore(delta, identity, tree);
        Assert.Null(replayed.Stat(root)); Assert.NotNull(replayed.Stat("allowed"));
        Assert.Throws<UnauthorizedAccessException>(() => replayed.Create(root, true, 16));
        replayed.Dispose();
        Assert.Throws<WorkspaceException>(() => { using var refused = new JournalDeltaStore(delta, identity with { DeniedTopLevel = null }, tree); });
    }

    [Fact]
    public void ExcludingCapturedAdministrationStillAllowsOwnedGitMetadataWithoutDenial()
    {
        using var d = new TestDirectory(); var tree = new FakeSnapshotTree(); tree.Files[".git/source-owner"] = [1];
        using var store = new JournalDeltaStore(Path.Combine(d.Path, "delta"), new(1, "git", tree.Snapshot, 100000, 4096, [".git"]), tree);
        Assert.Null(store.Stat(".git"));
        var privateGit = store.Create(".git", false, 32); store.Write(privateGit.Id, 0, [2]);
        Assert.NotNull(store.Stat(".git")); Assert.Equal(0, tree.ReadBytes);
    }
}
