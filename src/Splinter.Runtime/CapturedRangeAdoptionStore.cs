using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Splinter.Core;

namespace Splinter.Runtime;

// Only the fixed request can publish or consume this adoption map. Location is
// a guard; digest identity covers the immutable rows rather than their offsets.
public static class CapturedRangeAdoptionStore
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, RespectRequiredConstructorParameters = true, RespectNullableAnnotations = true };
    public static SourceRangeAdoption Publish(string control, SourceCaptureIdentity capture,
        IEnumerable<CapturedRangeAdoptionEntry> entries, IJournalFilePins pins, CancellationToken ct = default)
    {
        string artifact = Artifact(control, capture.Id), directory = Path.Combine(Path.GetDirectoryName(artifact)!, "preserved-ranges");
        SourceRangeAdoption value;
        if (File.Exists(artifact)) value = Read(control, capture);
        else
        {
            using var pages = new PagedMetadataStore(directory, true, filePins: pins);
            var root = pages.Build(MetadataSort.Order(entries.Select(Encode), directory, ct), ct); pages.Flush();
            value = new(2, capture, directory, root, root.Count, Identity(capture, root));
            string temporary = artifact + "." + Guid.NewGuid().ToString("N") + ".tmp"; WorkspaceStore.EnsurePlainPath(temporary);
            using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { JsonSerializer.Serialize(output, value, Json); output.Flush(true); }
            AtomicFilePublication.Publish(temporary, artifact);
        }
        _ = MetadataMaintenance.Collect(value.Directory, [value.Root], "adoption-" + capture.Id, pins, ct);
        return value;
    }
    public static SourceRangeAdoption Read(string control, SourceCaptureIdentity expected)
    {
        string artifact = Artifact(control, expected.Id), directory = Path.Combine(Path.GetDirectoryName(artifact)!, "preserved-ranges");
        SourceRangeAdoption value;
        try
        {
            using var input = new FileStream(artifact, FileMode.Open, FileAccess.Read, FileShare.Read); using var document = JsonDocument.Parse(input);
            if (!JsonPropertyValidation.HasUniqueProperties(document.RootElement)) throw Invalid();
            value = document.RootElement.Deserialize<SourceRangeAdoption>(Json) ?? throw Invalid();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException) { throw Invalid(); }
        if (value.Version != 2 || value.CaptureIdentity != expected || !WorkspaceStore.SamePath(value.Directory, directory) ||
            value.Count != value.Root.Count || value.Digest != Identity(expected, value.Root)) throw Invalid();
        using var pages = new PagedMetadataStore(directory); _ = pages.Open(value.Root); return value;
    }
    public static IEnumerable<CapturedRangeAdoptionEntry> Enumerate(SourceRangeAdoption descriptor)
    {
        if (descriptor.Version != 2 || descriptor.Count != descriptor.Root.Count || descriptor.Digest != Identity(descriptor.CaptureIdentity, descriptor.Root)) throw Invalid();
        using var pages = new PagedMetadataStore(descriptor.Directory);
        foreach (var entry in pages.Open(descriptor.Root).Enumerate()) yield return Decode(entry);
    }
    public static string Artifact(string control, string id)
    {
        if (id.Length != 24 || id.Any(c => c is not (>= '0' and <= '9' or >= 'a' and <= 'f'))) throw Invalid();
        string path = Path.Combine(control, "captures", id, "preserved-ranges.json"); WorkspaceStore.EnsurePlainPath(path); return path;
    }
    private static string Identity(SourceCaptureIdentity capture, MetadataTreeRoot root) => "sha256:" +
        Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { capture, root.ContentHash, root.Count }, Json)));
    public static MetadataEntry Encode(CapturedRangeAdoptionEntry value)
    {
        Validate(value); byte[] data = new byte[90]; data[0] = 2; data[1] = value.RawRange ? (byte)1 : (byte)0;
        BinaryPrimitives.WriteInt64LittleEndian(data.AsSpan(2), value.Offset); BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(10), value.Length);
        Convert.FromHexString(value.BlockIdentity).CopyTo(data, 14); BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(46), value.BlockOffset);
        Convert.FromHexString(value.ChunkHash).CopyTo(data, 50); BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(82), value.ChunkOffset);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(86), value.ContentLength);
        return new(value.FileId.ToString("N") + "/" + value.Offset.ToString("x16", System.Globalization.CultureInfo.InvariantCulture), data);
    }
    public static CapturedRangeAdoptionEntry Decode(MetadataEntry entry)
    {
        var data = entry.Value.Span;
        if (data.Length != 90 || data[0] != 2 || data[1] > 1 || entry.Child is not null || entry.Key.Length != 49 || entry.Key[32] != '/' ||
            !Guid.TryParseExact(entry.Key[..32], "N", out var id)) throw Invalid();
        long offset = BinaryPrimitives.ReadInt64LittleEndian(data[2..]);
        if (entry.Key[33..] != offset.ToString("x16", System.Globalization.CultureInfo.InvariantCulture)) throw Invalid();
        var value = new CapturedRangeAdoptionEntry(id, offset, BinaryPrimitives.ReadInt32LittleEndian(data[10..]), Convert.ToHexStringLower(data.Slice(14, 32)),
            BinaryPrimitives.ReadInt32LittleEndian(data[46..]), Convert.ToHexStringLower(data.Slice(50, 32)), BinaryPrimitives.ReadInt32LittleEndian(data[82..]), data[1] == 1,
            BinaryPrimitives.ReadInt32LittleEndian(data[86..]));
        Validate(value); return value;
    }
    private static void Validate(CapturedRangeAdoptionEntry value)
    {
        static bool Hash(string hash) => hash.Length == 64 && hash.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
        if (value.FileId == Guid.Empty || value.Offset < 0 || value.Length < 1 || value.Offset > long.MaxValue - value.Length ||
            value.BlockOffset < 0 || value.BlockOffset > int.MaxValue - value.Length || value.ChunkOffset < 0 || value.ContentLength < value.Length || value.ChunkOffset > value.ContentLength - value.Length ||
            !Hash(value.BlockIdentity) || !Hash(value.ChunkHash)) throw Invalid();
    }
    private static WorkspaceException Invalid() => new("SourceRangeAdoptionInvalid", "Preserve an invalid or foreign immutable range adoption map.");
}
