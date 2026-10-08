using System.Text.Json;
using System.Text.Json.Nodes;
using Splinter.Cli;
using Splinter.Core;
using Splinter.Protocol;
using Splinter.Runtime;

namespace Splinter.Tests;

public sealed class StateWriteDiagnosticTests
{
    private readonly ContractCatalog contracts = new();

    [Theory]
    [InlineData("prepare-record", "not-attempted")]
    [InlineData("validate-target", "not-attempted")]
    [InlineData("open-temporary", "not-attempted")]
    [InlineData("write-temporary", "not-attempted")]
    [InlineData("flush-temporary", "not-attempted")]
    [InlineData("close-temporary", "not-attempted")]
    [InlineData("publish-state", "unknown")]
    [InlineData("append-event", "completed")]
    public void CommonDiagnosticContractDistinguishesPublicationFromCommandSuccess(string stage, string publication)
    {
        var details = new StateWriteFailureDetails(new string('a', 24), "save",
            new(stage, publication, "IOException", new IOException("unexposed source path").HResult));
        var valid = Response(details);
        Assert.True(contracts.Validate("urn:splinter:v1:response", valid).IsValid);
        var diagnostic = valid.GetProperty("error").GetProperty("details").GetProperty("diagnostic");
        Assert.False(diagnostic.TryGetProperty("nativeErrorCode", out _));
        Assert.DoesNotContain("unexposed", valid.GetRawText());
        var invalid = JsonNode.Parse(valid.GetRawText())!;
        invalid["error"]!["details"]!["diagnostic"]!["statePublication"] = publication == "completed" ? "unknown" : "completed";
        Assert.False(contracts.Validate("urn:splinter:v1:response", JsonSerializer.SerializeToElement(invalid)).IsValid);
    }

    [Fact]
    public void ResponseRejectsMissingDiagnosticsAndUnqualifiedNativeCodes()
    {
        var details = new StateWriteFailureDetails(new string('b', 24), "save", new("publish-state", "unknown", "IOException", unchecked((int)0x80070020), "win32", 32));
        var response = JsonNode.Parse(Response(details).GetRawText())!;
        Assert.True(contracts.Validate("urn:splinter:v1:response", JsonSerializer.SerializeToElement(response)).IsValid);
        var error = response["error"]!;
        var diagnostic = error["details"]!["diagnostic"]!.AsObject();
        diagnostic.Remove("nativeErrorDomain");
        Assert.False(contracts.Validate("urn:splinter:v1:response", JsonSerializer.SerializeToElement(response)).IsValid);
        diagnostic["nativeErrorDomain"] = "win32";
        diagnostic["hResult"] = new IOException().HResult;
        Assert.False(contracts.Validate("urn:splinter:v1:response", JsonSerializer.SerializeToElement(response)).IsValid);
        diagnostic["hResult"] = unchecked((int)0x80070020);
        diagnostic["path"] = "private-state-path";
        Assert.False(contracts.Validate("urn:splinter:v1:response", JsonSerializer.SerializeToElement(response)).IsValid);
        error.AsObject().Remove("details");
        Assert.False(contracts.Validate("urn:splinter:v1:response", JsonSerializer.SerializeToElement(response)).IsValid);
    }

    [Fact]
    public void BlockedReplacementKeepsOldStateFlushedTemporaryAndOtherWorkspace()
    {
        if (!OperatingSystem.IsWindows()) Assert.Skip("Windows file sharing or native contract.");
        using var dir = new TestDirectory(); var store = new WorkspaceStore(dir.Path);
        var record = Record(store, "replacement"); store.Save(record, "initial");
        var other = Record(store, "other"); store.Save(other, "initial");
        string control = store.ControlPath(record.Id), target = Path.Combine(control, "workspace.json");
        byte[] before = File.ReadAllBytes(target), events = File.ReadAllBytes(Path.Combine(control, "events.jsonl"));
        byte[] otherBefore = File.ReadAllBytes(Path.Combine(store.ControlPath(other.Id), "workspace.json"));
        record.ResultRevision = "next";
        string pending;
        byte[] pendingBytes;
        using (var held = new FileStream(target, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
            var error = Assert.Throws<WorkspaceException>(() => store.Save(record, "updated"));
            AssertDiagnostic(error, "publish-state", "unknown", 32);
            Assert.Equal(before, File.ReadAllBytes(target));
            Assert.Equal(events, File.ReadAllBytes(Path.Combine(control, "events.jsonl")));
            pending = Assert.Single(Directory.GetFiles(control, "*.tmp")); pendingBytes = File.ReadAllBytes(pending);
            Assert.Equal("next", ReadRecord(pending).ResultRevision);
        }
        store.Save(record, "updated");
        Assert.Equal("next", new WorkspaceStore(dir.Path).Get(record.Id).ResultRevision);
        Assert.Equal(pending, Assert.Single(Directory.GetFiles(control, "*.tmp")));
        Assert.Equal(pendingBytes, File.ReadAllBytes(pending));
        Assert.Equal(otherBefore, File.ReadAllBytes(Path.Combine(store.ControlPath(other.Id), "workspace.json")));
    }

    [Fact]
    public void ReadOnlyReplacementRetainsAttributesAndDiagnosticEvidence()
    {
        if (!OperatingSystem.IsWindows()) Assert.Skip("Windows file sharing or native contract.");
        using var dir = new TestDirectory(); var store = new WorkspaceStore(dir.Path);
        var record = Record(store, "read-only"); store.Save(record, "initial");
        string control = store.ControlPath(record.Id), target = Path.Combine(control, "workspace.json");
        byte[] before = File.ReadAllBytes(target);
        File.SetAttributes(target, File.GetAttributes(target) | FileAttributes.ReadOnly);
        record.ResultRevision = "next";
        var error = Assert.Throws<WorkspaceException>(() => store.Save(record, "updated"));
        AssertDiagnostic(error, "publish-state", "unknown", 5);
        Assert.True((File.GetAttributes(target) & FileAttributes.ReadOnly) != 0);
        Assert.Equal(before, File.ReadAllBytes(target));
        Assert.Equal("next", ReadRecord(Assert.Single(Directory.GetFiles(control, "*.tmp"))).ResultRevision);
        // Only TestDirectory teardown restores its own fixture's attributes.
    }

    [Fact]
    public void BlockedEventAppendReportsCompletedStatePublicationAndFailedCommand()
    {
        if (!OperatingSystem.IsWindows()) Assert.Skip("Windows file sharing or native contract.");
        using var dir = new TestDirectory(); var store = new WorkspaceStore(dir.Path);
        var record = Record(store, "events"); store.Save(record, "initial");
        string control = store.ControlPath(record.Id), journal = Path.Combine(control, "events.jsonl");
        byte[] before = File.ReadAllBytes(journal); record.ResultRevision = "next";
        using (var held = new FileStream(journal, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            var error = Assert.Throws<WorkspaceException>(() => store.Save(record, "updated"));
            AssertDiagnostic(error, "append-event", "completed", 32);
            Assert.Equal("next", store.Get(record.Id).ResultRevision);
            Assert.Empty(Directory.GetFiles(control, "*.tmp"));
        }
        Assert.Equal(before, File.ReadAllBytes(journal));
        store.Save(record, "updated");
        Assert.Single(File.ReadAllLines(journal).Select(line => JsonSerializer.Deserialize<JsonElement>(line)),
            entry => entry.GetProperty("action").GetString() == "updated");
    }

    [Fact]
    public void RecordDirectoryBlockerIsPreservedBeforePublicationIsAttempted()
    {
        using var dir = new TestDirectory(); var store = new WorkspaceStore(dir.Path);
        var record = Record(store, "blocked-control"); string control = store.ControlPath(record.Id);
        File.WriteAllText(control, "existing-blocker");
        var error = Assert.Throws<WorkspaceException>(() => store.Save(record, "initial"));
        AssertDiagnostic(error, "prepare-record", "not-attempted");
        Assert.Equal("existing-blocker", File.ReadAllText(control));
        Assert.False(Directory.Exists(store.WorkPath(record.Id)));
        Assert.Equal("StateRecoveryRequired", Assert.Throws<WorkspaceException>(() => store.Find(record.Id)).Code);
    }

    [Fact]
    public void UnexpectedDestinationDirectorySurvivesFailedFirstPublication()
    {
        using var dir = new TestDirectory(); var store = new WorkspaceStore(dir.Path);
        var record = Record(store, "directory-target"); string control = store.ControlPath(record.Id);
        string target = Path.Combine(control, "workspace.json"); Directory.CreateDirectory(target);
        string sentinel = Path.Combine(target, "preserved"); File.WriteAllText(sentinel, "existing-directory");
        var error = Assert.Throws<WorkspaceException>(() => store.Save(record, "initial"));
        AssertDiagnostic(error, "publish-state", "unknown");
        Assert.Equal("existing-directory", File.ReadAllText(sentinel));
        Assert.Equal(record.Id, ReadRecord(Assert.Single(Directory.GetFiles(control, "*.tmp"))).Id);
        Assert.False(File.Exists(Path.Combine(control, "events.jsonl")));
    }

    [Fact]
    public async Task CliReportsDiagnosticAndResumesSameOwnedGitFixture()
    {
        if (!OperatingSystem.IsWindows()) Assert.Skip("Windows file sharing or native contract.");
        using var dir = new TestDirectory(); var ct = TestContext.Current.CancellationToken;
        string project = Path.Combine(dir.Path, "project"), home = Path.Combine(dir.Path, "home"); Directory.CreateDirectory(project);
        await TestDirectory.Git(project, "init", "-b", "development");
        await TestDirectory.Git(project, "config", "user.name", "Splinter fixture");
        await TestDirectory.Git(project, "config", "user.email", "fixture@example.invalid");
        await File.WriteAllTextAsync(Path.Combine(project, "input.txt"), "original fixture", ct);
        await TestDirectory.Git(project, "add", "input.txt");
        await TestDirectory.Git(project, "-c", "commit.gpgsign=false", "commit", "-m", "fixture");
        string original = await TestDirectory.Git(project, "rev-parse", "HEAD");
        byte[] index = File.ReadAllBytes(Path.Combine(project, ".git", "index"));
        string[] ensure = ["workspace", "ensure", "--project", project, "--home", home, "--key", "diagnostic", "--base", "development", "--storage", "git-checkout"];
        using var initialOutput = new StringWriter();
        Assert.Equal(0, await Program.RunAsync(ensure, initialOutput, ct, development: true));
        var initial = JsonSerializer.Deserialize<JsonElement>(initialOutput.ToString());
        string id = initial.GetProperty("data").GetProperty("workspace").GetProperty("id").GetString()!;
        string control = Path.Combine(home, "records", id), statePath = Path.Combine(control, "workspace.json");
        byte[] before = File.ReadAllBytes(statePath);
        string registrations = await TestDirectory.Git(project, "worktree", "list", "--porcelain");
        string[] releaseArguments = ["workspace", "release", "--id", id, "--home", home];
        using var held = new FileStream(statePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var output = new StringWriter();
        Assert.Equal(2, await Program.RunAsync(releaseArguments, output, ct));
        var response = JsonSerializer.Deserialize<JsonElement>(output.ToString());
        Assert.True(contracts.Validate("urn:splinter:v1:response", response).IsValid, response.GetRawText());
        var details = response.GetProperty("error").GetProperty("details");
        Assert.Equal("release-intent", details.GetProperty("action").GetString());
        Assert.Equal("publish-state", details.GetProperty("diagnostic").GetProperty("stage").GetString());
        Assert.Equal(32, details.GetProperty("diagnostic").GetProperty("nativeErrorCode").GetInt32());
        Assert.Equal(before, File.ReadAllBytes(statePath));
        Assert.Equal(registrations, await TestDirectory.Git(project, "worktree", "list", "--porcelain"));
        Assert.Equal("requires-user-action", response.GetProperty("retryDisposition").GetString());
        Assert.True(response.GetProperty("nextActions")[0].GetProperty("requiresUserAction").GetBoolean());
        Assert.Equal(id, details.GetProperty("id").GetString());
        var retained = Directory.GetFiles(control, "*.tmp").ToDictionary(path => path, File.ReadAllBytes);
        Assert.Single(retained);
        Assert.All(retained.Keys, path => Assert.Equal(id, ReadRecord(path).Id));
        held.Dispose();
        using var retry = new StringWriter(); Assert.Equal(0, await Program.RunAsync(ensure, retry, ct, development: true));
        var ready = JsonSerializer.Deserialize<JsonElement>(retry.ToString());
        Assert.True(contracts.Validate("urn:splinter:v1:response", ready).IsValid);
        Assert.Equal(id, ready.GetProperty("data").GetProperty("workspace").GetProperty("id").GetString());
        Assert.Equal(original, await TestDirectory.Git(project, "rev-parse", "HEAD"));
        Assert.Equal(index, File.ReadAllBytes(Path.Combine(project, ".git", "index")));
        Assert.Equal("original fixture", await File.ReadAllTextAsync(Path.Combine(project, "input.txt"), ct));
        Assert.All(retained, item => Assert.Equal(item.Value, File.ReadAllBytes(item.Key)));
        using var release = new StringWriter();
        Assert.Equal(0, await Program.RunAsync(releaseArguments, release, ct));
        Assert.Equal("released", new WorkspaceStore(home).Get(id).Phase);
    }

    private void AssertDiagnostic(WorkspaceException error, string stage, string publication, int? nativeCode = null)
    {
        Assert.Equal("StateWriteIncomplete", error.Code);
        var details = Assert.IsType<StateWriteFailureDetails>(error.Details);
        Assert.Equal(stage, details.Diagnostic.Stage); Assert.Equal(publication, details.Diagnostic.StatePublication);
        Assert.NotEqual(0, details.Diagnostic.HResult);
        if (nativeCode is not null)
        {
            Assert.Equal("win32", details.Diagnostic.NativeErrorDomain); Assert.Equal(nativeCode, details.Diagnostic.NativeErrorCode);
            Assert.Equal(nativeCode, details.Diagnostic.HResult & 0xffff);
        }
        var action = Assert.Single(error.RecoveryActions!); Assert.Equal("workspace.status", action.Command); Assert.True(action.RequiresUserAction);
        Assert.True(contracts.Validate("urn:splinter:v1:response", Response(details)).IsValid);
    }
    private static JsonElement Response(object details) => JsonSerializer.SerializeToElement(
        ApiResponse.Failure("workspace.ensure", "StateWriteIncomplete", "Preserve this identity.", details), ApiJson.Options);
    private static WorkspaceRecord Record(WorkspaceStore store, string key) => new()
    { Id = WorkspaceStore.Key(key), RequestKey = key, ProjectPath = "source", Path = store.WorkPath(WorkspaceStore.Key(key)), SourceReference = "task", BaseReference = "development" };
    private static WorkspaceRecord ReadRecord(string path) => JsonSerializer.Deserialize<WorkspaceRecord>(File.ReadAllText(path), ApiJson.Options)!;
}
