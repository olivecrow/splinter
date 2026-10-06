using System.Collections.Concurrent;
using System.Text.Json;
using Fsp;
using Splinter.Core;
using Splinter.Platform.Windows;
using Splinter.Runtime;

namespace Splinter.Tests;

public sealed class JournalLatestWinFspEvidenceTests
{
    [Fact]
    public void EightFilesRepeatedWritesUnmountAndReconnect()
    {
        string? directory = Environment.GetEnvironmentVariable("SPLINTER_LATEST_WINFSP_EVIDENCE");
        if (directory is null) Assert.Skip("Explicit owned WinFsp fixture requires SPLINTER_LATEST_WINFSP_EVIDENCE.");
        directory = Path.GetFullPath(directory); Assert.False(Directory.Exists(directory)); Directory.CreateDirectory(directory);
        string delta = Path.Combine(directory, "delta"), mount = Path.Combine(directory, "view");
        var tree = new SnapshotOverlayTests.FakeSnapshotTree(); var identity = new OverlayIdentity(2, "latest-native", tree.Snapshot, 10485760, 1048576);
        using (var empty = new JournalDeltaStore(delta, identity, tree)) Assert.True(empty.JournalOptimizationEnabled);
        var errors = new ConcurrentBag<Exception>(); byte[][] expected = Enumerable.Range(0, 8).Select(_ => new byte[8192]).ToArray();
        SnapshotDeltaSeal seal; OverlayStorageObservation storage;
        using (var writer = new JournalDeltaStore(delta, identity, tree))
        {
            var fs = new WinFspOverlayFileSystem(writer, errors.Add); using var host = new FileSystemHost(fs);
            int status = host.MountEx(mount, 8, null, false, 0); Assert.True(status >= 0, "MountEx: 0x" + status.ToString("x8"));
            try
            {
                var random = new Random(61006);
                for (int cycle = 0; cycle < 4; cycle++)
                for (int file = 0; file < expected.Length; file++)
                {
                    random.NextBytes(expected[file]);
                    using var client = new FileStream(Path.Combine(mount, $"file-{file}.bin"), cycle == 0 ? FileMode.CreateNew : FileMode.Open,
                        FileAccess.ReadWrite, FileShare.Read, 4096, FileOptions.None);
                    client.Write(expected[file]); client.Flush(true);
                }
                string unlinked = Path.Combine(mount, "unlinked.bin");
                File.WriteAllBytes(unlinked, expected[0]);
                using (var first = new FileStream(unlinked, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (var second = new FileStream(unlinked, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    File.Delete(unlinked); first.Dispose();
                    byte[] actual = new byte[8192]; second.ReadExactly(actual); Assert.Equal(expected[0], actual);
                }
                Assert.True(SpinWait.SpinUntil(() => fs.RetainedFileContexts == 0, TimeSpan.FromSeconds(5)));
                Assert.Equal(0, fs.OpenHandles); Assert.Equal(0, fs.RetainedFileContexts);
                writer.Flush(); storage = writer.Observe(); seal = writer.Seal();
                Assert.Equal(8 * 8192, storage.PayloadBytes); Assert.Equal(0, tree.ReadBytes); Assert.Empty(errors);
            }
            finally { host.Unmount(); }
        }
        using (var resumed = new JournalDeltaStore(delta, identity, tree))
        {
            Assert.Equal(seal, resumed.Seal()); Assert.True(resumed.JournalOptimizationEnabled);
            for (int file = 0; file < expected.Length; file++)
            { byte[] actual = new byte[8192]; resumed.Read(resumed.Stat($"file-{file}.bin")!.Id, 0, actual); Assert.Equal(expected[file], actual); }
        }
        File.WriteAllText(Path.Combine(directory, "result.json"), JsonSerializer.Serialize(new
        { qualification = "real local WinFsp callbacks / fake immutable basis / 8 files, 4 saves each / two-context unlinked reads and final close / normal unmount and reconnect",
            seal, storage, initialPayloadCopiedBytes = 0, sourceReads = tree.ReadBytes, openHandles = 0, retainedFileContexts = 0 }, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
    }
}
