using System.Diagnostics;
using System.Text.Json;
using Splinter.Core;
using Splinter.Platform.Windows;
using Splinter.Runtime;

namespace Splinter.Tests;

public sealed class SourceCaptureProcessTests
{
    private sealed record Request(string Root, string Boundary, string Nonce, int Version = 2);
    private static OverlayIdentity Identity(ISnapshotTree basis, int version) => new(version, "source-capture-process", basis.Snapshot, 10485760, 65536);
    private sealed class Gate(Request request) : IJournalMutationObserver
    {
        public void Observe(JournalMutationBoundary boundary)
        {
            if (boundary.Boundary != request.Boundary) return;
            string pending = Path.Combine(request.Root, "signal.pending");
            using (var output = new FileStream(pending, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
            { JsonSerializer.Serialize(output, new { request.Nonce, Pid = Environment.ProcessId, Start = Process.GetCurrentProcess().StartTime.ToUniversalTime().Ticks }); output.Flush(true); }
            File.Move(pending, Path.Combine(request.Root, "signal.json"));
            Thread.Sleep(Timeout.Infinite);
        }
    }
    [Fact]
    public void Child()
    {
        string? input = Environment.GetEnvironmentVariable("SPLINTER_SOURCE_CAPTURE_CHILD");
        if (input is null) Assert.Skip("Subprocess-only source capture entrypoint.");
        var request = JsonSerializer.Deserialize<Request>(File.ReadAllText(input))!;
        var basis = new SnapshotOverlayTests.FakeSnapshotTree();
        using var writer = new JournalDeltaStore(Path.Combine(request.Root, "delta"), Identity(basis, request.Version), basis,
            new Gate(request), sourceFilePins: new WindowsJournalFilePins());
        var file = writer.Create("source", false, 32); writer.Write(file.Id, 0, "frozen"u8);
        writer.CaptureSource(Path.Combine(request.Root, "captures", new string('a', 24)), new string('a', 24), []);
        throw new InvalidOperationException("Capture boundary not reached.");
    }
    [Theory]
    [InlineData("capture.intent-published")]
    [InlineData("capture.backings-pinned")]
    public Task ForcedProviderExitRecoversFrozenRangesBeforeLaterWrites(string boundary) => ForcedProviderExit(2, boundary);

    [Theory]
    [InlineData("capture.freeze-accepted")]
    [InlineData("capture.intent-published")]
    public Task ForcedCurrentProviderExitRecoversFrozenRootsAndNativePinsBeforeLaterWrites(string boundary) => ForcedProviderExit(3, boundary);

    private static async Task ForcedProviderExit(int version, string boundary)
    {
        if (!OperatingSystem.IsWindows()) Assert.Skip("NTFS source backing aliases.");
        using var root = new TestDirectory(); var ct = TestContext.Current.CancellationToken;
        var request = new Request(root.Path, boundary, Guid.NewGuid().ToString("N"), version);
        string input = Path.Combine(root.Path, "request.json"); File.WriteAllText(input, JsonSerializer.Serialize(request));
        var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (string arg in new[] { typeof(SourceCaptureProcessTests).Assembly.Location, "-method", "Splinter.Tests.SourceCaptureProcessTests.Child" }) start.ArgumentList.Add(arg);
        start.Environment["SPLINTER_SOURCE_CAPTURE_CHILD"] = input;
        using var child = Process.Start(start)!; long ticks = child.StartTime.ToUniversalTime().Ticks;
        var stdout = child.StandardOutput.ReadToEndAsync(ct); var stderr = child.StandardError.ReadToEndAsync(ct);
        try
        {
            string signal = Path.Combine(root.Path, "signal.json"); long began = Stopwatch.GetTimestamp();
            while (!File.Exists(signal) && !child.HasExited && Stopwatch.GetElapsedTime(began).TotalSeconds < 25) await Task.Delay(20, ct);
            Assert.True(File.Exists(signal), "The owned capture process did not reach its boundary.");
            var observed = await WindowsProcessWorkspaceProvider.ReadAsync<JsonElement>(signal, ct);
            Assert.Equal(request.Nonce, observed.GetProperty("Nonce").GetString()); Assert.Equal(child.Id, observed.GetProperty("Pid").GetInt32());
            Assert.Equal(ticks, observed.GetProperty("Start").GetInt64()); Assert.Equal(ticks, child.StartTime.ToUniversalTime().Ticks);
            child.Kill(); await child.WaitForExitAsync(ct);
            // A signaled Windows process can still have a journal handle in
            // teardown. Observe sharing release before the single recovery
            // attempt, as in JournalInterruptionTests. Never retry replay,
            // corruption, access denial or missing input as an open delay.
            string journal = Path.Combine(root.Path, "delta", "delta.journal");
            long opening = Stopwatch.GetTimestamp();
            while (true)
            {
                try { using var probe = new FileStream(journal, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete); break; }
                catch (IOException error) when ((error.HResult & 0xffff) is 32 or 33 && Stopwatch.GetElapsedTime(opening) < TimeSpan.FromSeconds(5))
                { await Task.Delay(20, ct); }
            }
            var pins = new WindowsJournalFilePins(); string captures = Path.Combine(root.Path, "captures"), capture = Path.Combine(captures, new string('a', 24));
            SourceCaptureTree.RecoverPins(captures, pins);
            var basis = new SnapshotOverlayTests.FakeSnapshotTree();
            using (var writer = new JournalDeltaStore(Path.Combine(root.Path, "delta"), Identity(basis, version), basis, sourceFilePins: pins))
            {
                if (version == 3) writer.RecoverSourceCaptures(captures);
                var fixedCapture = SourceCaptureTree.ReadCapture(capture);
                if (version == 3)
                {
                    Assert.NotNull(fixedCapture.Paged?.Segments); Assert.Empty(fixedCapture.Nodes);
                    string[] aliases = Directory.GetFiles(capture, "*.pin"); Assert.NotEmpty(aliases);
                    foreach (string alias in aliases)
                    {
                        string source = Path.Combine(root.Path, "delta", Path.GetFileNameWithoutExtension(alias) + ".sjr");
                        Assert.Equal(pins.Identify(source), pins.Identify(alias));
                    }
                    Assert.Equal(fixedCapture.ReaderIdentity, pins.Identify(Path.Combine(capture, fixedCapture.ReaderName!)));
                    writer.MaintainLiveStorage(force: true, collectSegments: true, ct: ct);
                }
                writer.Write(writer.Stat("source")!.Id, 0, "later!"u8);
                Assert.Equal(fixedCapture.Digest, writer.CaptureSource(capture, fixedCapture.Id, []).Digest);
                using (var frozen = new SourceCaptureTree(capture, basis, new WindowsJournalFilePins()))
                {
                    byte[] bytes = new byte[6]; Assert.Equal(6, frozen.Read("source", 0, bytes)); Assert.Equal("frozen"u8.ToArray(), bytes);
                    if (version == 3)
                    {
                        Assert.ThrowsAny<IOException>(() => SourceCaptureTree.Complete(capture, pins));
                        Assert.False(File.Exists(Path.Combine(capture, "materialized.json")));
                    }
                }
                if (version == 3)
                {
                    string nextPath = Path.Combine(captures, new string('b', 24));
                    var next = writer.CaptureSource(nextPath, new string('b', 24), []); Assert.True(next.Sequence > fixedCapture.Sequence);
                    using (var later = new SourceCaptureTree(nextPath, basis, pins))
                    { byte[] bytes = new byte[6]; Assert.Equal(6, later.Read("source", 0, bytes)); Assert.Equal("later!"u8.ToArray(), bytes); }
                    SourceCaptureTree.Complete(nextPath, pins); writer.CompleteSourceCaptureRequest(next.Id);
                }
            }
            SourceCaptureTree.Complete(capture, pins); Assert.Empty(Directory.EnumerateFiles(capture, "*.pin"));
        }
        finally
        {
            if (!child.HasExited && child.StartTime.ToUniversalTime().Ticks == ticks) { child.Kill(); await child.WaitForExitAsync(CancellationToken.None); }
            File.WriteAllText(Path.Combine(root.Path, "stdout.log"), await stdout); File.WriteAllText(Path.Combine(root.Path, "stderr.log"), await stderr);
        }
    }
}
