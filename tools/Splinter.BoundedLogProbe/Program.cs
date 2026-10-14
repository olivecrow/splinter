using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Splinter.BoundedLogProbe;

internal static class Program
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private sealed record WorkerConfig(string Method, string Store, string Basis, ProbeIdentity Identity, string Acks, string? Boundary, int Occurrence, int Writes);
    internal static async Task<int> Main(string[] args)
    {
        try
        {
            if (args.Length == 0 || args[0] == "capabilities")
            {
                Print(new { developmentOnly = true, productBackend = false, format = "probe-bounded-wal-v1",
                    commands = new[] { "capabilities", "selftest --root NEW_DIRECTORY", "measure --root NEW_DIRECTORY --method sjl4|wal [--writes 4096 --objects 20000 --hot 256 --seed-kib 192 --cap-mib 4]" },
                    profiles = new[] { "serial-per-ack-durable; incompressible deterministic partial ranges", "process-termination and exact-owner recovery" },
                    bounds = new { objectsMaximum = 20000, hotObjectsMaximum = 256, basisMaximumMiB = 128, payloadMaximumBytes = 1048576, walCapMiB = "4 by default; 1..64 accepted for measurement", ram = "current private ranges plus bounded frame/checkpoint scratch; not total-process capped" },
                    contractChange = "Obsolete WAL bytes are bounded, not zero after ACK. Checkpoint temporarily duplicates current private ranges. One flush per write, plus separately counted checkpoint flushes. No group commit.",
                    notRun = new[] { "Unity", "WinFsp", "production lifecycle", "OS power loss", "kernel crash", "hostile concurrent filesystem mutation", "rename/delete/resize API", "concurrent writers", "group commit" } });
                return 0;
            }
            if (args[0] == "worker") { Worker(args[1]); return 0; }
            if (args[0] is not ("selftest" or "measure")) throw new ArgumentException("Unknown development probe command.");
            var options = Options(args[1..]);
            string[] allowed = args[0] == "selftest" ? ["root"] : ["root", "method", "writes", "objects", "hot", "seed-kib", "cap-mib"];
            if (options.Keys.Except(allowed, StringComparer.Ordinal).Any()) throw new ArgumentException("Unknown probe option.");
            string root = NewRoot(Required(options, "root"));
            if (args[0] == "selftest") { await SelfTest(root); return 0; }
            if (args[0] == "measure") { Measure(root, options); return 0; }
            throw new ArgumentException("Unknown development probe command.");
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
    private static Dictionary<string, string> Options(string[] args)
    {
        if (args.Length % 2 != 0) throw new ArgumentException("Options require --name value pairs.");
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int i = 0; i < args.Length; i += 2)
        { if (!args[i].StartsWith("--", StringComparison.Ordinal) || !result.TryAdd(args[i][2..], args[i + 1])) throw new ArgumentException("Duplicate or invalid option."); }
        return result;
    }
    private static string Required(Dictionary<string, string> options, string name) => options.GetValueOrDefault(name) ?? throw new ArgumentException("Missing --" + name);
    private static int Number(Dictionary<string, string> options, string name, int fallback) => options.TryGetValue(name, out string? value) ? int.Parse(value) : fallback;
    private static string NewRoot(string path)
    {
        string root = Path.GetFullPath(path);
        if (Directory.Exists(root) || File.Exists(root)) throw new IOException("Probe root must not already exist.");
        Directory.CreateDirectory(root); File.WriteAllText(Path.Combine(root, "probe-owned.json"), JsonSerializer.Serialize(new { version = 1, kind = "bounded-log-probe", root }));
        return root;
    }
    private static IProbeStore Open(string method, string path, BasisFixture basis, bool create, Action<string>? boundary = null) => method switch
    { "wal" => new WalProbeStore(path, basis, create, boundary), "sjl4" => new SjlProbeStore(path, basis, boundary), _ => throw new ArgumentException("Method must be wal or sjl4.") };

    private static void Measure(string root, Dictionary<string, string> options)
    {
        string method = Required(options, "method");
        if (method is not ("wal" or "sjl4")) throw new ArgumentException("Method must be wal or sjl4.");
        int objects = Number(options, "objects", 20000), hot = Number(options, "hot", 256);
        int seed = checked(Number(options, "seed-kib", 192) * 1024), writes = Number(options, "writes", 4096);
        long cap = checked(Number(options, "cap-mib", 4) * 1048576L);
        if (objects < 1 || objects > 20000 || hot < 1 || hot > objects || hot > 256 || seed < 4096 || seed > 192 * 1024 || seed % 4096 != 0 || writes < 1 || writes > 100000)
            throw new ArgumentException("Probe resources exceed the declared bounds.");
        string basisPath = Path.Combine(root, "original.bin"), storePath = Path.Combine(root, "store"), resultPath = Path.Combine(root, "independent-result.bin");
        var identity = BasisFixture.Create(basisPath, "bounded-probe", objects, hot, 256 * 1024, cap);
        WriteJson(Path.Combine(root, "identity.json"), identity);
        object before, after; double initializationSeconds, elapsedSeconds, validationSeconds, exportSeconds, reopenSeconds, teardownSeconds;
        long allocated, peakWorkingSet; string resultHash, logicalHash; long seedWrites = 0;
        long[] latencies = new long[writes];
        using (var basis = new BasisFixture(basisPath, identity))
        {
            var initial = Stopwatch.StartNew();
            using (var store = Open(method, storePath, basis, true))
            {
                foreach (var mutation in Workload.Seeds(identity, seed)) { store.Write(mutation.File, mutation.Offset, mutation.Bytes); seedWrites++; }
                if (store is WalProbeStore wal) wal.Store.Checkpoint();
                initializationSeconds = initial.Elapsed.TotalSeconds; before = store.Metrics();
                long allocationStart = GC.GetTotalAllocatedBytes(true); var timer = Stopwatch.StartNew();
                for (int i = 0; i < writes; i++)
                {
                    var mutation = Workload.At(i, identity, seed); long start = Stopwatch.GetTimestamp();
                    store.Write(mutation.File, mutation.Offset, mutation.Bytes); latencies[i] = Stopwatch.GetTimestamp() - start;
                }
                // Count final consolidation and old-WAL retirement in throughput,
                // rather than shifting it outside the reported write interval.
                if (store is WalProbeStore completed) completed.Store.Checkpoint();
                elapsedSeconds = timer.Elapsed.TotalSeconds; allocated = GC.GetTotalAllocatedBytes(true) - allocationStart;
                after = store.Metrics(); peakWorkingSet = Process.GetCurrentProcess().PeakWorkingSet64;
                if (store.Sequence != seedWrites + writes) throw new InvalidDataException("Mutation ACK sequence differs.");
                timer.Restart(); logicalHash = Verify(store, identity, seed, writes, seeded: true); validationSeconds = timer.Elapsed.TotalSeconds;
                timer.Restart(); resultHash = ResultArchive.Export(resultPath, store, identity); exportSeconds = timer.Elapsed.TotalSeconds;
            }
            var reopen = Stopwatch.StartNew();
            using (var resumed = Open(method, storePath, basis, false))
            {
                if (resumed.Sequence != seedWrites + writes || Verify(resumed, identity, seed, writes, true) != logicalHash)
                    throw new InvalidDataException("Reconnect content or sequence differs.");
            }
            reopenSeconds = reopen.Elapsed.TotalSeconds;
        }
        string basisAfter = BoundedWal.HashFile(basisPath);
        if (basisAfter != identity.BasisHash) throw new InvalidDataException("Original was modified.");
        var inventory = Inventory(storePath);
        var teardown = Stopwatch.StartNew(); DeleteOwnedStore(root, storePath); teardownSeconds = teardown.Elapsed.TotalSeconds;
        long independentPayload = ResultArchive.Verify(resultPath, resultHash, identity);
        Array.Sort(latencies);
        var report = new
        {
            version = 1, method, scope = "Development serial range-write fixture; not a product, Unity, kernel or power-loss qualification.",
            identity, writes, seedWrites, liveSeedPrivateBytes = (long)seed * hot, seedBytesPerHotFile = seed,
            payload = "deterministic incompressible 4/8/16KiB; immutable original in one pinned physical file with logical objects",
            initializationSeconds, elapsedSeconds, writesPerSecond = writes / elapsedSeconds,
            ackP50Milliseconds = latencies[writes / 2] * 1000.0 / Stopwatch.Frequency,
            ackP95Milliseconds = latencies[Math.Min(writes - 1, (int)(writes * .95))] * 1000.0 / Stopwatch.Frequency,
            ackMaxMilliseconds = latencies[^1] * 1000.0 / Stopwatch.Frequency,
            finalCheckpointIncluded = true, allocatedManagedBytes = allocated, processPeakWorkingSetBytes = peakWorkingSet,
            before, after, inventory, validationSeconds, reopenSeconds, exportSeconds, teardownSeconds,
            originalUnchanged = true, logicalHash, independentResult = new { resultHash, independentPayload, survivesStoreTeardown = true },
            stopwatchFrequency = Stopwatch.Frequency,
            limits = new[] { "Single ordered run; OS cache not reset", "Peak owned file bytes sampled at transaction/checkpoint boundaries, not NTFS allocation peak", "No global memory bound; current private bytes retained in RAM", "WAL allows capped obsolete history and checkpoint duplicate private bytes", "Serial per-ACK durability, no group commit", "Current SJL4 production path compared directly" }
        };
        WriteJson(Path.Combine(root, "result.json"), report); Print(report);
    }

    private static string Verify(IProbeStore store, ProbeIdentity identity, int seed, int writes, bool seeded)
    {
        var expected = new byte[identity.HotObjects][];
        for (int file = 0; file < expected.Length; file++)
        {
            expected[file] = new byte[seed]; long baseAt = identity.FileOffset(file);
            for (int i = 0; i < seed; i++) expected[file][i] = (byte)((baseAt + i) * 37 + 19);
        }
        if (seeded) foreach (var mutation in Workload.Seeds(identity, seed)) mutation.Bytes.CopyTo(expected[mutation.File], mutation.Offset);
        for (int i = 0; i < writes; i++) { var mutation = Workload.At(i, identity, seed); mutation.Bytes.CopyTo(expected[mutation.File], mutation.Offset); }
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] read = new byte[65536], oracle = new byte[65536];
        for (int file = 0; file < identity.Objects; file++)
            for (long offset = 0; offset < identity.FileLength(file);)
            {
                int count = (int)Math.Min(read.Length, identity.FileLength(file) - offset);
                for (int i = 0; i < count; i++) oracle[i] = file < identity.HotObjects && offset + i < seed
                    ? expected[file][offset + i] : (byte)((identity.FileOffset(file) + offset + i) * 37 + 19);
                if (store.Read(file, offset, read.AsSpan(0, count)) != count || !read.AsSpan(0, count).SequenceEqual(oracle.AsSpan(0, count)))
                    throw new InvalidDataException($"Logical content differs at object {file}, offset {offset}.");
                hash.AppendData(read.AsSpan(0, count)); offset += count;
            }
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    private static async Task SelfTest(string root)
    {
        var results = new List<object>(); string basisPath = Path.Combine(root, "original.bin");
        var identity = BasisFixture.Create(basisPath, "selftest", 8, 2, 16384, 16384);
        using (var basis = new BasisFixture(basisPath, identity))
        {
            string? expectedHash = null;
            foreach (string method in new[] { "sjl4", "wal" })
            {
                string directory = Path.Combine(root, "normal-" + method); string result = Path.Combine(root, method + "-result.bin"); string resultHash;
                using (var store = Open(method, directory, basis, true))
                {
                    for (int i = 0; i < 12; i++) { var change = Workload.At(i, identity, 8192); store.Write(change.File, change.Offset, change.Bytes); }
                    string hash = Verify(store, identity, 8192, 12, false);
                    if (expectedHash is not null && hash != expectedHash) throw new InvalidDataException("Compared methods differ."); expectedHash = hash;
                    resultHash = ResultArchive.Export(result, store, identity);
                }
                using (var store = Open(method, directory, basis, false))
                { if (store.Sequence != 12 || Verify(store, identity, 8192, 12, false) != expectedHash) throw new InvalidDataException("Normal reconnect differs."); }
                DeleteOwnedStore(root, directory); ResultArchive.Verify(result, resultHash, identity);
                results.Add(new { test = "normal-content-reconnect-independent-result", method, passed = true });
            }
        }
        string[] boundaries = ["wal.after-header", "wal.after-prefix", "wal.before-flush", "wal.after-flush", "wal.before-ack",
            "checkpoint.after-intent", "checkpoint.after-header", "checkpoint.after-data-flush", "checkpoint.after-new-wal",
            "checkpoint.before-publication", "checkpoint.after-publication", "checkpoint.after-old-retirement"];
        string[] sjlBoundaries = ["latest.after-intent-header", "latest.after-intent-prefix", "latest.after-intent-flush",
            "latest.after-root-publication", "latest.after-object-reclamation", "latest.after-ack-ready"];
        foreach (string boundary in boundaries.Concat(sjlBoundaries))
        {
            string method = boundary.StartsWith("latest.", StringComparison.Ordinal) ? "sjl4" : "wal";
            string directory = Path.Combine(root, boundary.Replace('.', '-'));
            using (var setupBasis = new BasisFixture(basisPath, identity))
            using (var setup = Open(method, directory, setupBasis, true)) { }
            string acks = directory + ".acks", configPath = directory + ".worker.json";
            var config = new WorkerConfig(method, directory, basisPath, identity, acks, boundary, boundary.StartsWith("checkpoint.", StringComparison.Ordinal) ? 1 : 2, 8);
            WriteJson(configPath, config);
            await KillAtBoundary(configPath, boundary);
            long[] acknowledged = File.Exists(acks) ? File.ReadAllLines(acks).Select(long.Parse).ToArray() : [];
            if (!acknowledged.SequenceEqual(Enumerable.Range(1, acknowledged.Length).Select(i => (long)i))) throw new InvalidDataException("External ACKs are not a prefix.");
            using var basis = new BasisFixture(basisPath, identity);
            using var resumed = Open(method, directory, basis, false);
            if (resumed.Sequence < acknowledged.Length || resumed.Sequence > acknowledged.Length + 1) throw new InvalidDataException("Recovered sequence lost an ACK or skipped an unacknowledged mutation.");
            Verify(resumed, identity, 4096, checked((int)resumed.Sequence), false);
            var wal = resumed as WalProbeStore;
            if (wal?.Store.PeakWalBytes > identity.WalCapBytes) throw new InvalidDataException("WAL cap exceeded.");
            results.Add(new { test = "external-process-termination", method, boundary, acked = acknowledged.Length, recovered = resumed.Sequence, incompleteTailBytes = wal?.Store.IncompleteTailBytes, passed = true });
        }
        string corruption = Path.Combine(root, "corrupt");
        using (var store = new BoundedWal(corruption, identity, true)) store.Write(0, 0, Workload.Payload(1, 0, 4096));
        string walPath = Directory.GetFiles(corruption, "wal-*.bin").Single();
        using (var file = new FileStream(walPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        { file.Position = BoundedWal.FileHeaderBytes + BoundedWal.FrameHeaderBytes + 7; int previous = file.ReadByte(); file.Position--; file.WriteByte((byte)(previous ^ 1)); file.Flush(true); }
        string corruptHash = BoundedWal.HashFile(walPath);
        ExpectReject(() => { using var bad = new BoundedWal(corruption, identity, false); });
        if (BoundedWal.HashFile(walPath) != corruptHash) throw new InvalidDataException("Corrupt input was modified.");
        results.Add(new { test = "complete-frame-corruption-preserved", passed = true });
        string checkpointCorruption = Path.Combine(root, "checkpoint-corrupt");
        using (var store = new BoundedWal(checkpointCorruption, identity, true))
        { store.Write(0, 0, Workload.Payload(1, 0, 4096)); store.Checkpoint(); }
        string checkpointPath = Directory.GetFiles(checkpointCorruption, "checkpoint-*.bin").Single();
        using (var file = new FileStream(checkpointPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        { file.Position = BoundedWal.FileHeaderBytes + BoundedWal.FrameHeaderBytes + 7; int previous = file.ReadByte(); file.Position--; file.WriteByte((byte)(previous ^ 1)); file.Flush(true); }
        var beforeCorruptCheckpoint = Inventory(checkpointCorruption);
        ExpectReject(() => { using var bad = new BoundedWal(checkpointCorruption, identity, false); });
        if (JsonSerializer.Serialize(beforeCorruptCheckpoint) != JsonSerializer.Serialize(Inventory(checkpointCorruption))) throw new InvalidDataException("Corrupt checkpoint was modified.");
        results.Add(new { test = "checkpoint-corruption-preserved", passed = true });
        string partialHeader = Path.Combine(root, "partial-header");
        using (var store = new BoundedWal(partialHeader, identity, true))
        { var mutation = Workload.At(0, identity, 4096); store.Write(mutation.File, mutation.Offset, mutation.Bytes); }
        using (var file = new FileStream(Directory.GetFiles(partialHeader, "wal-*.bin").Single(), FileMode.Append, FileAccess.Write, FileShare.None))
        { file.Write([66, 87, 70, 49, 1, 0, 0]); file.Flush(true); }
        using (var basis = new BasisFixture(basisPath, identity))
        using (var resumed = new WalProbeStore(partialHeader, basis, false))
        {
            if (resumed.Sequence != 1 || resumed.Store.IncompleteTailBytes != 7) throw new InvalidDataException("Incomplete header recovery differs.");
            Verify(resumed, identity, 4096, 1, false);
        }
        results.Add(new { test = "physical-incomplete-header-truncated-after-acked-frame", passed = true });
        string owned = Path.Combine(root, "owner"); using (var store = new BoundedWal(owned, identity, true)) { }
        var beforeOwner = Inventory(owned);
        ExpectReject(() => { using var bad = new BoundedWal(owned, identity with { Owner = "foreign" }, false); });
        if (JsonSerializer.Serialize(beforeOwner) != JsonSerializer.Serialize(Inventory(owned))) throw new InvalidDataException("Wrong-owner open mutated files.");
        File.WriteAllText(Path.Combine(owned, "foreign.file"), "keep");
        ExpectReject(() => { using var bad = new BoundedWal(owned, identity, false); });
        if (File.ReadAllText(Path.Combine(owned, "foreign.file")) != "keep") throw new InvalidDataException("Unknown member was changed.");
        results.Add(new { test = "wrong-owner-and-unknown-file-preserved", passed = true });
        if (BoundedWal.HashFile(basisPath) != identity.BasisHash) throw new InvalidDataException("Immutable original changed.");
        var report = new { version = 1, passed = results.Count, failed = 0, originalUnchanged = true, results,
            limits = new[] { "Process termination only; no OS power-loss claim", "Not a WinFsp/Unity or production backend qualification", "ACK prefix is an external durable test log", "No timing result produced by selftest" } };
        WriteJson(Path.Combine(root, "selftest-result.json"), report); Print(report);
    }

    private static void Worker(string configPath)
    {
        var config = JsonSerializer.Deserialize<WorkerConfig>(File.ReadAllText(configPath), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        int seen = 0;
        using var basis = new BasisFixture(config.Basis, config.Identity);
        using var store = Open(config.Method, config.Store, basis, false, at =>
        {
            if (at != config.Boundary || ++seen != config.Occurrence) return;
            Console.WriteLine(JsonSerializer.Serialize(new { boundary = at })); Console.Out.Flush();
            if (Console.ReadLine() != "continue") throw new IOException("Boundary was not resumed.");
        });
        using var acks = new FileStream(config.Acks, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        for (int i = 0; i < config.Writes; i++)
        {
            var mutation = Workload.At(i, config.Identity, 4096); store.Write(mutation.File, mutation.Offset, mutation.Bytes);
            acks.Write(Encoding.ASCII.GetBytes(store.Sequence + "\n")); acks.Flush(true);
        }
    }
    private static async Task KillAtBoundary(string config, string boundary)
    {
        var info = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
        info.ArgumentList.Add(Assembly.GetExecutingAssembly().Location); info.ArgumentList.Add("worker"); info.ArgumentList.Add(config);
        using var process = Process.Start(info) ?? throw new IOException("Could not start owned recovery child.");
        Task<string> errors = process.StandardError.ReadToEndAsync();
        try
        {
            string? line = await process.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(20));
            if (line is null || JsonDocument.Parse(line).RootElement.GetProperty("boundary").GetString() != boundary)
                throw new IOException("Owned child did not reach its boundary: " + await errors.WaitAsync(TimeSpan.FromSeconds(2)));
            process.Kill(entireProcessTree: true); await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
        }
        finally { if (!process.HasExited) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(); } }
    }
    private static void ExpectReject(Action action)
    { try { action(); } catch (InvalidDataException) { return; } throw new InvalidDataException("Unsafe state was accepted."); }
    private static object Inventory(string directory) => new
    {
        files = Directory.EnumerateFiles(directory).Order(StringComparer.Ordinal).Select(p => new { name = Path.GetFileName(p), bytes = new FileInfo(p).Length, hash = BoundedWal.HashFile(p) }).ToArray()
    };
    private static void DeleteOwnedStore(string root, string store)
    {
        string expectedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string target = Path.GetFullPath(store);
        if (!File.Exists(Path.Combine(root, "probe-owned.json")) || !target.StartsWith(expectedRoot, StringComparison.OrdinalIgnoreCase) ||
            target == Path.GetFullPath(root)) throw new InvalidDataException("Teardown target is not inside this exact probe root.");
        foreach (string path in Directory.EnumerateFileSystemEntries(target, "*", SearchOption.AllDirectories))
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Teardown preserves reparse points.");
        Directory.Delete(target, true);
    }
    private static void WriteJson(string path, object value) => File.WriteAllText(path, JsonSerializer.Serialize(value, Json));
    private static void Print(object value) => Console.WriteLine(JsonSerializer.Serialize(value, Json));
}
