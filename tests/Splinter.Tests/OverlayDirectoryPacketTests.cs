using System.Collections;
using System.Runtime.InteropServices;
using Splinter.Core;
using Splinter.Platform.Windows;

namespace Splinter.Tests;

// Exercise FileSystemBase.ReadDirectory itself: it resets Context for each
// native buffer, and consumes one lookahead before AddDirInfo reports full.
public sealed class OverlayDirectoryPacketTests
{
    [Fact]
    public void TenThousandEntriesAcrossTinyNativePacketsVisitEachMemberOnce()
    {
        RequireWindows(); using var store = new CountingStore(10000);
        var fs = Open(store, out var node, out var handle);
        try
        {
            var all = ReadAll(fs, node, handle);
            Assert.Equal(Enumerable.Range(0, 10000).Select(Name), all);
            Assert.Equal(1, store.Listings); Assert.Equal(10000, store.Visited);
            Assert.Equal(0, store.Active); Assert.Equal(1, store.Disposals);
            Assert.Empty(Packet(fs, node, handle, "*", all[^1]));
            Assert.Equal(1, store.Listings);
        }
        finally { fs.Close(node, handle); }
        Assert.Equal(1, store.Disposals); Assert.Equal(0, fs.RetainedFileContexts);
    }

    [Fact]
    public void OverflowLookaheadAndFixedMembershipSurviveMutationBetweenPackets()
    {
        RequireWindows(); using var store = new CountingStore(11);
        var fs = Open(store, out var node, out var handle);
        try
        {
            var first = Packet(fs, node, handle, "*", null);
            Assert.Equal(new[] { Name(0), Name(1) }, first);
            Assert.Equal(3, store.Visited); Assert.Equal(1, store.Active);
            store.Count = 12;
            var all = first.Concat(ReadAll(fs, node, handle, first[^1])).ToArray();
            Assert.Equal(Enumerable.Range(0, 11).Select(Name), all);
            Assert.Equal(1, store.Listings); Assert.Equal(11, store.Visited);
            Assert.Equal(Enumerable.Range(0, 12).Select(Name), ReadAll(fs, node, handle));
            Assert.Equal(2, store.Listings); Assert.Equal(0, store.Active);
        }
        finally { fs.Close(node, handle); }
    }

    [Fact]
    public void OmittedContinuationPatternKeepsTheOriginalFixedRoot()
    {
        RequireWindows(); using var store = new CountingStore(11);
        var fs = Open(store, out var node, out var handle);
        try
        {
            var first = Packet(fs, node, handle, "*.cs", null);
            store.Count = 12;
            var all = first.Concat(ReadAll(fs, node, handle, first[^1], pattern: null));
            Assert.Equal(Enumerable.Range(0, 11).Select(Name), all);
            Assert.Equal(1, store.Listings); Assert.Equal(11, store.Visited); Assert.Equal(0, store.Active);
        }
        finally { fs.Close(node, handle); }
    }

    [Fact]
    public void RewindOutOfSequenceAndPatternChangeResetAndReleaseOnlyTheirCursor()
    {
        RequireWindows(); using var store = new CountingStore(20);
        var fs = Open(store, out var node, out var handle);
        try
        {
            Assert.Equal(new[] { Name(0), Name(1) }, Packet(fs, node, handle, "*.a", null));
            Assert.Equal(new[] { Name(2), Name(3) }, Packet(fs, node, handle, "*.a", Name(1)));
            Assert.Equal(1, store.Listings);
            Assert.Equal(new[] { Name(1), Name(2) }, Packet(fs, node, handle, "*.a", Name(0)));
            Assert.Equal(2, store.Listings); Assert.Equal(1, store.Disposals);
            Assert.Equal(new[] { Name(10), Name(11) }, Packet(fs, node, handle, "*.a", Name(9)));
            Assert.Equal(3, store.Listings); Assert.Equal(2, store.Disposals);
            Assert.Equal(new[] { Name(12), Name(13) }, Packet(fs, node, handle, "*.b", Name(11)));
            Assert.Equal(4, store.Listings); Assert.Equal(3, store.Disposals);
        }
        finally { fs.Close(node, handle); }
        Assert.Equal(4, store.Disposals); Assert.Equal(0, store.Active);
    }

    [Fact]
    public void CleanupThenCloseReleasesAnUnfinishedPacketExactlyOnce()
    {
        RequireWindows(); using var store = new CountingStore(10000);
        var fs = Open(store, out var node, out var handle);
        _ = Packet(fs, node, handle, "*", null);
        Assert.Equal(1, store.Active);
        fs.Cleanup(node, handle, "\\", 0);
        Assert.Equal(0, store.Active); Assert.Equal(1, store.Disposals);
        fs.Close(node, handle);
        Assert.Equal(1, store.Disposals); Assert.Equal(0, fs.OpenHandles); Assert.Equal(0, fs.RetainedFileContexts);
    }

    [Fact]
    public void PacketFailureDisposesTheFixedReaderBeforeReturning()
    {
        RequireWindows(); using var store = new CountingStore(20) { ThrowAt = 3 };
        var fs = Open(store, out var node, out var handle);
        try
        {
            _ = Packet(fs, node, handle, "*", null);
            Assert.Throws<IOException>(() => Packet(fs, node, handle, "*", Name(1)));
            Assert.Equal(0, store.Active); Assert.Equal(1, store.Disposals);
        }
        finally { fs.Close(node, handle); }
        Assert.Equal(1, store.Disposals);
    }

    [Fact]
    public void DotEntriesAreNotLostAtTheNativePacketBoundary()
    {
        RequireWindows(); using var store = new CountingStore(3, "D");
        var fs = Open(store, out var node, out var handle, "\\D");
        try { Assert.Equal(new[] { ".", "..", Name(0), Name(1), Name(2) }, ReadAll(fs, node, handle)); }
        finally { fs.Close(node, handle); }
        Assert.Equal(1, store.Disposals);
    }

    [Fact]
    public void RewindingDotMarkersIncludesValidPunctuationNames()
    {
        RequireWindows(); using var store = new CountingStore(3, "D") { Labels = ["!bang", "$money", "alpha"] };
        var fs = Open(store, out var node, out var handle, "\\D");
        try
        {
            Assert.Equal(new[] { ".", ".." }, Packet(fs, node, handle, "*", null));
            Assert.Equal(new[] { "..", "!bang" }, Packet(fs, node, handle, "changed", "."));
            Assert.Equal(new[] { "!bang", "$money" }, Packet(fs, node, handle, "changed", ".."));
        }
        finally { fs.Close(node, handle); }
        Assert.Equal(store.Listings, store.Disposals); Assert.Equal(0, store.Active);
    }

    [Fact]
    public async Task ConcurrentNativePacketsOnOneHandleDoNotInterleaveTheirLookahead()
    {
        RequireWindows(); using var store = new CountingStore(100) { PauseMoves = true };
        var fs = Open(store, out var node, out var handle);
        try
        {
            for (int round = 0; round < 12; round++)
            {
                var packets = await Task.WhenAll(Enumerable.Range(0, 8).Select(i => Task.Run(() =>
                    Packet(fs, node, handle, "*", i == 0 ? null : Name(i * 3 - 1)))));
                for (int i = 0; i < packets.Length; i++)
                    Assert.Equal(new[] { Name(i * 3), Name(i * 3 + 1) }, packets[i]);
            }
        }
        finally { fs.Close(node, handle); }
        Assert.Equal(0, store.Active); Assert.Equal(store.Listings, store.Disposals);
    }

    private static void RequireWindows() { if (!OperatingSystem.IsWindows()) Assert.Skip("Installed Windows WinFsp native directory-buffer contract."); }
    private static string Name(int value) => "f" + value.ToString("D5");
    private static WinFspOverlayFileSystem Open(CountingStore store, out object node, out object handle, string path = "\\")
    {
        var fs = new WinFspOverlayFileSystem(store);
        Assert.Equal(0, fs.Open(path, 0, 0, out node, out handle, out _, out _)); return fs;
    }
    private static List<string> ReadAll(WinFspOverlayFileSystem fs, object node, object handle, string? marker = null, string? pattern = "*")
    {
        var names = new List<string>();
        for (int packets = 0; packets < 20000; packets++)
        {
            var page = Packet(fs, node, handle, pattern, marker);
            if (page.Count == 0) return names;
            names.AddRange(page); marker = page[^1];
        }
        throw new InvalidOperationException("Native directory enumeration did not reach EOF.");
    }
    private static List<string> Packet(WinFspOverlayFileSystem fs, object node, object handle, string? pattern, string? marker)
    {
        const int capacity = 256, header = 104; var buffer = Marshal.AllocHGlobal(capacity);
        try
        {
            Assert.Equal(0, fs.ReadDirectory(node, handle, pattern!, marker!, buffer, capacity, out uint bytes));
            var names = new List<string>(); int offset = 0;
            while (offset + 2 <= bytes)
            {
                int size = (ushort)Marshal.ReadInt16(buffer, offset); if (size == 0) break;
                Assert.InRange(size, header, capacity); Assert.True(offset + size <= bytes);
                names.Add(Marshal.PtrToStringUni(buffer + offset + header, (size - header) / 2)!);
                offset += (size + 7) & ~7;
            }
            return names;
        }
        finally { Marshal.FreeHGlobal(buffer); }
    }

    private sealed class CountingStore(int count, string path = "") : IWorkspaceDeltaStore
    {
        internal int Count = count, Listings, Visited, Active, Disposals, ThrowAt = -1;
        internal bool PauseMoves;
        internal string[]? Labels;
        private readonly DeltaEntry root = new(Guid.Empty, "", true, 0, 16, 1, 1, 1, "", 0);
        private readonly DeltaEntry directory = new(new Guid(1, 0, 0, new byte[8]), path, true, 0, 16, 1, 1, 1, path, 0);
        public OverlayIdentity Identity { get; } = new(2, "packet-directory", new("packet-basis", "fake-memory", "source", "root", DateTimeOffset.UnixEpoch, true, true, "test-only"));
        public DeltaEntry? Stat(string value) => value.Trim('\\', '/').Length == 0 ? root : directory;
        public DeltaEntry Get(Guid id) => id == Guid.Empty ? root : directory;
        public IReadOnlyList<DeltaEntry> List(string directory) => throw new InvalidOperationException("No whole-directory list.");
        public IEnumerable<DeltaEntry> EnumerateChildren(string directory) { Listings++; Active++; return new Members(this, Count, directory, Labels); }
        private sealed class Members(CountingStore owner, int count, string path, string[]? labels) : IEnumerable<DeltaEntry>, IDisposable
        {
            private int disposed;
            public IEnumerator<DeltaEntry> GetEnumerator() => Read().GetEnumerator();
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
            private IEnumerable<DeltaEntry> Read()
            {
                try
                {
                    for (int i = 0; i < count; i++)
                    {
                        if (owner.Visited == owner.ThrowAt) throw new IOException("Injected directory read failure.");
                        if (owner.PauseMoves) Thread.Sleep(1);
                        owner.Visited++;
                        string name = labels is null ? Name(i) : labels[i];
                        yield return new(new Guid(i + 2, 0, 0, new byte[8]), path.Length == 0 ? name : path + "/" + name, false, i, 32, 1, 1, 1, null, 0);
                    }
                }
                finally { Dispose(); }
            }
            public void Dispose() { if (Interlocked.Exchange(ref disposed, 1) == 0) { owner.Active--; owner.Disposals++; } }
        }
        public DeltaEntry Create(string p, bool d, uint a) => throw new NotSupportedException();
        public int Read(Guid id, long offset, Span<byte> destination) => throw new NotSupportedException();
        public void Write(Guid id, long offset, ReadOnlySpan<byte> bytes) => throw new NotSupportedException();
        public void SetLength(Guid id, long length) => throw new NotSupportedException();
        public void SetMetadata(Guid id, uint a, long c, long r, long w) => throw new NotSupportedException();
        public void SetLengthAndMetadata(Guid id, long length, uint a, long c, long r, long w) => throw new NotSupportedException();
        public void Rename(Guid id, string p, bool replace) => throw new NotSupportedException();
        public void Delete(Guid id) => throw new NotSupportedException();
        public void Flush() { }
        public OverlayStorageObservation Observe() => new(Identity.Id, Identity.Snapshot.Id, 0, 0, 0, 0, 0, 0, 4096, 0, 0);
        public long GetJournalBytes() => 0;
        public OverlayStorageObservation? ObserveActivity() => null;
        public void Dispose() { }
    }
}
