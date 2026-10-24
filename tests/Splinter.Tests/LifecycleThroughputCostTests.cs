using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Splinter.Core;
using Splinter.Platform.Windows;
using Splinter.Runtime;

namespace Splinter.Tests;

// Opt-in component evidence. Execute the same test assembly against the old
// and new product DLLs, in separate processes, without concurrent probes.
public sealed class LifecycleThroughputCostTests
{
    [Fact]
    public async Task LargeLibraryCaptureAdmissionAndUnchangedRestore()
    {
        string output = Output("SPLINTER_LIFECYCLE_LIBRARY_COST");
        const int files = 20000, bytes = 65536;
        using var f = new ManagedDirectoryRestoreOptimizationTests.Fixture(files, false,
            payloadBytes: bytes, unchangedDirectory: true, bounded: true);
        var samples = new List<object>();
        OwnedDirectoryManifest manifest = null!;
        Measure("capture", () => manifest = OwnedDirectorySnapshotTree.Capture(f.State.Basis.Snapshot, f.State.Basis.OwnerId));
        Measure("admission", () =>
        {
            using var tree = new OwnedDirectorySnapshotTree(manifest, manifest.Snapshot);
            byte[] read = new byte[bytes]; Assert.Equal(bytes, tree.Read("dir/f0000", 0, read));
            Assert.Equal(Enumerable.Range(0, bytes).Select(i => (byte)i), read);
        });
        long started = Stopwatch.GetTimestamp(), allocation = GC.GetTotalAllocatedBytes();
        await f.Restore();
        samples.Add(new { stage = "unchanged-restore", seconds = Stopwatch.GetElapsedTime(started).TotalSeconds,
            allocatedBytes = GC.GetTotalAllocatedBytes() - allocation });
        f.AssertRestored();
        Write(output, new { scope = "Single managed Library components on a warm synthetic NTFS tree; no Unity or full workspace timing.",
            files, bytesPerFile = bytes, logicalBytes = (long)files * bytes, samples, product = Product() });
        void Measure(string stage, Action action)
        {
            long begin = Stopwatch.GetTimestamp(), before = GC.GetTotalAllocatedBytes(); action();
            samples.Add(new { stage, seconds = Stopwatch.GetElapsedTime(begin).TotalSeconds,
                allocatedBytes = GC.GetTotalAllocatedBytes() - before });
        }
    }

    private static string Output(string variable)
    {
        string? path = Environment.GetEnvironmentVariable(variable);
        Assert.SkipWhen(string.IsNullOrWhiteSpace(path), $"Set {variable} to a new JSON path for opt-in cost evidence.");
        Assert.False(File.Exists(path)); return Path.GetFullPath(path!);
    }
    private static object Product() => new { measuredAt = DateTimeOffset.UtcNow, processors = Environment.ProcessorCount,
        runtimeSha256 = Hash(typeof(JournalDeltaStore).Assembly.Location), windowsSha256 = Hash(typeof(OwnedDirectorySnapshotTree).Assembly.Location) };
    private static string Hash(string path) => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
    private static void Write(string path, object data)
    { using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None); JsonSerializer.Serialize(file, data, new JsonSerializerOptions { WriteIndented = true }); }
}
