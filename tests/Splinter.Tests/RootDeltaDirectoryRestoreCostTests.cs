using System.Diagnostics;
using System.Text.Json;
using Splinter.Platform.Windows;
using Splinter.Runtime;

namespace Splinter.Tests;

// Parent controls serial baseline/candidate ABBA order; this fixture never
// selects a candidate implementation or launches a provider/Editor itself.
public sealed class RootDeltaDirectoryRestoreCostTests
{
    [Fact]
    public async Task MixedPrivateRangesRestoreTheWholeFixedLibrary()
    {
        string? path = Environment.GetEnvironmentVariable("SPLINTER_ROOT_DELTA_RESTORE_COST");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(path), "Set SPLINTER_ROOT_DELTA_RESTORE_COST to a fresh absolute JSON path.");
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Requires ordinary NTFS identity and range restoration.");
        Assert.True(Path.IsPathFullyQualified(path!)); Assert.False(File.Exists(path));
        Assert.True(Directory.Exists(Path.GetDirectoryName(path)));
        using var fixture = new RootDeltaRestoreFixture(true, "cost", 10000, 1024);
        var stages = new Dictionary<string, int>(StringComparer.Ordinal);
        long entered = Stopwatch.GetTimestamp(), allocated = GC.GetTotalAllocatedBytes();
        await fixture.Restore(stage => stages[stage] = stages.GetValueOrDefault(stage) + 1);
        long returned = Stopwatch.GetTimestamp(); allocated = GC.GetTotalAllocatedBytes() - allocated;
        int rootMoves = stages.GetValueOrDefault("root-delta.after-move");
        // The prior general route creates ordinary final directories and moves
        // each file. File identity is required in both; directory identity is a
        // property of the candidate root transfer, not a baseline assumption.
        fixture.AssertRestored(rootMoved: false);
        bool directoryIdentitiesPreserved = fixture.OriginalDirectoryIdentitiesPreserved();
        using var output = new FileStream(path!, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        JsonSerializer.Serialize(output, new
        {
            version = 1, measuredAt = DateTimeOffset.UtcNow,
            scope = "Full restore of 10000 fixed ordinary NTFS 1KiB files, 4KiB overwrite, one truncation, one extension, one new sparse leaf, one deleted leaf and fixed metadata; no Unity or mounted-provider qualification.",
            timing = "Product restore includes content/identity proof, range flush, final verification and reclamation. Fixture construction and independent assertions are outside the stopwatch. External serial ABBA controller owns comparison order.",
            files = 10000, bytesPerFile = 1024, logicalInputBytes = 10240000,
            fixture.InputHash, fixture.ExpectedHash, enteredTicks = entered, returnedTicks = returned,
            stopwatchFrequency = Stopwatch.Frequency, seconds = (double)(returned - entered) / Stopwatch.Frequency,
            allocatedBytes = allocated, rootMoves, directoryIdentitiesPreserved,
            survivingFileMoves = stages.GetValueOrDefault("file.after-move"), stages,
            windowsHash = ManagedDirectoryBasis.HashFile(typeof(OwnedDirectorySnapshotTree).Assembly.Location),
            runtimeHash = ManagedDirectoryBasis.HashFile(typeof(JournalDeltaStore).Assembly.Location),
            testAssemblyHash = ManagedDirectoryBasis.HashFile(typeof(RootDeltaDirectoryRestoreCostTests).Assembly.Location),
            verified = true
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true });
        output.Flush(true); fixture.AllowCleanup();
    }
}
