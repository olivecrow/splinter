namespace Splinter.Core;

// Optional development instrumentation. Production composition supplies none;
// an observer does not change ownership, recovery or persistence decisions.
public interface IWorkspacePreparationObserver
{
    Task ReachedAsync(string boundary, WorkspaceRecord workspace, CancellationToken ct);
}
