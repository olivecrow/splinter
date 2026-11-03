using System.Security.Cryptography;
using Splinter.Core;
using Splinter.Platform.Windows;
using Splinter.Runtime;

namespace Splinter.Tests;

public sealed class OwnedContentStoreTests
{
    [Fact(Timeout = 10000)]
    public void BoundedReferencesAndAdoptionReuseSixteenProofsAndRevalidateTheNextOperation()
    {
        RequireWindows(); using var files = new TestDirectory(); var pins = new WindowsJournalFilePins(); var ct = TestContext.Current.CancellationToken;
        string home = Path.Combine(files.Path, "home"); var a = Workspace(home, 'a'); var b = Workspace(home, 'b'); int proofs = 0;
        var content = new OwnedContentStore(home, pins, point => { if (point == "content-existing-proof-admitted") proofs++; });
        byte[][] payloads = Enumerable.Range(0, 16).Select(i => Enumerable.Repeat((byte)i, 32).ToArray()).ToArray();
        foreach (var bytes in payloads) content.Preserve(a, Control(home, a), Hash(bytes), bytes.Length, output => output.Write(bytes), ct);
        var requests = Enumerable.Range(0, 32).Select(i => new OwnedContentStore.ExistingReference(Hash(payloads[i % 16]), 32)).ToArray();
        Assert.All(content.TryReferenceExistingBatch(b, Control(home, b), requests, ct), Assert.True); Assert.Equal(16, proofs);
        Assert.All(content.TryReferenceExistingBatch(b, Control(home, b), requests, ct), Assert.True); Assert.Equal(32, proofs);
        using (var scope = content.BeginExistingReferenceScope(b, Control(home, b)))
        {
            Assert.All(scope.TryReferenceBatch(requests, ct), Assert.True); Assert.Equal(48, proofs);
            for (int repeat = 0; repeat < 2; repeat++) Assert.All(scope.TryReferenceBatch(requests, ct), Assert.True);
            Assert.Equal(48, proofs);
            Assert.ThrowsAny<IOException>(() => new FileStream(Alias(home, b, requests[0].Hash), FileMode.Open, FileAccess.Read, FileShare.None));
        }
        Assert.All(content.TryReferenceExistingBatch(b, Control(home, b), requests, ct), Assert.True); Assert.Equal(64, proofs);
        using var reader = new OwnedCapturedContentReader(home, Control(home, b), b, pins);
        for (int operation = 1; operation <= 2; operation++)
        {
            using (reader.BeginAdoptionScope())
            {
                foreach (var request in requests) Assert.Equal(32, reader.GetLength(request.Hash, false));
                Assert.Equal(16 * operation, reader.ProofAdmissions);
                Assert.ThrowsAny<IOException>(() => new FileStream(Alias(home, b, requests[0].Hash), FileMode.Open, FileAccess.Read, FileShare.None));
            }
            using var released = new FileStream(Alias(home, b, requests[0].Hash), FileMode.Open, FileAccess.Read, FileShare.None);
        }
        Assert.Throws<WorkspaceException>(() => content.TryReferenceExistingBatch(b, Control(home, b), [requests[0], requests[0] with { Length = 31 }], ct));
        content.ReleaseReference(b, Control(home, b), requests[0].Hash, 32, _ => false, ct);
        // The second distinct reference must still exist in the final catalog.
        using var retained = content.Open(b, Control(home, b), requests[1].Hash, 32, ct);
        Assert.Equal(payloads[1], ReadAll(retained));
    }

    [Fact(Timeout = 10000)]
    public async Task PinnedReaderReleasesCatalogLockBeforeByteVerificationAndKeepsNativeFence()
    {
        RequireWindows(); using var files = new TestDirectory(); var pins = new WindowsJournalFilePins(); var ct = TestContext.Current.CancellationToken;
        string home = Path.Combine(files.Path, "home"); var w = Workspace(home, 'a'); var content = new OwnedContentStore(home, pins);
        byte[] first = "first payload"u8.ToArray(), second = "second payload"u8.ToArray();
        foreach (var bytes in new[] { first, second }) content.Preserve(w, Control(home, w), Hash(bytes), bytes.Length, output => output.Write(bytes), ct);
        using var entered = new ManualResetEventSlim(); using var release = new ManualResetEventSlim();
        var paused = new OwnedContentStore(home, pins, point => { if (point == "content-reader-pinned") { entered.Set(); release.Wait(ct); } });
        Task reader = Task.Run(() => { using var input = paused.Open(w, Control(home, w), Hash(first), first.Length, ct); Assert.Equal(first, ReadAll(input)); }, ct);
        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(2), ct));
            Assert.ThrowsAny<IOException>(() => new FileStream(Alias(home, w, Hash(first)), FileMode.Open, FileAccess.Read, FileShare.None));
            await Task.Run(() => { using var input = content.Open(w, Control(home, w), Hash(second), second.Length, ct); Assert.Equal(second, ReadAll(input)); }, ct).WaitAsync(TimeSpan.FromSeconds(2), ct);
        }
        finally { release.Set(); await reader; }
    }

    [Fact]
    public void TwoWorkspaceReferencesShareOneNativePayloadAndRootProofBlocksPrematureReclaim()
    {
        RequireWindows(); using var files = new TestDirectory(); var pins = new WindowsJournalFilePins();
        string home = Path.Combine(files.Path, "home"); var a = Workspace(home, 'a'); var b = Workspace(home, 'b');
        var content = new OwnedContentStore(home, pins); byte[] bytes = "independent-result"u8.ToArray(); string hash = Hash(bytes);
        Preserve(a); Preserve(b); Preserve(a);
        string aliasA = Alias(home, a, hash), aliasB = Alias(home, b, hash);
        Assert.Equal(pins.Identify(aliasA), pins.Identify(aliasB)); Assert.Equal(3, pins.LinkCount(aliasA));
        Assert.True((File.GetAttributes(aliasA) & FileAttributes.ReadOnly) != 0);
        using (var reader = content.Open(a, Control(home, a), hash, bytes.Length, TestContext.Current.CancellationToken))
        { Assert.Equal(bytes, ReadAll(reader)); Assert.False(content.Reclaim(hash, TestContext.Current.CancellationToken)); }
        Assert.Equal("ContentStillReferenced", Assert.Throws<WorkspaceException>(() => content.ReleaseReference(a, Control(home, a), hash, bytes.Length, _ => true, TestContext.Current.CancellationToken)).Code);
        content.ReleaseReference(a, Control(home, a), hash, bytes.Length, _ => false, TestContext.Current.CancellationToken);
        Assert.False(File.Exists(aliasA)); Assert.True(File.Exists(aliasB)); Assert.False(content.Reclaim(hash, TestContext.Current.CancellationToken));
        content.ReleaseReference(b, Control(home, b), hash, bytes.Length, _ => false, TestContext.Current.CancellationToken);
        Assert.True(content.Reclaim(hash, TestContext.Current.CancellationToken)); Assert.Empty(Directory.GetFiles(Path.Combine(home, "content-store", "objects"), "*", SearchOption.AllDirectories));
        void Preserve(WorkspaceRecord w) => content.Preserve(w, Control(home, w), hash, bytes.Length, output => output.Write(bytes), TestContext.Current.CancellationToken);
    }
    [Fact]
    public void AnExistingOwnedWholeObjectCanBeReferencedWithoutCreatingAnotherPayload()
    {
        RequireWindows(); using var files = new TestDirectory(); var pins = new WindowsJournalFilePins(); var ct = TestContext.Current.CancellationToken;
        string home = Path.Combine(files.Path, "home"); var a = Workspace(home, 'a'); var b = Workspace(home, 'b');
        var content = new OwnedContentStore(home, pins); byte[] bytes = "shared whole and range"u8.ToArray(); string hash = Hash(bytes);
        Assert.False(content.TryReferenceExisting(b, Control(home, b), hash, bytes.Length, ct)); Assert.False(Directory.Exists(Path.Combine(home, "content-store")));
        content.Preserve(a, Control(home, a), hash, bytes.Length, output => output.Write(bytes), ct);
        Assert.True(content.TryReferenceExisting(b, Control(home, b), hash, bytes.Length, ct));
        Assert.True(content.TryReferenceExisting(b, Control(home, b), hash, bytes.Length, ct));
        Assert.Equal(pins.Identify(Alias(home, a, hash)), pins.Identify(Alias(home, b, hash)));
        Assert.Single(Directory.GetFiles(Path.Combine(home, "content-store", "objects"), "*", SearchOption.AllDirectories));
        Assert.Equal(3, pins.LinkCount(Alias(home, b, hash)));
    }
    [Theory]
    [InlineData("content-reference-protected")]
    [InlineData("content-alias-published")]
    public void AnExistingWholeReferenceRecoversTheSameOwnedAliasAfterResponseLoss(string boundary)
    {
        RequireWindows(); using var files = new TestDirectory(); var pins = new WindowsJournalFilePins(); var ct = TestContext.Current.CancellationToken;
        string home = Path.Combine(files.Path, "home"); var a = Workspace(home, 'a'); var b = Workspace(home, 'b');
        byte[] bytes = "shared exact identity"u8.ToArray(); string hash = Hash(bytes); var content = new OwnedContentStore(home, pins);
        content.Preserve(a, Control(home, a), hash, bytes.Length, output => output.Write(bytes), ct);
        bool failed = false; var interrupted = new OwnedContentStore(home, pins, point =>
        { if (!failed && point == boundary) { failed = true; throw new IOException("Lost existing-object reference response."); } });
        Assert.Throws<IOException>(() => interrupted.TryReferenceExisting(b, Control(home, b), hash, bytes.Length, ct)); Assert.True(failed);
        Assert.True(new OwnedContentStore(home, pins).TryReferenceExisting(b, Control(home, b), hash, bytes.Length, ct));
        Assert.Equal(pins.Identify(Alias(home, a, hash)), pins.Identify(Alias(home, b, hash))); Assert.Equal(3, pins.LinkCount(Alias(home, b, hash)));
    }
    [Fact]
    public void AnExistingWholeReferenceNeverAdoptsAnEqualByteForeignAlias()
    {
        RequireWindows(); using var files = new TestDirectory(); var pins = new WindowsJournalFilePins(); var ct = TestContext.Current.CancellationToken;
        string home = Path.Combine(files.Path, "home"); var a = Workspace(home, 'a'); var b = Workspace(home, 'b');
        byte[] bytes = "equal but foreign"u8.ToArray(); string hash = Hash(bytes); var content = new OwnedContentStore(home, pins);
        content.Preserve(a, Control(home, a), hash, bytes.Length, output => output.Write(bytes), ct);
        string foreign = Alias(home, b, hash); Directory.CreateDirectory(Path.GetDirectoryName(foreign)!); File.WriteAllBytes(foreign, bytes); string identity = pins.Identify(foreign);
        Assert.Throws<WorkspaceException>(() => content.TryReferenceExisting(b, Control(home, b), hash, bytes.Length, ct));
        Assert.Equal(identity, pins.Identify(foreign)); Assert.Equal(bytes, File.ReadAllBytes(foreign));
        Assert.NotEqual(pins.Identify(Alias(home, a, hash)), identity);
    }
    [Fact]
    public void RestartedPartialReaderRejectsSameIdentitySameLengthTampering()
    {
        RequireWindows(); using var files = new TestDirectory(); var pins = new WindowsJournalFilePins(); var ct = TestContext.Current.CancellationToken;
        string home = Path.Combine(files.Path, "home"); var w = Workspace(home, 'a'); byte[] bytes = "immutable bytes"u8.ToArray(); string hash = Hash(bytes);
        var content = new OwnedContentStore(home, pins); content.Preserve(w, Control(home, w), hash, bytes.Length, output => output.Write(bytes), ct);
        using (var beforeRestart = new OwnedCapturedContentReader(home, Control(home, w), w, pins))
            Assert.Equal(1, beforeRestart.Read(hash, false, 0, new byte[1]));
        string alias = Alias(home, w, hash), identity = pins.Identify(alias); var attributes = File.GetAttributes(alias);
        File.SetAttributes(alias, attributes & ~FileAttributes.ReadOnly);
        using (var writer = new FileStream(alias, FileMode.Open, FileAccess.Write, FileShare.None))
        { writer.WriteByte((byte)'X'); writer.Flush(true); }
        File.SetAttributes(alias, attributes);
        Assert.Equal(identity, pins.Identify(alias)); Assert.Equal(bytes.Length, new FileInfo(alias).Length);
        Assert.Equal("ContentStoreInvalid", Assert.Throws<WorkspaceException>(() => new OwnedContentStore(home, pins).Open(w, Control(home, w), hash, bytes.Length, ct)).Code);
        using var restarted = new OwnedCapturedContentReader(home, Control(home, w), w, pins);
        Assert.Equal("ContentStoreInvalid", Assert.Throws<WorkspaceException>(() => restarted.Read(hash, false, 0, new byte[1])).Code);
    }
    [Theory]
    [InlineData("content-catalog-prefix-written")]
    [InlineData("content-catalog-durable-before-publish")]
    [InlineData("content-object-created-before-receipt")]
    [InlineData("content-object-owned")]
    [InlineData("content-payload-durable")]
    [InlineData("content-object-published")]
    [InlineData("content-reference-protected")]
    [InlineData("content-alias-published")]
    public void SameRequestRecoversOwnedContentAcrossPublicationBoundaries(string boundary)
    {
        RequireWindows(); using var files = new TestDirectory(); string home = Path.Combine(files.Path, "home"); var w = Workspace(home, 'a');
        byte[] bytes = "fixed"u8.ToArray(); string hash = Hash(bytes); bool failed = false; var pins = new WindowsJournalFilePins();
        var content = new OwnedContentStore(home, pins, point =>
        { if (point == boundary && !failed) { failed = true; throw new IOException("Injected response loss."); } });
        Assert.Throws<IOException>(() => Preserve(content)); Assert.True(failed);
        var resumed = new OwnedContentStore(home, pins); Preserve(resumed);
        using var reader = resumed.Open(w, Control(home, w), hash, bytes.Length, TestContext.Current.CancellationToken); Assert.Equal(bytes, ReadAll(reader));
        Assert.Equal(2, pins.LinkCount(Alias(home, w, hash)));
        if (boundary == "content-object-created-before-receipt")
        {
            var scratch = Assert.Single(resumed.ReadPreservedScratchPage(ct: TestContext.Current.CancellationToken).Entries);
            Assert.True(File.Exists(Path.Combine(home, "content-store", scratch.RelativePath.Replace('/', Path.DirectorySeparatorChar))));
            Assert.Equal(w.Id, scratch.ObservedByWorkspaceId);
        }
        void Preserve(OwnedContentStore value) => value.Preserve(w, Control(home, w), hash, bytes.Length, output => { output.Position = 0; output.Write(bytes); }, TestContext.Current.CancellationToken);
    }
    [Fact]
    public void AnOpenIndependentReaderPreventsCanonicalReclaimUntilTheReaderCloses()
    {
        RequireWindows(); using var files = new TestDirectory(); string home = Path.Combine(files.Path, "home"); var w = Workspace(home, 'a');
        byte[] bytes = "reader"u8.ToArray(); string hash = Hash(bytes); var content = new OwnedContentStore(home, new WindowsJournalFilePins());
        content.Preserve(w, Control(home, w), hash, bytes.Length, output => output.Write(bytes), TestContext.Current.CancellationToken);
        using (var reader = content.Open(w, Control(home, w), hash, bytes.Length, TestContext.Current.CancellationToken))
        {
            content.ReleaseReference(w, Control(home, w), hash, bytes.Length, _ => false, TestContext.Current.CancellationToken);
            Assert.Throws<WorkspaceException>(() => content.Reclaim(hash, TestContext.Current.CancellationToken));
            Assert.Equal(bytes, ReadAll(reader));
        }
        content.ReleaseReference(w, Control(home, w), hash, bytes.Length, _ => false, TestContext.Current.CancellationToken);
        Assert.True(content.Reclaim(hash, TestContext.Current.CancellationToken));
    }
    [Fact]
    public void ReplacedAliasIsPreservedEvenWhenItsBytesStillMatchTheContentHash()
    {
        RequireWindows(); using var files = new TestDirectory(); string home = Path.Combine(files.Path, "home"); var w = Workspace(home, 'a'); var pins = new WindowsJournalFilePins();
        byte[] bytes = "identity"u8.ToArray(); string hash = Hash(bytes); var content = new OwnedContentStore(home, pins);
        content.Preserve(w, Control(home, w), hash, bytes.Length, output => output.Write(bytes), TestContext.Current.CancellationToken);
        string alias = Alias(home, w, hash); pins.Unpin(alias, pins.Identify(alias)); File.WriteAllBytes(alias, bytes);
        Assert.Equal("ContentStoreInvalid", Assert.Throws<WorkspaceException>(() => content.Open(w, Control(home, w), hash, bytes.Length, TestContext.Current.CancellationToken)).Code);
        Assert.Throws<WorkspaceException>(() => content.ReleaseReference(w, Control(home, w), hash, bytes.Length, _ => false, TestContext.Current.CancellationToken));
        Assert.Equal(bytes, File.ReadAllBytes(alias)); Assert.False(content.Reclaim(hash, TestContext.Current.CancellationToken));
    }
    [Fact]
    public void PruneRetainsOtherWorkspaceAndHistoricalRootsAndResumesAReaderBlockedReclaim()
    {
        RequireWindows(); using var files = new TestDirectory(); string home = Path.Combine(files.Path, "home"); var a = Workspace(home, 'a'); var b = Workspace(home, 'b');
        byte[] old = "old"u8.ToArray(), current = "current"u8.ToArray(); string oldHash = Hash(old), currentHash = Hash(current);
        var content = new OwnedContentStore(home, new WindowsJournalFilePins()); var ct = TestContext.Current.CancellationToken;
        content.Preserve(a, Control(home, a), oldHash, old.Length, output => output.Write(old), ct);
        content.Preserve(a, Control(home, a), currentHash, current.Length, output => output.Write(current), ct);
        content.Preserve(b, Control(home, b), oldHash, old.Length, output => output.Write(old), ct);
        content.PruneUnreferenced(a, Control(home, a), hash => hash == currentHash, ct);
        Assert.False(File.Exists(Alias(home, a, oldHash))); Assert.True(File.Exists(Alias(home, b, oldHash))); Assert.True(File.Exists(Alias(home, a, currentHash)));
        using (var reader = content.Open(b, Control(home, b), oldHash, old.Length, ct))
        {
            content.PruneUnreferenced(b, Control(home, b), _ => false, ct);
            Assert.Single(content.ReadDeferredReclamationPage(ct: ct).Entries);
            Assert.Equal(old, ReadAll(reader));
        }
        new OwnedContentStore(home, new WindowsJournalFilePins()).PruneUnreferenced(b, Control(home, b), _ => false, ct);
        Assert.True(content.Reclaim(oldHash, ct));
        using var retained = content.Open(a, Control(home, a), currentHash, current.Length, ct); Assert.Equal(current, ReadAll(retained));
    }
    [Fact]
    public void NewFixedPreservationCanReferenceTheSameOwnedObjectWhileAnOldReaderDefersItsRetirement()
    {
        RequireWindows(); using var files = new TestDirectory(); string home = Path.Combine(files.Path, "home"); var w = Workspace(home, 'a'); var ct = TestContext.Current.CancellationToken;
        byte[] bytes = "retained again"u8.ToArray(); string hash = Hash(bytes); var content = new OwnedContentStore(home, new WindowsJournalFilePins());
        content.Preserve(w, Control(home, w), hash, bytes.Length, output => output.Write(bytes), ct);
        using (var reader = content.Open(w, Control(home, w), hash, bytes.Length, ct))
        {
            content.PruneUnreferenced(w, Control(home, w), _ => false, ct);
            Assert.Single(content.ReadDeferredReclamationPage(ct: ct).Entries);
            content.Preserve(w, Control(home, w), hash, bytes.Length, _ => throw new InvalidOperationException("The exact immutable object must be reused."), ct);
            Assert.Empty(content.ReadDeferredReclamationPage(ct: ct).Entries);
            using var restored = content.Open(w, Control(home, w), hash, bytes.Length, ct); Assert.Equal(bytes, ReadAll(restored)); Assert.Equal(bytes, ReadAll(reader));
        }
        content.PruneUnreferenced(w, Control(home, w), _ => false, ct); Assert.True(content.Reclaim(hash, ct));
    }
    [Fact]
    public void DiskFullAfterNativeCreationPreservesUnknownAttemptAndRecoversTheSameFrozenContent()
    {
        RequireWindows(); using var files = new TestDirectory(); string home = Path.Combine(files.Path, "home"); var w = Workspace(home, 'a');
        byte[] bytes = "same-request"u8.ToArray(); string hash = Hash(bytes); bool injected = false;
        var content = new OwnedContentStore(home, new WindowsJournalFilePins(), point =>
        {
            if (point == "content-object-created-before-receipt" && !injected)
            { injected = true; throw new IOException("Injected disk full.", unchecked((int)0x80070070)); }
        });
        var failure = Assert.Throws<IOException>(() => content.Preserve(w, Control(home, w), hash, bytes.Length, output => output.Write(bytes), TestContext.Current.CancellationToken));
        Assert.Equal(unchecked((int)0x80070070), failure.HResult);
        var resumed = new OwnedContentStore(home, new WindowsJournalFilePins());
        resumed.Preserve(w, Control(home, w), hash, bytes.Length, output => output.Write(bytes), TestContext.Current.CancellationToken);
        Assert.Single(resumed.ReadPreservedScratchPage(ct: TestContext.Current.CancellationToken).Entries);
        using var reader = resumed.Open(w, Control(home, w), hash, bytes.Length, TestContext.Current.CancellationToken); Assert.Equal(bytes, ReadAll(reader));
    }
    private static void RequireWindows() { if (!OperatingSystem.IsWindows()) Assert.Skip("Exact NTFS content identities and owned hard-link deletion."); }
    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static string Control(string home, WorkspaceRecord w) => Path.Combine(home, "records", w.Id);
    private static string Alias(string home, WorkspaceRecord w, string hash) => Path.Combine(Control(home, w), "files", "objects", hash);
    private static byte[] ReadAll(Stream input) { using var output = new MemoryStream(); input.CopyTo(output); return output.ToArray(); }
    private static WorkspaceRecord Workspace(string home, char key)
    {
        var w = new WorkspaceRecord { Id = new string(key, 24), RequestKey = "request-" + key, RequestDigest = "digest-" + key,
            ProjectPath = Path.Combine(home, "original"), Path = Path.Combine(home, "workspaces", new string(key, 24)),
            SourceReference = "source", BaseReference = "base", SourceKind = "none", ApplicationKind = "none", StorageMode = "project-snapshot" };
        Directory.CreateDirectory(Control(home, w)); return w;
    }
}
