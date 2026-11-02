using System.Runtime.InteropServices;
using Splinter.Core;
using Splinter.Platform.Windows;
using Splinter.Runtime;

namespace Splinter.Tests;

public sealed class PreparationBottleneckTests
{
    [Fact(Timeout = 15000)]
    public void CurrentNodeCacheKeepsMutationsEvictionFrozenCursorAndReplayConsistent()
    {
        using var root = new TestDirectory(); var basis = new SnapshotOverlayTests.FakeSnapshotTree();
        var ct = TestContext.Current.CancellationToken;
        for (int i = 0; i < 600; i++) { ct.ThrowIfCancellationRequested(); basis.Files[$"dir/file{i:D4}"] = [1, 2, 3]; }
        var identity = new OverlayIdentity(3, "cached-admission", basis.Snapshot, long.MaxValue, 65536);
        Guid id;
        using (var store = new JournalDeltaStore(root.Path, identity, basis))
        {
            id = store.Stat("dir/file0000")!.Id;
            for (int i = 0; i < 600; i++) { ct.ThrowIfCancellationRequested(); Assert.NotNull(store.Stat($"dir/file{i:D4}")); }
            Assert.Equal(id, store.Stat("dir/file0000")!.Id);
            using var old = store.EnumerateChildren("dir").GetEnumerator();
            Assert.True(old.MoveNext()); var frozen = new List<string> { old.Current.Path };
            store.Rename(id, "dir/renamed", false); store.Write(id, 1, [9]);
            store.SetMetadata(id, 32, 11, 12, 13);
            Assert.Null(store.Stat("dir/file0000")); Assert.Equal(13, store.Get(id).LastWriteTime);
            while (old.MoveNext()) frozen.Add(old.Current.Path);
            Assert.Contains("dir/file0000", frozen); Assert.DoesNotContain("dir/renamed", frozen);
            byte[] content = new byte[3]; store.Read(id, 0, content); Assert.Equal(new byte[] { 1, 9, 3 }, content);
            store.Seal();
        }
        using var reopened = new JournalDeltaStore(root.Path, identity, basis);
        Assert.Equal("dir/renamed", reopened.Get(id).Path); Assert.Equal(13, reopened.Get(id).LastWriteTime);
        byte[] actual = new byte[3]; reopened.Read(id, 0, actual); Assert.Equal(new byte[] { 1, 9, 3 }, actual);
    }

    [Fact]
    public void CachedNodeNeverBypassesSnapshotLossOrPoisonedBatch()
    {
        using var root = new TestDirectory(); var basis = new SnapshotOverlayTests.FakeSnapshotTree(); basis.Files["file"] = [1];
        var identity = new OverlayIdentity(3, "cache-guards", basis.Snapshot, long.MaxValue, 65536);
        using var store = new JournalDeltaStore(root.Path, identity, basis);
        Guid id = store.Stat("file")!.Id; Assert.Equal(1, store.Get(id).Length);
        basis.Available = false;
        Assert.Equal("SnapshotMissing", Assert.Throws<WorkspaceException>(() => store.Get(id)).Code);
        basis.Available = true;
        Assert.Throws<IOException>(() => store.ApplyMetadataBatch([() => store.SetLength(id, 2), () => throw new IOException("injected")]));
        Assert.Equal("DeltaReconnectRequired", Assert.Throws<WorkspaceException>(() => store.Get(id)).Code);
    }

    [Fact]
    public void FailedReadOnlyAdmissionBatchDoesNotLeaveUnpublishedCachedNodes()
    {
        using var root = new TestDirectory(); var basis = new SnapshotOverlayTests.FakeSnapshotTree(); basis.Files["later"] = [7];
        var identity = new OverlayIdentity(3, "cache-aborted-admission", basis.Snapshot, long.MaxValue, 65536);
        using var store = new JournalDeltaStore(root.Path, identity, basis);
        Assert.Throws<IOException>(() => store.ApplyMetadataBatch([() => { Assert.NotNull(store.Stat("later")); }, () => throw new IOException("before any mutation")]));
        Guid id = store.Stat("later")!.Id;
        byte[] actual = new byte[1]; store.Read(id, 0, actual); Assert.Equal((byte)7, actual[0]);
    }

    [Fact(Timeout = 10000)]
    public async Task CachedPagedNodeDoesNotMixConcurrentBaseReadAndMutationGenerations()
    {
        using var root = new TestDirectory(); using var basis = new PausedBasis(); var ct = TestContext.Current.CancellationToken;
        using var store = new JournalDeltaStore(root.Path, new(3, "cache-concurrent-read", basis.Snapshot, long.MaxValue, 65536), basis);
        Guid id = store.Stat("file")!.Id; byte[] first = new byte[3];
        var read = Task.Run(() => store.Read(id, 0, first), ct);
        try
        {
            Assert.True(basis.Entered.Wait(TimeSpan.FromSeconds(3), ct));
            store.Write(id, 0, [9]); store.SetMetadata(id, 32, 1, 2, 10);
        }
        finally { basis.Proceed.Set(); await read; }
        Assert.Equal(new byte[] { 1, 2, 3 }, first);
        byte[] second = new byte[3]; store.Read(id, 0, second); Assert.Equal(new byte[] { 9, 2, 3 }, second);
        Assert.Equal(10, store.Get(id).LastWriteTime);
    }

    private sealed class PausedBasis : ISnapshotTree, IDisposable
    {
        private readonly SnapshotOverlayTests.FakeSnapshotTree inner = new();
        internal readonly ManualResetEventSlim Entered = new(), Proceed = new();
        public PausedBasis() => inner.Files["file"] = [1, 2, 3];
        public SnapshotReference Snapshot => inner.Snapshot;
        public void EnsureAvailable() => inner.EnsureAvailable();
        public SnapshotEntry? Stat(string path) => inner.Stat(path);
        public IReadOnlyList<SnapshotEntry> List(string path) => inner.List(path);
        public int Read(string path, long offset, Span<byte> destination)
        {
            Entered.Set(); if (!Proceed.Wait(TimeSpan.FromSeconds(5))) throw new TimeoutException("Release the admitted read.");
            return inner.Read(path, offset, destination);
        }
        public void Dispose() { Proceed.Set(); Entered.Dispose(); Proceed.Dispose(); }
    }

    [Theory]
    [InlineData(".git")][InlineData(".plastic")][InlineData(".hg")][InlineData(".svn")][InlineData(".splinter-apply-pending")]
    public void CombinedManagedPreflightRejectsNestedReservedNamesAndReleasesPins(string name)
    {
        if (!OperatingSystem.IsWindows()) Assert.Skip("Native NTFS admission.");
        using var root = new TestDirectory(); string source = Path.Combine(root.Path, "source");
        string nested = Path.Combine(source, "nested"); Directory.CreateDirectory(nested);
        File.WriteAllText(Path.Combine(nested, name), "preserve");
        Assert.Equal("VcsMetadataUnsupported", Assert.Throws<WorkspaceException>(() => SnapshotInputChecks.VerifyManagedProjectTree(source, TestContext.Current.CancellationToken)).Code);
        Directory.Move(source, source + "-moved");
        Assert.Equal("preserve", File.ReadAllText(Path.Combine(source + "-moved", "nested", name)));
    }

    [Theory]
    [InlineData(false)][InlineData(true)]
    public void CombinedManagedPreflightRetainsStreamAndHardlinkAdmission(bool hardlink)
    {
        if (!OperatingSystem.IsWindows()) Assert.Skip("Native NTFS admission.");
        using var root = new TestDirectory(); string file = Path.Combine(root.Path, "source"); File.WriteAllText(file, "preserve");
        if (hardlink) Assert.True(CreateHardLinkW(Path.Combine(root.Path, "alias"), file, 0));
        else File.WriteAllText(file + ":extra", "stream");
        Assert.Equal("SnapshotFileUnsupported", Assert.Throws<WorkspaceException>(() => SnapshotInputChecks.VerifyManagedProjectTree(root.Path, TestContext.Current.CancellationToken)).Code);
        Assert.Equal("preserve", File.ReadAllText(file));
    }

    [Fact]
    public void CombinedManagedPreflightChecksCancellationBeforePinning()
    {
        using var root = new TestDirectory(); using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => SnapshotInputChecks.VerifyManagedProjectTree(root.Path, cancellation.Token));
    }

    [Fact]
    public void DerivedMetadataNeverOffersDecodedCacheProofAndStillDetectsSharedWriteDamage()
    {
        using var root = new TestDirectory(); string directory = Path.Combine(root.Path, "metadata");
        using var writer = new PagedMetadataStore(directory, true);
        Assert.NotNull(writer.DecodedNodeCacheEpoch);
        var fixedRoot = writer.Build([new("key", "value"u8.ToArray())], TestContext.Current.CancellationToken); writer.Flush();
        using var reader = new PagedMetadataStore(directory);
        Assert.Equal("value"u8.ToArray(), reader.Open(fixedRoot).Find("key"));
        using var derived = reader.Derive(Path.Combine(root.Path, "derived"), null);
        Assert.Null(derived.DecodedNodeCacheEpoch); Assert.Equal("value"u8.ToArray(), derived.Open(fixedRoot).Find("key"));
        writer.Dispose();
        using var mutable = new FileStream(Path.Combine(directory, "pages-0000000000000000.bin"), FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
        long offset = fixedRoot.Page!.Offset + fixedRoot.Page.Bytes - 1; mutable.Position = offset; int value = mutable.ReadByte();
        mutable.Position = offset; mutable.WriteByte((byte)(value ^ 1)); mutable.Flush(true);
        Assert.Throws<WorkspaceException>(() => derived.Open(fixedRoot).Find("key"));
    }

    [Fact(Timeout = 15000)]
    public void DecodedCacheEpochChangesWhenNativeContainerFencesAreEvicted()
    {
        using var root = new TestDirectory(); string directory = Path.Combine(root.Path, "metadata");
        var ct = TestContext.Current.CancellationToken; int capacity = PagedMetadataStore.MaximumContainerReaders;
        var roots = new List<MetadataTreeRoot>(); var fences = new List<PagedMetadataStore>();
        try
        {
            // Real no-write reader fences make each next writer open a new
            // small valid container; no large padding/data fixture is needed.
            for (int i = 0; i < capacity + 2; i++)
            {
                ct.ThrowIfCancellationRequested();
                using (var writer = new PagedMetadataStore(directory, true))
                { roots.Add(writer.Build([new("key", new byte[] { (byte)i })], ct)); writer.Flush(); }
                var reader = new PagedMetadataStore(directory); fences.Add(reader); _ = reader.Open(roots[^1]).Find("key");
            }
        }
        finally { foreach (var reader in fences) reader.Dispose(); }
        using var current = new PagedMetadataStore(directory, true);
        long epoch = current.DecodedNodeCacheEpoch!.Value;
        for (int i = 0; i <= capacity; i++) { ct.ThrowIfCancellationRequested(); Assert.Equal(new byte[] { (byte)i }, current.Open(roots[i]).Find("key")); }
        Assert.True(current.DecodedNodeCacheEpoch > epoch);
        using var mutable = new FileStream(Path.Combine(directory, "pages-0000000000000000.bin"), FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
        var page = roots[0].Page!;
        long offset = page.Offset + page.Bytes - 1; mutable.Position = offset; int value = mutable.ReadByte();
        mutable.Position = offset; mutable.WriteByte((byte)(value ^ 1)); mutable.Flush(true);
        Assert.Throws<WorkspaceException>(() => current.Open(roots[0]).Find("key"));
        Assert.Null(current.DecodedNodeCacheEpoch);
    }

    [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CreateHardLinkW(string link, string source, nint security);
}
