using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Splinter.Core;

namespace Splinter.Runtime;

// One short-lived CLI owns one operation lock. Editor and record lifetimes are
// independent of that CLI, so closing a conversation never releases its workspace.
public sealed partial class WorkspaceStore : IWorkspaceBaselineReader
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    public string Root { get; }

    public WorkspaceStore(string root)
    {
        Root = Path.GetFullPath(root);
        EnsurePlainPath(Root);
        Directory.CreateDirectory(Root);
        foreach (string name in new[] { "records", "workspaces", "locks", "baselines" })
        {
            string path = Path.Combine(Root, name);
            EnsurePlainPath(path);
            Directory.CreateDirectory(path);
        }
    }

    public static string Key(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..24];
    public static string CanonicalProject(string project)
    {
        string path = Path.TrimEndingDirectorySeparator(Path.GetFullPath(project));
        EnsurePlainPath(path);
        return path;
    }
    public static string ProjectKey(string project) => Key(OperatingSystem.IsWindows() ? project.ToUpperInvariant() : project);
    public string WorkPath(string id) { CheckId(id); return Path.Combine(Root, "workspaces", id); }
    public string ControlPath(string id) { CheckId(id); return Path.Combine(Root, "records", id); }
    public string BaselinePath(string id) { CheckId(id); return Path.Combine(Root, "baselines", id, "tree"); }
    public WorkspaceBaseline GetBaseline(string id)
    {
        string path = Path.Combine(Path.GetDirectoryName(BaselinePath(id))!, "baseline.json"); EnsurePlainPath(path);
        if (!File.Exists(path)) throw new WorkspaceException("BaselineNotReady", "Prepare, validate and seal this baseline first.");
        var record = JsonSerializer.Deserialize<WorkspaceBaseline>(File.ReadAllText(path), Json)
            ?? throw new WorkspaceException("StateCorrupt", "Empty baseline record.");
        if (record.Id != id || !SamePath(record.Path, BaselinePath(id))) throw new WorkspaceException("StateCorrupt", "Baseline path/identity mismatch.");
        EnsurePlainPath(record.Path);
        if (!Directory.Exists(record.Path)) throw new WorkspaceException("BaselineNotReady", "Baseline files are unavailable.");
        if (BaselineManifest.Hash(record.Path) != record.ManifestHash) throw new WorkspaceException("BaselineChanged", "Sealed baseline files changed. Preserve them and prepare a new baseline.");
        return record;
    }
    public void SaveBaseline(WorkspaceBaseline record)
    {
        if (!SamePath(record.Path, BaselinePath(record.Id))) throw new WorkspaceException("StateCorrupt", "Invalid baseline path.");
        string file = Path.Combine(Path.GetDirectoryName(record.Path)!, "baseline.json"); EnsurePlainPath(file);
        string temporary = file + ".tmp"; EnsurePlainPath(temporary);
        using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
        { JsonSerializer.Serialize(stream, record, Json); stream.Flush(true); }
        AtomicFilePublication.Publish(temporary, file);
    }

    public async Task<FileStream> LockAsync(string key, CancellationToken cancellationToken = default)
    {
        CheckId(key);
        string path = Path.Combine(Root, "locks", key + ".lock");
        EnsurePlainPath(path);
        for (int attempt = 0; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try { return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
            catch (IOException ex) when (IsLockContention(ex) && attempt < 50) { await Task.Delay(100, cancellationToken); }
            catch (IOException ex) when (IsLockContention(ex))
            { throw new WorkspaceException("WorkspaceBusy", "Another command owns this workspace. Retry the same command."); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            { throw new WorkspaceException("StateReadUnavailable", "The workspace operation lock cannot be opened. Preserve state and restore its path, access or storage before retrying the same identity."); }
        }
    }

    // Confirmed sharing/lock violations are contention. Missing paths, access,
    // storage and other I/O failures must not become a safe-to-retry busy reply.
    private static bool IsLockContention(IOException error) => (uint)error.HResult is 0x80070020 or 0x80070021;

    public WorkspaceRecord? Find(string id)
    {
        string path = Path.Combine(ControlPath(id), "workspace.json");
        EnsurePlainPath(path);
        WorkspaceRecord record;
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            record = JsonSerializer.Deserialize<WorkspaceRecord>(stream, Json)
                ?? throw new WorkspaceException("StateCorrupt", "Workspace record is empty; preserve the directory for recovery.");
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            // A missing record alongside prior preparation is not a new key.
            // Never replace an interrupted write or orphaned external resources.
            try
            {
                if (PathPresent(ControlPath(id)) || PathPresent(WorkPath(id)))
                    throw new WorkspaceException("StateRecoveryRequired", "Workspace state is missing alongside existing preparation. Preserve its files and reconcile this identity.");
                return null;
            }
            catch (Exception accessError) when (accessError is IOException or UnauthorizedAccessException)
            { throw new WorkspaceException("StateReadUnavailable", "Workspace state cannot be inspected. Restore access before retrying the same key."); }
        }
        catch (JsonException)
        { throw new WorkspaceException("StateCorrupt", "Workspace JSON is invalid or incomplete. Preserve its record and temporary files for recovery."); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { throw new WorkspaceException("StateReadUnavailable", "Workspace state cannot be read. Restore access before retrying the same key."); }
        if (record.Version != WorkspaceRecord.CurrentVersion || record.SourceKind is not ("git" or "none" or "plastic") ||
            record.StorageMode is not ("ntfs-snapshot" or "git-snapshot" or "plastic-snapshot" or "refs-cow" or "git-checkout") ||
            record.StorageMode == "git-snapshot" && (record.SourceKind != "git" || record.Purpose != "task" || record.BaselineId is not null) ||
            record.ApplicationKind is not ("unity" or "none") || record.ApplicationKind == "none" && record.ManagedPaths.Length != 0 &&
                (record.GeneratedDirectory is null || !record.ManagedPaths.SequenceEqual(new[] { record.GeneratedDirectory.RelativePath })) ||
            (record.SourceKind == "none" || record.SourceKind == "plastic" && record.StorageMode != "plastic-snapshot") && (record.StorageMode != "ntfs-snapshot" || record.BaseReference != "snapshot" || record.Purpose != "task") ||
            record.Id != id || !SamePath(record.Path, WorkPath(id)))
            throw new WorkspaceException("StateCorrupt", "Workspace identity/path/version does not match its registry.");
        if (record.GeneratedDirectoryId is not null && record.StorageMode is not ("git-snapshot" or "plastic-snapshot") || record.GeneratedDirectory is { } generated &&
            (generated.Id != record.GeneratedDirectoryId || !SamePath(generated.ProjectPath, record.ProjectPath) ||
             string.IsNullOrWhiteSpace(generated.RelativePath) || generated.RelativePath.Contains('/') || generated.RelativePath.Contains('\\')) ||
            record.GeneratedDirectoryId is not null && record.GeneratedDirectory is null && record.Snapshot?.Attached == true)
            throw new WorkspaceException("GeneratedBasisMismatch", "Retain the task's exact generated-directory identity.");
        if (record.SourceKind == "plastic" && (record.OriginalApply is not null || record.IntegratedRevision is not null || record.Phase is "integrating" or "integrated"))
            throw new WorkspaceException("StateCorrupt", "Plastic original application is unavailable; preserve the unsupported state.");
        if (record.Discard is { } discard) discard.RequireMatch(record);
        else if (record.Phase is "discarding" or "discard-reclaim-pending" or "discarded" ||
            record.Snapshot?.Step.StartsWith("discard", StringComparison.Ordinal) == true)
            throw new WorkspaceException("DiscardIntentMismatch", "Preserve discard state: its durable intent is missing.");
        record.OriginalApply?.RequireMatch(record);
        if (record.SourceKind == "none" &&
            (record.Phase is "integrating" or "integrated" && record.OriginalApply is null ||
             record.IntegratedRevision is not null && (record.OriginalApply?.Completed != true || record.IntegratedRevision != record.ResultRevision)))
            throw new WorkspaceException("OriginalApplyStateInvalid", "Preserve the original-apply record: its fixed intent or completion receipt is missing.");
        if (record.Snapshot?.Result is { } result && result.CollectionProtocol != SnapshotResult.StableJournalProtocol)
            throw new WorkspaceException("StateCorrupt", "The result collection protocol is invalid. Preserve the record and its resources.");
        if (WorkspaceStorage.HasRangeJournal(record.StorageMode) && (record.Snapshot is null ||
            record.Snapshot.Attached && record.Snapshot.Reference is null ||
            record.Snapshot.Mount is { } mount && (mount.Id != id || !SamePath(mount.Path, record.Path) || mount.Snapshot != record.Snapshot.Reference)))
            throw new WorkspaceException("SnapshotStateMissing", "Preserve this workspace: snapshot identities do not match its durable record.");
        if (record.StorageMode == "plastic-snapshot" && (record.SourceKind != "plastic" || record.Purpose != "task" || record.BaselineId is not null || record.BaseReference == "snapshot" ||
            record.Snapshot?.Reference is { } plasticBasis && (plasticBasis.Provider != "plastic-revisions" || plasticBasis.Consistency != "recorded-project-changeset-raw-revisions" || !SamePath(plasticBasis.SourcePath, record.ProjectPath))))
            throw new WorkspaceException("SnapshotIdentityMismatch", "Preserve the Plastic task recorded basis and source selection.");
        if (record.StorageMode == "git-snapshot" && record.Snapshot?.Reference is { } basis &&
            (basis.Provider != "git-objects" || basis.Consistency != "recorded-project-commit-raw-blobs" ||
             !SamePath(basis.SourcePath, record.ProjectPath) || record.BaseRevision is null || basis.Id != "git-" + record.Id + "-" + record.BaseRevision))
            throw new WorkspaceException("SnapshotIdentityMismatch", "Preserve the Git task's original recorded basis.");
        // Reading durable metadata must also work while an overlay is mounted
        // or a mount reply was lost. The backend verifies the root's live
        // ownership before any source access; all registry ancestors stay plain.
        EnsurePlainPath(WorkspaceStorage.HasRangeJournal(record.StorageMode) ? Path.GetDirectoryName(record.Path)! : record.Path);
        return record;
    }

    private static bool PathPresent(string path)
    {
        try { _ = File.GetAttributes(path); return true; }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException) { return false; }
    }

    public WorkspaceRecord Get(string id) => Find(id) ?? throw new WorkspaceException("WorkspaceNotFound", $"No workspace '{id}' in this state root.");

    public void Save(WorkspaceRecord workspace, string action)
    {
        var progress = new StateWriteProgress();
        try { SaveCore(workspace, action, progress); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A low HRESULT word is a native error only for a confirmed Win32
            // facility on Windows. Managed failures are not Win32 error zero.
            bool win32 = OperatingSystem.IsWindows() && ((uint)ex.HResult & 0xffff0000) == 0x80070000;
            var diagnostic = new StateWriteDiagnostic(progress.Stage, progress.StatePublication, ex.GetType().Name,
                ex.HResult, win32 ? "win32" : null, win32 ? ex.HResult & 0xffff : null);
            throw new WorkspaceException("StateWriteIncomplete", "Workspace state could not be fully persisted. Preserve records and temporary files; restore access or space before observing the same identity.",
                new StateWriteFailureDetails(workspace.Id, action, diagnostic),
                [new("workspace.status", new { id = workspace.Id, home = Root }, true)]);
        }
    }

    private sealed class StateWriteProgress
    {
        public string Stage = "prepare-record";
        public string StatePublication = "not-attempted";
    }

    private void SaveCore(WorkspaceRecord workspace, string action, StateWriteProgress progress)
    {
        string control = ControlPath(workspace.Id);
        EnsurePlainPath(control);
        Directory.CreateDirectory(control);
        if (!SamePath(workspace.Path, WorkPath(workspace.Id)))
            throw new WorkspaceException("StateCorrupt", "Refusing to persist an unmanaged workspace path.");
        workspace.UpdatedAt = DateTimeOffset.UtcNow;
        string target = Path.Combine(control, "workspace.json");
        progress.Stage = "validate-target";
        EnsurePlainPath(target);
        string temporary = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
        progress.Stage = "open-temporary";
        using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            progress.Stage = "write-temporary";
            JsonSerializer.Serialize(stream, workspace, Json);
            progress.Stage = "flush-temporary";
            stream.Flush(flushToDisk: true);
            progress.Stage = "close-temporary";
        }
        progress.Stage = "publish-state";
        progress.StatePublication = "unknown";
        AtomicFilePublication.Publish(temporary, target);
        progress.StatePublication = "completed";
        progress.Stage = "append-event";
        string journal = Path.Combine(control, "events.jsonl");
        EnsurePlainPath(journal);
        File.AppendAllText(journal, JsonSerializer.Serialize(new
        {
            at = workspace.UpdatedAt, action, workspace.Phase, workspace.LastError,
            workspace.ResultRevision, workspace.IntegratedRevision
        }) + "\n", new UTF8Encoding(false));
    }

    public IReadOnlyList<WorkspaceRecord> List(string? project = null) =>
        Directory.EnumerateDirectories(Path.Combine(Root, "records"))
            .Select(Path.GetFileName).Where(id => id is not null && IdPattern().IsMatch(id))
            .Select(id => Find(id!)).OfType<WorkspaceRecord>()
            .Where(record => project is null || SamePath(record.ProjectPath, project))
            .OrderBy(record => record.CreatedAt).ToArray();

    public static bool SamePath(string left, string right) => string.Equals(
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(left)), Path.TrimEndingDirectorySeparator(Path.GetFullPath(right)),
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    public static void EnsurePlainPath(string path)
    {
        for (string? current = Path.GetFullPath(path); current is not null; current = Path.GetDirectoryName(current))
            if ((Directory.Exists(current) || File.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new WorkspaceException("UnsafePath", $"Reparse points are not supported: {current}");
    }

    private static void CheckId(string id)
    {
        if (!IdPattern().IsMatch(id)) throw new WorkspaceException("InvalidWorkspaceId", "Workspace IDs are 24 lowercase hexadecimal characters.");
    }
    [GeneratedRegex("^[a-f0-9]{24}$", RegexOptions.CultureInvariant)]
    private static partial Regex IdPattern();
}
