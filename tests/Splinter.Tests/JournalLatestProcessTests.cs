using System.Diagnostics;
using System.Text.Json;
using Splinter.Core;
using Splinter.Runtime;

namespace Splinter.Tests;

public sealed class JournalLatestProcessTests
{
    private sealed record Request(string Directory, string Boundary, string Nonce, Guid Id, byte[] C);
    private static OverlayIdentity Identity(ISnapshotTree tree) => new(2, "latest-process", tree.Snapshot, 1048576, 1048576);
    private static void Publish(string path, object data)
    {
        using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        JsonSerializer.Serialize(file, data); file.Flush(true);
    }
    private sealed class Gate(Request request) : IJournalMutationObserver
    {
        public void Observe(JournalMutationBoundary boundary)
        {
            if (boundary.Boundary != request.Boundary) return;
            Publish(Path.Combine(request.Directory, "signal.json"), new { request.Nonce, Pid = Environment.ProcessId,
                Start = Process.GetCurrentProcess().StartTime.ToUniversalTime().Ticks });
            Thread.Sleep(Timeout.Infinite);
        }
    }
    [Fact]
    public void Child()
    {
        string? input = Environment.GetEnvironmentVariable("SPLINTER_LATEST_JOURNAL_CHILD");
        if (input is null) return;
        var request = JsonSerializer.Deserialize<Request>(File.ReadAllText(input))!;
        var tree = new SnapshotOverlayTests.FakeSnapshotTree();
        using var writer = new JournalDeltaStore(Path.Combine(request.Directory, "delta"), Identity(tree), tree, new Gate(request));
        writer.Write(request.Id, 0, request.C);
        throw new InvalidOperationException("The requested process boundary was not reached.");
    }
    [Theory]
    [InlineData("latest.after-intent-prefix", false)]
    [InlineData("latest.after-block-prefix", false)]
    [InlineData("latest.after-staging", true)]
    [InlineData("latest.after-root-publication", true)]
    [InlineData("latest.after-object-reclamation", true)]
    public async Task OwnedProcessTerminationPreservesBOrCompletesC(string boundary, bool completed)
    {
        using var root = new TestDirectory(); string delta = Path.Combine(root.Path, "delta");
        var tree = new SnapshotOverlayTests.FakeSnapshotTree(); var identity = Identity(tree);
        using (var empty = new JournalDeltaStore(delta, identity, tree)) { }
        JournalDeltaStore.CompactDirectory(delta, identity, tree, revertBaseRanges: true); JournalDeltaStore.CompleteCompaction(delta, identity);
        byte[] b = new byte[8192], c = new byte[8192]; new Random(61006).NextBytes(b); new Random(61007).NextBytes(c);
        Guid id;
        using (var writer = new JournalDeltaStore(delta, identity, tree))
        { id = writer.Create("file", false, 32).Id; writer.Write(id, 0, b); }
        var request = new Request(root.Path, boundary, Guid.NewGuid().ToString("N"), id, c);
        string input = Path.Combine(root.Path, "request.json"); Publish(input, request);
        var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add(typeof(JournalLatestProcessTests).Assembly.Location); start.ArgumentList.Add("-method");
        start.ArgumentList.Add("Splinter.Tests.JournalLatestProcessTests.Child"); start.Environment["SPLINTER_LATEST_JOURNAL_CHILD"] = input;
        using var child = Process.Start(start)!; long ticks = child.StartTime.ToUniversalTime().Ticks;
        var ct = TestContext.Current.CancellationToken;
        var stdout = child.StandardOutput.ReadToEndAsync(ct); var stderr = child.StandardError.ReadToEndAsync(ct);
        try
        {
            string signal = Path.Combine(root.Path, "signal.json"); long began = Stopwatch.GetTimestamp();
            while (!File.Exists(signal) && !child.HasExited && Stopwatch.GetElapsedTime(began).TotalSeconds < 25) await Task.Delay(20, ct);
            Assert.True(File.Exists(signal), "The exact owned child did not reach its requested boundary.");
            JsonElement observed = default;
            for (int i = 0; i < 50; i++)
            {
                try { observed = JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(signal)); break; }
                catch (IOException ex) when ((ex.HResult & 65535) is 32 or 33) { await Task.Delay(20, ct); }
            }
            Assert.Equal(request.Nonce, observed.GetProperty("Nonce").GetString()); Assert.Equal(child.Id, observed.GetProperty("Pid").GetInt32());
            Assert.Equal(ticks, observed.GetProperty("Start").GetInt64()); Assert.Equal(ticks, child.StartTime.ToUniversalTime().Ticks);
            child.Kill(); await child.WaitForExitAsync(ct);
            File.WriteAllText(Path.Combine(root.Path, "stdout.log"), await stdout); File.WriteAllText(Path.Combine(root.Path, "stderr.log"), await stderr);
            using var resumed = new JournalDeltaStore(delta, identity, tree);
            byte[] actual = new byte[8192]; resumed.Read(id, 0, actual); Assert.Equal(completed ? c : b, actual);
            Assert.Equal(completed ? 3 : 2, resumed.Observe().DurableSequence);
            Assert.Equal(8192, resumed.Observe().PayloadBytes);
        }
        finally
        {
            if (!child.HasExited)
            { Assert.Equal(ticks, child.StartTime.ToUniversalTime().Ticks); child.Kill(); await child.WaitForExitAsync(ct); }
        }
    }
}
