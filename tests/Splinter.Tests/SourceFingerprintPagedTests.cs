using System.Security.Cryptography;
using System.Text.Json;
using Splinter.Core;
using Splinter.Runtime;

namespace Splinter.Tests;

public sealed class PagedSourceFingerprintTests : SourceFingerprintFixtureBase
{
    [Theory(Timeout = 60000)]
    [InlineData(false)] [InlineData(true)]
    public async Task PagedInitialUnavailableStopsQueriesButEmptyBatchesDoNot(bool unavailable)
    {
        using var fixture = await Fixture.Create(32); fixture.Tracker.Paged = true;
        var ct = TestContext.Current.CancellationToken;
        for (int i = 0; i < 4100; i++) File.WriteAllText(fixture.Path("small-" + i.ToString("D5")), "x");
        fixture.Tracker.InitialSummaries = unavailable ? null : [];
        var provider = fixture.Provider(); await provider.BeginInitialContentInspectionAsync(fixture.Workspace, ct);
        var result = await provider.InspectAsync(fixture.Workspace, ct);
        Assert.Equal(unavailable ? 1 : 2, fixture.Tracker.InitialCalls);
        Assert.Equal(await fixture.Oracle(), result.Fingerprint);
        Assert.True(result.Inspection!.PayloadBytesHashed >= 4100);
    }

    [Fact]
    public async Task PagedProductInspectionKeepsIncrementalRawHashReuseAndSmallControlHeader()
    {
        using var fixture = await Fixture.Create(1024 * 1024); fixture.Tracker.Paged = true;
        var ct = TestContext.Current.CancellationToken;
        Directory.CreateDirectory(fixture.Path("Assets/Nested"));
        for (int i = 0; i < 520; i++)
        {
            string path = "Assets/Nested/file-" + i.ToString("D4") + ".txt";
            File.WriteAllText(fixture.Path(path), "line\r\n"); fixture.Tracker.Mark(path);
        }
        var first = await fixture.Provider().InspectAsync(fixture.Workspace, ct);
        Assert.Equal(await fixture.Oracle(), first.Fingerprint); Assert.True(first.ChangedPathCount > 256); Assert.Equal(256, first.ChangedPaths.Length);
        using (var header = JsonDocument.Parse(File.ReadAllBytes(fixture.Tracker.CachePath(fixture.Workspace))))
        {
            var value = header.RootElement.GetProperty("value"); Assert.Equal(4, value.GetProperty("version").GetInt32());
            Assert.False(value.TryGetProperty("files", out _));
            var root = value.GetProperty("catalog").Deserialize<SnapshotCatalogDescriptor>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
            using var catalog = new PagedSnapshotCatalog(fixture.Tracker.CachePath(fixture.Workspace) + ".pages", root);
            Assert.Equal(520, catalog.EnumerateChildren("Assets/Nested").LongCount());
            Assert.True(new FileInfo(fixture.Tracker.CachePath(fixture.Workspace)).Length < 8192);
        }
        using (var held = new FileStream(fixture.Path("large.bin"), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            var unchanged = await fixture.Provider().InspectAsync(fixture.Workspace, ct);
            Assert.Equal(first.Fingerprint, unchanged.Fingerprint); Assert.Equal("incremental", unchanged.Inspection!.Mode);
            Assert.Equal(0, unchanged.Inspection.PayloadBytesHashed); Assert.Equal(0, unchanged.Inspection.GitHashInputBytes);
        }
        File.WriteAllText(fixture.Path("Assets/Nested/file-0000.txt"), "next\r\n"); fixture.Tracker.Mark("Assets/Nested/file-0000.txt");
        var changed = await fixture.Provider().InspectAsync(fixture.Workspace, ct);
        Assert.Equal(await fixture.Oracle(), changed.Fingerprint); Assert.Equal(6, changed.Inspection!.PayloadBytesHashed);
        var audit = await fixture.Provider(audit: true).InspectAsync(fixture.Workspace, ct);
        Assert.Equal(changed.Fingerprint, audit.Fingerprint); Assert.True(audit.Inspection!.PayloadBytesHashed >= 1024 * 1024);
    }

    [Fact]
    public async Task PagedInspectionProofDetectsMutationAndPagedCacheRejectsOldVersionWithoutRewriting()
    {
        using var fixture = await Fixture.Create(); fixture.Tracker.Paged = true;
        var ct = TestContext.Current.CancellationToken;
        var proof = await fixture.Provider().InspectWithProofAsync(fixture.Workspace, ct); Assert.NotNull(proof.Proof);
        File.WriteAllText(fixture.Path("code.cs"), "return 2;\n"); fixture.Tracker.Mark("code.cs");
        Assert.Equal("SourceChanged", (await Assert.ThrowsAsync<WorkspaceException>(() => proof.Proof!.RevalidateAsync(fixture.Workspace, ct))).Code);
        string path = fixture.Tracker.CachePath(fixture.Workspace);
        string control = File.ReadAllText(path).Replace("\"version\":4", "\"version\":3", StringComparison.Ordinal);
        File.WriteAllText(path, control); byte[] before = File.ReadAllBytes(path);
        Assert.Equal("SourceCacheVersionUnsupported", (await Assert.ThrowsAsync<WorkspaceException>(() => fixture.Provider().InspectAsync(fixture.Workspace, ct))).Code);
        Assert.Equal(before, File.ReadAllBytes(path));
    }


}
