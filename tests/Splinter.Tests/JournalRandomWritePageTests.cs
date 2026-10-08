using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Splinter.Core;
using Splinter.Runtime;

namespace Splinter.Tests;

public sealed class JournalRandomWritePageTests
{
    private static OverlayIdentity Identity(ISnapshotTree tree) => new(2, "random-pages", tree.Snapshot, 64L * 1048576, 1048576);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ScatteredTinyWritesReusePagesWithoutCopyingUnchangedPrivatePayload(bool descending)
    {
        using var root = new TestDirectory(); var tree = new SnapshotOverlayTests.FakeSnapshotTree();
        var identity = Identity(tree); byte[] expected = new byte[512 * 128]; Guid id, peer;
        using (var writer = new JournalDeltaStore(root.Path, identity, tree))
        {
            id = writer.Create("random", false, 32).Id; writer.SetLength(id, expected.Length);
            peer = writer.Create("peer", false, 32).Id; writer.Write(peer, 0, [7, 8, 9]);
        }
        for (int pass = 0; pass < 2; pass++)
        {
            using var writer = new JournalDeltaStore(root.Path, identity, tree);
            var before = writer.Observe().Metrics!;
            for (int i = 0; i < 512; i++)
            {
                int at = (descending ? 511 - i : (i * 317 + pass * 127) % 512) * 128;
                byte[] data = new byte[64]; new Random(pass * 512 + i).NextBytes(data);
                data.CopyTo(expected, at); writer.Write(id, at, data);
                // Raw capsule data distinguishes SJB1 user bytes from SJM2
                // pages, so metadata retirement cannot hide payload carry.
                Assert.Equal(64, HeadPrivateBytes(root.Path));
            }
            var after = writer.Observe().Metrics!;
            Assert.Equal(512, after.FlushCount - before.FlushCount);
            Assert.InRange(after.JournalAppendedBytes - before.JournalAppendedBytes, 512 * 64, 512 * 20000);
            AssertContents(writer, id, expected); AssertContents(writer, peer, [7, 8, 9]);
            Assert.Equal(512 * 64 + 3, writer.Observe().PayloadBytes);
            Assert.Equal(IndependentSealHash(root.Path), writer.Seal().JournalHash);
        }
        using var reopened = JournalDeltaStore.OpenReadOnly(root.Path, identity, tree);
        AssertContents(reopened, id, expected); AssertContents(reopened, peer, [7, 8, 9]);
        Assert.Equal(IndependentSealHash(root.Path), reopened.Seal().JournalHash); Assert.Equal(0, tree.ReadBytes);
    }

    [Theory]
    [InlineData("header")]
    [InlineData("payload")]
    [InlineData("partial-block")]
    [InlineData("missing-block")]
    public void RetainedPageRetirementPrefixDamageIsPreservedAndRejected(string kind)
    {
        using var root = new TestDirectory(); var tree = new SnapshotOverlayTests.FakeSnapshotTree();
        var identity = Identity(tree); CreateRetiredPage(root.Path, identity, tree);
        string prefix = ShortPrefixes(root.Path).First(); byte[] bytes = ReadSharedBytes(prefix);
        if (kind == "header") bytes[8] ^= 1;
        else if (kind == "payload") bytes[^1] ^= 1;
        else if (kind == "partial-block") bytes = bytes[..^1];
        else bytes = bytes[..128];
        File.WriteAllBytes(prefix, bytes);
        var before = Snapshot(root.Path);
        Assert.Equal("DeltaCorrupt", Assert.Throws<WorkspaceException>(() => JournalDeltaStore.OpenReadOnly(root.Path, identity, tree)).Code);
        Assert.Equal("DeltaCorrupt", Assert.Throws<WorkspaceException>(() => new JournalDeltaStore(root.Path, identity, tree)).Code);
        AssertSnapshot(before, root.Path);
    }

    [Theory]
    [InlineData("latest.after-intent-header", false)]
    [InlineData("latest.before-intent-flush", true)]
    [InlineData("latest.after-root-publication", true)]
    [InlineData("latest.after-object-reclamation", true)]
    public void PageReplacementRecoversWithoutDuplicatingOrRetainingOldRanges(string boundary, bool completed)
    {
        using var root = new TestDirectory(); var tree = new SnapshotOverlayTests.FakeSnapshotTree();
        var identity = Identity(tree); var observer = new StopAt(); Guid id; byte[] expected = new byte[64 * 64];
        using (var writer = new JournalDeltaStore(root.Path, identity, tree, observer))
        {
            id = writer.Create("file", false, 32).Id;
            for (int i = 0; i < 64; i++)
            {
                byte[] data = Enumerable.Repeat((byte)(i + 1), 64).ToArray(); data.CopyTo(expected, i * 64); writer.Write(id, i * 64, data);
            }
            observer.Boundary = boundary;
            Assert.Throws<IOException>(() => writer.Write(id, 0, [99]));
        }
        if (completed) expected[0] = 99;
        using (var resumed = new JournalDeltaStore(root.Path, identity, tree))
        {
            AssertContents(resumed, id, expected); Assert.Equal(expected.Length, resumed.Observe().PayloadBytes);
            Assert.Equal(IndependentSealHash(root.Path), resumed.Seal().JournalHash);
        }
        using var reader = JournalDeltaStore.OpenReadOnly(root.Path, identity, tree);
        AssertContents(reader, id, expected); Assert.Equal(IndependentSealHash(root.Path), reader.Seal().JournalHash);
    }

    [Fact]
    public void ChangedRetirementTargetAfterHashingIsRejectedAndPreserved()
    {
        using var root = new TestDirectory(); var tree = new SnapshotOverlayTests.FakeSnapshotTree();
        var identity = Identity(tree); string head = Path.Combine(root.Path, "delta.journal");
        var observer = new ChangeAfterRetirementPlan(head);
        using (var writer = new JournalDeltaStore(root.Path, identity, tree, observer))
        {
            Guid id = writer.Create("file", false, 32).Id;
            for (int i = 0; i < 64; i++) writer.Write(id, i * 64, Enumerable.Repeat((byte)(i + 1), 64).ToArray());
            writer.Write(id, 0, [99]);
            // This next page replacement keeps the first write's private block
            // in the old head. Change it after both retirement hashes were
            // calculated, proving final reclamation still reads the target.
            observer.Enabled = true;
            Assert.Equal("DeltaCorrupt", Assert.Throws<WorkspaceException>(() => writer.Write(id, 64, [98])).Code);
            Assert.True(observer.Changed);
        }
        var before = Snapshot(root.Path);
        Assert.Equal("DeltaCorrupt", Assert.Throws<WorkspaceException>(() => new JournalDeltaStore(root.Path, identity, tree)).Code);
        AssertSnapshot(before, root.Path);
    }

    private static void CreateRetiredPage(string root, OverlayIdentity identity, ISnapshotTree tree)
    {
        using var writer = new JournalDeltaStore(root, identity, tree);
        Guid id = writer.Create("file", false, 32).Id;
        for (int i = 0; i < 64; i++) writer.Write(id, i * 64, Enumerable.Repeat((byte)(i + 1), 64).ToArray());
        writer.Write(id, 0, [99]); writer.Seal(); Assert.NotEmpty(ShortPrefixes(root));
    }

    private static IEnumerable<string> ShortPrefixes(string root) => Directory.EnumerateFiles(root, "commit-*.bin", SearchOption.AllDirectories)
        .Where(path => { byte[] b = ReadSharedBytes(path); return b.Length >= 128 && b.Length < 128 + BinaryPrimitives.ReadInt64LittleEndian(b.AsSpan(16)); });
    private static long HeadPrivateBytes(string root)
    {
        byte[] bytes = ReadSharedBytes(Path.Combine(root, "delta.journal"));
        int end = checked(128 + (int)BinaryPrimitives.ReadInt64LittleEndian(bytes.AsSpan(16))); long total = 0;
        for (int at = 128; at < end;)
        {
            int length = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(at + 12));
            if (bytes.AsSpan(at, 4).SequenceEqual("SJB1"u8)) total += length;
            else Assert.True(bytes.AsSpan(at, 4).SequenceEqual("SJM2"u8));
            at += 80 + length;
        }
        return total;
    }
    private static string IndependentSealHash(string root)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(ReadSharedBytes(Path.Combine(root, "delta.journal")));
        string directory = Directory.GetDirectories(root, "latest-*").Single();
        foreach (string path in Directory.GetFiles(directory).OrderBy(Path.GetFileName, StringComparer.Ordinal))
        {
            byte[] name = Encoding.UTF8.GetBytes(Path.GetFileName(path)), bytes = ReadSharedBytes(path);
            byte[] size = new byte[8]; BinaryPrimitives.WriteInt64LittleEndian(size, name.Length); hash.AppendData(size); hash.AppendData(name);
            BinaryPrimitives.WriteInt64LittleEndian(size, bytes.Length); hash.AppendData(size); hash.AppendData(bytes);
        }
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }
    private static void AssertContents(JournalDeltaStore store, Guid id, byte[] expected)
    {
        byte[] actual = new byte[expected.Length]; Assert.Equal(actual.Length, store.Read(id, 0, actual)); Assert.Equal(expected, actual);
    }
    private static Dictionary<string, byte[]> Snapshot(string root) => Directory.GetFiles(root, "*", SearchOption.AllDirectories).ToDictionary(p => p, ReadSharedBytes);
    private static void AssertSnapshot(Dictionary<string, byte[]> before, string root)
    {
        Assert.Equal(before.Keys.Order(), Directory.GetFiles(root, "*", SearchOption.AllDirectories).Order());
        foreach (var item in before) Assert.Equal(item.Value, ReadSharedBytes(item.Key));
    }
    private static byte[] ReadSharedBytes(string path)
    {
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        byte[] bytes = new byte[checked((int)file.Length)]; file.ReadExactly(bytes); return bytes;
    }
    private sealed class StopAt : IJournalMutationObserver
    {
        internal string? Boundary;
        public void Observe(JournalMutationBoundary boundary) { if (boundary.Boundary == Boundary) throw new IOException("Interrupted page replacement."); }
    }
    private sealed class ChangeAfterRetirementPlan(string path) : IJournalMutationObserver
    {
        internal bool Enabled, Changed;
        public void Observe(JournalMutationBoundary boundary)
        {
            if (!Enabled || Changed || boundary.Boundary != "latest.after-retirement-plan") return;
            using var file = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete);
            file.Position = 128 + 80; int original = file.ReadByte(); Assert.NotEqual(-1, original);
            file.Position--; file.WriteByte((byte)(original ^ 1)); file.Flush(true); Changed = true;
        }
    }
}
