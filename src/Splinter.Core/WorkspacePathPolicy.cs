namespace Splinter.Core;

// This is a property of an immutable tree, not of the machine running the reader.
// A missing identifier is the current Windows contract; it is never guessed from
// the host OS. Providers for other filesystems must choose an explicit policy.
public sealed class WorkspacePathPolicy
{
    public const string PosixId = "posix-case-sensitive-v1";
    public static WorkspacePathPolicy Windows { get; } = new(null, false);
    public static WorkspacePathPolicy Posix { get; } = new(PosixId, true);
    private WorkspacePathPolicy(string? id, bool caseSensitive) { Id = id; CaseSensitive = caseSensitive; }
    public string? Id { get; }
    public bool CaseSensitive { get; }
    public StringComparer Comparer => CaseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;
    public StringComparison Comparison => CaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
    public static WorkspacePathPolicy For(SnapshotReference snapshot) => snapshot.PathPolicy switch
    {
        null => Windows,
        PosixId => Posix,
        _ => throw new WorkspaceException("PathPolicyUnsupported", "Preserve the tree with its unsupported immutable path policy.")
    };
    public string IdentityPath(string path) => CaseSensitive ? path : path.ToUpperInvariant();
    public string Normalize(string path)
    {
        path = (CaseSensitive ? path : path.Replace('\\', '/')).Trim('/');
        if (path.Length > 32000 || path.Contains('\0') || !CaseSensitive && path.Contains(':') ||
            path.Length != 0 && path.Split('/').Any(p => p is "." or ".." or "" ||
                !CaseSensitive && (p.EndsWith('.') || p.EndsWith(' '))))
            throw new ArgumentException("Only relative ordinary file paths are supported by this tree's path policy.");
        return path;
    }
    public bool IsChild(string child, string parent) => child.StartsWith(parent + "/", Comparison);
    public bool Managed(string path, string[] roots) => roots.Any(root => root.Contains('*')
        ? !path.Contains('/') && System.IO.Enumeration.FileSystemName.MatchesSimpleExpression(root, path, !CaseSensitive)
        : path.Equals(root, Comparison) || path.StartsWith(root.TrimEnd('/') + "/", Comparison));
}

public static class WorkspaceUnixPermissions
{
    // Permission bits only, including setuid/setgid/sticky. File type is carried
    // separately; unknown permission metadata remains null, never defaulted.
    public static void RequireValid(uint? mode)
    {
        if (mode is > 0xFFF) throw new ArgumentOutOfRangeException(nameof(mode), "Unix permission metadata must contain only the low twelve mode bits.");
    }
}
