namespace Splinter.Core;

// "Library" denotes a generated subtree independent of its VCS source basis;
// an empty relative path denotes the explicitly managed whole-project basis.
// The platform owns freezing, write interception and restoration of the original.
public sealed record GeneratedDirectoryReference(string Id, string ProjectPath, string RelativePath,
    SnapshotReference Snapshot, string ReceiptDirectory, string OwnerId, string ManifestHash);
public sealed record SnapshotGraft(string RelativePath, GeneratedDirectoryReference Basis);
public sealed record DeltaRestoreRange(long Offset, int Length);
public sealed record DeltaRestoreEntry(DeltaEntry Entry, DeltaRestoreRange[] Ranges);

public interface IGeneratedDirectoryDependencies
{
    Task<GeneratedDirectoryReference> AttachAsync(string id, WorkspaceRecord workspace, CancellationToken ct = default);
    Task DetachAsync(GeneratedDirectoryReference basis, WorkspaceRecord workspace, CancellationToken ct = default);
}

// Verifies the exact managed original before source reads or publication.
// Platform adapters retain the basis/mount lease for the entire operation.
public interface IProjectSourceAccess
{
    IDisposable Acquire(WorkspaceRecord workspace, bool publishing, CancellationToken ct = default);
}
