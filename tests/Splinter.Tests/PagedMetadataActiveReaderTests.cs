using Splinter.Core;
using Splinter.Platform.Windows;
using Splinter.Runtime;

namespace Splinter.Tests;

public sealed class PagedMetadataActiveReaderTests
{
    [Fact]
    public void AnActiveWriterMayAppendWhileTheReaderReusesOneUncachedHandleForItsFrozenRoot()
    {
        RequireWindows(); using var files = new TestDirectory(); var ct = TestContext.Current.CancellationToken;
        string directory = Path.Combine(files.Path, "metadata"); var native = new WindowsJournalFilePins(); var observed = new CountingPins(native);
        using var writer = new PagedMetadataStore(directory, true, filePins: native);
        var fixedRoot = writer.Build([new("first", "old"u8.ToArray())], ct); writer.Flush();
        using var reader = new PagedMetadataStore(directory, filePins: observed);
        Assert.Equal("old"u8.ToArray(), reader.Open(fixedRoot).Find("first"));
        int verified = observed.Verifications; Assert.Equal(1, verified);
        var current = writer.Set(fixedRoot, "second", "new"u8.ToArray());
        long flushes = writer.DurableFlushes;
        writer.FlushForRead();
        Assert.Equal(flushes, writer.DurableFlushes);
        Assert.Equal("old"u8.ToArray(), reader.Open(fixedRoot).Find("first")); Assert.Null(reader.Open(fixedRoot).Find("second"));
        Assert.Equal("new"u8.ToArray(), reader.Open(current).Find("second")); Assert.Equal(verified, observed.Verifications);
        writer.Flush();
        Assert.Equal(flushes + 1, writer.DurableFlushes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReusingAWriteSharedHandleStillRejectsSameIdentitySameLengthMetadataCorruption(bool containerHeader)
    {
        RequireWindows(); using var files = new TestDirectory(); var ct = TestContext.Current.CancellationToken;
        string directory = Path.Combine(files.Path, "metadata"); var pins = new WindowsJournalFilePins(); MetadataTreeRoot root;
        using (var writer = new PagedMetadataStore(directory, true, filePins: pins))
        { root = writer.Build([new("key", "value"u8.ToArray())], ct); writer.Flush(); }
        string path = Path.Combine(directory, "pages-0000000000000000.bin"); string identity = pins.Identify(path); long length = new FileInfo(path).Length;
        using var mutable = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read | FileShare.Delete);
        using var reader = new PagedMetadataStore(directory, filePins: pins);
        Assert.Equal("value"u8.ToArray(), reader.Open(root).Find("key"));
        long offset = containerHeader ? 0 : root.Page!.Offset + root.Page.Bytes - 1; mutable.Position = offset; int previous = mutable.ReadByte();
        mutable.Position = offset; mutable.WriteByte((byte)(previous ^ 1)); mutable.Flush(true);
        Assert.Equal(length, mutable.Length); Assert.Equal(identity, pins.Identify(path));
        var failure = Assert.Throws<WorkspaceException>(() => reader.Open(root).Find("key"));
        Assert.Contains(containerHeader ? "container" : "checksum", failure.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TheResourceSizedHandleBoundEvictsAndDisposalReleasesAllRemainingFences()
    {
        RequireWindows(); using var files = new TestDirectory(); var ct = TestContext.Current.CancellationToken;
        string directory = Path.Combine(files.Path, "metadata"); var roots = new List<MetadataTreeRoot>();
        using (var writer = new PagedMetadataStore(directory, true))
        { for (int i = 0; i <= PagedMetadataStore.MaximumContainerReaders; i++) roots.Add(writer.Build([new("key", new byte[] { (byte)i })], ct)); writer.Flush(); }
        string first = Path.Combine(directory, "pages-0000000000000000.bin"); byte[] source = File.ReadAllBytes(first);
        int header = checked((int)roots[0].Page!.Offset);
        // Small valid leaf containers exercise physical handle eviction without
        // manufacturing a GiB of unrelated metadata. Their logical identity is
        // independent of the physical container/offset.
        for (int i = 1; i < roots.Count; i++)
        {
            var page = roots[i].Page!; string path = Path.Combine(directory, $"pages-{i:x16}.bin");
            using var output = File.Create(path); output.Write(source.AsSpan(0, header)); output.Write(source.AsSpan(checked((int)page.Offset), page.Bytes));
            roots[i] = roots[i] with { Page = page with { Container = i, Offset = header } };
        }
        var writers = new List<FileStream>();
        try
        {
            for (int i = 0; i < roots.Count; i++) writers.Add(new(Path.Combine(directory, $"pages-{i:x16}.bin"), FileMode.Open, FileAccess.ReadWrite, FileShare.Read | FileShare.Delete));
            using var reader = new PagedMetadataStore(directory);
            for (int i = 0; i < roots.Count; i++) Assert.Equal(new byte[] { (byte)i }, reader.Open(roots[i]).Find("key"));
            File.Move(first, first + ".moved"); // Exceeding the handle budget evicted the first.
            string retained = Path.Combine(directory, "pages-0000000000000001.bin");
            Assert.Throws<IOException>(() => File.Move(retained, retained + ".moved"));
            reader.Dispose();
            for (int i = 1; i < roots.Count; i++)
            {
                string path = Path.Combine(directory, $"pages-{i:x16}.bin"); File.Move(path, path + ".moved");
            }
            using var exclusive = new FileStream(Path.Combine(directory, "writer.lock"), FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            Assert.Equal(0, exclusive.Length);
        }
        finally { foreach (var writer in writers) writer.Dispose(); }
    }

    private sealed class CountingPins(WindowsJournalFilePins native) : IJournalFilePins
    {
        public int Verifications;
        public string Identify(string path) => native.Identify(path);
        public string IdentifyOpened(FileStream input) => native.IdentifyOpened(input);
        public void Verify(FileStream input, string expectedIdentity) { Verifications++; native.Verify(input, expectedIdentity); }
        public void Pin(string source, string destination, string expectedIdentity) => native.Pin(source, destination, expectedIdentity);
        public bool DetachBeforeTruncate(string path, long length) => native.DetachBeforeTruncate(path, length);
    }
    private static void RequireWindows()
    { if (!OperatingSystem.IsWindows()) Assert.Skip("Active native write sharing and exact file/name fences require Windows."); }
}
