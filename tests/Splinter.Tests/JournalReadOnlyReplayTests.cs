using System.Security.Cryptography;
using Splinter.Core;
using Splinter.Platform.Windows;
using Splinter.Runtime;

namespace Splinter.Tests;

public sealed class JournalReadOnlyReplayTests
{
    private sealed record SourceFile(string Name, long Length, string Identity, string Hash);
    private static SourceFile[] SourceFiles(string directory, WindowsJournalFilePins pins) =>
        Directory.EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly).Order(StringComparer.Ordinal).Select(path =>
        {
            using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            return new SourceFile(Path.GetFileName(path), input.Length, pins.Identify(path), Convert.ToHexStringLower(SHA256.HashData(input)));
        }).ToArray();

    // Real native metadata and real accepted SJR3 frames, with a modeled
    // immutable source tree. No physical project copy or 100k durable ACKs.
    internal static (SnapshotOverlayTests.FakeSnapshotTree Basis, OverlayIdentity Identity, SnapshotDeltaSeal Seal, string Index)
        SeedLargeDerivedTail(string control, WindowsJournalFilePins pins)
    {
        var basis = new SnapshotOverlayTests.FakeSnapshotTree();
        for (int i = 0; i < 1024; i++) basis.Files.Add("folder/file-" + i.ToString("D4"), []);
        var identity = new OverlayIdentity(3, "readonly-large-tail", basis.Snapshot, long.MaxValue, 65536);
        var tail = SeedLargeDerivedTail(control, pins, basis, identity, "folder", basis.Files.Keys);
        return (basis, identity, tail.Seal, tail.Index);
    }
    internal static (SnapshotDeltaSeal Seal, string Index, string FinalFolder) SeedLargeDerivedTail(string control,
        WindowsJournalFilePins pins, ISnapshotTree basis, OverlayIdentity identity, string folder, IEnumerable<string> childPaths)
    {
        string delta = Path.Combine(control, "delta"); SnapshotDeltaSeal seal; string index; string current = folder;
        using (var writer = new JournalDeltaStore(delta, identity, basis, sourceFilePins: pins))
        {
            foreach (string path in childPaths) Assert.NotNull(writer.Stat(path));
            string captureId = new('a', 24), captureDirectory = Path.Combine(control, "captures", captureId);
            var capture = writer.CaptureSource(captureDirectory, captureId, []); index = capture.Paged!.Directory;
            SourceCaptureTree.Complete(captureDirectory, pins); writer.CompleteSourceCaptureRequest(captureId);
            long before = Directory.EnumerateFiles(index, "pages-*.bin").Sum(path => new FileInfo(path).Length);
            int changes = 0;
            do
            {
                string next = current == folder ? folder + ".renamed" : folder;
                writer.Rename(writer.Stat(current)!.Id, next, false); current = next; changes++;
                if (changes == 64) throw new InvalidOperationException("The fixed tail fixture did not produce its expected metadata volume.");
            }
            while (Directory.EnumerateFiles(index, "pages-*.bin").Sum(path => new FileInfo(path).Length) - before < 96L * 1024 * 1024);
            seal = writer.Seal(); Assert.InRange(changes, 1, 63);
        }
        return (seal, index, current);
    }

    [Fact]
    public void NativeReplayBeyondOneContainerNeverAppendsOrAdoptsSourceMetadata()
    {
        if (!OperatingSystem.IsWindows()) Assert.Skip("Native metadata ownership requires Windows.");
        using var files = new TestDirectory(); var pins = new WindowsJournalFilePins();
        var fixture = SeedLargeDerivedTail(files.Path, pins); var before = SourceFiles(fixture.Index, pins);
        string scratch;
        using (var reader = JournalDeltaStore.OpenReadOnly(Path.Combine(files.Path, "delta"), fixture.Identity, fixture.Basis, sourceFilePins: pins))
        {
            scratch = reader.ReadOnlyReplayScratchDirectory!;
            Assert.Equal(fixture.Seal, reader.Seal()); Assert.Equal(1026, reader.EnumerateRestore().LongCount());
            string[] containers = Directory.EnumerateFiles(scratch, "pages-*.bin", SearchOption.AllDirectories).ToArray();
            Assert.True(containers.Length >= 2, "Actual derived metadata must cross the real 64 MiB container boundary.");
            Assert.True(containers.Sum(path => new FileInfo(path).Length) > PagedMetadataStore.ContainerTargetBytes);
            foreach (string path in containers) Assert.True(File.Exists(path + ".owner"));
            Assert.Equal("SourceObservationUnavailable", Assert.Throws<WorkspaceException>(() => reader.ObserveChanges([])).Code);
            Assert.Equal(before, SourceFiles(fixture.Index, pins));
        }
        Assert.False(Directory.Exists(scratch)); Assert.Equal(before, SourceFiles(fixture.Index, pins));
        using (var again = JournalDeltaStore.OpenReadOnly(Path.Combine(files.Path, "delta"), fixture.Identity, fixture.Basis, sourceFilePins: pins))
            Assert.Equal(fixture.Seal, again.Seal());
        Assert.Equal(before, SourceFiles(fixture.Index, pins));
    }

    [Fact]
    public void AdmittedRestoreCursorRetainsBaseScratchAndOwnedBasisUntilItsLastReaderCloses()
    {
        if (!OperatingSystem.IsWindows()) Assert.Skip("Native metadata ownership requires Windows.");
        using var files = new TestDirectory(); var pins = new WindowsJournalFilePins(); var basis = new SnapshotOverlayTests.FakeSnapshotTree();
        basis.Files.Add("base/nested", [1]); var identity = new OverlayIdentity(3, "readonly-cursor", basis.Snapshot, long.MaxValue, 65536);
        string delta = Path.Combine(files.Path, "delta");
        using (var writer = new JournalDeltaStore(delta, identity, basis, sourceFilePins: pins)) writer.Create("private", false, 32);
        var owned = new OwnedBasis(basis);
        var reader = JournalDeltaStore.OpenReadOnly(delta, identity, owned, ownsSnapshotTree: true, sourceFilePins: pins);
        string scratch = reader.ReadOnlyReplayScratchDirectory!;
        using var cursor = reader.EnumerateRestore().GetEnumerator(); Assert.True(cursor.MoveNext());
        reader.Dispose(); Assert.False(owned.Disposed); Assert.True(Directory.Exists(scratch));
        var paths = new List<string> { cursor.Current.Entry.Path };
        while (cursor.MoveNext()) paths.Add(cursor.Current.Entry.Path);
        cursor.Dispose(); Assert.Contains("base/nested", paths); Assert.Contains("private", paths);
        Assert.True(owned.Disposed); Assert.False(Directory.Exists(scratch));
        Assert.Throws<ObjectDisposedException>(() => reader.EnumerateRestore());
    }

    private sealed class OwnedBasis(SnapshotOverlayTests.FakeSnapshotTree tree) : ISnapshotTree, IDisposable
    {
        public bool Disposed { get; private set; }
        public SnapshotReference Snapshot => tree.Snapshot;
        public void EnsureAvailable() { ObjectDisposedException.ThrowIf(Disposed, this); tree.EnsureAvailable(); }
        public SnapshotEntry? Stat(string path) { EnsureAvailable(); return tree.Stat(path); }
        public IReadOnlyList<SnapshotEntry> List(string path) { EnsureAvailable(); return tree.List(path); }
        public int Read(string path, long offset, Span<byte> destination) { EnsureAvailable(); return tree.Read(path, offset, destination); }
        public void Dispose() => Disposed = true;
    }
}
