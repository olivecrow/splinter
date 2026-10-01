namespace Splinter.Core;

// Data only: Runtime interprets platform error codes; Core owns no native API.
public sealed record StateWriteDiagnostic(string Stage, string StatePublication, string ExceptionType, int HResult,
    string? NativeErrorDomain = null, int? NativeErrorCode = null);

public sealed record StateWriteFailureDetails(string Id, string Action, StateWriteDiagnostic Diagnostic);
