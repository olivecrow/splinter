using Splinter.Platform.Windows;

namespace Splinter.Tests;

public sealed class OverlayRequestWakeupTests
{
    [Theory]
    [InlineData("source-query.json")]
    [InlineData("flush.json")]
    [InlineData("stop.json")]
    public async Task PublishedRequestsWakeTheReaderAndRetainExactFileContents(string name)
    {
        using var root = new TestDirectory();
        using var wakeup = new OverlayRequestWakeup(root.Path);
        var wait = wakeup.WaitAsync(TimeSpan.FromSeconds(5));
        string path = Path.Combine(root.Path, name);
        await WindowsProcessWorkspaceProvider.WriteAsync(path, new { token = "owned", requestId = "one" }, TestContext.Current.CancellationToken);
        Assert.True(await wait);
        Assert.Contains("owned", File.ReadAllText(path));
        // A notification is not a request. The reader can still observe a
        // later publication and apply its ordinary token/request-ID checks.
        using var replacementWakeup = new OverlayRequestWakeup(root.Path);
        var replacementWait = replacementWakeup.WaitAsync(TimeSpan.FromSeconds(5));
        await WindowsProcessWorkspaceProvider.WriteAsync(path, new { token = "foreign", requestId = "two" }, TestContext.Current.CancellationToken);
        Assert.True(await replacementWait);
        Assert.Contains("foreign", File.ReadAllText(path));
    }

    [Fact]
    public async Task StatusAndResponseWritesDoNotCreateAProviderFeedbackLoop()
    {
        using var root = new TestDirectory();
        using var wakeup = new OverlayRequestWakeup(root.Path);
        foreach (string name in new[] { "state.json", "source-response.json", "flush-result.json", "unrelated.json" })
            await WindowsProcessWorkspaceProvider.WriteAsync(Path.Combine(root.Path, name), new { value = 1 }, TestContext.Current.CancellationToken);
        Assert.False(await wakeup.WaitAsync(TimeSpan.FromMilliseconds(150)));
        Assert.False(await wakeup.WaitAsync(TimeSpan.FromMilliseconds(1))); // Polling remains possible without an event.
    }

    [Theory]
    [InlineData("io")]
    [InlineData("access")]
    [InlineData("native")]
    public async Task UnavailableNotificationsRetainTimedPolling(string failure)
    {
        using var root = new TestDirectory();
        using var wakeup = new OverlayRequestWakeup(root.Path, _ => throw failure switch
        {
            "io" => new IOException("Notification buffer unavailable"),
            "access" => new UnauthorizedAccessException("Notifications unavailable"),
            _ => new System.ComponentModel.Win32Exception(8)
        });
        string path = Path.Combine(root.Path, "source-query.json");
        await WindowsProcessWorkspaceProvider.WriteAsync(path, new { token = "owned" }, TestContext.Current.CancellationToken);
        Assert.False(await wakeup.WaitAsync(TimeSpan.FromMilliseconds(50)));
        Assert.Contains("owned", File.ReadAllText(path)); // The normal poll can still read the durable request.
    }
}
