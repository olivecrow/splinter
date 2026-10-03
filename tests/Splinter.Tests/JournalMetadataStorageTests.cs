using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using Splinter.Core;
using Splinter.Runtime;

namespace Splinter.Tests;

public sealed class JournalMetadataStorageTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LiteralCurrentFramesWithOmittedOrExplicitDefaultsKeepTheirState(bool explicitDefaults)
    {
        using var root = new TestDirectory(); var tree = new SnapshotOverlayTests.FakeSnapshotTree();
        var identity = new OverlayIdentity(2, "metadata", tree.Snapshot, 1024 * 1024, 4096);
        File.WriteAllText(Path.Combine(root.Path, "identity.json"), JsonSerializer.Serialize(identity));
        string journal = Path.Combine(root.Path, "delta.journal");
        var entry = new DeltaEntry(Guid.Parse("617e1748-b4dd-4d37-acf7-121a2c13236a"), "file", false, 0, 32, 1, 2, 3, null, 0);
        using (var output = File.Create(journal))
        {
            AppendLiteral(output, 1, "create", entry, [], explicitDefaults);
            entry = entry with { Length = 4 };
            AppendLiteral(output, 2, "write", entry, [1, 2, 3, 4], explicitDefaults);
            AppendLiteral(output, 3, "write", entry, [9], explicitDefaults, offset: 2);
            entry = entry with { Attributes = 33, LastWriteTime = 7 };
            AppendLiteral(output, 4, "metadata", entry, [], explicitDefaults);
            AppendLiteral(output, 5, "rename", entry, [], explicitDefaults, destination: "target", replace: true);
        }
        byte[] original = File.ReadAllBytes(journal); SourceChangeView changes;
        using (var reader = JournalDeltaStore.OpenReadOnly(root.Path, identity, tree))
        {
            changes = reader.ObserveChanges([]);
            AssertState(reader, entry with { Path = "target" });
        }
        Assert.Equal(original, File.ReadAllBytes(journal)); // Opening never rewrites saved current frames.
        long estimate;
        using (var reader = new JournalDeltaStore(root.Path, identity, tree))
            estimate = reader.EstimateCompactionJournalOverlapBytes();
        var receipt = JournalDeltaStore.CompactDirectory(root.Path, identity, tree,
            maxEstimatedJournalOverlapBytes: estimate);
        Assert.Equal(original.LongLength + receipt.After.JournalBytes, estimate);
        Assert.True(receipt.After.JournalBytes <= original.LongLength);
        if (explicitDefaults) Assert.True(receipt.After.JournalBytes < original.LongLength);
        Assert.Equal(original, File.ReadAllBytes(Path.Combine(root.Path, "delta.journal.previous")));
        using (var reader = new JournalDeltaStore(root.Path, identity, tree))
        {
            AssertState(reader, entry with { Path = "target" });
            Assert.Equal(changes.Changes, reader.ObserveChanges([]).Changes);
        }
        AssertReducedFrames(journal);
        JournalDeltaStore.CompleteCompaction(root.Path, identity);
        Assert.Equal(0, tree.ReadBytes);
    }

    [Fact]
    public void NewRawAndEncodedWritesPreserveOneFlushAndNondefaultRenameFields()
    {
        using var root = new TestDirectory(); var tree = new SnapshotOverlayTests.FakeSnapshotTree();
        var identity = new OverlayIdentity(2, "metadata-write", tree.Snapshot, 1024 * 1024, 4096);
        byte[] expected = new byte[4096]; Guid id;
        using (var store = new JournalDeltaStore(root.Path, identity, tree))
        {
            id = store.Create("file", false, 32).Id;
            store.Create("target", false, 32);
            var before = store.Observe(); store.Write(id, 0, expected); var after = store.Observe();
            Assert.Equal(before.DurableSequence + 1, after.DurableSequence);
            Assert.Equal(before.Metrics!.FlushCount + 1, after.Metrics!.FlushCount);
            Assert.True(after.PayloadBytes < expected.Length);
            store.Write(id, 2, [9]); expected[2] = 9;
            store.Rename(id, "target", true);
        }
        AssertReducedFrames(Path.Combine(root.Path, "delta.journal"));
        using var replay = new JournalDeltaStore(root.Path, identity, tree);
        Assert.Null(replay.Stat("file")); Assert.Equal(id, replay.Stat("target")!.Id);
        byte[] actual = new byte[expected.Length]; Assert.Equal(actual.Length, replay.Read(id, 0, actual));
        Assert.Equal(expected, actual); Assert.Equal(0, tree.ReadBytes);
    }

    private static void AssertState(JournalDeltaStore store, DeltaEntry entry)
    {
        Assert.Equal(5, store.Observe().DurableSequence);
        Assert.Null(store.Stat("file")); Assert.Equal(entry, store.Stat("target"));
        byte[] actual = new byte[4]; Assert.Equal(4, store.Read(entry.Id, 0, actual));
        Assert.Equal(new byte[] { 1, 2, 9, 4 }, actual);
    }

    // Independent current SJR2 writer: the fixture does not use Mutation serialization.
    private static void AppendLiteral(Stream output, long sequence, string operation, DeltaEntry entry,
        byte[] payload, bool defaults, long offset = 0, string? destination = null, bool replace = false)
    {
        var mutation = new Dictionary<string, object?> { ["Sequence"] = sequence, ["Operation"] = operation, ["Entry"] = entry };
        if (defaults || offset != 0) mutation["Offset"] = offset;
        if (defaults || destination is not null) mutation["Destination"] = destination;
        if (defaults || replace) mutation["Replace"] = replace;
        byte[] metadata = JsonSerializer.SerializeToUtf8Bytes(mutation);
        Span<byte> header = stackalloc byte[16]; "SJR2"u8.CopyTo(header);
        BinaryPrimitives.WriteInt32LittleEndian(header[4..], metadata.Length);
        BinaryPrimitives.WriteInt32LittleEndian(header[8..], payload.Length);
        BinaryPrimitives.WriteInt32LittleEndian(header[12..], payload.Length);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(header); hash.AppendData(metadata); hash.AppendData(payload);
        output.Write(header); output.Write(metadata); output.Write(payload); output.Write(hash.GetHashAndReset());
    }

    private static void AssertReducedFrames(string path)
    {
        bool nonzeroOffset = false, replaceRename = false;
        using var input = File.OpenRead(path);
        byte[] header = new byte[16];
        while (input.Position < input.Length)
        {
            input.ReadExactly(header); Assert.Equal("SJR2"u8.ToArray(), header[..4]);
            int length = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(4));
            int payload = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(8));
            byte[] metadata = new byte[length]; input.ReadExactly(metadata);
            using var document = JsonDocument.Parse(metadata); var frame = document.RootElement;
            if (frame.TryGetProperty("Offset", out var offset)) { Assert.NotEqual(0, offset.GetInt64()); nonzeroOffset = true; }
            if (frame.TryGetProperty("Destination", out var target)) Assert.Equal("target", target.GetString());
            if (frame.TryGetProperty("Replace", out var replace)) { Assert.True(replace.GetBoolean()); replaceRename = true; }
            input.Position += payload + 32;
        }
        Assert.True(nonzeroOffset); Assert.True(replaceRename);
    }
}
