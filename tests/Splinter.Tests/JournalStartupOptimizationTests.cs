using System.Buffers.Binary;
using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using Splinter.Core;
using Splinter.Runtime;

namespace Splinter.Tests;

public sealed class JournalStartupOptimizationTests
{
    private static OverlayIdentity Identity(ISnapshotTree tree) =>
        new(2, "startup-optimization", tree.Snapshot, 64L * 1048576, 1048576);

    [Fact]
    public void AppendingKeepsPayloadButReleasesRetiredFooterAndItemMetadataWhileWriterLives()
    {
        using var root = new TestDirectory();
        var tree = new SnapshotOverlayTests.FakeSnapshotTree();
        var identity = Identity(tree);
        byte[] expected = Bytes(16 * 4096, 17);
        SnapshotDeltaSeal seal;
        Guid id;
        using (var writer = new JournalDeltaStore(root.Path, identity, tree))
        {
            id = writer.Create("append", false, 32).Id;
            var retired = new List<WeakReference>();
            long flushes = writer.Observe().Metrics!.FlushCount;
            for (int i = 0; i < 16; i++)
            {
                writer.Write(id, i * 4096, expected.AsSpan(i * 4096, 4096));
                Assert.Equal(++flushes, writer.Observe().Metrics!.FlushCount);
                if (i < 15) retired.AddRange(CaptureCurrentMetadata(writer));
            }

            CollectRetiredMetadata();
            Assert.NotEmpty(retired);
            Assert.All(retired, reference => Assert.False(reference.IsAlive,
                "A live payload still retains its retired capsule metadata."));
            AssertContent(writer, id, expected);
            Assert.Equal(expected.Length, writer.Observe().PayloadBytes);
            seal = writer.Seal();
            GC.KeepAlive(writer);
        }

        using var resumed = JournalDeltaStore.OpenReadOnly(root.Path, identity, tree);
        Assert.Equal(seal, resumed.Seal());
        AssertContent(resumed, id, expected);
        Assert.Equal(0, tree.ReadBytes);
    }

    [Fact]
    public void MixedCapsuleReclamationKeepsUnrelatedPayloadAndSurvivesWritableReconnect()
    {
        using var root = new TestDirectory();
        var tree = new SnapshotOverlayTests.FakeSnapshotTree();
        var identity = Identity(tree);
        var expected = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        SnapshotDeltaSeal seal;
        SourceChangeView changes;
        Guid hot;
        using (var writer = new JournalDeltaStore(root.Path, identity, tree))
        {
            long flushes = writer.Observe().Metrics!.FlushCount;
            void Mutate(Action action)
            {
                action();
                Assert.Equal(++flushes, writer.Observe().Metrics!.FlushCount);
            }

            Guid directory = Guid.Empty;
            Mutate(() => directory = writer.Create("directory", true, 16).Id);
            for (int i = 0; i < 40; i++)
            {
                string path = $"cold-{i:D2}";
                byte[] bytes = Bytes(257, i);
                Guid id = Guid.Empty;
                Mutate(() => id = writer.Create(path, false, 32).Id);
                Mutate(() => writer.Write(id, 0, bytes));
                expected.Add(path, bytes);
            }

            hot = Guid.Empty;
            Mutate(() => hot = writer.Create("directory/hot", false, 32).Id);
            byte[] content = Bytes(32768, 49);
            for (int i = 0; i < 8; i++)
            {
                int offset = i * 4096;
                Mutate(() => writer.Write(hot, offset, content.AsSpan(offset, 4096)));
            }
            for (int i = 0; i < 3; i++)
            {
                long time = 100 + i;
                Mutate(() => writer.SetMetadata(hot, 32, time, time, time));
            }
            Mutate(() => writer.SetLength(hot, 20000));
            content = content[..20000];
            byte[] replacement = Bytes(8000, 53);
            Mutate(() => writer.Write(hot, 4090, replacement));
            replacement.CopyTo(content, 4090);
            Mutate(() => writer.Rename(directory, "moved", false));
            expected.Add("moved/hot", content);

            Guid deleted = writer.Stat("cold-07")!.Id;
            Mutate(() => writer.Delete(deleted));
            AssertContent(writer, deleted, expected["cold-07"]);
            Mutate(() => writer.ReleaseUnlinkedFile(deleted));
            expected.Remove("cold-07");

            Guid replaced = writer.Stat("cold-09")!.Id;
            Guid replacing = writer.Stat("cold-08")!.Id;
            Mutate(() => writer.Rename(replacing, "cold-09", true));
            AssertContent(writer, replaced, expected["cold-09"]);
            Mutate(() => writer.ReleaseUnlinkedFile(replaced));
            expected["cold-09"] = expected["cold-08"];
            expected.Remove("cold-08");
            Assert.Null(writer.Stat("directory"));
            Assert.Null(writer.Stat("cold-07"));
            Assert.Null(writer.Stat("cold-08"));
            AssertContents(writer, expected);
            changes = writer.ObserveChanges([]);
            seal = writer.Seal();
        }

        using (var resumed = new JournalDeltaStore(root.Path, identity, tree))
        {
            Assert.Equal(seal, resumed.Seal());
            Assert.Equal(changes.Changes, resumed.ObserveChanges([]).Changes);
            AssertContents(resumed, expected);
            long flushes = resumed.Observe().Metrics!.FlushCount;
            byte[] tail = Bytes(1031, 61);
            resumed.Write(hot, expected["moved/hot"].Length, tail);
            Assert.Equal(++flushes, resumed.Observe().Metrics!.FlushCount);
            expected["moved/hot"] = [.. expected["moved/hot"], .. tail];
            resumed.Rename(hot, "final", false);
            Assert.Equal(++flushes, resumed.Observe().Metrics!.FlushCount);
            expected.Add("final", expected["moved/hot"]);
            expected.Remove("moved/hot");
            resumed.SetMetadata(hot, 33, 111, 222, 333);
            Assert.Equal(++flushes, resumed.Observe().Metrics!.FlushCount);
            AssertContents(resumed, expected);
            seal = resumed.Seal();
        }

        using var reader = JournalDeltaStore.OpenReadOnly(root.Path, identity, tree);
        Assert.Equal(seal, reader.Seal());
        AssertContents(reader, expected);
        Assert.Equal(33u, reader.Get(hot).Attributes);
        Assert.Equal(111L, reader.Get(hot).CreationTime);
        Assert.Equal(222L, reader.Get(hot).LastAccessTime);
        Assert.Equal(333L, reader.Get(hot).LastWriteTime);
        Assert.Null(reader.Stat("moved/hot"));
        Assert.Equal(0, tree.ReadBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ChangedMetadataBeyondFirstFooterBufferIsPreservedAndRejected(bool finalByte)
    {
        using var root = new TestDirectory();
        var tree = new SnapshotOverlayTests.FakeSnapshotTree();
        var identity = Identity(tree);
        string path = Path.Combine(root.Path, "delta.journal");
        byte[] changed;
        using (var writer = new JournalDeltaStore(root.Path, identity, tree))
        {
            Guid id = writer.Create("many-nodes", true, 16).Id;
            for (int i = 0; i < 160; i++) writer.Create($"many-nodes/{i:D4}-file", false, 32);
            // A subtree rename legitimately publishes many node records in one
            // footer even though per-file extent metadata is now paged.
            writer.Rename(id, "renamed-many-nodes", false);
            int metadataLength;
            using (var source = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                byte[] header = new byte[128]; source.ReadExactly(header);
                metadataLength = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(24));
            }
            Assert.True(metadataLength > 65536 + 128, "The fixture must exercise more than one footer buffer.");
            writer.Seal();
            changed = ReadSharedBytes(path);
            long dataEnd = 128 + BinaryPrimitives.ReadInt64LittleEndian(changed.AsSpan(16));
            int relativeOffset = finalByte ? metadataLength - 1 : 65536 + 17;
            changed[checked((int)dataEnd + relativeOffset)] ^= 1;
            string replacement = Path.Combine(root.Path, "changed-root.tmp");
            File.WriteAllBytes(replacement, changed);
            File.Replace(replacement, path, null);

            Assert.Equal("DeltaCorrupt", Assert.Throws<WorkspaceException>(() =>
                writer.SetMetadata(id, 33, 111, 222, 333)).Code);
            Assert.Equal(changed, ReadSharedBytes(path));
            Assert.DoesNotContain(Directory.EnumerateFiles(root.Path, "*", SearchOption.AllDirectories),
                file => file.EndsWith(".pending", StringComparison.Ordinal));
        }
        Assert.Equal("DeltaCorrupt", Assert.Throws<WorkspaceException>(() =>
            new JournalDeltaStore(root.Path, identity, tree)).Code);
        Assert.Equal(changed, File.ReadAllBytes(path));
    }

    // Reflection is limited to the lifetime proof: disk size or total managed heap
    // size cannot show whether a surviving payload retains an obsolete footer.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference[] CaptureCurrentMetadata(JournalDeltaStore writer)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        object latest = typeof(JournalDeltaStore).GetField("latest", flags)!.GetValue(writer)!;
        object head = latest.GetType().GetField("head", flags)!.GetValue(latest)!;
        object Property(object instance, string name) => instance.GetType().GetProperty(name, flags)!.GetValue(instance)!;
        object metadata = Property(head, "Metadata");
        object commit = Property(head, "Commit");
        object items = Property(commit, "Items");
        var references = new List<WeakReference> { new(head), new(metadata), new(items) };
        foreach (object item in (IEnumerable)items)
            if (item.GetType().GetProperty("Metadata", flags)!.GetValue(item) is byte[] bytes)
                references.Add(new(bytes));
        Assert.True(references.Count > 3, "The captured capsule must own item metadata and a live payload.");
        return references.ToArray();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void CollectRetiredMetadata()
    {
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
    }

    private static byte[] Bytes(int length, int seed)
    {
        byte[] bytes = new byte[length];
        new Random(seed).NextBytes(bytes);
        return bytes;
    }

    private static void AssertContent(JournalDeltaStore store, Guid id, byte[] expected)
    {
        byte[] actual = new byte[expected.Length];
        Assert.Equal(actual.Length, store.Read(id, 0, actual));
        Assert.Equal(expected, actual);
    }

    private static void AssertContents(JournalDeltaStore store, IReadOnlyDictionary<string, byte[]> expected)
    {
        foreach (var pair in expected)
        {
            var entry = store.Stat(pair.Key);
            Assert.NotNull(entry);
            Assert.Equal(pair.Value.Length, entry.Length);
            AssertContent(store, entry.Id, pair.Value);
        }
        Assert.Equal(expected.Values.Sum(bytes => (long)bytes.Length), store.Observe().PayloadBytes);
    }

    private static byte[] ReadSharedBytes(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        byte[] bytes = new byte[checked((int)stream.Length)];
        stream.ReadExactly(bytes);
        return bytes;
    }
}
