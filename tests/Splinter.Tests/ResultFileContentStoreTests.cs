using System.Security.Cryptography;
using Splinter.Core;
using Splinter.Platform.Windows;
using Splinter.Runtime;

namespace Splinter.Tests;

public sealed class ResultFileContentStoreTests
{
    [Fact]
    public void CapturedPrivateRangeIsPinnedAndNewVersionComposesWithoutAFullPayloadCopy()
    {
        RequireWindows(); using var files = new TestDirectory(); string home = Path.Combine(files.Path, "home"); var w = Workspace(home); string control = Control(home, w); var ct = TestContext.Current.CancellationToken;
        byte[] basis = new byte[4 * 1024 * 1024]; new Random(7).NextBytes(basis); string basisHash = Hash(basis);
        var pins = new WindowsJournalFilePins(); var whole = new OwnedContentStore(home, pins);
        whole.Preserve(w, control, basisHash, basis.Length, output => output.Write(basis), ct);
        byte[] changed = "range-only-write"u8.ToArray(), segment = new byte[128 + changed.Length]; changed.CopyTo(segment, 128);
        string source = Segment(control, w, segment), chunkHash = Hash(changed);
        using var raw = new OwnedRangeContentStore(home, pins);
        raw.Preserve(w, control, chunkHash, changed.Length, new(source, pins.Identify(source), 128, segment.Length, w.CaptureIntent!.Id, new string('b', 64), Hash(segment)), ct);
        byte[] current = basis.ToArray(); changed.CopyTo(current, 4096); string resultHash = Hash(current);
        var contents = new ResultFileContentStore(home, control, w, pins, raw);
        var root = contents.Preserve(resultHash, current.Length, basisHash, basis.Length, basis.Length, [new(4096, changed.Length, chunkHash, true)], ct);
        Assert.Single(Directory.GetFiles(Path.Combine(home, "content-store", "objects"), "*", SearchOption.AllDirectories));
        string retained = Assert.Single(Directory.GetFiles(Path.Combine(home, "range-store", "backings")));
        Assert.Equal(pins.Identify(source), pins.Identify(retained)); Assert.False(File.Exists(Path.Combine(control, "files", "objects", resultHash)));
        File.WriteAllBytes(Path.Combine(files.Path, "mutable-original"), new byte[basis.Length]);
        using (var independent = new ResultFileContentStore(home, control, w, pins, raw, root).Open(resultHash, current.Length, ct))
        { Assert.Equal(resultHash, Convert.ToHexStringLower(SHA256.HashData(independent))); independent.Position = 4096; byte[] value = new byte[changed.Length]; independent.ReadExactly(value); Assert.Equal(changed, value); }
        Assert.Equal(2, contents.EnumerateReferences(root).LongCount());
    }
    [Fact]
    public void TruncateThenExtendUsesZeroHolesAndRetainsOnlyTheVisibleImmutableBasis()
    {
        RequireWindows(); using var files = new TestDirectory(); string home = Path.Combine(files.Path, "home"); var w = Workspace(home); string control = Control(home, w); var ct = TestContext.Current.CancellationToken; var pins = new WindowsJournalFilePins();
        byte[] basis = "0123456789abcdef"u8.ToArray(), change = "AB"u8.ToArray(); string basisHash = Hash(basis), chunkHash = Hash(change);
        var whole = new OwnedContentStore(home, pins); whole.Preserve(w, control, basisHash, basis.Length, output => output.Write(basis), ct);
        string source = Segment(control, w, change); using var raw = new OwnedRangeContentStore(home, pins);
        raw.Preserve(w, control, chunkHash, change.Length, new(source, pins.Identify(source), 0, change.Length, w.CaptureIntent!.Id, new string('b', 64), Hash(change)), ct);
        byte[] expected = new byte[12]; basis.AsSpan(0, 4).CopyTo(expected); change.CopyTo(expected, 8); string hash = Hash(expected);
        var contents = new ResultFileContentStore(home, control, w, pins, raw); var root = contents.Preserve(hash, 12, basisHash, basis.Length, 4, [new(8, 2, chunkHash, true)], ct);
        using var result = contents.Open(hash, 12, ct); byte[] actual = new byte[12]; result.ReadExactly(actual); Assert.Equal(expected, actual);
        result.Position = 6; byte[] middle = new byte[5]; result.ReadExactly(middle); Assert.Equal(new byte[] { 0, 0, (byte)'A', (byte)'B', 0 }, middle);
        Assert.Equal(root, contents.CurrentRoot);
    }
    [Fact]
    public void APrivateOnlyReadDoesNotDecodeOrReadTheCoveredImmutableBasisBytes()
    {
        RequireWindows(); using var files = new TestDirectory(); string home = Path.Combine(files.Path, "home"); var w = Workspace(home); string control = Control(home, w); var ct = TestContext.Current.CancellationToken; var pins = new WindowsJournalFilePins();
        byte[] basis = "01234567"u8.ToArray(), changed = "ZZ"u8.ToArray(), expected = "01ZZ4567"u8.ToArray(); string hash = Hash(expected), privateHash = Hash(changed);
        new OwnedContentStore(home, pins).Preserve(w, control, privateHash, changed.Length, output => output.Write(changed), ct);
        using var raw = new OwnedRangeContentStore(home, pins); int basisOpens = 0;
        Stream OpenBasis(string _, long? length) { Assert.Equal(basis.Length, length); basisOpens++; return new MemoryStream(basis, false); }
        var contents = new ResultFileContentStore(home, control, w, pins, raw, externalBasis: OpenBasis);
        contents.Preserve(hash, expected.Length, Hash(basis), basis.Length, basis.Length, [new(2, 2, privateHash, false)], ct, useExternalBasis: true);
        basisOpens = 0;
        using var read = contents.Open(hash, expected.Length, ct); read.Position = 2; byte[] actual = new byte[2]; read.ReadExactly(actual); Assert.Equal(changed, actual); Assert.Equal(0, basisOpens);
        read.Position = 0; read.ReadExactly(actual); Assert.Equal("01"u8.ToArray(), actual); Assert.Equal(1, basisOpens);
    }
    [Fact]
    public void WrongFullFileIdentityCannotPublishAComposedRoot()
    {
        RequireWindows(); using var files = new TestDirectory(); string home = Path.Combine(files.Path, "home"); var w = Workspace(home); var pins = new WindowsJournalFilePins(); using var raw = new OwnedRangeContentStore(home, pins);
        var contents = new ResultFileContentStore(home, Control(home, w), w, pins, raw);
        Assert.Equal("ResultContentInvalid", Assert.Throws<WorkspaceException>(() => contents.Preserve(new string('a', 64), 0, null, 0, 0, [], TestContext.Current.CancellationToken)).Code);
        Assert.Equal(0, contents.CurrentRoot!.Count);
    }
    [Fact]
    public void ClosedBackingPackingReclaimsDeadFramesAndCoalescesOverlappingLiveBytes()
    {
        RequireWindows(); using var files = new TestDirectory(); string home = Path.Combine(files.Path, "home"); var w = Workspace(home); string control = Control(home, w); var ct = TestContext.Current.CancellationToken; var pins = new WindowsJournalFilePins();
        byte[] segment = new byte[65536]; new Random(9).NextBytes(segment); string source = Segment(control, w, segment), identity = pins.Identify(source);
        byte[] first = segment.AsSpan(1000, 4000).ToArray(), second = segment.AsSpan(2000, 1000).ToArray();
        using (var raw = new OwnedRangeContentStore(home, pins))
        {
            raw.Preserve(w, control, Hash(first), first.Length, new(source, identity, 1000, segment.Length, w.CaptureIntent!.Id, new string('b', 64), Hash(segment)), ct);
            raw.Preserve(w, control, Hash(second), second.Length, new(source, identity, 2000, segment.Length, w.CaptureIntent!.Id, new string('b', 64), Hash(segment)), ct);
        }
        pins.Unpin(source, identity);
        using var retained = new OwnedRangeContentStore(home, pins); retained.CompactClosedBackings(ct);
        string packed = Assert.Single(Directory.GetFiles(Path.Combine(home, "range-store", "backings")));
        Assert.Equal(4000, new FileInfo(packed).Length); Assert.NotEqual(identity, pins.Identify(packed));
        using (var read = retained.Open(w, control, Hash(first), first.Length, ct)) { byte[] actual = new byte[first.Length]; read.ReadExactly(actual); Assert.Equal(first, actual); }
        using (var read = retained.Open(w, control, Hash(second), second.Length, ct)) { byte[] actual = new byte[second.Length]; read.ReadExactly(actual); Assert.Equal(second, actual); }
    }
    [Theory]
    [InlineData("range-backing-intent")] [InlineData("range-backing-pinned")]
    [InlineData("range-object-pages-flushed")] [InlineData("range-object-published")]
    public void CapturedRangePreservationResumesTheSameFixedBytesAfterLostReplies(string boundary)
    {
        RequireWindows(); using var files = new TestDirectory(); string home = Path.Combine(files.Path, "home"); var w = Workspace(home); string control = Control(home, w); var ct = TestContext.Current.CancellationToken; var pins = new WindowsJournalFilePins();
        byte[] bytes = "fixed range"u8.ToArray(); string path = Segment(control, w, bytes), hash = Hash(bytes);
        var backing = new CapturedImmutableRangeBacking(path, pins.Identify(path), 0, bytes.Length, w.CaptureIntent!.Id, new string('b', 64), hash); bool fired = false;
        using (var interrupted = new OwnedRangeContentStore(home, pins, at => { if (!fired && at == boundary) { fired = true; throw new WorkspaceException("ReplyLost", at); } }))
            Assert.Equal("ReplyLost", Assert.Throws<WorkspaceException>(() => interrupted.Preserve(w, control, hash, bytes.Length, backing, ct)).Code);
        using var resumed = new OwnedRangeContentStore(home, pins); resumed.Preserve(w, control, hash, bytes.Length, backing, ct);
        using var input = resumed.Open(w, control, hash, bytes.Length, ct); byte[] actual = new byte[bytes.Length]; input.ReadExactly(actual); Assert.Equal(bytes, actual);
        Assert.Single(Directory.GetFiles(Path.Combine(home, "range-store", "backings")));
    }
    [Theory]
    [InlineData("range-pack-attempt-owned")] [InlineData("range-pack-created-before-receipt")]
    [InlineData("range-pack-stage-owned")] [InlineData("range-pack-payload-durable")]
    [InlineData("range-pack-pages-flushed")] [InlineData("range-pack-published")]
    [InlineData("range-backing-reclaimed")]
    public void ClosedBackingPackingRecoversOwnedStagesAndPreservesAnUnownedCreationGap(string boundary)
    {
        RequireWindows(); using var files = new TestDirectory(); string home = Path.Combine(files.Path, "home"); var w = Workspace(home); string control = Control(home, w); var ct = TestContext.Current.CancellationToken; var pins = new WindowsJournalFilePins();
        byte[] segment = new byte[65536]; new Random(47).NextBytes(segment); byte[] bytes = segment.AsSpan(4096, 500).ToArray(); string hash = Hash(bytes);
        string path = Segment(control, w, segment), native = pins.Identify(path);
        using (var raw = new OwnedRangeContentStore(home, pins)) raw.Preserve(w, control, hash, bytes.Length, new(path, native, 4096, segment.Length, w.CaptureIntent!.Id, new string('b', 64), Hash(segment)), ct);
        pins.Unpin(path, native); bool fired = false;
        using (var interrupted = new OwnedRangeContentStore(home, pins, at => { if (!fired && at == boundary) { fired = true; throw new WorkspaceException("ReplyLost", at); } }))
            Assert.Equal("ReplyLost", Assert.Throws<WorkspaceException>(() => interrupted.CompactClosedBackings(ct)).Code);
        using var resumed = new OwnedRangeContentStore(home, pins); resumed.PruneUnreferenced(w, control, _ => true, ct);
        using (var input = resumed.Open(w, control, hash, bytes.Length, ct)) { byte[] actual = new byte[bytes.Length]; input.ReadExactly(actual); Assert.Equal(bytes, actual); }
        var scratch = resumed.ReadPreservedScratchPage(ct: ct);
        Assert.Equal(boundary == "range-pack-created-before-receipt" ? 1 : 0, scratch.Entries.Length);
        foreach (var unknown in scratch.Entries) Assert.True(File.Exists(Path.Combine(home, unknown.RelativePath.Replace('/', Path.DirectorySeparatorChar))));
        Assert.Equal(1 + scratch.Entries.Length, Directory.GetFiles(Path.Combine(home, "range-store", "backings")).Length);
    }
    [Theory]
    [InlineData("range-pack-created-before-receipt")]
    [InlineData("range-pack-payload-durable")]
    [InlineData("range-pack-pages-flushed")]
    public void PackingNoSpaceKeepsOriginalReferencesUntilTheSameMaintenanceCanResume(string boundary)
    {
        RequireWindows(); using var files = new TestDirectory(); string home = Path.Combine(files.Path, "home"); var w = Workspace(home); string control = Control(home, w); var ct = TestContext.Current.CancellationToken; var pins = new WindowsJournalFilePins();
        byte[] segment = new byte[65536]; new Random(63).NextBytes(segment); byte[] bytes = segment.AsSpan(1024, 100).ToArray(); string hash = Hash(bytes);
        string path = Segment(control, w, segment), native = pins.Identify(path);
        using (var original = new OwnedRangeContentStore(home, pins)) original.Preserve(w, control, hash, bytes.Length, new(path, native, 1024, segment.Length, w.CaptureIntent!.Id, new string('b', 64), Hash(segment)), ct);
        pins.Unpin(path, native);
        using (var interrupted = new OwnedRangeContentStore(home, pins, at => { if (at == boundary) throw new IOException("Injected backing-store exhaustion.", unchecked((int)0x80070070)); }))
            Assert.Equal(unchecked((int)0x80070070), Assert.Throws<IOException>(() => interrupted.CompactClosedBackings(ct)).HResult);
        using var resumed = new OwnedRangeContentStore(home, pins);
        using (var before = resumed.Open(w, control, hash, bytes.Length, ct)) { byte[] actual = new byte[bytes.Length]; before.ReadExactly(actual); Assert.Equal(bytes, actual); }
        resumed.PruneUnreferenced(w, control, _ => true, ct);
        using var after = resumed.Open(w, control, hash, bytes.Length, ct); byte[] recovered = new byte[bytes.Length]; after.ReadExactly(recovered); Assert.Equal(bytes, recovered);
    }
    [Fact]
    public void BusyCachedReaderDefersOnlyItsOwnedRetiredBackingAndDoesNotBlockPackingOrLaterCollection()
    {
        RequireWindows(); using var files = new TestDirectory(); string home = Path.Combine(files.Path, "home"); var w = Workspace(home); string control = Control(home, w); var ct = TestContext.Current.CancellationToken; var pins = new WindowsJournalFilePins();
        byte[] segment = new byte[65536]; new Random(73).NextBytes(segment); byte[] bytes = segment.AsSpan(4096, 1024).ToArray(); string hash = Hash(bytes);
        string path = Segment(control, w, segment), native = pins.Identify(path);
        using (var original = new OwnedRangeContentStore(home, pins)) original.Preserve(w, control, hash, bytes.Length, new(path, native, 4096, segment.Length, w.CaptureIntent!.Id, new string('b', 64), Hash(segment)), ct);
        pins.Unpin(path, native);
        using var cache = new OwnedCapturedContentReader(home, control, w, pins); byte[] actual = new byte[bytes.Length]; Assert.Equal(bytes.Length, cache.Read(hash, true, 0, actual)); Assert.Equal(bytes, actual);
        using var retained = new OwnedRangeContentStore(home, pins); retained.CompactClosedBackings(ct);
        var deferred = Assert.Single(retained.ReadDeferredReclamationPage(ct: ct).Entries); Assert.Equal(segment.Length, deferred.LogicalBytes);
        Assert.Equal(2, Directory.GetFiles(Path.Combine(home, "range-store", "backings")).Length);
        Assert.Equal(bytes.Length, cache.Read(hash, true, 0, actual)); Assert.Equal(bytes, actual);
        using (var independent = retained.Open(w, control, hash, bytes.Length, ct)) { independent.ReadExactly(actual); Assert.Equal(bytes, actual); }
        cache.ReleaseIdleReaders(); retained.ReclaimEmptyBackings(ct);
        Assert.Empty(retained.ReadDeferredReclamationPage(ct: ct).Entries); Assert.Single(Directory.GetFiles(Path.Combine(home, "range-store", "backings")));
    }
    [Fact]
    public void EmptyScratchAndDeferredQueriesDoNotCreateAStoreOrControlFiles()
    {
        RequireWindows(); using var files = new TestDirectory(); string home = Path.Combine(files.Path, "never-created"); var pins = new WindowsJournalFilePins();
        var whole = new OwnedContentStore(home, pins); using var ranges = new OwnedRangeContentStore(home, pins); var ct = TestContext.Current.CancellationToken;
        Assert.Null(whole.ReadPreservedScratchPage(ct: ct).Root); Assert.Null(ranges.ReadPreservedScratchPage(ct: ct).Root);
        Assert.Null(whole.ReadDeferredReclamationPage(ct: ct).Root); Assert.Null(ranges.ReadDeferredReclamationPage(ct: ct).Root);
        Assert.False(Directory.Exists(home));
    }
    private static void RequireWindows() { if (!OperatingSystem.IsWindows()) Assert.Skip("Native sealed capture range links."); }
    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static string Control(string home, WorkspaceRecord w) => Path.Combine(home, "records", w.Id);
    private static string Segment(string control, WorkspaceRecord w, byte[] bytes)
    { string directory = Path.Combine(control, "captures", w.CaptureIntent!.Id); Directory.CreateDirectory(directory); string path = Path.Combine(directory, "00000000.pin"); File.WriteAllBytes(path, bytes); return path; }
    private static WorkspaceRecord Workspace(string home)
    {
        var w = new WorkspaceRecord { Id = new string('a', 24), RequestKey = "capture", RequestDigest = "request", ProjectPath = Path.Combine(home, "original"),
            Path = Path.Combine(home, "workspaces", new string('a', 24)), SourceReference = "source", BaseReference = "base", SourceKind = "none", ApplicationKind = "none", StorageMode = "project-snapshot",
            CaptureIntent = new(new string('c', 24), 1, "capture", "capture") };
        Directory.CreateDirectory(Control(home, w)); return w;
    }
}
