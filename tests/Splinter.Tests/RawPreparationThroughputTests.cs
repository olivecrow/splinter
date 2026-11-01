using System.Security.Cryptography;
using Splinter.Core;
using Splinter.Platform.Windows;
using Splinter.Runtime;
using Splinter.SourceControl.Files;

namespace Splinter.Tests;

public sealed class RawPreparationThroughputTests
{
    [Fact(Timeout = 60000)]
    public void InterleavedFilesAcrossMoreThanEightBackingsAdmitEachBackingOnce()
    {
        using var files = new TestDirectory(); var ct = TestContext.Current.CancellationToken; var pins = new WindowsJournalFilePins();
        string home = Path.Combine(files.Path, "home"), id = new('a', 24), captureId = new('b', 24), control = Path.Combine(home, "records", id);
        var identity = new SourceCaptureIdentity(captureId, 1, new string('c', 64));
        var w = new WorkspaceRecord { Id = id, RequestKey = "raw-group", RequestDigest = "raw-group-digest", ProjectPath = files.Path,
            Path = Path.Combine(home, "workspaces", id), SourceReference = "source", BaseReference = "basis", SourceKind = "none",
            ApplicationKind = "none", StorageMode = "project-snapshot", CaptureIntent = new(captureId, 1, "fixed", "fixed") };
        string directory = Path.Combine(control, "captures", captureId); Directory.CreateDirectory(directory);
        var tree = new Capture(identity); var entries = new List<FileSourceEntry>();
        for (int segment = 0; segment < 9; segment++)
        {
            byte[] bytes = new byte[256]; new Random(segment).NextBytes(bytes);
            string path = Path.Combine(directory, "segment-" + segment); File.WriteAllBytes(path, bytes);
            var backing = new CapturedImmutableRangeBacking(path, pins.Identify(path), 0, bytes.Length, captureId, identity.Digest, Hash(bytes));
            for (int part = 0; part < 2; part++)
            {
                string name = "file-" + (part * 9 + segment).ToString("D2"); byte[] payload = bytes.AsSpan(part * 32, 32).ToArray();
                tree.Files[name] = payload; tree.Backings[name] = backing with { Offset = part * 32 };
                entries.Add(new(name, "file", 32, Hash(payload), 0, 32));
            }
        }
        using var manifestRoot = new TestDirectory(); using var manifest = Manifest(manifestRoot.Path, entries);
        int publications = 0;
        using var raw = new OwnedRangeContentStore(home, pins, point => { if (point == "range-object-published") publications++; }); var whole = new OwnedContentStore(home, pins);
        using var prepared = FileWorkspaceProvider.PrepareRawRanges(w, control, identity, tree, tree, manifest, raw, whole, ct);
        Assert.Equal(9, raw.BackingProofAdmissions);
        Assert.Equal(1, publications);
        foreach (var entry in entries)
        {
            var result = prepared.Find(entry.Path, 0, 32); Assert.Equal(entry.Hash, result.Hash); Assert.True(result.Raw);
            using var input = raw.Open(w, control, result.Hash, 32, ct); Assert.Equal(entry.Hash, Convert.ToHexStringLower(SHA256.HashData(input)));
        }
    }

    [Fact]
    public void PayloadEnumerationChecksMissingExtraAndMismatchedIndexEntries()
    {
        using var root = new TestDirectory(); string hash = Hash("one"u8.ToArray());
        using var manifest = Manifest(root.Path, [new("a", "file", 3, hash, 0, 32), new("b", "file", 3, hash, 0, 32)]);
        Assert.Single(manifest.EnumeratePayloads(TestContext.Current.CancellationToken));
        using var pages = new PagedMetadataStore(root.Path, true);
        var missing = manifest with { PayloadIndex = pages.Empty };
        Assert.Throws<WorkspaceException>(() => missing.EnumeratePayloads(TestContext.Current.CancellationToken).ToArray());
        var item = pages.Open(manifest.PayloadIndex!).Enumerate().Single();
        var extra = manifest with { PayloadIndex = pages.Set(manifest.PayloadIndex!, new string('f', 64), item.Value.Span) };
        Assert.Throws<WorkspaceException>(() => extra.EnumeratePayloads(TestContext.Current.CancellationToken).ToArray());
    }
    private static FileSourceManifest Manifest(string directory, IEnumerable<FileSourceEntry> entries)
    {
        var items = entries.ToArray(); var ct = TestContext.Current.CancellationToken;
        var catalog = PagedSnapshotCatalog.Build(directory, new("", true, 0, 16, 0, 0, 0),
            items.Select(entry => new DirectoryMetadataRecord(new(entry.Path, false, entry.Length, 32, 0, 0, 0), entry.Hash)), ct: ct);
        using var pages = new PagedMetadataStore(directory, true); var names = new PagedPathInterner(pages); names.Import(items.Select(entry => entry.Path), ct);
        var payloads = pages.Build(items.GroupBy(entry => entry.Hash).OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => new MetadataEntry(group.Key!, FileApplyIndex.Encode(group.First(), names))), ct); pages.Flush();
        return new("test", "test", "test", "sha256:" + catalog.Children.ContentHash, [])
            { Directory = directory, Catalog = catalog, PayloadIndex = payloads, PayloadPaths = names.Root };
    }
    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private sealed class Capture(SourceCaptureIdentity identity) : ISnapshotTree, ISourceCaptureRanges
    {
        private readonly SnapshotOverlayTests.FakeSnapshotTree tree = new();
        public Dictionary<string, byte[]> Files => tree.Files;
        public SnapshotReference Snapshot => tree.Snapshot;
        public void EnsureAvailable() => tree.EnsureAvailable();
        public SnapshotEntry? Stat(string path) => tree.Stat(path);
        public IReadOnlyList<SnapshotEntry> List(string path) => tree.List(path);
        public int Read(string path, long offset, Span<byte> output) => tree.Read(path, offset, output);
        public readonly Dictionary<string, CapturedImmutableRangeBacking> Backings = new();
        public SourceCaptureIdentity CaptureIdentity => identity;
        public CapturedFileRangeLayout? GetFileRanges(string path) => new(null, 0,
            [new(0, Files[path].Length, "fixed", Backings[path])]);
    }
}
