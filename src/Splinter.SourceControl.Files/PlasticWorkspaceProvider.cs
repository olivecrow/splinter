using System.Security.Cryptography;
using System.Text.Json.Serialization;
using Splinter.Core;
using Splinter.Runtime;

namespace Splinter.SourceControl.Files;

public sealed record PlasticWorkspaceIdentity(string Path, string Guid, string Type, string Dynamic);
public sealed record PlasticWorkspaceLocation(string Path, string Guid);
// Only local, observational commands belong at this boundary. No checkout,
// update, registration, server, credentials or check-in operation is provided.
public interface IPlasticWorkspaceClient
{
    Task<string> VersionAsync(CancellationToken ct = default);
    Task<PlasticWorkspaceIdentity> InspectAsync(string path, CancellationToken ct = default);
    Task<PlasticWorkspaceLocation[]> ListAsync(CancellationToken ct = default);
}

public sealed class PlasticWorkspaceProvider(WorkspaceStore store, IPlasticWorkspaceClient client) :
    ISourceWorkspaceProvider, ISnapshotSourceWorkspaceProvider, ISourceDiscardProvider
{
    private readonly FileWorkspaceProvider files = new(store);
    private string Control(WorkspaceRecord w) => store.ControlPath(w.Id);
    private static string Evidence(string control, string name) => Path.Combine(control, "plastic", name);
    private static WorkspaceException Invalid(string message) => new("PlasticCompatibilityInvalid", message);
    private static void Require(WorkspaceRecord w)
    {
        if (w.SourceKind != "plastic" || w.StorageMode != "ntfs-snapshot" || w.BaseReference != "snapshot" || w.Purpose != "task")
            throw Invalid("Select Plastic explicitly with an NTFS task and snapshot basis.");
    }
    public static void RequireIdentity(PlasticWorkspaceIdentity identity, string project)
    {
        if (!Path.IsPathFullyQualified(identity.Path) || !WorkspaceStore.SamePath(identity.Path, project) ||
            !System.Guid.TryParseExact(identity.Guid, "D", out _) || identity.Type != "regular" || identity.Dynamic != "static")
            throw Invalid("Only the exact registered regular/static Plastic workspace root is supported.");
    }
    private static string AdministrationHash(string root)
    {
        string directory = Path.Combine(root, ".plastic"), path = Path.Combine(directory, "plastic.workspace");
        WorkspaceStore.EnsurePlainPath(path);
        if (!Directory.Exists(directory) || (File.GetAttributes(directory) & (FileAttributes.ReparsePoint | FileAttributes.Encrypted | FileAttributes.Compressed | FileAttributes.SparseFile)) != 0)
            throw Invalid("Preserve missing or unsupported Plastic administration.");
        using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
        if (input.Length is < 1 or > 65536) throw Invalid("Preserve unknown Plastic workspace administration.");
        return Convert.ToHexStringLower(SHA256.HashData(input));
    }
    private static bool Contains(string root, string path) => WorkspaceStore.SamePath(root, path) ||
        Path.GetFullPath(path).StartsWith(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase);
    private async Task VerifyIsolationAsync(WorkspaceRecord w, CancellationToken ct)
    {
        // Global/local Plastic registration is never inherited by a worker.
        // Overlapping registrations are rejected, not removed or repaired.
        var registrations = await client.ListAsync(ct);
        if (registrations.Any(r => Contains(r.Path, store.Root) || Contains(store.Root, r.Path)))
            throw Invalid("The Splinter home must not overlap any Plastic workspace registration.");
        for (string? path = store.Root; path is not null; path = Path.GetDirectoryName(path))
            foreach (string name in FileWorkspaceProvider.VcsMetadata)
                if (File.Exists(Path.Combine(path, name)) || Directory.Exists(Path.Combine(path, name)))
                    throw Invalid("The Splinter home cannot inherit VCS administration.");
        if (WorkspaceMountAccess.ObservedRoot(w) is not null)
            foreach (string name in FileWorkspaceProvider.VcsMetadata)
                if (File.Exists(Path.Combine(w.Path, name)) || Directory.Exists(Path.Combine(w.Path, name)))
                    throw Invalid("A Plastic worker must not expose or own VCS administration.");
    }
    public async Task<string> PreflightAsync(WorkspaceRecord w, CancellationToken ct = default)
    {
        Require(w);
        var identity = await client.InspectAsync(w.ProjectPath, ct); RequireIdentity(identity, w.ProjectPath);
        string version = await client.VersionAsync(ct);
        await VerifyIsolationAsync(w, ct);
        await FileWorkspaceProvider.PreflightFilesAsync(w, ct);
        FileWorkspaceProvider.SaveFixed(Evidence(Control(w), "admission.json"), new PlasticSourceAdmission(w.Id, w.RequestDigest!,
            w.ProjectPath, identity.Guid, version, AdministrationHash(w.ProjectPath)));
        return "snapshot:" + w.Id;
    }
    public async Task RegisterAsync(WorkspaceRecord w, string control, CancellationToken ct = default)
    {
        Require(w);
        string receiptPath = Evidence(control, "snapshot.json");
        if (!File.Exists(receiptPath))
        {
            var admission = Admission(w, control);
            var identity = await client.InspectAsync(w.ProjectPath, ct); RequireIdentity(identity, w.ProjectPath);
            if (identity.Guid != admission.WorkspaceGuid || AdministrationHash(w.ProjectPath) != admission.AdministrationHash)
                throw Invalid("Original Plastic registration changed during capture; preserve this task.");
            FileWorkspaceProvider.SaveFixed(receiptPath, new PlasticSourceReceipt(admission, w.Path, w.Snapshot!.Reference!));
        }
        VerifyReceipt(w, control); await VerifyIsolationAsync(w, ct); await files.RegisterAsync(w, control, ct);
    }
    private static PlasticSourceAdmission Admission(WorkspaceRecord w, string control, Func<string, Stream>? open = null)
    {
        Require(w);
        var a = FileWorkspaceProvider.Read<PlasticSourceAdmission>(Evidence(control, "admission.json"), open);
        if (a.Version != 1 || a.OwnerId != w.Id || a.RequestDigest != w.RequestDigest || a.ProjectPath != w.ProjectPath ||
            !System.Guid.TryParseExact(a.WorkspaceGuid, "D", out _) || string.IsNullOrWhiteSpace(a.ClientVersion) ||
            a.AdministrationHash is not { Length: 64 } || a.AdministrationHash.Any(c => c is not (>= '0' and <= '9' or >= 'a' and <= 'f')))
            throw Invalid("The immutable Plastic admission belongs to another request or is corrupt.");
        return a;
    }
    // Authority validation reads owned receipts only. It never invokes cm or
    // reads credentials/original Plastic metadata as the privileged service.
    public static void VerifyReceipt(WorkspaceRecord w, string control, Func<string, Stream>? open = null)
    {
        var admission = Admission(w, control, open);
        var receipt = FileWorkspaceProvider.Read<PlasticSourceReceipt>(Evidence(control, "snapshot.json"), open);
        if (receipt.Version != 1 || receipt.Admission != admission || receipt.Path != w.Path || receipt.Snapshot != w.Snapshot?.Reference)
            throw Invalid("The Plastic isolation receipt differs from this task and immutable snapshot.");
    }
    public async Task AttachAsync(WorkspaceRecord w, string c, CancellationToken ct = default)
    { VerifyReceipt(w, c); await VerifyIsolationAsync(w, ct); await files.AttachAsync(w, c, ct); }
    public Task<string> EstablishBaselineAsync(WorkspaceRecord w, string c, CancellationToken ct = default)
    {
        var admission = Admission(w, c);
        var access = w.Snapshot?.Observation?.SnapshotStorage?.ReadAccess ?? throw Invalid("Verify immutable captured access before computing the basis.");
        access.RequireMatch(w.Snapshot!.Reference!);
        if (AdministrationHash(access.RootPath) != admission.AdministrationHash)
            throw Invalid("Captured Plastic administration differs from admission; preserve this snapshot.");
        return files.EstablishBaselineAsync(w, c, ct);
    }
    public async Task<SourceState> InspectAsync(WorkspaceRecord w, CancellationToken ct = default)
    { await VerifyIsolationAsync(w, ct); return await files.InspectAsync(w, ct); }
    public async Task<string> CheckpointAsync(WorkspaceRecord w, string m, CancellationToken ct = default)
    { await VerifyIsolationAsync(w, ct); return await files.CheckpointAsync(w, m, ct); }
    public async Task<string> PreserveAsync(WorkspaceRecord w, string c, CancellationToken ct = default)
    { await VerifyIsolationAsync(w, ct); return await files.PreserveAsync(w, c, ct); }
    public Task VerifyResultAsync(WorkspaceRecord w, string c, CancellationToken ct = default) => files.VerifyResultAsync(w, c, ct);
    public Task UnregisterAsync(WorkspaceRecord w, string c, CancellationToken ct = default) => files.UnregisterAsync(w, c, ct);
    public async Task<string> ObserveDiscardRevisionAsync(WorkspaceRecord w, string c, CancellationToken ct = default)
    { await VerifyIsolationAsync(w, ct); return await files.ObserveDiscardRevisionAsync(w, c, ct); }
    public Task DiscardSourceAsync(WorkspaceRecord w, string c, CancellationToken ct = default) => files.DiscardSourceAsync(w, c, ct);
    public Task VerifyDiscardedAsync(WorkspaceRecord w, string c, CancellationToken ct = default) => files.VerifyDiscardedAsync(w, c, ct);
    public Task<string> ResolveRevisionAsync(string p, string r, CancellationToken ct = default) => throw Invalid("Use the captured file basis, not a Plastic changeset checkout.");
    public Task<string> SnapshotKeyAsync(string p, string r, string[] paths, CancellationToken ct = default) => throw Invalid("Plastic cache baselines are unavailable.");
    public Task<SourceWorkspace> CreateAsync(WorkspaceRecord w, CancellationToken ct = default) => throw Invalid("No Plastic checkout or full-copy fallback is available.");
    public Task<IntegrationResult> IntegrateAsync(WorkspaceRecord w, string t, CancellationToken ct = default) => throw new WorkspaceException("SourceIntegrationUnsupported", "Plastic/Unity original application is not qualified; preserve an independent result.");
    public Task ReleaseAsync(WorkspaceRecord w, CancellationToken ct = default) => throw Invalid("Use the sealed NTFS release lifecycle.");
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PlasticSourceAdmission([property: JsonRequired] string OwnerId, [property: JsonRequired] string RequestDigest,
    [property: JsonRequired] string ProjectPath, [property: JsonRequired] string WorkspaceGuid,
    [property: JsonRequired] string ClientVersion, [property: JsonRequired] string AdministrationHash)
{ [JsonRequired] public int Version { get; init; } = 1; }
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PlasticSourceReceipt([property: JsonRequired] PlasticSourceAdmission Admission,
    [property: JsonRequired] string Path, [property: JsonRequired] SnapshotReference Snapshot)
{ [JsonRequired] public int Version { get; init; } = 1; }
