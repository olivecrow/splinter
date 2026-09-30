using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Splinter.Core;
using Splinter.Protocol;
using Splinter.Runtime;

namespace Splinter.Tests;

public sealed class JournalInterruptionTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    public static readonly string[] Operations = ["write", "rename", "replace", "truncate", "create", "delete"];
    public static readonly string[] CommitBoundaries = ["commit.before-append", "commit.after-header",
        "commit.after-metadata", "commit.after-payload-prefix", "commit.after-payload", "commit.after-checksum",
        "commit.before-flush", "commit.after-flush", "commit.after-apply", "caller.before-ack", "caller.after-ack"];
    public static TheoryData<string, string> Cases
    {
        get
        {
            var data = new TheoryData<string, string>();
            foreach (string operation in Operations)
                foreach (string boundary in CommitBoundaries)
                    if (operation == "write" || !boundary.Contains("payload", StringComparison.Ordinal)) data.Add(operation, boundary);
            data.Add("flush", "flush.before"); data.Add("flush", "flush.after");
            return data;
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task KilledWriterPreservesAcknowledgedPrefixAndReplaysOnlyWholeFrames(string operation, string boundary)
    {
        using var dir = new TestDirectory();
        await RunTrial(Path.Combine(dir.Path, "trial"), operation, boundary, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task EvidenceRun()
    {
        string? root = Environment.GetEnvironmentVariable("SPLINTER_JOURNAL_RECOVERY_EVIDENCE");
        if (root is null) return;
        root = Path.GetFullPath(root);
        Assert.False(Directory.Exists(root) || File.Exists(root), "Preserve existing evidence; use a new directory.");
        Directory.CreateDirectory(root);
        var manifest = new[] { typeof(JournalDeltaStore).Assembly.Location, typeof(IJournalMutationObserver).Assembly.Location,
            typeof(JournalInterruptionTests).Assembly.Location }.Select(p => new { path = p, sha256 = Hash(File.ReadAllBytes(p)) }).ToArray();
        WriteNew(Path.Combine(root, "candidate.json"), new { version = 1, runtime = Environment.Version.ToString(),
            os = Environment.OSVersion.ToString(), binaries = manifest, fixture = "deterministic-empty-fake-tree; no WinFsp/VSS/Unity",
            policy = "hard child process termination; per-commit durable flush unchanged; no reboot or power-loss evidence" });
        var results = new List<JsonElement>();
        foreach (string operation in Operations.Append("flush"))
            foreach (string boundary in operation == "flush" ? new[] { "flush.before", "flush.after" } : CommitBoundaries)
            {
                if (operation != "write" && boundary.Contains("payload", StringComparison.Ordinal)) continue;
                string directory = Path.Combine(root, operation + "-" + boundary);
                results.Add(await RunTrial(directory, operation, boundary, TestContext.Current.CancellationToken));
            }
        foreach (var binary in manifest) Assert.Equal(binary.sha256, Hash(File.ReadAllBytes(binary.path)));
        WriteNew(Path.Combine(root, "result.json"), new { version = 1, passed = results.Count, failed = 0,
            qualification = "journal-process-boundaries-only", results });
    }

    [Fact]
    public void ChildWriter()
    {
        string? input = Environment.GetEnvironmentVariable("SPLINTER_JOURNAL_RECOVERY_CHILD");
        if (input is null) return;
        var request = JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(input));
        string directory = request.GetProperty("directory").GetString()!, operation = request.GetProperty("operation").GetString()!,
            boundary = request.GetProperty("boundary").GetString()!, nonce = request.GetProperty("nonce").GetString()!;
        var tree = new EmptyTree();
        var observer = new Gate(directory, operation, boundary, nonce);
        using var store = new JournalDeltaStore(Path.Combine(directory, "delta"), Identity(tree), tree, observer);
        var alpha = store.Create("alpha", false, 32); store.Write(alpha.Id, 0, Alpha());
        var beta = store.Create("beta", false, 32); store.Write(beta.Id, 0, Beta());
        WriteNew(Path.Combine(directory, "ack-prefix.json"), new { version = 1, sequence = 4,
            journalBytes = store.Observe().JournalBytes, oracle = Model("before", operation) });
        observer.Armed = true;
        switch (operation)
        {
            case "write": store.Write(alpha.Id, 8192, Pending()); break;
            case "rename": store.Rename(alpha.Id, "gamma", false); break;
            case "replace": store.Rename(alpha.Id, "beta", true); break;
            case "truncate": store.SetLength(alpha.Id, 4096); break;
            case "create": store.Create("gamma", false, 32); break;
            case "delete": store.Delete(alpha.Id); break;
            case "flush": store.Flush(); break;
            default: throw new InvalidOperationException("Unknown synthetic operation.");
        }
        observer.Caller("caller.before-ack", operation == "flush" ? 4 : 5);
        WriteNew(Path.Combine(directory, "ack-final.json"), new { version = 1, sequence = operation == "flush" ? 4 : 5,
            oracle = Model("after", operation) });
        observer.Caller("caller.after-ack", operation == "flush" ? 4 : 5);
        throw new InvalidOperationException("Requested gate was not reached.");
    }

    private static async Task<JsonElement> RunTrial(string directory, string operation, string boundary, CancellationToken ct)
    {
        Assert.False(Directory.Exists(directory) || File.Exists(directory)); Directory.CreateDirectory(directory);
        string nonce = Guid.NewGuid().ToString("N"), input = Path.Combine(directory, "request.json"), signal = Path.Combine(directory, "signal.json");
        WriteNew(input, new { version = 1, directory, operation, boundary, nonce });
        var request = JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(input));
        var catalog = new ContractCatalog();
        Assert.True(catalog.Validate("urn:splinter:v1:journal-recovery-probe-request", request).IsValid);
        var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (string arg in new[] { typeof(JournalInterruptionTests).Assembly.Location, "-method", "Splinter.Tests.JournalInterruptionTests.ChildWriter" }) start.ArgumentList.Add(arg);
        start.Environment.Remove("SPLINTER_JOURNAL_RECOVERY_EVIDENCE");
        start.Environment["SPLINTER_JOURNAL_RECOVERY_CHILD"] = input;
        using var child = Process.Start(start)!;
        var stdout = child.StandardOutput.ReadToEndAsync(ct); var stderr = child.StandardError.ReadToEndAsync(ct);
        long startTicks = child.StartTime.ToUniversalTime().Ticks;
        try
        {
            long started = Stopwatch.GetTimestamp();
            while (!File.Exists(signal) && !child.HasExited && Stopwatch.GetElapsedTime(started) < TimeSpan.FromSeconds(25)) await Task.Delay(20, ct);
            Assert.True(File.Exists(signal), child.HasExited ? await stdout + await stderr : "Child did not reach " + boundary);
            var proof = JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(signal));
            Assert.True(catalog.Validate("urn:splinter:v1:journal-recovery-probe-signal", proof).IsValid);
            Assert.Equal(nonce, proof.GetProperty("nonce").GetString());
            Assert.Equal(operation, proof.GetProperty("operation").GetString()); Assert.Equal(boundary, proof.GetProperty("boundary").GetString());
            Assert.Equal(child.Id, proof.GetProperty("processId").GetInt32());
            Assert.Equal(startTicks, proof.GetProperty("processStartTimeUtcTicks").GetInt64());
            Assert.False(child.HasExited); child.Kill(); await child.WaitForExitAsync(ct);
            WriteNew(Path.Combine(directory, "termination.json"), new { processId = child.Id, processStartTimeUtcTicks = startTicks,
                observedExited = child.HasExited, exitCode = child.ExitCode, gracefulDispose = false });
            Assert.Equal(boundary == "caller.after-ack", File.Exists(Path.Combine(directory, "ack-final.json")));
            var ack = JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(Path.Combine(directory, "ack-prefix.json")));
            Assert.Equal(4, ack.GetProperty("sequence").GetInt32());
            Assert.Equal(Model("before", operation), ack.GetProperty("oracle").GetString());
            string journal = Path.Combine(directory, "delta", "delta.journal");
            int sharingRetries = 0;
            // WaitForExit may precede final Windows handle teardown. Retry only open's sharing violation,
            // never checksum/JSON corruption, access denial, missing files or the replay itself.
            long openStarted = Stopwatch.GetTimestamp(); FileStream original;
            while (true)
            {
                try { original = new FileStream(journal, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete); break; }
                catch (IOException error) when ((error.HResult & 0xffff) is 32 or 33 && Stopwatch.GetElapsedTime(openStarted) < TimeSpan.FromSeconds(5))
                { sharingRetries++; await Task.Delay(20, ct); }
            }
            byte[] before;
            using (original) { using var bytes = new MemoryStream(); original.CopyTo(bytes); before = bytes.ToArray(); }
            long prefix = ack.GetProperty("journalBytes").GetInt64();
            Assert.True(before.Length >= prefix); WriteBytesNew(Path.Combine(directory, "journal-before-replay.bin"), before);
            var tree = new EmptyTree(); string model; OverlayStorageObservation observation;
            using (var resumed = new JournalDeltaStore(Path.Combine(directory, "delta"), Identity(tree), tree))
            {
                observation = resumed.Observe(); model = ReadModel(resumed);
                bool durable = boundary is "commit.after-flush" or "commit.after-apply" or "caller.before-ack" or "caller.after-ack";
                bool incomplete = boundary is "commit.before-append" or "commit.after-header" or "commit.after-metadata" or "commit.after-payload-prefix" or "commit.after-payload";
                if (operation == "flush" || incomplete) Assert.Equal(Model("before", operation), model);
                else if (durable) Assert.Equal(Model("after", operation), model);
                else Assert.True(model == Model("before", operation) || model == Model("after", operation), "Unacknowledged full frame may survive, but a mixed transaction may not.");
                Assert.Equal(model == Model("before", operation) ? 4 : 5, observation.DurableSequence);
                Assert.Equal(0, observation.InitialPayloadCopiedBytes); Assert.Equal(0, observation.ReadCacheBytes);
                Assert.Equal(0, observation.Metrics!.BaseReadBytes);
            }
            byte[] after = File.ReadAllBytes(journal); Assert.True(after.Length <= before.Length);
            Assert.Equal(before[..checked((int)prefix)], after[..checked((int)prefix)]);
            Assert.Equal(before[..after.Length], after); // Replay may truncate only the incomplete trailing frame.
            using (var second = new JournalDeltaStore(Path.Combine(directory, "delta"), Identity(tree), tree)) Assert.Equal(model, ReadModel(second));
            Assert.Equal(after, File.ReadAllBytes(journal));
            var result = JsonSerializer.SerializeToElement(new { version = 1, operation, boundary, nonce,
                actualProcessTermination = true, acknowledgedPrefixSequence = 4, externalFinalAck = boundary == "caller.after-ack",
                replaySequence = observation.DurableSequence, modelHash = model, beforeHash = Hash(before), afterHash = Hash(after),
                beforeBytes = before.Length, afterBytes = after.Length, truncatedTailBytes = before.Length - after.Length,
                initialPayloadCopiedBytes = observation.InitialPayloadCopiedBytes, readCacheBytes = observation.ReadCacheBytes,
                baseReadBytes = observation.Metrics!.BaseReadBytes, postExitSharingRetries = sharingRetries, secondReplayUnchanged = true }, Json);
            Assert.True(catalog.Validate("urn:splinter:v1:journal-recovery-probe-result", result).IsValid);
            WriteNew(Path.Combine(directory, "result.json"), result); return result;
        }
        finally
        {
            if (!child.HasExited) { child.Kill(); await child.WaitForExitAsync(CancellationToken.None); }
            WriteBytesNew(Path.Combine(directory, "child.stdout.txt"), System.Text.Encoding.UTF8.GetBytes(await stdout));
            WriteBytesNew(Path.Combine(directory, "child.stderr.txt"), System.Text.Encoding.UTF8.GetBytes(await stderr));
        }
    }

    private sealed class Gate(string directory, string operation, string target, string nonce) : IJournalMutationObserver
    {
        public bool Armed { get; set; }
        public void Observe(JournalMutationBoundary boundary)
        {
            if (!Armed || boundary.Boundary != target) return;
            Signal(boundary.Sequence, boundary.FrameStart, boundary.FrameBytes, boundary.PayloadBytes);
        }
        public void Caller(string boundary, long sequence) { if (target == boundary) Signal(sequence, 0, 0, 0); }
        private void Signal(long sequence, long frameStart, long frameBytes, int payloadBytes)
        {
            using var self = Process.GetCurrentProcess();
            WriteNew(Path.Combine(directory, "signal.json"), new { version = 1, operation, boundary = target, nonce,
                processId = self.Id, processStartTimeUtcTicks = self.StartTime.ToUniversalTime().Ticks,
                sequence, frameStart, frameBytes, payloadBytes });
            // No store access here: Length/Position/Dispose could flush FileStream's buffer.
            if (!new ManualResetEventSlim().Wait(TimeSpan.FromMinutes(1))) throw new TimeoutException("Unclaimed probe gate; evidence remains intact.");
        }
    }

    private static byte[] Alpha() => Enumerable.Range(0, 65536).Select(i => (byte)(i % 251)).ToArray();
    private static byte[] Beta() => Enumerable.Repeat((byte)0x5a, 4096).ToArray();
    private static byte[] Pending() => Enumerable.Range(0, 65536).Select(i => (byte)(250 - i % 251)).ToArray();
    private static string Model(string stage, string operation)
    {
        var files = new SortedDictionary<string, byte[]>(StringComparer.Ordinal) { ["alpha"] = Alpha(), ["beta"] = Beta() };
        if (stage == "after")
            switch (operation)
            {
                case "write": var bytes = new byte[73728]; files["alpha"].CopyTo(bytes, 0); Pending().CopyTo(bytes, 8192); files["alpha"] = bytes; break;
                case "rename": files["gamma"] = files["alpha"]; files.Remove("alpha"); break;
                case "replace": files["beta"] = files["alpha"]; files.Remove("alpha"); break;
                case "truncate": files["alpha"] = files["alpha"][..4096]; break;
                case "create": files["gamma"] = []; break;
                case "delete": files.Remove("alpha"); break;
            }
        return Digest(files.Select(f => new { path = f.Key, length = f.Value.Length, sha256 = Hash(f.Value) }));
    }
    private static string ReadModel(JournalDeltaStore store) => Digest(store.List("").OrderBy(e => e.Path, StringComparer.Ordinal).Select(e =>
    {
        Assert.False(e.Directory); byte[] bytes = new byte[e.Length]; Assert.Equal(bytes.Length, store.Read(e.Id, 0, bytes));
        return new { path = e.Path, length = bytes.Length, sha256 = Hash(bytes) };
    }));
    private static string Digest<T>(T value) => Hash(JsonSerializer.SerializeToUtf8Bytes(value));
    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static OverlayIdentity Identity(EmptyTree tree) => new(1, "journal-recovery-probe", tree.Snapshot, 4 * 1024 * 1024, 65536);
    private static void WriteNew<T>(string path, T value) => WriteBytesNew(path, JsonSerializer.SerializeToUtf8Bytes(value, Json));
    private static void WriteBytesNew(string path, byte[] value)
    {
        string temporary = path + ".tmp";
        using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.Read | FileShare.Delete))
        { stream.Write(value); stream.Flush(true); }
        File.Move(temporary, path, overwrite: false);
    }
    private sealed class EmptyTree : ISnapshotTree
    {
        public SnapshotReference Snapshot { get; } = new("journal-probe", "fake", "no-live-source", "synthetic-root", DateTimeOffset.UnixEpoch, true, true, "synthetic-only");
        public void EnsureAvailable() { }
        public SnapshotEntry? Stat(string path) => path == "" ? new("", true, 0, 16, 1, 1, 1) : null;
        public IReadOnlyList<SnapshotEntry> List(string path) => [];
        public int Read(string path, long offset, Span<byte> destination) => throw new InvalidOperationException("No base payload is read.");
    }
}
