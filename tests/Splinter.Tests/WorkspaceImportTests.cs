using System.Text.Json;
using Splinter.Application.Unity;
using Splinter.Cli;
using Splinter.Core;
using Splinter.Platform.Windows;
using Splinter.Protocol;

namespace Splinter.Tests;

public sealed partial class FileWorkspaceTests
{
    private sealed partial class Backend : IWorkspacePublicationTargetProvider
    {
        internal IOriginalFilePublisher? Publisher;
        public WorkspacePublicationTarget OpenPublicationTarget(WorkspaceRecord target) =>
            Mounted.Contains(target.Id) && Publisher is not null ? new(Publisher, new MemoryStream()) :
                throw new WorkspaceException("WorkspaceImportTargetUnavailable", "Fake destination is unavailable.");
    }

    public sealed class WorkspaceImportTests(ContractCatalogFixture contracts) : IClassFixture<ContractCatalogFixture>
    {
        [Fact]
        public async Task LiveUnityTargetRequiresItsOwnPermitAndImportsAssetMetadata()
        {
            RequireWindowsApply();
            var editor = new ObservedEditor();
            using var f = new Fixture(new WindowsOriginalFilePublisher(), application: editor,
                policies: new ApplicationProjectPolicies(new UnityProjectPolicy(editor)));
            Directory.CreateDirectory(Path.Combine(f.Project, "Assets"));
            var ct = TestContext.Current.CancellationToken;
            var a = (await f.Ensure("producer", ct, "unity")).Workspace;
            var b = (await f.Ensure("consumer", ct, "unity")).Workspace;
            File.WriteAllText(Path.Combine(a.Path, "Assets", "item.txt"), "asset");
            File.WriteAllText(Path.Combine(a.Path, "Assets", "item.txt.meta"), "new guid");
            var generation = (await f.New().CheckpointAsync(a.Id, "asset", "asset", ct)).ResultGeneration!;
            editor.Target = b.Path;
            Directory.CreateDirectory(Path.Combine(b.Path, "Temp"));
            File.WriteAllText(Path.Combine(b.Path, "Temp", "UnityLockfile"), "");
            var now = DateTimeOffset.UtcNow;
            var permit = new LiveOriginalEditorPermit(1, b.Path, 123, 456, generation.Id, "saved-b", now, now, now.AddMinutes(4), true);
            var review = new WorkspaceImportRequest(b.Id, a.Id, generation.Id, true, Paths: ["Assets/item.txt"]);
            Assert.Equal("WorkspaceImportEditorMismatch", (await Assert.ThrowsAsync<WorkspaceException>(() =>
                f.New().ImportAsync(review, ct: ct))).Code);
            Assert.Equal("WorkspaceImportEditorMismatch", (await Assert.ThrowsAsync<WorkspaceException>(() =>
                f.New().ImportAsync(review, permit with { ProcessId = 321 }, ct))).Code);
            Assert.Equal("LiveOriginalPermitInvalid", (await Assert.ThrowsAsync<WorkspaceException>(() =>
                f.New().ImportAsync(review, permit with { ProjectPath = a.Path }, ct))).Code);
            var plan = (await f.New().ImportAsync(review, permit, ct)).ImportPlan!;
            Assert.Equal(["Assets/item.txt", "Assets/item.txt.meta"], plan.EnumerateChanges().Select(change => change.Path));
            var apply = review with { Plan = false, IntentKey = "import-asset", ExpectedPlanDigest = plan.Digest };
            Assert.Equal("WorkspaceImportPlanChanged", (await Assert.ThrowsAsync<WorkspaceException>(() =>
                f.New().ImportAsync(apply, permit with { CooperationKey = "other-permit" }, ct))).Code);
            await f.New().ImportAsync(apply, permit, ct);
            Assert.True(editor.NativeChecks >= 3);
            Assert.Equal("asset", File.ReadAllText(Path.Combine(b.Path, "Assets", "item.txt")));
            Assert.Equal("new guid", File.ReadAllText(Path.Combine(b.Path, "Assets", "item.txt.meta")));
            Assert.False(File.Exists(Path.Combine(f.Project, "Assets", "item.txt")));
        }

        private sealed class ObservedEditor : IApplicationWorkspaceProvider, IOriginalEditorIdentityVerifier
        {
            internal string? Target;
            internal int NativeChecks;
            public string[] ManagedPaths => ["Library", "Temp"];
            public string CompatibilityKey(string project) => "fixture";
            public Task<ApplicationState> ObserveAsync(WorkspaceRecord w, string control, CancellationToken ct) =>
                Task.FromResult(w.Path == Target
                    ? new ApplicationState(true, true, "ready", new(123, 456, "owned", "Unity.exe"), new(true, true, 1, 0, 0))
                    : new ApplicationState(false, false, "stopped", Resources: new(true, false, 0, 0, 0)));
            public IDisposable Acquire(string project, int processId, long ticks)
            {
                Assert.Equal((Target, 123, 456L), (project, processId, ticks)); NativeChecks++;
                return new MemoryStream();
            }
            public Task<ApplicationLease> StartAsync(WorkspaceRecord w, string control, CancellationToken ct) => throw new NotSupportedException();
            public Task StopAsync(WorkspaceRecord w, string control, CancellationToken ct) => throw new NotSupportedException();
            public Task RemoveInfrastructureAsync(WorkspaceRecord w, string control, CancellationToken ct) => throw new NotSupportedException();
        }

        [Theory]
        [InlineData("none")]
        [InlineData("plastic")]
        public async Task SelectedAndFullImportsUseFixedBytesAndTheirOwnPerPathBaseline(string kind)
        {
            RequireWindowsApply();
            using var f = new Fixture(new WindowsOriginalFilePublisher(), kind);
            var ct = TestContext.Current.CancellationToken;
            var a = (await f.Ensure("producer", ct)).Workspace;
            var b = (await f.Ensure("consumer", ct)).Workspace;
            var c = (await f.Ensure("other", ct)).Workspace;
            string aData = Path.Combine(a.Path, "data.txt"), bData = Path.Combine(b.Path, "data.txt");
            File.WriteAllText(aData, "first");
            File.WriteAllText(Path.Combine(a.Path, "Library", "user.txt"), "new library file in ordinary application");
            Directory.CreateDirectory(Path.Combine(a.Path, "new-dir"));
            File.WriteAllText(Path.Combine(a.Path, "new-dir", "added.txt"), "additional");
            var first = (await f.New().CheckpointAsync(a.Id, "first", "one", ct)).ResultGeneration!;
            File.WriteAllText(aData, "second");
            File.Delete(Path.Combine(a.Path, "Library", "user.txt"));
            var second = (await f.New().CheckpointAsync(a.Id, "second", "two", ct)).ResultGeneration!;
            File.WriteAllText(Path.Combine(b.Path, "local.txt"), "B work");
            var select = new WorkspaceImportRequest(b.Id, a.Id, first.Id, true, Paths: ["data.txt", "Library"]);
            var planned = await f.New().ImportAsync(select, ct: ct);
            var plan = planned.ImportPlan!;
            Assert.Equal(0, plan.ConflictCount);
            Assert.Equal(2, plan.ChangeCount);
            Assert.Equal("basis", File.ReadAllText(bData));
            Assert.True(contracts.Product.Validate("urn:splinter:v1:response", JsonSerializer.SerializeToElement(
                WorkspaceCommands.Result("workspace.import", planned, false, f.Store.Root), ApiJson.Options)).IsValid);
            var apply = select with { Plan = false, IntentKey = "take-first", ExpectedPlanDigest = plan.Digest };
            var applied = await f.New().ImportAsync(apply, ct: ct);
            Assert.True(applied.ImportReceipt!.Completed);
            Assert.True(contracts.Product.Validate("urn:splinter:v1:response", JsonSerializer.SerializeToElement(
                WorkspaceCommands.Result("workspace.import", applied, false, f.Store.Root), ApiJson.Options)).IsValid);
            Assert.Equal("first", File.ReadAllText(bData));
            Assert.False(Directory.Exists(Path.Combine(b.Path, "new-dir")));
            Assert.Equal("second", File.ReadAllText(aData));
            Assert.Equal("basis", File.ReadAllText(Path.Combine(f.Project, "data.txt")));
            Assert.Equal("basis", File.ReadAllText(Path.Combine(c.Path, "data.txt")));

            var full = new WorkspaceImportRequest(b.Id, a.Id, second.Id, true);
            var next = (await f.New().ImportAsync(full, ct: ct)).ImportPlan!;
            Assert.Equal(plan.Digest, next.PreviousApplyDigest);
            Assert.Equal(0, next.ConflictCount);
            await f.New().ImportAsync(full with { Plan = false, IntentKey = "take-second", ExpectedPlanDigest = next.Digest }, ct: ct);
            Assert.Equal("second", File.ReadAllText(bData));
            Assert.Equal("additional", File.ReadAllText(Path.Combine(b.Path, "new-dir", "added.txt")));
            Assert.False(File.Exists(Path.Combine(b.Path, "Library", "user.txt")));
            Assert.Equal("B work", File.ReadAllText(Path.Combine(b.Path, "local.txt")));
            var local = (await f.New().CheckpointAsync(b.Id, "combined", "combined", ct)).ResultGeneration!;
            Assert.Equal(local.Id, f.Store.Get(b.Id).Results.Last().Id);
            if (kind == "none")
            {
                var original = (await f.New().PlanOriginalAsync(b.Id, local.Id, null, ct)).OriginalApplyPlan!;
                Assert.Contains(original.EnumerateChanges(), entry => entry.Path == "data.txt");
                Assert.Null(original.PreviousApplyDigest);
            }
            Assert.Equal("basis", File.ReadAllText(Path.Combine(f.Project, "data.txt")));
            File.WriteAllText(bData, "B continued");
            Assert.Equal(plan.Digest, (await f.New().ImportAsync(apply, ct: ct)).ImportReceipt!.Plan.Digest);
            Assert.Equal("B continued", File.ReadAllText(bData));
        }

        [Fact]
        public async Task ImportRequiresTheSameProjectAndAResultOwnedByTheNamedSource()
        {
            using var f = new Fixture(new FakePublisher());
            var ct = TestContext.Current.CancellationToken;
            var a = (await f.Ensure("producer", ct)).Workspace;
            var b = (await f.Ensure("consumer", ct)).Workspace;
            var result = (await f.New().CheckpointAsync(a.Id, "fixed", "one", ct)).ResultGeneration!;
            string other = Path.Combine(Path.GetDirectoryName(f.Project)!, "other");
            Directory.CreateDirectory(other);
            var foreign = (await f.New().EnsureAsync(other, "consumer", "snapshot", "project-snapshot", false,
                ct, sourceKind: "none", applicationKind: "none", generatedDirectoryId: new string('b', 24))).Workspace;
            var request = new WorkspaceImportRequest(b.Id, a.Id, result.Id, true);
            Assert.Equal("WorkspaceImportBasisMismatch", (await Assert.ThrowsAsync<WorkspaceException>(() =>
                f.New().ImportAsync(request with { Id = foreign.Id }, ct: ct))).Code);
            Assert.Equal("ResultNotReady", (await Assert.ThrowsAsync<WorkspaceException>(() =>
                f.New().ImportAsync(request with { From = b.Id, Id = a.Id }, ct: ct))).Code);
            Assert.Equal("OriginalApplyRequestInvalid", (await Assert.ThrowsAsync<WorkspaceException>(() =>
                f.New().ImportAsync(request with { Paths = ["../data.txt"] }, ct: ct))).Code);
            var schema = contracts.Product.Find("workspace.import")!.InputSchema;
            bool Valid(WorkspaceImportRequest input) => contracts.Product.Validate(schema, JsonSerializer.SerializeToElement(input, ApiJson.Options)).IsValid;
            Assert.True(Valid(request));
            Assert.False(Valid(request with { From = "bad" }));
            Assert.False(Valid(request with { Plan = false }));
            Assert.False(Valid(request with { IntentKey = "mixed" }));
            Assert.Empty(f.Store.Get(foreign.Id).Imports);
            Assert.Null(f.Store.Get(b.Id).PendingImport);
        }

        [Fact]
        public async Task ConflictsAndStaleReviewPreserveEveryTarget()
        {
            RequireWindowsApply();
            using var f = new Fixture(new WindowsOriginalFilePublisher());
            var ct = TestContext.Current.CancellationToken;
            var a = (await f.Ensure("producer", ct)).Workspace;
            var b = (await f.Ensure("consumer", ct)).Workspace;
            File.WriteAllText(Path.Combine(a.Path, "data.txt"), "producer");
            File.WriteAllText(Path.Combine(a.Path, "added.txt"), "producer addition");
            var result = (await f.New().CheckpointAsync(a.Id, "changed", "changed", ct)).ResultGeneration!;
            var request = new WorkspaceImportRequest(b.Id, a.Id, result.Id, true);
            var clean = (await f.New().ImportAsync(request, ct: ct)).ImportPlan!;
            File.WriteAllText(Path.Combine(b.Path, "data.txt"), "consumer");
            var apply = request with { Plan = false, IntentKey = "apply", ExpectedPlanDigest = clean.Digest };
            Assert.Equal("WorkspaceImportPlanChanged", (await Assert.ThrowsAsync<WorkspaceException>(() => f.New().ImportAsync(apply, ct: ct))).Code);
            var conflict = (await f.New().ImportAsync(request, ct: ct)).ImportPlan!;
            Assert.Contains("data.txt", Assert.Single(conflict.Conflicts));
            Assert.Equal("WorkspaceImportConflict", (await Assert.ThrowsAsync<WorkspaceException>(() =>
                f.New().ImportAsync(apply with { ExpectedPlanDigest = conflict.Digest }, ct: ct))).Code);
            Assert.False(File.Exists(Path.Combine(b.Path, "added.txt")));
            Assert.Equal("consumer", File.ReadAllText(Path.Combine(b.Path, "data.txt")));
            Assert.Null(f.Store.Get(b.Id).PendingImport);
            Assert.Equal("WorkspaceImportRequestInvalid", (await Assert.ThrowsAsync<WorkspaceException>(() =>
                f.New().ImportAsync(request with { Id = a.Id }, ct: ct))).Code);
            f.Backend.Mounted.Remove(b.Id);
            Assert.Equal("WorkspaceImportTargetUnavailable", (await Assert.ThrowsAsync<WorkspaceException>(() => f.New().ImportAsync(request, ct: ct))).Code);
        }

        [Theory]
        [InlineData("retired-before-receipt")]
        [InlineData("completed")]
        public async Task LostResponseResumesFixedImportAfterProducerRelease(string boundary)
        {
            RequireWindowsApply();
            bool lost = false;
            var publisher = new WindowsOriginalFilePublisher((point, _) =>
            {
                if (point == boundary && !lost) { lost = true; throw new WorkspaceException("ReplyLost", point); }
            });
            using var f = new Fixture(publisher);
            var ct = TestContext.Current.CancellationToken;
            var a = (await f.Ensure("producer", ct)).Workspace;
            var b = (await f.Ensure("consumer", ct)).Workspace;
            File.WriteAllText(Path.Combine(a.Path, "data.txt"), "fixed");
            var fixedResult = (await f.New().CheckpointAsync(a.Id, "fixed", "fixed", ct)).ResultGeneration!;
            var review = new WorkspaceImportRequest(b.Id, a.Id, fixedResult.Id, true, Paths: ["data.txt"]);
            var plan = (await f.New().ImportAsync(review, ct: ct)).ImportPlan!;
            var apply = review with { Plan = false, IntentKey = "dependency", ExpectedPlanDigest = plan.Digest };
            Assert.Equal("ReplyLost", (await Assert.ThrowsAsync<WorkspaceException>(() => f.New().ImportAsync(apply, ct: ct))).Code);
            if (boundary != "completed")
            {
                Assert.NotNull(f.Store.Get(b.Id).PendingImport);
                Assert.Equal("WorkspaceImportPending", (await Assert.ThrowsAsync<WorkspaceException>(() => f.New().ReleaseAsync(b.Id, ct))).Code);
                Assert.Equal("WorkspaceImportPending", (await Assert.ThrowsAsync<WorkspaceException>(() => f.New().CheckpointAsync(b.Id, "partial", "partial", ct))).Code);
                Assert.Equal("WorkspaceImportPending", (await Assert.ThrowsAsync<WorkspaceException>(() =>
                    f.New().ImportAsync(apply with { IntentKey = "another" }, ct: ct))).Code);
            }
            else File.WriteAllText(Path.Combine(b.Path, "data.txt"), "edited after completion");
            using var output = new StringWriter();
            Assert.Equal(0, await Cli.Program.RunAsync(["workspace", "read", "--id", b.Id, "--home", f.Store.Root,
                "--collection", "preserved-scratch", "--store-scope", "workspace-import", "--request-key", "dependency"], output, ct));
            using var page = JsonDocument.Parse(output.ToString());
            Assert.Equal(0, page.RootElement.GetProperty("data").GetProperty("page").GetProperty("count").GetInt64());
            await f.New().ReleaseAsync(a.Id, ct);
            var retry = await f.New().ImportAsync(apply, ct: ct);
            Assert.True(retry.ImportReceipt!.Completed);
            Assert.Single(retry.Workspace.Imports);
            Assert.Equal(boundary == "completed" ? "edited after completion" : "fixed", File.ReadAllText(Path.Combine(b.Path, "data.txt")));
        }
    }
}
