using System.Text.Json;

namespace Splinter.Runtime;

// Receipts precede scratch payload. Cleanup verifies the native object, so an
// equal-byte replacement is preserved. Unknown/torn creation gaps stay owned
// by no one and are deliberately excluded from automatic deletion.
internal static class MetadataScratchOwnership
{
    private sealed record FileOwner(int Version, string Name, string Identity);
    private sealed record ScopeOwner(int Version, string Name, string Kind);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    { RespectRequiredConstructorParameters = true };
    public static void MarkScope(string directory, string kind)
        => Write(Path.Combine(directory, "scratch-scope.owner.json"), new ScopeOwner(1, Path.GetFileName(directory), kind));
    public static FileStream Create(string path, FileAccess access, FileShare sharing, int buffer, FileOptions options, IJournalFilePins? pins)
    {
        WorkspaceStore.EnsurePlainPath(path);
        var stream = new FileStream(path, FileMode.CreateNew, access, sharing, buffer, options);
        try
        {
            if (pins is not null)
            {
                string identity = pins.Identify(path); pins.Verify(stream, identity);
                Write(path + ".owner", new FileOwner(1, Path.GetFileName(path), identity));
            }
            return stream;
        }
        catch { stream.Dispose(); throw; }
    }
    public static void Remove(string path, IJournalFilePins? pins)
    {
        if (pins is null) { File.Delete(path); return; }
        var owner = Read<FileOwner>(path + ".owner");
        if (owner.Version != 1 || owner.Name != Path.GetFileName(path)) throw new IOException("Preserve an unsupported metadata scratch owner.");
        pins.Unpin(path, owner.Identity); File.Delete(path + ".owner");
    }
    public static bool TryReclaimScope(string directory, string kind, IJournalFilePins pins)
    {
        WorkspaceStore.EnsurePlainPath(directory); string marker = Path.Combine(directory, "scratch-scope.owner.json");
        if (!File.Exists(marker)) return false;
        ScopeOwner owner;
        try { owner = Read<ScopeOwner>(marker); } catch (Exception ex) when (ex is IOException or JsonException) { return false; }
        if (owner.Version != 1 || owner.Name != Path.GetFileName(directory) || owner.Kind != kind) return false;
        bool complete = true;
        foreach (string path in Directory.EnumerateFileSystemEntries(directory))
        {
            WorkspaceStore.EnsurePlainPath(path); string name = Path.GetFileName(path);
            if (Directory.Exists(path))
            {
                string? nested = name.StartsWith("sort-", StringComparison.Ordinal) ? "metadata-sort" :
                    name.StartsWith("repack-", StringComparison.Ordinal) ? "metadata-repack" : null;
                if (nested is null || !TryReclaimScope(path, nested, pins)) complete = false;
            }
            else if (name == "scratch-scope.owner.json" || !File.Exists(path)) { }
            else if (name.EndsWith(".bin", StringComparison.Ordinal))
            {
                try { Remove(path, pins); }
                catch (Exception ex) when (ex is IOException or Splinter.Core.WorkspaceException) { complete = false; }
            }
            else if (name.EndsWith(".bin.owner", StringComparison.Ordinal))
            {
                if (File.Exists(path[..^6])) continue;
                try
                {
                    var file = Read<FileOwner>(path);
                    if (file.Version != 1 || file.Name != Path.GetFileName(path[..^6])) { complete = false; continue; }
                    File.Delete(path);
                }
                catch (Exception ex) when (ex is IOException or JsonException) { complete = false; }
            }
            else complete = false;
        }
        if (complete) { File.Delete(marker); Directory.Delete(directory); }
        return complete;
    }
    private static T Read<T>(string path)
    { WorkspaceStore.EnsurePlainPath(path); using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read); return JsonSerializer.Deserialize<T>(input, Json) ?? throw new IOException("An owned scratch receipt is empty."); }
    private static void Write<T>(string path, T value)
    { WorkspaceStore.EnsurePlainPath(path); using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read); JsonSerializer.Serialize(output, value, Json); output.Flush(true); }
}
