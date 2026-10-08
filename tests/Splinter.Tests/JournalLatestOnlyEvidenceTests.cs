using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Splinter.Core;
using Splinter.Runtime;
using Splinter.SourceControl.Git;

namespace Splinter.Tests;

// Opt-in real committed PNG inputs, direct Runtime on NTFS. No source edit,
// Unity, service, mount, installation, or earlier A/B lifecycle is repeated.
public sealed class JournalLatestOnlyEvidenceTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    public sealed record Profile(string Name, int Files, int Ranges, int Cycles);
    public sealed record Input(string Source, string Revision, string Owner, string Receipt, string Output, string[] Files, Profile[] Profiles);
    private sealed record Range(long Offset, byte[] B, byte[] C);
    private sealed record FileInput(string Path, Range[] Ranges, string Hash);
    private sealed class Stages : IJournalMutationObserver
    {
        public Dictionary<string, long> Ticks { get; } = [];
        private string previous = "write-start";
        private long began;
        private bool active;
        public void Start() { active = true; previous = "write-start"; began = Stopwatch.GetTimestamp(); }
        public void Observe(JournalMutationBoundary boundary)
        {
            if (!active) return;
            long now = Stopwatch.GetTimestamp();
            if (began != 0) Ticks[previous] = Ticks.GetValueOrDefault(previous) + now - began;
            began = now; previous = boundary.Boundary;
        }
        public void End()
        { Ticks[previous] = Ticks.GetValueOrDefault(previous) + Stopwatch.GetTimestamp() - began; began = 0; active = false; }
    }
    private sealed class Reader : IDisposable
    {
        private readonly FileStream lease;
        public GitObjectSnapshotTree Tree { get; }
        public Reader(GitObjectSnapshotProvider provider, SnapshotReference snapshot)
        {
            lease = provider.AcquireReadLease();
            var observed = provider.ObserveAsync(snapshot).GetAwaiter().GetResult(); Assert.True(observed.Available);
            Tree = provider.OpenTree(snapshot, observed.ReadAccess!, () => observed);
        }
        public void Dispose() { Tree.Dispose(); lease.Dispose(); }
    }
    private static FileInput Seed(GitObjectSnapshotTree tree, string path, Profile profile)
    {
        long length = tree.Stat(path)!.Length;
        var ranges = Enumerable.Range(0, profile.Ranges).Select(r => new Range(65536 + r * 262144,
            new byte[131072], new byte[131072])).ToArray();
        byte[] scratch = new byte[65536]; using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        for (long at = 0; at < length;)
        {
            int count = (int)Math.Min(scratch.Length, length - at); Assert.Equal(count, tree.Read(path, at, scratch.AsSpan(0, count)));
            foreach (var range in ranges)
            {
                long begin = Math.Max(at, range.Offset), end = Math.Min(at + count, range.Offset + range.B.Length);
                for (long i = begin; i < end; i++)
                {
                    byte original = scratch[i - at]; int local = (int)(i - range.Offset);
                    range.B[local] = (byte)(original ^ 0x55); range.C[local] = (byte)(original ^ 0xAA); scratch[i - at] = range.C[local];
                }
            }
            hash.AppendData(scratch.AsSpan(0, count)); at += count;
        }
        return new(path, ranges, Convert.ToHexStringLower(hash.GetHashAndReset()));
    }
    [Fact]
    public async Task RecoverPreservedMeasurement()
    {
        string? config = Environment.GetEnvironmentVariable("SPLINTER_LATEST_ONLY_RECOVER_EVIDENCE");
        if (config is null) Assert.Skip("Explicit measurement recovery requires SPLINTER_LATEST_ONLY_RECOVER_EVIDENCE.");
        var input = JsonSerializer.Deserialize<Input>(File.ReadAllText(config), Json)!;
        var provider = new GitObjectSnapshotProvider(input.Receipt, input.Owner, input.Revision);
        var recovered = new List<object>(); SnapshotReference? snapshot = null;
        foreach (string directory in Directory.EnumerateDirectories(input.Output))
        {
            string identityPath = Path.Combine(directory, "identity.json");
            var identity = JsonSerializer.Deserialize<OverlayIdentity>(File.ReadAllText(identityPath))!;
            Assert.Equal(input.Owner, identity.Id); snapshot ??= identity.Snapshot; Assert.Equal(snapshot, identity.Snapshot);
            using var reader = new Reader(provider, identity.Snapshot);
            using var writer = new JournalDeltaStore(directory, identity, reader.Tree);
            recovered.Add(new { directory, seal = writer.Seal(), storage = writer.Observe() });
        }
        File.WriteAllText(Path.Combine(input.Output, "recovery.json"), JsonSerializer.Serialize(recovered, Json));
        Assert.NotNull(snapshot);
        var release = await provider.ReleaseAsync(snapshot, TestContext.Current.CancellationToken);
        File.WriteAllText(Path.Combine(input.Output, "basis-release.json"), JsonSerializer.Serialize(release, Json));
    }
    [Fact]
    public async Task Benchmark()
    {
        string? config = Environment.GetEnvironmentVariable("SPLINTER_LATEST_ONLY_MANY_FILES");
        if (config is null) Assert.Skip("Explicit benchmark requires SPLINTER_LATEST_ONLY_MANY_FILES.");
        var input = JsonSerializer.Deserialize<Input>(File.ReadAllText(config), Json)!;
        Assert.False(Directory.Exists(input.Output)); Directory.CreateDirectory(input.Output);
        var ct = TestContext.Current.CancellationToken;
        var provider = new GitObjectSnapshotProvider(input.Receipt, input.Owner, input.Revision);
        var snapshot = await provider.CaptureAsync(new(input.Source, input.Owner, input.Receipt), ct);
        var identity = new OverlayIdentity(2, input.Owner, snapshot, 2147483648, 1048576);
        var results = new List<object>();
        foreach (var profile in input.Profiles)
        {
            Assert.InRange(profile.Files, 1, 32); Assert.InRange(profile.Ranges, 1, 3); Assert.InRange(profile.Cycles, 1, 12);
            FileInput[] files;
            using (var reader = new Reader(provider, snapshot)) files = input.Files.Take(profile.Files).Select(path => Seed(reader.Tree, path, profile)).ToArray();
            for (int repeat = 0; repeat < 3; repeat++)
            foreach (bool enabled in repeat == 1 ? new[] { true, false } : new[] { false, true })
            {
                string directory = Path.Combine(input.Output, $"{profile.Name}-{repeat}-{enabled}");
                using (var reader = new Reader(provider, snapshot))
                {
                    using var empty = new JournalDeltaStore(directory, identity, reader.Tree, optimizeJournal: enabled);
                    Assert.Equal(enabled, empty.JournalOptimizationEnabled);
                }
                var entries = new Dictionary<string, DeltaEntry>(); SourceChangeView changes; object? writeMetrics = null;
                Stages? stages = Environment.GetEnvironmentVariable("SPLINTER_LATEST_ONLY_PROFILE") is null ? null : new();
                var samples = new List<object>(); var latencies = new List<long>(); long writeTicks = 0, writeAllocated = 0;
                using (var reader = new Reader(provider, snapshot))
                using (var store = stages is null ? new JournalDeltaStore(directory, identity, reader.Tree) : new JournalDeltaStore(directory, identity, reader.Tree, stages))
                {
                    foreach (var file in files) entries[file.Path] = store.Stat(file.Path)!;
                    for (int cycle = 0; cycle < profile.Cycles; cycle++)
                    {
                        long allocated = GC.GetTotalAllocatedBytes(true), began = Stopwatch.GetTimestamp();
                        for (int variant = 0; variant < 2; variant++)
                        for (int r = 0; r < profile.Ranges; r++)
                        foreach (var file in files)
                        {
                            var range = file.Ranges[r]; long at = Stopwatch.GetTimestamp();
                            stages?.Start();
                            store.Write(entries[file.Path].Id, range.Offset, variant == 0 ? range.B : range.C);
                            stages?.End();
                            latencies.Add(Stopwatch.GetTimestamp() - at);
                        }
                        writeTicks += Stopwatch.GetTimestamp() - began; writeAllocated += GC.GetTotalAllocatedBytes(true) - allocated;
                        var storage = store.Observe(); string journal = Path.Combine(directory, "delta.journal");
                        long allocation = JournalDeltaStore.JournalStorageFiles(journal).Sum(JournalBaseReversionEvidenceTests.Allocation);
                        samples.Add(new { cycle = cycle + 1, writes = latencies.Count, storage.JournalBytes, storage.PayloadBytes, allocatedBytes = allocation,
                            storageFileCount = JournalDeltaStore.JournalStorageFiles(journal).Count, cumulativeWriteTicks = writeTicks });
                        if (enabled) Assert.Equal(profile.Files * profile.Ranges * 131072L, storage.PayloadBytes);
                    }
                    writeMetrics = store.Observe().Metrics;
                    foreach (var file in files)
                    { store.SetMetadata(entries[file.Path].Id, 32, 101, 202, 303); entries[file.Path] = store.Get(entries[file.Path].Id); }
                    changes = store.ObserveChanges([]);
                }
                long replayBegan = Stopwatch.GetTimestamp(); SnapshotDeltaSeal seal;
                using (var reader = new Reader(provider, snapshot))
                using (var replay = new JournalDeltaStore(directory, identity, reader.Tree)) { seal = replay.Seal(); }
                long replayTicks = Stopwatch.GetTimestamp() - replayBegan;
                using (var reader = new Reader(provider, snapshot))
                using (var replay = new JournalDeltaStore(directory, identity, reader.Tree))
                {
                    Assert.Equal(changes.Changes, replay.ObserveChanges([]).Changes);
                    byte[] scratch = new byte[65536];
                    foreach (var file in files)
                    {
                        Assert.Equal(entries[file.Path], replay.Stat(file.Path)); using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                        for (long at = 0; at < entries[file.Path].Length;)
                        {
                            int count = (int)Math.Min(scratch.Length, entries[file.Path].Length - at);
                            Assert.Equal(count, replay.Read(entries[file.Path].Id, at, scratch.AsSpan(0, count)));
                            hash.AppendData(scratch.AsSpan(0, count)); at += count;
                        }
                        Assert.Equal(file.Hash, Convert.ToHexStringLower(hash.GetHashAndReset()));
                    }
                }
                latencies.Sort(); results.Add(new { profile, repeat, enabled, stopwatchFrequency = Stopwatch.Frequency, writeTicks, replayTicks, writeAllocated,
                    writeP50 = latencies[latencies.Count / 2], writeP95 = latencies[(int)(latencies.Count * .95)], writes = latencies.Count, writeMetrics, writeStageTicks = stages?.Ticks, samples, seal,
                    verifiedHashes = files.ToDictionary(f => f.Path, f => f.Hash) });
                File.WriteAllText(Path.Combine(input.Output, "result.json"), JsonSerializer.Serialize(new
                { qualification = "fixed-real-PNG-Git-blobs/actual-NTFS-current-journal; no Editor or WinFsp lifecycle", osCache = "not-evicted; warm/cold unknown", results }, Json));
            }
        }
        var release = await provider.ReleaseAsync(snapshot, ct);
        File.WriteAllText(Path.Combine(input.Output, "basis-release.json"), JsonSerializer.Serialize(release, Json));
    }
}
