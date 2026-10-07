namespace Splinter.Platform.Windows;

// File notifications are only wake-up hints. The provider still reads the
// published request, checks its token/request ID and polls after missed hints.
internal sealed class OverlayRequestWakeup : IDisposable
{
    private readonly SemaphoreSlim pending = new(0, 1);
    private readonly FileSystemWatcher? watcher;

    internal OverlayRequestWakeup(string control) : this(control, static watcher => watcher.EnableRaisingEvents = true) { }

    internal OverlayRequestWakeup(string control, Action<FileSystemWatcher> startWatching)
    {
        FileSystemWatcher? candidate = null;
        try
        {
            candidate = new(control, "*.json") { NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite };
            candidate.Created += Changed; candidate.Changed += Changed; candidate.Renamed += Changed;
            candidate.Error += (_, _) => Signal();
            startWatching(candidate);
            watcher = candidate;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            // Notifications are optional. Keep the timed poll if the OS cannot
            // start them; ordinary request reads still enforce access/identity.
            candidate?.Dispose();
        }
    }

    private void Changed(object sender, FileSystemEventArgs change)
    {
        if (change.Name is "stop.json" or "flush.json" or "source-query.json") Signal();
    }
    private void Signal()
    {
        try { pending.Release(); }
        catch (SemaphoreFullException) { } // A pending wake already covers this hint.
        catch (ObjectDisposedException) { } // An in-flight notification during shutdown.
    }
    internal Task<bool> WaitAsync(TimeSpan maximum) => pending.WaitAsync(maximum);
    public void Dispose() { watcher?.Dispose(); pending.Dispose(); }
}
