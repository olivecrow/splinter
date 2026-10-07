using System.Buffers;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Splinter.Core;
using static Splinter.SourceControl.Git.GitObjectSnapshotProvider;

namespace Splinter.SourceControl.Git;

// cat-file streams raw object bytes, including packed objects. A requested range
// is staged until a full blob proof or its verified chunk proofs match. Only
// bounded blob proofs and two two-chunk reader sessions are retained, never disk payload.
public sealed class GitObjectSnapshotTree : ISnapshotTree, IDisposable
{
    public SnapshotReference Snapshot { get; }
    private readonly Func<SnapshotObservation> health;
    private readonly WorkspacePathPolicy policy;
    private readonly Dictionary<string, SnapshotEntry> entries;
    private readonly Dictionary<string, string> blobs;
    private readonly Dictionary<string, List<SnapshotEntry>> children;
    private readonly object readGate = new();
    private bool disposed;
    private const int ChunkBytes = 65536, MaxProofBytes = 4 * 1024 * 1024;
    private const int MaxProofEntries = 4096, MaxReaderSessions = 2;
    private sealed record BlobProof(string ObjectId, long Length, byte[] Chunks);
    private readonly Dictionary<string, LinkedListNode<BlobProof>> proofs = new(StringComparer.Ordinal);
    private readonly LinkedList<BlobProof> proofLru = new();
    private int proofBytes;
    private readonly int proofByteLimit, proofEntryLimit;
    internal int CachedProofBytes => proofBytes;
    internal int CachedProofCount => proofs.Count;
    internal int PeakProofBytes { get; private set; }
    internal int FullBlobProofs { get; private set; }
    private sealed class PayloadChunk
    {
        internal readonly byte[] Bytes = new byte[ChunkBytes];
        internal long Offset = -1;
        internal int Length;
    }
    private sealed class ReaderSession(string objectId)
    {
        internal string ObjectId = objectId;
        internal Process? Reader;
        internal GitProgressReadStream? Output;
        internal Task<string>? Error;
        internal readonly PayloadChunk[] Chunks = [new(), new()];
        internal int Newest = -1;
        internal long Position;
        internal bool Streaming;
        internal PayloadChunk NextChunk() => Chunks[Newest = (Newest + 1) % Chunks.Length];
    }
    private readonly Dictionary<string, LinkedListNode<ReaderSession>> sessions = new(StringComparer.Ordinal);
    private readonly LinkedList<ReaderSession> sessionLru = new();
    internal int ReaderProcessStarts { get; private set; }
    internal int ActiveReaderProcesses { get; private set; }
    internal int PeakReaderProcesses { get; private set; }
    internal int CachedPayloadBytes => sessions.Count * 2 * ChunkBytes;
    internal int[] ReaderProcessIds => sessions.Values.Where(s => s.Value.Reader is not null).Select(s => s.Value.Reader!.Id).ToArray();
    public long DecodedBlobBytes { get; private set; }

    internal GitObjectSnapshotTree(SnapshotReference snapshot, string tree, string revision, string reference, Func<SnapshotObservation> health,
        int proofByteLimit = MaxProofBytes, int proofEntryLimit = MaxProofEntries)
    {
        if (proofByteLimit is < 1 or > MaxProofBytes || proofEntryLimit is < 1 or > MaxProofEntries)
            throw new ArgumentOutOfRangeException(nameof(proofByteLimit));
        this.proofByteLimit = proofByteLimit; this.proofEntryLimit = proofEntryLimit;
        Snapshot = snapshot; this.health = health;
        policy = WorkspacePathPolicy.For(snapshot);
        entries = new(policy.Comparer); blobs = new(policy.Comparer); children = new(policy.Comparer);
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
            Add(new(path, false, length, (uint)FileAttributes.Archive, timestamp, timestamp, timestamp,
                fields[0] == "100755" ? 0x1EDu : 0x1A4u));
            blobs.Add(path, fields[2]);
        }
        VerifySubtrees(snapshot.RootPath, subtrees);
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
        using var output = ProgressOutput(process);
        using var hash = IncrementalHash.CreateHash(objectId.Length == 40 ? HashAlgorithmName.SHA1 : HashAlgorithmName.SHA256);
        hash.AppendData(Encoding.ASCII.GetBytes(type + " " + length.ToString(CultureInfo.InvariantCulture) + "\0"));
        byte[] scratch = ArrayPool<byte>.Shared.Rent(65536);
        long read = 0;
        try
        {
            int n;
            while ((n = output.Read(scratch, 0, 65536)) != 0)
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
    private static void VerifySubtrees(string common, IReadOnlyCollection<string> objects)
    {
        if (objects.Count == 0) return;
        using var process = Process.Start(StartInfo(common, ["cat-file", "--batch"])) ?? throw Failure("GitBasisUnavailable", "Git did not start.");
        var error = process.StandardError.ReadToEndAsync();
        using var output = ProgressOutput(process);
        byte[] scratch = ArrayPool<byte>.Shared.Rent(ChunkBytes);
        try
        {
            foreach (string objectId in objects)
            {
                process.StandardInput.Write(objectId + "\n"); process.StandardInput.Flush();
                string[] header = Header(output).Split(' ');
                if (header.Length != 3 || header[0] != objectId || header[1] != "tree" ||
                    !long.TryParse(header[2], NumberStyles.None, CultureInfo.InvariantCulture, out long length) || length < 0)
                    throw Failure("GitBasisObjectInvalid", "The fixed subtree header is invalid.");
                using var hash = IncrementalHash.CreateHash(objectId.Length == 40 ? HashAlgorithmName.SHA1 : HashAlgorithmName.SHA256);
                hash.AppendData(Encoding.ASCII.GetBytes("tree " + length.ToString(CultureInfo.InvariantCulture) + "\0"));
                for (long position = 0; position < length;)
                {
                    int n = (int)Math.Min(ChunkBytes, length - position);
                    output.ReadExactly(scratch.AsSpan(0, n)); hash.AppendData(scratch.AsSpan(0, n)); position += n;
                }
                if (output.ReadByte() != '\n' || Convert.ToHexStringLower(hash.GetHashAndReset()) != objectId)
                    throw Failure("GitBasisObjectInvalid", "The fixed subtree hash failed; preserve the object store and receipt.");
            }
            process.StandardInput.Close();
            if (!process.WaitForExit(1000)) { process.Kill(entireProcessTree: true); process.WaitForExit(); }
            _ = error.GetAwaiter().GetResult();
            if (process.ExitCode != 0) throw Failure("GitBasisObjectInvalid", "The fixed subtree reader failed.");
        }
        catch (IOException) { throw Failure("GitBasisObjectInvalid", "The fixed subtree stream is incomplete."); }
        finally
        {
            if (!process.HasExited) { process.Kill(entireProcessTree: true); process.WaitForExit(); }
            ArrayPool<byte>.Shared.Return(scratch);
        }
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
            byte[] staged = ArrayPool<byte>.Shared.Rent(count);
            try
            {
                bool proved = proofs.TryGetValue(objectId, out var cached);
                if (proved)
                {
                    if (cached!.Value.Length != length) throw Failure("GitBasisObjectInvalid", "The fixed blob proof length changed.");
                    proofLru.Remove(cached); proofLru.AddLast(cached);
                }
                else
                {
                    long proofLength = ((length - 1) / ChunkBytes + 1) * 32;
                    if (proofLength > proofByteLimit) throw Failure("GitBasisUnsupported", "A recorded blob exceeds the bounded chunk-proof limit.");
                    // Include the in-flight proof in the original 4MiB budget.
                    // Retire its reader too: no retained payload outlives its proof.
                    while (proofBytes + proofLength > proofByteLimit || proofs.Count >= proofEntryLimit)
                    {
                        var oldest = proofLru.First!;
                        RemoveSession(oldest.Value.ObjectId);
                        proofBytes -= oldest.Value.Chunks.Length;
                        proofs.Remove(oldest.Value.ObjectId); proofLru.RemoveFirst();
                    }
                }
                var session = GetSession(objectId);
                if (!proved)
                {
                    byte[] proof = new byte[(int)(((length - 1) / ChunkBytes + 1) * 32)];
                    PeakProofBytes = Math.Max(PeakProofBytes, proofBytes + proof.Length);
                    var output = BeginBlob(session, length);
                    using var hash = IncrementalHash.CreateHash(objectId.Length == 40 ? HashAlgorithmName.SHA1 : HashAlgorithmName.SHA256);
                    hash.AppendData(Encoding.ASCII.GetBytes("blob " + length.ToString(CultureInfo.InvariantCulture) + "\0"));
                    for (long position = 0; position < length;)
                    {
                        var chunk = session.NextChunk();
                        int n = (int)Math.Min(ChunkBytes, length - position);
                        output.ReadExactly(chunk.Bytes.AsSpan(0, n)); DecodedBlobBytes += n;
                        hash.AppendData(chunk.Bytes.AsSpan(0, n));
                        SHA256.HashData(chunk.Bytes.AsSpan(0, n), proof.AsSpan((int)(position / ChunkBytes) * 32, 32));
                        long begin = Math.Max(position, offset), end = Math.Min(position + n, offset + count);
                        if (end > begin) chunk.Bytes.AsSpan((int)(begin - position), (int)(end - begin)).CopyTo(staged.AsSpan((int)(begin - offset)));
                        chunk.Offset = position; chunk.Length = n; position += n;
                    }
                    if (output.ReadByte() != '\n' || Convert.ToHexStringLower(hash.GetHashAndReset()) != objectId)
                        throw Failure("GitBasisObjectInvalid", "The recorded blob hash failed; no requested bytes are published.");
                    proofs.Add(objectId, proofLru.AddLast(new BlobProof(objectId, length, proof))); proofBytes += proof.Length; FullBlobProofs++;
                    session.Streaming = false; session.Position = length;
                }
                else
                {
                    for (long at = offset; at < offset + count;)
                    {
                        var chunk = LoadProvedChunk(session, length, cached!.Value.Chunks, at);
                        int n = (int)Math.Min(offset + count - at, chunk.Offset + chunk.Length - at);
                        chunk.Bytes.AsSpan((int)(at - chunk.Offset), n).CopyTo(staged.AsSpan((int)(at - offset))); at += n;
                    }
                }
                staged.AsSpan(0, count).CopyTo(destination); return count;
            }
            catch (Exception ex) when (ex is IOException or InvalidOperationException or WorkspaceException or System.ComponentModel.Win32Exception)
            {
                try { ResetProof(); }
                catch (Exception cleanup) when (cleanup is IOException or InvalidOperationException or System.ComponentModel.Win32Exception) { }
                throw ex is WorkspaceException ? ex : Failure("GitBasisObjectInvalid", "The owned blob reader failed; preserve the basis and journal.");
            }
            finally { ArrayPool<byte>.Shared.Return(staged); }
        }
    }
    private ReaderSession GetSession(string objectId)
    {
        if (sessions.TryGetValue(objectId, out var found))
        {
            sessionLru.Remove(found); sessionLru.AddLast(found); return found.Value;
        }
        ReaderSession session;
        if (sessions.Count == MaxReaderSessions)
        {
            var oldest = sessionLru.First!;
            session = oldest.Value;
            sessions.Remove(session.ObjectId); sessionLru.Remove(oldest);
            try
            {
                // A drained batch reader can accept another object immediately.
                // Only an unread suffix requires cancellation on reassignment.
                if (session.Streaming) StopReader(session);
            }
            catch
            {
                // ResetProof must still find this reader if cleanup failed.
                sessions.Add(session.ObjectId, sessionLru.AddFirst(session)); throw;
            }
            session.ObjectId = objectId; session.Position = 0; session.Newest = -1;
            foreach (var chunk in session.Chunks) { chunk.Offset = -1; chunk.Length = 0; }
        }
        else session = new ReaderSession(objectId);
        sessions.Add(objectId, sessionLru.AddLast(session)); return session;
    }
    private void RemoveSession(string objectId)
    {
        if (!sessions.Remove(objectId, out var node)) return;
        sessionLru.Remove(node);
        StopReader(node.Value);
    }
    private Stream BeginBlob(ReaderSession session, long length)
    {
        if (session.Streaming) StopReader(session);
        if (session.Reader is null)
        {
            session.Reader = Process.Start(StartInfo(Snapshot.RootPath, ["cat-file", "--batch"])) ?? throw Failure("GitBasisUnavailable", "Git reader did not start.");
            ReaderProcessStarts++; ActiveReaderProcesses++;
            PeakReaderProcesses = Math.Max(PeakReaderProcesses, ActiveReaderProcesses);
            session.Error = session.Reader.StandardError.ReadToEndAsync();
            session.Output = ProgressOutput(session.Reader);
        }
        session.Reader.StandardInput.Write(session.ObjectId + "\n"); session.Reader.StandardInput.Flush();
        var output = session.Output!;
        if (Header(output) != session.ObjectId + " blob " + length.ToString(CultureInfo.InvariantCulture)) throw Failure("GitBasisObjectInvalid", "The fixed blob is missing or its header changed.");
        foreach (var chunk in session.Chunks) { chunk.Offset = -1; chunk.Length = 0; }
        session.Position = 0; session.Streaming = true; return output;
    }
    private PayloadChunk LoadProvedChunk(ReaderSession session, long length, byte[] proof, long at)
    {
        foreach (var retained in session.Chunks)
            if (at >= retained.Offset && at < retained.Offset + retained.Length) return retained;
        if (!session.Streaming || at < session.Position) BeginBlob(session, length);
        var output = session.Output!;
        Span<byte> digest = stackalloc byte[32];
        PayloadChunk chunk;
        do
        {
            chunk = session.NextChunk();
            int n = (int)Math.Min(ChunkBytes, length - session.Position);
            output.ReadExactly(chunk.Bytes.AsSpan(0, n)); DecodedBlobBytes += n;
            SHA256.HashData(chunk.Bytes.AsSpan(0, n), digest);
            if (!digest.SequenceEqual(proof.AsSpan((int)(session.Position / ChunkBytes) * 32, 32))) throw Failure("GitBasisObjectInvalid", "The requested chunk does not match the fully verified fixed blob; no bytes are published.");
            chunk.Offset = session.Position; chunk.Length = n; session.Position += n;
        } while (at >= session.Position);
        if (session.Position == length)
        {
            if (output.ReadByte() != '\n') throw Failure("GitBasisObjectInvalid", "The fixed blob framing changed.");
            session.Streaming = false;
        }
        return chunk;
    }
    private void ResetProof()
    {
        Exception? failure = null;
        try
        {
            // One cleanup failure must not strand another owned decoder.
            foreach (var node in sessions.Values)
                try { StopReader(node.Value); }
                catch (Exception ex) when (ex is IOException or InvalidOperationException or System.ComponentModel.Win32Exception) { failure ??= ex; }
        }
        finally { sessions.Clear(); sessionLru.Clear(); proofs.Clear(); proofLru.Clear(); proofBytes = 0; }
        if (failure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
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
    private string Normalize(string path)
    {
        string normalized = policy.Normalize(path);
        if (normalized.Length != 0) ValidateRecordedPath(normalized);
        return normalized;
    }
    private void ValidateRecordedPath(string path)
    {
        bool invalid;
        try { invalid = path.Length == 0 || policy.Normalize(path) != path; }
        catch (ArgumentException) { invalid = true; }
        if (invalid || path.Split('/').Any(p => p.Equals(".git", policy.Comparison)) ||
            !policy.CaseSensitive && (path.Contains('\\') || path.Any(c => char.IsControl(c) || ":*?\"<>|".Contains(c)) ||
                path.Split('/').Any(Reserved)))
            throw Failure("GitBasisUnsupported", "Recorded paths must be unambiguous regular paths under the immutable tree policy.");
    }
    private static bool Reserved(string part)
    {
        string stem = part.Split('.')[0].ToUpperInvariant();
        return stem is "CON" or "PRN" or "AUX" or "NUL" || stem.Length == 4 &&
            (stem.StartsWith("COM", StringComparison.Ordinal) || stem.StartsWith("LPT", StringComparison.Ordinal)) && stem[3] is >= '1' and <= '9';
    }
    private void StopReader(ReaderSession session)
    {
        if (session.Reader is not { } reader) return;
        try
        {
            session.Output?.Dispose();
            // A paused decoder may be blocked on its unread suffix. Close its
            // output before stdin EOF; eviction never drains or caches a blob.
            reader.StandardOutput.Close();
            reader.StandardInput.Close();
        }
        finally
        {
            try
            {
                if (!reader.WaitForExit(1000)) { reader.Kill(entireProcessTree: true); reader.WaitForExit(); }
                _ = session.Error?.GetAwaiter().GetResult();
            }
            finally
            {
                reader.Dispose(); session.Reader = null; session.Output = null; session.Error = null;
                session.Streaming = false; ActiveReaderProcesses--;
            }
        }
    }
    private static GitProgressReadStream ProgressOutput(Process process) => new(process.StandardOutput.BaseStream,
        () => { try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { } });
    public void Dispose() { lock (readGate) { try { ResetProof(); } finally { disposed = true; } } }
}
