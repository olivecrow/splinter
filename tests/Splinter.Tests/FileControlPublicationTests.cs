using System.Security.Cryptography;
using Splinter.Core;
using Splinter.SourceControl.Files;

namespace Splinter.Tests;

public sealed class FileControlPublicationTests
{
    [Fact]
    public void FixedControlStreamsBeyondTheFormerJsonLimitAndPreservesAnUnknownPartialAttempt()
    {
        using var files = new TestDirectory(); string path = Path.Combine(files.Path, "fixed.json"), partial = path + ".pending";
        File.WriteAllText(partial, "\"unpublished-prefix");
        string value = new('x', 16 * 1024 * 1024 + 1);
        FileWorkspaceProvider.SaveFixed(path, value);
        Assert.Equal(value, FileWorkspaceProvider.Read<string>(path)); Assert.Equal("\"unpublished-prefix", File.ReadAllText(partial));
        string Before() { using var input = File.OpenRead(path); return Convert.ToHexStringLower(SHA256.HashData(input)); }
        string original = Before(); FileWorkspaceProvider.SaveFixed(path, value); Assert.Equal(original, Before());
        Assert.Throws<WorkspaceException>(() => FileWorkspaceProvider.SaveFixed(path, value + "changed")); Assert.Equal(original, Before());
        Assert.Single(Directory.GetFiles(files.Path, "*.pending")); Assert.Empty(Directory.GetFiles(files.Path, "*.tmp"));
    }
}
