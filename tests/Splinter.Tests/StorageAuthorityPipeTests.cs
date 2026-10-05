using System.IO.Pipes;
using System.Buffers.Binary;
using System.Security.Principal;
using Splinter.Platform.Windows;

namespace Splinter.Tests;

public sealed class StorageAuthorityPipeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AnIncompleteHeaderOrBodyExpiresAndTheNextClientCanProceed(bool body)
    {
        if (!OperatingSystem.IsWindows()) { Assert.Skip("Windows pipe receive deadline."); return; }
        string name = "Splinter.test." + Guid.NewGuid().ToString("N");
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        limit.CancelAfter(TimeSpan.FromSeconds(10));
        NamedPipeServerStream Server() => new(name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly, 4096, 4096);
        using (var server = Server())
        using (var stalled = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous))
        {
            var accept = server.WaitForConnectionAsync(limit.Token);
            await stalled.ConnectAsync(limit.Token); await accept;
            byte[] partial = body ? [8, 0, 0, 0, 1, 2] : [8, 0];
            var receive = StorageAuthorityServer.ReadRequestAsync(server, limit.Token);
            await stalled.WriteAsync(partial, limit.Token);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => receive);
            Assert.False(limit.IsCancellationRequested);
        }
        using var next = Server();
        using var client = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous);
        var connected = next.WaitForConnectionAsync(limit.Token);
        await client.ConnectAsync(limit.Token); await connected;
        byte[] payload = [42, 43]; byte[] frame = new byte[6];
        BinaryPrimitives.WriteInt32LittleEndian(frame, payload.Length); payload.CopyTo(frame, 4);
        var response = StorageAuthorityServer.ReadRequestAsync(next, limit.Token);
        await client.WriteAsync(frame, limit.Token);
        Assert.Equal(payload, await response);
    }
    [Fact]
    public async Task RealPipeAuthenticatesTheTokenAndDoesNotGrantServerCreation()
    {
        if (!OperatingSystem.IsWindows()) { Assert.Skip("Windows named-pipe token test."); return; }
        using var identity = WindowsIdentity.GetCurrent(); string sid = identity.User!.Value;
        string name = "Splinter.test." + Guid.NewGuid().ToString("N");
        var security = StorageAuthorityServer.PipeSecurityFor([sid]);
        using var server = NamedPipeServerStreamAcl.Create(name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous | PipeOptions.FirstPipeInstance, 4096, 4096, security);
        using var ct = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken); ct.CancelAfter(TimeSpan.FromSeconds(10));
        var accept = server.WaitForConnectionAsync(ct.Token);
        using var client = new NamedPipeClientStream(".", name, PipeAccessRights.ReadWrite | PipeAccessRights.Synchronize,
            PipeOptions.Asynchronous, TokenImpersonationLevel.Impersonation, HandleInheritability.None);
        await client.ConnectAsync(ct.Token); await accept;
        await client.WriteAsync(new byte[] { 42 }, ct.Token);
        var value = new byte[1]; await server.ReadExactlyAsync(value, ct.Token);
        string? peer = null;
        server.RunAsClient(() => { if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
            using var token = WindowsIdentity.GetCurrent(true); peer = token!.User!.Value; });
        Assert.Equal(sid, peer); Assert.Equal(42, value[0]);
        Assert.Throws<UnauthorizedAccessException>(() => {
            if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
            return NamedPipeServerStreamAcl.Create(name, PipeDirection.InOut, 1,
                PipeTransmissionMode.Byte, PipeOptions.FirstPipeInstance, 4096, 4096, security);
        });
    }

    [Fact]
    public async Task AUserOutsideTheInstalledAclCannotOpenThePipe()
    {
        if (!OperatingSystem.IsWindows()) { Assert.Skip("Windows pipe ACL test."); return; }
        string name = "Splinter.test." + Guid.NewGuid().ToString("N");
        using var server = NamedPipeServerStreamAcl.Create(name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous | PipeOptions.FirstPipeInstance, 4096, 4096,
            StorageAuthorityServer.PipeSecurityFor(["S-1-5-21-111-222-333-1002"]));
        using var client = new NamedPipeClientStream(".", name, PipeAccessRights.ReadWrite | PipeAccessRights.Synchronize,
            PipeOptions.Asynchronous, TokenImpersonationLevel.Impersonation, HandleInheritability.None);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => client.ConnectAsync(1000, TestContext.Current.CancellationToken));
    }
}
