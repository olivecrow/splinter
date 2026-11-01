using System.Security.Cryptography;
using System.Text.Json;
using Splinter.Protocol;
using Splinter.Core;
using Splinter.Platform.Windows;
using Splinter.Runtime;
using Splinter.SourceControl.Files;

namespace Splinter.Tests;

public sealed class ReferenceCaptureTests
{
    [Fact(Timeout = 10000)]
    public async Task UntouchedPayloadIsNotCopiedAndRepeatedResultsSurviveBasisLoss()
    {
        using var temp = new TestDirectory(); var ct = TestContext.Current.CancellationToken; var pins = new WindowsJournalFilePins();
        string home = Path.Combine(temp.Path, "home"), project = Path.Combine(temp.Path, "project"); Directory.CreateDirectory(project);
        var basis = new SnapshotOverlayTests.FakeSnapshotTree(); basis.Files["touched"] = "abcdefgh"u8.ToArray(); basis.Files["untouched"] = new byte[65536];
        string id = new('a', 24); var store = new WorkspaceStore(home, pins); string control = store.ControlPath(id);
        var w = new WorkspaceRecord { Id = id, RequestKey = "reference", RequestDigest = "reference-request", ProjectPath = project, Path = store.WorkPath(id),
            SourceReference = "source", BaseReference = "basis", SourceKind = "none", ApplicationKind = "none", StorageMode = "project-snapshot", GeneratedDirectoryId = new string('d', 24),
            Snapshot = new() { Reference = basis.Snapshot } };
        using (var metadata = new TemporaryMetadataDirectory())
        {
            var catalog = PagedSnapshotCatalog.Build(metadata.Path, new("", true, 0, 16, 0, 0, 0), basis.Files.Select(file =>
                new DirectoryMetadataRecord(new(file.Key, false, file.Value.Length, 32, 0, 0, 1), Hash(file.Value))), ct: ct);
            w.BaseRevision = "sha256:" + catalog.Children.ContentHash;
        }
        var identity = new OverlayIdentity(3, id, basis.Snapshot); string delta = Path.Combine(control, "delta");
        using var journal = new JournalDeltaStore(delta, identity, basis, sourceFilePins: pins);
        var provider = new FileWorkspaceProvider(store, contentPins: pins);
        async Task<WorkspaceResultGeneration> Capture(char key, int sequence)
        {
            string captureId = new(key, 24), directory = Path.Combine(control, "captures", captureId);
            w.CaptureIntent = new(captureId, sequence, "request-" + key, "result"); var capture = journal.CaptureSource(directory, captureId, []);
            using var tree = new SourceCaptureTree(directory, basis, pins);
            var result = await provider.PreserveGenerationAsync(w, control, w.CaptureIntent, new(captureId, capture.Sequence, capture.Digest), tree, ct);
            w.Results.Add(result); w.ResultRevision = result.Revision;
            tree.Dispose(); SourceCaptureTree.Complete(directory, pins); journal.CompleteSourceCaptureRequest(captureId); return result;
        }
        journal.Write(journal.Stat("touched")!.Id, 2, "ZZ"u8); var first = await Capture('b', 1);
        string unchangedHash = Hash(basis.Files["untouched"]);
        Assert.False(File.Exists(Path.Combine(control, "files", "objects", unchangedHash)));
        Assert.True(w.ResultStorage!.UniquePayloadBytes < 65536);
        journal.Write(journal.Stat("touched")!.Id, 2, "cd"u8); var reverted = await Capture('c', 2);
        w.CaptureIntent = null; basis.Available = false;
        await provider.VerifyGenerationAsync(w, control, first, ct); await provider.VerifyGenerationAsync(w, control, reverted, ct);
        var manifest = FileWorkspaceProvider.Read<FileSourceManifest>(Path.Combine(control, "files", "generations", reverted.Id, "result.json"));
        Assert.Equal(4, manifest.Version); Assert.Equal(3, manifest.Catalog!.Count); Assert.NotNull(manifest.RetainedPayloads);
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var contract = new ContractCatalog();
        Assert.True(contract.Validate("urn:splinter:v1:file-source-manifest", JsonSerializer.SerializeToElement(manifest with { RequestDigest = new string('d', 24) }, json)).IsValid);
        string header = Path.Combine(control, "files", "generations", reverted.Id, "result.json"), original = File.ReadAllText(header);
        try
        {
            File.WriteAllText(header, JsonSerializer.Serialize(manifest with { Version = 3 }, json));
            await Assert.ThrowsAsync<WorkspaceException>(() => provider.VerifyGenerationAsync(w, control, reverted, ct));
            File.WriteAllText(header, JsonSerializer.Serialize(manifest with { RetainedPayloads = null }, json));
            var failure = await Assert.ThrowsAsync<WorkspaceException>(() => provider.VerifyGenerationAsync(w, control, reverted, ct));
            Assert.Contains("receipt", failure.Message);
        }
        finally { File.WriteAllText(header, original); }
        using var raw = new OwnedRangeContentStore(home, pins);
        using var input = new ResultFileContentStore(home, control, w, pins, raw, manifest.ContentFiles).Open(Hash("abcdefgh"u8.ToArray()), 8, ct);
        Assert.Equal("abcdefgh", new StreamReader(input).ReadToEnd());
    }

    [Fact(Timeout = 10000)]
    public void FailedDescriptorGroupKeepsPublishedPrefixAndRetriesExactContent()
    {
        using var temp = new TestDirectory(); var pins = new WindowsJournalFilePins(); var ct = TestContext.Current.CancellationToken;
        string home = Path.Combine(temp.Path, "home"), id = new('a', 24), control = Path.Combine(home, "records", id);
        var w = new WorkspaceRecord { Id = id, SourceReference = "source", BaseReference = "basis", RequestKey = "batch", RequestDigest = "batch-request", Path = Path.Combine(home, "workspaces", id), ProjectPath = temp.Path };
        Directory.CreateDirectory(control); using var raw = new OwnedRangeContentStore(home, pins);
        var contents = new ResultFileContentStore(home, control, w, pins, raw); var whole = new OwnedContentStore(home, pins);
        string Add(byte value) { byte[] bytes = [value]; string hash = Hash(bytes); whole.Preserve(w, control, hash, 1, output => output.Write(bytes), ct); return hash; }
        string first = Add(1), second = Add(2);
        contents.Preserve(first, 1, null, 0, 0, [new(0, 1, first, false)], ct); var prior = contents.CurrentRoot;
        using (var batch = contents.BeginPreservation())
        {
            batch.Preserve(second, 1, null, 0, 0, [new(0, 1, second, false)], ct);
            Assert.Throws<WorkspaceException>(() => batch.Preserve(Hash([3]), 1, null, 0, 0, [new(0, 1, second, false)], ct));
        }
        Assert.Equal(prior, contents.CurrentRoot); Assert.False(contents.Contains(second, 1));
        using (var batch = contents.BeginPreservation()) { batch.Preserve(second, 1, null, 0, 0, [new(0, 1, second, false)], ct); batch.Complete(); }
        Assert.True(contents.Contains(first, 1)); Assert.True(contents.Contains(second, 1));
        using var read = contents.Open(second, 1, ct); Assert.Equal(2, read.ReadByte());
    }

    [Fact(Timeout = 10000)]
    public void FrozenReadFetchesOnlyBasisGaps()
    {
        using var temp = new TestDirectory(); var basis = new SnapshotOverlayTests.FakeSnapshotTree(); var pins = new WindowsJournalFilePins();
        basis.Files["file"] = "abcdefgh"u8.ToArray(); var identity = new OverlayIdentity(3, "read-gaps", basis.Snapshot);
        using var journal = new JournalDeltaStore(Path.Combine(temp.Path, "delta"), identity, basis, sourceFilePins: pins);
        journal.Write(journal.Stat("file")!.Id, 2, "ZZYY"u8); string directory = Path.Combine(temp.Path, "captures", new string('b', 24));
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        journal.CaptureSource(directory, new string('b', 24), []);
        using (var tree = new SourceCaptureTree(directory, basis, pins))
        {
            long before = basis.ReadBytes; byte[] covered = new byte[4]; Assert.Equal(4, tree.Read("file", 2, covered));
            Assert.Equal("ZZYY"u8.ToArray(), covered); Assert.Equal(before, basis.ReadBytes);
            byte[] all = new byte[8]; Assert.Equal(8, tree.Read("file", 0, all)); Assert.Equal("abZZYYgh"u8.ToArray(), all);
            Assert.Equal(before + 4, basis.ReadBytes);
        }
        SourceCaptureTree.Complete(directory, pins);
    }
    [Fact(Timeout = 10000)]
    public void FailureAfterFirstDescriptorGroupKeepsIts64PublishedEntries()
    {
        using var temp = new TestDirectory(); var pins = new WindowsJournalFilePins(); var ct = TestContext.Current.CancellationToken;
        string home = Path.Combine(temp.Path, "home"), id = new('a', 24), control = Path.Combine(home, "records", id);
        var w = new WorkspaceRecord { Id = id, SourceReference = "source", BaseReference = "basis", RequestKey = "prefix", RequestDigest = "prefix-request",
            Path = Path.Combine(home, "workspaces", id), ProjectPath = temp.Path };
        Directory.CreateDirectory(control); using var raw = new OwnedRangeContentStore(home, pins);
        var contents = new ResultFileContentStore(home, control, w, pins, raw);
        using (var batch = contents.BeginPreservation())
        {
            for (int length = 1; length <= 65; length++) batch.Preserve(Hash(new byte[length]), length, null, 0, 0, [], ct);
            Assert.Throws<WorkspaceException>(() => batch.Preserve(Hash([1]), 1, null, 0, 0, [], ct));
        }
        Assert.Equal(64, contents.CurrentRoot!.Count); Assert.False(contents.Contains(Hash(new byte[65]), 65));
        using (var retry = contents.BeginPreservation())
        { retry.Preserve(Hash(new byte[65]), 65, null, 0, 0, [], ct); retry.Complete(); }
        Assert.Equal(65, contents.CurrentRoot!.Count);
        using var input = contents.Open(Hash(new byte[65]), 65, ct); Assert.Equal(Hash(new byte[65]), Convert.ToHexStringLower(SHA256.HashData(input)));
    }
    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}
