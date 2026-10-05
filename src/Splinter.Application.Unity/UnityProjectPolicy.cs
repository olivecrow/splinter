using System.Security.Cryptography;
using System.Text;
using Splinter.Core;

namespace Splinter.Application.Unity;

// Project structure and the original Editor reservation belong to this adapter.
public sealed class UnityProjectPolicy : IApplicationProjectPolicy
{
    public static UnityProjectPolicy Instance { get; } = new();
    public static ApplicationProjectPolicies Policies { get; } = new(NoApplicationProjectPolicy.Instance, Instance);
    public string ApplicationKind => "unity";
    public GeneratedDirectoryDescriptor GeneratedDirectory { get; } = new("Library", "Library");
    public string CompatibilityKey(string project)
    {
        using var resource = typeof(UnityWorkspaceProvider).Assembly.GetManifestResourceStream("Splinter.Editor.WorkspaceBridge.cs")!;
        using var reader = new StreamReader(resource);
        string version = string.Join('\n', File.ReadAllLines(Path.Combine(project, "ProjectSettings", "ProjectVersion.txt")).Select(line => line.Trim()));
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(version + "\n" +
            reader.ReadToEnd().Replace("\r\n", "\n", StringComparison.Ordinal) + "\nWindows")));
    }
    public IDisposable? AcquireOriginal(string project, bool reservePublication = false, string? verifiedManagedRoot = null)
    {
        string temp = Path.Combine(project, "Temp"), file = Path.Combine(temp, "UnityLockfile");
        if (verifiedManagedRoot is not null && !SamePath(project, verifiedManagedRoot))
            throw new WorkspaceException("UnsafePath", "The verified mount must be the exact original project root.");
        for (string? p = Path.GetFullPath(file); p is not null; p = Path.GetDirectoryName(p))
            if (verifiedManagedRoot is null || !SamePath(p, verifiedManagedRoot))
            if ((File.Exists(p) || Directory.Exists(p)) && (File.GetAttributes(p) & FileAttributes.ReparsePoint) != 0)
                throw new WorkspaceException("UnsafePath", "Reparse point: " + p);
        if (File.Exists(file)) return new FileStream(file, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        if (!reservePublication) return null;
        bool createdTemp = !Directory.Exists(temp);
        if (createdTemp) Directory.CreateDirectory(temp);
        try
        {
            return new Reservation(new FileStream(file, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None,
                4096, FileOptions.DeleteOnClose), createdTemp ? temp : null);
        }
        catch { RemoveEmpty(createdTemp ? temp : null); throw; }
    }
    private static void RemoveEmpty(string? temp)
    {
        if (temp is not null && Directory.Exists(temp) && !Directory.EnumerateFileSystemEntries(temp).Any()) Directory.Delete(temp);
    }
    private static bool SamePath(string left, string right) => string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(left)),
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(right)), OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    private sealed class Reservation(FileStream handle, string? createdTemp) : IDisposable
    {
        private bool disposed;
        public void Dispose()
        {
            if (disposed) return;
            disposed = true; handle.Dispose(); RemoveEmpty(createdTemp);
        }
    }
    public ApplicationSelection CloseSelection(IEnumerable<string> paths, Func<string, bool> existedBefore, Func<string, bool> existsAfter)
    {
        var included = paths.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (string path in included.ToArray())
            for (string? parent = Parent(path); parent is not null; parent = Parent(parent))
                if (parent.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase)) included.Add(parent);
        var companions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string path in included.ToArray())
        {
            if (!path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase)) continue;
            string asset = path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase) ? path[..^5] : path;
            if (existsAfter(asset) != existsAfter(asset + ".meta"))
                throw new WorkspaceException("OriginalApplyMetadataMissing", "Preserve an incomplete result asset/metadata pair: " + asset);
            foreach (string pair in new[] { asset, asset + ".meta" })
            {
                if (!existedBefore(pair) && !existsAfter(pair))
                    throw new WorkspaceException("OriginalApplyMetadataMissing", "Preserve the missing Unity asset/metadata companion: " + pair);
                included.Add(pair); companions.Add(pair);
            }
        }
        return new(included.Order(StringComparer.Ordinal).ToArray(), companions.Order(StringComparer.Ordinal).ToArray());
    }
    private static string? Parent(string path) => path.LastIndexOf('/') is var slash && slash >= 0 ? path[..slash] : null;
}
