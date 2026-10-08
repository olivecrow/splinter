using Splinter.Core;
using Splinter.Runtime;
using Splinter.Platform.Windows;
using System.Security.Cryptography;
using System.Text.Json;

namespace Splinter.Tests;

public sealed class GeneratedDirectoryTests
{
    [Fact]
    public void TrackedLibraryIsRejectedAndSourceAndGeneratedWritersRemainIndependent()
    {
        using var directory = new TestDirectory();
        var source = new SnapshotOverlayTests.FakeSnapshotTree(); source.Files["Assets/code.cs"] = [1, 2];
        var generated = new SnapshotOverlayTests.FakeSnapshotTree { Snapshot = new("cache", "fake-memory", "cache-source", "cache-root", DateTimeOffset.UnixEpoch, true, true, "fixture") };
        generated.Files["db/cache"] = [3, 4, 5];
        var basis = new GeneratedDirectoryReference("cache", "project", "Library", generated.Snapshot, "receipt", "owner", "manifest"); var graft = new SnapshotGraft("Library", basis);
        source.Files["Library/tracked"] = [1];
        Assert.Equal("GeneratedBasisTracked", Assert.Throws<WorkspaceException>(() => new GraftedSnapshotTree(source, generated, graft)).Code);
        source.Files.Remove("Library/tracked");
        var tree = new GraftedSnapshotTree(source, generated, graft);
        Assert.Contains(tree.List(""), e => e.Path == "Library"); Assert.Equal("Library/db/cache", tree.List("Library/db").Single().Path);
        for (int index = 0; index < 3; index++)
        {
            var identity = new OverlayIdentity(2, "writer-" + index, source.Snapshot, 1024 * 1024, 65536, Graft: graft);
            string delta = Path.Combine(directory.Path, index.ToString());
            using (var writer = new JournalDeltaStore(delta, identity, tree))
            {
                writer.Write(writer.Stat("Library/db/cache")!.Id, 1, [(byte)(index + 10)]);
                Assert.Equal(0, writer.Observe().InitialPayloadCopiedBytes); Assert.Equal(0, writer.Observe().ReadCacheBytes);
            }
            using var reader = JournalDeltaStore.OpenReadOnly(delta, identity, tree); byte[] data = new byte[3];
            Assert.Equal(3, reader.Read(reader.Stat("Library/db/cache")!.Id, 0, data)); Assert.Equal(new byte[] { 3, (byte)(index + 10), 5 }, data);
            var changedGraft = graft with { Basis = basis with { ManifestHash = "different" } };
            Assert.Equal("SnapshotIdentityMismatch", Assert.Throws<WorkspaceException>(() => new JournalDeltaStore(delta, identity with { Graft = changedGraft }, tree)).Code);
        }
        Assert.Equal(new byte[] { 3, 4, 5 }, generated.Files["db/cache"]);
        generated.Available = false; Assert.Equal("SnapshotMissing", Assert.Throws<WorkspaceException>(() => tree.Stat("Assets/code.cs")).Code);
    }
    [Fact]
    public void RestoreRangesNeverHydrateUnchangedDataOrReadFromALostBasis()
    {
        using var directory = new TestDirectory(); var basis = new SnapshotOverlayTests.FakeSnapshotTree(); basis.Files["dir/file"] = new byte[65536]; basis.Files["delete"] = [8];
        var identity = new OverlayIdentity(2, "restore", basis.Snapshot, 1024 * 1024, 65536);
        using (var writer = new JournalDeltaStore(directory.Path, identity, basis))
        {
            writer.Write(writer.Stat("dir/file")!.Id, 100, [6, 7]); writer.Rename(writer.Stat("dir")!.Id, "renamed", false); writer.Delete(writer.Stat("delete")!.Id);
        }
        using var read = JournalDeltaStore.OpenReadOnly(directory.Path, identity, basis); var descriptions = read.DescribeRestore();
        var file = Assert.Single(descriptions, e => e.Entry.Path == "renamed/file"); Assert.Equal("dir/file", file.Entry.BasePath);
        Assert.Equal(new DeltaRestoreRange[] { new(100, 2) }, file.Ranges); Assert.DoesNotContain(descriptions, e => e.Entry.Path == "delete");
        var seal = read.Seal(); read.PrepareRestoreReads(seal); basis.Available = false;
        byte[] payload = new byte[2]; read.ReadRestoreRange(file.Entry.Id, 100, payload, seal); Assert.Equal(new byte[] { 6, 7 }, payload);
        Assert.Equal("RestoreRangeNotPrivate", Assert.Throws<WorkspaceException>(() => read.ReadRestoreRange(file.Entry.Id, 99, payload, seal)).Code);
        Assert.Equal("RestoreSealMismatch", Assert.Throws<WorkspaceException>(() => read.ReadRestoreRange(file.Entry.Id, 100, payload, seal with { JournalHash = "wrong" })).Code);
        Assert.Equal(0, basis.ReadBytes);
    }
    [Fact]
    public void OwnedDirectoryFencesWritesAndRejectsChangedPayloadOnReopen()
    {
        if (!OperatingSystem.IsWindows()) Assert.Skip("Windows file sharing or native contract.");
        using var directory = new TestDirectory(); File.WriteAllBytes(Path.Combine(directory.Path, "file"), [1, 2]);
        var snapshot = new SnapshotReference("owned", OwnedDirectorySnapshotTree.ProviderName, directory.Path, directory.Path, DateTimeOffset.UnixEpoch, true, true, "stopped-original-owned-directory");
        var manifest = OwnedDirectorySnapshotTree.Capture(snapshot, "owner");
        using (var tree = new OwnedDirectorySnapshotTree(manifest, snapshot))
        {
            Assert.Throws<IOException>(() => File.WriteAllBytes(Path.Combine(directory.Path, "file"), [9, 9]));
            Assert.Throws<IOException>(() => File.Delete(Path.Combine(directory.Path, "file")));
            byte[] data = new byte[2]; Assert.Equal(2, tree.Read("file", 0, data)); Assert.Equal(new byte[] { 1, 2 }, data);
        }
        File.WriteAllBytes(Path.Combine(directory.Path, "file"), [9, 9]);
        Assert.Equal("GeneratedBasisChanged", Assert.Throws<WorkspaceException>(() => new OwnedDirectorySnapshotTree(manifest, snapshot)).Code);
    }
}
