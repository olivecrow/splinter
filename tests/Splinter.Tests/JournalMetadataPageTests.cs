using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Splinter.Core;
using Splinter.Runtime;

namespace Splinter.Tests;

public sealed class JournalMetadataPageTests
{
    private static OverlayIdentity Identity(ISnapshotTree tree) => new(2, "metadata-pages", tree.Snapshot, 64L * 1048576, 1048576);

    [Fact]
    public void AppendPagesBoundWrittenMetadataWithoutRecopyingAnyEarlierPayload()
    {
        using var root = new TestDirectory(); var tree = new SnapshotOverlayTests.FakeSnapshotTree();
        var identity = Identity(tree); var data = Bytes(4096, 3); Guid id; SnapshotDeltaSeal seal;
        using (var writer = new JournalDeltaStore(root.Path, identity, tree))
        {
            id = writer.Create("growing", false, 32).Id;
            long flushes = writer.Observe().Metrics!.FlushCount, written = writer.Observe().Metrics!.JournalAppendedBytes;
            for (int i = 0; i < 128; i++)
            {
                writer.Write(id, i * data.Length, data);
                var blocks = HeadBlocks(root.Path);
                Assert.Single(blocks, b => !b.AsSpan().StartsWith("SJP2"u8));
                Assert.Equal(data, blocks.Single(b => !b.AsSpan().StartsWith("SJP2"u8)));
                Assert.Equal((i + 1) % 32 == 0 ? 1 : 0, blocks.Count(b => b.AsSpan().StartsWith("SJP2"u8)));
            }
            Assert.Equal(128, writer.Observe().Metrics!.FlushCount - flushes);
            Assert.InRange(writer.Observe().Metrics!.JournalAppendedBytes - written, 128 * 4096, 128 * 12000);
            Assert.Equal(128 * 4096, writer.Observe().PayloadBytes);
            AssertRepeated(writer, id, data, 128); seal = writer.Seal();
        }
        using var reader = JournalDeltaStore.OpenReadOnly(root.Path, identity, tree);
        Assert.Equal(seal, reader.Seal()); AssertRepeated(reader, id, data, 128); Assert.Equal(0, tree.ReadBytes);
    }

    [Fact]
    public void ChangedPrefixReusesUnaffectedPagesWithoutCopyingTheirPrivatePayloadAcrossReconnect()
    {
        using var root = new TestDirectory(); var tree = new SnapshotOverlayTests.FakeSnapshotTree();
        var identity = Identity(tree); byte[] expected = Bytes(96 * 4096, 17); Guid id;
        using (var writer = new JournalDeltaStore(root.Path, identity, tree))
        {
            id = writer.Create("file", false, 32).Id;
            for (int i = 0; i < 96; i++) writer.Write(id, i * 4096, expected.AsSpan(i * 4096, 4096));
            expected[0] ^= 47; writer.Write(id, 0, expected.AsSpan(0, 1));
            // The first private block is repacked, but the untouched page
            // payloads remain in their original capsule prefixes.
            Assert.Equal(4096, HeadBlocks(root.Path).Where(b => !b.AsSpan().StartsWith("SJP2"u8)).Sum(b => b.Length));
        }
        using (var writer = new JournalDeltaStore(root.Path, identity, tree))
        {
            for (int i = 0; i < 64; i++)
            {
                byte[] extra = Bytes(4096, i); writer.Write(id, expected.Length, extra); expected = [.. expected, .. extra];
                Assert.Single(HeadBlocks(root.Path), b => !b.AsSpan().StartsWith("SJP2"u8));
                Assert.Equal(extra, HeadBlocks(root.Path).Single(b => !b.AsSpan().StartsWith("SJP2"u8)));
            }
            writer.Rename(id, "renamed", false);
            writer.SetLength(id, 5000); expected = expected[..5000];
            byte[] actual = new byte[expected.Length]; writer.Read(id, 0, actual); Assert.Equal(expected, actual);
            Assert.Equal(expected.Length, writer.Observe().PayloadBytes);
        }
        using (var reader = JournalDeltaStore.OpenReadOnly(root.Path, identity, tree))
        {
            Assert.Null(reader.Stat("file")); Assert.Equal(id, reader.Stat("renamed")!.Id);
            byte[] actual = new byte[expected.Length]; reader.Read(id, 0, actual); Assert.Equal(expected, actual);
        }
        Assert.Equal(0, tree.ReadBytes);
    }

    [Fact]
    public void PagedContentSurvivesCandidateAndOffOnCompactionThenUnlinkedCloseReclaimsPages()
    {
        using var root = new TestDirectory(); using var candidate = new TestDirectory();
        var tree = new SnapshotOverlayTests.FakeSnapshotTree(); var identity = Identity(tree); byte[] data = Bytes(4096, 24); Guid id;
        using (var writer = new JournalDeltaStore(root.Path, identity, tree))
        {
            id = writer.Create("file", false, 32).Id;
            for (int i = 0; i < 65; i++) writer.Write(id, i * 4096, data);
        }
        string exported = Path.Combine(candidate.Path, "export");
        JournalDeltaStore.ExportCandidate(root.Path, identity, tree, exported, "paged-export", []);
        using (var reader = JournalDeltaStore.OpenReadOnly(exported, identity, tree)) AssertRepeated(reader, id, data, 65);
        JournalDeltaStore.CompactDirectory(root.Path, identity, tree, revertBaseRanges: false);
        JournalDeltaStore.CompleteCompaction(root.Path, identity);
        JournalDeltaStore.CompactDirectory(root.Path, identity, tree, revertBaseRanges: true);
        JournalDeltaStore.CompleteCompaction(root.Path, identity);
        using (var writer = new JournalDeltaStore(root.Path, identity, tree))
        {
            AssertRepeated(writer, id, data, 65); writer.Delete(id); AssertRepeated(writer, id, data, 65);
            writer.ReleaseUnlinkedFile(id); Assert.Equal(0, writer.Observe().PayloadBytes);
        }
        using var resumed = new JournalDeltaStore(root.Path, identity, tree);
        Assert.Null(resumed.Stat("file")); Assert.Equal(0, resumed.Observe().PayloadBytes); Assert.Empty(HeadBlocks(root.Path));
        Assert.Equal(0, tree.ReadBytes);
    }

    [Theory]
    [InlineData("latest.after-intent-header", false)]
    [InlineData("latest.before-intent-flush", true)]
    [InlineData("latest.after-root-publication", true)]
    [InlineData("latest.after-object-reclamation", true)]
    public void PagePublicationRecoveryRetainsOnlyAcknowledgedOrCompletedCurrentData(string boundary, bool completed)
    {
        using var root = new TestDirectory(); var tree = new SnapshotOverlayTests.FakeSnapshotTree();
        var identity = Identity(tree); var observer = new Interruption(); byte[] data = Bytes(4096, 41); Guid id;
        using (var writer = new JournalDeltaStore(root.Path, identity, tree, observer))
        {
            id = writer.Create("file", false, 32).Id;
            for (int i = 0; i < 31; i++) writer.Write(id, i * 4096, data);
            observer.Boundary = boundary;
            Assert.Throws<IOException>(() => writer.Write(id, 31 * 4096, data));
            Assert.Equal("DeltaReconnectRequired", Assert.Throws<WorkspaceException>(() => writer.Write(id, 0, data)).Code);
        }
        using var resumed = new JournalDeltaStore(root.Path, identity, tree);
        AssertRepeated(resumed, id, data, completed ? 32 : 31);
        Assert.Equal((completed ? 32 : 31) * 4096, resumed.Observe().PayloadBytes); resumed.Seal();
        Assert.Equal(0, tree.ReadBytes);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("corrupt")]
    [InlineData("foreign-node")]
    [InlineData("duplicate")]
    [InlineData("payload-alias")]
    [InlineData("extra")]
    [InlineData("previous-node-format")]
    public void PageInventoryAndOwnerDamageArePreservedAndRejected(string kind)
    {
        using var root = new TestDirectory(); var tree = new SnapshotOverlayTests.FakeSnapshotTree();
        var identity = Identity(tree); Guid id;
        using (var writer = new JournalDeltaStore(root.Path, identity, tree))
        {
            id = writer.Create("file", false, 32).Id;
            for (int i = 0; i < 64; i++) writer.Write(id, i * 4096, Bytes(4096, i));
        }
        JournalDeltaStore.CompactDirectory(root.Path, identity, tree, revertBaseRanges: true);
        JournalDeltaStore.CompleteCompaction(root.Path, identity);
        string directory = Directory.GetDirectories(root.Path, "latest-*").Single();
        string nodePath = Path.Combine(directory, $"node-{id:N}.json");
        var node = Decode(File.ReadAllBytes(nodePath));
        long pageId = node["Pages"]![0]!["Id"]!.GetValue<long>();
        string pagePath = Path.Combine(directory, $"page-{pageId:x16}.bin");
        byte[] page = File.ReadAllBytes(pagePath);
        if (kind == "missing") File.Delete(pagePath);
        else if (kind == "corrupt") { page[^1] ^= 47; File.WriteAllBytes(pagePath, page); }
        else if (kind == "extra") File.Copy(pagePath, Path.Combine(directory, "page-000000000000ffff.bin"));
        else if (kind == "previous-node-format") File.WriteAllBytes(nodePath, Encode(node, "SJN1"));
        else if (kind == "duplicate")
        {
            node["Pages"]![1] = node["Pages"]![0]!.DeepClone(); File.WriteAllBytes(nodePath, Encode(node, "SJN3"));
        }
        else if (kind == "payload-alias")
        {
            byte[] payload = File.ReadAllBytes(Path.Combine(directory, "block-000000000000000a.bin"));
            node["Pages"]![0]!["Id"] = 10;
            node["Pages"]![0]!["Hash"] = Convert.ToHexStringLower(SHA256.HashData(payload));
            File.WriteAllBytes(nodePath, Encode(node, "SJN3"));
        }
        else
        {
            var body = Decode(page[80..]); body["NodeId"] = Guid.NewGuid();
            byte[] changed = Encode(body, "SJP2"); Assert.Equal(page.Length - 80, changed.Length);
            changed.CopyTo(page, 80); SHA256.HashData(changed, page.AsSpan(48, 32));
            File.WriteAllBytes(pagePath, page);
            node["Pages"]![0]!["Hash"] = Convert.ToHexStringLower(SHA256.HashData(page));
            File.WriteAllBytes(nodePath, Encode(node, "SJN3"));
        }
        var before = Directory.GetFiles(root.Path, "*", SearchOption.AllDirectories).ToDictionary(p => p, File.ReadAllBytes);
        Assert.Equal("DeltaCorrupt", Assert.Throws<WorkspaceException>(() => new JournalDeltaStore(root.Path, identity, tree)).Code);
        foreach (var file in before) Assert.Equal(file.Value, File.ReadAllBytes(file.Key));
    }

    [Theory]
    [InlineData("empty", false)]
    [InlineData("pending", false)]
    [InlineData("published", false)]
    [InlineData("initializing", false)]
    [InlineData("empty", true)]
    [InlineData("pending", true)]
    [InlineData("published", true)]
    [InlineData("initializing", true)]
    public void PreviousNodeFormatIsRejectedBeforeAnyRecoveryOrSeedPublication(string stage, bool previous)
    {
        using var root = new TestDirectory(); var tree = new SnapshotOverlayTests.FakeSnapshotTree();
        var identity = Identity(tree); var observer = new Interruption();
        if (stage == "initializing")
        {
            observer.Boundary = "latest.initialize.intent-flushed";
            Assert.Throws<IOException>(() => new JournalDeltaStore(root.Path, identity, tree, observer));
            string intent = Path.Combine(root.Path, "journal-initialization.json.pending");
            var value = JsonNode.Parse(File.ReadAllBytes(intent))!.AsObject(); if (previous) value["NodeFormat"] = 2; else value.Remove("NodeFormat");
            File.WriteAllBytes(intent, System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(value));
        }
        else
        {
            using (var writer = new JournalDeltaStore(root.Path, identity, tree, observer))
            {
                if (stage != "empty")
                {
                    observer.Boundary = stage == "pending" ? "latest.after-intent-header" : "latest.after-root-publication";
                    Assert.Throws<IOException>(() => writer.Create("file", false, 32));
                }
            }
            string path = Path.Combine(root.Path, "delta.journal"); byte[] bytes = File.ReadAllBytes(path);
            if (stage == "published")
            {
                int dataEnd = checked(128 + (int)BinaryPrimitives.ReadInt64LittleEndian(bytes.AsSpan(16)));
                int count = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(24));
                var capsule = JsonNode.Parse(bytes.AsSpan(dataEnd, count))!;
                if (previous) capsule["After"]!["NodeFormat"] = 2; else capsule["After"]!.AsObject().Remove("NodeFormat");
                byte[] metadata = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(capsule);
                byte[] changed = new byte[dataEnd + metadata.Length + 32]; bytes.AsSpan(0, dataEnd).CopyTo(changed);
                BinaryPrimitives.WriteInt32LittleEndian(changed.AsSpan(24), metadata.Length); metadata.CopyTo(changed, dataEnd);
                SHA256.HashData(changed.AsSpan(0, changed.Length - 32), changed.AsSpan(changed.Length - 32));
                File.WriteAllBytes(path, changed);
            }
            else
            {
                var value = Decode(bytes).AsObject(); if (previous) value["NodeFormat"] = 2; else value.Remove("NodeFormat"); File.WriteAllBytes(path, Encode(value, "SJL4"));
            }
        }
        var before = Directory.GetFiles(root.Path, "*", SearchOption.AllDirectories).ToDictionary(p => p, File.ReadAllBytes);
        Assert.Equal("DeltaCorrupt", Assert.Throws<WorkspaceException>(() => new JournalDeltaStore(root.Path, identity, tree)).Code);
        Assert.Equal(before.Keys.Order(), Directory.GetFiles(root.Path, "*", SearchOption.AllDirectories).Order());
        foreach (var file in before) Assert.Equal(file.Value, File.ReadAllBytes(file.Key));
    }

    private static byte[] Bytes(int length, int seed) { byte[] result = new byte[length]; new Random(seed).NextBytes(result); return result; }
    private static void AssertRepeated(JournalDeltaStore store, Guid id, byte[] expected, int count)
    {
        Assert.Equal((long)count * expected.Length, store.Get(id).Length);
        for (int i = 0; i < count; i++) { byte[] actual = new byte[expected.Length]; store.Read(id, (long)i * expected.Length, actual); Assert.Equal(expected, actual); }
    }
    private static List<byte[]> HeadBlocks(string directory)
    {
        using var input = new FileStream(Path.Combine(directory, "delta.journal"), FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        byte[] header = new byte[128]; input.ReadExactly(header);
        long end = 128 + BinaryPrimitives.ReadInt64LittleEndian(header.AsSpan(16)); var result = new List<byte[]>();
        while (input.Position < end)
        {
            byte[] block = new byte[80]; input.ReadExactly(block); Assert.True(block.AsSpan().StartsWith("SJB1"u8) || block.AsSpan().StartsWith("SJM2"u8));
            byte[] payload = new byte[BinaryPrimitives.ReadInt32LittleEndian(block.AsSpan(12))]; input.ReadExactly(payload); result.Add(payload);
        }
        Assert.Equal(end, input.Position); return result;
    }
    private static JsonNode Decode(byte[] bytes) => JsonNode.Parse(bytes.AsSpan(12, bytes.Length - 44))!;
    private static byte[] Encode(JsonNode node, string magic)
    {
        byte[] json = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(node), result = new byte[json.Length + 44];
        System.Text.Encoding.ASCII.GetBytes(magic).CopyTo(result, 0); BinaryPrimitives.WriteInt64LittleEndian(result.AsSpan(4), json.Length);
        json.CopyTo(result, 12); SHA256.HashData(result.AsSpan(0, result.Length - 32), result.AsSpan(result.Length - 32)); return result;
    }
    private sealed class Interruption : IJournalMutationObserver
    {
        internal string? Boundary;
        public void Observe(JournalMutationBoundary boundary) { if (boundary.Boundary == Boundary) throw new IOException("Interrupted paged mutation."); }
    }
}
