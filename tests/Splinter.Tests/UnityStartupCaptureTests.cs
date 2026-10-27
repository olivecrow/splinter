using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Splinter.Development.UnityStartup;

namespace Splinter.Tests;

public sealed class UnityStartupCaptureTests
{
    [Fact]
    public void BeforeStartCapturesBoundedInputsWithoutChangingAnyOwnedFile()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var f = new Fixture();
        var before = f.Inventory();
        using JsonDocument result = JsonDocument.Parse(f.Run());
        JsonElement root = result.RootElement;
        Assert.True(root.GetProperty("complete").GetBoolean());
        Assert.Equal("before-start", root.GetProperty("stage").GetString());
        Assert.Equal(f.Root, root.GetProperty("identity").GetProperty("path").GetString());
        Assert.True(root.GetProperty("captureSeconds").GetDouble() >= 0);
        Assert.Contains(root.GetProperty("artifacts").EnumerateArray(), x => x.GetProperty("name").GetString()!.EndsWith("compile.rsp"));
        Assert.DoesNotContain(root.GetProperty("artifacts").EnumerateArray(), x => x.GetProperty("name").GetString()!.EndsWith("assembly.dll"));
        foreach (JsonElement entry in root.GetProperty("artifacts").EnumerateArray())
        {
            string payload = Path.Combine(f.Output, "payload", entry.GetProperty("name").GetString()!);
            Assert.Equal(entry.GetProperty("sha256").GetString(), Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(payload))));
        }
        Assert.Equal(before, f.Inventory());
        Assert.DoesNotContain("provider-secret", File.ReadAllText(Path.Combine(f.Output, "provider-before.json")));
        Assert.True(File.Exists(Path.Combine(f.Output, "capture.json")));
    }

    [Fact]
    public void LiveCaptureVerifiesExactEditorAndKeepsOrderedNonAdditiveLogScopes()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var f = new Fixture(); f.Live();
        File.WriteAllText(Path.Combine(f.Control, "editor.log"), """
            [1/20  1s] Csc Library/one.dll
            [2/20  1s] ILPostProcess Library/one.dll
            *** Tundra build success (2.1 seconds), 2 items updated, 20 evaluated
            Domain Reload Profiling: 300ms
                LoadAssemblies (100ms)
                ProcessInitializeOnLoadAttributes (200ms)
            [3/20  1s] Csc Library/two.dll
            """);
        var before = f.Inventory();
        f.Run();
        using JsonDocument analysis = JsonDocument.Parse(File.ReadAllText(Path.Combine(f.Output, "analysis.json")));
        Assert.False(analysis.RootElement.GetProperty("additive").GetBoolean());
        Assert.Equal(1, analysis.RootElement.GetProperty("firstBuildActions").GetProperty("Csc").GetInt32());
        Assert.Equal(4, analysis.RootElement.GetProperty("observations").GetArrayLength());
        Assert.Equal(before, f.Inventory());
        Assert.DoesNotContain("editor-secret", File.ReadAllText(Path.Combine(f.Output, "capture.json")));
    }

    [Theory]
    [InlineData("process")]
    [InlineData("project")]
    [InlineData("token")]
    [InlineData("stale")]
    public void ForeignOrStaleEditorNeverCreatesEvidence(string mismatch)
    {
        if (!OperatingSystem.IsWindows()) return;
        using var f = new Fixture(); f.Live(mismatch);
        Assert.Throws<IOException>(() => f.Run(lease => mismatch != "process" || lease.Pid != 22));
        Assert.False(Directory.Exists(f.Output));
    }

    [Theory]
    [InlineData("released")]
    [InlineData("provider")]
    [InlineData("mount")]
    [InlineData("missing")]
    public void ReleasedMissingOrUnownedWorkspaceIsNotAValidCapture(string failure)
    {
        if (!OperatingSystem.IsWindows()) return;
        using var f = new Fixture();
        if (failure == "released") f.WriteWorkspace("released");
        if (failure == "missing") Directory.Delete(f.Root, true);
        Assert.Throws<IOException>(() => f.Run(_ => failure != "provider", _ => failure != "mount"));
        Assert.False(Directory.Exists(f.Output));
    }

    [Fact]
    public void BeforeStartRefusesPriorOrUncertainLaunch()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var f = new Fixture();
        File.WriteAllText(Path.Combine(f.Control, "launch-intent.json"), "{}");
        Assert.Throws<IOException>(() => f.Run());
        Assert.False(Directory.Exists(f.Output));
    }

    [Theory]
    [InlineData("request")]
    [InlineData("state")]
    [InlineData("provider-launch-receipt")]
    public void ProviderReceiptsMustAgreeOnTheSameOwnerAndToken(string file)
    {
        if (!OperatingSystem.IsWindows()) return;
        using var f = new Fixture();
        string path = Path.Combine(f.Control, "overlay", file + ".json");
        File.WriteAllText(path, File.ReadAllText(path).Replace("provider-secret", "foreign-secret", StringComparison.Ordinal));
        Assert.Throws<IOException>(() => f.Run());
        Assert.False(Directory.Exists(f.Output));
    }

    [Theory]
    [InlineData("file-bytes")]
    [InlineData("total-bytes")]
    [InlineData("files")]
    public void LimitFailurePreservesPartialEvidenceWithoutPublishingCompletion(string limit)
    {
        if (!OperatingSystem.IsWindows()) return;
        using var f = new Fixture();
        if (limit == "file-bytes") f.Options.MaxFileBytes = 1;
        if (limit == "total-bytes") f.Options.MaxTotalBytes = 1;
        if (limit == "files") f.Options.MaxFiles = 1;
        var before = f.Inventory();
        Assert.Throws<IOException>(() => f.Run());
        Assert.True(File.Exists(Path.Combine(f.Output, "failure.json")));
        Assert.False(File.Exists(Path.Combine(f.Output, "capture.json")));
        Assert.Equal(before, f.Inventory());
        long payloadBytes = Directory.EnumerateFiles(Path.Combine(f.Output, "payload"), "*", SearchOption.AllDirectories).Sum(p => new FileInfo(p).Length);
        Assert.True(payloadBytes <= f.Options.MaxTotalBytes);
    }

    [Fact]
    public void ReparseDescendantIsRefusedWithoutFollowingOrChangingTarget()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var f = new Fixture();
        string foreign = Path.Combine(f.Directory.Path, "foreign"); Directory.CreateDirectory(foreign);
        string payload = Path.Combine(foreign, "foreign.rsp"); File.WriteAllText(payload, "sentinel");
        string link = Path.Combine(f.Root, "Library", "Bee", "linked");
        // Junction creation is an ordinary-user operation; no Developer Mode/symlink privilege needed.
        var start = new ProcessStartInfo("pwsh") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        string script = Path.Combine(f.Directory.Path, "link.ps1");
        File.WriteAllText(script, "param($Link,$Target) $ErrorActionPreference='Stop'; New-Item -ItemType Junction -Path $Link -Target $Target | Out-Null");
        foreach (string arg in new[] { "-NoProfile", "-File", script, link, foreign }) start.ArgumentList.Add(arg);
        using Process process = Process.Start(start)!;
        string error = process.StandardError.ReadToEnd(); process.WaitForExit(); Assert.True(process.ExitCode == 0, error);
        try
        {
            Assert.Throws<IOException>(() => f.Run());
            Assert.Equal("sentinel", File.ReadAllText(payload));
            Assert.False(File.Exists(Path.Combine(f.Output, "capture.json")));
            Assert.False(File.Exists(Path.Combine(f.Output, "payload", "workspace", "Library", "Bee", "linked", "foreign.rsp")));
        }
        finally { Directory.Delete(link); }
    }

    [Fact]
    public void MatchedBeforeAndLiveCaptureBindClockAndProviderCounterIntervals()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var f = new Fixture();
        f.Run();
        f.Live();
        f.Options.BeforeCapture = f.Output;
        f.Options.OutputPath = Path.Combine(f.Directory.Path, "live-evidence");
        f.Run();
        using JsonDocument interval = JsonDocument.Parse(File.ReadAllText(Path.Combine(f.Options.OutputPath, "interval.json")));
        Assert.True(interval.RootElement.GetProperty("intervalSeconds").GetDouble() >= 0);
        Assert.True(interval.RootElement.GetProperty("sameProviderEpoch").GetBoolean());
        Assert.Equal(0, interval.RootElement.GetProperty("providerCounterDeltas").GetProperty("flushCount").GetInt64());
        Assert.Contains("Caller interval", interval.RootElement.GetProperty("boundary").GetString());
        f.Options.BeforeCapture = f.Options.OutputPath;
        f.Options.OutputPath = Path.Combine(f.Directory.Path, "mismatched-evidence");
        Assert.Throws<IOException>(() => f.Run());
        Assert.False(File.Exists(Path.Combine(f.Options.OutputPath, "capture.json")));
    }

    [Fact]
    public void EvidenceCannotOverwriteOrOverlapProjectAndOwnershipChangesPreventCompletion()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var f = new Fixture();
        f.Options.OutputPath = Path.Combine(f.Root, "capture");
        Assert.Throws<IOException>(() => f.Run());
        f.Options.OutputPath = f.Output;
        Directory.CreateDirectory(f.Output); File.WriteAllText(Path.Combine(f.Output, "keep"), "sentinel");
        Assert.Throws<IOException>(() => f.Run());
        Assert.Equal("sentinel", File.ReadAllText(Path.Combine(f.Output, "keep")));
        Directory.Delete(f.Output, true);
        int observations = 0;
        Assert.Throws<IOException>(() => f.Run(_ => ++observations == 1));
        Assert.False(File.Exists(Path.Combine(f.Output, "capture.json")));
        Assert.True(File.Exists(Path.Combine(f.Output, "failure.json")));
    }

    [Fact]
    public async Task PowerShellEntryPointCompilesHelperAndRejectsMissingControlWithoutSourceWrites()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var d = new TestDirectory();
        var start = new ProcessStartInfo("pwsh") { UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (string arg in new[] { "-NoProfile", "-NonInteractive", "-File", Path.Combine(AppContext.BaseDirectory, "Scripts", "capture-unity-startup.ps1"),
            "-Stage", "before-start", "-ControlPath", Path.Combine(d.Path, "missing"), "-OutputPath", Path.Combine(d.Path, "output") }) start.ArgumentList.Add(arg);
        using Process process = Process.Start(start)!;
        Task<string> error = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        Task<string> output = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken); timeout.CancelAfter(TimeSpan.FromSeconds(45));
        try { await process.WaitForExitAsync(timeout.Token); }
        finally { if (!process.HasExited) { process.Kill(true); await process.WaitForExitAsync(CancellationToken.None); } }
        string stderr = await error; await output;
        Assert.NotEqual(0, process.ExitCode);
        Assert.DoesNotContain("CompilerErrors", stderr);
        Assert.DoesNotContain("Add-Type", stderr);
        Assert.False(Directory.Exists(Path.Combine(d.Path, "output")));
    }

    private sealed class Fixture : IDisposable
    {
        internal TestDirectory Directory { get; } = new();
        internal string Root { get; }
        internal string Control { get; }
        internal string Output { get; }
        internal StartupCapture.Options Options { get; }
        private const string Id = "aaaaaaaaaaaaaaaaaaaaaaaa";
        internal Fixture()
        {
            Root = Path.Combine(Directory.Path, "workspace"); Control = Path.Combine(Directory.Path, "records", Id); Output = Path.Combine(Directory.Path, "evidence");
            Put(Path.Combine(Root, "ProjectSettings", "ProjectVersion.txt"), "m_EditorVersion: 6000.3.22f1");
            Put(Path.Combine(Root, "Packages", "manifest.json"), "{}");
            Put(Path.Combine(Root, "Library", "Bee", "graph.dag.json"), "{\"Nodes\":[]}");
            Put(Path.Combine(Root, "Library", "Bee", "graph-inputdata.json"), "{}");
            Put(Path.Combine(Root, "Library", "Bee", "backend.traceevents"), "[]");
            Put(Path.Combine(Root, "Library", "Bee", "artifacts", "rsp", "compile.rsp"), "--project workspace");
            Put(Path.Combine(Root, "Library", "Bee", "artifacts", "assembly.dll"), "unselected");
            WriteWorkspace();
            string overlay = Path.Combine(Control, "overlay");
            PutJson(Path.Combine(overlay, "request.json"), new { identity = new { version = 2, id = Id, snapshot = new { id = "basis" } }, mountPath = Root, controlPath = overlay, token = "provider-secret" });
            PutJson(Path.Combine(overlay, "state.json"), new { version = 1, id = Id, snapshotId = "basis", phase = "mounted", mountPath = Root,
                token = "provider-secret", processId = 11, processStartTimeUtcTicks = 111L, observedAt = DateTimeOffset.UtcNow,
                storage = new { metrics = new { epoch = "provider-generation", flushCount = 3, stopwatchFrequency = 10000000 } },
                gitReads = new { bytes = 100 }, writeAdmission = new { requests = 2 } });
            PutJson(Path.Combine(overlay, "provider-launch-receipt.json"), new { token = "provider-secret", processId = 11, startTimeUtcTicks = 111L });
            Options = new StartupCapture.Options { Stage = "before-start", ControlPath = Control, OutputPath = Output };
        }
        internal void WriteWorkspace(string phase = "ready") => PutJson(Path.Combine(Control, "workspace.json"), new
        {
            version = 2, applicationKind = "unity", storageMode = "git-snapshot", phase, id = Id, path = Root,
            projectPath = Path.Combine(Directory.Path, "original"), requestDigest = "digest", baseRevision = "revision", generatedDirectoryId = "library",
            snapshot = new { attached = true, reference = new { id = "basis" }, mount = new { id = Id, path = Root, providerProcessId = 11, providerStartTimeUtcTicks = 111L } }
        });
        internal void Live(string mismatch = "")
        {
            Options.Stage = "live";
            string executable = Path.Combine(Directory.Path, "Unity.exe");
            PutJson(Path.Combine(Control, "launch-receipt.json"), new { processId = 22, startTimeUtcTicks = 222L, token = "editor-secret", executablePath = executable, group = new { controlPath = Control } });
            PutJson(Path.Combine(Control, "launch-intent.json"), new { id = Id, projectPath = Root, token = "editor-secret", executable });
            PutJson(Path.Combine(Control, "heartbeat.json"), new { pid = 22, startTimeUtcTicks = 222L,
                token = mismatch == "token" ? "other" : "editor-secret", projectPath = mismatch == "project" ? Directory.Path : Root,
                observedAt = DateTimeOffset.UtcNow.AddMinutes(mismatch == "stale" ? -1 : 0) });
            Put(Path.Combine(Control, "editor.log"), "log");
        }
        internal string Run(Func<StartupCapture.Lease, bool>? process = null, Func<string, bool>? mount = null) =>
            StartupCapture.Execute(Options, process ?? (_ => true), mount ?? (_ => true));
        internal string[] Inventory() => System.IO.Directory.EnumerateFiles(Directory.Path, "*", SearchOption.AllDirectories)
            .Where(p => !p.StartsWith(Output + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            .Order(StringComparer.Ordinal).Select(p => p + ":" + File.GetLastWriteTimeUtc(p).Ticks + ":" + Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(p)))).ToArray();
        private static void Put(string path, string content) { System.IO.Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllText(path, content); }
        private static void PutJson(string path, object value) => Put(path, JsonSerializer.Serialize(value));
        public void Dispose() => Directory.Dispose();
    }
}
