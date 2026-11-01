using System.Security.Cryptography;
using Splinter.Core;
using Splinter.SourceControl.Files;

namespace Splinter.Tests;

public sealed class PlasticRevisionBatchTests
{
    [Fact]
    public void ExactOutOfOrderBulkRecordsKeepEveryRevisionIdentityAndBinaryByte()
    {
        byte[] one = [0xff, 0xfe, 0, 0x51], two = [0, 3, 0, 0xff];
        var items = new[] { Item(11, one), Item(12, two) };
        var result = PlasticRevisionBridge.ValidateBatch(items,
            [new(12, 0, true, 0, two), new(11, 0, true, 0, one)], new byte[4 * 1024 * 1024], (_, _) => throw new Exception());
        Assert.Equal(one, result[11]); Assert.Equal(two, result[12]);
    }

    [Theory]
    [InlineData("foreign")]
    [InlineData("duplicate")]
    [InlineData("missing")]
    [InlineData("segment")]
    [InlineData("not-last")]
    [InlineData("length")]
    [InlineData("hash")]
    [InlineData("format")]
    public void AnyInvalidBulkRecordRejectsTheWholePreparedBatch(string fault)
    {
        byte[] payload = [7, 8, 9]; var good = new PlasticRevisionBridge.BatchRecord(11, 0, true, 0, payload);
        PlasticRevisionBridge.BatchRecord[] records = fault switch
        {
            "foreign" => [good with { Revision = 99 }],
            "duplicate" => [good, good],
            "missing" => [],
            "segment" => [good with { Segment = 1 }],
            "not-last" => [good with { Last = false }],
            "length" => [good with { Stored = [7, 8] }],
            "hash" => [good with { Stored = [7, 8, 0] }],
            "format" => [good with { Format = 1 }],
            _ => throw new Exception()
        };
        Assert.Throws<InvalidDataException>(() => PlasticRevisionBridge.ValidateBatch([Item(11, payload)], records,
            new byte[4 * 1024 * 1024], (_, _) => throw new Exception()));
    }

    [Fact]
    public void BatchUnitsRejectOversizedHintsWithoutLimitingIndividualRevisionReads()
    {
        Assert.Throws<InvalidDataException>(() => PlasticRevisionBridge.ValidateBatchRequests(
            Enumerable.Range(0, 65).Select(i => Item(i, [1])).ToArray()));
        Assert.Throws<InvalidDataException>(() => PlasticRevisionBridge.ValidateBatchRequests(
            [new(1, 4 * 1024 * 1024 + 1, Convert.ToBase64String(MD5.HashData([1])))]));
        Assert.Throws<InvalidDataException>(() => PlasticRevisionBridge.ValidateBatchRequests([Item(1, [1]), Item(1, [1])]));
    }

    [Fact]
    public void TreeAdvancesThroughBoundedExactLookaheadBatchesAndKeepsLargeRevisionStreaming()
    {
        var client = new BatchClient();
        using var tree = new PlasticRevisionSnapshotTree(new("fixed", PlasticRevisionSnapshotProvider.ProviderName,
            Path.GetTempPath(), Path.GetTempPath(), DateTimeOffset.UtcNow, true, true, PlasticRevisionSnapshotProvider.Consistency), client.Manifest, client);
        byte[] bytes = new byte[32];
        for (int i = 0; i < 130; i++)
        { Assert.Equal(32, tree.Read($"f{i:D4}", 0, bytes)); Assert.Equal(BatchClient.Payload(i), bytes); }
        Assert.Equal(3, client.Batches.Count); Assert.Equal([64, 64, 2], client.Batches.Select(batch => batch.Count).ToArray());
        Assert.All(client.Batches, batch =>
        {
            Assert.InRange(batch.Count, 1, 64); Assert.InRange(batch.Sum(item => item.Length), 1, 4 * 1024 * 1024);
            Assert.Equal(batch.Count, batch.Select(item => item.Revision).Distinct().Count());
        });
        tree.Read("large", 0, bytes); Assert.Equal(3, client.Batches.Count);
        Assert.Equal(131, client.Opened); Assert.Equal(4 * 1024 * 1024 + 1 + 130 * 32, tree.DecodedRevisionBytes);
    }

    private static PlasticRevisionBridge.BatchItem Item(long id, byte[] payload) => new(id, payload.Length, Convert.ToBase64String(MD5.HashData(payload)));
    private sealed class BatchClient : IPlasticRevisionClient, IPlasticRevisionBatchReader
    {
        private HashSet<string> prepared = [];
        public int Opened;
        public List<IReadOnlyList<PlasticRevisionReadRequest>> Batches { get; } = [];
        public static byte[] Payload(int index) => index == 130 ? new byte[4 * 1024 * 1024 + 1] : Enumerable.Repeat((byte)index, 32).ToArray();
        public PlasticRevisionManifest Manifest { get; } = new("1@repo@server", "42", "1", "11111111-1111-1111-1111-111111111111",
            Enumerable.Range(0, 131).Select(i => new PlasticRevisionEntry(i == 130 ? "large" : $"f{i:D4}", false, (100 + i).ToString(),
                Payload(i).Length, Convert.ToBase64String(MD5.HashData(Payload(i))), DateTime.UtcNow.ToFileTimeUtc())).ToArray(), "rep:repo@server");
        public bool HasPreparedRevision(string revision) => prepared.Contains(revision);
        public void PrepareRevisions(string treeSpec, IReadOnlyList<PlasticRevisionReadRequest> revisions)
        { Batches.Add(revisions.ToArray()); prepared = revisions.Select(item => item.Revision).ToHashSet(); }
        public Stream OpenRevision(string treeSpec, string revision)
        { Opened++; if (!prepared.Contains(revision)) prepared.Clear(); return new MemoryStream(Payload(int.Parse(revision) - 100), false); }
        public Task<PlasticRevisionManifest> ListAsync(string treeSpec, CancellationToken ct = default) => Task.FromResult(Manifest);
        public Task VerifyProjectAsync(string project, PlasticRevisionManifest manifest, CancellationToken ct = default) => Task.CompletedTask;
    }
}
