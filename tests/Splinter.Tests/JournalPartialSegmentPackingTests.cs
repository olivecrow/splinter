using Splinter.Core;
using Splinter.Platform.Windows;
using Splinter.Runtime;

namespace Splinter.Tests;

// Private journal packing evidence. No CAS/content reader is composed: the
// Library/managed-original producer must reclaim partially dead payload itself.
public sealed class JournalPartialSegmentPackingTests
{
    [Fact]
    public void MoreThanOneTargetUnitDrainsOneFrozenPlanWithoutRescanningOrDroppingLaterSources()
    {
        RequireWindows(); using var f = new Fixture("multiple-units"); Guid[] files = new Guid[3];
        var jobs = new HashSet<string>(StringComparer.Ordinal); var plans = new HashSet<string>(StringComparer.Ordinal);
        int intents = 0, published = 0; SnapshotDeltaSeal seal;
        using (var writer = f.Open(processingBytes: null))
        {
            byte[] chunk = new byte[1024 * 1024];
            for (int index = 0; index < files.Length; index++)
            {
                Array.Fill(chunk, (byte)(index + 61)); var file = writer.Create("many-" + index, false, 32); files[index] = file.Id;
                for (int part = 0; part < 10; part++) writer.Write(file.Id, (long)part * chunk.Length, chunk);
                writer.SetLength(file.Id, 9L * chunk.Length);
            }
            seal = writer.Seal(); long before = PayloadAllocated(f);
            writer.MaintainLiveStorage(collectSegments: true, ct: TestContext.Current.CancellationToken, boundary: point =>
            {
                if (point == "segment-pack.intent") intents++;
                if (point == "segment-pack.published") published++;
                string root = Path.Combine(f.Root.Path, "journal-packing"); if (!Directory.Exists(root)) return;
                foreach (string job in Directory.EnumerateDirectories(root))
                { jobs.Add(job); foreach (string plan in Directory.EnumerateDirectories(job, "index-*")) plans.Add(plan); }
            });
            Assert.Equal(1, intents); Assert.True(published >= 2, "More than 16MiB of disjoint live survivors must advance through multiple target units.");
            Assert.Single(jobs); Assert.Single(plans); Assert.Equal(seal, writer.Seal()); Assert.True(PayloadAllocated(f) < before);
            for (int index = 0; index < files.Length; index++) VerifyNineMiB(writer, files[index], (byte)(index + 61));
        }
        using var resumed = f.Open(processingBytes: null); Assert.Equal(seal, resumed.Seal());
        for (int index = 0; index < files.Length; index++) VerifyNineMiB(resumed, files[index], (byte)(index + 61));
    }
    [Fact]
    public void RepeatedRealSixteenMiBSegmentsRetainingOneByteDoNotKeepTheirDeadPayload()
    {
        RequireWindows(); using var f = new Fixture("real-window"); Guid[] files = new Guid[3];
        using (var writer = f.Open(processingBytes: null))
        {
            byte[] chunk = new byte[1024 * 1024];
            for (int generation = 0; generation < files.Length; generation++)
            {
                Array.Fill(chunk, (byte)(generation + 41)); var file = writer.Create("keep-" + generation, false, 32); files[generation] = file.Id;
                for (int part = 0; part < 16; part++) writer.Write(file.Id, (long)part * chunk.Length, chunk);
                writer.SetLength(file.Id, 1); var seal = writer.Seal();
                Assert.True(PayloadAllocated(f) >= 15L * 1024 * 1024);
                writer.MaintainLiveStorage(collectSegments: true, ct: TestContext.Current.CancellationToken);
                Assert.Equal(seal, writer.Seal());
                Assert.True(PayloadAllocated(f) < 1024 * 1024, "A one-byte survivor must not retain a 16MiB backing; fixed headers/allocation units are included.");
                for (int prior = 0; prior <= generation; prior++)
                { byte[] observed = new byte[1]; Assert.Equal(1, writer.Read(files[prior], 0, observed)); Assert.Equal((byte)(prior + 41), observed[0]); }
            }
        }
        using var reopened = f.Open(processingBytes: null);
        for (int index = 0; index < files.Length; index++)
        { byte[] observed = new byte[1]; Assert.Equal(1, reopened.Read(files[index], 0, observed)); Assert.Equal((byte)(index + 41), observed[0]); }
    }

    [Fact]
    public void PrefixAndSuffixOfOnePhysicalBlockRemainExactWithoutRetainingItsDeletedMiddle()
    {
        RequireWindows(); using var f = new Fixture("split-block"); using var writer = f.Open();
        byte[] expected = Enumerable.Range(0, 65536).Select(i => (byte)i).ToArray(); var file = writer.Create("split", false, 32);
        writer.Write(file.Id, 0, expected); byte[] middle = Enumerable.Repeat((byte)0x91, 59900).ToArray();
        writer.Write(file.Id, 100, middle); middle.CopyTo(expected, 100); var seal = writer.Seal(); long before = PayloadAllocated(f);
        writer.MaintainLiveStorage(collectSegments: true, ct: TestContext.Current.CancellationToken);
        byte[] observed = new byte[expected.Length]; Assert.Equal(observed.Length, writer.Read(file.Id, 0, observed)); Assert.Equal(expected, observed);
        Assert.Equal(seal, writer.Seal()); Assert.True(PayloadAllocated(f) < before);
        Assert.True(PayloadAllocated(f) < 96 * 1024, "Packed private payload must exclude the dead original middle and account fixed allocation overhead.");
    }

    [Fact]
    public void PayloadCopyRunsOutsideTheMutationGateAndConditionalRelocationKeepsLaterEditsAndCapture()
    {
        RequireWindows(); using var f = new Fixture("concurrent"); using var writer = f.Open();
        var file = f.Partial(writer, "source", 8192, 16, 0x31); bool changed = false; string id = new('a', 24), path = f.CapturePath(id);
        writer.MaintainLiveStorage(collectSegments: true, ct: TestContext.Current.CancellationToken, boundary: point =>
        {
            if (point != "segment-pack.payload-durable" || changed) return; changed = true;
            var mutation = Task.Run(() =>
            {
                writer.Write(file.Id, 4, new byte[] { 0x77, 0x77, 0x77 }); writer.SetLength(file.Id, 12); writer.Rename(file.Id, "continued", false);
                writer.CaptureSource(path, id, []);
            }, TestContext.Current.CancellationToken);
            Assert.True(mutation.Wait(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken), "Payload copying cannot hold the live mutation gate.");
            mutation.GetAwaiter().GetResult();
        });
        Assert.True(changed); byte[] expected = Enumerable.Repeat((byte)0x31, 12).ToArray(); Array.Fill(expected, (byte)0x77, 4, 3);
        byte[] observed = new byte[12]; Assert.Equal(12, writer.Read(file.Id, 0, observed)); Assert.Equal(expected, observed); Assert.Equal("continued", writer.Get(file.Id).Path);
        using var captured = new SourceCaptureTree(path, f.Basis, f.Pins);
        Assert.Equal(12, captured.Read("continued", 0, observed)); Assert.Equal(expected, observed);
    }

    [Fact]
    public void PendingCaptureProtectsOldSegmentEvenWithItsExternalHeaderMissing()
    {
        RequireWindows(); using var f = new Fixture("pending-capture"); using var writer = f.Open();
        var file = f.Partial(writer, "source", 8192, 16, 0x44); string id = new('b', 24), path = f.CapturePath(id);
        var original = writer.CaptureSource(path, id, []); File.Delete(Path.Combine(path, "capture.json"));
        writer.MaintainLiveStorage(collectSegments: true, ct: TestContext.Current.CancellationToken);
        writer.RecoverSourceCaptures(Path.Combine(f.Root.Path, "captures")); Assert.Equal(original.Digest, SourceCaptureTree.ReadCapture(path).Digest);
        writer.Write(file.Id, 0, Enumerable.Repeat((byte)0x66, 16).ToArray());
        using (var captured = new SourceCaptureTree(path, f.Basis, f.Pins))
        {
            byte[] observed = new byte[16]; Assert.Equal(16, captured.Read("source", 0, observed)); Assert.All(observed, value => Assert.Equal((byte)0x44, value));
            Assert.ThrowsAny<IOException>(() => SourceCaptureTree.Complete(path, f.Pins));
        }
        SourceCaptureTree.Complete(path, f.Pins); writer.CompleteSourceCaptureRequest(id);
        writer.MaintainLiveStorage(collectSegments: true, ct: TestContext.Current.CancellationToken);
        byte[] current = new byte[16]; Assert.Equal(16, writer.Read(file.Id, 0, current)); Assert.All(current, value => Assert.Equal((byte)0x66, value));
    }

    [Fact]
    public void AnOldBackingReaderDefersPhysicalDeletionWhileTheWriterCanContinue()
    {
        RequireWindows(); using var f = new Fixture("reader-defer"); using var writer = f.Open();
        var file = f.Partial(writer, "source", 8192, 1, 0x51);
        string old = Directory.EnumerateFiles(f.Delta, "segment-*.sjr").MaxBy(path => new FileInfo(path).Length)!;
        var seal = writer.Seal();
        using (var reader = new FileStream(old, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            writer.MaintainLiveStorage(collectSegments: true, ct: TestContext.Current.CancellationToken);
            Assert.Equal(seal, writer.Seal()); Assert.True(File.Exists(old));
            writer.Write(file.Id, 0, new byte[] { 0xa2 }); byte[] observed = new byte[1]; Assert.Equal(1, writer.Read(file.Id, 0, observed)); Assert.Equal((byte)0xa2, observed[0]);
        }
        writer.MaintainLiveStorage(collectSegments: true, ct: TestContext.Current.CancellationToken);
        Assert.False(File.Exists(old));
    }

    [Theory]
    [InlineData("segment-pack.intent")]
    [InlineData("segment-pack.created-before-identity")]
    [InlineData("segment-pack.identity-durable")]
    [InlineData("segment-pack.payload-durable")]
    [InlineData("segment-pack.map-durable")]
    [InlineData("segment-pack.published")]
    [InlineData("segment-pack.relocated")]
    [InlineData("segment-pack.relocated-all")]
    [InlineData("segment-pack.root-rolled")]
    [InlineData("segment-pack.alias-retired")]
    public void SamePackingRequestRecoversEveryDurableBoundaryIncludingDiskFullBeforeOwnership(string boundary)
    {
        RequireWindows(); using var f = new Fixture("recovery"); Guid file; SnapshotDeltaSeal seal; bool hit = false;
        using (var writer = f.Open())
        {
            file = f.Partial(writer, "source", 8192, 16, 0x62).Id; seal = writer.Seal();
            Assert.Throws<IOException>(() => writer.MaintainLiveStorage(collectSegments: true, ct: TestContext.Current.CancellationToken, boundary: point =>
            { if (!hit && point == boundary) { hit = true; throw new IOException("Injected disk full / response loss at exact packing phase."); } }));
        }
        Assert.True(hit, "The required packing boundary must be exercised.");
        using var resumed = f.Open(); resumed.MaintainLiveStorage(collectSegments: true, ct: TestContext.Current.CancellationToken);
        Assert.Equal(seal, resumed.Seal()); byte[] observed = new byte[16]; Assert.Equal(16, resumed.Read(file, 0, observed)); Assert.All(observed, value => Assert.Equal((byte)0x62, value));
        var completed = resumed.Seal(); resumed.MaintainLiveStorage(collectSegments: true, ct: TestContext.Current.CancellationToken); Assert.Equal(completed, resumed.Seal());
    }

    [Fact]
    public void ForeignEqualBytePackedReplacementIsRejectedBeforeOldPayloadDeletion()
    {
        RequireWindows(); using var f = new Fixture("target-clone"); using var writer = f.Open();
        f.Partial(writer, "source", 8192, 16, 0x73); bool replaced = false, prevented = false; string? foreign = null, preserved = null;
        string old = Directory.EnumerateFiles(f.Delta, "segment-*.sjr").MaxBy(path => new FileInfo(path).Length)!;
        var failure = Record.Exception(() => writer.MaintainLiveStorage(collectSegments: true, ct: TestContext.Current.CancellationToken, boundary: point =>
        {
            if (point != "segment-pack.relocated-all" || replaced) return;
            foreign = Directory.EnumerateFiles(f.Delta, "packed-*.sjr").First(); preserved = Path.Combine(f.Root.Path, "preserved-proven-packed.sjr");
            string known = f.Pins.Identify(foreign);
            try { File.Move(foreign, preserved); }
            catch (IOException) { Assert.Equal(known, f.Pins.Identify(foreign)); prevented = true; return; }
            replaced = true; File.Copy(preserved, foreign); Assert.NotEqual(known, f.Pins.Identify(foreign));
        }));
        Assert.True(replaced || prevented);
        if (replaced)
        { Assert.IsAssignableFrom<WorkspaceException>(failure); Assert.True(File.Exists(old)); Assert.True(File.Exists(foreign)); Assert.True(File.Exists(preserved)); }
        else Assert.Null(failure); // A held native fence can prevent replacement outright.
    }

    [Fact]
    public void PackedDataSurvivesLiveMetadataCollectionAndLaterNormalFrameRolls()
    {
        RequireWindows(); using var f = new Fixture("index-tail"); Guid first, second; SnapshotDeltaSeal seal;
        using (var writer = f.Open())
        {
            first = f.Partial(writer, "first", 8192, 1, 0x84).Id;
            writer.MaintainLiveStorage(collectSegments: true, ct: TestContext.Current.CancellationToken);
            Assert.NotEmpty(Directory.EnumerateFiles(f.Delta, "packed-*.sjr"));
            second = f.Partial(writer, "second", 8192, 1, 0x85).Id;
            writer.CollectLiveMetadata(force: true, ct: TestContext.Current.CancellationToken);
            writer.MaintainLiveStorage(collectSegments: true, ct: TestContext.Current.CancellationToken); seal = writer.Seal();
            writer.CollectLiveMetadata(force: true, ct: TestContext.Current.CancellationToken); Assert.Equal(seal, writer.Seal());
        }
        using var resumed = f.Open(); Assert.Equal(seal, resumed.Seal()); byte[] observed = new byte[1];
        Assert.Equal(1, resumed.Read(first, 0, observed)); Assert.Equal((byte)0x84, observed[0]);
        Assert.Equal(1, resumed.Read(second, 0, observed)); Assert.Equal((byte)0x85, observed[0]);
    }

    [Fact]
    public void LogicalOffsetsAboveFourGiBAreNotTruncatedByPacking()
    {
        RequireWindows(); using var f = new Fixture("long-offset"); using var writer = f.Open();
        long offset = 4L * 1024 * 1024 * 1024 + 37; var file = writer.Create("huge-logical", false, 32);
        writer.Write(file.Id, offset, Enumerable.Repeat((byte)0x83, 8192).ToArray()); writer.SetLength(file.Id, offset + 1); var seal = writer.Seal();
        writer.MaintainLiveStorage(collectSegments: true, ct: TestContext.Current.CancellationToken);
        Assert.Equal(offset + 1, writer.Get(file.Id).Length); byte[] observed = new byte[1]; Assert.Equal(1, writer.Read(file.Id, offset, observed)); Assert.Equal((byte)0x83, observed[0]);
        Assert.Equal(seal, writer.Seal()); Assert.True(PayloadAllocated(f) < 65536);
    }

    [Fact(Timeout = 10000)]
    public void OpportunisticPackingReclaimsDeadPayloadAndLeavesLowGainNormalBacking()
    {
        RequireWindows(); using var f = new Fixture("normal-pack-payback"); var ct = TestContext.Current.CancellationToken;
        byte[] stableBytes = Enumerable.Repeat((byte)0x57, 4096).ToArray();
        string stableBacking = Path.Combine(f.Delta, "segment-0000000000000000.sjr");
        string stableIdentity;
        Guid stable, partial;
        SnapshotDeltaSeal seal;
        using (var writer = f.Open(processingBytes: 64 * 1024))
        {
            stable = writer.Create("stable", false, 32).Id;
            for (int block = 0; block < 64; block++) writer.Write(stable, (long)block * stableBytes.Length, stableBytes);
            stableIdentity = f.Pins.Identify(stableBacking);

            // Put genuinely dead payload in a different normal segment.
            partial = f.Partial(writer, "partial", 128 * 1024, 16, 0x61).Id;
            string deadBacking = Assert.Single(Directory.EnumerateFiles(f.Delta, "segment-*.sjr"), path => new FileInfo(path).Length > 128 * 1024);
            seal = writer.Seal();
            writer.MaintainLiveStorage(collectSegments: true, ct: ct);

            Assert.Equal(stableIdentity, f.Pins.Identify(stableBacking));
            Assert.False(File.Exists(deadBacking));
            Assert.Equal(seal, writer.Seal());
        }

        using var resumed = f.Open(processingBytes: 64 * 1024);
        Assert.Equal(seal, resumed.Seal());
        byte[] observed = new byte[stableBytes.Length];
        Assert.Equal(observed.Length, resumed.Read(stable, 32L * stableBytes.Length, observed));
        Assert.Equal(stableBytes, observed);
        Assert.Equal(16, resumed.Read(partial, 0, observed));
        Assert.All(observed[..16], value => Assert.Equal((byte)0x61, value));

        resumed.MaintainLiveStorage(force: true, ct: ct);
        Assert.False(File.Exists(stableBacking));
        Assert.Equal(seal, resumed.Seal());
        Assert.Equal(observed.Length, resumed.Read(stable, 32L * stableBytes.Length, observed));
        Assert.Equal(stableBytes, observed);
    }

    [Fact(Timeout = 10000)]
    public void FrameHeavyNormalBackingCanPayForPackingWithoutDeadPayload()
    {
        RequireWindows(); using var f = new Fixture("frame-heavy-normal"); using var writer = f.Open(processingBytes: null);
        byte[] payload = Enumerable.Repeat((byte)0x63, 32).ToArray();
        var file = writer.Create("dense-frames", false, 32);
        for (int block = 0; block < 128; block++) writer.Write(file.Id, (long)block * payload.Length, payload);
        var seal = writer.Seal();

        writer.MaintainLiveStorage(collectSegments: true, ct: TestContext.Current.CancellationToken);

        string packed = Assert.Single(Directory.EnumerateFiles(f.Delta, "packed-*.sjr"));
        Assert.Equal(4096, new FileInfo(packed).Length);
        Assert.Equal(seal, writer.Seal());
        byte[] observed = new byte[payload.Length];
        Assert.Equal(observed.Length, writer.Read(file.Id, 127L * payload.Length, observed));
        Assert.Equal(payload, observed);
    }

    [Theory(Timeout = 10000)]
    [InlineData(false)] [InlineData(true)]
    public void SparsePackedGarbageWaitsForForceAndPendingForceStillRecovers(bool interrupt)
    {
        VerifySparsePackedGarbage(interrupt, scratchPressure: false, TestContext.Current.CancellationToken);
    }

    internal static void VerifySparsePackedGarbage(bool interrupt, bool scratchPressure, CancellationToken ct)
    {
        RequireWindows(); using var f = new Fixture("sparse-repack-" + interrupt);
        Guid id; string packed; SnapshotDeltaSeal seal;
        using (var writer = f.Open(processingBytes: null))
        {
            id = f.Partial(writer, "file", 192 * 1024, 128 * 1024, 0x58).Id;
            writer.MaintainLiveStorage(force: true, ct: ct);
            packed = Assert.Single(Directory.EnumerateFiles(f.Delta, "packed-*.sjr"));
            writer.SetLength(id, 124 * 1024); seal = writer.Seal();
            // Account the entire common budget without allocating it. The
            // sort and new row spool must use their existing disk fallbacks.
            using var pressure = scratchPressure ? StorageResources.Scratch.Open() : null;
            if (pressure is not null) Assert.True(pressure.TryResize(StorageResources.ScratchBytes));
            writer.MaintainLiveStorage(collectSegments: true, ct: ct);
            Assert.True(File.Exists(packed)); Assert.Equal(seal, writer.Seal());
            if (interrupt)
                Assert.Throws<IOException>(() => writer.MaintainLiveStorage(force: true, ct: ct,
                    boundary: point => { if (point == "segment-pack.intent") throw new IOException("Lost forced packing response before planning."); }));
            else writer.MaintainLiveStorage(force: true, ct: ct);
        }
        using var resumed = f.Open(processingBytes: null);
        // A recorded request is recovery, even when the resumed call is not force.
        resumed.MaintainLiveStorage(collectSegments: true, ct: ct);
        Assert.False(File.Exists(packed)); Assert.Equal(seal, resumed.Seal());
        byte[] observed = new byte[124 * 1024]; Assert.Equal(observed.Length, resumed.Read(id, 0, observed));
        Assert.All(observed, value => Assert.Equal((byte)0x58, value));
    }

    [Fact(Timeout = 10000)]
    public void PackedGarbageAtQuarterThresholdIsReclaimedWithoutForce()
    {
        RequireWindows(); using var f = new Fixture("worthwhile-repack"); using var writer = f.Open(processingBytes: null);
        var file = f.Partial(writer, "file", 192 * 1024, 128 * 1024, 0x59);
        writer.MaintainLiveStorage(force: true, ct: TestContext.Current.CancellationToken);
        string packed = Assert.Single(Directory.EnumerateFiles(f.Delta, "packed-*.sjr"));
        writer.SetLength(file.Id, 96 * 1024); var seal = writer.Seal();
        writer.MaintainLiveStorage(collectSegments: true, ct: TestContext.Current.CancellationToken);
        Assert.False(File.Exists(packed)); Assert.Equal(seal, writer.Seal());
        byte[] observed = new byte[96 * 1024]; Assert.Equal(observed.Length, writer.Read(file.Id, 0, observed));
        Assert.All(observed, value => Assert.Equal((byte)0x59, value));
    }

    private static long PayloadAllocated(Fixture f)
    {
        long bytes = 0;
        foreach (string path in Directory.EnumerateFiles(f.Delta).Where(path => path.EndsWith(".sjr", StringComparison.Ordinal) || Path.GetFileName(path) == "delta.journal"))
        { using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete); bytes = checked(bytes + f.Pins.AllocatedBytes(input)); }
        return bytes;
    }
    private static void VerifyNineMiB(JournalDeltaStore writer, Guid id, byte value)
    {
        const long length = 9L * 1024 * 1024; Assert.Equal(length, writer.Get(id).Length);
        byte[] observed = new byte[65536], expected = Enumerable.Repeat(value, 65536).ToArray();
        using var actualHash = System.Security.Cryptography.IncrementalHash.CreateHash(System.Security.Cryptography.HashAlgorithmName.SHA256);
        using var expectedHash = System.Security.Cryptography.IncrementalHash.CreateHash(System.Security.Cryptography.HashAlgorithmName.SHA256);
        for (long offset = 0; offset < length; offset += observed.Length)
        { Assert.Equal(observed.Length, writer.Read(id, offset, observed)); actualHash.AppendData(observed); expectedHash.AppendData(expected); }
        Assert.Equal(expectedHash.GetHashAndReset(), actualHash.GetHashAndReset());
    }
    private static void RequireWindows() { if (!OperatingSystem.IsWindows()) Assert.Skip("Exact NTFS packing ownership and allocated-byte evidence."); }
    private sealed class Fixture : IDisposable
    {
        public TestDirectory Root { get; } = new();
        public SnapshotOverlayTests.FakeSnapshotTree Basis { get; } = new();
        public WindowsJournalFilePins Pins { get; } = new();
        public OverlayIdentity Identity { get; }
        public string Delta => Path.Combine(Root.Path, "delta");
        public Fixture(string name) => Identity = new(3, "pack-" + name, Basis.Snapshot, long.MaxValue, 1024 * 1024);
        public JournalDeltaStore Open(long? processingBytes = 4096) => new(Delta, Identity, Basis, boundedLogBytes: processingBytes, sourceFilePins: Pins);
        public string CapturePath(string id) => Path.Combine(Root.Path, "captures", id);
        public DeltaEntry Partial(JournalDeltaStore writer, string path, int bytes, int retained, byte value)
        { var file = writer.Create(path, false, 32); writer.Write(file.Id, 0, Enumerable.Repeat(value, bytes).ToArray()); writer.SetLength(file.Id, retained); return file; }
        public void Dispose() => Root.Dispose();
    }
}
