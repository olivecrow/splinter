using System.Text.Json;
using Splinter.Core;
using Splinter.Runtime;

namespace Splinter.Platform.Windows;

// A task owns only its dependency receipt, never the common original payload.
// Release/discard remove task resources; project.restore owns the final basis.
public sealed class ProjectDirectorySnapshotProvider(string directory, string owner) : ISourceSnapshotProvider
{
    private sealed record Receipt(int Version, string Owner, string RequestDigest, GeneratedDirectoryReference Basis);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public static bool Present(string directory) => File.Exists(Path.Combine(directory, "project-reference.json"));
    public static async Task CaptureAsync(string directory, WorkspaceRecord w, CancellationToken ct)
    {
        var receipt = new Receipt(1, w.Id, w.RequestDigest!, w.GeneratedDirectory!);
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "project-reference.json");
        var prior = await WindowsProcessWorkspaceProvider.ReadAsync<Receipt>(path, ct);
        if (prior is not null && prior != receipt) throw new WorkspaceException("ProjectBasisMismatch", "Preserve another task's project reference.");
        if (prior is null) await WindowsProcessWorkspaceProvider.WriteAsync(path, receipt, ct);
    }
    private Receipt Read(SnapshotReference snapshot)
    {
        WorkspaceStore.EnsurePlainPath(directory);
        var receipt = JsonSerializer.Deserialize<Receipt>(File.ReadAllText(Path.Combine(directory, "project-reference.json")), Json);
        string home = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(directory)))!;
        if (receipt?.Basis is null || receipt.Version != 1 || receipt.Owner != owner || owner.Length != 24 || owner.Any(c => !char.IsAsciiHexDigit(c)) ||
            receipt.RequestDigest is not { Length: 24 } || receipt.RequestDigest.Any(c => !char.IsAsciiHexDigit(c)) ||
            receipt.Basis.Id is not { Length: 24 } || receipt.Basis.Id.Any(c => !char.IsAsciiHexDigit(c)) || receipt.Basis.OwnerId != receipt.Basis.Id ||
            receipt.Basis.RelativePath != "" || receipt.Basis.Snapshot != snapshot || snapshot.Id != receipt.Basis.Id || snapshot.Provider != OwnedDirectorySnapshotTree.ProviderName ||
            !snapshot.Persistent || !snapshot.Differential || snapshot.Consistency != "stopped-original-owned-directory" || !WorkspaceStore.SamePath(snapshot.SourcePath, snapshot.RootPath) ||
            !WorkspaceStore.SamePath(receipt.Basis.ReceiptDirectory, Path.Combine(home, "project-bases", receipt.Basis.Id)) ||
            !WorkspaceStore.SamePath(snapshot.RootPath, Path.Combine(receipt.Basis.ReceiptDirectory, "base")) ||
            !WorkspaceStore.SamePath(directory, Path.Combine(home, "records", owner, "snapshot")))
            throw new WorkspaceException("ProjectBasisMismatch", "Retain a changed task/project receipt.");
        var workspace = new WorkspaceStore(home).Get(owner);
        if (workspace.StorageMode != "project-snapshot" || workspace.SourceKind != "none" || workspace.RequestDigest != receipt.RequestDigest ||
            workspace.GeneratedDirectoryId != receipt.Basis.Id || !WorkspaceStore.SamePath(workspace.ProjectPath, receipt.Basis.ProjectPath) ||
            workspace.GeneratedDirectory is { } bound && bound != receipt.Basis || workspace.Snapshot?.Reference is { } prior && prior != snapshot)
            throw new WorkspaceException("ProjectBasisMismatch", "The task receipt must match its fixed request and project state.");
        return receipt;
    }
    public OwnedDirectorySnapshotProvider Shared(SnapshotReference snapshot)
    {
        var receipt = Read(snapshot);
        var provider = new OwnedDirectorySnapshotProvider(receipt.Basis.ReceiptDirectory, receipt.Basis.OwnerId);
        if (provider.Read(snapshot) is not { } manifest || ManagedDirectoryBasis.HashFile(Path.Combine(receipt.Basis.ReceiptDirectory, "manifest.json")) != receipt.Basis.ManifestHash || manifest.OwnerId != receipt.Basis.OwnerId)
            throw new WorkspaceException("ProjectBasisMismatch", "Preserve a changed common project manifest.");
        return provider;
    }
    public IDisposable AcquireReadLease(SnapshotReference snapshot) => Shared(snapshot).AcquireReadLease();
    public Task<SnapshotReference> CaptureAsync(SnapshotCapture request, CancellationToken ct = default) =>
        throw new WorkspaceException("ProjectBasisPreparationRequired", "Use project prepare and an exact project basis id.");
    public async Task<SnapshotObservation> ObserveAsync(SnapshotReference snapshot, CancellationToken ct = default)
    {
        var receipt = Read(snapshot);
        var released = await WindowsProcessWorkspaceProvider.ReadAsync<Receipt>(Path.Combine(directory, "project-released.json"), ct);
        if (released is not null)
        {
            if (released != receipt) throw new WorkspaceException("ProjectBasisMismatch", "Preserve an inconsistent task release receipt.");
            return new(snapshot.Id, false, "SnapshotReleased", null, null);
        }
        return await Shared(snapshot).ObserveAsync(snapshot, ct);
    }
    public async Task<SnapshotReadAccess> ResolveReadAccessAsync(SnapshotReference snapshot, CancellationToken ct = default) =>
        (await ObserveAsync(snapshot, ct)).ReadAccess ?? throw new WorkspaceException("SnapshotMissing", "Retain the unavailable fixed project basis.");
    public async Task<SnapshotReclamation> ReleaseAsync(SnapshotReference snapshot, CancellationToken ct = default, string? previousAttemptId = null)
    {
        var receipt = Read(snapshot);
        string control = Path.GetDirectoryName(directory)!;
        if (Directory.Exists(Path.Combine(control, "delta")) || File.Exists(Path.Combine(control, "delta")) ||
            !File.Exists(Path.Combine(directory, "release-authorized.json")) && !File.Exists(Path.Combine(directory, "discard-authorized.json")))
            throw new WorkspaceException("SnapshotInUse", "A fixed task reclamation authorization and absent delta are required.");
        string path = Path.Combine(directory, "project-released.json");
        var prior = await WindowsProcessWorkspaceProvider.ReadAsync<Receipt>(path, ct);
        if (prior is not null && prior != receipt) throw new WorkspaceException("ProjectBasisMismatch", "Preserve another task's release receipt.");
        if (prior is null) await WindowsProcessWorkspaceProvider.WriteAsync(path, receipt, ct);
        return new(snapshot.Id, previousAttemptId ?? "project-" + owner, "released", DateTimeOffset.UtcNow, []);
    }
}
