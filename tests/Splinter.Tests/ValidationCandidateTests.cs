using System.Text;
using System.Text.Json;
using Splinter.Core;
using Splinter.Runtime;
using Splinter.Protocol;

namespace Splinter.Tests;

public sealed class ValidationCandidateTests
{
    private static readonly string[] CacheRoots = ["Library", "Temp"];
    private static OverlayIdentity Identity(ISnapshotTree tree) => new(1, "view", tree.Snapshot, 100_000_000, 65536, [".git"]);
    private static void Write(IWorkspaceDeltaStore store, string path, string value)
    {
        var entry = store.Stat(path) ?? store.Create(path, false, 32);
        store.SetLength(entry.Id, 0); store.Write(entry.Id, 0, Encoding.UTF8.GetBytes(value));
    }
    private static string Read(IWorkspaceDeltaStore store, string path)
    { var entry = store.Stat(path)!; byte[] data = new byte[(int)entry.Length]; store.Read(entry.Id, 0, data); return Encoding.UTF8.GetString(data); }
    private static SnapshotOverlayTests.FakeSnapshotTree Tree()
    {
        var tree = new SnapshotOverlayTests.FakeSnapshotTree();
        tree.Files["Assets/code.cs"] = "original"u8.ToArray(); tree.Files["Assets/old.meta"] = "guid"u8.ToArray();
        tree.Files["Assets/deleted.txt"] = [1]; tree.Files["Assets/large"] = new byte[16 * 1024 * 1024];
        return tree;
    }
    private static string Export(string root, string label, ISnapshotTree tree)
    {
        string worker = Path.Combine(root, "worker-" + label), destination = Path.Combine(root, "candidate-" + label);
        using (var store = new JournalDeltaStore(worker, Identity(tree), tree))
        {
            Write(store, "Assets/code.cs", label);
            if (label == "A")
            {
                store.Rename(store.Stat("Assets/old.meta")!.Id, "Assets/new.meta", false);
                store.Delete(store.Stat("Assets/deleted.txt")!.Id);
                Write(store, "Assets/added.txt", "new");
                var large = store.Stat("Assets/large")!;
                store.Write(large.Id, 65536, new byte[4096]); store.SetLength(large.Id, 131072);
            }
        }
        JournalDeltaStore.ExportCandidate(worker, Identity(tree), tree, destination, label, CacheRoots);
        return destination;
    }

    [Fact]
    public void SnapshotLibraryIsReadThroughWithoutCopyAndKeepsItsBaseAcrossCandidates()
    {
        using var dir = new TestDirectory(); var a = Tree(); var b = Tree();
        a.Files["Library/large-cache"] = new byte[16 * 1024 * 1024];
        a.Files["Library/base-label"] = "library-a"u8.ToArray();
        b.Snapshot = b.Snapshot with { Id = "independent-b" };
        b.Files["Library/base-label"] = "library-b"u8.ToArray();
        string ca = Export(dir.Path, "A", a), cb = Export(dir.Path, "B", b), cache = Path.Combine(dir.Path, "cache");
        var cacheIdentity = Identity(a) with { Id = "shared-cache", ExcludedTopLevel = [], IncludedTopLevel = CacheRoots };
        var patch = Enumerable.Repeat((byte)0x42, 4096).ToArray();
        using (var first = new ValidationViewDeltaStore(Path.Combine(dir.Path, "source-a"), Identity(a), a,
                   new(ca, cache, CacheRoots, cacheIdentity, "snapshot"), cacheBasis: a))
        {
            Assert.Equal(0, a.ReadBytes);
            Assert.Equal(0, new FileInfo(Path.Combine(cache, "delta.journal")).Length);
            var file = first.Stat("Library/large-cache")!;
            var actual = new byte[4096]; first.Read(file.Id, 8192, actual);
            Assert.Equal(new byte[4096], actual);
            Assert.Equal(0, new FileInfo(Path.Combine(cache, "delta.journal")).Length);
            first.Write(file.Id, 8192, patch);
            Assert.True(new FileInfo(Path.Combine(cache, "delta.journal")).Length < 10000);
        }
        using (var next = new ValidationViewDeltaStore(Path.Combine(dir.Path, "source-b"), Identity(b), b,
                   new(cb, cache, CacheRoots, cacheIdentity, "snapshot"), cacheBasis: a))
        {
            Assert.Equal("B", Read(next, "Assets/code.cs"));
            Assert.Equal("library-a", Read(next, "Library/base-label"));
            var actual = new byte[4096]; next.Read(next.Stat("Library/large-cache")!.Id, 8192, actual);
            Assert.Equal(patch, actual);
            Assert.Equal(new byte[4096], a.Files["Library/large-cache"].AsSpan(8192, 4096).ToArray());
            Assert.Equal("library-b", Encoding.UTF8.GetString(b.Files["Library/base-label"]));
        }
        using var check = ValidationViewDeltaStore.OpenCacheReadOnly(cache, cacheIdentity, a);
        Assert.Equal(4096, check.Observe().PayloadBytes);
        Assert.Equal(0, check.Observe().InitialPayloadCopiedBytes);
        Assert.Equal(0, check.Observe().ReadCacheBytes);
        Assert.True(a.ReadBytes < 10000);
    }

    [Fact]
    public void MissingOrReplacedLibraryBaseIsRejectedWithoutFallingBackToEmptyCache()
    {
        using var dir = new TestDirectory(); var a = Tree(); var b = Tree();
        a.Files["Library/base"] = "keep"u8.ToArray(); b.Snapshot = b.Snapshot with { Id = "independent-b" };
        string ca = Export(dir.Path, "A", a), cb = Export(dir.Path, "B", b), cache = Path.Combine(dir.Path, "cache");
        var cacheIdentity = Identity(a) with { Id = "shared-cache", ExcludedTopLevel = [], IncludedTopLevel = CacheRoots };
        var selection = new ValidationViewSelection(cb, cache, CacheRoots, cacheIdentity, "snapshot");
        using (var first = new ValidationViewDeltaStore(Path.Combine(dir.Path, "source-a"), Identity(a), a,
                   new(ca, cache, CacheRoots, cacheIdentity, "snapshot"), cacheBasis: a))
            first.Write(first.Stat("Library/base")!.Id, 0, "K"u8);
        byte[] retained = File.ReadAllBytes(Path.Combine(cache, "delta.journal"));
        string source = Path.Combine(dir.Path, "source-b");
        Assert.Equal("ValidationCacheMismatch", Assert.Throws<WorkspaceException>(() =>
            new ValidationViewDeltaStore(source, Identity(b), b, selection)).Code);
        Assert.Equal("ValidationCacheMismatch", Assert.Throws<WorkspaceException>(() =>
            new ValidationViewDeltaStore(source, Identity(b), b, selection, cacheBasis: b)).Code);
        Assert.Equal("ValidationCacheMismatch", Assert.Throws<WorkspaceException>(() =>
            new ValidationViewDeltaStore(source, Identity(b), b, selection with { CacheBasis = "empty" })).Code);
        a.Available = false;
        Assert.Equal("SnapshotMissing", Assert.Throws<WorkspaceException>(() =>
            new ValidationViewDeltaStore(source, Identity(b), b, selection, cacheBasis: a)).Code);
        Assert.Equal(retained, File.ReadAllBytes(Path.Combine(cache, "delta.journal")));
        Assert.False(Directory.Exists(source));
    }

    [Fact]
    public void CacheSnapshotCannotExposeSourceAndCompactsOnlyPrivateRepeatedRanges()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var dir = new TestDirectory(); var tree = Tree();
        tree.Files["Library/large"] = new byte[16 * 1024 * 1024];
        var basis = new ValidationCacheSnapshotTree(tree, tree.Snapshot, CacheRoots);
        Assert.Null(basis.Stat("Assets/code.cs"));
        Assert.DoesNotContain(basis.List(""), e => e.Path == "Assets");
        Assert.Throws<FileNotFoundException>(() => basis.Read("Assets/code.cs", 0, new byte[4096]));
        var identity = Identity(tree) with { Id = "shared-cache", ExcludedTopLevel = [], IncludedTopLevel = CacheRoots };
        string path = Path.Combine(dir.Path, "cache");
        using (var store = new JournalDeltaStore(path, identity, basis))
        {
            var id = store.Stat("Library/large")!.Id;
            for (int i = 0; i < 32; i++) store.Write(id, 4096, Enumerable.Repeat((byte)i, 4096).ToArray());
        }
        var receipt = JournalDeltaStore.CompactDirectory(path, identity, basis, maxEstimatedJournalOverlapBytes: 500_000);
        JournalDeltaStore.CompleteCompaction(path, identity);
        Assert.Equal(0, tree.ReadBytes);
        Assert.True(receipt.After.JournalBytes < receipt.Before.JournalBytes / 2);
        using var resumed = ValidationViewDeltaStore.OpenCacheReadOnly(path, identity, tree);
        Assert.Equal(16 * 1024 * 1024, resumed.Stat("Library/large")!.Length);
        byte[] changed = new byte[4096], unchanged = new byte[4096];
        resumed.Read(resumed.Stat("Library/large")!.Id, 4096, changed);
        resumed.Read(resumed.Stat("Library/large")!.Id, 8192, unchanged);
        Assert.Equal(Enumerable.Repeat((byte)31, 4096).ToArray(), changed);
        Assert.Equal(new byte[4096], unchanged);
        Assert.Equal(4096, resumed.Observe().PayloadBytes);
    }

    [Fact]
    public void ExplicitCacheOwnerSupportsIndependentSnapshotsWithoutReadingAnOldBase()
    {
        using var dir = new TestDirectory(); var a = Tree(); var b = Tree();
        b.Snapshot = b.Snapshot with { Id = "independent-b" };
        string ca = Export(dir.Path, "A", a), cb = Export(dir.Path, "B", b), cache = Path.Combine(dir.Path, "cache");
        var cacheIdentity = Identity(a) with { Id = "owned-cache", ExcludedTopLevel = CacheRoots,
            Snapshot = a.Snapshot with { Provider = "validation-empty-cache" } };
        using (var first = new ValidationViewDeltaStore(Path.Combine(dir.Path, "source-a"), Identity(a), a, new(ca, cache, CacheRoots, cacheIdentity)))
        { first.Create("Library", true, 16); Write(first, "Library/reusable", "warm-cache"); }
        a.Available = false;
        using var next = new ValidationViewDeltaStore(Path.Combine(dir.Path, "source-b"), Identity(b), b, new(cb, cache, CacheRoots, cacheIdentity));
        Assert.Equal("B", Read(next, "Assets/code.cs"));
        Assert.Equal("warm-cache", Read(next, "Library/reusable"));
        Write(next, "Assets/output.cs", "generation-b");
        var changes = next.ObserveChanges([]);
        Assert.Equal(b.Snapshot.Id, changes.SnapshotId);
        Assert.Contains(changes.Changes, change => change.Path == "Assets/output.cs");
    }

    [Fact]
    public void OfflineABAReusesOnlyCacheAndPreservesEachSourceOutputWithoutHydration()
    {
        using var dir = new TestDirectory(); var tree = Tree();
        string a = Export(dir.Path, "A", tree), b = Export(dir.Path, "B", tree);
        Assert.Equal(0, tree.ReadBytes);
        using (var worker = new JournalDeltaStore(Path.Combine(dir.Path, "worker-A"), Identity(tree), tree))
            Write(worker, "Assets/code.cs", "A-follow-up");
        string cache = Path.Combine(dir.Path, "cache");
        for (int i = 0; i < 3; i++)
        {
            bool isA = i != 1;
            using var view = new ValidationViewDeltaStore(Path.Combine(dir.Path, "source-" + i), Identity(tree), tree,
                new(isA ? a : b, cache, CacheRoots));
            Assert.Equal(isA ? "A" : "B", Read(view, "Assets/code.cs"));
            Assert.Equal(isA, view.Stat("Assets/new.meta") is not null);
            Assert.Equal(!isA, view.Stat("Assets/old.meta") is not null);
            Assert.Equal(!isA, view.Stat("Assets/deleted.txt") is not null);
            Assert.Equal(isA, view.Stat("Assets/added.txt") is not null);
            Assert.Equal(isA ? 131072 : 16 * 1024 * 1024, view.Stat("Assets/large")!.Length);
            Assert.Null(view.Stat("Assets/generated.meta"));
            Write(view, "Assets/generated.meta", "output-" + i);
            if (i == 0) { view.Create("Library", true, 16); Write(view, "Library/cache", "persistent"); }
            else Assert.Equal("persistent", Read(view, "Library/cache"));
            Assert.Single(view.List(""), e => e.Path == "Library");
            Assert.Equal("ViewPartitionBoundary", Assert.Throws<WorkspaceException>(() =>
                view.Rename(view.Stat("Assets/large")!.Id, "Library/leak", false)).Code);
        }
        using var resumed = new ValidationViewDeltaStore(Path.Combine(dir.Path, "source-0"), Identity(tree), tree, new(a, cache, CacheRoots));
        Assert.Equal("output-0", Read(resumed, "Assets/generated.meta"));
        Assert.Equal("persistent", Read(resumed, "Library/cache"));
        Assert.Equal("original", Encoding.UTF8.GetString(tree.Files["Assets/code.cs"]));
    }

    [Fact]
    public void ActiveWorkerAndCacheWritersAreRejectedAndCandidateCannotBeReplaced()
    {
        using var dir = new TestDirectory(); var tree = Tree(); string a = Export(dir.Path, "A", tree);
        using (var worker = new JournalDeltaStore(Path.Combine(dir.Path, "worker-A"), Identity(tree), tree))
            Assert.Throws<IOException>(() => JournalDeltaStore.ExportCandidate(Path.Combine(dir.Path, "worker-A"), Identity(tree), tree,
                Path.Combine(dir.Path, "busy"), "busy", CacheRoots));
        using var view = new ValidationViewDeltaStore(Path.Combine(dir.Path, "source"), Identity(tree), tree, new(a, Path.Combine(dir.Path, "cache"), CacheRoots));
        Assert.Throws<IOException>(() => new ValidationViewDeltaStore(Path.Combine(dir.Path, "other"), Identity(tree), tree,
            new(a, Path.Combine(dir.Path, "cache"), CacheRoots)));
        Assert.Throws<IOException>(() => File.WriteAllBytes(Path.Combine(a, "delta.journal"), [1]));
        Assert.Equal("CandidateAlreadyExists", Assert.Throws<WorkspaceException>(() => JournalDeltaStore.ExportCandidate(
            Path.Combine(dir.Path, "worker-A"), Identity(tree), tree, a, "duplicate", CacheRoots)).Code);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("incomplete")]
    [InlineData("changed")]
    public void BadCandidateIsPreservedAndNeverRepaired(string fault)
    {
        using var dir = new TestDirectory(); var tree = Tree(); string a = Export(dir.Path, "A", tree);
        string journal = Path.Combine(a, "delta.journal");
        if (fault == "missing") File.Delete(Path.Combine(a, "candidate.json"));
        else if (fault == "incomplete") { using var file = new FileStream(journal, FileMode.Append); file.WriteByte(1); }
        else
        {
            var receipt = JsonSerializer.Deserialize<ValidationCandidate>(File.ReadAllText(Path.Combine(a, "candidate.json")))!;
            File.WriteAllText(Path.Combine(a, "candidate.json"), JsonSerializer.Serialize(receipt with {
                Seal = receipt.Seal with { JournalHash = new string('0', 64) } }));
        }
        byte[] before = File.ReadAllBytes(journal);
        Assert.Throws<WorkspaceException>(() => new CandidateSnapshotTree(a, tree));
        Assert.Equal(before, File.ReadAllBytes(journal));
    }

    [Fact]
    public void WrongSourceGenerationAndMissingSnapshotCannotReuseValidation()
    {
        using var dir = new TestDirectory(); var tree = Tree(); string a = Export(dir.Path, "A", tree), b = Export(dir.Path, "B", tree);
        string source = Path.Combine(dir.Path, "source"), cache = Path.Combine(dir.Path, "cache");
        using (var view = new ValidationViewDeltaStore(source, Identity(tree), tree, new(a, cache, CacheRoots))) Write(view, "Assets/result", "keep");
        Assert.Equal("OverlayIdentityMismatch", Assert.Throws<WorkspaceException>(() =>
            new ValidationViewDeltaStore(source, Identity(tree), tree, new(b, cache, CacheRoots))).Code);
        tree.Available = false;
        Assert.Equal("SnapshotMissing", Assert.Throws<WorkspaceException>(() => new CandidateSnapshotTree(a, tree)).Code);
    }

    [Fact]
    public void CandidateSchemaAndPartitionChangesFailClosed()
    {
        using var dir = new TestDirectory(); var tree = Tree(); string a = Export(dir.Path, "A", tree);
        using var candidate = new CandidateSnapshotTree(a, tree);
        var contracts = new ContractCatalog();
        Assert.True(contracts.Validate("urn:splinter:v1:validation-candidate",
            JsonSerializer.SerializeToElement(candidate.Candidate, ApiJson.Options)).IsValid);
        Assert.False(contracts.Validate("urn:splinter:v1:validation-candidate",
            JsonSerializer.SerializeToElement(candidate.Candidate with { Seal = candidate.Candidate.Seal with { JournalHash = "bad" } }, ApiJson.Options)).IsValid);
        string source = Path.Combine(dir.Path, "source"), cache = Path.Combine(dir.Path, "cache");
        using (var view = new ValidationViewDeltaStore(source, Identity(tree), tree, new(a, cache, CacheRoots)))
        { view.Create("Library", true, 16); Write(view, "Library/cache", "keep"); }
        Assert.Equal("OverlayIdentityMismatch", Assert.Throws<WorkspaceException>(() =>
            new ValidationViewDeltaStore(source, Identity(tree), tree, new(a, cache, ["Library"]))).Code);
        Assert.Equal("UnsafePath", Assert.Throws<WorkspaceException>(() =>
            new ValidationViewDeltaStore(source, Identity(tree), tree, new(a, Path.Combine(a, "cache"), CacheRoots))).Code);
    }

    [Theory]
    [InlineData("intent-flushed")]
    [InlineData("intent")]
    [InlineData("staged")]
    [InlineData("published")]
    [InlineData("source-removed")]
    public void PrivateSparseRenameRecoversEveryTransferBoundaryWithoutBaseHydration(string boundary)
    {
        using var dir = new TestDirectory(); var tree = Tree(); string a = Export(dir.Path, "A", tree);
        string source = Path.Combine(dir.Path, "source"), cache = Path.Combine(dir.Path, "cache");
        Guid id;
        using (var view = new ValidationViewDeltaStore(source, Identity(tree), tree, new(a, cache, CacheRoots),
                   stage => { if (stage == boundary) throw new IOException("injected interruption"); }))
        {
            view.Create("Temp", true, 16);
            var file = view.Create("Temp/staged", false, 32); id = file.Id;
            view.SetLength(id, 1024L * 1024 * 1024);
            view.Write(id, 1024L * 1024 * 1024 - 4096, new byte[4096]);
            Assert.Throws<IOException>(() => view.Rename(id, "Assets/sparse-output", false));
            Assert.Equal("ViewTransferRecoveryRequired", Assert.Throws<WorkspaceException>(() => view.Stat("Assets/code.cs")).Code);
        }
        Assert.Equal("ViewTransferRecoveryRequired", Assert.Throws<WorkspaceException>(() =>
            new ValidationViewDeltaStore(Path.Combine(dir.Path, "wrong-generation"), Identity(tree), tree, new(a, cache, CacheRoots))).Code);
        using var resumed = new ValidationViewDeltaStore(source, Identity(tree), tree, new(a, cache, CacheRoots));
        Assert.Null(resumed.Stat("Temp/staged"));
        Assert.Equal(id, resumed.Stat("Assets/sparse-output")!.Id);
        Assert.Equal(1024L * 1024 * 1024, resumed.Stat("Assets/sparse-output")!.Length);
        Assert.True(resumed.Observe().JournalBytes < 40000);
        Assert.Equal(0, tree.ReadBytes);
        Assert.False(File.Exists(Path.Combine(cache, "partition-transfer.json")));
    }

    [Fact]
    public void PrivateRenamePreservesOpenIdentityAndReplaceDestination()
    {
        using var dir = new TestDirectory(); var tree = Tree(); string a = Export(dir.Path, "A", tree);
        using var view = new ValidationViewDeltaStore(Path.Combine(dir.Path, "source"), Identity(tree), tree,
            new(a, Path.Combine(dir.Path, "cache"), CacheRoots));
        view.Create("Temp", true, 16); Write(view, "Temp/private", "new content");
        Guid openId = view.Stat("Temp/private")!.Id;
        view.Rename(openId, "Assets/code.cs", true);
        Assert.Equal(openId, view.Stat("Assets/code.cs")!.Id);
        Assert.Equal("Assets/code.cs", view.Get(openId).Path);
        Assert.Equal("new content", Read(view, "Assets/code.cs"));
        Assert.Null(view.Stat("Temp/private"));
        view.Write(openId, 0, "NEW"u8);
        Assert.Equal("NEW content", Read(view, "Assets/code.cs"));
        view.Rename(openId, "Temp/private-again", false);
        Assert.Equal(openId, view.Stat("Temp/private-again")!.Id);
        Assert.Equal("NEW content", Read(view, "Temp/private-again"));
        Assert.Null(view.Stat("Assets/code.cs"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("intent")]
    [InlineData("staged")]
    [InlineData("published")]
    [InlineData("source-removed")]
    public void EmptyPrivateFileCanReturnToItsOriginalPartitionAndReconnect(string? interruption)
    {
        using var dir = new TestDirectory(); var tree = Tree(); string a = Export(dir.Path, "A", tree);
        string source = Path.Combine(dir.Path, "source"), cache = Path.Combine(dir.Path, "cache");
        Guid id; bool returning = false;
        using (var view = new ValidationViewDeltaStore(source, Identity(tree), tree, new(a, cache, CacheRoots),
                   stage => { if (returning && stage == interruption) throw new IOException("interrupted return"); }))
        {
            view.Create("Temp", true, 16); Write(view, "Temp/private", "old ranges");
            id = view.Stat("Temp/private")!.Id;
            view.Rename(id, "Assets/private", false);
            view.SetLength(id, 0); returning = true;
            if (interruption is null) view.Rename(id, "Temp/private-again", false);
            else Assert.Throws<IOException>(() => view.Rename(id, "Temp/private-again", false));
        }
        using var resumed = new ValidationViewDeltaStore(source, Identity(tree), tree, new(a, cache, CacheRoots));
        Assert.Null(resumed.Stat("Assets/private"));
        Assert.Equal(id, resumed.Stat("Temp/private-again")!.Id);
        Assert.Equal("", Read(resumed, "Temp/private-again"));
        // Extending the returned empty file must not resurrect its old payload.
        resumed.SetLength(id, 10);
        var bytes = new byte[10]; Assert.Equal(10, resumed.Read(id, 0, bytes));
        Assert.Equal(new byte[10], bytes);
        Assert.False(File.Exists(Path.Combine(cache, "partition-transfer.json")));
        Assert.Equal(0, tree.ReadBytes);
    }
}
