using System.Text.Json;
using System.Diagnostics;
using Splinter.Core;
using Splinter.Runtime;

namespace Splinter.Platform.Windows;

public sealed partial class NtfsSnapshotWorkspaceBackend
{
    private static string CapturePath(string control, string id)
    {
        if (id.Length != 24 || id.Any(c => !char.IsAsciiHexDigit(c)))
            throw new WorkspaceException("SourceCaptureRequestInvalid", "Require a fixed owned generation ID.");
        string path = Path.Combine(control, "captures", id); WorkspaceStore.EnsurePlainPath(path); return path;
    }
    public async Task<SourceCaptureIdentity> CaptureSourceAsync(WorkspaceRecord w, string control, string id, CancellationToken ct = default)
    {
        string destination = CapturePath(control, id);
        if (!File.Exists(Path.Combine(destination, "capture.json")))
        {
            var request = await Request(w, control, ct) ?? throw new WorkspaceException("OverlayRequestMissing", "Reconnect this task's provider before capturing source.");
            var observed = await ObserveAsync(w, control, ct);
            if (observed is not { ProviderAlive: true, Mounted: true, OwnershipVerified: true })
                throw new WorkspaceException("OverlayNotReady", "Live source capture requires the exact owned provider.");
            string requestId = Guid.NewGuid().ToString("N");
            await WindowsProcessWorkspaceProvider.WriteAsync(Path.Combine(request.ControlPath, "capture-source.json"),
                new { request.Token, requestId, id, w.ManagedPaths }, ct);
            var providerProbeClock = Stopwatch.StartNew();
            for (long attempt = 0; ; attempt++)
            {
                ct.ThrowIfCancellationRequested();
                var receipt = await WindowsProcessWorkspaceProvider.ReadAsync<JsonElement>(Path.Combine(request.ControlPath, "capture-source-result.json"), ct, waitForPublication: false);
                if (receipt.ValueKind == JsonValueKind.Object && receipt.GetProperty("token").GetString() == request.Token && receipt.GetProperty("requestId").GetString() == requestId)
                {
                    if (receipt.GetProperty("captureError").GetString() is { } error)
                        throw new WorkspaceException(error, "Capture did not complete; preserve its exact request and backing pins.");
                    break;
                }
                // The full owned-provider proof immediately preceded request
                // publication. Keep periodic proofs without repeating it on
                // the first empty response or forcing a 100ms short round trip.
                if (providerProbeClock.Elapsed >= TimeSpan.FromSeconds(1))
                { await RequireCaptureProvider(w, request, ct); providerProbeClock.Restart(); }
                await Task.Delay(attempt < 5 ? 20 : 100, ct);
            }
        }
        var captured = SourceCaptureTree.ReadCapture(destination);
        ValidateCapture(w, captured, id);
        return new(id, captured.Sequence, captured.Digest);
    }
    private static void ValidateCapture(WorkspaceRecord w, JournalSourceCapture captured, string id)
    {
        if (captured.Id != id || !SourceCaptureTree.SameIdentity(captured.Identity, Identity(w)) || !captured.ManagedPaths.SequenceEqual(w.ManagedPaths) ||
            w.Results.SingleOrDefault(r => r.Id == id) is { } result && (result.CaptureDigest != captured.Digest || result.SourceSequence != captured.Sequence))
            throw new WorkspaceException("SourceCaptureIdentityMismatch", "The generation belongs to another workspace request.");
    }
    public async Task<ISourceCaptureTree> OpenSourceCaptureAsync(WorkspaceRecord w, string control, string id, CancellationToken ct = default)
    {
        ValidateCapture(w, SourceCaptureTree.ReadCapture(CapturePath(control, id)), id);
        var request = await Request(w, control, ct) ?? throw new WorkspaceException("OverlayRequestMissing", "Preserve the captured source without its exact basis request.");
        var lease = WorkspaceSnapshotBasis.AcquireReadLease(request.Identity.Snapshot, request.SnapshotReceiptDirectory!, request.SnapshotOwnerId!);
        IDisposable? treeOwner = null;
        OwnedCapturedContentReader? capturedContent = null;
        try
        {
            var tree = WorkspaceSnapshotBasis.OpenCaptureTree(request.Identity.Snapshot, request.ReadAccess,
                request.SnapshotReceiptDirectory, request.SnapshotOwnerId, lease);
            treeOwner = tree;
            capturedContent = new OwnedCapturedContentReader(Path.GetDirectoryName(Path.GetDirectoryName(control)!)!, control, w, new WindowsJournalFilePins());
            return new OwnedSourceCapture(new SourceCaptureTree(CapturePath(control, id), tree, new WindowsJournalFilePins(), capturedContent), tree, lease, capturedContent);
        }
        catch { capturedContent?.Dispose(); treeOwner?.Dispose(); lease.Dispose(); throw; }
    }
    public async Task CompleteSourceCaptureAsync(WorkspaceRecord w, string control, string id, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        ValidateCapture(w, SourceCaptureTree.ReadCapture(CapturePath(control, id)), id);
        bool maintenancePending = File.Exists(Path.Combine(DeltaPath(control), "segment-reclaim.json")) || File.Exists(Path.Combine(DeltaPath(control), "live-repack.json"));
        if ((!File.Exists(Path.Combine(CapturePath(control, id), "materialized.json")) || maintenancePending) && File.Exists(CapturedRangeAdoptionStore.Artifact(control, id)))
        {
            var request = await Request(w, control, ct) ?? throw new WorkspaceException("OverlayRequestMissing", "Retain the fixed range adoption without its provider request.");
            var observed = await ObserveAsync(w, control, ct);
            if (observed is { ProviderAlive: true, Mounted: true, OwnershipVerified: true })
            {
                string requestId = Guid.NewGuid().ToString("N");
                await WindowsProcessWorkspaceProvider.WriteAsync(Path.Combine(request.ControlPath, "complete-source.json"), new { request.Token, requestId, id }, ct);
                var providerProbeClock = Stopwatch.StartNew();
                for (long attempt = 0; ; attempt++)
                {
                    ct.ThrowIfCancellationRequested();
                    var receipt = await WindowsProcessWorkspaceProvider.ReadAsync<JsonElement>(Path.Combine(request.ControlPath, "complete-source-result.json"), ct, waitForPublication: false);
                    if (receipt.ValueKind == JsonValueKind.Object && receipt.GetProperty("token").GetString() == request.Token && receipt.GetProperty("requestId").GetString() == requestId && receipt.GetProperty("id").GetString() == id)
                    {
                        if (receipt.GetProperty("completionError").GetString() is { } error) throw new WorkspaceException(error, "Preserve and resume the exact source adoption request.");
                        return;
                    }
                    if (providerProbeClock.Elapsed >= TimeSpan.FromSeconds(1))
                    { await RequireCaptureProvider(w, request, ct); providerProbeClock.Restart(); }
                    await Task.Delay(attempt < 5 ? 20 : 100, ct);
                }
            }
            if (observed is not { ProviderAlive: false, Mounted: false, OwnershipVerified: true })
                throw new WorkspaceException("SourceCaptureUncertain", "Retain pending adoption until the exact provider lifetime is known.");
            using var readLease = WorkspaceSnapshotBasis.AcquireReadLease(request.Identity.Snapshot, request.SnapshotReceiptDirectory, request.SnapshotOwnerId);
            using var tree = WorkspaceSnapshotBasis.OpenCaptureTree(request.Identity.Snapshot, request.ReadAccess,
                request.SnapshotReceiptDirectory, request.SnapshotOwnerId, readLease);
            using var generatedLease = request.Identity.Graft is { } g
                ? WorkspaceSnapshotBasis.AcquireReadLease(g.Basis.Snapshot, g.Basis.ReceiptDirectory, g.Basis.OwnerId) : null;
            using var generatedTree = request.Identity.Graft is { } gt
                ? WorkspaceSnapshotBasis.OpenTree(gt.Basis.Snapshot, request.GeneratedSnapshot!.ReadAccess, gt.Basis.ReceiptDirectory, gt.Basis.OwnerId) : null;
            ISnapshotTree basis = request.Identity.Graft is { } graft ? new GraftedSnapshotTree(tree, generatedTree!, graft) : tree;
            using var content = CapturedResultReaders.Open(request.Identity, request.DeltaPath);
            using var journal = new JournalDeltaStore(request.DeltaPath, request.Identity, basis, sourceFilePins: new WindowsJournalFilePins(), capturedContentReader: content);
            journal.AdoptCapturedRanges(control, id, ct);
            SourceCaptureTree.Complete(CapturePath(control, id), new WindowsJournalFilePins());
            journal.CompleteSourceCaptureRequest(id);
            journal.MaintainLiveStorage(collectSegments: true, ct: ct);
        }
        SourceCaptureTree.Complete(CapturePath(control, id), new WindowsJournalFilePins());
    }
    private static void ResumeStoppedJournalMaintenance(OverlayServeRequest request, SnapshotDeltaSeal expected, CancellationToken ct)
    {
        // The caller proved this exact provider/mount absent and the complete
        // accepted seal before opening a writer. Resume its existing request;
        // never drop a packing plan merely because the task is being released.
        using var lease = WorkspaceSnapshotBasis.AcquireReadLease(request.Identity.Snapshot, request.SnapshotReceiptDirectory, request.SnapshotOwnerId);
        using var tree = WorkspaceSnapshotBasis.OpenCaptureTree(request.Identity.Snapshot, request.ReadAccess,
            request.SnapshotReceiptDirectory, request.SnapshotOwnerId, lease);
        using var generatedLease = request.Identity.Graft is { } g
            ? WorkspaceSnapshotBasis.AcquireReadLease(g.Basis.Snapshot, g.Basis.ReceiptDirectory, g.Basis.OwnerId) : null;
        using var generatedTree = request.Identity.Graft is { } gt
            ? WorkspaceSnapshotBasis.OpenTree(gt.Basis.Snapshot, request.GeneratedSnapshot!.ReadAccess, gt.Basis.ReceiptDirectory, gt.Basis.OwnerId) : null;
        ISnapshotTree basis = request.Identity.Graft is { } graft ? new GraftedSnapshotTree(tree, generatedTree!, graft) : tree;
        using var content = CapturedResultReaders.Open(request.Identity, request.DeltaPath);
        using var journal = new JournalDeltaStore(request.DeltaPath, request.Identity, basis,
            directoryGuardFactory: path => new WindowsJournalDirectoryGuard(path), sourceFilePins: new WindowsJournalFilePins(), capturedContentReader: content);
        if (journal.Seal() != expected)
            throw new WorkspaceException("ViewSourceChanged", "The stopped journal changed before its exact maintenance request resumed.");
        journal.MaintainLiveStorage(collectSegments: true, ct: ct);
        if (journal.Seal() != expected)
            throw new WorkspaceException("ViewSourceChanged", "Maintenance changed the accepted source generation; preserve its storage.");
    }
    private static async Task RequireCaptureProvider(WorkspaceRecord workspace, OverlayServeRequest request, CancellationToken ct)
    {
        var observation = await WinFspOverlayClient.ObserveAsync(request, ct);
        var mount = workspace.Snapshot?.Mount;
        if (mount is null || !observation.GetProperty("processAlive").GetBoolean() || observation.GetProperty("phase").GetString() != "mounted" ||
            !observation.TryGetProperty("state", out var state) || state.GetProperty("processId").GetInt32() != mount.ProviderProcessId ||
            state.GetProperty("processStartTimeUtcTicks").GetInt64() != mount.ProviderStartTimeUtcTicks)
            throw new WorkspaceException("SourceCaptureUncertain", "Preserve this exact request until its owned provider is reconnected.");
    }
    private sealed class OwnedSourceCapture(SourceCaptureTree captured, IDisposable tree, IDisposable lease, IDisposable capturedContent) : ISourceCaptureTree, ISourceCaptureRanges
    {
        public ISnapshotTree Basis => captured.Basis;
        public SourceCaptureIdentity CaptureIdentity => captured.CaptureIdentity;
        public CapturedFileRangeLayout? GetFileRanges(string relativePath) => captured.GetFileRanges(relativePath);
        public string[]? ChangedPaths => captured.ChangedPaths;
        public IEnumerable<string>? EnumerateChangedPaths() => captured.EnumerateChangedPaths();
        public SnapshotReference Snapshot => captured.Snapshot;
        public bool SupportsConcurrentReads => captured.SupportsConcurrentReads;
        public void EnsureAvailable() => captured.EnsureAvailable();
        public void VerifyReadBoundary() => captured.VerifyReadBoundary();
        public SnapshotEntry? Stat(string path) => captured.Stat(path);
        public IReadOnlyList<SnapshotEntry> List(string path) => captured.List(path);
        public IEnumerable<SnapshotEntry> EnumerateChildren(string path) => captured.EnumerateChildren(path);
        public int Read(string path, long offset, Span<byte> destination) => captured.Read(path, offset, destination);
        public void Dispose()
        {
            try { captured.Dispose(); }
            finally { try { capturedContent.Dispose(); } finally { try { tree.Dispose(); } finally { lease.Dispose(); } } }
        }
    }
}
