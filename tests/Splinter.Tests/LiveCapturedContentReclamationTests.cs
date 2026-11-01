using System.Security.Cryptography;
using Splinter.Core;
using Splinter.Platform.Windows;
using Splinter.Runtime;
using Splinter.SourceControl.Files;

namespace Splinter.Tests;

public sealed class LiveCapturedContentReclamationTests
{
    [Fact]
    public async Task BasisReversionAndDuplicateFullFileCompositionsKeepSourceOnlyChunksUntilTheirLastLiveReferenceDisappears()
    {
        if (!OperatingSystem.IsWindows()) Assert.Skip("Native immutable range and segmented source ownership.");
        using var files = new TestDirectory(); var ct = TestContext.Current.CancellationToken; var pins = new WindowsJournalFilePins();
        string home = Path.Combine(files.Path, "home"), project = Path.Combine(files.Path, "original"); Directory.CreateDirectory(project);
        var basis = new SnapshotOverlayTests.FakeSnapshotTree(); basis.Files["left"] = "abcd"u8.ToArray(); basis.Files["right"] = "abXY"u8.ToArray(); basis.Files["reverted"] = "abcd"u8.ToArray();
        foreach (var file in basis.Files) File.WriteAllBytes(Path.Combine(project, file.Key), file.Value);
        var store = new WorkspaceStore(home, pins); string id = new('a', 24), captureId = new('b', 24), control = store.ControlPath(id);
        var w = new WorkspaceRecord { Id = id, RequestKey = "live", RequestDigest = "live-request", ProjectPath = project, Path = store.WorkPath(id),
            SourceReference = "source", BaseReference = "basis", SourceKind = "none", ApplicationKind = "none", StorageMode = "project-snapshot", GeneratedDirectoryId = new string('d', 24),
            Snapshot = new() { Reference = basis.Snapshot }, CaptureIntent = new(captureId, 1, "fixed", "fixed") };
        using (var metadata = new TemporaryMetadataDirectory())
        {
            var catalog = PagedSnapshotCatalog.Build(metadata.Path, new("", true, 0, 16, 0, 0, 0), basis.Files.Select(file =>
                new DirectoryMetadataRecord(new(file.Key, false, file.Value.Length, 32, 0, 0, 1), Hash(file.Value))), ct: ct);
            w.BaseRevision = "sha256:" + catalog.Children.ContentHash;
        }
        Directory.CreateDirectory(w.Path);
        using var contentReader = new OwnedCapturedContentReader(home, control, w, pins);
        var identity = new OverlayIdentity(3, id, basis.Snapshot);
        using var journal = new JournalDeltaStore(Path.Combine(control, "delta"), identity, basis, sourceFilePins: pins, capturedContentReader: contentReader);
        journal.Write(journal.Stat("left")!.Id, 2, "ZZ"u8); journal.Write(journal.Stat("right")!.Id, 0, "abZZ"u8); journal.Write(journal.Stat("reverted")!.Id, 0, "abcd"u8);
        string directory = Path.Combine(control, "captures", captureId); var captured = journal.CaptureSource(directory, captureId, []);
        var source = new Barrier(journal, Path.Combine(control, "delta"), identity);
        var provider = new FileWorkspaceProvider(store, sourceChanges: source, contentPins: pins);
        WorkspaceResultGeneration result;
        using (var tree = new SourceCaptureTree(directory, basis, pins, contentReader))
            result = await provider.PreserveGenerationAsync(w, control, w.CaptureIntent, new(captureId, captured.Sequence, captured.Digest), tree, ct);
        w.Results.Add(result); w.ResultRevision = result.Revision; w.CaptureIntent = null;
        journal.AdoptCapturedRanges(control, captureId, TestContext.Current.CancellationToken); SourceCaptureTree.Complete(directory, pins); journal.CompleteSourceCaptureRequest(captureId);
        await provider.MaintainRetainedResultsAsync(w, control, ct);
        using (var raw = new OwnedRangeContentStore(home, pins))
        {
            Assert.False(raw.Contains(w, control, Hash("abcd"u8.ToArray()), 4, ct));
            Assert.True(raw.Contains(w, control, Hash("abZZ"u8.ToArray()), 4, ct));
        }
        Assert.Equal(4, new OwnedContentStore(home, pins).LengthOf(w, control, Hash("abcd"u8.ToArray()), ct));
        var reversion = Assert.Single(CapturedRangeAdoptionStore.Enumerate(CapturedRangeAdoptionStore.Read(control,
            new(captureId, captured.Sequence, captured.Digest))), row => row.FileId == journal.Stat("reverted")!.Id);
        Assert.False(reversion.RawRange); Assert.Equal(Hash("abcd"u8.ToArray()), reversion.ChunkHash);
        byte[] bytes = new byte[4]; Assert.Equal(4, journal.Read(journal.Stat("right")!.Id, 0, bytes)); Assert.Equal("abZZ"u8.ToArray(), bytes);
        Assert.Equal(4, journal.Read(journal.Stat("reverted")!.Id, 0, bytes)); Assert.Equal("abcd"u8.ToArray(), bytes);
        string pendingId = new('c', 24), pendingDirectory = Path.Combine(control, "captures", pendingId);
        var pending = journal.CaptureSource(pendingDirectory, pendingId, []); w.CaptureIntent = new(pendingId, 2, "pending", "pending");
        WorkspaceResultGeneration second;
        using (var frozen = new SourceCaptureTree(pendingDirectory, basis, pins, contentReader))
        {
            journal.Write(journal.Stat("right")!.Id, 0, "next"u8); journal.Write(journal.Stat("reverted")!.Id, 0, "next"u8); contentReader.ReleaseIdleReaders();
            await provider.MaintainRetainedResultsAsync(w, control, ct);
            using (var raw = new OwnedRangeContentStore(home, pins))
            {
                Assert.False(raw.Contains(w, control, Hash("abcd"u8.ToArray()), 4, ct));
                Assert.True(raw.Contains(w, control, Hash("abZZ"u8.ToArray()), 4, ct));
            }
            Assert.Equal(4, frozen.Read("right", 0, bytes)); Assert.Equal("abZZ"u8.ToArray(), bytes);
            second = await provider.PreserveGenerationAsync(w, control, w.CaptureIntent, new(pendingId, pending.Sequence, pending.Digest), frozen, ct);
        }
        w.Results.Add(second); w.ResultRevision = second.Revision; w.CaptureIntent = null;
        journal.AdoptCapturedRanges(control, pendingId, TestContext.Current.CancellationToken); SourceCaptureTree.Complete(pendingDirectory, pins); journal.CompleteSourceCaptureRequest(pendingId); contentReader.ReleaseIdleReaders();
        await provider.MaintainRetainedResultsAsync(w, control, ct);
        using (var raw = new OwnedRangeContentStore(home, pins))
        {
            Assert.False(raw.Contains(w, control, Hash("abcd"u8.ToArray()), 4, ct));
            Assert.False(raw.Contains(w, control, Hash("abZZ"u8.ToArray()), 4, ct));
            Assert.True(raw.Contains(w, control, Hash("ZZ"u8.ToArray()), 2, ct));
        }
        await provider.VerifyGenerationAsync(w, control, result, ct);
        // Keep every returned view object strongly reachable. Closing its
        // native index lease must be deterministic, before this same process
        // reclaims the stopped writer; a finalizer cannot repair this test.
        contentReader.ReleaseIdleReaders(); journal.Dispose();
        JournalDeltaStore.ReclaimPagedIndices(Path.Combine(control, "delta"), identity, pins);
        Assert.Empty(Directory.EnumerateDirectories(Path.Combine(control, "delta"), "index-*"));
        GC.KeepAlive(source);
        await provider.VerifyGenerationAsync(w, control, result, ct);
    }
    private sealed class Barrier(JournalDeltaStore journal, string delta, OverlayIdentity identity) : ISourceChangeProvider
    {
        private readonly List<SourceChangeView> observed = [];
        public Task<SourceChangeView> ObserveChangesAsync(WorkspaceRecord w, CancellationToken ct = default)
        {
            using var view = journal.ObserveChanges(w.ManagedPaths);
            var bound = JournalDeltaStore.BindChangeView(view, delta, identity); observed.Add(bound);
            return Task.FromResult(bound);
        }
        public string CachePath(WorkspaceRecord w) => Path.Combine(delta, "test-cache.json");
    }
    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}
