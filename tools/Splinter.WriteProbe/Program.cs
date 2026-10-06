using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Splinter.Core;
using Splinter.Runtime;

namespace Splinter.WriteProbe;

internal static class Program
{
    private const long Budget = 16 * 1024 * 1024;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private sealed record ExpectedFile(string Path, int Length, string Sha256, long? LastWriteTime);

    private static int Main(string[] args)
    {
        try
        {
            if (args.Length != 3 || args[0] != "run" || args[1] is not ("serial-1024" or "parallel-4x256" or "metadata-1024"))
                throw new ArgumentException("Usage: run <serial-1024|parallel-4x256|metadata-1024> <new-directory>");
            Run(args[1], Path.GetFullPath(args[2]));
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(JsonSerializer.Serialize(new { error = error.GetType().Name, error.Message }, Json));
            return 1;
        }
    }

    private static void Run(string profile, string directory)
    {
        if (Directory.Exists(directory) || File.Exists(directory))
            throw new IOException("Preserve the existing run; a new directory is required.");
        Directory.CreateDirectory(directory);
        var snapshot = new SnapshotReference("write-probe-" + profile, "synthetic-write-probe", "no-live-source",
            "empty-synthetic-root", DateTimeOffset.UnixEpoch, true, true, "synthetic-only");
        var identity = new OverlayIdentity(2, "write-probe-" + profile, snapshot, Budget, 4096);
        var tree = new EmptyTree(snapshot);
        var expected = new List<ExpectedFile>();
        OverlayStorageObservation observed;
        long elapsed, allocated;
        using (var store = new JournalDeltaStore(directory, identity, tree, optimizeJournal: false))
        {
            int workers = profile == "parallel-4x256" ? 4 : 1;
            var entries = Enumerable.Range(0, workers).Select(i => store.Create($"file-{i}", false, 32)).ToArray();
            if (profile == "metadata-1024")
            {
                long allocationStart = GC.GetTotalAllocatedBytes(true), started = Stopwatch.GetTimestamp();
                for (int i = 0; i < 1024; i++) store.SetMetadata(entries[0].Id, 32, 1, 1, 2 + i);
                elapsed = Stopwatch.GetTimestamp() - started;
                allocated = GC.GetTotalAllocatedBytes(true) - allocationStart;
                expected.Add(new(entries[0].Path, 0, Convert.ToHexStringLower(SHA256.HashData([])), 1025));
            }
            else
            {
                int writesPerWorker = 1024 / workers;
                byte[][] references = Enumerable.Range(0, workers).Select(_ => new byte[writesPerWorker * 4096]).ToArray();
                byte[][] payloads = Enumerable.Range(0, workers).Select(_ => new byte[4096]).ToArray();
                void WriteWorker(int worker)
                {
                    for (int i = 0; i < writesPerWorker; i++)
                    {
                        Array.Fill(payloads[worker], (byte)((worker * 31 + i) % 251 + 1));
                        store.Write(entries[worker].Id, i * 4096L, payloads[worker]);
                        payloads[worker].CopyTo(references[worker], i * 4096);
                    }
                }
                long allocationStart = GC.GetTotalAllocatedBytes(true), started = Stopwatch.GetTimestamp();
                if (workers == 1) WriteWorker(0);
                else
                {
                    using var ready = new CountdownEvent(workers);
                    using var start = new ManualResetEventSlim();
                    var tasks = Enumerable.Range(0, workers).Select(worker => Task.Factory.StartNew(() =>
                    {
                        ready.Signal(); start.Wait(); WriteWorker(worker);
                    }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default)).ToArray();
                    ready.Wait(); start.Set(); Task.WaitAll(tasks);
                }
                elapsed = Stopwatch.GetTimestamp() - started;
                allocated = GC.GetTotalAllocatedBytes(true) - allocationStart;
                for (int i = 0; i < workers; i++)
                    expected.Add(new(entries[i].Path, references[i].Length,
                        Convert.ToHexStringLower(SHA256.HashData(references[i])), null));
            }
            store.Flush(); observed = store.Observe();
            if (observed.JournalBytes > Budget || observed.InitialPayloadCopiedBytes != 0 || observed.ReadCacheBytes != 0)
                throw new InvalidDataException("Synthetic write bounds or storage contract failed.");
            Verify(store, expected);
        }

        string journalPath = Path.Combine(directory, "delta.journal"), journalHash = HashFile(journalPath);
        // Use the existing private read-only constructor for both the baseline and instrumented Runtime.
        var constructor = typeof(JournalDeltaStore).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic,
            null, [typeof(string), typeof(OverlayIdentity), typeof(ISnapshotTree), typeof(string), typeof(bool)], null)
            ?? throw new NotSupportedException("Read-only replay constructor unavailable.");
        using (var replayed = (JournalDeltaStore)constructor.Invoke([directory, identity, tree, "delta.journal", true]))
        {
            Verify(replayed, expected);
            var replayMetrics = replayed.Observe().Metrics!;
            if (replayMetrics.CommitCount != 0 || replayMetrics.FlushCount != 0)
                throw new InvalidDataException("Replay wrote to the input.");
        }
        if (HashFile(journalPath) != journalHash) throw new InvalidDataException("Replay changed the journal.");
        string contentHash = Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(expected, Json)));
        using var process = Process.GetCurrentProcess(); process.Refresh();
        Console.WriteLine(JsonSerializer.Serialize(new { profile, expectedFiles = expected, contentHash, journalHash,
            outputVerified = true, replayVerified = true, inputUnchangedByReplay = true,
            elapsedTicks = elapsed, stopwatchFrequency = Stopwatch.Frequency, allocatedManagedBytes = allocated,
            peakWorkingSetBytes = process.PeakWorkingSet64, storage = observed,
            measurement = "fixed synthetic live writes; timer includes caller loops and thread start; one durable flush per acknowledged commit; not Unity or power-loss qualification" }, Json));
    }

    private static void Verify(JournalDeltaStore store, List<ExpectedFile> expected)
    {
        foreach (var file in expected)
        {
            var entry = store.Stat(file.Path) ?? throw new InvalidDataException("Missing expected file.");
            if (entry.Directory || entry.Length != file.Length || file.LastWriteTime is { } time && entry.LastWriteTime != time)
                throw new InvalidDataException("Unexpected file shape or metadata.");
            byte[] bytes = new byte[file.Length];
            if (store.Read(entry.Id, 0, bytes) != bytes.Length || Convert.ToHexStringLower(SHA256.HashData(bytes)) != file.Sha256)
                throw new InvalidDataException("Bytes differ from the independent fixture model.");
        }
        if (store.List("").Count != expected.Count) throw new InvalidDataException("Unexpected namespace.");
    }

    private static string HashFile(string path)
    {
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Convert.ToHexStringLower(SHA256.HashData(file));
    }

    private sealed class EmptyTree(SnapshotReference snapshot) : ISnapshotTree
    {
        public SnapshotReference Snapshot => snapshot;
        public void EnsureAvailable() { }
        public SnapshotEntry? Stat(string path) => path == "" ? new("", true, 0, 16, 1, 1, 1) : null;
        public IReadOnlyList<SnapshotEntry> List(string path) => [];
        public int Read(string path, long offset, Span<byte> destination) => throw new InvalidOperationException("Synthetic writes must not read a base payload.");
    }
}
