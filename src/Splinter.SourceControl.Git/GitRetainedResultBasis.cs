using System.Text.Json;
using System.Text.Json.Serialization;
using Splinter.Core;
using Splinter.Runtime;

namespace Splinter.SourceControl.Git;

// Result history retains its own basis owner. Releasing the mounted workspace's
// basis cannot make these raw committed bytes unreachable.
public static class GitRetainedResultBasis
{
    private sealed record Receipt(int Version, string OwnerId, string RequestDigest, string Revision, SnapshotReference Snapshot);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, RespectRequiredConstructorParameters = true };
    private static string DirectoryOf(string control) => Path.Combine(control, "files", "retained-git-basis");
    private static string Owner(WorkspaceRecord w) => "result-base-" + w.Id;
    public static string Reference(WorkspaceRecord w) => "refs/splinter/git-bases/" + Owner(w);
    // The returned independent reader is owned by this operation. Its exact
    // lease and native ref/receipt fences remain live through both payload
    // verification passes; callers still check full read boundaries.
    public static async Task<GitRetainedResultBasisScope> PreserveAsync(WorkspaceRecord w, string control, CancellationToken ct = default)
    {
        string directory = DirectoryOf(control), path = Path.Combine(directory, "result-owner.json");
        WorkspaceStore.EnsurePlainPath(path); Directory.CreateDirectory(directory);
        if (File.Exists(path)) return Open(w, control);
        var provider = new GitObjectSnapshotProvider(directory, Owner(w), w.BaseRevision);
        var snapshot = await provider.CaptureAsync(new(w.ProjectPath, Owner(w), directory), ct);
        var receipt = new Receipt(1, w.Id, w.RequestDigest!, w.BaseRevision!, snapshot);
        string pending = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        using (var output = new FileStream(pending, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        { JsonSerializer.Serialize(output, receipt, Json); output.Flush(true); }
        AtomicFilePublication.Publish(pending, path);
        return Open(w, control);
    }
    public static GitRetainedResultBasisScope Open(WorkspaceRecord w, string control)
    {
        string directory = DirectoryOf(control), path = Path.Combine(directory, "result-owner.json"); WorkspaceStore.EnsurePlainPath(path);
        Receipt receipt;
        try
        {
            using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read); using var document = JsonDocument.Parse(input);
            if (!JsonPropertyValidation.HasUniqueProperties(document.RootElement)) throw Invalid();
            receipt = document.RootElement.Deserialize<Receipt>(Json) ?? throw Invalid();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException) { throw Invalid(); }
        if (receipt.Version != 1 || receipt.OwnerId != w.Id || receipt.RequestDigest != w.RequestDigest || receipt.Revision != w.BaseRevision ||
            !WorkspaceStore.SamePath(receipt.Snapshot.SourcePath, w.ProjectPath)) throw Invalid();
        var provider = new GitObjectSnapshotProvider(directory, Owner(w), w.BaseRevision);
        string readers = Path.Combine(directory, "readers.lock"); WorkspaceStore.EnsurePlainPath(readers);
        if (!File.Exists(readers))
        { using var created = new FileStream(readers, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite); created.Flush(true); }
        var lease = new FileStream(readers, FileMode.Open, FileAccess.Read, FileShare.Read);
        try
        {
            var access = provider.ResolveReadAccessAsync(receipt.Snapshot).GetAwaiter().GetResult();
            return new(provider.OpenReadScope(receipt.Snapshot, access, lease), lease);
        }
        catch { lease.Dispose(); throw; }
    }
    private static WorkspaceException Invalid() => new("GitResultBasisInvalid", "Preserve the missing, changed or foreign immutable result basis owner and ref.");
}

public sealed class GitRetainedResultBasisScope : ISnapshotTree, IDisposable
{
    private readonly GitSnapshotReadScope tree;
    private readonly FileStream lease;
    internal GitRetainedResultBasisScope(GitSnapshotReadScope tree, FileStream lease) { this.tree = tree; this.lease = lease; }
    public SnapshotReference Snapshot => tree.Snapshot;
    public bool SupportsConcurrentReads => tree.SupportsConcurrentReads;
    public void EnsureAvailable() => tree.EnsureAvailable();
    public void VerifyReadBoundary() => tree.VerifyReadBoundary();
    public SnapshotEntry? Stat(string path) => tree.Stat(path);
    public IReadOnlyList<SnapshotEntry> List(string path) => tree.List(path);
    public IEnumerable<SnapshotEntry> EnumerateChildren(string path) => tree.EnumerateChildren(path);
    public int Read(string path, long offset, Span<byte> destination) => tree.Read(path, offset, destination);
    public void Dispose() { try { tree.Dispose(); } finally { lease.Dispose(); } }
}
