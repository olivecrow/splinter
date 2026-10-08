using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Splinter.Core;
using Splinter.Runtime;

namespace Splinter.Tests;

public sealed class JournalPublicationHandleTests
{
    private static OverlayIdentity Identity(ISnapshotTree tree) => new(2, "publication-handle", tree.Snapshot, 8L * 1048576, 1048576);

    [Fact]
    public void PublishedRootIsPinnedAcrossValidationAndIndexWithoutCombiningFlushes()
    {
        if (!OperatingSystem.IsWindows()) { Assert.Skip("Windows file sharing pins the published namespace and bytes."); return; }
        using var fixture = new TestDirectory(); var tree = new SnapshotOverlayTests.FakeSnapshotTree();
        var identity = Identity(tree); string path = Path.Combine(fixture.Path, "delta.journal");
        int validations = 0; Guid id; SnapshotDeltaSeal seal;
        var observer = new Observer(boundary =>
        {
            if (boundary != "latest.after-root-validation") return;
            validations++;
            byte[] before = File.ReadAllBytes(path);
            Assert.ThrowsAny<IOException>(() => File.WriteAllBytes(path, before));
            Assert.ThrowsAny<IOException>(() => File.Move(path, path + ".moved"));
            Assert.Equal(before, File.ReadAllBytes(path));
        });
        byte[] payload = RandomNumberGenerator.GetBytes(8192);
        using (var store = new JournalDeltaStore(fixture.Path, identity, tree, observer))
        {
            long flushes = store.Observe().Metrics!.FlushCount;
            id = store.Create("file", false, 32).Id;
            store.Write(id, 0, payload);
            store.Write(id, payload.Length, payload);
            store.Rename(id, "renamed", false);
            Assert.Equal(4, validations);
            Assert.Equal(4, store.Observe().Metrics!.FlushCount - flushes);
            Assert.Equal(payload.Concat(payload).ToArray(), Read(store, id, 2 * payload.Length));
            seal = store.Seal();
        }
        using var resumed = JournalDeltaStore.OpenReadOnly(fixture.Path, identity, tree);
        Assert.Equal(seal, resumed.Seal()); Assert.Equal(id, resumed.Stat("renamed")!.Id);
        Assert.Equal(payload.Concat(payload).ToArray(), Read(resumed, id, 2 * payload.Length));
    }

    [Theory]
    [InlineData("checksum")]
    [InlineData("block-owner")]
    [InlineData("block-length")]
    [InlineData("payload-offset")]
    [InlineData("metadata-hash")]
    [InlineData("item-namespace")]
    [InlineData("generation")]
    public void PublishedCorruptionCannotPassTheCombinedValidationAndIndex(string damage)
    {
        using var fixture = new TestDirectory(); var tree = new SnapshotOverlayTests.FakeSnapshotTree();
        var identity = Identity(tree); string path = Path.Combine(fixture.Path, "delta.journal");
        bool armed = false; byte[]? damaged = null;
        var observer = new Observer(boundary =>
        {
            if (!armed || boundary != "latest.after-object-publication") return;
            damaged = Damage(File.ReadAllBytes(path), damage);
            File.WriteAllBytes(path, damaged);
        });
        using (var store = new JournalDeltaStore(fixture.Path, identity, tree, observer))
        {
            Guid id = store.Create("file", false, 32).Id;
            store.Write(id, 0, RandomNumberGenerator.GetBytes(8192));
            armed = true;
            Assert.Equal("DeltaCorrupt", Assert.Throws<WorkspaceException>(() => store.Write(id, 0, RandomNumberGenerator.GetBytes(8192))).Code);
            Assert.Equal("DeltaReconnectRequired", Assert.Throws<WorkspaceException>(() => store.GetJournalBytes()).Code);
            Assert.NotNull(damaged); Assert.Equal(damaged, File.ReadAllBytes(path));
        }
        string[] files = Directory.GetFiles(fixture.Path, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal).ToArray();
        byte[][] before = files.Select(File.ReadAllBytes).ToArray();
        Assert.Equal("DeltaCorrupt", Assert.Throws<WorkspaceException>(() => new JournalDeltaStore(fixture.Path, identity, tree)).Code);
        Assert.Equal(files, Directory.GetFiles(fixture.Path, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal).ToArray());
        for (int i = 0; i < files.Length; i++) Assert.Equal(before[i], File.ReadAllBytes(files[i]));
    }

    [Fact]
    public void InterruptionBetweenValidationAndIndexReleasesHandleAndRecoversDurableMutation()
    {
        using var fixture = new TestDirectory(); var tree = new SnapshotOverlayTests.FakeSnapshotTree();
        var identity = Identity(tree); string path = Path.Combine(fixture.Path, "delta.journal");
        bool armed = false; Guid id; byte[] payload = RandomNumberGenerator.GetBytes(8192);
        var observer = new Observer(boundary =>
        {
            if (armed && boundary == "latest.after-root-validation") throw new IOException("Interrupted after validation and before indexing.");
        });
        using (var store = new JournalDeltaStore(fixture.Path, identity, tree, observer))
        {
            id = store.Create("file", false, 32).Id;
            store.Write(id, 0, RandomNumberGenerator.GetBytes(8192));
            armed = true;
            Assert.Throws<IOException>(() => store.Write(id, 0, payload));
            Assert.Equal("DeltaReconnectRequired", Assert.Throws<WorkspaceException>(() => store.GetJournalBytes()).Code);
            // The failed call must have closed its publication handle already,
            // even though the poisoned writer still owns its separate locks.
            File.Move(path, path + ".moved"); File.Move(path + ".moved", path);
        }
        SnapshotDeltaSeal seal;
        using (var resumed = new JournalDeltaStore(fixture.Path, identity, tree))
        {
            Assert.Equal(payload, Read(resumed, id, payload.Length));
            Assert.Equal(3, resumed.Observe().DurableSequence);
            Assert.DoesNotContain(Directory.GetFiles(fixture.Path, "*", SearchOption.AllDirectories),
                p => p.EndsWith(".pending", StringComparison.Ordinal) || Path.GetFileName(p).StartsWith("commit-", StringComparison.Ordinal));
            resumed.Write(id, payload.Length, [42]);
            Assert.Equal(payload.Concat(new byte[] { 42 }).ToArray(), Read(resumed, id, payload.Length + 1));
            seal = resumed.Seal();
        }
        using var reader = JournalDeltaStore.OpenReadOnly(fixture.Path, identity, tree);
        Assert.Equal(seal, reader.Seal());
    }

    private static byte[] Read(JournalDeltaStore store, Guid id, int count)
    {
        byte[] bytes = new byte[count]; Assert.Equal(count, store.Read(id, 0, bytes)); return bytes;
    }

    private static byte[] Damage(byte[] bytes, string damage)
    {
        const int headerBytes = 128;
        int dataBytes = checked((int)BinaryPrimitives.ReadInt64LittleEndian(bytes.AsSpan(16)));
        int metadataBytes = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(24));
        if (damage == "checksum") { bytes[^1] ^= 1; return bytes; }
        if (damage == "block-owner") bytes[headerBytes + 16] ^= 1;
        else if (damage == "block-length") BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(headerBytes + 12), 1048577);
        else
        {
            var record = JsonNode.Parse(bytes.AsSpan(headerBytes + dataBytes, metadataBytes))!.AsObject();
            var items = record["Items"]!.AsArray();
            if (damage == "payload-offset")
            {
                var item = items.First(i => i!["Metadata"] is null)!;
                item["Offset"] = item["Offset"]!.GetValue<long>() + 1;
            }
            else if (damage == "metadata-hash") items.First(i => i!["Metadata"] is not null)!["Hash"] = new string('0', 64);
            else if (damage == "item-namespace") items.First(i => i!["Metadata"] is not null)!["Name"] = "../outside.json";
            else if (damage == "generation") record["Generation"] = record["Generation"]!.GetValue<long>() + 1;
            else throw new InvalidOperationException(damage);
            byte[] metadata = JsonSerializer.SerializeToUtf8Bytes(record);
            Array.Resize(ref bytes, headerBytes + dataBytes + metadata.Length + 32);
            metadata.CopyTo(bytes, headerBytes + dataBytes);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(24), metadata.Length);
        }
        // Preserve valid outer checksums for semantic-corruption cases, so the
        // test exercises block/index/ownership guards rather than only SHA256.
        SHA256.HashData(bytes.AsSpan(headerBytes, dataBytes), bytes.AsSpan(96, 32));
        SHA256.HashData(bytes.AsSpan(0, bytes.Length - 32), bytes.AsSpan(bytes.Length - 32));
        return bytes;
    }

    private sealed class Observer(Action<string> action) : IJournalMutationObserver
    {
        public void Observe(JournalMutationBoundary boundary) => action(boundary.Boundary);
    }
}
