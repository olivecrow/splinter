using System.Buffers.Binary;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Splinter.Core;
using Splinter.Platform.Windows;
using Splinter.Runtime;

namespace Splinter.Tests;

public sealed class PagedMetadataSpineRecoveryTests
{
    private sealed record Request(string Root, string Nonce);
    private sealed record Signal(string Nonce, int Pid, long Start, MetadataTreeRoot Root, string Container, string ContainerIdentity);

    [Fact]
    public void InterruptedDeepBuildRemovesOnlyItsOpenedScratchAndKeepsThePriorNativeRoot()
    {
        if (!OperatingSystem.IsWindows()) Assert.Skip("Native metadata ownership and delete-on-close.");
        using var files = new TestDirectory(); string directory = Path.Combine(files.Path, "metadata");
        var pins = new WindowsJournalFilePins(); MetadataTreeRoot prior; bool interrupted = false;
        using (var store = new PagedMetadataStore(directory, writable: true, filePins: pins))
        {
            prior = store.Set(store.Empty, "retained", "prior"u8); store.Flush();
            IEnumerable<MetadataEntry> FaultedInput()
            {
                foreach (var entry in DeepEntries())
                {
                    yield return entry;
                    if (!Directory.EnumerateFiles(directory, "build-*.pending").Any()) continue;
                    interrupted = true; throw new IOException("Injected storage exhaustion while the private build spine is open.", unchecked((int)0x80070070));
                }
            }
            var error = Assert.Throws<IOException>(() => store.Build(FaultedInput(), TestContext.Current.CancellationToken));
            Assert.Equal(unchecked((int)0x80070070), error.HResult); Assert.True(interrupted);
            Assert.Empty(Directory.EnumerateFiles(directory, "build-*.pending"));
            store.Open(prior).Verify(TestContext.Current.CancellationToken); Assert.Equal("prior"u8.ToArray(), store.Open(prior).Find("retained"));
            var recovered = store.Build(DeepEntries(), TestContext.Current.CancellationToken); store.Flush(); store.Open(recovered).Verify(TestContext.Current.CancellationToken);
        }
        PagedMetadataStore.ValidateReclaimInventory(directory, pins);
        using var reader = new PagedMetadataStore(directory); Assert.Equal("prior"u8.ToArray(), reader.Open(prior).Find("retained"));
    }

    [Fact]
    public void Child()
    {
        string? input = Environment.GetEnvironmentVariable("SPLINTER_METADATA_SPINE_CHILD");
        if (input is null) Assert.Skip("Subprocess-only private metadata spine actor.");
        var request = JsonSerializer.Deserialize<Request>(File.ReadAllText(input))!;
        string directory = Path.Combine(request.Root, "metadata"); var pins = new WindowsJournalFilePins();
        using var store = new PagedMetadataStore(directory, writable: true, filePins: pins);
        var prior = store.Set(store.Empty, "retained", "prior"u8); store.Flush();
        IEnumerable<MetadataEntry> PausedInput()
        {
            foreach (var entry in DeepEntries())
            {
                yield return entry;
                if (!Directory.EnumerateFiles(directory, "build-*.pending").Any()) continue;
                string container = Directory.EnumerateFiles(directory, "pages-*.bin").Single();
                var signal = new Signal(request.Nonce, Environment.ProcessId, Process.GetCurrentProcess().StartTime.ToUniversalTime().Ticks,
                    prior, container, pins.Identify(container));
                string pending = Path.Combine(request.Root, "signal.pending");
                using (var output = new FileStream(pending, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
                { JsonSerializer.Serialize(output, signal); output.Flush(true); }
                File.Move(pending, Path.Combine(request.Root, "signal.json")); Thread.Sleep(Timeout.Infinite);
            }
        }
        store.Build(PausedInput(), TestContext.Current.CancellationToken);
        throw new InvalidOperationException("The deep metadata spine was not opened.");
    }

    [Fact]
    public async Task ForcedBuildProcessExitLeavesNoRecoveryBlockingScratchOrForeignDeletion()
    {
        if (!OperatingSystem.IsWindows()) Assert.Skip("Native metadata ownership and process handle teardown.");
        using var files = new TestDirectory(); var ct = TestContext.Current.CancellationToken;
        var request = new Request(files.Path, Guid.NewGuid().ToString("N")); string input = Path.Combine(files.Path, "request.json");
        File.WriteAllText(input, JsonSerializer.Serialize(request));
        var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (string arg in new[] { typeof(PagedMetadataSpineRecoveryTests).Assembly.Location, "-method", "Splinter.Tests.PagedMetadataSpineRecoveryTests.Child" }) start.ArgumentList.Add(arg);
        start.Environment["SPLINTER_METADATA_SPINE_CHILD"] = input;
        using var child = Process.Start(start)!; long ticks = child.StartTime.ToUniversalTime().Ticks;
        var stdout = child.StandardOutput.ReadToEndAsync(ct); var stderr = child.StandardError.ReadToEndAsync(ct);
        try
        {
            string signalPath = Path.Combine(files.Path, "signal.json"); long began = Stopwatch.GetTimestamp();
            while (!File.Exists(signalPath) && !child.HasExited && Stopwatch.GetElapsedTime(began) < TimeSpan.FromSeconds(25)) await Task.Delay(20, ct);
            Assert.True(File.Exists(signalPath), child.HasExited ? await stdout + await stderr : "The owned spine actor did not reach its spill.");
            var signal = JsonSerializer.Deserialize<Signal>(File.ReadAllText(signalPath))!;
            Assert.Equal(request.Nonce, signal.Nonce); Assert.Equal(child.Id, signal.Pid); Assert.Equal(ticks, signal.Start);
            string directory = Path.Combine(files.Path, "metadata"), scratch = Assert.Single(Directory.GetFiles(directory, "build-*.pending"));
            // Only the actor's exact create-new object is delete-on-close.
            // Foreign replacement cannot become a later pathname deletion.
            Assert.ThrowsAny<IOException>(() => File.Move(scratch, scratch + ".foreign"));
            child.Kill(); await child.WaitForExitAsync(ct);
            long opening = Stopwatch.GetTimestamp();
            while (true)
            {
                try { using var probe = new FileStream(Path.Combine(directory, "writer.lock"), FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete); break; }
                catch (IOException error) when ((error.HResult & 0xffff) is 32 or 33 && Stopwatch.GetElapsedTime(opening) < TimeSpan.FromSeconds(5))
                { await Task.Delay(20, ct); }
            }
            Assert.Empty(Directory.EnumerateFiles(directory, "build-*.pending"));
            var pins = new WindowsJournalFilePins(); Assert.Equal(signal.ContainerIdentity, pins.Identify(signal.Container));
            using (var store = new PagedMetadataStore(directory, writable: true, filePins: pins))
            {
                store.Open(signal.Root).Verify(ct); Assert.Equal("prior"u8.ToArray(), store.Open(signal.Root).Find("retained"));
                var after = store.Build(DeepEntries(), ct); store.Flush(); store.Open(after).Verify(ct);
            }
            PagedMetadataStore.ValidateReclaimInventory(directory, pins);
        }
        finally
        {
            if (!child.HasExited && child.StartTime.ToUniversalTime().Ticks == ticks) { child.Kill(); await child.WaitForExitAsync(CancellationToken.None); }
            File.WriteAllText(Path.Combine(files.Path, "stdout.log"), await stdout); File.WriteAllText(Path.Combine(files.Path, "stderr.log"), await stderr);
        }
    }

    private static IEnumerable<MetadataEntry> DeepEntries()
    {
        // A strictly increasing-priority subsequence forces an actual >64
        // one-sided construction spine, independently of random tree shape.
        const int count = 8192; var priorities = new ulong[count]; var previous = new int[count]; var tails = new List<int>();
        for (int index = 0; index < count; index++)
        {
            priorities[index] = BinaryPrimitives.ReadUInt64BigEndian(SHA256.HashData(Encoding.UTF8.GetBytes(index.ToString("D8"))));
            int low = 0, high = tails.Count;
            while (low < high)
            { int middle = (low + high) / 2; if (priorities[tails[middle]] < priorities[index]) low = middle + 1; else high = middle; }
            previous[index] = low == 0 ? -1 : tails[low - 1]; if (low == tails.Count) tails.Add(index); else tails[low] = index;
        }
        var selected = new List<int>(); for (int index = tails[^1]; index >= 0; index = previous[index]) selected.Add(index);
        selected.Reverse(); Assert.True(selected.Count > 64);
        foreach (int index in selected) yield return new(index.ToString("D8"), "value"u8.ToArray());
    }
}
