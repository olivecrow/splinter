using Splinter.Application.Unity;
using Splinter.Core;

namespace Splinter.Tests;

public sealed class UnityProjectPolicyTests
{
    [Fact]
    public void LivePublicationRequiresFreshExactResultAndEditorCooperation()
    {
        using var d = new TestDirectory(); string project = Path.Combine(d.Path, "project");
        Directory.CreateDirectory(Path.Combine(project, "Temp")); File.WriteAllText(Path.Combine(project, "Temp", "UnityLockfile"), "owned by test Editor");
        var w = new WorkspaceRecord { Id = new string('a', 24), RequestKey = "live", ProjectPath = project, Path = project,
            SourceReference = "test", BaseReference = "test", ApplicationKind = "unity" };
        var generation = new WorkspaceResultGeneration(new string('b', 24), 1, "capture", "result", "revision", "revision", "reference", new string('c', 64), 1, DateTimeOffset.UtcNow);
        w.Results.Add(generation);
        var now = DateTimeOffset.UtcNow;
        var permit = new LiveOriginalEditorPermit(1, project, 123, 456, generation.Id, "cooperation", now, now, now.AddMinutes(1), true);
        var verifier = new EditorVerifier(); var policy = new UnityProjectPolicy(verifier);
        using var heldOriginal = new FileStream(Path.Combine(project, "Temp", "UnityLockfile"), FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        foreach (var invalid in new[] { permit with { ResultId = "different" }, permit with { ProjectPath = d.Path }, permit with { ImportsDeferred = false },
            permit with { SavedAt = now.AddMinutes(1) }, permit with { ExpiresAt = now.AddMinutes(-1) }, permit with { ExpiresAt = now.AddMinutes(6) } })
        {
            w.LiveOriginal = invalid;
            Assert.Equal("LiveOriginalPermitInvalid", Assert.Throws<WorkspaceException>(() => policy.AcquireOriginal(w, true)).Code);
        }
        Assert.Equal(0, verifier.Calls);
        w.LiveOriginal = permit; using (policy.AcquireOriginal(w, true)) { }
        Assert.Equal(1, verifier.Calls); Assert.Equal((project, 123, 456L), verifier.Last);
        Assert.Equal(permit.Identity, (permit with { ObservedAt = now.AddSeconds(1), ExpiresAt = now.AddMinutes(2) }).Identity);
        verifier.Refuse = true;
        Assert.Equal("LiveOriginalIdentityMismatch", Assert.Throws<WorkspaceException>(() => policy.AcquireOriginal(w, true)).Code);
        w.LiveOriginal = null; Assert.Throws<IOException>(() => policy.AcquireOriginal(w, true));
    }
    private sealed class EditorVerifier : IOriginalEditorIdentityVerifier
    {
        public int Calls; public bool Refuse; public (string, int, long) Last;
        public IDisposable Acquire(string project, int processId, long startTimeUtcTicks)
        {
            Calls++; Last = (project, processId, startTimeUtcTicks);
            if (Refuse) throw new WorkspaceException("LiveOriginalIdentityMismatch", "Injected exact Editor identity mismatch.");
            return new MemoryStream();
        }
    }
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
