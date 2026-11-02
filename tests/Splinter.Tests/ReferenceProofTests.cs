using System.Buffers.Binary;
using System.Security.Cryptography;
using Splinter.Core;
using Splinter.Platform.Windows;
using Splinter.Runtime;
using Splinter.SourceControl.Files;

namespace Splinter.Tests;

public sealed class ReferenceProofTests
{
    [Fact]
    public void BorrowedCatalogReaderKeepsOwnerAliveAndRejectsForeignRoots()
    {
        using var temp = new TestDirectory();
        using var pages = new PagedMetadataStore(temp.Path, true);
        var tree = new DirectoryMetadataTree(pages);
        var root = tree.Build([new(new("file", false, 1, 32, 0, 0, 1), Hash([1]))], TestContext.Current.CancellationToken); pages.Flush();
        var descriptor = new SnapshotCatalogDescriptor(1, new("", true, 0, 16, 0, 0, 0), root, 2, false);
        using (var catalog = new PagedSnapshotCatalog(pages, descriptor)) Assert.Equal(1, catalog.Find("file")!.Entry.Length);
        Assert.Equal(1, pages.Open(root).Root.Count);
        Assert.Throws<WorkspaceException>(() => new PagedSnapshotCatalog(pages, descriptor with { Children = root with { StoreId = new string('f', 24) } }));
        Assert.Equal(1, pages.Open(root).Root.Count);
    }

    [Fact(Timeout = 10000)]
    public void MetadataReadersRemainFencedSeeNewRootsAndFallBackAtBudget()
    {
        using var temp = new TestDirectory(); var pins = new WindowsJournalFilePins(); var ct = TestContext.Current.CancellationToken;
        var roots = new List<MetadataTreeRoot>();
        for (int i = 0; i < 5; i++)
        {
            ct.ThrowIfCancellationRequested();
            using var writer = new PagedMetadataStore(Path.Combine(temp.Path, i.ToString()), true, filePins: pins);
            roots.Add(writer.Set(writer.Empty, "key", [1])); writer.Flush();
        }
        using (var proofs = new ImmutableReadProofs(pins))
        {
            for (int i = 0; i < 5; i++)
            {
                string directory = Path.Combine(temp.Path, i.ToString());
                using var first = ImmutableReadProofs.OpenMetadata(directory, proofs);
                using var second = ImmutableReadProofs.OpenMetadata(directory, proofs);
                if (i < 4) Assert.Same(first.Pages, second.Pages); else Assert.NotSame(first.Pages, second.Pages);
                Assert.Equal(new byte[] { 1 }, first.Pages.Open(roots[i]).Find("key"));
            }
            string fixedDirectory = Path.Combine(temp.Path, "0");
            Assert.Throws<IOException>(() => { using var write = new FileStream(Path.Combine(fixedDirectory, "identity.bin"), FileMode.Open, FileAccess.Write, FileShare.ReadWrite); });
            Assert.Throws<IOException>(() => { using var exclusive = new FileStream(Path.Combine(fixedDirectory, "writer.lock"), FileMode.Open, FileAccess.ReadWrite, FileShare.None); });
            MetadataTreeRoot updated;
            using (var writer = new PagedMetadataStore(fixedDirectory, true, filePins: pins))
            { updated = writer.Set(roots[0], "key", [2]); writer.Flush(); }
            using var current = ImmutableReadProofs.OpenMetadata(fixedDirectory, proofs);
            Assert.Equal(new byte[] { 2 }, current.Pages.Open(updated).Find("key"));
            Assert.Equal(new byte[] { 1 }, current.Pages.Open(roots[0]).Find("key"));
        }
        using var released = new FileStream(Path.Combine(temp.Path, "0", "identity.bin"), FileMode.Open, FileAccess.Write, FileShare.None);
    }

    [Fact(Timeout = 10000)]
    public void CatalogProofSharesSubtreesAndMeasuresBytesWithoutAnotherWalk()
    {
        using var temp = new TestDirectory(); var ct = TestContext.Current.CancellationToken;
        var rows = Enumerable.Range(0, 31).Select(i => new DirectoryMetadataRecord(new($"f{i:D2}", false, i, 32, 0, 0, 1), Hash([(byte)i]))).ToArray();
        var root = new SnapshotEntry("", true, 0, 16, 0, 0, 0);
        var first = PagedSnapshotCatalog.Build(temp.Path, root, rows, ct: ct);
        SnapshotCatalogDescriptor second;
        using (var writer = new PagedMetadataStore(temp.Path, true))
        { var tree = new DirectoryMetadataTree(writer); var changed = tree.Set(first.Children, rows[15] with { Entry = rows[15].Entry with { Length = 99 } }); writer.Flush(); second = first with { Children = changed }; }
        using (var scope = new PagedMetadataStore.CatalogReadScope(temp.Path))
        {
            Assert.Equal(465, scope.Verify(first, ct).Bytes); long visited = scope.VisitedNodes;
            Assert.Equal(465, scope.Verify(first, ct).Bytes); Assert.Equal(visited, scope.VisitedNodes);
            Assert.Equal(549, scope.Verify(second, ct).Bytes); Assert.InRange(scope.VisitedNodes - visited, 1, 30);
            Assert.True(scope.ReusedSubtrees > 1);
            Assert.Throws<WorkspaceException>(() => scope.Verify(second with { Count = 1 }, ct));
            string container = Path.Combine(temp.Path, $"pages-{first.Children.Page!.Container:x16}.bin");
            Assert.Throws<IOException>(() => { using var forbidden = new FileStream(container, FileMode.Open, FileAccess.Write, FileShare.ReadWrite); });
        }
        string damaged = Path.Combine(temp.Path, $"pages-{first.Children.Page!.Container:x16}.bin");
        using (var write = new FileStream(damaged, FileMode.Open, FileAccess.Write, FileShare.ReadWrite))
        { write.Position = first.Children.Page.Offset + 40; write.WriteByte(0xff); }
        using var reopened = new PagedMetadataStore.CatalogReadScope(temp.Path);
        Assert.Throws<WorkspaceException>(() => reopened.Verify(first, ct));
    }

    [Fact(Timeout = 10000)]
    public void WritableContainerCannotSupplyReusableCatalogProof()
    {
        using var temp = new TestDirectory(); var ct = TestContext.Current.CancellationToken;
        using var writer = new PagedMetadataStore(temp.Path, true); var tree = new DirectoryMetadataTree(writer);
        var entries = tree.Build([new(new("dir", true, 0, 16, 0, 0, 1), null, tree.Build([new(new("file", false, 7, 32, 0, 0, 1), Hash([7]))], ct))], ct); writer.Flush();
        var descriptor = new SnapshotCatalogDescriptor(1, new("", true, 0, 16, 0, 0, 0), entries, 3, false);
        using var scope = new PagedMetadataStore.CatalogReadScope(temp.Path);
        Assert.Equal(7, scope.Verify(descriptor, ct).Bytes); long first = scope.VisitedNodes;
        Assert.Equal(7, scope.Verify(descriptor, ct).Bytes); Assert.Equal(first * 2, scope.VisitedNodes); Assert.Equal(0, scope.ReusedSubtrees);
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        Assert.Throws<OperationCanceledException>(() => scope.Verify(descriptor, cancelled.Token));
    }

    [Fact(Timeout = 10000)]
    public void PersistentDifferencePreservesChangedRemovedAndAddedRows()
    {
        using var temp = new TestDirectory(); var ct = TestContext.Current.CancellationToken;
        using var pages = new PagedMetadataStore(temp.Path, true);
        var first = pages.Build(Enumerable.Range(0, 31).Select(i => new MetadataEntry($"k{i:D2}", new byte[] { (byte)i })), ct);
        var second = pages.Set(pages.Remove(pages.Set(first, "k15", [99]), "k07"), "new", [88]);
        Assert.Equal(new[] { "k07", "k15" }, pages.EntriesNotIdentical(first, second, ct).Select(e => e.Key).Order());
        Assert.Equal(new[] { "k15", "new" }, pages.EntriesNotIdentical(second, first, ct).Select(e => e.Key).Order());
        Assert.Empty(pages.EntriesNotIdentical(first, first, ct));
    }

    [Fact(Timeout = 10000)]
    public void HistoricalCoverageRequiresSamePhysicalDescriptorForEveryRetainedFile()
    {
        using var temp = new TestDirectory(); var ct = TestContext.Current.CancellationToken;
        string metadata = Path.Combine(temp.Path, "metadata"), control = Path.Combine(temp.Path, "control");
        using var pages = new PagedMetadataStore(metadata, true);
        byte[] size = new byte[8]; BinaryPrimitives.WriteInt64LittleEndian(size, 1);
        string a = Hash([1]), b = Hash([2]);
        var retained = pages.Set(pages.Empty, a, size); var more = pages.Set(retained, b, size);
        using var contents = new PagedMetadataStore(Path.Combine(control, "files", "result-ranges"), true);
        var first = contents.Set(contents.Empty, a, [1]); var second = contents.Set(first, b, [2]);
        pages.Flush(); contents.Flush();
        var prior = new FileSourceManifest("owner", "digest", "snapshot", "identity", []) { Directory = metadata, RetainedPayloads = retained, ContentFiles = first };
        var current = prior with { RetainedPayloads = more, ContentFiles = second };
        Assert.True(FileWorkspaceProvider.ReferencesCoveredBy(prior, current, control, ct));
        var changed = contents.Set(second, a, [3]); contents.Flush();
        Assert.False(FileWorkspaceProvider.ReferencesCoveredBy(prior, current with { ContentFiles = changed }, control, ct));
        Assert.False(FileWorkspaceProvider.ReferencesCoveredBy(prior with { ContentFiles = null }, current, control, ct));
        Assert.False(FileWorkspaceProvider.ReferencesCoveredBy(current, prior, control, ct));
    }

    [Theory(Timeout = 10000)]
    [InlineData(false)] [InlineData(true)]
    public void CompositionProofRevalidatesOwnershipAndReusesOnlyFencedContent(bool rawChunk)
    {
        using var temp = new TestDirectory(); var ct = TestContext.Current.CancellationToken; var pins = new WindowsJournalFilePins();
        string home = Path.Combine(temp.Path, "home"), id = new('a', 24), control = Path.Combine(home, "records", id);
        var w = new WorkspaceRecord { Id = id, RequestKey = "proof", RequestDigest = "proof-request", SourceReference = "source", BaseReference = "basis", ProjectPath = temp.Path, Path = Path.Combine(home, "workspaces", id), CaptureIntent = new(new string('b', 24), 1, "capture", "capture") };
        Directory.CreateDirectory(control); var whole = new OwnedContentStore(home, pins); using var raw = new OwnedRangeContentStore(home, pins);
        string Add(byte[] bytes) { string hash = Hash(bytes); whole.Preserve(w, control, hash, bytes.Length, output => output.Write(bytes), ct); return hash; }
        string basis = Add("abcdefgh"u8.ToArray()), chunk = Hash("XY"u8.ToArray()), file = Hash("abXYefgh"u8.ToArray());
        if (rawChunk)
        {
            string directory = Path.Combine(control, "captures", w.CaptureIntent.Id); Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "segment.pin"); byte[] segment = new byte[256]; "XY"u8.CopyTo(segment.AsSpan(128)); File.WriteAllBytes(path, segment);
            raw.Preserve(w, control, chunk, 2, new(path, pins.Identify(path), 128, 256, w.CaptureIntent.Id, new string('c', 64), Hash(segment)), ct);
        }
        else _ = Add("XY"u8.ToArray());
        var contents = new ResultFileContentStore(home, control, w, pins, raw);
        contents.Preserve(file, 8, basis, 8, 8, [new(2, 2, chunk, rawChunk)], ct);
        using (var proof = contents.BeginVerification())
        {
            proof.Verify(file, 8, ct); proof.Verify(file, 8, ct);
            Assert.Equal(1, proof.FullFileHashes); Assert.Equal(1, proof.CompositionReuses); Assert.Equal(2, proof.PayloadHashes);
            Assert.Throws<WorkspaceException>(() => proof.Verify(file, 9, ct));
            // Even with retained byte proofs, an ownership transition is not
            // accepted. Canonical native fences preserve the payload even if
            // the owned alias can be unlinked independently.
            whole.ReleaseReference(w, control, basis, 8, _ => false, ct);
            Assert.Throws<WorkspaceException>(() => proof.Verify(file, 8, ct));
        }
    }

    [Fact(Timeout = 10000)]
    public void ProofBudgetFallsBackAndDoesNotTrustChangedUnfencedBytes()
    {
        using var temp = new TestDirectory(); var ct = TestContext.Current.CancellationToken; var pins = new WindowsJournalFilePins();
        using var proofs = new ImmutableReadProofs(pins); string last = "", identity = "";
        for (int i = 0; i < 65; i++)
        {
            last = Path.Combine(temp.Path, i.ToString()); File.WriteAllBytes(last, [1]); identity = pins.Identify(last);
            using var input = new FileStream(last, FileMode.Open, FileAccess.Read, FileShare.Read);
            proofs.Verify(input, identity, 0, 1, Hash([1]), ct);
        }
        Assert.Equal(65, proofs.HashAdmissions); File.WriteAllBytes(last, [2]);
        using var changed = new FileStream(last, FileMode.Open, FileAccess.Read, FileShare.Read);
        Assert.Throws<WorkspaceException>(() => proofs.Verify(changed, identity, 0, 1, Hash([1]), ct));
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        Assert.Throws<OperationCanceledException>(() => proofs.Verify(changed, identity, 0, 1, Hash([2]), cancelled.Token));
    }
    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}
