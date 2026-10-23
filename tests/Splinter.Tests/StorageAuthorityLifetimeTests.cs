using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text.Json;
using Microsoft.Win32.SafeHandles;
using Splinter.Core;
using Splinter.Platform.Windows;
using Splinter.Runtime;

namespace Splinter.Tests;

public sealed class StorageAuthorityLifetimeTests
{
    [Theory]
    [InlineData("success")]
    [InlineData("failure")]
    [InlineData("cancellation")]
    public async Task RealServerIsUnpinnedWhileIdleAndBeforeEveryCompletedResponse(string outcome)
    {
        using var f = new Fixture();
        var environment = f.Load();
        AssertWritable(f.Root); // Startup inspection has already released every pin.
        var operations = new Operations(outcome);
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        stop.CancelAfter(TimeSpan.FromSeconds(20));
        string pipe = "Splinter.lifetime." + Guid.NewGuid().ToString("N");
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var server = StorageAuthorityServer.ServeAsync(environment, pipe, operations, () => ready.SetResult(), stop.Token);
        try
        {
            await ready.Task.WaitAsync(stop.Token);
            AssertWritable(f.Root);
            var response = Exchange(pipe, f.Request, stop.Token);
            await operations.Entered.Task.WaitAsync(stop.Token);
            Assert.Equal(32, WriteAccess(f.Root));
            Assert.Throws<IOException>(() => Directory.Move(f.Install, f.Install + "-moved"));
            Assert.Throws<IOException>(() => Directory.Move(f.Source, f.Source + "-moved"));
            Assert.Equal(32, OwnedDirectoryLifetimeTests.TrySetJunction(f.Install, f.Source));
            operations.Proceed.SetResult();
            Assert.Equal(outcome == "success" ? null : outcome == "failure" ? "SnapshotOperationIncomplete" : "StorageOperationUncertain",
                (await response).ErrorCode);
            AssertWritable(f.Root);
            // Rejected requests must also release the newly acquired environment.
            Assert.Equal("StorageServiceVersionMismatch", (await Exchange(pipe, f.Request with { Version = 0 }, stop.Token)).ErrorCode);
            AssertWritable(f.Root);
            if (outcome == "success")
            {
                Assert.Null((await Exchange(pipe, f.Request, stop.Token)).ErrorCode);
                Assert.Equal(1, operations.Calls); // Completed-response replay keeps its original contract.
                AssertWritable(f.Root);
            }
        }
        finally { await StopServer(stop, server); }
        AssertWritable(f.Root);
    }

    [Fact]
    public async Task IncompleteInputDoesNotPinPathsAndServiceCancellationReleasesAnActiveOperation()
    {
        using var f = new Fixture(); var operations = new Operations("success");
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        stop.CancelAfter(TimeSpan.FromSeconds(20));
        string pipe = "Splinter.lifetime." + Guid.NewGuid().ToString("N");
        var server = StorageAuthorityServer.ServeAsync(f.Load(), pipe, operations, () => { }, stop.Token);
        try
        {
            using (var client = Client(pipe))
            {
                await client.ConnectAsync(stop.Token);
                await client.WriteAsync(new byte[] { 1, 0 }, stop.Token);
                AssertWritable(f.Root);
                var failure = StorageAuthorityProtocol.Decode<StorageAuthorityResponse>(await StorageAuthorityServer.ReadFrameAsync(client,
                    StorageAuthorityProtocol.MaxResponseBytes, stop.Token));
                Assert.Equal("StorageOperationUncertain", failure.ErrorCode);
                AssertWritable(f.Root);
            }
            using var active = Client(pipe);
            await active.ConnectAsync(stop.Token);
            await StorageAuthorityServer.WriteFrameAsync(active, JsonSerializer.SerializeToUtf8Bytes(f.Request, StorageAuthorityProtocol.Json), stop.Token);
            await operations.Entered.Task.WaitAsync(stop.Token);
            Assert.Equal(32, WriteAccess(f.Root));
        }
        finally { await StopServer(stop, server); }
        AssertWritable(f.Root);
    }

    [Theory]
    [InlineData("ancestor")]
    [InlineData("directory")]
    [InlineData("configuration-file")]
    [InlineData("configuration-content")]
    [InlineData("powershell")]
    [InlineData("acl")]
    public void IdleSubstitutionIsRejectedAndFailedAdmissionLeavesNoPins(string change)
    {
        using var f = new Fixture(); var environment = f.Load(); AssertWritable(f.Root);
        switch (change)
        {
            case "ancestor":
                Directory.Move(f.Ancestor, f.Ancestor + "-old"); Directory.CreateDirectory(f.Ancestor);
                foreach (string name in new[] { "install", "state", "powershell" })
                    Directory.Move(Path.Combine(f.Ancestor + "-old", name), Path.Combine(f.Ancestor, name));
                break;
            case "directory": Directory.Move(f.Install, f.Install + "-old"); Directory.CreateDirectory(f.Install); break;
            case "configuration-file":
                File.Move(f.ConfigurationPath, f.ConfigurationPath + "-old"); File.Copy(f.ConfigurationPath + "-old", f.ConfigurationPath); break;
            case "configuration-content":
                var config = environment.Configuration with { Grants = [environment.Configuration.Grants[0] with { HomeIdentity = "different" }] };
                File.WriteAllText(f.ConfigurationPath, JsonSerializer.Serialize(config, StorageAuthorityProtocol.Json)); break;
            case "powershell": File.Move(f.PowerShell, f.PowerShell + "-old"); File.WriteAllText(f.PowerShell, "replacement"); break;
            case "acl": f.UnsafeAcl = true; break;
        }
        Assert.Equal("StorageServiceUnsafe", Assert.Throws<WorkspaceException>(() => environment.Acquire()).Code);
        AssertWritable(f.Root);
    }

    [Fact]
    public void IdleReparseSubstitutionAndInvalidStartupBothReleaseTheirPartialPins()
    {
        using var f = new Fixture(); var environment = f.Load();
        Directory.Move(f.Install, f.Install + "-old"); Directory.CreateDirectory(f.Install);
        OwnedDirectoryLifetimeTests.SetJunction(f.Install, f.Install + "-old");
        try { Assert.Equal("StoragePathRejected", Assert.Throws<WorkspaceException>(() => environment.Acquire()).Code); }
        finally { Directory.Delete(f.Install); Directory.Move(f.Install + "-old", f.Install); }
        AssertWritable(f.Root);
        File.WriteAllText(f.ConfigurationPath, "{broken");
        Assert.ThrowsAny<JsonException>(() => f.Load()); AssertWritable(f.Root);
    }

    private static NamedPipeClientStream Client(string pipe)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
        return new(".", pipe, PipeAccessRights.ReadWrite | PipeAccessRights.Synchronize,
            PipeOptions.Asynchronous, TokenImpersonationLevel.Impersonation, HandleInheritability.None);
    }
    private static async Task StopServer(CancellationTokenSource stop, Task server)
    {
        stop.Cancel();
        // Cancellation at the loop condition returns normally; cancellation in
        // an outstanding accept/operation throws. Both must finish disposing.
        try { await server; }
        catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
    }
    private static async Task<StorageAuthorityResponse> Exchange(string pipe, StorageAuthorityRequest request, CancellationToken ct)
    {
        using var client = Client(pipe); await client.ConnectAsync(ct);
        await StorageAuthorityServer.WriteFrameAsync(client, JsonSerializer.SerializeToUtf8Bytes(request, StorageAuthorityProtocol.Json), ct);
        return StorageAuthorityProtocol.Decode<StorageAuthorityResponse>(await StorageAuthorityServer.ReadFrameAsync(client,
            StorageAuthorityProtocol.MaxResponseBytes, ct));
    }
    private static void AssertWritable(string path) => Assert.Equal(0, WriteAccess(path));
    private static int WriteAccess(string path)
    {
        // CBS-equivalent access, OPEN_EXISTING only: no write/delete/rename occurs.
        // Use our private ancestor so unrelated services pinning C:\ cannot mask a regression.
        using var handle = CreateFileW(path, 0x12019F, 7, 0, 3, 0x02200000, 0);
        return handle.IsInvalid ? Marshal.GetLastWin32Error() : 0;
    }
    [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(string path, uint access, uint share, nint security, uint disposition, uint flags, nint template);

    private sealed class Operations(string outcome) : IStorageAuthorityOperations
    {
        internal readonly TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource Proceed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal int Calls;
        public async Task<string?> ExecuteAsync(string operation, string receipts, string source, string owner, CancellationToken ct)
        {
            Calls++; Entered.TrySetResult(); await Proceed.Task.WaitAsync(ct);
            if (outcome == "cancellation") throw new OperationCanceledException();
            var snapshot = new SnapshotReference(Guid.NewGuid().ToString("B"), "fake", source, source,
                DateTimeOffset.UtcNow, true, true, "fixture");
            File.WriteAllText(Path.Combine(receipts, "snapshot.json"), JsonSerializer.Serialize(new { ownerId = owner, snapshot }, StorageAuthorityProtocol.Json));
            return outcome == "failure" ? "SnapshotOperationIncomplete" : null;
        }
    }
    private sealed class Fixture : IDisposable
    {
        private readonly TestDirectory directory;
        internal string Root => directory.Path;
        internal string Ancestor => Path.Combine(Root, "ancestor");
        internal string Install => Path.Combine(Ancestor, "install");
        internal string State => Path.Combine(Ancestor, "state");
        internal string PowerShell => Path.Combine(Ancestor, "powershell", "pwsh.exe");
        internal string ConfigurationPath => Path.Combine(State, "configuration.json");
        internal string Source => Path.Combine(Root, "source");
        internal bool UnsafeAcl;
        internal StorageAuthorityRequest Request { get; }
        internal Fixture()
        {
            if (!OperatingSystem.IsWindows()) { Assert.Skip("Windows native directory sharing and real named pipes."); throw new PlatformNotSupportedException(); }
            directory = new TestDirectory();
            Directory.CreateDirectory(Install); Directory.CreateDirectory(State); Directory.CreateDirectory(Path.GetDirectoryName(PowerShell)!);
            File.WriteAllText(PowerShell, "fixture only; never executed"); Directory.CreateDirectory(Source);
            var store = new WorkspaceStore(Path.Combine(Root, "home"));
            string owner = WorkspaceStore.Key("authority-lifetime"), id = Guid.NewGuid().ToString("N");
            Request = new(1, id, "Capture", owner, Source, store.Root, null,
                new(id, owner, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(-1)), null);
            var workspace = new WorkspaceRecord { Id = owner, RequestKey = "authority-lifetime", ProjectPath = Source,
                Path = store.WorkPath(owner), SourceReference = "splinter/test", BaseReference = "development",
                StorageMode = "ntfs-snapshot", Snapshot = new() { Step = "capture-intent" } };
            store.Save(workspace, "capture-intent"); Directory.CreateDirectory(Path.Combine(store.ControlPath(owner), "snapshot"));
            using var identity = WindowsIdentity.GetCurrent();
            using var paths = new StorageAuthorityPaths();
            var grant = new StorageGrant(identity.User!.Value, Source, store.Root, StorageAuthorityPaths.Volume(Source), paths.Pin(Source), paths.Pin(store.Root));
            File.WriteAllText(ConfigurationPath, JsonSerializer.Serialize(new StorageAuthorityConfiguration(1, PowerShell, [grant]), StorageAuthorityProtocol.Json));
        }
        internal StorageAuthorityEnvironment Load() => StorageAuthorityEnvironment.Load(Install, State, PowerShell, (_, _) =>
        {
            if (UnsafeAcl) throw StorageAuthorityProtocol.Error("StorageServiceUnsafe", "Fixture ACL changed.");
        });
        public void Dispose() => directory.Dispose();
    }
}
