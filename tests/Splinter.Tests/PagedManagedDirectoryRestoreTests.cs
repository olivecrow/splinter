using System.Text.Json;
using Splinter.Core;
using Splinter.Platform.Windows;
using Splinter.Runtime;

namespace Splinter.Tests;

public sealed class PagedManagedDirectoryRestoreTests
{
    [Fact]
    public async Task NativeOfflineReplayRolloverKeepsSourceImmutableAndRestoresThenReclaimsTheExactManagedBasis()
    {
        using var fixture = new Fixture(extraFiles: 1025); var ct = TestContext.Current.CancellationToken; var pins = new WindowsJournalFilePins();
        string control = fixture.State.Basis.ReceiptDirectory;
        var manifest = new OwnedDirectorySnapshotProvider(control, fixture.State.Basis.OwnerId).Read(fixture.State.Basis.Snapshot);
        (SnapshotDeltaSeal Seal, string Index, string FinalFolder) tail;
        using (var basis = new OwnedDirectorySnapshotTree(manifest, manifest.Snapshot))
            tail = JournalReadOnlyReplayTests.SeedLargeDerivedTail(control, pins, basis, fixture.Request.Identity, "bulk",
                Enumerable.Range(0, 1025).Select(index => "bulk/" + index.ToString("d6")));
        string[] before = Inventory(); int checkedReplays = 0;
        await ManagedPagedDirectoryRestore.ExecuteAsync(fixture.State, fixture.Request, manifest, ct, stage =>
        {
            if (stage is not ("restore.plan-published" or "restore.after-verified")) return;
            Assert.Equal(before, Inventory());
            string scope = Assert.Single(Directory.EnumerateDirectories(Path.Combine(control, "read-only-replays")));
            string[] containers = Directory.EnumerateFiles(scope, "pages-*.bin", SearchOption.AllDirectories).ToArray();
            Assert.True(containers.Length >= 2, "The actual replay must roll over the real 64 MiB native container size.");
            Assert.True(containers.Sum(path => new FileInfo(path).Length) > PagedMetadataStore.ContainerTargetBytes);
            foreach (string path in containers)
                PagedMetadataStore.ValidateCreatedContainer(path, PagedMetadataStore.ReadIdentity(Path.GetDirectoryName(path)!), pins);
            checkedReplays++;
        });
        Assert.Equal(2, checkedReplays);
        Assert.Equal(1025, Directory.EnumerateFiles(Path.Combine(fixture.State.OriginalPath, tail.FinalFolder)).LongCount());
        Assert.Equal(new byte[] { 244 }, File.ReadAllBytes(Path.Combine(fixture.State.OriginalPath, tail.FinalFolder, "000500")));
        Assert.Equal(new byte[] { 1, 7, 3 }, File.ReadAllBytes(Path.Combine(fixture.State.OriginalPath, "renamed", "value")));
        Assert.Equal(new byte[] { 9, 0, 6 }, File.ReadAllBytes(Path.Combine(fixture.State.OriginalPath, "new")));
        Assert.False(Directory.Exists(fixture.Request.DeltaPath)); Assert.False(Directory.Exists(fixture.State.Basis.Snapshot.RootPath));
        Assert.Empty(JournalDeltaStore.EnumeratePreservedReadOnlyReplays(control, fixture.Request.Identity));

        string[] Inventory() => Directory.EnumerateFiles(tail.Index, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal)
            .Select(path =>
            {
                using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                return string.Join('|', Path.GetRelativePath(tail.Index, path), input.Length, pins.IdentifyOpened(input),
                    Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(input)));
            }).ToArray();
    }

    [Fact]
    public async Task OfflinePlanningLeavesEverySourceIndexByteAndNativeIdentityUntouchedBeforeExactRestoreResume()
    {
        using var fixture = new Fixture(); var ct = TestContext.Current.CancellationToken; var pins = new WindowsJournalFilePins();
        var manifest = new OwnedDirectorySnapshotProvider(fixture.State.Basis.ReceiptDirectory, fixture.State.Basis.OwnerId).Read(fixture.State.Basis.Snapshot);
        var before = IndexInventory();
        await Assert.ThrowsAsync<IOException>(() => ManagedPagedDirectoryRestore.ExecuteAsync(fixture.State, fixture.Request, manifest, ct,
            stage => { if (stage == "restore.plan-published") throw new IOException("Stop after offline replay and immutable planning."); }));
        Assert.Equal(before, IndexInventory());
        await ManagedPagedDirectoryRestore.ExecuteAsync(fixture.State, fixture.Request, manifest, ct, null);
        Assert.Equal(new byte[] { 1, 7, 3 }, File.ReadAllBytes(Path.Combine(fixture.State.OriginalPath, "renamed", "value")));
        Assert.Equal(new byte[] { 9, 0, 6 }, File.ReadAllBytes(Path.Combine(fixture.State.OriginalPath, "new")));
        Assert.False(Directory.Exists(fixture.Request.DeltaPath)); Assert.False(Directory.Exists(fixture.State.Basis.Snapshot.RootPath));

        string[] IndexInventory() => Directory.EnumerateDirectories(fixture.Request.DeltaPath, "index-*")
            .SelectMany(path => Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories)).Order(StringComparer.Ordinal)
            .Select(path => string.Join('|', Path.GetRelativePath(fixture.Request.DeltaPath, path), new FileInfo(path).Length,
                pins.Identify(path), Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path))))).ToArray();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("restore.plan-published")]
    [InlineData("file.after-move")]
    [InlineData("file.after-ranges-flush")]
    [InlineData("restore.before-cursor")]
    [InlineData("restore.after-verified")]
    [InlineData("restore.after-basis-reclaim")]
    public async Task PagedPlanMovesExactFilesAndResumesFrozenRangeReplay(string? boundary)
    {
        using var fixture = new Fixture(); var ct = TestContext.Current.CancellationToken;
        var manifest = new OwnedDirectorySnapshotProvider(fixture.State.Basis.ReceiptDirectory, fixture.State.Basis.OwnerId).Read(fixture.State.Basis.Snapshot);
        if (boundary is not null)
        {
            bool injected = false;
            await Assert.ThrowsAsync<IOException>(() => ManagedPagedDirectoryRestore.ExecuteAsync(fixture.State, fixture.Request, manifest, ct, stage =>
            { if (!injected && stage == boundary) { injected = true; throw new IOException("fixed paged interruption"); } }));
            Assert.True(injected);
        }
        await ManagedPagedDirectoryRestore.ExecuteAsync(fixture.State, fixture.Request, manifest, ct, null);
        Assert.Equal(new byte[] { 1, 7, 3 }, File.ReadAllBytes(Path.Combine(fixture.State.OriginalPath, "renamed", "value")));
        Assert.Equal(new byte[] { 9, 0, 6 }, File.ReadAllBytes(Path.Combine(fixture.State.OriginalPath, "new")));
        Assert.Equal(new byte[] { 5 }, File.ReadAllBytes(Path.Combine(fixture.State.OriginalPath, "unchanged")));
        Assert.False(File.Exists(Path.Combine(fixture.State.OriginalPath, "deleted")));
        Assert.False(Directory.Exists(fixture.State.Basis.Snapshot.RootPath)); Assert.False(Directory.Exists(fixture.Request.DeltaPath));
    }

    [Fact]
    public async Task SmallFinalNamespaceWithManyPrivateRangesAlsoSelectsStreamingRestore()
    {
        using var fixture = new Fixture(); var ct = TestContext.Current.CancellationToken;
        var manifest = new OwnedDirectorySnapshotProvider(fixture.State.Basis.ReceiptDirectory, fixture.State.Basis.OwnerId).Read(fixture.State.Basis.Snapshot);
        using (var basis = new OwnedDirectorySnapshotTree(manifest, manifest.Snapshot))
        using (var source = new JournalDeltaStore(fixture.Request.DeltaPath, fixture.Request.Identity, basis, sourceFilePins: new WindowsJournalFilePins()))
        {
            var file = source.Create("fragmented", false, 32);
            for (int index = 0; index < 1025; index++) source.Write(file.Id, 2L * index, new byte[] { 7 });
        }
        await ManagedDirectoryRestore.ExecuteAsync(fixture.State, fixture.Request, ct);
        Assert.True(File.Exists(Path.Combine(fixture.State.Basis.ReceiptDirectory, "paged-restore-plan.json")));
        Assert.False(File.Exists(Path.Combine(fixture.State.Basis.ReceiptDirectory, "restore-plan.json")));
        byte[] result = File.ReadAllBytes(Path.Combine(fixture.State.OriginalPath, "fragmented")); Assert.Equal(2049, result.Length);
        for (int index = 0; index < result.Length; index++) Assert.Equal((byte)(index % 2 == 0 ? 7 : 0), result[index]);
    }

    [Fact]
    public async Task SmallBasisWithPrivateFilesAbovePlanningBudgetSelectsStreamingRestore()
    {
        // Real NTFS files and the product stopped SJR2 journal/restore path.
        // This is not a mounted WinFsp/Unity or million-durable-ACK claim.
        using var fixture = new Fixture(); var ct = TestContext.Current.CancellationToken;
        int count = MetadataTestScale.Large ? 32769 : 1025;
        var manifest = new OwnedDirectorySnapshotProvider(fixture.State.Basis.ReceiptDirectory, fixture.State.Basis.OwnerId).Read(fixture.State.Basis.Snapshot);
        Assert.True(manifest.ItemCount < 1024);
        using (var basis = new OwnedDirectorySnapshotTree(manifest, manifest.Snapshot))
        using (var source = new JournalDeltaStore(fixture.Request.DeltaPath, fixture.Request.Identity, basis, sourceFilePins: new WindowsJournalFilePins()))
        {
            source.Create("many", true, 16);
            foreach (int[] chunk in Enumerable.Range(0, count).Chunk(IWorkspaceWriteBatch.MaximumWrites))
                source.ApplyMetadataBatch(chunk.Select(index => (Action)(() =>
                { ct.ThrowIfCancellationRequested(); source.Create("many/" + index.ToString("d8"), false, 32); })).ToArray());
        }
        await ManagedDirectoryRestore.ExecuteAsync(fixture.State, fixture.Request, ct);
        Assert.True(File.Exists(Path.Combine(fixture.State.Basis.ReceiptDirectory, "paged-restore-plan.json")));
        Assert.False(File.Exists(Path.Combine(fixture.State.Basis.ReceiptDirectory, "restore-plan.json")));
        Assert.Equal(count, Directory.EnumerateFiles(Path.Combine(fixture.State.OriginalPath, "many")).LongCount());
        Assert.False(Directory.Exists(fixture.Request.DeltaPath));
    }

    [Fact]
    public async Task StoppedRestoreResumesItsExactInterruptedPrivatePayloadPackingBeforePlanning()
    {
        using var fixture = new Fixture(); var ct = TestContext.Current.CancellationToken;
        var manifest = new OwnedDirectorySnapshotProvider(fixture.State.Basis.ReceiptDirectory, fixture.State.Basis.OwnerId).Read(fixture.State.Basis.Snapshot);
        using (var basis = new OwnedDirectorySnapshotTree(manifest, manifest.Snapshot))
        using (var source = new JournalDeltaStore(fixture.Request.DeltaPath, fixture.Request.Identity, basis, sourceFilePins: new WindowsJournalFilePins()))
        {
            var file = source.Create("packed", false, 32); source.Write(file.Id, 0, Enumerable.Repeat((byte)0x53, 8192).ToArray()); source.SetLength(file.Id, 16);
            Assert.Throws<IOException>(() => source.MaintainLiveStorage(ct: ct, collectSegments: true,
                boundary: point => { if (point == "segment-pack.payload-durable") throw new IOException("stopped exact packing request"); }));
        }
        await ManagedDirectoryRestore.ExecuteAsync(fixture.State, fixture.Request, ct);
        Assert.Equal(Enumerable.Repeat((byte)0x53, 16), File.ReadAllBytes(Path.Combine(fixture.State.OriginalPath, "packed")));
        Assert.False(Directory.Exists(fixture.Request.DeltaPath));
        Assert.Empty(Directory.EnumerateFileSystemEntries(Path.Combine(fixture.State.Basis.ReceiptDirectory, "journal-packing")));
    }

    [Fact]
    public async Task LargeManagedInputSelectsPagedPlanInsteadOfArrayRestore()
    {
        using var fixture = new Fixture(extraFiles: 1025); var ct = TestContext.Current.CancellationToken;
        await ManagedDirectoryRestore.ExecuteAsync(fixture.State, fixture.Request, ct);
        Assert.True(File.Exists(Path.Combine(fixture.State.Basis.ReceiptDirectory, "paged-restore-plan.json")));
        Assert.False(File.Exists(Path.Combine(fixture.State.Basis.ReceiptDirectory, "restore-plan.json")));
        Assert.Equal(1025, Directory.EnumerateFiles(Path.Combine(fixture.State.OriginalPath, "bulk")).LongCount());
    }

    [Fact]
    public async Task LargeStableTopologyMovesItsRootAndPreservesUnchangedFileIdentities()
    {
        using var fixture = new Fixture(extraFiles: 1025, rename: false); var ct = TestContext.Current.CancellationToken;
        string path = Path.Combine(fixture.State.Basis.Snapshot.RootPath, "bulk", "000500"); string identity;
        using (var file = ManagedRestoreFileAccess.Read(path)) identity = ManagedRestoreFileAccess.Identity(file);
        bool moved = false;
        await ManagedDirectoryRestore.ExecuteAsync(fixture.State, fixture.Request, ct, stage => { if (stage == "paged-root.after-move") moved = true; });
        Assert.True(moved); using var retained = ManagedRestoreFileAccess.Read(Path.Combine(fixture.State.OriginalPath, "bulk", "000500"));
        Assert.Equal(identity, ManagedRestoreFileAccess.Identity(retained)); Assert.Equal(new byte[] { 1, 7, 3 }, File.ReadAllBytes(Path.Combine(fixture.State.OriginalPath, "dir", "value")));
        Assert.False(File.Exists(Path.Combine(fixture.State.OriginalPath, "deleted")));
    }

    [Fact]
    public async Task ReplacedCompletedFileWithSameBytesIsPreservedAndBlocksCleanup()
    {
        using var fixture = new Fixture(); var ct = TestContext.Current.CancellationToken;
        var manifest = new OwnedDirectorySnapshotProvider(fixture.State.Basis.ReceiptDirectory, fixture.State.Basis.OwnerId).Read(fixture.State.Basis.Snapshot);
        await Assert.ThrowsAsync<IOException>(() => ManagedPagedDirectoryRestore.ExecuteAsync(fixture.State, fixture.Request, manifest, ct,
            stage => { if (stage == "restore.after-verified") throw new IOException("before cleanup"); }));
        string file = Path.Combine(fixture.State.OriginalPath, "new"); byte[] bytes = File.ReadAllBytes(file);
        File.Move(file, Path.Combine(fixture.State.Basis.ReceiptDirectory, "retained-native-file")); File.WriteAllBytes(file, bytes);
        Assert.Equal("GeneratedRestoreConflict", (await Assert.ThrowsAsync<WorkspaceException>(() =>
            ManagedPagedDirectoryRestore.ExecuteAsync(fixture.State, fixture.Request, manifest, ct, null))).Code);
        Assert.Equal(bytes, File.ReadAllBytes(file)); Assert.True(Directory.Exists(fixture.Request.DeltaPath));
    }

    [Fact(Timeout = 60000)]
    public async Task UnchangedDeepPagedRootRetainsEveryNativeIdentityAndByte()
    {
        using var fixture = new Fixture(extraFiles: 1025, rename: false, mutate: false, deep: true);
        var ct = TestContext.Current.CancellationToken; string basis = fixture.State.Basis.Snapshot.RootPath;
        var expected = Directory.EnumerateFiles(basis, "*", SearchOption.AllDirectories).ToDictionary(
            path => Path.GetRelativePath(basis, path), path =>
            { using var file = ManagedRestoreFileAccess.Read(path); return (Identity: ManagedRestoreFileAccess.Identity(file), Bytes: File.ReadAllBytes(path)); });
        string rootIdentity;
        using (var root = ManagedRestoreFileAccess.DirectoryHandle(basis, metadataWrite: false))
            rootIdentity = SnapshotInputChecks.FileIdentity(root, directory: true);
        var clock = System.Diagnostics.Stopwatch.StartNew();
        bool protectedParent = false;
        await ManagedDirectoryRestore.ExecuteAsync(fixture.State, fixture.Request, ct, stage =>
        {
            if (stage != "file.after-complete" || protectedParent) return;
            string parent = Path.GetDirectoryName(fixture.State.OriginalPath)!;
            Assert.ThrowsAny<IOException>(() => Directory.Move(parent, parent + "-unowned-move"));
            protectedParent = true;
        });
        Assert.True(protectedParent);
        using (var plan = JsonDocument.Parse(File.ReadAllText(Path.Combine(fixture.State.Basis.ReceiptDirectory, "paged-restore-plan.json"))))
        { Assert.True(plan.RootElement.GetProperty("unchanged").GetBoolean()); Assert.Equal(JsonValueKind.Null, plan.RootElement.GetProperty("files").GetProperty("page").ValueKind); }
        TestContext.Current.TestOutputHelper!.WriteLine($"Restore-only seconds: {clock.Elapsed.TotalSeconds:F6}");
        using var restoredRoot = ManagedRestoreFileAccess.DirectoryHandle(fixture.State.OriginalPath, metadataWrite: false);
        Assert.Equal(rootIdentity, SnapshotInputChecks.FileIdentity(restoredRoot, directory: true));
        foreach (var pair in expected)
        {
            ct.ThrowIfCancellationRequested(); string path = Path.Combine(fixture.State.OriginalPath, pair.Key);
            using var file = ManagedRestoreFileAccess.Read(path);
            Assert.Equal(pair.Value.Identity, ManagedRestoreFileAccess.Identity(file)); Assert.Equal(pair.Value.Bytes, File.ReadAllBytes(path));
        }
        Assert.Equal(expected.Count, Directory.EnumerateFiles(fixture.State.OriginalPath, "*", SearchOption.AllDirectories).Count());
        Assert.False(Directory.Exists(basis)); Assert.False(Directory.Exists(fixture.Request.DeltaPath));
    }

    [Theory]
    [InlineData("restore.plan-published")]
    [InlineData("paged-root.before-move")]
    [InlineData("paged-root.after-move")]
    [InlineData("restore.after-proof")]
    [InlineData("file.after-complete")]
    [InlineData("restore.after-verified")]
    [InlineData("restore.reclaim.intent")]
    public async Task UnchangedPagedRestoreResumesExactOwnershipAcrossEveryPublicationBoundary(string point)
    {
        using var fixture = new Fixture(extraFiles: 17, rename: false, mutate: false, deep: true);
        var ct = TestContext.Current.CancellationToken; var native = new WindowsJournalFilePins();
        var manifest = new OwnedDirectorySnapshotProvider(fixture.State.Basis.ReceiptDirectory, fixture.State.Basis.OwnerId).Read(fixture.State.Basis.Snapshot);
        var before = Directory.EnumerateFiles(fixture.State.Basis.Snapshot.RootPath, "*", SearchOption.AllDirectories).ToDictionary(
            path => Path.GetRelativePath(fixture.State.Basis.Snapshot.RootPath, path), path => (Id: native.Identify(path), Bytes: File.ReadAllBytes(path)));
        bool injected = false;
        await Assert.ThrowsAsync<IOException>(() => ManagedPagedDirectoryRestore.ExecuteAsync(fixture.State, fixture.Request, manifest, ct, stage =>
        { if (!injected && stage == point) { injected = true; throw new IOException("unchanged exact restart"); } }));
        Assert.True(injected);
        await ManagedPagedDirectoryRestore.ExecuteAsync(fixture.State, fixture.Request, manifest, ct, null);
        foreach (var pair in before)
        {
            string path = Path.Combine(fixture.State.OriginalPath, pair.Key);
            Assert.Equal(pair.Value.Id, native.Identify(path)); Assert.Equal(pair.Value.Bytes, File.ReadAllBytes(path));
        }
        Assert.Equal(before.Count, Directory.EnumerateFiles(fixture.State.OriginalPath, "*", SearchOption.AllDirectories).Count());
        Assert.False(Directory.Exists(fixture.Request.DeltaPath)); Assert.False(Directory.Exists(fixture.State.Basis.Snapshot.RootPath));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnchangedPagedResumePreservesSameByteReplacementAndUnknownChildren(bool unknownChild)
    {
        using var fixture = new Fixture(extraFiles: 17, rename: false, mutate: false, deep: true);
        var ct = TestContext.Current.CancellationToken;
        var manifest = new OwnedDirectorySnapshotProvider(fixture.State.Basis.ReceiptDirectory, fixture.State.Basis.OwnerId).Read(fixture.State.Basis.Snapshot);
        await Assert.ThrowsAsync<IOException>(() => ManagedPagedDirectoryRestore.ExecuteAsync(fixture.State, fixture.Request, manifest, ct,
            stage => { if (stage == "restore.after-verified") throw new IOException("retain exact restored tree"); }));
        string path = Path.Combine(fixture.State.OriginalPath, unknownChild ? "unknown" : "unchanged");
        if (!unknownChild) File.Move(path, Path.Combine(fixture.State.Basis.ReceiptDirectory, "retained-original-leaf"));
        File.WriteAllBytes(path, [5]);
        Assert.Equal("GeneratedRestoreConflict", (await Assert.ThrowsAsync<WorkspaceException>(() =>
            ManagedPagedDirectoryRestore.ExecuteAsync(fixture.State, fixture.Request, manifest, ct, null))).Code);
        Assert.Equal(new byte[] { 5 }, File.ReadAllBytes(path)); Assert.True(Directory.Exists(fixture.Request.DeltaPath));
    }

    [Fact]
    public async Task UnchangedPagedPlanningPreservesAnUnknownBasisChildBeforeAnyMove()
    {
        using var fixture = new Fixture(extraFiles: 17, rename: false, mutate: false);
        string unknown = Path.Combine(fixture.State.Basis.Snapshot.RootPath, "unowned"); File.WriteAllBytes(unknown, [37]);
        var manifest = new OwnedDirectorySnapshotProvider(fixture.State.Basis.ReceiptDirectory, fixture.State.Basis.OwnerId).Read(fixture.State.Basis.Snapshot);
        Assert.Equal("GeneratedRestoreConflict", (await Assert.ThrowsAsync<WorkspaceException>(() =>
            ManagedPagedDirectoryRestore.ExecuteAsync(fixture.State, fixture.Request, manifest, TestContext.Current.CancellationToken, null))).Code);
        Assert.Equal(new byte[] { 37 }, File.ReadAllBytes(unknown)); Assert.False(Directory.Exists(fixture.State.OriginalPath));
        Assert.True(Directory.Exists(fixture.Request.DeltaPath));
    }

    [Fact]
    public async Task UnchangedPagedReaderHandoffRetainsTheOriginalAncestorLease()
    {
        using var fixture = new Fixture(extraFiles: 17, rename: false, mutate: false);
        string parent = Path.GetDirectoryName(fixture.State.OriginalPath)!; Directory.CreateDirectory(parent);
        var manifest = new OwnedDirectorySnapshotProvider(fixture.State.Basis.ReceiptDirectory, fixture.State.Basis.OwnerId).Read(fixture.State.Basis.Snapshot);
        bool observed = false;
        await ManagedPagedDirectoryRestore.ExecuteAsync(fixture.State, fixture.Request, manifest, TestContext.Current.CancellationToken, stage =>
        {
            if (stage != "unchanged.after-readers-closed") return;
            observed = true;
            Assert.Throws<WorkspaceException>(() => { using var foreign = ManagedRestoreFileAccess.DirectoryHandle(parent, moving: true); });
        });
        Assert.True(observed); Assert.False(Directory.Exists(fixture.Request.DeltaPath));
    }

    private sealed class Fixture : IDisposable
    {
        private readonly TestDirectory directory = new();
        public ManagedDirectoryState State { get; }
        public OverlayServeRequest Request { get; }
        public Fixture(int extraFiles = 0, bool rename = true, bool mutate = true, bool deep = false)
        {
            string root = Path.Combine(directory.Path, "owned"), basisPath = Path.Combine(root, "base"); Directory.CreateDirectory(Path.Combine(basisPath, "dir"));
            File.WriteAllBytes(Path.Combine(basisPath, "dir", "value"), [1, 2, 3]); File.WriteAllBytes(Path.Combine(basisPath, "unchanged"), [5]);
            File.WriteAllBytes(Path.Combine(basisPath, "deleted"), [4]);
            if (extraFiles != 0)
            {
                string bulk = Path.Combine(basisPath, deep ? "bulk/packages/example/editor/resources/fixtures/nested/paths/content" : "bulk");
                Directory.CreateDirectory(bulk);
                for (int i = 0; i < extraFiles; i++) File.WriteAllBytes(Path.Combine(bulk, i.ToString("d6")), [(byte)i]);
            }
            var snapshot = new SnapshotReference("abcdef0123456789abcdef01", OwnedDirectorySnapshotTree.ProviderName, basisPath, basisPath,
                DateTimeOffset.UnixEpoch, true, true, "stopped-original-owned-directory");
            var manifest = OwnedDirectorySnapshotTree.Capture(snapshot, snapshot.Id, Path.Combine(root, OwnedDirectoryManifest.MetadataDirectoryName));
            var json = new JsonSerializerOptions(JsonSerializerDefaults.Web); string manifestPath = Path.Combine(root, "manifest.json");
            File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest, json)); string hash = ManagedDirectoryBasis.HashFile(manifestPath);
            File.WriteAllText(Path.Combine(root, "proof.json"), JsonSerializer.Serialize(new OwnedDirectoryProof(1, snapshot, snapshot.Id, hash, manifest.Items.Sum(i => i.Entry.Length)), json));
            State = new() { ApplicationKind = "unity", DirectoryKind = "Library", Key = "paged-fixture", OriginalPath = Path.Combine(directory.Path, "original", "Library"),
                CompatibilityKey = "fixture", Phase = "restoring", Basis = new(snapshot.Id, Path.Combine(directory.Path, "original"), "Library", snapshot, root, snapshot.Id, hash) };
            Request = new(new(3, snapshot.Id, snapshot, long.MaxValue, 65536), Path.Combine(root, "delta"), State.OriginalPath, Path.Combine(root, "overlay"),
                "token", new(snapshot.Id, basisPath, hash), root, snapshot.Id);
            using var tree = new OwnedDirectorySnapshotTree(manifest, snapshot); using var writer = new JournalDeltaStore(Request.DeltaPath, Request.Identity, tree, sourceFilePins: new WindowsJournalFilePins());
            if (!mutate) return;
            writer.Write(writer.Stat("dir/value")!.Id, 1, [7]); if (rename) writer.Rename(writer.Stat("dir")!.Id, "renamed", false); writer.Delete(writer.Stat("deleted")!.Id);
            var created = writer.Create("new", false, 32); writer.Write(created.Id, 0, [9]); writer.Write(created.Id, 2, [6]);
        }
        public void Dispose() => directory.Dispose();
    }
}
