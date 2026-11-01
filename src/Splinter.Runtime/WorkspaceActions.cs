using Splinter.Core;

namespace Splinter.Runtime;

// One projection owns the exact request arguments used by CLI and failure recovery.
public static class WorkspaceActions
{
    public static WorkspaceRecoveryAction Ensure(WorkspaceRecord w, string home, bool editor = false) => new("workspace.ensure", new
    {
        project = w.ProjectPath, key = w.RequestKey, baseReference = w.BaseReference,
        source = w.SourceKind, application = w.ApplicationKind, storage = w.StorageMode,
        libraryBasis = w.StorageMode == "project-snapshot" ? null : w.GeneratedDirectoryId,
        projectBasis = w.StorageMode == "project-snapshot" ? w.GeneratedDirectoryId : null,
        editor = editor ? true : (bool?)null, home
    });

    public static WorkspaceRecoveryAction Next(WorkspaceView view, string home, bool accepted = false)
    {
        var w = view.Workspace;
        if (view.MetadataMaintenance is { } maintenance)
            return new("workspace.maintain", new { id = w.Id, requestKey = maintenance.RequestKey, scope = maintenance.Scope, home });
        if (view.OriginalApplyPlan is { SelectionRoot: not null } selection && w.OriginalApply is not { Completed: false })
            return new("workspace.read", new { id = w.Id, collection = "selection", planId = selection.Digest, home });
        string command = accepted ? "workspace.status" : view.Command;
        if (!accepted && view.Recovery is { Disposition: not "ready" } recovery) command = recovery.NextCommand;
        if (view.OriginalApplyPlan is not null || w.OriginalApply is { Completed: false }) command = "workspace.integrate";
        bool requiresUser = view.Recovery is { Disposition: "observe" or "preserve" or "deferred-reclamation" or "deferred-discard-reclamation" };
        if (command == "workspace.ensure") return Ensure(w, home) with { RequiresUserAction = requiresUser };
        object arguments = command switch
        {
            "workspace.integrate" when w.SourceKind is "none" or "plastic" || view.OriginalApplyPlan is not null || w.OriginalApply is { Completed: false } => OriginalApply(w, view.OriginalApplyPlan, home),
            "workspace.checkpoint" when w.CaptureIntent is { } capture => new { id = w.Id, capture.RequestKey, capture.Message, home },
            "workspace.discard" when w.Discard is { } discard => new { id = w.Id, discard.Intent.IntentKey, expectedRequestDigest = discard.Intent.RequestDigest, home },
            "workspace.list" => new { home },
            _ => new { id = w.Id, home }
        };
        return new(command, arguments, requiresUser);
    }
    private static object OriginalApply(WorkspaceRecord w, OriginalApplyPlan? plan, string home) => w.OriginalApply is { Completed: false } apply
        ? new { id = w.Id, target = "original", result = apply.Plan.ResultId, apply.IntentKey, expectedPlanDigest = apply.Plan.Digest, home }
        : (object)new { id = w.Id, target = "original", result = plan?.ResultId ?? w.Results.LastOrDefault()?.Id, plan = true, paths = plan?.SelectedPaths, home };
}
