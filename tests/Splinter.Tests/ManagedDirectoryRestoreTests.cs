using System.Text.Json;
using Splinter.Core;
using Splinter.Runtime;
using Splinter.Platform.Windows;

namespace Splinter.Tests;

public sealed class ManagedDirectoryRestoreTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("file.after-move")]
    [InlineData("file.after-ranges-flush")]
    [InlineData("restore.after-verified")]
    [InlineData("restore.after-basis-reclaim")]
    [InlineData("restore.after-delta-reclaim")]
    public async Task FilesMoveWithoutFullCopyAndFixedRangeRestorationResumes(string? interruption)
    {
        using var fixture = new Fixture(); bool interrupted = false;
        if (interruption is not null)
        {
            await Assert.ThrowsAsync<IOException>(() => ManagedDirectoryRestore.ExecuteAsync(fixture.State, fixture.Request, TestContext.Current.CancellationToken,
                stage => { if (!interrupted && stage == interruption) { interrupted = true; throw new IOException("fixed interruption"); } }));
            Assert.True(interrupted);
        }
        await ManagedDirectoryRestore.ExecuteAsync(fixture.State, fixture.Request, TestContext.Current.CancellationToken);
        var expected = Enumerable.Repeat((byte)1, 65536).ToArray(); expected[100] = 7; expected[101] = 8;
        Assert.Equal(expected, File.ReadAllBytes(Path.Combine(fixture.Original, "renamed", "large")));
        Assert.Equal(new byte[] { 3, 4 }, File.ReadAllBytes(Path.Combine(fixture.Original, "unchanged")));
        Assert.Equal(new byte[] { 9, 0, 0, 0, 6 }, File.ReadAllBytes(Path.Combine(fixture.Original, "new")));
        Assert.Equal(new byte[] { 5, 0, 0, 0, 2 }, File.ReadAllBytes(Path.Combine(fixture.Original, "truncated")));
        Assert.False(File.Exists(Path.Combine(fixture.Original, "deleted"))); Assert.False(Directory.Exists(Path.Combine(fixture.Original, "dir")));
        Assert.False(Directory.Exists(fixture.State.Basis.Snapshot.RootPath)); Assert.False(Directory.Exists(fixture.Request.DeltaPath));
        Assert.Equal(0, fixture.State.InitialPayloadCopiedBytes);
    }
    [Fact]
    public async Task ChangedRetainedBytesBlockRedoAndKeepJournal()
    {
        using var fixture = new Fixture(); bool interrupted = false;
        await Assert.ThrowsAsync<IOException>(() => ManagedDirectoryRestore.ExecuteAsync(fixture.State, fixture.Request, TestContext.Current.CancellationToken,
            stage => { if (!interrupted && stage == "file.after-move") { interrupted = true; throw new IOException(); } }));
        string first = Path.Combine(fixture.Original, "renamed", "large");
        using (var stream = new FileStream(first, FileMode.Open, FileAccess.Write)) { stream.WriteByte(99); stream.Flush(true); }
        var error = await Assert.ThrowsAsync<WorkspaceException>(() => ManagedDirectoryRestore.ExecuteAsync(fixture.State, fixture.Request, TestContext.Current.CancellationToken));
        Assert.Equal("GeneratedRestoreConflict", error.Code); Assert.True(File.Exists(Path.Combine(fixture.Request.DeltaPath, "delta.journal")));
        Assert.Equal(99, File.ReadAllBytes(first)[0]);
    }
    [Fact]
    public async Task UnexpectedOriginalTreeIsPreservedBeforeFirstEffect()
    {
        using var fixture = new Fixture(); Directory.CreateDirectory(fixture.Original); File.WriteAllText(Path.Combine(fixture.Original, "foreign"), "preserve");
        Assert.Equal("GeneratedRestoreConflict", (await Assert.ThrowsAsync<WorkspaceException>(() => ManagedDirectoryRestore.ExecuteAsync(fixture.State, fixture.Request, TestContext.Current.CancellationToken))).Code);
        Assert.Equal("preserve", File.ReadAllText(Path.Combine(fixture.Original, "foreign"))); Assert.True(Directory.Exists(fixture.State.Basis.Snapshot.RootPath));
    }
    private sealed class Fixture : IDisposable
    {
        private readonly TestDirectory directory = new();
        public ManagedDirectoryState State { get; }
        public OverlayServeRequest Request { get; }
        public string Original => State.OriginalPath;
        public Fixture()
        {
            string root = Path.Combine(directory.Path, "owned"), basisPath = Path.Combine(root, "base"); Directory.CreateDirectory(Path.Combine(basisPath, "dir"));
            File.WriteAllBytes(Path.Combine(basisPath, "dir", "large"), Enumerable.Repeat((byte)1, 65536).ToArray());
            File.WriteAllBytes(Path.Combine(basisPath, "unchanged"), [3, 4]); File.WriteAllBytes(Path.Combine(basisPath, "deleted"), [0]); File.WriteAllBytes(Path.Combine(basisPath, "truncated"), [5, 5, 5, 5, 5]);
            var snapshot = new SnapshotReference("012345678901234567890123", OwnedDirectorySnapshotTree.ProviderName, basisPath, basisPath, DateTimeOffset.UnixEpoch, true, true, "stopped-original-owned-directory");
            var manifest = OwnedDirectorySnapshotTree.Capture(snapshot, snapshot.Id); var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
            File.WriteAllText(Path.Combine(root, "manifest.json"), JsonSerializer.Serialize(manifest, options)); string hash = ManagedDirectoryBasis.HashFile(Path.Combine(root, "manifest.json"));
            File.WriteAllText(Path.Combine(root, "proof.json"), JsonSerializer.Serialize(new OwnedDirectoryProof(1, snapshot, snapshot.Id, hash, manifest.Items.Sum(i => i.Entry.Length)), options));
            State = new() { Key = "fixture", OriginalPath = Path.Combine(directory.Path, "original", "Library"), CompatibilityKey = "fixture", Phase = "restoring",
                Basis = new(snapshot.Id, Path.Combine(directory.Path, "original"), "Library", snapshot, root, snapshot.Id, hash) };
            Request = new(new(2, snapshot.Id, snapshot, 1024 * 1024, 65536), Path.Combine(root, "delta"), Original, Path.Combine(root, "overlay"), "token", new(snapshot.Id, basisPath, hash), root, snapshot.Id);
            using var tree = new OwnedDirectorySnapshotTree(manifest, snapshot); using var journal = new JournalDeltaStore(Request.DeltaPath, Request.Identity, tree);
            journal.Write(journal.Stat("dir/large")!.Id, 100, [7, 8]); journal.Rename(journal.Stat("dir")!.Id, "renamed", false); journal.Delete(journal.Stat("deleted")!.Id);
            var created = journal.Create("new", false, 32); journal.Write(created.Id, 0, [9]); journal.Write(created.Id, 4, [6]);
            var truncate = journal.Stat("truncated")!; journal.SetLength(truncate.Id, 1); journal.SetLength(truncate.Id, 5); journal.Write(truncate.Id, 4, [2]);
        }
        public void Dispose() => directory.Dispose();
    }
}
