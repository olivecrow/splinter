using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Splinter.Core;

namespace Splinter.Runtime;

internal static class JournalControlIntegrity
{
    // Fixed control records contain a fixed number of filesystem paths and
    // roots, not project entry lists. Account for JSON escaping of those paths.
    internal const long MaximumBytes = 16L * 6 * 32767 + 65536;
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    { PropertyNameCaseInsensitive = false, RespectRequiredConstructorParameters = true, RespectNullableAnnotations = true, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
    internal static string Digest<T>(T value) => Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value, Json)));
    internal static T Read<T>(string path)
    {
        WorkspaceStore.EnsurePlainPath(path);
        using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (input.Length is < 1 or > MaximumBytes) throw Invalid();
        try
        {
            using var document = JsonDocument.Parse(input);
            if (!JsonPropertyValidation.HasUniqueProperties(document.RootElement)) throw Invalid();
            return document.RootElement.Deserialize<T>(Json) ?? throw Invalid();
        }
        catch (JsonException) { throw Invalid(); }
    }
    internal static WorkspaceException Invalid() => new("JournalControlCorrupt", "Preserve unsupported or changed journal control records.");
}
