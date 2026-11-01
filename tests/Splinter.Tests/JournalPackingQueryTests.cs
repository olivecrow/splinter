using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Splinter.Core;
using Splinter.Platform.Windows;
using Splinter.Protocol;
using Splinter.Runtime;

namespace Splinter.Tests;

[CollectionDefinition(JournalPackingQueryIsolation.Name, DisableParallelization = true)]
public sealed class JournalPackingQueryIsolation
{
    public const string Name = "Journal packing query scratch isolation";
}

[Collection(JournalPackingQueryIsolation.Name)]
public sealed class JournalPackingQueryTests
{
    [Fact]
    public void RealCreationGapReceiptsHaveStableRootBoundPagesAcrossLongPathsWithoutScratchResidue()
    {
        RequireWindows(); using var fixture = new Fixture(longPath: true);
        var expected = fixture.Proofs(); Assert.Equal(2, expected.Length);
        Assert.All(expected, proof =>
        {
            Assert.True(proof.Path.Length > 6000); Assert.True(File.Exists(proof.Path));
            Assert.True(JsonSerializer.SerializeToUtf8Bytes(new { proof.Path, proof.Request, proof.Reason }, ApiJson.Options).Length > 32768,
                "The valid filesystem path must exercise more than one query-value chunk.");
        });
        using var unrelated = new TemporaryMetadataDirectory(); string marker = File.ReadAllText(Path.Combine(unrelated.Path, "scratch.owner"));
        var before = ScratchDirectories();
        var first = Read(fixture.Workspace, fixture.Control, pageSize: 1);
        Assert.Equal(2, first.GetProperty("count").GetInt64());
        string root = first.GetProperty("root").GetString()!, cursor = first.GetProperty("nextCursor").GetString()!;
        Assert.NotEmpty(cursor); var firstRow = Assert.Single(first.GetProperty("entries").EnumerateArray());
        var repeated = Read(fixture.Workspace, fixture.Control, pageSize: 1);
        Assert.Equal(root, repeated.GetProperty("root").GetString()); Assert.Equal(cursor, repeated.GetProperty("nextCursor").GetString());
        Assert.Equal(firstRow.GetRawText(), Assert.Single(repeated.GetProperty("entries").EnumerateArray()).GetRawText());
        var second = Read(fixture.Workspace, fixture.Control, cursor, 1);
        Assert.Equal(root, second.GetProperty("root").GetString());
        Assert.True(!second.TryGetProperty("nextCursor", out var nextCursor) || nextCursor.ValueKind == JsonValueKind.Null);
        var secondRow = Assert.Single(second.GetProperty("entries").EnumerateArray());
        Assert.NotEqual(firstRow.GetProperty("path").GetString(), secondRow.GetProperty("path").GetString());
        Assert.Equal(expected.Select(proof => proof.Path).Order(StringComparer.Ordinal), new[]
            { firstRow.GetProperty("path").GetString()!, secondRow.GetProperty("path").GetString()! }.Order(StringComparer.Ordinal));
        foreach (var row in new[] { firstRow, secondRow })
        {
            var proof = Assert.Single(expected, value => value.Path == row.GetProperty("path").GetString());
            Assert.Equal(proof.Request, row.GetProperty("request").GetString()); Assert.Equal(proof.Reason, row.GetProperty("reason").GetString());
        }
        string decoded = Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
        string stale = Convert.ToBase64String(Encoding.UTF8.GetBytes(decoded.Replace(root, "sha256:" + new string('0', 64), StringComparison.Ordinal)));
        Assert.Equal("CursorInvalid", Assert.Throws<WorkspaceException>(() => Read(fixture.Workspace, fixture.Control, stale, 1)).Code);
        Assert.Empty(ScratchDirectories().Except(before, StringComparer.Ordinal));
        Assert.True(Directory.Exists(unrelated.Path)); Assert.Equal(marker, File.ReadAllText(Path.Combine(unrelated.Path, "scratch.owner")));
        fixture.AssertOwnedAndPreserved();
    }

    [Fact]
    public void ForeignOwnerAndTamperedReceiptAreRejectedWithoutRepairOrScratchResidue()
    {
        RequireWindows(); using var fixture = new Fixture(longPath: false); var before = ScratchDirectories();
        string owner = fixture.Workspace.Id; fixture.Workspace.Id = new string('f', 24);
        try { Assert.Equal("DeltaCorrupt", Assert.Throws<WorkspaceException>(() => Read(fixture.Workspace, fixture.Control)).Code); }
        finally { fixture.Workspace.Id = owner; }
        fixture.AssertOwnedAndPreserved();
        string receipt = fixture.Receipts[0]; var document = JsonNode.Parse(File.ReadAllText(receipt))!.AsObject();
        document["reason"] = "tampered-signed-reason";
        File.WriteAllText(receipt, document.ToJsonString(ApiJson.Options)); byte[] changed = File.ReadAllBytes(receipt);
        Assert.Equal("DeltaCorrupt", Assert.Throws<WorkspaceException>(() => Read(fixture.Workspace, fixture.Control)).Code);
        Assert.Equal(changed, File.ReadAllBytes(receipt)); fixture.AssertOwnedAndPreserved(receipt);
        Assert.Empty(ScratchDirectories().Except(before, StringComparer.Ordinal));
    }

    // Invoke the real CLI projection rather than reproducing its sort, chunk or
    // cursor codec. Native packing below creates its signed source receipts.
    private static JsonElement Read(WorkspaceRecord workspace, string control, string? cursor = null, int pageSize = 128)
    {
        var type = typeof(Splinter.Cli.Program).Assembly.GetType("Splinter.Cli.WorkspaceReadCommands", throwOnError: true)!;
        var method = type.GetMethod("ReadPacking", BindingFlags.Static | BindingFlags.NonPublic)!;
        try
        {
            object page = method.Invoke(null, [workspace, control, cursor, pageSize, TestContext.Current.CancellationToken])!;
            return JsonSerializer.SerializeToElement(page, ApiJson.Options);
        }
        catch (TargetInvocationException error) when (error.InnerException is not null)
        { ExceptionDispatchInfo.Capture(error.InnerException).Throw(); throw; }
    }
    private static HashSet<string> ScratchDirectories() => Directory.EnumerateDirectories(Path.GetTempPath(), "splinter-metadata-*").ToHashSet(StringComparer.Ordinal);
    private static void RequireWindows() { if (!OperatingSystem.IsWindows()) Assert.Skip("Exact native journal creation-gap receipts and long NTFS paths."); }

    private sealed class Fixture : IDisposable
    {
        private readonly TestDirectory files = new();
        private readonly SnapshotOverlayTests.FakeSnapshotTree basis = new();
        private readonly WindowsJournalFilePins pins = new();
        private readonly (string Path, string NativeIdentity, byte[] Bytes)[] preserved;
        private readonly Dictionary<string, byte[]> receipts;
        public WorkspaceRecord Workspace { get; }
        public string Control { get; }
        public string[] Receipts { get; }
        public Fixture(bool longPath)
        {
            Control = Path.Combine(files.Path, "native");
            Directory.CreateDirectory(Control);
            Workspace = new() { Id = new string('a', 24), RequestKey = "packing-query", RequestDigest = "packing-query-request",
                ProjectPath = Path.Combine(Control, "original"), Path = Path.Combine(Control, "workspace"), SourceReference = "source", BaseReference = "basis",
                SourceKind = "none", ApplicationKind = "none", StorageMode = "project-snapshot", Snapshot = new() { Reference = basis.Snapshot } };
            var identity = NtfsSnapshotWorkspaceBackend.Identity(Workspace); string delta = Path.Combine(Control, "delta");
            for (int attempt = 0; attempt < 3; attempt++)
            {
                using var writer = new JournalDeltaStore(delta, identity, basis, boundedLogBytes: 4096, sourceFilePins: pins);
                if (attempt == 0)
                {
                    var file = writer.Create("private", false, 32); writer.Write(file.Id, 0, Enumerable.Repeat((byte)0x62, 8192).ToArray()); writer.SetLength(file.Id, 16);
                }
                if (attempt == 2) writer.MaintainLiveStorage(collectSegments: true, ct: TestContext.Current.CancellationToken);
                else
                {
                    bool failed = false;
                    Assert.Throws<IOException>(() => writer.MaintainLiveStorage(collectSegments: true, ct: TestContext.Current.CancellationToken, boundary: point =>
                    { if (point == "segment-pack.created-before-identity") { failed = true; throw new IOException("Lost native creation response."); } }));
                    Assert.True(failed);
                }
            }
            if (longPath)
            {
                // The query consumes external signed receipts. Expand their
                // filesystem paths independently of packing-plan construction,
                // while keeping the real producer's owner/request/reason. This
                // qualifies the query codec, not long-home packing itself.
                var original = Proofs(); string extended = Path.Combine(files.Path, "long");
                for (int index = 0; index < 28; index++) extended = Path.Combine(extended, new string('\uD55C', 220) + index.ToString("D2"));
                foreach (var proof in original)
                {
                    string directory = Path.Combine(extended, "journal-packing", proof.Request); Directory.CreateDirectory(directory);
                    string destination = Path.Combine(directory, Path.GetFileName(proof.Path)); File.Move(proof.Path, destination);
                    var receipt = JsonNode.Parse(File.ReadAllText(proof.Path + ".preserved.json"))!.AsObject();
                    receipt["path"] = destination; receipt["digest"] = "";
                    receipt["digest"] = JournalControlIntegrity.Digest(receipt);
                    File.WriteAllText(destination + ".preserved.json", receipt.ToJsonString(JournalControlIntegrity.Json));
                }
                Control = extended;
            }
            var proofs = Proofs(); Assert.Equal(2, proofs.Length);
            preserved = proofs.Select(proof => (proof.Path, pins.Identify(proof.Path), File.ReadAllBytes(proof.Path))).ToArray();
            Receipts = Directory.EnumerateDirectories(Path.Combine(Control, "journal-packing"))
                .SelectMany(path => Directory.EnumerateFiles(path, "*.preserved.json")).Order(StringComparer.Ordinal).ToArray();
            receipts = Receipts.ToDictionary(path => path, File.ReadAllBytes, StringComparer.Ordinal);
        }
        public (string Path, string Request, string Reason)[] Proofs() => JournalDeltaStore.EnumeratePreservedPacking(Control, NtfsSnapshotWorkspaceBackend.Identity(Workspace)).ToArray();
        public void AssertOwnedAndPreserved(string? deliberatelyTamperedReceipt = null)
        {
            foreach (var value in preserved)
            { Assert.True(File.Exists(value.Path)); Assert.Equal(value.NativeIdentity, pins.Identify(value.Path)); Assert.Equal(value.Bytes, File.ReadAllBytes(value.Path)); }
            foreach (var receipt in receipts)
                if (receipt.Key != deliberatelyTamperedReceipt) Assert.Equal(receipt.Value, File.ReadAllBytes(receipt.Key));
        }
        public void Dispose() => files.Dispose();
    }
}
