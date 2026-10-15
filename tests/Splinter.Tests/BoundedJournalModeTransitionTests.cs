using System.Security.Cryptography;
using Splinter.Core;
using Splinter.Platform.Windows;
using Splinter.Runtime;

namespace Splinter.Tests;

public sealed class BoundedJournalModeTransitionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NativePinsBridgeExplicitConversionUntilTheNewWriterOwnsTheJournal(bool latest)
    {
        if (!OperatingSystem.IsWindows()) Assert.Skip("Windows directory pins and file-sharing handoff.");
        using var root = new TestDirectory();
        string ancestor = Path.Combine(root.Path, "ancestor"), delta = Path.Combine(ancestor, "delta");
        string journal = Path.Combine(delta, "delta.journal"), marker = Path.Combine(delta, BoundedJournalReclamation.GuardName);
        string previous = Path.Combine(delta, "delta.journal.previous"), intent = Path.Combine(delta, "compaction-intent.json");
        var tree = new SnapshotOverlayTests.FakeSnapshotTree();
        byte[] original = new byte[4096]; new Random(1807).NextBytes(original);
        tree.Files["source"] = original.ToArray();
        byte[] expected = original.ToArray(); expected[127] = 91; expected[128] = 92;
        var identity = new OverlayIdentity(2, "native-bounded-transition", tree.Snapshot, 64L * 1048576, 1048576);
        Guid id;
        using (var writer = new JournalDeltaStore(delta, identity, tree, directoryGuardFactory: Pin, boundedLogBytes: 8192))
        {
            id = writer.Stat("source")!.Id;
            writer.Write(id, 127, [91, 92]);
        }

        bool visitedPublication = false;
        var compacted = JournalDeltaStore.CompactDirectory(delta, identity, tree, at =>
        {
            if (at != "published") return;
            visitedPublication = true;
            // The old main handle has closed, but its native ancestry pin and
            // bounded writer fence must survive the atomic format switch.
            Assert.ThrowsAny<IOException>(() => Directory.Move(ancestor, ancestor + "-moved"));
            Assert.ThrowsAny<IOException>(() => File.Delete(marker));
            Assert.ThrowsAny<IOException>(() =>
            { using var competitor = new JournalDeltaStore(delta, identity, tree, directoryGuardFactory: Pin); });
        }, revertBaseRanges: latest, directoryGuardFactory: Pin);
        Assert.True(visitedPublication);
        Assert.True(File.Exists(marker)); Assert.True(File.Exists(previous)); Assert.True(File.Exists(intent));

        // Match the backend's post-publication Files/Seal observation before
        // reconnecting the provider; these reads must not retire the old fence.
        var published = Inventory(delta);
        var files = JournalDeltaStore.JournalStorageFiles(journal);
        Assert.Contains(journal, files); Assert.Contains(marker, files);
        Assert.Equal(compacted.After, JournalDeltaStore.SealJournal(journal, compacted.After.DurableSequence, Pin));
        AssertInventory(published, delta);

        SnapshotDeltaSeal final;
        using (var resumed = new JournalDeltaStore(delta, identity, tree, directoryGuardFactory: Pin))
        {
            Assert.Equal(latest ? "latest" : "append", resumed.JournalStorageMode);
            Assert.False(File.Exists(marker));
            Assert.Equal(compacted.After, resumed.Seal()); Assert.Equal(expected, Read(resumed, id));
            Assert.ThrowsAny<IOException>(() =>
            { using var competitor = new JournalDeltaStore(delta, identity, tree, directoryGuardFactory: Pin); });
            if (latest)
                Assert.ThrowsAny<IOException>(() => Directory.Move(ancestor, ancestor + "-moved"));

            JournalDeltaStore.CompleteCompaction(delta, identity);
            Assert.False(File.Exists(previous)); Assert.False(File.Exists(intent)); Assert.False(File.Exists(marker));
            Assert.Equal(compacted.After, resumed.Seal());
            resumed.Write(id, 129, [93]); expected[129] = 93;
            Assert.Equal(expected, Read(resumed, id)); final = resumed.Seal();
        }
        // All native pins must drain after the admitted writer closes.
        Directory.Move(ancestor, ancestor + "-moved"); Directory.Move(ancestor + "-moved", ancestor);
        using (var reopened = new JournalDeltaStore(delta, identity, tree, directoryGuardFactory: Pin))
        { Assert.Equal(final, reopened.Seal()); Assert.Equal(expected, Read(reopened, id)); }
        Assert.Equal(original, tree.Files["source"]);
    }

    private static IJournalDirectoryGuard Pin(string path) => new WindowsJournalDirectoryGuard(path);

    private static byte[] Read(JournalDeltaStore store, Guid id)
    {
        byte[] bytes = new byte[checked((int)store.Get(id).Length)];
        Assert.Equal(bytes.Length, store.Read(id, 0, bytes)); return bytes;
    }

    private static SortedDictionary<string, string> Inventory(string directory) => new(
        Directory.GetFiles(directory, "*", SearchOption.AllDirectories).ToDictionary(
            path => Path.GetRelativePath(directory, path), path => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)))), StringComparer.Ordinal);

    private static void AssertInventory(SortedDictionary<string, string> expected, string directory)
    {
        var actual = Inventory(directory); Assert.Equal(expected.Keys, actual.Keys);
        foreach (var pair in expected) Assert.Equal(pair.Value, actual[pair.Key]);
    }
}
