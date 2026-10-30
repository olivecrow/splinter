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
        var current = Manifest(w, Scan(w.Path, w.ManagedPaths, ct, mount: w.Path,
            hashWorkers: w.SourceKind == "plastic" ? 1 : 8, omitGitRoot: w.SourceKind == "git"));
        var result = GenerationManifest(w);
        bool changed = current.Identity != result.Identity;
        return Task.FromResult(new SourceState(result.Identity, current.Identity, changed,
            changed ? Changed(result.Entries, current.Entries) : []));
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
        string receipt = GenerationPath(control, intent.Id, "generation.json");
        SaveFixed(FilePath(control, "owner.json"), new FileSourceOwner(w.Id, w.RequestDigest!, w.ProjectPath,
            w.Path, w.SourceReference, w.Snapshot!.Reference!));
        if (w.SourceKind == "plastic") PlasticWorkspaceProvider.VerifyReceipt(w, control);
        if (File.Exists(receipt))
        {
            var prior = Read<WorkspaceResultGeneration>(receipt);
            if (prior.Id != intent.Id || prior.RequestKey != intent.RequestKey || prior.Message != intent.Message || prior.CaptureDigest != capture.Digest || prior.Sequence != intent.Sequence)
                throw Error("The result differs from its fixed capture request.");
            await VerifyGenerationAsync(w, control, prior, ct); return prior;
        }
        string basisPath = FilePath(control, "generation-basis.json");
        var baseline = File.Exists(basisPath) ? ReadManifest(w, basisPath) : Manifest(w, ScanTree(w, tree.Basis, ct));
        if (w.SourceKind != "git" && baseline.Identity != w.BaseRevision) throw Error("The generation basis differs from the admitted immutable source.");
        if (w.SourceKind != "git") PreserveTreePayloads(control, baseline, tree.Basis, ct);
        SaveFixed(basisPath, baseline);
        var result = Manifest(w, ScanTree(w, tree, ct));
        if (w.SourceKind != "git")
        {
            PreserveTreePayloads(control, result, tree, ct);
            VerifyPayloads(control, baseline.Entries.Concat(result.Entries), ct);
        }
        if (preparationObserver is not null) await preparationObserver.ReachedAsync("source-payloads-preserved", w, ct);
        SaveFixed(GenerationPath(control, intent.Id, "result.json"), result);
        string? gitReference = null, gitFingerprint = null;
        if (w.SourceKind == "git")
        {
            var preserved = await GitSourceResult.PreserveAsync(w.ProjectPath, w.Results.LastOrDefault()?.GitReference ?? w.BaseRevision!,
                intent.Id, tree, tree.ChangedPaths ?? result.Entries.Select(e => e.Path).ToArray(), w.ManagedPaths, ct);
            gitReference = preserved.Reference; gitFingerprint = preserved.Hash;
        }
        var generation = new WorkspaceResultGeneration(intent.Id, intent.Sequence, intent.RequestKey, intent.Message,
            result.Identity, result.Identity, "files:" + w.Id + ":" + intent.Id, capture.Digest,
            capture.Sequence, DateTimeOffset.UtcNow, gitReference, gitFingerprint);
        SaveFixed(receipt, generation); await VerifyGenerationAsync(w, control, generation, ct);
        var retainedEntries = baseline.Entries.Concat(result.Entries).Concat(w.Results
            .Where(g => g.Id != intent.Id).SelectMany(g => ReadManifest(w, GenerationPath(control, g.Id, "result.json")).Entries));
        var payloads = UniquePayloads(retainedEntries).ToArray();
        w.ResultStorage = new(baseline.Entries.Sum(e => e.Length), result.Entries.Sum(e => e.Length), payloads.Sum(e => e.Length), payloads.Length);
        return generation;
    }
    public Task VerifyGenerationAsync(WorkspaceRecord w, string control, WorkspaceResultGeneration generation, CancellationToken ct = default) =>
        VerifyGenerationCoreAsync(w, control, generation, ct);
    private static async Task VerifyGenerationCoreAsync(WorkspaceRecord w, string control, WorkspaceResultGeneration generation,
        CancellationToken ct, Func<string, Stream>? open = null)
    {
        var receipt = Read<WorkspaceResultGeneration>(GenerationPath(control, generation.Id, "generation.json"), open);
        var result = ReadManifest(w, GenerationPath(control, generation.Id, "result.json"), open);
        var baseline = ReadManifest(w, FilePath(control, "generation-basis.json"), open);
        if (w.SourceKind != "git" && baseline.Identity != w.BaseRevision) throw Error("The retained generation basis differs from admission.");
        if (receipt != generation || result.Identity != generation.Revision || generation.Fingerprint != result.Identity ||
            generation.ResultReference != "files:" + w.Id + ":" + generation.Id || generation.Sequence < 1 ||
            string.IsNullOrWhiteSpace(generation.RequestKey) || generation.CaptureDigest.Length != 64)
            throw Error("The independent generation differs from its receipt.");
        if (w.SourceKind != "git") VerifyPayloads(control, baseline.Entries.Concat(result.Entries), ct, open);
        if (w.SourceKind == "git" && (generation.GitReference is null ||
            await GitSourceResult.VerifyAsync(w.ProjectPath, generation.GitReference, ct) != generation.GitFingerprint))
            throw Error("The independent Git generation changed or disappeared.");
    }
    private static FileSourceEntry[] ScanTree(WorkspaceRecord w, ISnapshotTree tree, CancellationToken ct)
    {
        var entries = new List<FileSourceEntry>(); var pending = new Stack<string>(); pending.Push("");
        byte[] buffer = new byte[65536]; long pathBytes = 0;
        while (pending.TryPop(out var directory))
        foreach (var entry in tree.List(directory))
        {
            ct.ThrowIfCancellationRequested();
            if (Managed(entry.Path, w.ManagedPaths) || VcsMetadata.Contains(entry.Path.Split('/')[0], StringComparer.OrdinalIgnoreCase)) continue;
            if (!ValidPath(entry.Path) || (entry.Attributes & (0x400u | 0x800u | 0x4000u | 0x200u)) != 0)
                throw new WorkspaceException("SnapshotFileUnsupported", "Generations require supported ordinary source entries.");
            pathBytes += System.Text.Encoding.UTF8.GetByteCount(entry.Path);
            if (entries.Count >= 32768 || pathBytes > 1024 * 1024) throw new WorkspaceException("SourceManifestLimit", "The generation exceeds bounded manifest membership.");
            string? hash = null;
            if (entry.Directory) pending.Push(entry.Path);
            else
            {
                using var digest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                for (long position = 0; position < entry.Length;)
                {
                    ct.ThrowIfCancellationRequested(); int n = tree.Read(entry.Path, position, buffer.AsSpan(0, (int)Math.Min(buffer.Length, entry.Length - position)));
                    if (n <= 0) throw Error("A frozen payload ended early."); digest.AppendData(buffer.AsSpan(0, n)); position += n;
                }
                hash = Convert.ToHexStringLower(digest.GetHashAndReset());
            }
            entries.Add(new(entry.Path, entry.Directory ? "directory" : "file", entry.Length, hash,
                DateTime.FromFileTimeUtc(entry.LastWriteTime).Ticks, (int)entry.Attributes));
        }
        return entries.OrderBy(e => e.Path, StringComparer.Ordinal).ToArray();
    }
    private static void PreserveTreePayloads(string control, FileSourceManifest manifest, ISnapshotTree tree, CancellationToken ct)
    {
        void Preserve(FileSourceEntry entry)
        {
            ct.ThrowIfCancellationRequested(); string target = FilePath(control, "objects/" + entry.Hash); WorkspaceStore.EnsurePlainPath(target);
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
        var payloads = UniquePayloads(manifest.Entries);
        if (tree.SupportsConcurrentReads) ForEachPayload(payloads, ct, Preserve);
        else foreach (var entry in payloads) Preserve(entry);
    }
    private FileSourceManifest GenerationManifest(WorkspaceRecord w) => ReadManifest(w,
        GenerationPath(Control(w), w.Results.Last().Id, "result.json"));
}
