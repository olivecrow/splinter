using System.Text.Json;
using Splinter.Core;

namespace Splinter.Application.Unity;

public sealed class UnityProjectInspector : IApplicationInspector
{
    public ApplicationInspection? Inspect(string path)
    {
        var directory = new DirectoryInfo(Path.GetFullPath(path));
        while (directory is not null)
        {
            string versionFile = Path.Combine(directory.FullName, "ProjectSettings", "ProjectVersion.txt");
            string manifestFile = Path.Combine(directory.FullName, "Packages", "manifest.json");
            if (Directory.Exists(Path.Combine(directory.FullName, "Assets")) &&
                File.Exists(versionFile) && File.Exists(manifestFile))
            {
                string? version = File.ReadLines(versionFile)
                    .FirstOrDefault(line => line.StartsWith("m_EditorVersion:", StringComparison.Ordinal))?
                    .Split(':', 2)[1].Trim();
                if (string.IsNullOrWhiteSpace(version))
                    throw new InspectionException("UnityVersionMissing", "ProjectVersion.txt does not specify an Editor version.");
                using var manifest = JsonDocument.Parse(File.ReadAllText(manifestFile));
                if (manifest.RootElement.ValueKind != JsonValueKind.Object ||
                    !manifest.RootElement.TryGetProperty("dependencies", out var dependencies) ||
                    dependencies.ValueKind != JsonValueKind.Object)
                    throw new InspectionException("UnityManifestInvalid", "Unity manifest must contain a dependencies object.");
                string? Package(string name)
                {
                    if (!dependencies.TryGetProperty(name, out var value)) return null;
                    if (value.ValueKind != JsonValueKind.String)
                        throw new InspectionException("UnityManifestInvalid", $"Package '{name}' must specify a string version or source.");
                    return value.GetString();
                }
                return new("unity", directory.FullName, version, new Dictionary<string, string?>
                {
                    ["testFramework"] = Package("com.unity.test-framework"),
                    ["inputSystem"] = Package("com.unity.inputsystem"),
                    ["universalRenderPipeline"] = Package("com.unity.render-pipelines.universal"),
                    ["runtimeVerification"] = "not-run"
                });
            }
            directory = directory.Parent;
        }
        return null;
    }
}
