using System.Reflection;
using Splinter.Core;
using Splinter.Runtime;

namespace Splinter.Tests;

public sealed class PagedMetadataVerificationTests
{
    [Theory]
    [InlineData("order")]
    [InlineData("duplicate")]
    [InlineData("cycle")]
    [InlineData("forward-child")]
    public void CompleteVerificationRejectsChecksummedMalformedTreeShape(string kind)
    {
        using var root = new TestDirectory(); string directory = Path.Combine(root.Path, "pages"); MetadataTreeRoot tree;
        using (var store = new PagedMetadataStore(directory, true))
        {
            var leaf = store.Set(store.Empty, "z", new byte[] { 7 });
            var child = leaf.Page!;
            if (kind is "cycle" or "forward-child")
            {
                var output = (FileStream)typeof(PagedMetadataStore).GetField("output", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(store)!;
                child = child with { Offset = output.Length + (kind == "cycle" ? 0 : 1024) };
            }
            Type nodeType = typeof(PagedMetadataStore).GetNestedType("Node", BindingFlags.NonPublic)!;
            object node = Activator.CreateInstance(nodeType, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null,
                [kind == "duplicate" ? "z" : "a", Array.Empty<byte>(), child, null, null], null)!;
            var reference = (MetadataPageReference)typeof(PagedMetadataStore).GetMethod("WriteNode", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(store, [node, null])!;
            tree = new(1, store.StoreId, reference); store.Flush();
        }
        using var read = new PagedMetadataStore(directory);
        Assert.Equal("MetadataStoreInvalid", Assert.Throws<WorkspaceException>(() => read.Open(tree).Verify(TestContext.Current.CancellationToken)).Code);
    }
    [Fact]
    public void LookupRankAndCursorConformanceAreIndependentOfTheLinearVerifier()
    {
        using var root = new TestDirectory(); using var store = new PagedMetadataStore(Path.Combine(root.Path, "pages"), true); var ct = TestContext.Current.CancellationToken;
        const int count = 4096;
        var first = store.Build(Enumerable.Range(0, count).Select(index => new MetadataEntry(index.ToString("d8"), BitConverter.GetBytes(index))), ct);
        var changed = first;
        for (int index = 0; index < count; index += 7) changed = store.Remove(changed, index.ToString("d8"));
        store.Flush(); store.Open(first).Verify(ct); store.Open(changed).Verify(ct);
        long rank = 0;
        foreach (var entry in store.Open(changed).Enumerate())
        {
            var found = store.Open(changed).FindEntry(entry.Key)!; Assert.Equal(entry.Key, found.Key); Assert.Equal(entry.Value.ToArray(), found.Value.ToArray());
            Assert.Equal(rank, store.Rank(changed, entry.Key)); Assert.Equal(entry.Key, store.EntryAt(changed, rank++).Key);
        }
        Assert.Equal(changed.Count, rank);
        for (int index = 0; index < count; index += 7)
        { Assert.Null(store.Open(changed).FindEntry(index.ToString("d8"))); Assert.Equal(BitConverter.GetBytes(index), store.Open(first).Find(index.ToString("d8"))); }
    }
}
