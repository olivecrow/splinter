namespace Splinter.Core;

// Application-specific source companions, original-process reservations and
// generated content belong to the application adapter, not a VCS or mount.
public sealed record GeneratedDirectoryDescriptor(string Kind, string RelativePath);
public sealed record ApplicationSelection(string[] Paths, string[] Companions);
public interface IApplicationProjectPolicy
{
    string ApplicationKind { get; }
    GeneratedDirectoryDescriptor? GeneratedDirectory { get; }
    string CompatibilityKey(string project);
    // verifiedManagedRoot may only be passed while the caller holds the exact
    // owned mount identity lease; it is not permission for arbitrary reparse paths.
    IDisposable? AcquireOriginal(string project, bool reservePublication = false, string? verifiedManagedRoot = null);
    ApplicationSelection CloseSelection(IEnumerable<string> paths, Func<string, bool> existedBefore,
        Func<string, bool> existsAfter);
}

public sealed class ApplicationProjectPolicies
{
    private readonly Dictionary<string, IApplicationProjectPolicy> policies;
    public ApplicationProjectPolicies(params IApplicationProjectPolicy[] policies)
    {
        this.policies = new(StringComparer.Ordinal);
        foreach (var policy in policies)
            if (string.IsNullOrWhiteSpace(policy.ApplicationKind) || !this.policies.TryAdd(policy.ApplicationKind, policy))
                throw new ArgumentException("Application policy identities must be explicit and unique.");
    }
    public IApplicationProjectPolicy Get(string kind) => policies.TryGetValue(kind, out var policy) ? policy :
        throw new WorkspaceException("ApplicationPolicyUnsupported", "Compose the exact application project policy; no fallback is available.");
    public static ApplicationProjectPolicies None { get; } = new(NoApplicationProjectPolicy.Instance);
}

public sealed class NoApplicationProjectPolicy : IApplicationProjectPolicy
{
    public static NoApplicationProjectPolicy Instance { get; } = new();
    private NoApplicationProjectPolicy() { }
    public string ApplicationKind => "none";
    public GeneratedDirectoryDescriptor? GeneratedDirectory => null;
    public string CompatibilityKey(string project) => "plain-project-v1";
    public IDisposable? AcquireOriginal(string project, bool reservePublication = false, string? verifiedManagedRoot = null) => null;
    public ApplicationSelection CloseSelection(IEnumerable<string> paths, Func<string, bool> existedBefore,
        Func<string, bool> existsAfter) => new(paths.ToArray(), []);
}
