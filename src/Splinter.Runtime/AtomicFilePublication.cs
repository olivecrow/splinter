namespace Splinter.Runtime;

// Callers serialize writers and flush a temporary file in the destination's
// directory before publishing it. Readers must allow FileShare.Delete.
public static class AtomicFilePublication
{
    public static void Publish(string temporary, string destination)
    {
        // Move(overwrite: true) can fail with ERROR_ACCESS_DENIED on Windows
        // even when a reader grants delete sharing. Replace preserves the
        // reader's old file while publishing the complete new file by name.
        // Never delete first or copy over the visible file; leave remaining
        // files intact on failure so the caller can reconcile them.
        if (File.Exists(destination)) File.Replace(temporary, destination, null);
        else File.Move(temporary, destination);
    }
}
