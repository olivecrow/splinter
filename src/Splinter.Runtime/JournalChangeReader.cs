using System.Buffers.Binary;
using System.Text.Json;
using System.Text;
using System.Security.Cryptography;
using Splinter.Core;

namespace Splinter.Runtime;

public sealed partial class JournalDeltaStore
{
    private SourceChangeView ObservePagedChanges(string[] managedPaths)
    {
        if (readOnlyReplay is not null)
            throw new WorkspaceException("SourceObservationUnavailable", "Observe current change roots through the exact owned live provider; offline replay metadata is scoped and cannot be published as rebindable IPC roots.");
        MetadataTreeRoot fixedChanges, fixedNames, fixedNodes, fixedCaptures; string directory; BoundChangeReader lease;
        using (EnterGate())
        {
            Check(); fixedChanges = ChangeRoot!; fixedNames = pathComponents!.Root; fixedNodes = NodeRoot!; fixedCaptures = captureRoot!; directory = pagedIndexDirectory!;
            pagedIndex!.Flush(); pagedIndexCaptured = true; lease = new(directory);
        }
        using (lease)
        {
        long count = 0, last = 0;
        using var digest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var components = new PathComponents(lease.Store) { Root = fixedNames };
        foreach (var item in lease.Store.Open(fixedChanges).Enumerate())
        {
            var pair = UnpackPath(item.Value.ToArray(), components, fixedNames);
            if (pair.Value.Length != 8) throw PagedInvalid(); long sequence = BinaryPrimitives.ReadInt64LittleEndian(pair.Value);
            if (pair.Path.Length == 0 || pathPolicy.Managed(pair.Path, managedPaths)) continue;
            count = checked(count + 1); last = Math.Max(last, sequence);
            AppendChange(digest, pair.Path, sequence);
        }
        var index = new SourceChangeIndex(1, directory, fixedChanges, fixedNames, managedPaths.ToArray(), count, pathPolicy.CaseSensitive,
            Convert.ToHexStringLower(digest.GetHashAndReset()), fixedNodes, fixedCaptures);
        var view = new SourceChangeView(Identity.Id, Identity.Snapshot.Id, epoch, last, []) { Index = index };
        lease.Bind(view);
        return view with { ReaderLease = lease };
        }
    }

    public static SourceChangeView BindChangeView(SourceChangeView view, string deltaDirectory, OverlayIdentity expectedIdentity)
    {
        var index = view.Index ?? throw new WorkspaceException("SourceObservationInvalid", "Current overlays require a paged change index.");
        WorkspaceStore.EnsurePlainPath(index.Directory);
        if (index.Version != 1 || index.Count < 0 || index.ContentHash is not { Length: 64 } || index.ContentHash.Any(c => c is not (>= '0' and <= '9' or >= 'a' and <= 'f')) || index.ManagedPaths is null || index.ManagedPaths.Any(string.IsNullOrWhiteSpace) ||
            view.WorkspaceId != expectedIdentity.Id || view.Sequence < 0 || view.Changes is not { Length: 0 } ||
            !WorkspaceStore.SamePath(Path.GetDirectoryName(Path.GetFullPath(index.Directory))!, deltaDirectory)) throw PagedInvalid();
        string identityPath = Path.Combine(deltaDirectory, "identity.json"); WorkspaceStore.EnsurePlainPath(identityPath);
        using var input = new FileStream(identityPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var identity = JsonSerializer.Deserialize<OverlayIdentity>(input) ?? throw PagedInvalid();
        if (identity.Version != 3 || identity.Id != expectedIdentity.Id) throw PagedInvalid();
        ValidateIndexOwner(index.Directory, identity);
        using var lease = new BoundChangeReader(index.Directory);
        {
            _ = lease.Store.Open(index.Changes); _ = lease.Store.Open(index.Names); _ = lease.Store.Open(index.ContentNodes ?? throw PagedInvalid());
            _ = lease.Store.Open(index.ContentCaptures ?? throw PagedInvalid()); lease.Bind(view); return view with { ReaderLease = lease };
        }
    }

    private sealed class BoundChangeReader : ISourceChangeReaderLease
    {
        private sealed class Shared(string directory)
        {
            public readonly PagedMetadataStore Store = new(directory);
            public SourceChangeView? Header;
            public long References = 1;
        }
        private readonly Shared shared;
        private readonly object lifetime = new();
        private bool disposed;
        public PagedMetadataStore Store => shared.Store;
        public BoundChangeReader(string directory) => shared = new(directory);
        private BoundChangeReader(Shared shared) => this.shared = shared;
        public void Bind(SourceChangeView view)
        {
            if (shared.Header is not null) throw PagedInvalid();
            // The detached header contains no reader owner and cannot keep an
            // otherwise finished view alive through a captured delegate.
            shared.Header = new(view.WorkspaceId, view.SnapshotId, view.Epoch, view.Sequence, view.Changes) { Index = view.Index };
        }
        public ISourceChangeReaderLease Retain()
        {
            lock (lifetime)
            {
                ObjectDisposedException.ThrowIf(disposed, this);
                Interlocked.Increment(ref shared.References); return new BoundChangeReader(shared);
            }
        }
        public IEnumerable<SourcePathChange> Read(SourceChangeView view)
        {
            using var retained = (BoundChangeReader)Retain();
            var header = shared.Header ?? throw PagedInvalid(); var expected = header.Index; var actual = view.Index;
            if (view.WorkspaceId != header.WorkspaceId || view.SnapshotId != header.SnapshotId || view.Epoch != header.Epoch || view.Sequence != header.Sequence ||
                expected is null || actual is null || actual.Version != expected.Version || !WorkspaceStore.SamePath(actual.Directory, expected.Directory) ||
                actual.Changes != expected.Changes || actual.Names != expected.Names || actual.ContentNodes != expected.ContentNodes || actual.ContentCaptures != expected.ContentCaptures ||
                actual.Count != expected.Count || actual.CaseSensitive != expected.CaseSensitive || actual.ContentHash != expected.ContentHash ||
                !actual.ManagedPaths.SequenceEqual(expected.ManagedPaths)) throw PagedInvalid();
            foreach (var change in EnumerateChangeIndex(view, Store)) yield return change;
        }
        ~BoundChangeReader() { try { Release(); } catch { } }
        private void Release()
        {
            lock (lifetime)
            {
                if (disposed) return; disposed = true;
                if (Interlocked.Decrement(ref shared.References) == 0) Store.Dispose();
            }
        }
        public void Dispose() { Release(); GC.SuppressFinalize(this); }
    }

    private static IEnumerable<SourcePathChange> EnumerateChangeIndex(SourceChangeView view, PagedMetadataStore? bound = null)
    {
        var descriptor = view.Index ?? throw PagedInvalid();
        using var opened = bound is null ? new PagedMetadataStore(descriptor.Directory) : null;
        var store = bound ?? opened!;
        var components = new PathComponents(store) { Root = descriptor.Names };
        var policy = descriptor.CaseSensitive ? WorkspacePathPolicy.Posix : WorkspacePathPolicy.Windows;
        long count = 0, last = 0;
        using var digest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        // The metadata roots and reader lease are retained throughout the
        // cursor, including between callbacks and across concurrent mutations.
        foreach (var item in store.Open(descriptor.Changes).Enumerate())
        {
            var pair = UnpackPath(item.Value.ToArray(), components, descriptor.Names);
            if (pair.Value.Length != 8) throw PagedInvalid();
            long sequence = BinaryPrimitives.ReadInt64LittleEndian(pair.Value);
            if (sequence < 1 || sequence > view.Sequence && !policy.Managed(pair.Path, descriptor.ManagedPaths) && pair.Path.Length != 0) throw PagedInvalid();
            if (pair.Path.Length == 0 || policy.Managed(pair.Path, descriptor.ManagedPaths)) continue;
            count = checked(count + 1); last = Math.Max(last, sequence); AppendChange(digest, pair.Path, sequence); yield return new(pair.Path, sequence);
        }
        if (count != descriptor.Count || last != view.Sequence || Convert.ToHexStringLower(digest.GetHashAndReset()) != descriptor.ContentHash) throw PagedInvalid();
    }
    private static void AppendChange(IncrementalHash digest, string path, long sequence)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(path); Span<byte> header = stackalloc byte[12];
        BinaryPrimitives.WriteInt32LittleEndian(header, bytes.Length); BinaryPrimitives.WriteInt64LittleEndian(header[4..], sequence);
        digest.AppendData(header); digest.AppendData(bytes);
    }
}
