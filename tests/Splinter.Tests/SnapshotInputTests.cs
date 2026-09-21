using System.Runtime.InteropServices;
using Splinter.Core;
using Splinter.Platform.Windows;

namespace Splinter.Tests;

public sealed class SnapshotInputTests
{
    [Fact]
    public void MetadataProbeRejectsAlternateStreamsAndHardLinksWithoutChangingFiles()
    {
        if (!OperatingSystem.IsWindows()) Assert.Skip("NTFS metadata admission.");
        var ct = TestContext.Current.CancellationToken;
        using var d = new TestDirectory();
        string file = Path.Combine(d.Path, "source.bin"); File.WriteAllText(file, "original");
        Assert.Equal(1, SnapshotInputChecks.VerifyTree(d.Path, ct));
        File.WriteAllText(file + ":private", "stream");
        Assert.Equal("SnapshotFileUnsupported", Assert.Throws<WorkspaceException>(() => SnapshotInputChecks.VerifyTree(d.Path, ct)).Code);
        Assert.Equal("stream", File.ReadAllText(file + ":private")); File.Delete(file + ":private");
        string link = Path.Combine(d.Path, "linked.bin");
        Assert.True(CreateHardLinkW(link, file, 0), "CreateHardLinkW error " + Marshal.GetLastWin32Error());
        Assert.Equal("SnapshotFileUnsupported", Assert.Throws<WorkspaceException>(() => SnapshotInputChecks.VerifyTree(d.Path, ct)).Code);
        Assert.Equal("original", File.ReadAllText(file)); Assert.Equal("original", File.ReadAllText(link));
    }

    [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool CreateHardLinkW(string link, string existing, nint security);
}
