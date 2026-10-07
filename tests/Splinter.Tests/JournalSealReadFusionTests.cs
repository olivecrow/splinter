using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Splinter.Core;
using Splinter.Runtime;

namespace Splinter.Tests;

public sealed class JournalSealReadFusionTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(24)]
    public void ValidatedSealMatchesIndependentPhysicalByteFormat(int count)
    {
        using var directory = new TestDirectory();
        var tree = new SnapshotOverlayTests.FakeSnapshotTree();
        var identity = new OverlayIdentity(2, "seal-fusion", tree.Snapshot, 64L * 1048576, 1048576);
        SnapshotDeltaSeal seal;
        using (var store = new JournalDeltaStore(directory.Path, identity, tree, optimizeJournal: true))
        {
            for (int i = 0; i < count; i++)
            {
                var file = store.Create($"f{i:D4}", false, 32);
                store.Write(file.Id, 0, Enumerable.Repeat((byte)(i + 1), 65536).ToArray());
                store.Write(file.Id, 32768, new byte[16384]);
            }
            seal = store.Seal();
        }
        string root = Path.Combine(directory.Path, "delta.journal");
        var paths = JournalDeltaStore.JournalStorageFiles(root);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256); long bytes = 0;
        Span<byte> size = stackalloc byte[8];
        foreach (string path in paths)
        {
            byte[] content = File.ReadAllBytes(path);
            if (path != root)
            {
                byte[] name = Encoding.UTF8.GetBytes(Path.GetFileName(path));
                BinaryPrimitives.WriteInt64LittleEndian(size, name.Length); hash.AppendData(size); hash.AppendData(name);
                BinaryPrimitives.WriteInt64LittleEndian(size, content.Length); hash.AppendData(size);
            }
            hash.AppendData(content); bytes += content.Length;
        }
        Assert.Equal(bytes, seal.JournalBytes); Assert.Equal(Convert.ToHexStringLower(hash.GetHashAndReset()), seal.JournalHash);
        Assert.Equal(seal, JournalDeltaStore.SealJournal(root, seal.DurableSequence));
        if (count == 0) return;
        string frame = paths.Where(p => new FileInfo(p).Length > 1024).OrderByDescending(p => new FileInfo(p).Length).First();
        using (var file = new FileStream(frame, FileMode.Open, FileAccess.ReadWrite))
        { file.Position = 150; int value = file.ReadByte(); file.Position = 150; file.WriteByte((byte)(value ^ 1)); file.Flush(true); }
        Assert.Equal("DeltaCorrupt", Assert.Throws<WorkspaceException>(() => JournalDeltaStore.SealJournal(root, seal.DurableSequence)).Code);
    }
}
