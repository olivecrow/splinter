namespace Splinter.Core;

public sealed record RepositoryInspection(
    string Provider,
    string RootPath,
    string RepositoryId,
    string? Revision,
    IReadOnlyDictionary<string, string?> Details);

public sealed record ApplicationInspection(
    string Provider,
    string RootPath,
    string Version,
    IReadOnlyDictionary<string, string?> Details);

public interface IRepositoryInspector
{
    Task<RepositoryInspection?> InspectAsync(string path, CancellationToken cancellationToken = default);
}

public interface IApplicationInspector
{
    ApplicationInspection? Inspect(string path);
}

public sealed class InspectionException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
