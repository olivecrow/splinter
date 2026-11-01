using System.Buffers.Binary;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Splinter.Core;
using Splinter.Platform.Windows;
using Splinter.Runtime;

namespace Splinter.Tests;

public sealed class JournalStoppedIndexPreservationTests
{
    [Fact]
    public void AuthorizedStoppedRecoveryPreservesEveryIndexByteAndNativeIdentityWithoutAdoptingOpaquePayload()
    {
        using var fixture = new Fixture(); fixture.Reclaim(); fixture.AssertPreserved(); fixture.Reclaim();
    }
    [Theory]
    [InlineData("stopped-index-preserve.intent")]
    [InlineData("stopped-index-preserve.inventory")]
    [InlineData("stopped-index-preserve.descendants-released")]
    [InlineData("stopped-index-preserve.moved")]
    [InlineData("stopped-index-preserve.complete")]
    public void ExactStoppedRequestRecoversEveryIntactPreservationBoundary(string point)
    {
        using var fixture = new Fixture(); bool hit = false;
        Assert.Throws<IOException>(() => fixture.Reclaim(boundary =>
        { if (!hit && boundary == point) { hit = true; throw new IOException("lost stopped preservation response"); } }));
        Assert.True(hit);
        if (point != "stopped-index-preserve.complete") Assert.Single(JournalDeltaStore.EnumeratePendingJournalIndexPreservations(fixture.Root.Path, fixture.Identity));
        fixture.Reclaim(); fixture.AssertPreserved(); Assert.Empty(JournalDeltaStore.EnumeratePendingJournalIndexPreservations(fixture.Root.Path, fixture.Identity));
    }
    [Fact]
    public void UnpublishedStoppedControlRemainsVisibleAndOpaqueWhileTheSameRequestCompletes()
    {
        using var fixture = new Fixture(); string? pending = null, native = null; byte[] prefix = "{\"version\":"u8.ToArray();
        Assert.Throws<IOException>(() => fixture.Reclaim(point =>
        {
            if (point != "stopped-index-preserve.intent") return;
            string root = Directory.EnumerateDirectories(Path.Combine(fixture.Root.Path, "journal-stopped-index-residuals")).Single();
            pending = Path.Combine(root, "prepared.json." + Guid.NewGuid().ToString("N") + ".pending");
            File.WriteAllBytes(pending, prefix); native = fixture.Pins.Identify(pending); throw new IOException("unpublished stopped control");
        }));
        Assert.Single(JournalDeltaStore.EnumeratePendingJournalIndexPreservations(fixture.Root.Path, fixture.Identity));
        fixture.Reclaim(); fixture.AssertPreserved();
        var proof = Assert.Single(JournalDeltaStore.EnumerateUnpublishedIndexResidualControls(fixture.Root.Path, fixture.Identity));
        Assert.Equal(pending, proof.Path); Assert.Equal(native, fixture.Pins.Identify(proof.Path)); Assert.Equal(prefix, File.ReadAllBytes(proof.Path));
    }
    [Fact]
    public void OrdinaryIndexReclamationCannotUseTheNarrowStoppedRecovery()
    {
        using var fixture = new Fixture();
        Assert.ThrowsAny<IOException>(() => JournalDeltaStore.ReclaimPagedIndices(fixture.Delta, fixture.Identity, fixture.Pins));
        Assert.True(File.Exists(fixture.Orphan)); Assert.Empty(JournalDeltaStore.EnumeratePreservedJournalIndices(fixture.Root.Path, fixture.Identity));
    }
    [Fact]
    public void ProtectedJsonRootIntoAnUnownedContainerIsRefusedBeforeTheDirectoryMoves()
    {
        using var fixture = new Fixture();
        File.WriteAllText(Path.Combine(fixture.Root.Path, "source-response.json"), JsonSerializer.Serialize(new { root = fixture.UnownedRoot }));
        Assert.NotNull(Record.Exception(() => fixture.Reclaim()));
        Assert.True(Directory.Exists(fixture.Index)); Assert.True(File.Exists(fixture.Orphan));
    }
    [Fact]
    public void EmbeddedAdministrativeFrameRootIntoAnUnownedContainerIsRefusedBeforeTheDirectoryMoves()
    {
        using var fixture = new Fixture(); fixture.AppendProtectedAdminRoot();
        Assert.NotNull(Record.Exception(() => fixture.Reclaim()));
        Assert.True(Directory.Exists(fixture.Index)); Assert.True(File.Exists(fixture.Orphan));
    }
    [Fact]
    public void AdmittedReaderBlocksTheWholeIndexMoveAndTheSameRequestResumesAfterItCloses()
    {
        using var fixture = new Fixture();
        using (var reader = new PagedMetadataStore(fixture.Index)) Assert.NotNull(Record.Exception(() => fixture.Reclaim()));
        fixture.Reclaim(); fixture.AssertPreserved();
    }
    [Fact]
    public void ReaderAdmittedDuringTheNativeHandoffGapPreventsCompletionWithoutDeletingAnyIndexFile()
    {
        using var fixture = new Fixture(); PagedMetadataStore? reader = null;
        try
        {
            Assert.NotNull(Record.Exception(() => fixture.Reclaim(point =>
            { if (point == "stopped-index-preserve.descendants-released") reader = new PagedMetadataStore(fixture.Index); })));
            Assert.NotNull(reader);
        }
        finally { reader?.Dispose(); }
        fixture.Reclaim(); fixture.AssertPreserved();
    }
    [Fact]
    public void EqualByteOpaqueReplacementAfterInventoryCannotBeAdoptedOrCompleted()
    {
        using var fixture = new Fixture(); string old = Path.Combine(fixture.Root.Path, "retained-original-container");
        var failure = Record.Exception(() => fixture.Reclaim(point =>
        {
            if (point != "stopped-index-preserve.inventory") return;
            File.Move(fixture.Orphan, old); File.Copy(old, fixture.Orphan);
        }));
        Assert.NotNull(failure); Assert.True(Directory.Exists(fixture.Index));
        Assert.Equal(fixture.OrphanIdentity, fixture.Pins.Identify(old)); Assert.False(File.Exists(fixture.Orphan + ".owner"));
    }
    [Fact]
    public void CorruptOpaquePagesRemainRefusedByTheNarrowRecovery()
    {
        using var fixture = new Fixture();
        using (var output = new FileStream(fixture.Orphan, FileMode.Open, FileAccess.Write, FileShare.Read))
        { output.Position = 90; output.WriteByte(173); output.Flush(true); }
        Assert.NotNull(Record.Exception(() => fixture.Reclaim())); Assert.True(Directory.Exists(fixture.Index));
    }
    [Fact]
    public void EqualByteReclamationReceiptReplacementCannotAuthorizeTheSamePreservationRequest()
    {
        using var fixture = new Fixture();
        Assert.Throws<IOException>(() => fixture.Reclaim(point => { if (point == "stopped-index-preserve.intent") throw new IOException("intent response lost"); }));
        string receipt = Path.Combine(fixture.Root.Path, "reclaim.bin"), original = Path.Combine(fixture.Root.Path, "exact-original-receipt");
        File.Move(receipt, original); File.Copy(original, receipt);
        Assert.NotNull(Record.Exception(() => fixture.Reclaim())); Assert.True(Directory.Exists(fixture.Index));
        File.Delete(receipt); File.Move(original, receipt); fixture.Reclaim(); fixture.AssertPreserved();
    }
    [Fact]
    public void MovedRetryRevalidatesEveryAuthorityControlBeforeCompletingThePreservation()
    {
        using var fixture = new Fixture(); string control = Path.Combine(fixture.Root.Path, "source-response.json");
        const string before = "{\"proof\":1}", changed = "{\"proof\":2}"; File.WriteAllText(control, before);
        Assert.Throws<IOException>(() => fixture.Reclaim(point =>
        {
            if (point != "stopped-index-preserve.moved") return;
            File.WriteAllText(control, changed); throw new IOException("moved response lost and unrelated authority changed");
        }));
        Assert.NotNull(Record.Exception(() => fixture.Reclaim()));
        Assert.Single(JournalDeltaStore.EnumeratePendingJournalIndexPreservations(fixture.Root.Path, fixture.Identity));
        File.WriteAllText(control, before); fixture.Reclaim(); fixture.AssertPreserved();
    }
    [Fact]
    public void AnUnknownNamespaceIsRefusedBeforeArbitraryPayloadJsonIsTreatedAsControlMetadata()
    {
        using var fixture = new Fixture(); string payload = Path.Combine(fixture.Root.Path, "project-backing"); Directory.CreateDirectory(payload);
        string file = Path.Combine(payload, "asset.json"); File.WriteAllText(file, "arbitrary asset bytes, not JSON control"); string native = fixture.Pins.Identify(file);
        var error = Assert.Throws<WorkspaceException>(() => fixture.Reclaim()); Assert.Equal("StoppedIndexPreservationUnsupported", error.Code);
        Assert.Equal(native, fixture.Pins.Identify(file)); Assert.True(Directory.Exists(fixture.Index));
    }
    [Fact]
    public async Task AControlDirectoryJunctionIsRefusedBeforeItsContentsAreEnumerated()
    {
        using var fixture = new Fixture(); using var target = new TestDirectory();
        string file = Path.Combine(target.Path, "state.json"), link = Path.Combine(fixture.Root.Path, "overlay"); File.WriteAllText(file, "external asset payload");
        string native = fixture.Pins.Identify(file);
        var start = new ProcessStartInfo("cmd.exe") { UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true, Arguments = $"/d /c mklink /J \"{link}\" \"{target.Path}\"" };
        using var process = Process.Start(start)!; await process.WaitForExitAsync(TestContext.Current.CancellationToken); Assert.Equal(0, process.ExitCode);
        try
        {
            var error = Assert.Throws<WorkspaceException>(() => fixture.Reclaim()); Assert.Equal("UnsafePath", error.Code);
            Assert.Equal(native, fixture.Pins.Identify(file)); Assert.True(Directory.Exists(fixture.Index));
        }
        finally { Directory.Delete(link); }
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ContentPathControlBeyondTheOldWholeFileBoundIsStreamedAndItsLastRootStillValidated(bool unowned)
    {
        using var fixture = new Fixture(); string path = Path.Combine(fixture.Root.Path, "source-query.json");
        using (var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
        {
            using (var json = new Utf8JsonWriter(output))
            {
                json.WriteStartObject(); json.WriteString("token", "owned-test-request"); json.WriteString("requestId", "root-scan-request");
                json.WriteStartArray("managedPaths"); json.WriteEndArray(); json.WriteStartArray("contentPaths");
                string prefix = string.Join('/', Enumerable.Repeat(new string('x', 100), 10));
                for (int index = 0; index < 4096; index++) json.WriteStringValue(prefix + "/file-" + index.ToString("D4"));
                json.WriteEndArray(); json.WritePropertyName("expectedGeneration");
                var empty = new MetadataTreeRoot(1, fixture.OwnedRoot.StoreId, null);
                var changes = new SourceChangeIndex(1, fixture.Index, empty, empty, [], 0, false, new string('a', 64), unowned ? fixture.UnownedRoot : fixture.OwnedRoot, empty);
                JsonSerializer.Serialize(json, new SourceChangeView(fixture.Identity.Id, fixture.Identity.Snapshot.Id, "frozen-test-epoch", 0, []) { Index = changes }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
                json.WriteEndObject(); json.Flush();
            }
            output.Flush(true);
        }
        Assert.True(new FileInfo(path).Length > 16L * 6 * 32767 + 65536);
        if (unowned) { Assert.NotNull(Record.Exception(() => fixture.Reclaim())); Assert.True(Directory.Exists(fixture.Index)); }
        else { fixture.Reclaim(); fixture.AssertPreserved(); }
    }
    [Theory]
    [InlineData("duplicate")]
    [InlineData("truncated")]
    [InlineData("overflow")]
    [InlineData("depth")]
    public void StreamingDiscoveryRejectsMalformedRecognizedRootsAndIncompleteOrDeepJson(string kind)
    {
        using var fixture = new Fixture(); string root = JsonSerializer.Serialize(fixture.OwnedRoot, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        string json = kind switch
        {
            "duplicate" => "{\"root\":{\"version\":1,\"storeId\":\"" + fixture.OwnedRoot.StoreId + "\",\"storeId\":\"" + new string('0', 24) + "\",\"page\":" + JsonSerializer.Serialize(fixture.OwnedRoot.Page, new JsonSerializerOptions(JsonSerializerDefaults.Web)) + "}}",
            "truncated" => "{\"root\":" + root + ",\"tail\":[",
            "overflow" => "{\"root\":{\"version\":1,\"storeId\":\"" + fixture.OwnedRoot.StoreId + "\",\"page\":{\"container\":9223372036854775808,\"offset\":48,\"bytes\":100,\"count\":1,\"contentHash\":\"" + new string('a', 64) + "\"}}}",
            _ => new string('[', 70) + root + new string(']', 70)
        };
        File.WriteAllText(Path.Combine(fixture.Root.Path, "source-response.json"), json);
        Assert.NotNull(Record.Exception(() => fixture.Reclaim())); Assert.True(Directory.Exists(fixture.Index));
    }
    private sealed record FileProof(string Name, long Bytes, string Identity, string Hash);
    private sealed class Fixture : IDisposable
    {
        public TestDirectory Root { get; } = new();
        public WindowsJournalFilePins Pins { get; } = new();
        public OverlayIdentity Identity { get; }
        public string Delta { get; }
        public string Index { get; }
        public string Orphan { get; }
        public string OrphanIdentity { get; }
        public MetadataTreeRoot UnownedRoot { get; }
        public MetadataTreeRoot OwnedRoot { get; }
        private readonly SnapshotDeltaSeal seal;
        private readonly DeltaEntry rootEntry;
        private readonly FileProof[] before;
        public Fixture()
        {
            if (!OperatingSystem.IsWindows()) Assert.Skip("Native stopped-index preservation requires Windows.");
            var basis = new SnapshotOverlayTests.FakeSnapshotTree(); Identity = new(3, "stopped-replay-orphans", basis.Snapshot, long.MaxValue, 65536);
            Delta = Path.Combine(Root.Path, "delta");
            using (var writer = new JournalDeltaStore(Delta, Identity, basis, sourceFilePins: Pins))
            { var file = writer.Create("kept", false, 32); writer.Write(file.Id, 0, new byte[] { 7 }); rootEntry = writer.Stat("")!; seal = writer.Seal(); }
            var state = JsonSerializer.Deserialize<JournalSegmentState>(File.ReadAllText(Path.Combine(Delta, "segment-state.json")), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
            Index = state.Roots.Directory;
            OwnedRoot = state.Roots.Nodes;
            string owned = Path.Combine(Index, "pages-0000000000000000.bin"); Orphan = Path.Combine(Index, "pages-0000000000000007.bin");
            File.Copy(owned, Orphan); OrphanIdentity = Pins.Identify(Orphan);
            UnownedRoot = state.Roots.Nodes with { Page = state.Roots.Nodes.Page! with { Container = 7 } };
            before = Files(Index);
        }
        private FileProof[] Files(string directory) => Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal).Select(path =>
        { using var input = File.OpenRead(path); return new FileProof(Path.GetRelativePath(directory, path), input.Length, Pins.IdentifyOpened(input), Convert.ToHexStringLower(SHA256.HashData(input))); }).ToArray();
        public void AppendProtectedAdminRoot()
        {
            byte[] metadata = JsonSerializer.SerializeToUtf8Bytes(new
            {
                Sequence = seal.DurableSequence, Operation = "capture-freeze", Entry = rootEntry,
                FrozenCapture = new { Id = new string('a', 24), Directory = Index, Control = UnownedRoot, Digest = new string('b', 64) }
            });
            byte[] header = new byte[16]; "SJR2"u8.CopyTo(header); BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(4), metadata.Length);
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256); hash.AppendData(header); hash.AppendData(metadata);
            using var output = new FileStream(Path.Combine(Delta, "delta.journal"), FileMode.Append, FileAccess.Write, FileShare.Read);
            output.Write(header); output.Write(metadata); output.Write(hash.GetHashAndReset()); output.Flush(true);
        }
        public void Reclaim(Action<string>? boundary = null) => SegmentedJournalReclamation.ReclaimDirectory(Delta, seal, ["identity.json"],
            Path.Combine(Root.Path, "reclaim.bin"), boundary, path => new WindowsJournalDirectoryGuard(path, CurrentJournalReclamation.GuardName), Pins);
        public void AssertPreserved()
        {
            Assert.False(Directory.Exists(Delta)); var proof = Assert.Single(JournalDeltaStore.EnumeratePreservedJournalIndices(Root.Path, Identity));
            Assert.Equal("stopped-source-index-retained-intact", proof.Reason); Assert.Equal(before, Files(proof.Directory));
            Assert.Equal(OrphanIdentity, Pins.Identify(Path.Combine(proof.Directory, Path.GetFileName(Orphan))));
            Assert.False(File.Exists(Path.Combine(proof.Directory, Path.GetFileName(Orphan)) + ".owner"));
            Assert.True(File.Exists(Path.Combine(proof.Directory, "identity.bin"))); Assert.True(proof.Bytes > 0);
            using var inventory = new PagedMetadataStore(proof.InventoryDirectory); Assert.Equal(before.LongLength, inventory.Open(proof.Entries).Root.Count);
        }
        public void Dispose() => Root.Dispose();
    }
}
