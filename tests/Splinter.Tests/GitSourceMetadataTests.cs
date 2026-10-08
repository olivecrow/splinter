using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Splinter.Core;
using Splinter.Platform.Windows;
using Splinter.Protocol;
using Splinter.Runtime;
using Splinter.SourceControl.Git;

namespace Splinter.Tests;

public sealed class GitSourceMetadataTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Theory]
    [InlineData("none")]
    [InlineData("library-only")]
    [InlineData("metadata")]
    public async Task SeparateWorkspaceStoreReopensActualProviderIdentityWithExactOptionalMetadata(string selection)
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Requires Windows same-handle source capture.");
        using var f = await Fixture.Create();
        GeneratedDirectoryReference? basis = selection == "none" ? null : f.Basis;
        if (selection == "metadata") basis = f.Save((await WindowsGitSourceMetadata.CaptureAsync(f.Source, f.Basis, TestContext.Current.CancellationToken))!);
        var store = new WorkspaceStore(Path.Combine(Path.GetDirectoryName(f.Source)!, "home"));
        string id = WorkspaceStore.Key("separate-store");
        var provider = new GitObjectSnapshotProvider(f.Receipt, id, f.Revision, basis);
        var snapshot = await provider.CaptureAsync(new(f.Source, id, f.Receipt), TestContext.Current.CancellationToken);
        var record = MetadataWorkspace(store, id, f.Source, f.Revision, basis, snapshot);
        store.Save(record, "fixture-attached");
        var reopened = new WorkspaceStore(store.Root).Get(id);
        Assert.Equal(snapshot, reopened.Snapshot!.Reference);
        Assert.Equal(basis, reopened.GeneratedDirectory);
        Assert.Equal(record.Snapshot!.Mount, reopened.Snapshot.Mount);
        using (var reader = new GitObjectSnapshotProvider(f.Receipt, id).OpenTree(reopened.Snapshot.Reference!,
            await provider.ResolveReadAccessAsync(snapshot, TestContext.Current.CancellationToken)))
            Assert.Equal(f.Data.Length, reader.Stat("data.bin")!.Length);
        await provider.ReleaseAsync(snapshot, TestContext.Current.CancellationToken);
    }

    [Theory]
    [InlineData("hash-mismatch")]
    [InlineData("hash-removed")]
    [InlineData("hash-injected")]
    [InlineData("hash-empty")]
    [InlineData("hash-short")]
    [InlineData("hash-upper")]
    [InlineData("hash-nonhex")]
    [InlineData("provider")]
    [InlineData("consistency")]
    [InlineData("source")]
    [InlineData("revision")]
    public void SeparateWorkspaceStoreRejectsMetadataIdentityChangesWithoutMutatingRecord(string change)
    {
        using var directory = new TestDirectory();
        var store = new WorkspaceStore(Path.Combine(directory.Path, "home"));
        string id = WorkspaceStore.Key("task"), project = Path.Combine(directory.Path, "source"), revision = new('a', 40);
        string libraryId = WorkspaceStore.Key("library"), libraryRoot = Path.Combine(directory.Path, "library");
        var library = new GeneratedDirectoryReference(libraryId, project, "Library",
            new(libraryId, "owned-directory", libraryRoot, libraryRoot, DateTimeOffset.UnixEpoch, true, true, "stopped-original-owned-directory"),
            libraryRoot, libraryId, new('b', 64), new('c', 64));
        string baseId = "git-" + id + "-" + revision;
        var snapshot = new SnapshotReference(baseId + "-metadata-" + library.SourceMetadataHash, "git-objects", project,
            Path.Combine(project, ".git"), DateTimeOffset.UnixEpoch, true, true, "recorded-project-commit-raw-blobs");
        if (change == "hash-mismatch") library = library with { SourceMetadataHash = new('d', 64) };
        else if (change == "hash-removed") library = library with { SourceMetadataHash = null };
        else if (change == "hash-injected") snapshot = snapshot with { Id = baseId };
        else if (change.StartsWith("hash-", StringComparison.Ordinal))
        {
            string invalid = change switch { "hash-empty" => "", "hash-short" => new('c', 63), "hash-upper" => new('C', 64), _ => new('z', 64) };
            library = library with { SourceMetadataHash = invalid };
            // Even an ID carrying the same malformed digest must be rejected.
            snapshot = snapshot with { Id = baseId + "-metadata-" + invalid };
        }
        else if (change == "provider") snapshot = snapshot with { Provider = "foreign" };
        else if (change == "consistency") snapshot = snapshot with { Consistency = "foreign" };
        else if (change == "source") snapshot = snapshot with { SourcePath = project + "-foreign" };
        else revision = new('d', 40);
        var record = MetadataWorkspace(store, id, project, revision, library, snapshot);
        store.Save(record, "fixture-corrupt");
        string statePath = Path.Combine(store.ControlPath(id), "workspace.json");
        byte[] before = File.ReadAllBytes(statePath);
        Assert.Equal("SnapshotIdentityMismatch", Assert.Throws<WorkspaceException>(() => new WorkspaceStore(store.Root).Get(id)).Code);
        Assert.Equal(before, File.ReadAllBytes(statePath));
    }

    private static WorkspaceRecord MetadataWorkspace(WorkspaceStore store, string id, string project, string revision,
        GeneratedDirectoryReference? library, SnapshotReference snapshot) => new()
    {
        Id = id, RequestKey = "metadata-store", ProjectPath = project, Path = store.WorkPath(id),
        SourceReference = "splinter/" + id, BaseReference = "development", BaseRevision = revision,
        SourceKind = "git", ApplicationKind = "unity", StorageMode = "git-snapshot", Phase = "ready",
        GeneratedDirectoryId = library?.Id, GeneratedDirectory = library,
        Snapshot = new() { Attached = true, Reference = snapshot,
            Mount = new(id, store.WorkPath(id), Path.Combine(store.ControlPath(id), "delta"), snapshot, 123, 1) }
    };

    [Fact]
    public async Task PublishedMetadataReferenceAndReceiptMatchTheirStrictSchemas()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Requires Windows same-handle source capture.");
        using var f = await Fixture.Create();
        var map = (await WindowsGitSourceMetadata.CaptureAsync(f.Source, f.Basis, TestContext.Current.CancellationToken))!;
        var basis = f.Save(map);
        var provider = new GitObjectSnapshotProvider(f.Receipt, "task", f.Revision, basis);
        var snapshot = await provider.CaptureAsync(new(f.Source, "task", f.Receipt), TestContext.Current.CancellationToken);
        var catalog = new ContractCatalog();
        using var publishedMap = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(basis.ReceiptDirectory, GitSourceMetadata.FileName)));
        using var publishedReceipt = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(f.Receipt, "git-basis.json")));
        Assert.True(catalog.Validate("urn:splinter:v1:git-source-metadata", publishedMap.RootElement).IsValid);
        Assert.True(catalog.Validate("urn:splinter:v1:generated-directory-reference", JsonSerializer.SerializeToElement(basis, Json)).IsValid);
        Assert.True(catalog.Validate("urn:splinter:v1:git-project-basis-receipt", publishedReceipt.RootElement).IsValid);

        var invalidMap = JsonNode.Parse(publishedMap.RootElement.GetRawText())!;
        invalidMap["version"] = 0;
        Assert.False(catalog.Validate("urn:splinter:v1:git-source-metadata", JsonSerializer.SerializeToElement(invalidMap)).IsValid);
        invalidMap["version"] = 1; invalidMap["basis"]!["sourceMetadataHash"] = basis.SourceMetadataHash;
        Assert.False(catalog.Validate("urn:splinter:v1:git-source-metadata", JsonSerializer.SerializeToElement(invalidMap)).IsValid);
        Assert.False(catalog.Validate("urn:splinter:v1:generated-directory-reference",
            JsonSerializer.SerializeToElement(basis with { SourceMetadataHash = "unknown" }, Json)).IsValid);
        var invalidReceipt = JsonNode.Parse(publishedReceipt.RootElement.GetRawText())!;
        invalidReceipt["sourceMetadata"]!.AsObject().Remove("sourceMetadataHash");
        Assert.False(catalog.Validate("urn:splinter:v1:git-project-basis-receipt", JsonSerializer.SerializeToElement(invalidReceipt)).IsValid);
        await provider.ReleaseAsync(snapshot, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task CaptureProvesRawContentAndSameHandleTimesThenOriginalRemainsWritable()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Requires Windows same-handle source capture.");
        using var f = await Fixture.Create();
        string path = Path.Combine(f.Source, "data.bin");
        long creation = File.GetCreationTimeUtc(path).ToFileTimeUtc();
        long written = File.GetLastWriteTimeUtc(path).ToFileTimeUtc();
        bool fenced = false;
        var map = (await WindowsGitSourceMetadata.CaptureAsync(f.Source, f.Basis, TestContext.Current.CancellationToken, opened: name =>
        {
            if (name != "data.bin") return;
            fenced = true;
            Assert.Throws<IOException>(() => File.WriteAllBytes(path, [4]));
            Assert.Throws<IOException>(() => File.Delete(path));
        }))!;
        Assert.True(fenced);
        var item = Assert.Single(map.Files, e => e.Path == "data.bin");
        Assert.Equal(creation, item.CreationTime); Assert.Equal(written, item.LastWriteTime);
        Assert.DoesNotContain(map.Files, e => e.Path is "line-endings.txt" or "changed.txt");
        Assert.Equal(f.Revision, map.Revision);
        Assert.Equal(4, map.ExaminedFiles);
        Assert.True(map.ExaminedBytes >= f.Data.Length + 8);
        Assert.True(map.CaptureTicks > 0); Assert.True(map.ClockFrequency > 0);
        File.WriteAllBytes(path, [9, 8, 7]);
        Assert.Equal(new byte[] { 9, 8, 7 }, File.ReadAllBytes(path));
    }

    [Fact]
    public async Task FrozenLibraryMapSurvivesOriginalChangesAndReconnectWithRawMismatchFallback()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Requires Windows same-handle source capture.");
        using var f = await Fixture.Create();
        var map = (await WindowsGitSourceMetadata.CaptureAsync(f.Source, f.Basis, TestContext.Current.CancellationToken))!;
        var basis = f.Save(map);
        var provider = new GitObjectSnapshotProvider(f.Receipt, "task", f.Revision, basis);
        var snapshot = await provider.CaptureAsync(new(f.Source, "task", f.Receipt), TestContext.Current.CancellationToken);
        Assert.EndsWith("-metadata-" + basis.SourceMetadataHash, snapshot.Id);
        File.WriteAllBytes(Path.Combine(f.Source, "data.bin"), [9, 8, 7]);
        File.SetLastWriteTimeUtc(Path.Combine(f.Source, "data.bin"), DateTime.UtcNow);
        var access = await provider.ResolveReadAccessAsync(snapshot, TestContext.Current.CancellationToken);
        for (int i = 0; i < 2; i++)
        {
            using var reader = new GitObjectSnapshotProvider(f.Receipt, "task").OpenTree(snapshot, access);
            Assert.Equal(map.Files.Single(e => e.Path == "data.bin").LastWriteTime, reader.Stat("data.bin")!.LastWriteTime);
            Assert.Equal(snapshot.CapturedAt.UtcDateTime.ToFileTimeUtc(), reader.Stat("line-endings.txt")!.LastWriteTime);
            Assert.Equal(snapshot.CapturedAt.UtcDateTime.ToFileTimeUtc(), reader.Stat("changed.txt")!.LastWriteTime);
            byte[] data = new byte[f.Data.Length]; Assert.Equal(data.Length, reader.Read("data.bin", 0, data)); Assert.Equal(f.Data, data);
        }
        Assert.Equal(snapshot, await provider.CaptureAsync(new(f.Source, "task", f.Receipt), TestContext.Current.CancellationToken));
        await provider.ReleaseAsync(snapshot, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task LaterCommitReusesOnlyUnchangedPathBlobAndLength()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Requires Windows same-handle source capture.");
        using var f = await Fixture.Create();
        var map = (await WindowsGitSourceMetadata.CaptureAsync(f.Source, f.Basis, TestContext.Current.CancellationToken))!;
        var basis = f.Save(map);
        byte[] changed = (byte[])f.Data.Clone(); changed[0] ^= 43;
        File.WriteAllBytes(Path.Combine(f.Source, "data.bin"), changed);
        await TestDirectory.Git(f.Source, "add", "data.bin"); await TestDirectory.Git(f.Source, "commit", "-m", "changed blob");
        string next = await TestDirectory.Git(f.Source, "rev-parse", "HEAD");
        var provider = new GitObjectSnapshotProvider(f.Receipt, "task", next, basis);
        var snapshot = await provider.CaptureAsync(new(f.Source, "task", f.Receipt), TestContext.Current.CancellationToken);
        using var reader = provider.OpenTree(snapshot, await provider.ResolveReadAccessAsync(snapshot, TestContext.Current.CancellationToken));
        Assert.Equal(snapshot.CapturedAt.UtcDateTime.ToFileTimeUtc(), reader.Stat("data.bin")!.LastWriteTime);
        Assert.Equal(map.Files.Single(e => e.Path == ".gitignore").LastWriteTime, reader.Stat(".gitignore")!.LastWriteTime);
        Assert.Equal(snapshot.CapturedAt.UtcDateTime.ToFileTimeUtc(), reader.Stat("")!.LastWriteTime);
        byte[] actual = new byte[changed.Length]; reader.Read("data.bin", 0, actual); Assert.Equal(changed, actual);
    }

    [Theory]
    [InlineData("digest")]
    [InlineData("library")]
    [InlineData("format")]
    [InlineData("duplicate")]
    public async Task ChangedMetadataAndForeignLibraryArePreservedAndRejected(string failure)
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Requires Windows same-handle source capture.");
        using var f = await Fixture.Create();
        var map = (await WindowsGitSourceMetadata.CaptureAsync(f.Source, f.Basis, TestContext.Current.CancellationToken))!;
        var basis = f.Save(map); string path = Path.Combine(basis.ReceiptDirectory, GitSourceMetadata.FileName);
        if (failure == "digest") File.AppendAllText(path, " ");
        else
        {
            var changed = failure switch
            {
                "library" => map with { Basis = map.Basis with { ManifestHash = new string('c', 64) } },
                "format" => map with { Version = 0 },
                _ => map with { Files = [.. map.Files, map.Files[0]], ExaminedFiles = map.ExaminedFiles + 1 }
            };
            basis = f.Save(changed);
        }
        byte[] before = File.ReadAllBytes(path);
        Assert.Equal("GitSourceMetadataInvalid", Assert.Throws<WorkspaceException>(() => GitSourceMetadata.Read(basis, f.Source)).Code);
        Assert.Equal(before, File.ReadAllBytes(path));
    }

    [Fact]
    public async Task ReceiptMetadataSwapCannotChangeSavedSnapshotIdentity()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Requires Windows same-handle source capture.");
        using var f = await Fixture.Create();
        var map = (await WindowsGitSourceMetadata.CaptureAsync(f.Source, f.Basis, TestContext.Current.CancellationToken))!;
        var basis = f.Save(map); var provider = new GitObjectSnapshotProvider(f.Receipt, "task", f.Revision, basis);
        var snapshot = await provider.CaptureAsync(new(f.Source, "task", f.Receipt), TestContext.Current.CancellationToken);
        var access = await provider.ResolveReadAccessAsync(snapshot, TestContext.Current.CancellationToken);
        string receiptPath = Path.Combine(f.Receipt, "git-basis.json");
        var receipt = JsonNode.Parse(File.ReadAllText(receiptPath))!;
        receipt["sourceMetadata"]!["sourceMetadataHash"] = new string('d', 64);
        File.WriteAllText(receiptPath, receipt.ToJsonString());
        byte[] before = File.ReadAllBytes(receiptPath);
        Assert.Equal("SnapshotIdentityMismatch", Assert.Throws<WorkspaceException>(() => provider.OpenTree(snapshot, access)).Code);
        Assert.Equal("SnapshotIdentityMismatch", (await provider.ObserveAsync(snapshot, TestContext.Current.CancellationToken)).FailureCode);
        Assert.Equal(before, File.ReadAllBytes(receiptPath));
    }

    [Theory]
    [InlineData("alternates")]
    [InlineData("shallow")]
    [InlineData("partial")]
    public async Task CatalogRejectsNonlocalOrIncompleteObjectsBeforeAnyCapture(string kind)
    {
        using var f = await Fixture.Create();
        if (kind == "alternates") File.WriteAllText(Path.Combine(f.Source, ".git", "objects", "info", "alternates"), "not-a-local-store");
        else if (kind == "shallow") File.WriteAllText(Path.Combine(f.Source, ".git", "shallow"), f.Revision + "\n");
        else await TestDirectory.Git(f.Source, "config", "extensions.partialClone", "not-a-remote");
        Assert.Equal("GitSourceMetadataInvalid", (await Assert.ThrowsAsync<WorkspaceException>(() =>
            GitSourceMetadata.CaptureTreeAsync(f.Source, TestContext.Current.CancellationToken))).Code);
        Assert.False(File.Exists(Path.Combine(f.Basis.ReceiptDirectory, GitSourceMetadata.FileName)));
    }

    [Fact]
    public async Task NonRepositoryAndLinkedGitFileDoNotSelectTimestampReuse()
    {
        using var directory = new TestDirectory();
        Assert.Null(await GitSourceMetadata.CaptureTreeAsync(directory.Path, TestContext.Current.CancellationToken));
        File.WriteAllText(Path.Combine(directory.Path, ".git"), "gitdir: unavailable\n");
        Assert.Null(await GitSourceMetadata.CaptureTreeAsync(directory.Path, TestContext.Current.CancellationToken));
        Assert.Equal("gitdir: unavailable\n", File.ReadAllText(Path.Combine(directory.Path, ".git")));
    }

    [Theory]
    [InlineData("valid")]
    [InlineData("corrupt-map")]
    [InlineData("missing-proof")]
    [InlineData("foreign-map")]
    [InlineData("timestamp-corruption")]
    public async Task InterruptedLibraryPreparationReusesOnlyItsFrozenPairedMapBeforeState(string scenario)
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Requires Windows Library preparation guards.");
        using var f = await Fixture.Create();
        string library = Path.Combine(f.Source, "Library"); Directory.CreateDirectory(library); File.WriteAllBytes(Path.Combine(library, "cache"), [1, 2, 3]);
        var store = new WorkspaceStore(Path.Combine(Path.GetDirectoryName(f.Source)!, "home"));
        var policy = new StopBeforeMountPolicy(); var manager = new ManagedDirectoryBasis(store, new(policy));
        const string key = "metadata-retry";
        string id = WorkspaceStore.Key(WorkspaceStore.ProjectKey(f.Source) + "\nLibrary\n" + key);
        string root = Path.Combine(store.Root, "library-bases", id); Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "prepare-intent.json"), JsonSerializer.Serialize(new
        {
            version = 2, id, projectPath = f.Source, key, originalPath = library, basisPath = Path.Combine(root, "base"),
            applicationKind = "unity", directoryKind = "Library", compatibilityKey = "fixed"
        }, Json));
        string readerLock = Path.Combine(root, "readers.lock"); File.WriteAllBytes(readerLock, []);
        using (var blocker = new FileStream(readerLock, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            // Readers are opened after map publication and before state. This
            // fails without ever moving Library or launching a provider.
            await Assert.ThrowsAsync<IOException>(() => manager.PrepareAsync(f.Source, key, "unity", TestContext.Current.CancellationToken));
            string mapPath = Path.Combine(root, GitSourceMetadata.FileName);
            Assert.True(File.Exists(mapPath)); Assert.False(File.Exists(Path.Combine(root, "state.json")));
            File.WriteAllBytes(Path.Combine(f.Source, "data.bin"), [9, 8, 7]);
            if (scenario == "corrupt-map") File.WriteAllText(mapPath, "invalid");
            else if (scenario == "missing-proof") File.Delete(Path.Combine(root, "proof.json"));
            else if (scenario == "foreign-map")
            {
                var json = JsonNode.Parse(File.ReadAllText(mapPath))!; json["basis"]!["ownerId"] = "foreign";
                File.WriteAllText(mapPath, json.ToJsonString());
            }
            else if (scenario == "timestamp-corruption")
            {
                var json = JsonNode.Parse(File.ReadAllText(mapPath))!;
                var entry = json["files"]!.AsArray().First(e => e!["path"]!.GetValue<string>() == "data.bin")!;
                entry["lastWriteTime"] = entry["lastWriteTime"]!.GetValue<long>() + 1;
                File.WriteAllText(mapPath, json.ToJsonString());
            }
            var before = Directory.GetFiles(root).Where(p => p != readerLock).ToDictionary(p => p, File.ReadAllBytes);
            if (scenario == "valid")
                await Assert.ThrowsAsync<IOException>(() => manager.PrepareAsync(f.Source, key, "unity", TestContext.Current.CancellationToken));
            else
                await Assert.ThrowsAsync<WorkspaceException>(() => manager.PrepareAsync(f.Source, key, "unity", TestContext.Current.CancellationToken));
            foreach (var file in before) Assert.Equal(file.Value, File.ReadAllBytes(file.Key));
            Assert.True(Directory.Exists(library)); Assert.False(Directory.Exists(Path.Combine(root, "base")));
        }
        if (scenario != "valid") return;
        // Permit the next initial reservation, then stop at the existing
        // original-editor guard immediately after durable state publication.
        policy.FailAt = policy.Acquisitions + 2;
        Assert.Equal("OriginalEditorStopRequired", (await Assert.ThrowsAsync<WorkspaceException>(() =>
            manager.PrepareAsync(f.Source, key, "unity", TestContext.Current.CancellationToken))).Code);
        var state = await manager.GetAsync(id, TestContext.Current.CancellationToken);
        Assert.Equal("preparing", state.Phase); Assert.NotNull(state.Basis.SourceMetadataHash);
        var map = GitSourceMetadata.Read(state.Basis, f.Source);
        Assert.Equal(65536, map.Files.Single(e => e.Path == "data.bin").Length);
        Assert.True(Directory.Exists(library)); Assert.False(Directory.Exists(state.Basis.Snapshot.RootPath));
    }

    private sealed class StopBeforeMountPolicy : IApplicationProjectPolicy
    {
        public string ApplicationKind => "unity";
        public GeneratedDirectoryDescriptor GeneratedDirectory { get; } = new("Library", "Library");
        public int Acquisitions, FailAt = int.MaxValue;
        public string CompatibilityKey(string project) => "fixed";
        public IDisposable? AcquireOriginal(string project, bool reservePublication = false, string? verifiedManagedRoot = null)
        { if (++Acquisitions == FailAt) throw new IOException("Stop before mount in metadata recovery test."); return null; }
        public ApplicationSelection CloseSelection(IEnumerable<string> paths, Func<string, bool> existedBefore, Func<string, bool> existsAfter) => new(paths.ToArray(), []);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly TestDirectory directory = new();
        public string Source => Path.Combine(directory.Path, "source");
        public string Receipt => Path.Combine(directory.Path, "receipt");
        public string Revision { get; private set; } = "";
        public byte[] Data { get; } = new byte[65536];
        public GeneratedDirectoryReference Basis { get; }
        private Fixture()
        {
            string root = Path.Combine(directory.Path, "library-basis"), basis = Path.Combine(root, "base");
            Directory.CreateDirectory(root);
            const string id = "aaaaaaaaaaaaaaaaaaaaaaaa";
            Basis = new(id, Source, "Library", new(id, "owned-directory", basis, basis,
                DateTimeOffset.UnixEpoch, true, true, "stopped-original-owned-directory"), root, id, new string('b', 64));
            new Random(31).NextBytes(Data);
        }
        public static async Task<Fixture> Create()
        {
            var f = new Fixture(); Directory.CreateDirectory(f.Source);
            await TestDirectory.Git(f.Source, "init", "-b", "development");
            await TestDirectory.Git(f.Source, "config", "core.autocrlf", "false");
            await TestDirectory.Git(f.Source, "config", "user.name", "Fixture");
            await TestDirectory.Git(f.Source, "config", "user.email", "fixture@example.invalid");
            File.WriteAllBytes(Path.Combine(f.Source, "data.bin"), f.Data);
            File.WriteAllText(Path.Combine(f.Source, "line-endings.txt"), "alpha\nbeta\n");
            File.WriteAllText(Path.Combine(f.Source, "changed.txt"), "abcdefgh");
            File.WriteAllText(Path.Combine(f.Source, ".gitignore"), "Library/\n");
            await TestDirectory.Git(f.Source, "add", "."); await TestDirectory.Git(f.Source, "commit", "-m", "basis");
            f.Revision = await TestDirectory.Git(f.Source, "rev-parse", "HEAD");
            File.WriteAllText(Path.Combine(f.Source, "line-endings.txt"), "alpha\r\nbeta\r\n");
            File.WriteAllText(Path.Combine(f.Source, "changed.txt"), "abcdEFGH");
            foreach (string file in Directory.GetFiles(f.Source))
                File.SetLastWriteTimeUtc(file, new DateTime(2020, 2, 3, 4, 5, 6, DateTimeKind.Utc));
            return f;
        }
        public GeneratedDirectoryReference Save(GitSourceMetadataManifest map)
        {
            string path = Path.Combine(Basis.ReceiptDirectory, GitSourceMetadata.FileName);
            byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(map, Json); File.WriteAllBytes(path, bytes);
            return Basis with { SourceMetadataHash = Convert.ToHexStringLower(SHA256.HashData(bytes)) };
        }
        public void Dispose() => directory.Dispose();
    }
}
