using Splinter.Core;
using Splinter.SourceControl.Git;

namespace Splinter.Tests;

// The cases test journal deletion authority, not repeated repository creation.
// Share one real immutable Git basis, while each case owns fresh journal/control
// state and opens/verifies its own reader. No snapshots or receipts are forged.
public sealed class StoppedJournalBasisFixture : IAsyncLifetime
{
    private readonly TestDirectory root = new();
    private string Receipt => Path.Combine(root.Path, "snapshot");
    private bool attached;
    public string Source => Path.Combine(root.Path, "source");
    public string Revision { get; private set; } = "";
    public SnapshotReference Snapshot { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        if (!OperatingSystem.IsWindows()) return;
        Directory.CreateDirectory(Source);
        await TestDirectory.Git(Source, "init", "-b", "development");
        await TestDirectory.Git(Source, "config", "user.name", "Fixture");
        await TestDirectory.Git(Source, "config", "user.email", "fixture@example.invalid");
        File.WriteAllText(Path.Combine(Source, "file"), "base");
        await TestDirectory.Git(Source, "add", ".");
        await TestDirectory.Git(Source, "commit", "-m", "basis");
        Revision = await TestDirectory.Git(Source, "rev-parse", "HEAD");
        var provider = new GitObjectSnapshotProvider(Receipt, "task", Revision);
        Snapshot = await provider.CaptureAsync(new(Source, "task", Receipt), TestContext.Current.CancellationToken);
    }

    internal IDisposable Attach(string control)
    {
        if (attached) throw new InvalidOperationException("Shared basis cases must run sequentially.");
        string path = Path.Combine(control, "snapshot");
        // Move only the already-created test receipt directory. Reconnect uses
        // the same native files, owner and fixed commit; it creates no new ref.
        Directory.Move(Receipt, path); attached = true;
        return new Attachment(this, path);
    }

    private sealed class Attachment(StoppedJournalBasisFixture owner, string path) : IDisposable
    {
        public void Dispose()
        {
            Directory.Move(path, owner.Receipt);
            owner.attached = false;
        }
    }

    public ValueTask DisposeAsync()
    {
        root.Dispose();
        return ValueTask.CompletedTask;
    }
}
