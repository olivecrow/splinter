using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Splinter.Core;
using Splinter.SourceControl.Git;

namespace Splinter.Tests;

public sealed class SourceFingerprintTests
{
    [Theory]
    [InlineData("false")]
    [InlineData("true")]
    public async Task BatchedHashesPreservePathSpecificConversionAcrossRestartAndAudit(string autoCrlf)
    {
        using var f = await Fixture.Create(); var ct = TestContext.Current.CancellationToken;
        await TestDirectory.Git(f.Workspace.Path, "config", "core.autocrlf", autoCrlf);
        File.WriteAllText(f.Path(".gitattributes"), "*.txt text eol=lf\n*.bin -text\n"); f.Tracker.Mark(".gitattributes");
        Directory.CreateDirectory(f.Path("자료 폴더"));
        for (int i = 0; i < 520; i++)
        {
            string name = $"자료 폴더/file {i:D3}.txt";
            File.WriteAllText(f.Path(name), $"line {i}\r\nnext\r\n"); f.Tracker.Mark(name);
        }
        string[] special = OperatingSystem.IsWindows() ? ["-leading.txt", "binary.bin"] :
            ["-leading.txt", "binary.bin", "\"quoted.txt", "line\nbreak.txt", "tab\tname.txt"];
        foreach (string name in special)
        { File.WriteAllBytes(f.Path(name), [0, 13, 10, 255]); f.Tracker.Mark(name); }
        var first = await f.Provider().InspectAsync(f.Workspace, ct);
        Assert.Equal(3, first.Inspection!.GitHashProcesses);
        Assert.Equal(await f.Oracle(), first.Fingerprint);
        await CheckGitHashes();
        var unchanged = await f.Provider().InspectAsync(f.Workspace, ct);
        Assert.Equal(0, unchanged.Inspection!.GitHashProcesses);
        f.Tracker.Epoch = "restarted";
        var rebuilt = await f.Provider().InspectAsync(f.Workspace, ct);
        Assert.Equal(3, rebuilt.Inspection!.GitHashProcesses); Assert.Equal(first.Fingerprint, rebuilt.Fingerprint);
        var audited = await f.Provider(true).InspectAsync(f.Workspace, ct);
        Assert.Equal(3, audited.Inspection!.GitHashProcesses); Assert.Equal(first.Fingerprint, audited.Fingerprint);
        await CheckGitHashes();

        async Task CheckGitHashes()
        {
            using var manifest = JsonDocument.Parse(File.ReadAllText(f.Tracker.CachePath(f.Workspace)));
            var hashes = manifest.RootElement.GetProperty("value").GetProperty("files").EnumerateArray()
                .ToDictionary(e => e.GetProperty("path").GetString()!, e => e.GetProperty("gitHash").GetString()!);
            foreach (string name in special.Concat(new[] { "자료 폴더/file 000.txt", "자료 폴더/file 255.txt", "자료 폴더/file 256.txt", "자료 폴더/file 519.txt" }))
                Assert.Equal(await TestDirectory.Git(f.Workspace.Path, "hash-object", "--path=" + name, "--", name), hashes[name]);
        }
    }

    [Fact]
    public async Task NewCliReusesLargeFilesAndDetectsSameSizeSameTimeWrites()
    {
        using var f = await Fixture.Create();
        var first = await f.Provider().InspectAsync(f.Workspace, TestContext.Current.CancellationToken);
        Assert.Equal("initial", first.Inspection!.Mode);
        Assert.True(first.Inspection.PayloadBytesHashed >= 16 * 1024 * 1024);
        Assert.Equal(await f.Oracle(), first.Fingerprint);
        string code = f.Path("code.cs"); var time = File.GetLastWriteTimeUtc(code);
        File.WriteAllText(code, "return 2;\n"); File.SetLastWriteTimeUtc(code, time); f.Tracker.Mark("code.cs");
        for (int i = 0; i < 3; i++)
        {
            using (var held = new FileStream(f.Path("large.bin"), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                var current = await f.Provider().InspectAsync(f.Workspace, TestContext.Current.CancellationToken);
                Assert.Equal("incremental", current.Inspection!.Mode);
                Assert.Equal(i == 0 ? 10 : 0, current.Inspection.PayloadBytesHashed);
                Assert.Equal(i == 0 ? 10 : 0, current.Inspection.GitHashInputBytes);
                Assert.True(current.HasChanges);
                Assert.Contains("code.cs", current.ChangedPaths);
                Assert.NotEqual(first.Fingerprint, current.Fingerprint);
            }
        }
        Assert.Equal(await f.Oracle(), (await f.Provider().InspectAsync(f.Workspace, TestContext.Current.CancellationToken)).Fingerprint);
        var validated = await f.Provider().InspectAsync(f.Workspace, TestContext.Current.CancellationToken);
        f.Workspace.Validation = new("same-mtime", validated.Fingerprint, true, DateTimeOffset.UtcNow, []);
        await f.Provider().CheckpointAsync(f.Workspace, "preserve same-size same-mtime content", TestContext.Current.CancellationToken);
        Assert.Equal("return 2;", await TestDirectory.Git(f.Workspace.Path, "show", "HEAD:code.cs"));
        Assert.False((await f.Provider().InspectAsync(f.Workspace, TestContext.Current.CancellationToken)).HasChanges);
    }

    [Fact]
    public async Task GitInterpretationChangesRehashCanonicalContentAndUnsupportedFiltersStillFailClosed()
    {
        using var f = await Fixture.Create();
        File.WriteAllText(f.Path(".gitattributes"), "code.cs text eol=lf\n"); f.Tracker.Mark(".gitattributes");
        File.WriteAllText(f.Path("code.cs"), "return 1;\r\n"); f.Tracker.Mark("code.cs");
        var text = await f.Provider().InspectAsync(f.Workspace, TestContext.Current.CancellationToken);
        string originalBlob = await TestDirectory.Git(f.Workspace.Path, "rev-parse", "HEAD:code.cs");
        Assert.Equal(originalBlob, CachedGitHash());
        File.WriteAllText(f.Path(".gitattributes"), "code.cs -text\n"); f.Tracker.Mark(".gitattributes");
        var binary = await f.Provider().InspectAsync(f.Workspace, TestContext.Current.CancellationToken);
        Assert.Contains("code.cs", binary.ChangedPaths);
        Assert.NotEqual(originalBlob, CachedGitHash());
        Assert.True(binary.Inspection!.GitHashInputBytes >= 16 * 1024 * 1024);
        Assert.True(binary.Inspection.PayloadBytesHashed < 1024);
        byte[] prior = File.ReadAllBytes(f.Tracker.CachePath(f.Workspace));
        File.WriteAllText(f.Path(".gitattributes"), "*.cs filter=lfs\n"); f.Tracker.Mark(".gitattributes");
        Assert.Equal("SnapshotFilterUnsupported", (await Assert.ThrowsAsync<WorkspaceException>(() => f.Provider().InspectAsync(f.Workspace, TestContext.Current.CancellationToken))).Code);
        Assert.Equal(prior, File.ReadAllBytes(f.Tracker.CachePath(f.Workspace)));
        string CachedGitHash()
        {
            using var manifest = JsonDocument.Parse(File.ReadAllText(f.Tracker.CachePath(f.Workspace)));
            return manifest.RootElement.GetProperty("value").GetProperty("files").EnumerateArray()
                .Single(file => file.GetProperty("path").GetString() == "code.cs").GetProperty("gitHash").GetString()!;
        }
    }

    [Fact]
    public async Task MembershipMetaTruncationDirectoryRenameAndAtomicReplacementMatchFullOracle()
    {
        using var f = await Fixture.Create();
        await f.Provider().InspectAsync(f.Workspace, TestContext.Current.CancellationToken);
        Directory.CreateDirectory(f.Path("dir")); File.WriteAllText(f.Path("dir/a"), "one"); f.Tracker.Mark("dir/a");
        await EqualOracle();
        Directory.Move(f.Path("dir"), f.Path("moved")); f.Tracker.Mark("dir"); f.Tracker.Mark("moved");
        await EqualOracle();
        File.WriteAllText(f.Path("code.cs.meta"), "guid: changed"); f.Tracker.Mark("code.cs.meta");
        await EqualOracle();
        File.WriteAllText(f.Path("temp"), "new code"); File.Move(f.Path("temp"), f.Path("code.cs"), true);
        f.Tracker.Mark("temp"); f.Tracker.Mark("code.cs"); await EqualOracle();
        using (var file = File.OpenWrite(f.Path("large.bin"))) file.SetLength(4096);
        f.Tracker.Mark("large.bin"); await EqualOracle();
        File.Delete(f.Path("moved/a")); f.Tracker.Mark("moved/a"); await EqualOracle();
        File.WriteAllText(f.Path("hidden.txt"), "hidden"); File.WriteAllText(f.Path(".gitignore"), "Library/\nhidden.txt\n");
        f.Tracker.Mark("hidden.txt"); f.Tracker.Mark(".gitignore"); await EqualOracle();
        File.WriteAllText(f.Path(".gitignore"), "Library/\n"); f.Tracker.Mark(".gitignore"); await EqualOracle();

        async Task EqualOracle() => Assert.Equal(await f.Oracle(), (await f.Provider().InspectAsync(f.Workspace, TestContext.Current.CancellationToken)).Fingerprint);
    }

    [Fact]
    public async Task CorruptCacheRequiresAuditAndPreservesRejectedBytes()
    {
        using var f = await Fixture.Create(); await f.Provider().InspectAsync(f.Workspace, TestContext.Current.CancellationToken);
        string cache = f.Tracker.CachePath(f.Workspace);
        File.WriteAllText(cache, "{broken");
        Assert.Equal("SourceCacheRevalidationRequired", (await Assert.ThrowsAsync<WorkspaceException>(() => f.Provider().InspectAsync(f.Workspace, TestContext.Current.CancellationToken))).Code);
        Assert.Equal("{broken", File.ReadAllText(cache));
        var audited = await f.Provider(true).InspectAsync(f.Workspace, TestContext.Current.CancellationToken);
        Assert.Equal("audit", audited.Inspection!.Mode); Assert.Equal(await f.Oracle(), audited.Fingerprint);
        Assert.Equal("{broken", File.ReadAllText(Directory.GetFiles(f.Directory.Path, "source.json.rejected-*").Single()));
        Assert.Equal(0, (await f.Provider().InspectAsync(f.Workspace, TestContext.Current.CancellationToken)).Inspection!.PayloadBytesHashed);
    }

    [Fact]
    public async Task ProviderEpochAndSourcePolicyChangesRebuildInsteadOfTrustingOldCache()
    {
        using var f = await Fixture.Create(); await f.Provider().InspectAsync(f.Workspace, TestContext.Current.CancellationToken);
        f.Tracker.Epoch = "restart";
        var rebuilt = await f.Provider().InspectAsync(f.Workspace, TestContext.Current.CancellationToken);
        Assert.Equal("rebuild", rebuilt.Inspection!.Mode); Assert.Equal(0, rebuilt.Inspection.FilesReused);
        f.Workspace.ManagedPaths = ["Library", "large.bin"];
        var changed = await f.Provider().InspectAsync(f.Workspace, TestContext.Current.CancellationToken);
        Assert.Equal("rebuild", changed.Inspection!.Mode); Assert.Equal(await f.Oracle(), changed.Fingerprint);
    }

    [Fact]
    public async Task ConcurrentWritesAndUnknownObservationCannotPublishSuccessOrReplaceManifest()
    {
        using var f = await Fixture.Create(); await f.Provider().InspectAsync(f.Workspace, TestContext.Current.CancellationToken);
        byte[] previous = File.ReadAllBytes(f.Tracker.CachePath(f.Workspace));
        f.Tracker.OnObserve = count =>
        {
            if (count != 4) return;
            File.WriteAllText(f.Path("code.cs"), "return 7;\n"); f.Tracker.Mark("code.cs");
        };
        Assert.Equal("SourceChanged", (await Assert.ThrowsAsync<WorkspaceException>(() => f.Provider().InspectAsync(f.Workspace, TestContext.Current.CancellationToken))).Code);
        Assert.Equal(previous, File.ReadAllBytes(f.Tracker.CachePath(f.Workspace)));
        f.Tracker.OnObserve = _ => throw new WorkspaceException("SourceObservationUncertain", "probe unavailable");
        Assert.Equal("SourceObservationUncertain", (await Assert.ThrowsAsync<WorkspaceException>(() => f.Provider().InspectAsync(f.Workspace, TestContext.Current.CancellationToken))).Code);
        Assert.Equal(previous, File.ReadAllBytes(f.Tracker.CachePath(f.Workspace)));
    }

    [Fact]
    public async Task ManagedLibraryWritesDoNotInvalidateSourceAndMetaChangesArePreserved()
    {
        using var f = await Fixture.Create(); var first = await f.Provider().InspectAsync(f.Workspace, TestContext.Current.CancellationToken);
        f.Tracker.OnObserve = _ => f.Tracker.Mark("Library/cache");
        Assert.Equal(0, (await f.Provider().InspectAsync(f.Workspace, TestContext.Current.CancellationToken)).Inspection!.PayloadBytesHashed);
        f.Workspace.Validation = new("old", first.Fingerprint, true, DateTimeOffset.UtcNow, []);
        File.WriteAllText(f.Path("code.cs.meta"), "guid: updated"); f.Tracker.Mark("code.cs.meta");
        Assert.NotEqual(first.Fingerprint, (await f.Provider().InspectAsync(f.Workspace, TestContext.Current.CancellationToken)).Fingerprint);
    }

    [Fact]
    public void SourceChangeAndMetricsContractsRejectMalformedValues()
    {
        var contracts = new Protocol.ContractCatalog();
        var good = new SourceChangeView("workspace", "snapshot", "epoch", 1, [new("code.cs", 1)]);
        Assert.True(contracts.Validate("urn:splinter:v1:source-change-view", JsonSerializer.SerializeToElement(good, Protocol.ApiJson.Options)).IsValid);
        Assert.False(contracts.Validate("urn:splinter:v1:source-change-view", JsonSerializer.SerializeToElement(good with { Sequence = -1 }, Protocol.ApiJson.Options)).IsValid);
        Assert.True(contracts.Validate("urn:splinter:v1:workspace-status-request", JsonSerializer.SerializeToElement(new { id = new string('a',24), auditSource = true })).IsValid);
        var response = new Platform.Windows.SourceChangeResponse("token", "request", 123, 456, good, null);
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        Assert.True(contracts.Validate("urn:splinter:v1:source-change-response", JsonSerializer.SerializeToElement(response, json)).IsValid);
        Assert.False(contracts.Validate("urn:splinter:v1:source-change-response", JsonSerializer.SerializeToElement(response with { Error = "failed" }, json)).IsValid);
    }

    private sealed class Tracker(string cache) : ISourceChangeProvider
    {
        public string Epoch = "first-provider";
        public Action<int>? OnObserve;
        private int observations;
        private long sequence;
        private readonly Dictionary<string, long> changes = new(StringComparer.OrdinalIgnoreCase);
        public void Mark(string path) => changes[path] = ++sequence;
        public string CachePath(WorkspaceRecord workspace) => cache;
        public Task<SourceChangeView> ObserveChangesAsync(WorkspaceRecord workspace, CancellationToken ct = default)
        {
            observations++; OnObserve?.Invoke(observations);
            var included = changes.Where(p => !SourcePathPolicy.Managed(p.Key, workspace.ManagedPaths))
                .OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => new SourcePathChange(p.Key, p.Value)).ToArray();
            return Task.FromResult(new SourceChangeView(workspace.Id, workspace.Snapshot!.Reference!.Id, Epoch,
                included.Length == 0 ? 0 : included.Max(c => c.Sequence), included));
        }
    }

    private sealed class Fixture : IDisposable
    {
        public TestDirectory Directory = new();
        public WorkspaceRecord Workspace = null!;
        public Tracker Tracker = null!;
        public string Path(string file) => System.IO.Path.Combine(Workspace.Path, file);
        public GitWorkspaceProvider Provider(bool audit = false) => new(sourceChanges: Tracker, auditSource: audit);
        public static async Task<Fixture> Create()
        {
            var f = new Fixture(); string repo = System.IO.Path.Combine(f.Directory.Path, "repo");
            System.IO.Directory.CreateDirectory(repo);
            await TestDirectory.Git(repo, "init", "-b", "development");
            await TestDirectory.Git(repo, "config", "user.name", "Fixture"); await TestDirectory.Git(repo, "config", "user.email", "fixture@example.invalid");
            await TestDirectory.Git(repo, "config", "core.autocrlf", "false");
            File.WriteAllText(System.IO.Path.Combine(repo, "code.cs"), "return 1;\n");
            File.WriteAllText(System.IO.Path.Combine(repo, "code.cs.meta"), "guid: initial");
            File.WriteAllText(System.IO.Path.Combine(repo, ".gitignore"), "Library/\n");
            File.WriteAllBytes(System.IO.Path.Combine(repo, "large.bin"), new byte[16 * 1024 * 1024]);
            await TestDirectory.Git(repo, "add", "."); await TestDirectory.Git(repo, "commit", "-m", "fixture");
            f.Workspace = new() { Id = "fingerprint-fixture", RequestKey = "test", ProjectPath = repo,
                Path = System.IO.Path.Combine(f.Directory.Path, "worker"), SourceReference = "splinter/test", BaseReference = "development", ManagedPaths = ["Library"] };
            f.Workspace.BaseRevision = (await new GitWorkspaceProvider().CreateAsync(f.Workspace, TestContext.Current.CancellationToken)).BaseRevision;
            f.Workspace.StorageMode = "ntfs-snapshot";
            f.Workspace.Snapshot = new() { Reference = new("fake-snapshot", "fake", repo, repo, DateTimeOffset.UtcNow, true, true, "fixture-only") };
            f.Tracker = new(System.IO.Path.Combine(f.Directory.Path, "source.json"));
            return f;
        }
        public async Task<string> Oracle()
        {
            string[] files = (await TestDirectory.Git(Workspace.Path, "ls-files", "--cached", "--others", "--exclude-standard", "-z"))
                .Split('\0', StringSplitOptions.RemoveEmptyEntries);
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            foreach (string file in files.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
            {
                if (SourcePathPolicy.Managed(file, Workspace.ManagedPaths) || !File.Exists(Path(file))) continue;
                byte[] name = Encoding.UTF8.GetBytes(file);
                hash.AppendData(BitConverter.GetBytes(name.Length)); hash.AppendData(name); hash.AppendData([1]);
                hash.AppendData(SHA256.HashData(File.ReadAllBytes(Path(file))));
            }
            return Convert.ToHexStringLower(hash.GetHashAndReset());
        }
        public void Dispose() => Directory.Dispose();
    }
}
