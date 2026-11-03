namespace Splinter.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class StorageBudgetIsolation
{
    public const string Name = "Process-wide storage scratch exhaustion";
}

// This deliberately reserves the entire process-wide scratch budget. Ordinary
// packing tests remain parallel; no other sort/spool may own memory here.
[Collection(StorageBudgetIsolation.Name)]
public sealed class JournalPackingScratchPressureTests
{
    [Theory(Timeout = 10000)]
    [InlineData(false)] [InlineData(true)]
    public void SparsePackedGarbageUsesDiskFallbackAndRecoversPendingForce(bool interrupt) =>
        JournalPartialSegmentPackingTests.VerifySparsePackedGarbage(interrupt, scratchPressure: true, TestContext.Current.CancellationToken);
}
