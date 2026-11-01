using System.Buffers.Binary;

namespace Splinter.Runtime;

public sealed partial class PagedMetadataStore
{
    // A stopped journal's accepted tail needs COW metadata during replay, but
    // its source index remains immutable. Existing addresses keep their exact
    // store identity and are read through the separately retained base; every
    // new address is allocated only in the caller's owned scratch directory.
    internal PagedMetadataStore Derive(string scratch, IJournalFilePins? pins)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (writable) throw Invalid("Derive replay metadata from a retained read-only base.");
        long maximum = -1;
        foreach (string path in Directory.EnumerateFiles(directory, "pages-*.bin"))
            maximum = Math.Max(maximum, ParseContainer(path));
        if (locations is not null)
        {
            Span<byte> row = stackalloc byte[76];
            for (long index = 0; index < locations.Header.Rows; index++)
            {
                ReadAt(locations.Input, row, checked(index * 76));
                long original = BinaryPrimitives.ReadInt64LittleEndian(row);
                var target = DecodeReference(row[16..]); CheckReference(target);
                if (original < 0) throw Invalid("Preserve an invalid metadata relocation address.");
                maximum = Math.Max(maximum, Math.Max(original, target.Container));
            }
        }
        var derived = new PagedMetadataStore(scratch, true, null, pins, maintenance: false,
            containerStart: checked(maximum + 1), initialStoreId: StoreId);
        derived.readThrough = this; derived.readThroughLastContainer = maximum;
        return derived;
    }
}
