using Splinter.Core;
using Splinter.Platform.Windows;

namespace Splinter.Tests;

public sealed class WindowsJournalDirectoryCreationTests
{
    [Fact]
    public void RetainedAtomicCreationPermitsChildrenButPreventsReplacementUntilItsExactFenceCloses()
    {
        if (!OperatingSystem.IsWindows()) Assert.Skip("Native atomic directory creation requires Windows.");
        using var files = new TestDirectory(); var pins = new WindowsJournalFilePins(); string parent = files.Path;
        for (int index = 0; index < 6; index++) parent = Path.Combine(parent, new string((char)('a' + index), 80));
        Directory.CreateDirectory(parent); Assert.True(parent.Length > 500);
        string scope = Path.Combine(parent, "scope"), metadata = Path.Combine(scope, "metadata"),
            moved = Path.Combine(parent, "moved"), foreign = Path.Combine(parent, "foreign");
        Directory.CreateDirectory(foreign); string foreignIdentity = pins.IdentifyDirectory(foreign);
        string scopeIdentity;
        using (pins.CreateDirectory(scope, out scopeIdentity))
        {
            Assert.Equal(scopeIdentity, pins.IdentifyDirectory(scope));
            using (pins.PinDirectory(scope, scopeIdentity)) { }
            using (pins.CreateDirectory(metadata, out string metadataIdentity))
            {
                Assert.Equal(metadataIdentity, pins.IdentifyDirectory(metadata));
                using (var output = new FileStream(Path.Combine(metadata, "child"), FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read))
                { output.WriteByte(7); Assert.NotEmpty(pins.IdentifyOpened(output)); }
                Assert.Equal(new byte[] { 7 }, File.ReadAllBytes(Path.Combine(metadata, "child")));
                Assert.ThrowsAny<IOException>(() => Directory.Move(metadata, Path.Combine(scope, "replaced")));
            }
            Assert.ThrowsAny<IOException>(() => Directory.Move(scope, moved));
            Assert.Equal("GeneratedRestoreConflict", Assert.Throws<WorkspaceException>(() => pins.CreateDirectory(scope, out _)).Code);
            Assert.Equal("SourceCaptureBackingInvalid", Assert.Throws<WorkspaceException>(() => pins.PinDirectory(foreign, scopeIdentity)).Code);
            Assert.Equal(foreignIdentity, pins.IdentifyDirectory(foreign));
        }
        Directory.Move(scope, moved);
        Assert.Equal(scopeIdentity, pins.IdentifyDirectory(moved));
        Assert.Equal(new byte[] { 7 }, File.ReadAllBytes(Path.Combine(moved, "metadata", "child")));
        Assert.Equal(foreignIdentity, pins.IdentifyDirectory(foreign));
    }
}
