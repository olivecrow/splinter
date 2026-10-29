using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Splinter.Core;
using Splinter.Runtime;
using Splinter.Platform.Windows;

namespace Splinter.Tests;

public sealed class ManagedDirectoryRestoreOptimizationTests
{
    [Theory]
    [InlineData("file.after-complete", 127, 0, false)]
    [InlineData("file.after-complete", 128, 128, true)]
    [InlineData("file.after-complete", 129, 128, true)]
    [InlineData("restore.before-cursor", 1, 0, true)]
    [InlineData("restore.after-cursor", 1, 128, false)]
    public async Task LostBatchPublicationResumesWithExactFiles(string hook, int occurrence, int saved, bool optimized)
    {
        using var f = new Fixture(260, optimized, existingGeneralPlan: true); int hits = 0;
        await Assert.ThrowsAsync<IOException>(() => f.Restore(stage =>
        { if (stage == hook && ++hits == occurrence) throw new IOException("lost cursor publication/reply"); }));
        var cursor = JsonNode.Parse(File.ReadAllText(Path.Combine(f.Root, "restore-cursor.json")))!;
        Assert.Equal(saved, cursor["completedFiles"]!.GetValue<int>());
        await f.Restore(); f.AssertRestored();
    }

    [Theory]
    [InlineData(127, 1)]
    [InlineData(128, 1)]
    [InlineData(129, 2)]
    public async Task CursorWritesAreBatchedWithoutNewFileReceipts(int count, int publications)
    {
        using var f = new Fixture(count, true, existingGeneralPlan: true); int actual = 0;
        await f.Restore(stage => { if (stage == "restore.after-cursor") actual++; });
        Assert.Equal(publications, actual); f.AssertRestored();
        Assert.Empty(Directory.GetFiles(f.Root, "restore-file-*.json"));
    }

    [Fact]
    public async Task UnchangedRenamedReadOnlyFileUsesTheSamePinnedIdentityForMetadata()
    {
        using var f = new Fixture(2, true, rename: true, existingGeneralPlan: true); string target = Path.Combine(f.State.OriginalPath, "renamed", "f0000");
        string identity;
        using (var file = ManagedRestoreFileAccess.Read(Path.Combine(f.State.Basis.Snapshot.RootPath, "dir", "f0000"))) identity = ManagedRestoreFileAccess.Identity(file);
        bool checkedPin = false;
        await f.Restore(stage =>
        {
            string pinnedTarget = Path.Combine(f.State.OriginalPath, "renamed", "f0001");
            if (stage != "file.after-move" || checkedPin || !File.Exists(pinnedTarget)) return;
            checkedPin = true;
            Assert.Throws<IOException>(() => File.Delete(pinnedTarget));
            Assert.Throws<IOException>(() => { using var writer = new FileStream(pinnedTarget, FileMode.Open, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete); });
        });
        using (var file = ManagedRestoreFileAccess.Read(target)) Assert.Equal(identity, ManagedRestoreFileAccess.Identity(file));
        Assert.True((File.GetAttributes(target) & FileAttributes.ReadOnly) != 0);
        Assert.Equal(f.FinalWriteTime, File.GetLastWriteTimeUtc(target).ToFileTimeUtc());
        Assert.True(checkedPin); f.AssertRestored();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnrecordedReadOnlyEffectsRejectChangedBytesAndMatchingByteReplacements(bool replacement)
    {
        using var f = new Fixture(129, true, rename: true, existingGeneralPlan: true);
        await Assert.ThrowsAsync<IOException>(() => f.Restore(stage =>
        { if (stage == "restore.before-cursor") throw new IOException("lost batch"); }));
        string target = Path.Combine(f.State.OriginalPath, "renamed", "f0000");
        byte[] bytes = File.ReadAllBytes(target); var attributes = File.GetAttributes(target);
        if (replacement)
        {
            File.Move(target, Path.Combine(f.Root, "preserved-file")); File.WriteAllBytes(target, bytes);
        }
        else
        {
            File.SetAttributes(target, FileAttributes.Normal); bytes[0] ^= 1; File.WriteAllBytes(target, bytes);
        }
        File.SetAttributes(target, attributes);
        Assert.Equal("GeneratedRestoreConflict", (await Assert.ThrowsAsync<WorkspaceException>(() => f.Restore())).Code);
        Assert.Equal(bytes, File.ReadAllBytes(target)); Assert.Equal(attributes, File.GetAttributes(target));
        Assert.True(Directory.Exists(f.Request.DeltaPath));
    }

    // Seed only a previously published current-v2 general plan, never a
    // product routing flag. These small fixtures retain the general route's
    // distinct per-file move/cursor/reclamation retry coverage after new
    // inputs become eligible for bulk restoration.
    internal static void SeedGeneralPlan(ManagedDirectoryState state, OverlayServeRequest request,
        OwnedDirectoryManifest manifest, JournalDeltaStore stopped)
    {
        var seal = stopped.Seal(); var entries = stopped.DescribeRestore(); // Requires a stopped read-only journal.
        var originals = manifest.Items.ToDictionary(i => i.Entry.Path, StringComparer.OrdinalIgnoreCase);
        var files = entries.Where(d => !d.Entry.Directory).OrderBy(d => d.Entry.Path, StringComparer.OrdinalIgnoreCase).Select(delta =>
        {
            var entry = delta.Entry;
            Assert.InRange(entry.Length, 0, 1048576); // Deliberately small test data, not a product payload planner.
            byte[] final = new byte[checked((int)entry.Length)]; int offset = 0;
            while (offset < final.Length)
            {
                int read = stopped.Read(entry.Id, offset, final.AsSpan(offset, Math.Min(request.Identity.MaxTransferBytes, final.Length - offset)));
                Assert.True(read > 0); offset += read;
            }
            using var retained = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            string? basisIdentity = null;
            if (entry.BasePath is { } basePath)
            {
                Assert.True(originals.TryGetValue(basePath, out var original));
                Assert.NotNull(original);
                Assert.InRange(entry.BaseLength, 0, original.Entry.Length);
                using var pin = ManagedRestoreFileAccess.Read(Path.Combine(state.Basis.Snapshot.RootPath, basePath.Replace('/', Path.DirectorySeparatorChar)));
                basisIdentity = ManagedRestoreFileAccess.Identity(pin);
                byte[] basisPrefix = new byte[checked((int)entry.BaseLength)]; pin.ReadExactly(basisPrefix);
                int keptThrough = 0;
                foreach (var range in delta.Ranges)
                {
                    int before = checked((int)Math.Min(entry.BaseLength, range.Offset));
                    if (before > keptThrough) retained.AppendData(basisPrefix.AsSpan(keptThrough, before - keptThrough));
                    keptThrough = checked((int)Math.Max(keptThrough, Math.Min(entry.BaseLength, range.Offset + range.Length)));
                }
                if (keptThrough < basisPrefix.Length) retained.AppendData(basisPrefix.AsSpan(keptThrough));
            }
            return new { delta, finalHash = Convert.ToHexStringLower(SHA256.HashData(final)),
                retainedHash = Convert.ToHexStringLower(retained.GetHashAndReset()), basisIdentity };
        }).ToArray();
        var directories = entries.Where(d => d.Entry.Directory).Select(d => new SnapshotEntry(d.Entry.Path, true, 0,
            d.Entry.Attributes, d.Entry.CreationTime, d.Entry.LastAccessTime, d.Entry.LastWriteTime, d.Entry.UnixMode)).OrderBy(d => d.Path.Length).ToArray();
        Assert.Equal(seal, stopped.Seal());
        using var output = new FileStream(Path.Combine(state.Basis.ReceiptDirectory, "restore-plan.json"), FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        JsonSerializer.Serialize(output, new { version = 2, basis = state.Basis, seal, directories, files }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        output.Flush(true);
    }

    internal sealed class Fixture : IDisposable
    {
        private readonly TestDirectory directory = new();
        private readonly int count;
        private readonly bool rename;
        private readonly int payloadBytes;
        public string Root => State.Basis.ReceiptDirectory;
        public ManagedDirectoryState State { get; }
        public OverlayServeRequest Request { get; }
        public long FinalWriteTime { get; } = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc).ToFileTimeUtc();
        public Fixture(int count, bool optimized, bool rename = false, int payloadBytes = 64, bool unchangedDirectory = false, bool bounded = false, bool existingGeneralPlan = false)
        {
            if (!OperatingSystem.IsWindows()) Assert.Skip("Windows owned-file identity contract.");
            this.count = count; this.rename = rename; this.payloadBytes = payloadBytes;
            string root = Path.Combine(directory.Path, "owned"), basis = Path.Combine(root, "base");
            Directory.CreateDirectory(Path.Combine(directory.Path, "original"));
            Directory.CreateDirectory(Path.Combine(basis, "dir"));
            for (int i = 0; i < count; i++) File.WriteAllBytes(Path.Combine(basis, "dir", $"f{i:D4}"), Bytes(i));
            if ((rename || unchangedDirectory) && count > 0) File.SetAttributes(Path.Combine(basis, "dir", "f0000"), FileAttributes.ReadOnly);
            var snapshot = new SnapshotReference("012345678901234567890123", OwnedDirectorySnapshotTree.ProviderName, basis, basis, DateTimeOffset.UnixEpoch, true, true, "stopped-original-owned-directory");
            var manifest = OwnedDirectorySnapshotTree.Capture(snapshot, snapshot.Id); var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
            File.WriteAllText(Path.Combine(root, "manifest.json"), JsonSerializer.Serialize(manifest, json));
            string hash = ManagedDirectoryBasis.HashFile(Path.Combine(root, "manifest.json"));
            File.WriteAllText(Path.Combine(root, "proof.json"), JsonSerializer.Serialize(new OwnedDirectoryProof(1, snapshot, snapshot.Id, hash, manifest.Items.Sum(i => i.Entry.Length)), json));
            State = new() { ApplicationKind = "unity", DirectoryKind = "Library", Key = "fixture", OriginalPath = Path.Combine(directory.Path, "original", "Library"), CompatibilityKey = "fixture", Phase = "restoring",
                Basis = new(snapshot.Id, Path.Combine(directory.Path, "original"), "Library", snapshot, root, snapshot.Id, hash) };
            Request = new(new(2, snapshot.Id, snapshot, 4 * 1048576, 65536), Path.Combine(root, "delta"), State.OriginalPath, Path.Combine(root, "overlay"), "token", new(snapshot.Id, basis, hash), root, snapshot.Id);
            using var tree = new OwnedDirectorySnapshotTree(manifest, snapshot);
            using var journal = new JournalDeltaStore(Request.DeltaPath, Request.Identity, tree, optimizeJournal: optimized,
                boundedLogBytes: bounded ? 8192 : null);
            if (rename)
            {
                journal.Rename(journal.Stat("dir")!.Id, "renamed", false);
                var file = journal.Stat("renamed/f0000")!;
                journal.SetMetadata(file.Id, file.Attributes, file.CreationTime, file.LastAccessTime, FinalWriteTime);
            }
            else if (!unchangedDirectory)
            {
                // Keep a changed topology while preserving every original
                // file payload. General-route tests explicitly fix an existing
                // plan below, so new faster routes cannot bypass their retry coverage.
                var entry = journal.Stat("")!;
                journal.SetMetadata(entry.Id, entry.Attributes, entry.CreationTime, entry.LastAccessTime, entry.LastWriteTime + 1);
                journal.Create("general-route", true, 16);
            }
            if (existingGeneralPlan)
            {
                journal.Dispose();
                using var stoppedBasis = new OwnedDirectorySnapshotTree(manifest, snapshot);
                using var stopped = JournalDeltaStore.OpenReadOnly(Request.DeltaPath, Request.Identity, stoppedBasis);
                SeedGeneralPlan(State, Request, manifest, stopped);
            }
        }
        private byte[] Bytes(int i) => Enumerable.Range(0, payloadBytes).Select(j => (byte)(i + j)).ToArray();
        public Task Restore(Action<string>? boundary = null) => ManagedDirectoryRestore.ExecuteAsync(State, Request, TestContext.Current.CancellationToken, boundary);
        public void AssertRestored()
        {
            for (int i = 0; i < count; i++) Assert.Equal(Bytes(i), File.ReadAllBytes(Path.Combine(State.OriginalPath, rename ? "renamed" : "dir", $"f{i:D4}")));
            Assert.False(Directory.Exists(State.Basis.Snapshot.RootPath)); Assert.False(Directory.Exists(Request.DeltaPath));
        }
        public void Dispose() => directory.Dispose();
    }
}
