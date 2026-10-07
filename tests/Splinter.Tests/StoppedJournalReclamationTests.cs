using System.Diagnostics;
using System.Text.Json;
using Splinter.Core;
using Splinter.Platform.Windows;
using Splinter.Runtime;
using Splinter.SourceControl.Git;

namespace Splinter.Tests;

public sealed class StoppedJournalReclamationTests
{
    [Theory]
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
    public async Task FreshAndResumedLatestReclamationRetainAuthorityAndContentGuards(string scenario)
    {
        if (!OperatingSystem.IsWindows()) Assert.Skip("Windows stopped-provider observation.");
        using var root = new TestDirectory(); var ct = TestContext.Current.CancellationToken;
        string source = Path.Combine(root.Path, "source"), control = Path.Combine(root.Path, "control");
        Directory.CreateDirectory(source); Directory.CreateDirectory(control);
        await TestDirectory.Git(source, "init", "-b", "development");
        await TestDirectory.Git(source, "config", "user.name", "Fixture");
        await TestDirectory.Git(source, "config", "user.email", "fixture@example.invalid");
        File.WriteAllText(Path.Combine(source, "file"), "base");
        await TestDirectory.Git(source, "add", "."); await TestDirectory.Git(source, "commit", "-m", "basis");
        string revision = await TestDirectory.Git(source, "rev-parse", "HEAD");
        var provider = new GitObjectSnapshotProvider(Path.Combine(control, "snapshot"), "task", revision);
        var snapshot = await provider.CaptureAsync(new(source, "task", Path.Combine(control, "snapshot")), ct);
        var access = await provider.ResolveReadAccessAsync(snapshot, ct);
        var w = new WorkspaceRecord { Id = "task", RequestKey = "test", ProjectPath = source, Path = Path.Combine(root.Path, "absent-mount"),
            SourceKind = "git", ApplicationKind = "none", StorageMode = "git-snapshot", BaseRevision = revision, ResultRevision = revision,
            BaseReference = "development", SourceReference = "splinter/task",
            Snapshot = new() { Reference = snapshot, MaxJournalBytes = 4 * 1048576 } };
        string delta = Path.Combine(control, "delta"); OverlayStorageObservation storage; SnapshotDeltaSeal seal;
        using (var tree = provider.OpenTree(snapshot, access))
        using (var writer = new JournalDeltaStore(delta, NtfsSnapshotWorkspaceBackend.Identity(w), tree))
        {
            writer.Write(writer.Stat("file")!.Id, 0, [5]);
            seal = writer.Seal(); storage = writer.Observe();
        }
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
            { JournalBytes = storage.JournalBytes + (scenario == "wrong-bytes" ? 1 : 0), DurableSequence = storage.DurableSequence + (scenario == "wrong-sequence" ? 1 : 0) }
        }, ct);
        if (scenario == "missing-request") File.Delete(requestPath);
        if (scenario == "wrong-seal") w.Snapshot.Result = w.Snapshot.Result with { Seal = seal with { JournalHash = new string('0', 64) } };
        if (scenario == "foreign-file") File.WriteAllText(Path.Combine(delta, "foreign.txt"), "retain");
        if (scenario is "retained-intent" or "partial-deletion" or "wrong-intent")
            await WindowsProcessWorkspaceProvider.WriteAsync(Path.Combine(control, "delta-reclaim.json"),
                scenario == "wrong-intent" ? w.Snapshot.Result with { Fingerprint = "other" } : w.Snapshot.Result, ct);
        if (scenario == "partial-deletion")
            Assert.Throws<IOException>(() => LatestJournalReclamation.ReclaimDirectory(delta, seal, ["identity.json"],
                Path.Combine(control, "latest-delta-reclaim.json"), at => { if (at == "object-removed") throw new IOException("Lost deletion reply."); }));
        var backend = new NtfsSnapshotWorkspaceBackend(Path.Combine(root.Path, "unused-runner"));
        string[] retained = Directory.GetFiles(delta, "*", SearchOption.AllDirectories);
        if (scenario is "fresh" or "retained-intent" or "partial-deletion")
        {
            await backend.RemoveDeltaAsync(w, control, ct);
            Assert.False(Directory.Exists(delta)); Assert.Equal(seal, w.Snapshot.Result.Seal);
            await backend.RemoveDeltaAsync(w, control, ct);
        }
        else
        {
            var error = await Assert.ThrowsAsync<WorkspaceException>(() => backend.RemoveDeltaAsync(w, control, ct));
            Assert.Equal(scenario switch
            {
                "missing-request" => "OverlayRequestMissing", "wrong-seal" => "ViewSourceChanged",
                "foreign-file" => "ViewReleaseUncertain", "wrong-intent" => "OverlayResultRequired",
                _ => "DeltaChangedAfterCollection"
            }, error.Code);
            Assert.All(retained, path => Assert.True(File.Exists(path)));
            Assert.False(File.Exists(Path.Combine(control, "latest-delta-reclaim.json")));
        }
    }
}
