namespace Splinter.Runtime;

// Scratch catalogs never claim project or result ownership. Only the exact
// freshly-created directory and recognized files can be reclaimed here.
public sealed class TemporaryMetadataDirectory : IDisposable
{
    private readonly string identity = Guid.NewGuid().ToString("N");
    private bool disposed;
    public string Path { get; }
    public TemporaryMetadataDirectory(string? parent = null)
    {
        string root = System.IO.Path.GetFullPath(parent ?? System.IO.Path.GetTempPath());
        WorkspaceStore.EnsurePlainPath(root); Directory.CreateDirectory(root);
        Path = System.IO.Path.Combine(root, "splinter-metadata-" + identity);
        WorkspaceStore.EnsurePlainPath(Path);
        if (Directory.Exists(Path) || File.Exists(Path)) throw new IOException("Preserve an occupied metadata scratch directory.");
        Directory.CreateDirectory(Path);
        using var marker = new FileStream(System.IO.Path.Combine(Path, "scratch.owner"), FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        marker.Write(System.Text.Encoding.ASCII.GetBytes(identity)); marker.Flush(true);
    }
    public void Dispose()
    {
        if (disposed) return; disposed = true;
        WorkspaceStore.EnsurePlainPath(Path);
        string marker = System.IO.Path.Combine(Path, "scratch.owner");
        if (!File.Exists(marker) || File.ReadAllText(marker) != identity)
            throw new IOException("Preserve changed scratch ownership.");
        foreach (string path in Directory.EnumerateFileSystemEntries(Path))
        {
            WorkspaceStore.EnsurePlainPath(path);
            string name = System.IO.Path.GetFileName(path);
            if (Directory.Exists(path) || name is not ("scratch.owner" or "identity.bin" or "writer.lock") &&
                !(name.StartsWith("pages-", StringComparison.Ordinal) && name.EndsWith(".bin", StringComparison.Ordinal)))
                throw new IOException("Preserve unexpected metadata scratch content.");
        }
        foreach (string path in Directory.EnumerateFiles(Path)) File.Delete(path);
        Directory.Delete(Path);
    }
}
