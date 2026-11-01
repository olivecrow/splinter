using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Splinter.Core;
using Splinter.Platform.Windows;
using Splinter.Runtime;

namespace Splinter.Tests;

public sealed class JournalReadOnlyReplayScopeTests
{
    private sealed record ActorRequest(string Root, string Nonce, MetadataTreeRoot Prior);
    private sealed record ActorSignal(string Nonce, int Pid, long Start, string Directory, string NativeIdentity);

    [Fact]
    public void AdmittedAndNestedReadersKeepDerivedPagesAndOwnedBasisAliveAfterOwnerDisposes()
    {
        RequireWindows(); using var f = new Fixture(); var before = f.SourceProof(); var resource = new CountedResource();
        var scope = f.Open(); string directory = scope.ScratchDirectory;
        var changed = scope.Store.Set(f.Prior, "derived", "private"u8); scope.Store.Flush();
        var outer = scope.Retain(); scope.OwnUntilReleased(resource); scope.Dispose();
        Assert.True(Directory.Exists(directory)); Assert.Equal(0, resource.Disposals);
        using (var nested = scope.Retain())
        {
            outer.Dispose(); Assert.Equal("prior"u8.ToArray(), scope.Store.Open(changed).Find("retained"));
            Assert.Equal("private"u8.ToArray(), scope.Store.Open(changed).Find("derived"));
            Assert.True(Directory.Exists(directory)); Assert.Equal(0, resource.Disposals);
        }
        Assert.False(Directory.Exists(directory)); Assert.Equal(1, resource.Disposals);
        Assert.Throws<ObjectDisposedException>(() => scope.Retain()); f.AssertSource(before);
        Assert.Empty(JournalDeltaStore.EnumeratePreservedReadOnlyReplays(f.Root.Path, f.Identity));
    }

    [Fact]
    public void AnotherReadOnlyScopeCannotReclaimAnActiveReadersNamespace()
    {
        RequireWindows(); using var f = new Fixture(); using var first = f.Open(); string live = first.ScratchDirectory;
        using (var second = f.Open())
        {
            Assert.NotEqual(live, second.ScratchDirectory); Assert.True(Directory.Exists(live));
            Assert.Contains(JournalDeltaStore.EnumeratePreservedReadOnlyReplays(f.Root.Path, f.Identity), row => row.Directory == live);
        }
        Assert.True(Directory.Exists(live)); first.Dispose(); Assert.False(Directory.Exists(live));
    }

    [Theory]
    [InlineData("replay.scope-created")]
    [InlineData("replay.metadata-created")]
    [InlineData("replay.controls-created")]
    public void ACreationGapStaysOpaqueWhileTheSameSourceCanCreateAnotherOwnedScope(string boundary)
    {
        RequireWindows(); using var f = new Fixture(); var before = f.SourceProof(); bool hit = false;
        Assert.Throws<IOException>(() => f.Open(point => { if (point == boundary) { hit = true; throw new IOException("Lost creation ownership reply."); } }));
        Assert.True(hit); var residual = Assert.Single(JournalDeltaStore.EnumeratePreservedReadOnlyReplays(f.Root.Path, f.Identity));
        string foreign = Path.Combine(residual.Directory, "opaque.bin"); File.WriteAllBytes(foreign, new byte[] { 7, 9 }); string native = f.Pins.Identify(foreign);
        using (var retry = f.Open()) { Assert.Equal("prior"u8.ToArray(), retry.Store.Open(f.Prior).Find("retained")); }
        Assert.Equal(native, f.Pins.Identify(foreign)); Assert.Equal(new byte[] { 7, 9 }, File.ReadAllBytes(foreign));
        Assert.Contains(JournalDeltaStore.EnumeratePreservedReadOnlyReplays(f.Root.Path, f.Identity), row => row.Directory == residual.Directory);
        f.AssertSource(before);
    }

    [Fact]
    public void AFailureAfterTheDurableOwnerRecoversOnlyItsExactKnownControls()
    {
        RequireWindows(); using var f = new Fixture(); var before = f.SourceProof();
        Assert.Throws<IOException>(() => f.Open(point => { if (point == "replay.owner-durable") throw new IOException("Reply lost after durable ownership."); }));
        Assert.Empty(Directory.EnumerateDirectories(f.Replays));
        using (var retry = f.Open()) Assert.Equal("prior"u8.ToArray(), retry.Store.Open(f.Prior).Find("retained"));
        Assert.Empty(Directory.EnumerateDirectories(f.Replays)); f.AssertSource(before);
    }

    [Fact]
    public void AnEqualByteForeignContainerIsPreservedRatherThanAdoptedOrUnlinked()
    {
        RequireWindows(); using var f = new Fixture(); var before = f.SourceProof(); var scope = f.Open();
        _ = scope.Store.Set(f.Prior, "derived", "changed"u8); scope.Store.Flush(); scope.Store.Dispose();
        string metadata = Path.Combine(scope.ScratchDirectory, "metadata"), page = Directory.EnumerateFiles(metadata, "pages-*.bin").Single();
        string old = Path.Combine(f.Root.Path, "prior-owned-page.bin"); File.Move(page, old); File.Copy(old, page);
        string foreign = f.Pins.Identify(page); Assert.NotEqual(f.Pins.Identify(old), foreign);
        byte[] bytes = File.ReadAllBytes(page); scope.Dispose();
        using (var retry = f.Open()) Assert.Equal("prior"u8.ToArray(), retry.Store.Open(f.Prior).Find("retained"));
        Assert.Equal(foreign, f.Pins.Identify(page)); Assert.Equal(bytes, File.ReadAllBytes(page));
        Assert.Contains(JournalDeltaStore.EnumeratePreservedReadOnlyReplays(f.Root.Path, f.Identity), row => row.Directory == scope.ScratchDirectory && row.Reason.Contains("preserved", StringComparison.Ordinal));
        f.AssertSource(before);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(48)]
    public void AnUnownedPageCreationPrefixRemainsProtectedAcrossRecovery(int bytes)
    {
        RequireWindows(); using var f = new Fixture(); var before = f.SourceProof(); var scope = f.Open();
        scope.Store.Dispose(); string metadata = Path.Combine(scope.ScratchDirectory, "metadata");
        string gap = Path.Combine(metadata, "pages-7fffffffffffffff.bin");
        File.WriteAllBytes(gap, File.ReadAllBytes(Path.Combine(metadata, "identity.bin")).AsSpan(0, bytes).ToArray());
        string native = f.Pins.Identify(gap); scope.Dispose();
        using (var retry = f.Open()) Assert.Equal("prior"u8.ToArray(), retry.Store.Open(f.Prior).Find("retained"));
        Assert.Equal(native, f.Pins.Identify(gap)); Assert.Equal(bytes, new FileInfo(gap).Length); Assert.False(File.Exists(gap + ".owner"));
        Assert.Contains(JournalDeltaStore.EnumeratePreservedReadOnlyReplays(f.Root.Path, f.Identity), row => row.Directory == scope.ScratchDirectory);
        f.AssertSource(before);
    }

    [Fact]
    public void NativeSourceAndScopeNamesCannotBeReplacedWhileTheirReaderFencesAreHeld()
    {
        RequireWindows(); using var f = new Fixture(); using var scope = f.Open();
        Assert.ThrowsAny<IOException>(() => Directory.Move(f.Source, f.Source + "-foreign"));
        Assert.ThrowsAny<IOException>(() => Directory.Move(scope.ScratchDirectory, scope.ScratchDirectory + "-foreign"));
    }

    [Fact]
    public void Child()
    {
        string? path = Environment.GetEnvironmentVariable("SPLINTER_READ_ONLY_REPLAY_CHILD");
        if (path is null) Assert.Skip("Subprocess-only read-only replay ownership actor.");
        var request = JsonSerializer.Deserialize<ActorRequest>(File.ReadAllText(path))!; var pins = new WindowsJournalFilePins();
        using var scope = JournalReadOnlyReplayScope.Create(Path.Combine(request.Root, "source"), request.Root, Identity(request.Root), pins);
        _ = scope.Store.Set(request.Prior, "child-private", "unpublished derived data"u8); scope.Store.Flush();
        var signal = new ActorSignal(request.Nonce, Environment.ProcessId, Process.GetCurrentProcess().StartTime.ToUniversalTime().Ticks,
            scope.ScratchDirectory, pins.IdentifyDirectory(scope.ScratchDirectory));
        string pending = Path.Combine(request.Root, "signal.pending");
        using (var output = new FileStream(pending, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
        { JsonSerializer.Serialize(output, signal); output.Flush(true); }
        File.Move(pending, Path.Combine(request.Root, "signal.json")); Thread.Sleep(Timeout.Infinite);
    }

    [Fact]
    public async Task ARealKilledReaderRecoversItsOwnedScratchWithoutWritingOrReplacingSourcePages()
    {
        RequireWindows(); using var f = new Fixture(); var before = f.SourceProof(); var ct = TestContext.Current.CancellationToken;
        var request = new ActorRequest(f.Root.Path, Guid.NewGuid().ToString("N"), f.Prior); string input = Path.Combine(f.Root.Path, "request.json");
        File.WriteAllText(input, JsonSerializer.Serialize(request));
        var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (string arg in new[] { typeof(JournalReadOnlyReplayScopeTests).Assembly.Location, "-method", "Splinter.Tests.JournalReadOnlyReplayScopeTests.Child" }) start.ArgumentList.Add(arg);
        start.Environment["SPLINTER_READ_ONLY_REPLAY_CHILD"] = input;
        using var child = Process.Start(start)!; long ticks = child.StartTime.ToUniversalTime().Ticks;
        var stdout = child.StandardOutput.ReadToEndAsync(ct); var stderr = child.StandardError.ReadToEndAsync(ct);
        try
        {
            string signalPath = Path.Combine(f.Root.Path, "signal.json"); long began = Stopwatch.GetTimestamp();
            while (!File.Exists(signalPath) && !child.HasExited && Stopwatch.GetElapsedTime(began).TotalSeconds < 30) await Task.Delay(20, ct);
            Assert.True(File.Exists(signalPath), child.HasExited ? await stdout + await stderr : "No reader scope signal.");
            var signal = JsonSerializer.Deserialize<ActorSignal>(File.ReadAllText(signalPath))!;
            Assert.Equal(request.Nonce, signal.Nonce); Assert.Equal(child.Id, signal.Pid); Assert.Equal(ticks, signal.Start);
            Assert.Equal(signal.NativeIdentity, f.Pins.IdentifyDirectory(signal.Directory));
            using (var concurrent = f.Open()) Assert.True(Directory.Exists(signal.Directory));
            child.Kill(); await child.WaitForExitAsync(ct);
            using (var resumed = f.Open())
            {
                Assert.False(Directory.Exists(signal.Directory)); Assert.Equal("prior"u8.ToArray(), resumed.Store.Open(f.Prior).Find("retained"));
            }
            Assert.Empty(Directory.EnumerateDirectories(f.Replays)); f.AssertSource(before);
        }
        finally
        {
            if (!child.HasExited && child.StartTime.ToUniversalTime().Ticks == ticks) { child.Kill(); await child.WaitForExitAsync(CancellationToken.None); }
            File.WriteAllText(Path.Combine(f.Root.Path, "stdout.log"), await stdout); File.WriteAllText(Path.Combine(f.Root.Path, "stderr.log"), await stderr);
        }
    }

    private static void RequireWindows() { if (!OperatingSystem.IsWindows()) Assert.Skip("Native read-only replay ownership requires Windows."); }
    private static OverlayIdentity Identity(string root) => new(3, "read-only-replay-test",
        new("fixed", "test-fixed", root, root, DateTimeOffset.UnixEpoch, true, true, "fixed"), long.MaxValue, 65536);
    private sealed class CountedResource : IDisposable { public int Disposals; public void Dispose() => Disposals++; }
    private sealed class Fixture : IDisposable
    {
        public TestDirectory Root { get; } = new();
        public WindowsJournalFilePins Pins { get; } = new();
        public string Source => Path.Combine(Root.Path, "source");
        public string Replays => Path.Combine(Root.Path, "read-only-replays");
        public MetadataTreeRoot Prior { get; }
        public OverlayIdentity Identity => JournalReadOnlyReplayScopeTests.Identity(Root.Path);
        public Fixture()
        {
            using var store = new PagedMetadataStore(Source, true, filePins: Pins);
            Prior = store.Set(store.Empty, "retained", "prior"u8); store.Flush();
        }
        public JournalReadOnlyReplayScope Open(Action<string>? boundary = null) => JournalReadOnlyReplayScope.Create(Source, Root.Path, Identity, Pins, boundary);
        public Dictionary<string, (string Identity, string Hash)> SourceProof() => Directory.EnumerateFiles(Source)
            .ToDictionary(path => Path.GetFileName(path)!, path => (Pins.Identify(path), Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)))));
        public void AssertSource(Dictionary<string, (string Identity, string Hash)> before)
        {
            var after = SourceProof(); Assert.Equal(before.Count, after.Count);
            foreach (var item in before) Assert.Equal(item.Value, after[item.Key]);
            using var source = new PagedMetadataStore(Source, filePins: Pins); Assert.Equal("prior"u8.ToArray(), source.Open(Prior).Find("retained"));
        }
        public void Dispose() => Root.Dispose();
    }
}
