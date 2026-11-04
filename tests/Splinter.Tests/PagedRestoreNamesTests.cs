using Splinter.Core;
using Splinter.Platform.Windows;
using Splinter.Runtime;

namespace Splinter.Tests;

public sealed class PagedRestoreNamesTests
{
    [Fact]
    public void BulkNamesReopenWithTheSameCompleteRecordsAndRootChangesRevokeDerivedPrefixes()
    {
        using var directory = new TestDirectory(); var ct = TestContext.Current.CancellationToken;
        string path = Path.Combine(directory.Path, "names"); MetadataTreeRoot root, missingParent; byte[] first, second, parent;
        using (var writer = new PagedMetadataStore(path, writable: true))
        {
            using var incremental = new ManagedPagedDirectoryRestore.RestoreNames(writer);
            using var bulk = new ManagedPagedDirectoryRestore.RestoreNames(writer, directory.Path);
            first = incremental.Intern("deep/packages/editor/first"); second = incremental.Intern("deep/packages/editor/second");
            Assert.Equal(first, bulk.Intern("deep/packages/editor/first")); Assert.Equal(second, bulk.Intern("deep/packages/editor/second"));
            _ = bulk.Intern("deep/packages/editor/first"); bulk.Complete(ct); root = bulk.Root;
            Assert.Equal(writer.Open(incremental.Root).Enumerate().Select(Describe), writer.Open(root).Enumerate().Select(Describe));
            parent = incremental.Intern("deep/packages/editor");
            missingParent = writer.Remove(root, Convert.ToHexStringLower(parent)); writer.Flush();
        }
        using var reader = new PagedMetadataStore(path);
        using var names = new ManagedPagedDirectoryRestore.RestoreNames(reader) { Root = root };
        Assert.Equal("deep/packages/editor/first", names.Resolve(first)); Assert.Equal("deep/packages/editor/second", names.Resolve(second));
        Assert.Equal("deep/packages/editor", names.Resolve(parent));
        Assert.Equal("deep/packages/editor/first", names.Resolve(first));
        Assert.NotNull(reader.ImmutableReadProofEpoch);
        names.Root = missingParent;
        Assert.Equal("GeneratedRestoreConflict", Assert.Throws<WorkspaceException>(() => names.Resolve(first)).Code);
        names.Root = root; Assert.Equal("deep/packages/editor/first", names.Resolve(first));

        static string Describe(MetadataEntry entry) => entry.Key + ":" + Convert.ToHexString(entry.Value.Span);
    }

    [Fact]
    public void WriteSharedNameReadsNeverReuseDerivedProofAndRejectNativeContainerCorruption()
    {
        if (!OperatingSystem.IsWindows()) Assert.Skip("Native metadata sharing requires Windows.");
        using var directory = new TestDirectory(); string path = Path.Combine(directory.Path, "names"); MetadataTreeRoot root; byte[] first, second;
        using (var writer = new PagedMetadataStore(path, writable: true))
        {
            using var names = new ManagedPagedDirectoryRestore.RestoreNames(writer, directory.Path);
            first = names.Intern("parent/first"); second = names.Intern("parent/second");
            names.Complete(TestContext.Current.CancellationToken); root = names.Root; writer.Flush();
        }
        using var mutation = new FileStream(Path.Combine(path, "pages-0000000000000000.bin"), FileMode.Open, FileAccess.ReadWrite, FileShare.Read | FileShare.Delete);
        using var reader = new PagedMetadataStore(path);
        using var resolved = new ManagedPagedDirectoryRestore.RestoreNames(reader) { Root = root };
        Assert.Equal("parent/first", resolved.Resolve(first)); Assert.Null(reader.ImmutableReadProofEpoch);
        mutation.Position = 0; mutation.WriteByte(0); mutation.Flush(true);
        Assert.Throws<WorkspaceException>(() => resolved.Resolve(second));
    }
}
