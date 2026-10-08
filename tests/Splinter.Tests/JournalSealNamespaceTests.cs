using Splinter.Core;
using Splinter.Platform.Windows;
using Splinter.Runtime;

namespace Splinter.Tests;

public sealed class JournalSealNamespaceTests
{
    private static void Windows()
    { if (!OperatingSystem.IsWindows()) Assert.Skip("Windows namespace lifetime proof."); }
    private static SnapshotDeltaSeal Populate(string delta)
    {
        var tree = new SnapshotOverlayTests.FakeSnapshotTree();
        var identity = new OverlayIdentity(2, "seal-namespace", tree.Snapshot, 64L * 1048576, 1048576);
        using var journal = new JournalDeltaStore(delta, identity, tree);
        for (int i = 0; i < 16; i++)
        {
            var file = journal.Create("file-" + i, false, 32);
            journal.Write(file.Id, 0, Enumerable.Repeat((byte)(i + 1), 1024).ToArray());
        }
        return journal.Seal();
    }

    [Fact]
    public void ScopedSealPinsAncestryButStillHashesEveryCurrentByte()
    {
        Windows(); using var root = new TestDirectory(); string delta = Path.Combine(root.Path, "delta");
        var seal = Populate(delta); string journal = Path.Combine(delta, "delta.journal");
        var guards = new List<Guard>();
        IJournalDirectoryGuard Pin(string path) { var guard = new Guard(path); guards.Add(guard); return guard; }
        Assert.Equal(seal, JournalDeltaStore.SealJournal(journal, seal.DurableSequence, Pin));
        Assert.All(guards, guard => Assert.True(guard.Disposed));
        Assert.True(guards.Sum(g => g.CheckedFiles) >= 16);
        string file = JournalDeltaStore.JournalStorageFiles(journal).First(p => p != journal && new FileInfo(p).Length > 256);
        using (var output = new FileStream(file, FileMode.Open, FileAccess.ReadWrite))
        { output.Position = 150; int value = output.ReadByte(); output.Position = 150; output.WriteByte((byte)(value ^ 1)); output.Flush(true); }
        Assert.Throws<WorkspaceException>(() => JournalDeltaStore.SealJournal(journal, seal.DurableSequence, Pin));
        Assert.All(guards, guard => Assert.True(guard.Disposed));
        string objects = guards[0].DirectoryPath;
        Directory.Move(objects, objects + ".retained"); // failure also releases every namespace pin
    }

    [Fact]
    public void ScopedSealRejectsForeignFilesAppearingDuringItsRead()
    {
        Windows(); using var root = new TestDirectory(); string delta = Path.Combine(root.Path, "delta");
        var seal = Populate(delta); string? foreign = null;
        var guards = new List<Guard>();
        IJournalDirectoryGuard Pin(string path)
        {
            var guard = new Guard(path, () =>
            {
                if (foreign is not null) return;
                foreign = Path.Combine(path, "foreign.bin"); File.WriteAllBytes(foreign, [41, 42]);
            }); guards.Add(guard); return guard;
        }
        Assert.Throws<WorkspaceException>(() => JournalDeltaStore.SealJournal(Path.Combine(delta, "delta.journal"), seal.DurableSequence, Pin));
        Assert.Equal(new byte[] { 41, 42 }, File.ReadAllBytes(foreign!));
        Assert.All(guards, guard => Assert.True(guard.Disposed));
    }

    [Theory]
    [InlineData("intent")]
    [InlineData("owner-removed")]
    [InlineData("journal-removed")]
    public void ScopedReclamationResumesWithOrWithoutTheNamespaceOwner(string interruption)
    {
        Windows(); using var root = new TestDirectory(); string delta = Path.Combine(root.Path, "delta");
        string receipt = Path.Combine(root.Path, "reclaim.bin"); var seal = Populate(delta);
        string objects = Directory.GetDirectories(delta).Single(); var guards = new List<Guard>();
        IJournalDirectoryGuard Pin(string path) { var guard = new Guard(path); guards.Add(guard); return guard; }
        Assert.Throws<IOException>(() => LatestJournalReclamation.ReclaimDirectory(delta, seal, ["identity.json"], receipt, stage =>
        {
            if (stage == interruption || interruption == "owner-removed" && stage == "object-removed" && !File.Exists(Path.Combine(objects, "owner.lock")))
                throw new IOException("Retain interrupted owned inventory.");
        }, Pin));
        Assert.All(guards, guard => Assert.True(guard.Disposed));
        string foreign = Path.Combine(delta, "foreign.bin"); File.WriteAllBytes(foreign, [23]);
        string[] retained = Directory.GetFiles(delta, "*", SearchOption.AllDirectories);
        Assert.Throws<WorkspaceException>(() => LatestJournalReclamation.ReclaimDirectory(delta, seal, ["identity.json"], receipt, directoryGuardFactory: Pin));
        Assert.All(retained, path => Assert.True(File.Exists(path)));
        File.Delete(foreign);
        LatestJournalReclamation.ReclaimDirectory(delta, seal, ["identity.json"], receipt, directoryGuardFactory: Pin);
        Assert.False(Directory.Exists(delta)); Assert.All(guards, guard => Assert.True(guard.Disposed));
        LatestJournalReclamation.ReclaimDirectory(delta, seal, ["identity.json"], receipt, directoryGuardFactory: Pin);
    }

    private sealed class Guard : IJournalDirectoryGuard
    {
        private readonly WindowsJournalDirectoryGuard inner;
        private readonly Action? observed;
        public string DirectoryPath => inner.DirectoryPath;
        internal bool Disposed;
        internal int CheckedFiles;
        internal Guard(string path, Action? observed = null)
        {
            inner = new(path); this.observed = observed;
            Assert.Throws<IOException>(() => Directory.Move(path, path + ".moved"));
        }
        public void EnsureFile(string path) { inner.EnsureFile(path); CheckedFiles++; observed?.Invoke(); }
        public void Dispose() { inner.Dispose(); Disposed = true; }
    }
}
