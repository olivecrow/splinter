using System.Diagnostics;
using System.Text.Json;
using Splinter.Platform.Windows;
using Splinter.Runtime;

namespace Splinter.Tests;

public sealed class RootTopologyLeafRestoreCostTests
{
    [Fact]
    public async Task IndependentLeafRenamesExposeTransportAndPublicationCost()
    {
        string? path = Environment.GetEnvironmentVariable("SPLINTER_ROOT_TOPOLOGY_LEAF_RESTORE_COST");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(path), "Set SPLINTER_ROOT_TOPOLOGY_LEAF_RESTORE_COST to a fresh absolute JSON path.");
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Requires ordinary NTFS identity restoration.");
        Assert.True(Path.IsPathFullyQualified(path!)); Assert.False(File.Exists(path));
        Assert.True(Directory.Exists(Path.GetDirectoryName(path)));
        using var fixture = new RootTopologyRestoreFixture(true, "leaf-cost", count: 1024, payloadBytes: 1024);
        var stages = new Dictionary<string, int>(StringComparer.Ordinal);
        long entered = Stopwatch.GetTimestamp(), allocated = GC.GetTotalAllocatedBytes();
        await fixture.Restore(stage => stages[stage] = stages.GetValueOrDefault(stage) + 1);
        long returned = Stopwatch.GetTimestamp(); allocated = GC.GetTotalAllocatedBytes() - allocated;
        fixture.AssertRestored(requireOriginalDirectoryIdentities: false);
        using var output = new FileStream(path!, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        JsonSerializer.Serialize(output, new
        {
            version = 1, measuredAt = DateTimeOffset.UtcNow,
            scope = "Ordinary NTFS complete restore of 1024 fixed 1KiB files, each independently renamed within its unchanged parent. No bulk transported file unit. Native regression-cost screen, not Unity qualification.",
            timing = "Product restore through final proof and reclamation; setup and independent assertions excluded.",
            files = fixture.OriginalFileCount, logicalInputBytes = fixture.OriginalBytes,
            fixture.InputHash, fixture.ExpectedHash, enteredTicks = entered, returnedTicks = returned,
            stopwatchFrequency = Stopwatch.Frequency, seconds = (double)(returned - entered) / Stopwatch.Frequency,
            allocatedBytes = allocated, stages,
            rootMoves = stages.GetValueOrDefault("topology.after-root-move"),
            unitStageMoves = stages.GetValueOrDefault("topology.after-stage"),
            unitInstallMoves = stages.GetValueOrDefault("topology.after-install"),
            generalFileMoves = stages.GetValueOrDefault("file.after-move"),
            directoryIdentitiesPreserved = fixture.OriginalDirectoryIdentitiesPreserved(),
            windowsHash = ManagedDirectoryBasis.HashFile(typeof(OwnedDirectorySnapshotTree).Assembly.Location),
            runtimeHash = ManagedDirectoryBasis.HashFile(typeof(JournalDeltaStore).Assembly.Location),
            testAssemblyHash = ManagedDirectoryBasis.HashFile(typeof(RootTopologyLeafRestoreCostTests).Assembly.Location),
            verified = true
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true });
        output.Flush(true); fixture.AllowCleanup();
    }
}
