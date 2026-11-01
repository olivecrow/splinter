using Splinter.Core;
using Splinter.Runtime;
using Splinter.Platform.Windows;

namespace Splinter.Tests;

public sealed class SegmentedJournalReclamationRecoveryTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(19)]
    [InlineData(-1)]
    public void ATruncatedUnpublishedInventoryResumesTheSameFullSealedNativeProof(int prefix)
    {
        using var root = new TestDirectory(); var basis = new SnapshotOverlayTests.FakeSnapshotTree(); var pins = new WindowsJournalFilePins();
        var identity = new OverlayIdentity(3, "segment-inventory-prefix", basis.Snapshot, long.MaxValue, 65536);
        string delta = Path.Combine(root.Path, "delta"), receipt = Path.Combine(root.Path, "reclaim.bin"); SnapshotDeltaSeal seal;
        using (var source = new JournalDeltaStore(delta, identity, basis, sourceFilePins: pins))
        { var file = source.Create("file", false, 32); source.Write(file.Id, 0, new byte[] { 1, 7, 4 }); seal = source.Seal(); }
        Assert.Throws<IOException>(() => SegmentedJournalReclamation.ReclaimDirectory(delta, seal, ["identity.json"], receipt,
            point => { if (point == "intent-flushed") throw new IOException("inventory durable response lost"); }, filePins: pins));
        using (var pending = new FileStream(receipt + ".pending", FileMode.Open, FileAccess.Write, FileShare.Read))
            pending.SetLength(prefix < 0 ? pending.Length - 17 : prefix);
        SegmentedJournalReclamation.ReclaimDirectory(delta, seal, ["identity.json"], receipt, filePins: pins);
        Assert.False(Directory.Exists(delta)); Assert.True(File.Exists(receipt));
        SegmentedJournalReclamation.ReclaimDirectory(delta, seal, ["identity.json"], receipt, filePins: pins);
    }
}
