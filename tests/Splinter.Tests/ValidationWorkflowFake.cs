using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Splinter.Core;

namespace Splinter.Tests;

// The live observations model an external owner/resource authority. Provider
// receipts and independent payloads survive construction of a new fake instance.
internal sealed class ValidationWorkflowWorld
{
    public Dictionary<string, ValidationOwner> Owners { get; } = [];
    public Dictionary<string, ValidationWorkerObservation> Workers { get; } = [];
    public Dictionary<string, string> Contents { get; } = [];
    public ValidationResources ViewResources { get; set; } = new(true, 0, 0, 0, false);
    public Dictionary<string, int> Effects { get; } = [];
    public CandidateValidationEvidence? EvidenceOverride { get; set; }
    public bool ValidationSuccess { get; set; } = true;
    public void Count(string action) => Effects[action] = Effects.GetValueOrDefault(action) + 1;
    public ValidationWorkerIdentity SetWorker(string id, ValidationOwner owner, SnapshotReference snapshot, string content, long sequence = 1)
    {
        var worker = new ValidationWorkerIdentity(id, owner, snapshot, ValidationWorkflowFake.Seal(content, sequence));
        Owners[id] = owner; Contents[id] = content;
        Workers[id] = new(worker, new(true, 0, 0, 0, false), true, "source-only-v1");
        return worker;
    }
}

internal sealed class ValidationWorkflowFake(string root, ValidationWorkflowWorld world) : IValidationWorkflowProvider
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public string? LoseReply { get; set; }
    public Action? BeforePublish { get; set; }
    public static SnapshotDeltaSeal Seal(string content, long sequence = 1) =>
        new(sequence, Encoding.UTF8.GetByteCount(content), Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(content))));
    public string PayloadPath(ValidationTarget target) => Path.Combine(root, target.Kind + "-" + target.Id + ".payload");
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
    private static void Quiet(ValidationResources resources) => Check(resources == new ValidationResources(true, 0, 0, 0, false), "ValidationResourcesBusy");
    private void Owned(string resource, ValidationOwner owner) => Check(world.Owners.GetValueOrDefault(resource) == owner, "ValidationOwnerMismatch");

    public Task VerifyOwnerAsync(string projectId, string resourceId, ValidationOwner owner, CancellationToken ct)
    { ct.ThrowIfCancellationRequested(); Owned(resourceId, owner); return Task.CompletedTask; }
    public Task<ValidationWorkerObservation> ObserveWorkerAsync(string projectId, string workspaceId, CancellationToken ct) => Task.FromResult(world.Workers[workspaceId]);
    public Task<CandidatePublicationReceipt?> FindCandidateAsync(CandidatePublicationIntent intent, CancellationToken ct) =>
        Task.FromResult(Find<CandidatePublicationReceipt>("candidate", intent.CandidateId));
    public Task<CandidatePublicationReceipt> PublishAsync(CandidatePublicationIntent intent, CancellationToken ct)
    {
        BeforePublish?.Invoke();
        Owned(intent.Worker.WorkspaceId, intent.Worker.Owner);
        var observation = world.Workers[intent.Worker.WorkspaceId];
        Quiet(observation.Resources);
        Check(observation.Worker == intent.Worker && observation.SnapshotAvailable == true, "ValidationSourceChanged");
        Check(observation.ManagedPathPolicy == intent.ManagedPathPolicy, "ValidationManagedPathsUnsupported");
        var old = Find<CandidatePublicationReceipt>("candidate", intent.CandidateId);
        if (old is not null) { Check(old.Intent == intent, "ValidationReceiptMismatch"); return Task.FromResult(old); }
        Directory.CreateDirectory(root);
        File.WriteAllText(PayloadPath(new("candidate", intent.CandidateId)), world.Contents[intent.Worker.WorkspaceId]);
        var receipt = new CandidatePublicationReceipt(intent, intent.Worker.SourceSeal, "publication:" + intent.CandidateId);
        Save("candidate", intent.CandidateId, receipt); world.Count("publish"); Reply("publish");
        return Task.FromResult(receipt);
    }
    public Task<ValidationViewObservation> ObserveViewAsync(string projectId, string viewId, CancellationToken ct) =>
        Task.FromResult(new ValidationViewObservation(world.Owners[viewId], world.ViewResources));
    public Task<ValidationSelectionReceipt?> FindSelectionAsync(ValidationSelectionIntent intent, CancellationToken ct) =>
        Task.FromResult(Find<ValidationSelectionReceipt>("selection", intent.GenerationId));
    public Task<ValidationSelectionReceipt> SelectAsync(ValidationSelectionIntent intent, CancellationToken ct)
    {
        Owned(intent.Cache.ViewId, intent.Owner); Quiet(world.ViewResources);
        var old = Find<ValidationSelectionReceipt>("selection", intent.GenerationId);
        if (old is not null) { Check(old.Intent == intent, "ValidationReceiptMismatch"); return Task.FromResult(old); }
        string content = File.ReadAllText(PayloadPath(new("candidate", intent.Candidate.Intent.CandidateId)));
        File.WriteAllText(PayloadPath(new("source-generation", intent.GenerationId)), content + "|generated:" + intent.GenerationId);
        var receipt = new ValidationSelectionReceipt(intent, "selection:" + intent.GenerationId);
        Save("selection", intent.GenerationId, receipt); world.Count("select"); Reply("select");
        return Task.FromResult(receipt);
    }
    public Task<CandidateValidationEvidence> ReadValidationAsync(ValidationSelectionReceipt selection, CancellationToken ct) =>
        Task.FromResult(world.EvidenceOverride ?? new("validation:" + selection.Intent.GenerationId,
            selection.Intent.GenerationId, selection.Intent.Candidate.Intent.CandidateId, selection.Intent.Candidate.CandidateSeal,
            Seal(SourceContent(selection.Intent.GenerationId)), world.ValidationSuccess, DateTimeOffset.UtcNow));
    public Task<SnapshotDeltaSeal> SealSourceAsync(ValidationSelectionReceipt selection, CancellationToken ct)
    { Quiet(world.ViewResources); return Task.FromResult(Seal(SourceContent(selection.Intent.GenerationId))); }
    public Task<ValidationIndependentResult?> FindResultAsync(ValidationPreservationIntent intent, CancellationToken ct) =>
        Task.FromResult(Find<ValidationIndependentResult>("result", intent.OperationId));
    public Task<ValidationIndependentResult> PreserveAsync(ValidationPreservationIntent intent, CancellationToken ct)
    {
        var old = Find<ValidationIndependentResult>("result", intent.OperationId);
        if (old is not null) { Check(old.Intent == intent, "ValidationReceiptMismatch"); return Task.FromResult(old); }
        string content = File.ReadAllText(PayloadPath(intent.Artifact.Target));
        Check(Seal(content, intent.Artifact.Seal.DurableSequence) == intent.Artifact.Seal, "ValidationSourceChanged");
        string reference = Path.Combine(root, "independent-" + intent.OperationId + ".payload");
        File.WriteAllText(reference, content);
        var result = new ValidationIndependentResult(intent, reference, Seal(content).JournalHash, true);
        Save("result", intent.OperationId, result); world.Count("preserve"); Reply("preserve");
        return Task.FromResult(result);
    }
    public Task VerifyResultAsync(ValidationIndependentResult result, CancellationToken ct)
    {
        Check(result.IndependentOfSnapshot && File.Exists(result.Reference) && Seal(File.ReadAllText(result.Reference)).JournalHash == result.ContentHash,
            "ValidationResultInvalid");
        return Task.CompletedTask;
    }
    public Task ReleaseAsync(ValidationReleaseIntent intent, CancellationToken ct)
    {
        if (intent.Artifact.Target.Kind == "source-generation") Quiet(world.ViewResources);
        var old = Find<ValidationReleaseIntent>("released", intent.Artifact.Target.Id);
        if (old is not null) { Check(old.Artifact == intent.Artifact && old.Result == intent.Result, "ValidationReceiptMismatch"); return Task.CompletedTask; }
        string path = PayloadPath(intent.Artifact.Target);
        Check(File.Exists(path) && Seal(File.ReadAllText(path), intent.Artifact.Seal.DurableSequence) == intent.Artifact.Seal,
            "ValidationSourceChanged");
        // Fake-only private payloads under the fixture root; no source/cache/snapshot deletion.
        File.Delete(path);
        Save("released", intent.Artifact.Target.Id, intent); world.Count("release"); Reply("release");
        return Task.CompletedTask;
    }

    public Task CloseCacheAsync(string projectId, ValidationCacheIdentity cache, ValidationOwner owner, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested(); Owned(cache.ViewId, owner); Quiet(world.ViewResources);
        var old = Find<ValidationCacheIdentity>("cache-closed", cache.ViewId);
        if (old is not null) { Check(old == cache, "ValidationCacheMismatch"); return Task.CompletedTask; }
        Save("cache-closed", cache.ViewId, cache); world.Count("close-view"); Reply("close-view");
        return Task.CompletedTask;
    }
}
