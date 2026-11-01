using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Splinter.Core;

namespace Splinter.Runtime;

// A history has two ordered keys per entry: sequence and request identity.
// Values are streamed JSON chunks referenced by immutable binary pages. Small
// task control JSON contains only this root and count, regardless of age.
internal sealed class WorkspaceHistoryStorage<T>(string directory,
    Func<WorkspaceHistoryReference> reference, Action<WorkspaceHistoryReference> publish,
    Func<T, string> identity, Action<T>? bind = null, IJournalFilePins? filePins = null) : IWorkspaceHistoryStorage<T>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public long Count => reference().Count;
    public T Read(long index)
    {
        var snapshot = Validate();
        if (index < 0 || index >= snapshot.Count) throw new ArgumentOutOfRangeException(nameof(index));
        using var pages = new PagedMetadataStore(directory);
        return ReadValue(pages, pages.Open(snapshot.Root!).FindEntry(Sequence(index))
            ?? throw Invalid("A history sequence is missing."));
    }
    public T? Find(string key)
    {
        var snapshot = Validate(); if (snapshot.Count == 0) return default;
        using var pages = new PagedMetadataStore(directory); var tree = pages.Open(snapshot.Root!);
        var found = tree.Find(Request(key)); if (found is null) return default;
        if (found.Length != 8) throw Invalid("A history request reference is malformed.");
        long index = BinaryPrimitives.ReadInt64LittleEndian(found);
        if (index < 0 || index >= snapshot.Count) throw Invalid("A history request references a missing sequence.");
        T item = ReadValue(pages, tree.FindEntry(Sequence(index)) ?? throw Invalid("A history sequence is missing."));
        if (identity(item) != key) throw Invalid("A history request identity does not match its record.");
        return item;
    }
    public void Append(T item)
    {
        var snapshot = Validate(); string key = identity(item);
        if (string.IsNullOrEmpty(key)) throw Invalid("A history request identity is missing.");
        using var pages = new PagedMetadataStore(directory, writable: true, filePins: filePins);
        var root = snapshot.Root ?? pages.Empty;
        if (pages.Open(root).Find(Request(key)) is not null)
            throw Invalid("A history request is already recorded.");
        using var chunks = new ChunkWriter(pages);
        JsonSerializer.Serialize(chunks, item, Json); chunks.Complete();
        root = pages.Set(root, Sequence(snapshot.Count), ReadOnlySpan<byte>.Empty, chunks.Root);
        Span<byte> number = stackalloc byte[8]; BinaryPrimitives.WriteInt64LittleEndian(number, snapshot.Count);
        root = pages.Set(root, Request(key), number);
        pages.Flush();
        publish(new(1, checked(snapshot.Count + 1), root));
    }
    public IEnumerable<T> Enumerate(long start = 0)
    {
        var snapshot = Validate();
        if (start < 0 || start > snapshot.Count) throw new ArgumentOutOfRangeException(nameof(start));
        if (start == snapshot.Count) yield break;
        using var pages = new PagedMetadataStore(directory);
        long index = start;
        foreach (var entry in pages.Open(snapshot.Root!).Enumerate(start == 0 ? "n" : Sequence(start - 1)))
        {
            if (!entry.Key.StartsWith('n')) yield break;
            if (entry.Key != Sequence(index) || index >= snapshot.Count) throw Invalid("History sequences are incomplete or reordered.");
            T item = ReadValue(pages, entry);
            byte[]? request = pages.Open(snapshot.Root!).Find(Request(identity(item)));
            if (request is null || request.Length != 8 || BinaryPrimitives.ReadInt64LittleEndian(request) != index)
                throw Invalid("A history request is missing, duplicated or references another sequence.");
            yield return item; index++;
        }
        if (index != snapshot.Count) throw Invalid("A history sequence is missing.");
    }
    private WorkspaceHistoryReference Validate()
    {
        var snapshot = reference();
        if (snapshot.Version != 1 || snapshot.Count < 0 || snapshot.Count > long.MaxValue / 2 ||
            (snapshot.Count == 0 ? snapshot.Root is not null : snapshot.Root is null || snapshot.Root.Count != snapshot.Count * 2))
            throw Invalid("Preserve an invalid history root or count.");
        return snapshot;
    }
    private T ReadValue(PagedMetadataStore pages, MetadataEntry entry)
    {
        if (entry.Value.Length != 0 || entry.Child is null) throw Invalid("A history entry has no immutable value.");
        using var input = new ChunkReader(pages.Open(entry.Child).Enumerate().GetEnumerator());
        T item;
        try { item = JsonSerializer.Deserialize<T>(input, Json) ?? throw Invalid("A history value is empty."); }
        catch (JsonException) { throw Invalid("Preserve malformed or unsupported fields in an immutable history value."); }
        bind?.Invoke(item); return item;
    }
    private static string Sequence(long index) => "n" + index.ToString("x16", System.Globalization.CultureInfo.InvariantCulture);
    private static string Request(string key) => "k" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(key)));
    private static WorkspaceException Invalid(string message) => new("StateCorrupt", message);

    private sealed class ChunkWriter(PagedMetadataStore pages) : Stream
    {
        private readonly byte[] buffer = new byte[32768]; private int used; private long number;
        public MetadataTreeRoot Root { get; private set; } = pages.Empty;
        public void Complete() { if (used != 0) Emit(); }
        private void Emit()
        {
            Root = pages.Set(Root, number++.ToString("x16", System.Globalization.CultureInfo.InvariantCulture), buffer.AsSpan(0, used)); used = 0;
        }
        public override void Write(ReadOnlySpan<byte> value)
        {
            while (!value.IsEmpty)
            {
                int take = Math.Min(value.Length, buffer.Length - used); value[..take].CopyTo(buffer.AsSpan(used));
                used += take; value = value[take..]; if (used == buffer.Length) Emit();
            }
        }
        public override void Write(byte[] value, int offset, int count) => Write(value.AsSpan(offset, count));
        public override void Flush() => Complete();
        public override bool CanRead => false; public override bool CanSeek => false; public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException(); public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] value, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long value, SeekOrigin origin) => throw new NotSupportedException(); public override void SetLength(long value) => throw new NotSupportedException();
    }
    private sealed class ChunkReader(IEnumerator<MetadataEntry> chunks) : Stream
    {
        private ReadOnlyMemory<byte> current; private long number;
        public override int Read(Span<byte> value)
        {
            int written = 0;
            while (!value.IsEmpty)
            {
                if (current.IsEmpty)
                {
                    if (!chunks.MoveNext()) break;
                    var entry = chunks.Current;
                    if (entry.Key != number++.ToString("x16", System.Globalization.CultureInfo.InvariantCulture) || entry.Child is not null || entry.Value.IsEmpty || entry.Value.Length > 32768)
                        throw Invalid("History chunks are incomplete or malformed.");
                    current = entry.Value;
                }
                int take = Math.Min(value.Length, current.Length); current.Span[..take].CopyTo(value);
                current = current[take..]; value = value[take..]; written += take;
            }
            return written;
        }
        public override int Read(byte[] value, int offset, int count) => Read(value.AsSpan(offset, count));
        protected override void Dispose(bool disposing) { if (disposing) chunks.Dispose(); base.Dispose(disposing); }
        public override bool CanRead => true; public override bool CanSeek => false; public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException(); public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { } public override void Write(byte[] value, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long value, SeekOrigin origin) => throw new NotSupportedException(); public override void SetLength(long value) => throw new NotSupportedException();
    }
}
