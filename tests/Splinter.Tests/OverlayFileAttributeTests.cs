using System.Security.Cryptography;
using Splinter.Core;
using Splinter.Platform.Windows;
using Splinter.Runtime;

namespace Splinter.Tests;

public sealed class OverlayFileAttributeTests
{
    [Theory]
    [InlineData(FileAttributes.ReparsePoint)]
    [InlineData(FileAttributes.Encrypted)]
    [InlineData(FileAttributes.Compressed)]
    [InlineData(FileAttributes.SparseFile)]
    public void RejectedMutationsPreserveBytesMetadataAndJournal(FileAttributes unsupported)
    {
        if (!OperatingSystem.IsWindows()) Assert.Skip("Windows overlay callback contract.");
        using var directory = new TestDirectory();
        var tree = new TestTree();
        var identity = new OverlayIdentity(1, "attributes", tree.Snapshot, 100000, 4096);
        string delta = Path.Combine(directory.Path, "delta");
        using var store = new JournalDeltaStore(delta, identity, tree);
        var fileSystem = new WinFspOverlayFileSystem(store);
        var file = store.Stat("source")!;
        store.Write(file.Id, 1, new byte[] { 9 });
        var expected = store.Get(file.Id);
        string journal = Path.Combine(delta, "delta.journal");
        byte[] ReadJournalHash()
        {
            using var reader = new FileStream(journal, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return SHA256.HashData(reader);
        }
        byte[] before = ReadJournalHash();
        long sequence = store.Observe().DurableSequence;
        uint attributes = (uint)(FileAttributes.Archive | unsupported);

        void Reject(Action mutation)
        {
            var error = Assert.Throws<WorkspaceException>(mutation);
            Assert.Equal("SnapshotFileUnsupported", error.Code);
            Assert.Equal(unchecked((int)0xc00000bb), fileSystem.ExceptionHandler(error));
            Assert.Equal(expected, store.Get(file.Id));
            Assert.Equal(before, ReadJournalHash());
            Assert.Equal(sequence, store.Observe().DurableSequence);
            byte[] bytes = new byte[4];
            Assert.Equal(4, store.Read(file.Id, 0, bytes));
            Assert.Equal(new byte[] { 1, 9, 3, 4 }, bytes);
        }

        Reject(() => fileSystem.Create("new", 0, 0, attributes, [], 0, out _, out _, out _, out _));
        Assert.Null(store.Stat("new")); Assert.Equal(0, fileSystem.OpenHandles);
        Reject(() => fileSystem.Overwrite(file.Id, null!, attributes, true, 0, out _));
        Reject(() => fileSystem.Overwrite(file.Id, null!, attributes, false, 0, out _));
        Reject(() => fileSystem.SetBasicInfo(file.Id, null!, attributes, 0, 0, 0, 0, out _));
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, tree.Bytes);
    }

    [Fact]
    public void OrdinaryAttributesAndNoChangeSentinelSurviveReconnect()
    {
        if (!OperatingSystem.IsWindows()) Assert.Skip("Windows overlay callback contract.");
        using var directory = new TestDirectory(); var tree = new TestTree();
        var identity = new OverlayIdentity(1, "attributes", tree.Snapshot, 100000, 4096);
        string delta = Path.Combine(directory.Path, "delta");
        uint attributes = (uint)(FileAttributes.Archive | FileAttributes.Hidden | FileAttributes.ReadOnly);
        using (var store = new JournalDeltaStore(delta, identity, tree))
        {
            var fileSystem = new WinFspOverlayFileSystem(store); var id = store.Stat("source")!.Id;
            Assert.Equal(0, fileSystem.SetBasicInfo(id, null!, attributes, 0, 0, 0, 0, out var info));
            Assert.Equal(attributes, info.FileAttributes);
            Assert.Equal(0, fileSystem.SetBasicInfo(id, null!, uint.MaxValue, 0, 0, 123, 0, out info));
            Assert.Equal(attributes, info.FileAttributes); Assert.Equal(123UL, info.LastWriteTime);
        }
        using var resumed = new JournalDeltaStore(delta, identity, tree);
        var entry = resumed.Stat("source")!;
        Assert.Equal(attributes, entry.Attributes); Assert.Equal(123, entry.LastWriteTime);
        byte[] bytes = new byte[4]; Assert.Equal(4, resumed.Read(entry.Id, 0, bytes));
        Assert.Equal(tree.Bytes, bytes);
    }

    private sealed class TestTree : ISnapshotTree
    {
        public SnapshotReference Snapshot { get; } = new("attribute-fixture", "fake-memory", "source", "root", DateTimeOffset.UnixEpoch, true, true, "test-only");
        public byte[] Bytes { get; } = [1, 2, 3, 4];
        public void EnsureAvailable() { }
        public SnapshotEntry? Stat(string path) => path switch
        {
            "" => new("", true, 0, 16, 1, 1, 1),
            "source" => new("source", false, Bytes.Length, 32, 1, 1, 1),
            _ => null
        };
        public IReadOnlyList<SnapshotEntry> List(string path) => path == "" ? [Stat("source")!] : [];
        public int Read(string path, long offset, Span<byte> destination)
        {
            int count = (int)Math.Min(destination.Length, Math.Max(0, Bytes.Length - offset));
            Bytes.AsSpan((int)offset, count).CopyTo(destination); return count;
        }
    }
}
