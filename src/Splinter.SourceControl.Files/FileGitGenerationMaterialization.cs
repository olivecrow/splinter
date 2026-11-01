using Splinter.Core;
using Splinter.Runtime;
using Splinter.SourceControl.Git;

namespace Splinter.SourceControl.Files;

public sealed partial class FileWorkspaceProvider
{
    private sealed record GitGenerationMaterialization(int Version, string OwnerId, string RequestDigest,
        string ResultId, string ResultIdentity, string Reference, string Fingerprint);

    // Only an explicit branch merge asks for a commit. Ordinary collection and
    // original-file publication retain ranges and leave Git's index/HEAD alone.
    public async Task<WorkspaceResultGeneration> MaterializeGitGenerationAsync(WorkspaceRecord w,
        WorkspaceResultGeneration generation, CancellationToken ct = default)
    {
        if (w.SourceKind != "git") throw new WorkspaceException("SourceIntegrationUnsupported", "A Git result is required.");
        if (generation.GitReference is not null) return generation;
        string control = Control(w); await VerifyGenerationAsync(w, control, generation, ct);
        string path = GenerationPath(control, generation.Id, "git-materialization.json");
        if (File.Exists(path))
        {
            var retained = Read<GitGenerationMaterialization>(path);
            if (retained.Version != 1 || retained.OwnerId != w.Id || retained.RequestDigest != w.RequestDigest ||
                retained.ResultId != generation.Id || retained.ResultIdentity != generation.Revision ||
                await GitSourceResult.VerifyAsync(w.ProjectPath, retained.Reference, ct) != retained.Fingerprint)
                throw new WorkspaceException("GitResultIncomplete", "Preserve the changed explicit Git materialization receipt.");
            return generation with { GitReference = retained.Reference, GitFingerprint = retained.Fingerprint };
        }
        using var manifest = ReadManifest(w, GenerationPath(control, generation.Id, "result.json"));
        using var basis = ReadManifest(w, FilePath(control, "generation-basis.json"));
        using var reader = ContentReader(w, control, manifest.ContentFiles);
        using var tree = new ImmutableManifestTree(w.Snapshot!.Reference!, manifest, reader.Reader ?? Open, control, reader.VerifyBoundary);
        IEnumerable<string> Changed()
        {
            foreach (var entry in manifest.EnumerateEntries())
            {
                var before = basis.Find(entry.Path);
                if (before is null || before.Kind != entry.Kind || before.Hash != entry.Hash || before.UnixMode != entry.UnixMode) yield return entry.Path;
            }
        }
        var retainedResult = await GitSourceResult.PreserveAsync(w.ProjectPath, w.BaseRevision!, generation.Id,
            tree, Changed(), w.ManagedPaths, ct);
        reader.VerifyBoundary();
        SaveFixed(path, new GitGenerationMaterialization(1, w.Id, w.RequestDigest!, generation.Id,
            generation.Revision, retainedResult.Reference, retainedResult.Hash));
        return generation with { GitReference = retainedResult.Reference, GitFingerprint = retainedResult.Hash };
    }

    private sealed class ImmutableManifestTree(SnapshotReference snapshot, FileSourceManifest manifest,
        Func<string, Stream> open, string control, Action verify) : ISnapshotTree, IDisposable
    {
        private string? activePath;
        private Stream? active;
        public SnapshotReference Snapshot => snapshot;
        public void EnsureAvailable() { }
        public void VerifyReadBoundary() => verify();
        public SnapshotEntry? Stat(string path)
        {
            if (path.Length == 0) return new("", true, 0, 16, 0, 0, 0);
            var value = manifest.Find(path); return value is null ? null : Entry(value);
        }
        public IReadOnlyList<SnapshotEntry> List(string path) => EnumerateChildren(path).ToArray();
        public IEnumerable<SnapshotEntry> EnumerateChildren(string path)
        {
            using var catalog = new PagedSnapshotCatalog(manifest.Directory!, manifest.Catalog!);
            foreach (var entry in catalog.EnumerateChildren(path)) yield return entry.Entry;
        }
        public int Read(string path, long offset, Span<byte> destination)
        {
            var entry = manifest.Find(path) ?? throw new WorkspaceException("GitResultIncomplete", "The fixed materialization path is missing.");
            if (entry.Kind != "file" || offset < 0 || offset > entry.Length) throw new WorkspaceException("GitResultIncomplete", "Invalid fixed materialization range.");
            if (activePath != path)
            { active?.Dispose(); active = open(FilePath(control, "objects/" + entry.Hash)); activePath = path; }
            active!.Position = offset; return active.Read(destination[..(int)Math.Min(destination.Length, entry.Length - offset)]);
        }
        private static SnapshotEntry Entry(FileSourceEntry entry)
        {
            long stamp = new DateTime(entry.LastWriteUtcTicks, DateTimeKind.Utc).ToFileTimeUtc();
            return new(entry.Path, entry.Kind == "directory", entry.Length, (uint)entry.Attributes, stamp, stamp, stamp, entry.UnixMode);
        }
        public void Dispose() => active?.Dispose();
    }
}
