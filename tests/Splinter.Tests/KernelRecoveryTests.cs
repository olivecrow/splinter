using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using System.Security.Cryptography;
using System.Text.Json;
using Fsp;
using Splinter.Core;
using Splinter.Platform.Windows;
using Splinter.Protocol;
using Splinter.Runtime;

namespace Splinter.Tests;

public sealed class KernelRecoveryTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    public static readonly string[] CaseNames = new[] { "write", "rename", "replace", "truncate", "flush" }
        .SelectMany(op => new[] { "callback.before-effect", "callback.after-effect", "client.after-ack" }.Select(b => op + ":" + b))
        .Concat(["mmap:client.before-view-flush", "mmap:callback.before-effect", "mmap:callback.after-effect", "mmap:client.after-ack"]).ToArray();
    private sealed record Request(int Version, string Directory, string MountPath, string Operation, string Boundary, string Nonce, int Generation);
    private sealed record Signal(int Version, string Nonce, string Boundary, int ProcessId, long ProcessStartTimeUtcTicks,
        string RequestDigest, string Operation, string Path, long RequestedBytes, long CompletedBytes);

    [Fact]
    public async Task EvidenceRun()
    {
        string? configuration = Environment.GetEnvironmentVariable("SPLINTER_KERNEL_RECOVERY_RUN");
        if (configuration is null) Assert.Skip("External WinFsp run requires SPLINTER_KERNEL_RECOVERY_RUN.");
        Assert.True(OperatingSystem.IsWindows(), "Real Windows/WinFsp run required.");
        var config = JsonSerializer.Deserialize<JsonElement>(configuration);
        string root = Path.GetFullPath(config.GetProperty("evidencePath").GetString()!), home = Path.GetFullPath(config.GetProperty("mountHome").GetString()!);
        string[] cases = config.GetProperty("caseNames").EnumerateArray().Select(e => e.GetString()!).ToArray();
        Assert.NotEmpty(cases); Assert.Equal(cases.Length, cases.Distinct().Count()); Assert.All(cases, name => Assert.Contains(name, CaseNames));
        RequireNew(root); RequireNew(home); Directory.CreateDirectory(root); Directory.CreateDirectory(home);
        var binaries = new[] { typeof(JournalDeltaStore).Assembly.Location, typeof(WinFspOverlayFileSystem).Assembly.Location,
            typeof(ContractCatalog).Assembly.Location, typeof(KernelRecoveryTests).Assembly.Location,
            typeof(IOverlayMutationObserver).Assembly.Location, typeof(FileSystemHost).Assembly.Location }
            .Select(p => new { path = p, sha256 = Hash(File.ReadAllBytes(p)) }).ToArray();
        WriteNew(Path.Combine(root, "candidate.json"), new { version = 1, binaries, os = Environment.OSVersion.ToString(), runtime = Environment.Version.ToString(),
            fixture = "real local WinFsp mount; deterministic fake snapshot; no VSS/Git/Unity", caseNames = cases, mountHome = home });
        var results = new List<JsonElement>();
        for (int i = 0; i < cases.Length; i++)
        {
            string[] parts = cases[i].Split(':');
            results.Add(await RunTrial(Path.Combine(root, cases[i].Replace(':', '-')), Path.Combine(home, "t" + i, "view"), parts[0], parts[1], TestContext.Current.CancellationToken));
        }
        foreach (var binary in binaries) Assert.Equal(binary.sha256, Hash(File.ReadAllBytes(binary.path)));
        WriteNew(Path.Combine(root, "result.json"), new { version = 1, passed = results.Count, failed = 0, fullMatrix = cases.Length == CaseNames.Length,
            qualification = "real-kernel-fake-snapshot-only", results });
    }

    [Fact]
    public void ChildProvider()
    {
        string? input = Environment.GetEnvironmentVariable("SPLINTER_KERNEL_RECOVERY_CHILD");
        if (input is null) Assert.Skip("Subprocess-only entrypoint requires SPLINTER_KERNEL_RECOVERY_CHILD.");
        Request request = Load(input); var tree = new Tree(); string ready = Path.Combine(request.Directory, "ready-" + request.Generation + ".json");
        Directory.CreateDirectory(Path.GetDirectoryName(request.MountPath)!);
        if (System.IO.Directory.Exists(request.MountPath))
        { Assert.True(MountAbsent(request.MountPath)); System.IO.Directory.Delete(request.MountPath, false); }
        using var store = new JournalDeltaStore(Path.Combine(request.Directory, "delta"), Identity(request), tree, optimizeJournal: false);
        var gate = new Gate(request, input);
        var fs = new WinFspOverlayFileSystem(store, ex => WriteNew(Path.Combine(request.Directory, "io-error-" + Guid.NewGuid().ToString("N") + ".json"),
            new { type = ex.GetType().Name, ex.Message, ex.HResult }), request.Generation == 1 ? gate : null);
        using var host = new FileSystemHost(fs);
        int status = host.MountEx(request.MountPath, 8, null, false, 0);
        Assert.True(status >= 0, "MountEx status: 0x" + status.ToString("x8"));
        WriteNew(ready, new { version = 1, request.Nonce, request.Generation, request.MountPath, requestDigest = Hash(File.ReadAllBytes(input)),
            processId = Environment.ProcessId, processStartTimeUtcTicks = Process.GetCurrentProcess().StartTime.ToUniversalTime().Ticks, storage = store.Observe() });
        var timeout = Stopwatch.StartNew();
        while (timeout.Elapsed < TimeSpan.FromMinutes(2))
        {
            if (request.Generation == 1 && !gate.Armed && File.Exists(Path.Combine(request.Directory, "prefix.json")))
            {
                WriteNew(Path.Combine(request.Directory, "prefix-storage.json"), store.Observe());
                gate.Armed = true; WriteNew(Path.Combine(request.Directory, "armed.json"), new { request.Nonce });
            }
            string stop = Path.Combine(request.Directory, "stop-" + request.Generation + ".json");
            if (File.Exists(stop))
            {
                Assert.Equal(request.Nonce, JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(stop)).GetProperty("nonce").GetString());
                if (fs.OpenHandles == 0 && fs.RetainedFileContexts == 0)
                {
                    store.Flush(); host.Unmount();
                    WriteNew(Path.Combine(request.Directory, "stopped-" + request.Generation + ".json"), new { version = 1, request.Nonce,
                        openHandles = fs.OpenHandles, retainedFileContexts = fs.RetainedFileContexts, storage = store.Observe() });
                    return;
                }
            }
            Thread.Sleep(20);
        }
        throw new TimeoutException("Unclaimed owned provider; evidence preserved.");
    }

    [Fact]
    public void ChildClient()
    {
        string? input = Environment.GetEnvironmentVariable("SPLINTER_KERNEL_RECOVERY_CHILD");
        if (input is null) Assert.Skip("Subprocess-only entrypoint requires SPLINTER_KERNEL_RECOVERY_CHILD.");
        Request request = Load(input); string alpha = Path.Combine(request.MountPath, "alpha");
        try
        {
            WaitFile(Path.Combine(request.Directory, "ready-1.json"));
            using (var prefix = OpenWrite(alpha)) { prefix.Write(Prefix()); prefix.Flush(true); }
            Assert.Equal(Model(false, request.Operation), ReadKernelModel(request.MountPath));
            WriteNew(Path.Combine(request.Directory, "prefix.json"), new { version = 1, request.Nonce, modelHash = Model(false, request.Operation) });
            WaitFile(Path.Combine(request.Directory, "armed.json"));
            if (request.Operation == "write" && request.Boundary == "callback.after-effect") WaitFile(Path.Combine(request.Directory, "reader-ready.json"));
            switch (request.Operation)
            {
                case "write": using (var file = OpenWrite(alpha)) { file.Position = 8192; file.Write(Patch()); file.Flush(true); } break;
                case "rename": File.Move(alpha, Path.Combine(request.MountPath, "gamma")); break;
                case "replace": File.Move(alpha, Path.Combine(request.MountPath, "beta"), true); break;
                case "truncate": using (var file = OpenWrite(alpha)) { file.SetLength(4096); file.Flush(true); } break;
                case "flush": using (var file = OpenWrite(alpha)) file.Flush(true); break;
                case "mmap":
                    using (var file = new FileStream(alpha, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete))
                    using (var map = MemoryMappedFile.CreateFromFile(file, null, 0, MemoryMappedFileAccess.ReadWrite, HandleInheritability.None, true))
                    using (var view = map.CreateViewAccessor(0, 65536, MemoryMappedFileAccess.ReadWrite))
                    {
                        view.WriteArray(8192, Patch(), 0, 4096);
                        if (request.Boundary == "client.before-view-flush") ClientGate(request, input, "client.before-view-flush");
                        view.Flush(); file.Flush(true); // Both explicit boundaries are required before external ACK.
                    }
                    break;
                default: throw new InvalidOperationException("Unknown operation.");
            }
            Assert.Equal(Model(true, request.Operation), ReadKernelModel(request.MountPath));
            WriteNew(Path.Combine(request.Directory, "ack.json"), new { version = 1, request.Nonce, modelHash = Model(true, request.Operation),
                explicitViewAndFileFlush = request.Operation == "mmap" });
            ClientGate(request, input, "client.after-ack");
            throw new InvalidOperationException("Requested callback did not gate the native operation.");
        }
        catch (Exception error)
        {
            WriteNew(Path.Combine(request.Directory, "client-outcome.json"), new { version = 1, request.Nonce, type = error.GetType().Name, error.Message, error.HResult });
            throw;
        }
    }

    [Fact]
    public void ChildReader()
    {
        string? input = Environment.GetEnvironmentVariable("SPLINTER_KERNEL_RECOVERY_CHILD");
        if (input is null) Assert.Skip("Subprocess-only entrypoint requires SPLINTER_KERNEL_RECOVERY_CHILD.");
        var request = Load(input); WaitFile(Path.Combine(request.Directory, "ready-1.json"));
        using var reader = new FileStream(Path.Combine(request.MountPath, "beta"), FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        WriteNew(Path.Combine(request.Directory, "reader-ready.json"), new { request.Nonce, processId = Environment.ProcessId,
            processStartTimeUtcTicks = Process.GetCurrentProcess().StartTime.ToUniversalTime().Ticks });
        WaitFile(Path.Combine(request.Directory, "reader-go.json"));
        byte[] bytes = new byte[4096]; reader.ReadExactly(bytes);
        Assert.Equal(Beta(), bytes);
        WriteNew(Path.Combine(request.Directory, "concurrent-reader.json"), new { version = 1, request.Nonce, path = "beta", sha256 = Hash(bytes),
            openedBeforeWriterGate = true, completedWhileWriterGated = true });
    }

    private static async Task<JsonElement> RunTrial(string directory, string mount, string operation, string boundary, CancellationToken ct)
    {
        RequireNew(directory); Directory.CreateDirectory(directory);
        var request = new Request(1, directory, mount, operation, boundary, Guid.NewGuid().ToString("N"), 1);
        string input = Path.Combine(directory, "request.json"); WriteNew(input, request);
        var catalog = new ContractCatalog(); Assert.True(catalog.Validate("urn:splinter:v1:kernel-recovery-probe-request", JsonSerializer.SerializeToElement(request, Json)).IsValid);
        using var provider = StartActor(input, "ChildProvider"); using var client = StartActor(input, "ChildClient");
        // Client starts only after the owned kernel mount is ready; its entrypoint waits below via ready file.
        Task<string> providerOut = provider.StandardOutput.ReadToEndAsync(ct), providerErr = provider.StandardError.ReadToEndAsync(ct),
            clientOut = client.StandardOutput.ReadToEndAsync(ct), clientErr = client.StandardError.ReadToEndAsync(ct);
        Process? resumed = null;
        Task<string>? resumedOut = null, resumedErr = null;
        Process? concurrentReader = null; Task<string>? readerOut = null, readerErr = null;
        try
        {
            await WaitFor(Path.Combine(directory, "ready-1.json"), provider, ct);
            var ready = JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(Path.Combine(directory, "ready-1.json")));
            RequireOwner(ready, provider); Assert.Equal(Hash(File.ReadAllBytes(input)), ready.GetProperty("requestDigest").GetString());
            bool concurrent = operation == "write" && boundary == "callback.after-effect";
            if (concurrent)
            {
                concurrentReader = StartActor(input, "ChildReader"); readerOut = concurrentReader.StandardOutput.ReadToEndAsync(ct); readerErr = concurrentReader.StandardError.ReadToEndAsync(ct);
                WriteNew(Path.Combine(directory, "reader-launch.json"), new { request.Nonce, processId = concurrentReader.Id,
                    processStartTimeUtcTicks = concurrentReader.StartTime.ToUniversalTime().Ticks });
            }
            await WaitFor(Path.Combine(directory, "signal.json"), boundary.StartsWith("callback", StringComparison.Ordinal) ? provider : client, ct);
            var signal = JsonSerializer.Deserialize<Signal>(File.ReadAllText(Path.Combine(directory, "signal.json")), Json)!;
            Assert.True(catalog.Validate("urn:splinter:v1:kernel-recovery-probe-signal", JsonSerializer.SerializeToElement(signal, Json)).IsValid);
            Assert.Equal(request.Nonce, signal.Nonce); Assert.Equal(boundary, signal.Boundary); Assert.Equal(Hash(File.ReadAllBytes(input)), signal.RequestDigest);
            var gated = boundary.StartsWith("callback", StringComparison.Ordinal) ? provider : client;
            Assert.Equal(gated.Id, signal.ProcessId); Assert.Equal(gated.StartTime.ToUniversalTime().Ticks, signal.ProcessStartTimeUtcTicks);
            if (concurrent)
            {
                var readerReady = JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(Path.Combine(directory, "reader-ready.json")));
                RequireOwner(readerReady, concurrentReader!); Assert.Equal(request.Nonce, readerReady.GetProperty("nonce").GetString());
                WriteNew(Path.Combine(directory, "reader-go.json"), new { request.Nonce });
                // Cleanup also owns mutationGate. Observe ReadExactly's response, not process exit/Close,
                // while the write callback is deliberately holding that gate.
                await WaitFor(Path.Combine(directory, "concurrent-reader.json"), concurrentReader!, ct);
            }
            Assert.Equal(boundary == "client.after-ack", File.Exists(Path.Combine(directory, "ack.json")));
            WriteNew(Path.Combine(directory, "termination.json"), new { version = 1, request.Nonce,
                providerId = provider.Id, providerStartTimeUtcTicks = provider.StartTime.ToUniversalTime().Ticks,
                clientId = client.Id, clientStartTimeUtcTicks = client.StartTime.ToUniversalTime().Ticks, gracefulProviderStop = false });
            await KillOwned(provider); await KillOwned(client);
            if (concurrentReader is not null)
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(15));
                await concurrentReader.WaitForExitAsync(timeout.Token); Assert.True(concurrentReader.ExitCode == 0, await readerOut! + await readerErr!);
            }
            await WaitMountAbsent(mount, ct);
            string journal = Path.Combine(directory, "delta", "delta.journal"); byte[] before = await ReadAfterExit(journal, ct);
            WriteBytesNew(Path.Combine(directory, "journal-before-replay.bin"), before);
            string model; OverlayStorageObservation observed;
            using (var store = new JournalDeltaStore(Path.Combine(directory, "delta"), Identity(request), new Tree(), optimizeJournal: false))
            { model = ReadStoreModel(store); observed = store.Observe(); }
            bool mustPreserveNew = boundary is "callback.after-effect" or "client.after-ack";
            if (mustPreserveNew) Assert.Equal(Model(true, operation), model);
            else if (operation == "mmap") Assert.True(model == Model(false, operation) || model == Model(true, operation));
            else Assert.Equal(Model(false, operation), model);
            byte[] after = File.ReadAllBytes(journal); Assert.Equal(before, after);
            Assert.Equal(0, observed.InitialPayloadCopiedBytes); Assert.Equal(0, observed.ReadCacheBytes);
            using (var store = new JournalDeltaStore(Path.Combine(directory, "delta"), Identity(request), new Tree(), optimizeJournal: false)) Assert.Equal(model, ReadStoreModel(store));
            Assert.Equal(after, File.ReadAllBytes(journal));
            bool pending = model == Model(true, operation);
            string nextInput = Path.Combine(directory, "resume-request.json"); WriteNew(nextInput, request with { Generation = 2 });
            resumed = StartActor(nextInput, "ChildProvider"); resumedOut = resumed.StandardOutput.ReadToEndAsync(ct); resumedErr = resumed.StandardError.ReadToEndAsync(ct);
            await WaitFor(Path.Combine(directory, "ready-2.json"), resumed, ct);
            var resumedReady = JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(Path.Combine(directory, "ready-2.json"))); RequireOwner(resumedReady, resumed);
            Assert.NotEqual(ready.GetProperty("processStartTimeUtcTicks").GetInt64(), resumedReady.GetProperty("processStartTimeUtcTicks").GetInt64());
            Assert.Equal(model, ReadKernelModel(mount));
            using (var writer = OpenWrite(Path.Combine(mount, "beta"))) { writer.Write(Rewrite()); writer.Flush(true); }
            Assert.Equal(Model(pending, operation, true), ReadKernelModel(mount));
            WriteNew(Path.Combine(directory, "stop-2.json"), new { request.Nonce });
            await WaitFor(Path.Combine(directory, "stopped-2.json"), resumed, ct); await resumed.WaitForExitAsync(ct);
            Assert.True(resumed.ExitCode == 0, await resumedOut + await resumedErr);
            await WaitMountAbsent(mount, ct);
            var stopped = JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(Path.Combine(directory, "stopped-2.json")));
            Assert.Equal(0, stopped.GetProperty("openHandles").GetInt32()); Assert.Equal(0, stopped.GetProperty("retainedFileContexts").GetInt32());
            using (var store = new JournalDeltaStore(Path.Combine(directory, "delta"), Identity(request), new Tree(), optimizeJournal: false)) Assert.Equal(Model(pending, operation, true), ReadStoreModel(store));
            var result = JsonSerializer.SerializeToElement(new { version = 1, request.Operation, request.Boundary, request.Nonce,
                actualProviderTermination = true, prefixPreserved = true, externalFinalAck = boundary == "client.after-ack",
                replayModelHash = model, journalBeforeHash = Hash(before), journalAfterHash = Hash(after), secondReplayUnchanged = true,
                samePathNewProviderReadWrite = true, concurrentDifferentFileReader = concurrent, mountAbsent = true, ownedLiveChildren = 0,
                openHandles = 0, retainedFileContexts = 0, initialPayloadCopiedBytes = observed.InitialPayloadCopiedBytes, readCacheBytes = observed.ReadCacheBytes,
                baseReadBytes = observed.Metrics!.BaseReadBytes, durableSequence = observed.DurableSequence }, Json);
            Assert.True(catalog.Validate("urn:splinter:v1:kernel-recovery-probe-result", result).IsValid);
            WriteNew(Path.Combine(directory, "result.json"), result); return result;
        }
        finally
        {
            await KillOwned(provider); await KillOwned(client); if (resumed is not null) await KillOwned(resumed); if (concurrentReader is not null) await KillOwned(concurrentReader);
            WriteNew(Path.Combine(directory, "process-output.json"), new { providerOut = await providerOut, providerErr = await providerErr,
                clientOut = await clientOut, clientErr = await clientErr, resumedOut = resumedOut is null ? null : await resumedOut,
                resumedErr = resumedErr is null ? null : await resumedErr,
                readerOut = readerOut is null ? null : await readerOut, readerErr = readerErr is null ? null : await readerErr }); resumed?.Dispose(); concurrentReader?.Dispose();
        }
    }

    private sealed class Gate(Request request, string input) : IOverlayMutationObserver
    {
        private int armed, claimed;
        public bool Armed { get => Volatile.Read(ref armed) != 0; set => Volatile.Write(ref armed, value ? 1 : 0); }
        public void Observe(OverlayMutationBoundary boundary)
        {
            string expected = request.Operation == "mmap" ? "flush" : request.Operation;
            if (!Armed || request.Boundary != boundary.Boundary || expected != boundary.Operation ||
                boundary.Path != "alpha" && !(expected == "flush" && boundary.Path == "") || Interlocked.CompareExchange(ref claimed, 1, 0) != 0) return;
            var signal = new Signal(1, request.Nonce, boundary.Boundary, Environment.ProcessId,
                Process.GetCurrentProcess().StartTime.ToUniversalTime().Ticks, Hash(File.ReadAllBytes(input)), boundary.Operation,
                boundary.Path, boundary.RequestedBytes, boundary.CompletedBytes);
            WriteNew(Path.Combine(request.Directory, "signal.json"), signal); Hold();
        }
    }
    private static void ClientGate(Request r, string input, string boundary)
    {
        WriteNew(Path.Combine(r.Directory, "signal.json"), new Signal(1, r.Nonce, boundary, Environment.ProcessId,
            Process.GetCurrentProcess().StartTime.ToUniversalTime().Ticks, Hash(File.ReadAllBytes(input)), r.Operation, "alpha", 4096, boundary == "client.after-ack" ? 4096 : 0)); Hold();
    }
    private static void Hold() { using var gate = new ManualResetEventSlim(); if (!gate.Wait(TimeSpan.FromMinutes(1))) throw new TimeoutException("Unclaimed gate; preserve evidence."); }
    private static void WaitFile(string path) { var timer = Stopwatch.StartNew(); while (!File.Exists(path) && timer.Elapsed < TimeSpan.FromSeconds(25)) Thread.Sleep(20); Assert.True(File.Exists(path), "Missing " + path); }
    private static async Task WaitFor(string path, Process child, CancellationToken ct)
    { var timer = Stopwatch.StartNew(); while (!File.Exists(path) && !child.HasExited && timer.Elapsed < TimeSpan.FromSeconds(25)) await Task.Delay(20, ct); Assert.True(File.Exists(path), "Missing " + path + "; process exited=" + child.HasExited); }
    private static Process StartActor(string input, string method)
    {
        var info = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (string arg in new[] { typeof(KernelRecoveryTests).Assembly.Location, "-method", "Splinter.Tests.KernelRecoveryTests." + method }) info.ArgumentList.Add(arg);
        info.Environment.Remove("SPLINTER_KERNEL_RECOVERY_RUN"); info.Environment["SPLINTER_KERNEL_RECOVERY_CHILD"] = input;
        return Process.Start(info)!;
    }
    private static async Task KillOwned(Process process) { if (!process.HasExited) { process.Kill(); await process.WaitForExitAsync(CancellationToken.None); } }
    private static void RequireOwner(JsonElement proof, Process process) { Assert.Equal(process.Id, proof.GetProperty("processId").GetInt32()); Assert.Equal(process.StartTime.ToUniversalTime().Ticks, proof.GetProperty("processStartTimeUtcTicks").GetInt64()); }
    private static bool MountAbsent(string path)
    {
        try { var attrs = File.GetAttributes(path); return (attrs & FileAttributes.ReparsePoint) == 0 && !System.IO.Directory.EnumerateFileSystemEntries(path).Any(); }
        catch (FileNotFoundException) { return true; } catch (DirectoryNotFoundException) { return true; }
    }
    private static async Task WaitMountAbsent(string path, CancellationToken ct)
    { var timer = Stopwatch.StartNew(); while (!MountAbsent(path) && timer.Elapsed < TimeSpan.FromSeconds(10)) await Task.Delay(20, ct); Assert.True(MountAbsent(path), "Preserve a still-mounted/unknown path."); }
    private static async Task<byte[]> ReadAfterExit(string path, CancellationToken ct)
    {
        var timer = Stopwatch.StartNew();
        while (true)
        {
            try { using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete); using var bytes = new MemoryStream(); file.CopyTo(bytes); return bytes.ToArray(); }
            catch (IOException error) when ((error.HResult & 0xffff) is 32 or 33 && timer.Elapsed < TimeSpan.FromSeconds(5)) { await Task.Delay(20, ct); }
        }
    }
    private static Request Load(string path) => JsonSerializer.Deserialize<Request>(File.ReadAllText(path), Json)!;
    private static FileStream OpenWrite(string path) => new(path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete, 4096, FileOptions.WriteThrough);
    private static void RequireNew(string path) { Assert.False(System.IO.Directory.Exists(path) || File.Exists(path), "Existing path must be preserved: " + path); }
    private static byte[] Alpha() => Enumerable.Range(0, 65536).Select(i => (byte)(i % 251)).ToArray();
    private static byte[] Beta() => Enumerable.Repeat((byte)0x5a, 4096).ToArray();
    private static byte[] Prefix() => Enumerable.Repeat((byte)0x3c, 4096).ToArray();
    private static byte[] Patch() => Enumerable.Repeat((byte)0xa5, 4096).ToArray();
    private static byte[] Rewrite() => Enumerable.Repeat((byte)0x7e, 4096).ToArray();
    private static string Model(bool after, string operation, bool rewrite = false)
    {
        var alpha = Alpha(); Prefix().CopyTo(alpha, 0);
        var files = new SortedDictionary<string, byte[]>(StringComparer.Ordinal) { ["alpha"] = alpha, ["beta"] = Beta() };
        if (after) switch (operation)
        {
            case "write": case "mmap": Patch().CopyTo(alpha, 8192); break;
            case "rename": files["gamma"] = alpha; files.Remove("alpha"); break;
            case "replace": files["beta"] = alpha; files.Remove("alpha"); break;
            case "truncate": files["alpha"] = alpha[..4096]; break;
        }
        if (rewrite) Rewrite().CopyTo(files["beta"], 0);
        return Digest(files.Select(f => new { path = f.Key, length = f.Value.Length, sha256 = Hash(f.Value) }));
    }
    private static string ReadKernelModel(string mount) => Digest(System.IO.Directory.GetFiles(mount).OrderBy(Path.GetFileName, StringComparer.Ordinal).Select(path =>
    { byte[] bytes = File.ReadAllBytes(path); return new { path = Path.GetFileName(path), length = bytes.Length, sha256 = Hash(bytes) }; }));
    private static string ReadStoreModel(JournalDeltaStore store) => Digest(store.List("").OrderBy(e => e.Path, StringComparer.Ordinal).Select(e =>
    { byte[] bytes = new byte[e.Length]; Assert.Equal(bytes.Length, store.Read(e.Id, 0, bytes)); return new { path = e.Path, length = bytes.Length, sha256 = Hash(bytes) }; }));
    private static string Digest<T>(T value) => Hash(JsonSerializer.SerializeToUtf8Bytes(value));
    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static OverlayIdentity Identity(Request r) => new(2, r.Nonce, new Tree().Snapshot, 4 * 1024 * 1024, 4096);
    private static void WriteNew<T>(string path, T value) => WriteBytesNew(path, JsonSerializer.SerializeToUtf8Bytes(value, Json));
    private static void WriteBytesNew(string path, byte[] bytes)
    { using (var file = new FileStream(path + ".tmp", FileMode.CreateNew, FileAccess.Write, FileShare.Read | FileShare.Delete)) { file.Write(bytes); file.Flush(true); } File.Move(path + ".tmp", path, false); }
    private sealed class Tree : ISnapshotTree
    {
        public SnapshotReference Snapshot { get; } = new("kernel-recovery-fixture", "fake", "synthetic-source", "synthetic-root", DateTimeOffset.UnixEpoch, true, true, "synthetic-only");
        private readonly byte[] alpha = Alpha(), beta = Beta();
        public void EnsureAvailable() { }
        public SnapshotEntry? Stat(string path) => path switch { "" => new("", true, 0, 16, 1, 1, 1), "alpha" => new(path, false, alpha.Length, 32, 1, 1, 1), "beta" => new(path, false, beta.Length, 32, 1, 1, 1), _ => null };
        public IReadOnlyList<SnapshotEntry> List(string path) => path == "" ? [Stat("alpha")!, Stat("beta")!] : [];
        public int Read(string path, long offset, Span<byte> destination) { byte[] bytes = path == "alpha" ? alpha : beta; int count = (int)Math.Min(destination.Length, Math.Max(0, bytes.Length - offset)); bytes.AsSpan((int)offset, count).CopyTo(destination); return count; }
    }
}
