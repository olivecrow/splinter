using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Splinter.Core;

namespace Splinter.Runtime;

// One short-lived CLI owns one operation lock. Editor and record lifetimes are
// independent of that CLI, so closing a conversation never releases its workspace.
public sealed partial class WorkspaceStore
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    public string Root { get; }

    public WorkspaceStore(string root)
    {
        Root = Path.GetFullPath(root);
        EnsurePlainPath(Root);
        Directory.CreateDirectory(Root);
        foreach (string name in new[] { "records", "workspaces", "locks" })
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

    public async Task<FileStream> LockAsync(string key, CancellationToken cancellationToken = default)
    {
        CheckId(key);
        string path = Path.Combine(Root, "locks", key + ".lock");
        EnsurePlainPath(path);
        for (int attempt = 0; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try { return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
            catch (IOException) when (attempt < 50) { await Task.Delay(100, cancellationToken); }
            catch (IOException) { throw new WorkspaceException("WorkspaceBusy", "Another command owns this workspace. Retry the same command."); }
        }
    }

    public WorkspaceRecord? Find(string id)
    {
        string path = Path.Combine(ControlPath(id), "workspace.json");
        EnsurePlainPath(path);
        if (!File.Exists(path)) return null;
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var record = JsonSerializer.Deserialize<WorkspaceRecord>(stream, Json)
            ?? throw new WorkspaceException("StateCorrupt", "Workspace record is empty; preserve the directory for recovery.");
        if (record.Version != 1 || record.Id != id || !SamePath(record.Path, WorkPath(id)))
            throw new WorkspaceException("StateCorrupt", "Workspace identity/path/version does not match its registry.");
        EnsurePlainPath(record.Path);
        return record;
    }

    public WorkspaceRecord Get(string id) => Find(id) ?? throw new WorkspaceException("WorkspaceNotFound", $"No workspace '{id}' in this state root.");

    public void Save(WorkspaceRecord workspace, string action)
    {
        string control = ControlPath(workspace.Id);
        EnsurePlainPath(control);
        Directory.CreateDirectory(control);
        if (!SamePath(workspace.Path, WorkPath(workspace.Id)))
            throw new WorkspaceException("StateCorrupt", "Refusing to persist an unmanaged workspace path.");
        workspace.UpdatedAt = DateTimeOffset.UtcNow;
        string target = Path.Combine(control, "workspace.json");
        EnsurePlainPath(target);
        string temporary = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
        using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            JsonSerializer.Serialize(stream, workspace, Json);
            stream.Flush(flushToDisk: true);
        }
        File.Move(temporary, target, overwrite: true);
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
