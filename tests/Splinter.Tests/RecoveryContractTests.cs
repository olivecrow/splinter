using Splinter.Core;
using Splinter.Runtime;
using Splinter.Platform.Windows;

namespace Splinter.Tests;

public sealed class RecoveryContractTests
{
    [Theory]
    [InlineData("ProjectBasisMismatch")]
    [InlineData("GeneratedBasisChanged")]
    [InlineData("GeneratedRestoreConflict")]
    [InlineData("RestoreSealMismatch")]
    [InlineData("PlasticBasisInvalid")]
    [InlineData("GitBasisReadTimeout")]
    [InlineData("SourceCacheVersionUnsupported")]
    [InlineData("GitAttributeOutputInvalid")]
    public void BasisProtectionErrorsAgreeAcrossResponseAndDurableRecovery(string code)
    {
        Assert.True(WorkspaceFailurePolicy.RequiresPreservation(code));
        Assert.Equal("requires-user-action", Splinter.Protocol.ApiResponse.Failure("workspace.ensure", code, "preserve").RetryDisposition);
        var record = Record("aaaaaaaaaaaaaaaaaaaaaaaa", "unused"); record.StorageMode = "project-snapshot";
        record.Snapshot = new(); record.LastError = code + ": preserve the fixed basis";
        var recovery = SnapshotRecoveryDecision.For(record);
        Assert.Equal("preserve", recovery.Disposition); Assert.Equal(code, recovery.ReasonCode); Assert.Equal("workspace.status", recovery.NextCommand);
    }
    [Fact]
    public void InvalidPlasticRevisionInputRemainsARequestCorrection()
    {
        var error = Assert.Throws<WorkspaceException>(() => new Splinter.SourceControl.Files.PlasticRevisionClient().OpenRevision("1@repo@server", "moving-revision"));
        Assert.Equal("PlasticBasisRequestInvalid", error.Code);
        Assert.False(WorkspaceFailurePolicy.RequiresPreservation(error.Code));
        Assert.Equal("correct-request", Splinter.Protocol.ApiResponse.Failure("workspace.ensure", error.Code, error.Message).RetryDisposition);
    }
    [Theory]
    [InlineData("ready", true, true, true, null, false, "ready", "workspace.status")]
    [InlineData("ready", null, true, false, null, false, "observe", "workspace.status")]
    [InlineData("ready", false, true, false, "SnapshotMissing", false, "preserve", "workspace.status")]
    [InlineData("ready", false, true, false, "SnapshotMissing", true, "resume-release", "workspace.release")]
    [InlineData("captured", true, false, false, null, false, "resume-ensure", "workspace.ensure")]
    [InlineData("release-delta", true, true, false, null, true, "resume-release", "workspace.release")]
    [InlineData("release-delta", null, true, false, null, true, "observe", "workspace.status")]
    [InlineData("release-unmount", true, true, false, "DeltaCorrupt", true, "preserve", "workspace.status")]
    [InlineData("ready", null, true, false, "SnapshotIdentityMismatch", false, "preserve", "workspace.status")]
    [InlineData("ready", null, true, false, "SnapshotVerificationRequired", false, "resume-ensure", "workspace.ensure")]
    public void RecoveryDistinguishesObservationPreservationAndResumableOperations(string step, bool? available,
        bool attached, bool mounted, string? failure, bool independentResult, string disposition, string command)
    {
        var record = Record("aaaaaaaaaaaaaaaaaaaaaaaa", "unused");
        record.StorageMode = "ntfs-snapshot";
        record.Snapshot = new()
        {
            Step = step, Attached = attached,
            Reference = new("fixed-snapshot", "fake", "source", "fixed-root", DateTimeOffset.UtcNow, true, true, "fake"),
            Observation = new(available, mounted, mounted, true, 0, 0, 0, FailureCode: failure),
            Result = independentResult ? new("fixed-snapshot", "commit", "fingerprint", "result-ref", new(1, 100, "hash"), true, SnapshotResult.StableJournalProtocol) : null
        };
        var recovery = SnapshotRecoveryDecision.For(record);
        Assert.Equal(disposition, recovery.Disposition);
        Assert.Equal(command, recovery.NextCommand);
    }

    [Fact]
    public void DamagedOrLockedStateCannotBecomeANewWorkspace()
    {
        using var d = new TestDirectory(); var store = new WorkspaceStore(d.Path);
        string id = WorkspaceStore.Key("durable-state");
        Assert.Null(store.Find(id));
        string control = store.ControlPath(id);
        Directory.CreateDirectory(control);
        string temporary = Path.Combine(control, "workspace.json.interrupted.tmp");
        File.WriteAllText(temporary, "incomplete");
        Assert.Equal("StateRecoveryRequired", Assert.Throws<WorkspaceException>(() => new WorkspaceStore(d.Path).Find(id)).Code);
        string path = Path.Combine(control, "workspace.json");
        File.WriteAllText(path, "{\"version\":");
        Assert.Equal("StateCorrupt", Assert.Throws<WorkspaceException>(() => store.Find(id)).Code);
        Assert.Equal("{\"version\":", File.ReadAllText(path));
        store.Save(Record(id, store.WorkPath(id)), "fixture");
        using (var locked = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            Assert.Equal("StateReadUnavailable", Assert.Throws<WorkspaceException>(() => store.Find(id)).Code);
            Assert.Equal("StateWriteIncomplete", Assert.Throws<WorkspaceException>(() => store.Save(Record(id, store.WorkPath(id)), "blocked-write")).Code);
        }
        Assert.Equal(id, new WorkspaceStore(d.Path).Get(id).Id);
        Assert.Equal("incomplete", File.ReadAllText(temporary));
    }

    [Fact]
    public void SnapshotPathMismatchIsUnknownAndNeverReportedDeleted()
    {
        using var d = new TestDirectory();
        string root = Path.Combine(d.Path, "snapshot");
        var snapshot = new SnapshotReference("same-id", "fake", d.Path, root, DateTimeOffset.UtcNow, true, true, "fake");
        Directory.CreateDirectory(root);
        Assert.Equal("available", SnapshotAvailabilityProbe.Observe(snapshot).Availability);
        Directory.Delete(root);
        Assert.Equal("missing", SnapshotAvailabilityProbe.Observe(snapshot).Availability);
        File.WriteAllText(root, "identity mismatch");
        var mismatch = SnapshotAvailabilityProbe.Observe(snapshot);
        Assert.Null(mismatch.Available); Assert.Equal("unknown", mismatch.Availability);
        Assert.Equal("SnapshotIdentityMismatch", mismatch.FailureCode);
        Assert.Equal("identity mismatch", File.ReadAllText(root));
    }

    private static WorkspaceRecord Record(string id, string path) => new()
    { Id = id, RequestKey = "task", ProjectPath = "source", Path = path, SourceReference = "task", BaseReference = "development" };
}
