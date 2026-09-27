using System.Diagnostics;
using Splinter.Application.Unity;
using Splinter.Core;
using Splinter.SourceControl.Git;

namespace Splinter.Tests;

public sealed class GitSnapshotWorkspaceTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CandidateAttachAllowsChangedSourcesButPreservesIndexLock(bool hasLock)
    {
        var ct = TestContext.Current.CancellationToken;
        using var d = new TestDirectory();
        string source = Path.Combine(d.Path, "source"), mount = Path.Combine(d.Path, "mount");
        Directory.CreateDirectory(source);
        await TestDirectory.Git(source, "init", "-b", "development");
        await TestDirectory.Git(source, "config", "user.name", "Fixture");
        await TestDirectory.Git(source, "config", "user.email", "fixture@example.invalid");
        await File.WriteAllTextAsync(Path.Combine(source, "file.txt"), "baseline\n", ct);
        await TestDirectory.Git(source, "add", "."); await TestDirectory.Git(source, "commit", "-m", "fixture");
        string revision = await TestDirectory.Git(source, "rev-parse", "HEAD");
        var receipt = await GitOverlayRegistration.PrepareAsync(source, mount, "codex/candidate", revision,
            "candidate", Path.Combine(d.Path, "receipt.json"), ct);
        Directory.CreateDirectory(mount);
        await File.WriteAllTextAsync(Path.Combine(mount, ".git"), receipt.Pointer, ct);
        await File.WriteAllTextAsync(Path.Combine(mount, "file.txt"), "candidate\n", ct);
        await TestDirectory.Git(mount, "read-tree", revision);
        string lockPath = Path.Combine(receipt.GitDirectory, "index.lock");
        if (hasLock)
        {
            await File.WriteAllTextAsync(lockPath, "owned-lock", ct);
            Assert.Equal("GitCommandFailed", (await Assert.ThrowsAsync<WorkspaceException>(() =>
                GitOverlayRegistration.AttachAsync(receipt, ct, allowSourceChanges: true))).Code);
            Assert.Equal("owned-lock", await File.ReadAllTextAsync(lockPath, ct));
        }
        else
        {
            await GitOverlayRegistration.AttachAsync(receipt, ct, allowSourceChanges: true);
            Assert.Contains("file.txt", await TestDirectory.Git(mount, "status", "--porcelain"));
        }
        Assert.Equal("candidate\n", await File.ReadAllTextAsync(Path.Combine(mount, "file.txt"), ct));
        Assert.Equal("baseline\n", await File.ReadAllTextAsync(Path.Combine(source, "file.txt"), ct));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InterruptedAttachRefreshesExistingIndexAndPreservesOutstandingLock(bool hasLock)
    {
        var ct = TestContext.Current.CancellationToken;
        using var d = new TestDirectory();
        string source = Path.Combine(d.Path, "source"), mount = Path.Combine(d.Path, "mount");
        Directory.CreateDirectory(source);
        await TestDirectory.Git(source, "init", "-b", "development");
        await TestDirectory.Git(source, "config", "user.name", "Fixture");
        await TestDirectory.Git(source, "config", "user.email", "fixture@example.invalid");
        await File.WriteAllTextAsync(Path.Combine(source, "file.txt"), "captured payload\n", ct);
        await TestDirectory.Git(source, "add", ".");
        await TestDirectory.Git(source, "commit", "-m", "fixture");
        string revision = await TestDirectory.Git(source, "rev-parse", "HEAD");
        var receipt = await GitOverlayRegistration.PrepareAsync(source, mount, "codex/interrupted", revision,
            "interrupted", Path.Combine(d.Path, "control", "git.json"), ct);
        // Real Git registration with a fake immutable tree, not WinFsp proof.
        Directory.CreateDirectory(mount);
        await File.WriteAllTextAsync(Path.Combine(mount, ".git"), receipt.Pointer, ct);
        await File.WriteAllTextAsync(Path.Combine(mount, "file.txt"), "captured payload\n", ct);
        await TestDirectory.Git(mount, "read-tree", revision);
        string indexPath = Path.Combine(receipt.GitDirectory, "index");
        byte[] initialIndex = await File.ReadAllBytesAsync(indexPath, ct);
        if (hasLock)
        {
            string lockPath = indexPath + ".lock";
            await File.WriteAllTextAsync(lockPath, "preserve interrupted writer", ct);
            Assert.Equal("GitCommandFailed", (await Assert.ThrowsAsync<WorkspaceException>(
                () => GitOverlayRegistration.AttachAsync(receipt, ct))).Code);
            Assert.Equal("preserve interrupted writer", await File.ReadAllTextAsync(lockPath, ct));
            Assert.Equal(initialIndex, await File.ReadAllBytesAsync(indexPath, ct));
        }
        else
        {
            await GitOverlayRegistration.AttachAsync(receipt, ct);
            byte[] refreshedIndex = await File.ReadAllBytesAsync(indexPath, ct);
            Assert.False(initialIndex.AsSpan().SequenceEqual(refreshedIndex));
            Assert.Equal("", await TestDirectory.Git(mount, "status", "--porcelain"));
        }
        Assert.Equal(revision, await TestDirectory.Git(mount, "rev-parse", "HEAD"));
        Assert.Equal("captured payload\n", await File.ReadAllTextAsync(Path.Combine(mount, "file.txt"), ct));
        Assert.Equal("captured payload\n", await File.ReadAllTextAsync(Path.Combine(source, "file.txt"), ct));
    }

    [Theory]
    [InlineData("ntfs-snapshot")]
    [InlineData("another-snapshot-provider")]
    public async Task OnlyObservedMountRootMayBeAReparsePoint(string storageMode)
    {
        if (!OperatingSystem.IsWindows()) Assert.Skip("Windows directory mount path admission.");
        var ct = TestContext.Current.CancellationToken; using var d = new TestDirectory();
        string source = Path.Combine(d.Path, "source"), tree = Path.Combine(d.Path, "tree"), control = Path.Combine(d.Path, "control");
        Directory.CreateDirectory(Path.Combine(source, "Nested")); Directory.CreateDirectory(control);
        await TestDirectory.Git(source, "init", "-b", "development");
        await TestDirectory.Git(source, "config", "user.name", "Fixture");
        await TestDirectory.Git(source, "config", "user.email", "fixture@example.invalid");
        await File.WriteAllTextAsync(Path.Combine(source, "Nested", "file.txt"), "original", ct);
        await TestDirectory.Git(source, "add", "."); await TestDirectory.Git(source, "commit", "-m", "fixture");
        var w = new WorkspaceRecord { Id = "mounted", RequestKey = "mounted", ProjectPath = source, Path = Path.Combine(d.Path, "mount"),
            BaseReference = "development", SourceReference = "codex/mounted", StorageMode = storageMode, Snapshot = new() };
        var git = new GitWorkspaceProvider(); var unity = new UnityWorkspaceProvider();
        w.BaseRevision = await git.PreflightAsync(w, ct);
        w.Snapshot.Reference = new("snapshot", "fake", source, "immutable", DateTimeOffset.UtcNow, true, true, "fake");
        await git.RegisterAsync(w, control, ct);
        Directory.CreateDirectory(Path.Combine(tree, "Nested"));
        await File.WriteAllTextAsync(Path.Combine(tree, "Nested", "file.txt"), "original", ct);
        // A junction models the root attribute only; this is not WinFsp evidence.
        await Junction(w.Path, tree, ct);
        string nestedLink = Path.Combine(w.Path, "Nested"); bool nestedIsLink = false, rootIsLink = true;
        try
        {
            await git.AttachAsync(w, control, ct);
            Assert.Equal("GitSymlinkRejected", (await Assert.ThrowsAsync<WorkspaceException>(() => git.InspectAsync(w, ct))).Code);
            Assert.Equal("UnsafePath", (await Assert.ThrowsAsync<WorkspaceException>(() => unity.RemoveInfrastructureAsync(w, control, ct))).Code);
            w.Snapshot.Mount = new(w.Id, w.Path, "delta", w.Snapshot.Reference, 123, 1);
            w.Snapshot.Observation = new(true, true, true, true, 0, 0, 0);
            // Ordinary junctions still fail Git's independent root identity
            // check; a WinFsp directory mount exposes its own worktree root.
            Assert.Equal("GitWorkspaceMismatch", (await Assert.ThrowsAsync<WorkspaceException>(() => git.InspectAsync(w, ct))).Code);
            await unity.RemoveInfrastructureAsync(w, control, ct);
            w.Snapshot.Observation = w.Snapshot.Observation with { OwnershipVerified = false };
            Assert.Equal("GitSymlinkRejected", (await Assert.ThrowsAsync<WorkspaceException>(() => git.InspectAsync(w, ct))).Code);
            Assert.Equal("UnsafePath", (await Assert.ThrowsAsync<WorkspaceException>(() => unity.RemoveInfrastructureAsync(w, control, ct))).Code);
            w.Snapshot.Observation = w.Snapshot.Observation with { OwnershipVerified = true };
            Directory.Delete(w.Path, false); rootIsLink = false;
            Directory.Move(tree, w.Path);
            Assert.False((await git.InspectAsync(w, ct)).HasChanges);
            string outside = Path.Combine(d.Path, "outside"); Directory.CreateDirectory(outside);
            File.Move(Path.Combine(nestedLink, "file.txt"), Path.Combine(outside, "file.txt")); Directory.Delete(nestedLink);
            await Junction(nestedLink, outside, ct); nestedIsLink = true;
            Assert.Equal("GitSymlinkRejected", (await Assert.ThrowsAsync<WorkspaceException>(() => git.InspectAsync(w, ct))).Code);
            Assert.Equal("original", await File.ReadAllTextAsync(Path.Combine(outside, "file.txt"), ct));
        }
        finally
        {
            if (nestedIsLink) Directory.Delete(nestedLink, false);
            if (rootIsLink) Directory.Delete(w.Path, false);
        }
    }

    private static async Task Junction(string path, string target, CancellationToken ct)
    {
        var start = new ProcessStartInfo("cmd.exe") { UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true, Arguments = $"/d /c mklink /J \"{path}\" \"{target}\"" };
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync(ct); var error = process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);
        Assert.True(process.ExitCode == 0, await output + await error);
    }

    [Fact]
    public async Task RegistrationHasNoCheckoutAndOriginalMayChangeAfterCapture()
    {
        var ct = TestContext.Current.CancellationToken; using var d = new TestDirectory();
        string source = Path.Combine(d.Path, "source"); Directory.CreateDirectory(source);
        await TestDirectory.Git(source, "init", "-b", "development");
        await TestDirectory.Git(source, "config", "user.name", "Fixture");
        await TestDirectory.Git(source, "config", "user.email", "fixture@example.invalid");
        await File.WriteAllTextAsync(Path.Combine(source, "README.md"), "original\n", ct);
        await TestDirectory.Git(source, "add", "."); await TestDirectory.Git(source, "commit", "-m", "fixture");
        string revision = await TestDirectory.Git(source, "rev-parse", "HEAD");
        string control = Path.Combine(d.Path, "control"); Directory.CreateDirectory(control);
        var w = new WorkspaceRecord { Id = "opaque", RequestKey = "task", ProjectPath = source, Path = Path.Combine(d.Path, "mount"),
            BaseReference = "development", SourceReference = "codex/test", StorageMode = "ntfs-snapshot", Snapshot = new() };
        var git = new GitWorkspaceProvider();
        w.BaseRevision = await git.PreflightAsync(w, ct);
        w.Snapshot.Reference = new("snapshot", "fake", source, "immutable", DateTimeOffset.UtcNow, true, true, "fake");
        await File.WriteAllTextAsync(Path.Combine(source, "README.md"), "original continues editing\n", ct);
        await git.RegisterAsync(w, control, ct); Assert.False(Directory.Exists(w.Path));
        await git.RegisterAsync(w, control, ct); Assert.False(Directory.Exists(w.Path));
        // Fake immutable tree; this is real Git evidence, not a CoW/mount test.
        Directory.CreateDirectory(w.Path); await File.WriteAllTextAsync(Path.Combine(w.Path, "README.md"), "original\n", ct);
        await git.AttachAsync(w, control, ct);
        Assert.False((await git.InspectAsync(w, ct)).HasChanges);
        await File.WriteAllTextAsync(Path.Combine(w.Path, ".gitattributes"), "README.md filter=new-filter\n", ct);
        Assert.Equal("SnapshotFilterUnsupported", (await Assert.ThrowsAsync<WorkspaceException>(() => git.CommitAsync(w, "must not collect filtered data", ct))).Code);
        File.Delete(Path.Combine(w.Path, ".gitattributes"));
        await File.WriteAllTextAsync(Path.Combine(w.Path, "README.md"), "private result\n", ct);
        var state = await git.InspectAsync(w, ct);
        w.Validation = new("request", state.Fingerprint, true, DateTimeOffset.UtcNow, []);
        w.ResultRevision = await git.CommitAsync(w, "result", ct);
        string reference = await git.PreserveAsync(w, control, ct);
        w.Snapshot.Result = new("snapshot", w.ResultRevision, state.Fingerprint, reference, new(1, 100, "fake"), true, SnapshotResult.StableJournalProtocol);
        await git.VerifyResultAsync(w, control, ct);
        File.Delete(Path.Combine(w.Path, "README.md")); File.Delete(Path.Combine(w.Path, ".git")); Directory.Delete(w.Path);
        await git.UnregisterAsync(w, control, ct); await git.UnregisterAsync(w, control, ct);
        Assert.Equal(revision, await TestDirectory.Git(source, "rev-parse", "HEAD"));
        Assert.Equal("original continues editing\n", await File.ReadAllTextAsync(Path.Combine(source, "README.md"), ct));
        Assert.Equal(w.ResultRevision, await TestDirectory.Git(source, "rev-parse", reference));
    }

    [Fact]
    public async Task LfsAttributesAreRejectedBeforeSnapshotOrRegistration()
    {
        var ct = TestContext.Current.CancellationToken; using var d = new TestDirectory();
        await TestDirectory.Git(d.Path, "init", "-b", "development");
        await TestDirectory.Git(d.Path, "config", "user.name", "Fixture");
        await TestDirectory.Git(d.Path, "config", "user.email", "fixture@example.invalid");
        await File.WriteAllTextAsync(Path.Combine(d.Path, "file.bin"), "payload", ct);
        await TestDirectory.Git(d.Path, "add", "."); await TestDirectory.Git(d.Path, "commit", "-m", "fixture");
        await File.WriteAllTextAsync(Path.Combine(d.Path, ".gitattributes"), "file.bin filter=not-qualified\n", ct);
        await TestDirectory.Git(d.Path, "add", ".gitattributes"); await TestDirectory.Git(d.Path, "commit", "-m", "filter");
        var w = new WorkspaceRecord { Id = "x", RequestKey = "x", ProjectPath = d.Path, Path = Path.Combine(d.Path, "unused"), BaseReference = "development", SourceReference = "task" };
        Assert.Equal("SnapshotFilterUnsupported", (await Assert.ThrowsAsync<WorkspaceException>(() => new GitWorkspaceProvider().PreflightAsync(w, ct))).Code);
        Assert.False(Directory.Exists(w.Path));
        await TestDirectory.Git(d.Path, "update-index", "--assume-unchanged", "file.bin");
        Assert.Equal("SnapshotSourceUnsupported", (await Assert.ThrowsAsync<WorkspaceException>(() => new GitWorkspaceProvider().PreflightAsync(w, ct))).Code);
    }
}
