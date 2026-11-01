using System.Text.Json;
using Splinter.Core;
using Splinter.Platform.Windows;
using Splinter.Runtime;

namespace Splinter.Tests;

// Segment protocol component evidence; actual workspace/lifecycle qualification
// is recorded separately from these publication and recovery boundaries.
public sealed class JournalSegmentStorageTests
{
    [Theory]
    [InlineData("segment.intent-published")]
    [InlineData("segment.head-renamed")]
    [InlineData("segment.new-head-durable")]
    [InlineData("segment.state-published")]
    [InlineData("segment.intent-removed")]
    public void SameRollRecoversWithoutCopyingTheSealedPayload(string boundary)
    {
        using var root = new TestDirectory(); string delta = Path.Combine(root.Path, "delta"); Directory.CreateDirectory(delta);
        using var pages = new PagedMetadataStore(Path.Combine(delta, "index-" + Guid.NewGuid().ToString("N")), writable: true); pages.Flush();
        var basis = new SnapshotOverlayTests.FakeSnapshotTree(); var identity = new OverlayIdentity(3, "segment-fixture", basis.Snapshot, long.MaxValue, 65536);
        var initial = new JournalIndexRoots(pages.WorkingDirectory, pages.Empty, pages.Empty, pages.Empty, pages.Empty, pages.Empty, pages.Empty, pages.Empty, 0, 0, 0, 0);
        var pins = new WindowsJournalFilePins(); bool armed = false, failed = false; JournalSegment? sealedSegment = null;
        using (var storage = new JournalSegmentStorage(delta, identity, initial, pins, point =>
        { if (armed && !failed && point == boundary) { failed = true; throw new IOException("fixed segment interruption"); } }))
        {
            storage.Active.Write("stable-private-payload"u8); storage.RecordAppend([], [], "stable-private-payload"u8, []); storage.Active.Flush(true); armed = true;
            Assert.Throws<IOException>(() => storage.Rotate(segment =>
            {
                sealedSegment = segment;
                var segments = pages.Set(pages.Empty, segment.Id.ToString("x16"), JsonSerializer.SerializeToUtf8Bytes(segment)); pages.Flush();
                return initial with { Segments = segments, Sequence = 1, PayloadBytes = 22, DecodedPayloadBytes = 22, ExtentCount = 1 };
            }));
        }
        Assert.True(failed); Assert.NotNull(sealedSegment);
        using var resumed = new JournalSegmentStorage(delta, identity, pins: pins);
        Assert.Equal(1, resumed.ActiveId); Assert.Equal(1, resumed.State.Roots.Sequence);
        Assert.Equal(JournalSegmentStorage.HeaderBytes, resumed.Active.Length);
        using var sealedInput = resumed.OpenSealed(sealedSegment); sealedInput.Position = JournalSegmentStorage.HeaderBytes;
        byte[] payload = new byte[checked((int)(sealedInput.Length - JournalSegmentStorage.HeaderBytes))]; sealedInput.ReadExactly(payload);
        Assert.Equal("stable-private-payload"u8.ToArray(), payload);
        Assert.Equal(sealedSegment.NativeIdentity, pins.Identify(resumed.SegmentPath(0)));
    }

    [Theory]
    [InlineData("segment.new-head-durable")]
    [InlineData("segment.state-published")]
    public void FirstHeadInitializationResumesItsPublishedIntent(string boundary)
    {
        using var root = new TestDirectory(); string delta = Path.Combine(root.Path, "delta"); Directory.CreateDirectory(delta);
        using var pages = new PagedMetadataStore(Path.Combine(delta, "index-" + Guid.NewGuid().ToString("N")), writable: true); pages.Flush();
        var basis = new SnapshotOverlayTests.FakeSnapshotTree(); var identity = new OverlayIdentity(3, "segment-init", basis.Snapshot, long.MaxValue, 65536);
        var initial = new JournalIndexRoots(pages.WorkingDirectory, pages.Empty, pages.Empty, pages.Empty, pages.Empty, pages.Empty, pages.Empty, pages.Empty, 0, 0, 0, 0);
        Assert.Throws<IOException>(() => new JournalSegmentStorage(delta, identity, initial, boundary: point =>
        { if (point == boundary) throw new IOException("fixed initialization interruption"); }));
        using var resumed = new JournalSegmentStorage(delta, identity, initial);
        Assert.Equal(0, resumed.ActiveId); Assert.Equal(JournalSegmentStorage.HeaderBytes, resumed.Active.Length);
    }
}
