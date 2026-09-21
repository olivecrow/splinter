using Splinter.Core;

namespace Splinter.Platform.Windows;

// Directory.Exists suppresses access and I/O failures. Those failures do not
// prove that the immutable snapshot was deleted or authorize reclamation.
public static class SnapshotAvailabilityProbe
{
    public static SnapshotObservation Observe(SnapshotReference snapshot)
    {
        try
        {
            var attributes = File.GetAttributes(snapshot.RootPath);
            return (attributes & FileAttributes.Directory) != 0
                ? new(snapshot.Id, true, null, null, null)
                : new(snapshot.Id, null, "SnapshotIdentityMismatch", null, null);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            // An offline source volume must remain unknown even if the saved
            // device path is absent. Do not read the mutable source payload.
            try
            {
                if (new DriveInfo(Path.GetPathRoot(Path.GetFullPath(snapshot.SourcePath))!).IsReady)
                    return new(snapshot.Id, false, "SnapshotMissing", null, null);
            }
            catch (Exception driveError) when (driveError is IOException or UnauthorizedAccessException or ArgumentException) { }
            return new(snapshot.Id, null, "SnapshotObservationUnknown", null, null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new(snapshot.Id, null, "SnapshotObservationUnknown", null, null);
        }
    }
}
