using System.Text.Json;
using System.Reflection;
using Splinter.Core;
using Splinter.Runtime;
using Splinter.Platform.Windows;

namespace Splinter.Tests;

public sealed class JournalIndexResidualTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(48)]
    public void HeaderOnlyCreationGapIsNotAdoptedAndDoesNotTrapStoppedReclamation(int length)
    {
        using var fixture = new Fixture(length); fixture.ContinueAfterGap();
        fixture.Reclaim(); fixture.AssertPreserved();
    }
    [Theory]
    [InlineData("index-preserve.intent")]
    [InlineData("index-preserve.inventory")]
    [InlineData("index-preserve.descendants-released")]
    [InlineData("index-preserve.moved")]
    [InlineData("index-preserve.file")]
    [InlineData("index-preserve.pruned")]
    [InlineData("index-preserve.complete")]
    public void SameStoppedInventoryResumesEveryDirectoryPreservationAndOwnedCleanupBoundary(string point)
    {
        using var fixture = new Fixture(48); bool hit = false;
        Assert.Throws<IOException>(() => fixture.Reclaim(boundary => { if (!hit && boundary == point) { hit = true; throw new IOException("index preservation response lost"); } }));
        Assert.True(hit); fixture.Reclaim(); fixture.AssertPreserved(); fixture.Reclaim();
    }
    [Fact]
    public void AReaderAdmittedAtTheDirectoryHandoffGapBlocksMoveOrDestinationAdmission()
    {
        using var fixture = new Fixture(0); PagedMetadataStore? reader = null;
        try
        {
            var failure = Record.Exception(() => fixture.Reclaim(point =>
            { if (point == "index-preserve.descendants-released") reader = new PagedMetadataStore(fixture.Index); }));
            Assert.NotNull(reader); Assert.True(failure is WorkspaceException or IOException);
        }
        finally { reader?.Dispose(); }
        fixture.Reclaim(); fixture.AssertPreserved();
    }

    [Fact]
    public void AHeaderGapReferencedThroughAnEntryChildIsRefusedBeforePreservation()
    {
        using var root = new TestDirectory(); var pins = new WindowsJournalFilePins(); string index = Path.Combine(root.Path, "index"); MetadataTreeRoot child;
        using (var pages = new PagedMetadataStore(index, true, filePins: pins)) { child = pages.Set(pages.Empty, "child", new byte[] { 3 }); pages.Flush(); }
        string gap = Path.Combine(index, "pages-0000000000000007.bin"); File.WriteAllBytes(gap, File.ReadAllBytes(Path.Combine(index, "identity.bin")));
        MetadataTreeRoot parent;
        using (var pages = new PagedMetadataStore(index, true, filePins: pins))
        {
            // Valid checksum/hash for a deliberately malformed *child* ref:
            // the normal Set API correctly refuses creating this structure.
            Type nodeType = typeof(PagedMetadataStore).GetNestedType("Node", BindingFlags.NonPublic)!;
            object node = Activator.CreateInstance(nodeType, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null,
                ["parent", Array.Empty<byte>(), null, null, child.Page! with { Container = 7 }], null)!;
            var reference = (MetadataPageReference)typeof(PagedMetadataStore).GetMethod("WriteNode", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(pages, [node, null])!;
            parent = new(1, pages.StoreId, reference); pages.Flush();
        }
        string native = pins.Identify(gap);
        using var exclusive = new FileStream(Path.Combine(index, "writer.lock"), FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        var failure = Record.Exception(() => PagedMetadataStore.ValidatePreservationInventory(index, pins, path => path == gap, activeRoots: [parent]));
        Assert.True(failure is WorkspaceException or IOException); Assert.Equal(native, pins.Identify(gap)); Assert.True(File.Exists(gap));
    }

    [Fact]
    public void AnUnpublishedPhasePrefixRemainsOpaqueWhileTheSameRequestCanFinish()
    {
        using var fixture = new Fixture(0); string? prefix = null, native = null; byte[] bytes = "{\"version\":"u8.ToArray();
        Assert.Throws<IOException>(() => fixture.Reclaim(point =>
        {
            if (point != "index-preserve.intent") return;
            string root = Directory.EnumerateDirectories(Path.Combine(fixture.Root.Path, "journal-index-residuals")).Single();
            prefix = Path.Combine(root, "phase-prepared." + Guid.NewGuid().ToString("N") + ".pending");
            File.WriteAllBytes(prefix, bytes); native = fixture.Pins.Identify(prefix); throw new IOException("unpublished control ownership gap");
        }));
        fixture.Reclaim(); fixture.AssertPreserved();
        var retained = Assert.Single(JournalDeltaStore.EnumerateUnpublishedIndexResidualControls(fixture.Root.Path, fixture.Identity));
        Assert.Equal(prefix, retained.Path); Assert.Equal(native, fixture.Pins.Identify(retained.Path)); Assert.Equal(bytes, File.ReadAllBytes(retained.Path));
    }

    [Fact]
    public void AReaderMustFinishBeforeAnUnreachableIndexCanMove()
    {
        using var fixture = new Fixture(0);
        using (var reader = new PagedMetadataStore(fixture.Index))
        { Assert.ThrowsAny<IOException>(() => fixture.Reclaim()); Assert.True(File.Exists(fixture.Gap)); }
        fixture.Reclaim(); fixture.AssertPreserved();
    }
    [Fact]
    public void UnknownPayloadBearingContainerIsPreservedAndRefusedInsteadOfClassifiedAsHeaderGap()
    {
        using var fixture = new Fixture(49);
        var failure = Record.Exception(() => fixture.Reclaim()); Assert.True(failure is WorkspaceException or IOException);
        Assert.True(File.Exists(fixture.Gap)); Assert.True(Directory.Exists(fixture.Delta)); Assert.Empty(JournalDeltaStore.EnumeratePreservedJournalIndices(fixture.Root.Path, fixture.Identity));
    }
    [Fact]
    public void EqualByteOpaqueReplacementAfterMoveBlocksKnownCleanup()
    {
        using var fixture = new Fixture(48); bool replaced = false, prevented = false;
        var failure = Record.Exception(() => fixture.Reclaim(point =>
        {
            if (point != "index-preserve.moved") return;
            var preserved = Assert.Single(JournalDeltaStore.EnumeratePreservedJournalIndices(fixture.Root.Path, fixture.Identity));
            string file = Path.Combine(preserved.Directory, Path.GetFileName(fixture.Gap)), retained = Path.Combine(fixture.Root.Path, "original-header");
            try { File.Move(file, retained); } catch (IOException) { prevented = true; return; }
            File.Copy(retained, file); replaced = true;
        }));
        Assert.True(replaced || prevented);
        if (prevented) { Assert.Null(failure); fixture.AssertPreserved(); return; }
        Assert.IsAssignableFrom<WorkspaceException>(failure); Assert.True(Directory.Exists(fixture.Delta));
        var residual = Assert.Single(JournalDeltaStore.EnumeratePreservedJournalIndices(fixture.Root.Path, fixture.Identity));
        Assert.True(File.Exists(Path.Combine(residual.Directory, "identity.bin"))); // Before first known unlink.
        Assert.Equal(fixture.NativeGap, fixture.Pins.Identify(Path.Combine(fixture.Root.Path, "original-header")));
    }
    private sealed class Fixture : IDisposable
    {
        public TestDirectory Root { get; } = new();
        public WindowsJournalFilePins Pins { get; } = new();
        private readonly SnapshotOverlayTests.FakeSnapshotTree basis = new();
        private readonly byte[] header;
        private readonly Guid file;
        public OverlayIdentity Identity { get; }
        public string Delta { get; }
        public string Index { get; }
        public string Gap { get; }
        public string NativeGap { get; }
        private SnapshotDeltaSeal seal;
        public Fixture(int length)
        {
            Identity = new(3, "source-index-gap", basis.Snapshot, long.MaxValue, 65536); Delta = Path.Combine(Root.Path, "delta");
            using (var source = new JournalDeltaStore(Delta, Identity, basis, sourceFilePins: Pins))
            { file = source.Create("kept", false, 32).Id; source.Write(file, 0, new byte[] { 7 }); seal = source.Seal(); }
            var state = JsonSerializer.Deserialize<JournalSegmentState>(File.ReadAllText(Path.Combine(Delta, "segment-state.json")), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
            Index = state.Roots.Directory; byte[] initial = File.ReadAllBytes(Path.Combine(Index, "identity.bin")).Take(length).ToArray();
            if (length > initial.Length) Array.Resize(ref initial, length); header = initial;
            Gap = Path.Combine(Index, "pages-0000000000000007.bin"); File.WriteAllBytes(Gap, header); NativeGap = Pins.Identify(Gap);
        }
        public void ContinueAfterGap()
        {
            using var source = new JournalDeltaStore(Delta, Identity, basis, sourceFilePins: Pins);
            byte[] bytes = new byte[1]; Assert.Equal(1, source.Read(file, 0, bytes)); Assert.Equal((byte)7, bytes[0]);
            source.Write(file, 0, new byte[] { 8 }); seal = source.Seal(); Assert.Equal(NativeGap, Pins.Identify(Gap));
            Assert.False(File.Exists(Gap + ".owner")); Assert.True(File.Exists(Path.Combine(Index, "pages-0000000000000008.bin.owner")));
        }
        public void Reclaim(Action<string>? boundary = null) => SegmentedJournalReclamation.ReclaimDirectory(Delta, seal, ["identity.json"],
            Path.Combine(Root.Path, "reclaim.bin"), boundary, path => new WindowsJournalDirectoryGuard(path, CurrentJournalReclamation.GuardName), Pins);
        public void AssertPreserved()
        {
            Assert.False(Directory.Exists(Delta)); var residual = Assert.Single(JournalDeltaStore.EnumeratePreservedJournalIndices(Root.Path, Identity));
            string gap = Path.Combine(residual.Directory, Path.GetFileName(Gap)); Assert.Equal(NativeGap, Pins.Identify(gap)); Assert.Equal(header, File.ReadAllBytes(gap));
            Assert.Equal(1, residual.Count); Assert.Equal("unowned-source-index-header-prefix", residual.Reason);
            Assert.False(File.Exists(Path.Combine(residual.Directory, "identity.bin"))); Assert.False(File.Exists(gap + ".owner"));
        }
        public void Dispose() => Root.Dispose();
    }
}
