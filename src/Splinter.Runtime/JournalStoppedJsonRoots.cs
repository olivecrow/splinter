using System.Text.Json;
using Splinter.Core;

namespace Splinter.Runtime;

public sealed partial class JournalDeltaStore
{
    // Discovery consumes arbitrary numbers of bounded schema values. Large
    // content-summary arrays never become JsonDocument/DTO arrays in memory.
    // Base64 maintenance controls can expand the existing filesystem-derived
    // single-control value bound; this is a token bound, never a file limit.
    private const int StoppedJsonTokenBytes = (int)(4 * ((JournalControlIntegrity.MaximumBytes + 2) / 3) + 2);
    private sealed class StoppedJsonProbe(bool array, string? assigned)
    {
        public readonly bool Array = array;
        public readonly string? Assigned = assigned;
        public string? Property;
        public int Members;
        public bool Duplicate, UnknownRoot, UnknownPage, StoreMatches, VersionValid, PageSet, PageValid;
        public bool ContainerValid, OffsetValid, BytesValid, CountValid;
        public int Version, Bytes;
        public long Container, Offset, Count;
        public string? Hash;
        public MetadataPageReference? Page;
        public void Name(string property)
        {
            Property = property.ToLowerInvariant();
            int bit = Property switch { "version" => 1, "storeid" => 2, "page" => 4, "container" => 8,
                "offset" => 16, "bytes" => 32, "count" => 64, "contenthash" => 128, _ => 0 };
            if (bit != 0 && (Members & bit) != 0) Duplicate = true;
            Members |= bit; UnknownRoot |= bit is not (1 or 2 or 4); UnknownPage |= bit is not (8 or 16 or 32 or 64 or 128);
        }
    }
    private static IEnumerable<MetadataTreeRoot> StreamStoppedJsonRoots(Stream input, string storeId)
    {
        byte[] buffer = new byte[65536]; int held = 0; var state = new JsonReaderState(); var probes = new Stack<StoppedJsonProbe>();
        while (true)
        {
            int read = input.Read(buffer, held, buffer.Length - held); bool final = read == 0; held += read;
            List<MetadataTreeRoot> roots;
            int consumed;
            try { roots = ReadStoppedJsonBlock(buffer.AsSpan(0, held), final, ref state, probes, storeId, out consumed); }
            catch (JsonException) { throw PagedInvalid(); }
            foreach (var root in roots) yield return root;
            if (final)
            { if (consumed != held || probes.Count != 0) throw PagedInvalid(); yield break; }
            if (consumed != 0) { buffer.AsSpan(consumed, held - consumed).CopyTo(buffer); held -= consumed; }
            if (held == buffer.Length)
            {
                if (buffer.Length == StoppedJsonTokenBytes)
                    throw new WorkspaceException("JournalControlCorrupt", "Preserve a control token outside its filesystem-derived schema value boundary.");
                Array.Resize(ref buffer, Math.Min(StoppedJsonTokenBytes, checked(buffer.Length * 2)));
            }
        }
    }
    private static List<MetadataTreeRoot> ReadStoppedJsonBlock(ReadOnlySpan<byte> bytes, bool final, ref JsonReaderState state,
        Stack<StoppedJsonProbe> probes, string storeId, out int consumed)
    {
        var reader = new Utf8JsonReader(bytes, final, state); List<MetadataTreeRoot> roots = [];
        while (reader.Read())
        {
            if (reader.TokenType is JsonTokenType.StartObject or JsonTokenType.StartArray)
            {
                var parent = probes.Count == 0 ? null : probes.Peek();
                if (parent is { Array: false, Property: "page" }) { parent.PageSet = true; parent.PageValid = false; }
                probes.Push(new(reader.TokenType == JsonTokenType.StartArray, parent is { Array: false } ? parent.Property : null));
            }
            else if (reader.TokenType is JsonTokenType.EndObject or JsonTokenType.EndArray)
            {
                if (!probes.TryPop(out var probe)) throw PagedInvalid();
                if (!probe.Array && probe.StoreMatches && (probe.Members & 4) != 0)
                {
                    if (probe.Members != 7 || probe.UnknownRoot || probe.Duplicate || !probe.VersionValid || !probe.PageSet || !probe.PageValid)
                        throw PagedInvalid();
                    roots.Add(new(probe.Version, storeId, probe.Page));
                }
                if (probes.TryPeek(out var parent) && !parent.Array && probe.Assigned == "page")
                {
                    parent.PageSet = true;
                    parent.PageValid = !probe.Array && !probe.Duplicate && !probe.UnknownPage && probe.Members == 248 &&
                        probe.ContainerValid && probe.OffsetValid && probe.BytesValid && probe.CountValid && probe.Hash is not null;
                    if (parent.PageValid) parent.Page = new(probe.Container, probe.Offset, probe.Bytes, probe.Count, probe.Hash!);
                }
            }
            else if (reader.TokenType == JsonTokenType.PropertyName)
            {
                if (!probes.TryPeek(out var probe) || probe.Array) throw PagedInvalid();
                probe.Name(reader.GetString() ?? throw PagedInvalid());
            }
            else if (probes.TryPeek(out var probe) && !probe.Array)
            {
                switch (probe.Property)
                {
                    case "version": probe.VersionValid = reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out probe.Version); break;
                    case "storeid": probe.StoreMatches |= reader.TokenType == JsonTokenType.String && reader.ValueTextEquals(storeId); break;
                    case "page": probe.PageSet = true; probe.PageValid = reader.TokenType == JsonTokenType.Null; probe.Page = null; break;
                    case "container": probe.ContainerValid = reader.TokenType == JsonTokenType.Number && reader.TryGetInt64(out probe.Container); break;
                    case "offset": probe.OffsetValid = reader.TokenType == JsonTokenType.Number && reader.TryGetInt64(out probe.Offset); break;
                    case "bytes": probe.BytesValid = reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out probe.Bytes); break;
                    case "count": probe.CountValid = reader.TokenType == JsonTokenType.Number && reader.TryGetInt64(out probe.Count); break;
                    case "contenthash": probe.Hash = reader.TokenType == JsonTokenType.String ? reader.GetString() : null; break;
                }
            }
        }
        consumed = checked((int)reader.BytesConsumed); state = reader.CurrentState; return roots;
    }
}
