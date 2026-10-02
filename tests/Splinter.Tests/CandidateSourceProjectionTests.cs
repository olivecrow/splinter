using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Splinter.Core;
using Splinter.Protocol;
using Splinter.Runtime;
using Splinter.SourceControl.Git;

namespace Splinter.Tests;

public sealed class CandidateSourceProjectionTests
{
    private const string Pointer = "gitdir: C:/owned-fixture/.git/worktrees/worker\n";
    private static readonly string[] Forbidden = [".git", "Library", "Temp", "Logs", "UserSettings", "obj"];
    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static void Write(IWorkspaceDeltaStore store, string path, string text)
    {
        var file = store.Stat(path) ?? store.Create(path, false, 32);
        store.SetLength(file.Id, 0); store.Write(file.Id, 0, Encoding.UTF8.GetBytes(text));
    }
    private static string Read(ISnapshotTree tree, string path)
    {
        byte[] bytes = new byte[(int)tree.Stat(path)!.Length];
        Assert.Equal(bytes.Length, tree.Read(path, 0, bytes));
        return Encoding.UTF8.GetString(bytes);
    }
    private sealed class Fixture : IDisposable
    {
        public readonly TestDirectory Directory = new();
        public readonly SnapshotOverlayTests.FakeSnapshotTree Tree = new();
        public OverlayIdentity Identity => new(2, "worker", Tree.Snapshot, 10_000_000, 65536, [".git"]);
        public string Worker => Path.Combine(Directory.Path, "worker");
        public string Candidate => Path.Combine(Directory.Path, "candidate");
        public string Journal => Path.Combine(Worker, "delta.journal");
        public CandidateSourceProjection Proof => new(1, Identity.Id, Tree.Snapshot, new string('a', 64),
            [new(".git", Encoding.UTF8.GetByteCount(Pointer), Hash(Encoding.UTF8.GetBytes(Pointer)))]);
        public Fixture()
        {
            Tree.Files[".git/config"] = "original repository"u8.ToArray();
            Tree.Files["Assets/code.cs"] = "original"u8.ToArray();
            Tree.Files["Assets/old.meta"] = "guid"u8.ToArray();
            Tree.Files["Assets/remove"] = [1];
            using var store = OpenWorker(); Write(store, ".git", Pointer); Write(store, "Assets/code.cs", "candidate");
        }
        public JournalDeltaStore OpenWorker() => new(Worker, Identity, Tree);
        public ValidationCandidate Export(CandidateSourceProjection? proof = null, Action<string>? fault = null) =>
            JournalDeltaStore.ExportSourceCandidate(Worker, Identity, Tree, Candidate, "candidate", Forbidden, proof ?? Proof, fault);
        public void Dispose() => Directory.Dispose();
    }

    [Fact]
    public void QualifiedPointerIsAbsentWhileSourceRangesAndWorkerRemainIntact()
    {
        using var f = new Fixture(); long sequence;
        using (var worker = f.OpenWorker())
        {
            worker.Rename(worker.Stat("Assets/old.meta")!.Id, "Assets/new.meta", false);
            worker.Delete(worker.Stat("Assets/remove")!.Id);
            var sparse = worker.Create("Assets/sparse", false, 32);
            worker.SetLength(sparse.Id, 1024L * 1024 * 1024);
            worker.Write(sparse.Id, 8192, [1, 2, 3, 4]);
            sequence = worker.Observe().DurableSequence;
        }
        byte[] original = File.ReadAllBytes(f.Journal);
        var receipt = f.Export();
        Assert.Equal(original, File.ReadAllBytes(f.Journal));
        Assert.Equal(Hash(original), receipt.SourceSeal.JournalHash);
        Assert.Equal(sequence, receipt.Seal.DurableSequence);
        Assert.Equal(0, f.Tree.ReadBytes);
        Assert.True(File.ReadAllBytes(Path.Combine(f.Candidate, "delta.journal")).AsSpan().IndexOf(Encoding.UTF8.GetBytes(Pointer)) < 0);
        using var candidate = new CandidateSnapshotTree(f.Candidate, f.Tree);
        Assert.Null(candidate.Stat(".git")); Assert.Null(candidate.Stat(".git/config"));
        Assert.Equal("candidate", Read(candidate, "Assets/code.cs"));
        Assert.Null(candidate.Stat("Assets/old.meta")); Assert.Equal("guid", Read(candidate, "Assets/new.meta"));
        Assert.Null(candidate.Stat("Assets/remove"));
        Assert.Equal(1024L * 1024 * 1024, candidate.Stat("Assets/sparse")!.Length);
        byte[] range = new byte[4]; candidate.Read("Assets/sparse", 8192, range); Assert.Equal(new byte[] { 1, 2, 3, 4 }, range);
        using (var worker = f.OpenWorker()) Write(worker, "Assets/code.cs", "follow-up");
        Assert.Equal("candidate", Read(candidate, "Assets/code.cs"));
        using var view = new ValidationViewDeltaStore(Path.Combine(f.Directory.Path, "view-source"), f.Identity, f.Tree,
            new(f.Candidate, Path.Combine(f.Directory.Path, "cache"), ["Library", "Temp"]));
        Assert.Null(view.Stat(".git")); Write(view, ".git", "a different view registration");
        Assert.Null(candidate.Stat(".git"));
        Assert.Equal("original", Encoding.UTF8.GetString(f.Tree.Files["Assets/code.cs"]));
    }

    [Theory]
    [InlineData("owner", "CandidateProjectionInvalid")]
    [InlineData("snapshot", "CandidateProjectionInvalid")]
    [InlineData("evidence", "CandidateProjectionInvalid")]
    [InlineData("user-path", "CandidateProjectionInvalid")]
    [InlineData("duplicates", "CandidateProjectionInvalid")]
    [InlineData("version", "CandidateProjectionInvalid")]
    [InlineData("content", "CandidateProjectionMismatch")]
    [InlineData("directory", "CandidateProjectionMismatch")]
    [InlineData("missing", "CandidateProjectionMismatch")]
    [InlineData("rename-out", "CandidateProjectionHistoryUnsupported")]
    [InlineData("rename-in", "CandidateProjectionHistoryUnsupported")]
    [InlineData("directory-history", "CandidateContainsManagedPaths")]
    [InlineData("cache", "CandidateContainsManagedPaths")]
    public void UnqualifiedProjectionCannotCreateACandidateOrChangeTheWorker(string fault, string error)
    {
        using var f = new Fixture(); var proof = f.Proof;
        switch (fault)
        {
            case "owner": proof = proof with { OwnerId = "other" }; break;
            case "snapshot": proof = proof with { Snapshot = proof.Snapshot with { RootPath = "other" } }; break;
            case "evidence": proof = proof with { EvidenceHash = "unverified" }; break;
            case "user-path": proof = proof with { Files = [proof.Files[0] with { Path = "Assets" }] }; break;
            case "duplicates": proof = proof with { Files = [proof.Files[0], proof.Files[0]] }; break;
            case "version": proof = proof with { Version = 0 }; break;
            default:
                using (var worker = f.OpenWorker())
                {
                    switch (fault)
                    {
                        case "content": Write(worker, ".git", "user data or another registration"); break;
                        case "directory": worker.Delete(worker.Stat(".git")!.Id); worker.Create(".git", true, 16); break;
                        case "missing": worker.Delete(worker.Stat(".git")!.Id); break;
                        case "rename-out": worker.Rename(worker.Stat(".git")!.Id, "saved-pointer", false); Write(worker, ".git", Pointer); break;
                        case "rename-in": Write(worker, "user-file", Pointer); worker.Delete(worker.Stat(".git")!.Id); worker.Rename(worker.Stat("user-file")!.Id, ".git", false); break;
                        case "directory-history":
                            worker.Delete(worker.Stat(".git")!.Id); worker.Create(".git", true, 16); Write(worker, ".git/user-data", "preserve");
                            worker.Delete(worker.Stat(".git/user-data")!.Id); worker.Delete(worker.Stat(".git")!.Id); Write(worker, ".git", Pointer); break;
                        case "cache": worker.Create("Library", true, 16); Write(worker, "Library/cache", "preserve"); break;
                    }
                }
                break;
        }
        byte[] before = File.ReadAllBytes(f.Journal);
        Assert.Equal(error, Assert.Throws<WorkspaceException>(() => f.Export(proof)).Code);
        Assert.Equal(before, File.ReadAllBytes(f.Journal)); Assert.False(System.IO.Directory.Exists(f.Candidate));
        Assert.Equal(0, f.Tree.ReadBytes);
    }

    [Fact]
    public void StrictExportAndActiveWriterCannotBypassProjectionQualification()
    {
        using var f = new Fixture(); byte[] before = File.ReadAllBytes(f.Journal);
        Assert.Equal("CandidateContainsManagedPaths", Assert.Throws<WorkspaceException>(() =>
            JournalDeltaStore.ExportCandidate(f.Worker, f.Identity, f.Tree, f.Candidate, "strict", [])).Code);
        using (var writer = f.OpenWorker()) Assert.Throws<IOException>(() => f.Export());
        Assert.Equal(before, File.ReadAllBytes(f.Journal)); Assert.False(System.IO.Directory.Exists(f.Candidate));
    }

    [Theory]
    [InlineData("candidate-flushed")]
    [InlineData("receipt-flushed")]
    public void InterruptedUnpublishedCandidateAndOriginalArePreserved(string boundary)
    {
        using var f = new Fixture(); byte[] source = File.ReadAllBytes(f.Journal);
        Assert.Throws<IOException>(() => f.Export(fault: current => { if (current == boundary) throw new IOException("injected interruption"); }));
        byte[] incomplete = File.ReadAllBytes(Path.Combine(f.Candidate, "delta.journal"));
        Assert.Equal("CandidateUnpublished", Assert.Throws<WorkspaceException>(() => new CandidateSnapshotTree(f.Candidate, f.Tree)).Code);
        Assert.Equal("CandidateAlreadyExists", Assert.Throws<WorkspaceException>(() => f.Export()).Code);
        Assert.Equal(source, File.ReadAllBytes(f.Journal));
        Assert.Equal(incomplete, File.ReadAllBytes(Path.Combine(f.Candidate, "delta.journal")));
        Assert.Equal(boundary == "receipt-flushed", File.Exists(Path.Combine(f.Candidate, "candidate.json.new")));
    }

    [Theory]
    [InlineData("old-version")]
    [InlineData("missing-proof-field")]
    [InlineData("null-proof")]
    [InlineData("foreign-owner")]
    [InlineData("bad-content-hash")]
    public void CurrentCandidateFormatRequiresItsProjectionEvidenceWithoutMigration(string fault)
    {
        using var f = new Fixture(); var receipt = f.Export();
        var contracts = new ContractCatalog();
        Assert.True(contracts.Validate("urn:splinter:v1:validation-candidate", JsonSerializer.SerializeToElement(receipt, ApiJson.Options)).IsValid);
        string path = Path.Combine(f.Candidate, "candidate.json"); var node = JsonNode.Parse(File.ReadAllText(path))!;
        switch (fault)
        {
            case "old-version": node["Version"] = 1; break;
            case "missing-proof-field": node.AsObject().Remove("SourceProjection"); break;
            case "null-proof": node["SourceProjection"] = null; break;
            case "foreign-owner": node["SourceProjection"]!["OwnerId"] = "other"; break;
            case "bad-content-hash": node["SourceProjection"]!["Files"]![0]!["ContentHash"] = "bad"; break;
        }
        string changed = node.ToJsonString(); File.WriteAllText(path, changed);
        byte[] journal = File.ReadAllBytes(Path.Combine(f.Candidate, "delta.journal"));
        Assert.Throws<WorkspaceException>(() => new CandidateSnapshotTree(f.Candidate, f.Tree));
        Assert.Equal(changed, File.ReadAllText(path)); Assert.Equal(journal, File.ReadAllBytes(Path.Combine(f.Candidate, "delta.journal")));
    }

    private sealed record GitFixture(TestDirectory Directory, string Source, OverlayGitReceipt Receipt, OverlayGitReceipt Other,
        OverlayIdentity Identity) : IDisposable
    {
        public void Dispose() => Directory.Dispose();
        public static async Task<GitFixture> Create(CancellationToken ct)
        {
            var d = new TestDirectory();
            try
            {
                string source = Path.Combine(d.Path, "source"); System.IO.Directory.CreateDirectory(source);
                await TestDirectory.Git(source, "init", "-b", "development");
                await TestDirectory.Git(source, "config", "user.name", "Fixture"); await TestDirectory.Git(source, "config", "user.email", "fixture@example.invalid");
                await File.WriteAllTextAsync(Path.Combine(source, "source.txt"), "original", ct);
                await TestDirectory.Git(source, "add", "."); await TestDirectory.Git(source, "commit", "-m", "fixture");
                string revision = await TestDirectory.Git(source, "rev-parse", "HEAD");
                var a = await GitOverlayRegistration.PrepareAsync(source, Path.Combine(d.Path, "mount-a"), "codex/a", revision, "a", Path.Combine(d.Path, "a.json"), ct);
                var b = await GitOverlayRegistration.PrepareAsync(source, Path.Combine(d.Path, "mount-b"), "codex/b", revision, "b", Path.Combine(d.Path, "b.json"), ct);
                var snapshot = new SnapshotReference("snapshot", "fake", source, "immutable", DateTimeOffset.UnixEpoch, true, true, "fixture");
                return new(d, source, a, b, new(2, "a", snapshot, 10_000_000, 65536, [".git"]));
            }
            catch { d.Dispose(); throw; }
        }
    }

    [Fact]
    public async Task RealGitQualificationAndSyntheticPublicationKeepOtherWorktreesAndDirtyOriginal()
    {
        var ct = TestContext.Current.CancellationToken; using var f = await GitFixture.Create(ct);
        string before = await TestDirectory.Git(f.Source, "worktree", "list", "--porcelain");
        byte[] originalIndex = await File.ReadAllBytesAsync(Path.Combine(f.Source, ".git", "index"), ct);
        await File.WriteAllTextAsync(Path.Combine(f.Source, "source.txt"), "ongoing original edit", ct);
        var proof = await GitOverlayRegistration.QualifyCandidateSourceAsync(f.Receipt, f.Identity, ct);
        var tree = new SnapshotOverlayTests.FakeSnapshotTree { Snapshot = f.Identity.Snapshot }; tree.Files["source.txt"] = "captured"u8.ToArray();
        string worker = Path.Combine(f.Directory.Path, "worker"), candidatePath = Path.Combine(f.Directory.Path, "candidate");
        using (var store = new JournalDeltaStore(worker, f.Identity, tree)) { Write(store, ".git", f.Receipt.Pointer); Write(store, "source.txt", "candidate"); }
        var candidate = JournalDeltaStore.ExportSourceCandidate(worker, f.Identity, tree, candidatePath, "c", Forbidden, proof);
        Assert.True(new ContractCatalog().Validate("urn:splinter:v1:validation-candidate", JsonSerializer.SerializeToElement(candidate, ApiJson.Options)).IsValid);
        using var opened = new CandidateSnapshotTree(candidatePath, tree); Assert.Null(opened.Stat(".git")); Assert.Equal("candidate", Read(opened, "source.txt"));
        Assert.Equal(before, await TestDirectory.Git(f.Source, "worktree", "list", "--porcelain"));
        Assert.Equal(originalIndex, await File.ReadAllBytesAsync(Path.Combine(f.Source, ".git", "index"), ct));
        Assert.Equal("ongoing original edit", await File.ReadAllTextAsync(Path.Combine(f.Source, "source.txt"), ct));
        await GitOverlayRegistration.VerifyAsync(f.Other, ct); Assert.Equal(0, tree.ReadBytes);
    }

    [Theory]
    [InlineData("owner", "GitOverlayIdentityMismatch")]
    [InlineData("source", "GitOverlayIdentityMismatch")]
    [InlineData("pointer", "GitOverlayPointerMismatch")]
    [InlineData("backlink", "GitOverlayOwnerMismatch")]
    [InlineData("head", "GitOverlayOwnerMismatch")]
    [InlineData("common", "GitOverlayOwnerMismatch")]
    [InlineData("ownership", "GitOverlayOwnerMismatch")]
    [InlineData("lock", "CandidateRegistrationBusy")]
    public async Task RealGitQualificationRejectsForeignOrBusyRegistrationWithoutRepair(string fault, string error)
    {
        var ct = TestContext.Current.CancellationToken; using var f = await GitFixture.Create(ct);
        var identity = f.Identity; var receipt = f.Receipt; string? altered = null;
        switch (fault)
        {
            case "owner": identity = identity with { Id = "b" }; break;
            case "source": identity = identity with { Snapshot = identity.Snapshot with { SourcePath = f.Directory.Path } }; break;
            case "pointer": receipt = receipt with { Pointer = f.Other.Pointer }; break;
            case "backlink": altered = Path.Combine(receipt.GitDirectory, "gitdir"); await File.WriteAllTextAsync(altered, Path.Combine(f.Other.Path, ".git"), ct); break;
            case "head": altered = Path.Combine(receipt.GitDirectory, "HEAD"); await File.WriteAllTextAsync(altered, "ref: refs/heads/codex/b\n", ct); break;
            case "common": altered = Path.Combine(receipt.GitDirectory, "commondir"); await File.WriteAllTextAsync(altered, f.Directory.Path, ct); break;
            case "ownership": altered = Path.Combine(receipt.GitDirectory, "locked"); await File.WriteAllTextAsync(altered, "Splinter overlay b\n", ct); break;
            case "lock": altered = Path.Combine(receipt.GitDirectory, "index.lock"); await File.WriteAllTextAsync(altered, "keep writer", ct); break;
        }
        byte[]? bytes = altered is null ? null : await File.ReadAllBytesAsync(altered, ct);
        Assert.Equal(error, (await Assert.ThrowsAsync<WorkspaceException>(() => GitOverlayRegistration.QualifyCandidateSourceAsync(receipt, identity, ct))).Code);
        if (altered is not null) Assert.Equal(bytes, await File.ReadAllBytesAsync(altered, ct));
        Assert.Equal("original", await File.ReadAllTextAsync(Path.Combine(f.Source, "source.txt"), ct));
        await GitOverlayRegistration.VerifyAsync(f.Other, ct);
    }
}
