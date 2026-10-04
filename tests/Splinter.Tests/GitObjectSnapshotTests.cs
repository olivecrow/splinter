using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;
using Splinter.Core;
using Splinter.Runtime;
using Splinter.SourceControl.Git;

namespace Splinter.Tests;

public sealed class GitObjectSnapshotTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FixedProjectRangeReadsSurviveOriginalBranchWritesAndPackedObjects(bool packed)
    {
        using var f = await Fixture.Create(); var ct = TestContext.Current.CancellationToken;
        var snapshot = await f.Capture();
        await File.WriteAllTextAsync(Path.Combine(f.Source, "file.bin"), "original changed", ct);
        await TestDirectory.Git(f.Source, "add", "."); await TestDirectory.Git(f.Source, "commit", "-m", "later");
        if (packed) await TestDirectory.Git(f.Source, "gc");
        using var lease = f.Provider.AcquireReadLease();
        using var tree = f.Provider.OpenTree(snapshot, await f.Provider.ResolveReadAccessAsync(snapshot, ct));
        Assert.Equal(f.Data.Length, tree.Stat("file.bin")!.Length);
        Assert.Null(tree.Stat("ignored/private.txt"));
        byte[] bytes = new byte[4096];
        Assert.Equal(bytes.Length, tree.Read("file.bin", 12345, bytes));
        Assert.Equal(f.Data.AsSpan(12345, bytes.Length).ToArray(), bytes);
        Assert.Equal(f.Data.Length, tree.DecodedBlobBytes);
        Assert.Equal(0, tree.Read("file.bin", f.Data.Length, bytes));
        Assert.Equal(snapshot, await f.Capture());
        Assert.Equal(f.Revision, await TestDirectory.Git(f.Source, "rev-parse", "refs/splinter/git-bases/task"));
        Assert.DoesNotContain(Directory.GetFiles(f.Receipt, "*", SearchOption.AllDirectories), p => p.EndsWith("file.bin", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SequentialSmallReadsUseLinearDecodingAndBackwardReadsKeepExactBytes()
    {
        using var f = await Fixture.Create(); var ct = TestContext.Current.CancellationToken;
        var snapshot = await f.Capture();
        using var tree = f.Provider.OpenTree(snapshot, await f.Provider.ResolveReadAccessAsync(snapshot, ct),
            () => new(snapshot.Id, true, null, 0, null));
        byte[] actual = new byte[f.Data.Length];
        for (int offset = 0; offset < actual.Length; offset += 4096)
            Assert.Equal(4096, tree.Read("file.bin", offset, actual.AsSpan(offset, 4096)));
        Assert.Equal(f.Data, actual);
        Assert.InRange(tree.DecodedBlobBytes, f.Data.Length, 2L * f.Data.Length);
        byte[] backwards = new byte[70000];
        Assert.Equal(backwards.Length, tree.Read("file.bin", 12000, backwards));
        Assert.Equal(f.Data.AsSpan(12000, backwards.Length).ToArray(), backwards);
        Assert.Empty(Directory.GetFiles(f.Receipt, "*.bin", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task CorruptRereadChunkAfterFullProofPublishesNoneOfTheRequestedRange()
    {
        using var f = await Fixture.Create(); var ct = TestContext.Current.CancellationToken;
        var snapshot = await f.Capture();
        using var tree = f.Provider.OpenTree(snapshot, await f.Provider.ResolveReadAccessAsync(snapshot, ct));
        tree.Read("file.bin", 0, new byte[16]);
        string blob = await TestDirectory.Git(f.Source, "rev-parse", f.Revision + ":file.bin");
        string objectPath = Path.Combine(f.Source, ".git", "objects", blob[..2], blob[2..]);
        File.SetAttributes(objectPath, FileAttributes.Normal);
        byte[] corrupt = f.Data.ToArray(); corrupt[65540] ^= 1;
        using (var file = File.Create(objectPath))
        using (var zlib = new ZLibStream(file, CompressionLevel.Fastest))
        { zlib.Write(Encoding.ASCII.GetBytes("blob " + corrupt.Length + "\0")); zlib.Write(corrupt); }
        byte[] destination = Enumerable.Repeat((byte)217, 70000).ToArray();
        Assert.Equal("GitBasisObjectInvalid", Assert.Throws<WorkspaceException>(() => tree.Read("file.bin", 0, destination)).Code);
        Assert.All(destination, b => Assert.Equal((byte)217, b));
    }

    [Fact]
    public async Task RangeJournalKeepsPrivateWritesAndReadsWithoutPayloadCopyOrDiskHydration()
    {
        using var f = await Fixture.Create(); var ct = TestContext.Current.CancellationToken;
        var snapshot = await f.Capture();
        var access = await f.Provider.ResolveReadAccessAsync(snapshot, ct);
        var identity = new OverlayIdentity(2, "task", snapshot, 4 * 1024 * 1024, 1024 * 1024, [".git"]);
        string delta = Path.Combine(f.Root, "delta"); byte[] edit = Enumerable.Repeat((byte)79, 4096).ToArray();
        using (var tree = f.Provider.OpenTree(snapshot, access))
        using (var journal = new JournalDeltaStore(delta, identity, tree))
        {
            Assert.Equal(0, journal.Observe().InitialPayloadCopiedBytes);
            Assert.Equal(0, journal.Observe().ReadCacheBytes);
            var entry = journal.Stat("file.bin")!;
            journal.Write(entry.Id, 32768, edit); journal.Flush();
            Assert.InRange(journal.Observe().PayloadBytes, 1, edit.Length);
            Assert.True(journal.Observe().JournalBytes < f.Data.Length / 4);
        }
        await File.WriteAllBytesAsync(Path.Combine(f.Source, "file.bin"), new byte[f.Data.Length], ct);
        using var resumedTree = f.Provider.OpenTree(snapshot, access);
        using var resumed = new JournalDeltaStore(delta, identity, resumedTree);
        byte[] actual = new byte[edit.Length + 16];
        Assert.Equal(actual.Length, resumed.Read(resumed.Stat("file.bin")!.Id, 32760, actual));
        byte[] expected = f.Data.AsSpan(32760, actual.Length).ToArray(); edit.CopyTo(expected, 8);
        Assert.Equal(expected, actual); Assert.Equal(0, resumed.Observe().ReadCacheBytes);
    }

    [Fact]
    public async Task ReleaseCannotDeleteUnderReadersOrTouchPeerAndResultRefs()
    {
        using var f = await Fixture.Create(); var ct = TestContext.Current.CancellationToken;
        var snapshot = await f.Capture();
        await TestDirectory.Git(f.Source, "update-ref", "refs/splinter/git-bases/peer", f.Revision);
        await TestDirectory.Git(f.Source, "update-ref", "refs/splinter/snapshot-results/result", f.Revision);
        using (f.Provider.AcquireReadLease())
            Assert.Equal("SnapshotReadersActive", (await Assert.ThrowsAsync<WorkspaceException>(() => f.Provider.ReleaseAsync(snapshot, ct))).Code);
        var released = await f.Provider.ReleaseAsync(snapshot, ct); released.RequireMatch(snapshot);
        Assert.False((await f.Provider.ObserveAsync(snapshot, ct)).Available);
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(released), System.Text.Json.JsonSerializer.Serialize(await f.Provider.ReleaseAsync(snapshot, ct, released.AttemptId)));
        Assert.Equal(f.Revision, await TestDirectory.Git(f.Source, "rev-parse", "refs/splinter/git-bases/peer"));
        Assert.Equal(f.Revision, await TestDirectory.Git(f.Source, "rev-parse", "refs/splinter/snapshot-results/result"));
        Assert.Equal(f.Revision, await TestDirectory.Git(f.Source, "rev-parse", "HEAD"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ChangedOrSymbolicRefIsPreservedAndNeverRecreated(bool symbolic)
    {
        using var f = await Fixture.Create(); var ct = TestContext.Current.CancellationToken;
        var snapshot = await f.Capture();
        await File.WriteAllTextAsync(Path.Combine(f.Source, "new.txt"), "other", ct);
        await TestDirectory.Git(f.Source, "add", "."); await TestDirectory.Git(f.Source, "commit", "-m", "other");
        string later = await TestDirectory.Git(f.Source, "rev-parse", "HEAD");
        if (symbolic) await TestDirectory.Git(f.Source, "symbolic-ref", "refs/splinter/git-bases/task", "refs/heads/development");
        else await TestDirectory.Git(f.Source, "update-ref", "refs/splinter/git-bases/task", later);
        Assert.Equal("GitBasisRefChanged", (await f.Provider.ObserveAsync(snapshot, ct)).FailureCode);
        Assert.Equal("GitBasisRefChanged", (await Assert.ThrowsAsync<WorkspaceException>(() => f.Provider.ReleaseAsync(snapshot, ct))).Code);
        Assert.Equal(later, await TestDirectory.Git(f.Source, "rev-parse", "refs/splinter/git-bases/task"));
    }

    [Fact]
    public async Task LostCreateAndDeleteRepliesResumeOnlyTheRecordedIntent()
    {
        using var f = await Fixture.Create(); var ct = TestContext.Current.CancellationToken;
        var snapshot = await f.Capture(); string receipt = Path.Combine(f.Receipt, "git-basis.json");
        var original = JsonNode.Parse(await File.ReadAllTextAsync(receipt, ct))!;
        original["pinned"] = false; await File.WriteAllTextAsync(receipt, original.ToJsonString(), ct);
        Assert.Equal(snapshot, await f.Capture());
        var released = await f.Provider.ReleaseAsync(snapshot, ct);
        original["pinned"] = true; original["released"] = false;
        await File.WriteAllTextAsync(receipt, original.ToJsonString(), ct);
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(released), System.Text.Json.JsonSerializer.Serialize(await f.Provider.ReleaseAsync(snapshot, ct, released.AttemptId)));
        Assert.False((await f.Provider.ObserveAsync(snapshot, ct)).Available);
        var other = new GitObjectSnapshotProvider(f.Receipt, "peer", f.Revision);
        Assert.Equal("SnapshotIdentityMismatch", (await other.ObserveAsync(snapshot, ct)).FailureCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingOrCorruptBlobPublishesNoRequestedBytesAndNeverReadsMutableOriginal(bool corrupt)
    {
        using var f = await Fixture.Create(); var ct = TestContext.Current.CancellationToken;
        var snapshot = await f.Capture();
        using var tree = f.Provider.OpenTree(snapshot, await f.Provider.ResolveReadAccessAsync(snapshot, ct));
        string blob = await TestDirectory.Git(f.Source, "rev-parse", f.Revision + ":file.bin");
        string objectPath = Path.GetFullPath(Path.Combine(f.Source, ".git", "objects", blob[..2], blob[2..]));
        Assert.StartsWith(Path.GetFullPath(f.Root) + Path.DirectorySeparatorChar, objectPath);
        File.SetAttributes(objectPath, FileAttributes.Normal);
        if (corrupt)
        {
            using var file = File.Create(objectPath); using var zlib = new ZLibStream(file, CompressionLevel.Fastest);
            zlib.Write(Encoding.ASCII.GetBytes("blob " + f.Data.Length + "\0")); zlib.Write(new byte[f.Data.Length]);
        }
        else File.Delete(objectPath);
        byte[] bytes = Enumerable.Repeat((byte)217, 64).ToArray();
        Assert.Equal("GitBasisObjectInvalid", Assert.Throws<WorkspaceException>(() => tree.Read("file.bin", 19, bytes)).Code);
        Assert.All(bytes, b => Assert.Equal((byte)217, b));
        Assert.Equal(f.Data, await File.ReadAllBytesAsync(Path.Combine(f.Source, "file.bin"), ct));
    }

    [Fact]
    public async Task CliContractAcceptsOnlyGitForRecordedProjectMode()
    {
        var catalog = new Splinter.Protocol.ContractCatalog();
        foreach (string source in new[] { "git", "none", "plastic" })
        {
            var input = System.Text.Json.JsonSerializer.SerializeToElement(new { project = "root", key = "a", source, storage = "git-snapshot", application = "none" });
            Assert.Equal(source == "git", catalog.Validate(catalog.Find("workspace.ensure")!.InputSchema, input).IsValid);
        }
        await Task.CompletedTask;
    }

    [Fact]
    public async Task CorruptNestedTreeIsRejectedByTheBoundedBatchVerifier()
    {
        using var f = await Fixture.Create(); var ct = TestContext.Current.CancellationToken;
        var snapshot = await f.Capture(); var access = await f.Provider.ResolveReadAccessAsync(snapshot, ct);
        string tree = await TestDirectory.Git(f.Source, "rev-parse", f.Revision + ":Assets");
        string path = Path.Combine(f.Source, ".git", "objects", tree[..2], tree[2..]);
        File.SetAttributes(path, FileAttributes.Normal);
        using (var file = File.Create(path))
        using (var zlib = new ZLibStream(file, CompressionLevel.Fastest)) zlib.Write(Encoding.ASCII.GetBytes("tree 0\0"));
        Assert.Equal("GitBasisObjectInvalid", Assert.Throws<WorkspaceException>(() => f.Provider.OpenTree(snapshot, access)).Code);
    }

    [Fact]
    public async Task TreeMetadataCorruptionAndIncompleteReceiptsAreRejected()
    {
        using var f = await Fixture.Create(); var ct = TestContext.Current.CancellationToken;
        var snapshot = await f.Capture(); var access = await f.Provider.ResolveReadAccessAsync(snapshot, ct);
        string tree = await TestDirectory.Git(f.Source, "rev-parse", f.Revision + "^{tree}");
        string path = Path.Combine(f.Source, ".git", "objects", tree[..2], tree[2..]);
        byte[] original = File.ReadAllBytes(path);
        File.SetAttributes(path, FileAttributes.Normal);
        using (var file = File.Create(path))
        using (var zlib = new ZLibStream(file, CompressionLevel.Fastest)) zlib.Write(Encoding.ASCII.GetBytes("tree 0\0"));
        Assert.Equal("GitBasisObjectInvalid", Assert.Throws<WorkspaceException>(() => f.Provider.OpenTree(snapshot, access)).Code);
        File.WriteAllBytes(path, original);
        string receiptPath = Path.Combine(f.Receipt, "git-basis.json");
        var receipt = JsonNode.Parse(await File.ReadAllTextAsync(receiptPath, ct))!.AsObject(); receipt.Remove("released");
        await File.WriteAllTextAsync(receiptPath, receipt.ToJsonString(), ct);
        Assert.Equal("GitBasisReceiptInvalid", (await f.Provider.ObserveAsync(snapshot, ct)).FailureCode);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly TestDirectory directory = new();
        public string Root => directory.Path;
        public string Source => Path.Combine(Root, "source");
        public string Receipt => Path.Combine(Root, "receipt");
        public string Revision { get; private set; } = "";
        public byte[] Data { get; } = Enumerable.Range(0, 1024 * 1024).Select(i => (byte)(i * 31)).ToArray();
        public GitObjectSnapshotProvider Provider => new(Receipt, "task", Revision);
        public Task<SnapshotReference> Capture() => Provider.CaptureAsync(new(Source, "task", Receipt), TestContext.Current.CancellationToken);
        public static async Task<Fixture> Create()
        {
            var f = new Fixture(); Directory.CreateDirectory(f.Source);
            await TestDirectory.Git(f.Source, "init", "-b", "development");
            await TestDirectory.Git(f.Source, "config", "core.autocrlf", "false");
            await TestDirectory.Git(f.Source, "config", "user.name", "Fixture");
            await TestDirectory.Git(f.Source, "config", "user.email", "fixture@example.invalid");
            await File.WriteAllBytesAsync(Path.Combine(f.Source, "file.bin"), f.Data);
            await File.WriteAllTextAsync(Path.Combine(f.Source, ".gitignore"), "ignored/\n");
            Directory.CreateDirectory(Path.Combine(f.Source, "Assets"));
            await File.WriteAllTextAsync(Path.Combine(f.Source, "Assets", "nested.txt"), "fixed nested source\n");
            Directory.CreateDirectory(Path.Combine(f.Source, "ignored"));
            await File.WriteAllTextAsync(Path.Combine(f.Source, "ignored", "private.txt"), "not recorded");
            await TestDirectory.Git(f.Source, "add", "."); await TestDirectory.Git(f.Source, "commit", "-m", "basis");
            f.Revision = await TestDirectory.Git(f.Source, "rev-parse", "HEAD"); return f;
        }
        public void Dispose() => directory.Dispose();
    }
}
