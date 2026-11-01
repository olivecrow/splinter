using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Splinter.Core;

namespace Splinter.Runtime;

public sealed partial class JournalDeltaStore
{
    private sealed record PagedIndexOwner(int Version, string OwnerHash);
    private static string PagedOwnerHash(OverlayIdentity identity) => Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(identity)));
    private static bool IndexName(string name) => name.StartsWith("index-", StringComparison.Ordinal) && Guid.TryParseExact(name.AsSpan(6), "N", out _);
    private static bool IndexFile(string name)
    {
        if (name.Contains('\\') || name.Contains('\0')) return false;
        if (name.StartsWith("collections/", StringComparison.Ordinal))
            return !name.AsSpan(12).Contains('/') && name.EndsWith(".json", StringComparison.Ordinal);
        if (name.Contains('/')) return false;
        if (name is "identity.bin" or "owner.json" or "writer.lock" or "locations.json") return true;
        string payload = name.EndsWith(".owner", StringComparison.Ordinal) ? name[..^6] : name;
        return payload.Length == 26 && payload.StartsWith("pages-", StringComparison.Ordinal) && payload.EndsWith(".bin", StringComparison.Ordinal) &&
            long.TryParse(payload.AsSpan(6, 16), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out long n) && n >= 0 ||
            payload.StartsWith("locations-", StringComparison.Ordinal) && payload.EndsWith(".bin", StringComparison.Ordinal);
    }

    public static IEnumerable<string> EnumeratePagedIndexFiles(string delta, OverlayIdentity identity)
    {
        if (identity.Version != 3 || !Directory.Exists(delta)) yield break;
        foreach (string directory in Directory.EnumerateDirectories(delta, "index-*"))
        {
            ValidateIndexOwner(directory, identity);
            foreach (string path in OrderedIndexFiles(directory)) yield return path;
            yield return Path.Combine(directory, "writer.lock");
        }
    }
    private static void ValidateIndexOwner(string directory, OverlayIdentity identity)
    {
        WorkspaceStore.EnsurePlainPath(directory);
        if (!IndexName(Path.GetFileName(directory))) throw PagedInvalid();
        string owner = Path.Combine(directory, "owner.json"); WorkspaceStore.EnsurePlainPath(owner);
        if (new FileInfo(owner).Length is <= 0 or > 4096) throw PagedInvalid();
        try
        {
            var saved = JsonSerializer.Deserialize<PagedIndexOwner>(File.ReadAllBytes(owner), new JsonSerializerOptions(JsonSerializerDefaults.Web));
            if (saved?.Version != 1 || saved.OwnerHash != PagedOwnerHash(identity)) throw PagedInvalid();
        }
        catch (JsonException) { throw PagedInvalid(); }
    }

    // Called only from an already authorized stopped-delta reclamation. Every
    // index has its own streamed inventory outside its directory; a shared
    // reader lease blocks deletion before the first file can disappear.
    public static void ReclaimPagedIndices(string delta, OverlayIdentity identity, IJournalFilePins? filePins = null, Action<string>? boundary = null)
        => ReclaimPagedIndicesCore(delta, identity, filePins, boundary, null);

    internal static void ReclaimStoppedPagedIndices(string delta, OverlayIdentity identity, IJournalFilePins? filePins,
        Action<string>? boundary, StoppedIndexContext context)
        => ReclaimPagedIndicesCore(delta, identity, filePins, boundary, context);

    private static void ReclaimPagedIndicesCore(string delta, OverlayIdentity identity, IJournalFilePins? filePins,
        Action<string>? boundary, StoppedIndexContext? context)
    {
        if (identity.Version != 3 || !Directory.Exists(delta)) return;
        string control = Path.GetDirectoryName(delta)!;
        RecoverIndexResiduals(control, delta, identity, filePins, boundary);
        if (context is not null && filePins is not null) RecoverStoppedIndexResiduals(control, delta, identity, filePins, context, boundary);
        string captures = Path.Combine(Path.GetDirectoryName(delta)!, "captures");
        foreach (string directory in Directory.EnumerateDirectories(delta, "index-*"))
        {
            string receipt = directory + ".reclaim";
            if (!File.Exists(receipt) && !File.Exists(receipt + ".pending"))
            {
                ValidateIndexOwner(directory, identity);
                if (Directory.Exists(captures))
                    foreach (string captureDirectory in Directory.EnumerateDirectories(captures))
                    {
                        if (!File.Exists(Path.Combine(captureDirectory, "capture.json"))) continue;
                        var capture = SourceCaptureTree.ReadCapture(captureDirectory);
                        if (capture.Paged?.Directory != directory) continue;
                        if (!File.Exists(Path.Combine(captureDirectory, "materialized.json")))
                            throw new WorkspaceException("SourceCapturePending", "Materialize the protected result before reclaiming its index root.");
                        SourceCaptureTree.RequireMaterialized(captureDirectory, capture);
                        if (Directory.EnumerateFiles(captureDirectory, "*.pin").Any())
                            throw new WorkspaceException("SourceCapturePending", "Complete the exact protected backing-alias cleanup before reclaiming its ownership root.");
                    }
            }
            ReclaimPagedIndex(directory, identity, filePins, control, boundary, context);
        }
        // An interrupted final directory removal leaves only its external
        // receipt. Resume that exact inventory before the parent can disappear.
        foreach (string receipt in Directory.EnumerateFiles(delta, "index-*.reclaim"))
        { string directory = receipt[..^8]; if (!Directory.Exists(directory)) ReclaimPagedIndex(directory, identity, filePins, control, boundary, context); }
    }

    private sealed record IndexProof(string Name, long Length, byte[] Hash, string? Identity = null);
    private static IndexProof IndexProofOf(string path, string? name = null, IJournalFilePins? pins = null)
    {
        WorkspaceStore.EnsurePlainPath(path);
        using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        string? identity = pins?.IdentifyOpened(input);
        long length = input.Length; byte[] hash = SHA256.HashData(input);
        if (input.Length != length) throw PagedInvalid(); return new(name ?? Path.GetFileName(path), length, hash, identity);
    }
    private static void WriteIndexInventory(Stream output, string directory, OverlayIdentity identity, IJournalFilePins? pins, FileStream fence)
    {
        using var writer = new BinaryWriter(output, Encoding.UTF8, true);
        writer.Write("SJI2"u8); writer.Write(Convert.FromHexString(PagedOwnerHash(identity)));
        writer.Write(Guid.ParseExact(Path.GetFileName(directory).AsSpan(6), "N").ToByteArray());
        void Record(string path)
        {
            string relative = Path.GetRelativePath(directory, path).Replace('\\', '/');
            var proof = relative == "writer.lock" ? new IndexProof("writer.lock", 0, SHA256.HashData(Array.Empty<byte>()), pins?.IdentifyOpened(fence)) : IndexProofOf(path, relative, pins); byte[] name = Encoding.UTF8.GetBytes(proof.Name);
            if (!IndexFile(proof.Name) || name.Length > 128) throw PagedInvalid();
            writer.Write((ushort)name.Length); writer.Write(name); writer.Write(proof.Length); writer.Write(proof.Hash);
            byte[] native = proof.Identity is null ? [] : Encoding.UTF8.GetBytes(proof.Identity);
            if (native.Length > 4096) throw PagedInvalid(); writer.Write((ushort)native.Length); writer.Write(native);
        }
        foreach (string path in OrderedIndexFiles(directory))
        {
            WorkspaceStore.EnsurePlainPath(path);
            if (!File.Exists(path) || !IndexFile(Path.GetRelativePath(directory, path).Replace('\\', '/'))) throw PagedInvalid();
            if (Path.GetFileName(path) != "writer.lock") Record(path);
        }
        Record(Path.Combine(directory, "writer.lock")); writer.Write((ushort)0);
    }
    private static IEnumerable<string> OrderedIndexFiles(string directory)
    {
        IEnumerable<MetadataEntry> Names()
        {
            foreach (string path in Directory.EnumerateFileSystemEntries(directory))
            {
                WorkspaceStore.EnsurePlainPath(path); string name = Path.GetFileName(path);
                if (Directory.Exists(path))
                {
                    if (name != "collections") throw PagedInvalid();
                    foreach (string child in Directory.EnumerateFileSystemEntries(path))
                    {
                        WorkspaceStore.EnsurePlainPath(child); string relative = "collections/" + Path.GetFileName(child);
                        if (!File.Exists(child) || !IndexFile(relative)) throw PagedInvalid();
                        yield return new(relative, ReadOnlyMemory<byte>.Empty);
                    }
                    continue;
                }
                if (!File.Exists(path) || !IndexFile(name)) throw PagedInvalid();
                if (name != "writer.lock") yield return new(name, ReadOnlyMemory<byte>.Empty);
            }
        }
        foreach (var name in MetadataExternalSort.Order(Names(), Path.GetTempPath(), default)) yield return Path.Combine(directory, name.Key);
    }
    private static IEnumerable<IndexProof> ReadIndexInventory(string receipt, string directory, OverlayIdentity identity)
    {
        WorkspaceStore.EnsurePlainPath(receipt);
        using var reader = new BinaryReader(new FileStream(receipt, FileMode.Open, FileAccess.Read, FileShare.Read), Encoding.UTF8);
        if (!reader.ReadBytes(4).AsSpan().SequenceEqual("SJI2"u8) || !reader.ReadBytes(32).AsSpan().SequenceEqual(Convert.FromHexString(PagedOwnerHash(identity))) ||
            !reader.ReadBytes(16).AsSpan().SequenceEqual(Guid.ParseExact(Path.GetFileName(directory).AsSpan(6), "N").ToByteArray())) throw PagedInvalid();
        bool last = false; string? prior = null;
        while (true)
        {
            if (reader.BaseStream.Length - reader.BaseStream.Position < 2) throw PagedInvalid();
            int length = reader.ReadUInt16(); if (length == 0) break;
            if (last || length > 128 || length > reader.BaseStream.Length - reader.BaseStream.Position - 42) throw PagedInvalid();
            string name = Encoding.UTF8.GetString(reader.ReadBytes(length)); long bytes = reader.ReadInt64(); byte[] hash = reader.ReadBytes(32);
            int nativeLength = reader.ReadUInt16(); if (nativeLength > 4096 || nativeLength > reader.BaseStream.Length - reader.BaseStream.Position) throw PagedInvalid();
            string? native = nativeLength == 0 ? null : Encoding.UTF8.GetString(reader.ReadBytes(nativeLength));
            if (!IndexFile(name) || bytes < 0 || hash.Length != 32 || name != "writer.lock" && prior is not null && StringComparer.Ordinal.Compare(name, prior) <= 0) throw PagedInvalid();
            prior = name;
            last = name == "writer.lock"; yield return new(name, bytes, hash, native);
        }
        if (!last || reader.BaseStream.Position != reader.BaseStream.Length) throw PagedInvalid();
    }
    private static void ReclaimPagedIndex(string directory, OverlayIdentity identity, IJournalFilePins? filePins = null,
        string? preservationControl = null, Action<string>? boundary = null, StoppedIndexContext? stopped = null)
    {
        preservationControl ??= Path.GetDirectoryName(Path.GetDirectoryName(directory)!)!;
        string receipt = directory + ".reclaim", pending = receipt + ".pending";
        WorkspaceStore.EnsurePlainPath(directory); WorkspaceStore.EnsurePlainPath(receipt); WorkspaceStore.EnsurePlainPath(pending);
        if (!IndexName(Path.GetFileName(directory)) || File.Exists(receipt) && File.Exists(pending)) throw PagedInvalid();
        if (!Directory.Exists(directory))
        {
            if (filePins is not null && LatestIndexResidual(ResidualControl(directory, identity, preservationControl), identity) is { } residual)
            {
                if (residual.Phase == "completed") return;
                using var restored = new FileStream(Path.Combine(residual.Directory, "writer.lock"), FileMode.Open, FileAccess.Read, FileShare.Delete);
                PreserveOpaqueIndex(directory, identity, filePins, restored, preservationControl, boundary); return;
            }
            if (!File.Exists(receipt)) throw PagedInvalid();
            foreach (var _ in ReadIndexInventory(receipt, directory, identity)) { }
            File.Delete(receipt); return;
        }
        string lease = Path.Combine(directory, "writer.lock");
        // FileShare.Delete permits our exact final unlink while denying new
        // page readers and writers for the whole multi-file deletion interval.
        using var fence = File.Exists(lease) ? new FileStream(lease, FileMode.Open, FileAccess.Read, FileShare.Delete) : null;
        if (!File.Exists(receipt))
        {
            ValidateIndexOwner(directory, identity);
            if (fence is null || fence.Length != 0) throw PagedInvalid();
            if (stopped is not null && filePins is not null && !File.Exists(pending) &&
                (LatestStoppedResidual(StoppedResidualRoot(preservationControl, directory, identity), identity) is not null ||
                 HasUnownedStoppedPayload(directory, PagedMetadataStore.ReadIdentity(directory), filePins)))
            { PreserveWholeStoppedIndex(directory, identity, filePins, fence, preservationControl, stopped, boundary); return; }
            if (!File.Exists(pending) && filePins is not null &&
                (LatestIndexResidual(ResidualControl(directory, identity, preservationControl), identity) is not null ||
                 HasOpaqueIndexHeader(directory, PagedMetadataStore.ReadIdentity(directory), filePins)))
            { PreserveOpaqueIndex(directory, identity, filePins, fence, preservationControl, boundary); return; }
            if (Directory.EnumerateFiles(directory, "pages-*.bin.owner").Any())
            {
                if (filePins is null) throw new WorkspaceException("ContentReclaimUnsupported", "Native metadata ownership must be verified before reclamation.");
                PagedMetadataStore.ValidateReclaimInventory(directory, filePins, new HashSet<string>(StringComparer.Ordinal) { "owner.json" });
            }
            if (File.Exists(pending))
            {
                using (var compare = new BoundedCandidatePrefixVerifier(pending)) { WriteIndexInventory(compare, directory, identity, filePins, fence); compare.Complete(); }
                File.Delete(pending);
            }
            using (var output = new FileStream(pending, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
            { WriteIndexInventory(output, directory, identity, filePins, fence); output.Flush(true); }
            File.Move(pending, receipt);
        }
        // Validate the full inventory before deleting any file. Its own record
        // is streamed twice rather than loaded into a project-sized structure.
        using (var remaining = OrderedIndexFiles(directory).GetEnumerator())
        {
            bool has = remaining.MoveNext();
            foreach (var proof in ReadIndexInventory(receipt, directory, identity))
            {
                if (proof.Name == "writer.lock") break;
                if (!has) continue;
                int order = StringComparer.Ordinal.Compare(Path.GetRelativePath(directory, remaining.Current).Replace('\\', '/'), proof.Name);
                if (order < 0) throw PagedInvalid();
                if (order == 0) has = remaining.MoveNext();
            }
            if (has) throw PagedInvalid();
        }
        foreach (var proof in ReadIndexInventory(receipt, directory, identity))
        {
            string path = Path.Combine(directory, proof.Name);
            if (!File.Exists(path)) continue;
            var actual = proof.Name == "writer.lock" && fence is not null ? new IndexProof(proof.Name, fence.Length, SHA256.HashData(Array.Empty<byte>())) : IndexProofOf(path);
            if (actual.Length != proof.Length || !actual.Hash.AsSpan().SequenceEqual(proof.Hash)) throw PagedInvalid();
            if (proof.Identity is not null)
            {
                if (filePins is null) throw new WorkspaceException("ContentReclaimUnsupported", "Resume with the original native metadata deletion adapter.");
                if (proof.Name == "writer.lock") filePins.Verify(fence!, proof.Identity);
                else { using var opened = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete); filePins.Verify(opened, proof.Identity); }
            }
        }
        foreach (var proof in ReadIndexInventory(receipt, directory, identity))
        {
            string path = Path.Combine(directory, proof.Name); if (!File.Exists(path)) continue;
            if (proof.Name != "writer.lock")
            { var actual = IndexProofOf(path); if (actual.Length != proof.Length || !actual.Hash.AsSpan().SequenceEqual(proof.Hash)) throw PagedInvalid(); }
            if (proof.Name == "writer.lock")
                foreach (string child in Directory.EnumerateDirectories(directory))
                { if (Path.GetFileName(child) != "collections" || Directory.EnumerateFileSystemEntries(child).Any()) throw PagedInvalid(); Directory.Delete(child); }
            if (proof.Identity is not null) filePins!.Unpin(path, proof.Identity); else File.Delete(path);
        }
        // Native disposition can keep the last marker name delete-pending
        // until this retained owner handle closes. No replacement can open it
        // between exact unlink and closing that same handle.
        fence?.Dispose();
        if (Directory.EnumerateFileSystemEntries(directory).Any()) throw PagedInvalid();
        Directory.Delete(directory); File.Delete(receipt);
    }
}
