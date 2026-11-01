using Splinter.Core;
using Splinter.Runtime;

namespace Splinter.Platform.Windows;

internal static class CapturedResultReaders
{
    internal static OwnedCapturedContentReader? Open(OverlayIdentity identity, string deltaPath)
    {
        if (identity.Version != 3) return null;
        string control = Path.GetDirectoryName(Path.GetFullPath(deltaPath))!;
        string records = Path.GetDirectoryName(control)!;
        // Generated-directory views have their own immutable backing and never
        // adopt source-result chunks. Task readers must use their exact record.
        if (Path.GetFileName(records) != "records") return null;
        string home = Path.GetDirectoryName(records)!;
        var pins = new WindowsJournalFilePins();
        var workspace = new WorkspaceStore(home, pins).Get(identity.Id);
        if (!WorkspaceStore.SamePath(control, Path.Combine(home, "records", workspace.Id)) ||
            !SourceCaptureTree.SameIdentity(identity, NtfsSnapshotWorkspaceBackend.Identity(workspace)))
            throw new WorkspaceException("SourceCaptureIdentityMismatch", "Canonical source ranges belong to another task identity.");
        return new(home, control, workspace, pins);
    }
}
