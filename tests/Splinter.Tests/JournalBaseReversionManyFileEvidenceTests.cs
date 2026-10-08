using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Splinter.Core;
using Splinter.Runtime;
using Splinter.SourceControl.Git;

namespace Splinter.Tests;

// Explicit offline evidence: fixed real asset blobs and independent NTFS journals.
// No Editor, WinFsp mount, source file write, service, or global configuration.
public sealed class JournalBaseReversionManyFileEvidenceTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    public sealed record Profile(string Name, int Files, int Ranges, int Cycles, bool Mixed);
    public sealed record Input(string Source, string Revision, string Owner, string Receipt, string Output,
        string Variant, string[] Files, Profile[] Profiles);
    private sealed record Range(long Offset, byte[] Original, byte[] B, byte[] C, byte[] Final);
    private sealed record FileInput(string Path, Range[] Ranges, string Hash);
    private const int RangeBytes = 131072;

    private sealed class Reader : IDisposable
    {
        private readonly FileStream lease;
        public GitObjectSnapshotTree Tree { get; }
        public Reader(GitObjectSnapshotProvider provider, SnapshotReference snapshot)
        {
            lease = provider.AcquireReadLease();
            try
            {
                var observed = provider.ObserveAsync(snapshot).GetAwaiter().GetResult();
                Assert.True(observed.Available);
                Tree = provider.OpenTree(snapshot, observed.ReadAccess!, () => observed);
            }
            catch { lease.Dispose(); throw; }
        }
        public void Dispose() { Tree.Dispose(); lease.Dispose(); }
    }

    private static FileInput Seed(GitObjectSnapshotTree tree, string path, Profile profile, int index)
    {
        long length = tree.Stat(path)!.Length;
        Assert.InRange(length, 1048576, 16777216);
        var ranges = new Range[profile.Ranges];
        for (int r = 0; r < ranges.Length; r++)
        {
            long offset = 65536 + r * 262144;
            byte[] original = new byte[RangeBytes];
            ranges[r] = new(offset, original, new byte[RangeBytes], new byte[RangeBytes], new byte[RangeBytes]);
        }
        byte[] scratch = new byte[65536];
        using var expected = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        for (long at = 0; at < length;)
        {
            int count = (int)Math.Min(scratch.Length, length - at);
            Assert.Equal(count, tree.Read(path, at, scratch.AsSpan(0, count)));
            foreach (var range in ranges)
            {
                long begin = Math.Max(at, range.Offset), end = Math.Min(at + count, range.Offset + RangeBytes);
                if (end <= begin) continue;
                int local = (int)(begin - at), offset = (int)(begin - range.Offset), size = (int)(end - begin);
                scratch.AsSpan(local, size).CopyTo(range.Original.AsSpan(offset));
                for (int i = offset; i < offset + size; i++)
                {
                    range.B[i] = (byte)(range.Original[i] ^ 0x55);
                    range.C[i] = (byte)(range.Original[i] ^ 0xAA);
                    range.Final[i] = profile.Mixed && index % 3 == 0 ||
                        profile.Mixed && index % 3 == 1 && i < RangeBytes / 2 ? range.Original[i] : range.C[i];
                }
                range.Final.AsSpan(offset, size).CopyTo(scratch.AsSpan(local));
            }
            expected.AppendData(scratch.AsSpan(0, count)); at += count;
        }
        return new(path, ranges, Convert.ToHexStringLower(expected.GetHashAndReset()));
    }

    [Fact]
    public async Task Benchmark()
    {
        string? config = Environment.GetEnvironmentVariable("SPLINTER_BASE_REVERSION_MANY_FILES");
        if (config is null) Assert.Skip("Explicit benchmark requires SPLINTER_BASE_REVERSION_MANY_FILES.");
        var ct = TestContext.Current.CancellationToken;
        var input = JsonSerializer.Deserialize<Input>(File.ReadAllText(config), Json)!;
        Assert.Contains(input.Variant, new[] { "before", "after" });
        Assert.False(Directory.Exists(input.Output)); Directory.CreateDirectory(input.Output);
        var provider = new GitObjectSnapshotProvider(input.Receipt, input.Owner, input.Revision);
        var snapshot = await provider.CaptureAsync(new(input.Source, input.Owner, input.Receipt), ct);
        var identity = new OverlayIdentity(2, input.Owner, snapshot, 2147483648, 1048576);
        var results = new List<object>();
        foreach (var profile in input.Profiles)
        {
            Assert.InRange(profile.Files, 1, 32); Assert.InRange(profile.Ranges, 1, 3); Assert.InRange(profile.Cycles, 1, 12);
            FileInput[] files;
            using (var reader = new Reader(provider, snapshot))
                files = input.Files.Take(profile.Files).Select((path, index) => Seed(reader.Tree, path, profile, index)).ToArray();
            for (int repeat = 0; repeat < 3; repeat++)
            foreach (bool enabled in repeat == 1 ? new[] { true, false } : new[] { false, true })
            {
                ct.ThrowIfCancellationRequested();
                string directory = Path.Combine(input.Output, $"{profile.Name}-{repeat}-{enabled}");
                var entries = new Dictionary<string, DeltaEntry>(); SourceChangeView changes;
                var latencies = new List<long>(); long began, allocated, writeTicks, writeAllocated;
                using (var reader = new Reader(provider, snapshot))
                using (var store = new JournalDeltaStore(directory, identity, reader.Tree, optimizeJournal: false))
                {
                    foreach (var file in files) entries[file.Path] = store.Stat(file.Path)!;
                    allocated = GC.GetTotalAllocatedBytes(true); began = Stopwatch.GetTimestamp();
                    // Interleaved files and distant ranges model repeated saves across an editing session.
                    for (int cycle = 0; cycle < profile.Cycles; cycle++)
                    for (int variant = 0; variant < 3; variant++)
                    for (int r = 0; r < profile.Ranges; r++)
                    foreach (var file in files)
                    {
                        var range = file.Ranges[r];
                        byte[] bytes = variant == 0 ? range.B : variant == 1 ? range.C : range.Original;
                        long at = Stopwatch.GetTimestamp(); store.Write(entries[file.Path].Id, range.Offset, bytes);
                        latencies.Add(Stopwatch.GetTimestamp() - at);
                    }
                    for (int r = 0; r < profile.Ranges; r++)
                    foreach (var file in files)
                    {
                        var range = file.Ranges[r]; long at = Stopwatch.GetTimestamp();
                        store.Write(entries[file.Path].Id, range.Offset, range.Final);
                        latencies.Add(Stopwatch.GetTimestamp() - at);
                    }
                    foreach (var file in files)
                    { store.SetMetadata(entries[file.Path].Id, 32, 101, 202, 303); entries[file.Path] = store.Get(entries[file.Path].Id); }
                    changes = store.ObserveChanges([]);
                    writeTicks = Stopwatch.GetTimestamp() - began; writeAllocated = GC.GetTotalAllocatedBytes(true) - allocated;
                }
                string journal = Path.Combine(directory, "delta.journal");
                long beforeAllocation = JournalBaseReversionEvidenceTests.Allocation(journal);
                allocated = GC.GetTotalAllocatedBytes(true); began = Stopwatch.GetTimestamp();
                SnapshotJournalCompaction receipt; long decodedBytes;
                using (var reader = new Reader(provider, snapshot))
                {
                    receipt = JournalDeltaStore.CompactDirectory(directory, identity, reader.Tree, revertBaseRanges: enabled);
                    decodedBytes = reader.Tree.DecodedBlobBytes;
                }
                long compactTicks = Stopwatch.GetTimestamp() - began, compactAllocated = GC.GetTotalAllocatedBytes(true) - allocated;
                long afterAllocation = JournalBaseReversionEvidenceTests.Allocation(journal);
                began = Stopwatch.GetTimestamp(); JournalPrivateUsage usage; SnapshotDeltaSeal seal;
                using (var reader = new Reader(provider, snapshot))
                using (var replay = new JournalDeltaStore(directory, identity, reader.Tree, optimizeJournal: false))
                { usage = replay.ObservePrivateUsage(); seal = replay.Seal(); }
                long replayTicks = Stopwatch.GetTimestamp() - began;
                // Full-file streaming hashes and metadata/sequence comparison are outside measured replay.
                using (var reader = new Reader(provider, snapshot))
                using (var replay = new JournalDeltaStore(directory, identity, reader.Tree, optimizeJournal: false))
                {
                    Assert.Equal(changes.Sequence, replay.ObserveChanges([]).Sequence);
                    Assert.Equal(changes.Changes, replay.ObserveChanges([]).Changes);
                    byte[] scratch = new byte[65536];
                    foreach (var file in files)
                    {
                        Assert.Equal(entries[file.Path], replay.Stat(file.Path));
                        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                        for (long at = 0; at < entries[file.Path].Length;)
                        {
                            int count = (int)Math.Min(scratch.Length, entries[file.Path].Length - at);
                            Assert.Equal(count, replay.Read(entries[file.Path].Id, at, scratch.AsSpan(0, count)));
                            hash.AppendData(scratch.AsSpan(0, count)); at += count;
                        }
                        Assert.Equal(file.Hash, Convert.ToHexStringLower(hash.GetHashAndReset()));
                    }
                }
                JournalDeltaStore.CompleteCompaction(directory, identity);
                latencies.Sort();
                results.Add(new { input.Variant, profile, repeat, enabled, stopwatchFrequency = Stopwatch.Frequency,
                    writeTicks, compactTicks, replayTicks, writeAllocated, compactAllocated, decodedBytes,
                    writeP50 = latencies[latencies.Count / 2], writeP95 = latencies[(int)(latencies.Count * .95)],
                    writes = latencies.Count, beforeAllocation, afterAllocation, receipt, usage, seal,
                    verifiedHashes = files.ToDictionary(f => f.Path, f => f.Hash) });
                File.WriteAllText(Path.Combine(input.Output, "result.json"), JsonSerializer.Serialize(new
                { qualification = "fixed-real-Unity-asset-Git-blobs/actual-NTFS-journals/offline; no Editor or WinFsp lifecycle", osCache = "not-evicted; warm/cold unknown", results }, Json));
            }
        }
        if (input.Variant == "after")
        {
            var release = await provider.ReleaseAsync(snapshot, ct);
            File.WriteAllText(Path.Combine(input.Output, "basis-release.json"), JsonSerializer.Serialize(release, Json));
        }
    }
}
