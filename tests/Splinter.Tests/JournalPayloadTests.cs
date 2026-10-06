using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using Splinter.Core;
using Splinter.Runtime;

namespace Splinter.Tests;

public sealed class JournalPayloadTests
{
    [Theory]
    [InlineData(10, 10, 3, 7)]
    [InlineData(70, 4096, 4095, 1)]
    [InlineData(0, 0, 0, 0)]
    public void CommonFrameDescriptorBoundsLogicalFragments(int stored, int decoded, int offset, int length)
    {
        // Independent literal frame model: stored bytes never determine the
        // byte address or bounds of a decoded fragment.
        var payload = new WorkspaceJournalPayload(stored, decoded);
        payload.RequireValid(4096); payload.RequireFragment(offset, length);
        Assert.Equal(stored != decoded, payload.Encoded);
        payload.RequireFragment(decoded, 0);
        Assert.Throws<WorkspaceException>(() => payload.RequireFragment(offset, decoded + 1));
    }

    [Theory]
    [InlineData(-1, 4)] [InlineData(5, 4)] [InlineData(0, 4)]
    [InlineData(1, 4097)] [InlineData(0, -1)]
    public void CommonFrameRejectsInvalidPhysicalAndLogicalBounds(int stored, int decoded) =>
        Assert.Equal("DeltaCorrupt", Assert.Throws<WorkspaceException>(() =>
            new WorkspaceJournalPayload(stored, decoded).RequireValid(4096)).Code);

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void SingleCallbackKeepsOneDurableFrameAndPhysicalBudget(bool compressible)
    {
        using var directory = new TestDirectory(); var tree = new SnapshotOverlayTests.FakeSnapshotTree();
        var identity = Identity(tree); var observer = new Observer();
        byte[] input = new byte[65536]; if (!compressible) new Random(537).NextBytes(input);
        using var store = new JournalDeltaStore(directory.Path, identity, tree, observer, optimizeJournal: false);
        Guid file = store.Create("file", false, 32).Id;
        var before = store.Observe(); observer.Events.Clear(); store.Write(file, 0, input);
        var after = store.Observe();
        Assert.Equal(before.DurableSequence + 1, after.DurableSequence);
        Assert.Equal(before.Metrics!.FlushCount + 1, after.Metrics!.FlushCount);
        Assert.Single(observer.Events, x => x.Boundary == "commit.after-flush");
        Assert.All(observer.Events, x => Assert.Equal(input.Length, x.PayloadBytes));
        Assert.Equal(input.Length, store.ObservePrivateUsage().RetainedLogicalPayloadBytes);
        Assert.Equal(compressible, after.PayloadBytes < input.Length);
        if (!compressible) Assert.Equal(input.Length, after.PayloadBytes);
        byte[] actual = new byte[input.Length]; store.Read(file, 0, actual); Assert.Equal(input, actual);
        Assert.Equal(0, tree.ReadBytes);
    }

    [Theory]
    [InlineData(111)] [InlineData(112)] [InlineData(113)] [InlineData(128)]
    [InlineData(4096)] [InlineData(65536)] [InlineData(1048576)]
    public void JournalSavingDecisionAndSelectedBytesMatchStandaloneCodecIncludingThreshold(int size)
    {
        using var directory = new TestDirectory(); var tree = new SnapshotOverlayTests.FakeSnapshotTree();
        var identity = Identity(tree) with { MaxJournalBytes = 16 * 1024 * 1024, MaxTransferBytes = 1048576 };
        var codec = new BrotliWorkspacePayloadCodec();
        foreach (int randomPrefix in new[] { 0, Math.Max(0, size - 160), size }.Distinct())
        {
            byte[] input = new byte[size]; new Random(1936).NextBytes(input.AsSpan(0, randomPrefix));
            byte[] encoded = new byte[codec.GetMaxEncodedBytes(size)]; int length = codec.Encode(input, encoded);
            bool compressed = size >= 112 && length <= size - 64;
            string child = Path.Combine(directory.Path, randomPrefix.ToString());
            using (var store = new JournalDeltaStore(child, identity, tree, optimizeJournal: false))
            {
                Guid file = store.Create("file", false, 32).Id; store.Write(file, 0, input);
                byte[] output = new byte[size]; Assert.Equal(size, store.Read(file, 0, output)); Assert.Equal(input, output);
            }
            byte[] frames = File.ReadAllBytes(Path.Combine(child, "delta.journal"));
            int firstMetadata = BinaryPrimitives.ReadInt32LittleEndian(frames.AsSpan(4));
            int cursor = 16 + firstMetadata + 32;
            int metadata = BinaryPrimitives.ReadInt32LittleEndian(frames.AsSpan(cursor + 4));
            int stored = BinaryPrimitives.ReadInt32LittleEndian(frames.AsSpan(cursor + 8));
            Assert.Equal(size, BinaryPrimitives.ReadInt32LittleEndian(frames.AsSpan(cursor + 12)));
            Assert.Equal(compressed ? length : size, stored);
            Assert.Equal(compressed ? encoded[..length] : input, frames.AsSpan(cursor + 16 + metadata, stored).ToArray());
        }
    }

    [Fact]
    public void CompressedFragmentsSurvivePartialOverlapResizeRenameCandidateAndCompaction()
    {
        using var directory = new TestDirectory(); var tree = new SnapshotOverlayTests.FakeSnapshotTree();
        var identity = Identity(tree); string delta = Path.Combine(directory.Path, "delta");
        byte[] expected = Enumerable.Repeat((byte)7, 65536).ToArray(); long sequence;
        using (var store = new JournalDeltaStore(delta, identity, tree, optimizeJournal: false))
        {
            var file = store.Create("file", false, 32);
            store.Write(file.Id, 0, expected);
            store.Write(file.Id, 321, [1, 2, 3]); new byte[] { 1, 2, 3 }.CopyTo(expected, 321);
            Array.Fill(expected, (byte)5, 8192, 16384); store.Write(file.Id, 8192, expected.AsSpan(8192, 16384));
            store.SetLength(file.Id, 50000); store.SetLength(file.Id, 65536); Array.Clear(expected, 50000, 15536);
            store.Rename(file.Id, "renamed", false);
            var garbage = store.Create("deleted", false, 32); store.Write(garbage.Id, 0, new byte[4096]); store.Delete(garbage.Id);
            var usage = store.ObservePrivateUsage(); Assert.True(usage.ObsoletePayloadBytes > 0);
            Assert.True(usage.LivePrivateBytes > usage.LiveStoredPayloadBytes);
            sequence = store.Observe().DurableSequence;
            byte[] tiny = new byte[7]; store.Read(file.Id, 6550, tiny); Assert.Equal(expected[6550..6557], tiny);
        }
        var compact = JournalDeltaStore.CompactDirectory(delta, identity, tree);
        Assert.True(compact.After.JournalBytes <= compact.Before.JournalBytes);
        JournalDeltaStore.CompleteCompaction(delta, identity);
        string candidate = Path.Combine(directory.Path, "candidate");
        var published = JournalDeltaStore.ExportCandidate(delta, identity, tree, candidate, "fixed", []);
        Assert.Equal(3, published.Version);
        using var input = new CandidateSnapshotTree(candidate, tree);
        byte[] output = new byte[expected.Length]; Assert.Equal(output.Length, input.Read("renamed", 0, output)); Assert.Equal(expected, output);
        using var replay = new JournalDeltaStore(delta, identity, tree, optimizeJournal: false);
        Assert.Equal(sequence, replay.Observe().DurableSequence); Assert.Null(replay.Stat("deleted"));
        replay.Read(replay.Stat("renamed")!.Id, 0, output); Assert.Equal(expected, output); Assert.Equal(0, tree.ReadBytes);
    }

    [Theory]
    [InlineData("checksum")] [InlineData("decoded-length")] [InlineData("stored-codec")]
    [InlineData("format")]
    public void CompleteEncodedCorruptionIsPreservedEvenWithAnOuterChecksum(string fault)
    {
        using var directory = new TestDirectory(); var tree = new SnapshotOverlayTests.FakeSnapshotTree(); var identity = Identity(tree);
        using (var store = new JournalDeltaStore(directory.Path, identity, tree, optimizeJournal: false))
            store.Write(store.Create("file", false, 32).Id, 0, new byte[65536]);
        string journal = Path.Combine(directory.Path, "delta.journal"); byte[] bytes = File.ReadAllBytes(journal);
        int cursor = 0;
        int metadata = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(4));
        int stored = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(8));
        cursor += JournalDeltaStore.FrameHeaderBytes + metadata + stored + 32;
        metadata = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(cursor + 4));
        stored = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(cursor + 8));
        int payload = cursor + JournalDeltaStore.FrameHeaderBytes + metadata;
        switch (fault)
        {
            case "checksum": bytes[^1] ^= 1; break;
            case "decoded-length": BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(cursor + 12), 65535); break;
            case "stored-codec": bytes[payload + 16] ^= 1; break;
            case "format": bytes[cursor] = (byte)'X'; break;
        }
        if (fault != "checksum") SHA256.HashData(bytes.AsSpan(cursor, bytes.Length - cursor - 32), bytes.AsSpan(bytes.Length - 32));
        File.WriteAllBytes(journal, bytes);
        var error = Assert.Throws<WorkspaceException>(() => new JournalDeltaStore(directory.Path, identity, tree, optimizeJournal: false));
        Assert.Contains(error.Code, new[] { "DeltaCorrupt", "PayloadBlockCorrupt", "JournalFormatUnsupported" });
        Assert.Equal(bytes, File.ReadAllBytes(journal));
    }

    [Fact]
    public void PreviousOverlayIsRejectedBeforeCreatingOrRewritingFiles()
    {
        using var directory = new TestDirectory(); var tree = new SnapshotOverlayTests.FakeSnapshotTree();
        string path = Path.Combine(directory.Path, "old"); Directory.CreateDirectory(path);
        byte[] original = [1, 2, 3]; File.WriteAllBytes(Path.Combine(path, "delta.journal"), original);
        Assert.Equal("OverlayFormatUnsupported", Assert.Throws<WorkspaceException>(() =>
            new JournalDeltaStore(path, Identity(tree) with { Version = 1 }, tree, optimizeJournal: false)).Code);
        Assert.Equal(original, File.ReadAllBytes(Path.Combine(path, "delta.journal")));
        Assert.False(File.Exists(Path.Combine(path, "identity.json")));
    }

    [Fact]
    public void SavedPreviousIdentityCannotCreateAMissingJournal()
    {
        using var directory = new TestDirectory(); var tree = new SnapshotOverlayTests.FakeSnapshotTree();
        string identityPath = Path.Combine(directory.Path, "identity.json");
        string saved = JsonSerializer.Serialize(Identity(tree) with { Version = 1 }); File.WriteAllText(identityPath, saved);
        Assert.Equal("OverlayFormatUnsupported", Assert.Throws<WorkspaceException>(() =>
            new JournalDeltaStore(directory.Path, Identity(tree), tree, optimizeJournal: false)).Code);
        Assert.Equal(saved, File.ReadAllText(identityPath)); Assert.False(File.Exists(Path.Combine(directory.Path, "delta.journal")));
    }

    private static OverlayIdentity Identity(ISnapshotTree tree) => new(2, "codec-journal", tree.Snapshot, 2 * 1024 * 1024, 65536);
    private sealed class Observer : IJournalMutationObserver
    {
        public List<JournalMutationBoundary> Events { get; } = [];
        public void Observe(JournalMutationBoundary boundary) => Events.Add(boundary);
    }
}
