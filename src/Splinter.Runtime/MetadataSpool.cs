using Splinter.Core;

namespace Splinter.Runtime;

// Operation-owned binary scratch. Reads are repeatable cursors and never load
// the whole spool. Publishing a durable immutable root remains the caller's job.
public sealed class MetadataSpool : IDisposable
{
    internal const int MemoryBudget = 64 * 1024;
    private readonly string directory;
    private readonly MemoryStream memory = new();
    private FileStream? output;
    private bool disposed;
    public MetadataSpool(string directory)
    {
        this.directory = directory;
    }
    public void Append(MetadataEntry entry)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        // Includes the record checksum/header and the largest child reference.
        long charge = 128L + System.Text.Encoding.UTF8.GetByteCount(entry.Key) + entry.Value.Length;
        if (output is null && charge > MemoryBudget - memory.Length) Spill();
        MetadataExternalSort.Write((Stream?)output ?? memory, entry);
    }
    private void Spill()
    {
        WorkspaceStore.EnsurePlainPath(directory); Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "metadata-spool-" + Guid.NewGuid().ToString("N") + ".bin");
        output = new(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read | FileShare.Delete, 65536,
            FileOptions.SequentialScan | FileOptions.DeleteOnClose);
        output.Write(memory.GetBuffer().AsSpan(0, checked((int)memory.Length)));
        memory.SetLength(0); memory.Capacity = 0;
    }
    public void FlushDurable()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (output is null) Spill();
        output!.Flush(true);
    }
    public IEnumerable<MetadataEntry> Read()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        output?.Flush();
        using Stream reader = output is null
            ? new MemoryStream(memory.GetBuffer(), 0, checked((int)memory.Length), writable: false)
            : new BufferedStream(new Cursor(output.SafeFileHandle, output.Length), 65536);
        for (MetadataEntry? entry; (entry = MetadataExternalSort.Read(reader)) is not null;) yield return entry;
    }
    public void Dispose() { disposed = true; output?.Dispose(); memory.Dispose(); }
    private sealed class Cursor(Microsoft.Win32.SafeHandles.SafeFileHandle handle, long length) : Stream
    {
        private long position;
        public override int Read(Span<byte> bytes)
        {
            int count = (int)Math.Min(bytes.Length, length - position); if (count == 0) return 0;
            int read = RandomAccess.Read(handle, bytes[..count], position); position += read; return read;
        }
        public override int Read(byte[] bytes, int offset, int count) => Read(bytes.AsSpan(offset, count));
        public override long Length => length;
        public override long Position { get => position; set { if (value < 0 || value > length) throw new ArgumentOutOfRangeException(nameof(value)); position = value; } }
        public override long Seek(long offset, SeekOrigin origin)
        { Position = checked((origin == SeekOrigin.Begin ? 0 : origin == SeekOrigin.Current ? position : length) + offset); return position; }
        public override bool CanRead => true; public override bool CanSeek => true; public override bool CanWrite => false;
        public override void Flush() => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] bytes, int offset, int count) => throw new NotSupportedException();
        // The spool owns the native handle; disposing a cursor never closes it.
    }
}

public static class SnapshotCatalogInput
{
    public static IEnumerable<DirectoryMetadataRecord> WithPathMarkers(IEnumerable<MetadataEntry> entries, string workingDirectory,
        bool caseSensitive = false, CancellationToken ct = default)
    {
        using var scratch = new TemporaryMetadataDirectory(); using var store = new PagedMetadataStore(scratch.Path, writable: true);
        var codec = new DirectoryMetadataTree(store, caseSensitive);
        IEnumerable<MetadataEntry> Records()
        {
            foreach (var entry in entries)
            {
                yield return codec.Encode(new(new(entry.Key, true, 0, 16, 0, 0, 0), null,
                    SourceReference: "v:" + Convert.ToBase64String(entry.Value.Span)), component: false);
                string path = entry.Key;
                for (int slash; (slash = path.LastIndexOf('/')) >= 0;)
                { path = path[..slash]; yield return codec.Encode(new(new(path, true, 0, 16, 0, 0, 0), null), component: false); }
            }
        }
        DirectoryMetadataRecord? pending = null; string? key = null;
        foreach (var encoded in MetadataExternalSort.Order(Records(), workingDirectory, ct))
        {
            var record = codec.Decode(encoded, component: false) with { Children = null };
            if (key == encoded.Key)
            {
                if (pending!.Entry.Path != record.Entry.Path || pending.SourceReference is not null && record.SourceReference is not null && pending.SourceReference != record.SourceReference)
                    throw new WorkspaceException("SnapshotCatalogInvalid", "A path marker has conflicting identities.");
                if (pending.SourceReference is null) pending = record;
                continue;
            }
            if (pending is not null) yield return pending;
            pending = record; key = encoded.Key;
        }
        if (pending is not null) yield return pending;
    }

    // File-only catalogs need ordinary directory records without retaining a
    // project-wide parent set. Identical synthesized parents are coalesced by
    // bounded external sort; aliases and all other duplicate records fail.
    public static IEnumerable<DirectoryMetadataRecord> WithImplicitDirectories(IEnumerable<DirectoryMetadataRecord> files,
        string workingDirectory, bool caseSensitive = false, CancellationToken ct = default)
    {
        using var scratch = new TemporaryMetadataDirectory();
        using var store = new PagedMetadataStore(scratch.Path, writable: true);
        var codec = new DirectoryMetadataTree(store, caseSensitive);
        IEnumerable<MetadataEntry> Records()
        {
            foreach (var file in files)
            {
                if (file.Entry.Directory) throw new WorkspaceException("SnapshotCatalogInvalid", "This input infers directories from file records only.");
                yield return codec.Encode(file, component: false);
                string path = file.Entry.Path;
                for (int slash; (slash = path.LastIndexOf('/')) >= 0;)
                {
                    path = path[..slash];
                    yield return codec.Encode(new(new(path, true, 0, 16, 0, 0, 0), null), component: false);
                }
            }
        }
        MetadataEntry? previous = null;
        foreach (var entry in MetadataExternalSort.Order(Records(), workingDirectory, ct))
        {
            if (previous?.Key == entry.Key)
            {
                var current = codec.Decode(entry, component: false); var prior = codec.Decode(previous, component: false);
                if (!current.Entry.Directory || !prior.Entry.Directory || !entry.Value.Span.SequenceEqual(previous.Value.Span))
                    throw new WorkspaceException("SnapshotCatalogInvalid", "Synthesized catalog paths collide.");
                continue;
            }
            previous = entry; yield return codec.Decode(entry, component: false) with { Children = null };
        }
    }
}
