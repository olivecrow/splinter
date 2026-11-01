using Splinter.Core;
using Splinter.Platform.Windows;

namespace Splinter.Tests;

public sealed class WindowsJournalFilePinsLongPathTests
{
    [Fact]
    public void ExtendedOwnedAliasesPreserveTheExactBackingAndRemoveOnlyTheSelectedName()
    {
        RequireWindows(); using var files = new TestDirectory(); string directory = LongDirectory(files.Path);
        var pins = new WindowsJournalFilePins(); string source = Path.Combine(directory, "source.bin"), alias = Path.Combine(directory, "alias.bin");
        File.WriteAllBytes(source, "retained bytes"u8.ToArray()); string identity = pins.Identify(source);
        string directoryIdentity = pins.IdentifyDirectory(directory); Assert.NotEmpty(directoryIdentity);
        pins.Pin(source, alias, identity); Assert.Equal(identity, pins.Identify(alias)); Assert.Equal(2, pins.LinkCount(source));
        File.SetAttributes(alias, File.GetAttributes(alias) | FileAttributes.ReadOnly);
        pins.Unpin(alias, identity);
        Assert.False(File.Exists(alias)); Assert.Equal(identity, pins.Identify(source)); Assert.Equal(1, pins.LinkCount(source));
        Assert.Equal("retained bytes"u8.ToArray(), File.ReadAllBytes(source)); Assert.True((File.GetAttributes(source) & FileAttributes.ReadOnly) != 0);
    }

    [Fact]
    public void ALongAliasWithAnotherIdentityOrAnIndependentReaderIsPreserved()
    {
        RequireWindows(); using var files = new TestDirectory(); string directory = LongDirectory(files.Path);
        var pins = new WindowsJournalFilePins(); string source = Path.Combine(directory, "source.bin"), alias = Path.Combine(directory, "alias.bin"), foreign = Path.Combine(directory, "foreign.bin");
        File.WriteAllText(source, "equal bytes"); File.WriteAllText(foreign, "equal bytes"); string identity = pins.Identify(source);
        Assert.NotEqual(identity, pins.Identify(foreign));
        Assert.Equal("SourceCaptureBackingInvalid", Assert.Throws<WorkspaceException>(() => pins.Unpin(foreign, identity)).Code);
        Assert.Equal("equal bytes", File.ReadAllText(foreign));
        pins.Pin(source, alias, identity);
        using (var reader = new FileStream(alias, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            Assert.Equal("ImmutableContentReaderBusy", Assert.Throws<WorkspaceException>(() => pins.Unpin(alias, identity)).Code);
            Assert.Equal(identity, pins.Identify(alias)); Assert.Equal((int)'e', reader.ReadByte());
        }
        pins.Unpin(alias, identity); Assert.False(File.Exists(alias)); Assert.True(File.Exists(source)); Assert.True(File.Exists(foreign));
    }

    private static string LongDirectory(string root)
    {
        string directory = root;
        for (int i = 0; i < 6; i++) directory = Path.Combine(directory, new string((char)('a' + i), 80));
        Directory.CreateDirectory(directory); Assert.True(directory.Length > 500); return directory;
    }
    private static void RequireWindows()
    { if (!OperatingSystem.IsWindows()) Assert.Skip("Native extended-path ownership and share-delete guards require Windows."); }
}
