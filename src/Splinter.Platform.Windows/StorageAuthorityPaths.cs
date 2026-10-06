using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using Microsoft.Win32.SafeHandles;
using Splinter.Core;
using Splinter.Runtime;

namespace Splinter.Platform.Windows;

// All user-tree inspection runs while impersonating the authenticated pipe
// client. Ancestor handles deny rename/delete until the VSS operation finishes.
internal sealed class StorageAuthorityPaths : IDisposable
{
    private readonly List<IDisposable> leases = [];
    internal static StorageAuthorityPaths Authorize(StorageGrant grant, StorageAuthorityRequest request)
    {
        var guard = new StorageAuthorityPaths();
        try
        {
            if (guard.Pin(grant.SourcePath) != grant.SourceIdentity || guard.Pin(grant.HomePath) != grant.HomeIdentity ||
                Volume(grant.SourcePath) != grant.SourceVolume)
                throw StorageAuthorityProtocol.Error("StoragePathChanged", "The installed project, volume or home identity changed; repair its grant explicitly.");
            string control = Path.Combine(grant.HomePath, "records", request.OwnerId);
            guard.Pin(control); guard.Pin(Path.Combine(control, "snapshot"));
            var workspace = guard.Read<WorkspaceRecord>(Path.Combine(control, "workspace.json"));
            if (workspace.Version != WorkspaceRecord.CurrentVersion || workspace.SourceKind is not ("git" or "none" or "plastic") ||
                workspace.ApplicationKind is not ("unity" or "none") || workspace.Id != request.OwnerId || workspace.StorageMode != "ntfs-snapshot" ||
                !StorageAuthorityProcessor.Same(workspace.ProjectPath, grant.SourcePath) ||
                !StorageAuthorityProcessor.Same(workspace.Path, Path.Combine(grant.HomePath, "workspaces", request.OwnerId)) ||
                workspace.Snapshot is null)
                throw StorageAuthorityProtocol.Error("StorageOwnerMismatch", "Workspace record does not match the registered scope.");
            if (request.Operation == "Capture")
            {
                if (workspace.Phase != "creating" || workspace.Snapshot.Reference is not null)
                    throw StorageAuthorityProtocol.Error("StorageOwnerMismatch", "Capture requires the original creating workspace intent.");
            }
            // A capture can finish before Runtime persists its returned ID.
            // Observe may refresh that exact service-owned capture after restart;
            // the processor still checks its protected receipt before any helper.
            else if (workspace.Snapshot.Reference?.Id != request.SnapshotId &&
                !(request.Operation == "Observe" && workspace.Phase == "creating" &&
                    workspace.Snapshot is { Step: "capture-intent", Reference: null }))
                throw StorageAuthorityProtocol.Error("StorageOwnerMismatch", "The workspace references another snapshot.");
            if (request.Operation == "Release")
            {
                workspace.OriginalApply?.RequireReclaimable(workspace);
                if (request.Release?.Discard is not null) guard.RequireDiscard(grant, request, workspace, control);
                else guard.RequireRelease(grant, request, workspace, control);
            }
            return guard;
        }
        catch { guard.Dispose(); throw; }
    }

    private void RequireRelease(StorageGrant grant, StorageAuthorityRequest request, WorkspaceRecord w, string control)
    {
        var state = w.Snapshot!;
        if (w.Phase is not ("releasing" or "reclaim-pending" or "released") || state.Step is not ("release-snapshot" or "released") ||
            state.Reference != request.Release!.Snapshot || state.Result is not { IndependentOfSnapshot: true, CollectionProtocol: SnapshotResult.StableJournalProtocol } result ||
            result.SnapshotId != request.SnapshotId || result.Revision != w.ResultRevision ||
            result.ResultReference != (w.SourceKind is "none" or "plastic" ? Splinter.SourceControl.Files.FileWorkspaceProvider.ResultReference(w) : "refs/splinter/snapshot-results/" + w.Id) ||
            w.ReleasedResources is not { OwnershipVerified: true, RootRunning: false, ActiveProcesses: 0, WorkingSetBytes: 0, PrivateBytes: 0 } ||
            state.Observation is not { Mounted: false, ProviderAlive: false, OwnershipVerified: true, OpenHandles: 0, WorkingSetBytes: 0, PrivateBytes: 0 })
            throw StorageAuthorityProtocol.Error("StorageReleaseNotAuthorized", "Require the durable independent-result and zero-owned-resource release stage.");
        if (Present(w.Path) || Present(Path.Combine(control, "delta")))
            throw StorageAuthorityProtocol.Error("SnapshotInUse", "The owned mount or delta still exists.");
        var gate = Read<JsonElement>(Path.Combine(control, "snapshot", "release-authorized.json"));
        if (gate.GetProperty("snapshotId").GetString() != request.SnapshotId || gate.GetProperty("remainingDependents").GetInt32() != 0 ||
            !gate.GetProperty("resultsPreserved").GetBoolean())
            throw StorageAuthorityProtocol.Error("StorageReleaseNotAuthorized", "The explicit release gate is incomplete.");
        // Runtime verifies reachable Git objects and the post-unmount seal before
        // this durable stage. Independently pin the preserved refs here, without
        // executing Git or reading user configuration as SYSTEM.
        if (w.SourceKind is "none" or "plastic")
            Splinter.SourceControl.Files.FileWorkspaceProvider.VerifyIndependentResult(w, control, path => OpenFileEvidence(path, control));
        else
        {
            Pin(Path.Combine(grant.SourcePath, ".git"));
            if (ReadReference(grant.SourcePath, result.ResultReference) != result.Revision ||
                ReadReference(grant.SourcePath, "refs/heads/" + w.SourceReference) != result.Revision)
                throw StorageAuthorityProtocol.Error("GitOverlayResultMismatch", "The independent result references changed; retain the snapshot.");
            string objects = Path.Combine(grant.SourcePath, ".git", "objects"); Pin(objects);
            if (Present(Path.Combine(objects, "info", "alternates")) || Present(Path.Combine(objects, "info", "http-alternates")))
                throw StorageAuthorityProtocol.Error("GitResultIncomplete", "External object dependencies cannot authorize reclamation.");
        }
        RequireNoDependencies(grant, request, state, control);
    }

    private void RequireDiscard(StorageGrant grant, StorageAuthorityRequest request, WorkspaceRecord w, string control)
    {
        var state = w.Snapshot!;
        w.Discard?.RequireMatch(w);
        if (w.Discard is null || w.Purpose != "task" || w.SourceReference != "splinter/" + w.Id || w.Phase is not ("discarding" or "discard-reclaim-pending") ||
            w.Discard.Step != "snapshot" || state.Reference != request.Release!.Snapshot ||
            JsonSerializer.Serialize(w.Discard, StorageAuthorityProtocol.Json) != JsonSerializer.Serialize(request.Release.Discard, StorageAuthorityProtocol.Json) ||
            w.ReleasedResources is not { OwnershipVerified: true, RootRunning: false, ActiveProcesses: 0, WorkingSetBytes: 0, PrivateBytes: 0 } ||
            state.Observation is not { DeltaPresent: false, Mounted: false, ProviderAlive: false, OwnershipVerified: true, OpenHandles: 0, WorkingSetBytes: 0, PrivateBytes: 0 } ||
            Present(w.Path) || Present(Path.Combine(control, "delta")))
            throw StorageAuthorityProtocol.Error("StorageDiscardNotAuthorized", "Require this task's explicit intent, frozen seal and zero-owned-resource discard stage.");
        var gate = Read<WorkspaceDiscardState>(Path.Combine(control, "snapshot", "discard-authorized.json"));
        if (JsonSerializer.Serialize(gate, StorageAuthorityProtocol.Json) != JsonSerializer.Serialize(w.Discard, StorageAuthorityProtocol.Json))
            throw StorageAuthorityProtocol.Error("StorageDiscardNotAuthorized", "The explicit discard authorization differs from the durable intent.");
        if (w.SourceKind is "none" or "plastic")
            Splinter.SourceControl.Files.FileWorkspaceProvider.VerifyDiscarded(w, control, OpenRead);
        else
        {
            var receipt = Read<JsonElement>(Path.Combine(control, "git-discard.json"));
            var registration = receipt.GetProperty("registration");
            string gitDirectory = registration.GetProperty("gitDirectory").GetString()!;
            string git = Path.Combine(grant.SourcePath, ".git"); Pin(git);
            if (!receipt.GetProperty("completed").GetBoolean() ||
                receipt.GetProperty("intent").Deserialize<WorkspaceDiscardIntent>(StorageAuthorityProtocol.Json) != w.Discard.Intent ||
                registration.GetProperty("ownerId").GetString() != w.Id || registration.GetProperty("branch").GetString() != w.SourceReference ||
                !StorageAuthorityProcessor.Same(registration.GetProperty("path").GetString()!, w.Path) ||
                !StorageAuthorityProcessor.Same(registration.GetProperty("project").GetString()!, grant.SourcePath) ||
                !StorageAuthorityProcessor.Same(Path.GetDirectoryName(gitDirectory)!, Path.Combine(git, "worktrees")) || Present(gitDirectory) ||
                ReferenceExists(grant.SourcePath, "refs/heads/" + w.SourceReference))
                throw StorageAuthorityProtocol.Error("StorageDiscardNotAuthorized", "Prove this intent's task registration and branch absent; retain all result references.");
        }
        RequireNoDependencies(grant, request, state, control);
    }

    private bool ReferenceExists(string source, string reference)
    {
        if (!reference.StartsWith("refs/heads/", StringComparison.Ordinal) || reference.Split('/').Any(s => s is "" or "." or ".." || s.Contains('\\') || s.Contains(':')))
            throw StorageAuthorityProtocol.Error("StoragePathRejected", "Invalid task reference.");
        string git = Path.Combine(source, ".git"), path = Path.Combine(git, reference.Replace('/', Path.DirectorySeparatorChar));
        if (Present(path)) { Pin(path, directory: false); return true; }
        string packed = Path.Combine(git, "packed-refs");
        return Present(packed) && ReadText(packed).Split('\n').Select(l => l.Trim().Split(' ', 2)).Any(p => p.Length == 2 && p[1] == reference);
    }

    private void RequireNoDependencies(StorageGrant grant, StorageAuthorityRequest request, SnapshotWorkspaceState state, string control)
    {
        string projectId = WorkspaceStore.ProjectKey(grant.SourcePath);
        string workflow = Path.Combine(grant.HomePath, "validation-workflows", WorkspaceStore.Key(projectId), "state.json");
        if (Present(workflow + ".pending")) throw StorageAuthorityProtocol.Error("ViewStateRecoveryRequired", "Reconcile retained view state before reclaiming.");
        if (Present(workflow))
        {
            Pin(Path.GetDirectoryName(workflow)!);
            var ledger = Read<ViewWorkflowState>(workflow);
            if (ledger.Version != 1 || ledger.ProjectId != projectId ||
                ViewWorkflowCoordinator.SnapshotDependencies(ledger, state.Reference!).Length != 0)
                throw StorageAuthorityProtocol.Error("SnapshotInUse", "View artifacts still depend on this snapshot.");
        }
        else if (Directory.Exists(Path.GetDirectoryName(workflow)))
            throw StorageAuthorityProtocol.Error("ViewStateRecoveryRequired", "Preserve orphaned view preparation.");
        string records = Path.Combine(grant.HomePath, "records"); Pin(records);
        foreach (string other in Directory.EnumerateDirectories(records))
        {
            if (StorageAuthorityProcessor.Same(other, control)) continue;
            Pin(other);
            var dependent = Read<WorkspaceRecord>(Path.Combine(other, "workspace.json"));
            if (dependent.Snapshot?.Reference?.Id == request.SnapshotId && dependent.Phase is not ("released" or "discarded") &&
                dependent.Snapshot!.Step != "release-snapshot" &&
                (dependent.Snapshot.Step != "discard-snapshot" || dependent.Snapshot.Observation?.DeltaPresent != false))
                throw StorageAuthorityProtocol.Error("SnapshotInUse", "Another workspace still depends on this snapshot.");
        }
        leases.Add(new FileStream(Path.Combine(control, "snapshot", "readers.lock"), FileMode.Open, FileAccess.Read, FileShare.None));
    }

    private string ReadReference(string source, string reference)
    {
        if (!reference.StartsWith("refs/", StringComparison.Ordinal) || reference.Split('/').Any(s => s is "" or "." or ".." || s.Contains('\\') || s.Contains(':')))
            throw StorageAuthorityProtocol.Error("StoragePathRejected", "Invalid result reference.");
        string git = Path.Combine(source, ".git"), path = Path.Combine(git, reference.Replace('/', Path.DirectorySeparatorChar));
        if (Present(path)) { Pin(Path.GetDirectoryName(path)!); return ReadText(path).Trim(); }
        string packed = Path.Combine(git, "packed-refs");
        if (!Present(packed)) throw StorageAuthorityProtocol.Error("GitResultIncomplete", "The preserved reference is missing.");
        return ReadText(packed).Split('\n').Select(l => l.Trim().Split(' ', 2)).SingleOrDefault(p => p.Length == 2 && p[1] == reference)?[0]
            ?? throw StorageAuthorityProtocol.Error("GitResultIncomplete", "The preserved reference is missing.");
    }
    private T Read<T>(string path)
    {
        var stream = OpenRead(path);
        using var bytes = new MemoryStream(); stream.CopyTo(bytes);
        return StorageAuthorityProtocol.Decode<T>(bytes.ToArray());
    }
    private string ReadText(string path) { using var reader = new StreamReader(OpenRead(path), leaveOpen: true); return reader.ReadToEnd(); }
    private FileStream OpenRead(string path)
    {
        // Parent is pinned by the caller; OPEN_REPARSE_POINT also checks the
        // file itself before a FileStream can follow it.
        Pin(path, directory: false);
        var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length > 16 * 1024 * 1024) { stream.Dispose(); throw new IOException("Oversized authority evidence."); }
        leases.Add(stream); return stream;
    }
    private Stream OpenFileEvidence(string path, string control)
    {
        if (!StorageAuthorityProcessor.Same(Path.GetDirectoryName(path)!, Path.Combine(control, "files", "objects"))) return OpenRead(path);
        // Validated hash names, pinned deny-write/delete until reclamation.
        Pin(path, directory: false);
        var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536);
        leases.Add(stream); return stream;
    }
    internal string Pin(string path, bool directory = true)
    {
        StorageAuthorityProtocol.PlainAbsolute(path);
        string root = Path.GetPathRoot(path)!;
        string cursor = root;
        var segments = path[root.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
        string identity = PinOne(root, true);
        for (int i = 0; i < segments.Length; i++)
        { cursor = Path.Combine(cursor, segments[i]); identity = PinOne(cursor, i < segments.Length - 1 || directory); }
        return identity;
    }
    private string PinOne(string path, bool directory)
    {
        var handle = CreateFileW(path, 0x80 | (directory ? 1u : 0u), 1, 0, 3, 0x00200000 | 0x02000000, 0);
        if (handle.IsInvalid) { handle.Dispose(); throw new Win32Exception(Marshal.GetLastWin32Error()); }
        leases.Add(handle);
        if (!GetFileInformationByHandle(handle, out var info)) throw new Win32Exception(Marshal.GetLastWin32Error());
        if ((info.Attributes & 0x400) != 0 || directory != ((info.Attributes & 0x10) != 0) || !directory && info.Links != 1)
            throw StorageAuthorityProtocol.Error("StoragePathRejected", "Reparse points and path-type substitutions are rejected.");
        return $"{info.VolumeSerial:X8}:{info.IndexHigh:X8}{info.IndexLow:X8}";
    }
    internal static string Volume(string path)
    {
        var mount = new StringBuilder(1024); var volume = new StringBuilder(1024);
        if (!GetVolumePathNameW(path, mount, mount.Capacity) || !GetVolumeNameForVolumeMountPointW(mount.ToString(), volume, volume.Capacity))
            throw new Win32Exception(Marshal.GetLastWin32Error());
        return volume.ToString();
    }
    internal static void RequireProtectedDirectory(string path, bool allowWindowsInstaller = false)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
        RequireProtectedAcl(new DirectoryInfo(path).GetAccessControl(), allowWindowsInstaller);
    }
    internal static void RequireProtectedAcl(FileSystemSecurity acl, bool allowWindowsInstaller = false)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
        string? owner = acl.GetOwner(typeof(SecurityIdentifier))?.Value;
        if (owner is not ("S-1-5-18" or "S-1-5-32-544"))
            throw StorageAuthorityProtocol.Error("StorageServiceUnsafe", "Service paths must be owned by SYSTEM or Administrators.");
        const FileSystemRights writes = FileSystemRights.Write | FileSystemRights.Delete | FileSystemRights.DeleteSubdirectoriesAndFiles |
            FileSystemRights.ChangePermissions | FileSystemRights.TakeOwnership | (FileSystemRights)0x50000000;
        string? installer = allowWindowsInstaller ? new NTAccount("NT SERVICE", "TrustedInstaller").Translate(typeof(SecurityIdentifier)).Value : null;
        foreach (FileSystemAccessRule rule in acl.GetAccessRules(true, true, typeof(SecurityIdentifier)))
            if (rule.AccessControlType == AccessControlType.Allow && (rule.FileSystemRights & writes) != 0 &&
                rule.IdentityReference.Value is not ("S-1-5-18" or "S-1-5-32-544") && rule.IdentityReference.Value != installer &&
                !(rule.IdentityReference.Value == "S-1-3-0" && (rule.PropagationFlags & PropagationFlags.InheritOnly) != 0))
                throw StorageAuthorityProtocol.Error("StorageServiceUnsafe", "A non-administrator can modify the authority directory or its children.");
    }
    private static bool Present(string path)
    {
        try { _ = File.GetAttributes(path); return true; }
        catch (FileNotFoundException) { return false; }
        catch (DirectoryNotFoundException) { return false; }
    }
    public void Dispose() { for (int i = leases.Count - 1; i >= 0; i--) leases[i].Dispose(); }
    [StructLayout(LayoutKind.Sequential)] private struct FileInfo
    { public uint Attributes; public System.Runtime.InteropServices.ComTypes.FILETIME Created, Accessed, Written; public uint VolumeSerial, SizeHigh, SizeLow, Links, IndexHigh, IndexLow; }
    [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)] private static extern SafeFileHandle CreateFileW(string path, uint access, uint sharing, nint security, uint disposition, uint flags, nint template);
    [DllImport("kernel32", SetLastError = true)] private static extern bool GetFileInformationByHandle(SafeFileHandle handle, out FileInfo info);
    [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool GetVolumePathNameW(string path, StringBuilder mount, int length);
    [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool GetVolumeNameForVolumeMountPointW(string path, StringBuilder volume, int length);
}
