using Splinter.Core;
using Splinter.Platform.Windows;
using Splinter.Runtime;

namespace Splinter.Tests;

public sealed class SnapshotTreeStreamingTests
{
    [Theory]
    [InlineData("disposable")]
    [InlineData("cache")]
    public void SingleBasisWrappersForwardTheLazyCursorAndTakeTwoDisposesItEarly(string wrapperKind)
    {
        string? branch = wrapperKind == "cache" ? "Library" : null;
        var basis = new LazyTree("source", branch);
        using var disposable = wrapperKind == "disposable" ? new WorkspaceSnapshotBasis.DisposableSnapshotTree(basis) : null;
        ISnapshotTree wrapper = disposable is null ? new CacheSnapshotTree(basis, basis.Snapshot, ["Library"]) : disposable;
        var cursor = wrapper.EnumerateChildren(branch ?? ""); Assert.Equal(0, basis.Yielded);
        var entries = cursor.Take(2).ToArray();
        Assert.Equal(2, entries.Length); Assert.Equal(2, basis.Yielded); Assert.Equal(1, basis.Enumerations); Assert.Equal(1, basis.DisposedCursors);
        Assert.Equal(0, basis.ListCalls); Assert.Equal(0, basis.PayloadReads);
        Assert.Equal(Prefix(branch, "entry-00000000"), entries[0].Path); Assert.Equal(Prefix(branch, "entry-00000001"), entries[1].Path);
        wrapper.VerifyReadBoundary(); Assert.Equal(1, basis.Boundaries);
        if (wrapperKind == "cache")
        {
            Assert.Null(wrapper.Stat("Assets"));
            Assert.Throws<DirectoryNotFoundException>(() => wrapper.EnumerateChildren("Assets"));
        }
    }

    [Fact]
    public void GraftedRootAndGeneratedSubtreeStreamIndependentlyWithoutMaterializingEitherLargeDirectory()
    {
        var source = new LazyTree("source"); var generated = new LazyTree("generated");
        var graft = Graft(source, generated);
        var root = graft.EnumerateChildren(""); Assert.Equal(0, source.Yielded); Assert.Equal(0, generated.Yielded);
        var sourceEntries = root.Take(2).ToArray();
        Assert.Equal(2, sourceEntries.Length); Assert.Equal(2, source.Yielded); Assert.Equal(1, source.DisposedCursors); Assert.Equal(0, generated.Yielded);
        var nested = graft.EnumerateChildren("Library"); Assert.Equal(0, generated.Yielded);
        var generatedEntries = nested.Take(2).ToArray();
        Assert.Equal(new[] { "Library/entry-00000000", "Library/entry-00000001" }, generatedEntries.Select(value => value.Path));
        Assert.Equal(2, generated.Yielded); Assert.Equal(1, generated.DisposedCursors); Assert.Equal(2, source.Yielded);
        Assert.Equal(0, source.ListCalls); Assert.Equal(0, generated.ListCalls); Assert.Equal(0, source.PayloadReads + generated.PayloadReads);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DisposableGraftChecksBothFullBoundariesAndPropagatesEitherProvidersRefusal(bool failGenerated)
    {
        var source = new LazyTree("source"); var generated = new LazyTree("generated");
        using var wrapped = new WorkspaceSnapshotBasis.DisposableSnapshotTree(Graft(source, generated));
        wrapped.VerifyReadBoundary(); Assert.Equal(1, source.Boundaries); Assert.Equal(1, generated.Boundaries);
        var expected = new WorkspaceException("FixedBasisChanged", "The full proof no longer matches its fixed basis.");
        (failGenerated ? generated : source).BoundaryFailure = expected;
        Assert.Same(expected, Assert.Throws<WorkspaceException>(() => wrapped.VerifyReadBoundary()));
        Assert.Equal(2, source.Boundaries); Assert.Equal(failGenerated ? 2 : 1, generated.Boundaries);
    }

    private static GraftedSnapshotTree Graft(LazyTree source, LazyTree generated) => new(source, generated,
        new("Library", new("generated", "fixture-project", "Library", generated.Snapshot, "fixture-receipts", "fixture-owner", new string('a', 64))));
    private static string Prefix(string? branch, string child) => branch is null ? child : branch + "/" + child;
    private sealed class LazyTree(string id, string? branch = null) : ISnapshotTree, IDisposable
    {
        private const int Count = 1_048_576;
        public SnapshotReference Snapshot { get; } = new(id, "streaming-fixture", "fixture-source", "fixture-basis", DateTimeOffset.UnixEpoch, true, true, "fixture-only");
        public int Yielded { get; private set; }
        public int Enumerations { get; private set; }
        public int DisposedCursors { get; private set; }
        public int ListCalls { get; private set; }
        public int PayloadReads { get; private set; }
        public int Boundaries { get; private set; }
        public WorkspaceException? BoundaryFailure { get; set; }
        public void EnsureAvailable() { }
        public void VerifyReadBoundary() { Boundaries++; if (BoundaryFailure is { } failure) throw failure; }
        public SnapshotEntry? Stat(string path) => path.Length == 0 || branch is not null && path == branch ?
            new(path, true, 0, 16, 0, 0, 0) : null;
        public IReadOnlyList<SnapshotEntry> List(string path)
        { ListCalls++; throw new InvalidOperationException("The streaming wrapper must not call List or materialize this directory."); }
        public IEnumerable<SnapshotEntry> EnumerateChildren(string path)
        {
            if (path != (branch ?? "")) throw new DirectoryNotFoundException(path);
            Enumerations++;
            try
            {
                for (int index = 0; index < Count; index++)
                {
                    Yielded++; yield return new(Prefix(branch, "entry-" + index.ToString("D8")), false, 0, 32, 0, 0, 0);
                }
            }
            finally { DisposedCursors++; }
        }
        public int Read(string path, long offset, Span<byte> output)
        { PayloadReads++; throw new InvalidOperationException("Directory enumeration must not open payloads."); }
        public void Dispose() { }
    }
}
