using System.Diagnostics;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using Splinter.Core;
using Splinter.Platform.Windows;

namespace Splinter.Tests;

public sealed class StorageAuthorityAclTests
{
    [Theory]
    [InlineData("S-1-5-32-545", false)]
    [InlineData("S-1-5-21-111-222-333-1002", false)]
    [InlineData("S-1-5-18", true)]
    [InlineData("S-1-5-32-544", true)]
    [InlineData("S-1-3-0", true)]
    public async Task RuntimeAndInstallerRejectUntrustedChildWrites(string sid, bool trusted)
    {
        if (!OperatingSystem.IsWindows()) { Assert.Skip("Windows ACL classification."); return; }
        var acl = new DirectorySecurity(); acl.SetOwner(new SecurityIdentifier("S-1-5-32-544"));
        acl.AddAccessRule(new(new SecurityIdentifier(sid), FileSystemRights.Modify,
            InheritanceFlags.ObjectInherit | InheritanceFlags.ContainerInherit, PropagationFlags.InheritOnly, AccessControlType.Allow));
        if (trusted) StorageAuthorityPaths.RequireProtectedAcl(acl);
        else Assert.Equal("StorageServiceUnsafe", Assert.Throws<WorkspaceException>(() => StorageAuthorityPaths.RequireProtectedAcl(acl)).Code);
        using var d = new TestDirectory(); string script = Path.Combine(d.Path, "check.ps1");
        // Execute the installer's actual function with a synthetic ACL. No
        // administrator-owned directory or installed service is modified.
        File.WriteAllText(script, """
            param($Source, $Sddl, $SnapshotSource, $Directory)
            $ErrorActionPreference = 'Stop'
            $tokens = $null; $errors = $null
            $ast = [Management.Automation.Language.Parser]::ParseFile($Source, [ref]$tokens, [ref]$errors)
            $function = $ast.Find({param($n) $n -is [Management.Automation.Language.FunctionDefinitionAst] -and $n.Name -eq 'Assert-ProtectedEntry'}, $true)
            . ([scriptblock]::Create($function.Extent.Text))
            $script:TestAcl = [Security.AccessControl.DirectorySecurity]::new()
            $script:TestAcl.SetSecurityDescriptorSddlForm($Sddl)
            function Get-Acl { param($LiteralPath) return $script:TestAcl }
            $installerDenied = $false
            try { Assert-ProtectedEntry 'synthetic-authority-path' } catch { $installerDenied = $true }
            $ast = [Management.Automation.Language.Parser]::ParseFile($SnapshotSource, [ref]$tokens, [ref]$errors)
            $function = $ast.Find({param($n) $n -is [Management.Automation.Language.FunctionDefinitionAst] -and $n.Name -eq 'Open-SnapshotReleaseLock'}, $true)
            . ([scriptblock]::Create($function.Extent.Text))
            $lock = $null
            try { $lock = Open-SnapshotReleaseLock $Directory }
            catch {
                if ($installerDenied -and $_.Exception.Message.Contains('writable release lock')) { throw 'writable by another principal' }
                throw
            }
            try {
                if ($installerDenied) { throw 'Installer and release protection differ.' }
                try { $second = Open-SnapshotReleaseLock $Directory; $second.Dispose(); throw 'The release lock was shared.' }
                catch { if (-not $_.Exception.Message.StartsWith('WorkspaceBusy:')) { throw } }
            } finally { $lock.Dispose() }
            $next = Open-SnapshotReleaseLock $Directory; $next.Dispose()
            if (-not (Test-Path -LiteralPath (Join-Path $Directory 'vss-release.lock'))) { throw 'The lock file was unexpectedly deleted.' }
            """, new UTF8Encoding(false));
        var info = new ProcessStartInfo("pwsh") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        foreach (string arg in new[] { "-NoProfile", "-NonInteractive", "-File", script,
            Path.Combine(AppContext.BaseDirectory, "Scripts", "setup-storage-service.ps1"), acl.GetSecurityDescriptorSddlForm(AccessControlSections.All),
            Path.Combine(AppContext.BaseDirectory, "Scripts", "vss-snapshot.ps1"), d.Path }) info.ArgumentList.Add(arg);
        using var process = Process.Start(info)!;
        var output = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        var error = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken); timeout.CancelAfter(TimeSpan.FromSeconds(20));
        try { await process.WaitForExitAsync(timeout.Token); }
        finally { if (!process.HasExited) { process.Kill(true); await process.WaitForExitAsync(CancellationToken.None); } }
        string diagnostic = await error; await output;
        Assert.Equal(trusted, process.ExitCode == 0);
        if (!trusted) Assert.Contains("writable by another principal", diagnostic);
    }
}
