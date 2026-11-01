using Splinter.Core;
using Splinter.Platform.Windows;
using Splinter.Runtime;

namespace Splinter.Tests;

public sealed class OverlayDirectoryEnumerationTests
{
    [Fact]
    public void ClosingAnUnfinishedPagedEnumerationReleasesItsReaderWithoutMaterializingIt()
    {
        if (!OperatingSystem.IsWindows()) Assert.Skip("Windows WinFsp callback contract.");
        using var directory = new TestDirectory(); var tree = new EmptyTree();
        var identity = new OverlayIdentity(2, "stream-directory", tree.Snapshot, 100000, 4096);
        using var underlying = new JournalDeltaStore(Path.Combine(directory.Path, "delta"), identity, tree);
        underlying.Create("first", false, 32); underlying.Create("second", false, 32);
        var store = new StreamingStore(underlying); var fs = new WinFspOverlayFileSystem(store);
        Assert.Equal(0, fs.Open("\\", 0, 0, out var node, out var handle, out _, out _));
        object context = null!;
        Assert.True(fs.ReadDirectoryEntry(node, handle, "*", null!, ref context, out var name, out _));
        Assert.Equal("first", name); Assert.Equal(1, store.Visited); Assert.Equal(0, store.Disposed);
        fs.Close(node, handle);
        Assert.Equal(1, store.Disposed); Assert.Equal(0, fs.RetainedFileContexts);
    }

    [Fact]
    public void EmptyChildDirectoryExposesDotEntriesWithoutChangingTheStore()
    {
        if (!OperatingSystem.IsWindows()) Assert.Skip("Windows WinFsp callback contract.");
        using var directory = new TestDirectory();
        var tree = new EmptyTree();
        var identity = new OverlayIdentity(2, "empty-directory", tree.Snapshot, 100000, 4096);
        using var store = new JournalDeltaStore(Path.Combine(directory.Path, "delta"), identity, tree);
        var child = store.Create("empty", true, (uint)FileAttributes.Directory);
        long sequence = store.Observe().DurableSequence;
        var fileSystem = new WinFspOverlayFileSystem(store);
        object context = null!;

        Assert.True(fileSystem.ReadDirectoryEntry(child.Id, null!, "*", null!, ref context, out var name, out var info));
        Assert.Equal(".", name);
        Assert.Equal((uint)FileAttributes.Directory, info.FileAttributes);
        Assert.True(fileSystem.ReadDirectoryEntry(child.Id, null!, "*", null!, ref context, out name, out info));
        Assert.Equal("..", name);
        Assert.Equal((uint)FileAttributes.Directory, info.FileAttributes);
        Assert.False(fileSystem.ReadDirectoryEntry(child.Id, null!, "*", null!, ref context, out _, out _));
        Assert.False(fileSystem.ReadDirectoryEntry(child.Id, null!, "*", null!, ref context, out _, out _));

        context = null!;
        Assert.True(fileSystem.ReadDirectoryEntry(child.Id, null!, "*", ".", ref context, out name, out _));
        Assert.Equal("..", name);
        Assert.False(fileSystem.ReadDirectoryEntry(child.Id, null!, "*", ".", ref context, out _, out _));
        context = null!;
        Assert.False(fileSystem.ReadDirectoryEntry(child.Id, null!, "*", "..", ref context, out _, out _));

        Assert.Equal(0, fileSystem.GetDirInfoByName(child.Id, null!, ".", out name, out info));
        Assert.Equal(".", name);
        Assert.Equal((uint)FileAttributes.Directory, info.FileAttributes);
        Assert.Equal(0, fileSystem.GetDirInfoByName(child.Id, null!, "..", out name, out info));
        Assert.Equal("..", name);
        Assert.Equal((uint)FileAttributes.Directory, info.FileAttributes);
        Assert.Empty(store.List("empty"));
        Assert.Equal(sequence, store.Observe().DurableSequence);
    }

    private sealed class EmptyTree : ISnapshotTree
    {
        public SnapshotReference Snapshot { get; } = new("empty-directory", "fake-memory", "source", "root",
            DateTimeOffset.UnixEpoch, true, true, "test-only");
        public void EnsureAvailable() { }
        public SnapshotEntry? Stat(string path) => path == "" ? new("", true, 0, 16, 1, 1, 1) : null;
        public IReadOnlyList<SnapshotEntry> List(string path) => [];
        public int Read(string path, long offset, Span<byte> destination) => 0;
    }

    private sealed class StreamingStore(JournalDeltaStore inner) : IWorkspaceDeltaStore
    {
        internal int Visited, Disposed;
        public OverlayIdentity Identity => inner.Identity;
        public DeltaEntry? Stat(string path) => inner.Stat(path);
        public DeltaEntry Get(Guid id) => inner.Get(id);
        public IReadOnlyList<DeltaEntry> List(string path) => throw new InvalidOperationException("Use the streaming directory contract.");
        public IEnumerable<DeltaEntry> EnumerateChildren(string path)
        {
            var fixedMembers = inner.List(path);
            return Items();
            IEnumerable<DeltaEntry> Items()
            {
                try { foreach (var member in fixedMembers) { Visited++; yield return member; } }
                finally { Disposed++; }
            }
        }
        public DeltaEntry Create(string path, bool directory, uint attributes) => inner.Create(path, directory, attributes);
        public int Read(Guid id, long offset, Span<byte> destination) => inner.Read(id, offset, destination);
        public void Write(Guid id, long offset, ReadOnlySpan<byte> bytes) => inner.Write(id, offset, bytes);
        public void SetLength(Guid id, long length) => inner.SetLength(id, length);
        public void SetMetadata(Guid id, uint attributes, long creationTime, long accessTime, long writeTime) => inner.SetMetadata(id, attributes, creationTime, accessTime, writeTime);
        public void SetLengthAndMetadata(Guid id, long length, uint attributes, long creationTime, long accessTime, long writeTime) => inner.SetLengthAndMetadata(id, length, attributes, creationTime, accessTime, writeTime);
        public void Rename(Guid id, string destination, bool replace) => inner.Rename(id, destination, replace);
        public void Delete(Guid id) => inner.Delete(id);
        public void Flush() => inner.Flush();
        public OverlayStorageObservation Observe() => inner.Observe();
        public long GetJournalBytes() => inner.GetJournalBytes();
        public OverlayStorageObservation? ObserveActivity() => inner.ObserveActivity();
        public void Dispose() => inner.Dispose();
    }
}
