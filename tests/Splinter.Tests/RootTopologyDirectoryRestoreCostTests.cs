using System.Diagnostics;
using System.Text.Json;
using Splinter.Platform.Windows;
using Splinter.Runtime;

namespace Splinter.Tests;

public sealed class RootTopologyDirectoryRestoreCostTests
{
    [Fact]
    public async Task BulkSubtreeAndNestedExceptionsRestoreTheWholeFixedLibrary()
    {
        string? path = Environment.GetEnvironmentVariable("SPLINTER_ROOT_TOPOLOGY_RESTORE_COST");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(path), "Set SPLINTER_ROOT_TOPOLOGY_RESTORE_COST to a fresh absolute JSON path.");
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Requires ordinary NTFS topology and identity restoration.");
        Assert.True(Path.IsPathFullyQualified(path!)); Assert.False(File.Exists(path)); Assert.True(Directory.Exists(Path.GetDirectoryName(path)));
        using var fixture = new RootTopologyRestoreFixture(true, "cost", count: 10000, payloadBytes: 1024);
        var stages = new Dictionary<string, int>(StringComparer.Ordinal);
        long entered = Stopwatch.GetTimestamp(), allocated = GC.GetTotalAllocatedBytes();
        await fixture.Restore(stage => stages[stage] = stages.GetValueOrDefault(stage) + 1);
        long returned = Stopwatch.GetTimestamp(); allocated = GC.GetTotalAllocatedBytes() - allocated;
        // Both implementations must preserve surviving FILE identities; the
        // candidate additionally preserves source directories through transport.
        fixture.AssertRestored(requireOriginalDirectoryIdentities: false);
        bool directoryIdentitiesPreserved = fixture.OriginalDirectoryIdentitiesPreserved();
        using var output = new FileStream(path!, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        JsonSerializer.Serialize(output, new
        {
            version = 1, measuredAt = DateTimeOffset.UtcNow,
            scope = "Full ordinary NTFS restore of 10000 fixed 1KiB files: one initially 9993-file subtree (9991 direct plus two nested leaves), nested and leaf exceptions extracted before bulk transport, parent reparenting, independent moved leaves, deleted subtree/leaves, new nested directories/files, overwrite/truncate/extend and fixed metadata. No Unity or mounted-provider qualification.",
            timing = "Complete product restore through proof, durable content writes, final verification and reclamation. Fixture preparation and independent assertions excluded. External serial ABBA controller owns order; the same test assembly uses existing product APIs for baseline and candidate.",
            files = fixture.OriginalFileCount, bytesPerFile = 1024, logicalInputBytes = fixture.OriginalBytes,
            initialRenamedSubtreeFiles = 9993, retainedMainTransportUnitFiles = 9989,
            fixture.InputHash, fixture.ExpectedHash, enteredTicks = entered, returnedTicks = returned,
            stopwatchFrequency = Stopwatch.Frequency, seconds = (double)(returned - entered) / Stopwatch.Frequency,
            allocatedBytes = allocated, rootMoves = stages.GetValueOrDefault("topology.after-root-move"),
            unitStageMoves = stages.GetValueOrDefault("topology.after-stage"), unitInstallMoves = stages.GetValueOrDefault("topology.after-install"),
            newDirectoryReceipts = stages.GetValueOrDefault("topology.new-directory-after-receipt"),
            survivingFileMoves = stages.GetValueOrDefault("file.after-move"), directoryIdentitiesPreserved, stages,
            windowsHash = ManagedDirectoryBasis.HashFile(typeof(OwnedDirectorySnapshotTree).Assembly.Location),
            runtimeHash = ManagedDirectoryBasis.HashFile(typeof(JournalDeltaStore).Assembly.Location),
            testAssemblyHash = ManagedDirectoryBasis.HashFile(typeof(RootTopologyDirectoryRestoreCostTests).Assembly.Location), verified = true
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true });
        output.Flush(true); fixture.AllowCleanup();
    }
}
