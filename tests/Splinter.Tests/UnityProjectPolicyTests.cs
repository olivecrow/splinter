using Splinter.Application.Unity;
using Splinter.Core;

namespace Splinter.Tests;

public sealed class UnityProjectPolicyTests
{
    [Fact]
    public void PublicationReservesOnlyOwnedLockAndPreservesForeignTemporaryFiles()
    {
        using var d = new TestDirectory(); string project = Path.Combine(d.Path, "project"); Directory.CreateDirectory(project);
        string temp = Path.Combine(project, "Temp"), file = Path.Combine(temp, "UnityLockfile");
        Assert.Null(UnityProjectPolicy.Instance.AcquireOriginal(project)); Assert.False(Directory.Exists(temp));
        using (UnityProjectPolicy.Instance.AcquireOriginal(project, true))
        {
            Assert.True(File.Exists(file));
            Assert.Throws<IOException>(() => UnityProjectPolicy.Instance.AcquireOriginal(project));
            File.WriteAllText(Path.Combine(temp, "foreign.txt"), "retain");
        }
        Assert.False(File.Exists(file)); Assert.Equal("retain", File.ReadAllText(Path.Combine(temp, "foreign.txt")));
        File.Delete(Path.Combine(temp, "foreign.txt")); Directory.Delete(temp);
        using (UnityProjectPolicy.Instance.AcquireOriginal(project, true)) { }
        Assert.False(Directory.Exists(temp));
    }
    [Fact]
    public void PlainPolicyDoesNotExpandAssetMetadataAndUnknownApplicationHasNoFallback()
    {
        string[] selected = ["Assets/item.txt"];
        var plain = NoApplicationProjectPolicy.Instance.CloseSelection(selected, _ => true, _ => true);
        Assert.Equal(selected, plain.Paths); Assert.Empty(plain.Companions);
        Assert.Equal("ApplicationPolicyUnsupported", Assert.Throws<WorkspaceException>(() => UnityProjectPolicy.Policies.Get("blender")).Code);
        Assert.Throws<ArgumentException>(() => new ApplicationProjectPolicies(UnityProjectPolicy.Instance, UnityProjectPolicy.Instance));
    }
}
