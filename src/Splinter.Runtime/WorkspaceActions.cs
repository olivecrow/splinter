using Splinter.Core;

namespace Splinter.Runtime;

// One projection owns the exact request arguments used by CLI and failure recovery.
public static class WorkspaceActions
{
    public static WorkspaceRecoveryAction Ensure(WorkspaceRecord w, string home, bool editor = false) => new("workspace.ensure", new
    {
        project = w.ProjectPath, key = w.RequestKey, baseReference = w.BaseReference,
        source = w.SourceKind, application = w.ApplicationKind, storage = w.StorageMode,
        baseline = w.BaselineId, maxJournalBytes = w.Snapshot?.MaxJournalBytes,
        libraryBasis = w.StorageMode == "project-snapshot" ? null : w.GeneratedDirectoryId,
        projectBasis = w.StorageMode == "project-snapshot" ? w.GeneratedDirectoryId : null,
        editor = editor ? true : (bool?)null, home
    });

    public static WorkspaceRecoveryAction Next(WorkspaceView view, string home, bool accepted = false)
    {
        var w = view.Workspace;
        string command = accepted ? "workspace.status" : view.Command;
        if (!accepted && view.Recovery is { Disposition: not "ready" } recovery) command = recovery.NextCommand;
        if (view.OriginalApplyPlan is not null || w.OriginalApply is { Completed: false }) command = "workspace.integrate";
        bool requiresUser = view.Recovery is { Disposition: "observe" or "preserve" or "deferred-reclamation" or "deferred-discard-reclamation" };
        if (w.Purpose == "baseline" && w.Phase == "released") return new("schema", new { command = "workspace.ensure" });
        if (command == "workspace.ensure") return Ensure(w, home) with { RequiresUserAction = requiresUser };
        object arguments = command switch
        {
            "workspace.integrate" when w.SourceKind is "none" or "plastic" => OriginalApply(w, view.OriginalApplyPlan, home),
            "workspace.discard" when w.Discard is { } discard => new { id = w.Id, discard.Intent.IntentKey, expectedRequestDigest = discard.Intent.RequestDigest, home },
            "workspace.list" => new { home },
            _ => new { id = w.Id, home }
        };
        return new(command, arguments, requiresUser);
    }
    private static object OriginalApply(WorkspaceRecord w, OriginalApplyPlan? plan, string home) => w.OriginalApply is { } apply
        ? new { id = w.Id, target = "original", apply.IntentKey, expectedPlanDigest = apply.Plan.Digest, paths = apply.Plan.SelectedPaths, home }
        : (object)new { id = w.Id, target = "original", plan = true, paths = plan?.SelectedPaths, home };
}
