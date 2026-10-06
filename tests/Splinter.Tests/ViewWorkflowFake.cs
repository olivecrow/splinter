using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Splinter.Core;

namespace Splinter.Tests;

// The live observations model an external owner/resource authority. Provider
// receipts and independent payloads survive construction of a new fake instance.
internal sealed class ViewWorkflowWorld
{
    public Dictionary<string, ViewOwner> Owners { get; } = [];
    public Dictionary<string, ViewWorkerObservation> Workers { get; } = [];
    public Dictionary<string, string> Contents { get; } = [];
    public ViewResources ViewResources { get; set; } = new(true, 0, 0, 0, false);
    public Dictionary<string, int> Effects { get; } = [];
    public void Count(string action) => Effects[action] = Effects.GetValueOrDefault(action) + 1;
    public ViewWorkerIdentity SetWorker(string id, ViewOwner owner, SnapshotReference snapshot, string content, long sequence = 1)
    {
        var worker = new ViewWorkerIdentity(id, owner, snapshot, ViewWorkflowFake.Seal(content, sequence));
        Owners[id] = owner; Contents[id] = content;
        Workers[id] = new(worker, new(true, 0, 0, 0, false), true, "source-only-v1");
        return worker;
    }
}

internal sealed class ViewWorkflowFake(string root, ViewWorkflowWorld world) : IViewWorkflowProvider
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public string? LoseReply { get; set; }
    public Action? BeforePublish { get; set; }
    public static SnapshotDeltaSeal Seal(string content, long sequence = 1) =>
        new(sequence, Encoding.UTF8.GetByteCount(content), Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(content))));
    public string PayloadPath(ViewTarget target) => Path.Combine(root, target.Kind + "-" + target.Id + ".payload");
    public string SourceContent(string id) => File.ReadAllText(PayloadPath(new("source-generation", id)));
    private string ReceiptPath(string kind, string id) => Path.Combine(root, kind + "-" + id + ".json");
    private T? Find<T>(string kind, string id) where T : class => File.Exists(ReceiptPath(kind, id))
        ? JsonSerializer.Deserialize<T>(File.ReadAllText(ReceiptPath(kind, id)), Json) : null;
    private void Save<T>(string kind, string id, T value)
    {
        Directory.CreateDirectory(root);
        using var file = new FileStream(ReceiptPath(kind, id), FileMode.CreateNew, FileAccess.Write, FileShare.None);
        JsonSerializer.Serialize(file, value, Json); file.Flush(true);
    }
    private void Reply(string kind)
    { if (LoseReply == kind) { LoseReply = null; throw new IOException("Simulated lost provider reply after durable receipt."); } }
    private static void Check(bool condition, string code)
    { if (!condition) throw new WorkspaceException(code, "Fake rejected an unqualified effect."); }
    private static void Quiet(ViewResources resources) => Check(resources == new ViewResources(true, 0, 0, 0, false), "ViewResourcesBusy");
    private void Owned(string resource, ViewOwner owner) => Check(world.Owners.GetValueOrDefault(resource) == owner, "ViewOwnerMismatch");

    public Task VerifyOwnerAsync(string projectId, string resourceId, ViewOwner owner, CancellationToken ct)
    { ct.ThrowIfCancellationRequested(); Owned(resourceId, owner); return Task.CompletedTask; }
    public Task<ViewWorkerObservation> ObserveWorkerAsync(string projectId, string workspaceId, CancellationToken ct) => Task.FromResult(world.Workers[workspaceId]);
    public Task<CandidatePublicationReceipt?> FindCandidateAsync(CandidatePublicationIntent intent, CancellationToken ct) =>
        Task.FromResult(Find<CandidatePublicationReceipt>("candidate", intent.CandidateId));
    public Task<CandidatePublicationReceipt> PublishAsync(CandidatePublicationIntent intent, CancellationToken ct)
    {
        BeforePublish?.Invoke();
        Owned(intent.Worker.WorkspaceId, intent.Worker.Owner);
        var observation = world.Workers[intent.Worker.WorkspaceId];
        Quiet(observation.Resources);
        Check(observation.Worker == intent.Worker && observation.SnapshotAvailable == true, "ViewSourceChanged");
        Check(observation.ManagedPathPolicy == intent.ManagedPathPolicy, "ViewManagedPathsUnsupported");
        var old = Find<CandidatePublicationReceipt>("candidate", intent.CandidateId);
        if (old is not null) { Check(old.Intent == intent, "ViewReceiptMismatch"); return Task.FromResult(old); }
        Directory.CreateDirectory(root);
        File.WriteAllText(PayloadPath(new("candidate", intent.CandidateId)), world.Contents[intent.Worker.WorkspaceId]);
        var receipt = new CandidatePublicationReceipt(intent, intent.Worker.SourceSeal, "publication:" + intent.CandidateId);
        Save("candidate", intent.CandidateId, receipt); world.Count("publish"); Reply("publish");
        return Task.FromResult(receipt);
    }
    public Task<SourceViewObservation> ObserveViewAsync(string projectId, string viewId, CancellationToken ct) =>
        Task.FromResult(new SourceViewObservation(world.Owners[viewId], world.ViewResources));
    public Task<ViewSelectionReceipt?> FindSelectionAsync(ViewSelectionIntent intent, CancellationToken ct) =>
        Task.FromResult(Find<ViewSelectionReceipt>("selection", intent.GenerationId));
    public Task<ViewSelectionReceipt> SelectAsync(ViewSelectionIntent intent, CancellationToken ct)
    {
        Owned(intent.Cache.ViewId, intent.Owner); Quiet(world.ViewResources);
        var old = Find<ViewSelectionReceipt>("selection", intent.GenerationId);
        if (old is not null) { Check(old.Intent == intent, "ViewReceiptMismatch"); return Task.FromResult(old); }
        string content = File.ReadAllText(PayloadPath(new("candidate", intent.Candidate.Intent.CandidateId)));
        File.WriteAllText(PayloadPath(new("source-generation", intent.GenerationId)), content + "|generated:" + intent.GenerationId);
        var receipt = new ViewSelectionReceipt(intent, "selection:" + intent.GenerationId);
        Save("selection", intent.GenerationId, receipt); world.Count("select"); Reply("select");
        return Task.FromResult(receipt);
    }
    public Task<SnapshotDeltaSeal> SealSourceAsync(ViewSelectionReceipt selection, CancellationToken ct)
    { Quiet(world.ViewResources); return Task.FromResult(Seal(SourceContent(selection.Intent.GenerationId))); }
    public Task<ViewIndependentResult?> FindResultAsync(ViewPreservationIntent intent, CancellationToken ct) =>
        Task.FromResult(Find<ViewIndependentResult>("result", intent.OperationId));
    public Task<ViewIndependentResult> PreserveAsync(ViewPreservationIntent intent, CancellationToken ct)
    {
        var old = Find<ViewIndependentResult>("result", intent.OperationId);
        if (old is not null) { Check(old.Intent == intent, "ViewReceiptMismatch"); return Task.FromResult(old); }
        string content = File.ReadAllText(PayloadPath(intent.Artifact.Target));
        Check(Seal(content, intent.Artifact.Seal.DurableSequence) == intent.Artifact.Seal, "ViewSourceChanged");
        string reference = Path.Combine(root, "independent-" + intent.OperationId + ".payload");
        File.WriteAllText(reference, content);
        var result = new ViewIndependentResult(intent, reference, Seal(content).JournalHash, true);
        Save("result", intent.OperationId, result); world.Count("preserve"); Reply("preserve");
        return Task.FromResult(result);
    }
    public Task VerifyResultAsync(ViewIndependentResult result, CancellationToken ct)
    {
        Check(result.IndependentOfSnapshot && File.Exists(result.Reference) && Seal(File.ReadAllText(result.Reference)).JournalHash == result.ContentHash,
            "ViewResultInvalid");
        return Task.CompletedTask;
    }
    public Task ReleaseAsync(ViewReleaseIntent intent, CancellationToken ct)
    {
        if (intent.Artifact.Target.Kind == "source-generation") Quiet(world.ViewResources);
        var old = Find<ViewReleaseIntent>("released", intent.Artifact.Target.Id);
        if (old is not null) { Check(old.Artifact == intent.Artifact && old.Result == intent.Result, "ViewReceiptMismatch"); return Task.CompletedTask; }
        string path = PayloadPath(intent.Artifact.Target);
        Check(File.Exists(path) && Seal(File.ReadAllText(path), intent.Artifact.Seal.DurableSequence) == intent.Artifact.Seal,
            "ViewSourceChanged");
        // Fake-only private payloads under the fixture root; no source/cache/snapshot deletion.
        File.Delete(path);
        Save("released", intent.Artifact.Target.Id, intent); world.Count("release"); Reply("release");
        return Task.CompletedTask;
    }

    public Task CloseCacheAsync(string projectId, ViewCacheIdentity cache, ViewOwner owner, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested(); Owned(cache.ViewId, owner); Quiet(world.ViewResources);
        var old = Find<ViewCacheIdentity>("cache-closed", cache.ViewId);
        if (old is not null) { Check(old == cache, "ViewCacheMismatch"); return Task.CompletedTask; }
        Save("cache-closed", cache.ViewId, cache); world.Count("close-view"); Reply("close-view");
        return Task.CompletedTask;
    }
}
