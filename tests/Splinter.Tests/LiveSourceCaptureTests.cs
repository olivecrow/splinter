using System.Text;
using System.Text.Json;
using Splinter.Core;
using Splinter.Platform.Windows;
using Splinter.Runtime;

namespace Splinter.Tests;

public sealed class LiveSourceCaptureTests
{
    [Theory]
    [InlineData(false, false)] [InlineData(false, true)]
    [InlineData(true, false)] [InlineData(true, true)]
    public void PrefixRecoveryRequiresExactCopyIdentityForPartialAndPublishedFiles(bool published, bool replaced)
    {
        if (!OperatingSystem.IsWindows()) Assert.Skip("NTFS internal backing pins.");
        using var root = new TestDirectory(); var pins = new WindowsJournalFilePins();
        string objects = Path.Combine(root.Path, "delta", "objects"); Directory.CreateDirectory(objects);
        string source = Path.Combine(objects, "capsule"), alias = Path.Combine(root.Path, "original.pin");
        byte[] original = Enumerable.Range(0, 1024).Select(i => (byte)i).ToArray(); File.WriteAllBytes(source, original);
        string identity = pins.Identify(source); pins.Pin(source, alias, identity);
        string copies = Path.Combine(root.Path, "capture-cow"); Directory.CreateDirectory(copies);
        string key = WorkspaceStore.Key(JsonSerializer.Serialize(new { Source = source, Identity = identity, Length = 256L }));
        string pending = Path.Combine(copies, key + ".copy"), receipt = Path.Combine(copies, key + ".json");
        byte[] prefix = original[..(published ? 256 : 100)]; File.WriteAllBytes(pending, prefix);
        string copyIdentity = pins.Identify(pending);
        File.WriteAllText(receipt, JsonSerializer.Serialize(new { Version = 1, Source = source, Identity = identity, Length = 256L, CopyIdentity = copyIdentity }));
        if (published) File.Move(pending, source, true);
        string target = published ? source : pending;
        if (replaced)
        {
            File.Move(target, Path.Combine(root.Path, "retained-known-copy")); File.WriteAllBytes(target, prefix);
            string foreignIdentity = pins.Identify(target); Assert.NotEqual(copyIdentity, foreignIdentity);
            Assert.Equal("SourceCaptureBackingInvalid", Assert.Throws<WorkspaceException>(() => WindowsJournalFilePins.RecoverCopies(root.Path)).Code);
            Assert.Equal(foreignIdentity, pins.Identify(target)); Assert.True(File.Exists(receipt));
        }
        else
        {
            WindowsJournalFilePins.RecoverCopies(root.Path);
            Assert.False(File.Exists(pending)); Assert.False(File.Exists(receipt));
            Assert.Equal(published ? prefix : original, File.ReadAllBytes(source));
        }
        Assert.Equal(original, File.ReadAllBytes(alias));
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void MalformedCaptureBackingsArePreservedAndRejected(bool nullEntry)
    {
        if (!OperatingSystem.IsWindows()) Assert.Skip("NTFS internal backing pins.");
        using var root = new TestDirectory(); var basis = new SnapshotOverlayTests.FakeSnapshotTree();
        var identity = new OverlayIdentity(2, "capture", basis.Snapshot, 10485760, 65536);
        using var writer = new JournalDeltaStore(Path.Combine(root.Path, "delta"), identity, basis, sourceFilePins: new WindowsJournalFilePins());
        var file = writer.Create("source", false, 32); writer.Write(file.Id, 0, "frozen"u8);
        string directory = Path.Combine(root.Path, "captures", new string('f', 24));
        var capture = writer.CaptureSource(directory, new string('f', 24), []);
        capture = capture with { Backings = [nullEntry ? null! : capture.Backings[0] with { Source = Path.Combine(root.Path, "foreign") }] };
        capture = capture with { Digest = SourceCaptureTree.Digest(capture) };
        File.WriteAllText(Path.Combine(directory, "capture.json"), JsonSerializer.Serialize(capture, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        Assert.Equal("SourceCaptureInvalid", Assert.Throws<WorkspaceException>(() => SourceCaptureTree.ReadCapture(directory)).Code);
        Assert.NotEmpty(Directory.EnumerateFiles(directory, "*.pin"));
    }
    [Fact]
    public void RetainedPrefixCopyRecoveryPreservesPinnedBytesAndRefusesUnknownCopies()
    {
        if (!OperatingSystem.IsWindows()) Assert.Skip("NTFS internal backing pins.");
        using var root = new TestDirectory(); var pins = new WindowsJournalFilePins();
        string objects = Path.Combine(root.Path, "delta", "objects"); Directory.CreateDirectory(objects);
        string source = Path.Combine(objects, "capsule"), alias = Path.Combine(root.Path, "retained.pin");
        byte[] original = Enumerable.Range(0, 1024).Select(i => (byte)i).ToArray(); File.WriteAllBytes(source, original);
        string identity = pins.Identify(source); pins.Pin(source, alias, identity);
        string copies = Path.Combine(root.Path, "capture-cow"); Directory.CreateDirectory(copies);
        string key = WorkspaceStore.Key(JsonSerializer.Serialize(new { Source = source, Identity = identity, Length = 256L }));
        File.WriteAllBytes(Path.Combine(copies, key + ".copy"), original[..100]);
        var receipt = new { Version = 1, Source = source, Identity = identity, Length = 256L, CopyIdentity = pins.Identify(Path.Combine(copies, key + ".copy")) };
        File.WriteAllText(Path.Combine(copies, key + ".json"), JsonSerializer.Serialize(receipt));
        WindowsJournalFilePins.RecoverCopies(root.Path);
        Assert.Equal(original, File.ReadAllBytes(source)); Assert.Equal(original, File.ReadAllBytes(alias));
        using var retainedReader = new FileStream(alias, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        Assert.True(pins.DetachBeforeTruncate(source, 256));
        Assert.Equal(original[..256], File.ReadAllBytes(source)); Assert.Equal(original, File.ReadAllBytes(alias));
        Assert.NotEqual(identity, pins.Identify(source));
        File.WriteAllText(Path.Combine(copies, "unknown.copy"), "unowned");
        Assert.Equal("SourceCaptureBackingInvalid", Assert.Throws<WorkspaceException>(() => WindowsJournalFilePins.RecoverCopies(root.Path)).Code);
        Assert.True(File.Exists(Path.Combine(copies, "unknown.copy")));
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task CaptureWaitsForWholeAcceptedBatchAndFrozenBytesSurviveConcurrentLaterBatches(bool latest)
    {
        if (!OperatingSystem.IsWindows()) Assert.Skip("NTFS internal backing pins.");
        using var root = new TestDirectory(); var basis = new SnapshotOverlayTests.FakeSnapshotTree();
        using var entered = new ManualResetEventSlim(); using var proceed = new ManualResetEventSlim();
        var observer = new BatchCaptureGate(entered, proceed);
        var identity = new OverlayIdentity(2, "capture", basis.Snapshot, 10485760, 65536);
        using var writer = new JournalDeltaStore(Path.Combine(root.Path, "delta"), identity, basis, observer,
            optimizeJournal: latest, boundedLogBytes: latest ? null : 16384, sourceFilePins: new WindowsJournalFilePins());
        var a = writer.Create("a", false, 32); var b = writer.Create("b", false, 32); observer.Armed = true;
        var write = Task.Run(() => writer.WriteBatch([new(a.Id, 0, "first"u8.ToArray()), new(b.Id, 0, "first"u8.ToArray())]), TestContext.Current.CancellationToken);
        Task<JournalSourceCapture>? capture = null;
        string directory = Path.Combine(root.Path, "captures", new string('d', 24));
        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
            capture = Task.Run(() => writer.CaptureSource(directory, new string('d', 24), []), TestContext.Current.CancellationToken);
            Assert.NotSame(capture, await Task.WhenAny(capture, Task.Delay(100, TestContext.Current.CancellationToken)));
        }
        finally { proceed.Set(); await write; if (capture is not null) await capture; }
        using var frozen = new SourceCaptureTree(directory, basis, new WindowsJournalFilePins());
        var later = Task.Run(() =>
        {
            for (int i = 0; i < 25; i++)
                writer.WriteBatch([new(a.Id, 0, Enumerable.Repeat((byte)i, 1024).ToArray()), new(b.Id, 0, Enumerable.Repeat((byte)i, 1024).ToArray())]);
        }, TestContext.Current.CancellationToken);
        for (int i = 0; i < 25; i++) { Assert.Equal("first", Read(frozen, "a")); Assert.Equal("first", Read(frozen, "b")); }
        await later;
        string next = Path.Combine(root.Path, "captures", new string('e', 24));
        writer.CaptureSource(next, new string('e', 24), []);
        using var newest = new SourceCaptureTree(next, basis, new WindowsJournalFilePins());
        byte[] bytes = new byte[1024]; Assert.Equal(1024, newest.Read("a", 0, bytes)); Assert.All(bytes, x => Assert.Equal((byte)24, x));
        Assert.Equal("first", Read(frozen, "a"));
    }
    private sealed class BatchCaptureGate(ManualResetEventSlim entered, ManualResetEventSlim proceed) : IJournalMutationObserver
    {
        public bool Armed; private bool fired;
        public void Observe(JournalMutationBoundary point)
        {
            if (!Armed || fired || point.Boundary is not ("commit.before-flush" or "latest.before-intent-flush")) return;
            fired = true; entered.Set();
            if (!proceed.Wait(TimeSpan.FromSeconds(15))) throw new TimeoutException("Capture batch gate.");
        }
    }
    [Theory]
    [InlineData("append")]
    [InlineData("bounded")]
    [InlineData("latest")]
    public void FrozenRangesSurviveLaterOverwriteTruncateRenameDeleteAndReconnect(string mode)
    {
        if (!OperatingSystem.IsWindows()) Assert.Skip("NTFS internal backing pins.");
        using var root = new TestDirectory(); var tree = new SnapshotOverlayTests.FakeSnapshotTree();
        tree.Files["Assets/base.txt"] = "immutable"u8.ToArray();
        var identity = new OverlayIdentity(2, "capture", tree.Snapshot, 64L * 1024 * 1024, 1048576, [".git"]);
        string delta = Path.Combine(root.Path, "delta"), capture = Path.Combine(root.Path, "captures", new string('a', 24));
        var pins = new WindowsJournalFilePins(); Guid id;
        using (var writer = new JournalDeltaStore(delta, identity, tree, optimizeJournal: mode == "latest",
            boundedLogBytes: mode == "bounded" ? 16384 : null, sourceFilePins: pins))
        {
            if (writer.Stat("Assets") is null) writer.Create("Assets", true, 16);
            id = writer.Create("Assets/private.txt", false, 32).Id;
            writer.Write(id, 0, "first"u8);
            writer.Create("Library", true, 16); var library = writer.Create("Library/cache", false, 32);
            writer.Write(library.Id, 0, new byte[4096]);
            long baseReads = tree.ReadBytes;
            var frozen = writer.CaptureSource(capture, new string('a', 24), ["Library"]);
            Assert.Equal(baseReads, tree.ReadBytes);
            Assert.DoesNotContain(frozen.Nodes, n => n.Entry.Path.StartsWith("Library", StringComparison.Ordinal));
            for (int i = 0; i < 30; i++) writer.Write(id, 0, Enumerable.Repeat((byte)i, 1024).ToArray());
            writer.SetLength(id, 2); writer.Rename(id, "Assets/renamed.txt", false);
            writer.Delete(id); writer.ReleaseUnlinkedFile(id);
            using var immutable = new SourceCaptureTree(capture, tree, new WindowsJournalFilePins());
            Assert.Equal("first", Read(immutable, "Assets/private.txt"));
            Assert.Null(immutable.Stat("Assets/renamed.txt")); Assert.Null(immutable.Stat("Library"));
            Assert.Equal("immutable", Read(immutable, "Assets/base.txt"));
            Assert.Equal(frozen.Digest, writer.CaptureSource(capture, new string('a', 24), ["Library"]).Digest);
        }
        SourceCaptureTree.RecoverPins(Path.GetDirectoryName(capture)!, pins);
        using (var resumed = new JournalDeltaStore(delta, identity, tree, sourceFilePins: pins))
        using (var frozen = new SourceCaptureTree(capture, tree, new WindowsJournalFilePins()))
        {
            Assert.Null(resumed.Stat("Assets/private.txt"));
            Assert.Equal("first", Read(frozen, "Assets/private.txt"));
        }
        SourceCaptureTree.Complete(capture, pins);
        Assert.Empty(Directory.EnumerateFiles(capture, "*.pin"));
        SourceCaptureTree.RecoverPins(Path.GetDirectoryName(capture)!, pins);
    }

    [Fact]
    public void FrozenPartialRangesAndDirectoryRenameRetainBasisMapping()
    {
        if (!OperatingSystem.IsWindows()) Assert.Skip("NTFS internal backing pins.");
        using var root = new TestDirectory(); var basis = new SnapshotOverlayTests.FakeSnapshotTree();
        basis.Files["Assets/old/file"] = "0123456789"u8.ToArray();
        var identity = new OverlayIdentity(2, "capture", basis.Snapshot, 10485760, 65536);
        using var writer = new JournalDeltaStore(Path.Combine(root.Path, "delta"), identity, basis,
            sourceFilePins: new WindowsJournalFilePins());
        var old = writer.Stat("Assets/old")!;
        writer.Rename(old.Id, "Assets/new", false);
        var file = writer.Stat("Assets/new/file")!; writer.Write(file.Id, 3, "ABC"u8);
        string capture = Path.Combine(root.Path, "captures", new string('b', 24));
        writer.CaptureSource(capture, new string('b', 24), []);
        writer.Write(file.Id, 0, "later"u8);
        using var frozen = new SourceCaptureTree(capture, basis, new WindowsJournalFilePins());
        Assert.Null(frozen.Stat("Assets/old"));
        Assert.Equal("012ABC6789", Read(frozen, "Assets/new/file"));
        Assert.Equal("Assets/new/file", Assert.Single(frozen.List("Assets/new")).Path);
    }
    private static string Read(ISnapshotTree tree, string path)
    { byte[] bytes = new byte[(int)tree.Stat(path)!.Length]; Assert.Equal(bytes.Length, tree.Read(path, 0, bytes)); return Encoding.UTF8.GetString(bytes); }
    [Theory]
    [InlineData("capture.intent-published")]
    [InlineData("capture.backings-pinned")]
    public void InterruptedCaptureResumesItsFrozenBytesBeforeWriterRecovery(string boundary)
    {
        if (!OperatingSystem.IsWindows()) Assert.Skip("NTFS internal backing pins.");
        using var root = new TestDirectory(); var basis = new SnapshotOverlayTests.FakeSnapshotTree();
        var identity = new OverlayIdentity(2, "capture", basis.Snapshot, 10485760, 65536, [".git"]);
        string delta = Path.Combine(root.Path, "delta"), captures = Path.Combine(root.Path, "captures"), capture = Path.Combine(captures, new string('c', 24));
        var pins = new WindowsJournalFilePins(); var interruption = new CaptureInterruption(boundary);
        using (var writer = new JournalDeltaStore(delta, identity, basis, interruption, sourceFilePins: pins))
        {
            var file = writer.Create("source", false, 32); writer.Write(file.Id, 0, "frozen"u8);
            Assert.Throws<IOException>(() => writer.CaptureSource(capture, new string('c', 24), []));
            Assert.Throws<WorkspaceException>(() => writer.Write(file.Id, 0, "later!"u8));
        }
        SourceCaptureTree.RecoverPins(captures, pins);
        using (var writer = new JournalDeltaStore(delta, identity, basis, sourceFilePins: pins))
        {
            var retained = writer.CaptureSource(capture, new string('c', 24), []);
            Assert.Equal(new string('c', 24), retained.Id);
            writer.Write(writer.Stat("source")!.Id, 0, "newest"u8);
            using var frozen = new SourceCaptureTree(capture, basis, new WindowsJournalFilePins());
            Assert.Equal("frozen", Read(frozen, "source"));
        }
        File.WriteAllText(Path.Combine(capture, "materialized.json"), "{}");
        Assert.Equal("SourceCaptureInvalid", Assert.Throws<WorkspaceException>(() => SourceCaptureTree.RecoverPins(captures, pins)).Code);
        Assert.NotEmpty(Directory.EnumerateFiles(capture, "*.pin"));
    }
    private sealed class CaptureInterruption(string boundary) : IJournalMutationObserver
    {
        private bool fired;
        public void Observe(JournalMutationBoundary point)
        { if (!fired && point.Boundary == boundary) { fired = true; throw new IOException("Interrupted capture boundary"); } }
    }
}
