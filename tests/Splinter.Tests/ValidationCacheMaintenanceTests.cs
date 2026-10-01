using System.Text.Json;
using Splinter.Core;
using Splinter.Protocol;
using Splinter.Runtime;
using Splinter.Workflows.Windows;

namespace Splinter.Tests;

public sealed class ValidationCacheMaintenanceTests
{
    [Theory]
    [InlineData("maintenance:intent")][InlineData("intent")][InlineData("candidate-flushed")]
    [InlineData("prepared")][InlineData("published")][InlineData("maintenance:observed")]
    [InlineData("maintenance:reclaimed")][InlineData("maintenance:receipt")]
    public void InterruptedMaintenancePreservesBaseAndRecoversOnlyTheSameRequest(string cut)
    {
        using var dir = new TestDirectory(); var tree = new SnapshotOverlayTests.FakeSnapshotTree();
        tree.Files["Library/large"] = new byte[16 * 1024 * 1024];
        string cachePath = Path.Combine(dir.Path, "cache"), pointer = Path.Combine(dir.Path, "view", "cache-maintenance.json");
        var cache = new ValidationCacheIdentity("view", "cache", tree.Snapshot, "fixture", "unity-top-level-v1", 10_000_000);
        var identity = WindowsValidationProvider.CacheIdentity(cache);
        byte[] expected = Enumerable.Repeat((byte)42, 4096).ToArray();
        using (var store = new JournalDeltaStore(cachePath, identity, new ValidationCacheSnapshotTree(tree, tree.Snapshot, WindowsValidationProvider.CacheRoots)))
        {
            var file = store.Stat("Library/large")!;
            // Real changes create obsolete payload; identical private writes reuse it.
            for (int i = 0; i < 32; i++) { expected[0] = checked((byte)(11 + i)); store.Write(file.Id, 8192, expected); }
            Assert.Equal(4096, store.ObservePrivateUsage().LivePrivateBytes);
            Assert.Equal(31 * 4096, store.ObservePrivateUsage().ObsoletePayloadBytes);
        }
        Assert.Throws<IOException>(() => WindowsValidationProvider.CompactCacheFiles(cachePath, pointer, cache, "compact", 1_000_000, tree,
            point => { if (point == cut) throw new IOException("injected"); }));
        Assert.Equal("ValidationCacheMaintenancePending", Assert.Throws<WorkspaceException>(() =>
            WindowsValidationProvider.CompactCacheFiles(cachePath, pointer, cache, "different", 1_000_000, tree)).Code);
        var result = WindowsValidationProvider.CompactCacheFiles(cachePath, pointer, cache, "compact", 1_000_000, tree);
        Assert.True(result.Completed); Assert.NotNull(result.After); Assert.NotNull(result.Compaction);
        Assert.Equal(4096, result.After.LivePrivateBytes); Assert.Equal(0, result.After.ObsoletePayloadBytes);
        Assert.True(result.After.JournalBytes < result.Before.JournalBytes / 2);
        Assert.Equal(0, tree.ReadBytes);
        var json = JsonSerializer.SerializeToElement(new { action = "compact-cache", result }, ApiJson.Options);
        Assert.True(new ContractCatalog().Validate("urn:splinter:v1:validation-product-data", json).IsValid, json.GetRawText());
        using (var reopened = ValidationViewDeltaStore.OpenCacheReadOnly(cachePath, identity, tree))
        {
            byte[] actual = new byte[4096]; reopened.Read(reopened.Stat("Library/large")!.Id, 8192, actual);
            Assert.Equal(expected, actual);
        }
        var replay = WindowsValidationProvider.CompactCacheFiles(cachePath, pointer, cache, "compact", 1_000_000, tree);
        Assert.Equal(result.Compaction.AttemptId, replay.Compaction!.AttemptId);
        Assert.False(File.Exists(Path.Combine(cachePath, "delta.journal.previous")));
        Assert.False(File.Exists(Path.Combine(cachePath, "compaction-intent.json")));
    }

    [Fact]
    public void TooSmallOverlapBudgetDoesNotStrandCacheMaintenance()
    {
        using var dir = new TestDirectory(); var tree = new SnapshotOverlayTests.FakeSnapshotTree();
        tree.Files["Library/large"] = new byte[8192];
        string path = Path.Combine(dir.Path, "cache"), pointer = Path.Combine(dir.Path, "maintenance.json");
        var cache = new ValidationCacheIdentity("view", "cache", tree.Snapshot, "fixture", "unity-top-level-v1", 1_000_000);
        using (var store = new JournalDeltaStore(path, WindowsValidationProvider.CacheIdentity(cache),
                   new ValidationCacheSnapshotTree(tree, tree.Snapshot, WindowsValidationProvider.CacheRoots)))
            store.Write(store.Stat("Library/large")!.Id, 0, new byte[4096]);
        byte[] before = File.ReadAllBytes(Path.Combine(path, "delta.journal"));
        Assert.Equal("CompactionOverlapBudgetExceeded", Assert.Throws<WorkspaceException>(() =>
            WindowsValidationProvider.CompactCacheFiles(path, pointer, cache, "small", 1, tree)).Code);
        Assert.False(File.Exists(pointer)); Assert.Equal(before, File.ReadAllBytes(Path.Combine(path, "delta.journal")));
        Assert.True(WindowsValidationProvider.CompactCacheFiles(path, pointer, cache, "larger", 1_000_000, tree).Completed);
    }
}
