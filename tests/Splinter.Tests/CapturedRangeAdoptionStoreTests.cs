using System.Text.Json;
using Splinter.Core;
using Splinter.Platform.Windows;
using Splinter.Runtime;

namespace Splinter.Tests;

public sealed class CapturedRangeAdoptionStoreTests
{
    [Fact]
    public void FixedAdoptionRootSortsFilesAndPreservesCanonicalSliceBoundsAcrossRetry()
    {
        if (!OperatingSystem.IsWindows()) Assert.Skip("Native ownership receipts for immutable adoption pages.");
        using var files = new TestDirectory(); var ct = TestContext.Current.CancellationToken; var pins = new WindowsJournalFilePins();
        var identity = new SourceCaptureIdentity(new string('a', 24), 7, new string('b', 64));
        var id = Guid.Parse("11111111-1111-1111-1111-111111111111");
        CapturedRangeAdoptionEntry Entry(long offset) => new(id, offset, 10, new string('c', 64), (int)offset, new string('d', 64), 5, true, 512);
        var descriptor = CapturedRangeAdoptionStore.Publish(files.Path, identity, [Entry(20), Entry(0)], pins, ct);
        Assert.Equal(2, descriptor.Count); Assert.Equal(new long[] { 0, 20 }, CapturedRangeAdoptionStore.Enumerate(descriptor).Select(row => row.Offset));
        Assert.All(CapturedRangeAdoptionStore.Enumerate(descriptor), row => { Assert.Equal(512, row.ContentLength); Assert.Equal(5, row.ChunkOffset); });
        IEnumerable<CapturedRangeAdoptionEntry> Unused() => Enumerable.Range(0, 1).Select<int, CapturedRangeAdoptionEntry>(_ =>
            throw new InvalidOperationException("A retry must never recollect later bytes."));
        Assert.Equal(descriptor, CapturedRangeAdoptionStore.Publish(files.Path, identity, Unused(), pins, ct));
        Assert.Equal(descriptor, CapturedRangeAdoptionStore.Read(files.Path, identity));
        Assert.Throws<WorkspaceException>(() => CapturedRangeAdoptionStore.Read(files.Path, identity with { Digest = new string('e', 64) }));
        string artifact = CapturedRangeAdoptionStore.Artifact(files.Path, identity.Id);
        File.WriteAllText(artifact, JsonSerializer.Serialize(descriptor with { Directory = files.Path }, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        Assert.Throws<WorkspaceException>(() => CapturedRangeAdoptionStore.Read(files.Path, identity));
    }
    [Theory]
    [InlineData(501, 512)] [InlineData(-1, 512)] [InlineData(0, 9)]
    public void MalformedCanonicalSliceBoundsCannotBePublished(int offset, int chunkLength)
    {
        var row = new CapturedRangeAdoptionEntry(Guid.NewGuid(), 0, 12, new string('a', 64), 0, new string('b', 64), offset, false, chunkLength);
        Assert.Equal("SourceRangeAdoptionInvalid", Assert.Throws<WorkspaceException>(() => CapturedRangeAdoptionStore.Encode(row)).Code);
    }
}
