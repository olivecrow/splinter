using System.Buffers;
using System.Security.Cryptography;
using Splinter.Core;
using Splinter.Runtime;
using Splinter.SourceControl.Git;

namespace Splinter.SourceControl.Files;

public sealed partial class FileWorkspaceProvider
{
    public Task<SourceState> InspectLiveGenerationAsync(WorkspaceRecord w, CancellationToken ct = default)
    {
        Owner(w, Control(w)); RequireMount(w);
        using var current = Manifest(w, Scan(w.Path, w.ManagedPaths, ct, mount: w.Path,
            hashWorkers: w.SourceKind == "plastic" ? 1 : StorageResources.HashWorkers, omitGitRoot: w.SourceKind == "git"));
        using var result = GenerationManifest(w);
        bool changed = current.Identity != result.Identity;
        var differences = changed ? Changed(result.EnumerateEntries(), current.EnumerateEntries(), Control(w), ct) : (Preview: Array.Empty<string>(), Count: 0L);
        return Task.FromResult(new SourceState(result.Identity, current.Identity, changed, differences.Preview) { ChangedPathCount = differences.Count });
    }
    private static string GenerationPath(string control, string id, string name)
    {
        if (id.Length != 24 || id.Any(c => !char.IsAsciiHexDigit(c))) throw Error("Invalid result generation ID.");
        return FilePath(control, "generations/" + id + "/" + name);
    }
    public async Task<WorkspaceResultGeneration> PreserveGenerationAsync(WorkspaceRecord w, string control,
        WorkspaceCaptureIntent intent, SourceCaptureIdentity capture, ISourceCaptureTree tree, CancellationToken ct = default)
    {
        if (capture.Id != intent.Id || tree.Snapshot != w.Snapshot?.Reference || string.IsNullOrWhiteSpace(w.RequestDigest))
            throw Error("Preserve only the exact frozen workspace generation.");
        var diagnostics = ResultGenerationDiagnostics.Open(control, intent.Id);
        string receipt = GenerationPath(control, intent.Id, "generation.json");
        SaveFixed(FilePath(control, "owner.json"), new FileSourceOwner(w.Id, w.RequestDigest!, w.ProjectPath,
            w.Path, w.SourceReference, w.Snapshot!.Reference!));
        if (w.SourceKind == "plastic") PlasticWorkspaceProvider.VerifyReceipt(w, control);
        if (File.Exists(receipt))
        {
            var prior = Read<WorkspaceResultGeneration>(receipt);
            if (prior.Id != intent.Id || prior.RequestKey != intent.RequestKey || prior.Message != intent.Message || prior.CaptureDigest != capture.Digest || prior.Sequence != intent.Sequence)
                throw Error("The result differs from its fixed capture request.");
            using (diagnostics?.Measure("files.verify-receipt-retry")) await VerifyGenerationAsync(w, control, prior, ct);
            return prior;
        }
        string basisPath = FilePath(control, "generation-basis.json");
        bool fixedBasis = File.Exists(basisPath);
        bool referenceResult = w.SourceKind != "git" && contentPins is not null && tree is ISourceCaptureRanges;
        FileSourceManifest baseline;
        using (diagnostics?.Measure("files.basis-scan")) baseline = fixedBasis ? ReadManifest(w, basisPath) : Manifest(w, ScanTree(w, tree.Basis, ct));
        using var baselineLifetime = baseline;
        if (w.SourceKind != "git" && baseline.Identity != w.BaseRevision) throw Error("The generation basis differs from the admitted immutable source.");
        // The comparison basis remains fixed. Native reference results retain
        // their own before/after dependencies after selecting changed paths;
        // full-payload providers keep their existing preservation contract.
        using (diagnostics?.Measure("files.basis-preserve"))
            if (w.SourceKind != "git" && !fixedBasis && !referenceResult) baseline.ContentFiles = PreserveBasisPayloads(w, control, baseline, tree.Basis, ct);
        GitRetainedResultBasisScope? retainedGitBasis;
        using (diagnostics?.Measure("files.retained-git-scope")) retainedGitBasis = w.SourceKind == "git" && contentPins is not null
            ? await GitRetainedResultBasis.PreserveAsync(w, control, ct, contentPins) : null;
        using var retainedGitLifetime = retainedGitBasis;
        FileSourceManifest result;
        using (diagnostics?.Measure("files.result-scan")) result = CaptureManifest();
        using var resultLifetime = result;
        if (referenceResult)
        {
            using (diagnostics?.Measure("files.retention-select")) BindRetainedPayloads(w, baseline, result, (ISourceCaptureRanges)tree, ct);
            using (diagnostics?.Measure("files.basis-preserve-selected"))
                _ = PreserveBasisPayloads(w, control, baseline, tree.Basis, ct, RequiredBasisPayloads(baseline, result, ct));
        }
        using (diagnostics?.Measure("files.basis-header-publish")) SaveFixed(basisPath, baseline);
        FileSourceManifest CaptureManifest()
        {
            var observed = Manifest(w, ScanTree(w, tree, ct, baseline)); string fixedPath = GenerationPath(control, intent.Id, "result.json");
            if (!File.Exists(fixedPath)) return observed;
            using (observed)
            {
                var retained = ReadManifest(w, fixedPath);
                if (retained.Identity == observed.Identity) return retained;
                retained.Dispose(); throw Error("The fixed result manifest differs from this capture; preserve both roots.");
            }
        }
        using (diagnostics?.Measure("files.range-preserve"))
        if (w.SourceKind != "git" || contentPins is not null)
        {
            if (contentPins is not null && tree is ISourceCaptureRanges ranges)
                result.ContentFiles = PreserveRangePayloads(w, control, capture, ranges, tree, baseline, result, ct);
            else PreserveTreePayloads(w, control, result, tree, ct);
        }
        ContentReadScope? independentReader;
        using (diagnostics?.Measure("files.independent-open")) independentReader = w.SourceKind != "git" || contentPins is not null
            ? ContentReader(w, control, result.ContentFiles, retainedGitBasis, baseline, ct) : null;
        using var independentLifetime = independentReader;
        using var catalogs = new PagedMetadataStore.CatalogReadScope(result.Directory!, contentPins);
        using (diagnostics?.Measure("files.independent-verify-1"))
        if (independentReader is not null)
        {
            VerifyPayloads(control, VerificationPayloads(baseline, result, ct, contentPins), ct, independentReader.Reader, contentPins is not null,
                uniqueRetainedRoot: result.RetainedPayloads is not null, verification: independentReader.Verification);
            independentReader.VerifyBoundary();
        }
        if (preparationObserver is not null) await preparationObserver.ReachedAsync("source-payloads-preserved", w, ct);
        using (diagnostics?.Measure("files.capture-boundary-1")) tree.VerifyReadBoundary();
        using (diagnostics?.Measure("files.result-header-publish")) SaveFixed(GenerationPath(control, intent.Id, "result.json"), result);
        if (preparationObserver is not null) await preparationObserver.ReachedAsync("source-result-manifest-published", w, ct);
        string? gitReference = null, gitFingerprint = null;
        if (w.SourceKind == "git" && contentPins is null)
        {
            var preserved = await GitSourceResult.PreserveAsync(w.ProjectPath, w.Results.LastOrDefault()?.GitReference ?? w.BaseRevision!,
                intent.Id, tree, tree.EnumerateChangedPaths() ?? result.EnumerateEntries().Select(e => e.Path), w.ManagedPaths, ct);
            gitReference = preserved.Reference; gitFingerprint = preserved.Hash;
        }
        var generation = new WorkspaceResultGeneration(intent.Id, intent.Sequence, intent.RequestKey, intent.Message,
            result.Identity, GenerationFingerprint(result), "files:" + w.Id + ":" + intent.Id, capture.Digest,
            capture.Sequence, DateTimeOffset.UtcNow, gitReference, gitFingerprint);
        using (diagnostics?.Measure("files.capture-boundary-2")) tree.VerifyReadBoundary();
        using (diagnostics?.Measure("files.receipt-publish"))
        {
            _ = OperationDiskSpace.Require("publish-source-generation", receipt, 0, 0, OperationDiskSpace.ControlBytes(generation));
            SaveFixed(receipt, generation);
        }
        if (preparationObserver is not null) await preparationObserver.ReachedAsync("source-generation-receipt-published", w, ct);
        using (diagnostics?.Measure("files.independent-verify-2"))
        {
            await VerifyGenerationCoreAsync(w, control, generation, ct, independentReader?.Reader, contentPins is not null, independentReader?.Verification, catalogs, contentPins);
            independentReader?.VerifyBoundary();
        }
        IEnumerable<FileSourceManifest> Retained()
        {
            if (result.RetainedPayloads is null) yield return baseline; yield return result;
            using var retainedPages = new PagedMetadataStore(result.Directory!, filePins: contentPins);
            using var descriptorPages = result.ContentFiles is null ? null : new PagedMetadataStore(FilePath(control, "result-ranges"), filePins: contentPins);
            foreach (var prior in w.Results.Where(g => g.Id != intent.Id))
            {
                using var manifest = ReadManifest(w, GenerationPath(control, prior.Id, "result.json"), catalogs: catalogs, ct: ct);
                if (manifest.Identity != prior.Revision || GenerationFingerprint(manifest) != prior.Fingerprint)
                    throw Error("A historical accounting root differs from its generation receipt.");
                if (!ReferencesCoveredBy(manifest, result, control, ct, retainedPages, descriptorPages)) yield return manifest;
            }
        }
        using (diagnostics?.Measure("files.storage-accounting")) w.ResultStorage = Storage(w, baseline, result, Retained(), control, ct, catalogs);
        // Retained-result maintenance at release owns pruning, outside the capture response.
        return generation;
    }
    public async Task VerifyGenerationAsync(WorkspaceRecord w, string control, WorkspaceResultGeneration generation, CancellationToken ct = default)
    {
        using var proof = new GenerationReadScope(this, w, control, generation, contentPins, ct);
        await proof.VerifyAsync(ct);
    }
    // Every public operation opens its own physical readers. Catalog summaries
    // and payload hashes can be reused only while their native fences survive.
    private sealed class GenerationReadScope : IDisposable
    {
        private readonly WorkspaceRecord workspace;
        private readonly WorkspaceResultGeneration generation;
        private readonly string control;
        private readonly IJournalFilePins? pins;
        private readonly PagedMetadataStore.CatalogReadScope catalogs;
        private readonly FileSourceManifest baseline;
        public FileSourceManifest Manifest { get; }
        public ContentReadScope Content { get; }

        public GenerationReadScope(FileWorkspaceProvider owner, WorkspaceRecord workspace, string control,
            WorkspaceResultGeneration generation, IJournalFilePins? pins, CancellationToken ct)
        {
            this.workspace = workspace;
            this.control = control;
            this.generation = generation;
            this.pins = pins;
            catalogs = new(FilePath(control, "metadata"), pins);
            try
            {
                Manifest = ReadManifest(workspace, GenerationPath(control, generation.Id, "result.json"), catalogs: catalogs, ct: ct);
                baseline = ReadManifest(workspace, FilePath(control, "generation-basis.json"), catalogs: catalogs, ct: ct);
                if (workspace.SourceKind != "git" && baseline.Identity != workspace.BaseRevision)
                    throw Error("The retained generation basis differs from admission.");
                if (Manifest.Identity != generation.Revision || GenerationFingerprint(Manifest) != generation.Fingerprint)
                    throw Error("The independent generation differs from its receipt.");
                Content = owner.ContentReader(workspace, control, Manifest.ContentFiles, retainedBasis: baseline, ct: ct);
            }
            catch { Dispose(); throw; }
        }
        public async Task VerifyAsync(CancellationToken ct)
        {
            await VerifyGenerationCoreAsync(workspace, control, generation, ct, Content.Reader, pins is not null, Content.Verification, catalogs, pins);
            Content.VerifyBoundary();
        }
        public void Dispose()
        {
            try { Content?.Dispose(); }
            finally { try { baseline?.Dispose(); } finally { try { Manifest?.Dispose(); } finally { catalogs.Dispose(); } } }
        }
    }
    private static async Task VerifyGenerationCoreAsync(WorkspaceRecord w, string control, WorkspaceResultGeneration generation,
        CancellationToken ct, Func<string, Stream>? open = null, bool concurrentOpen = false, ResultFileContentStore.VerificationScope? verification = null,
        PagedMetadataStore.CatalogReadScope? catalogs = null, IJournalFilePins? pins = null)
    {
        var receipt = Read<WorkspaceResultGeneration>(GenerationPath(control, generation.Id, "generation.json"), open);
        using var result = ReadManifest(w, GenerationPath(control, generation.Id, "result.json"), open, catalogs, ct);
        using var baseline = ReadManifest(w, FilePath(control, "generation-basis.json"), open, catalogs, ct);
        if (w.SourceKind != "git" && baseline.Identity != w.BaseRevision) throw Error("The retained generation basis differs from admission.");
        if (receipt != generation || result.Identity != generation.Revision || generation.Fingerprint != GenerationFingerprint(result) ||
            generation.ResultReference != "files:" + w.Id + ":" + generation.Id || generation.Sequence < 1 ||
            string.IsNullOrWhiteSpace(generation.RequestKey) || generation.CaptureDigest.Length != 64)
            throw Error("The independent generation differs from its receipt.");
        if (w.SourceKind != "git" || generation.GitReference is null) VerifyPayloads(control, VerificationPayloads(baseline, result, ct, pins), ct, open, concurrentOpen,
            uniqueRetainedRoot: result.RetainedPayloads is not null, verification: verification);
        if (w.SourceKind == "git" && generation.GitReference is not null &&
            await GitSourceResult.VerifyAsync(w.ProjectPath, generation.GitReference, ct) != generation.GitFingerprint)
            throw Error("The independent Git generation changed or disappeared.");
    }
    private static IEnumerable<FileSourceEntry> VerificationPayloads(FileSourceManifest baseline, FileSourceManifest result, CancellationToken ct, IJournalFilePins? pins = null) =>
        result.RetainedPayloads is null ? baseline.EnumeratePayloads(ct, pins).Concat(result.EnumeratePayloads(ct, pins)) : result.EnumerateRetainedPayloads(ct, pins);
    private static IEnumerable<FileSourceEntry> ScanTree(WorkspaceRecord w, ISnapshotTree tree, CancellationToken ct, FileSourceManifest? baseline = null)
    {
        using var basisReader = baseline?.OpenReader();
        int workers = tree.SupportsConcurrentReads ? StorageResources.HashWorkers : 1;
        // Final catalogs own sorting. Reuse the bounded pipeline so one slow
        // file cannot stall an entire batch, and enumeration/hashing/catalog
        // construction overlap. Disposal joins all admitted immutable reads.
        foreach (var entry in BoundedParallel.Select(Entries(), ConvertEntry, workers, ct)) yield return entry;

        IEnumerable<SnapshotEntry> Entries()
        {
            var pending = new Stack<IEnumerator<SnapshotEntry>>(); pending.Push(tree.EnumerateChildren("").GetEnumerator());
            try
            {
                while (pending.TryPeek(out var items))
                {
                    ct.ThrowIfCancellationRequested();
                    if (!items.MoveNext()) { items.Dispose(); pending.Pop(); continue; }
                    var entry = items.Current;
                    if (Managed(entry.Path, w.ManagedPaths) || VcsMetadata.Contains(entry.Path.Split('/')[0], StringComparer.OrdinalIgnoreCase)) continue;
                    if (!ValidPath(entry.Path) || (entry.Attributes & (0x400u | 0x800u | 0x4000u | 0x200u)) != 0)
                        throw new WorkspaceException("SnapshotFileUnsupported", "Generations require supported ordinary source entries.");
                    if (entry.Directory) pending.Push(tree.EnumerateChildren(entry.Path).GetEnumerator());
                    yield return entry;
                }
            }
            finally { foreach (var items in pending) items.Dispose(); }
        }

        string? ProvenHash(SnapshotEntry entry)
        {
            if (tree is not ISnapshotContentSummaryProvider summaries || summaries.GetVerifiedContentSummary(entry.Path) is not { } proof) return null;
            if (proof.Path != entry.Path || proof.Length != entry.Length || !ValidHash(proof.Sha256))
                throw Error("Immutable content evidence differs from the scanned entry.");
            return proof.Sha256;
        }
        FileSourceEntry ConvertEntry(SnapshotEntry entry, CancellationToken token)
        {
            token.ThrowIfCancellationRequested(); string? hash = null;
            if (!entry.Directory)
            {
                var layout = basisReader is not null && tree is ISourceCaptureRanges ranges ? ranges.GetFileRanges(entry.Path) : null;
                var basis = layout?.BasisPath is { } basisPath ? basisReader!.Find(basisPath) : null;
                if (basis is { Kind: "file", Hash: not null } && basis.Length == entry.Length &&
                    layout!.BasisVisibleLength == entry.Length && !layout.PrivateRanges.Any()) hash = basis.Hash;
                else if ((hash = ProvenHash(entry)) is null)
                {
                    using var digest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                    byte[] buffer = ArrayPool<byte>.Shared.Rent(65536);
                    try
                    {
                        for (long position = 0; position < entry.Length;)
                        {
                            token.ThrowIfCancellationRequested();
                            int n = tree.Read(entry.Path, position, buffer.AsSpan(0, (int)Math.Min(65536, entry.Length - position)));
                            if (n <= 0) throw Error("A frozen payload ended early.");
                            // Git's first range read verifies the whole blob and
                            // records its raw SHA-256. Reuse that same live proof
                            // rather than rereading/rehashing the entire payload.
                            if (position == 0 && (hash = ProvenHash(entry)) is not null) break;
                            digest.AppendData(buffer.AsSpan(0, n)); position += n;
                        }
                    }
                    finally { ArrayPool<byte>.Shared.Return(buffer); }
                    hash ??= Convert.ToHexStringLower(digest.GetHashAndReset());
                }
            }
            uint? mode = entry.UnixMode;
            if (w.SourceKind == "git" && !entry.Directory && mode is null)
                mode = (tree as ISourceCaptureTree)?.Basis.Stat(entry.Path)?.UnixMode ?? 0x1A4;
            return new(entry.Path, entry.Directory ? "directory" : "file", entry.Length, hash,
                DateTime.FromFileTimeUtc(entry.LastWriteTime).Ticks, (int)entry.Attributes, mode);
        }
    }
    private void PreserveTreePayloads(WorkspaceRecord w, string control, FileSourceManifest manifest, ISnapshotTree tree, CancellationToken ct, IEnumerable<FileSourceEntry>? selected = null)
    {
        void Preserve(FileSourceEntry entry)
        {
            ct.ThrowIfCancellationRequested();
            if (contentPins is not null)
            {
                new OwnedContentStore(store.Root, contentPins).Preserve(w, control, entry.Hash!, entry.Length, output =>
                {
                    using var input = new SnapshotPayloadStream(tree, entry.Path, entry.Length); PreserveStream(input, output, entry.Length, ct);
                }, ct);
                return;
            }
            string target = FilePath(control, "objects/" + entry.Hash); WorkspaceStore.EnsurePlainPath(target);
            if (File.Exists(target)) { VerifyObject(target, entry, ct); return; }
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            string pending = target + ".pending"; WorkspaceStore.EnsurePlainPath(pending);
            using (var output = new FileStream(pending, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
            {
                if (output.Length > entry.Length) throw Error("Preserve an oversized pending generation object.");
                byte[] buffer = new byte[65536]; long position = 0;
                while (position < entry.Length)
                {
                    ct.ThrowIfCancellationRequested(); int n = tree.Read(entry.Path, position, buffer.AsSpan(0, (int)Math.Min(buffer.Length, entry.Length - position)));
                    if (n <= 0) throw Error("A frozen independent payload ended early.");
                    if (position < output.Length)
                    {
                        int retained = (int)Math.Min(n, output.Length - position); byte[] prior = new byte[retained];
                        output.Position = position; output.ReadExactly(prior);
                        if (!prior.AsSpan().SequenceEqual(buffer.AsSpan(0, retained))) throw Error("An interrupted generation object changed.");
                    }
                    output.Position = position; output.Write(buffer, 0, n); position += n;
                }
                output.Flush(true);
            }
            VerifyObject(pending, entry, ct); File.Move(pending, target);
        }
        var payloads = selected ?? manifest.EnumeratePayloads(ct);
        if (tree.SupportsConcurrentReads) ForEachPayload(payloads, ct, Preserve);
        else foreach (var entry in payloads) Preserve(entry);
    }
    private MetadataTreeRoot? PreserveBasisPayloads(WorkspaceRecord w, string control, FileSourceManifest manifest, ISnapshotTree tree, CancellationToken ct, IEnumerable<FileSourceEntry>? selected = null)
    {
        if (contentPins is null) { PreserveTreePayloads(w, control, manifest, tree, ct, selected); return null; }
        using var raw = new OwnedRangeContentStore(store.Root, contentPins);
        if (!raw.HasPublishedCatalog) { PreserveTreePayloads(w, control, manifest, tree, ct, selected); return null; }
        var whole = new OwnedContentStore(store.Root, contentPins);
        var files = new ResultFileContentStore(store.Root, control, w, contentPins, raw); object descriptorGate = new(); bool usedRaw = false;
        void Preserve(FileSourceEntry entry)
        {
            ct.ThrowIfCancellationRequested();
            bool fixedRaw;
            lock (descriptorGate) fixedRaw = files.UsesRawBasis(entry.Hash!, entry.Length);
            if (fixedRaw)
            {
                // A failed header publication may already have fixed this
                // basis descriptor. Preserve its dependencies even if another
                // workspace publishes an equal whole object before the retry.
                if (entry.Length is not (> 0 and <= int.MaxValue) || !raw.TryReferenceExisting(w, control, entry.Hash!, (int)entry.Length, ct))
                    throw Error("A fixed raw basis descriptor lost its owned content dependency.");
                lock (descriptorGate) usedRaw = true;
                return;
            }
            if (whole.TryReferenceExisting(w, control, entry.Hash!, entry.Length, ct)) return;
            if (entry.Length is > 0 and <= int.MaxValue && raw.TryReferenceExisting(w, control, entry.Hash!, (int)entry.Length, ct))
            {
                lock (descriptorGate)
                {
                    _ = files.Preserve(entry.Hash!, entry.Length, entry.Hash, entry.Length, entry.Length, [], ct, useRawBasis: true);
                    usedRaw = true;
                }
                return;
            }
            whole.Preserve(w, control, entry.Hash!, entry.Length, output =>
            { using var input = new SnapshotPayloadStream(tree, entry.Path, entry.Length); PreserveStream(input, output, entry.Length, ct); }, ct);
        }
        var payloads = selected ?? manifest.EnumeratePayloads(ct);
        if (tree.SupportsConcurrentReads) ForEachPayload(payloads, ct, Preserve);
        else foreach (var entry in payloads) Preserve(entry);
        return usedRaw ? files.CurrentRoot : null;
    }
    public Task MaintainRetainedResultsAsync(WorkspaceRecord w, string control, CancellationToken ct = default)
    {
        if (contentPins is null || w.Results.Count == 0 && !File.Exists(FilePath(control, "result.json"))) return Task.CompletedTask;
        Owner(w, control);
        using var basis = ReadManifest(w, FilePath(control, w.Results.Count == 0 ? "baseline.json" : "generation-basis.json"));
        using var result = w.Results.Count == 0 ? ReadManifest(w, FilePath(control, "result.json")) : GenerationManifest(w);
        PrunePayloadAliases(w, control, basis, result, ct); return Task.CompletedTask;
    }
    private MetadataTreeRoot? PreserveRangePayloads(WorkspaceRecord w, string control, SourceCaptureIdentity expected,
        ISourceCaptureRanges ranges, ISnapshotTree tree, FileSourceManifest baseline, FileSourceManifest result, CancellationToken ct)
    {
        if (ranges.CaptureIdentity != expected) throw Error("Immutable range export differs from the fixed capture.");
        var diagnostics = ResultGenerationDiagnostics.Open(control, expected.Id);
        using var raw = new OwnedRangeContentStore(store.Root, contentPins!);
        var whole = new OwnedContentStore(store.Root, contentPins!);
        PreparedRawRanges preparedRaw;
        using (diagnostics?.Measure("files.raw-preparation")) preparedRaw = PrepareRawRanges(w, control, expected, ranges, tree, result, raw, whole, ct);
        using var preparedLifetime = preparedRaw;
        using var basisCatalog = baseline.OpenReader();
        Stream OpenBasis(string hash, long? length) { var entry = basisCatalog.FindPayload(hash, length); return new SnapshotPayloadStream(((ISourceCaptureTree)tree).Basis, entry.Path, entry.Length); }
        var files = new ResultFileContentStore(store.Root, control, w, contentPins!, raw, externalBasis: w.SourceKind != "git" ? null : OpenBasis);
        var basisContents = baseline.ContentFiles is null ? null : new ResultFileContentStore(store.Root, control, w, contentPins!, raw, baseline.ContentFiles);
        using var adoptionRows = new MetadataSpool(Path.Combine(control, "captures", expected.Id));
        using var baselinePages = new PagedMetadataStore(baseline.Directory!); var baselinePayloads = baselinePages.Open(baseline.PayloadIndex!);
        using var descriptors = files.BeginPreservation();
        using (diagnostics?.Measure("files.range-descriptors"))
        foreach (var entry in result.EnumerateEntries().Where(entry => entry.Kind == "file"))
        {
            ct.ThrowIfCancellationRequested();
            var layout = ranges.GetFileRanges(entry.Path) ?? throw Error("A frozen file has no immutable range layout.");
            var basis = layout.BasisPath is null ? null : basisCatalog.Find(layout.BasisPath);
            bool needsDescriptor = baselinePayloads.Find(entry.Hash!) is null;
            bool rawBasis = needsDescriptor && basis is not null && (basisContents ?? files).UsesRawBasis(basis.Hash!, basis.Length);
            if (layout.BasisPath is not null && (basis is null || basis.Kind != "file" || layout.BasisVisibleLength > basis.Length))
                throw Error("A frozen file references another or missing immutable basis version.");
            IEnumerable<ImmutableFilePrivateRange> Private()
            {
                long previousEnd = 0;
                foreach (var range in layout.PrivateRanges)
                {
                    if (range.Offset < previousEnd || range.Length < 1 || range.Offset > entry.Length - range.Length) throw Error("Invalid frozen private range membership.");
                    previousEnd = checked(range.Offset + range.Length);
                    if (range.PreservedHash is not null)
                    {
                        if (range.PreservedOffset < 0 || range.PreservedLength < range.Length || range.PreservedOffset > range.PreservedLength - range.Length ||
                            (range.PreservedRaw ? !raw.Contains(w, control, range.PreservedHash, range.PreservedLength, ct)
                                : whole.LengthOf(w, control, range.PreservedHash, ct) != range.PreservedLength))
                            throw Error("A captured canonical source slice differs from its preserved object.");
                        if (layout.FileId is { } retainedId && range.BlockIdentity is not null)
                            adoptionRows.Append(CapturedRangeAdoptionStore.Encode(new(retainedId, range.Offset, range.Length,
                                range.BlockIdentity, range.BlockOffset, range.PreservedHash, range.PreservedOffset, range.PreservedRaw, range.PreservedLength)));
                        yield return new(range.Offset, range.Length, range.PreservedHash, range.PreservedRaw, range.PreservedOffset, range.PreservedLength);
                        continue;
                    }
                    for (int at = 0; at < range.Length;)
                    {
                        ct.ThrowIfCancellationRequested(); int length = Math.Min(1048576, range.Length - at);
                        byte[] bytes = []; string hash; bool rawReference = false, prepared = range.Backing?.ExpectedBackingHash is not null;
                        if (prepared)
                        {
                            var prior = preparedRaw.Find(entry.Path, range.Offset + at, length); hash = prior.Hash; rawReference = prior.Raw;
                        }
                        else
                        {
                            bytes = new byte[length]; int done = 0;
                            while (done < length) { int count = tree.Read(entry.Path, range.Offset + at + done, bytes.AsSpan(done)); if (count <= 0) throw Error("A frozen private range ended early."); done += count; }
                            hash = Convert.ToHexStringLower(SHA256.HashData(bytes));
                        }
                        if (!prepared) whole.Preserve(w, control, hash, length, output =>
                        {
                            if (output.Length > bytes.Length) throw Error("An incomplete private chunk has foreign trailing bytes.");
                            if (output.Length > 0)
                            {
                                byte[] old = new byte[checked((int)output.Length)]; output.Position = 0; output.ReadExactly(old);
                                if (!old.AsSpan().SequenceEqual(bytes.AsSpan(0, old.Length))) throw Error("The interrupted private chunk changed.");
                            }
                            output.Position = 0; output.Write(bytes);
                        }, ct);
                        if (layout.FileId is { } fileId && range.BlockIdentity is not null)
                            adoptionRows.Append(CapturedRangeAdoptionStore.Encode(new(fileId, range.Offset + at, length,
                                range.BlockIdentity, checked(range.BlockOffset + at), hash, 0, rawReference)));
                        yield return new(range.Offset + at, length, hash, rawReference); at += length;
                    }
                }
            }
            using var privateItems = Private().GetEnumerator();
            if (!privateItems.MoveNext())
            {
                if (needsDescriptor)
                    _ = descriptors.Preserve(entry.Hash!, entry.Length, basis?.Hash, basis?.Length ?? 0, layout.BasisVisibleLength, [], ct, w.SourceKind == "git", rawBasis);
                continue;
            }
            if (!needsDescriptor)
            {
                // The immutable basis already supplies the complete payload.
                // Still drain every private range to validate/preserve chunks
                // and publish adoption rows for later writes/reversions. No
                // descriptor consumes a second temporary range list here.
                while (privateItems.MoveNext()) { }
                continue;
            }
            IEnumerable<ImmutableFilePrivateRange> Preserved()
            {
                // Preserve builds its range tree in one pass. Streaming keeps
                // the complete validation/adoption work without writing and
                // rereading a separate temporary list for every source file.
                do { yield return privateItems.Current; } while (privateItems.MoveNext());
            }
            _ = descriptors.Preserve(entry.Hash!, entry.Length, basis?.Hash, basis?.Length ?? 0, layout.BasisVisibleLength, Preserved(), ct, w.SourceKind == "git", rawBasis);
            // An already retained full-file identity verifies its descriptor
            // without consuming Preserved. Its current capture still owns all
            // private-range validation and adoption, including the last row.
            while (privateItems.MoveNext()) { }
        }
        descriptors.Complete();
        using (diagnostics?.Measure("files.adoption-publish"))
            _ = CapturedRangeAdoptionStore.Publish(control, expected, adoptionRows.Read().Select(CapturedRangeAdoptionStore.Decode), contentPins!, ct);
        if (w.SourceKind == "git") ((ISourceCaptureTree)tree).Basis.VerifyReadBoundary();
        return files.CurrentRoot;
    }
    private IEnumerable<ImmutableContentReference> ContentReferences(WorkspaceRecord w, string control, FileSourceManifest manifest, CancellationToken ct = default)
    {
        var files = manifest.EnumerateRetainedPayloads(ct).Select(entry => new ImmutableContentReference(entry.Hash!, entry.Length, false));
        if (manifest.ContentFiles is null)
        {
            foreach (var item in files)
                if (w.SourceKind != "git" || File.Exists(FilePath(control, "objects/" + item.Hash))) yield return item;
            yield break;
        }
        if (contentPins is null) throw new WorkspaceException("ResultContentVerificationRequired", "Compose native immutable-content ports to verify this range-backed result.");
        using var raw = new OwnedRangeContentStore(store.Root, contentPins);
        var contents = new ResultFileContentStore(store.Root, control, w, contentPins, raw, manifest.ContentFiles,
            w.SourceKind == "git" ? (_, _) => throw Error("Reference enumeration cannot open payloads.") : null);
        foreach (var item in contents.ReferencesForFiles(manifest.ContentFiles, files)) yield return item;
    }
    private void PrunePayloadAliases(WorkspaceRecord w, string control, FileSourceManifest baseline, FileSourceManifest current, CancellationToken ct)
    {
        if (contentPins is null) return;
        var diagnostics = ResultGenerationDiagnostics.Open(control, w.CaptureIntent?.Id ?? w.Results.LastOrDefault()?.Id ?? "");
        SourceChangeView? live = null;
        if (Directory.Exists(Path.Combine(control, "delta")))
        {
            // Writes cannot introduce a new canonical reference; only adoption
            // does, under this workspace operation lock. This barrier may
            // conservatively overmark later overwritten ranges, never omit a
            // newly adopted source-only slice such as a reversion to the basis.
            if (sourceChanges is null) return;
            using (diagnostics?.Measure("prune.observe-live")) live = sourceChanges.ObserveChangesAsync(w, ct).GetAwaiter().GetResult();
        }
        using var liveLifetime = live;
        IEnumerable<MetadataEntry> Hashes()
        {
            if (live is not null)
            foreach (var entry in JournalDeltaStore.EnumerateRequiredCapturedContent(live))
            {
                byte[] length = new byte[8]; System.Buffers.Binary.BinaryPrimitives.WriteInt64LittleEndian(length, entry.Length);
                yield return new((entry.Raw ? "r" : "w") + entry.Hash, length);
            }
            if (w.CaptureIntent is { } pending && File.Exists(CapturedRangeAdoptionStore.Artifact(control, pending.Id)))
            {
                var descriptor = Read<SourceRangeAdoption>(CapturedRangeAdoptionStore.Artifact(control, pending.Id));
                descriptor = CapturedRangeAdoptionStore.Read(control, descriptor.CaptureIdentity);
                if (descriptor.CaptureIdentity.Id != pending.Id) throw Error("The range adoption root differs from the pending capture.");
                foreach (var entry in CapturedRangeAdoptionStore.Enumerate(descriptor))
                {
                    byte[] length = new byte[8]; System.Buffers.Binary.BinaryPrimitives.WriteInt64LittleEndian(length, entry.ContentLength);
                    yield return new((entry.RawRange ? "r" : "w") + entry.ChunkHash, length);
                }
            }
            IEnumerable<FileSourceManifest> Manifests()
            {
                if (current.RetainedPayloads is null) yield return baseline; yield return current;
                foreach (var generation in w.Results)
                {
                    using var retained = ReadManifest(w, GenerationPath(control, generation.Id, "result.json"));
                    yield return retained;
                }
            }
            foreach (var manifest in Manifests())
            foreach (var item in ContentReferences(w, control, manifest, ct))
            {
                byte[] length = new byte[8]; System.Buffers.Binary.BinaryPrimitives.WriteInt64LittleEndian(length, item.Length);
                yield return new((item.RawRange ? "r" : "w") + item.Hash, length);
            }
        }
        MetadataMembership required;
        using (diagnostics?.Measure("prune.required-index")) required = new(Hashes(), FilePath(control, "gc-work"), ct);
        using var requiredLifetime = required;
        using (diagnostics?.Measure("prune.whole-aliases"))
            new OwnedContentStore(store.Root, contentPins).PruneUnreferenced(w, control, hash => required.Contains("w" + hash), ct);
        using var raw = new OwnedRangeContentStore(store.Root, contentPins);
        using (diagnostics?.Measure("prune.raw-aliases")) raw.PruneUnreferenced(w, control, hash => required.Contains("r" + hash), ct);
    }
    private sealed class SnapshotPayloadStream(ISnapshotTree tree, string path, long length) : Stream
    {
        private long position;
        public override int Read(Span<byte> buffer)
        {
            int requested = (int)Math.Min(buffer.Length, length - position);
            if (requested == 0) return 0;
            int count = tree.Read(path, position, buffer[..requested]); position += count; return count;
        }
        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));
        public override long Length => length; public override long Position { get => position; set { if (value < 0 || value > length) throw new ArgumentOutOfRangeException(nameof(value)); position = value; } }
        public override bool CanRead => true; public override bool CanWrite => false; public override bool CanSeek => true;
        public override void Flush() { } public override long Seek(long value, SeekOrigin origin) { Position = origin switch { SeekOrigin.Begin => value, SeekOrigin.Current => checked(position + value), SeekOrigin.End => checked(length + value), _ => throw new ArgumentOutOfRangeException(nameof(origin)) }; return position; }
        public override void SetLength(long value) => throw new NotSupportedException(); public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
    private FileSourceManifest GenerationManifest(WorkspaceRecord w) => ReadManifest(w,
        GenerationPath(Control(w), w.Results.Last().Id, "result.json"));
}
