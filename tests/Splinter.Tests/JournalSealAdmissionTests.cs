using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Splinter.Core;
using Splinter.Runtime;

namespace Splinter.Tests;

public sealed class JournalSealAdmissionTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void ExhaustedAddressBudgetStillReplaysAndValidatesCompleteAndRetainedFrames(int budget)
    {
        using var fixture = new TestDirectory(); var tree = new SnapshotOverlayTests.FakeSnapshotTree();
        var identity = new OverlayIdentity(2, "bounded-admission", tree.Snapshot, 8L * 1048576, 1048576);
        using (var writer = new JournalDeltaStore(fixture.Path, identity, tree))
        {
            Guid id = writer.Create("file", false, 32).Id;
            for (int i = 0; i < 64; i++) writer.Write(id, i * 64, Enumerable.Repeat((byte)i, 64).ToArray());
            writer.Write(id, 0, [99]);
            writer.Write(writer.Create("peer", false, 32).Id, 0, [42]);
        }
        string root = Path.Combine(fixture.Path, "delta.journal");
        var ordinary = new LatestJournalStorage(root, identity, true);
        var bounded = new LatestJournalStorage(root, identity, true, bufferedBlockAddressLimit: budget);
        string[] names = ordinary.Names().Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(names, bounded.Names().Order(StringComparer.Ordinal));
        foreach (string name in names) Assert.Equal(ordinary.Read(name), bounded.Read(name));
        var seal = LatestJournalStorage.Seal(root);
        Assert.Equal(seal, LatestJournalStorage.Seal(root, bufferedBlockAddressLimit: budget));
        Assert.Equal(IndependentHash(fixture.Path), seal.JournalHash);

        string prefix = ShortPrefixes(fixture.Path).First();
        byte[] original = File.ReadAllBytes(prefix), damaged = (byte[])original.Clone(); damaged[^1] ^= 1;
        File.WriteAllBytes(prefix, damaged);
        Assert.Equal("DeltaCorrupt", Assert.Throws<WorkspaceException>(() => new LatestJournalStorage(root, identity, true, bufferedBlockAddressLimit: budget)).Code);
        Assert.Equal("DeltaCorrupt", Assert.Throws<WorkspaceException>(() => LatestJournalStorage.Seal(root, bufferedBlockAddressLimit: budget)).Code);
        Assert.Equal(damaged, File.ReadAllBytes(prefix));
        File.WriteAllBytes(prefix, original);
        Assert.Equal(seal, LatestJournalStorage.Seal(root, bufferedBlockAddressLimit: budget));
    }

    [Theory]
    [InlineData("latest.after-intent-flush")]
    [InlineData("latest.after-root-validation")]
    public void StreamedAddressesPreserveMixedPayloadAndPagePrefixesAfterRecovery(string boundary)
    {
        using var fixture = new TestDirectory(); var tree = new SnapshotOverlayTests.FakeSnapshotTree();
        var identity = new OverlayIdentity(2, "seal-admission", tree.Snapshot, 64L * 1048576, 1048576);
        var observer = new Interrupt(); Guid id; byte[] expected = new byte[64 * 65539];
        using (var writer = new JournalDeltaStore(fixture.Path, identity, tree, observer))
        {
            id = writer.Create("file", false, 32).Id;
            for (int i = 0; i < 64; i++)
            {
                byte[] bytes = RandomNumberGenerator.GetBytes(65539);
                bytes.CopyTo(expected, i * bytes.Length); writer.Write(id, i * bytes.Length, bytes);
            }
            // Replacing one extent retires a metadata-page suffix while the
            // same old capsule retains its earlier private payload block.
            writer.Write(id, 0, [99]); expected[0] = 99;
            observer.Boundary = boundary;
            Assert.Throws<IOException>(() => writer.Write(id, 65539, [98])); expected[65539] = 98;
        }
        SnapshotDeltaSeal seal;
        using (var resumed = new JournalDeltaStore(fixture.Path, identity, tree))
        {
            byte[] actual = new byte[expected.Length];
            Assert.Equal(actual.Length, resumed.Read(id, 0, actual)); Assert.Equal(expected, actual);
            seal = resumed.Seal();
        }
        Assert.Equal(IndependentHash(fixture.Path), seal.JournalHash);
        Assert.NotEmpty(ShortPrefixes(fixture.Path));
        using var reader = JournalDeltaStore.OpenReadOnly(fixture.Path, identity, tree);
        Assert.Equal(seal, reader.Seal()); Assert.Equal(0, tree.ReadBytes);
    }

    [Fact]
    public void LaterSealRevalidatesRetainedBytesAndDoesNotReuseEarlierAddressProof()
    {
        using var fixture = new TestDirectory(); var tree = new SnapshotOverlayTests.FakeSnapshotTree();
        var identity = new OverlayIdentity(2, "fresh-admission", tree.Snapshot, 8L * 1048576, 1048576);
        using (var writer = new JournalDeltaStore(fixture.Path, identity, tree))
        {
            Guid id = writer.Create("file", false, 32).Id;
            for (int i = 0; i < 64; i++) writer.Write(id, i * 64, Enumerable.Repeat((byte)i, 64).ToArray());
            writer.Write(id, 0, [99]);
        }
        string root = Path.Combine(fixture.Path, "delta.journal"), prefix = ShortPrefixes(fixture.Path).First();
        var seal = JournalDeltaStore.SealJournal(root, 66);
        byte[] original = File.ReadAllBytes(prefix), damaged = (byte[])original.Clone(); damaged[^1] ^= 1;
        File.WriteAllBytes(prefix, damaged);
        Assert.Equal("DeltaCorrupt", Assert.Throws<WorkspaceException>(() => JournalDeltaStore.SealJournal(root, 66)).Code);
        Assert.Equal(damaged, File.ReadAllBytes(prefix));
        File.WriteAllBytes(prefix, original);
        Assert.Equal(seal, JournalDeltaStore.SealJournal(root, 66));
    }

    private static IEnumerable<string> ShortPrefixes(string root) => Directory.EnumerateFiles(root, "commit-*.bin", SearchOption.AllDirectories)
        .Where(path => { byte[] bytes = File.ReadAllBytes(path); return bytes.Length >= 128 && bytes.Length < 128 + BinaryPrimitives.ReadInt64LittleEndian(bytes.AsSpan(16)); });

    private static string IndependentHash(string root)
    {
        string head = Path.Combine(root, "delta.journal");
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256); Span<byte> length = stackalloc byte[8];
        hash.AppendData(File.ReadAllBytes(head));
        foreach (string path in Directory.GetFiles(Directory.GetDirectories(root, "latest-*").Single()).OrderBy(Path.GetFileName, StringComparer.Ordinal))
        {
            byte[] name = Encoding.UTF8.GetBytes(Path.GetFileName(path)), bytes = File.ReadAllBytes(path);
            BinaryPrimitives.WriteInt64LittleEndian(length, name.Length); hash.AppendData(length); hash.AppendData(name);
            BinaryPrimitives.WriteInt64LittleEndian(length, bytes.Length); hash.AppendData(length); hash.AppendData(bytes);
        }
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    private sealed class Interrupt : IJournalMutationObserver
    {
        public string? Boundary { get; set; }
        public void Observe(JournalMutationBoundary boundary)
        {
            if (boundary.Boundary == Boundary) { Boundary = null; throw new IOException("Stopped at " + boundary.Boundary); }
        }
    }
}
