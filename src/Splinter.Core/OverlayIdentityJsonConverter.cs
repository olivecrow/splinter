using System.Text.Json;
using System.Text.Json.Serialization;

namespace Splinter.Core;

// Current identities have no product byte quota. The historical wire value is
// retained only when reading/writing the explicit old-format test contracts;
// production task/basis version checks refuse those identities before use.
public sealed class OverlayIdentityJsonConverter : JsonConverter<OverlayIdentity>
{
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed record Wire([property: JsonRequired] int Version, [property: JsonRequired] string Id,
        [property: JsonRequired] SnapshotReference Snapshot, [property: JsonRequired] int MaxTransferBytes,
        long? MaxJournalBytes = null, string[]? ExcludedTopLevel = null, string[]? IncludedTopLevel = null,
        string[]? DeniedTopLevel = null, SnapshotGraft? Graft = null);

    public override OverlayIdentity Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var names = new HashSet<string>(options.PropertyNameCaseInsensitive ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        foreach (var property in document.RootElement.EnumerateObject())
            if (!names.Add(property.Name)) throw new JsonException("Duplicated overlay identity fields.");
        Wire value = document.RootElement.Deserialize<Wire>(options) ?? throw new JsonException("Missing overlay identity.");
        if (value.Id is null || value.Snapshot is null || value.Version >= 3 && value.MaxJournalBytes is not null ||
            value.Version < 3 && value.MaxJournalBytes is null)
            throw new JsonException("The overlay version and quota contract do not match.");
        return new(value.Version, value.Id, value.Snapshot, value.Version >= 3 ? long.MaxValue : value.MaxJournalBytes!.Value,
            value.MaxTransferBytes, value.ExcludedTopLevel, value.IncludedTopLevel, value.DeniedTopLevel, value.Graft);
    }
    public override void Write(Utf8JsonWriter writer, OverlayIdentity value, JsonSerializerOptions options)
    {
        writer.WriteStartObject(); Write(nameof(value.Version), value.Version); Write(nameof(value.Id), value.Id);
        Write(nameof(value.Snapshot), value.Snapshot);
        if (value.Version < 3) Write(nameof(value.MaxJournalBytes), value.MaxJournalBytes);
        Write(nameof(value.MaxTransferBytes), value.MaxTransferBytes);
        Optional(nameof(value.ExcludedTopLevel), value.ExcludedTopLevel); Optional(nameof(value.IncludedTopLevel), value.IncludedTopLevel);
        Optional(nameof(value.DeniedTopLevel), value.DeniedTopLevel); Optional(nameof(value.Graft), value.Graft); writer.WriteEndObject();
        void Write<T>(string name, T item)
        { writer.WritePropertyName(options.PropertyNamingPolicy?.ConvertName(name) ?? name); JsonSerializer.Serialize(writer, item, options); }
        void Optional<T>(string name, T? item)
        { if (item is not null || options.DefaultIgnoreCondition != JsonIgnoreCondition.WhenWritingNull) Write(name, item); }
    }
}
