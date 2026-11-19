using Splinter.Core;
using Splinter.Platform.Windows;
using Splinter.Runtime;
using Splinter.SourceControl.Files;
using Splinter.SourceControl.Git;

namespace Splinter.Tests;

public sealed class GitOriginalIndexingPolicyTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task NativeGitPublicationPreservesTargetIndexingAndResumesTheSameFixedIntent(bool indexed, bool unity)
    {
        if (!OperatingSystem.IsWindows()) Assert.Skip("Native conditional Git original-file publication.");
        using var files = new TestDirectory(); var ct = TestContext.Current.CancellationToken;
        string project = Path.Combine(files.Path, "project"), home = Path.Combine(files.Path, "home"); Directory.CreateDirectory(project);
        await TestDirectory.Git(project, "init", "-b", "development"); await TestDirectory.Git(project, "config", "user.name", "Fixture");
        await TestDirectory.Git(project, "config", "user.email", "fixture@example.invalid"); await TestDirectory.Git(project, "config", "core.autocrlf", "false");
        string original = Path.Combine(project, "data.txt"); File.WriteAllText(original, "basis"); File.WriteAllText(Path.Combine(project, "other.txt"), "unrelated");
        await TestDirectory.Git(project, "add", "."); await TestDirectory.Git(project, "commit", "-m", "basis");
        string revision = await TestDirectory.Git(project, "rev-parse", "HEAD"); byte[] index = File.ReadAllBytes(Path.Combine(project, ".git", "index"));
        var pins = new WindowsJournalFilePins(); var store = new WorkspaceStore(home, pins); string id = new('a', 24), captureId = new('b', 24), control = store.ControlPath(id);
        var snapshots = new GitObjectSnapshotProvider(Path.Combine(control, "snapshot"), id, revision);
        var snapshot = await snapshots.CaptureAsync(new(project, id, Path.Combine(control, "snapshot")), ct);
        using var lease = snapshots.AcquireReadLease(); using var basis = snapshots.OpenReadScope(snapshot, await snapshots.ResolveReadAccessAsync(snapshot, ct), lease);
        var intent = new WorkspaceCaptureIntent(captureId, 1, "saved", "saved"); var identity = new SourceCaptureIdentity(captureId, 1, new string('c', 64));
        var w = new WorkspaceRecord { Id = id, RequestKey = "index-policy", RequestDigest = "index-policy-request", ProjectPath = project, Path = store.WorkPath(id),
            SourceReference = "splinter/" + id, BaseReference = revision, BaseRevision = revision, SourceKind = "git", ApplicationKind = unity ? "unity" : "none",
            StorageMode = "git-snapshot", ManagedPaths = unity ? ["Temp", "Library"] : [],
            Snapshot = new() { Reference = snapshot }, CaptureIntent = intent };
        using var captured = new AtomicSavedCapture(basis, identity);
        string editorLock = Path.Combine(project, "Temp", "UnityLockfile");
        bool interrupted = false;
        var publisher = new PublishedReplyLoss(new WindowsOriginalFilePublisher(allowGitAdministration: true), () =>
        {
            if (unity) Assert.Throws<IOException>(() => File.Open(editorLock, FileMode.Open, FileAccess.ReadWrite, FileShare.None).Dispose());
            interrupted = true;
        });
        var source = new FileWorkspaceProvider(store, publisher, contentPins: pins, applicationPolicies: Application.Unity.UnityProjectPolicy.Policies);
        var result = await source.PreserveGenerationAsync(w, control, intent, identity, captured, ct);
        w.Results.Add(result); w.ResultRevision = result.Revision; w.CaptureIntent = null;
        File.SetAttributes(original, indexed ? File.GetAttributes(original) | FileAttributes.NotContentIndexed : File.GetAttributes(original) & ~FileAttributes.NotContentIndexed);
        if (unity)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(editorLock)!);
            using var running = new FileStream(editorLock, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.DeleteOnClose);
            await Assert.ThrowsAsync<IOException>(() => source.PlanOriginalAsync(w, ct, ["data.txt"]));
        }
        var plan = await source.PlanOriginalAsync(w, ct, ["data.txt"]); Assert.Empty(plan.Conflicts);
        var change = Assert.Single(plan.EnumerateChanges());
        Assert.Equal(indexed, (change.Before!.Attributes & (int)FileAttributes.NotContentIndexed) != 0);
        Assert.Equal(indexed, (change.After!.Attributes & (int)FileAttributes.NotContentIndexed) != 0);
        Assert.Equal((int)File.GetAttributes(original), change.Expected!.Version.Attributes);
        Assert.Equal("ReplyLost", (await Assert.ThrowsAsync<WorkspaceException>(() => source.ApplyOriginalAsync(w, "same", plan.Digest, ct, ["data.txt"]))).Code);
        Assert.True(interrupted);
        await source.ApplyOriginalAsync(w, "same", plan.Digest, ct);
        Assert.True(w.OriginalApply!.Completed); Assert.Equal("result", File.ReadAllText(original));
        Assert.Equal(indexed, (File.GetAttributes(original) & FileAttributes.NotContentIndexed) != 0);
        File.WriteAllText(original, "later original"); File.SetAttributes(original, indexed ? File.GetAttributes(original) & ~FileAttributes.NotContentIndexed : File.GetAttributes(original) | FileAttributes.NotContentIndexed);
        Assert.True((await source.ApplyOriginalAsync(w, "same", plan.Digest, ct)).AlreadyIntegrated); Assert.Equal("later original", File.ReadAllText(original));
        Assert.Equal(index, File.ReadAllBytes(Path.Combine(project, ".git", "index"))); Assert.Equal(revision, await TestDirectory.Git(project, "rev-parse", "HEAD"));
        Assert.Equal("refs/heads/development", await TestDirectory.Git(project, "symbolic-ref", "HEAD"));
    }

    private sealed class PublishedReplyLoss(IOriginalFilePublisher native, Action interrupted) : IOriginalFilePublisher
    {
        private bool failed;
        public string RootIdentity(string project) => native.RootIdentity(project);
        public OriginalFileObservation? Observe(string project, string path, CancellationToken ct = default) => native.Observe(project, path, ct);
        public void Publish(OriginalApplyState state, Func<OriginalFileVersion, Stream> openPayload, Action persist, CancellationToken ct = default) =>
            native.Publish(state, openPayload, () =>
            {
                persist();
                if (!failed && state.StepAt(0).Phase == "published")
                { failed = true; interrupted(); throw new WorkspaceException("ReplyLost", "Lost native Git publication response."); }
            }, ct);
    }

    private sealed class AtomicSavedCapture(ISnapshotTree basis, SourceCaptureIdentity identity) : ISourceCaptureTree, ISourceCaptureRanges
    {
        private static readonly byte[] Bytes = "result"u8.ToArray();
        public SnapshotReference Snapshot => basis.Snapshot;
        public ISnapshotTree Basis => basis;
        public SourceCaptureIdentity CaptureIdentity => identity;
        public void EnsureAvailable() => basis.EnsureAvailable();
        public void VerifyReadBoundary() => basis.VerifyReadBoundary();
        public SnapshotEntry? Stat(string path)
        { var entry = basis.Stat(path); return entry is null || path != "data.txt" ? entry : entry with { Length = Bytes.Length, Attributes = (uint)FileAttributes.Normal }; }
        public IReadOnlyList<SnapshotEntry> List(string path) => EnumerateChildren(path).ToArray();
        public IEnumerable<SnapshotEntry> EnumerateChildren(string path) => basis.EnumerateChildren(path).Select(entry => Stat(entry.Path)!);
        public int Read(string path, long offset, Span<byte> output)
        {
            if (path != "data.txt") return basis.Read(path, offset, output);
            int count = (int)Math.Min(output.Length, Bytes.Length - offset); Bytes.AsSpan((int)offset, count).CopyTo(output); return count;
        }
        public CapturedFileRangeLayout? GetFileRanges(string path)
        {
            var entry = basis.Stat(path)!;
            return path == "data.txt" ? new(path, Math.Min(entry.Length, Bytes.Length), [new(0, Bytes.Length, new string('d', 64), BlockIdentity: new string('d', 64))],
                Guid.Parse("11111111-1111-1111-1111-111111111111")) : new(path, entry.Length, []);
        }
        public void Dispose() { }
    }
}
