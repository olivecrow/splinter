using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;
using Splinter.Core;
using Splinter.Protocol;
using Splinter.Runtime;

namespace Splinter.Tests;

public sealed class UnlinkedJournalMetricsContractTests
{
    [Fact]
    public void FinalUnlinkedCloseKeepsAllLiveOperationMetricsSchemaValid()
    {
        using var directory = new TestDirectory();
        var tree = new SnapshotOverlayTests.FakeSnapshotTree();
        var identity = new OverlayIdentity(2, "unlink-metrics", tree.Snapshot, 4 * 1048576, 1048576);
        using var store = new JournalDeltaStore(directory.Path, identity, tree, optimizeJournal: true);
        var entry = store.Create("temporary", false, 32);
        store.Write(entry.Id, 0, [1, 2, 3]);
        store.SetLength(entry.Id, 4);
        store.SetMetadata(entry.Id, entry.Attributes, entry.CreationTime, entry.LastAccessTime, entry.LastWriteTime + 1);
        store.Rename(entry.Id, "renamed", false);
        store.Delete(entry.Id);
        store.ReleaseUnlinkedFile(entry.Id);

        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var observation = JsonSerializer.SerializeToNode(new
        {
            version = 1, token = "test", id = identity.Id, phase = "mounted", observedAt = DateTimeOffset.UtcNow,
            processId = 1, snapshotId = tree.Snapshot.Id, storage = store.Observe()
        }, options)!;
        var live = observation["storage"]!["metrics"]!["liveJournalWrites"]!;
        Assert.Equal(7, live["operations"]!.AsArray().Count);
        Assert.Contains(live["operations"]!.AsArray(), operation => operation!["operation"]!.GetValue<string>() == "forget-unlinked");
        var contracts = new ContractCatalog();
        Assert.True(contracts.Validate("urn:splinter:v1:overlay-observation", JsonSerializer.SerializeToElement(observation)).IsValid);

        // Validate the same real counters against both published contract locations.
        JsonElement[] schemas =
        [
            contracts.GetSchema("urn:splinter:v1:overlay-observation").GetProperty("properties").GetProperty("storage")
                .GetProperty("properties").GetProperty("metrics").GetProperty("properties").GetProperty("liveJournalWrites"),
            contracts.GetSchema("urn:splinter:v1:response").GetProperty("$defs").GetProperty("overlayIoMetrics")
                .GetProperty("properties").GetProperty("liveJournalWrites")
        ];
        foreach (var schema in schemas)
        {
            var validator = JsonSchema.FromText(schema.GetRawText(), new BuildOptions { SchemaRegistry = new SchemaRegistry() });
            Assert.True(validator.Evaluate(JsonSerializer.SerializeToElement(live)).IsValid);
            var invalid = live.DeepClone();
            invalid["operations"]![0]!["operation"] = "unrecognized-operation";
            Assert.False(validator.Evaluate(JsonSerializer.SerializeToElement(invalid)).IsValid);
        }
    }
}
