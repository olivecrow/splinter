using System.Diagnostics;
using System.Text.Json;
using Splinter.Core;
using Splinter.Platform.Windows;
using Splinter.Runtime;
using Splinter.SourceControl.Git;

namespace Splinter.Tests;

public sealed class StoppedJournalReclamationTests(StoppedJournalBasisFixture basis) : IClassFixture<StoppedJournalBasisFixture>
{
    [Theory(Timeout = 10000)]
    [InlineData("fresh")]
    [InlineData("retained-intent")]
    [InlineData("partial-deletion")]
    [InlineData("missing-request")]
    [InlineData("missing-storage")]
    [InlineData("wrong-bytes")]
    [InlineData("wrong-sequence")]
    [InlineData("wrong-seal")]
    [InlineData("foreign-file")]
    [InlineData("wrong-intent")]
    public async Task FreshAndResumedCurrentReclamationRetainAuthorityAndContentGuards(string scenario)
    {
        if (!OperatingSystem.IsWindows()) Assert.Skip("Windows stopped-provider observation.");
        long clock = Stopwatch.GetTimestamp();
        void Mark(string phase)
        { TestContext.Current.TestOutputHelper!.WriteLine($"{phase}: {Stopwatch.GetElapsedTime(clock).TotalSeconds:F6}s"); clock = Stopwatch.GetTimestamp(); }
        using var root = new TestDirectory(); var ct = TestContext.Current.CancellationToken;
        string source = basis.Source, control = Path.Combine(root.Path, "control"), revision = basis.Revision;
        Directory.CreateDirectory(control);
        using var attachment = basis.Attach(control);
        var provider = new GitObjectSnapshotProvider(Path.Combine(control, "snapshot"), "task");
        var snapshot = basis.Snapshot;
        var access = await provider.ResolveReadAccessAsync(snapshot, ct);
        Mark("basis-reconnect");
        var w = new WorkspaceRecord { Id = "task", RequestKey = "test", ProjectPath = source, Path = Path.Combine(root.Path, "absent-mount"),
            SourceKind = "git", ApplicationKind = "none", StorageMode = "git-snapshot", BaseRevision = revision, ResultRevision = revision,
            BaseReference = "development", SourceReference = "splinter/task",
            Snapshot = new() { Reference = snapshot, MaxJournalBytes = 4 * 1048576 } };
        string delta = Path.Combine(control, "delta"); OverlayStorageObservation storage; SnapshotDeltaSeal seal;
        using (var reader = provider.AcquireReadLease())
        using (var tree = provider.OpenReadScope(snapshot, access, reader))
        using (var writer = new JournalDeltaStore(delta, NtfsSnapshotWorkspaceBackend.Identity(w), tree, sourceFilePins: new WindowsJournalFilePins()))
        {
            writer.Write(writer.Stat("file")!.Id, 0, [5]);
            seal = writer.Seal(); storage = writer.Observe();
            tree.VerifyReadBoundary();
        }
        Mark("journal-seed");
        w.Snapshot.Result = new(snapshot.Id, revision, "fingerprint", "result", seal, true, SnapshotResult.StableJournalProtocol);
        var request = new OverlayServeRequest(NtfsSnapshotWorkspaceBackend.Identity(w), delta, w.Path,
            Path.Combine(control, "overlay"), "fixture-token", access, Path.Combine(control, "snapshot"), w.Id);
        string requestPath = Path.Combine(request.ControlPath, "request.json");
        Directory.CreateDirectory(request.ControlPath);
        await WindowsProcessWorkspaceProvider.WriteAsync(requestPath, request, ct);
        using var process = Process.GetCurrentProcess();
        await WindowsProcessWorkspaceProvider.WriteAsync(Path.Combine(request.ControlPath, "state.json"), new
        {
            id = w.Id, request.Token, snapshotId = snapshot.Id, phase = "unmounted", processId = process.Id,
            processStartTimeUtcTicks = process.StartTime.ToUniversalTime().Ticks - 1, openHandles = 0, observedAt = DateTimeOffset.UtcNow,
            storage = scenario == "missing-storage" ? null : storage with
            { SealJournalBytes = scenario == "wrong-bytes" ? (storage.SealJournalBytes ?? storage.JournalBytes) + 1 : storage.SealJournalBytes,
                DurableSequence = storage.DurableSequence + (scenario == "wrong-sequence" ? 1 : 0) }
        }, ct);
        if (scenario == "missing-request") File.Delete(requestPath);
        if (scenario == "wrong-seal") w.Snapshot.Result = w.Snapshot.Result with { Seal = seal with { JournalHash = new string('0', 64) } };
        if (scenario == "foreign-file") File.WriteAllText(Path.Combine(delta, "foreign.txt"), "retain");
        if (scenario is "retained-intent" or "partial-deletion" or "wrong-intent")
            await WindowsProcessWorkspaceProvider.WriteAsync(Path.Combine(control, "delta-reclaim.json"),
                scenario == "wrong-intent" ? w.Snapshot.Result with { Fingerprint = "other" } : w.Snapshot.Result, ct);
        if (scenario == "partial-deletion")
            Assert.Throws<IOException>(() => CurrentJournalReclamation.ReclaimDirectory(delta, seal, ["identity.json"],
                Path.Combine(control, "bounded-delta-reclaim.bin"), at => { if (at == "journal-removed") throw new IOException("Lost deletion reply."); }, filePins: new WindowsJournalFilePins()));
        var backend = new NtfsSnapshotWorkspaceBackend(Path.Combine(root.Path, "unused-runner"));
        string[] retained = Directory.GetFiles(delta, "*", SearchOption.AllDirectories);
        Mark("control-fixture");
        if (scenario is "fresh" or "retained-intent" or "partial-deletion")
        {
            await backend.RemoveDeltaAsync(w, control, ct);
            Mark("first-reclaim");
            Assert.False(Directory.Exists(delta)); Assert.Equal(seal, w.Snapshot.Result.Seal);
            await backend.RemoveDeltaAsync(w, control, ct);
            Mark("completed-retry");
        }
        else
        {
            var error = await Assert.ThrowsAsync<WorkspaceException>(() => backend.RemoveDeltaAsync(w, control, ct));
            Assert.Equal(scenario switch
            {
                "missing-request" => "OverlayRequestMissing", "wrong-seal" => "ViewSourceChanged",
                "foreign-file" => "ViewReceiptMismatch", "wrong-intent" => "OverlayResultRequired",
                _ => "DeltaChangedAfterCollection"
            }, error.Code);
            Assert.All(retained, path => Assert.True(File.Exists(path)));
            Assert.False(File.Exists(Path.Combine(control, "latest-delta-reclaim.json")));
        }
    }
}
