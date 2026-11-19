using Splinter.Core;
using Splinter.Platform.Windows;
using Splinter.Runtime;

namespace Splinter.Tests;

public sealed class WriteThroughputOptimizationTests
{
    [Fact(Timeout = 10000)]
    public void MixedAdmissionSeparatesAllCallbacksFromMutationsAfterDurableCompletion()
    {
        var ct = TestContext.Current.CancellationToken; ct.ThrowIfCancellationRequested();
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Native callback contract.");
        using var root = new TestDirectory(); var basis = new SnapshotOverlayTests.FakeSnapshotTree();
        var identity = new OverlayIdentity(3, "mixed-admission", basis.Snapshot, long.MaxValue, 65536);
        using var store = new JournalDeltaStore(root.Path, identity, basis, sourceFilePins: new WindowsJournalFilePins());
        store.Create("file", false, 32);
        var fs = new WinFspOverlayFileSystem(store, null, null, new WinFspOverlayFileSystem.WriteTelemetry());
        Assert.Equal(0, fs.Open("\\file", 0, 0, out var node, out var desc, out _, out _));
        Assert.Equal(0, fs.Open("\\file", 0, 0, out var otherNode, out var otherDesc, out _, out _));
        try { Assert.Equal(0, fs.Overwrite(node, desc, 32, false, 0, out _)); }
        finally { fs.Close(otherNode, otherDesc); fs.Close(node, desc); }
        var observed = Assert.IsType<WinFspMixedAdmission>(fs.ObserveMixedAdmission());
        Assert.Equal(3, observed.Callbacks + observed.IdleOpens); Assert.Equal(2, observed.IdleOpens); Assert.Equal(1, observed.MutatingCallbacks);
        Assert.Equal(0, observed.GatherTicks);
        Assert.Equal(observed.Groups, observed.CallbackBuckets.Sum()); Assert.True(observed.ServiceTicks > 0);
        Assert.True(observed.QueueWaitTicks >= 0); Assert.Equal(0, fs.RetainedFileContexts);
    }
    [Fact(Timeout = 10000)]
    public void MetadataAppendRetainsOldRootsAcrossReadAndFailedHeaderThenRetry()
    {
        var ct = TestContext.Current.CancellationToken; ct.ThrowIfCancellationRequested();
        using var root = new TestDirectory(); bool interrupt = false;
        MetadataTreeRoot first, final;
        using (var pages = new PagedMetadataStore(root.Path, true, point =>
        { if (interrupt && point == "metadata.page-header-written") { interrupt = false; throw new IOException("Injected partial metadata append."); } }))
        {
            first = pages.Set(pages.Empty, "first", new byte[] { 1, 2, 3 }); pages.Flush();
            for (int i = 0; i < 80; i++)
            {
                ct.ThrowIfCancellationRequested();
                var next = pages.Set(first, "temporary-" + i, BitConverter.GetBytes(i));
                Assert.Equal(new byte[] { 1, 2, 3 }, pages.Open(first).Find("first"));
                Assert.Equal(BitConverter.GetBytes(i), pages.Open(next).Find("temporary-" + i));
            }
            interrupt = true; Assert.Throws<IOException>(() => pages.Set(first, "failed", new byte[] { 9 }));
            final = pages.Set(first, "final", new byte[] { 4, 5 }); pages.Flush();
            Assert.True(pages.AppendedNodes > 0); Assert.True(pages.AppendedPageBytes > 0);
        }
        using var reader = new PagedMetadataStore(root.Path);
        Assert.Equal(new byte[] { 1, 2, 3 }, reader.Open(first).Find("first"));
        Assert.Null(reader.Open(first).Find("final")); Assert.Null(reader.Open(final).Find("failed"));
        Assert.Equal(new byte[] { 4, 5 }, reader.Open(final).Find("final"));
    }

    [Fact(Timeout = 10000)]
    public void SharedIndexPersistenceIsCountedAndEveryAcknowledgedFileReplays()
    {
        var ct = TestContext.Current.CancellationToken; ct.ThrowIfCancellationRequested();
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Native journal ownership.");
        using var root = new TestDirectory(); var basis = new SnapshotOverlayTests.FakeSnapshotTree();
        var identity = new OverlayIdentity(3, "index-flush-accounting", basis.Snapshot, long.MaxValue, 65536);
        Guid[] ids;
        using (var writer = new JournalDeltaStore(root.Path, identity, basis, sourceFilePins: new WindowsJournalFilePins()))
        {
            ids = Enumerable.Range(0, 32).Select(i => writer.Create("file" + i, false, 32).Id).ToArray();
            var before = writer.ReadIndexBatchAccounting(); long flushes = writer.Observe().Metrics!.FlushCount;
            writer.ApplyMutationBatch(ids.SelectMany((id, i) => new Action[] {
                () => writer.Write(id, 0, new byte[] { (byte)i, 7 }),
                () => writer.SetMetadata(id, 32, 1, 2, 100 + i) }).ToArray());
            var after = writer.ReadIndexBatchAccounting();
            Assert.Equal(flushes + 1, writer.Observe().Metrics!.FlushCount);
            Assert.True(after.Calls > before.Calls); Assert.True(after.Nodes > before.Nodes);
            Assert.True(after.Bytes > before.Bytes); Assert.True(after.Ticks >= before.Ticks);
        }
        using var replay = new JournalDeltaStore(root.Path, identity, basis, sourceFilePins: new WindowsJournalFilePins());
        for (int i = 0; i < ids.Length; i++)
        { ct.ThrowIfCancellationRequested(); byte[] bytes = new byte[2]; Assert.Equal(2, replay.Read(ids[i], 0, bytes)); Assert.Equal(new byte[] { (byte)i, 7 }, bytes); Assert.Equal(100 + i, replay.Get(ids[i]).LastWriteTime); }
    }
}
