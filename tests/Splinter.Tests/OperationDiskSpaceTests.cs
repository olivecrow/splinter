using Splinter.Runtime;

namespace Splinter.Tests;

public sealed class OperationDiskSpaceTests
{
    [Fact]
    public void SpaceRequirementUsesAdditionalPhysicalAllocationAndKnownControlInsteadOfAProjectQuota()
    {
        long large = 17L * 1024 * 1024 * 1024;
        var initial = OperationDiskSpace.Calculate("publish", large, 0, 512, large + 512, "fixture");
        Assert.Equal(large, initial.AdditionalPayloadBytes); Assert.Equal(large + 512, initial.MinimumRequiredBytes);
        var resumed = OperationDiskSpace.Calculate("publish", large, large, 512, 1024, "fixture");
        Assert.Equal(0, resumed.AdditionalPayloadBytes); Assert.Equal(512, resumed.MinimumRequiredBytes);
        Assert.Equal(0, OperationDiskSpace.Calculate("pin", 0, 0, 0, 0, "fixture").MinimumRequiredBytes);
        Assert.Equal(512, OperationDiskSpace.Calculate("publish", large, large + 4096, 512, 512, "fixture").MinimumRequiredBytes);
    }
}
