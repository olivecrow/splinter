using System.Text.Json;
using System.Diagnostics;
using Splinter.Core;
using Splinter.Platform.Windows;
using Splinter.Runtime;

namespace Splinter.Tests;

public sealed class ControlFilePublicationTests
{
    [Fact]
    public async Task ExcludedPublisherAbsenceDoesNotHideAnActualSharingConflict()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Requires native file sharing semantics.");
        using var files = new TestDirectory(); var ct = TestContext.Current.CancellationToken;
        string path = Path.Combine(files.Path, "request.json");
        Assert.Null(await WindowsProcessWorkspaceProvider.ReadAsync<Receipt>(path, ct, waitForPublication: false));
        var expected = new Receipt(1, "complete");
        await WindowsProcessWorkspaceProvider.WriteAsync(path, expected, ct);
        using var held = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);
        var reading = WindowsProcessWorkspaceProvider.ReadAsync<Receipt>(path, ct, waitForPublication: false);
        try { await Task.Delay(100, ct); Assert.False(reading.IsCompleted); }
        finally { held.Dispose(); }
        Assert.Equal(expected, await reading.WaitAsync(TimeSpan.FromSeconds(2), ct));
    }

    [Fact]
    public async Task BriefReaderWithoutDeleteSharingDoesNotFailLiveStatePublication()
    {
        if (!OperatingSystem.IsWindows()) Assert.Skip("Windows file sharing or native contract.");
        using var directory = new TestDirectory(); var ct = TestContext.Current.CancellationToken;
        string path = Path.Combine(directory.Path, "state.json");
        await WindowsProcessWorkspaceProvider.WriteAsync(path, new Receipt(1, "before"), ct);
        Task publication;
        using (var reader = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
            publication = WindowsProcessWorkspaceProvider.WriteAsync(path, new Receipt(2, "after"), ct);
            await Task.Delay(200, ct);
            Assert.False(publication.IsCompleted);
        }
        await publication;
        Assert.Contains("after", await File.ReadAllTextAsync(path, ct));
        Assert.Empty(Directory.EnumerateFiles(directory.Path, "*.tmp"));
    }

    [Fact]
    public async Task PublishWhilePollingReaderKeepsOldAndNewReceiptsComplete()
    {
        using var directory = new TestDirectory();
        string path = Path.Combine(directory.Path, "flush-result.json");
        var ct = TestContext.Current.CancellationToken;
        await WindowsProcessWorkspaceProvider.WriteAsync(path, new Receipt(0, new string('a', 8192)), ct);
        for (int sequence = 1; sequence <= 100; sequence++)
        {
            await using var oldReader = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var next = new Receipt(sequence, new string((char)('a' + sequence % 26), 8192));
            await WindowsProcessWorkspaceProvider.WriteAsync(path, next, ct);
            var old = await JsonSerializer.DeserializeAsync<Receipt>(oldReader, WindowsProcessWorkspaceProvider.Json, ct);
            Assert.Equal(sequence - 1, old!.Sequence);
            Assert.Equal(new string((char)('a' + (sequence - 1) % 26), 8192), old.Body);
            Assert.Equal(next, await WindowsProcessWorkspaceProvider.ReadAsync<Receipt>(path, ct));
        }
        Assert.Empty(Directory.GetFiles(directory.Path, "*.tmp"));
    }

    [Fact]
    public async Task DeniedPublicationPreservesOldReceiptAndFlushedTemporary()
    {
        if (!OperatingSystem.IsWindows()) Assert.Skip("Windows file sharing or native contract.");
        using var directory = new TestDirectory();
        string path = Path.Combine(directory.Path, "flush-result.json");
        var ct = TestContext.Current.CancellationToken;
        var previous = new Receipt(1, "previous");
        var next = new Receipt(2, "next");
        await WindowsProcessWorkspaceProvider.WriteAsync(path, previous, ct);
        await using (var held = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
            var error = await Assert.ThrowsAsync<IOException>(() => WindowsProcessWorkspaceProvider.WriteAsync(path, next, ct));
            Assert.Equal(32, error.HResult & 0xffff);
            Assert.Equal(path, error.Data["publicationPath"]);
            string temporary = Assert.Single(Directory.GetFiles(directory.Path, "*.tmp"));
            Assert.Equal(temporary, error.Data["publicationTemporaryPath"]);
            Assert.Equal(previous, await WindowsProcessWorkspaceProvider.ReadAsync<Receipt>(path, ct));
            Assert.Equal(next, await WindowsProcessWorkspaceProvider.ReadAsync<Receipt>(temporary, ct));
        }
        // Restoring access permits a new receipt; it does not silently reuse a
        // prior request or remove the retained evidence of the failed request.
        await WindowsProcessWorkspaceProvider.WriteAsync(path, new Receipt(3, "retry"), ct);
        Assert.Equal(3, (await WindowsProcessWorkspaceProvider.ReadAsync<Receipt>(path, ct))!.Sequence);
        Assert.Single(Directory.GetFiles(directory.Path, "*.tmp"));
    }

    [Fact]
    public async Task ReadOnlyReceiptIsNotMadeWritableOrOverwritten()
    {
        if (!OperatingSystem.IsWindows()) Assert.Skip("Windows file sharing or native contract.");
        using var directory = new TestDirectory();
        string path = Path.Combine(directory.Path, "state.json");
        var ct = TestContext.Current.CancellationToken;
        await WindowsProcessWorkspaceProvider.WriteAsync(path, new Receipt(1, "previous"), ct);
        File.SetAttributes(path, FileAttributes.ReadOnly);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => WindowsProcessWorkspaceProvider.WriteAsync(path, new Receipt(2, "next"), ct));
        Assert.True((File.GetAttributes(path) & FileAttributes.ReadOnly) != 0);
        Assert.Equal(1, (await WindowsProcessWorkspaceProvider.ReadAsync<Receipt>(path, ct))!.Sequence);
        Assert.Single(Directory.GetFiles(directory.Path, "*.tmp"));
    }

    [Fact]
    public void SynchronousStatePublicationAlsoAllowsAnOpenReader()
    {
        using var directory = new TestDirectory();
        var store = new WorkspaceStore(directory.Path);
        string id = WorkspaceStore.Key("polling-reader");
        var record = new WorkspaceRecord { Id = id, RequestKey = "task", ProjectPath = "source", Path = store.WorkPath(id),
            SourceReference = "task", BaseReference = "development", ResultRevision = "old" };
        store.Save(record, "initial");
        string path = Path.Combine(store.ControlPath(id), "workspace.json");
        using var held = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        record.ResultRevision = "new";
        store.Save(record, "updated");
        Assert.Equal("new", new WorkspaceStore(directory.Path).Get(id).ResultRevision);
        var previous = JsonSerializer.Deserialize<WorkspaceRecord>(held, WindowsProcessWorkspaceProvider.Json);
        Assert.Equal("old", previous!.ResultRevision);
        Assert.Empty(Directory.GetFiles(store.ControlPath(id), "*.tmp"));
    }

    private sealed record Receipt(int Sequence, string Body);
}
