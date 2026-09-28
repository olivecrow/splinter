using System.IO.Pipes;
using System.Security.Principal;
using Splinter.Platform.Windows;

namespace Splinter.Tests;

public sealed class StorageAuthorityPipeTests
{
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
