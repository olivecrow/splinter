using Splinter.Core;
using Splinter.SourceControl.Files;

namespace Splinter.Tests;

public sealed class PlasticLockIndexTests
{
    [Fact]
    public void LargeLockObservationHasNoOldEntryOrOutputQuotaAndFindsAncestorDescendantOverlap()
    {
        const int count = 16385; long bytes = 0;
        IEnumerable<string> Paths()
        {
            for (int index = 0; index < count; index++)
            {
                string row = $"repo|42|11111111-1111-1111-1111-111111111111|2026-11-01|main|1|main|1|Retained|user|workspace|/Assets/d{index:D6}/item.bin";
                bytes += row.Length; yield return PlasticWorkspaceClient.ParseLockRow(row);
            }
            yield return PlasticWorkspaceClient.ParseLockRow("repo|42|11111111-1111-1111-1111-111111111111|date|main|1|main|1|Locked|user|workspace|/ASSETS/d000001/item.bin");
        }
        using var locks = new PlasticLockIndex(Paths(), TestContext.Current.CancellationToken);
        Assert.True(bytes > 1024 * 1024); Assert.Equal(count, locks.Paths().LongCount());
        Assert.True(locks.Affects("assets/d000001/ITEM.bin")); Assert.True(locks.Affects("Assets/d016384"));
        Assert.True(locks.Affects("Assets/d000001/item.bin/child")); Assert.True(locks.Affects("Assets"));
        Assert.False(locks.Affects("Assets/d016385")); Assert.False(locks.Affects("Packages/manifest.json"));
    }
    [Fact]
    public void RootLockIsNotAnEmptyInventoryAndMalformedRowsRemainRejected()
    {
        using var locks = new PlasticLockIndex(["", "Assets/item.bin"]);
        Assert.True(locks.HasRootLock); Assert.True(locks.Affects("Packages/new.json"));
        Assert.Throws<WorkspaceException>(() => PlasticWorkspaceClient.ParseLockRow("Error: authentication failed"));
        Assert.Throws<WorkspaceException>(() => PlasticWorkspaceClient.ParseLockRow("repo|42|11111111-1111-1111-1111-111111111111|date|main|1|main|1|Unknown|user|workspace|/Assets/item.bin"));
    }
}
