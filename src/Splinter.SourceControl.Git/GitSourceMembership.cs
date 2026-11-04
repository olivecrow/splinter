using Splinter.Core;

namespace Splinter.SourceControl.Git;

public sealed partial class GitWorkspaceProvider
{
    // Independent read-only commands share one observation boundary. Join all
    // children before exposing a result/failure, and own every completed spool
    // even if another command fails. Final observations still run afresh.
    private sealed class SourceMembership(string revision, string index, PagedStrings files, PagedStrings staged) : IDisposable
    {
        internal string Revision { get; } = revision;
        internal string Index { get; } = index;
        internal PagedStrings Files { get; } = files;
        internal PagedStrings Staged { get; } = staged;
        public void Dispose() { try { Staged.Dispose(); } finally { Files.Dispose(); } }
    }

    private static async Task<SourceMembership> ObserveSourceMembership(WorkspaceRecord w, CancellationToken ct)
    {
        var revision = Task.Run(() => Required(w.Path, ["rev-parse", "HEAD"], ct), ct);
        var index = Task.Run(() => IndexDigest(w, ct), ct);
        var files = Task.Run(() => new PagedStrings(GitRecords(w.Path,
            ["ls-files", "--cached", "--others", "--exclude-standard", "-z"], ct)
            .Where(path => !Managed(path, w.ManagedPaths)), ct), ct);
        var staged = Task.Run(() => new PagedStrings(GitRecords(w.Path,
            ["diff", "--cached", "--name-only", "--no-renames", "-z", "HEAD", "--"], ct)
            .Where(path => !Managed(path, w.ManagedPaths)), ct), ct);
        try { await Task.WhenAll(revision, index, files, staged); }
        catch
        {
            try { if (staged.IsCompletedSuccessfully) staged.Result?.Dispose(); }
            finally { if (files.IsCompletedSuccessfully) files.Result.Dispose(); }
            // Retain inspection's previous failure precedence after draining.
            await revision; await index; await files; await staged; throw;
        }
        return new(revision.Result, index.Result, files.Result, staged.Result);
    }

    private static async Task<(PagedStrings Files, PagedStrings Staged)> ObserveSourcePaths(WorkspaceRecord w, CancellationToken ct)
    {
        var files = Task.Run(() => new PagedStrings(GitRecords(w.Path,
            ["ls-files", "--cached", "--others", "--exclude-standard", "-z"], ct).Where(path => !Managed(path, w.ManagedPaths)), ct), ct);
        var staged = Task.Run(() => new PagedStrings(GitRecords(w.Path,
            ["diff", "--cached", "--name-only", "--no-renames", "-z", "HEAD", "--"], ct).Where(path => !Managed(path, w.ManagedPaths)), ct), ct);
        try { await Task.WhenAll(files, staged); }
        catch
        {
            try { if (staged.IsCompletedSuccessfully) staged.Result.Dispose(); }
            finally { if (files.IsCompletedSuccessfully) files.Result.Dispose(); }
            await files; await staged; throw;
        }
        return (files.Result, staged.Result);
    }
}
