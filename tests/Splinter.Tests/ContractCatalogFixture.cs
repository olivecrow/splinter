using Splinter.Protocol;

namespace Splinter.Tests;

// Embedded schemas and command lists are immutable. This fixture is scoped to
// one sequential test class, rather than rebuilding every schema for each case.
public sealed class ContractCatalogFixture
{
    public ContractCatalog Product { get; } = new();
    public ContractCatalog Development { get; } = new(development: true);
}
