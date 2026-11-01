using System.Text;
using Splinter.Core;

namespace Splinter.SourceControl.Git;

public sealed partial class GitObjectSnapshotProvider
{
    // The caller owns this exact read lease until the returned tree is disposed.
    // Only this task's loose ref/receipt are fenced; no index, branch, pack file
    // or shared packed-refs lock is held. Packed-only refs use full observations.
    public GitSnapshotReadScope OpenReadScope(SnapshotReference snapshot, SnapshotReadAccess access, FileStream readLease)
    {
        if (!readLease.CanRead || !Same(readLease.Name, Path.Combine(receiptDirectory, "readers.lock")))
            throw Failure("GitBasisReadScopeInvalid", "The exact live basis reader lease is required.");
        var expected = ReadReceipt() ?? throw Failure("GitBasisReceiptInvalid", "The basis receipt is missing.");
        Require(expected, snapshot); access.RequireMatch(snapshot);
        string refPath = Path.Combine(snapshot.RootPath, Reference.Replace('/', Path.DirectorySeparatorChar));
        var fences = new List<FileStream>();
        try
        {
            fences.Add(OpenFence(ReceiptPath));
            FileStream? refFence = File.Exists(refPath) ? OpenFence(refPath) : null;
            if (refFence is not null) fences.Add(refFence);
            byte[]? fixedRef = refFence is null ? null : ReadReference(refPath);
            if (fixedRef is not null && Encoding.ASCII.GetString(fixedRef).TrimEnd('\r', '\n') != expected.Revision)
                throw Failure("GitBasisRefChanged", "Preserve the changed or symbolic owned basis ref.");
            var scopeGate = new object(); WorkspaceException? failure = null;
            SnapshotObservation FullObservation()
            {
                var observed = ObserveAsync(snapshot).GetAwaiter().GetResult();
                if (observed.Available != true || observed.ReadAccess != access)
                    throw Failure(observed.FailureCode ?? "SnapshotIdentityMismatch", "The scoped basis no longer matches its fixed read access.");
                return observed;
            }
            var observation = new SnapshotObservation(snapshot.Id, true, null, 0, null) { ReadAccess = access };
            SnapshotObservation Check(bool boundary)
            {
                lock (scopeGate)
                {
                    if (failure is not null) throw failure;
                    try
                    {
                        if (!readLease.CanRead || fences.Any(f => !f.CanRead))
                            throw Failure("GitBasisReadScopeInvalid", "The scoped reader or its fences have been closed.");
                        var current = ReadReceipt();
                        if (current != expected) throw Failure("GitBasisReceiptInvalid", "The scoped basis receipt changed or disappeared.");
                        Require(current, snapshot);
                        if (fixedRef is not null && !ReadReference(refPath).AsSpan().SequenceEqual(fixedRef))
                            throw Failure("GitBasisRefChanged", "The owned basis ref changed or disappeared.");
                        return boundary || fixedRef is null ? FullObservation() : observation;
                    }
                    catch (WorkspaceException ex) { failure = ex; throw; }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ObjectDisposedException)
                    {
                        failure = Failure("GitBasisReadScopeInvalid", "Preserve an unreadable or replaced basis lease, receipt or ref.");
                        throw failure;
                    }
                }
            }
            observation = Check(true);
            GitObjectSnapshotTree.VerifyObject(snapshot.RootPath, expected.Revision, "commit");
            var tree = OpenTree(snapshot, access, () => Check(false));
            return new GitSnapshotReadScope(tree, () => Check(true), fences.ToArray());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            foreach (var fence in fences) fence.Dispose();
            throw Failure("GitBasisReadScopeInvalid", "Preserve the basis when its scoped ref or receipt cannot be fenced.");
        }
        catch { foreach (var fence in fences) fence.Dispose(); throw; }
    }
    private static FileStream OpenFence(string path)
    {
        Plain(path);
        return new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
    }
    private static byte[] ReadReference(string path)
    {
        Plain(path);
        using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        if (input.Length is < 40 or > 66) throw Failure("GitBasisRefChanged", "The owned ref is not a fixed ordinary object ID.");
        byte[] bytes = new byte[(int)input.Length]; input.ReadExactly(bytes); return bytes;
    }
}

public sealed class GitSnapshotReadScope : ISnapshotTree, ISnapshotContentSummaryProvider, IDisposable
{
    private readonly GitObjectSnapshotTree tree;
    private readonly Action verify;
    private readonly FileStream[] fences;
    internal GitSnapshotReadScope(GitObjectSnapshotTree tree, Action verify, FileStream[] fences)
    { this.tree = tree; this.verify = verify; this.fences = fences; }
    public SnapshotReference Snapshot => tree.Snapshot;
    public bool SupportsConcurrentReads => tree.SupportsConcurrentReads;
    public void EnsureAvailable() => tree.EnsureAvailable();
    public void VerifyReadBoundary() => verify();
    public SnapshotEntry? Stat(string path) => tree.Stat(path);
    public IReadOnlyList<SnapshotEntry> List(string path) => tree.List(path);
    public IEnumerable<SnapshotEntry> EnumerateChildren(string path) => tree.EnumerateChildren(path);
    public int Read(string path, long offset, Span<byte> destination) => tree.Read(path, offset, destination);
    public SnapshotContentSummary? GetVerifiedContentSummary(string path) => tree.GetVerifiedContentSummary(path);
    public GitReadObservation? ObserveReads() => tree.ObserveReads();
    public void Dispose()
    {
        try { tree.Dispose(); }
        finally { foreach (var fence in fences) fence.Dispose(); }
    }
}
