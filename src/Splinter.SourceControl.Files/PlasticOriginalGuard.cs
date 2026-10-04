using System.Security.Cryptography;
using System.Text.Json;
using Splinter.Core;
using Splinter.Runtime;

namespace Splinter.SourceControl.Files;

public sealed record PlasticOriginalState(string Repository, string Server, string Changeset, string Selector, string[] LockedPaths);
public interface IPlasticOriginalStateClient
{
    Task<PlasticOriginalState> OriginalStateAsync(string project, string repository, CancellationToken ct = default);
}
public interface IPlasticOriginalLease : IDisposable { string Identity { get; } }
public interface IPlasticOriginalGuard
{
    IPlasticOriginalLease Acquire(WorkspaceRecord workspace, string[] paths, CancellationToken ct, bool publishing = false);
}

// Normal-user observational VCS boundary. No server mutation or privileged
// process is used, and remote observations are not a distributed reservation.
public sealed class PlasticOriginalGuard(WorkspaceStore store, IPlasticWorkspaceClient client,
    IPlasticOriginalStateClient stateClient, IPlasticRevisionClient revisions) : IPlasticOriginalGuard
{
    public IPlasticOriginalLease Acquire(WorkspaceRecord w, string[] paths, CancellationToken ct, bool publishing = false)
    {
        var lease = new Lease();
        try
        {
            var admission = PlasticWorkspaceProvider.Admission(w, store.ControlPath(w.Id));
            foreach (string name in new[] { "plastic.workspace", "plastic.selector", "plastic.wktree" })
            {
                string path = Path.Combine(w.ProjectPath, ".plastic", name); WorkspaceStore.EnsurePlainPath(path);
                lease.Streams.Add(new(path, FileMode.Open, FileAccess.Read, FileShare.Read));
            }
            if (w.ApplicationKind == "unity")
            {
                string temp = Path.Combine(w.ProjectPath, "Temp"), file = Path.Combine(temp, "UnityLockfile");
                WorkspaceStore.EnsurePlainPath(file);
                if (File.Exists(file)) lease.Streams.Add(new(file, FileMode.Open, FileAccess.ReadWrite, FileShare.None));
                else if (publishing)
                {
                    if (!Directory.Exists(temp)) { Directory.CreateDirectory(temp); lease.CreatedTemp = temp; }
                    lease.Streams.Add(new(file, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 4096, FileOptions.DeleteOnClose));
                }
            }
            var identity = client.InspectAsync(w.ProjectPath, ct).GetAwaiter().GetResult();
            PlasticWorkspaceProvider.RequireIdentity(identity, w.ProjectPath);
            if (identity.Guid != admission.WorkspaceGuid || client.VersionAsync(ct).GetAwaiter().GetResult() != admission.ClientVersion ||
                PlasticWorkspaceProvider.AdministrationHash(w.ProjectPath) != admission.AdministrationHash)
                throw new WorkspaceException("PlasticOriginalIdentityChanged", "Preserve the changed original registration or client identity.");
            var manifest = revisions.ListAsync(w.BaseReference, ct).GetAwaiter().GetResult();
            revisions.VerifyProjectAsync(w.ProjectPath, manifest, ct).GetAwaiter().GetResult();
            var state = stateClient.OriginalStateAsync(w.ProjectPath, manifest.Repository, ct).GetAwaiter().GetResult();
            int first = w.BaseReference.IndexOf('@'), second = w.BaseReference.IndexOf('@', first + 1);
            if (second < 0 || state.Changeset != w.BaseReference[..first] || state.Repository != w.BaseReference[(first + 1)..second] ||
                state.Server != w.BaseReference[(second + 1)..] || string.IsNullOrWhiteSpace(state.Selector))
                throw new WorkspaceException("PlasticOriginalIdentityChanged", "The original repository, loaded changeset or selector differs from the fixed source.");
            if (state.LockedPaths.Any(l => l.Length == 0 || paths.Any(p => p.Equals(l, StringComparison.OrdinalIgnoreCase) ||
                p.StartsWith(l + "/", StringComparison.OrdinalIgnoreCase) || l.StartsWith(p + "/", StringComparison.OrdinalIgnoreCase))))
                throw new WorkspaceException("PlasticOriginalLocked", "Preserve affected locked or retained server paths; Splinter does not change Plastic locks.");
            using var selector = new FileStream(Path.Combine(w.ProjectPath, ".plastic", "plastic.selector"), FileMode.Open, FileAccess.Read, FileShare.Read);
            if (selector.Length is < 1 or > 65536) throw new WorkspaceException("PlasticOriginalIdentityChanged", "Preserve unknown selector metadata.");
            lease.Identity = "sha256:" + Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
            { admission, manifest.RepositoryId, manifest.Repository, state.Changeset, state.Selector, selectorHash = Convert.ToHexStringLower(SHA256.HashData(selector)) })));
            return lease;
        }
        catch (Exception ex)
        {
            lease.Dispose();
            if (ex is IOException or UnauthorizedAccessException)
                throw new WorkspaceException("PlasticOriginalUnavailable", "Stop the original Editor normally and preserve unavailable or concurrently changed administration.");
            throw;
        }
    }
    private sealed class Lease : IPlasticOriginalLease
    {
        public List<FileStream> Streams { get; } = [];
        public string Identity { get; set; } = "";
        public string? CreatedTemp;
        public void Dispose()
        {
            for (int i = Streams.Count - 1; i >= 0; i--) Streams[i].Dispose(); Streams.Clear();
            if (CreatedTemp is not null && Directory.Exists(CreatedTemp) && !Directory.EnumerateFileSystemEntries(CreatedTemp).Any()) Directory.Delete(CreatedTemp);
            CreatedTemp = null;
        }
    }
}
