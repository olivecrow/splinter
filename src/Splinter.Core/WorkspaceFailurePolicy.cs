namespace Splinter.Core;

// Shared by durable recovery decisions and machine-readable failure responses.
public static class WorkspaceFailurePolicy
{
    public static bool RequiresPreservation(string? code) => code is
        "DeltaChangedAfterCollection" or "SnapshotResultInvalid" or "StateCorrupt" or
        "SnapshotReclamationInvalid" or "SnapshotCollateralLoss" or "CompactionGenerationMismatch" or
        "CompactionStateMismatch" or "CompactionCandidateMismatch" or "CompactionIntentCorrupt" or "CompactionIntentMismatch";

    public static string RetryDisposition(string code) => RequiresPreservation(code) ? "requires-user-action" : code switch
    {
        "GitInspectionTimeout" or "Cancelled" or "WorkspaceBusy" or "EditorNotReady" or "ValidationInProgress" or "SourceObservationUncertain" or "SourceChanged" => "safe-to-retry",
        "CoWUnsupported" or "ProcessOwnershipUnknown" or "SnapshotPrivilegeRequired" or
        "SnapshotHeadroomRequired" or "SnapshotReleaseIsolationRequired" or "WinFspMissing" or "SnapshotMissing" or "OverlayResultRequired" or
        "OverlayStartUncertain" or "SnapshotStateMissing" or "SnapshotCheckpointMismatch" or "SnapshotPreparationIncomplete" or "SnapshotOperationIncomplete" or
        "SnapshotObservationUnknown" or "StateRecoveryRequired" or "StateReadUnavailable" or "StateWriteIncomplete" or
        "DeltaCorrupt" or "DeltaReconnectRequired" or "OverlayIdentityMissing" or "OverlayIdentityMismatch" or "SnapshotIdentityMismatch" or
        "SourceCacheRevalidationRequired" or "SourceObservationInvalid" or "SourceManifestLimit" or "AccessDenied" or "GitUnavailable" or "EditorFocusRequired" => "requires-user-action",
        _ => "correct-request"
    };
}
