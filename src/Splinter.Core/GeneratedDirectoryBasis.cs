namespace Splinter.Core;

// An application-generated subtree is independent of its VCS source basis.
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
