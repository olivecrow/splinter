using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Splinter.Core;
using Splinter.Runtime;
using static Splinter.SourceControl.Files.PlasticRevisionClient;

namespace Splinter.SourceControl.Files;

public sealed class PlasticRevisionSnapshotProvider(string directory, string owner, string? treeSpec = null, IPlasticRevisionClient? client = null) : ISourceSnapshotProvider
{
    public const string ProviderName = "plastic-revisions", Consistency = "recorded-project-changeset-raw-revisions";
    private readonly IPlasticRevisionClient revisions = client ?? new PlasticRevisionClient();
    private string ReceiptPath => Path.Combine(directory, "plastic-basis.json");
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
    public async Task<SnapshotReference> CaptureAsync(SnapshotCapture request, CancellationToken ct = default)
    {
        if (request.OwnerId != owner || !WorkspaceStore.SamePath(request.ReceiptDirectory, directory) || string.IsNullOrEmpty(treeSpec))
            throw Failure("SnapshotIdentityMismatch", "The exact basis owner/directory and recorded tree are required.");
        RequireTreeSpec(treeSpec); WorkspaceStore.EnsurePlainPath(directory); Directory.CreateDirectory(directory);
        using var operation = Fence("operation.lock", true); var receipt = Read();
        if (receipt is null)
        {
            var manifest = await revisions.ListAsync(treeSpec, ct); ValidateManifest(manifest);
            if (manifest.TreeSpec != treeSpec) throw Failure("PlasticBasisIdentityMismatch", "The reader must return the exact requested tree.");
            string digest = Digest(manifest);
            var snapshot = new SnapshotReference("plastic-" + owner + "-" + digest, ProviderName, Path.GetFullPath(request.SourcePath),
                Path.GetFullPath(directory), DateTimeOffset.UtcNow, true, true, Consistency);
            receipt = new(1, owner, snapshot, manifest, digest, false); Save(receipt);
        }
        Require(receipt, receipt.Snapshot);
        if (receipt.Released || receipt.Manifest.TreeSpec != treeSpec || !WorkspaceStore.SamePath(receipt.Snapshot.SourcePath, request.SourcePath))
            throw Failure("SnapshotIdentityMismatch", "Never replace this task's recorded basis.");
        var live = await revisions.ListAsync(treeSpec, ct); ValidateManifest(live);
        if (Digest(live) != receipt.ManifestDigest) throw Failure("PlasticBasisInvalid", "The fixed tree listing changed; preserve the receipt.");
        return receipt.Snapshot;
    }
    public Task<SnapshotObservation> ObserveAsync(SnapshotReference snapshot, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        try
        {
            var receipt = Required(snapshot);
            // This is the availability of the owned fixed manifest. Content
            // access is separately verified by each raw revision stream.
            return Task.FromResult(receipt.Released ? new SnapshotObservation(snapshot.Id, false, "SnapshotMissing", 0, null) :
                new(snapshot.Id, true, null, 0, null) { ReadAccess = new(snapshot.Id, snapshot.RootPath, receipt.ManifestDigest) });
        }
        catch (WorkspaceException ex) { return Task.FromResult(new SnapshotObservation(snapshot.Id, null, ex.Code, null, null)); }
    }
    public async Task<SnapshotReadAccess> ResolveReadAccessAsync(SnapshotReference snapshot, CancellationToken ct = default)
    {
        var observed = await ObserveAsync(snapshot, ct);
        return observed.ReadAccess ?? throw Failure(observed.FailureCode ?? "SnapshotObservationUnknown", "Retain an unavailable revision basis.");
    }
    public IDisposable AcquireReadLease() => new ManifestLease(this);
    private sealed class ManifestLease : IDisposable
    {
        private readonly FileStream readers; private readonly FileStream manifest;
        public ManifestLease(PlasticRevisionSnapshotProvider provider)
        {
            readers = provider.Fence("readers.lock", false);
            try
            {
                WorkspaceStore.EnsurePlainPath(provider.ReceiptPath);
                // Pin immutable metadata against replacement/writes, as for the
                // common owned-directory manifest. Reclamation still needs the reader fence.
                manifest = new(provider.ReceiptPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            }
            catch { readers.Dispose(); throw; }
        }
        public void Dispose() { manifest.Dispose(); readers.Dispose(); }
    }
    public PlasticRevisionSnapshotTree OpenTree(SnapshotReference snapshot, SnapshotReadAccess access)
    {
        var lease = AcquireReadLease();
        try
        {
            var receipt = Required(snapshot); access.RequireMatch(snapshot);
            if (receipt.Released || !WorkspaceStore.SamePath(access.RootPath, snapshot.RootPath) || access.VerificationId != receipt.ManifestDigest)
                throw Failure("SnapshotIdentityMismatch", "Use the exact manifest proof and owned basis.");
            // The opened manifest cannot change while this tree owns its handle.
            // Do not deserialize/hash/rebuild it for every Library metadata callback.
            return new(snapshot, receipt.Manifest, revisions, () => WorkspaceStore.EnsurePlainPath(directory), lease);
        }
        catch { lease.Dispose(); throw; }
    }
    public Task<SnapshotReclamation> ReleaseAsync(SnapshotReference snapshot, CancellationToken ct = default, string? previousAttemptId = null)
    {
        ct.ThrowIfCancellationRequested(); using var operation = Fence("operation.lock", true); using var readers = Fence("readers.lock", true);
        var receipt = Required(snapshot); string path = Path.Combine(directory, "plastic-release.json");
        SnapshotReclamation intent;
        if (File.Exists(path))
        { intent = FileWorkspaceProvider.Read<SnapshotReclamation>(path); intent.RequireMatch(snapshot); }
        else
        {
            if (receipt.Released || previousAttemptId is not null) throw Failure("SnapshotIdentityMismatch", "Retain an incomplete release identity.");
            intent = new(snapshot.Id, Guid.NewGuid().ToString("N"), "released", DateTimeOffset.UtcNow, []); FileWorkspaceProvider.SaveFixed(path, intent);
        }
        if (previousAttemptId is not null && previousAttemptId != intent.AttemptId) throw Failure("SnapshotIdentityMismatch", "Reuse the exact release attempt.");
        if (!receipt.Released) Save(receipt with { Released = true });
        // Server revisions, caches, original/peer registrations and history stay untouched.
        return Task.FromResult(intent);
    }
    public PlasticBasisReceipt Required(SnapshotReference snapshot)
    { var receipt = Read() ?? throw Failure("PlasticBasisInvalid", "The fixed basis receipt is absent."); Require(receipt, snapshot); return receipt; }
    private void Require(PlasticBasisReceipt r, SnapshotReference snapshot)
    {
        WorkspaceStore.EnsurePlainPath(directory);
        ValidateManifest(r.Manifest);
        if (r.Version != 1 || r.OwnerId != owner || r.Snapshot != snapshot || snapshot.Provider != ProviderName || snapshot.Consistency != Consistency ||
            !snapshot.Persistent || !snapshot.Differential || !WorkspaceStore.SamePath(snapshot.RootPath, directory) || r.ManifestDigest != Digest(r.Manifest) ||
            snapshot.Id != "plastic-" + owner + "-" + r.ManifestDigest)
            throw Failure("SnapshotIdentityMismatch", "Retain the exact recorded manifest, owner and snapshot.");
    }
    private static void ValidateManifest(PlasticRevisionManifest manifest)
    {
        if (manifest is null) throw Failure("PlasticBasisInvalid", "Retain absent recorded manifest metadata.");
        RequireTreeSpec(manifest.TreeSpec);
        if (!Numeric(manifest.RepositoryId) || !Numeric(manifest.RootRevision) || !Guid.TryParseExact(manifest.RootGuid, "D", out _) ||
            manifest.Entries is null || manifest.Entries.Length > 32768 || string.IsNullOrEmpty(manifest.Repository) || manifest.Repository.Length > 2048 || !manifest.Repository.StartsWith("rep:") || manifest.Repository.Any(char.IsControl)) throw Failure("PlasticBasisInvalid", "Unsupported manifest identity.");
        using var tree = new PlasticRevisionSnapshotTree(new("check", ProviderName, ".", ".", DateTimeOffset.UtcNow, true, true, Consistency), manifest, new PlasticRevisionClient());
    }
    internal static string Digest(PlasticRevisionManifest manifest) => Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(manifest, Json)));
    private PlasticBasisReceipt? Read()
    {
        WorkspaceStore.EnsurePlainPath(ReceiptPath);
        if (!File.Exists(ReceiptPath)) return null;
        try { return FileWorkspaceProvider.Read<PlasticBasisReceipt>(ReceiptPath); }
        catch (WorkspaceException) { throw Failure("PlasticBasisInvalid", "Retain corrupt or unreadable basis metadata."); }
    }
    private void Save(PlasticBasisReceipt receipt)
    {
        WorkspaceStore.EnsurePlainPath(ReceiptPath); string pending = ReceiptPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        using (var stream = new FileStream(pending, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        { JsonSerializer.Serialize(stream, receipt, Json); stream.Flush(true); }
        if (File.Exists(ReceiptPath)) File.Replace(pending, ReceiptPath, null); else File.Move(pending, ReceiptPath, false);
    }
    private FileStream Fence(string name, bool exclusive)
    {
        string path = Path.Combine(directory, name); WorkspaceStore.EnsurePlainPath(path);
        try { return new(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, exclusive ? FileShare.None : FileShare.ReadWrite); }
        catch (IOException) { throw Failure("SnapshotReadersActive", "Stop the exact owned readers before basis reclamation."); }
    }
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PlasticBasisReceipt([property: JsonRequired] int Version, [property: JsonRequired] string OwnerId,
    [property: JsonRequired] SnapshotReference Snapshot, [property: JsonRequired] PlasticRevisionManifest Manifest,
    [property: JsonRequired] string ManifestDigest, [property: JsonRequired] bool Released);
