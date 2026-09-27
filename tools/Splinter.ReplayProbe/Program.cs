using System.Buffers.Binary;
using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Splinter.Core;
using Splinter.Runtime;

namespace Splinter.ReplayProbe;

// Development-only synthetic replay evidence. No VSS, mount, Unity or live source.
internal static class Program
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private const long Budget = 128 * 1024 * 1024;
    private sealed record ExpectedFile(string Path, int Length, string Sha256);
    private sealed record Receipt(int Version, string Profile, OverlayIdentity Identity, long Frames,
        string JournalSha256, ExpectedFile[] Files, string[] Absent, double GenerationSeconds);
    private sealed record Mutation(long Sequence, string Operation, DeltaEntry Entry, long Offset = 0,
        string? Destination = null, bool Replace = false);

    public static int Main(string[] args)
    {
        try
        {
            if (args.Length == 3 && args[0] == "generate") Generate(args[1], Path.GetFullPath(args[2]));
            else if (args.Length == 2 && args[0] == "run") Run(Path.GetFullPath(args[1]));
            else throw new ArgumentException("Usage: generate <append|overlap|file-renames> <new-directory> | run <sealed-synthetic-directory>");
            return 0;
        }
        catch (Exception error)
        {
            var cause = error is TargetInvocationException { InnerException: { } inner } ? inner : error;
            Console.Error.WriteLine(JsonSerializer.Serialize(new { status = "failed", error = cause.GetType().Name, cause.Message }, Json));
            return 1;
        }
    }

    private static SnapshotReference Snapshot(string profile) => new("probe-" + profile, "synthetic-replay-probe",
        "no-live-source", "empty-synthetic-root", DateTimeOffset.UnixEpoch, true, true, "synthetic-only");

    private static void Generate(string profile, string directory)
    {
        if (profile is not ("append" or "overlap" or "file-renames")) throw new ArgumentException("Unknown bounded profile.");
        if (Directory.Exists(directory) || File.Exists(directory)) throw new IOException("Preserve the existing input; use a new directory.");
        long started = Stopwatch.GetTimestamp();
        Directory.CreateDirectory(directory);
        var identity = new OverlayIdentity(1, "probe-" + profile, Snapshot(profile), Budget, 1024 * 1024);
        // Runtime identity uses its default (PascalCase) serialization contract.
        using (var file = new FileStream(Path.Combine(directory, "identity.json"), FileMode.CreateNew, FileAccess.Write, FileShare.None))
        { JsonSerializer.Serialize(file, identity); file.Flush(true); }
        string journalPath = Path.Combine(directory, "delta.journal");
        var expected = new List<ExpectedFile>(); var absent = new List<string>();
        long sequence = 0; int nextId = 0;
        using (var journal = new FileStream(journalPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536))
        {
            void Append(string operation, DeltaEntry entry, ReadOnlySpan<byte> payload, long offset = 0, string? destination = null)
            {
                byte[] metadata = JsonSerializer.SerializeToUtf8Bytes(new Mutation(++sequence, operation, entry, offset, destination));
                if (journal.Position + 40L + metadata.Length + payload.Length > Budget) throw new IOException("Synthetic input budget exceeded.");
                Span<byte> header = stackalloc byte[8];
                BinaryPrimitives.WriteInt32LittleEndian(header, metadata.Length);
                BinaryPrimitives.WriteInt32LittleEndian(header[4..], payload.Length);
                using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                hash.AppendData(header); hash.AppendData(metadata); hash.AppendData(payload);
                journal.Write(header); journal.Write(metadata); journal.Write(payload); journal.Write(hash.GetHashAndReset());
            }
            DeltaEntry Create(string path, bool isDirectory = false)
            {
                var entry = new DeltaEntry(new Guid(++nextId, 0, 0, new byte[8]), path, isDirectory, 0,
                    isDirectory ? 16u : 32u, 1, 1, 1, null, 0);
                Append("create", entry, []); return entry;
            }
            Create("Library", true);
            if (profile == "file-renames")
            {
                var entries = Enumerable.Range(0, 35000).Select(i => Create($"Library/file-{i:D5}")).ToArray();
                for (int i = 0; i < 4032; i++)
                {
                    string destination = $"Library/renamed-{i:D5}";
                    Append("rename", entries[i], [], destination: destination); absent.Add(entries[i].Path);
                    entries[i] = entries[i] with { Path = destination };
                }
                string emptyHash = Convert.ToHexStringLower(SHA256.HashData([]));
                expected.AddRange(entries.Select(e => new ExpectedFile(e.Path, 0, emptyHash)));
            }
            else
            {
                var entry = Create("Library/ranges");
                int blocks = profile == "append" ? 8192 : 2048;
                byte[] reference = new byte[blocks * 4096], payload = new byte[4096];
                for (int i = 0; i < blocks; i++)
                {
                    Array.Fill(payload, (byte)(i % 251 + 1));
                    entry = entry with { Length = (i + 1L) * 4096 };
                    Append("write", entry, payload, i * 4096L); payload.CopyTo(reference, i * 4096);
                }
                if (profile == "overlap")
                {
                    payload = new byte[1024];
                    for (int i = 0; i < 6144; i++)
                    {
                        int offset = i * 1543 % (reference.Length - payload.Length);
                        Array.Fill(payload, (byte)(i % 239 + 7));
                        Append("write", entry, payload, offset); payload.CopyTo(reference, offset);
                    }
                    entry = entry with { Length = reference.Length - 777 };
                    Append("resize", entry, []); Array.Clear(reference, reference.Length - 777, 777);
                    entry = entry with { Length = reference.Length };
                    Append("resize", entry, []);
                }
                expected.Add(new(entry.Path, reference.Length, Convert.ToHexStringLower(SHA256.HashData(reference))));
            }
            // This is fixture construction, not the acknowledged product write path.
            journal.Flush(true);
        }
        var receipt = new Receipt(1, profile, identity, sequence, HashFile(journalPath), expected.ToArray(), absent.ToArray(),
            Stopwatch.GetElapsedTime(started).TotalSeconds);
        string pending = Path.Combine(directory, "probe.json.new");
        using (var file = new FileStream(pending, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        { JsonSerializer.Serialize(file, receipt, Json); file.Flush(true); }
        File.Move(pending, Path.Combine(directory, "probe.json"));
        Console.WriteLine(JsonSerializer.Serialize(new { profile, receipt.Frames, receipt.GenerationSeconds,
            journalBytes = new FileInfo(journalPath).Length, receipt.JournalSha256 }, Json));
    }

    private static void Run(string directory)
    {
        var receipt = JsonSerializer.Deserialize<Receipt>(File.ReadAllText(Path.Combine(directory, "probe.json")), Json)
            ?? throw new InvalidDataException("Synthetic receipt required.");
        if (receipt.Version != 1 || receipt.Profile is not ("append" or "overlap" or "file-renames") ||
            receipt.Identity != new OverlayIdentity(1, "probe-" + receipt.Profile, Snapshot(receipt.Profile), Budget, 1024 * 1024))
            throw new InvalidDataException("Only this tool's synthetic identities are accepted.");
        string journalPath = Path.Combine(directory, "delta.journal");
        if (new FileInfo(journalPath).Length > Budget || HashFile(journalPath) != receipt.JournalSha256)
            throw new InvalidDataException("Preserve the changed input.");
        var tree = new EmptyTree(receipt.Identity.Snapshot);
        // The fixed read-only constructor also exists in d48b3b0. Reflection keeps
        // this development probe compatible with that baseline without adding a
        // product API or risking incomplete-tail repair of its input.
        var constructor = typeof(JournalDeltaStore).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic,
            null, [typeof(string), typeof(OverlayIdentity), typeof(ISnapshotTree), typeof(string), typeof(bool)], null)
            ?? throw new NotSupportedException("This runtime has no supported read-only replay constructor.");
        long allocatedBefore = GC.GetTotalAllocatedBytes(true), started = Stopwatch.GetTimestamp();
        using var store = (JournalDeltaStore)constructor.Invoke([directory, receipt.Identity, tree, "delta.journal", true]);
        long elapsed = Stopwatch.GetTimestamp() - started, allocated = GC.GetTotalAllocatedBytes(true) - allocatedBefore;
        long managedBytes = GC.GetTotalMemory(false);
        using var process = Process.GetCurrentProcess(); process.Refresh();
        long peakWorkingSetBytes = process.PeakWorkingSet64;
        var observed = store.Observe();
        if (observed.DurableSequence != receipt.Frames || observed.Metrics!.ReplayFrames != receipt.Frames ||
            observed.Metrics.CommitCount != 0 || observed.Metrics.FlushCount != 0 || observed.Metrics.BaseReadBytes != 0)
            throw new InvalidDataException("Unexpected replay or write activity.");
        foreach (var file in receipt.Files)
        {
            var entry = store.Stat(file.Path) ?? throw new InvalidDataException("Missing expected path.");
            if (entry.Directory || entry.Length != file.Length) throw new InvalidDataException("Wrong file shape.");
            byte[] data = new byte[file.Length];
            if (store.Read(entry.Id, 0, data) != data.Length || Convert.ToHexStringLower(SHA256.HashData(data)) != file.Sha256)
                throw new InvalidDataException("Replayed bytes differ from the independent fixture model.");
        }
        if (store.List("Library").Count != receipt.Files.Length || receipt.Absent.Any(p => store.Stat(p) is not null))
            throw new InvalidDataException("Replayed namespace differs.");
        string stateHash = (string)(typeof(JournalDeltaStore).GetMethod("StateHash", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.Invoke(store, null) ?? throw new NotSupportedException("State hash unavailable."));
        store.Dispose();
        if (HashFile(journalPath) != receipt.JournalSha256) throw new InvalidDataException("Input changed during observation.");
        Console.WriteLine(JsonSerializer.Serialize(new { receipt.Profile, journalHash = receipt.JournalSha256,
            stateHash, outputVerified = true, inputUnchanged = true, elapsedTicks = elapsed, stopwatchFrequency = Stopwatch.Frequency,
            allocatedManagedBytes = allocated, managedHeapBytes = managedBytes, peakWorkingSetBytes, storage = observed,
            cachePolicy = "full input hash before and after; OS cache not purged; timing excludes output verification" }, Json));
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
        public int Read(string path, long offset, Span<byte> destination) => throw new InvalidOperationException("Synthetic replay must not read a base payload.");
    }
}
