using System.Security.Cryptography;
using Splinter.Core;
using Splinter.Platform.Windows;
using Splinter.Runtime;

namespace Splinter.Tests;

public sealed class OwnedRangePreservationTests
{
    [Fact(Timeout = 10000)]
    public void RepeatedBatchSharesOneCatalogAndOneFencedProofPerDistinctRange()
    {
        RequireWindows(); using var fixture = new Fixture(); var ct = TestContext.Current.CancellationToken;
        byte[] bytes = new byte[512]; new Random(72).NextBytes(bytes); File.WriteAllBytes(fixture.Source.Path, bytes);
        var source = fixture.Source with { Offset = 0, MinimumLength = bytes.Length, ExpectedBackingHash = PayloadHash.Sha256(new MemoryStream(bytes), ct) };
        var distinct = Enumerable.Range(0, 16).Select(i => new OwnedRangeContentStore.Preservation(
            Convert.ToHexStringLower(SHA256.HashData(bytes.AsSpan(i * 32, 32))), 32, source with { Offset = i * 32 })).ToArray();
        int proofs = 0;
        using var store = new OwnedRangeContentStore(fixture.Home, fixture.Pins, point => { if (point == "range-existing-proof-admitted") proofs++; });
        store.PreserveBatch(fixture.Workspace, fixture.Control, distinct, ct); var before = fixture.Snapshot();
        var repeated = Enumerable.Range(0, 64).Select(i => distinct[i % 16]).ToArray();
        store.PreserveBatch(fixture.Workspace, fixture.Control, repeated, ct); Assert.Equal(16, proofs);
        store.PreserveBatch(fixture.Workspace, fixture.Control, repeated, ct); Assert.Equal(32, proofs);
        var after = fixture.Snapshot(); Assert.Equal(before.Keys.Order(StringComparer.Ordinal), after.Keys.Order(StringComparer.Ordinal));
        foreach (var item in before) Assert.Equal(item.Value, after[item.Key]);
    }

    [Fact]
    public void BoundedPublicationRecoversTheSamePinnedBackingAfterPageFlushFailure()
    {
        RequireWindows(); using var fixture = new Fixture(); var ct = TestContext.Current.CancellationToken;
        byte[] bytes = new byte[256]; new Random(42).NextBytes(bytes); File.WriteAllBytes(fixture.Source.Path, bytes);
        var source = fixture.Source with { Offset = 0, ExpectedBackingHash = Convert.ToHexStringLower(SHA256.HashData(bytes)) };
        var rows = Enumerable.Range(0, 3).Select(i => new OwnedRangeContentStore.Preservation(
            Convert.ToHexStringLower(SHA256.HashData(bytes.AsSpan(i * 32, 32))), 32, source with { Offset = i * 32 })).ToArray();
        using (var interrupted = new OwnedRangeContentStore(fixture.Home, fixture.Pins, point =>
            { if (point == "range-object-pages-flushed") throw new IOException("Lost batch before root publication"); }))
            Assert.Throws<IOException>(() => interrupted.PreserveBatch(fixture.Workspace, fixture.Control, rows, ct));
        string backing = Assert.Single(Directory.GetFiles(Path.Combine(fixture.Home, "range-store", "backings")));
        Assert.Equal(source.FileIdentity, fixture.Pins.Identify(backing));
        using var recovered = new OwnedRangeContentStore(fixture.Home, fixture.Pins);
        recovered.PreserveBatch(fixture.Workspace, fixture.Control, rows, ct);
        foreach (var row in rows)
        { using var input = recovered.Open(fixture.Workspace, fixture.Control, row.Hash, row.Length, ct); Assert.Equal(row.Hash, PayloadHash.Sha256(input, ct)); }
        Assert.Single(Directory.GetFiles(Path.Combine(fixture.Home, "range-store", "backings")));
    }

    [Fact]
    public void RepeatedReadyPreservationLeavesEveryOwnedFileIdentityBytesAndModificationTimeUnchanged()
    {
        RequireWindows(); using var fixture = new Fixture(); var ct = TestContext.Current.CancellationToken;
        using (var first = new OwnedRangeContentStore(fixture.Home, fixture.Pins))
            first.Preserve(fixture.Workspace, fixture.Control, fixture.Hash, fixture.Bytes.Length, fixture.Source, ct);
        var before = fixture.Snapshot();
        for (int attempt = 0; attempt < 3; attempt++)
        {
            using var fresh = new OwnedRangeContentStore(fixture.Home, fixture.Pins);
            fresh.Preserve(fixture.Workspace, fixture.Control, fixture.Hash, fixture.Bytes.Length, fixture.Source, ct);
            var after = fixture.Snapshot(); Assert.Equal(before.Keys.Order(StringComparer.Ordinal), after.Keys.Order(StringComparer.Ordinal));
            foreach (var file in before) Assert.Equal(file.Value, after[file.Key]);
        }
    }

    [Fact]
    public void UnchangedReferenceStillRejectsSameIdentitySameLengthTamperingAndPreservesTheDamagedBacking()
    {
        RequireWindows(); using var fixture = new Fixture(); var ct = TestContext.Current.CancellationToken;
        using (var first = new OwnedRangeContentStore(fixture.Home, fixture.Pins))
            first.Preserve(fixture.Workspace, fixture.Control, fixture.Hash, fixture.Bytes.Length, fixture.Source, ct);
        var freshSource = fixture.AnotherSource(); string backing = Assert.Single(Directory.GetFiles(Path.Combine(fixture.Home, "range-store", "backings")));
        string identity = fixture.Pins.Identify(backing); long length = new FileInfo(backing).Length;
        using (var writer = new FileStream(backing, FileMode.Open, FileAccess.Write, FileShare.None))
        { writer.Position = fixture.Source.Offset; writer.WriteByte(0xff); writer.Flush(true); }
        Assert.Equal(identity, fixture.Pins.Identify(backing)); Assert.Equal(length, new FileInfo(backing).Length);
        var before = fixture.Snapshot();
        using var reopened = new OwnedRangeContentStore(fixture.Home, fixture.Pins);
        Assert.Equal("RangeContentInvalid", Assert.Throws<WorkspaceException>(() =>
            reopened.Preserve(fixture.Workspace, fixture.Control, fixture.Hash, fixture.Bytes.Length, freshSource, ct)).Code);
        var after = fixture.Snapshot(); Assert.Equal(before.Keys.Order(StringComparer.Ordinal), after.Keys.Order(StringComparer.Ordinal));
        foreach (var file in before) Assert.Equal(file.Value, after[file.Key]);
        Assert.Equal(fixture.Bytes, File.ReadAllBytes(freshSource.Path).AsSpan((int)freshSource.Offset, fixture.Bytes.Length).ToArray());
    }

    private static void RequireWindows() { if (!OperatingSystem.IsWindows()) Assert.Skip("Exact NTFS immutable backing identities and durable metadata."); }
    private sealed class Fixture : IDisposable
    {
        private readonly TestDirectory files = new();
        public WindowsJournalFilePins Pins { get; } = new();
        public string Home { get; }
        public string Control { get; }
        public WorkspaceRecord Workspace { get; }
        public byte[] Bytes { get; } = "unchanged private range"u8.ToArray();
        public string Hash => Convert.ToHexStringLower(SHA256.HashData(Bytes));
        public CapturedImmutableRangeBacking Source { get; }
        public Fixture()
        {
            Home = Path.Combine(files.Path, "home"); string id = new('a', 24); Control = Path.Combine(Home, "records", id);
            Workspace = new() { Id = id, RequestKey = "range-replay", RequestDigest = "range-replay-request", ProjectPath = files.Path,
                Path = Path.Combine(Home, "workspaces", id), SourceReference = "source", BaseReference = "basis",
                SourceKind = "none", ApplicationKind = "none", StorageMode = "project-snapshot", CaptureIntent = new(new string('b', 24), 1, "fixed", "fixed") };
            Source = CreateSource("first.pin");
        }
        public CapturedImmutableRangeBacking AnotherSource() => CreateSource("next.pin");
        private CapturedImmutableRangeBacking CreateSource(string name)
        {
            string directory = Path.Combine(Control, "captures", Workspace.CaptureIntent!.Id); Directory.CreateDirectory(directory);
            byte[] payload = new byte[256]; Bytes.CopyTo(payload, 128); string path = Path.Combine(directory, name); File.WriteAllBytes(path, payload);
            return new(path, Pins.Identify(path), 128, payload.Length, Workspace.CaptureIntent.Id, new string('c', 64), Convert.ToHexStringLower(SHA256.HashData(payload)));
        }
        public Dictionary<string, OwnedFile> Snapshot() => Directory.EnumerateFiles(Path.Combine(Home, "range-store"), "*", SearchOption.AllDirectories)
            .ToDictionary(path => path, path =>
            {
                using var input = File.OpenRead(path);
                return new OwnedFile(Pins.Identify(path), input.Length, File.GetLastWriteTimeUtc(path).Ticks, Convert.ToHexStringLower(SHA256.HashData(input)));
            }, StringComparer.Ordinal);
        public sealed record OwnedFile(string Identity, long Length, long LastWriteUtcTicks, string Hash);
        public void Dispose() => files.Dispose();
    }
}
