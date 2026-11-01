using System.Text.Json.Serialization;
using Splinter.Core;
using Splinter.Runtime;
using static Splinter.SourceControl.Files.PlasticRevisionClient;

namespace Splinter.SourceControl.Files;

public sealed record PlasticRevisionManifest(string TreeSpec, string RepositoryId, string RootRevision, string RootGuid,
    [property: JsonIgnore] PlasticRevisionEntry[] Entries, string Repository) : IDisposable
{
    [JsonRequired] public int Version { get; init; } = 2;
    [JsonRequired] public SnapshotCatalogDescriptor? Catalog { get; init; }
    [JsonIgnore] public string? CatalogDirectory { get; init; }
    [JsonIgnore] internal TemporaryMetadataDirectory? ScratchOwner { get; init; }

    // The array constructor is used by fake clients only. Production listing
    // and persisted receipts bind an immutable paged catalog before use.
    public PlasticRevisionManifest WriteCatalog(string directory, CancellationToken ct = default)
    {
        var descriptor = BuildCatalog(directory, EnumerateEntries(), ct);
        return this with { Entries = null!, Catalog = descriptor, CatalogDirectory = Path.GetFullPath(directory), ScratchOwner = null };
    }
    internal static SnapshotCatalogDescriptor BuildCatalog(string directory, IEnumerable<PlasticRevisionEntry> entries, CancellationToken ct)
    {
        var descriptor = PagedSnapshotCatalog.Build(directory, new("", true, 0, (uint)FileAttributes.Directory, 0, 0, 0), entries.Select(ToRecord), ct: ct);
        using var catalog = new PagedSnapshotCatalog(directory, descriptor);
        string? previous = null; byte[]? prior = null;
        IEnumerable<MetadataEntry> Identities()
        {
            foreach (var record in catalog.Walk().Skip(1))
            {
                var entry = FromRecord(record);
                if (!entry.Directory) yield return new(entry.Revision, System.Text.Encoding.ASCII.GetBytes(entry.Length.ToString(System.Globalization.CultureInfo.InvariantCulture) + "|" + entry.ContentHash));
            }
        }
        foreach (var identity in MetadataExternalSort.Order(Identities(), directory, ct))
        {
            if (identity.Key == previous && !identity.Value.Span.SequenceEqual(prior))
                throw Failure("PlasticBasisInvalid", "A revision cannot name different recorded content.");
            previous = identity.Key; prior = identity.Value.ToArray();
        }
        return descriptor;
    }

    public IEnumerable<PlasticRevisionEntry> EnumerateEntries()
    {
        if (Catalog is null)
        {
            if (Entries is null) throw Failure("PlasticBasisInvalid", "The fixed revision catalog is absent.");
            foreach (var entry in Entries) yield return entry;
            yield break;
        }
        if (Version != 2 || CatalogDirectory is null) throw Failure("PlasticBasisInvalid", "Bind the exact recorded catalog directory.");
        using var catalog = new PagedSnapshotCatalog(CatalogDirectory, Catalog);
        foreach (var record in catalog.Walk().Skip(1)) yield return FromRecord(record);
    }
    internal PagedSnapshotCatalog OpenCatalog() => Catalog is not null && CatalogDirectory is not null
        ? new(CatalogDirectory, Catalog) : throw Failure("PlasticBasisInvalid", "The fixed revision catalog is not bound.");
    internal static DirectoryMetadataRecord ToRecord(PlasticRevisionEntry entry)
    {
        if (entry is null || string.IsNullOrEmpty(entry.Path) || !PlasticRevisionSnapshotTree.ValidRecordedPath(entry.Path) || entry.Length < 0 || !Numeric(entry.Revision) ||
            entry.Directory && entry.Length != 0 || !ValidHash(entry.ContentHash) ||
            !entry.Directory && entry.Length == 0 && entry.ContentHash != "1B2M2Y8AsgTpgAmY7PhCfg==")
            throw Failure("PlasticBasisInvalid", "Retain unsupported recorded source entries.");
        return new(new(entry.Path, entry.Directory, entry.Length, (uint)(entry.Directory ? FileAttributes.Directory : FileAttributes.Archive),
            entry.Timestamp, entry.Timestamp, entry.Timestamp), null, SourceReference: entry.Revision + "|" + entry.ContentHash);
    }
    internal static PlasticRevisionEntry FromRecord(DirectoryMetadataRecord record)
    {
        string? reference = record.SourceReference; int separator = reference?.IndexOf('|') ?? -1;
        if (separator < 1 || !Numeric(reference![..separator]) || !ValidHash(reference[(separator + 1)..]))
            throw Failure("PlasticBasisInvalid", "A recorded revision identity is corrupt.");
        var e = record.Entry;
        return new(e.Path, e.Directory, reference[..separator], e.Length, reference[(separator + 1)..], e.LastWriteTime);
    }
    private static bool ValidHash(string hash)
    { try { if (hash is null) return false; var bytes = Convert.FromBase64String(hash); return bytes.Length == 16 && Convert.ToBase64String(bytes) == hash; } catch (FormatException) { return false; } }
    public void Dispose() => ScratchOwner?.Dispose();
}
