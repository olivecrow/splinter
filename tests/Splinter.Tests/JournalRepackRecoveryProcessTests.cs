using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Splinter.Core;
using Splinter.Platform.Windows;
using Splinter.Runtime;

namespace Splinter.Tests;

public sealed class JournalRepackRecoveryProcessTests
{
    private sealed record Request(string Root, string Nonce);
    private static OverlayIdentity Identity(ISnapshotTree basis) => new(3, "repack-process-owner", basis.Snapshot, long.MaxValue, 65536);
    private static void Roll(JournalDeltaStore writer) => typeof(JournalDeltaStore).GetMethod("RollSegment", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(writer, null);
    private static void OldCollect(JournalDeltaStore writer, CancellationToken ct) => typeof(JournalDeltaStore).GetMethod("CollectDeadSegments", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(writer, [ct, null]);
    [Fact]
    public void Child()
    {
        string? path = Environment.GetEnvironmentVariable("SPLINTER_REPACK_RECOVERY_CHILD");
        if (path is null) Assert.Skip("Subprocess-only fixed index-copy crash actor.");
        var request = JsonSerializer.Deserialize<Request>(File.ReadAllText(path!))!; var basis = new SnapshotOverlayTests.FakeSnapshotTree();
        using var writer = new JournalDeltaStore(Path.Combine(request.Root, "delta"), Identity(basis), basis, sourceFilePins: new WindowsJournalFilePins());
        var file = writer.Create("source", false, 32); writer.Write(file.Id, 0, "base"u8); Roll(writer); Roll(writer);
        writer.CollectLiveMetadata(true, TestContext.Current.CancellationToken, point =>
        {
            if (point != "metadata.repack-payload-durable") return;
            string signal = Path.Combine(request.Root, "signal.json"), pending = signal + ".pending";
            using (var output = new FileStream(pending, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
            {
                using var process = Process.GetCurrentProcess();
                JsonSerializer.Serialize(output, new { request.Nonce, Pid = process.Id, Start = process.StartTime.ToUniversalTime().Ticks }); output.Flush(true);
            }
            File.Move(pending, signal);
            Thread.Sleep(Timeout.Infinite);
        });
        throw new InvalidOperationException("Fixed durable-copy boundary was not reached.");
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task KilledCopiedRequestResumesInOrderAndReconcilesOnlyAlreadyRetiredEmptyHeaders(bool oldStartupAlreadyTrimmed)
    {
        using var root = new TestDirectory(); var ct = TestContext.Current.CancellationToken; await KillAtCopiedRoot(root.Path, ct);
        var basis = new SnapshotOverlayTests.FakeSnapshotTree(); var pins = new WindowsJournalFilePins();
        string delta = Path.Combine(root.Path, "delta"), intent = Path.Combine(delta, "live-repack.json");
        using var writer = new JournalDeltaStore(delta, Identity(basis), basis, sourceFilePins: pins);
        var before = writer.Seal(); string request = ReadRequest(intent);
        if (oldStartupAlreadyTrimmed) OldCollect(writer, ct); // Models the previous startup order, not a production bypass.
        List<string> phases = [];
        writer.MaintainLiveStorage(true, ct, true, phases.Add);
        Assert.Equal(before, writer.Seal()); Assert.False(File.Exists(intent)); Assert.Contains("metadata.repack-published", phases);
        int copied = phases.IndexOf("metadata.repack-published"), retired = phases.IndexOf("segments.reclaim-intent");
        Assert.True(retired < 0 || retired > copied, "Independent catalog collection must follow the same pending copy publication.");
        byte[] data = new byte[4]; Assert.Equal(4, writer.Read(writer.Stat("source")!.Id, 0, data)); Assert.Equal("base"u8.ToArray(), data);
        var current = writer.Stat("source")!; writer.Write(current.Id, 0, "next"u8);
        string captureId = new('a', 24), capture = Path.Combine(root.Path, "captures", captureId);
        var frozen = writer.CaptureSource(capture, captureId, []); writer.Write(current.Id, 0, "last"u8);
        using (var reader = new SourceCaptureTree(capture, basis, pins))
        { Assert.Equal(4, reader.Read("source", 0, data)); Assert.Equal("next"u8.ToArray(), data); writer.MaintainLiveStorage(true, ct, true); }
        SourceCaptureTree.Complete(capture, pins); writer.CompleteSourceCaptureRequest(frozen.Id);
        Assert.Equal(4, writer.Read(current.Id, 0, data)); Assert.Equal("last"u8.ToArray(), data); Assert.NotEmpty(request);
    }
    [Fact]
    public async Task DeferredFixedReaderPreventsAnyIndependentSegmentCatalogMutation()
    {
        using var root = new TestDirectory(); var ct = TestContext.Current.CancellationToken; await KillAtCopiedRoot(root.Path, ct);
        var basis = new SnapshotOverlayTests.FakeSnapshotTree(); string delta = Path.Combine(root.Path, "delta");
        using var writer = new JournalDeltaStore(delta, Identity(basis), basis, sourceFilePins: new WindowsJournalFilePins());
        string state = File.ReadAllText(Path.Combine(delta, "segment-state.json"));
        var held = writer.EnumerateChildren("");
        try
        {
            writer.MaintainLiveStorage(true, ct, true);
            Assert.Equal(state, File.ReadAllText(Path.Combine(delta, "segment-state.json")));
            Assert.True(File.Exists(Path.Combine(delta, "live-repack.json")));
        }
        finally { ((IDisposable)held).Dispose(); }
        writer.MaintainLiveStorage(true, ct, true); Assert.False(File.Exists(Path.Combine(delta, "live-repack.json")));
    }
    [Theory]
    [InlineData("nonempty")]
    [InlineData("native-clone")]
    [InlineData("descriptor")]
    [InlineData("logical-root")]
    [InlineData("accounting")]
    public async Task ReconciliationRejectsNonemptyRetirementForeignBindingsAndAnyOtherLogicalOrAccountingChange(string kind)
    {
        using var root = new TestDirectory(); var ct = TestContext.Current.CancellationToken; await KillAtCopiedRoot(root.Path, ct);
        var basis = new SnapshotOverlayTests.FakeSnapshotTree(); string delta = Path.Combine(root.Path, "delta");
        using var writer = new JournalDeltaStore(delta, Identity(basis), basis, sourceFilePins: new WindowsJournalFilePins());
        if (kind == "nonempty")
        {
            var file = writer.Stat("source")!; writer.Delete(file.Id); writer.ReleaseUnlinkedFile(file.Id);
        }
        OldCollect(writer, ct);
        if (kind == "nonempty") Assert.False(File.Exists(Path.Combine(delta, JournalSegmentStorage.FileName(0))), "The negative fixture must actually retire the nonempty backing, not merely unlink its still-owned ranges.");
        if (kind == "native-clone")
        { string path = Path.Combine(delta, JournalSegmentStorage.FileName(0)), saved = Path.Combine(root.Path, "original-segment"); File.Move(path, saved); File.Copy(saved, path); }
        if (kind == "descriptor")
        {
            var rootField = typeof(JournalDeltaStore).GetField("segmentRoot", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var store = (PagedMetadataStore)typeof(JournalDeltaStore).GetField("pagedIndex", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(writer)!;
            var catalog = (MetadataTreeRoot)rootField.GetValue(writer)!; byte[] value = store.Open(catalog).Find(0L.ToString("x16"))!;
            var descriptor = JournalDeltaStore.ReadCapturedSegment(value) with { NativeIdentity = "foreign-binding" };
            byte[] changed = (byte[])typeof(JournalDeltaStore).GetMethod("EncodeSegment", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [descriptor])!;
            rootField.SetValue(writer, store.Set(catalog, 0L.ToString("x16"), changed)); store.Flush(); Roll(writer);
        }
        if (kind == "logical-root")
        { var hidden = (ISet<string>)typeof(JournalDeltaStore).GetField("hidden", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(writer)!; hidden.Add("unacknowledged-hidden"); Roll(writer); }
        if (kind == "accounting")
        { typeof(JournalDeltaStore).GetField("nextDataSegment", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(writer, -3L); Roll(writer); }
        var error = Record.Exception(() => writer.MaintainLiveStorage(true, ct, true)); Assert.NotNull(error);
        Assert.True(File.Exists(Path.Combine(delta, "live-repack.json"))); // No reset/new request/deletion of copied metadata.
    }
    private static string ReadRequest(string path)
    { using var document = JsonDocument.Parse(File.ReadAllText(path)); return document.RootElement.GetProperty("request").GetString()!; }
    private static async Task KillAtCopiedRoot(string root, CancellationToken ct)
    {
        if (!OperatingSystem.IsWindows()) Assert.Skip("Native current-journal reader and ownership protections.");
        var request = new Request(root, Guid.NewGuid().ToString("N")); string input = Path.Combine(root, "request.json"); File.WriteAllText(input, JsonSerializer.Serialize(request));
        var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (string argument in new[] { typeof(JournalRepackRecoveryProcessTests).Assembly.Location, "-method", "Splinter.Tests.JournalRepackRecoveryProcessTests.Child" }) start.ArgumentList.Add(argument);
        start.Environment["SPLINTER_REPACK_RECOVERY_CHILD"] = input;
        using var child = Process.Start(start)!; long ticks = child.StartTime.ToUniversalTime().Ticks;
        var stdout = child.StandardOutput.ReadToEndAsync(ct); var stderr = child.StandardError.ReadToEndAsync(ct);
        try
        {
            string signal = Path.Combine(root, "signal.json"); long began = Stopwatch.GetTimestamp();
            while (!File.Exists(signal) && !child.HasExited && Stopwatch.GetElapsedTime(began) < TimeSpan.FromSeconds(30)) await Task.Delay(20, ct);
            Assert.True(File.Exists(signal), "The exact current-provider actor did not reach its durable copied root.");
            var proof = await WindowsProcessWorkspaceProvider.ReadAsync<JsonElement>(signal, ct);
            Assert.Equal(request.Nonce, proof.GetProperty("Nonce").GetString()); Assert.Equal(child.Id, proof.GetProperty("Pid").GetInt32());
            Assert.Equal(ticks, proof.GetProperty("Start").GetInt64()); Assert.Equal(ticks, child.StartTime.ToUniversalTime().Ticks);
            child.Kill(); await child.WaitForExitAsync(ct);
            string journal = Path.Combine(root, "delta", "delta.journal"); long opening = Stopwatch.GetTimestamp();
            while (true)
            {
                try { using var probe = new FileStream(journal, FileMode.Open, FileAccess.ReadWrite, FileShare.Read); break; }
                catch (IOException error) when ((error.HResult & 0xffff) is 32 or 33 && Stopwatch.GetElapsedTime(opening) < TimeSpan.FromSeconds(5)) { await Task.Delay(20, ct); }
            }
        }
        finally
        {
            if (!child.HasExited) { child.Kill(); await child.WaitForExitAsync(ct); }
            await stdout; await stderr;
        }
    }
}
