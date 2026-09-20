using Splinter.Core;

namespace Splinter.Tests;

public sealed class PlanGraphTests
{
    [Fact]
    public void IndependentTasksRunTogetherEvenWhenDependentTaskIsDeclaredFirst()
    {
        var result = PlanGraph.Validate([
            new("integration", ["code", "assets"]), new("code", []), new("assets", [])
        ]);
        Assert.True(result.IsValid);
        Assert.Equal(["code", "assets"], result.ExecutionLayers[0]);
        Assert.Equal(["integration"], result.ExecutionLayers[1]);
    }

    [Fact]
    public void CyclesDoNotProduceAnExecutablePartialGraph()
    {
        var result = PlanGraph.Validate([new("independent", []), new("a", ["b"]), new("b", ["a"])]);
        Assert.Contains(result.Issues, issue => issue.Code == "DependencyCycle");
        Assert.Empty(result.ExecutionLayers);
    }

    [Fact]
    public void DuplicateIdsAndMissingDependenciesAreReportedBeforeScheduling()
    {
        var result = PlanGraph.Validate([new("a", ["missing"]), new("a", [])]);
        Assert.Contains(result.Issues, issue => issue.Code == "DuplicateTaskId");
        Assert.Contains(result.Issues, issue => issue.Code == "MissingDependency");
        Assert.Empty(result.ExecutionLayers);
    }
}
