using Splinter.Core;
using Splinter.Runtime;
using Splinter.Platform.Windows;

namespace Splinter.Tests;

public sealed class ManagedDirectoryPreparationTests
{
    [Fact]
    public async Task FailedCaptureKeepsOriginalAndSameOwnedIntentCanRetry()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var directory = new TestDirectory();
        string project = Path.Combine(directory.Path, "project"), library = Path.Combine(project, "Library");
        Directory.CreateDirectory(library); string file = Path.Combine(library, "shared-log"); File.WriteAllBytes(file, [1, 2]);
        var store = new WorkspaceStore(Path.Combine(directory.Path, "home")); var manager = new ManagedDirectoryBasis(store);
        string id = WorkspaceStore.Key(WorkspaceStore.ProjectKey(project) + "\nLibrary\nfixed");
        using var writer = new FileStream(file, FileMode.Open, FileAccess.Write, FileShare.Read);
        for (int attempt = 0; attempt < 2; attempt++)
        {
            await Assert.ThrowsAsync<IOException>(() => manager.PrepareAsync(project, "fixed", "compatibility", TestContext.Current.CancellationToken));
            Assert.True(File.Exists(Path.Combine(store.Root, "library-bases", id, "prepare-intent.json")));
            Assert.True(File.Exists(file)); Assert.False(Directory.Exists(Path.Combine(store.Root, "library-bases", id, "base")));
        }
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExplicitUndoPreservesIntactCurrentDataBeforeOrAfterRename(bool renamed)
    {
        using var fixture = new Fixture(renamed);
        File.WriteAllBytes(Path.Combine(fixture.DataPath, "file"), [9, 8]);
        var restored = await fixture.Manager.RestoreAsync(fixture.State.Basis.Id, TestContext.Current.CancellationToken);
        Assert.Equal("restored", restored.Phase);
        Assert.Equal(new byte[] { 9, 8 }, File.ReadAllBytes(Path.Combine(fixture.State.OriginalPath, "file")));
        Assert.False(Directory.Exists(fixture.State.Basis.Snapshot.RootPath));
        Assert.Equal("restored", (await fixture.Manager.RestoreAsync(fixture.State.Basis.Id, TestContext.Current.CancellationToken)).Phase);
    }

    [Theory]
    [InlineData("launch")]
    [InlineData("foreign-identity")]
    [InlineData("payload")]
    [InlineData("both")]
    public async Task UncertainPreparationIsPreserved(string boundary)
    {
        using var fixture = new Fixture(true);
        string root = fixture.State.Basis.ReceiptDirectory;
        if (boundary == "launch") { Directory.CreateDirectory(Path.Combine(root, "overlay")); File.WriteAllText(Path.Combine(root, "overlay", "provider-launch-intent.json"), "preserve"); }
        if (boundary is "foreign-identity" or "payload")
        {
            var request = new OverlayServeRequest(new(2, fixture.State.Basis.Id, fixture.State.Basis.Snapshot, 1024 * 1024, 65536), Path.Combine(root, "delta"), fixture.State.OriginalPath,
                Path.Combine(root, "overlay"), "token", new(fixture.State.Basis.Id, fixture.State.Basis.Snapshot.RootPath, fixture.State.Basis.ManifestHash), root, fixture.State.Basis.Id);
            Directory.CreateDirectory(request.ControlPath); Directory.CreateDirectory(request.DeltaPath);
            await WindowsProcessWorkspaceProvider.WriteAsync(Path.Combine(request.ControlPath, "request.json"), request, TestContext.Current.CancellationToken);
            await WindowsProcessWorkspaceProvider.WriteAsync(Path.Combine(request.DeltaPath, "identity.json"), boundary == "foreign-identity" ? request.Identity with { Id = "foreign" } : request.Identity, TestContext.Current.CancellationToken);
            File.WriteAllBytes(Path.Combine(request.DeltaPath, "delta.journal"), boundary == "payload" ? [7] : []);
        }
        if (boundary == "both") { Directory.CreateDirectory(fixture.State.OriginalPath); File.WriteAllText(Path.Combine(fixture.State.OriginalPath, "foreign"), "preserve"); }
        await Assert.ThrowsAsync<WorkspaceException>(() => fixture.Manager.RestoreAsync(fixture.State.Basis.Id, TestContext.Current.CancellationToken));
        Assert.Equal(new byte[] { 1, 2 }, File.ReadAllBytes(Path.Combine(fixture.DataPath, "file")));
        Assert.Equal("preparing", (await fixture.Manager.GetAsync(fixture.State.Basis.Id, TestContext.Current.CancellationToken)).Phase);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly TestDirectory directory = new();
        public ManagedDirectoryState State { get; }
        public ManagedDirectoryBasis Manager { get; }
        public string DataPath { get; }
        public Fixture(bool renamed)
        {
            var store = new WorkspaceStore(Path.Combine(directory.Path, "home")); Manager = new(store);
            string project = Path.Combine(directory.Path, "project"), key = "fixed", id = WorkspaceStore.Key(WorkspaceStore.ProjectKey(project) + "\nLibrary\n" + key);
            string root = Path.Combine(store.Root, "library-bases", id), original = Path.Combine(project, "Library"), basis = Path.Combine(root, "base");
            Directory.CreateDirectory(project);
            Directory.CreateDirectory(Path.Combine(root, "dependents")); File.WriteAllBytes(Path.Combine(root, "readers.lock"), []);
            DataPath = renamed ? basis : original; Directory.CreateDirectory(DataPath); File.WriteAllBytes(Path.Combine(DataPath, "file"), [1, 2]);
            var snapshot = new SnapshotReference(id, OwnedDirectorySnapshotTree.ProviderName, basis, basis, DateTimeOffset.UnixEpoch, true, true, "stopped-original-owned-directory");
            State = new() { Key = key, OriginalPath = original, CompatibilityKey = "fixed", Basis = new(id, project, "Library", snapshot, root, id, new string('a', 64)) };
            WindowsProcessWorkspaceProvider.WriteAsync(Path.Combine(root, "state.json"), State, TestContext.Current.CancellationToken).GetAwaiter().GetResult();
        }
        public void Dispose() => directory.Dispose();
    }
}
