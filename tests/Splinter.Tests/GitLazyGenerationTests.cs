using System.Security.Cryptography;
using Splinter.Core;
using Splinter.Platform.Windows;
using Splinter.Runtime;
using Splinter.SourceControl.Files;
using Splinter.SourceControl.Git;

namespace Splinter.Tests;

public sealed class GitLazyGenerationTests
{
    [Fact]
    public async Task IntermediateCaptureKeepsOnlyPrivateChunksAndExplicitBranchMaterializationPreservesModes()
    {
        if (!OperatingSystem.IsWindows()) Assert.Skip("Native shared immutable result ranges.");
        using var files = new TestDirectory(); var ct = TestContext.Current.CancellationToken;
        string project = Path.Combine(files.Path, "original"), home = Path.Combine(files.Path, "home"); Directory.CreateDirectory(project);
        await TestDirectory.Git(project, "init", "-b", "development");
        await TestDirectory.Git(project, "config", "user.name", "Fixture"); await TestDirectory.Git(project, "config", "user.email", "fixture@example.invalid");
        await TestDirectory.Git(project, "config", "core.autocrlf", "false");
        byte[] original = new byte[2 * 1024 * 1024]; new Random(31).NextBytes(original);
        File.WriteAllBytes(Path.Combine(project, "large.bin"), original); File.WriteAllText(Path.Combine(project, "run.sh"), "base\n");
        await TestDirectory.Git(project, "add", "."); await TestDirectory.Git(project, "update-index", "--chmod=+x", "run.sh"); await TestDirectory.Git(project, "commit", "-m", "base");
        string revision = await TestDirectory.Git(project, "rev-parse", "HEAD"); byte[] index = File.ReadAllBytes(Path.Combine(project, ".git", "index"));
        var pins = new WindowsJournalFilePins(); var store = new WorkspaceStore(home, pins);
        var basis = new SnapshotOverlayTests.FakeSnapshotTree(); basis.Files["large.bin"] = original; basis.Files["run.sh"] = "base\n"u8.ToArray();
        string id = new('a', 24), captureId = new('b', 24); var intent = new WorkspaceCaptureIntent(captureId, 1, "first", "first");
        var identity = new SourceCaptureIdentity(captureId, 1, new string('c', 64));
        var w = new WorkspaceRecord { Id = id, RequestKey = "lazy", RequestDigest = "lazy-request", ProjectPath = project,
            Path = store.WorkPath(id), SourceReference = "codex/lazy", BaseReference = "HEAD", BaseRevision = revision,
            SourceKind = "git", ApplicationKind = "none", StorageMode = "git-snapshot", CaptureIntent = intent,
            Snapshot = new() { Reference = basis.Snapshot } };
        byte[] changed = original.ToArray(); "private!"u8.CopyTo(changed.AsSpan(4096));
        using var capture = new Capture(basis, changed, identity);
        var provider = new FileWorkspaceProvider(store, contentPins: pins);
        var result = await provider.PreserveGenerationAsync(w, store.ControlPath(id), intent, identity, capture, ct);
        Assert.Null(result.GitReference); Assert.Null(result.GitFingerprint); w.Results.Add(result); w.ResultRevision = result.Revision;
        Assert.Equal("", await TestDirectory.Git(project, "for-each-ref", "--format=%(refname)", "refs/splinter/validation-results/"));
        string retainedChunk = Assert.Single(Directory.GetFiles(Path.Combine(home, "content-store", "objects"), "*", SearchOption.AllDirectories));
        Assert.Equal(8, new FileInfo(retainedChunk).Length);
        capture.Available = false; basis.Available = false; File.WriteAllText(Path.Combine(project, "large.bin"), "independent original edit");
        await provider.VerifyGenerationAsync(w, store.ControlPath(id), result, ct);
        var materialized = await provider.MaterializeGitGenerationAsync(w, result, ct);
        Assert.NotNull(materialized.GitReference); Assert.Null(result.GitReference);
        Assert.Equal(materialized, await provider.MaterializeGitGenerationAsync(w, result, ct));
        Assert.StartsWith("100755 blob ", await TestDirectory.Git(project, "ls-tree", materialized.GitReference!, "run.sh"));
        using (var payload = GitSourceResult.OpenPayload(project, materialized.GitReference!, "large.bin"))
            Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(changed)), Convert.ToHexStringLower(SHA256.HashData(payload)));
        Assert.Equal(index, File.ReadAllBytes(Path.Combine(project, ".git", "index"))); Assert.Equal(revision, await TestDirectory.Git(project, "rev-parse", "HEAD"));
        await TestDirectory.Git(project, "update-ref", "-d", GitRetainedResultBasis.Reference(w));
        await Assert.ThrowsAsync<WorkspaceException>(() => provider.VerifyGenerationAsync(w, store.ControlPath(id), result, ct));
    }

    private sealed class Capture(SnapshotOverlayTests.FakeSnapshotTree basis, byte[] current, SourceCaptureIdentity identity) : ISourceCaptureTree, ISourceCaptureRanges
    {
        public bool Available { get; set; } = true;
        public SnapshotReference Snapshot => basis.Snapshot;
        public ISnapshotTree Basis => new ModeTree(basis);
        public SourceCaptureIdentity CaptureIdentity => identity;
        public void EnsureAvailable() { if (!Available) throw new WorkspaceException("SnapshotMissing", "Released test capture."); }
        public SnapshotEntry? Stat(string path)
        { var entry = new ModeTree(basis).Stat(path); return entry is null || path != "large.bin" ? entry : entry with { Length = current.Length }; }
        public IReadOnlyList<SnapshotEntry> List(string path) { EnsureAvailable(); return new ModeTree(basis).List(path); }
        public int Read(string path, long offset, Span<byte> output)
        { EnsureAvailable(); if (path != "large.bin") return basis.Read(path, offset, output); int count = (int)Math.Min(output.Length, current.Length - offset); current.AsSpan((int)offset, count).CopyTo(output); return count; }
        public CapturedFileRangeLayout? GetFileRanges(string path) => path == "large.bin" ? new(path, current.Length,
            [new(4096, 8, new string('d', 64), BlockIdentity: new string('d', 64))], Guid.Parse("11111111-1111-1111-1111-111111111111")) : new(path, basis.Files[path].Length, []);
        public void Dispose() { }
    }
    private sealed class ModeTree(SnapshotOverlayTests.FakeSnapshotTree basis) : ISnapshotTree
    {
        public SnapshotReference Snapshot => basis.Snapshot;
        public void EnsureAvailable() => basis.EnsureAvailable();
        public SnapshotEntry? Stat(string path) { var entry = basis.Stat(path); return entry is null || entry.Directory ? entry : entry with { UnixMode = path == "run.sh" ? 0x1EDu : 0x1A4u }; }
        public IReadOnlyList<SnapshotEntry> List(string path) => basis.List(path).Select(entry => Stat(entry.Path)!).ToArray();
        public int Read(string path, long offset, Span<byte> output) => basis.Read(path, offset, output);
    }
}
