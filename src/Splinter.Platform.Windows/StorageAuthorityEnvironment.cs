using System.Text.Json;

namespace Splinter.Platform.Windows;

// Retain identities, not handles, between requests. Reacquire the original
// deny-write/delete pins and validate every ancestor before privileged work.
internal sealed class StorageAuthorityEnvironment
{
    private readonly string install, state, powershell;
    private readonly Action<string, bool> requireProtectedDirectory;
    private readonly Dictionary<string, string> identities;
    private readonly string configurationJson;
    internal StorageAuthorityConfiguration Configuration { get; }
    internal string StatePath => state;

    private StorageAuthorityEnvironment(string install, string state, string powershell,
        Action<string, bool> requireProtectedDirectory)
    {
        this.install = install; this.state = state; this.powershell = powershell;
        this.requireProtectedDirectory = requireProtectedDirectory;
        using var guard = Open(out var configuration);
        Configuration = configuration;
        configurationJson = JsonSerializer.Serialize(configuration, StorageAuthorityProtocol.Json);
        identities = new(guard.Identities, StringComparer.OrdinalIgnoreCase);
    }

    internal static StorageAuthorityEnvironment LoadInstalled() => Load(StorageAuthorityProtocol.InstallPath,
        StorageAuthorityProtocol.StatePath, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "PowerShell", "7", "pwsh.exe"),
        StorageAuthorityPaths.RequireProtectedDirectory);

    // Explicit paths/ACL double let tests exercise real pins and the actual pipe
    // loop in a private fixture without installing a SYSTEM service.
    internal static StorageAuthorityEnvironment Load(string install, string state, string powershell,
        Action<string, bool> requireProtectedDirectory) => new(install, state, powershell, requireProtectedDirectory);

    internal StorageAuthorityPaths Acquire()
    {
        var guard = Open(out var configuration);
        try
        {
            if (guard.Identities.Count != identities.Count || guard.Identities.Any(pair =>
                !identities.TryGetValue(pair.Key, out var expected) || pair.Value != expected) ||
                JsonSerializer.Serialize(configuration, StorageAuthorityProtocol.Json) != configurationJson)
                throw StorageAuthorityProtocol.Error("StorageServiceUnsafe", "The authority paths or configuration changed while idle; preserve state and restart explicitly.");
            return guard;
        }
        catch { guard.Dispose(); throw; }
    }

    private StorageAuthorityPaths Open(out StorageAuthorityConfiguration configuration)
    {
        var guard = new StorageAuthorityPaths();
        try
        {
            guard.Pin(install); guard.Pin(state);
            requireProtectedDirectory(install, false); requireProtectedDirectory(state, false);
            configuration = guard.Read<StorageAuthorityConfiguration>(Path.Combine(state, "configuration.json"));
            if (configuration.Version != 1 || configuration.Grants.Length == 0)
                throw StorageAuthorityProtocol.Error("StorageServiceVersionMismatch", "Repair the current service configuration.");
            if (!StorageAuthorityProcessor.Same(configuration.PowerShellPath, powershell))
                throw StorageAuthorityProtocol.Error("StorageServiceUnsafe", "Only the installed PowerShell 7 host is permitted.");
            guard.Pin(Path.GetDirectoryName(powershell)!); guard.Pin(powershell, directory: false);
            requireProtectedDirectory(Path.GetDirectoryName(powershell)!, true);
            return guard;
        }
        catch { guard.Dispose(); throw; }
    }
}
