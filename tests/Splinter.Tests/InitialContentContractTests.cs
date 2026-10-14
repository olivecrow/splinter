using System.Security.Cryptography;
using System.Text.Json;
using Splinter.Core;
using Splinter.Platform.Windows;
using Splinter.Runtime;

namespace Splinter.Tests;

public sealed class InitialContentContractTests
{
    [Fact]
    public void FirstGenerationSummaryIsBoundToUnmodifiedPathsAndDoesNotReadPayload()
    {
        using var directory = new TestDirectory(); var snapshot = new SummaryTree();
        using var store = Open(directory.Path, snapshot, initial: true);
        var generation = store.ObserveChanges(["Library"]);
        var initial = store.ObserveInitialContent(["Library"], generation, ["file", "dir/nested", "Library/cache", "unproved"]);
        Assert.Equal(new[] { "file", "dir/nested" }, initial!.Files.Select(file => file.Path));
        Assert.Equal(0, snapshot.PayloadReads);
        store.Write(store.Stat("file")!.Id, 0, [9]);
        Assert.Equal("SourceChanged", Assert.Throws<WorkspaceException>(() =>
            store.ObserveInitialContent(["Library"], generation, ["file"])).Code);
        generation = store.ObserveChanges(["Library"]);
        Assert.Empty(store.ObserveInitialContent(["Library"], generation, ["file"])!.Files);
        Assert.Single(store.ObserveInitialContent(["Library"], generation, ["dir/nested"])!.Files);
    }

    [Fact]
    public void ReconnectHealthAndInvalidSummaryCannotSupplyInitialEvidence()
    {
        using var directory = new TestDirectory(); var snapshot = new SummaryTree();
        using (var initial = Open(directory.Path, snapshot, initial: true))
            Assert.Single(initial.ObserveInitialContent([], initial.ObserveChanges([]), ["file"])!.Files);
        using (var reconnect = Open(directory.Path, snapshot, initial: false))
            Assert.Null(reconnect.ObserveInitialContent([], reconnect.ObserveChanges([]), ["file"]));
        using var fresh = Open(Path.Combine(directory.Path, "fresh"), snapshot, initial: true);
        var generation = fresh.ObserveChanges([]);
        snapshot.BadSummary = true;
        Assert.Equal("SourceObservationInvalid", Assert.Throws<WorkspaceException>(() => fresh.ObserveInitialContent([], generation, ["file"])).Code);
        snapshot.Available = false;
        Assert.Equal("SnapshotMissing", Assert.Throws<WorkspaceException>(() => fresh.ObserveInitialContent([], generation, ["file"])).Code);
    }

    [Fact]
    public void ContentObservationRechecksGenerationAfterReaderCallback()
    {
        using var directory = new TestDirectory(); var snapshot = new SummaryTree();
        using var store = Open(directory.Path, snapshot, initial: true);
        var generation = store.ObserveChanges([]);
        snapshot.OnSummary = () => store.Write(store.Stat("dir/nested")!.Id, 0, [5]);
        Assert.Equal("SourceChanged", Assert.Throws<WorkspaceException>(() => store.ObserveInitialContent([], generation, ["file"])).Code);
    }

    [Fact]
    public void LaunchProvenanceNeverTreatsPriorIntentReceiptOrPartialDeltaAsFresh()
    {
        using var directory = new TestDirectory(); var snapshot = new SummaryTree();
        string delta = Path.Combine(directory.Path, "delta");
        var request = new OverlayServeRequest(Identity(snapshot), delta, Path.Combine(directory.Path, "mount"),
            Path.Combine(directory.Path, "control"), "token", new(snapshot.Snapshot.Id, "root", "verified"), "receipt", "owner");
        Assert.True(WinFspOverlayClient.IsInitialContentGeneration(request, null, null));
        var intent = new WinFspOverlayClient.ProviderIntent("token", "initial-content", "generation", DateTimeOffset.UtcNow,
            InitialContentGeneration: true);
        var launch = new WinFspOverlayClient.ProviderLaunch("token", 12, 34, "generation");
        Assert.False(WinFspOverlayClient.IsInitialContentGeneration(request, intent, null)); // Failed/lost launch response.
        Assert.False(WinFspOverlayClient.IsInitialContentGeneration(request, intent, launch)); // Same intent retry.
        Assert.False(WinFspOverlayClient.IsInitialContentGeneration(request, null, launch));
        Directory.CreateDirectory(delta);
        File.WriteAllText(Path.Combine(delta, "journal-initialization.json.pending"), "preserve");
        Assert.False(WinFspOverlayClient.IsInitialContentGeneration(request, null, null));
        Assert.Equal("preserve", File.ReadAllText(Path.Combine(delta, "journal-initialization.json.pending")));
        // Older launch evidence has no authorization for this new optimization.
        var old = JsonSerializer.Deserialize<WinFspOverlayClient.ProviderIntent>(
            "{\"Token\":\"token\",\"Id\":\"initial-content\",\"Generation\":\"generation\",\"RequestedAt\":\"2026-10-15T00:00:00Z\"}")!;
        Assert.False(old.InitialContentGeneration);
    }

    [Fact]
    public void ContentSummaryIpcRequiresPairedRequestAndExactBoundedHashFields()
    {
        var catalog = new Protocol.ContractCatalog(); var snapshot = new SummaryTree();
        var view = new SourceChangeView("workspace", snapshot.Snapshot.Id, "epoch", 0, []);
        var content = new SourceContentSummaryView(snapshot.Snapshot with
        { Provider = "git-objects", Consistency = "recorded-project-commit-raw-blobs" }, view, [snapshot.GetVerifiedContentSummary("file")!]);
        var request = new SourceChangeRequest("token", "request", [], view, ["file"]);
        var response = new SourceChangeResponse("token", "request", 12, 34, view, null, content);
        Assert.True(Valid("source-change-request", request));
        Assert.True(Valid("source-change-response", response));
        Assert.False(Valid("source-change-request", request with { ContentPaths = null }));
        Assert.False(Valid("source-change-response", response with { Error = "failed" }));
        Assert.False(Valid("source-content-summary-view", content with { Files = [content.Files[0] with { Sha256 = "invalid" }] }));
        Assert.False(Valid("source-content-summary-view", content with { Files = [content.Files[0] with { Length = -1 }] }));
        bool Valid(string name, object value) => catalog.Validate("urn:splinter:v1:" + name,
            JsonSerializer.SerializeToElement(value, new JsonSerializerOptions(JsonSerializerDefaults.Web))).IsValid;
    }

    private static OverlayIdentity Identity(SummaryTree snapshot) => new(2, "initial-content", snapshot.Snapshot, 1048576, 4096);
    private static JournalDeltaStore Open(string path, SummaryTree snapshot, bool initial) =>
        new(path, Identity(snapshot), snapshot, initialContentGeneration: initial);
    private sealed class SummaryTree : ISnapshotTree, ISnapshotContentSummaryProvider
    {
        private readonly SnapshotOverlayTests.FakeSnapshotTree inner = new();
        internal bool Available { set => inner.Available = value; }
        internal bool BadSummary;
        internal Action? OnSummary;
        internal long PayloadReads => inner.ReadBytes;
        public SnapshotReference Snapshot => inner.Snapshot;
        internal SummaryTree()
        {
            foreach (string path in new[] { "file", "dir/nested", "Library/cache", "unproved" }) inner.Files.Add(path, [1, 2, 3]);
        }
        public void EnsureAvailable() => inner.EnsureAvailable();
        public SnapshotEntry? Stat(string path) => inner.Stat(path);
        public IReadOnlyList<SnapshotEntry> List(string path) => inner.List(path);
        public int Read(string path, long offset, Span<byte> target) => inner.Read(path, offset, target);
        public SnapshotContentSummary? GetVerifiedContentSummary(string path)
        {
            EnsureAvailable(); OnSummary?.Invoke();
            return path == "unproved" ? null : new(path, BadSummary ? 99 : 3,
                Convert.ToHexStringLower(SHA256.HashData(inner.Files[path])), false, false);
        }
    }
}
