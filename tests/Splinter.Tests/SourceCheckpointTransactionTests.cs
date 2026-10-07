using Splinter.Core;

namespace Splinter.Tests;

public sealed partial class SourceFingerprintTests
{
    [Theory]
    [InlineData("rewrite")]
    [InlineData("rename")]
    [InlineData("restart")]
    [InlineData("unknown")]
    [InlineData("config")]
    [InlineData("attributes")]
    public async Task StagingBarrierRetainsSourceAndMetadataChangeRejections(string change)
    {
        using var f = await Fixture.Create(); var ct = TestContext.Current.CancellationToken;
        File.WriteAllText(f.Path("code.cs"), "return 2;\n"); f.Tracker.Mark("code.cs");
        string initial = await TestDirectory.Git(f.Workspace.Path, "rev-parse", "HEAD");
        f.Tracker.OnObserve = count =>
        {
            if (change is "config" or "attributes")
            {
                if (count != 2) return;
                if (change == "config") TestDirectory.Git(f.Workspace.Path, "config", "core.abbrev", "11").GetAwaiter().GetResult();
                else
                {
                    string common = TestDirectory.Git(f.Workspace.Path, "rev-parse", "--git-common-dir").GetAwaiter().GetResult();
                    Directory.CreateDirectory(Path.Combine(common, "info"));
                    File.WriteAllText(Path.Combine(common, "info", "attributes"), "*.cs text eol=lf\n");
                }
                return;
            }
            if (count != 3) return;
            if (change == "rewrite") { File.WriteAllText(f.Path("code.cs"), "return 7;\n"); f.Tracker.Mark("code.cs"); File.WriteAllText(f.Path("code.cs"), "return 2;\n"); f.Tracker.Mark("code.cs"); }
            if (change == "rename") { File.Move(f.Path("code.cs"), f.Path("renamed.cs")); f.Tracker.Mark("code.cs"); f.Tracker.Mark("renamed.cs"); }
            if (change == "restart") f.Tracker.Epoch = "another-writer";
            if (change == "unknown") throw new WorkspaceException("SourceObservationUncertain", "No current barrier.");
        };
        var error = await Assert.ThrowsAsync<WorkspaceException>(() => f.Provider().CheckpointAsync(f.Workspace, "reject overlapping changes", ct));
        Assert.Equal(change == "unknown" ? "SourceObservationUncertain" : "SourceChanged", error.Code);
        Assert.Equal(initial, await TestDirectory.Git(f.Workspace.Path, "rev-parse", "HEAD"));
        Assert.True(File.Exists(f.Path(change == "rename" ? "renamed.cs" : "code.cs")));
    }

    [Fact]
    public async Task CommitHookCannotSubstituteTheValidatedStagedTree()
    {
        using var f = await Fixture.Create(); var ct = TestContext.Current.CancellationToken;
        File.WriteAllText(f.Path("code.cs"), "return 2;\n"); f.Tracker.Mark("code.cs");
        string hooks = Path.Combine(f.Directory.Path, "hooks"); Directory.CreateDirectory(hooks);
        await TestDirectory.Git(f.Workspace.Path, "config", "core.hooksPath", hooks);
        string hook = Path.Combine(hooks, "pre-commit");
        File.WriteAllText(hook, "#!/bin/sh\nprintf 'return 9;\\n' > code.cs\ngit add -- code.cs\n");
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(hook, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        Assert.Equal("SourceChanged", (await Assert.ThrowsAsync<WorkspaceException>(() => f.Provider().CheckpointAsync(f.Workspace, "hook changes", ct))).Code);
        Assert.Equal("return 9;", await TestDirectory.Git(f.Workspace.Path, "show", "HEAD:code.cs"));
        Assert.Equal("return 9;\n", File.ReadAllText(f.Path("code.cs")));
    }

    [Fact]
    public async Task TrackedDeletionKeepsStagedInspectionAndPreservesCreatedDeletedRenamedContent()
    {
        using var f = await Fixture.Create(); var ct = TestContext.Current.CancellationToken;
        File.Move(f.Path("code.cs"), f.Path("renamed.cs")); f.Tracker.Mark("code.cs"); f.Tracker.Mark("renamed.cs");
        File.WriteAllText(f.Path("new.txt"), "new\n"); f.Tracker.Mark("new.txt");
        File.Delete(f.Path("code.cs.meta")); f.Tracker.Mark("code.cs.meta");
        string revision = await f.Provider().CheckpointAsync(f.Workspace, "changed membership", ct);
        Assert.Equal("return 1;", await TestDirectory.Git(f.Workspace.Path, "show", revision + ":renamed.cs"));
        Assert.Equal("new", await TestDirectory.Git(f.Workspace.Path, "show", revision + ":new.txt"));
        string[] paths = (await TestDirectory.Git(f.Workspace.Path, "ls-tree", "-r", "--name-only", revision)).Split('\n');
        Assert.DoesNotContain("code.cs", paths); Assert.DoesNotContain("code.cs.meta", paths);
        Assert.Equal(3, File.ReadLines(Path.Combine(f.Directory.Path, "source-inspections.jsonl")).Count());
        Assert.False((await f.Provider().InspectAsync(f.Workspace, ct)).HasChanges);
    }
}
