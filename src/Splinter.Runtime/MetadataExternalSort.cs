using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Splinter.Core;

namespace Splinter.Runtime;

// Bounded runs and bounded merge fan-in. Neither run membership nor the input
// is collected in a project-sized array. Failed work is preserved, not swept.
public static class MetadataExternalSort
{
    // A transient sort record can contain a complete filesystem path in both
    // key and value; persistent logical pages still contain component names.
    private const int MaximumRecordBytes = 8 * 32767 * 4 + 65536;
    private const int RunBudget = 4 * 1024 * 1024, FanIn = 8;
    private static readonly UTF8Encoding Utf8 = new(false, true);
    public static IEnumerable<MetadataEntry> Order(IEnumerable<MetadataEntry> input, string workingDirectory, CancellationToken ct, IJournalFilePins? filePins = null)
    {
        string directory = Path.Combine(workingDirectory, "sort-" + Guid.NewGuid().ToString("N"));
        WorkspaceStore.EnsurePlainPath(directory);
        bool completed = false, created = false;
        void CreateDirectory()
        {
            if (created) return;
            if (Directory.Exists(directory) || File.Exists(directory)) throw Invalid("Preserve an occupied sort directory.");
            Directory.CreateDirectory(directory); created = true;
            if (filePins is not null) MetadataScratchOwnership.MarkScope(directory, "metadata-sort");
        }
        try
        {
            var entries = new List<MetadataEntry>(); int bytes = 0; long runs = 0;
            foreach (var entry in input)
            {
                ct.ThrowIfCancellationRequested(); int charge = checked(Utf8.GetByteCount(entry.Key) * 2 + entry.Value.Length + 512);
                if (charge > MaximumRecordBytes) throw Invalid("Split oversized metadata records before sorting.");
                if (entries.Count > 0 && bytes > RunBudget - charge)
                { CreateDirectory(); WriteSorted(RunPath(directory, 0, runs++), entries, filePins); entries.Clear(); bytes = 0; }
                entries.Add(entry with { Value = entry.Value.ToArray() }); bytes += charge;
            }
            if (runs == 0)
            {
                entries.Sort((a, b) => StringComparer.Ordinal.Compare(a.Key, b.Key));
                foreach (var entry in entries) yield return entry;
                completed = true; yield break;
            }
            if (entries.Count > 0) WriteSorted(RunPath(directory, 0, runs++), entries, filePins);
            entries.Clear(); int phase = 0;
            while (runs > FanIn)
            {
                long next = 0; var group = new List<string>(FanIn);
                foreach (string path in Directory.EnumerateFiles(directory, "run-" + phase.ToString("x8") + "-*.bin"))
                {
                    group.Add(path); if (group.Count != FanIn) continue;
                    MergeTo(RunPath(directory, phase + 1, next++), group, ct, filePins); group.Clear();
                }
                if (group.Count > 0) MergeTo(RunPath(directory, phase + 1, next++), group, ct, filePins);
                phase++; runs = next;
            }
            var final = new List<string>(FanIn);
            foreach (string path in Directory.EnumerateFiles(directory, "run-" + phase.ToString("x8") + "-*.bin"))
            {
                if (final.Count == FanIn) throw Invalid("The owned sort run inventory changed.");
                final.Add(path);
            }
            foreach (var entry in Merge(final, ct)) yield return entry;
            completed = true;
        }
        finally
        {
            if (created && filePins is not null) _ = MetadataScratchOwnership.TryReclaimScope(directory, "metadata-sort", filePins);
            else if (completed && created)
            {
                foreach (string path in Directory.EnumerateFiles(directory))
                {
                    string name = Path.GetFileName(path);
                    if (!name.StartsWith("run-", StringComparison.Ordinal) || !name.EndsWith(".bin", StringComparison.Ordinal))
                        throw Invalid("Preserve an unexpected item in the owned sort directory.");
                    WorkspaceStore.EnsurePlainPath(path); File.Delete(path);
                }
                Directory.Delete(directory);
            }
        }
    }
    private static string RunPath(string directory, int phase, long run) => Path.Combine(directory, "run-" + phase.ToString("x8") + "-" + run.ToString("x16") + ".bin");
    private static void WriteSorted(string path, List<MetadataEntry> entries, IJournalFilePins? filePins)
    {
        entries.Sort((a, b) => StringComparer.Ordinal.Compare(a.Key, b.Key));
        using var stream = NewRun(path, filePins);
        foreach (var entry in entries) Write(stream, entry);
    }
    private static FileStream NewRun(string path, IJournalFilePins? filePins)
    {
        WorkspaceStore.EnsurePlainPath(path); var stream = MetadataScratchOwnership.Create(path, FileAccess.ReadWrite, FileShare.Read, 65536, FileOptions.None, filePins);
        try { stream.Write("SMS1"u8); return stream; }
        catch { stream.Dispose(); throw; }
    }
    private static void MergeTo(string path, IReadOnlyList<string> sources, CancellationToken ct, IJournalFilePins? filePins)
    {
        using (var stream = NewRun(path, filePins)) foreach (var entry in Merge(sources, ct)) Write(stream, entry);
        foreach (string source in sources) MetadataScratchOwnership.Remove(source, filePins);
    }
    private static IEnumerable<MetadataEntry> Merge(IReadOnlyList<string> paths, CancellationToken ct)
    {
        var streams = new List<FileStream>(FanIn);
        var queue = new PriorityQueue<(MetadataEntry Entry, int Run), (string Key, int Run)>(
            Comparer<(string Key, int Run)>.Create((a, b) =>
            { int order = StringComparer.Ordinal.Compare(a.Key, b.Key); return order != 0 ? order : a.Run.CompareTo(b.Run); }));
        try
        {
            byte[] header = new byte[4];
            foreach (string path in paths)
            {
                ct.ThrowIfCancellationRequested(); WorkspaceStore.EnsurePlainPath(path);
                var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536); streams.Add(stream);
                stream.ReadExactly(header);
                if (!header.AsSpan().SequenceEqual("SMS1"u8)) throw Invalid("An owned sort run has an invalid header.");
                var first = Read(stream); if (first is not null) queue.Enqueue((first, streams.Count - 1), (first.Key, streams.Count - 1));
            }
            while (queue.TryDequeue(out var item, out _))
            {
                ct.ThrowIfCancellationRequested(); yield return item.Entry;
                var next = Read(streams[item.Run]);
                if (next is not null) queue.Enqueue((next, item.Run), (next.Key, item.Run));
            }
        }
        finally { foreach (var stream in streams) stream.Dispose(); }
    }
    internal static void Write(Stream output, MetadataEntry entry)
    {
        using var body = new MemoryStream();
        using (var writer = new BinaryWriter(body, Utf8, true))
        {
            byte[] key = Utf8.GetBytes(entry.Key);
            writer.Write(key.Length); writer.Write(entry.Value.Length); writer.Write(key); writer.Write(entry.Value.Span);
            writer.Write(entry.Child is not null);
            if (entry.Child is { } child)
            {
                if (child.Version != 1 || child.StoreId.Length != 24 || child.StoreId.Any(c => !char.IsAsciiHexDigit(c)))
                    throw Invalid("An owned sort record has an invalid child store.");
                writer.Write(Convert.FromHexString(child.StoreId)); writer.Write(child.Page is not null);
                if (child.Page is { } page)
                {
                    writer.Write(page.Container); writer.Write(page.Offset); writer.Write(page.Bytes); writer.Write(page.Count);
                    writer.Write(Convert.FromHexString(page.ContentHash));
                }
            }
        }
        if (body.Length > MaximumRecordBytes) throw Invalid("Split oversized sort records into logical pages.");
        Span<byte> header = stackalloc byte[36]; BinaryPrimitives.WriteInt32LittleEndian(header, checked((int)body.Length));
        SHA256.HashData(body.GetBuffer().AsSpan(0, (int)body.Length), header[4..]); output.Write(header);
        output.Write(body.GetBuffer().AsSpan(0, (int)body.Length));
    }
    internal static MetadataEntry? Read(Stream input)
    {
        if (input.Position == input.Length) return null;
        Span<byte> header = stackalloc byte[36]; input.ReadExactly(header);
        int length = BinaryPrimitives.ReadInt32LittleEndian(header);
        if (length is < 9 or > MaximumRecordBytes || length > input.Length - input.Position)
            throw Invalid("An owned sort record is truncated or oversized.");
        byte[] body = new byte[length]; input.ReadExactly(body);
        if (!SHA256.HashData(body).AsSpan().SequenceEqual(header[4..])) throw Invalid("An owned sort record checksum changed.");
        using var memory = new MemoryStream(body, writable: false); using var reader = new BinaryReader(memory, Utf8);
        int keyBytes = reader.ReadInt32(), valueBytes = reader.ReadInt32();
        if (keyBytes < 1 || valueBytes < 0 || (long)keyBytes + valueBytes + 9 > body.Length) throw Invalid("An owned sort record has invalid lengths.");
        string key = Utf8.GetString(reader.ReadBytes(keyBytes)); byte[] value = reader.ReadBytes(valueBytes);
        MetadataTreeRoot? child = null;
        if (reader.ReadBoolean())
        {
            byte[] id = reader.ReadBytes(12); if (id.Length != 12) throw Invalid("An owned sort child identity is truncated.");
            MetadataPageReference? page = null;
            if (reader.ReadBoolean())
            {
                long container = reader.ReadInt64(), offset = reader.ReadInt64(); int bytes = reader.ReadInt32(); long count = reader.ReadInt64();
                byte[] digest = reader.ReadBytes(32); if (digest.Length != 32) throw Invalid("An owned sort child hash is truncated.");
                page = new(container, offset, bytes, count, Convert.ToHexStringLower(digest));
            }
            child = new(1, Convert.ToHexStringLower(id), page);
        }
        if (memory.Position != memory.Length) throw Invalid("An owned sort record has trailing bytes.");
        return new(key, value, child);
    }
    private static WorkspaceException Invalid(string message) => new("MetadataSortInvalid", message);
}
