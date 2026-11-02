using System.Diagnostics;
using Splinter.Core;
using Splinter.Platform.Windows;
using Splinter.Runtime;

namespace Splinter.Tests;

public sealed class CaptureBatchingTests
{
    [Fact]
    public void MetadataFlushSharesHeaderAndOnlyReusesItsOwnDurableWatermark()
    {
        using var temp = new TestDirectory(); MetadataTreeRoot root;
        int acknowledgements = 0;
        using (var pages = new PagedMetadataStore(temp.Path, true, stage => { if (stage == "metadata.pages-durable") acknowledgements++; }))
        {
            Assert.Equal(0, pages.DurableFlushes);
            root = pages.Set(pages.Empty, "key", [1]); Assert.Equal(0, pages.DurableFlushes);
            pages.Flush(); Assert.Equal(1, pages.DurableFlushes);
            root = pages.Set(root, "key", [1]); pages.Flush(); Assert.Equal(1, pages.DurableFlushes);
            root = pages.Set(root, "key", [2]); pages.Flush(); Assert.Equal(2, pages.DurableFlushes);
            Assert.Equal(3, acknowledgements);
        }
        using var reopened = new PagedMetadataStore(temp.Path, true);
        Assert.Equal(new byte[] { 2 }, reopened.Open(root).Find("key"));
        reopened.Flush(); Assert.Equal(1, reopened.DurableFlushes);
        reopened.Flush(); Assert.Equal(1, reopened.DurableFlushes);
    }

    [Fact]
    public void OwnedHeaderPrefixCanResumeBeforeFirstRootAcknowledgement()
    {
        using var temp = new TestDirectory(); var pins = new WindowsJournalFilePins(); string identity;
        using (var initial = new PagedMetadataStore(temp.Path, true, filePins: pins))
        { identity = initial.StoreId; Assert.Equal(0, initial.DurableFlushes); }
        string container = Directory.GetFiles(temp.Path, "pages-*.bin").Single();
        using (var interrupted = new FileStream(container, FileMode.Open, FileAccess.Write, FileShare.Read))
        { interrupted.SetLength(16); interrupted.Flush(true); }
        MetadataTreeRoot root;
        using (var resumed = new PagedMetadataStore(temp.Path, true, filePins: pins))
        {
            Assert.Equal(identity, resumed.StoreId); root = resumed.Set(resumed.Empty, "key", [42]);
            resumed.Flush(); Assert.Equal(1, resumed.DurableFlushes);
        }
        using var reader = new PagedMetadataStore(temp.Path, filePins: pins);
        Assert.Equal(new byte[] { 42 }, reader.Open(root).Find("key"));
    }

    [Fact]
    public void InterruptedPageNeverChangesPreviouslyAcknowledgedRoot()
    {
        using var temp = new TestDirectory(); MetadataTreeRoot root; bool interrupt = false;
        using (var pages = new PagedMetadataStore(temp.Path, true, stage =>
        { if (interrupt && stage == "metadata.page-header-written") throw new IOException("injected"); }))
        {
            root = pages.Set(pages.Empty, "key", [1]); pages.Flush(); interrupt = true;
            Assert.Throws<IOException>(() => pages.Set(root, "key", [2]));
        }
        using var resumed = new PagedMetadataStore(temp.Path, true);
        Assert.Equal(new byte[] { 1 }, resumed.Open(root).Find("key"));
        var final = resumed.Set(root, "key", [3]); resumed.Flush();
        Assert.Equal(new byte[] { 3 }, resumed.Open(final).Find("key"));
    }

    [Fact(Timeout = 10000)]
    public async Task RequestPathGuardPinsAncestorsButRechecksMutableLeaves()
    {
        using var temp = new TestDirectory(); var ct = TestContext.Current.CancellationToken;
        string directory = Path.Combine(temp.Path, "metadata"), target = Path.Combine(temp.Path, "target"), link = Path.Combine(directory, "leaf");
        using (var pages = new PagedMetadataStore(directory, true)) pages.Flush();
        Directory.CreateDirectory(target);
        using (var guard = new WindowsJournalFilePins().PinMetadataPaths(directory))
        {
            Assert.NotNull(guard);
            Assert.True(guard.TryEnsurePlainPath(directory));
            Assert.True(guard.TryEnsurePlainPath(link));
            Assert.True(guard.TryEnsurePlainPath(Path.Combine(temp.Path, "catalog.json")));
            Assert.False(guard.TryEnsurePlainPath(Path.Combine(target, "file")));
            Assert.False(guard.TryEnsurePlainPath(Path.Combine(directory.ToUpperInvariant(), "file")));
            Assert.Throws<IOException>(() => Directory.Move(directory, directory + "-moved"));
            Assert.Throws<IOException>(() => File.Delete(Path.Combine(directory, "identity.bin")));
            var start = new ProcessStartInfo("cmd.exe") { UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardError = true, RedirectStandardOutput = true, Arguments = $"/d /c mklink /J \"{link}\" \"{target}\"" };
            using var process = Process.Start(start)!; await process.WaitForExitAsync(ct); Assert.Equal(0, process.ExitCode);
            try { Assert.Equal("UnsafePath", Assert.Throws<WorkspaceException>(() => guard.TryEnsurePlainPath(link)).Code); }
            finally { Directory.Delete(link); }
        }
        Directory.Move(directory, directory + "-moved");
    }

    [Fact]
    public void MissingNativePathProofFallsBackWithoutCreatingAnOwnershipMarker()
    {
        using var temp = new TestDirectory(); var pins = new WindowsJournalFilePins();
        string directory = Path.Combine(temp.Path, "metadata"); Directory.CreateDirectory(directory);
        Assert.Null(pins.PinMetadataPaths(directory)); Assert.Empty(Directory.EnumerateFileSystemEntries(directory));
        // Default interface implementations cannot accidentally claim a proof.
        Assert.Null(((IJournalFilePins)new NoPathProofPins()).PinMetadataPaths(directory));
    }
    private sealed class NoPathProofPins : IJournalFilePins
    {
        public string Identify(string path) => throw new NotSupportedException();
        public void Verify(FileStream input, string expectedIdentity) => throw new NotSupportedException();
        public void Pin(string source, string destination, string expectedIdentity) => throw new NotSupportedException();
        public bool DetachBeforeTruncate(string path, long length) => throw new NotSupportedException();
    }
}
