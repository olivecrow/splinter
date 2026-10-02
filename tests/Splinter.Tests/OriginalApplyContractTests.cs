using Splinter.Core;
using Splinter.Platform.Windows;
using Splinter.Protocol;
using Splinter.Cli;
using System.Text.Json;
using System.Security.AccessControl;
using System.IO.MemoryMappedFiles;

namespace Splinter.Tests;

public sealed partial class FileWorkspaceTests
{
    [Fact]
    public async Task ReadOnlyPlanAndNoOpUsePortablePortAndValidateResponseContracts()
    {
        var publisher = new FakePublisher(); using var f = new Fixture(publisher); var ct = TestContext.Current.CancellationToken;
        var w = (await f.Ensure("portable", ct)).Workspace;
        await f.New().CheckpointAsync(w.Id, "unchanged", ct);
        string record = Path.Combine(f.Store.ControlPath(w.Id), "workspace.json"); var saved = File.ReadAllBytes(record);
        var view = await f.New().PlanOriginalAsync(w.Id, ct); var plan = view.OriginalApplyPlan!;
        Assert.Empty(plan.Changes); Assert.Equal(saved, File.ReadAllBytes(record));
        var catalog = new ContractCatalog();
        var output = JsonSerializer.SerializeToElement(WorkspaceCommands.Result("workspace.integrate", view, false, f.Store.Root), ApiJson.Options);
        Assert.True(catalog.Validate("urn:splinter:v1:response", output).IsValid);
        var done = await f.New().ApplyOriginalAsync(w.Id, "same", plan.Digest, ct);
        Assert.Equal(1, publisher.Publications);
        await f.New().ApplyOriginalAsync(w.Id, "same", plan.Digest, ct); Assert.Equal(1, publisher.Publications);
        Assert.True(catalog.Validate("urn:splinter:v1:response", JsonSerializer.SerializeToElement(WorkspaceCommands.Result("workspace.integrate", done, false, f.Store.Root), ApiJson.Options)).IsValid);
    }

    [Fact]
    public async Task NativeNoOpKeepsOriginalFileIdentityAndAppliedResultCannotBeReplaced()
    {
        RequireWindowsApply(); var publisher = new WindowsOriginalFilePublisher(); using var f = new Fixture(publisher); var ct = TestContext.Current.CancellationToken;
        var w = (await f.Ensure("noop", ct)).Workspace;
        File.WriteAllText(Path.Combine(w.Path, "data.txt"), "same result"); await f.New().CheckpointAsync(w.Id, "result", ct);
        File.WriteAllText(Path.Combine(f.Project, "data.txt"), "same result");
        var original = publisher.Observe(f.Project, "data.txt", ct)!;
        var view = await f.New().PlanOriginalAsync(w.Id, ct); Assert.True(Assert.Single(view.OriginalApplyPlan!.Changes).AlreadyMatches);
        await f.New().ApplyOriginalAsync(w.Id, "apply", view.OriginalApplyPlan.Digest, ct);
        Assert.Equal(original.Identity, publisher.Observe(f.Project, "data.txt", ct)!.Identity);
        File.WriteAllText(Path.Combine(w.Path, "data.txt"), "later edit");
        Assert.Equal("OriginalApplyResultFixed", (await Assert.ThrowsAsync<WorkspaceException>(() => f.New().CheckpointAsync(w.Id, "later", ct))).Code);
        Assert.Equal("same result", File.ReadAllText(Path.Combine(f.Project, "data.txt")));
        Assert.True(f.Store.Get(w.Id).OriginalApply!.Completed);
    }

    [Fact]
    public async Task UnknownStageIdentityIsPreservedRatherThanAdopted()
    {
        RequireWindowsApply(); var publisher = new WindowsOriginalFilePublisher((at, _) => { if (at == "stage-created-before-receipt") throw new WorkspaceException("ReplyLost", "Unknown stage creation receipt."); });
        using var f = new Fixture(publisher); var ct = TestContext.Current.CancellationToken;
        var w = (await f.Ensure("unknown", ct)).Workspace;
        File.WriteAllText(Path.Combine(w.Path, "data.txt"), "result"); await f.New().CheckpointAsync(w.Id, "result", ct);
        var plan = (await f.New().PlanOriginalAsync(w.Id, ct)).OriginalApplyPlan!;
        Assert.Equal("ReplyLost", (await Assert.ThrowsAsync<WorkspaceException>(() => f.New().ApplyOriginalAsync(w.Id, "apply", plan.Digest, ct))).Code);
        Assert.Equal("OriginalApplyConflict", (await Assert.ThrowsAsync<WorkspaceException>(() => f.New().ApplyOriginalAsync(w.Id, "apply", plan.Digest, ct))).Code);
        Assert.Null(Assert.Single(f.Store.Get(w.Id).OriginalApply!.Steps).StagedIdentity);
        Assert.Single(Directory.GetFiles(f.Project, ".splinter-apply-*.new")); Assert.Equal("basis", File.ReadAllText(Path.Combine(f.Project, "data.txt")));
    }

    [Theory]
    [InlineData("res", true)]
    [InlineData("bad", false)]
    public async Task IncompleteStageRequiresExactPayloadPrefix(string prefix, bool resumable)
    {
        RequireWindowsApply(); bool stopped = false;
        var publisher = new WindowsOriginalFilePublisher((at, _) => { if (at == "stage-created" && !stopped) { stopped = true; throw new WorkspaceException("ReplyLost", "Incomplete stage."); } });
        using var f = new Fixture(publisher); var ct = TestContext.Current.CancellationToken;
        var w = (await f.Ensure("prefix", ct)).Workspace;
        File.WriteAllText(Path.Combine(w.Path, "data.txt"), "result"); await f.New().CheckpointAsync(w.Id, "result", ct);
        var plan = (await f.New().PlanOriginalAsync(w.Id, ct)).OriginalApplyPlan!;
        await Assert.ThrowsAsync<WorkspaceException>(() => f.New().ApplyOriginalAsync(w.Id, "apply", plan.Digest, ct));
        string stage = Assert.Single(Directory.GetFiles(f.Project, ".splinter-apply-*.new"));
        File.WriteAllText(stage, prefix);
        if (resumable)
        { await f.New().ApplyOriginalAsync(w.Id, "apply", plan.Digest, ct); Assert.Equal("result", File.ReadAllText(Path.Combine(f.Project, "data.txt"))); }
        else
        { Assert.Equal("OriginalApplyConflict", (await Assert.ThrowsAsync<WorkspaceException>(() => f.New().ApplyOriginalAsync(w.Id, "apply", plan.Digest, ct))).Code); Assert.Equal(prefix, File.ReadAllText(stage)); Assert.Equal("basis", File.ReadAllText(Path.Combine(f.Project, "data.txt"))); }
    }

    [Fact]
    public async Task AlternateStreamAndForeignDirectoryChildBlockBeforeOriginalWrites()
    {
        RequireWindowsApply(); using var f = new Fixture(new WindowsOriginalFilePublisher()); var ct = TestContext.Current.CancellationToken;
        var w = (await f.Ensure("unsupported", ct)).Workspace;
        File.WriteAllText(Path.Combine(w.Path, "data.txt"), "result"); File.Delete(Path.Combine(w.Path, "Library", "user.txt")); Directory.Delete(Path.Combine(w.Path, "Library"));
        await f.New().CheckpointAsync(w.Id, "result", ct);
        File.WriteAllText(Path.Combine(f.Project, "data.txt") + ":extra", "retain stream");
        Assert.Equal("OriginalApplyUnsupported", (await Assert.ThrowsAsync<WorkspaceException>(() => f.New().PlanOriginalAsync(w.Id, ct))).Code);
        File.Delete(Path.Combine(f.Project, "data.txt") + ":extra");
        File.WriteAllText(Path.Combine(f.Project, "Library", "foreign.txt"), "keep");
        var plan = (await f.New().PlanOriginalAsync(w.Id, ct)).OriginalApplyPlan!; Assert.NotEmpty(plan.Conflicts);
        Assert.Equal("OriginalApplyConflict", (await Assert.ThrowsAsync<WorkspaceException>(() => f.New().ApplyOriginalAsync(w.Id, "apply", plan.Digest, ct))).Code);
        Assert.Equal("basis", File.ReadAllText(Path.Combine(f.Project, "data.txt"))); Assert.Null(f.Store.Get(w.Id).OriginalApply);
    }

    [Fact]
    public async Task NativePublicationPreservesProtectedOriginalOwnerGroupAndDacl()
    {
        if (!OperatingSystem.IsWindows()) { Assert.Skip("Windows security descriptor preservation."); return; }
        using var f = new Fixture(new WindowsOriginalFilePublisher()); var ct = TestContext.Current.CancellationToken;
        var file = new FileInfo(Path.Combine(f.Project, "data.txt"));
        const AccessControlSections sections = AccessControlSections.Owner | AccessControlSections.Group | AccessControlSections.Access;
        var security = file.GetAccessControl(sections); security.SetAccessRuleProtection(true, true); file.SetAccessControl(security);
        string original = file.GetAccessControl(sections).GetSecurityDescriptorSddlForm(sections);
        var w = (await f.Ensure("permissions", ct)).Workspace;
        File.WriteAllText(Path.Combine(w.Path, "data.txt"), "result"); await f.New().CheckpointAsync(w.Id, "result", ct);
        var plan = (await f.New().PlanOriginalAsync(w.Id, ct)).OriginalApplyPlan!;
        await f.New().ApplyOriginalAsync(w.Id, "apply", plan.Digest, ct);
        var expected = new RawSecurityDescriptor(original);
        var actual = new RawSecurityDescriptor(file.GetAccessControl(sections).GetSecurityDescriptorSddlForm(sections));
        Assert.Equal(expected.Owner, actual.Owner); Assert.Equal(expected.Group, actual.Group);
        Assert.Equal(expected.ControlFlags & ControlFlags.DiscretionaryAclProtected, actual.ControlFlags & ControlFlags.DiscretionaryAclProtected);
        byte[] beforeAcl = new byte[expected.DiscretionaryAcl!.BinaryLength], afterAcl = new byte[actual.DiscretionaryAcl!.BinaryLength];
        expected.DiscretionaryAcl.GetBinaryForm(beforeAcl, 0); actual.DiscretionaryAcl.GetBinaryForm(afterAcl, 0);
        Assert.Equal(beforeAcl, afterAcl);
    }

    [Fact]
    public async Task AuthorityRejectsPendingApplyBeforeAnySnapshotReleaseAuthorization()
    {
        RequireWindowsApply(); var publisher = new WindowsOriginalFilePublisher((at, _) => { if (at == "staged") throw new WorkspaceException("ReplyLost", "Pending publication."); });
        using var f = new Fixture(publisher); var ct = TestContext.Current.CancellationToken;
        var w = (await f.Ensure("authority-pending", ct)).Workspace;
        File.WriteAllText(Path.Combine(w.Path, "data.txt"), "result"); await f.New().CheckpointAsync(w.Id, "result", ct);
        var plan = (await f.New().PlanOriginalAsync(w.Id, ct)).OriginalApplyPlan!;
        await Assert.ThrowsAsync<WorkspaceException>(() => f.New().ApplyOriginalAsync(w.Id, "apply", plan.Digest, ct));
        Directory.CreateDirectory(Path.Combine(f.Store.ControlPath(w.Id), "snapshot"));
        StorageGrant grant;
        using (var paths = new StorageAuthorityPaths()) grant = new("test-sid", f.Project, f.Store.Root,
            StorageAuthorityPaths.Volume(f.Project), paths.Pin(f.Project), paths.Pin(f.Store.Root));
        var request = new StorageAuthorityRequest(1, Guid.NewGuid().ToString("N"), "Release", w.Id, f.Project, f.Store.Root, w.Snapshot!.Reference!.Id,
            new("request", w.Id, w.Snapshot.Reference.Id, DateTimeOffset.UtcNow, null), null);
        Assert.Equal("OriginalApplyPending", Assert.Throws<WorkspaceException>(() => StorageAuthorityPaths.Authorize(grant, request)).Code);
        Assert.Empty(f.Backend.Released);
    }

    [Fact]
    public async Task ExistingWritableMappingCannotRaceConditionalOriginalPublication()
    {
        RequireWindowsApply(); IntPtr view = IntPtr.Zero; bool reached = false;
        var publisher = new WindowsOriginalFilePublisher((at, _) =>
        {
            if (at == "original-checked") { System.Runtime.InteropServices.Marshal.Copy("other"u8.ToArray(), 0, view, 5); Assert.True(FlushViewOfFile(view, 5)); reached = true; }
        });
        using var f = new Fixture(publisher); var ct = TestContext.Current.CancellationToken;
        var w = (await f.Ensure("mapped", ct)).Workspace;
        File.WriteAllText(Path.Combine(w.Path, "data.txt"), "result"); await f.New().CheckpointAsync(w.Id, "result", ct);
        var plan = (await f.New().PlanOriginalAsync(w.Id, ct)).OriginalApplyPlan!;
        IntPtr map;
        using (var file = new FileStream(Path.Combine(f.Project, "data.txt"), FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete))
            map = CreateFileMappingW(file.SafeFileHandle, IntPtr.Zero, 4, 0, 0, null);
        Assert.NotEqual(IntPtr.Zero, map);
        try
        {
            view = MapViewOfFile(map, 2, 0, 0, 0); Assert.NotEqual(IntPtr.Zero, view);
            var error = await Assert.ThrowsAsync<WorkspaceException>(() => f.New().ApplyOriginalAsync(w.Id, "apply", plan.Digest, ct));
            Assert.Equal("OriginalApplyNativeFailure", error.Code); Assert.False(reached);
        }
        finally { if (view != IntPtr.Zero) Assert.True(UnmapViewOfFile(view)); Assert.True(CloseHandle(map)); }
        Assert.Equal(reached ? "other" : "basis", File.ReadAllText(Path.Combine(f.Project, "data.txt")));
    }
    [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)] private static extern IntPtr CreateFileMappingW(Microsoft.Win32.SafeHandles.SafeFileHandle h, IntPtr security, uint protection, uint high, uint low, string? name);
    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr MapViewOfFile(IntPtr mapping, uint access, uint high, uint low, nuint size);
    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)] [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)] private static extern bool FlushViewOfFile(IntPtr address, nuint size);
    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)] [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)] private static extern bool UnmapViewOfFile(IntPtr address);
    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)] [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)] private static extern bool CloseHandle(IntPtr handle);

    [Fact]
    public async Task RetiredFileHeldByReaderKeepsIntentPendingUntilExactAbsence()
    {
        RequireWindowsApply(); using var f = new Fixture(new WindowsOriginalFilePublisher()); var ct = TestContext.Current.CancellationToken;
        var w = (await f.Ensure("reader", ct)).Workspace;
        File.WriteAllText(Path.Combine(w.Path, "data.txt"), "result"); await f.New().CheckpointAsync(w.Id, "result", ct);
        var plan = (await f.New().PlanOriginalAsync(w.Id, ct)).OriginalApplyPlan!;
        using (var reader = new FileStream(Path.Combine(f.Project, "data.txt"), FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
        {
            Assert.Equal("OriginalApplyPending", (await Assert.ThrowsAsync<WorkspaceException>(() => f.New().ApplyOriginalAsync(w.Id, "apply", plan.Digest, ct))).Code);
            Assert.False(f.Store.Get(w.Id).OriginalApply!.Completed);
            Assert.Equal("OriginalApplyPending", (await Assert.ThrowsAsync<WorkspaceException>(() => f.New().ReleaseAsync(w.Id, ct))).Code);
            Assert.Equal((byte)'b', reader.ReadByte());
        }
        Assert.True((await f.New().ApplyOriginalAsync(w.Id, "apply", plan.Digest, ct)).Workspace.OriginalApply!.Completed);
        Assert.Empty(Directory.GetFiles(f.Project, ".splinter-apply-*"));
    }

    [Fact]
    public async Task PendingPublicationCannotBeCapturedByNewTaskWhileExistingPeerContinues()
    {
        RequireWindowsApply(); bool stopped = false;
        var publisher = new WindowsOriginalFilePublisher((at, _) => { if (at == "staged" && !stopped) { stopped = true; throw new WorkspaceException("ReplyLost", "Retain stage."); } });
        using var f = new Fixture(publisher); var ct = TestContext.Current.CancellationToken;
        var a = (await f.Ensure("a", ct)).Workspace; var b = (await f.Ensure("b", ct)).Workspace;
        File.WriteAllText(Path.Combine(a.Path, "data.txt"), "result"); await f.New().CheckpointAsync(a.Id, "result", ct);
        var plan = (await f.New().PlanOriginalAsync(a.Id, ct)).OriginalApplyPlan!;
        await Assert.ThrowsAsync<WorkspaceException>(() => f.New().ApplyOriginalAsync(a.Id, "apply", plan.Digest, ct));
        Assert.Equal("OriginalApplyResourcesPresent", (await Assert.ThrowsAsync<WorkspaceException>(() => f.Ensure("new-task", ct))).Code);
        Assert.Equal(2, f.Backend.Captures);
        File.WriteAllText(Path.Combine(b.Path, "data.txt"), "peer continues");
        Assert.Equal("peer continues", File.ReadAllText(Path.Combine(b.Path, "data.txt")));
        await f.New().ApplyOriginalAsync(a.Id, "apply", plan.Digest, ct);
        Assert.NotNull((await f.Ensure("new-task", ct)).Workspace.BaseRevision); Assert.Equal(3, f.Backend.Captures);
    }

    [Theory]
    [InlineData("future-field")]
    [InlineData("missing-step-phase")]
    [InlineData("invalid-path")]
    [InlineData("missing-intent")]
    public async Task CorruptApplyRecordCannotBeReclaimedOrSilentlyRewritten(string damage)
    {
        var publisher = new FakePublisher(); using var f = new Fixture(publisher); var ct = TestContext.Current.CancellationToken;
        var w = (await f.Ensure("corrupt", ct)).Workspace;
        File.WriteAllText(Path.Combine(w.Path, "data.txt"), "result"); await f.New().CheckpointAsync(w.Id, "result", ct);
        var plan = (await f.New().PlanOriginalAsync(w.Id, ct)).OriginalApplyPlan!;
        await f.New().ApplyOriginalAsync(w.Id, "apply", plan.Digest, ct);
        string record = Path.Combine(f.Store.ControlPath(w.Id), "workspace.json"); var json = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(record))!;
        var apply = json["originalApply"]!;
        if (damage == "future-field") apply["future"] = 1;
        if (damage == "missing-step-phase") apply["steps"]![0]!.AsObject().Remove("phase");
        if (damage == "invalid-path") apply["plan"]!["changes"]![0]!["path"] = "../outside";
        if (damage == "missing-intent") json.AsObject().Remove("originalApply");
        File.WriteAllText(record, json.ToJsonString()); var corrupt = File.ReadAllBytes(record);
        Assert.Throws<WorkspaceException>(() => f.Store.Get(w.Id));
        await Assert.ThrowsAsync<WorkspaceException>(() => f.New().ReleaseAsync(w.Id, ct));
        Assert.Equal(corrupt, File.ReadAllBytes(record)); Assert.Empty(f.Backend.Released);
    }

    [Theory]
    [InlineData("--target", "development", "--plan")]
    [InlineData("--target", "original", "--intent-key", "key")]
    [InlineData("--target", "original", "--plan", "--intent-key", "key")]
    public async Task InvalidApplyCliOptionsFailBeforeCreatingHome(params string[] extra)
    {
        using var directory = new TestDirectory(); string home = Path.Combine(directory.Path, "untouched"); using var output = new StringWriter();
        Assert.Equal(2, await Splinter.Cli.Program.RunAsync(["workspace", "integrate", "--id", new string('a', 24), "--home", home, .. extra], output, TestContext.Current.CancellationToken));
        Assert.False(Directory.Exists(home));
    }

    // Protocol/coordinator fake only; it makes no native publication claim.
    private sealed class FakePublisher : IOriginalFilePublisher
    {
        public int Publications;
        public string RootIdentity(string project) => "fake-original";
        public OriginalFileObservation? Observe(string project, string relativePath, CancellationToken ct = default)
        {
            string path = Path.Combine(project, relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!Path.Exists(path)) return null;
            var info = new FileInfo(path); bool directory = (info.Attributes & FileAttributes.Directory) != 0;
            using var stream = directory ? null : File.OpenRead(path);
            string? hash = directory ? null : Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(stream!));
            return new("fake-" + relativePath, new(directory ? "directory" : "file", directory ? 0 : info.Length, hash, (int)info.Attributes, info.LastWriteTimeUtc.Ticks));
        }
        public void Publish(OriginalApplyState state, Func<OriginalFileVersion, Stream> payload, Action persist, CancellationToken ct = default)
        {
            Publications++;
            for (int i = 0; i < state.Steps.Length; i++)
            {
                var step = state.Steps[i]; step.Phase = "done";
                if (!state.Plan.Changes[i].AlreadyMatches && state.Plan.Changes[i].After is { } result)
                { step.StagedIdentity = "fake-publication"; step.StagedBytes = result.Length; }
            }
            state.Completed = true; persist();
        }
    }
}
