using Splinter.Core;
using Splinter.Platform.Windows;
using Splinter.Runtime;
using Splinter.SourceControl.Files;

namespace Splinter.Tests;

public sealed class MetadataLeaseCoordinationTests
{
    [Theory(Timeout = 10000)]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CollectionCannotEnterWhileAReaderIsStillClosingPhysicalFences(bool immutableReader)
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Requires native metadata sharing semantics.");
        using var files = new TestDirectory(); string directory = Path.Combine(files.Path, "metadata");
        var native = new WindowsJournalFilePins(); var ct = TestContext.Current.CancellationToken; MetadataTreeRoot root;
        using (var writer = new PagedMetadataStore(directory, true, filePins: native))
        { root = writer.Set(writer.Empty, "kept", "payload"u8); writer.Flush(); }
        PagedMetadataStore.Collect(directory, [root], "initial", native, ct: ct);
        Assert.True(File.Exists(Path.Combine(directory, "locations.json")));
        using var closing = new ManualResetEventSlim(); using var resume = new ManualResetEventSlim();
        var pins = new ClosingPins(native, closing, resume, ct);
        IDisposable owner;
        if (immutableReader)
        {
            var proofs = new ImmutableReadProofs(pins);
            using (var reader = ImmutableReadProofs.OpenMetadata(directory, proofs))
                Assert.Equal("payload"u8.ToArray(), reader.Pages.Open(root).Find("kept"));
            owner = proofs;
        }
        else owner = new PagedMetadataStore.CatalogReadScope(directory, pins);
        var disposing = Task.Run(owner.Dispose, CancellationToken.None);
        try
        {
            Assert.True(closing.Wait(TimeSpan.FromSeconds(2), ct));
            // The collector must remain excluded until every identity,
            // location, page and ancestry fence has finished closing.
            Assert.Throws<IOException>(() =>
            { using var admission = new FileStream(Path.Combine(directory, "writer.lock"), FileMode.Open, FileAccess.ReadWrite, FileShare.None); });
        }
        finally { resume.Set(); await disposing.WaitAsync(TimeSpan.FromSeconds(3), ct); }
        PagedMetadataStore.Collect(directory, [root], "after-close", native, ct: ct);
        using var reopened = new PagedMetadataStore(directory);
        Assert.Equal("payload"u8.ToArray(), reopened.Open(root).Find("kept"));
    }

    private sealed class ClosingPins(WindowsJournalFilePins native, ManualResetEventSlim closing, ManualResetEventSlim resume,
        CancellationToken ct) : IJournalFilePins
    {
        public IRequestPathGuard? PinMetadataPaths(string directory) => new ClosingGuard(native.PinMetadataPaths(directory), closing, resume, ct);
        public string Identify(string path) => native.Identify(path);
        public string IdentifyOpened(FileStream input) => native.IdentifyOpened(input);
        public void Verify(FileStream input, string identity) => native.Verify(input, identity);
        public void Pin(string source, string destination, string identity) => native.Pin(source, destination, identity);
        public bool DetachBeforeTruncate(string path, long length) => native.DetachBeforeTruncate(path, length);
    }
    private sealed class ClosingGuard(IRequestPathGuard? inner, ManualResetEventSlim closing, ManualResetEventSlim resume,
        CancellationToken ct) : IRequestPathGuard
    {
        public bool TryEnsurePlainPath(string path) => inner?.TryEnsurePlainPath(path) == true;
        public void Dispose()
        { closing.Set(); try { resume.Wait(ct); } finally { inner?.Dispose(); } }
    }

    [Fact]
    public void NativeReadPathPinsAllowSiblingPublicationAndReleaseTheirExactFileAndAncestry()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Requires native path and file sharing proofs.");
        using var files = new TestDirectory(); string directory = Path.Combine(files.Path, "scope");
        string reads = Path.Combine(directory, "reads"), writes = Path.Combine(directory, "writes");
        Directory.CreateDirectory(reads); Directory.CreateDirectory(writes);
        string fixedPath = Path.Combine(reads, "fixed"), sibling = Path.Combine(reads, "sibling"), peer = Path.Combine(writes, "peer");
        foreach (string path in new[] { fixedPath, sibling, peer }) File.WriteAllText(path, "old");
        using (var guard = new WindowsJournalFilePins().PinReadPath(fixedPath))
        {
            Assert.NotNull(guard);
            foreach (string path in new[] { sibling, peer })
            { File.WriteAllText(path + ".pending", "new"); AtomicFilePublication.Publish(path + ".pending", path); Assert.Equal("new", File.ReadAllText(path)); }
            File.WriteAllText(fixedPath + ".pending", "new");
            Assert.Throws<IOException>(() => AtomicFilePublication.Publish(fixedPath + ".pending", fixedPath));
            Assert.Throws<IOException>(() => Directory.Move(directory, directory + ".moved"));
            Assert.Equal("old", File.ReadAllText(fixedPath));
        }
        AtomicFilePublication.Publish(fixedPath + ".pending", fixedPath); Assert.Equal("new", File.ReadAllText(fixedPath));
        Directory.Move(directory, directory + ".moved"); Directory.Move(directory + ".moved", directory);
    }

    [Fact(Timeout = 10000)]
    public async Task SlowSourceHashingDoesNotHoldTheSharedMetadataWriterLock()
    {
        using var files = new TestDirectory(); var ct = TestContext.Current.CancellationToken;
        var store = new WorkspaceStore(Path.Combine(files.Path, "home"));
        using var entered = new ManualResetEventSlim(); using var resume = new ManualResetEventSlim();
        var snapshot = new SnapshotReference("basis", "fake", files.Path, files.Path, DateTimeOffset.UtcNow, true, true, "fixture");
        var identity = new SourceCaptureIdentity(new string('b', 24), 1, new string('c', 64));
        var intent = new WorkspaceCaptureIntent(identity.Id, 1, "fixed", "fixed");
        using var tree = new PausedCapture(snapshot, identity, entered, resume, ct);
        var workspace = new WorkspaceRecord { Id = new string('a', 24), RequestKey = "task", RequestDigest = "fixed-digest",
            ProjectPath = files.Path, Path = store.WorkPath(new string('a', 24)), SourceKind = "git", ApplicationKind = "none",
            SourceReference = "splinter/task", BaseReference = "HEAD", Snapshot = new() { Reference = snapshot } };
        var preserving = Task.Run(() => new FileWorkspaceProvider(store).PreserveGenerationAsync(workspace,
            store.ControlPath(workspace.Id), intent, identity, tree, ct), ct);
        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(2), ct));
            using var peerDeadline = CancellationTokenSource.CreateLinkedTokenSource(ct); peerDeadline.CancelAfter(2000);
            using var peer = new SharedMetadataPool(store.Root, "independent-peer", peerDeadline.Token);
        }
        finally
        {
            resume.Set();
            Assert.Equal("InjectedReadFailure", (await Assert.ThrowsAsync<WorkspaceException>(() => preserving)).Code);
        }
        Assert.False(File.Exists(Path.Combine(store.ControlPath(workspace.Id), "files", "generation-basis.json")));
    }

    private sealed class PausedCapture(SnapshotReference snapshot, SourceCaptureIdentity identity, ManualResetEventSlim entered,
        ManualResetEventSlim resume, CancellationToken ct) : ISourceCaptureTree
    {
        public SnapshotReference Snapshot => snapshot;
        public SourceCaptureIdentity CaptureIdentity => identity;
        public ISnapshotTree Basis => this;
        public void EnsureAvailable() { }
        public SnapshotEntry? Stat(string path) => path == "file" ? new("file", false, 1, 32, 0, 0, 0) : null;
        public IReadOnlyList<SnapshotEntry> List(string path) => path == "" ? [Stat("file")!] : [];
        public int Read(string path, long offset, Span<byte> destination)
        { entered.Set(); resume.Wait(ct); throw new WorkspaceException("InjectedReadFailure", "No source root may publish."); }
        public void Dispose() { }
    }

    [Fact(Timeout = 10000)]
    public async Task CollectionWaitsForAnExistingReaderWithoutDroppingItsRoot()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Requires native metadata sharing semantics.");
        using var files = new TestDirectory(); string directory = Path.Combine(files.Path, "metadata");
        var pins = new WindowsJournalFilePins(); MetadataTreeRoot root;
        using (var writer = new PagedMetadataStore(directory, true, filePins: pins))
        { root = writer.Set(writer.Empty, "kept", "payload"u8); writer.Flush(); }
        using var reader = new PagedMetadataStore(directory);
        using var started = new ManualResetEventSlim();
        var ct = TestContext.Current.CancellationToken;
        var collection = Task.Run(() => { started.Set(); return PagedMetadataStore.Collect(directory, [root], "fixed", pins, ct: ct); }, ct);
        Assert.True(started.Wait(TimeSpan.FromSeconds(2), ct)); await Task.Delay(50, ct);
        Assert.False(collection.IsCompleted);
        Assert.Equal("payload"u8.ToArray(), reader.Open(root).Find("kept"));
        reader.Dispose(); await collection.WaitAsync(TimeSpan.FromSeconds(3), ct);
        using var reopened = new PagedMetadataStore(directory);
        Assert.Equal("payload"u8.ToArray(), reopened.Open(root).Find("kept"));
    }

    [Fact(Timeout = 10000)]
    public async Task QueuedCatalogReaderDoesNotFenceIdentityBeforeCollectionFinishes()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Requires native metadata sharing semantics.");
        using var files = new TestDirectory(); string directory = Path.Combine(files.Path, "metadata");
        using (var writer = new PagedMetadataStore(directory, true)) writer.Flush();
        using var collector = new FileStream(Path.Combine(directory, "writer.lock"), FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        using var started = new ManualResetEventSlim(); var ct = TestContext.Current.CancellationToken;
        var reading = Task.Run(() => { started.Set(); return new PagedMetadataStore.CatalogReadScope(directory); }, ct);
        Assert.True(started.Wait(TimeSpan.FromSeconds(2), ct)); await Task.Delay(50, ct);
        Assert.False(reading.IsCompleted);
        // A waiting reader must not block a collector's identity/location work.
        using (new FileStream(Path.Combine(directory, "identity.bin"), FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
        collector.Dispose(); using var ready = await reading.WaitAsync(TimeSpan.FromSeconds(3), ct);
    }

    [Fact(Timeout = 10000)]
    public async Task CancelledCollectionLeaseLeavesInputAndRequestStateUntouched()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Requires native metadata sharing semantics.");
        using var files = new TestDirectory(); string directory = Path.Combine(files.Path, "metadata");
        MetadataTreeRoot root;
        using (var writer = new PagedMetadataStore(directory, true))
        { root = writer.Set(writer.Empty, "kept", "payload"u8); writer.Flush(); }
        using var reader = new PagedMetadataStore(directory);
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cancel.CancelAfter(50);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Task.Run(() =>
            PagedMetadataStore.Collect(directory, [root], "cancelled", new WindowsJournalFilePins(), ct: cancel.Token)));
        Assert.False(File.Exists(Path.Combine(directory, "collection.json")));
        Assert.False(Directory.Exists(Path.Combine(directory, "collections")));
        Assert.Equal("payload"u8.ToArray(), reader.Open(root).Find("kept"));
    }
}
