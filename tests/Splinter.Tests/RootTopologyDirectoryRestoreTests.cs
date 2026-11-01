using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Win32.SafeHandles;
using Splinter.Core;
using Splinter.Platform.Windows;
using Splinter.Runtime;

namespace Splinter.Tests;

public sealed class RootTopologyDirectoryRestoreTests
{
    [Theory]
    [InlineData("mixed")]
    [InlineData("swap")]
    [InlineData("case")]
    [InlineData("reparent")]
    [InlineData("ancestor")]
    [InlineData("deleted-parent")]
    [InlineData("types")]
    [InlineData("delete-tree")]
    public async Task FinalTopologyKeepsSourceIdentitiesAndExactSpelling(string profile)
    {
        foreach (bool bounded in new[] { false, true })
        {
            using var f = new RootTopologyRestoreFixture(bounded, profile);
            var stages = new List<string>(); await f.RestoreTopologyProtocolAsync(stages.Add); f.AssertRestored();
            Assert.Equal(1, stages.Count(s => s == "topology.after-root-move"));
            Assert.DoesNotContain("file.after-move", stages);
            Assert.True(File.Exists(f.PlanPath)); Assert.False(File.Exists(Path.Combine(f.Root, "restore-plan.json")));
            f.AllowCleanup();
        }
    }

    [Fact]
    public async Task AWholeRenamedSubtreeUsesOneTransportUnitRatherThanOneMovePerFile()
    {
        using var f = new RootTopologyRestoreFixture(true, "bulk", count: 300);
        var stages = new List<string>(); await f.RestoreTopologyProtocolAsync(stages.Add); f.AssertRestored();
        Assert.Equal(1, stages.Count(s => s == "topology.after-root-move"));
        Assert.Equal(1, stages.Count(s => s == "topology.after-stage"));
        Assert.Equal(1, stages.Count(s => s == "topology.after-install"));
        Assert.DoesNotContain("file.after-move", stages); f.AllowCleanup();
    }

    [Theory]
    [InlineData("topology.after-plan")]
    [InlineData("topology.root-unpinned")]
    [InlineData("topology.before-root-move")]
    [InlineData("topology.after-root-move")]
    [InlineData("topology.before-stage")]
    [InlineData("topology.after-stage")]
    [InlineData("topology.after-stage-cursor")]
    [InlineData("topology.before-install")]
    [InlineData("topology.after-install")]
    [InlineData("topology.after-install-cursor")]
    [InlineData("topology.new-directory-after-receipt")]
    [InlineData("topology.after-forest-proof")]
    [InlineData("topology.file.before-create")]
    [InlineData("topology.file.after-create-receipt")]
    [InlineData("topology.file.after-write-intent")]
    [InlineData("topology.file.before-ranges-write")]
    [InlineData("topology.file.after-range-write")]
    [InlineData("topology.file.after-ranges-flush")]
    [InlineData("topology.file.after-complete")]
    [InlineData("topology.after-verified")]
    [InlineData("topology.reclaim.intent")]
    [InlineData("topology.reclaim.file")]
    [InlineData("topology.reclaim.directory")]
    [InlineData("topology.journal.intent")]
    [InlineData("topology.journal.journal-removed")]
    [InlineData("topology.after-reclaim")]
    public async Task ExactIntentResumesTopologyContentAndReclaimBoundaries(string boundary)
    {
        foreach (bool bounded in new[] { false, true })
        {
            using var f = new RootTopologyRestoreFixture(bounded);
            await f.InterruptTopologyProtocolAsync(boundary); await f.RestoreTopologyProtocolAsync(); f.AssertRestored(); f.AllowCleanup();
        }
    }

    [Theory]
    [InlineData("topology.after-stage", 1)]
    [InlineData("topology.after-stage", 2)]
    [InlineData("topology.after-install", 1)]
    [InlineData("topology.after-install", 2)]
    public async Task SwapRecoversTheOneUnpublishedRenameWithoutGuessingPathOccupants(string stage, int occurrence)
    {
        using var f = new RootTopologyRestoreFixture(true, "swap");
        await f.InterruptTopologyProtocolAsync(stage, occurrence); await f.Restore(); f.AssertRestored(); f.AllowCleanup();
    }

    [Fact]
    public async Task CursorCannotAuthorizeTwoUnpublishedMoves()
    {
        using var f = new RootTopologyRestoreFixture(true); byte[]? firstCursor = null; int moved = 0;
        await Assert.ThrowsAsync<IOException>(() => f.RestoreTopologyProtocolAsync(stage =>
        {
            if (stage == "topology.after-stage-cursor" && firstCursor is null) firstCursor = File.ReadAllBytes(f.CursorPath);
            if (stage == "topology.after-stage" && ++moved == 3) throw new IOException("Two effects, old cursor.");
        }));
        Assert.NotNull(firstCursor); File.WriteAllBytes(f.CursorPath, firstCursor);
        var before = f.StageIdentityMap(); await Assert.ThrowsAsync<WorkspaceException>(() => f.Restore());
        Assert.Equal(before, f.StageIdentityMap()); f.AssertJournalPreserved(); f.AllowCleanup();
    }

    [Fact]
    public async Task MissingCursorAfterRootMoveCannotBeRecreatedAsAnInitialPosition()
    {
        using var f = new RootTopologyRestoreFixture(true, "root-swap", count: 2);
        await f.SeedTopologyPlanAsync();
        await f.InterruptTopologyProtocolAsync("topology.after-root-move");
        var cursor = JsonNode.Parse(File.ReadAllText(f.CursorPath))!;
        Assert.Equal(0, cursor["completed"]!.GetValue<int>());
        string originalRoot = RootTopologyRestoreFixture.Identity(f.State.OriginalPath, true);
        File.Move(f.CursorPath, Path.Combine(f.Root, "preserved-cursor.json"));
        await Assert.ThrowsAsync<WorkspaceException>(() => f.Restore());
        Assert.False(File.Exists(f.CursorPath)); Assert.Empty(f.Stages());
        Assert.Equal(originalRoot, RootTopologyRestoreFixture.Identity(f.State.OriginalPath, true));
        f.AssertOriginalFilesPreserved(); f.AssertJournalPreserved(); f.AllowCleanup();
    }

    [Theory]
    [InlineData("root-swap", "topology.before-install", 0x40000000u)]
    [InlineData("root-create", "topology.new-directory-before-create", 0x40000000u)]
    [InlineData("root-swap", "topology.before-install", 0x100u)]
    [InlineData("root-create", "topology.new-directory-before-create", 0x100u)]
    public async Task EmptyRootReparseAtTheNativeEffectBoundaryCannotRedirectPublication(string profile, string boundary, uint access)
    {
        using var f = new RootTopologyRestoreFixture(true, profile, count: 2);
        await f.SeedTopologyPlanAsync();
        string outside = PrepareOutside(f.DirectoryPath); string outsideBefore = OutsideInventory(outside);
        SafeFileHandle? injectedRoot = null; int injectionError = int.MinValue, hits = 0;
        byte[]? planBefore = null, intentBefore = null, cursorBefore = null;
        Exception? failure;
        try
        {
            failure = await Record.ExceptionAsync(() => f.Restore(stage =>
            {
                if (stage != boundary || hits != 0) return;
                hits++;
                // This is a real empty root: every source entry lives inside
                // alpha/beta and both complete units have already been staged.
                Assert.Empty(Directory.GetFileSystemEntries(f.State.OriginalPath)); Assert.Equal(2, f.Stages().Length);
                Assert.Throws<IOException>(() => Directory.Move(f.State.OriginalPath, Path.Combine(f.Root, "unexpected-root-move")));
                planBefore = File.ReadAllBytes(f.PlanPath);
                intentBefore = File.ReadAllBytes(Path.Combine(f.Root, "restore-topology-intent.json"));
                cursorBefore = File.ReadAllBytes(f.CursorPath);
                injectionError = InjectJunctionKeepingExactHandle(f.State.OriginalPath, outside, access, out injectedRoot);
                TestContext.Current.TestOutputHelper?.WriteLine($"Native topology boundary {boundary}; access=0x{access:x}; junction result={injectionError}.");
            }));
        }
        finally
        {
            // A parent-relative operation may have put an owned child inside
            // the original directory object. Clear only its reparse tag using
            // the exact retained handle; deleting the junction path could lose
            // that child's recoverable namespace.
            if (injectedRoot is not null)
            {
                try { ClearJunction(injectedRoot); }
                finally { injectedRoot.Dispose(); }
            }
        }
        Assert.Equal(1, hits);
        Assert.Equal(outsideBefore, OutsideInventory(outside));
        // GENERIC_WRITE must actually exercise the vulnerable window. The
        // attributes-only attempt separately records what this NTFS accepts;
        // if its access is refused, the unaffected restore must still finish.
        if (access == 0x40000000) Assert.Equal(0, injectionError);
        if (injectionError != 0)
        {
            Assert.Contains(injectionError, new[] { 5, 32 }); Assert.Null(failure);
            f.AssertRestored(); f.AllowCleanup(); return;
        }
        Assert.IsType<WorkspaceException>(failure);
        Assert.Equal(planBefore, File.ReadAllBytes(f.PlanPath));
        Assert.Equal(intentBefore, File.ReadAllBytes(Path.Combine(f.Root, "restore-topology-intent.json")));
        Assert.Equal(cursorBefore, File.ReadAllBytes(f.CursorPath));
        Assert.NotEmpty(f.Stages()); Assert.False(File.Exists(Path.Combine(f.Root, "restore-topology-verified.json")));
        Assert.False(File.Exists(Path.Combine(f.Root, "restore-topology-staging-reclaim.json")));
        Assert.Empty(Directory.GetFiles(f.Root, "restore-topology-directory-*.json"));
        f.AssertOriginalFilesPreserved(); f.AssertJournalPreserved(); f.AllowCleanup();
    }

    [Theory]
    [InlineData("topology.after-forest-proof", 0x40000000u, false)]
    [InlineData("topology.after-verified", 0x40000000u, false)]
    [InlineData("topology.reclaim.intent", 0x40000000u, false)]
    [InlineData("topology.after-forest-proof", 0x100u, false)]
    [InlineData("topology.after-verified", 0x100u, false)]
    [InlineData("topology.reclaim.intent", 0x100u, false)]
    [InlineData("topology.after-verified", 0x100u, true)]
    [InlineData("topology.reclaim.intent", 0x100u, true)]
    public async Task EmptyFinalRootReparseIsBlockedOrRefusedBeforeReclaim(string boundary, uint access, bool emptyTarget)
    {
        using var f = new RootTopologyRestoreFixture(true, "root-empty", count: 2);
        await f.SeedTopologyPlanAsync();
        string outside = PrepareOutside(f.DirectoryPath), target = outside;
        if (emptyTarget) { target = Path.Combine(outside, "empty-target"); Directory.CreateDirectory(target); }
        string outsideBefore = OutsideInventory(outside);
        int hits = 0, error = int.MinValue; SafeFileHandle? unexpectedReparse = null;
        Exception? failure;
        try
        {
            failure = await Record.ExceptionAsync(() => f.Restore(stage =>
            {
                if (stage != boundary) return; hits++;
                Assert.Empty(Directory.GetFileSystemEntries(f.State.OriginalPath));
                if (emptyTarget) Assert.Empty(Directory.GetFileSystemEntries(target));
                Assert.Throws<IOException>(() => Directory.Move(f.State.OriginalPath, Path.Combine(f.Root, "unexpected-root-move")));
                error = InjectJunctionKeepingExactHandle(f.State.OriginalPath, target, access, out unexpectedReparse);
                TestContext.Current.TestOutputHelper?.WriteLine($"Final root boundary {boundary}; access=0x{access:x}; empty target={emptyTarget}; junction result={error}.");
                // If NTFS accepts attributes-only conversion, let the real
                // product continue. The product must detect it before reclaim;
                // a test assertion here would hide a missing current-type guard.
            }));
        }
        finally
        {
            if (unexpectedReparse is not null)
            {
                try { ClearJunction(unexpectedReparse); }
                finally { unexpectedReparse.Dispose(); }
            }
        }
        Assert.Equal(1, hits); Assert.Equal(outsideBefore, OutsideInventory(outside));
        if (access == 0x40000000) Assert.Equal(32, error);
        if (error != 0)
        {
            Assert.Contains(error, new[] { 5, 32 }); Assert.Null(failure);
            f.AssertRestored(); Assert.Empty(Directory.GetFileSystemEntries(f.State.OriginalPath));
        }
        else
        {
            Assert.IsType<WorkspaceException>(failure); Assert.NotEmpty(f.Stages());
            f.AssertOriginalFilesPreserved(); f.AssertJournalPreserved();
        }
        f.AllowCleanup();
    }

    internal static string PrepareOutside(string fixture)
    {
        string outside = Path.Combine(fixture, "outside"); Directory.CreateDirectory(Path.Combine(outside, "nested"));
        File.WriteAllBytes(Path.Combine(outside, "marker"), [71, 72, 73]); File.WriteAllBytes(Path.Combine(outside, "nested", "keep"), [91, 92]);
        return outside;
    }
    internal static string OutsideInventory(string root) => JsonSerializer.Serialize(
        Directory.EnumerateFileSystemEntries(root, "*", SearchOption.AllDirectories).Prepend(root).Order(StringComparer.Ordinal).Select(path =>
        {
            var attributes = File.GetAttributes(path); Assert.Equal(0, (int)(attributes & FileAttributes.ReparsePoint));
            bool directory = (attributes & FileAttributes.Directory) != 0;
            return new { path = Path.GetRelativePath(root, path), directory, attributes,
                creation = File.GetCreationTimeUtc(path).ToFileTimeUtc(), written = File.GetLastWriteTimeUtc(path).ToFileTimeUtc(),
                identity = RootTopologyRestoreFixture.Identity(path, directory),
                bytes = directory ? null : Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))) };
        }));
    internal static int InjectJunctionKeepingExactHandle(string root, string outside, uint access, out SafeFileHandle? retained)
    {
        retained = null;
        var handle = CreateFileW(root, access, 7, 0, 3, 0x02200000, 0);
        if (handle.IsInvalid) { int error = Marshal.GetLastWin32Error(); handle.Dispose(); return error; }
        // Do not reopen through the GENERIC_WRITE helper: that would mask
        // whether FILE_WRITE_ATTRIBUTES alone can perform the control call.
        byte[] substitute = Encoding.Unicode.GetBytes(@"\??\" + Path.GetFullPath(outside)), print = Encoding.Unicode.GetBytes(Path.GetFullPath(outside));
        byte[] buffer = new byte[16 + substitute.Length + 2 + print.Length + 2];
        BitConverter.GetBytes(0xA0000003u).CopyTo(buffer, 0); BitConverter.GetBytes(checked((ushort)(buffer.Length - 8))).CopyTo(buffer, 4);
        BitConverter.GetBytes(checked((ushort)substitute.Length)).CopyTo(buffer, 10);
        BitConverter.GetBytes(checked((ushort)(substitute.Length + 2))).CopyTo(buffer, 12); BitConverter.GetBytes(checked((ushort)print.Length)).CopyTo(buffer, 14);
        substitute.CopyTo(buffer, 16); print.CopyTo(buffer, 18 + substitute.Length);
        int result = DeviceIoControl(handle, 0x900A4, buffer, buffer.Length, 0, 0, out _, 0) ? 0 : Marshal.GetLastWin32Error();
        if (result == 0) retained = handle; else handle.Dispose();
        return result;
    }
    internal static void ClearJunction(SafeFileHandle exactRoot)
    {
        byte[] request = new byte[8]; BitConverter.GetBytes(0xA0000003u).CopyTo(request, 0);
        bool cleared = DeviceIoControl(exactRoot, 0x900AC, request, request.Length, 0, 0, out _, 0);
        int error = cleared ? 0 : Marshal.GetLastWin32Error();
        Assert.True(cleared, "Preserve the fixture: clearing its exact injected junction failed with native error " + error);
    }
    // Test observation uses the same native access on every frozen runtime;
    // it must not bind to an evolving optional-parameter product overload.
    internal static string ReadNativeIdentity(string path, bool directory)
    {
        WorkspaceStore.EnsurePlainPath(path);
        using var handle = CreateFileW(path, 0x80, 7, 0, 3, 0x00200000u | (directory ? 0x02000000u : 0u), 0);
        if (handle.IsInvalid)
        {
            int error = Marshal.GetLastWin32Error();
            throw new IOException("Cannot observe fixture identity (native error " + error + ").", new System.ComponentModel.Win32Exception(error));
        }
        return SnapshotInputChecks.FileIdentity(handle, directory);
    }
    [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(string path, uint access, uint share, nint security, uint disposition, uint flags, nint template);
    [DllImport("kernel32", SetLastError = true)]
    private static extern bool DeviceIoControl(SafeFileHandle handle, uint code, byte[] input, int inputSize, nint output, int outputSize, out int returned, nint overlapped);

    [Theory]
    [InlineData("topology.new-directory-after-create-before-receipt")]
    [InlineData("topology.new-directory-after-receipt")]
    public async Task UnknownOrReplacedNewDirectoryIsPreserved(string boundary)
    {
        using var f = new RootTopologyRestoreFixture(true, "new-directory"); await f.InterruptTopologyProtocolAsync(boundary);
        string path = f.Target("new-parent");
        if (boundary.EndsWith("after-receipt", StringComparison.Ordinal))
        { Directory.Move(path, Path.Combine(f.Root, "preserved-created-directory")); Directory.CreateDirectory(path); }
        File.WriteAllText(Path.Combine(path, "foreign"), "preserve"); string foreign = RootTopologyRestoreFixture.Identity(path, true);
        await Assert.ThrowsAsync<WorkspaceException>(() => f.Restore());
        Assert.Equal(foreign, RootTopologyRestoreFixture.Identity(path, true));
        Assert.Equal("preserve", File.ReadAllText(Path.Combine(path, "foreign"))); f.AssertJournalPreserved(); f.AllowCleanup();
    }

    [Fact]
    public async Task EmptyNewDirectoryWithoutReceiptIsStillUnknownOwnership()
    {
        using var f = new RootTopologyRestoreFixture(true, "new-directory");
        await f.InterruptTopologyProtocolAsync("topology.new-directory-after-create-before-receipt");
        string path = f.Target("new-parent"), identity = RootTopologyRestoreFixture.Identity(path, true);
        Assert.Empty(Directory.GetFileSystemEntries(path)); await Assert.ThrowsAsync<WorkspaceException>(() => f.Restore());
        Assert.Equal(identity, RootTopologyRestoreFixture.Identity(path, true)); Assert.Empty(Directory.GetFileSystemEntries(path));
        f.AssertJournalPreserved(); f.AllowCleanup();
    }

    [Theory]
    [InlineData("topology.file.after-create-before-receipt")]
    [InlineData("topology.file.after-create-receipt")]
    public async Task NewFileUnderAReceiptedDirectoryStillNeedsItsOwnExactReceipt(string boundary)
    {
        using var f = new RootTopologyRestoreFixture(true, "new-directory"); await f.InterruptTopologyProtocolAsync(boundary);
        string path = f.Target("new-parent/new");
        if (boundary.EndsWith("after-create-receipt", StringComparison.Ordinal)) File.Move(path, Path.Combine(f.Root, "preserved-created-file"));
        File.WriteAllBytes(path, [77]); string identity = RootTopologyRestoreFixture.Identity(path);
        await Assert.ThrowsAsync<WorkspaceException>(() => f.Restore());
        Assert.Equal(new byte[] { 77 }, File.ReadAllBytes(path)); Assert.Equal(identity, RootTopologyRestoreFixture.Identity(path));
        f.AssertJournalPreserved(); f.AllowCleanup();
    }

    [Theory]
    [InlineData("junction")]
    [InlineData("occupied-target")]
    public async Task NewLeafPublicationIsRelativeAndExclusiveUnderAReceiptedEmptyDirectory(string change)
    {
        using var f = new RootTopologyRestoreFixture(true, "root-create", count: 2);
        await f.SeedTopologyPlanAsync();
        string outside = PrepareOutside(f.DirectoryPath), outsideBefore = OutsideInventory(outside);
        SafeFileHandle? injected = null;
        int hits = 0, error = int.MinValue; string? parentIdentity = null, foreignIdentity = null;
        string parent = f.Target("fresh"), target = f.Target("fresh/new");
        Exception? failure;
        try
        {
            failure = await Record.ExceptionAsync(() => f.Restore(stage =>
            {
                if (stage != "topology.file.before-create" || hits != 0) return; hits++;
                Assert.Empty(Directory.GetFileSystemEntries(parent)); parentIdentity = RootTopologyRestoreFixture.Identity(parent, true);
                Assert.Single(Directory.GetFiles(f.Root, "restore-topology-directory-*.json"));
                if (change == "junction")
                {
                    error = InjectJunctionKeepingExactHandle(parent, outside, 0x100, out injected);
                    TestContext.Current.TestOutputHelper?.WriteLine($"B receipted empty parent; access=0x100; junction result={error}.");
                }
                else { File.WriteAllBytes(target, [77]); foreignIdentity = RootTopologyRestoreFixture.Identity(target); }
            }));
        }
        finally
        {
            if (injected is not null) { try { ClearJunction(injected); } finally { injected.Dispose(); } }
        }
        Assert.Equal(1, hits); if (change == "junction") Assert.Equal(0, error);
        Assert.IsType<WorkspaceException>(failure); Assert.Equal(outsideBefore, OutsideInventory(outside));
        Assert.Equal(parentIdentity, RootTopologyRestoreFixture.Identity(parent, true));
        Assert.Empty(Directory.GetFiles(f.Root, "restore-topology-file-*.json"));
        Assert.Empty(Directory.GetFiles(f.Root, "restore-topology-write-*.json"));
        Assert.False(File.Exists(Path.Combine(f.Root, "restore-topology-verified.json")));
        f.AssertOriginalFilesPreserved(allowNewFiles: true); f.AssertJournalPreserved();
        if (File.Exists(target))
        {
            byte[] bytes = File.ReadAllBytes(target); string identity = RootTopologyRestoreFixture.Identity(target);
            if (change == "occupied-target") { Assert.Equal(new byte[] { 77 }, bytes); Assert.Equal(foreignIdentity, identity); }
            await Assert.ThrowsAsync<WorkspaceException>(() => f.Restore());
            Assert.Equal(bytes, File.ReadAllBytes(target)); Assert.Equal(identity, RootTopologyRestoreFixture.Identity(target));
        }
        f.AllowCleanup();
    }

    [Theory]
    [InlineData("foreign-file")]
    [InlineData("foreign-directory")]
    [InlineData("both-roots")]
    public async Task RootTransferGapCannotAdoptForeignInventory(string change)
    {
        using var f = new RootTopologyRestoreFixture(true); await f.InterruptTopologyProtocolAsync("topology.after-root-move");
        if (change == "foreign-file") File.WriteAllText(f.Target("foreign"), "preserve");
        else Directory.CreateDirectory(change == "both-roots" ? f.State.Basis.Snapshot.RootPath : f.Target("foreign"));
        await Assert.ThrowsAsync<WorkspaceException>(() => f.Restore()); f.AssertJournalPreserved();
        Assert.False(File.Exists(Path.Combine(f.Root, "restore-topology-verified.json")));
        if (change == "foreign-file") Assert.Equal("preserve", File.ReadAllText(f.Target("foreign")));
        f.AllowCleanup();
    }

    [Fact]
    public async Task AnOccupiedFinalDirectoryIsNotReplacedDuringInstall()
    {
        using var f = new RootTopologyRestoreFixture(true, "bulk"); await f.InterruptTopologyProtocolAsync("topology.before-install");
        Directory.CreateDirectory(f.Target("renamed")); File.WriteAllText(f.Target("renamed/foreign"), "preserve");
        string foreign = RootTopologyRestoreFixture.Identity(f.Target("renamed"), true);
        await Assert.ThrowsAsync<WorkspaceException>(() => f.Restore()); Assert.Single(f.Stages());
        Assert.Equal(foreign, RootTopologyRestoreFixture.Identity(f.Target("renamed"), true));
        Assert.Equal("preserve", File.ReadAllText(f.Target("renamed/foreign"))); f.AssertJournalPreserved(); f.AllowCleanup();
    }

    [Theory]
    [InlineData("extra-file")]
    [InlineData("extra-directory")]
    [InlineData("replacement-child")]
    [InlineData("retained-byte")]
    [InlineData("reparse")]
    public async Task ForeignDataInAStagedSubtreeStopsBeforePrivateWritesOrReclamation(string change)
    {
        using var f = new RootTopologyRestoreFixture(true, "bulk"); await f.InterruptTopologyProtocolAsync("topology.after-stage");
        string stage = Assert.Single(f.Stages()); string survivor = Path.Combine(stage, "f00000");
        string? link = null;
        if (change == "extra-file") File.WriteAllText(Path.Combine(stage, "foreign"), "preserve");
        else if (change == "extra-directory") Directory.CreateDirectory(Path.Combine(stage, "foreign"));
        else if (change == "replacement-child")
        { byte[] bytes = File.ReadAllBytes(survivor); File.Move(survivor, Path.Combine(f.Root, "preserved-child")); File.WriteAllBytes(survivor, bytes); }
        else if (change == "retained-byte")
        { byte[] bytes = File.ReadAllBytes(survivor); bytes[0] ^= 1; File.SetAttributes(survivor, FileAttributes.Normal); File.WriteAllBytes(survivor, bytes); }
        else
        {
            string outside = Path.Combine(f.DirectoryPath, "outside"); Directory.CreateDirectory(outside); File.WriteAllText(Path.Combine(outside, "marker"), "preserve");
            link = Path.Combine(stage, "foreign"); Directory.CreateDirectory(link); OwnedDirectoryLifetimeTests.SetJunction(link, outside);
        }
        try
        {
            await Assert.ThrowsAsync<WorkspaceException>(() => f.Restore()); f.AssertJournalPreserved();
            Assert.False(File.Exists(Path.Combine(f.Root, "restore-topology-verified.json")));
            Assert.False(File.Exists(Path.Combine(f.Root, "restore-topology-staging-reclaim.json")));
            if (change == "reparse") Assert.Equal("preserve", File.ReadAllText(Path.Combine(f.DirectoryPath, "outside", "marker")));
        }
        finally
        {
            // The owned parent can have moved before the final forest detects
            // a foreign child. Remove only our exact junction, never its target.
            if (link is not null)
            {
                foreach (string candidate in new[] { link, f.Target("renamed/foreign") })
                    if (Directory.Exists(candidate) && (File.GetAttributes(candidate) & FileAttributes.ReparsePoint) != 0) Directory.Delete(candidate);
            }
        }
        f.AllowCleanup();
    }

    [Theory]
    [InlineData("unknown-stage")]
    [InlineData("replace-stage")]
    [InlineData("missing-stage")]
    [InlineData("both-locations")]
    public async Task StagedUnitNeedsExactIdentityMembershipAndAUniqueLocation(string change)
    {
        using var f = new RootTopologyRestoreFixture(true, "bulk"); await f.InterruptTopologyProtocolAsync("topology.after-stage");
        string stage = Assert.Single(f.Stages());
        if (change == "unknown-stage") File.WriteAllText(Path.Combine(f.Root, "restore-topology-stage-99999999"), "foreign");
        else if (change == "both-locations") Directory.CreateDirectory(f.Target("bulk"));
        else
        { Directory.Move(stage, Path.Combine(f.Root, "preserved-stage")); if (change == "replace-stage") Directory.CreateDirectory(stage); }
        await Assert.ThrowsAsync<WorkspaceException>(() => f.Restore()); f.AssertJournalPreserved(); f.AllowCleanup();
    }

    [Theory]
    [InlineData("seal")]
    [InlineData("plan-hash")]
    [InlineData("cursor-hash")]
    [InlineData("missing-cursor")]
    [InlineData("missing-intent")]
    [InlineData("general-plan")]
    [InlineData("root-delta-plan")]
    public async Task WrongIntentSealOrRestoreModeCannotContinueTopology(string change)
    {
        using var f = new RootTopologyRestoreFixture(true);
        await f.InterruptTopologyProtocolAsync(change == "seal" ? "topology.after-plan" : "topology.after-stage-cursor", change == "missing-cursor" ? 2 : 1);
        if (change == "seal") f.Mutate(j => j.Write(j.Stat("beta/stay")!.Id, 0, [99]));
        else if (change == "plan-hash") File.AppendAllText(f.PlanPath, " ");
        else if (change == "cursor-hash")
        { var cursor = JsonNode.Parse(File.ReadAllText(f.CursorPath))!; cursor["planHash"] = new string('0', 64); File.WriteAllText(f.CursorPath, cursor.ToJsonString()); }
        else if (change.StartsWith("missing-", StringComparison.Ordinal))
        { string name = change == "missing-cursor" ? "cursor" : "intent"; File.Move(Path.Combine(f.Root, $"restore-topology-{name}.json"), Path.Combine(f.Root, "preserved-" + name)); }
        else File.WriteAllText(Path.Combine(f.Root, change == "general-plan" ? "restore-plan.json" : "restore-root-delta-plan.json"), "{}");
        await Assert.ThrowsAsync<WorkspaceException>(() => f.Restore()); f.AssertJournalPreserved(); f.AllowCleanup();
    }

    [Theory]
    [InlineData("directory-metadata")]
    [InlineData("range")]
    [InlineData("length")]
    [InlineData("unit-target")]
    [InlineData("operation-target")]
    public async Task ReboundPlanStillMustMatchExactJournalEffectsAndDerivedTopology(string change)
    {
        using var f = new RootTopologyRestoreFixture(true); await f.InterruptTopologyProtocolAsync("topology.after-plan");
        var plan = JsonNode.Parse(File.ReadAllText(f.PlanPath))!;
        var delta = plan["files"]!.AsArray().Single(n => n!["delta"]!["entry"]!["path"]!.GetValue<string>() == "renamed/f00000")!["delta"]!;
        if (change == "directory-metadata")
        { var entry = plan["directories"]![0]!["entry"]!; entry["lastWriteTime"] = entry["lastWriteTime"]!.GetValue<long>() + 1; }
        else if (change == "range") delta["ranges"]![0]!["offset"] = delta["ranges"]![0]!["offset"]!.GetValue<long>() + 1;
        else if (change == "length") delta["entry"]!["length"] = delta["entry"]!["length"]!.GetValue<long>() + 1;
        else if (change == "unit-target") plan["topology"]!["units"]![1]!["finalPath"] = "not-the-recorded-path";
        else
        { var op = plan["topology"]!["operations"]!.AsArray().First(n => n!["kind"]!.GetValue<string>() == "install")!; op["path"] = "not-the-recorded-path"; }
        File.WriteAllText(f.PlanPath, plan.ToJsonString());
        string intentPath = Path.Combine(f.Root, "restore-topology-intent.json"); var intent = JsonNode.Parse(File.ReadAllText(intentPath))!;
        intent["planHash"] = ManagedDirectoryBasis.HashFile(f.PlanPath); File.WriteAllText(intentPath, intent.ToJsonString());
        byte[] changedPlan = File.ReadAllBytes(f.PlanPath), changedIntent = File.ReadAllBytes(intentPath);
        await Assert.ThrowsAsync<WorkspaceException>(() => f.Restore());
        Assert.Equal(changedPlan, File.ReadAllBytes(f.PlanPath)); Assert.Equal(changedIntent, File.ReadAllBytes(intentPath));
        Assert.True(Directory.Exists(f.State.Basis.Snapshot.RootPath)); Assert.False(Directory.Exists(f.State.OriginalPath));
        f.AssertJournalPreserved(); f.AllowCleanup();
    }

    [Theory]
    [InlineData("restore-topology-intent.json")]
    [InlineData("restore-topology-cursor.json")]
    [InlineData("restore-topology-verified.json")]
    [InlineData("restore-topology-staging-reclaim.json")]
    [InlineData("restore-topology-stage-00000000")]
    [InlineData("restore-topology-directory-00000000000000000000000000000001.json")]
    [InlineData("restore-topology-file-00000000000000000000000000000001.json")]
    [InlineData("restore-topology-write-00000000000000000000000000000001.json")]
    [InlineData("restore-topology-complete-00000000000000000000000000000001.json")]
    public async Task OrphanTopologyEvidenceCannotInitializeAnotherRoute(string name)
    {
        using var f = new RootTopologyRestoreFixture(true); string path = Path.Combine(f.Root, name); File.WriteAllText(path, "foreign");
        await Assert.ThrowsAsync<WorkspaceException>(() => f.Restore());
        Assert.Equal("foreign", File.ReadAllText(path)); Assert.True(Directory.Exists(f.State.Basis.Snapshot.RootPath));
        Assert.False(Directory.Exists(f.State.OriginalPath)); f.AssertJournalPreserved(); f.AllowCleanup();
    }

    [Fact]
    public async Task APartialRangeInsideARenamedDirectoryResumesWithoutTheOldBasePath()
    {
        using var f = new RootTopologyRestoreFixture(true, "large-range", payloadBytes: 262144);
        await f.InterruptTopologyProtocolAsync("topology.file.after-range-write");
        string path = f.Target("renamed/f00000"); byte[] partial = File.ReadAllBytes(path);
        Assert.NotEqual(f.ExpectedBytes["renamed/f00000"], partial); Assert.False(Directory.Exists(f.State.Basis.Snapshot.RootPath));
        f.AssertJournalPreserved(); await f.Restore(); f.AssertRestored(); f.AllowCleanup();
    }

    [Theory]
    [InlineData("topology.after-forest-proof", false)]
    [InlineData("topology.file.after-range-write", false)]
    [InlineData("topology.file.after-complete", true)]
    public async Task RetainedOrCompletedRangeChangesAreNotAbsorbedByTopologyResume(string boundary, bool privateByte)
    {
        using var f = new RootTopologyRestoreFixture(true, "range"); await f.InterruptTopologyProtocolAsync(boundary);
        string path = f.Target("renamed/f00000"); byte[] changed = File.ReadAllBytes(path); changed[privateByte ? 18 : 0] ^= 1;
        File.SetAttributes(path, FileAttributes.Normal); File.WriteAllBytes(path, changed); File.SetAttributes(path, FileAttributes.ReadOnly);
        await Assert.ThrowsAsync<WorkspaceException>(() => f.Restore()); Assert.Equal(changed, File.ReadAllBytes(path));
        Assert.Equal(FileAttributes.ReadOnly, File.GetAttributes(path)); f.AssertJournalPreserved(); f.AllowCleanup();
    }

    [Theory]
    [InlineData("foreign-child")]
    [InlineData("replace-directory")]
    [InlineData("replace-file")]
    public async Task VerifiedDeletionTreeStillRequiresEveryPresentOwnedIdentity(string change)
    {
        using var f = new RootTopologyRestoreFixture(true, "delete-tree"); await f.InterruptTopologyProtocolAsync("topology.after-verified");
        string staged = Assert.Single(f.Stages());
        if (change == "foreign-child") File.WriteAllText(Path.Combine(staged, "foreign"), "preserve");
        else if (change == "replace-directory")
        { Directory.Move(staged, Path.Combine(f.Root, "preserved-deleted-tree")); Directory.CreateDirectory(staged); }
        else
        { string path = Path.Combine(staged, "dead"); byte[] bytes = File.ReadAllBytes(path); File.Move(path, Path.Combine(f.Root, "preserved-deleted-file")); File.WriteAllBytes(path, bytes); }
        await Assert.ThrowsAsync<WorkspaceException>(() => f.Restore()); Assert.True(Directory.Exists(staged)); f.AssertJournalPreserved(); f.AllowCleanup();
    }

    [Fact]
    public async Task ChangedFinalSurvivorStopsPartiallyAuthorizedTreeReclamation()
    {
        using var f = new RootTopologyRestoreFixture(true, "delete-tree"); await f.InterruptTopologyProtocolAsync("topology.reclaim.file");
        Assert.NotEmpty(f.Stages()); string path = f.Target("renamed/f00001"); File.WriteAllBytes(path, [77]);
        await Assert.ThrowsAsync<WorkspaceException>(() => f.Restore()); Assert.NotEmpty(f.Stages());
        Assert.Equal(new byte[] { 77 }, File.ReadAllBytes(path)); f.AssertJournalPreserved(); f.AllowCleanup();
    }

    [Fact]
    public async Task StableForestPlanJournalAndWritableLeafArePinnedAtTheirEffectBoundaries()
    {
        using var f = new RootTopologyRestoreFixture(true, "range"); int proofs = 0, writes = 0;
        await f.RestoreTopologyProtocolAsync(stage =>
        {
            if (stage == "topology.after-forest-proof")
            {
                foreach (string path in new[] { f.PlanPath, Path.Combine(f.Request.DeltaPath, "delta.journal") })
                    Assert.Throws<IOException>(() => { using var file = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete); });
                proofs++;
            }
            if (stage == "topology.file.before-ranges-write")
            { Assert.Throws<IOException>(() => File.Move(f.Target("renamed/f00000"), Path.Combine(f.Root, "foreign-replacement"))); writes++; }
        });
        Assert.Equal(1, proofs); Assert.Equal(1, writes); f.AssertRestored(); f.AllowCleanup();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancelledCompletionPublicationPreservesItsTemporaryAndResumesWithFreshCancellation(bool bounded)
    {
        using var f = new RootTopologyRestoreFixture(bounded, "range");
        using var cancelled = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        bool interrupted = false;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => f.RestoreTopologyProtocolAsync(stage =>
        { if (stage == "topology.file.after-ranges-flush") { interrupted = true; cancelled.Cancel(); } }, cancelled.Token));
        Assert.True(interrupted);
        string temporary = Assert.Single(Directory.GetFiles(f.Root, "restore-topology-complete-*.json.*.tmp"));
        byte[] before = File.ReadAllBytes(temporary); string identity = RootTopologyRestoreFixture.Identity(temporary);
        f.AssertJournalPreserved(); await f.Restore(); f.AssertRestored();
        Assert.Equal(before, File.ReadAllBytes(temporary)); Assert.Equal(identity, RootTopologyRestoreFixture.Identity(temporary)); f.AllowCleanup();
    }

    [Fact]
    public async Task UnpublishedPlanTemporaryIsNeverAdoptedOrDeletedWhenStartingFresh()
    {
        using var f = new RootTopologyRestoreFixture(true);
        string temporary = f.PlanPath + ".0123456789abcdef0123456789abcdef.tmp";
        File.WriteAllText(temporary, "{unpublished partial plan"); byte[] before = File.ReadAllBytes(temporary);
        string identity = RootTopologyRestoreFixture.Identity(temporary);
        await f.RestoreTopologyProtocolAsync(); f.AssertRestored();
        Assert.Equal(before, File.ReadAllBytes(temporary)); Assert.Equal(identity, RootTopologyRestoreFixture.Identity(temporary)); f.AllowCleanup();
    }

    [Theory]
    [InlineData("unknown-stem")]
    [InlineData("bad-guid")]
    [InlineData("directory")]
    [InlineData("reparse")]
    public async Task TemporaryLookingForeignArtifactsAreStillPreservedAndRefused(string change)
    {
        using var f = new RootTopologyRestoreFixture(true);
        string path = Path.Combine(f.Root, change == "unknown-stem" ? "restore-topology-foreign.json.0123456789abcdef0123456789abcdef.tmp" :
            change == "bad-guid" ? "restore-topology-plan.json.invalid.tmp" : "restore-topology-plan.json.0123456789abcdef0123456789abcdef.tmp");
        if (change is "directory" or "reparse") Directory.CreateDirectory(path); else File.WriteAllText(path, "foreign");
        if (change == "reparse")
        { string outside = Path.Combine(f.DirectoryPath, "outside"); Directory.CreateDirectory(outside); File.WriteAllText(Path.Combine(outside, "marker"), "preserve"); OwnedDirectoryLifetimeTests.SetJunction(path, outside); }
        try
        {
            await Assert.ThrowsAsync<WorkspaceException>(() => f.Restore()); f.AssertJournalPreserved();
            Assert.True(Directory.Exists(f.State.Basis.Snapshot.RootPath)); Assert.False(Directory.Exists(f.State.OriginalPath));
            if (change == "reparse") Assert.Equal("preserve", File.ReadAllText(Path.Combine(f.DirectoryPath, "outside", "marker")));
            else if (change != "directory") Assert.Equal("foreign", File.ReadAllText(path));
        }
        finally { if (change == "reparse" && Directory.Exists(path)) Directory.Delete(path); }
        f.AllowCleanup();
    }
}

// Public product calls are deliberately unchanged: one frozen Tests.dll can
// exercise the old general route and the candidate distribution independently.
internal sealed class RootTopologyRestoreFixture : IDisposable
{
    private readonly TestDirectory directory = new();
    private readonly bool bounded;
    private bool allowCleanup;
    private readonly Dictionary<string, string> originalIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, byte[]> expectedBytes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DeltaEntry> expectedEntries = new(StringComparer.OrdinalIgnoreCase);
    private static readonly DateTime FixedTime = new(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
    private static long FinalTime => FixedTime.AddDays(1).ToFileTimeUtc();
    internal string DirectoryPath => directory.Path;
    internal string Root => State.Basis.ReceiptDirectory;
    internal string PlanPath => Path.Combine(Root, "restore-topology-plan.json");
    internal string CursorPath => Path.Combine(Root, "restore-topology-cursor.json");
    internal ManagedDirectoryState State { get; }
    internal OverlayServeRequest Request { get; }
    internal OwnedDirectoryManifest Manifest { get; }
    internal IReadOnlyDictionary<string, byte[]> ExpectedBytes => expectedBytes;
    internal string InputHash { get; }
    internal string ExpectedHash { get; private set; } = "";
    internal int OriginalFileCount => Manifest.Items.Count(i => !i.Entry.Directory);
    internal long OriginalBytes => Manifest.Items.Sum(i => i.Entry.Length);
    internal string Target(string path) => Path.Combine(State.OriginalPath, path.Replace('/', Path.DirectorySeparatorChar));
    private string BasisPath(string path) => Path.Combine(State.Basis.Snapshot.RootPath, path.Replace('/', Path.DirectorySeparatorChar));
    internal string[] Stages() => Directory.GetFileSystemEntries(Root, "restore-topology-stage-*").Order(StringComparer.Ordinal).ToArray();
    internal string[] StageIdentityMap() => Stages().Select(p => Path.GetFileName(p) + ":" + Identity(p, Directory.Exists(p))).ToArray();

    internal RootTopologyRestoreFixture(bool bounded, string profile = "mixed", int count = 24, int payloadBytes = 4096)
    {
        if (!OperatingSystem.IsWindows()) Assert.Skip("Ordinary NTFS topology, identity and root transfer contract.");
        bool rootOnly = profile is "root-swap" or "root-create" or "root-empty";
        if ((rootOnly ? count != 2 : count < 20) || payloadBytes < 1024) throw new ArgumentOutOfRangeException(nameof(count));
        this.bounded = bounded;
        string root = Path.Combine(directory.Path, "owned"), basis = Path.Combine(root, "base"), project = Path.Combine(directory.Path, "original");
        Directory.CreateDirectory(basis); Directory.CreateDirectory(project);
        string[] specialFiles = ["alpha/nested/keep", "alpha/nested/dead", "alpha/loose", "beta/stay", "doomed/child/keep", "doomed/dead", "as-file", "as-directory/inside", "case/File"];
        if (profile == "cost") { specialFiles[0] = "bulk/nested/keep"; specialFiles[1] = "bulk/nested/dead"; }
        string[] paths = rootOnly ? ["alpha/leaf", "beta/leaf"] :
            Enumerable.Range(0, count - specialFiles.Length).Select(i => $"bulk/f{i:D5}").Concat(specialFiles).ToArray();
        for (int i = 0; i < paths.Length; i++)
        {
            string path = Path.Combine(basis, paths[i]); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, Enumerable.Range(0, payloadBytes).Select(offset => (byte)(i * 13 + offset * 17)).ToArray());
            File.SetCreationTimeUtc(path, FixedTime); File.SetLastAccessTimeUtc(path, FixedTime); File.SetLastWriteTimeUtc(path, FixedTime);
            File.SetAttributes(path, i == 0 ? FileAttributes.ReadOnly : FileAttributes.Normal);
        }
        if (!rootOnly) Directory.CreateDirectory(Path.Combine(basis, "empty"));
        foreach (string path in Directory.GetDirectories(basis, "*", SearchOption.AllDirectories).Append(basis))
        { Directory.SetCreationTimeUtc(path, FixedTime); Directory.SetLastAccessTimeUtc(path, FixedTime); Directory.SetLastWriteTimeUtc(path, FixedTime); }
        var snapshot = new SnapshotReference("012345678901234567890123", OwnedDirectorySnapshotTree.ProviderName, basis, basis,
            DateTimeOffset.UnixEpoch, true, true, "stopped-original-owned-directory");
        Manifest = OwnedDirectorySnapshotTree.Capture(snapshot, snapshot.Id, Path.Combine(root, OwnedDirectoryManifest.MetadataDirectoryName));
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        File.WriteAllText(Path.Combine(root, "manifest.json"), JsonSerializer.Serialize(Manifest, json));
        string hash = ManagedDirectoryBasis.HashFile(Path.Combine(root, "manifest.json"));
        File.WriteAllText(Path.Combine(root, "proof.json"), JsonSerializer.Serialize(new OwnedDirectoryProof(1, snapshot, snapshot.Id, hash, OriginalBytes), json));
        State = new() { ApplicationKind = "unity", DirectoryKind = "Library", Key = "topology", Phase = "restoring",
            OriginalPath = Path.Combine(project, "Library"), CompatibilityKey = "topology",
            Basis = new(snapshot.Id, project, "Library", snapshot, root, snapshot.Id, hash) };
        Request = new(new(2, snapshot.Id, snapshot, 64L * 1048576, 65536), Path.Combine(root, "delta"), State.OriginalPath,
            Path.Combine(root, "overlay"), "token", new(snapshot.Id, basis, hash), root, snapshot.Id);
        foreach (var item in Manifest.Items) originalIds.Add(item.Entry.Path, Identity(BasisPath(item.Entry.Path), item.Entry.Directory));
        Mutate(j => Apply(j, profile, payloadBytes));
        InputHash = Hash(JsonSerializer.SerializeToUtf8Bytes(new { profile, count, payloadBytes, Manifest.Items }, json));
        CaptureExpected();
    }

    private static void Apply(JournalDeltaStore j, string profile, int payloadBytes)
    {
        void Move(string source, string target) => j.Rename(j.Stat(source)!.Id, target, false);
        void Delete(string path) => j.Delete(j.Stat(path)!.Id);
        void Dir(string path) => j.Create(path, true, 16);
        void New(string path) { Guid id = j.Create(path, false, 32).Id; j.Write(id, 0, [9]); j.SetLength(id, 17); j.Write(id, 16, [6]); }
        if (profile is "swap" or "root-swap") { Move("alpha", "swap-temporary"); Move("beta", "alpha"); Move("swap-temporary", "beta"); }
        else if (profile is "root-create" or "root-empty")
        {
            Delete("alpha/leaf"); Delete("alpha"); Delete("beta/leaf"); Delete("beta");
            if (profile == "root-create") { Dir("fresh"); New("fresh/new"); }
        }
        else if (profile == "leaf-cost")
        {
            var leaves = new List<string>();
            void CollectLeaves(string path)
            {
                foreach (var entry in j.List(path))
                    if (entry.Directory) CollectLeaves(entry.Path); else leaves.Add(entry.Path);
            }
            CollectLeaves("");
            foreach (string path in leaves.Order(StringComparer.Ordinal)) Move(path, path + "-renamed");
        }
        else if (profile == "case") { Move("alpha", "ALPHA"); Move("case/File", "case/file"); Move("case", "CASE"); }
        else if (profile == "reparent") { Move("alpha/nested", "beta/nested"); Move("alpha", "beta/parent"); }
        else if (profile == "ancestor") { Move("alpha/nested", "detached"); Move("alpha", "detached/parent"); }
        else if (profile == "deleted-parent")
        { Move("doomed/child", "survivor"); Delete("doomed/dead"); Delete("doomed"); Dir("doomed"); Move("survivor", "doomed/child"); }
        else if (profile == "types")
        { Delete("as-file"); Dir("as-file"); New("as-file/new"); Delete("as-directory/inside"); Delete("as-directory"); New("as-directory"); }
        else
        {
            Move("bulk", "renamed");
            if (profile == "new-directory") { Dir("new-parent"); Dir("new-parent/empty"); New("new-parent/new"); }
            else if (profile == "delete-tree")
            { Delete("doomed/child/keep"); Delete("doomed/child"); Delete("doomed/dead"); Delete("doomed"); }
            else if (profile is "range" or "large-range")
            {
                byte[] bytes = profile == "range" ? [91, 92, 93, 94] : Enumerable.Range(0, 131089).Select(i => (byte)(i * 7)).ToArray();
                for (int offset = 0; offset < bytes.Length; offset += 65536)
                    j.Write(j.Stat("renamed/f00000")!.Id, 17 + offset, bytes.AsSpan(offset, Math.Min(65536, bytes.Length - offset)));
            }
            else if (profile != "bulk")
            {
                Move(profile == "cost" ? "renamed/nested" : "alpha/nested", "renamed/extracted"); Move("alpha", "renamed/parent");
                Delete("renamed/extracted/dead"); Move("renamed/parent/loose", "beta/moved");
                Move("doomed/child/keep", "beta/rescued"); Delete("doomed/child"); Delete("doomed/dead"); Delete("doomed");
                Dir("renamed/new-parent"); Dir("renamed/new-parent/empty"); New("renamed/new-parent/new"); j.Create("renamed/new-parent/new-empty", false, 32);
                j.Write(j.Stat("renamed/f00000")!.Id, 17, [91, 92, 93, 94]);
                j.SetLength(j.Stat("renamed/f00001")!.Id, 17);
                Guid extended = j.Stat("renamed/f00002")!.Id; j.SetLength(extended, payloadBytes + 17); j.Write(extended, payloadBytes + 14, [5, 6, 7]);
                Delete("renamed/f00003"); Move("renamed/f00004", "beta/moved-leaf");
                j.Write(j.Stat("renamed/f00005")!.Id, 0, Enumerable.Repeat((byte)77, payloadBytes).ToArray());
            }
        }
        // Only changed/new files and directories need deterministic post-edit
        // times. Do not create 10,000 unrelated durable metadata writes in setup.
        var entries = new List<DeltaEntry> { j.Stat("")! };
        void Collect(string path)
        {
            foreach (var entry in j.List(path)) { entries.Add(entry); if (entry.Directory) Collect(entry.Path); }
        }
        Collect("");
        foreach (var entry in entries)
        {
            if (entry.Directory || entry.BasePath is null || entry.LastWriteTime != FixedTime.ToFileTimeUtc())
                j.SetMetadata(entry.Id, entry.Attributes, FinalTime, FinalTime, FinalTime);
        }
    }

    internal void Mutate(Action<JournalDeltaStore> action)
    {
        using var tree = new OwnedDirectorySnapshotTree(Manifest, State.Basis.Snapshot);
        using var journal = new JournalDeltaStore(Request.DeltaPath, Request.Identity, tree, optimizeJournal: !bounded, boundedLogBytes: bounded ? 1048576 : null);
        action(journal);
    }
    private void CaptureExpected()
    {
        using var tree = new OwnedDirectorySnapshotTree(Manifest, State.Basis.Snapshot);
        using var journal = JournalDeltaStore.OpenReadOnly(Request.DeltaPath, Request.Identity, tree);
        Assert.True(journal.Seal().DurableSequence > 0);
        foreach (var delta in journal.DescribeRestore())
        {
            var entry = delta.Entry; expectedEntries.Add(entry.Path, entry); if (entry.Directory) continue;
            byte[] bytes = new byte[checked((int)entry.Length)]; int offset = 0;
            while (offset < bytes.Length)
            { int read = journal.Read(entry.Id, offset, bytes.AsSpan(offset, Math.Min(Request.Identity.MaxTransferBytes, bytes.Length - offset))); Assert.True(read > 0); offset += read; }
            expectedBytes.Add(entry.Path, bytes);
        }
        ExpectedHash = Hash(JsonSerializer.SerializeToUtf8Bytes(expectedEntries.Values.OrderBy(e => e.Path, StringComparer.Ordinal)
            .Select(e => new { e.Path, e.Directory, e.Length, e.Attributes, e.CreationTime, e.LastAccessTime, e.LastWriteTime,
                hash = e.Directory ? null : Hash(expectedBytes[e.Path]) })));
    }
    internal Task Restore(Action<string>? boundary = null) => ManagedDirectoryRestore.ExecuteAsync(State, Request, TestContext.Current.CancellationToken, boundary);
    // Explicit protocol entry for algorithm/ownership tests, independent of
    // fresh routing economics. Keep initial callbacks (including root-unpin
    // and after-plan) in the tested call. Once a plan exists, always use the
    // real dispatcher so conflicting modes and resume authorization stay live.
    // Cost tests call Restore above, never this internal-protocol helper.
    internal async Task RestoreTopologyProtocolAsync(Action<string>? boundary = null, CancellationToken? cancellationToken = null)
    {
        var ct = cancellationToken ?? TestContext.Current.CancellationToken;
        if (File.Exists(PlanPath))
        {
            await ManagedDirectoryRestore.ExecuteAsync(State, Request, ct, boundary); return;
        }
        using var tree = new OwnedDirectorySnapshotTree(Manifest, State.Basis.Snapshot);
        using var journal = JournalDeltaStore.OpenReadOnly(Request.DeltaPath, Request.Identity, tree);
        var entries = journal.DescribeRestore(); var seal = journal.Seal();
        Assert.True(ManagedTopologyDirectoryRestore.Eligible(State, Request, Manifest, entries));
        await ManagedTopologyDirectoryRestore.CreateAsync(State, Request, Manifest, seal, entries, tree, journal, ct, boundary);
    }
    // Safety fixtures can resume a valid already-published B plan even when
    // its small input would now choose the general fresh-restore route. Keep
    // this helper out of Restore/constructor: the same cost test assembly is
    // also loaded against a frozen runtime without the new internal methods.
    internal async Task SeedTopologyPlanAsync()
    {
        using var tree = new OwnedDirectorySnapshotTree(Manifest, State.Basis.Snapshot);
        using var journal = JournalDeltaStore.OpenReadOnly(Request.DeltaPath, Request.Identity, tree);
        var entries = journal.DescribeRestore(); var seal = journal.Seal();
        Assert.True(ManagedTopologyDirectoryRestore.Eligible(State, Request, Manifest, entries));
        Assert.False(ManagedTopologyDirectoryRestore.PreferFresh(Manifest, entries));
        int published = 0;
        await Assert.ThrowsAsync<IOException>(() => ManagedTopologyDirectoryRestore.CreateAsync(State, Request, Manifest,
            seal, entries, tree, journal, TestContext.Current.CancellationToken, stage =>
            {
                if (stage == "topology.after-plan")
                { published++; throw new IOException("Preserve a current topology plan before its root move."); }
            }));
        Assert.Equal(1, published); Assert.True(File.Exists(PlanPath));
        Assert.True(File.Exists(Path.Combine(Root, "restore-topology-intent.json")));
        Assert.True(Directory.Exists(State.Basis.Snapshot.RootPath)); Assert.False(Directory.Exists(State.OriginalPath));
    }
    internal async Task InterruptTopologyProtocolAsync(string stage, int occurrence = 1)
    {
        int observed = 0;
        await Assert.ThrowsAsync<IOException>(() => RestoreTopologyProtocolAsync(value =>
        { if (value == stage && ++observed == occurrence) throw new IOException("Fixed topology interruption: " + stage); }));
        Assert.Equal(occurrence, observed);
    }
    internal void AssertRestored(bool requireOriginalDirectoryIdentities = true)
    {
        foreach (var entry in expectedEntries.Values)
        {
            string path = Target(entry.Path);
            Assert.Equal(entry.Attributes, (uint)File.GetAttributes(path));
            Assert.Equal(entry.CreationTime, File.GetCreationTimeUtc(path).ToFileTimeUtc());
            Assert.Equal(entry.LastAccessTime, File.GetLastAccessTimeUtc(path).ToFileTimeUtc());
            Assert.Equal(entry.LastWriteTime, File.GetLastWriteTimeUtc(path).ToFileTimeUtc());
            if (entry.BasePath is { } basis && (!entry.Directory || requireOriginalDirectoryIdentities)) Assert.Equal(originalIds[basis], Identity(path, entry.Directory));
            if (!entry.Directory) { Assert.Equal(entry.Length, new FileInfo(path).Length); Assert.Equal(expectedBytes[entry.Path], File.ReadAllBytes(path)); }
        }
        Assert.Equal(expectedEntries.Keys.Order(StringComparer.Ordinal), Directory.EnumerateFileSystemEntries(State.OriginalPath, "*", SearchOption.AllDirectories)
            .Select(p => Path.GetRelativePath(State.OriginalPath, p).Replace('\\', '/')).Prepend("").Order(StringComparer.Ordinal));
        Assert.False(Directory.Exists(State.Basis.Snapshot.RootPath)); Assert.False(Directory.Exists(Request.DeltaPath)); Assert.Empty(Stages());
        Assert.Equal(0, State.InitialPayloadCopiedBytes);
    }
    internal bool OriginalDirectoryIdentitiesPreserved() => expectedEntries.Values.Where(e => e.Directory && e.BasePath is not null)
        .All(e => originalIds[e.BasePath!] == Identity(Target(e.Path), true));
    internal void AssertOriginalFilesPreserved(bool allowNewFiles = false)
    {
        var files = Directory.EnumerateFiles(State.OriginalPath, "*", SearchOption.AllDirectories)
            .Concat(Stages().SelectMany(path => Directory.Exists(path) ? Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories) : new[] { path }))
            .ToDictionary(path => Identity(path), path => path, StringComparer.Ordinal);
        if (!allowNewFiles) Assert.Equal(OriginalFileCount, files.Count);
        foreach (var item in Manifest.Items.Where(item => !item.Entry.Directory))
        {
            Assert.True(files.TryGetValue(originalIds[item.Entry.Path], out string? path)); Assert.NotNull(path);
            Assert.Equal(item.Entry.Length, new FileInfo(path).Length); Assert.Equal(item.Hash, Hash(File.ReadAllBytes(path)));
        }
    }
    internal void AssertJournalPreserved() { Assert.True(Directory.Exists(Request.DeltaPath)); Assert.True(File.Exists(Path.Combine(Request.DeltaPath, "delta.journal"))); }
    internal static string Identity(string path, bool directory = false)
        => RootTopologyDirectoryRestoreTests.ReadNativeIdentity(path, directory);
    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    internal void AllowCleanup() => allowCleanup = true;
    public void Dispose()
    { if (allowCleanup) directory.Dispose(); else TestContext.Current.TestOutputHelper?.WriteLine("Preserved unexpected topology fixture failure at " + directory.Path); }
}
