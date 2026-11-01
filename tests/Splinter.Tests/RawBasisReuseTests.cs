using System.Security.Cryptography;
using Splinter.Core;
using Splinter.Platform.Windows;
using Splinter.Runtime;
using Splinter.SourceControl.Files;

namespace Splinter.Tests;

public sealed class RawBasisReuseTests
{
    [Theory(Timeout = 10000)]
    [InlineData(false, false, false)] [InlineData(false, false, true)]
    [InlineData(true, false, false)] [InlineData(true, false, true)]
    [InlineData(true, true, false)] [InlineData(true, true, true)]
    public async Task PreservationDrainsAllPrivateRangesForNewRetainedAndBasisIdentities(bool changed, bool retained, bool invalidTail)
    {
        RequireWindows(); using var files = new TestDirectory(); var ct = TestContext.Current.CancellationToken; var pins = new WindowsJournalFilePins();
        string home = Path.Combine(files.Path, "home"), project = Path.Combine(files.Path, "original"); Directory.CreateDirectory(project);
        byte[] bytes = "abcdefgh"u8.ToArray(); var basis = new SnapshotOverlayTests.FakeSnapshotTree(); basis.Files["data"] = bytes;
        var w = Workspace(home, project, 'a'); w.Snapshot = new() { Reference = basis.Snapshot };
        using (var metadata = new TemporaryMetadataDirectory())
        {
            var catalog = PagedSnapshotCatalog.Build(metadata.Path, new("", true, 0, 16, 0, 0, 0),
                [new(new("data", false, bytes.Length, 32, 0, 0, 1), Hash(bytes))], ct: ct);
            w.BaseRevision = "sha256:" + catalog.Children.ContentHash;
        }
        string control = Control(home, w); byte[] contents = changed ? "ZZYYefgh"u8.ToArray() : bytes;
        var provider = new FileWorkspaceProvider(new WorkspaceStore(home, pins), contentPins: pins);
        WorkspaceResultGeneration? prior = null;
        if (retained)
        {
            var firstIdentity = new SourceCaptureIdentity(w.CaptureIntent!.Id, 1, new string('b', 64));
            using var first = new Capture(basis, contents, firstIdentity, 0,
                [new(0, 2, new string('b', 64), BlockIdentity: new string('b', 64)),
                 new(2, 2, new string('c', 64), BlockIdentity: new string('c', 64))]);
            prior = await provider.PreserveGenerationAsync(w, control, w.CaptureIntent, firstIdentity, first, ct);
            w.Results.Add(prior); w.ResultRevision = prior.Revision;
            w.CaptureIntent = new(new string('c', 24), 2, "second", "second");
        }
        var identity = new SourceCaptureIdentity(w.CaptureIntent!.Id, w.CaptureIntent.Sequence, new string('b', 64));
        using var captured = new Capture(basis, contents, identity, 0,
            [new(0, 2, new string('b', 64), BlockIdentity: new string('b', 64)),
             new(invalidTail ? 7 : 2, 2, new string('c', 64), BlockIdentity: new string('c', 64))]);
        if (invalidTail)
        {
            await Assert.ThrowsAsync<WorkspaceException>(() => provider.PreserveGenerationAsync(w, control, w.CaptureIntent, identity, captured, ct));
            Assert.False(File.Exists(CapturedRangeAdoptionStore.Artifact(control, identity.Id)));
            Assert.False(File.Exists(Path.Combine(control, "files", "generations", identity.Id, "generation.json")));
            if (prior is not null) await provider.VerifyGenerationAsync(w, control, prior, ct);
            return;
        }
        var result = await provider.PreserveGenerationAsync(w, control, w.CaptureIntent, identity, captured, ct);
        Assert.Equal(!changed, w.BaseRevision == result.Revision);
        var adoption = CapturedRangeAdoptionStore.Read(control, identity);
        Assert.Equal(new long[] { 0, 2 }, CapturedRangeAdoptionStore.Enumerate(adoption).Select(row => row.Offset));
        captured.Available = false; basis.Available = false;
        await provider.VerifyGenerationAsync(w, control, result, ct);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AnExistingPeerRawChunkCanBackAnImmutableBasisAndRepeatedResultsWithoutAWholeFileCopy(bool failBeforeBasisHeader)
    {
        RequireWindows(); using var files = new TestDirectory(); var ct = TestContext.Current.CancellationToken; var pins = new WindowsJournalFilePins();
        string home = Path.Combine(files.Path, "home"), project = Path.Combine(files.Path, "original"); Directory.CreateDirectory(project);
        byte[] bytes = "abcdefgh"u8.ToArray(); string hash = Hash(bytes); File.WriteAllBytes(Path.Combine(project, "data"), bytes);
        var donor = Workspace(home, project, 'd'); string donorControl = Control(home, donor), source = Segment(donorControl, donor, bytes);
        string nativeIdentity = pins.Identify(source); using var raw = new OwnedRangeContentStore(home, pins);
        raw.Preserve(donor, donorControl, hash, bytes.Length, new(source, nativeIdentity, 128, 256,
            donor.CaptureIntent!.Id, new string('b', 64), Hash(File.ReadAllBytes(source))), ct);
        var basis = new SnapshotOverlayTests.FakeSnapshotTree(); basis.Files["data"] = bytes;
        var w = Workspace(home, project, 'a'); w.Snapshot = new() { Reference = basis.Snapshot };
        using (var metadata = new TemporaryMetadataDirectory())
        {
            var catalog = PagedSnapshotCatalog.Build(metadata.Path, new("", true, 0, 16, 0, 0, 0),
                [new(new("data", false, bytes.Length, 32, 0, 0, 1), hash)], ct: ct);
            w.BaseRevision = "sha256:" + catalog.Children.ContentHash;
        }
        var store = new WorkspaceStore(home, pins); var provider = new FileWorkspaceProvider(store, contentPins: pins);
        string control = Control(home, w); byte[] changed = "abZZefgh"u8.ToArray(); var identity = new SourceCaptureIdentity(w.CaptureIntent!.Id, 1, new string('b', 64));
        using var first = new Capture(basis, changed, identity, 2);
        if (failBeforeBasisHeader)
        {
            string header = Path.Combine(control, "files", "generation-basis.json"); Directory.CreateDirectory(header);
            var failure = await Record.ExceptionAsync(() => provider.PreserveGenerationAsync(w, control, w.CaptureIntent, identity, first, ct));
            Assert.True(failure is IOException or UnauthorizedAccessException);
            Assert.True(Directory.Exists(header)); Assert.False(File.Exists(header));
            Assert.False(Directory.Exists(Path.Combine(home, "content-store", "objects")));
            // This other owner can independently acquire a whole representation;
            // recovery must retain the first descriptor rather than rewrite it.
            new OwnedContentStore(home, pins).Preserve(donor, donorControl, hash, bytes.Length, output => output.Write(bytes), ct);
            Directory.Delete(header);
        }
        var result = await provider.PreserveGenerationAsync(w, control, w.CaptureIntent, identity, first, ct);
        w.Results.Add(result); w.ResultRevision = result.Revision;
        var baseline = FileWorkspaceProvider.Read<FileSourceManifest>(Path.Combine(control, "files", "generation-basis.json"));
        Assert.NotNull(baseline.ContentFiles);
        var basisContents = new ResultFileContentStore(home, control, w, pins, raw, baseline.ContentFiles);
        Assert.True(basisContents.UsesRawBasis(hash, bytes.Length));
        Assert.True(raw.Contains(w, control, hash, bytes.Length, ct));
        Assert.Equal(nativeIdentity, pins.Identify(Assert.Single(Directory.GetFiles(Path.Combine(home, "range-store", "backings")))));
        Assert.False(File.Exists(Path.Combine(control, "files", "objects", hash)));
        var wholeObjects = Directory.GetFiles(Path.Combine(home, "content-store", "objects"), "*", SearchOption.AllDirectories);
        Assert.Equal(failBeforeBasisHeader ? 2 : 1, wholeObjects.Length);
        Assert.Equal(2, new FileInfo(Assert.Single(wholeObjects, path => Path.GetFileName(path) != hash)).Length);
        first.Available = false; basis.Available = false; File.WriteAllText(Path.Combine(project, "data"), "unrelated later original");
        Assert.Equal(result, await provider.PreserveGenerationAsync(w, control, w.CaptureIntent, identity, first, ct));
        raw.PruneUnreferenced(donor, donorControl, _ => false, ct);
        await provider.VerifyGenerationAsync(w, control, result, ct);
        Assert.True(raw.Contains(w, control, hash, bytes.Length, ct));
        basis.Available = true; w.CaptureIntent = new(new string('c', 24), 2, "second", "second");
        var secondIdentity = new SourceCaptureIdentity(w.CaptureIntent.Id, 2, new string('c', 64));
        using var second = new Capture(basis, "abcdYYgh"u8.ToArray(), secondIdentity, 4);
        var next = await provider.PreserveGenerationAsync(w, control, w.CaptureIntent, secondIdentity, second, ct);
        w.Results.Add(next); w.ResultRevision = next.Revision; w.CaptureIntent = null; second.Available = false; basis.Available = false;
        Assert.Equal(12, w.ResultStorage!.UniquePayloadBytes); Assert.Equal(3, w.ResultStorage.PayloadObjects);
        await provider.MaintainRetainedResultsAsync(w, control, ct);
        await provider.VerifyGenerationAsync(w, control, result, ct); await provider.VerifyGenerationAsync(w, control, next, ct);
        Assert.Equal(nativeIdentity, pins.Identify(Assert.Single(Directory.GetFiles(Path.Combine(home, "range-store", "backings")))));
        Assert.False(File.Exists(Path.Combine(control, "files", "objects", hash)));
        Assert.Equal(failBeforeBasisHeader ? 3 : 2, Directory.GetFiles(Path.Combine(home, "content-store", "objects"), "*", SearchOption.AllDirectories).Length);
    }
    [Theory]
    [InlineData("range-existing-reference-durable")]
    [InlineData("range-existing-reference-published")]
    public void ExistingRawReferenceRecoversAfterAResponseOrDiskSpaceFailure(string boundary)
    {
        RequireWindows(); using var files = new TestDirectory(); var pins = new WindowsJournalFilePins(); var ct = TestContext.Current.CancellationToken;
        string home = Path.Combine(files.Path, "home"); var a = Workspace(home, files.Path, 'a'); var b = Workspace(home, files.Path, 'b');
        byte[] bytes = "shared raw"u8.ToArray(); string source = Segment(Control(home, a), a, bytes), hash = Hash(bytes);
        using var raw = new OwnedRangeContentStore(home, pins);
        raw.Preserve(a, Control(home, a), hash, bytes.Length, new(source, pins.Identify(source), 128, 256, a.CaptureIntent!.Id, new string('b', 64), Hash(File.ReadAllBytes(source))), ct);
        bool fired = false;
        using (var interrupted = new OwnedRangeContentStore(home, pins, point =>
            { if (!fired && point == boundary) { fired = true; throw new IOException("Injected existing-reference failure.", unchecked((int)0x80070070)); } }))
            Assert.Throws<IOException>(() => interrupted.TryReferenceExisting(b, Control(home, b), hash, bytes.Length, ct));
        Assert.True(fired); Assert.True(raw.TryReferenceExisting(b, Control(home, b), hash, bytes.Length, ct));
        Assert.True(raw.Contains(a, Control(home, a), hash, bytes.Length, ct)); Assert.True(raw.Contains(b, Control(home, b), hash, bytes.Length, ct));
        Assert.Single(Directory.GetFiles(Path.Combine(home, "range-store", "backings")));
    }
    [Fact]
    public void RawBasisRejectsAnExternalReaderInvalidLengthAndThePreviousDescriptorCodec()
    {
        RequireWindows(); using var files = new TestDirectory(); var pins = new WindowsJournalFilePins(); var ct = TestContext.Current.CancellationToken;
        string home = Path.Combine(files.Path, "home"); var w = Workspace(home, files.Path, 'a'); using var raw = new OwnedRangeContentStore(home, pins);
        var contents = new ResultFileContentStore(home, Control(home, w), w, pins, raw); string hash = new('a', 64);
        Assert.Equal("ResultContentInvalid", Assert.Throws<WorkspaceException>(() => contents.Preserve(hash, 1, hash, 1, 1, [], ct, useExternalBasis: true, useRawBasis: true)).Code);
        Assert.Equal("ResultContentInvalid", Assert.Throws<WorkspaceException>(() => contents.Preserve(hash, 0, hash, 0, 0, [], ct, useRawBasis: true)).Code);
        MetadataTreeRoot root;
        using (var pages = new PagedMetadataStore(contents.MetadataDirectory, true, filePins: pins))
        { var old = new byte[26]; old[0] = 2; root = pages.Set(pages.Empty, hash, old); pages.Flush(); }
        Assert.Equal("ResultContentInvalid", Assert.Throws<WorkspaceException>(() => new ResultFileContentStore(home, Control(home, w), w, pins, raw, root).Open(hash, ct: ct)).Code);
    }
    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static string Control(string home, WorkspaceRecord w) => Path.Combine(home, "records", w.Id);
    private static string Segment(string control, WorkspaceRecord w, byte[] bytes)
    {
        string directory = Path.Combine(control, "captures", w.CaptureIntent!.Id); Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "segment.pin"); byte[] segment = new byte[256]; bytes.CopyTo(segment, 128); File.WriteAllBytes(path, segment); return path;
    }
    private static WorkspaceRecord Workspace(string home, string project, char id)
    {
        var w = new WorkspaceRecord { Id = new string(id, 24), RequestKey = "request-" + id, RequestDigest = "request-digest-" + id,
            ProjectPath = project, Path = Path.Combine(home, "workspaces", new string(id, 24)), SourceReference = "source", BaseReference = "basis",
            SourceKind = "none", ApplicationKind = "none", StorageMode = "project-snapshot", GeneratedDirectoryId = new string('e', 24),
            CaptureIntent = new(new string(id, 24), 1, "first", "first") };
        Directory.CreateDirectory(Control(home, w)); return w;
    }
    private static void RequireWindows() { if (!OperatingSystem.IsWindows()) Assert.Skip("Exact native immutable backing references."); }
    private sealed class Capture(SnapshotOverlayTests.FakeSnapshotTree basis, byte[] bytes, SourceCaptureIdentity identity, int offset,
        IEnumerable<CapturedPrivateSourceRange>? privateRanges = null) : ISourceCaptureTree, ISourceCaptureRanges
    {
        public bool Available { get; set; } = true;
        public SnapshotReference Snapshot => basis.Snapshot;
        public ISnapshotTree Basis => basis;
        public SourceCaptureIdentity CaptureIdentity => identity;
        public void EnsureAvailable() { if (!Available) throw new WorkspaceException("SnapshotMissing", "The test frozen source has been released."); }
        public SnapshotEntry? Stat(string path) { EnsureAvailable(); return basis.Stat(path); }
        public IReadOnlyList<SnapshotEntry> List(string path) { EnsureAvailable(); return basis.List(path); }
        public int Read(string path, long at, Span<byte> output)
        { EnsureAvailable(); int count = (int)Math.Min(output.Length, bytes.Length - at); bytes.AsSpan((int)at, count).CopyTo(output); return count; }
        public CapturedFileRangeLayout? GetFileRanges(string path) => new(path, bytes.Length,
            privateRanges ?? [new(offset, 2, new string('b', 64), BlockIdentity: new string('b', 64))], Guid.Parse("11111111-1111-1111-1111-111111111111"));
        public void Dispose() { }
    }
}
