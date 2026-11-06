using Splinter.Core;
using Splinter.Platform.Windows;
using Splinter.Runtime;

namespace Splinter.Tests;

public sealed class JournalSealedReaderCacheTests
{
    [Fact]
    public void AlternatingReadsKeepNativeFencesAndReverifyAfterEviction()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Requires native sealed NTFS identities and sharing.");
        using var fixture = new Fixture();
        using var store = fixture.Open();
        var ids = fixture.Seed(store);
        Verify(store, ids[0], 0);
        Verify(store, ids[1], 1);
        Assert.Throws<IOException>(() => OpenWritable(fixture.Segment(0)).Dispose());
        Assert.Throws<IOException>(() => File.Move(fixture.Segment(0), fixture.Segment(0) + ".moved"));
        Verify(store, ids[0], 0);
        Verify(store, ids[2], 2);
        Verify(store, ids[3], 3);
        Verify(store, ids[4], 4);

        Assert.Throws<IOException>(() => OpenWritable(fixture.Segment(0)).Dispose());
        using (var evicted = OpenWritable(fixture.Segment(1)))
        {
            evicted.Position = evicted.Length - 1;
            int value = evicted.ReadByte();
            evicted.Position--;
            evicted.WriteByte((byte)(value ^ 1));
            evicted.Flush(true);
        }
        Assert.Equal("JournalSegmentCorrupt", Assert.Throws<WorkspaceException>(() => Verify(store, ids[1], 1)).Code);
        Verify(store, ids[0], 0);
    }

    [Fact]
    public void RetirementClosesReadersBeforeUnlinkAndResumesTheSameProtectedPlan()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Requires native sealed NTFS identities and sharing.");
        using var fixture = new Fixture();
        using var store = fixture.Open();
        var ids = fixture.Seed(store);
        Verify(store, ids[0], 0);
        Verify(store, ids[1], 1);
        foreach (var id in ids[..2]) { store.Delete(id); store.ReleaseUnlinkedFile(id); }
        var expected = store.Seal();
        var ct = TestContext.Current.CancellationToken;

        using (var holder = File.Open(fixture.Segment(0), FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            store.MaintainLiveStorage(collectSegments: true, ct: ct);
            Assert.True(File.Exists(fixture.Retirement));
            Assert.True(File.Exists(fixture.Segment(0)));
            Verify(store, ids[2], 2);
        }
        store.MaintainLiveStorage(collectSegments: true, ct: ct);
        Assert.False(File.Exists(fixture.Retirement));
        Assert.False(File.Exists(fixture.Segment(0)));
        Assert.False(File.Exists(fixture.Segment(1)));
        Assert.Equal(expected, store.Seal());
        Verify(store, ids[2], 2);
        store.Dispose();
        using (OpenWritable(fixture.Segment(2))) { }

        using var replay = fixture.Open();
        Assert.Equal(expected, replay.Seal());
        Assert.Null(replay.Stat(Fixture.Name(0)));
        Assert.Null(replay.Stat(Fixture.Name(1)));
        Verify(replay, ids[2], 2);
        Verify(replay, ids[5], 5);
    }

    [Fact]
    public void FrozenCaptureReleasesWarmedHandlesBeforeCompletingNativeAliases()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Requires native sealed NTFS identities and sharing.");
        using var fixture = new Fixture();
        using var store = fixture.Open();
        var ids = fixture.Seed(store);
        Verify(store, ids[0], 0);
        Verify(store, ids[1], 1);
        Assert.Throws<IOException>(() => OpenWritable(fixture.Segment(0)).Dispose());
        var expected = store.Seal();
        string capture = new('a', 24), directory = fixture.Capture(capture);

        store.CaptureSource(directory, capture, []);
        Assert.True(Directory.GetFiles(directory, "*.pin").Length >= 2);
        SourceCaptureTree.Complete(directory, new WindowsJournalFilePins());
        store.CompleteSourceCaptureRequest(capture);
        Assert.Empty(Directory.GetFiles(directory, "*.pin"));
        Assert.Equal(expected, store.Seal());

        Verify(store, ids[0], 0);
        Verify(store, ids[1], 1);
        Assert.Throws<IOException>(() => OpenWritable(fixture.Segment(0)).Dispose());
    }

    private static FileStream OpenWritable(string path) =>
        File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete);

    private static void Verify(JournalDeltaStore store, Guid id, int index)
    {
        byte[] actual = new byte[17];
        Assert.Equal(16, store.Read(id, Fixture.PayloadLength - 16, actual));
        Assert.All(actual[..16], value => Assert.Equal(Fixture.Value(index), value));
        Assert.Equal(0, store.Read(id, Fixture.PayloadLength, actual));
    }

    private sealed class Fixture : IDisposable
    {
        internal const int PayloadLength = 3072;
        private readonly TestDirectory root = new();
        private readonly SnapshotOverlayTests.FakeSnapshotTree basis = new();
        private string Delta => Path.Combine(root.Path, "delta");
        internal string Retirement => Path.Combine(Delta, "segment-reclaim.json");
        internal string Capture(string id) => Path.Combine(root.Path, "captures", id);
        internal string Segment(int index) => Path.Combine(Delta, JournalSegmentStorage.FileName(index));
        internal static string Name(int index) => $"file-{index}";
        internal static byte Value(int index) => checked((byte)(61 + index));
        internal JournalDeltaStore Open() => new(Delta, new(3, "sealed-reader-cache", basis.Snapshot, long.MaxValue, 4096), basis,
            directoryGuardFactory: path => new WindowsJournalDirectoryGuard(path), boundedLogBytes: 4096, sourceFilePins: new WindowsJournalFilePins());
        internal Guid[] Seed(JournalDeltaStore store)
        {
            var ids = new Guid[6];
            for (int index = 0; index < ids.Length; index++)
            {
                ids[index] = store.Create(Name(index), false, 32).Id;
                byte[] bytes = new byte[PayloadLength]; bytes.AsSpan().Fill(Value(index));
                store.Write(ids[index], 0, bytes);
                if (index > 0) Assert.True(File.Exists(Segment(index - 1)));
            }
            return ids;
        }
        public void Dispose() => root.Dispose();
    }
}
