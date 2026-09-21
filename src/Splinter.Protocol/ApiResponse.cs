using System.Text.Json;
using System.Text.Json.Serialization;

namespace Splinter.Protocol;

public static class ApiJson
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}

public sealed record ApiError(string Code, string Message, object? Details = null);
public sealed record NextAction(string Command, object Arguments, bool RequiresUserAction = false);

public sealed record ApiResponse(
    string SchemaVersion,
    string RequestId,
    string Command,
    string Status,
    bool Terminal,
    string RetryDisposition,
    object? Data,
    ApiError? Error,
    IReadOnlyList<NextAction> NextActions)
{
    public static ApiResponse Success(string command, object data) => new(
        "1.0", Guid.NewGuid().ToString("N"), command, "Succeeded", true, "not-needed", data, null, []);

    public static ApiResponse Failure(string command, string code, string message, object? details = null,
        IReadOnlyList<NextAction>? nextActions = null) => new(
        "1.0", Guid.NewGuid().ToString("N"), command, "Failed", true, code switch
        {
            "GitInspectionTimeout" or "Cancelled" or "WorkspaceBusy" or "EditorNotReady" or "ValidationInProgress" => "safe-to-retry",
            "CoWUnsupported" or "LegacyProcessOwnership" or "ProcessOwnershipUnknown" or "SnapshotPrivilegeRequired" or
            "SnapshotHeadroomRequired" or "WinFspMissing" or "SnapshotMissing" or "OverlayResultRequired" or "DeltaChangedAfterCollection" or
            "OverlayStartUncertain" or "SnapshotStateMissing" or "SnapshotCheckpointMismatch" or "SnapshotPreparationIncomplete" or "SnapshotOperationIncomplete" => "requires-user-action",
            "AccessDenied" or "GitUnavailable" or "EditorFocusRequired" => "requires-user-action",
            _ => "correct-request"
        }, null,
        new(code, message, details), nextActions ?? []);
}
