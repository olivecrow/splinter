using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
using Splinter.Core;
using Splinter.Platform.Windows;
using Splinter.Runtime;

namespace Splinter.Tests;

public sealed class FastWorkspaceOptimizationTests
{
    private sealed class FailingReadSnapshotTree(ISnapshotTree inner, Func<string, bool> shouldFail) : ISnapshotTree
    {
        public SnapshotReference Snapshot => inner.Snapshot;
        public void EnsureAvailable() => inner.EnsureAvailable();
        public SnapshotEntry? Stat(string path) => inner.Stat(path);
        public IReadOnlyList<SnapshotEntry> List(string path) => inner.List(path);
        public int Read(string path, long offset, Span<byte> destination)
        {
            if (shouldFail(path)) throw new IOException($"Simulated basis read failure on '{path}'.");
            return inner.Read(path, offset, destination);
        }
    }

    private static (string BasisRoot, string ReceiptRoot, string OriginalPath, string DeltaPath, SnapshotReference Snapshot, OwnedDirectoryManifest Manifest, ManagedDirectoryState State, OverlayServeRequest Request)
        CreateFastLibraryFixture(string root, string id, Dictionary<string, byte[]> files, Action<string>? afterWriteFiles = null)
    {
        string basisRoot = Path.Combine(root, "base");
        string receiptRoot = Path.Combine(root, "receipt");
        string originalPath = Path.Combine(root, "project", "Library");
        string deltaPath = Path.Combine(receiptRoot, "delta");
        Directory.CreateDirectory(basisRoot);
        Directory.CreateDirectory(receiptRoot);
        foreach (var (relative, data) in files)
        {
            string full = Path.Combine(basisRoot, relative.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllBytes(full, data);
        }
        afterWriteFiles?.Invoke(basisRoot);
        File.WriteAllBytes(Path.Combine(receiptRoot, "readers.lock"), []);

        var snapshot = new SnapshotReference(id, OwnedDirectorySnapshotTree.ProviderName,
            basisRoot, basisRoot, DateTimeOffset.UtcNow, true, true, "stopped-original-owned-directory");
        string fastIndexPath = Path.Combine(receiptRoot, OwnedDirectoryManifest.FastIndexFileName);
        var manifest = OwnedDirectorySnapshotTree.CaptureFastLibrary(snapshot, id, fastIndexPath, CancellationToken.None);
        WindowsProcessWorkspaceProvider.WriteAsync(Path.Combine(receiptRoot, "manifest.json"), manifest).GetAwaiter().GetResult();
        string manifestHash = ManagedDirectoryBasis.HashFile(Path.Combine(receiptRoot, "manifest.json"));
        WindowsProcessWorkspaceProvider.WriteAsync(Path.Combine(receiptRoot, "proof.json"),
            new OwnedDirectoryProof(1, snapshot, id, manifestHash, manifest.FastIndexLogicalBytes!.Value)).GetAwaiter().GetResult();
        var reloaded = new OwnedDirectorySnapshotProvider(receiptRoot, id).Read(snapshot);
        var identity = new OverlayIdentity(3, id, snapshot, SnapshotWorkspaceState.DefaultJournalBytes, 1024 * 1024);
        var basis = new GeneratedDirectoryReference(id, Path.Combine(root, "project"), "Library",
            snapshot, receiptRoot, id, manifestHash);
        var state = new ManagedDirectoryState
        {
            ApplicationKind = "unity",
            DirectoryKind = "Library",
            Key = "fast-" + id[..6],
            OriginalPath = originalPath,
            CompatibilityKey = "fixed",
            Basis = basis,
            Phase = "restoring"
        };
        var request = new OverlayServeRequest(identity, deltaPath, originalPath, Path.Combine(receiptRoot, "overlay"),
            "tok-" + id[..6], new(snapshot.Id, basisRoot, manifestHash), receiptRoot, id);
        return (basisRoot, receiptRoot, originalPath, deltaPath, snapshot, reloaded, state, request);
    }

    [Fact]
    public void EphemeralPartitionIsolatesLibraryWritesFromSourceJournalAndPersistsAcrossReconnect()
    {
        using var directory = new TestDirectory();
        var sourceBasis = new SnapshotOverlayTests.FakeSnapshotTree();
        sourceBasis.Files["Assets/Game.cs"] = "class Game {}"u8.ToArray();
        var generatedBasis = new SnapshotOverlayTests.FakeSnapshotTree
        {
            Snapshot = new("gen", "fake-memory", "gen-source", "gen-root", DateTimeOffset.UnixEpoch, true, true, "fixture")
        };
        byte[] baseArtifact = new byte[8192];
        new Random(17).NextBytes(baseArtifact);
        generatedBasis.Files["ArtifactDB"] = baseArtifact;
        var basisRef = new GeneratedDirectoryReference("gen", "project", "Library", generatedBasis.Snapshot, "receipt", "owner", "manifest");
        var graft = new SnapshotGraft("Library", basisRef);
        var tree = new GraftedSnapshotTree(sourceBasis, generatedBasis, graft);
        var identity = new OverlayIdentity(3, "fast-part", sourceBasis.Snapshot, SnapshotWorkspaceState.DefaultJournalBytes, 1024 * 1024, Graft: graft);
        string deltaDir = Path.Combine(directory.Path, "delta");
        string ephemeralDir = Path.Combine(directory.Path, "ephemeral");
        string[] patterns = ["Library", "Temp", "Logs", "*.csproj"];

        using (var journal = new JournalDeltaStore(deltaDir, identity, tree, boundedLogBytes: JournalDeltaStore.DefaultBoundedLogBytes))
        using (var ephemeral = new EphemeralRangeStore(ephemeralDir, identity, generatedBasis, "Library", patterns))
        using (var partitioned = new PartitionedWorkspaceDeltaStore(journal, ephemeral))
        {
            // Partial range write on a basis-backed Library file: zero frames in source journal!
            var artifact = partitioned.OpenStat("Library/ArtifactDB")!;
            partitioned.Write(artifact.Id, 100, [11, 22, 33, 44]);
            Assert.Equal(0, journal.Observe().DurableSequence);

            // Create new ephemeral files in Temp and top-level *.csproj.
            var tempDir = partitioned.Create("Temp", true, 16);
            var staging = partitioned.Create("Temp/staging.tmp", false, 128);
            partitioned.Write(staging.Id, 0, "hello-cross"u8);
            var csproj = partitioned.Create("Assembly-CSharp.csproj", false, 128);
            partitioned.Write(csproj.Id, 0, "<Project/>"u8);
            Assert.Equal(0, journal.Observe().DurableSequence);

            // Truncate-to-0 promotes basis file to native private file cleanly.
            var libNew = partitioned.Create("Library/temp.bin", false, 128);
            partitioned.Write(libNew.Id, 0, [1, 2, 3]);
            partitioned.SetLength(libNew.Id, 0);
            partitioned.Write(libNew.Id, 0, [9, 8]);

            // Cross-partition rename: Temp/staging.tmp -> Assets/Staged.cs (now durable in source journal!).
            partitioned.Rename(staging.Id, "Assets/Staged.cs", replace: false);
            Assert.Equal("Assets/Staged.cs", partitioned.Get(staging.Id).Path);
            Assert.Null(partitioned.Stat("Temp/staging.tmp"));
            Assert.NotNull(partitioned.Stat("Assets/Staged.cs"));
            Assert.True(journal.Observe().DurableSequence > 0);

            // Root listing merges both partitions without duplicating Library.
            var rootNames = partitioned.EnumerateChildren("").Select(e => e.Path).ToArray();
            Assert.Contains("Assets", rootNames);
            Assert.Contains("Library", rootNames);
            Assert.Contains("Temp", rootNames);
            Assert.Contains("Assembly-CSharp.csproj", rootNames);
            Assert.Equal(1, rootNames.Count(n => n.Equals("Library", StringComparison.OrdinalIgnoreCase)));
            Assert.NotNull(tempDir);
        }

        // Reconnect: verify both durable source file and persisted ephemeral ranges/files survive cleanly.
        using (var journal = new JournalDeltaStore(deltaDir, identity, tree, boundedLogBytes: JournalDeltaStore.DefaultBoundedLogBytes))
        using (var ephemeral = new EphemeralRangeStore(ephemeralDir, identity, generatedBasis, "Library", patterns))
        using (var partitioned = new PartitionedWorkspaceDeltaStore(journal, ephemeral))
        {
            var staged = partitioned.OpenStat("Assets/Staged.cs")!;
            byte[] stagedBytes = new byte[staged.Length];
            Assert.Equal(stagedBytes.Length, partitioned.Read(staged.Id, 0, stagedBytes));
            Assert.Equal("hello-cross"u8.ToArray(), stagedBytes);

            var artifact = partitioned.OpenStat("Library/ArtifactDB")!;
            byte[] slice = new byte[8];
            Assert.Equal(8, partitioned.Read(artifact.Id, 98, slice));
            Assert.Equal(baseArtifact[98], slice[0]);
            Assert.Equal(baseArtifact[99], slice[1]);
            Assert.Equal(new byte[] { 11, 22, 33, 44 }, slice[2..6]);
            Assert.Equal(baseArtifact[104], slice[6]);

            var libNew = partitioned.OpenStat("Library/temp.bin")!;
            byte[] libBytes = new byte[2];
            Assert.Equal(2, partitioned.Read(libNew.Id, 0, libBytes));
            Assert.Equal(new byte[] { 9, 8 }, libBytes);
        }
    }

    [Fact]
    public void CrossPartitionRenameReadFailurePreservesExistingAssetsFile()
    {
        using var directory = new TestDirectory();
        var sourceBasis = new SnapshotOverlayTests.FakeSnapshotTree();
        sourceBasis.Files["Assets/Save.asset"] = "OLD"u8.ToArray();
        var innerGen = new SnapshotOverlayTests.FakeSnapshotTree
        {
            Snapshot = new("gen", "fake-memory", "gen-source", "gen-root", DateTimeOffset.UnixEpoch, true, true, "fixture")
        };
        innerGen.Files["Broken.asset"] = "NEW-PAYLOAD"u8.ToArray();
        var failingGen = new FailingReadSnapshotTree(innerGen, path => path.Equals("Broken.asset", StringComparison.OrdinalIgnoreCase));
        var basisRef = new GeneratedDirectoryReference("gen", "project", "Library", innerGen.Snapshot, "receipt", "owner", "manifest");
        var graft = new SnapshotGraft("Library", basisRef);
        var tree = new GraftedSnapshotTree(sourceBasis, failingGen, graft);
        var identity = new OverlayIdentity(3, "xpart-fail", sourceBasis.Snapshot, SnapshotWorkspaceState.DefaultJournalBytes, 1024 * 1024, Graft: graft);

        using var journal = new JournalDeltaStore(Path.Combine(directory.Path, "delta"), identity, tree, boundedLogBytes: JournalDeltaStore.DefaultBoundedLogBytes);
        using var ephemeral = new EphemeralRangeStore(Path.Combine(directory.Path, "ephemeral"), identity, failingGen, "Library", ["Library", "Temp"]);
        using var partitioned = new PartitionedWorkspaceDeltaStore(journal, ephemeral);

        var broken = partitioned.OpenStat("Library/Broken.asset")!;
        Assert.Throws<IOException>(() => partitioned.Rename(broken.Id, "Assets/Save.asset", replace: true));

        // Existing Assets/Save.asset must retain its original bytes intact and no staging temp should remain.
        var existing = partitioned.OpenStat("Assets/Save.asset")!;
        Assert.Equal(3, existing.Length);
        byte[] content = new byte[existing.Length];
        Assert.Equal(3, partitioned.Read(existing.Id, 0, content));
        Assert.Equal("OLD"u8.ToArray(), content);
        Assert.DoesNotContain(partitioned.EnumerateChildren("Assets"), e => e.Path.Contains(".splinter-xpart-", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(partitioned.Stat("Library/Broken.asset"));
    }

    [Fact]
    public void CrossPartitionRenameHonorsReplaceFalseWhenDestinationExists()
    {
        using var directory = new TestDirectory();
        var sourceBasis = new SnapshotOverlayTests.FakeSnapshotTree();
        sourceBasis.Files["Assets/Existing.asset"] = "EXISTING"u8.ToArray();
        var identity = new OverlayIdentity(3, "xpart-norep", sourceBasis.Snapshot, SnapshotWorkspaceState.DefaultJournalBytes, 1024 * 1024);

        using var journal = new JournalDeltaStore(Path.Combine(directory.Path, "delta"), identity, sourceBasis, boundedLogBytes: JournalDeltaStore.DefaultBoundedLogBytes);
        using var ephemeral = new EphemeralRangeStore(Path.Combine(directory.Path, "ephemeral"), identity, null, null, ["Temp"]);
        using var partitioned = new PartitionedWorkspaceDeltaStore(journal, ephemeral);

        partitioned.Create("Temp", true, 16);
        var incoming = partitioned.Create("Temp/incoming.asset", false, 128);
        partitioned.Write(incoming.Id, 0, "NEW"u8);

        Assert.Throws<IOException>(() => partitioned.Rename(incoming.Id, "Assets/Existing.asset", replace: false));

        var existing = partitioned.OpenStat("Assets/Existing.asset")!;
        byte[] content = new byte[existing.Length];
        Assert.Equal(content.Length, partitioned.Read(existing.Id, 0, content));
        Assert.Equal("EXISTING"u8.ToArray(), content);
        Assert.DoesNotContain(partitioned.EnumerateChildren("Assets"), e => e.Path.Contains(".splinter-xpart-", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(partitioned.Stat("Temp/incoming.asset"));
    }

    [Fact]
    public void CrossPartitionRenameInterruptionAfterDestinationCommitConvergesOnRetry()
    {
        using var directory = new TestDirectory();
        var sourceBasis = new SnapshotOverlayTests.FakeSnapshotTree();
        sourceBasis.Files["Assets/Target.asset"] = "OLD"u8.ToArray();
        var identity = new OverlayIdentity(3, "xpart-retry", sourceBasis.Snapshot, SnapshotWorkspaceState.DefaultJournalBytes, 1024 * 1024);

        using var journal = new JournalDeltaStore(Path.Combine(directory.Path, "delta"), identity, sourceBasis, boundedLogBytes: JournalDeltaStore.DefaultBoundedLogBytes);
        using var ephemeral = new EphemeralRangeStore(Path.Combine(directory.Path, "ephemeral"), identity, null, null, ["Temp"]);
        using var partitioned = new PartitionedWorkspaceDeltaStore(journal, ephemeral);

        partitioned.Create("Temp", true, 16);
        var staged = partitioned.Create("Temp/staged.asset", false, 128);
        partitioned.Write(staged.Id, 0, "NEW-CONTENT"u8);

        partitioned.CrossPartitionBoundary = stage =>
        {
            if (stage == "after-destination-commit") throw new IOException("Simulated interruption after destination commit.");
        };

        Assert.Throws<IOException>(() => partitioned.Rename(staged.Id, "Assets/Target.asset", replace: true));
        // Destination already holds complete new bytes while source still exists before retry.
        var midTarget = partitioned.OpenStat("Assets/Target.asset")!;
        byte[] midBytes = new byte[midTarget.Length];
        Assert.Equal(midBytes.Length, partitioned.Read(midTarget.Id, 0, midBytes));
        Assert.Equal("NEW-CONTENT"u8.ToArray(), midBytes);
        Assert.NotNull(partitioned.Stat("Temp/staged.asset"));

        partitioned.CrossPartitionBoundary = null;
        partitioned.Rename(staged.Id, "Assets/Target.asset", replace: true);
        Assert.Null(partitioned.Stat("Temp/staged.asset"));
        var finalTarget = partitioned.OpenStat("Assets/Target.asset")!;
        byte[] finalBytes = new byte[finalTarget.Length];
        Assert.Equal(finalBytes.Length, partitioned.Read(finalTarget.Id, 0, finalBytes));
        Assert.Equal("NEW-CONTENT"u8.ToArray(), finalBytes);
    }

    [Fact]
    public void CrossPartitionDirectoryRenamePreservesPreOpenedChildHandleForWrites()
    {
        using var directory = new TestDirectory();
        var sourceBasis = new SnapshotOverlayTests.FakeSnapshotTree();
        sourceBasis.Files["Assets/Root.cs"] = "class Root {}"u8.ToArray();
        var identity = new OverlayIdentity(3, "xpart-dir", sourceBasis.Snapshot, SnapshotWorkspaceState.DefaultJournalBytes, 1024 * 1024);
        string deltaDir = Path.Combine(directory.Path, "delta");
        string ephemeralDir = Path.Combine(directory.Path, "ephemeral");

        using (var journal = new JournalDeltaStore(deltaDir, identity, sourceBasis, boundedLogBytes: JournalDeltaStore.DefaultBoundedLogBytes))
        using (var ephemeral = new EphemeralRangeStore(ephemeralDir, identity, null, null, ["Temp"]))
        using (var partitioned = new PartitionedWorkspaceDeltaStore(journal, ephemeral))
        {
            partitioned.Create("Temp", true, 16);
            var tempDir = partitioned.Create("Temp/movedir", true, 16);
            var child = partitioned.Create("Temp/movedir/child.txt", false, 128);
            partitioned.Write(child.Id, 0, "v1"u8);

            // Move the parent directory across partitions while child.Id remains open.
            partitioned.Rename(tempDir.Id, "Assets/movedir", replace: false);
            Assert.Null(partitioned.Stat("Temp/movedir"));
            Assert.Equal("Assets/movedir/child.txt", partitioned.Get(child.Id).Path);

            // Subsequent writes via the pre-opened child handle must land at the new durable Assets path.
            partitioned.Write(child.Id, 0, "v2-updated"u8);

            var byPath = partitioned.OpenStat("Assets/movedir/child.txt")!;
            byte[] buf = new byte[byPath.Length];
            Assert.Equal(buf.Length, partitioned.Read(byPath.Id, 0, buf));
            Assert.Equal("v2-updated"u8.ToArray(), buf);

            partitioned.Rename(partitioned.Stat("Assets/movedir")!.Id, "Temp/back", replace: false);
            partitioned.ReleaseUnlinkedFile(byPath.Id); // closing the intermediate alias must not break the first handle
            partitioned.Write(child.Id, 0, "v3"u8);
            Assert.Equal("Temp/back/child.txt", partitioned.Get(child.Id).Path);
            partitioned.Rename(tempDir.Id, "Assets/movedir", replace: false);
        }

        // Reconnect source journal and confirm the write through the redirected handle persisted durably.
        using (var journal = new JournalDeltaStore(deltaDir, identity, sourceBasis, boundedLogBytes: JournalDeltaStore.DefaultBoundedLogBytes))
        {
            var persisted = journal.Stat("Assets/movedir/child.txt")!;
            byte[] buf = new byte[persisted.Length];
            Assert.Equal(buf.Length, journal.Read(persisted.Id, 0, buf));
            Assert.Equal("v3-updated"u8.ToArray(), buf);
        }
    }

    [Fact]
    public void EphemeralRangeStoreReusesExistingBasisRangeExtentInPlace()
    {
        using var directory = new TestDirectory();
        var generatedBasis = new SnapshotOverlayTests.FakeSnapshotTree
        {
            Snapshot = new("gen", "fake-memory", "gen-source", "gen-root", DateTimeOffset.UnixEpoch, true, true, "fixture")
        };
        generatedBasis.Files["ArtifactDB"] = new byte[16384];
        var identity = new OverlayIdentity(3, "range-reuse", generatedBasis.Snapshot, SnapshotWorkspaceState.DefaultJournalBytes, 1024 * 1024);

        using var ephemeral = new EphemeralRangeStore(Path.Combine(directory.Path, "ephemeral"), identity, generatedBasis, "Library", ["Library"]);
        var entry = ephemeral.Stat("Library/ArtifactDB", retainForHandle: true)!;
        byte[] block = new byte[4096];
        for (int i = 0; i < 256; i++)
        {
            Array.Fill(block, (byte)i);
            ephemeral.Write(entry.Id, 0, block);
        }
        Assert.Equal(4096, ephemeral.RangesFileLength);

        byte[] readBack = new byte[4096];
        Assert.Equal(4096, ephemeral.Read(entry.Id, 0, readBack));
        Assert.All(readBack, b => Assert.Equal((byte)255, b));
    }

    [Fact]
    public async Task FastLibraryCaptureLazyReadAndUnchangedOrModifiedRestore()
    {
        if (!OperatingSystem.IsWindows()) Assert.Skip("Windows native fast Library basis test.");
        using var directory = new TestDirectory();
        var ct = TestContext.Current.CancellationToken;
        byte[] fileA = [1, 2, 3, 4, 5, 6];
        byte[] fileB = [9, 8, 7, 6];
        var f = CreateFastLibraryFixture(directory.Path, "aaaaaaaaaaaaaaaaaaaaaaaa", new()
        {
            ["a.bin"] = fileA,
            ["sub/b.bin"] = fileB
        });

        // Lazy reader serves Stat, List, and Read without eagerly opening all files.
        Assert.NotNull(f.Manifest.FastIndexPath);
        Assert.NotNull(f.Manifest.FastIndexHash);
        using (var lazyTree = OwnedDirectorySnapshotTree.OpenLazy(f.Manifest, f.Snapshot))
        {
            Assert.Equal(fileA.Length, lazyTree.Stat("a.bin")!.Length);
            Assert.Equal("sub/b.bin", lazyTree.List("sub").Single().Path);
            byte[] buf = new byte[fileB.Length];
            Assert.Equal(fileB.Length, lazyTree.Read("sub/b.bin", 0, buf));
            Assert.Equal(fileB, buf);
        }

        // Test modified restore path (edits + new file + delete in delta).
        using (var lazyTree = OwnedDirectorySnapshotTree.OpenLazy(f.Manifest, f.Snapshot))
        using (var journal = new JournalDeltaStore(f.DeltaPath, f.Request.Identity, lazyTree, boundedLogBytes: JournalDeltaStore.DefaultBoundedLogBytes,
            sourceFilePins: new WindowsJournalFilePins()))
        {
            journal.Write(journal.Stat("a.bin")!.Id, 2, [99, 88]);
            var added = journal.Create("sub/new.bin", false, 128);
            journal.Write(added.Id, 0, [42, 43]);
            journal.Delete(journal.Stat("sub/b.bin")!.Id);
        }

        await ManagedFastLibraryRestore.ExecuteAsync(f.State, f.Request, f.Manifest, ct);
        Assert.True(Directory.Exists(f.OriginalPath));
        Assert.False(Directory.Exists(f.BasisRoot));
        Assert.False(Directory.Exists(f.DeltaPath));
        Assert.False(File.Exists(Path.Combine(f.ReceiptRoot, ManagedFastLibraryRestore.PlanFileName)));
        Assert.Equal(new byte[] { 1, 2, 99, 88, 5, 6 }, await File.ReadAllBytesAsync(Path.Combine(f.OriginalPath, "a.bin"), ct));
        Assert.Equal(new byte[] { 42, 43 }, await File.ReadAllBytesAsync(Path.Combine(f.OriginalPath, "sub", "new.bin"), ct));
        Assert.False(File.Exists(Path.Combine(f.OriginalPath, "sub", "b.bin")));
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(false, true)]
    public void InterruptedCrossPartitionRenameResumesWithoutReplacingNewerWrites(bool replace, bool toCache)
    {
        using var directory = new TestDirectory();
        var basis = new SnapshotOverlayTests.FakeSnapshotTree();
        basis.Files["Assets/Ready"] = [1];
        var identity = new OverlayIdentity(3, "move-recovery", basis.Snapshot, SnapshotWorkspaceState.DefaultJournalBytes, 1024 * 1024);
        PartitionedWorkspaceDeltaStore Open() => new(
            new JournalDeltaStore(Path.Combine(directory.Path, "delta"), identity, basis, boundedLogBytes: JournalDeltaStore.DefaultBoundedLogBytes),
            new EphemeralRangeStore(Path.Combine(directory.Path, "cache"), identity, null, null, ["Temp"]));
        string from = toCache ? "Assets/source" : "Temp/source", to = toCache ? "Temp/target" : "Assets/target";
        Guid original;
        using (var fs = Open())
        {
            fs.Create("Temp", true, 16);
            original = fs.Create(from, false, 128).Id;
            fs.Write(original, 0, "draft"u8);
            if (replace) fs.Create(to, false, 128);
            fs.CrossPartitionBoundary = _ => throw new IOException("lost response");
            Assert.Throws<IOException>(() => fs.Rename(original, to, replace));
            fs.Write(fs.Stat(to)!.Id, 0, "later"u8);
        }
        using var resumed = Open();
        resumed.Rename(original, to, replace);
        Assert.Null(resumed.Stat(from));
        byte[] bytes = new byte[5];
        Assert.Equal(bytes.Length, resumed.Read(resumed.Stat(to)!.Id, 0, bytes));
        Assert.Equal("later"u8.ToArray(), bytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ChangedLibraryRefusesDamagedStagingOrLaterOriginalEdit(bool published)
    {
        if (!OperatingSystem.IsWindows()) Assert.Skip("Windows NTFS restore.");
        using var directory = new TestDirectory();
        var ct = TestContext.Current.CancellationToken;
        var f = CreateFastLibraryFixture(directory.Path, "eeeeeeeeeeeeeeeeeeeeeeee", new() { ["a.bin"] = "OLD"u8.ToArray() });
        using (var tree = OwnedDirectorySnapshotTree.OpenLazy(f.Manifest, f.Snapshot))
        using (var journal = new JournalDeltaStore(f.DeltaPath, f.Request.Identity, tree,
            boundedLogBytes: JournalDeltaStore.DefaultBoundedLogBytes, sourceFilePins: new WindowsJournalFilePins()))
            journal.Write(journal.Stat("a.bin")!.Id, 0, "NEW"u8);
        string boundary = published ? "modified.after-root-move" : "modified.after-plan";
        await Assert.ThrowsAsync<IOException>(() => ManagedDirectoryRestore.ExecuteAsync(f.State, f.Request, ct,
            phase => { if (phase == boundary) throw new IOException("interrupted"); }));
        string changed = published ? Path.Combine(f.OriginalPath, "a.bin")
            : Directory.GetFiles(Directory.GetDirectories(f.ReceiptRoot, "restore-stage-*").Single(), "*.bin").Single();
        await File.WriteAllTextAsync(changed, published ? "later-original" : "BAD", ct);
        var failure = await Assert.ThrowsAsync<WorkspaceException>(() => ManagedDirectoryRestore.ExecuteAsync(f.State, f.Request, ct));
        Assert.Equal("GeneratedRestoreConflict", failure.Code);
        Assert.Equal(published ? "later-original" : "BAD", await File.ReadAllTextAsync(changed, ct));
        if (!published) Assert.Equal("OLD", await File.ReadAllTextAsync(Path.Combine(f.BasisRoot, "a.bin"), ct));
        Assert.True(Directory.Exists(f.DeltaPath));
        Assert.True(File.Exists(Path.Combine(f.ReceiptRoot, ManagedFastLibraryRestore.PlanFileName)));
    }

    [Fact]
    public async Task ChangedLibraryKeepsActualSiblingAndExactCaseOfFileAndFolder()
    {
        if (!OperatingSystem.IsWindows()) Assert.Skip("Windows NTFS restore.");
        using var directory = new TestDirectory();
        var ct = TestContext.Current.CancellationToken;
        var f = CreateFastLibraryFixture(directory.Path, "ffffffffffffffffffffffff", new()
        { [".splinter-restore.tmp"] = [7], ["changed"] = [1], ["sub/mixed.txt"] = [3] });
        using (var tree = OwnedDirectorySnapshotTree.OpenLazy(f.Manifest, f.Snapshot))
        using (var journal = new JournalDeltaStore(f.DeltaPath, f.Request.Identity, tree,
            boundedLogBytes: JournalDeltaStore.DefaultBoundedLogBytes, sourceFilePins: new WindowsJournalFilePins()))
        {
            journal.Write(journal.Stat("changed")!.Id, 0, [2]);
            journal.Rename(journal.Stat("sub/mixed.txt")!.Id, "sub/MIXED.TXT", false);
            journal.Rename(journal.Stat("sub")!.Id, "SUB", false);
        }
        await ManagedDirectoryRestore.ExecuteAsync(f.State, f.Request, ct);
        Assert.Equal(new byte[] { 7 }, await File.ReadAllBytesAsync(Path.Combine(f.OriginalPath, ".splinter-restore.tmp"), ct));
        Assert.Equal("SUB", Path.GetFileName(Directory.GetDirectories(f.OriginalPath).Single()));
        Assert.Equal("MIXED.TXT", Path.GetFileName(Directory.GetFiles(Path.Combine(f.OriginalPath, "SUB")).Single()));
    }

    [Fact]
    public async Task FastLibraryUnchangedRestorePerformsInstantMove()
    {
        if (!OperatingSystem.IsWindows()) Assert.Skip("Windows native fast Library basis test.");
        using var directory = new TestDirectory();
        var ct = TestContext.Current.CancellationToken;
        var f = CreateFastLibraryFixture(directory.Path, "bbbbbbbbbbbbbbbbbbbbbbbb", new()
        {
            ["cache.bin"] = [5, 4, 3, 2, 1]
        });

        using (var lazyTree = OwnedDirectorySnapshotTree.OpenLazy(f.Manifest, f.Snapshot))
        using (var journal = new JournalDeltaStore(f.DeltaPath, f.Request.Identity, lazyTree, boundedLogBytes: JournalDeltaStore.DefaultBoundedLogBytes,
            sourceFilePins: new WindowsJournalFilePins()))
        {
            Assert.Equal(0, journal.Observe().DurableSequence);
        }

        await ManagedFastLibraryRestore.ExecuteAsync(f.State, f.Request, f.Manifest, ct);
        Assert.True(Directory.Exists(f.OriginalPath));
        Assert.False(Directory.Exists(f.BasisRoot));
        Assert.False(Directory.Exists(f.DeltaPath));
        Assert.Equal(new byte[] { 5, 4, 3, 2, 1 }, await File.ReadAllBytesAsync(Path.Combine(f.OriginalPath, "cache.bin"), ct));
    }

    [Fact]
    public async Task FastLibraryPlanlessRetryRefusesWhenModifiedDeltaRemainsAndPreservesDelta()
    {
        if (!OperatingSystem.IsWindows()) Assert.Skip("Windows native fast Library basis test.");
        using var directory = new TestDirectory();
        var ct = TestContext.Current.CancellationToken;
        var f = CreateFastLibraryFixture(directory.Path, "111111111111111111111111", new()
        {
            ["a.bin"] = "OLD"u8.ToArray()
        });

        using (var lazyTree = OwnedDirectorySnapshotTree.OpenLazy(f.Manifest, f.Snapshot))
        using (var journal = new JournalDeltaStore(f.DeltaPath, f.Request.Identity, lazyTree, boundedLogBytes: JournalDeltaStore.DefaultBoundedLogBytes,
            sourceFilePins: new WindowsJournalFilePins()))
        {
            journal.Write(journal.Stat("a.bin")!.Id, 0, "NEW"u8);
        }

        // Simulate a moved basis without fast-restore-plan.json while modified delta changes remain.
        Directory.CreateDirectory(Path.GetDirectoryName(f.OriginalPath)!);
        Directory.Move(f.BasisRoot, f.OriginalPath);

        var error = await Assert.ThrowsAsync<WorkspaceException>(() =>
            ManagedFastLibraryRestore.ExecuteAsync(f.State, f.Request, f.Manifest, ct));
        Assert.Equal("GeneratedRestoreConflict", error.Code);
        Assert.True(Directory.Exists(f.DeltaPath));
        Assert.Equal("OLD"u8.ToArray(), await File.ReadAllBytesAsync(Path.Combine(f.OriginalPath, "a.bin"), ct));
    }

    [Fact]
    public async Task FastLibraryModifiedRestoreResumesAfterRootMoveInterruption()
    {
        if (!OperatingSystem.IsWindows()) Assert.Skip("Windows native fast Library basis test.");
        using var directory = new TestDirectory();
        var ct = TestContext.Current.CancellationToken;
        var f = CreateFastLibraryFixture(directory.Path, "222222222222222222222222", new()
        {
            ["a.bin"] = "OLD1"u8.ToArray(),
            ["b.bin"] = "OLD2"u8.ToArray(),
            ["del.bin"] = "GONE"u8.ToArray()
        });

        using (var lazyTree = OwnedDirectorySnapshotTree.OpenLazy(f.Manifest, f.Snapshot))
        using (var journal = new JournalDeltaStore(f.DeltaPath, f.Request.Identity, lazyTree, boundedLogBytes: JournalDeltaStore.DefaultBoundedLogBytes,
            sourceFilePins: new WindowsJournalFilePins()))
        {
            journal.Write(journal.Stat("a.bin")!.Id, 0, "NEW1"u8);
            journal.Write(journal.Stat("b.bin")!.Id, 0, "NEW2"u8);
            journal.Delete(journal.Stat("del.bin")!.Id);
            journal.Create("sub", true, 16);
            var added = journal.Create("sub/added.bin", false, 128);
            journal.Write(added.Id, 0, "ADD"u8);
        }

        await Assert.ThrowsAsync<IOException>(() => ManagedFastLibraryRestore.ExecuteAsync(f.State, f.Request, f.Manifest, ct,
            stage => { if (stage == "modified.after-root-move") throw new IOException("Simulated crash after root move."); }));

        // Plan, staging, and delta must all remain preserved for retry.
        Assert.True(Directory.Exists(f.OriginalPath));
        Assert.False(Directory.Exists(f.BasisRoot));
        Assert.True(File.Exists(Path.Combine(f.ReceiptRoot, ManagedFastLibraryRestore.PlanFileName)));
        Assert.True(Directory.Exists(f.DeltaPath));
        Assert.NotEmpty(Directory.EnumerateDirectories(f.ReceiptRoot, "restore-stage-*"));

        // Retry with the exact same request completes cleanly.
        await ManagedFastLibraryRestore.ExecuteAsync(f.State, f.Request, f.Manifest, ct);
        Assert.Equal("NEW1"u8.ToArray(), await File.ReadAllBytesAsync(Path.Combine(f.OriginalPath, "a.bin"), ct));
        Assert.Equal("NEW2"u8.ToArray(), await File.ReadAllBytesAsync(Path.Combine(f.OriginalPath, "b.bin"), ct));
        Assert.Equal("ADD"u8.ToArray(), await File.ReadAllBytesAsync(Path.Combine(f.OriginalPath, "sub", "added.bin"), ct));
        Assert.False(File.Exists(Path.Combine(f.OriginalPath, "del.bin")));
        Assert.False(Directory.Exists(f.DeltaPath));
        Assert.False(File.Exists(Path.Combine(f.ReceiptRoot, ManagedFastLibraryRestore.PlanFileName)));
        Assert.Empty(Directory.EnumerateDirectories(f.ReceiptRoot, "restore-stage-*"));
    }

    [Fact]
    public async Task FastLibraryModifiedRestoreResumesAfterFirstOfTwoFileInstalls()
    {
        if (!OperatingSystem.IsWindows()) Assert.Skip("Windows native fast Library basis test.");
        using var directory = new TestDirectory();
        var ct = TestContext.Current.CancellationToken;
        var f = CreateFastLibraryFixture(directory.Path, "333333333333333333333333", new()
        {
            ["a.bin"] = "OLD1"u8.ToArray(),
            ["b.bin"] = "OLD2"u8.ToArray()
        });

        using (var lazyTree = OwnedDirectorySnapshotTree.OpenLazy(f.Manifest, f.Snapshot))
        using (var journal = new JournalDeltaStore(f.DeltaPath, f.Request.Identity, lazyTree, boundedLogBytes: JournalDeltaStore.DefaultBoundedLogBytes,
            sourceFilePins: new WindowsJournalFilePins()))
        {
            journal.Write(journal.Stat("a.bin")!.Id, 0, "NEW1"u8);
            journal.Write(journal.Stat("b.bin")!.Id, 0, "NEW2"u8);
        }

        int installed = 0;
        await Assert.ThrowsAsync<IOException>(() => ManagedFastLibraryRestore.ExecuteAsync(f.State, f.Request, f.Manifest, ct,
            stage =>
            {
                if (stage == "modified.after-file-install" && ++installed == 1)
                    throw new IOException("Simulated crash after first file install.");
            }));

        Assert.Equal(1, installed);
        Assert.True(File.Exists(Path.Combine(f.ReceiptRoot, ManagedFastLibraryRestore.PlanFileName)));
        Assert.True(Directory.Exists(f.DeltaPath));
        Assert.NotEmpty(Directory.EnumerateDirectories(f.ReceiptRoot, "restore-stage-*"));

        // Retry installs the second file and cleans up plan, staging, and delta.
        await ManagedFastLibraryRestore.ExecuteAsync(f.State, f.Request, f.Manifest, ct);
        Assert.Equal("NEW1"u8.ToArray(), await File.ReadAllBytesAsync(Path.Combine(f.OriginalPath, "a.bin"), ct));
        Assert.Equal("NEW2"u8.ToArray(), await File.ReadAllBytesAsync(Path.Combine(f.OriginalPath, "b.bin"), ct));
        Assert.False(Directory.Exists(f.DeltaPath));
        Assert.False(File.Exists(Path.Combine(f.ReceiptRoot, ManagedFastLibraryRestore.PlanFileName)));
        Assert.Empty(Directory.EnumerateDirectories(f.ReceiptRoot, "restore-stage-*"));
    }

    [Fact]
    public async Task FastLibraryRestorePreservesMetadataOnlyChangesOnUnchangedBytes()
    {
        if (!OperatingSystem.IsWindows()) Assert.Skip("Windows native fast Library basis test.");
        using var directory = new TestDirectory();
        var ct = TestContext.Current.CancellationToken;
        var initialWriteUtc = new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        var updatedWriteUtc = new DateTime(2022, 6, 15, 12, 30, 0, DateTimeKind.Utc);
        var f = CreateFastLibraryFixture(directory.Path, "444444444444444444444444", new()
        {
            ["meta.bin"] = "SAME"u8.ToArray()
        }, basisRoot =>
        {
            string p = Path.Combine(basisRoot, "meta.bin");
            File.SetLastWriteTimeUtc(p, initialWriteUtc);
            File.SetAttributes(p, FileAttributes.Archive);
        });

        using (var lazyTree = OwnedDirectorySnapshotTree.OpenLazy(f.Manifest, f.Snapshot))
        using (var journal = new JournalDeltaStore(f.DeltaPath, f.Request.Identity, lazyTree, boundedLogBytes: JournalDeltaStore.DefaultBoundedLogBytes,
            sourceFilePins: new WindowsJournalFilePins()))
        {
            var entry = journal.Stat("meta.bin")!;
            journal.SetMetadata(entry.Id, (uint)(FileAttributes.ReadOnly | FileAttributes.Archive),
                entry.CreationTime, entry.LastAccessTime, updatedWriteUtc.ToFileTimeUtc());
        }

        string restoredFile = Path.Combine(f.OriginalPath, "meta.bin");
        try
        {
            await ManagedFastLibraryRestore.ExecuteAsync(f.State, f.Request, f.Manifest, ct);
            Assert.Equal("SAME"u8.ToArray(), await File.ReadAllBytesAsync(restoredFile, ct));
            Assert.Equal(updatedWriteUtc, File.GetLastWriteTimeUtc(restoredFile));
            Assert.True((File.GetAttributes(restoredFile) & FileAttributes.ReadOnly) != 0);
        }
        finally
        {
            if (File.Exists(restoredFile))
                File.SetAttributes(restoredFile, FileAttributes.Normal);
        }
    }

    [Fact]
    public void FastLibraryCaptureRejectsHardlinksInSharedBasis()
    {
        if (!OperatingSystem.IsWindows()) Assert.Skip("Windows NTFS hardlink check.");
        using var directory = new TestDirectory();
        string basisRoot = Path.Combine(directory.Path, "base");
        Directory.CreateDirectory(basisRoot);
        string a = Path.Combine(basisRoot, "a.bin");
        string b = Path.Combine(basisRoot, "b.bin");
        File.WriteAllText(a, "shared");
        Assert.True(CreateHardLinkW(b, a, 0));

        var snapshot = new SnapshotReference("555555555555555555555555", OwnedDirectorySnapshotTree.ProviderName,
            basisRoot, basisRoot, DateTimeOffset.UtcNow, true, true, "stopped-original-owned-directory");
        string fastIndexPath = Path.Combine(directory.Path, OwnedDirectoryManifest.FastIndexFileName);

        var error = Assert.Throws<WorkspaceException>(() =>
            OwnedDirectorySnapshotTree.CaptureFastLibrary(snapshot, snapshot.Id, fastIndexPath, CancellationToken.None));
        Assert.Equal("SnapshotFileUnsupported", error.Code);
    }

    [Fact]
    public void FastLibraryLazyReadDetectsSameLengthContentOverwrite()
    {
        if (!OperatingSystem.IsWindows()) Assert.Skip("Windows native fast Library basis test.");
        using var directory = new TestDirectory();
        var oldWriteUtc = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var f = CreateFastLibraryFixture(directory.Path, "666666666666666666666666", new()
        {
            ["a.bin"] = "111"u8.ToArray()
        }, basisRoot => File.SetLastWriteTimeUtc(Path.Combine(basisRoot, "a.bin"), oldWriteUtc));

        using var lazyTree = OwnedDirectorySnapshotTree.OpenLazy(f.Manifest, f.Snapshot);
        // External mutation of an unread basis file with the exact same byte length updates NTFS mtime.
        File.WriteAllBytes(Path.Combine(f.BasisRoot, "a.bin"), "222"u8.ToArray());

        byte[] buf = new byte[3];
        var error = Assert.Throws<WorkspaceException>(() => lazyTree.Read("a.bin", 0, buf));
        Assert.Equal("GeneratedBasisChanged", error.Code);
    }

    [Fact]
    public void FastOwnedDirectoryIndexRejectsTamperedChecksumAndHashMismatch()
    {
        using var directory = new TestDirectory();
        string indexPath = Path.Combine(directory.Path, "fast-index.bin");
        long now = DateTime.UtcNow.ToFileTimeUtc();
        var items = new SnapshotEntry[]
        {
            new("", true, 0, (uint)FileAttributes.Directory, now, now, now),
            new("a.bin", false, 4, (uint)FileAttributes.Archive, now, now, now)
        };
        var (_, _, hash) = FastOwnedDirectoryIndex.Write(indexPath, items);
        Assert.Equal(2, FastOwnedDirectoryIndex.Load(indexPath, hash).Count);

        string wrongHash = (hash[0] == 'a' ? 'b' : 'a') + hash[1..];
        Assert.Equal("GeneratedBasisCorrupt",
            Assert.Throws<WorkspaceException>(() => FastOwnedDirectoryIndex.Load(indexPath, wrongHash)).Code);

        byte[] raw = File.ReadAllBytes(indexPath);
        raw[10] ^= 0xFF;
        File.WriteAllBytes(indexPath, raw);
        Assert.Equal("GeneratedBasisCorrupt",
            Assert.Throws<WorkspaceException>(() => FastOwnedDirectoryIndex.Load(indexPath)).Code);
    }

    [Fact]
    public void WinFspKernelCachingMountIsolatesEphemeralWritesAndFlushesDirtySourceBeforeCapture()
    {
        if (!OperatingSystem.IsWindows() || Microsoft.Win32.Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\WinFsp", "InstallDir", null) is not string)
            Assert.Skip("Requires Windows with WinFsp installed.");
        using var directory = new TestDirectory();
        var sourceBasis = new SnapshotOverlayTests.FakeSnapshotTree();
        sourceBasis.Files["Assets/Initial.cs"] = "class Initial {}"u8.ToArray();
        var generatedBasis = new SnapshotOverlayTests.FakeSnapshotTree
        {
            Snapshot = new("gen", "fake-memory", "gen-source", "gen-root", DateTimeOffset.UnixEpoch, true, true, "fixture")
        };
        generatedBasis.Files["ArtifactDB"] = new byte[4096];
        var basisRef = new GeneratedDirectoryReference("gen", "project", "Library", generatedBasis.Snapshot, "receipt", "owner", "manifest");
        var graft = new SnapshotGraft("Library", basisRef);
        var tree = new GraftedSnapshotTree(sourceBasis, generatedBasis, graft);
        var identity = new OverlayIdentity(3, "winfsp-cache-test", sourceBasis.Snapshot, SnapshotWorkspaceState.DefaultJournalBytes, 1024 * 1024, Graft: graft);
        string deltaDir = Path.Combine(directory.Path, "delta");
        string ephemeralDir = Path.Combine(directory.Path, "ephemeral");
        string mountDir = Path.Combine(directory.Path, "mount");
        string captureDir = Path.Combine(directory.Path, "capture");

        using var journal = new JournalDeltaStore(deltaDir, identity, tree, boundedLogBytes: JournalDeltaStore.DefaultBoundedLogBytes,
            sourceFilePins: new WindowsJournalFilePins());
        using var ephemeral = new EphemeralRangeStore(ephemeralDir, identity, generatedBasis, "Library", ["Library", "Temp", "Logs"]);
        using var partitioned = new PartitionedWorkspaceDeltaStore(journal, ephemeral);
        var fs = new WinFspOverlayFileSystem(partitioned, null, null, null, enableKernelCaching: true);
        using (var host = new Fsp.FileSystemHost(fs))
        {
            Assert.True(host.MountEx(mountDir, 8, null, false, 0) >= 0);

            // 1. Write heavily to Library and Temp through the mounted WinFsp path.
            Directory.CreateDirectory(Path.Combine(mountDir, "Temp"));
            for (int i = 0; i < 20; i++)
                File.WriteAllBytes(Path.Combine(mountDir, "Temp", $"f{i}.tmp"), new byte[1024]);
            using (var libStream = File.Open(Path.Combine(mountDir, "Library", "ArtifactDB"), FileMode.Open, FileAccess.ReadWrite))
            {
                libStream.Position = 128;
                libStream.Write([7, 8, 9, 10]);
            }
            Assert.Equal(0, journal.Observe().DurableSequence);

            // 2. Write a real source file (buffered via Windows Cache Manager with FlushAndPurgeOnCleanup=false).
            string scriptPath = Path.Combine(mountDir, "Assets", "FastScript.cs");
            string content = "public static class FastScript { public static int V => 42; }\n";
            File.WriteAllText(scriptPath, content);

            // 3. FlushPendingKernelWrites forces Windows Cc dirty source pages into JournalDeltaStore before capture!
            fs.FlushPendingKernelWrites(mountDir);
            var capture = fs.CaptureSource(captureDir, "cccccccccccccccccccccccc", ["Library", "Temp", "Logs"]);
            Assert.True(capture.Sequence > 0);
            Assert.NotNull(journal.Stat("Assets/FastScript.cs"));
            Assert.Equal(Encoding.UTF8.GetByteCount(content), journal.Stat("Assets/FastScript.cs")!.Length);

            // 4. Verify ephemeral writes are readable through mount and still isolated from source journal.
            byte[] libCheck = new byte[4];
            using (var libRead = File.OpenRead(Path.Combine(mountDir, "Library", "ArtifactDB")))
            {
                libRead.Position = 128;
                libRead.ReadExactly(libCheck);
            }
            Assert.Equal(new byte[] { 7, 8, 9, 10 }, libCheck);
            Assert.Equal(0, fs.OpenHandles);
        }
    }

    [Fact]
    public void WinFspKernelSourceFlushPropagatesLockFailureAndPreservesOpenHandleDirtyMark()
    {
        if (!OperatingSystem.IsWindows() || Microsoft.Win32.Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\WinFsp", "InstallDir", null) is not string)
            Assert.Skip("Requires Windows with WinFsp installed.");
        using var directory = new TestDirectory();
        var sourceBasis = new SnapshotOverlayTests.FakeSnapshotTree();
        sourceBasis.Files["Assets/Initial.cs"] = "class Initial {}"u8.ToArray();
        var identity = new OverlayIdentity(3, "winfsp-flush-test", sourceBasis.Snapshot, SnapshotWorkspaceState.DefaultJournalBytes, 1024 * 1024);
        string mountDir = Path.Combine(directory.Path, "mount");

        using var journal = new JournalDeltaStore(Path.Combine(directory.Path, "delta"), identity, sourceBasis,
            boundedLogBytes: JournalDeltaStore.DefaultBoundedLogBytes, sourceFilePins: new WindowsJournalFilePins());
        var fs = new WinFspOverlayFileSystem(journal, null, null, null, enableKernelCaching: true);
        using (var host = new Fsp.FileSystemHost(fs))
        {
            Assert.True(host.MountEx(mountDir, 8, null, false, 0) >= 0);

            // A: Exclusively locked dirty source file must fail flush with OverlayFlushFailed and preserve its dirty mark.
            string lockedPath = Path.Combine(mountDir, "Assets", "Locked.cs");
            using (var exclusive = new FileStream(lockedPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
            {
                exclusive.Write("secret"u8);
                var error = Assert.Throws<WorkspaceException>(() => fs.FlushPendingKernelWrites(mountDir));
                Assert.Equal("OverlayFlushFailed", error.Code);
                Assert.True(fs.DirtyKernelSourceCount > 0);
            }
            fs.FlushPendingKernelWrites(mountDir);
            Assert.Equal(0, fs.DirtyKernelSourceCount);
            Assert.Equal(6, journal.Stat("Assets/Locked.cs")!.Length);

            // B: Open shared writer handle flushes current kernel-cached writes yet stays marked dirty for subsequent writes on the same handle.
            string sharedPath = Path.Combine(mountDir, "Assets", "Shared.cs");
            using (var shared = new FileStream(sharedPath, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite))
            {
                shared.Write("part1"u8);
                shared.Flush(flushToDisk: false);
                fs.FlushPendingKernelWrites(mountDir);
                Assert.Equal(5, journal.Stat("Assets/Shared.cs")!.Length);
                Assert.True(fs.DirtyKernelSourceCount > 0);

                shared.Write("part2"u8);
                shared.Flush(flushToDisk: false);
                fs.FlushPendingKernelWrites(mountDir);
                Assert.Equal(10, journal.Stat("Assets/Shared.cs")!.Length);
                Assert.True(fs.DirtyKernelSourceCount > 0);
            }
            fs.FlushPendingKernelWrites(mountDir);
            Assert.Equal(0, fs.DirtyKernelSourceCount);
            var sharedEntry = journal.Stat("Assets/Shared.cs")!;
            byte[] sharedBytes = new byte[sharedEntry.Length];
            Assert.Equal(10, journal.Read(sharedEntry.Id, 0, sharedBytes));
            Assert.Equal("part1part2"u8.ToArray(), sharedBytes);

            // C: Source file written then marked ReadOnly before flush must still flush cleanly (not fail with ACCESS_DENIED).
            string roPath = Path.Combine(mountDir, "Assets", "ReadOnly.cs");
            File.WriteAllText(roPath, "readonly-body");
            File.SetAttributes(roPath, FileAttributes.ReadOnly);
            fs.FlushPendingKernelWrites(mountDir);
            Assert.Equal(0, fs.DirtyKernelSourceCount);
            var roEntry = journal.Stat("Assets/ReadOnly.cs")!;
            Assert.True((roEntry.Attributes & (uint)FileAttributes.ReadOnly) != 0);
            byte[] roBytes = new byte[roEntry.Length];
            Assert.Equal(13, journal.Read(roEntry.Id, 0, roBytes));
            Assert.Equal("readonly-body"u8.ToArray(), roBytes);
        }
    }

    private sealed class RefuseReadOnlyRestore : IOverlayMutationObserver
    {
        internal bool Refuse = true;
        public void Observe(OverlayMutationBoundary boundary)
        {
            if (Refuse && boundary.Operation == "restore-kernel-readonly") throw new UnauthorizedAccessException("Attribute restore denied");
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void KernelReadOnlyRestoreFailureKeepsReceiptAndRecovers(bool reconnect)
    {
        if (!OperatingSystem.IsWindows() || Microsoft.Win32.Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\WinFsp", "InstallDir", null) is not string)
            Assert.Skip("Requires WinFsp.");
        using var directory = new TestDirectory();
        var basis = new SnapshotOverlayTests.FakeSnapshotTree();
        basis.Files["Assets/Other"] = [0];
        var identity = new OverlayIdentity(3, "readonly-retry", basis.Snapshot, SnapshotWorkspaceState.DefaultJournalBytes, 1024 * 1024);
        string mount = Path.Combine(directory.Path, "m"), receipt = Path.Combine(directory.Path, "readonly.json");
        using var journal = new JournalDeltaStore(Path.Combine(directory.Path, "delta"), identity, basis, boundedLogBytes: JournalDeltaStore.DefaultBoundedLogBytes);
        var fault = new RefuseReadOnlyRestore();
        var fs = new WinFspOverlayFileSystem(journal, null, fault, null, true, receipt);
        using (var host = new Fsp.FileSystemHost(fs))
        {
            Assert.True(host.MountEx(mount, 8, null, false, 0) >= 0);
            string path = Path.Combine(mount, "Assets", "R");
            File.WriteAllText(path, "data");
            File.SetAttributes(path, FileAttributes.ReadOnly);
            Assert.Equal("OverlayFlushFailed", Assert.Throws<WorkspaceException>(() => fs.FlushPendingKernelWrites(mount)).Code);
            Assert.True(File.Exists(receipt));
            Assert.True(fs.DirtyKernelSourceCount > 0);
            if (!reconnect)
            {
                fault.Refuse = false;
                fs.FlushPendingKernelWrites(mount);
            }
        }
        if (reconnect) _ = new WinFspOverlayFileSystem(journal, null, null, null, true, receipt);
        Assert.True((journal.Stat("Assets/R")!.Attributes & (uint)FileAttributes.ReadOnly) != 0);
        Assert.False(File.Exists(receipt));
    }

    [Fact]
    public void ConcurrentCloseAndReopenKeepBeeBasisFileIdentity()
    {
        if (!OperatingSystem.IsWindows()) Assert.Skip("Windows file system callbacks are required.");
        using var directory = new TestDirectory();
        var basis = new SnapshotOverlayTests.FakeSnapshotTree();
        const string symbol = "Bee/artifacts/fixture.dag/UnityEngine.TestRunner.pdb";
        basis.Files[symbol] = new byte[97];
        var identity = new OverlayIdentity(3, "bee-handles", basis.Snapshot, SnapshotWorkspaceState.DefaultJournalBytes, 1048576);
        using var journal = new JournalDeltaStore(Path.Combine(directory.Path, "delta"), identity, basis);
        using var cache = new EphemeralRangeStore(Path.Combine(directory.Path, "cache"), identity, basis, "Library", ["Library"]);
        using var store = new PartitionedWorkspaceDeltaStore(journal, cache);
        var fs = new WinFspOverlayFileSystem(store, null, null, null, true);
        var options = new ParallelOptions { MaxDegreeOfParallelism = 2, CancellationToken = TestContext.Current.CancellationToken };
        Parallel.For(0, 2, options, worker =>
        {
            for (int i = 0; i < 3000; i++)
            {
                Assert.Equal(0, fs.Open("Library/" + symbol, 0, 1, out var node, out var handle, out _, out _));
                try
                {
                    Assert.Equal(0, fs.GetFileInfo(node, handle, out var info));
                    Assert.Equal(97UL, info.FileSize);
                }
                finally { fs.Close(node, handle); }
            }
        });
        Assert.Equal(0, fs.RetainedFileContexts);
        Assert.Equal(0, journal.Observe().DurableSequence);
    }

    [Fact]
    public void MountedFileHandleSurvivesTwoCrossPartitionMoves()
    {
        if (!OperatingSystem.IsWindows() || Microsoft.Win32.Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\WinFsp", "InstallDir", null) is not string)
            Assert.Skip("Requires WinFsp.");
        using var directory = new TestDirectory();
        var basis = new SnapshotOverlayTests.FakeSnapshotTree();
        basis.Files["Assets/Other"] = [1];
        var identity = new OverlayIdentity(3, "move-open", basis.Snapshot, SnapshotWorkspaceState.DefaultJournalBytes, 1024 * 1024);
        using var journal = new JournalDeltaStore(Path.Combine(directory.Path, "delta"), identity, basis, boundedLogBytes: JournalDeltaStore.DefaultBoundedLogBytes);
        using var store = new PartitionedWorkspaceDeltaStore(journal, new EphemeralRangeStore(Path.Combine(directory.Path, "cache"), identity, null, null, ["Temp"]));
        using var host = new Fsp.FileSystemHost(new WinFspOverlayFileSystem(store, null, null, null, true));
        string mount = Path.Combine(directory.Path, "m");
        Assert.True(host.MountEx(mount, 8, null, false, 0) >= 0);
        string temp = Directory.CreateDirectory(Path.Combine(mount, "Temp")).FullName;
        string a = Path.Combine(temp, "a"), b = Path.Combine(mount, "Assets", "b"), c = Path.Combine(temp, "c");
        using (var file = new FileStream(a, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete))
        {
            file.Write("initial"u8);
            file.Flush(true);
            File.Move(a, b);
            File.Move(b, c);
            file.Position = 0;
            file.Write("changed"u8);
            file.Flush(true);
        }
        Assert.Equal("changed", File.ReadAllText(c));
    }

    [Fact]
    public async Task WinFspServerStopBlocksRootHandleUnlessExplicitlyAllowed()
    {
        if (!OperatingSystem.IsWindows() || Microsoft.Win32.Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\WinFsp", "InstallDir", null) is not string)
            Assert.Skip("Requires Windows with WinFsp installed.");
        using var directory = new TestDirectory();
        var ct = TestContext.Current.CancellationToken;
        var f = CreateFastLibraryFixture(directory.Path, "777777777777777777777777", new()
        {
            ["a.bin"] = "hello"u8.ToArray()
        });

        string mountDir = Path.Combine(directory.Path, "mount");
        string controlDir = Path.Combine(directory.Path, "control");
        Directory.CreateDirectory(controlDir);
        string deltaDir = Path.Combine(directory.Path, "server-delta");
        string requestPath = Path.Combine(controlDir, "request.json");
        string generation = "gen-root-stop-test";
        var serveRequest = new OverlayServeRequest(f.Request.Identity, deltaDir, mountDir, controlDir,
            "tok-root-stop", f.Request.ReadAccess, f.ReceiptRoot, f.Snapshot.Id);
        await WindowsProcessWorkspaceProvider.WriteAsync(requestPath, serveRequest, ct);
        await WindowsProcessWorkspaceProvider.WriteAsync(Path.Combine(controlDir, "provider-launch-intent.json"),
            new WinFspOverlayClient.ProviderIntent(serveRequest.Token, serveRequest.Identity.Id, generation, DateTimeOffset.UtcNow), ct);

        var serverTask = Task.Run(() => WinFspOverlayServer.RunAsync(requestPath, generation), CancellationToken.None);
        try
        {
            // Wait until the server publishes mounted state.
            bool mounted = false;
            for (int i = 0; i < 100 && !serverTask.IsCompleted; i++)
            {
                var state = await WindowsProcessWorkspaceProvider.ReadAsync<System.Text.Json.JsonElement?>(
                    Path.Combine(controlDir, "state.json"), ct, waitForPublication: false);
                if (state is { ValueKind: System.Text.Json.JsonValueKind.Object } obj &&
                    obj.TryGetProperty("phase", out var phase) && phase.GetString() == "mounted" &&
                    Directory.Exists(mountDir))
                { mounted = true; break; }
                await Task.Delay(50, ct);
            }
            Assert.True(mounted, "WinFsp overlay server did not reach mounted state.");

            // Hold a directory handle on the mount root only (simulating a CWD reference).
            using var rootHandle = CreateFileW(mountDir, 0x00000001u, 0x00000007u, 0, 3u, 0x02000000u, 0);
            Assert.False(rootHandle.IsInvalid);

            // 1. Default stop (AllowRootDirectoryHandles = false) must NOT unmount while root handle is open.
            await WindowsProcessWorkspaceProvider.WriteAsync(Path.Combine(controlDir, "stop.json"),
                new { serveRequest.Token, allowRootDirectoryHandles = false }, ct);
            await Task.Delay(1300, ct);
            Assert.False(serverTask.IsCompleted);
            Assert.True(File.Exists(Path.Combine(controlDir, "stop-blocked.json")));

            // 2. Explicit opt-in (AllowRootDirectoryHandles = true) unmounts cleanly after the 750ms grace.
            await WindowsProcessWorkspaceProvider.WriteAsync(Path.Combine(controlDir, "stop.json"),
                new { serveRequest.Token, allowRootDirectoryHandles = true }, ct);
            var completed = await Task.WhenAny(serverTask, Task.Delay(5000, ct));
            Assert.Same(serverTask, completed);
            Assert.Equal(0, await serverTask);
        }
        finally
        {
            if (!serverTask.IsCompleted)
            {
                await WindowsProcessWorkspaceProvider.WriteAsync(Path.Combine(controlDir, "stop.json"),
                    new { serveRequest.Token, allowRootDirectoryHandles = true }, CancellationToken.None);
                await Task.WhenAny(serverTask, Task.Delay(5000, CancellationToken.None));
            }
        }
    }

    [Fact]
    public void SharedLicensingProcessFilterMatchesUnityLicensingClientOnly()
    {
        Assert.True(WindowsProcessGroup.IsSharedLicensingProcess("Unity.Licensing.Client"));
        Assert.True(WindowsProcessGroup.IsSharedLicensingProcess("unity.licensing.client"));
        Assert.True(WindowsProcessGroup.IsSharedLicensingProcess("UnityLicensingClient"));
        Assert.False(WindowsProcessGroup.IsSharedLicensingProcess("Unity"));
        Assert.False(WindowsProcessGroup.IsSharedLicensingProcess("pwsh"));
        Assert.False(WindowsProcessGroup.IsSharedLicensingProcess(null));
        Assert.False(WindowsProcessGroup.CanTerminateRemainder(null));
        Assert.False(WindowsProcessGroup.CanTerminateRemainder(""));
        Assert.False(WindowsProcessGroup.CanTerminateRemainder("Unity.Licensing.Client"));
        Assert.True(WindowsProcessGroup.CanTerminateRemainder("UnityShaderCompiler"));
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateHardLinkW(string lpFileName, string lpExistingFileName, nint lpSecurityAttributes);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(string lpFileName, uint dwDesiredAccess, uint dwShareMode,
        nint lpSecurityAttributes, uint dwCreationDisposition, uint dwFlagsAndAttributes, nint hTemplateFile);
}
