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
    [InlineData("file.after-create-receipt")]
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
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReplacedNewTargetIsPreservedEvenWhenItsBytesMatch(bool matchingBytes)
    {
        using var fixture = new Fixture();
        await Interrupt(fixture, "file.after-ranges-flush");
        string target = Path.Combine(fixture.Original, "new");
        byte[] foreign = matchingBytes ? File.ReadAllBytes(target) : [42, 43, 44];
        File.Move(target, Path.Combine(fixture.State.Basis.ReceiptDirectory, "preserved-created-file"));
        File.WriteAllBytes(target, foreign);
        var error = await Assert.ThrowsAsync<WorkspaceException>(() => ManagedDirectoryRestore.ExecuteAsync(fixture.State, fixture.Request, TestContext.Current.CancellationToken));
        Assert.Equal("GeneratedRestoreConflict", error.Code); Assert.Equal(foreign, File.ReadAllBytes(target));
        Assert.True(Directory.Exists(fixture.State.Basis.Snapshot.RootPath));
        Assert.True(File.Exists(Path.Combine(fixture.Request.DeltaPath, "delta.journal")));
    }
    [Fact]
    public async Task CreateWithoutReceiptIsPreservedRatherThanAssumedOwned()
    {
        using var fixture = new Fixture();
        await Interrupt(fixture, "file.after-create-before-receipt");
        string target = Path.Combine(fixture.Original, "new");
        File.WriteAllBytes(target, [77]);
        var error = await Assert.ThrowsAsync<WorkspaceException>(() => ManagedDirectoryRestore.ExecuteAsync(fixture.State, fixture.Request, TestContext.Current.CancellationToken));
        Assert.Equal("GeneratedRestoreConflict", error.Code); Assert.Equal(new byte[] { 77 }, File.ReadAllBytes(target));
        Assert.True(Directory.Exists(fixture.State.Basis.Snapshot.RootPath)); Assert.True(Directory.Exists(fixture.Request.DeltaPath));
    }
    [Fact]
    public async Task FullyOverwrittenMovedBasisStillRequiresItsPhysicalIdentity()
    {
        using var fixture = new Fixture(overwrittenBasis: true);
        await Interrupt(fixture, "file.after-move");
        string target = Path.Combine(fixture.Original, "overwrite");
        File.Move(target, Path.Combine(fixture.State.Basis.ReceiptDirectory, "preserved-moved-file"));
        File.WriteAllBytes(target, [77]);
        var error = await Assert.ThrowsAsync<WorkspaceException>(() => ManagedDirectoryRestore.ExecuteAsync(fixture.State, fixture.Request, TestContext.Current.CancellationToken));
        Assert.Equal("GeneratedRestoreConflict", error.Code); Assert.Equal(new byte[] { 77 }, File.ReadAllBytes(target));
        Assert.True(File.Exists(Path.Combine(fixture.Request.DeltaPath, "delta.journal")));
    }
    [Fact]
    public async Task PinnedWriteRejectsConcurrentReplacement()
    {
        using var fixture = new Fixture(); bool checkedPin = false;
        await ManagedDirectoryRestore.ExecuteAsync(fixture.State, fixture.Request, TestContext.Current.CancellationToken, stage =>
        {
            if (stage != "file.before-ranges-write" || checkedPin) return;
            checkedPin = true; Assert.Throws<IOException>(() => File.Delete(Path.Combine(fixture.Original, "new")));
        });
        Assert.True(checkedPin); Assert.Equal(new byte[] { 9, 0, 0, 0, 6 }, File.ReadAllBytes(Path.Combine(fixture.Original, "new")));
    }
    [Fact]
    public async Task UnsupportedRestorePlanIsKeptWithoutMigration()
    {
        using var fixture = new Fixture(); await Interrupt(fixture, "file.after-ranges-flush");
        string path = Path.Combine(fixture.State.Basis.ReceiptDirectory, "restore-plan.json");
        var plan = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(path))!; plan["version"] = 1; File.WriteAllText(path, plan.ToJsonString());
        var error = await Assert.ThrowsAsync<WorkspaceException>(() => ManagedDirectoryRestore.ExecuteAsync(fixture.State, fixture.Request, TestContext.Current.CancellationToken));
        Assert.Equal("RestoreSealMismatch", error.Code); Assert.Equal(1, System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(path))!["version"]!.GetValue<int>());
        Assert.True(Directory.Exists(fixture.Request.DeltaPath));
    }
    [Fact]
    public async Task ReplacedCompletedFileBlocksCleanupEvenWithIdenticalBytes()
    {
        using var fixture = new Fixture(); await Interrupt(fixture, "restore.after-verified");
        string target = Path.Combine(fixture.Original, "new"); byte[] content = File.ReadAllBytes(target);
        File.Move(target, Path.Combine(fixture.State.Basis.ReceiptDirectory, "preserved-completed-file")); File.WriteAllBytes(target, content);
        var error = await Assert.ThrowsAsync<WorkspaceException>(() => ManagedDirectoryRestore.ExecuteAsync(fixture.State, fixture.Request, TestContext.Current.CancellationToken));
        Assert.Equal("GeneratedRestoreConflict", error.Code); Assert.Equal(content, File.ReadAllBytes(target));
        Assert.True(Directory.Exists(fixture.State.Basis.Snapshot.RootPath)); Assert.True(Directory.Exists(fixture.Request.DeltaPath));
    }
    [Fact]
    public async Task OwnedReadOnlyFileIsMovedAndItsFinalAttributesAreRestored()
    {
        using var fixture = new Fixture(readOnlyBasis: true);
        await ManagedDirectoryRestore.ExecuteAsync(fixture.State, fixture.Request, TestContext.Current.CancellationToken);
        string target = Path.Combine(fixture.Original, "renamed", "large");
        Assert.True((File.GetAttributes(target) & FileAttributes.ReadOnly) != 0); Assert.Equal(7, File.ReadAllBytes(target)[100]);
    }
    [Fact]
    public async Task ChangedReadOnlyRetainedBytesAreRejectedBeforeAttributeMutation()
    {
        using var fixture = new Fixture(readOnlyBasis: true); await Interrupt(fixture, "file.after-move");
        string target = Path.Combine(fixture.Original, "renamed", "large");
        var attributes = File.GetAttributes(target); File.SetAttributes(target, attributes & ~FileAttributes.ReadOnly);
        using (var stream = new FileStream(target, FileMode.Open, FileAccess.Write)) { stream.WriteByte(99); stream.Flush(true); }
        File.SetAttributes(target, attributes);
        var error = await Assert.ThrowsAsync<WorkspaceException>(() => ManagedDirectoryRestore.ExecuteAsync(fixture.State, fixture.Request, TestContext.Current.CancellationToken));
        Assert.Equal("GeneratedRestoreConflict", error.Code); Assert.Equal(attributes, File.GetAttributes(target));
        Assert.Equal(99, File.ReadAllBytes(target)[0]); Assert.True(Directory.Exists(fixture.Request.DeltaPath));
    }
    private static async Task Interrupt(Fixture fixture, string stage)
    {
        bool interrupted = false;
        await Assert.ThrowsAsync<IOException>(() => ManagedDirectoryRestore.ExecuteAsync(fixture.State, fixture.Request, TestContext.Current.CancellationToken,
            boundary => { if (!interrupted && boundary == stage) { interrupted = true; throw new IOException("fixed interruption"); } }));
        Assert.True(interrupted);
    }
    private sealed class Fixture : IDisposable
    {
        private readonly TestDirectory directory = new();
        public ManagedDirectoryState State { get; }
        public OverlayServeRequest Request { get; }
        public string Original => State.OriginalPath;
        public Fixture(bool overwrittenBasis = false, bool readOnlyBasis = false)
        {
            string root = Path.Combine(directory.Path, "owned"), basisPath = Path.Combine(root, "base"); Directory.CreateDirectory(Path.Combine(basisPath, "dir"));
            File.WriteAllBytes(Path.Combine(basisPath, "dir", "large"), Enumerable.Repeat((byte)1, 65536).ToArray());
            File.WriteAllBytes(Path.Combine(basisPath, "unchanged"), [3, 4]); File.WriteAllBytes(Path.Combine(basisPath, "deleted"), [0]); File.WriteAllBytes(Path.Combine(basisPath, "truncated"), [5, 5, 5, 5, 5]);
            if (overwrittenBasis) File.WriteAllBytes(Path.Combine(basisPath, "overwrite"), [1]);
            if (readOnlyBasis) File.SetAttributes(Path.Combine(basisPath, "dir", "large"), File.GetAttributes(Path.Combine(basisPath, "dir", "large")) | FileAttributes.ReadOnly);
            var snapshot = new SnapshotReference("012345678901234567890123", OwnedDirectorySnapshotTree.ProviderName, basisPath, basisPath, DateTimeOffset.UnixEpoch, true, true, "stopped-original-owned-directory");
            var manifest = OwnedDirectorySnapshotTree.Capture(snapshot, snapshot.Id); var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
            File.WriteAllText(Path.Combine(root, "manifest.json"), JsonSerializer.Serialize(manifest, options)); string hash = ManagedDirectoryBasis.HashFile(Path.Combine(root, "manifest.json"));
            File.WriteAllText(Path.Combine(root, "proof.json"), JsonSerializer.Serialize(new OwnedDirectoryProof(1, snapshot, snapshot.Id, hash, manifest.Items.Sum(i => i.Entry.Length)), options));
            State = new() { ApplicationKind = "unity", DirectoryKind = "Library", Key = "fixture", OriginalPath = Path.Combine(directory.Path, "original", "Library"), CompatibilityKey = "fixture", Phase = "restoring",
                Basis = new(snapshot.Id, Path.Combine(directory.Path, "original"), "Library", snapshot, root, snapshot.Id, hash) };
            Request = new(new(2, snapshot.Id, snapshot, 1024 * 1024, 65536), Path.Combine(root, "delta"), Original, Path.Combine(root, "overlay"), "token", new(snapshot.Id, basisPath, hash), root, snapshot.Id);
            using var tree = new OwnedDirectorySnapshotTree(manifest, snapshot); using var journal = new JournalDeltaStore(Request.DeltaPath, Request.Identity, tree);
            journal.Write(journal.Stat("dir/large")!.Id, 100, [7, 8]); journal.Rename(journal.Stat("dir")!.Id, "renamed", false); journal.Delete(journal.Stat("deleted")!.Id);
            var created = journal.Create("new", false, 32); journal.Write(created.Id, 0, [9]); journal.Write(created.Id, 4, [6]);
            var truncate = journal.Stat("truncated")!; journal.SetLength(truncate.Id, 1); journal.SetLength(truncate.Id, 5); journal.Write(truncate.Id, 4, [2]);
            if (overwrittenBasis) journal.Write(journal.Stat("overwrite")!.Id, 0, [2]);
        }
        public void Dispose() => directory.Dispose();
    }
}
