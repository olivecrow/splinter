using System.Text.Json;
using System.Text.Json.Nodes;
using Splinter.Core;
using Splinter.Runtime;
using Splinter.Platform.Windows;

namespace Splinter.Tests;

public sealed class ManagedDirectoryRestoreOptimizationTests
{
    [Theory]
    [InlineData("file.after-complete", 127, 0, false)]
    [InlineData("file.after-complete", 128, 128, true)]
    [InlineData("file.after-complete", 129, 128, true)]
    [InlineData("restore.before-cursor", 1, 0, true)]
    [InlineData("restore.after-cursor", 1, 128, false)]
    public async Task LostBatchPublicationResumesWithExactFiles(string hook, int occurrence, int saved, bool optimized)
    {
        using var f = new Fixture(260, optimized); int hits = 0;
        await Assert.ThrowsAsync<IOException>(() => f.Restore(stage =>
        { if (stage == hook && ++hits == occurrence) throw new IOException("lost cursor publication/reply"); }));
        var cursor = JsonNode.Parse(File.ReadAllText(Path.Combine(f.Root, "restore-cursor.json")))!;
        Assert.Equal(saved, cursor["completedFiles"]!.GetValue<int>());
        await f.Restore(); f.AssertRestored();
    }

    [Theory]
    [InlineData(127, 1)]
    [InlineData(128, 1)]
    [InlineData(129, 2)]
    public async Task CursorWritesAreBatchedWithoutNewFileReceipts(int count, int publications)
    {
        using var f = new Fixture(count, true); int actual = 0;
        await f.Restore(stage => { if (stage == "restore.after-cursor") actual++; });
        Assert.Equal(publications, actual); f.AssertRestored();
        Assert.Empty(Directory.GetFiles(f.Root, "restore-file-*.json"));
    }

    [Fact]
    public async Task UnchangedRenamedReadOnlyFileUsesTheSamePinnedIdentityForMetadata()
    {
        using var f = new Fixture(2, true, rename: true); string target = Path.Combine(f.State.OriginalPath, "renamed", "f0000");
        string identity;
        using (var file = ManagedRestoreFileAccess.Read(Path.Combine(f.State.Basis.Snapshot.RootPath, "dir", "f0000"))) identity = ManagedRestoreFileAccess.Identity(file);
        bool checkedPin = false;
        await f.Restore(stage =>
        {
            string pinnedTarget = Path.Combine(f.State.OriginalPath, "renamed", "f0001");
            if (stage != "file.after-move" || checkedPin || !File.Exists(pinnedTarget)) return;
            checkedPin = true;
            Assert.Throws<IOException>(() => File.Delete(pinnedTarget));
            Assert.Throws<IOException>(() => { using var writer = new FileStream(pinnedTarget, FileMode.Open, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete); });
        });
        using (var file = ManagedRestoreFileAccess.Read(target)) Assert.Equal(identity, ManagedRestoreFileAccess.Identity(file));
        Assert.True((File.GetAttributes(target) & FileAttributes.ReadOnly) != 0);
        Assert.Equal(f.FinalWriteTime, File.GetLastWriteTimeUtc(target).ToFileTimeUtc());
        Assert.True(checkedPin); f.AssertRestored();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnrecordedReadOnlyEffectsRejectChangedBytesAndMatchingByteReplacements(bool replacement)
    {
        using var f = new Fixture(129, true, rename: true);
        await Assert.ThrowsAsync<IOException>(() => f.Restore(stage =>
        { if (stage == "restore.before-cursor") throw new IOException("lost batch"); }));
        string target = Path.Combine(f.State.OriginalPath, "renamed", "f0000");
        byte[] bytes = File.ReadAllBytes(target); var attributes = File.GetAttributes(target);
        if (replacement)
        {
            File.Move(target, Path.Combine(f.Root, "preserved-file")); File.WriteAllBytes(target, bytes);
        }
        else
        {
            File.SetAttributes(target, FileAttributes.Normal); bytes[0] ^= 1; File.WriteAllBytes(target, bytes);
        }
        File.SetAttributes(target, attributes);
        Assert.Equal("GeneratedRestoreConflict", (await Assert.ThrowsAsync<WorkspaceException>(() => f.Restore())).Code);
        Assert.Equal(bytes, File.ReadAllBytes(target)); Assert.Equal(attributes, File.GetAttributes(target));
        Assert.True(Directory.Exists(f.Request.DeltaPath));
    }

    private sealed class Fixture : IDisposable
    {
        private readonly TestDirectory directory = new();
        private readonly int count;
        private readonly bool rename;
        private readonly int payloadBytes;
        public string Root => State.Basis.ReceiptDirectory;
        public ManagedDirectoryState State { get; }
        public OverlayServeRequest Request { get; }
        public long FinalWriteTime { get; } = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc).ToFileTimeUtc();
        public Fixture(int count, bool optimized, bool rename = false, int payloadBytes = 64)
        {
            if (!OperatingSystem.IsWindows()) Assert.Skip("Windows owned-file identity contract.");
            this.count = count; this.rename = rename; this.payloadBytes = payloadBytes;
            string root = Path.Combine(directory.Path, "owned"), basis = Path.Combine(root, "base");
            Directory.CreateDirectory(Path.Combine(basis, "dir"));
            for (int i = 0; i < count; i++) File.WriteAllBytes(Path.Combine(basis, "dir", $"f{i:D4}"), Bytes(i));
            if (rename) File.SetAttributes(Path.Combine(basis, "dir", "f0000"), FileAttributes.ReadOnly);
            var snapshot = new SnapshotReference("012345678901234567890123", OwnedDirectorySnapshotTree.ProviderName, basis, basis, DateTimeOffset.UnixEpoch, true, true, "stopped-original-owned-directory");
            var manifest = OwnedDirectorySnapshotTree.Capture(snapshot, snapshot.Id); var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
            File.WriteAllText(Path.Combine(root, "manifest.json"), JsonSerializer.Serialize(manifest, json));
            string hash = ManagedDirectoryBasis.HashFile(Path.Combine(root, "manifest.json"));
            File.WriteAllText(Path.Combine(root, "proof.json"), JsonSerializer.Serialize(new OwnedDirectoryProof(1, snapshot, snapshot.Id, hash, manifest.Items.Sum(i => i.Entry.Length)), json));
            State = new() { ApplicationKind = "unity", DirectoryKind = "Library", Key = "fixture", OriginalPath = Path.Combine(directory.Path, "original", "Library"), CompatibilityKey = "fixture", Phase = "restoring",
                Basis = new(snapshot.Id, Path.Combine(directory.Path, "original"), "Library", snapshot, root, snapshot.Id, hash) };
            Request = new(new(2, snapshot.Id, snapshot, 4 * 1048576, 65536), Path.Combine(root, "delta"), State.OriginalPath, Path.Combine(root, "overlay"), "token", new(snapshot.Id, basis, hash), root, snapshot.Id);
            using var tree = new OwnedDirectorySnapshotTree(manifest, snapshot);
            using var journal = new JournalDeltaStore(Request.DeltaPath, Request.Identity, tree, optimizeJournal: optimized);
            if (rename)
            {
                journal.Rename(journal.Stat("dir")!.Id, "renamed", false);
                var file = journal.Stat("renamed/f0000")!;
                journal.SetMetadata(file.Id, file.Attributes, file.CreationTime, file.LastAccessTime, FinalWriteTime);
            }
        }
        private byte[] Bytes(int i) => Enumerable.Range(0, payloadBytes).Select(j => (byte)(i + j)).ToArray();
        public Task Restore(Action<string>? boundary = null) => ManagedDirectoryRestore.ExecuteAsync(State, Request, TestContext.Current.CancellationToken, boundary);
        public void AssertRestored()
        {
            for (int i = 0; i < count; i++) Assert.Equal(Bytes(i), File.ReadAllBytes(Path.Combine(State.OriginalPath, rename ? "renamed" : "dir", $"f{i:D4}")));
            Assert.False(Directory.Exists(State.Basis.Snapshot.RootPath)); Assert.False(Directory.Exists(Request.DeltaPath));
        }
        public void Dispose() => directory.Dispose();
    }
}
