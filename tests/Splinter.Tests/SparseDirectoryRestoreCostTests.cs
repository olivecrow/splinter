using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Splinter.Core;
using Splinter.Platform.Windows;
using Splinter.Runtime;

namespace Splinter.Tests;

// Existing product APIs only: copy this test unchanged into the frozen baseline
// and run each candidate in separate, serial processes against identical inputs.
public sealed class SparseDirectoryRestoreCostTests
{
    [Fact]
    public async Task OneLeafDeletionRestoresTheWholeFixedLibrary()
    {
        string? output = Environment.GetEnvironmentVariable("SPLINTER_SPARSE_RESTORE_COST");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(output), "Set SPLINTER_SPARSE_RESTORE_COST to a new evidence path.");
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Requires ordinary NTFS identity and move operations.");
        Assert.False(File.Exists(output));
        const int count = 10000, bytesPerFile = 1024;
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        using var fixture = new TestDirectory();
        string receipt = Path.Combine(fixture.Path, "owned"), basis = Path.Combine(receipt, "base"), project = Path.Combine(fixture.Path, "original");
        Directory.CreateDirectory(Path.Combine(basis, "files")); Directory.CreateDirectory(project);
        DateTime fixedTime = new(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        byte[] Bytes(int index) => Enumerable.Range(0, bytesPerFile).Select(i => (byte)(index + i)).ToArray();
        for (int i = 0; i < count; i++)
        {
            string path = Path.Combine(basis, "files", $"f{i:D5}"); File.WriteAllBytes(path, Bytes(i));
            File.SetCreationTimeUtc(path, fixedTime); File.SetLastAccessTimeUtc(path, fixedTime); File.SetLastWriteTimeUtc(path, fixedTime);
            File.SetAttributes(path, i == 0 ? FileAttributes.ReadOnly : FileAttributes.Normal);
        }
        foreach (string path in new[] { Path.Combine(basis, "files"), basis })
        { Directory.SetCreationTimeUtc(path, fixedTime); Directory.SetLastAccessTimeUtc(path, fixedTime); Directory.SetLastWriteTimeUtc(path, fixedTime); }
        var snapshot = new SnapshotReference("012345678901234567890123", OwnedDirectorySnapshotTree.ProviderName, basis, basis,
            DateTimeOffset.UnixEpoch, true, true, "stopped-original-owned-directory");
        var manifest = OwnedDirectorySnapshotTree.Capture(snapshot, snapshot.Id, Path.Combine(receipt, OwnedDirectoryManifest.MetadataDirectoryName));
        File.WriteAllText(Path.Combine(receipt, "manifest.json"), JsonSerializer.Serialize(manifest, json));
        string manifestHash = ManagedDirectoryBasis.HashFile(Path.Combine(receipt, "manifest.json"));
        File.WriteAllText(Path.Combine(receipt, "proof.json"), JsonSerializer.Serialize(new OwnedDirectoryProof(1, snapshot, snapshot.Id,
            manifestHash, manifest.Items.Sum(i => i.Entry.Length)), json));
        var state = new ManagedDirectoryState { ApplicationKind = "unity", DirectoryKind = "Library", Key = "sparse-cost", Phase = "restoring",
            OriginalPath = Path.Combine(project, "Library"), CompatibilityKey = "sparse-cost",
            Basis = new(snapshot.Id, project, "Library", snapshot, receipt, snapshot.Id, manifestHash) };
        var request = new OverlayServeRequest(new(2, snapshot.Id, snapshot, 4 * 1048576, 65536), Path.Combine(receipt, "delta"),
            state.OriginalPath, Path.Combine(receipt, "overlay"), "token", new(snapshot.Id, basis, manifestHash), receipt, snapshot.Id);
        using (var tree = new OwnedDirectorySnapshotTree(manifest, snapshot))
        using (var journal = new JournalDeltaStore(request.DeltaPath, request.Identity, tree, boundedLogBytes: 8192))
            journal.Delete(journal.Stat("files/f00000")!.Id);
        string inputHash = Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(manifest.Items, json)));
        long started = Stopwatch.GetTimestamp(), allocated = GC.GetTotalAllocatedBytes(); int roots = 0, moves = 0, stages = 0;
        await ManagedDirectoryRestore.ExecuteAsync(state, request, TestContext.Current.CancellationToken, stage =>
        {
            if (stage is "directory.after-move" or "sparse.after-move") roots++;
            if (stage == "file.after-move") moves++;
            if (stage == "sparse.after-stage") stages++;
        });
        double seconds = Stopwatch.GetElapsedTime(started).TotalSeconds; allocated = GC.GetTotalAllocatedBytes() - allocated;
        Assert.False(File.Exists(Path.Combine(state.OriginalPath, "files/f00000")));
        for (int i = 1; i < count; i++)
        {
            string path = Path.Combine(state.OriginalPath, "files", $"f{i:D5}");
            Assert.Equal(Bytes(i), File.ReadAllBytes(path));
            Assert.Equal(fixedTime, File.GetLastWriteTimeUtc(path)); Assert.Equal(fixedTime, File.GetCreationTimeUtc(path));
            Assert.Equal(FileAttributes.Normal, File.GetAttributes(path));
        }
        Assert.Equal(count - 1, Directory.GetFiles(state.OriginalPath, "*", SearchOption.AllDirectories).Length);
        Assert.False(Directory.Exists(basis)); Assert.False(Directory.Exists(request.DeltaPath));
        Assert.Empty(Directory.GetFiles(receipt, "restore-sparse-leaf-*.bin"));
        using var evidence = new FileStream(output!, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        JsonSerializer.Serialize(evidence, new { measuredAt = DateTimeOffset.UtcNow,
            scope = "Single leaf deletion, 10000 fixed ordinary NTFS files; full restore including proof/metadata/reclaim, no Unity.",
            count, bytesPerFile, logicalBytes = (long)count * bytesPerFile, inputHash, seconds, allocatedBytes = allocated,
            rootMoves = roots, survivingFileMoves = moves, deletedLeafStageMoves = stages,
            runtimeHash = ManagedDirectoryBasis.HashFile(typeof(JournalDeltaStore).Assembly.Location),
            windowsHash = ManagedDirectoryBasis.HashFile(typeof(OwnedDirectorySnapshotTree).Assembly.Location),
            verified = true }, json);
    }
}
