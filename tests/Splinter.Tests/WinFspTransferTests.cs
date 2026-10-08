using System.Runtime.InteropServices;
using Splinter.Core;
using Splinter.Platform.Windows;
using Splinter.Runtime;

namespace Splinter.Tests;

public sealed class WinFspTransferTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(4096)]
    [InlineData(16384)]
    [InlineData(131072)]
    public void RequestSizedBuffersPreserveSmallEmptyAndMultiChunkCallbacks(int length)
    {
        if (!OperatingSystem.IsWindows()) Assert.Skip("Windows file sharing or native contract.");
        using var root = new TestDirectory(); var tree = new SnapshotOverlayTests.FakeSnapshotTree();
        var identity = new OverlayIdentity(2, "transfer", tree.Snapshot, 4 * 1024 * 1024, 65536);
        using var store = new JournalDeltaStore(root.Path, identity, tree);
        Guid id = store.Create("file", false, 32).Id;
        var fs = new WinFspOverlayFileSystem(store);
        byte[] expected = Enumerable.Range(0, length).Select(i => (byte)(i % 251)).ToArray();
        IntPtr buffer = Marshal.AllocHGlobal(Math.Max(1, length));
        try
        {
            Marshal.Copy(expected, 0, buffer, length);
            Assert.Equal(0, fs.Write(id, null!, buffer, 0, (uint)length, false, false, out uint written, out var info));
            Assert.Equal((uint)length, written); Assert.Equal((ulong)length, info.FileSize);
            Marshal.Copy(new byte[length], 0, buffer, length);
            Assert.Equal(0, fs.Read(id, null!, buffer, 0, (uint)length, out uint read)); Assert.Equal(written, read);
            byte[] actual = new byte[length]; Marshal.Copy(buffer, actual, 0, length); Assert.Equal(expected, actual);
            Marshal.Copy(Enumerable.Repeat((byte)255, length).ToArray(), 0, buffer, length);
            Assert.Equal(0, fs.Write(id, null!, buffer, (ulong)length, (uint)length, false, true, out written, out info));
            Assert.Equal(0u, written); Assert.Equal((ulong)length, info.FileSize);
            Assert.Equal(1 + (length + 65535) / 65536, store.Observe().DurableSequence);
        }
        finally { Marshal.FreeHGlobal(buffer); }
    }
}
