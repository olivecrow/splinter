using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Splinter.Core;
using Splinter.Runtime;

namespace Splinter.BeeProbe;

// Interruption is limited to an exact child created by this harness. This tests
// process recovery of physical NTFS files, not OS crashes or a killed Editor.
internal static class InternalNativeRecovery
{
    private static readonly byte[] Source = Encoding.UTF8.GetBytes("immutable-recovery-source");
    private static readonly byte[] Acknowledged = Encoding.UTF8.GetBytes("acknowledged-durable-source");
    private static readonly byte[] Payload = Enumerable.Range(0, 131089).Select(i => (byte)(i % 251)).ToArray();
    private static readonly byte[] Flushed = Enumerable.Range(0, 131089).Select(i => (byte)((i * 17 + 31) % 251)).ToArray();
    private const string NativePath = "Library/Bee/item.tmp";
    private const string RenamedPath = "Library/Bee/item.state";
    private const string SourcePath = "Assets/promoted.bin";
    private sealed record Request(string Root, string Original, string Nonce, OverlayIdentity Identity,
        string Operation, string Boundary, Guid? FileId);
    private sealed record Cut(string Operation, string Boundary);
    private sealed record GuardEnvelope<T>(int Version, T Payload, string Sha256);
    private sealed record GuardAssociation(Guid Id, string OwnerHash, uint VolumeSerial, ulong FileIndex);
    private sealed record GuardIntent(string Operation, string OwnerHash, Guid? Id, string? Path,
        uint Attributes = 0, string? Destination = null, bool Replace = false,
        long Length = 0, string? Sha256 = null, GuardAssociation? Association = null);
    private static readonly JsonSerializerOptions GuardJson = new(JsonSerializerDefaults.Web);

    // Keep the names synchronized with InternalNativeDeltaStore's optional
    // observer. Normal stores and throughput samples do not install an observer.
    private static readonly Cut[] Cuts =
    [
        new("create", "create.namespace-durable"),
        new("create", "create.association-durable"),
        new("flush", "native.after-payload-flush"),
        new("rename", "rename.namespace-published"),
        new("promote", "promotion.intent-durable"),
        new("promote", "promotion.journal-imported"),
        new("promote", "promotion.marker-durable"),
        new("promote", "promotion.namespace-published"),
        new("reclaim", "reclaim.before-payload-delete"),
        new("reclaim", "reclaim.after-payload-delete")
    ];

    internal static object Run(string root) => RunAsync(root).GetAwaiter().GetResult();
    private static async Task<object> RunAsync(string root)
    {
        root = Path.GetFullPath(root);
        if (Path.Exists(root)) throw new IOException("Recovery verification requires a new owned root.");
        Directory.CreateDirectory(root);
        string original = Path.Combine(root, "original.txt");
        using (var source = new FileStream(original, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
        { source.Write(Source); source.Flush(true); }
        var rows = new List<object>();
        foreach (var cut in Cuts)
        {
            string directory = Path.Combine(root, cut.Boundary.Replace('.', '-'));
            var request = Seed(directory, original, cut.Operation, cut.Boundary);
            string config = directory + ".request.json"; Program.Write(config, request);
            var stopped = await KillAtBoundary(config, cut.Boundary);
            object recovered;
            using (var basis = new RecoveryBasis(original, request.Nonce))
            using (var store = Open(request, basis))
            {
                VerifyDurableSource(store);
                var before = store.NativeInventory();
                Guid? recoveredId = Resume(request, store);
                store.Flush(); store.VerifyNativeInventory(); VerifyDurableSource(store);
                string? exportedHash = null;
                if (cut.Operation == "promote")
                {
                    var entry = store.Journal.Stat(SourcePath) ?? throw new IOException("Promoted source is not durable in the source journal.");
                    byte[] bytes = Read(store.Journal, entry.Id); RequireBytes(Payload, bytes, "Promoted payload");
                    string export = Path.Combine(directory, "independent-promoted.bin");
                    using (var output = new FileStream(export, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
                    { output.Write(bytes); output.Flush(true); }
                    exportedHash = Hash(File.ReadAllBytes(export));
                }
                recovered = new { priorFileId = request.FileId, recoveredId, inventoryBeforeRetry = before,
                    inventoryAfterRetry = store.NativeInventory(), durableSourcePreserved = true, independentExportSha256 = exportedHash };
            }
            // Reopening a second time establishes that the first recovery did
            // not merely repair a transient in-memory index.
            using (var basis = new RecoveryBasis(original, request.Nonce))
            using (var store = Open(request, basis))
            {
                VerifyDurableSource(store); store.VerifyNativeInventory();
                if (cut.Operation == "promote")
                {
                    var entry = store.Journal.Stat(SourcePath) ?? throw new IOException("Second reopen lost promoted source.");
                    RequireBytes(Payload, Read(store.Journal, entry.Id), "Second promoted reopen");
                    RequireBytes(Payload, File.ReadAllBytes(Path.Combine(directory, "independent-promoted.bin")), "Independent output after store disposal");
                }
            }
            RequireBytes(Source, File.ReadAllBytes(original), "Original after child interruption");
            rows.Add(new { cut.Operation, cut.Boundary, stopped, recovered, sameOwnerSecondReopen = true, passed = true });
        }
        object guards = OwnershipGuards(Path.Combine(root, "ownership"), original);
        var result = new { passed = true, interruptionCases = rows.Count, rows, guards, originalSha256 = Hash(Source),
            scope = "Exact owned child termination; physical NTFS journal/backing; fixed small pinned source; no mount/Editor kill, OS crash, power loss or foreign mapping absence proof." };
        Program.Write(Path.Combine(root, "recovery-result.json"), result); return result;
    }

    internal static void RunChild(string config)
    {
        var request = JsonSerializer.Deserialize<Request>(File.ReadAllText(config), Program.Json) ?? throw new IOException("Missing recovery request.");
        using var basis = new RecoveryBasis(request.Original, request.Nonce);
        bool stopped = false;
        using var store = Open(request, basis, boundary =>
        {
            if (stopped || boundary != request.Boundary) return;
            stopped = true;
            Program.Write(Path.Combine(request.Root, "reached.json"), new { boundary, processId = Environment.ProcessId,
                processStartTimeUtcTicks = Process.GetCurrentProcess().StartTime.ToUniversalTime().Ticks });
            Console.WriteLine(JsonSerializer.Serialize(new { boundary })); Console.Out.Flush();
            if (Console.ReadLine() != "continue") throw new IOException("Owned child boundary was not resumed.");
        });
        switch (request.Operation)
        {
            case "create": store.Create(NativePath, false, 32); break;
            case "flush": WriteChunks(store, request.FileId!.Value, Flushed); store.Flush(); break;
            case "rename": store.Rename(request.FileId!.Value, RenamedPath, false); break;
            case "promote": store.Rename(request.FileId!.Value, SourcePath, false); break;
            case "reclaim": store.Delete(request.FileId!.Value); store.ReleaseUnlinkedFile(request.FileId.Value); break;
            default: throw new IOException("Unsupported recovery operation.");
        }
        if (!stopped) throw new IOException("Requested interruption boundary was not reached: " + request.Boundary);
    }

    private static Request Seed(string root, string original, string operation, string boundary)
    {
        if (Path.Exists(root)) throw new IOException("Refusing an existing recovery case.");
        Directory.CreateDirectory(root); string nonce = Guid.NewGuid().ToString("N");
        using var basis = new RecoveryBasis(original, nonce);
        var identity = new OverlayIdentity(2, "bee-internal-recovery-" + nonce, basis.Snapshot, 64L * 1024 * 1024, 65536);
        var request = new Request(root, original, nonce, identity, operation, boundary, null);
        using (var store = Open(request, basis))
        {
            var source = store.Create("Assets/acknowledged.txt", false, 32); store.Write(source.Id, 0, Acknowledged);
            if (operation != "create")
            {
                var generated = store.Create(NativePath, false, 32); WriteChunks(store, generated.Id, Payload);
                request = request with { FileId = generated.Id };
            }
            store.Flush(); store.VerifyNativeInventory();
        }
        return request;
    }

    private static InternalNativeDeltaStore Open(Request request, RecoveryBasis basis, Action<string>? observer = null)
    {
        var journal = new JournalDeltaStore(Path.Combine(request.Root, "delta"), request.Identity, basis,
            optimizeJournal: false, boundedLogBytes: 16 * 1024 * 1024);
        try { return new InternalNativeDeltaStore(journal, Path.Combine(request.Root, "native"), observer); }
        catch { journal.Dispose(); throw; }
    }

    private static Guid? Resume(Request request, InternalNativeDeltaStore store)
    {
        switch (request.Operation)
        {
            case "create":
            {
                var entry = store.Stat(NativePath) ?? store.Create(NativePath, false, 32);
                if (entry.Length != 0) throw new IOException("Interrupted empty create gained unexpected contents.");
                WriteChunks(store, entry.Id, Payload); RequireBytes(Payload, Read(store, entry.Id), "Resumed create");
                return entry.Id;
            }
            case "flush":
                RequireId(store.Stat(NativePath), request.FileId); RequireBytes(Flushed, Read(store, request.FileId!.Value), "Durably flushed native bytes");
                return request.FileId;
            case "rename":
            {
                var entry = store.Stat(RenamedPath) ?? store.Stat(NativePath); RequireId(entry, request.FileId);
                if (entry!.Path != RenamedPath) store.Rename(entry.Id, RenamedPath, false);
                if (store.Stat(NativePath) is not null) throw new IOException("Recovered rename exposes two names.");
                RequireBytes(Payload, Read(store, entry.Id), "Recovered namespace bytes"); return entry.Id;
            }
            case "promote":
            {
                var entry = store.Stat(SourcePath) ?? store.Stat(NativePath); RequireId(entry, request.FileId);
                if (entry!.Path != SourcePath) store.Rename(entry.Id, SourcePath, false);
                if (store.Stat(NativePath) is not null) throw new IOException("Recovered promotion exposes two names.");
                RequireBytes(Payload, Read(store, entry.Id), "Recovered source bytes");
                RequireBytes(Payload, Read(store.Journal, entry.Id), "Independent journal source bytes"); return entry.Id;
            }
            case "reclaim":
                if (store.Stat(NativePath) is not null) throw new IOException("Interrupted reclamation revived the deleted name.");
                store.ReleaseUnlinkedFile(request.FileId!.Value);
                if (store.NativeInventory().Any(row => row.Id == request.FileId)) throw new IOException("Same-owner reclaim retained native payload.");
                return request.FileId;
            default: throw new IOException("Unknown retry.");
        }
    }

    private static object OwnershipGuards(string root, string original)
    {
        Directory.CreateDirectory(root);
        var unknown = Seed(Path.Combine(root, "unknown"), original, "rename", "none");
        string foreign = Path.Combine(unknown.Root, "native", "foreign-preserve.bin"); File.WriteAllText(foreign, "unknown-owner");
        ExpectRejected(unknown);
        if (File.ReadAllText(foreign) != "unknown-owner") throw new IOException("Unknown native file was modified.");

        var owner = Seed(Path.Combine(root, "owner"), original, "rename", "none");
        var another = Seed(Path.Combine(root, "other-journal"), original, "rename", "none");
        bool refused = false;
        using (var basis = new RecoveryBasis(original, another.Nonce))
        using (var journal = new JournalDeltaStore(Path.Combine(another.Root, "delta"), another.Identity, basis,
            optimizeJournal: false, boundedLogBytes: 16 * 1024 * 1024))
        {
            try { using var wrong = new InternalNativeDeltaStore(journal, Path.Combine(owner.Root, "native")); }
            catch (Exception error) when (IsExpectedRefusal(error)) { refused = true; }
        }
        if (!refused) throw new IOException("A different journal owner adopted the native backing.");
        using (var basis = new RecoveryBasis(original, owner.Nonce))
        using (var reopened = Open(owner, basis)) { reopened.VerifyNativeInventory(); VerifyDurableSource(reopened); }

        var pendingOwner = Seed(Path.Combine(root, "pending-owner"), original, "rename", "none");
        string pendingOwnerFile = Path.Combine(pendingOwner.Root, "native", "owner.json");
        string pendingOwnerAttempt = pendingOwnerFile + ".pending";
        byte[] validCurrentOwner = File.ReadAllBytes(pendingOwnerFile);
        byte[] otherOwner = File.ReadAllBytes(Path.Combine(another.Root, "native", "owner.json"));
        File.WriteAllBytes(pendingOwnerAttempt, otherOwner);
        ExpectRejected(pendingOwner);
        RequireBytes(validCurrentOwner, File.ReadAllBytes(pendingOwnerFile), "Current owner after mismatched pending control");
        RequireBytes(otherOwner, File.ReadAllBytes(pendingOwnerAttempt), "Mismatched pending owner preservation");

        var pendingAssociation = Seed(Path.Combine(root, "pending-association"), original, "create", "none");
        using (var basis = new RecoveryBasis(original, pendingAssociation.Nonce))
        using (var store = Open(pendingAssociation, basis))
        {
            var empty = store.Create(NativePath, false, 32); store.Flush();
            pendingAssociation = pendingAssociation with { FileId = empty.Id };
        }
        string associationFile = Path.Combine(pendingAssociation.Root, "native", pendingAssociation.FileId!.Value.ToString("N") + ".record.json");
        byte[] originalAssociation = File.ReadAllBytes(associationFile);
        var association = JsonSerializer.Deserialize<GuardEnvelope<GuardAssociation>>(originalAssociation, GuardJson)!.Payload;
        WriteEnvelope(Path.Combine(pendingAssociation.Root, "native", "operation.json"),
            new GuardIntent("create", association.OwnerHash, association.Id, NativePath, 32));
        WriteEnvelope(associationFile + ".pending", association with { FileIndex = association.FileIndex + 1 });
        byte[] invalidPreparedAssociation = File.ReadAllBytes(associationFile + ".pending");
        ExpectRejected(pendingAssociation);
        RequireBytes(originalAssociation, File.ReadAllBytes(associationFile), "Current association after wrong native identity");
        RequireBytes(invalidPreparedAssociation, File.ReadAllBytes(associationFile + ".pending"), "Wrong native identity prepared association preservation");

        var pendingIntent = Seed(Path.Combine(root, "pending-unrelated-operation"), original, "rename", "none");
        string intentAssociationFile = Path.Combine(pendingIntent.Root, "native", pendingIntent.FileId!.Value.ToString("N") + ".record.json");
        var intentOwner = JsonSerializer.Deserialize<GuardEnvelope<GuardAssociation>>(File.ReadAllBytes(intentAssociationFile), GuardJson)!.Payload.OwnerHash;
        string operationFile = Path.Combine(pendingIntent.Root, "native", "operation.json");
        WriteEnvelope(operationFile, new GuardIntent("create", intentOwner, null, "Library/Bee/current-create.tmp", 32));
        WriteEnvelope(operationFile + ".pending", new GuardIntent("create", intentOwner, null, "Library/Bee/unrelated-create.tmp", 32));
        byte[] currentIntent = File.ReadAllBytes(operationFile), unrelatedPendingIntent = File.ReadAllBytes(operationFile + ".pending");
        ExpectRejected(pendingIntent);
        RequireBytes(currentIntent, File.ReadAllBytes(operationFile), "Current operation after unrelated same-owner pending intent");
        RequireBytes(unrelatedPendingIntent, File.ReadAllBytes(operationFile + ".pending"), "Unrelated same-owner pending operation preservation");

        var hardlink = Seed(Path.Combine(root, "hardlink"), original, "rename", "none");
        string backing;
        using (var basis = new RecoveryBasis(original, hardlink.Nonce))
        using (var store = Open(hardlink, basis)) { backing = store.NativeInventory().Single(row => row.Id == hardlink.FileId).BackingPath; }
        string linked = Path.Combine(hardlink.Root, "owned-extra-link.bin");
        if (!CreateHardLinkW(linked, backing, IntPtr.Zero)) throw new Win32Exception(Marshal.GetLastWin32Error(), "Cannot create owned hardlink guard fixture.");
        ExpectRejected(hardlink);
        if (Hash(File.ReadAllBytes(linked)) != Hash(Payload) || Hash(File.ReadAllBytes(backing)) != Hash(Payload))
            throw new IOException("Rejected hardlinked payload changed.");

        var pinned = Seed(Path.Combine(root, "pinned"), original, "rename", "none");
        bool renameRefused = false;
        using (var basis = new RecoveryBasis(original, pinned.Nonce))
        using (var store = Open(pinned, basis))
        {
            try { Directory.Move(Path.Combine(pinned.Root, "native"), Path.Combine(pinned.Root, "native-moved")); }
            catch (IOException) { renameRefused = true; }
            catch (UnauthorizedAccessException) { renameRefused = true; }
            if (!renameRefused) throw new IOException("Native root was replaceable while its owner was active.");
            store.VerifyNativeInventory();
        }
        RequireBytes(Source, File.ReadAllBytes(original), "Original after ownership guard tests");
        return new { unknownFilePreservedAndRejected = true, differentJournalOwnerRejected = true,
            mismatchedPendingOwnerPreservesBothGenerations = true,
            wrongNativeIdentityPendingAssociationPreservesBothGenerations = true,
            unrelatedSameOwnerPendingOperationPreservesBothGenerations = true,
            hardlinkPreservedAndRejected = true, activeNativeRootPinned = true,
            reparseInjection = "NotRun; does not claim an exhaustive filesystem attack matrix." };
    }

    private static void ExpectRejected(Request request)
    {
        bool rejected = false;
        using var basis = new RecoveryBasis(request.Original, request.Nonce);
        try { using var store = Open(request, basis); store.VerifyNativeInventory(); }
        catch (Exception error) when (IsExpectedRefusal(error)) { rejected = true; }
        if (!rejected) throw new IOException("Unsafe native backing was accepted.");
    }
    private static bool IsExpectedRefusal(Exception error) => error is IOException or UnauthorizedAccessException or WorkspaceException;
    private static void WriteEnvelope<T>(string path, T payload)
    {
        string hash = Hash(JsonSerializer.SerializeToUtf8Bytes(payload, GuardJson));
        using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        JsonSerializer.Serialize(output, new GuardEnvelope<T>(1, payload, hash), GuardJson); output.Flush(true);
    }
    private static async Task<object> KillAtBoundary(string config, string boundary)
    {
        var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
        start.ArgumentList.Add("internal-recovery-child"); start.ArgumentList.Add(config);
        using var process = Process.Start(start) ?? throw new IOException("Could not launch the owned recovery child.");
        int pid = process.Id; long startTicks = process.StartTime.ToUniversalTime().Ticks;
        Task<string> errors = process.StandardError.ReadToEndAsync();
        try
        {
            string? line = await process.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(30));
            if (line is null || JsonDocument.Parse(line).RootElement.GetProperty("boundary").GetString() != boundary)
                throw new IOException("Owned child did not reach " + boundary + ": " + await errors.WaitAsync(TimeSpan.FromSeconds(2)));
            process.Kill(entireProcessTree: false); await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
            return new { processId = pid, processStartTimeUtcTicks = startTicks, exitCode = process.ExitCode,
                exactCreatedChildTerminated = true, stderr = await errors };
        }
        finally
        {
            if (!process.HasExited)
            { process.Kill(entireProcessTree: false); await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10)); }
        }
    }

    private static void VerifyDurableSource(InternalNativeDeltaStore store)
    {
        var entry = store.Stat("Assets/acknowledged.txt") ?? throw new IOException("Durably acknowledged source disappeared.");
        RequireBytes(Acknowledged, Read(store, entry.Id), "Acknowledged source");
        var original = store.Stat("Assets/original.txt") ?? throw new IOException("Immutable original disappeared.");
        RequireBytes(Source, Read(store, original.Id), "Immutable source read");
    }
    private static void RequireId(DeltaEntry? entry, Guid? id)
    { if (entry is null || entry.Id != id) throw new IOException("Recovery changed or lost the logical file identity."); }
    private static byte[] Read(IWorkspaceDeltaStore store, Guid id)
    {
        var entry = store.Get(id); if (entry.Length > 1024 * 1024) throw new IOException("Recovery fixture bounds exceeded.");
        byte[] bytes = new byte[checked((int)entry.Length)]; int offset = 0;
        while (offset < bytes.Length)
        { int read = store.Read(id, offset, bytes.AsSpan(offset)); if (read == 0) throw new EndOfStreamException(); offset += read; }
        return bytes;
    }
    private static void WriteChunks(IWorkspaceDeltaStore store, Guid id, byte[] bytes)
    {
        for (int offset = 0; offset < bytes.Length; offset += store.Identity.MaxTransferBytes)
            store.Write(id, offset, bytes.AsSpan(offset, Math.Min(store.Identity.MaxTransferBytes, bytes.Length - offset)));
    }
    private static void RequireBytes(byte[] expected, byte[] actual, string name)
    { if (!expected.AsSpan().SequenceEqual(actual)) throw new IOException(name + " differs."); }
    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    private sealed class RecoveryBasis : ISnapshotTree, IDisposable
    {
        private readonly FileStream original;
        public SnapshotReference Snapshot { get; }
        public bool SupportsConcurrentReads => true;
        internal RecoveryBasis(string path, string nonce)
        {
            original = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (original.Length != Source.Length || Convert.ToHexStringLower(SHA256.HashData(original)) != Hash(Source))
            { original.Dispose(); throw new IOException("Original recovery fixture differs."); }
            Snapshot = new("bee-recovery-" + nonce, "development-pinned-ntfs", Path.GetFullPath(path), Path.GetFullPath(path),
                new DateTimeOffset(2026, 10, 15, 0, 0, 0, TimeSpan.Zero), true, true, "small-pinned-source");
        }
        public void EnsureAvailable() { if (original.Length != Source.Length) throw new IOException("Recovery basis changed."); }
        public SnapshotEntry? Stat(string path)
        {
            EnsureAvailable(); path = JournalDeltaStore.Normalize(path); long stamp = Snapshot.CapturedAt.UtcDateTime.ToFileTimeUtc();
            if (path is "" or "Assets" or "Library" or "Library/Bee") return new(path, true, 0, 16, stamp, stamp, stamp);
            return path == "Assets/original.txt" ? new(path, false, Source.Length, 32, stamp, stamp, stamp) : null;
        }
        public IReadOnlyList<SnapshotEntry> List(string directory) => JournalDeltaStore.Normalize(directory) switch
        { "" => [Stat("Assets")!, Stat("Library")!], "Assets" => [Stat("Assets/original.txt")!], "Library" => [Stat("Library/Bee")!], _ => [] };
        public int Read(string path, long offset, Span<byte> destination)
        {
            EnsureAvailable(); if (JournalDeltaStore.Normalize(path) != "Assets/original.txt") throw new FileNotFoundException(path);
            return RandomAccess.Read(original.SafeFileHandle, destination, offset);
        }
        public void Dispose() => original.Dispose();
    }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CreateHardLinkW(string name, string existing, IntPtr security);
}
