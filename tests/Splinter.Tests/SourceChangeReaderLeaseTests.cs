using System.Text.Json;
using Splinter.Core;
using Splinter.Platform.Windows;
using Splinter.Runtime;

namespace Splinter.Tests;

public sealed class SourceChangeReaderLeaseTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public void CopiesAndExplicitRebindingKeepIndependentOwnedReferences()
    {
        using var first = new CountingLease(); using var second = new CountingLease();
        var view = new SourceChangeView("w", "s", "e", 1, []) { ReaderLease = first };
        var clone = view with { }; var rebound = clone with { ReaderLease = second };
        Assert.Equal(3, first.References); Assert.Equal(2, second.References);
        first.Dispose(); second.Dispose();
        view.Dispose(); view.Dispose(); Assert.Equal(1, first.References); Assert.Equal(0, first.Closed);
        Assert.Single(clone.EnumerateChanges()); clone.Dispose(); Assert.Equal(1, first.Closed);
        Assert.Single(rebound.EnumerateChanges()); rebound.Dispose(); rebound.Dispose(); Assert.Equal(1, second.Closed);
        Assert.Throws<ObjectDisposedException>(() => view.EnumerateChanges());
    }

    [Fact]
    public void LastViewCloneClosesTheStoppedNativeReaderBeforeReclamationWithoutGarbageCollection()
    {
        RequireWindows(); using var files = new TestDirectory(); var basis = new SnapshotOverlayTests.FakeSnapshotTree(); var pins = new WindowsJournalFilePins();
        string delta = Path.Combine(files.Path, "delta"); var identity = new OverlayIdentity(3, "view-lease", basis.Snapshot, long.MaxValue, 65536);
        SourceChangeView view;
        using (var writer = new JournalDeltaStore(delta, identity, basis, sourceFilePins: pins))
        { writer.Create("first", false, 32); view = writer.ObserveChanges([]); }
        var clone = view with { }; string directory = view.Index!.Directory;
        Assert.ThrowsAny<IOException>(() => JournalDeltaStore.ReclaimPagedIndices(delta, identity, pins));
        view.Dispose(); view.Dispose(); Assert.Single(clone.EnumerateChanges());
        Assert.ThrowsAny<IOException>(() => JournalDeltaStore.ReclaimPagedIndices(delta, identity, pins));
        Assert.True(Directory.Exists(directory)); clone.Dispose(); clone.Dispose();
        JournalDeltaStore.ReclaimPagedIndices(delta, identity, pins); Assert.False(Directory.Exists(directory));
        GC.KeepAlive(view); GC.KeepAlive(clone);
    }

    [Fact]
    public void AnAdmittedCursorRetainsItsReaderAfterItsViewCloses()
    {
        RequireWindows(); using var files = new TestDirectory(); var basis = new SnapshotOverlayTests.FakeSnapshotTree(); var pins = new WindowsJournalFilePins();
        string delta = Path.Combine(files.Path, "delta"); var identity = new OverlayIdentity(3, "cursor-lease", basis.Snapshot, long.MaxValue, 65536);
        SourceChangeView view;
        using (var writer = new JournalDeltaStore(delta, identity, basis, sourceFilePins: pins))
        { writer.Create("first", false, 32); writer.Create("second", false, 32); view = writer.ObserveChanges([]); }
        using var cursor = view.EnumerateChanges().GetEnumerator(); Assert.True(cursor.MoveNext()); view.Dispose();
        Assert.ThrowsAny<IOException>(() => JournalDeltaStore.ReclaimPagedIndices(delta, identity, pins));
        Assert.True(cursor.MoveNext()); Assert.False(cursor.MoveNext()); cursor.Dispose();
        JournalDeltaStore.ReclaimPagedIndices(delta, identity, pins);
    }

    [Fact]
    public void AClonedHeaderCannotUseItsRetainedReaderForAnotherGeneration()
    {
        RequireWindows(); using var files = new TestDirectory(); var basis = new SnapshotOverlayTests.FakeSnapshotTree();
        using var writer = new JournalDeltaStore(Path.Combine(files.Path, "delta"), new(3, "bound-view", basis.Snapshot, long.MaxValue, 65536), basis);
        writer.Create("first", false, 32); using var view = writer.ObserveChanges([]);
        using var changed = view with { SnapshotId = "another-basis" };
        Assert.Throws<WorkspaceException>(() => changed.EnumerateChanges().ToArray()); Assert.Single(view.EnumerateChanges());
    }

    [Fact]
    public void WireHeadersOwnNoReaderAndBindingCreatesASeparateNativeOwner()
    {
        RequireWindows(); using var files = new TestDirectory(); var basis = new SnapshotOverlayTests.FakeSnapshotTree();
        string delta = Path.Combine(files.Path, "delta"); var identity = new OverlayIdentity(3, "wire-view", basis.Snapshot, long.MaxValue, 65536);
        using var writer = new JournalDeltaStore(delta, identity, basis); writer.Create("first", false, 32);
        using var view = writer.ObserveChanges([]); string json = JsonSerializer.Serialize(view, Json);
        Assert.DoesNotContain("readerLease", json, StringComparison.Ordinal); Assert.DoesNotContain("changesReader", json, StringComparison.Ordinal);
        using var wire = JsonSerializer.Deserialize<SourceChangeView>(json, Json)!;
        Assert.Null(wire.ReaderLease); Assert.Equal("SourceObservationUnbound", Assert.Throws<WorkspaceException>(() => wire.EnumerateChanges()).Code);
        using var bound = JournalDeltaStore.BindChangeView(wire, delta, identity); view.Dispose(); wire.Dispose();
        Assert.Single(bound.EnumerateChanges());
    }

    [Fact]
    public void DurableResponseAndClientReaderBridgeIndexRepackingWithoutAFinalizer()
    {
        RequireWindows(); using var files = new TestDirectory(); var basis = new SnapshotOverlayTests.FakeSnapshotTree(); var pins = new WindowsJournalFilePins(); var ct = TestContext.Current.CancellationToken;
        string delta = Path.Combine(files.Path, "delta"), overlay = Path.Combine(files.Path, "overlay"); Directory.CreateDirectory(overlay);
        var identity = new OverlayIdentity(3, "response-handoff", basis.Snapshot, long.MaxValue, 65536);
        using var writer = new JournalDeltaStore(delta, identity, basis, sourceFilePins: pins); writer.Create("first", false, 32);
        var server = writer.ObserveChanges([]); string old = server.Index!.Directory;
        string response = Path.Combine(overlay, "source-response.json");
        File.WriteAllText(response, JsonSerializer.Serialize(new { view = server }, Json));
        string wireText = JsonSerializer.Serialize(server, Json); server.Dispose();
        writer.CollectLiveMetadata(force: true, ct: ct); Assert.True(Directory.Exists(old));
        using var wire = JsonSerializer.Deserialize<SourceChangeView>(wireText, Json)!;
        using var client = JournalDeltaStore.BindChangeView(wire, delta, identity); Assert.Single(client.EnumerateChanges());
        using (var next = writer.ObserveChanges([])) File.WriteAllText(response, JsonSerializer.Serialize(new { view = next }, Json));
        writer.MaintainLiveStorage(force: true, ct: ct); Assert.True(Directory.Exists(old));
        client.Dispose(); writer.MaintainLiveStorage(force: true, ct: ct); Assert.False(Directory.Exists(old));
        GC.KeepAlive(server); GC.KeepAlive(client);
    }

    private static void RequireWindows()
    { if (!OperatingSystem.IsWindows()) Assert.Skip("Exact native immutable-index reader and reclamation leases."); }
    private sealed class CountingLease : ISourceChangeReaderLease
    {
        private sealed class State { public int References = 1; public int Closed; }
        private readonly State state; private int disposed;
        public CountingLease() => state = new();
        private CountingLease(State state) { this.state = state; Interlocked.Increment(ref state.References); }
        public int References => state.References;
        public int Closed => state.Closed;
        public ISourceChangeReaderLease Retain()
        { ObjectDisposedException.ThrowIf(disposed != 0, this); return new CountingLease(state); }
        public IEnumerable<SourcePathChange> Read(SourceChangeView view)
        { ObjectDisposedException.ThrowIf(disposed != 0, this); return [new("first", 1)]; }
        public void Dispose()
        { if (Interlocked.Exchange(ref disposed, 1) == 0 && Interlocked.Decrement(ref state.References) == 0) Interlocked.Increment(ref state.Closed); }
    }
}
