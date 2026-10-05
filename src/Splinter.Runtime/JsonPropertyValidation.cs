using System.Text.Json;

namespace Splinter.Runtime;

// Only property-name uniqueness is shared. Parsing bounds, versions and error
// mapping remain with the request/state boundary that owns the document.
public static class JsonPropertyValidation
{
    public static bool HasUniqueProperties(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in value.EnumerateObject())
                if (!names.Add(property.Name) || !HasUniqueProperties(property.Value)) return false;
        }
        else if (value.ValueKind == JsonValueKind.Array)
            foreach (var item in value.EnumerateArray())
                if (!HasUniqueProperties(item)) return false;
        return true;
    }
}
