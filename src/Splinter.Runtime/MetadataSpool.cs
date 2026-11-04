using Splinter.Core;

namespace Splinter.Runtime;

// Operation-owned binary scratch. Reads are repeatable cursors and never load
// the whole spool. Publishing a durable immutable root remains the caller's job.
public sealed class MetadataSpool : IDisposable
{
    internal static int MemoryBudget => StorageResources.SpoolBytes;
    private readonly string directory;
    private readonly int memoryLimit;
    private readonly ScratchBudget.Lease allocation;
    private readonly MemoryStream memory = new();
    private FileStream? output;
    private bool disposed;
    public MetadataSpool(string directory) : this(directory, MemoryBudget, StorageResources.Scratch) { }
    internal MetadataSpool(string directory, int memoryLimit, ScratchBudget budget)
    {
        this.directory = directory; this.memoryLimit = memoryLimit; allocation = budget.Open();
    }
    public void Append(MetadataEntry entry)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        // Includes the record checksum/header and the largest child reference.
        long charge = 128L + System.Text.Encoding.UTF8.GetByteCount(entry.Key) + entry.Value.Length;
        if (output is null)
        {
            long required = memory.Length + charge;
            if (required > memoryLimit) Spill();
            else if (required > memory.Capacity)
            {
                int capacity = (int)Math.Min(memoryLimit, Math.Max(required, Math.Max(65536L, memory.Capacity * 2L)));
                if (!allocation.TryResize(capacity)) Spill();
                else memory.Capacity = capacity;
            }
        }
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
        _ = allocation.TryResize(0);
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
    public void Dispose() { disposed = true; try { output?.Dispose(); memory.Dispose(); } finally { allocation.Dispose(); } }
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
    // Comparison-only marker streams need the same path/component/page
    // admission as a materialized catalog, without publishing unused pages.
    public static void ValidatePathMarker(MetadataEntry entry, bool caseSensitive = false)
    {
        if (entry.Key.Length == 0)
            throw new WorkspaceException("DirectoryMetadataInvalid", "A directory record has invalid field lengths.");
        var policy = caseSensitive ? WorkspacePathPolicy.Posix : WorkspacePathPolicy.Windows;
        bool ordinary;
        try { ordinary = policy.Normalize(entry.Key) == entry.Key; }
        catch (ArgumentException) { ordinary = false; }
        string[] components = entry.Key.Split('/');
        if (!ordinary)
        {
            // Materialized marker inputs admit synthesized parents first.
            // Preserve that failure precedence only on malformed inputs; valid
            // paths must not repeatedly split/normalize every growing prefix.
            string prefix = "";
            for (int i = 0; i < components.Length; i++)
            {
                prefix = i == 0 ? components[i] : prefix + "/" + components[i];
                if (prefix.Length == 0)
                    throw new WorkspaceException("DirectoryMetadataInvalid", "A directory record has invalid field lengths.");
                if (policy.Normalize(prefix) != prefix)
                    throw new WorkspaceException("SnapshotCatalogInvalid", "Catalog input requires unique ordinary paths and explicit directories.");
            }
        }
        for (int i = 0; i < components.Length; i++)
        {
            var encoded = DirectoryMetadataTree.EncodeRecord(new(new(components[i], true, 0, 16, 0, 0, 0), null,
                SourceReference: i == components.Length - 1 ? "v:" + Convert.ToBase64String(entry.Value.Span) : null), caseSensitive);
            PagedMetadataStore.ValidateEntry(encoded.Key, encoded.Value.Length);
        }
    }

    public static IEnumerable<DirectoryMetadataRecord> WithPathMarkers(IEnumerable<MetadataEntry> entries, string workingDirectory,
        bool caseSensitive = false, CancellationToken ct = default)
    {
        MetadataEntry Encode(DirectoryMetadataRecord record) => DirectoryMetadataTree.EncodeRecord(record, caseSensitive, component: false);
        DirectoryMetadataRecord Decode(MetadataEntry record) => DirectoryMetadataTree.DecodeRecord(record, caseSensitive, component: false);
        IEnumerable<MetadataEntry> Records()
        {
            foreach (var entry in entries)
            {
                yield return Encode(new(new(entry.Key, true, 0, 16, 0, 0, 0), null,
                    SourceReference: "v:" + Convert.ToBase64String(entry.Value.Span)));
                string path = entry.Key;
                for (int slash; (slash = path.LastIndexOf('/')) >= 0;)
                { path = path[..slash]; yield return Encode(new(new(path, true, 0, 16, 0, 0, 0), null)); }
            }
        }
        DirectoryMetadataRecord? pending = null; string? key = null;
        foreach (var encoded in MetadataExternalSort.Order(Records(), workingDirectory, ct))
        {
            var record = Decode(encoded) with { Children = null };
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
        MetadataEntry Encode(DirectoryMetadataRecord record) => DirectoryMetadataTree.EncodeRecord(record, caseSensitive, component: false);
        DirectoryMetadataRecord Decode(MetadataEntry record) => DirectoryMetadataTree.DecodeRecord(record, caseSensitive, component: false);
        IEnumerable<MetadataEntry> Records()
        {
            foreach (var file in files)
            {
                if (file.Entry.Directory) throw new WorkspaceException("SnapshotCatalogInvalid", "This input infers directories from file records only.");
                yield return Encode(file);
                string path = file.Entry.Path;
                for (int slash; (slash = path.LastIndexOf('/')) >= 0;)
                {
                    path = path[..slash];
                    yield return Encode(new(new(path, true, 0, 16, 0, 0, 0), null));
                }
            }
        }
        MetadataEntry? previous = null;
        foreach (var entry in MetadataExternalSort.Order(Records(), workingDirectory, ct))
        {
            if (previous?.Key == entry.Key)
            {
                var current = Decode(entry); var prior = Decode(previous);
                if (!current.Entry.Directory || !prior.Entry.Directory || !entry.Value.Span.SequenceEqual(previous.Value.Span))
                    throw new WorkspaceException("SnapshotCatalogInvalid", "Synthesized catalog paths collide.");
                continue;
            }
            previous = entry; yield return Decode(entry) with { Children = null };
        }
    }
}
