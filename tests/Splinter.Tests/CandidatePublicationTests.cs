using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Splinter.Core;
using Splinter.Protocol;
using Splinter.Runtime;

namespace Splinter.Tests;

public sealed class CandidatePublicationTests
{
    private static readonly string[] Forbidden = [".git", "Library", "Temp"];
    private const string Pointer = "gitdir: C:/owned/.git/worktrees/worker\n";
    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static void Write(JournalDeltaStore store, string path, string value)
    {
        var node = store.Stat(path) ?? store.Create(path, false, 32);
        store.SetLength(node.Id, 0); store.Write(node.Id, 0, Encoding.UTF8.GetBytes(value));
    }
    private sealed class Fixture : IDisposable
    {
        public TestDirectory Root { get; } = new();
        public SnapshotOverlayTests.FakeSnapshotTree Tree { get; } = new();
        public string Worker => Path.Combine(Root.Path, "worker");
        public string Candidate => Path.Combine(Root.Path, "candidate");
        public string Journal => Path.Combine(Worker, "delta.journal");
        public OverlayIdentity Identity => new(2, "worker", Tree.Snapshot, 10_000_000, 65536, [".git"]);
        public CandidateExportRequest Request => new(1, "candidate-a");
        public CandidateSourceProjection Projection => new(1, Identity.Id, Tree.Snapshot, new string('a', 64),
            [new(".git", Encoding.UTF8.GetByteCount(Pointer), Hash(Encoding.UTF8.GetBytes(Pointer)))]);
        public Fixture()
        {
            Tree.Files["Assets/code.cs"] = "original"u8.ToArray();
            Tree.Files["Assets/old.meta"] = "guid"u8.ToArray(); Tree.Files["Assets/remove"] = [1];
            using var worker = OpenWorker(); Write(worker, ".git", Pointer); Write(worker, "Assets/code.cs", "candidate");
            worker.Rename(worker.Stat("Assets/old.meta")!.Id, "Assets/new.meta", false);
            worker.Delete(worker.Stat("Assets/remove")!.Id);
            var sparse = worker.Create("Assets/sparse", false, 32);
            worker.SetLength(sparse.Id, 1024L * 1024 * 1024);
            worker.Write(sparse.Id, 65536, Enumerable.Range(0, 50000).Select(n => (byte)(n % 251)).ToArray());
            worker.Write(sparse.Id, 65536 + 10000, new byte[12000]);
        }
        public JournalDeltaStore OpenWorker() => new(Worker, Identity, Tree);
        public ImmutableCandidate Publish(Action<string>? boundary = null) => JournalDeltaStore.PublishCandidate(
            Worker, Identity, Tree, Candidate, Request, Forbidden, Projection, boundary);
        public void StopAt(string boundary) => Assert.Throws<IOException>(() => Publish(stage =>
        { if (stage == boundary) throw new IOException("injected publication interruption"); }));
        public byte[] ReferenceJournal()
        {
            string reference = Path.Combine(Root.Path, "reference");
            JournalDeltaStore.ExportSourceCandidate(Worker, Identity, Tree, reference, "reference", Forbidden, Projection);
            return File.ReadAllBytes(Path.Combine(reference, "delta.journal"));
        }
        public void Dispose() => Root.Dispose();
    }
    private static SortedDictionary<string, string> Files(string path) => new(Directory.EnumerateFiles(path)
        .ToDictionary(file => Path.GetFileName(file), file => Hash(File.ReadAllBytes(file))), StringComparer.Ordinal);
    private static void Unchanged(string directory, SortedDictionary<string, string> before) => Assert.Equal(before, Files(directory));
    private static void ChangeJson(string path, Action<JsonObject> change)
    {
        var node = JsonNode.Parse(File.ReadAllText(path))!.AsObject(); change(node); File.WriteAllText(path, node.ToJsonString());
    }

    [Theory]
    [InlineData("intent-flushed")]
    [InlineData("intent-published")]
    [InlineData("identity-flushed")]
    [InlineData("identity-published")]
    [InlineData("journal-flushed")]
    [InlineData("journal-published")]
    [InlineData("candidate-flushed")]
    [InlineData("receipt-flushed")]
    [InlineData("receipt-published")]
    public void EveryDurableBoundaryReconcilesTheSameCandidateAndStableReceipt(string boundary)
    {
        using var f = new Fixture(); byte[] worker = File.ReadAllBytes(f.Journal);
        SnapshotDeltaSeal sourceSeal; using (var source = f.OpenWorker()) sourceSeal = source.Seal();
        f.StopAt(boundary);
        if (boundary != "receipt-published")
            Assert.Equal("CandidateUnpublished", Assert.Throws<WorkspaceException>(() => new CandidateSnapshotTree(f.Candidate, f.Tree)).Code);
        var result = f.Publish(); var files = Files(f.Candidate);
        Assert.Equal(JsonSerializer.Serialize(result), JsonSerializer.Serialize(f.Publish()));
        Unchanged(f.Candidate, files); Assert.Equal(worker, File.ReadAllBytes(f.Journal));
        Assert.Equal(f.Request.RequestKey, result.Id); Assert.Equal(sourceSeal, result.SourceSeal);
        Assert.Equal(sourceSeal, JournalDeltaStore.SealJournal(f.Journal, sourceSeal.DurableSequence));
        Assert.Equal(result.SourceSeal.DurableSequence, result.Seal.DurableSequence);
        Assert.False(Directory.EnumerateFiles(f.Candidate, "*.new").Any());
        using var candidate = new CandidateSnapshotTree(f.Candidate, f.Tree);
        Assert.Null(candidate.Stat(".git")); Assert.Null(candidate.Stat("Assets/remove")); Assert.Null(candidate.Stat("Assets/old.meta"));
        Assert.NotNull(candidate.Stat("Assets/new.meta")); Assert.Equal(1024L * 1024 * 1024, candidate.Stat("Assets/sparse")!.Length);
        byte[] bytes = new byte[9]; candidate.Read("Assets/code.cs", 0, bytes); Assert.Equal("candidate"u8.ToArray(), bytes);
        Assert.Equal(0, f.Tree.ReadBytes);
        var contracts = new ContractCatalog();
        var intent = JsonSerializer.Deserialize<CandidateExportIntent>(File.ReadAllText(Path.Combine(f.Candidate, "candidate-export.json")))!;
        Assert.True(contracts.Validate("urn:splinter:v1:candidate-export-intent", JsonSerializer.SerializeToElement(intent, ApiJson.Options)).IsValid);
        Assert.True(contracts.Validate("urn:splinter:v1:immutable-candidate", JsonSerializer.SerializeToElement(result, ApiJson.Options)).IsValid);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(129)]
    [InlineData(-2)]
    [InlineData(-1)]
    public void ExactPartialHeaderMetadataPayloadOrChecksumIsVerifiedAndOnlyExtended(int cut)
    {
        using var f = new Fixture(); byte[] expected = f.ReferenceJournal(); f.StopAt("identity-published");
        int count = cut == -1 ? expected.Length - 1 : cut == -2 ? expected.Length / 2 : cut;
        File.WriteAllBytes(Path.Combine(f.Candidate, "delta.journal.new"), expected[..count]);
        byte[] worker = File.ReadAllBytes(f.Journal);
        f.Publish(); Assert.Equal(expected, File.ReadAllBytes(Path.Combine(f.Candidate, "delta.journal")));
        Assert.Equal(worker, File.ReadAllBytes(f.Journal)); Assert.Equal(0, f.Tree.ReadBytes);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(129)]
    [InlineData(-1)]
    public void AChangedPrefixCannotBeOverwrittenTruncatedOrExtended(int cut)
    {
        using var f = new Fixture(); byte[] expected = f.ReferenceJournal(); f.StopAt("identity-published");
        byte[] partial = expected[..(cut == -1 ? expected.Length / 2 : cut)]; partial[^1] ^= 1;
        File.WriteAllBytes(Path.Combine(f.Candidate, "delta.journal.new"), partial); var before = Files(f.Candidate);
        Assert.Equal("CandidatePartialMismatch", Assert.Throws<WorkspaceException>(() => f.Publish()).Code);
        Unchanged(f.Candidate, before); Assert.Equal(0, f.Tree.ReadBytes);
    }

    [Theory]
    [InlineData("journal-flushed")]
    [InlineData("journal-published")]
    [InlineData("candidate-flushed")]
    [InlineData("receipt-flushed")]
    [InlineData("receipt-published")]
    public void FullyWrittenCandidateRecoversAfterWorkerEditsEvenWhileWorkerIsBusy(string boundary)
    {
        using var f = new Fixture(); f.StopAt(boundary);
        using (var worker = f.OpenWorker()) Write(worker, "Assets/code.cs", "follow-up");
        SnapshotDeltaSeal later; using (var worker = f.OpenWorker()) later = worker.Seal();
        ImmutableCandidate result;
        using (var worker = f.OpenWorker()) result = f.Publish();
        Assert.NotEqual(later, result.SourceSeal);
        Assert.Equal(later, JournalDeltaStore.SealJournal(f.Journal, later.DurableSequence));
        using var candidate = new CandidateSnapshotTree(f.Candidate, f.Tree);
        byte[] bytes = new byte[9]; candidate.Read("Assets/code.cs", 0, bytes); Assert.Equal("candidate"u8.ToArray(), bytes);
        Assert.Equal(0, f.Tree.ReadBytes);
    }

    [Theory]
    [InlineData("intent-published")]
    [InlineData("identity-published")]
    public void IncompletePublicationCannotSelectALaterWorkerGeneration(string boundary)
    {
        using var f = new Fixture(); f.StopAt(boundary);
        using (var worker = f.OpenWorker()) Write(worker, "Assets/code.cs", "follow-up");
        var before = Files(f.Candidate); byte[] later = File.ReadAllBytes(f.Journal);
        Assert.Equal("CandidateSourceChanged", Assert.Throws<WorkspaceException>(() => f.Publish()).Code);
        Unchanged(f.Candidate, before); Assert.Equal(later, File.ReadAllBytes(f.Journal));
    }

    [Theory]
    [InlineData("request")]
    [InlineData("owner")]
    [InlineData("budget")]
    [InlineData("forbidden")]
    [InlineData("projection")]
    [InlineData("source")]
    [InlineData("destination")]
    public void AnotherRequestOrPublicationIdentityCannotAdoptACompleteCandidate(string fault)
    {
        using var f = new Fixture(); f.Publish(); var before = Files(f.Candidate);
        var request = f.Request; var identity = f.Identity; var projection = f.Projection;
        string source = f.Worker, destination = f.Candidate; string[] forbidden = Forbidden;
        switch (fault)
        {
            case "request": request = request with { RequestKey = "other" }; break;
            case "owner": identity = identity with { Id = "other" }; projection = projection with { OwnerId = "other" }; break;
            case "budget": identity = identity with { MaxJournalBytes = identity.MaxJournalBytes + 1 }; break;
            case "forbidden": forbidden = [".git", "Library"]; break;
            case "projection": projection = projection with { EvidenceHash = new string('b', 64) }; break;
            case "source": source = Path.Combine(f.Root.Path, "other-worker"); break;
            case "destination":
                destination = Path.Combine(f.Root.Path, "other-candidate"); Directory.CreateDirectory(destination);
                foreach (string path in Directory.EnumerateFiles(f.Candidate)) File.Copy(path, Path.Combine(destination, Path.GetFileName(path)));
                break;
        }
        Assert.Equal("CandidatePublicationMismatch", Assert.Throws<WorkspaceException>(() => JournalDeltaStore.PublishCandidate(
            source, identity, f.Tree, destination, request, forbidden, projection)).Code);
        Unchanged(f.Candidate, before); if (fault == "destination") Unchanged(destination, before);
    }

    [Theory]
    [InlineData("intent-json")]
    [InlineData("intent-version")]
    [InlineData("intent-field-missing")]
    [InlineData("intent-field-unknown")]
    [InlineData("intent-field-duplicate")]
    [InlineData("identity-json")]
    [InlineData("identity-field-unknown")]
    [InlineData("identity-field-duplicate")]
    [InlineData("identity-missing")]
    [InlineData("journal-changed")]
    [InlineData("receipt-json")]
    [InlineData("receipt-seal")]
    [InlineData("receipt-source")]
    [InlineData("unknown-file")]
    [InlineData("competing-pending")]
    [InlineData("intent-missing")]
    public void DamagedOrAmbiguousStateIsPreservedByteForByte(string fault)
    {
        using var f = new Fixture(); f.StopAt("receipt-flushed");
        string intent = Path.Combine(f.Candidate, "candidate-export.json"), receipt = Path.Combine(f.Candidate, "candidate.json.new");
        string identity = Path.Combine(f.Candidate, "identity.json"), journal = Path.Combine(f.Candidate, "delta.journal");
        switch (fault)
        {
            case "intent-json": File.WriteAllText(intent, "{"); break;
            case "intent-version": ChangeJson(intent, node => node["Version"] = 0); break;
            case "intent-field-missing": ChangeJson(intent, node => node.Remove("SourceProjection")); break;
            case "intent-field-unknown": ChangeJson(intent, node => node["Unexpected"] = true); break;
            case "intent-field-duplicate": File.WriteAllText(intent, File.ReadAllText(intent).Replace("\"Version\":1", "\"Version\":1,\"Version\":1", StringComparison.Ordinal)); break;
            case "identity-json": File.WriteAllText(identity, "{"); break;
            case "identity-field-unknown": ChangeJson(identity, node => node["Unexpected"] = true); break;
            case "identity-field-duplicate": File.WriteAllText(identity, File.ReadAllText(identity).Replace("\"Version\":2", "\"Version\":2,\"Version\":2", StringComparison.Ordinal)); break;
            case "identity-missing": File.Delete(identity); break;
            case "journal-changed": using (var file = new FileStream(journal, FileMode.Append)) file.WriteByte(1); break;
            case "receipt-json": File.WriteAllText(receipt, "{"); break;
            case "receipt-seal": ChangeJson(receipt, node => node["Seal"]!["JournalHash"] = new string('0', 64)); break;
            case "receipt-source": ChangeJson(receipt, node => node["SourceSeal"]!["JournalHash"] = new string('0', 64)); break;
            case "unknown-file": File.WriteAllText(Path.Combine(f.Candidate, "other-work.txt"), "preserve"); break;
            case "competing-pending": File.Copy(intent, intent + ".new"); break;
            case "intent-missing": File.Delete(intent); break;
        }
        var before = Files(f.Candidate); byte[] source = File.ReadAllBytes(f.Journal);
        Assert.Throws<WorkspaceException>(() => f.Publish()); Unchanged(f.Candidate, before);
        Assert.Equal(source, File.ReadAllBytes(f.Journal));
    }

    [Fact]
    public void APublicationLockRejectsAnotherPublisherAndReleasesAfterInterruption()
    {
        using var f = new Fixture(); int observations = 0;
        f.Publish(stage =>
        {
            if (stage != "intent-published") return;
            var before = Files(f.Candidate);
            Assert.Equal("CandidatePublicationBusy", Assert.Throws<WorkspaceException>(() => f.Publish()).Code);
            Unchanged(f.Candidate, before); observations++;
        });
        Assert.Equal(1, observations); f.Publish();
        Assert.Equal("safe-to-retry", WorkspaceFailurePolicy.RetryDisposition("CandidatePublicationBusy"));
        Assert.True(WorkspaceFailurePolicy.RequiresPreservation("CandidatePartialMismatch"));
    }

    [Fact]
    public void ActiveSourceWriterAndAnUnownedDirectoryAreNeverAdopted()
    {
        using var f = new Fixture();
        using (var writer = f.OpenWorker()) Assert.Throws<IOException>(() => f.Publish());
        Assert.False(Directory.Exists(f.Candidate)); Directory.CreateDirectory(f.Candidate);
        File.WriteAllText(Path.Combine(f.Candidate, "candidate-export.json.new"), "{"); var before = Files(f.Candidate);
        Assert.Equal("CandidatePublicationCorrupt", Assert.Throws<WorkspaceException>(() => f.Publish()).Code);
        Unchanged(f.Candidate, before);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PlainSourceAndAnEmptyJournalUseTheSameDurableContract(bool optimized)
    {
        using var root = new TestDirectory(); var tree = new SnapshotOverlayTests.FakeSnapshotTree();
        var identity = new OverlayIdentity(2, "empty", tree.Snapshot, 65536, 4096, []);
        string source = Path.Combine(root.Path, "source"), candidate = Path.Combine(root.Path, "candidate");
        using (var writer = new JournalDeltaStore(source, identity, tree, optimizeJournal: optimized)) Assert.Equal(optimized, writer.JournalOptimizationEnabled);
        var request = new CandidateExportRequest(1, "empty");
        var first = JournalDeltaStore.PublishCandidate(source, identity, tree, candidate, request, []);
        Assert.Equal(0, first.Seal.DurableSequence); Assert.Null(first.SourceProjection);
        using (var read = new CandidateSnapshotTree(candidate, tree))
        { Assert.NotNull(read.Stat("")); Assert.Equal(tree.List("").Select(e => e.Path), read.List("").Select(e => e.Path)); }
        Assert.Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(JournalDeltaStore.PublishCandidate(source, identity, tree, candidate, request, [])));
        var contracts = new ContractCatalog();
        Assert.True(contracts.Validate("urn:splinter:v1:candidate-export-request", JsonSerializer.SerializeToElement(request, ApiJson.Options)).IsValid);
        Assert.False(contracts.Validate("urn:splinter:v1:candidate-export-request", JsonSerializer.SerializeToElement(request with { Version = 0 }, ApiJson.Options)).IsValid);
    }

    [Theory]
    [InlineData(0, "candidate-a")]
    [InlineData(1, "")]
    [InlineData(1, "../escape")]
    [InlineData(1, "request with spaces")]
    public void InvalidRequestsCannotCreatePublicationArtifacts(int version, string key)
    {
        using var directory = new TestDirectory(); var tree = new SnapshotOverlayTests.FakeSnapshotTree();
        var identity = new OverlayIdentity(2, "worker", tree.Snapshot, 10_000_000, 65536);
        string worker = Path.Combine(directory.Path, "worker"), candidate = Path.Combine(directory.Path, "candidate");
        Assert.Equal("CandidateRequestInvalid", Assert.Throws<WorkspaceException>(() => JournalDeltaStore.PublishCandidate(
            worker, identity, tree, candidate, new(version, key), Forbidden)).Code);
        Assert.False(Directory.Exists(worker)); Assert.False(Directory.Exists(candidate));
        Assert.False(File.Exists(candidate + ".publication.lock"));
    }

    [Theory]
    [InlineData("snapshot")]
    [InlineData("missing-worker")]
    [InlineData("partial-identity")]
    [InlineData("oversized-journal")]
    [InlineData("corrupt-full-journal")]
    public void IncompleteOrUnavailableDependenciesCannotBeAssumedComplete(string fault)
    {
        using var f = new Fixture(); byte[] expected = f.ReferenceJournal(); f.StopAt("identity-published");
        string temporary = Path.Combine(f.Candidate, "delta.journal.new");
        switch (fault)
        {
            case "snapshot": f.Tree.Available = false; break;
            case "missing-worker":
                // Test-only relocation preserves all source bytes.
                Directory.Move(f.Worker, Path.Combine(f.Root.Path, "preserved-worker")); break;
            case "partial-identity": File.WriteAllText(Path.Combine(f.Candidate, "identity.json"), "{"); break;
            case "oversized-journal": File.WriteAllBytes(temporary, [.. expected, 1]); break;
            case "corrupt-full-journal": expected[^1] ^= 1; File.WriteAllBytes(temporary, expected); break;
        }
        var before = Files(f.Candidate);
        if (fault == "missing-worker") Assert.Throws<DirectoryNotFoundException>(() => f.Publish());
        else Assert.Throws<WorkspaceException>(() => f.Publish());
        Unchanged(f.Candidate, before);
        Assert.False(File.Exists(Path.Combine(f.Candidate, "candidate.json")));
    }
}
