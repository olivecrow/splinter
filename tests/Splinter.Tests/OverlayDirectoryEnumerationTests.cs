using Splinter.Core;
using Splinter.Platform.Windows;
using Splinter.Runtime;

namespace Splinter.Tests;

public sealed class OverlayDirectoryEnumerationTests
{
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
}
