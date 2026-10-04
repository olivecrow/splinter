using System.Buffers;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Splinter.Core;
using static Splinter.SourceControl.Git.GitObjectSnapshotProvider;

namespace Splinter.SourceControl.Git;

// cat-file streams raw object bytes, including packed objects. A requested range
// is staged until the entire blob hash has verified. Scratch is bounded, there
// is no read-to-disk cache; full-blob decoding cost is intentionally observable.
public sealed class GitObjectSnapshotTree : ISnapshotTree, IDisposable
{
    public SnapshotReference Snapshot { get; }
    private readonly Func<SnapshotObservation> health;
    private readonly Dictionary<string, SnapshotEntry> entries = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> blobs = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<SnapshotEntry>> children = new(StringComparer.OrdinalIgnoreCase);
    private readonly object readGate = new();
    private Process? reader;
    private Task<string>? stderr;
    private bool disposed;
    public long DecodedBlobBytes { get; private set; }

    internal GitObjectSnapshotTree(SnapshotReference snapshot, string tree, string revision, string reference, Func<SnapshotObservation> health)
    {
        Snapshot = snapshot; this.health = health;
        EnsureAvailable();
        long timestamp = snapshot.CapturedAt.UtcDateTime.ToFileTimeUtc();
        Add(new("", true, 0, (uint)FileAttributes.Directory, timestamp, timestamp, timestamp));
        VerifyObject(snapshot.RootPath, tree, "tree");
        string listing = Command(snapshot.RootPath, ["ls-tree", "-r", "-t", "-l", "-z", tree], CancellationToken.None,
            failureCode: "GitBasisObjectInvalid").GetAwaiter().GetResult();
        var subtrees = new HashSet<string>(StringComparer.Ordinal);
        foreach (string line in listing.Split('\0', StringSplitOptions.RemoveEmptyEntries))
        {
            int tab = line.IndexOf('\t');
            if (tab < 0) throw Failure("GitBasisUnsupported", "Invalid project tree entry.");
            string[] fields = line[..tab].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            string path = line[(tab + 1)..];
            if (fields.Length == 4 && fields[0] == "040000" && fields[1] == "tree" && fields[3] == "-")
            {
                RequireObjectId(fields[2]); ValidateRecordedPath(path); subtrees.Add(fields[2]);
                EnsureParents(path, timestamp);
                Add(new(path, true, 0, (uint)FileAttributes.Directory, timestamp, timestamp, timestamp));
                continue;
            }
            if (fields.Length != 4 || fields[0] is not ("100644" or "100755") || fields[1] != "blob" ||
                !long.TryParse(fields[3], NumberStyles.None, CultureInfo.InvariantCulture, out long length) || length < 0)
                throw Failure("GitBasisUnsupported", "Only regular committed files are supported; no symlink/submodule fallback.");
            RequireObjectId(fields[2]); ValidateRecordedPath(path);
            EnsureParents(path, timestamp);
            Add(new(path, false, length, (uint)FileAttributes.Archive, timestamp, timestamp, timestamp));
            blobs.Add(path, fields[2]);
        }
        foreach (string subtree in subtrees) VerifyObject(snapshot.RootPath, subtree, "tree");
        // metadata listing uses the fixed tree, never the branch or mutable index.
        _ = revision; _ = reference;
    }
    private void EnsureParents(string path, long timestamp)
    {
        string parent = Parent(path); var parents = new Stack<string>();
        while (!entries.ContainsKey(parent)) { parents.Push(parent); parent = Parent(parent); }
        if (!entries[parent].Directory || entries[parent].Path != parent)
            throw Failure("GitBasisUnsupported", "A file or case alias shadows a directory.");
        while (parents.TryPop(out string? directory)) Add(new(directory, true, 0, (uint)FileAttributes.Directory, timestamp, timestamp, timestamp));
    }
    internal static void VerifyObject(string common, string objectId, string type)
    {
        long length = long.Parse(Command(common, ["cat-file", "-s", objectId], CancellationToken.None,
            failureCode: "GitBasisObjectInvalid").GetAwaiter().GetResult(), CultureInfo.InvariantCulture);
        using var process = Process.Start(StartInfo(common, ["cat-file", type, objectId])) ?? throw Failure("GitBasisUnavailable", "Git did not start.");
        var error = process.StandardError.ReadToEndAsync(); process.StandardInput.Close();
        using var timeout = new Timer(_ => { try { if (!process.HasExited) process.Kill(); } catch (InvalidOperationException) { } },
            null, TimeSpan.FromSeconds(30), Timeout.InfiniteTimeSpan);
        using var hash = IncrementalHash.CreateHash(objectId.Length == 40 ? HashAlgorithmName.SHA1 : HashAlgorithmName.SHA256);
        hash.AppendData(Encoding.ASCII.GetBytes(type + " " + length.ToString(CultureInfo.InvariantCulture) + "\0"));
        byte[] scratch = ArrayPool<byte>.Shared.Rent(65536);
        long read = 0;
        try
        {
            int n;
            while ((n = process.StandardOutput.BaseStream.Read(scratch, 0, 65536)) != 0)
            { read += n; if (read > length) break; hash.AppendData(scratch.AsSpan(0, n)); }
            if (!process.WaitForExit(1000)) { process.Kill(); process.WaitForExit(); }
            _ = error.GetAwaiter().GetResult();
            if (process.ExitCode != 0 || read != length || Convert.ToHexStringLower(hash.GetHashAndReset()) != objectId)
                throw Failure("GitBasisObjectInvalid", "The fixed commit/tree hash failed; preserve the object store and receipt.");
        }
        finally { if (!process.HasExited) { process.Kill(); process.WaitForExit(); } ArrayPool<byte>.Shared.Return(scratch); }
    }
    public void EnsureAvailable()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        var observed = health();
        if (observed.Id != Snapshot.Id || observed.Available != true)
            throw Failure(observed.FailureCode ?? "SnapshotObservationUnknown", "Preserve this task's immutable object basis.");
    }
    public SnapshotEntry? Stat(string relativePath)
    { EnsureAvailable(); return entries.GetValueOrDefault(Normalize(relativePath)); }
    public IReadOnlyList<SnapshotEntry> List(string relativeDirectory)
    {
        EnsureAvailable(); string path = Normalize(relativeDirectory);
        if (!entries.TryGetValue(path, out var entry)) throw new DirectoryNotFoundException(path);
        if (!entry.Directory) throw new IOException("Not a directory.");
        return children[path].ToArray();
    }
    public int Read(string relativePath, long offset, Span<byte> destination)
    {
        EnsureAvailable(); string path = Normalize(relativePath);
        if (offset < 0 || destination.Length > 1024 * 1024) throw new ArgumentOutOfRangeException(nameof(offset));
        if (!blobs.TryGetValue(path, out string? objectId)) throw new FileNotFoundException(path);
        long length = entries[path].Length;
        int count = offset >= length ? 0 : (int)Math.Min(destination.Length, length - offset);
        if (count == 0) return 0;
        lock (readGate)
        {
            EnsureAvailable();
            byte[] scratch = ArrayPool<byte>.Shared.Rent(65536), staged = ArrayPool<byte>.Shared.Rent(count);
            try
            {
                if (reader is null)
                {
                    reader = Process.Start(StartInfo(Snapshot.RootPath, ["cat-file", "--batch"])) ?? throw Failure("GitBasisUnavailable", "Git reader did not start.");
                    stderr = reader.StandardError.ReadToEndAsync();
                }
                var process = reader;
                using var timeout = new Timer(_ => { try { if (!process.HasExited) process.Kill(); } catch (InvalidOperationException) { } },
                    null, TimeSpan.FromSeconds(30), Timeout.InfiniteTimeSpan);
                process.StandardInput.Write(objectId + "\n"); process.StandardInput.Flush();
                var output = process.StandardOutput.BaseStream;
                string header = Header(output);
                if (header != objectId + " blob " + length.ToString(CultureInfo.InvariantCulture))
                    throw Failure("GitBasisObjectInvalid", "The fixed blob is missing or its header changed.");
                using var hash = IncrementalHash.CreateHash(objectId.Length == 40 ? HashAlgorithmName.SHA1 : HashAlgorithmName.SHA256);
                hash.AppendData(Encoding.ASCII.GetBytes("blob " + length.ToString(CultureInfo.InvariantCulture) + "\0"));
                for (long position = 0; position < length;)
                {
                    int n = output.Read(scratch, 0, (int)Math.Min(65536, length - position));
                    if (n == 0) throw Failure("GitBasisObjectInvalid", "A fixed blob ended before its recorded length.");
                    hash.AppendData(scratch.AsSpan(0, n)); DecodedBlobBytes += n;
                    long begin = Math.Max(position, offset), end = Math.Min(position + n, offset + count);
                    if (end > begin) scratch.AsSpan((int)(begin - position), (int)(end - begin)).CopyTo(staged.AsSpan((int)(begin - offset)));
                    position += n;
                }
                if (output.ReadByte() != '\n' || Convert.ToHexStringLower(hash.GetHashAndReset()) != objectId)
                    throw Failure("GitBasisObjectInvalid", "The recorded blob hash failed; no requested bytes are published.");
                staged.AsSpan(0, count).CopyTo(destination); return count;
            }
            catch (Exception ex) when (ex is IOException or InvalidOperationException or WorkspaceException)
            { StopReader(); throw ex is WorkspaceException ? ex : Failure("GitBasisObjectInvalid", "The owned blob reader failed; preserve the basis and journal."); }
            finally { ArrayPool<byte>.Shared.Return(scratch); ArrayPool<byte>.Shared.Return(staged); }
        }
    }
    private static string Header(Stream stream)
    {
        Span<byte> header = stackalloc byte[256];
        for (int i = 0; i < header.Length; i++)
        {
            int b = stream.ReadByte(); if (b < 0) break;
            if (b == '\n') return Encoding.ASCII.GetString(header[..i]);
            header[i] = (byte)b;
        }
        throw Failure("GitBasisObjectInvalid", "Invalid bounded cat-file header.");
    }
    private void Add(SnapshotEntry entry)
    {
        if (entries.Count >= 1000000 || !entries.TryAdd(entry.Path, entry))
            throw Failure("GitBasisUnsupported", "Project paths collide or exceed the metadata limit.");
        if (entry.Directory) children.Add(entry.Path, []);
        if (entry.Path.Length != 0) children[Parent(entry.Path)].Add(entry);
    }
    private static string Parent(string path) => path.Contains('/') ? path[..path.LastIndexOf('/')] : "";
    private static string Normalize(string path)
    {
        string normalized = path.Replace('\\', '/').Trim('/');
        if (normalized.Length != 0) ValidateRecordedPath(normalized);
        return normalized;
    }
    private static void ValidateRecordedPath(string path)
    {
        if (path.Length == 0 || path.Contains('\\') || path.Any(c => char.IsControl(c) || ":*?\"<>|".Contains(c)) ||
            path.Split('/').Any(p => p.Length == 0 || p is "." or ".." || p.Equals(".git", StringComparison.OrdinalIgnoreCase) ||
                p.EndsWith('.') || p.EndsWith(' ') || Reserved(p)))
            throw Failure("GitBasisUnsupported", "Recorded paths must be unambiguous regular Windows project paths.");
    }
    private static bool Reserved(string part)
    {
        string stem = part.Split('.')[0].ToUpperInvariant();
        return stem is "CON" or "PRN" or "AUX" or "NUL" || stem.Length == 4 &&
            (stem.StartsWith("COM", StringComparison.Ordinal) || stem.StartsWith("LPT", StringComparison.Ordinal)) && stem[3] is >= '1' and <= '9';
    }
    private void StopReader()
    {
        if (reader is null) return;
        try { reader.StandardInput.Close(); if (!reader.WaitForExit(1000)) { reader.Kill(); reader.WaitForExit(); } }
        finally { reader.Dispose(); reader = null; stderr = null; }
    }
    public void Dispose() { lock (readGate) { StopReader(); disposed = true; } }
}
