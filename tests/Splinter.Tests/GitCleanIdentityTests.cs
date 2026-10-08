using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Splinter.Core;
using Splinter.SourceControl.Git;

namespace Splinter.Tests;

public sealed class GitCleanIdentityTests
{
    [Theory]
    [InlineData("sha1", "false")]
    [InlineData("sha1", "input")]
    [InlineData("sha1", "true")]
    [InlineData("sha256", "input")]
    public async Task ProvenHashesMatchGitAcrossAttributeAndRandomByteCases(string format, string autoCrlf)
    {
        using var directory = new TestDirectory();
        await Initialize(directory.Path, format, autoCrlf);
        string[] rules = ["-text", "text=auto", "text eol=lf", "text=unset", "crlf=input", "ident", "-ident"];
        var random = new Random(91743);
        byte[][] contents = [[], "one\ntwo\n"u8.ToArray(), "one\r\ntwo\r\n"u8.ToArray(),
            [0, 13, 10, 65], "lone\rcarriage"u8.ToArray(), new byte[511], new byte[513], new byte[4097]];
        for (int i = 5; i < contents.Length; i++) random.NextBytes(contents[i]);
        var names = new List<string>();
        var attributes = new StringBuilder();
        for (int r = 0; r < rules.Length; r++)
            for (int c = 0; c < contents.Length; c++)
            {
                string name = $"case-{r}-{c}.data";
                names.Add(name); attributes.Append(name).Append(' ').Append(rules[r]).Append('\n');
                File.WriteAllBytes(Path.Combine(directory.Path, name), contents[c]);
            }
        File.WriteAllText(Path.Combine(directory.Path, ".gitattributes"), attributes.ToString());
        var policy = new GitCleanIdentity(await TestDirectory.Git(directory.Path, "config", "--null", "--list"));
        await GitWorkspaceProvider.AttributeOutputAsync(directory.Path, names, false, true, policy.AddAttributes,
            TestContext.Current.CancellationToken);
        string[] oracle = (await TestDirectory.Git(directory.Path, ["hash-object", "--", .. names])).Split('\n');
        int proved = 0, fallback = 0;
        for (int i = 0; i < names.Count; i++)
        {
            var requirement = policy.Requirement(names[i]);
            if (requirement == GitCleanRequirement.None) { fallback++; continue; }
            byte[] bytes = File.ReadAllBytes(Path.Combine(directory.Path, names[i]));
            using var stream = new CountingStream(bytes);
            var result = await GitCleanIdentity.HashAsync(stream, requirement,
                format == "sha1" ? HashAlgorithmName.SHA1 : HashAlgorithmName.SHA256, TestContext.Current.CancellationToken);
            Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(bytes)), result.RawHash);
            Assert.Equal(bytes.Length, stream.BytesRead);
            if (result.GitHash is null) fallback++;
            else { Assert.Equal(oracle[i].TrimEnd('\r'), result.GitHash); proved++; }
        }
        Assert.True(proved > 0); Assert.True(fallback > 0);
    }

    [Theory]
    [InlineData("core.safecrlf\ntrue\0", "")]
    [InlineData("core.safecrlf\nwarn\0", "")]
    [InlineData("core.safecrlf\ninvalid\0", "")]
    [InlineData("core.autocrlf\ninvalid\0", "")]
    [InlineData("", "file\0filter\0unset\0")]
    [InlineData("", "file\0working-tree-encoding\0unset\0")]
    [InlineData("", "file\0ident\0unset\0")]
    [InlineData("", "file\0text\0")]
    public void AmbiguousOrUnsupportedConversionRetainsGit(string config, string attributes)
    {
        var policy = new GitCleanIdentity(config); policy.AddAttributes(attributes);
        Assert.Equal(GitCleanRequirement.None, policy.Requirement("file"));
    }

    [Fact]
    public async Task AutomaticBinaryProofReadsPastPrefixAndBlockBoundaryButForcedTextDoesNotUseIt()
    {
        byte[] bytes = Enumerable.Repeat((byte)'a', 1024 * 1024 + 19).ToArray();
        bytes[5] = 13; bytes[6] = 10; bytes[^1] = 0;
        using var stream = new CountingStream(bytes);
        var auto = await GitCleanIdentity.HashAsync(stream, GitCleanRequirement.NoCarriageReturnOrNul,
            HashAlgorithmName.SHA1, TestContext.Current.CancellationToken);
        Assert.Equal(bytes.Length, stream.BytesRead); Assert.Equal(1024 * 1024, stream.MaximumRequest);
        Assert.NotNull(auto.GitHash);
        using var forced = new CountingStream(bytes);
        Assert.Null((await GitCleanIdentity.HashAsync(forced, GitCleanRequirement.NoCarriageReturn,
            HashAlgorithmName.SHA1, TestContext.Current.CancellationToken)).GitHash);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1)]
    public async Task TruncatedOrGrowingStreamCannotProduceAProof(int lengthDifference)
    {
        using var stream = new CountingStream([1, 2, 3], lengthDifference);
        var error = await Assert.ThrowsAsync<WorkspaceException>(() => GitCleanIdentity.HashAsync(stream,
            GitCleanRequirement.Always, HashAlgorithmName.SHA1, TestContext.Current.CancellationToken));
        Assert.Equal("SourceChanged", error.Code);
    }

    [Fact]
    public async Task CancellationCannotProduceAProof()
    {
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        using var stream = new CountingStream([1, 2, 3]);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => GitCleanIdentity.HashAsync(stream,
            GitCleanRequirement.Always, HashAlgorithmName.SHA1, cancellation.Token));
    }

    [Theory]
    [InlineData("sha1")]
    [InlineData("sha256")]
    public async Task InspectionAvoidsSecondPayloadReadAndPreservesGitFallbackAndSameTimeDirtyDetection(string format)
    {
        using var f = await Fixture.Create(format); var ct = TestContext.Current.CancellationToken;
        var initial = await f.Provider().InspectAsync(f.Workspace, ct);
        Assert.False(initial.HasChanges);
        long totalBytes = Directory.GetFiles(f.Workspace.Path).Where(path => Path.GetFileName(path) != ".git").Sum(path => new FileInfo(path).Length);
        Assert.Equal(totalBytes, initial.Inspection!.PayloadBytesHashed);
        Assert.Equal(new FileInfo(f.Path("keyword.txt")).Length, initial.Inspection.GitHashInputBytes);
        Assert.Equal(1, initial.Inspection.GitHashProcesses);
        await f.AssertGitHashes();
        var time = File.GetLastWriteTimeUtc(f.Path("code.cs"));
        File.WriteAllText(f.Path("code.cs"), "return 2;\n"); File.SetLastWriteTimeUtc(f.Path("code.cs"), time); f.Tracker.Mark("code.cs");
        var changed = await f.Provider().InspectAsync(f.Workspace, ct);
        Assert.Contains("code.cs", changed.ChangedPaths); Assert.NotEqual(initial.Fingerprint, changed.Fingerprint);
        Assert.Equal(10, changed.Inspection!.PayloadBytesHashed); Assert.Equal(0, changed.Inspection.GitHashInputBytes);
        Assert.Equal(0, changed.Inspection.GitHashProcesses);
        File.WriteAllText(f.Path("code.cs"), "return 1;\r\n"); f.Tracker.Mark("code.cs");
        var normalized = await f.Provider().InspectAsync(f.Workspace, ct);
        Assert.False(normalized.HasChanges); Assert.NotEqual(initial.Fingerprint, normalized.Fingerprint);
        Assert.Equal(11, normalized.Inspection!.GitHashInputBytes); Assert.Equal(1, normalized.Inspection.GitHashProcesses);
        await f.AssertGitHashes();
        var audited = await f.Provider(audit: true).InspectAsync(f.Workspace, ct);
        Assert.Equal("audit", audited.Inspection!.Mode); Assert.Equal(0, audited.Inspection.FilesReused);
        Assert.Equal(normalized.Fingerprint, audited.Fingerprint);
    }

    [Fact]
    public async Task ChangedInterpretationWithOnlyCachedRawHashStillUsesGitAndEpochForcesFullContent()
    {
        using var f = await Fixture.Create("sha1"); var ct = TestContext.Current.CancellationToken;
        await f.Provider().InspectAsync(f.Workspace, ct);
        await TestDirectory.Git(f.Workspace.Path, "config", "core.autocrlf", "false");
        var configChanged = await f.Provider().InspectAsync(f.Workspace, ct);
        Assert.Equal(0, configChanged.Inspection!.PayloadBytesHashed);
        Assert.True(configChanged.Inspection.GitHashInputBytes > 1024 * 1024);
        await f.AssertGitHashes();
        File.AppendAllText(f.Path(".gitattributes"), "code.cs -text\n"); f.Tracker.Mark(".gitattributes");
        var attributesChanged = await f.Provider().InspectAsync(f.Workspace, ct);
        Assert.True(attributesChanged.Inspection!.GitHashInputBytes > 1024 * 1024);
        Assert.True(attributesChanged.Inspection.PayloadBytesHashed < 1024);
        await f.AssertGitHashes();
        f.Tracker.Epoch = "restart";
        var rebuilt = await f.Provider().InspectAsync(f.Workspace, ct);
        Assert.Equal("rebuild", rebuilt.Inspection!.Mode); Assert.Equal(0, rebuilt.Inspection.FilesReused);
        Assert.Equal(attributesChanged.Fingerprint, rebuilt.Fingerprint);
    }

    [Fact]
    public async Task GenerationMutationCannotPublishSinglePassResult()
    {
        using var f = await Fixture.Create("sha1"); var ct = TestContext.Current.CancellationToken;
        await f.Provider().InspectAsync(f.Workspace, ct);
        byte[] prior = File.ReadAllBytes(f.Tracker.CachePath(f.Workspace));
        string measurements = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(f.Tracker.CachePath(f.Workspace))!, "source-inspections.jsonl");
        byte[] priorMeasurements = File.ReadAllBytes(measurements);
        f.Tracker.Epoch = "force-full-read";
        int observations = 0;
        f.Tracker.OnObserve = _ =>
        {
            if (++observations != 2) return;
            File.WriteAllText(f.Path("code.cs"), "return 2;\n");
            f.Tracker.Mark("code.cs");
        };
        Assert.Equal("SourceChanged", (await Assert.ThrowsAsync<WorkspaceException>(() => f.Provider().InspectAsync(f.Workspace, ct))).Code);
        Assert.Equal(2, observations);
        Assert.Equal("return 2;\n", File.ReadAllText(f.Path("code.cs")));
        Assert.Equal(prior, File.ReadAllBytes(f.Tracker.CachePath(f.Workspace)));
        Assert.Equal(priorMeasurements, File.ReadAllBytes(measurements));
    }

    private static async Task Initialize(string path, string format, string autoCrlf)
    {
        await TestDirectory.Git(path, "init", "-b", "development", "--object-format=" + format);
        await TestDirectory.Git(path, "config", "core.autocrlf", autoCrlf);
        await TestDirectory.Git(path, "config", "core.safecrlf", "false");
    }

    private sealed class CountingStream(byte[] bytes, int lengthDifference = 0) : MemoryStream(bytes, writable: false)
    {
        public int BytesRead, MaximumRequest;
        public override long Length => base.Length + lengthDifference;
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            MaximumRequest = Math.Max(MaximumRequest, buffer.Length);
            int count = await base.ReadAsync(buffer, cancellationToken); BytesRead += count; return count;
        }
    }

    private sealed class Tracker(string cache) : ISourceChangeProvider
    {
        public string Epoch = "initial";
        public Action<int>? OnObserve;
        private int observations;
        private long sequence;
        private readonly Dictionary<string, long> changes = new(StringComparer.Ordinal);
        public void Mark(string path) => changes[path] = ++sequence;
        public string CachePath(WorkspaceRecord workspace) => cache;
        public Task<SourceChangeView> ObserveChangesAsync(WorkspaceRecord workspace, CancellationToken ct = default)
        {
            observations++;
            OnObserve?.Invoke(observations);
            return Task.FromResult(new SourceChangeView(workspace.Id, workspace.Snapshot!.Reference!.Id, Epoch, sequence,
                changes.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => new SourcePathChange(pair.Key, pair.Value)).ToArray()));
        }
    }

    private sealed class Fixture : IDisposable
    {
        private readonly TestDirectory directory = new();
        public WorkspaceRecord Workspace = null!;
        public Tracker Tracker = null!;
        public string Path(string name) => System.IO.Path.Combine(Workspace.Path, name);
        public GitWorkspaceProvider Provider(bool audit = false) => new(sourceChanges: Tracker, auditSource: audit);
        public static async Task<Fixture> Create(string format)
        {
            var f = new Fixture(); string repo = System.IO.Path.Combine(f.directory.Path, "repo"); Directory.CreateDirectory(repo);
            await Initialize(repo, format, "input");
            await TestDirectory.Git(repo, "config", "user.name", "Fixture");
            await TestDirectory.Git(repo, "config", "user.email", "fixture@example.invalid");
            File.WriteAllText(System.IO.Path.Combine(repo, ".gitattributes"), "* text=auto\n*.bin binary\n*.cs text eol=lf\nkeyword.txt ident\n");
            File.WriteAllText(System.IO.Path.Combine(repo, "code.cs"), "return 1;\n");
            File.WriteAllText(System.IO.Path.Combine(repo, "keyword.txt"), "$Id$\n");
            byte[] binary = new byte[1024 * 1024 + 19]; binary[0] = 13; binary[1] = 10;
            File.WriteAllBytes(System.IO.Path.Combine(repo, "payload.bin"), binary);
            await TestDirectory.Git(repo, "add", "."); await TestDirectory.Git(repo, "commit", "-m", "fixture");
            f.Workspace = new() { Id = "clean-identity", RequestKey = "test", ProjectPath = repo,
                Path = System.IO.Path.Combine(f.directory.Path, "worker"), StorageMode = "git-checkout",
                SourceReference = "splinter/test", BaseReference = "development", ManagedPaths = ["Library"] };
            f.Workspace.BaseRevision = (await new GitWorkspaceProvider().CreateAsync(f.Workspace, TestContext.Current.CancellationToken)).BaseRevision;
            f.Workspace.StorageMode = "git-snapshot";
            f.Workspace.Snapshot = new() { Reference = new("fake-snapshot", "fake", repo, repo, DateTimeOffset.UtcNow, true, true, "fixture-only") };
            f.Tracker = new(System.IO.Path.Combine(f.directory.Path, "source.json")); return f;
        }
        public async Task AssertGitHashes()
        {
            using var manifest = JsonDocument.Parse(File.ReadAllText(Tracker.CachePath(Workspace)));
            foreach (var file in manifest.RootElement.GetProperty("value").GetProperty("files").EnumerateArray())
            {
                string name = file.GetProperty("path").GetString()!;
                Assert.Equal(await TestDirectory.Git(Workspace.Path, "hash-object", "--path=" + name, "--", name), file.GetProperty("gitHash").GetString());
                Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(Path(name)))), file.GetProperty("hash").GetString());
            }
        }
        public void Dispose() => directory.Dispose();
    }
}
