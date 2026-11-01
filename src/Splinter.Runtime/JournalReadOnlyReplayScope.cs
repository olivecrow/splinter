using System.Security.Cryptography;
using System.Text.Json;
using Splinter.Core;

namespace Splinter.Runtime;

public sealed record PreservedJournalReplay(string Directory, string Request, string Reason);

// Offline replay may append derived index pages, but never to the frozen
// source index. The reader lock and retained cursor leases keep this one
// separately owned scratch namespace alive until its last consumer finishes.
internal sealed class JournalReadOnlyReplayScope : IDisposable
{
    private sealed record Owner(int Version, string Request, string Overlay, string Source, string SourceStore,
        string Directory, string DirectoryIdentity, string MetadataIdentity, string ReaderIdentity,
        string IdentityIdentity, string WriterIdentity, bool Native, string Digest = "");
    private sealed record Phase(int Version, string Request, string OwnerDigest, string Name, string Reason, string Digest = "");
    private static readonly HashSet<string> ScopeNames = new(StringComparer.Ordinal) { "reader.lock", "metadata" };
    private readonly object gate = new();
    private readonly string parent;
    private readonly Owner owner;
    private readonly IJournalFilePins? pins;
    private readonly PagedMetadataStore source;
    private readonly List<IDisposable> retained = [];
    private IDisposable? scopeFence, metadataFence, sourceFence;
    private FileStream? reader, identityPin, writerPin;
    private int references = 1;
    private bool ownerDisposed;
    public PagedMetadataStore Store { get; }
    public string ScratchDirectory => owner.Directory;

    private JournalReadOnlyReplayScope(string parent, Owner owner, IJournalFilePins? pins, PagedMetadataStore source,
        PagedMetadataStore derived, IDisposable? scopeFence, IDisposable? metadataFence, IDisposable? sourceFence,
        FileStream reader, FileStream identityPin, FileStream writerPin)
    {
        this.parent = parent; this.owner = owner; this.pins = pins; this.source = source; Store = derived;
        this.scopeFence = scopeFence; this.metadataFence = metadataFence; this.sourceFence = sourceFence;
        this.reader = reader; this.identityPin = identityPin; this.writerPin = writerPin;
    }

    internal static JournalReadOnlyReplayScope Create(string sourceDirectory, string controlParent,
        OverlayIdentity identity, IJournalFilePins? pins, Action<string>? boundary = null)
    {
        sourceDirectory = Path.GetFullPath(sourceDirectory);
        string parent = Path.Combine(Path.GetFullPath(controlParent), "read-only-replays");
        WorkspaceStore.EnsurePlainPath(parent); Directory.CreateDirectory(parent);
        using var parentFence = pins?.PinDirectory(parent, pins.IdentifyDirectory(parent));
        string overlay = Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(identity)));
        using var operation = Operation(parent);
        Recover(parent, overlay, pins);
        string request = Guid.NewGuid().ToString("N"), directory = Path.Combine(parent, request), metadata = Path.Combine(directory, "metadata");
        PagedMetadataStore? source = null, derived = null;
        IDisposable? scopeFence = null, metadataFence = null, sourceFence = null;
        FileStream? reader = null, identityPin = null, writerPin = null;
        Owner? owner = null;
        try
        {
            sourceFence = pins?.PinDirectory(sourceDirectory, pins.IdentifyDirectory(sourceDirectory));
            source = new(sourceDirectory, filePins: pins);
            scopeFence = CreateDirectory(directory, pins, out string directoryIdentity);
            boundary?.Invoke("replay.scope-created");
            metadataFence = CreateDirectory(metadata, pins, out string metadataIdentity);
            boundary?.Invoke("replay.metadata-created");
            reader = NewControl(Path.Combine(directory, "reader.lock"), pins, out string readerIdentity);
            // These fixed controls are created and fenced before Derive can
            // write page payload. Only the source identity header is copied.
            string identityPath = Path.Combine(metadata, "identity.bin"), writerPath = Path.Combine(metadata, "writer.lock");
            string identityIdentity, writerIdentity;
            using (var output = NewControl(identityPath, pins, out identityIdentity))
            {
                byte[] header = new byte[48]; PagedMetadataStore.FormatMagic.CopyTo(header);
                Convert.FromHexString(source.StoreId).CopyTo(header, 4); SHA256.HashData(header.AsSpan(0, 16), header.AsSpan(16));
                output.Write(header); output.Flush(true);
            }
            using (var output = NewControl(writerPath, pins, out writerIdentity)) output.Flush(true);
            identityPin = new(identityPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            writerPin = new(writerPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            Verify(identityPin, identityIdentity, pins); Verify(writerPin, writerIdentity, pins);
            boundary?.Invoke("replay.controls-created");
            owner = new(1, request, overlay, sourceDirectory, source.StoreId, directory, directoryIdentity,
                metadataIdentity, readerIdentity, identityIdentity, writerIdentity, pins is not null);
            owner = owner with { Digest = JournalControlIntegrity.Digest(owner) };
            Publish(Path.Combine(parent, request + ".owner.json"), owner);
            boundary?.Invoke("replay.owner-durable");
            derived = source.Derive(metadata, pins);
            boundary?.Invoke("replay.derived-created");
            return new(parent, owner, pins, source, derived, scopeFence, metadataFence, sourceFence, reader, identityPin, writerPin);
        }
        catch
        {
            derived?.Dispose(); source?.Dispose(); identityPin?.Dispose(); writerPin?.Dispose(); reader?.Dispose();
            metadataFence?.Dispose(); scopeFence?.Dispose(); sourceFence?.Dispose();
            // Before the durable owner, every creation gap stays opaque. A
            // later invocation exposes it and uses another fresh namespace.
            if (owner is not null && File.Exists(Path.Combine(parent, request + ".owner.json")))
                ReclaimOrPreserve(parent, owner, pins);
            throw;
        }
    }

    public IDisposable Retain()
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(references == 0, this);
            references = checked(references + 1); return new ReaderLease(this);
        }
    }
    public void OwnUntilReleased(IDisposable resource)
    {
        ArgumentNullException.ThrowIfNull(resource);
        lock (gate) { ObjectDisposedException.ThrowIf(ownerDisposed || references == 0, this); retained.Add(resource); }
    }
    public void Dispose()
    {
        lock (gate) { if (ownerDisposed) return; ownerDisposed = true; }
        Release();
    }
    private void Release()
    {
        lock (gate) { if (--references != 0) return; }
        try { Store.Dispose(); }
        finally
        {
            try { source.Dispose(); }
            finally
            {
                try
                {
                    Exception? first = null;
                    foreach (var resource in retained) try { resource.Dispose(); } catch (Exception error) { first ??= error; }
                    if (first is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(first).Throw();
                }
                finally
                {
                    identityPin?.Dispose(); identityPin = null; writerPin?.Dispose(); writerPin = null;
                    reader?.Dispose(); reader = null; metadataFence?.Dispose(); metadataFence = null; scopeFence?.Dispose(); scopeFence = null;
                    sourceFence?.Dispose(); sourceFence = null;
                    using var parentFence = pins?.PinDirectory(parent, pins.IdentifyDirectory(parent));
                    using var operation = Operation(parent); ReclaimOrPreserve(parent, owner, pins);
                }
            }
        }
    }
    private sealed class ReaderLease(JournalReadOnlyReplayScope owner) : IDisposable
    {
        private JournalReadOnlyReplayScope? scope = owner;
        public void Dispose() { Interlocked.Exchange(ref scope, null)?.Release(); GC.SuppressFinalize(this); }
        ~ReaderLease() { try { Interlocked.Exchange(ref scope, null)?.Release(); } catch { /* Durable owner permits later exact recovery. */ } }
    }

    private static FileStream Operation(string parent)
    {
        string path = Path.Combine(parent, "operation.lock"); WorkspaceStore.EnsurePlainPath(path);
        FileStream lease;
        while (true)
        {
            try { lease = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); break; }
            catch (IOException error) when ((error.HResult & 0xffff) == 32) { Thread.Sleep(10); }
        }
        if (lease.Length != 0) { lease.Dispose(); throw Invalid("Preserve a changed replay coordination control."); }
        return lease;
    }
    private static IDisposable? CreateDirectory(string directory, IJournalFilePins? pins, out string identity)
    {
        WorkspaceStore.EnsurePlainPath(directory);
        if (pins is not null) return pins.CreateDirectory(directory, out identity);
        if (Path.Exists(directory)) throw Invalid("Preserve an occupied synthetic replay directory.");
        Directory.CreateDirectory(directory); identity = ""; return null;
    }
    private static FileStream NewControl(string path, IJournalFilePins? pins, out string identity)
    {
        WorkspaceStore.EnsurePlainPath(path);
        var output = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read);
        try { identity = Identify(output, pins); return output; } catch { output.Dispose(); throw; }
    }
    private static string Identify(FileStream input, IJournalFilePins? pins) => pins?.IdentifyOpened(input) ?? "";
    private static void Verify(FileStream input, string expected, IJournalFilePins? pins)
    { if (pins is not null) pins.VerifyPath(input, input.Name, expected); else if (expected.Length != 0) throw Invalid("Preserve a native replay scope without its ownership adapter."); }
    private static void Publish<T>(string path, T value)
    {
        WorkspaceStore.EnsurePlainPath(path);
        if (File.Exists(path))
        {
            if (JournalControlIntegrity.Digest(JournalControlIntegrity.Read<T>(path)) != JournalControlIntegrity.Digest(value))
                throw Invalid("A fixed replay control changed.");
            return;
        }
        string pending = path + "." + Guid.NewGuid().ToString("N") + ".pending";
        using (var output = new FileStream(pending, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
        { JsonSerializer.Serialize(output, value, JournalControlIntegrity.Json); output.Flush(true); }
        File.Move(pending, path);
    }
    private static Owner ReadOwner(string path, string overlay)
    {
        var owner = JournalControlIntegrity.Read<Owner>(path); string parent = Path.GetDirectoryName(path)!;
        if (owner.Version != 1 || owner.Request.Length != 32 || !Guid.TryParseExact(owner.Request, "N", out _) ||
            Path.GetFileName(path) != owner.Request + ".owner.json" || owner.Overlay != overlay ||
            !WorkspaceStore.SamePath(owner.Directory, Path.Combine(parent, owner.Request)) || owner.SourceStore.Length != 24 ||
            owner.Digest != JournalControlIntegrity.Digest(owner with { Digest = "" })) throw Invalid("Preserve an unsupported replay owner.");
        return owner;
    }
    private static bool HasPhase(string parent, Owner owner, string phase)
    {
        string path = Path.Combine(parent, owner.Request + "." + phase + ".json"); if (!File.Exists(path)) return false;
        var value = JournalControlIntegrity.Read<Phase>(path);
        if (value.Version != 1 || value.Request != owner.Request || value.OwnerDigest != owner.Digest || value.Name != phase ||
            value.Digest != JournalControlIntegrity.Digest(value with { Digest = "" })) throw Invalid("Preserve a changed replay cleanup phase.");
        return true;
    }
    private static void Mark(string parent, Owner owner, string phase, string reason = "")
    {
        var value = new Phase(1, owner.Request, owner.Digest, phase, reason);
        value = value with { Digest = JournalControlIntegrity.Digest(value) };
        Publish(Path.Combine(parent, owner.Request + "." + phase + ".json"), value);
    }
    private static void Recover(string parent, string overlay, IJournalFilePins? pins)
    {
        // Completed scopes have no directory. Do not reparse all historical
        // receipts whenever another read-only operation starts.
        foreach (string directory in Directory.EnumerateDirectories(parent))
        {
            string path = Path.Combine(parent, Path.GetFileName(directory) + ".owner.json");
            if (!File.Exists(path)) continue; // Opaque creation gap, never adopted.
            var owner = ReadOwner(path, overlay);
            if (owner.Native != (pins is not null)) throw Invalid("Preserve a replay owner from another ownership mode.");
            if (!HasPhase(parent, owner, "completed")) ReclaimOrPreserve(parent, owner, pins);
        }
    }
    private static void ReclaimOrPreserve(string parent, Owner owner, IJournalFilePins? pins)
    {
        try { if (Reclaim(parent, owner, pins)) return; }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or WorkspaceException)
        { Mark(parent, owner, "preserved", "ownership-or-I/O-conflict"); return; }
        // A live reader is not a preserved residual; it retains its exact
        // directory and its accepted owner until that reader releases it.
    }
    private static bool Reclaim(string parent, Owner owner, IJournalFilePins? pins)
    {
        bool cleared = HasPhase(parent, owner, "payload-cleared");
        if (!Directory.Exists(owner.Directory))
        { if (!cleared) throw Invalid("Preserve a missing owned replay directory."); Mark(parent, owner, "completed"); return true; }
        using var directory = new Once(pins?.PinDirectory(owner.Directory, owner.DirectoryIdentity));
        string readerPath = Path.Combine(owner.Directory, "reader.lock"), metadata = Path.Combine(owner.Directory, "metadata");
        FileStream? reader = null;
        if (File.Exists(readerPath))
        {
            try { reader = new(readerPath, FileMode.Open, FileAccess.Read, FileShare.Delete); }
            catch (IOException error) when ((error.HResult & 0xffff) == 32) { return false; }
            try { Verify(reader, owner.ReaderIdentity, pins); if (reader.Length != 0) throw Invalid("Preserve a changed replay reader control."); }
            catch { reader.Dispose(); throw; }
        }
        else if (!cleared) throw Invalid("Preserve a missing replay reader control.");
        try
        {
            foreach (string path in Directory.EnumerateFileSystemEntries(owner.Directory))
                if (!ScopeNames.Contains(Path.GetFileName(path))) throw Invalid("Preserve an unknown replay scratch item.");
            if (Directory.Exists(metadata))
            {
                using (var metadataFence = pins?.PinDirectory(metadata, owner.MetadataIdentity))
                {
                    string identityPath = Path.Combine(metadata, "identity.bin"), writerPath = Path.Combine(metadata, "writer.lock");
                    if (!cleared)
                    {
                        using var identity = new FileStream(identityPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                        Verify(identity, owner.IdentityIdentity, pins);
                        if (PagedMetadataStore.ReadIdentity(metadata) != owner.SourceStore) throw Invalid("Preserve changed replay metadata identity.");
                        using var writer = new FileStream(writerPath, FileMode.Open, FileAccess.Read, FileShare.Delete);
                        Verify(writer, owner.WriterIdentity, pins); if (writer.Length != 0) throw Invalid("Preserve a changed replay writer control.");
                        if (pins is not null)
                        {
                            PagedMetadataStore.ValidateReclaimInventory(metadata, pins);
                            foreach (var file in PagedMetadataStore.EnumerateOwnedPayloads(metadata, pins)) pins.Unpin(file.Path, file.Identity);
                            // Checksummed ownership controls authorize only
                            // their already validated metadata namespace.
                            foreach (string path in Directory.EnumerateFiles(metadata, "pages-*.bin.owner")) File.Delete(path);
                        }
                        else
                        {
                            foreach (string path in Directory.EnumerateFileSystemEntries(metadata))
                                if (Directory.Exists(path) || Path.GetFileName(path) is not ("identity.bin" or "writer.lock") &&
                                    !(Path.GetFileName(path).StartsWith("pages-", StringComparison.Ordinal) && path.EndsWith(".bin", StringComparison.Ordinal)))
                                    throw Invalid("Preserve unknown synthetic replay metadata.");
                            foreach (string path in Directory.EnumerateFiles(metadata, "pages-*.bin")) File.Delete(path);
                        }
                        Mark(parent, owner, "payload-cleared"); cleared = true;
                    }
                    foreach (string path in Directory.EnumerateFileSystemEntries(metadata))
                        if (Directory.Exists(path) || Path.GetFileName(path) is not ("identity.bin" or "writer.lock"))
                            throw Invalid("Preserve metadata appearing during replay cleanup.");
                    RemoveControl(identityPath, owner.IdentityIdentity, pins, expectedIdentity: owner.SourceStore);
                    RemoveControl(writerPath, owner.WriterIdentity, pins);
                }
                if (pins is not null) pins.UnpinDirectory(metadata, owner.MetadataIdentity); else Directory.Delete(metadata);
            }
            else if (!cleared) throw Invalid("Preserve missing replay metadata before cleanup publication.");
            reader?.Dispose(); reader = null; RemoveControl(readerPath, owner.ReaderIdentity, pins);
        }
        finally { reader?.Dispose(); }
        // Release the no-delete directory fence before exact Native unlink.
        directory.Dispose();
        if (pins is not null) pins.UnpinDirectory(owner.Directory, owner.DirectoryIdentity); else Directory.Delete(owner.Directory);
        Mark(parent, owner, "completed"); return true;
    }
    private static void RemoveControl(string path, string identity, IJournalFilePins? pins, string? expectedIdentity = null)
    {
        if (!File.Exists(path)) return;
        using (var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            Verify(input, identity, pins);
            if (expectedIdentity is null ? input.Length != 0 : input.Length != 48 || PagedMetadataStore.ReadIdentity(Path.GetDirectoryName(path)!) != expectedIdentity)
                throw Invalid("Preserve a changed replay control during cleanup.");
        }
        if (pins is not null) pins.Unpin(path, identity); else File.Delete(path);
    }

    public static IEnumerable<PreservedJournalReplay> EnumeratePreserved(string controlParent, OverlayIdentity identity)
    {
        string parent = Path.Combine(Path.GetFullPath(controlParent), "read-only-replays"); if (!Directory.Exists(parent)) yield break;
        string overlay = Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(identity)));
        foreach (string directory in Directory.EnumerateDirectories(parent))
        {
            WorkspaceStore.EnsurePlainPath(directory); string request = Path.GetFileName(directory), path = Path.Combine(parent, request + ".owner.json");
            if (!File.Exists(path)) { yield return new(directory, request, "unpublished-replay-scope-creation-gap"); continue; }
            var owner = ReadOwner(path, overlay);
            if (!HasPhase(parent, owner, "completed")) yield return new(directory, request, HasPhase(parent, owner, "preserved")
                ? "unverified-replay-scratch-preserved" : "read-only-replay-active-or-recovery-pending");
        }
        foreach (string pending in Directory.EnumerateFiles(parent, "*.pending"))
            yield return new(pending, Path.GetFileName(pending), "unpublished-replay-owner-control");
    }
    private sealed class Once(IDisposable? resource) : IDisposable
    {
        private IDisposable? value = resource;
        public void Dispose() => Interlocked.Exchange(ref value, null)?.Dispose();
    }
    private static WorkspaceException Invalid(string message) => new("ReplayScratchConflict", message);
}

public sealed partial class JournalDeltaStore
{
    public static IEnumerable<PreservedJournalReplay> EnumeratePreservedReadOnlyReplays(string control,
        OverlayIdentity identity) => JournalReadOnlyReplayScope.EnumeratePreserved(control, identity);
}
