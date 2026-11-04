using System.Buffers;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Splinter.Core;
using Splinter.Runtime;
using static Splinter.SourceControl.Git.GitObjectSnapshotProvider;

namespace Splinter.SourceControl.Git;

// cat-file streams raw object bytes, including packed objects. A requested range
// is staged until a full blob proof or its verified chunk proofs match. Only
// bounded blob proofs, two reader windows and a bounded verified-chunk RAM LRU
// are retained, never disk payload.
public sealed partial class GitObjectSnapshotTree : ISnapshotTree, ISnapshotContentSummaryProvider, IDisposable
{
    public SnapshotReference Snapshot { get; }
    // The gate protects leases/proof ownership, not decoding. Each of the two
    // readers can progress independently; aliases lease the same reader.
    public bool SupportsConcurrentReads => true;
    private readonly Func<SnapshotObservation> health;
    private readonly WorkspacePathPolicy policy;
    private readonly PagedSnapshotCatalog catalog;
    private readonly TemporaryMetadataDirectory catalogDirectory;
    private readonly object readGate = new();
    private volatile bool disposed, closing;
    private bool resetting;
    private int activeReads, pendingProofs, waitingReads;
    private const int ChunkBytes = 65536, MaxProofBytes = 4 * 1024 * 1024;
    private const int MaxProofEntries = 4096, MaxReaderSessions = 2;
    private const int MaxPayloadBytes = 256 * 1024 * 1024, MaxPayloadEntries = 4096;
    private const int MaxProofReadAheadBytes = 128 * 1024 * 1024;
    private sealed record CachedChunk(string ObjectId, long BlobLength, long Offset, byte[] Bytes)
    {
        internal bool UnusedReadAhead;
    }
    private readonly Dictionary<(string ObjectId, long Offset), LinkedListNode<CachedChunk>> payloads = new();
    private readonly Dictionary<string, HashSet<long>> payloadOffsets = new(StringComparer.Ordinal);
    private readonly LinkedList<CachedChunk> payloadLru = new();
    private readonly int payloadByteLimit;
    private readonly int proofReadAheadBytes;
    private int payloadBytes;
    internal int CachedChunkPayloadBytes { get { lock (readGate) return payloadBytes; } }
    internal int CachedChunkCount { get { lock (readGate) return payloads.Count; } }
    internal int PeakChunkPayloadBytes { get; private set; }
    internal long PayloadCacheHits { get; private set; }
    internal long PayloadCacheMisses { get; private set; }
    // The final 32 bytes retain the raw SHA256, within the same proof budget.
    private sealed record BlobProof(string ObjectId, long Length, GitChunkProof Chunks, bool ContainsCarriageReturn, bool ContainsNul);
    private readonly Dictionary<string, LinkedListNode<BlobProof>> proofs = new(StringComparer.Ordinal);
    private readonly LinkedList<BlobProof> proofLru = new();
    private int proofBytes;
    private readonly int proofByteLimit, proofEntryLimit;
    internal int CachedProofBytes { get { lock (readGate) return proofBytes; } }
    internal int CachedProofCount { get { lock (readGate) return proofs.Count; } }
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
        internal bool Busy;
        internal PayloadChunk NextChunk() => Chunks[Newest = (Newest + 1) % Chunks.Length];
    }
    private sealed class ReaderLease(ReaderSession session, GitChunkProof proof, bool createProof)
    {
        internal ReaderSession Session { get; } = session;
        internal GitChunkProof Proof { get; } = proof;
        internal bool CreateProof { get; } = createProof;
        internal bool Published;
    }
    private readonly Dictionary<string, LinkedListNode<ReaderSession>> sessions = new(StringComparer.Ordinal);
    private readonly LinkedList<ReaderSession> sessionLru = new();
    internal int ReaderProcessStarts { get; private set; }
    internal int ActiveReaderProcesses { get; private set; }
    internal int PeakReaderProcesses { get; private set; }
    internal int CachedPayloadBytes { get { lock (readGate) return sessions.Count * 2 * ChunkBytes + payloadBytes; } }
    internal int[] ReaderProcessIds { get { lock (readGate) return sessions.Values.Where(s => s.Value.Reader is not null).Select(s => s.Value.Reader!.Id).ToArray(); } }
    private long decodedBlobBytes;
    public long DecodedBlobBytes => Interlocked.Read(ref decodedBlobBytes);
    internal int ActiveReadCount { get { lock (readGate) return activeReads; } }
    internal int WaitingReadCount { get { lock (readGate) return waitingReads; } }
    internal bool IsClosing => closing;
    internal bool IsResetting { get { lock (readGate) return resetting; } }
    // Deterministic boundary observation for tests; production leaves this null.
    internal Action<string, string>? ReadObserver { get; set; }

    internal GitObjectSnapshotTree(SnapshotReference snapshot, string tree, string revision, string reference, Func<SnapshotObservation> health,
        int proofByteLimit = MaxProofBytes, int proofEntryLimit = MaxProofEntries,
        GitSourceMetadataManifest? sourceMetadata = null, int payloadByteLimit = MaxPayloadBytes,
        int proofReadAheadBytes = MaxProofReadAheadBytes, bool diagnostics = false, bool verifyCommit = false)
    {
        if (proofByteLimit is < 1 or > MaxProofBytes || proofEntryLimit is < 1 or > MaxProofEntries)
            throw new ArgumentOutOfRangeException(nameof(proofByteLimit));
        if (payloadByteLimit is < 0 or > MaxPayloadBytes) throw new ArgumentOutOfRangeException(nameof(payloadByteLimit));
        if (proofReadAheadBytes is < 0 or > MaxProofReadAheadBytes) throw new ArgumentOutOfRangeException(nameof(proofReadAheadBytes));
        this.proofByteLimit = proofByteLimit; this.proofEntryLimit = proofEntryLimit;
        this.payloadByteLimit = payloadByteLimit;
        this.proofReadAheadBytes = Math.Min(proofReadAheadBytes, payloadByteLimit / MaxReaderSessions);
        readDiagnostics = diagnostics || Environment.GetEnvironmentVariable("SPLINTER_GIT_PREPARATION_DIAGNOSTICS") == "1";
        Snapshot = snapshot; this.health = health;
        policy = WorkspacePathPolicy.For(snapshot);
        using var metadata = sourceMetadata?.OpenCatalog();
        EnsureAvailable();
        long timestamp = snapshot.CapturedAt.UtcDateTime.ToFileTimeUtc();
        using var verifier = new ObjectVerifier(snapshot.RootPath);
        verifier.Verify(verifyCommit ? [(revision, "commit"), (tree, "tree")] : [(tree, "tree")]);
        catalogDirectory = new();
        try
        {
            var descriptor = PagedSnapshotCatalog.Build(catalogDirectory.Path,
                new("", true, 0, (uint)FileAttributes.Directory, timestamp, timestamp, timestamp), Records(), policy.CaseSensitive);
            catalog = new(catalogDirectory.Path, descriptor);
            try
            {
                verifier.Verify(catalog.Walk().Where(e => e.Entry.Directory && e.SourceReference is not null).Select(e => (e.SourceReference!, "tree")));
                verifier.Complete();
            }
            catch { catalog.Dispose(); throw; }
        }
        catch
        {
            // A failed build may retain damaged scratch runs for diagnosis.
            // They never become a project/result root or a source fallback.
            try { catalogDirectory.Dispose(); } catch (IOException) { }
            throw;
        }
        IEnumerable<DirectoryMetadataRecord> Records()
        {
            foreach (string line in EnumerateListing(snapshot.RootPath, tree))
            {
                int tab = line.IndexOf('\t');
                if (tab < 0) throw Failure("GitBasisUnsupported", "Invalid project tree entry.");
                string[] fields = line[..tab].Split(' ', StringSplitOptions.RemoveEmptyEntries);
                string path = line[(tab + 1)..];
                if (fields.Length == 4 && fields[0] == "040000" && fields[1] == "tree" && fields[3] == "-")
                {
                    RequireObjectId(fields[2]); ValidateRecordedPath(path);
                    yield return new(new(path, true, 0, (uint)FileAttributes.Directory, timestamp, timestamp, timestamp), null, SourceReference: fields[2]);
                    continue;
                }
                if (fields.Length != 4 || fields[0] is not ("100644" or "100755") || fields[1] != "blob" ||
                    !long.TryParse(fields[3], NumberStyles.None, CultureInfo.InvariantCulture, out long length) || length < 0)
                    throw Failure("GitBasisUnsupported", "Only regular committed files are supported; no symlink/submodule fallback.");
                RequireObjectId(fields[2]); ValidateRecordedPath(path);
                long creation = timestamp, access = timestamp, write = timestamp;
                var original = metadata?.Find(path);
                if (original is not null && original.Entry.Path == path && original.SourceReference == fields[2] && original.Entry.Length == length)
                { creation = original.Entry.CreationTime; access = original.Entry.LastAccessTime; write = original.Entry.LastWriteTime; }
                yield return new(new(path, false, length, (uint)FileAttributes.Archive, creation, access, write,
                    fields[0] == "100755" ? 0x1EDu : 0x1A4u), null, SourceReference: fields[2]);
            }
        }
        // metadata listing uses the fixed tree, never the branch or mutable index.
        _ = revision; _ = reference;
    }
    internal static IEnumerable<string> EnumerateListing(string common, string tree)
    {
        using var process = Process.Start(StartInfo(common, ["ls-tree", "-r", "-t", "-l", "-z", tree])) ?? throw Failure("GitBasisUnavailable", "Git did not start.");
        var error = process.StandardError.ReadToEndAsync(); process.StandardInput.Close();
        using var output = ProgressOutput(process);
        using var reader = new StreamReader(output, new UTF8Encoding(false, true), false, 65536, leaveOpen: true);
        var record = new StringBuilder(); char[] buffer = new char[4096];
        try
        {
            for (int n; (n = reader.Read(buffer)) != 0;)
                for (int i = 0; i < n; i++)
                    if (buffer[i] == '\0') { if (record.Length == 0) throw Failure("GitBasisUnsupported", "An empty tree record is invalid."); yield return record.ToString(); record.Clear(); }
                    else
                    {
                        if (record.Length >= 32767 + 1024) throw Failure("GitBasisUnsupported", "A Git tree record exceeds the supported filesystem path and protocol header bounds.");
                        record.Append(buffer[i]);
                    }
            if (record.Length != 0) throw Failure("GitBasisObjectInvalid", "The fixed tree listing is incomplete.");
            if (!process.WaitForExit(1000)) { process.Kill(entireProcessTree: true); process.WaitForExit(); }
            _ = error.GetAwaiter().GetResult();
            if (process.ExitCode != 0) throw Failure("GitBasisObjectInvalid", "The fixed tree listing failed.");
        }
        finally { if (!process.HasExited) { process.Kill(entireProcessTree: true); process.WaitForExit(); } }
    }
    internal static void VerifyObject(string common, string objectId, string type) =>
        VerifyObjects(common, [(objectId, type)]);
    public void EnsureAvailable()
    {
        ObjectDisposedException.ThrowIf(disposed || closing, this);
        var observed = health();
        if (observed.Id != Snapshot.Id || observed.Available != true)
            throw Failure(observed.FailureCode ?? "SnapshotObservationUnknown", "Preserve this task's immutable object basis.");
    }
    // The batch header and bytes come from the same process/stream. This keeps
    // full object hashing while avoiding a separate size-query process for
    // every commit/tree and amortizes subtree verification process startup.
    internal static void VerifyObjects(string common, IEnumerable<(string Id, string Type)> objects)
    {
        using var verifier = new ObjectVerifier(common); verifier.Verify(objects); verifier.Complete();
    }
    // Retain one drained batch stream across root proof, listing/catalog build
    // and subtree proof. All raw hashes/framing and the final child exit still
    // pass before this tree becomes visible; listing never precedes root proof.
    private sealed class ObjectVerifier : IDisposable
    {
        private readonly Process process;
        private readonly Task<string> error;
        private readonly GitProgressReadStream output;
        private readonly byte[] scratch;
        private bool disposed;
        internal ObjectVerifier(string common)
        {
            process = Process.Start(StartInfo(common, ["cat-file", "--batch"])) ?? throw Failure("GitBasisUnavailable", "Git did not start.");
            try
            {
                error = process.StandardError.ReadToEndAsync(); output = ProgressOutput(process);
                scratch = ArrayPool<byte>.Shared.Rent(ChunkBytes);
            }
            catch
            {
                try { if (!process.HasExited) { process.Kill(entireProcessTree: true); process.WaitForExit(); } }
                finally { output?.Dispose(); process.Dispose(); }
                throw;
            }
        }
        internal void Verify(IEnumerable<(string Id, string Type)> objects)
        {
            try
            {
            foreach (var (objectId, type) in objects)
            {
                RequireObjectId(objectId);
                process.StandardInput.Write(objectId + "\n"); process.StandardInput.Flush();
                string[] header = Header(output).Split(' ');
                if (header.Length != 3 || header[0] != objectId || header[1] != type ||
                    !long.TryParse(header[2], NumberStyles.None, CultureInfo.InvariantCulture, out long length) || length < 0)
                    throw Failure("GitBasisObjectInvalid", "The fixed object header is invalid.");
                using var hash = IncrementalHash.CreateHash(objectId.Length == 40 ? HashAlgorithmName.SHA1 : HashAlgorithmName.SHA256);
                hash.AppendData(Encoding.ASCII.GetBytes(type + " " + length.ToString(CultureInfo.InvariantCulture) + "\0"));
                for (long position = 0; position < length;)
                {
                    int n = (int)Math.Min(ChunkBytes, length - position);
                    output.ReadExactly(scratch.AsSpan(0, n)); hash.AppendData(scratch.AsSpan(0, n)); position += n;
                }
                if (output.ReadByte() != '\n' || Convert.ToHexStringLower(hash.GetHashAndReset()) != objectId)
                    throw Failure("GitBasisObjectInvalid", "The fixed object hash failed; preserve the object store and receipt.");
            }
            }
            catch (IOException) { throw Failure("GitBasisObjectInvalid", "The fixed object stream is incomplete."); }
        }
        internal void Complete()
        {
            try
            {
                process.StandardInput.Close();
                if (!process.WaitForExit(1000)) { process.Kill(entireProcessTree: true); process.WaitForExit(); }
                _ = error.GetAwaiter().GetResult();
                if (process.ExitCode != 0) throw Failure("GitBasisObjectInvalid", "The fixed object reader failed.");
            }
            catch (IOException) { throw Failure("GitBasisObjectInvalid", "The fixed object stream is incomplete."); }
        }
        public void Dispose()
        {
            if (disposed) return; disposed = true;
            try { if (!process.HasExited) { process.Kill(entireProcessTree: true); process.WaitForExit(); } }
            finally
            {
                try { output.Dispose(); }
                finally { try { process.Dispose(); } finally { ArrayPool<byte>.Shared.Return(scratch); } }
            }
        }
    }
    public SnapshotEntry? Stat(string relativePath)
    { EnsureAvailable(); return catalog.Find(Normalize(relativePath))?.Entry; }
    public IReadOnlyList<SnapshotEntry> List(string relativeDirectory)
    {
        return EnumerateChildren(relativeDirectory).ToArray();
    }
    public IEnumerable<SnapshotEntry> EnumerateChildren(string relativeDirectory)
    { EnsureAvailable(); return catalog.EnumerateChildren(Normalize(relativeDirectory)).Select(e => e.Entry); }
    public SnapshotContentSummary? GetVerifiedContentSummary(string relativePath)
    {
        lock (readGate)
        {
            EnsureAvailable();
            if (resetting) throw Failure("GitBasisObjectInvalid", "The owned reader generation is being invalidated.");
            string path = Normalize(relativePath);
            var item = catalog.Find(path);
            if (item is null || item.Entry.Directory || item.SourceReference is not { } objectId || !proofs.TryGetValue(objectId, out var proof)) return null;
            if (proof.Value.Length != item.Entry.Length)
                throw Failure("GitBasisObjectInvalid", "The fixed blob proof length changed.");
            return new(path, proof.Value.Length, Convert.ToHexStringLower(proof.Value.Chunks.ReadRawHash()),
                proof.Value.ContainsCarriageReturn, proof.Value.ContainsNul);
        }
    }
    public int Read(string relativePath, long offset, Span<byte> destination)
    {
        EnsureAvailable(); string path = Normalize(relativePath);
        if (offset < 0 || destination.Length > 1024 * 1024) throw new ArgumentOutOfRangeException(nameof(offset));
        var item = catalog.Find(path);
        if (item is null || item.Entry.Directory || item.SourceReference is not { } objectId) throw new FileNotFoundException(path);
        long length = item.Entry.Length;
        int count = offset >= length ? 0 : (int)Math.Min(destination.Length, length - offset);
        if (count == 0) return 0;
        if (readDiagnostics)
        { Interlocked.Increment(ref readRequests); Interlocked.Add(ref requestedReadBytes, count); }
        ReaderLease? lease = null;
        byte[]? staged = null;
        long processingStarted = 0;
        bool succeeded = false;
        try
        {
            lease = AcquireReader(objectId, length);
            if (readDiagnostics) processingStarted = Stopwatch.GetTimestamp();
            staged = ArrayPool<byte>.Shared.Rent(count);
            ReadObserver?.Invoke(objectId, "leased");
            var session = lease.Session;
            bool hasCr = false, hasNul = false;
            if (lease.CreateProof)
            {
                var output = BeginBlob(session, length);
                using var hash = IncrementalHash.CreateHash(objectId.Length == 40 ? HashAlgorithmName.SHA1 : HashAlgorithmName.SHA256);
                using var rawHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                hash.AppendData(Encoding.ASCII.GetBytes("blob " + length.ToString(CultureInfo.InvariantCulture) + "\0"));
                long readAheadStart = offset / ChunkBytes * ChunkBytes;
                long readAheadEnd = readAheadStart + this.proofReadAheadBytes;
                Span<byte> chunkDigest = stackalloc byte[32];
                for (long position = 0; position < length;)
                {
                    var chunk = session.NextChunk();
                    int n = (int)Math.Min(ChunkBytes, length - position);
                    output.ReadExactly(chunk.Bytes.AsSpan(0, n)); Interlocked.Add(ref decodedBlobBytes, n);
                    if (readDiagnostics) Interlocked.Add(ref fullProofDecodedBytes, n);
                    hash.AppendData(chunk.Bytes.AsSpan(0, n));
                    rawHash.AppendData(chunk.Bytes.AsSpan(0, n));
                    if (!hasCr) hasCr = chunk.Bytes.AsSpan(0, n).Contains((byte)'\r');
                    if (!hasNul) hasNul = chunk.Bytes.AsSpan(0, n).Contains((byte)0);
                    SHA256.HashData(chunk.Bytes.AsSpan(0, n), chunkDigest); lease.Proof.Write(position / ChunkBytes * 32, chunkDigest);
                    long begin = Math.Max(position, offset), end = Math.Min(position + n, offset + count);
                    chunk.Offset = position; chunk.Length = n;
                    if (end > begin)
                    {
                        chunk.Bytes.AsSpan((int)(begin - position), (int)(end - begin)).CopyTo(staged.AsSpan((int)(begin - offset)));
                    }
                    if (end > begin || position >= readAheadStart && position < readAheadEnd)
                    {
                        // Keep a bounded forward window from this unavoidable
                        // full proof, avoiding a second decode for sequential
                        // index reads. Pending chunks share the global RAM/LRU
                        // budget and remain inaccessible until the proof and
                        // health checks publish under the exclusive blob lease.
                        RememberChunk(objectId, length, chunk, unusedReadAhead: end <= begin);
                    }
                    position += n;
                }
                if (output.ReadByte() != '\n' || Convert.ToHexStringLower(hash.GetHashAndReset()) != objectId)
                    throw Failure("GitBasisObjectInvalid", "The recorded blob hash failed; no requested bytes are published.");
                Span<byte> rawDigest = stackalloc byte[32]; rawHash.GetHashAndReset(rawDigest);
                lease.Proof.WriteRawHash(rawDigest); lease.Proof.Flush();
                session.Streaming = false; session.Position = length;
            }
            else
            {
                for (long at = offset; at < offset + count;)
                {
                    var chunk = LoadProvedChunk(session, length, lease.Proof, at);
                    int n = (int)Math.Min(offset + count - at, chunk.Offset + chunk.Length - at);
                    chunk.Bytes.AsSpan((int)(at - chunk.Offset), n).CopyTo(staged.AsSpan((int)(at - offset))); at += n;
                }
            }
            ReadObserver?.Invoke(objectId, "verified");
            lock (readGate)
            {
                if (resetting) throw Failure("GitBasisObjectInvalid", "Another owned reader failed; no requested bytes are published.");
                EnsureAvailable();
                if (lease.CreateProof)
                {
                    proofs.Add(objectId, proofLru.AddLast(new BlobProof(objectId, length, lease.Proof, hasCr, hasNul)));
                    lease.Published = true; FullBlobProofs++;
                }
                staged.AsSpan(0, count).CopyTo(destination);
                if (readDiagnostics)
                    for (long at = offset / ChunkBytes * ChunkBytes; at < offset + count; at += ChunkBytes)
                        if (payloads.TryGetValue((objectId, at), out var used)) used.Value.UnusedReadAhead = false;
            }
            succeeded = true;
            return count;
        }
        catch (Exception ex)
        {
            // Classify only after unwinding the nested bookkeeping locks.
            // Every failure must reach the lease-retirement finally below.
            if (ex is ObjectDisposedException) throw;
            if (ex is not (IOException or InvalidOperationException or WorkspaceException or System.ComponentModel.Win32Exception)) throw;
            lock (readGate) { resetting = true; Monitor.PulseAll(readGate); }
            throw ex is WorkspaceException ? ex : Failure("GitBasisObjectInvalid", "The owned blob reader failed; preserve the basis and journal.");
        }
        finally
        {
            if (readDiagnostics && !succeeded) Interlocked.Increment(ref failedReadRequests);
            if (readDiagnostics && lease is not null)
            {
                long ticks = Stopwatch.GetTimestamp() - processingStarted;
                if (lease.CreateProof) Interlocked.Add(ref fullProofProcessingTicks, ticks);
                else Interlocked.Add(ref provedReadProcessingTicks, ticks);
            }
            // Return staging before admitting a replacement lease, keeping
            // requested-range buffers bounded even if completion is preempted.
            try { if (staged is not null) ArrayPool<byte>.Shared.Return(staged); }
            finally { CompleteReader(lease); }
        }
    }
    private ReaderLease AcquireReader(string objectId, long length)
    {
        lock (readGate)
        {
            while (true)
            {
                EnsureAvailable();
                if (resetting || sessions.TryGetValue(objectId, out var existing) && existing.Value.Busy ||
                    !sessions.ContainsKey(objectId) && sessions.Count == MaxReaderSessions && sessions.Values.All(s => s.Value.Busy))
                { WaitForReader(); continue; }
                if (proofs.TryGetValue(objectId, out var cached))
                {
                    if (cached.Value.Length != length) throw Failure("GitBasisObjectInvalid", "The fixed blob proof length changed.");
                    proofLru.Remove(cached); proofLru.AddLast(cached);
                    var session = GetSession(objectId);
                    session.Busy = true; activeReads++;
                    return new(session, cached.Value.Chunks, false);
                }
                long proofLength = ((length - 1) / ChunkBytes + 1) * 32 + 32;
                int proofCharge = proofLength <= proofByteLimit ? (int)proofLength : Math.Min(256, proofByteLimit);
                // Active leases pin their proofs. Both reservations count before
                // allocation; waiting callers allocate neither proofs nor readers.
                if (proofBytes + proofCharge > proofByteLimit || proofs.Count + pendingProofs >= proofEntryLimit)
                {
                    var oldest = proofLru.First;
                    while (oldest is not null && sessions.TryGetValue(oldest.Value.ObjectId, out var owner) && owner.Value.Busy)
                        oldest = oldest.Next;
                    if (oldest is null) { WaitForReader(); continue; }
                    RemoveSession(oldest.Value.ObjectId);
                    RemovePayload(oldest.Value.ObjectId);
                    proofBytes -= oldest.Value.Chunks.Charge; oldest.Value.Chunks.Dispose();
                    proofs.Remove(oldest.Value.ObjectId); proofLru.Remove(oldest);
                    continue;
                }
                var selected = GetSession(objectId);
                var proof = new GitChunkProof(proofLength, proofByteLimit);
                proofBytes += proof.Charge; pendingProofs++;
                PeakProofBytes = Math.Max(PeakProofBytes, proofBytes);
                selected.Busy = true; activeReads++;
                return new(selected, proof, true);
            }
        }
    }
    private void WaitForReader()
    {
        waitingReads++;
        if (readDiagnostics) readerWaitCount++;
        long started = readDiagnostics ? Stopwatch.GetTimestamp() : 0;
        try { Monitor.Wait(readGate); }
        finally { waitingReads--; if (readDiagnostics) readerWaitTicks += Stopwatch.GetTimestamp() - started; }
    }
    private void CompleteReader(ReaderLease? lease)
    {
        lock (readGate)
        {
            if (lease is not null)
            {
                lease.Session.Busy = false; activeReads--;
                if (lease.CreateProof)
                {
                    pendingProofs--;
                    if (!lease.Published)
                    {
                        proofBytes -= lease.Proof.Charge;
                        try { lease.Proof.Dispose(); }
                        catch (IOException) { resetting = true; }
                        finally { RemovePayload(lease.Session.ObjectId); }
                    }
                }
            }
            Monitor.PulseAll(readGate);
            // A failed generation cannot publish any later read. Drain all
            // leases before closing either decoder or dropping shared proofs.
            if (resetting && activeReads == 0)
            {
                try { ResetProof(); }
                catch (Exception cleanup) when (cleanup is IOException or InvalidOperationException or System.ComponentModel.Win32Exception) { }
                finally { resetting = false; Monitor.PulseAll(readGate); }
            }
            else while (resetting) Monitor.Wait(readGate);
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
            while (oldest.Value.Busy) oldest = oldest.Next!;
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
            var reader = Process.Start(StartInfo(Snapshot.RootPath, ["cat-file", "--batch"])) ?? throw Failure("GitBasisUnavailable", "Git reader did not start.");
            lock (readGate)
            {
                session.Reader = reader; ReaderProcessStarts++; ActiveReaderProcesses++;
                PeakReaderProcesses = Math.Max(PeakReaderProcesses, ActiveReaderProcesses);
            }
            session.Error = session.Reader.StandardError.ReadToEndAsync();
            session.Output = ProgressOutput(session.Reader);
        }
        session.Reader.StandardInput.Write(session.ObjectId + "\n"); session.Reader.StandardInput.Flush();
        var output = session.Output!;
        ReadBlobHeader(output, session.ObjectId, length);
        foreach (var chunk in session.Chunks) { chunk.Offset = -1; chunk.Length = 0; }
        session.Position = 0; session.Streaming = true;
        ReadObserver?.Invoke(session.ObjectId, "header");
        return output;
    }
    private PayloadChunk LoadProvedChunk(ReaderSession session, long length, GitChunkProof proof, long at)
    {
        foreach (var retained in session.Chunks)
            if (at >= retained.Offset && at < retained.Offset + retained.Length) return retained;
        if (TryLoadCachedChunk(session, length, proof, at, out var cached)) return cached!;
        if (!session.Streaming || at < session.Position) BeginBlob(session, length);
        var output = session.Output!;
        Span<byte> digest = stackalloc byte[32];
        PayloadChunk chunk;
        do
        {
            chunk = session.NextChunk();
            int n = (int)Math.Min(ChunkBytes, length - session.Position);
            output.ReadExactly(chunk.Bytes.AsSpan(0, n)); Interlocked.Add(ref decodedBlobBytes, n);
            if (readDiagnostics) Interlocked.Add(ref provedReadDecodedBytes, n);
            SHA256.HashData(chunk.Bytes.AsSpan(0, n), digest);
            if (!proof.Matches(session.Position / ChunkBytes * 32, digest)) throw Failure("GitBasisObjectInvalid", "The requested chunk does not match the fully verified fixed blob; no bytes are published.");
            chunk.Offset = session.Position; chunk.Length = n; session.Position += n;
        } while (at >= session.Position);
        if (session.Position == length)
        {
            if (output.ReadByte() != '\n') throw Failure("GitBasisObjectInvalid", "The fixed blob framing changed.");
            session.Streaming = false;
        }
        RememberChunk(session.ObjectId, length, chunk);
        return chunk;
    }
    private bool TryLoadCachedChunk(ReaderSession session, long length, GitChunkProof proof, long at, out PayloadChunk? result)
    {
        if (payloadByteLimit == 0) { result = null; return false; }
        lock (readGate)
        {
            long offset = at / ChunkBytes * ChunkBytes;
            if (!payloads.TryGetValue((session.ObjectId, offset), out var node))
            { PayloadCacheMisses++; result = null; return false; }
            var cached = node.Value;
            Span<byte> digest = stackalloc byte[32];
            SHA256.HashData(cached.Bytes, digest);
            if (cached.BlobLength != length || cached.Bytes.Length != Math.Min(ChunkBytes, length - offset) ||
                !proof.Matches(offset / ChunkBytes * 32, digest))
                throw Failure("GitBasisObjectInvalid", "The retained chunk does not match the fixed blob proof; no bytes are published.");
            payloadLru.Remove(node); payloadLru.AddLast(node);
            // Copy into the leased window before unlocking. Concurrent LRU
            // eviction cannot leave unaccounted cache arrays held by readers.
            result = session.NextChunk(); cached.Bytes.CopyTo(result.Bytes, 0);
            result.Offset = offset; result.Length = cached.Bytes.Length;
            PayloadCacheHits++;
            return true;
        }
    }
    private void RememberChunk(string objectId, long length, PayloadChunk chunk, bool unusedReadAhead = false)
    {
        if (chunk.Length > payloadByteLimit) return;
        lock (readGate)
        {
            var key = (objectId, chunk.Offset);
            if (payloads.TryGetValue(key, out var existing))
            { payloadLru.Remove(existing); payloadLru.AddLast(existing); return; }
            // Evict before allocation; in-flight first-proof chunks share this
            // same bound. At most 4,096 entries also bounds tiny-file metadata.
            while (payloadBytes + chunk.Length > payloadByteLimit || payloads.Count >= MaxPayloadEntries)
                RemovePayloadNode(payloadLru.First!, capacityEviction: true);
            byte[] bytes = chunk.Bytes.AsSpan(0, chunk.Length).ToArray();
            payloads.Add(key, payloadLru.AddLast(new CachedChunk(objectId, length, chunk.Offset, bytes) { UnusedReadAhead = unusedReadAhead }));
            if (!payloadOffsets.TryGetValue(objectId, out var offsets)) payloadOffsets.Add(objectId, offsets = []);
            offsets.Add(chunk.Offset);
            payloadBytes += bytes.Length;
            PeakChunkPayloadBytes = Math.Max(PeakChunkPayloadBytes, payloadBytes);
        }
    }
    private void RemovePayload(string objectId)
    {
        if (!payloadOffsets.Remove(objectId, out var offsets)) return;
        foreach (long offset in offsets) RemovePayloadNode(payloads[(objectId, offset)]);
    }
    private void RemovePayloadNode(LinkedListNode<CachedChunk> node, bool capacityEviction = false)
    {
        if (readDiagnostics && capacityEviction)
        {
            capacityEvictedBytes += node.Value.Bytes.Length;
            if (node.Value.UnusedReadAhead) unusedReadAheadEvictedBytes += node.Value.Bytes.Length;
        }
        payloads.Remove((node.Value.ObjectId, node.Value.Offset));
        if (payloadOffsets.TryGetValue(node.Value.ObjectId, out var offsets))
        {
            offsets.Remove(node.Value.Offset);
            if (offsets.Count == 0) payloadOffsets.Remove(node.Value.ObjectId);
        }
        payloadLru.Remove(node); payloadBytes -= node.Value.Bytes.Length;
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
        finally
        {
            foreach (var proof in proofs.Values)
                try { proof.Value.Chunks.Dispose(); }
                catch (Exception ex) when (ex is IOException or InvalidOperationException) { failure ??= ex; }
            sessions.Clear(); sessionLru.Clear(); proofs.Clear(); proofLru.Clear(); proofBytes = 0;
            payloads.Clear(); payloadOffsets.Clear(); payloadLru.Clear(); payloadBytes = 0;
        }
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
    internal static void ReadBlobHeader(Stream stream, string objectId, long length)
    {
        // The exact fixed blob header is known. Read it in bounded spans rather
        // than entering the no-progress timer and pipe once for every byte.
        // An early newline (including "missing") fails immediately: requesting
        // the rest with ReadExactly would wait on an idle batch reader.
        string expected = objectId + " blob " + length.ToString(CultureInfo.InvariantCulture) + "\n";
        Span<byte> header = stackalloc byte[256];
        if (expected.Length > header.Length) throw Failure("GitBasisObjectInvalid", "Invalid bounded cat-file header.");
        int read = 0;
        while (read < expected.Length)
        {
            int n = stream.Read(header.Slice(read, expected.Length - read));
            if (n == 0) throw Failure("GitBasisObjectInvalid", "The fixed blob header is incomplete.");
            int newline = header.Slice(read, n).IndexOf((byte)'\n');
            if (newline >= 0 && read + newline != expected.Length - 1)
                throw Failure("GitBasisObjectInvalid", "The fixed blob is missing or its header changed.");
            read += n;
        }
        Span<byte> expectedBytes = stackalloc byte[256];
        int expectedCount = Encoding.ASCII.GetBytes(expected, expectedBytes);
        if (!header[..read].SequenceEqual(expectedBytes[..expectedCount]))
            throw Failure("GitBasisObjectInvalid", "The fixed blob is missing or its header changed.");
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
                lock (readGate)
                {
                    reader.Dispose(); session.Reader = null; session.Output = null; session.Error = null;
                    session.Streaming = false; ActiveReaderProcesses--;
                }
            }
        }
    }
    private static GitProgressReadStream ProgressOutput(Process process) => new(process.StandardOutput.BaseStream,
        () => { try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { } });
    public void Dispose()
    {
        lock (readGate)
        {
            if (closing) { while (!disposed) Monitor.Wait(readGate); return; }
            if (disposed) return;
            closing = true; Monitor.PulseAll(readGate);
            while (activeReads != 0) Monitor.Wait(readGate);
            try { ResetProof(); }
            finally
            {
                try { catalog.Dispose(); catalogDirectory.Dispose(); }
                finally { disposed = true; Monitor.PulseAll(readGate); }
            }
        }
    }
}
