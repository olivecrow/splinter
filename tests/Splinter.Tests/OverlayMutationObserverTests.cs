using System.Runtime.InteropServices;
using Splinter.Core;
using Splinter.Platform.Windows;
using Splinter.Runtime;

namespace Splinter.Tests;

public sealed class OverlayMutationObserverTests
{
    [Theory]
    [InlineData("write")]
    [InlineData("rename")]
    [InlineData("replace")]
    [InlineData("truncate")]
    [InlineData("flush")]
    public void CallbackObserverBracketsCompletedEffectsAndSuccessReturn(string operation)
    {
        if (!OperatingSystem.IsWindows()) Assert.Skip("Windows callback composition.");
        using var dir = new TestDirectory(); var tree = new SnapshotOverlayTests.FakeSnapshotTree();
        tree.Files["alpha"] = [1, 2, 3, 4]; tree.Files["beta"] = [8, 8];
        using var store = new JournalDeltaStore(dir.Path, new(1, "callback", tree.Snapshot, 100000, 4096), tree);
        var events = new List<OverlayMutationBoundary>();
        var fileSystem = new WinFspOverlayFileSystem(store, mutationObserver: new Recorder(events));
        Guid id = store.Stat("alpha")!.Id;
        int status;
        if (operation == "write")
        {
            var buffer = Marshal.AllocHGlobal(4);
            try { Marshal.Copy(new byte[] { 7, 6, 5, 4 }, 0, buffer, 4); status = fileSystem.Write(id, null!, buffer, 0, 4, false, false, out uint count, out _); Assert.Equal(4u, count); }
            finally { Marshal.FreeHGlobal(buffer); }
        }
        else if (operation is "rename" or "replace") status = fileSystem.Rename(id, null!, "alpha", operation == "replace" ? "beta" : "gamma", operation == "replace");
        else if (operation == "truncate") status = fileSystem.SetFileSize(id, null!, 2, false, out _);
        else status = fileSystem.Flush(id, null!, out _);
        Assert.Equal(0, status);
        Assert.Equal(new[] { "callback.before-effect", "callback.after-effect" }, events.Select(e => e.Boundary));
        Assert.All(events, e => { Assert.Equal(operation, e.Operation); Assert.Equal("alpha", e.Path); });
        Assert.Equal(operation == "flush" ? 0 : 1, store.Observe().DurableSequence);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, tree.Files["alpha"]);
        if (operation == "write") Assert.Equal(4, events[1].CompletedBytes);
        if (operation == "truncate") Assert.Equal(2, store.Get(id).Length);
        if (operation is "rename" or "replace") { Assert.Null(store.Stat("alpha")); Assert.Equal(id, store.Stat(operation == "replace" ? "beta" : "gamma")!.Id); }
    }

    private sealed class Recorder(List<OverlayMutationBoundary> events) : IOverlayMutationObserver
    { public void Observe(OverlayMutationBoundary boundary) => events.Add(boundary); }
}
