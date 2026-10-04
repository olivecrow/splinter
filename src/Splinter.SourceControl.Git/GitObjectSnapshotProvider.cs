using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Splinter.Core;

namespace Splinter.SourceControl.Git;

// A project commit is already immutable. Only identity/ownership metadata and a
// reachability ref are published here; no checkout, payload cache or VSS exists.
public sealed class GitObjectSnapshotProvider(string receiptDirectory, string ownerId, string? revision = null) : ISourceSnapshotProvider
{
    public const string ProviderName = "git-objects";
    public const string Consistency = "recorded-project-commit-raw-blobs";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
        { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
    private string ReceiptPath => Path.Combine(receiptDirectory, "git-basis.json");
    private string Reference => "refs/splinter/git-bases/" + ownerId;

    public async Task<SnapshotReference> CaptureAsync(SnapshotCapture request, CancellationToken ct = default)
    {
        if (request.OwnerId != ownerId || !Same(request.ReceiptDirectory, receiptDirectory) || string.IsNullOrWhiteSpace(revision) ||
            ownerId.Length is < 1 or > 128 || ownerId.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-'))
            throw Failure("SnapshotIdentityMismatch", "A fixed project revision and exact basis owner are required.");
        Plain(receiptDirectory); Plain(request.SourcePath); Directory.CreateDirectory(receiptDirectory);
        using var operation = Fence("operation.lock", true);
        var prior = ReadReceipt();
        if (prior is null)
        {
            string project = Path.GetFullPath(request.SourcePath);
            string common = await GitWorkspaceProvider.Required(project, ["rev-parse", "--path-format=absolute", "--git-common-dir"], ct);
            Plain(common);
            string fixedCommit = await Command(common, ["rev-parse", "--verify", "--end-of-options", revision + "^{commit}"], ct);
            RequireObjectId(fixedCommit);
            if (fixedCommit != revision) throw Failure("SnapshotIdentityMismatch", "Preflight must fix the exact commit ID.");
            if (File.Exists(Path.Combine(common, "objects", "info", "alternates")) || File.Exists(Path.Combine(common, "shallow")) ||
                (await Command(common, ["config", "--get", "extensions.partialClone"], ct, allowMissing: true)).Length != 0)
                throw Failure("GitBasisUnsupported", "Use a complete local object store without alternates, shallow history or lazy fetch.");
            string tree = await Command(common, ["rev-parse", fixedCommit + "^{tree}"], ct);
            GitObjectSnapshotTree.VerifyObject(common, fixedCommit, "commit");
            GitObjectSnapshotTree.VerifyObject(common, tree, "tree");
            var snapshot = new SnapshotReference("git-" + ownerId + "-" + fixedCommit, ProviderName, project, common,
                DateTimeOffset.UtcNow, true, true, Consistency);
            prior = new(1, ownerId, snapshot, fixedCommit, tree, Reference, false, false);
            // Intent before the external effect supports a lost create-ref reply.
            Save(ReceiptPath, prior);
        }
        Require(prior, prior.Snapshot);
        if (!Same(prior.Snapshot.SourcePath, request.SourcePath) || prior.Revision != revision || prior.Released)
            throw Failure("SnapshotIdentityMismatch", "Never replace this task's fixed basis.");
        if (!prior.Pinned)
        {
            string? found = await FindRef(prior, ct);
            if (found is null) await Command(prior.Snapshot.RootPath, ["update-ref", "--no-deref", Reference, prior.Revision, new string('0', prior.Revision.Length)], ct);
            else if (found != prior.Revision) throw Failure("GitBasisRefChanged", "The reserved basis ref belongs to different content.");
            prior = prior with { Pinned = true }; Save(ReceiptPath, prior);
        }
        var observation = await ObserveAsync(prior.Snapshot, ct);
        if (observation.Available != true) throw Failure(observation.FailureCode!, "Preserve the unavailable recorded basis.");
        return prior.Snapshot;
    }

    public async Task<SnapshotObservation> ObserveAsync(SnapshotReference snapshot, CancellationToken ct = default)
    {
        try
        {
            var receipt = ReadReceipt() ?? throw Failure("SnapshotIdentityMismatch", "The owned basis receipt is missing.");
            Require(receipt, snapshot);
            if (receipt.Released) return new(snapshot.Id, false, "SnapshotMissing", 0, null);
            if (!receipt.Pinned) return new(snapshot.Id, null, "SnapshotVerificationRequired", 0, null);
            if (await FindRef(receipt, ct) != receipt.Revision)
                return new(snapshot.Id, null, "GitBasisRefChanged", 0, null);
            if (await Command(snapshot.RootPath, ["rev-parse", "--verify", receipt.Revision + "^{tree}"], ct,
                failureCode: "GitBasisObjectInvalid") != receipt.Tree)
                return new(snapshot.Id, null, "SnapshotIdentityMismatch", 0, null);
            return new(snapshot.Id, true, null, 0, null)
                { ReadAccess = new(snapshot.Id, snapshot.RootPath, receipt.Tree) };
        }
        catch (WorkspaceException ex) { return new(snapshot.Id, null, ex.Code, null, null); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        { return new(snapshot.Id, null, "GitBasisReceiptInvalid", null, null); }
    }

    public async Task<SnapshotReadAccess> ResolveReadAccessAsync(SnapshotReference snapshot, CancellationToken ct = default)
    {
        var observed = await ObserveAsync(snapshot, ct);
        return observed.Available == true && observed.ReadAccess is { } access ? access :
            throw Failure(observed.FailureCode ?? "SnapshotObservationUnknown", "The exact recorded basis cannot be read.");
    }

    public FileStream AcquireReadLease() => Fence("readers.lock", false);

    public GitObjectSnapshotTree OpenTree(SnapshotReference snapshot, SnapshotReadAccess access, Func<SnapshotObservation>? health = null)
    {
        var receipt = ReadReceipt() ?? throw Failure("SnapshotIdentityMismatch", "The basis receipt is missing.");
        Require(receipt, snapshot); access.RequireMatch(snapshot);
        if (!receipt.Pinned || receipt.Released || access.RootPath != snapshot.RootPath || access.VerificationId != receipt.Tree)
            throw Failure("SnapshotIdentityMismatch", "Retain the owned basis and its exact tree proof.");
        return new(snapshot, receipt.Tree, receipt.Revision, receipt.Reference,
            health ?? (() => ObserveAsync(snapshot).GetAwaiter().GetResult()));
    }

    public async Task<SnapshotReclamation> ReleaseAsync(SnapshotReference snapshot, CancellationToken ct = default, string? previousAttemptId = null)
    {
        Plain(receiptDirectory);
        using var operation = Fence("operation.lock", true);
        using var readers = Fence("readers.lock", true);
        var receipt = ReadReceipt() ?? throw Failure("SnapshotIdentityMismatch", "The exact basis receipt is required.");
        Require(receipt, snapshot);
        string path = Path.Combine(receiptDirectory, "git-release.json");
        SnapshotReclamation? intent = Read<SnapshotReclamation>(path);
        if (intent is not null) intent.RequireMatch(snapshot);
        if (previousAttemptId is not null && intent?.AttemptId != previousAttemptId)
            throw Failure("SnapshotIdentityMismatch", "Reuse the exact release attempt.");
        if (intent is null)
        {
            if (receipt.Released || await FindRef(receipt, ct) != receipt.Revision)
                throw Failure("GitBasisRefChanged", "Basis ownership must be established before release intent.");
            intent = new(snapshot.Id, Guid.NewGuid().ToString("N"), "released", DateTimeOffset.UtcNow, []);
            Save(path, intent);
        }
        string? found = await FindRef(receipt, ct);
        if (found is not null && (receipt.Released || found != receipt.Revision))
            throw Failure("GitBasisRefChanged", "Preserve a changed or recreated ref.");
        if (found is not null) await Command(snapshot.RootPath, ["update-ref", "--no-deref", "-d", receipt.Reference, receipt.Revision], ct);
        if (await FindRef(receipt, ct) is not null) throw Failure("GitBasisRefChanged", "The owned ref reappeared.");
        if (!receipt.Released) Save(ReceiptPath, receipt with { Released = true });
        // Repository objects, source/result/peer refs and GC are never touched.
        return intent;
    }

    private async Task<string?> FindRef(Receipt receipt, CancellationToken ct)
    {
        string output = await Command(receipt.Snapshot.RootPath, ["for-each-ref", "--format=%(refname) %(objectname) %(symref)", receipt.Reference], ct);
        var entries = output.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(s => s.TrimEnd('\r').Split(' ', 3))
            .Where(p => p[0] == receipt.Reference).ToArray();
        if (entries.Length == 0) return null;
        if (entries.Length != 1 || entries[0].Length != 3 || entries[0][2].Length != 0)
            throw Failure("GitBasisRefChanged", "A symbolic or ambiguous basis ref cannot authorize reads or deletion.");
        return entries[0][1];
    }
    private Receipt? ReadReceipt() => Read<Receipt>(ReceiptPath);
    private void Require(Receipt r, SnapshotReference snapshot)
    {
        Plain(snapshot.RootPath); Plain(snapshot.SourcePath);
        if (r.Version != 1 || r.OwnerId != ownerId || r.Snapshot != snapshot || r.Reference != Reference ||
            snapshot.Provider != ProviderName || snapshot.Consistency != Consistency || !snapshot.Persistent || !snapshot.Differential ||
            snapshot.Id != "git-" + ownerId + "-" + r.Revision)
            throw Failure("SnapshotIdentityMismatch", "Basis receipt, provider and owner must match.");
        RequireObjectId(r.Revision); RequireObjectId(r.Tree);
    }
    private FileStream Fence(string name, bool exclusive)
    {
        string path = Path.Combine(receiptDirectory, name); Plain(path);
        try { return new(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, exclusive ? FileShare.None : FileShare.ReadWrite); }
        catch (IOException) { throw Failure("SnapshotReadersActive", "Stop the owned basis readers before release."); }
    }
    private static T? Read<T>(string path)
    {
        Plain(path);
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            return JsonSerializer.Deserialize<T>(stream, Json) ?? throw Failure("GitBasisReceiptInvalid", "Preserve an empty basis receipt.");
        }
        catch (FileNotFoundException) { return default; }
        catch (JsonException) { throw Failure("GitBasisReceiptInvalid", "Preserve an incomplete or corrupt owned basis receipt."); }
    }
    private static void Save<T>(string path, T value)
    {
        Plain(path);
        string pending = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        using (var stream = new FileStream(pending, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        { JsonSerializer.Serialize(stream, value, Json); stream.Flush(true); }
        if (File.Exists(path)) File.Replace(pending, path, null);
        else File.Move(pending, path, false);
    }
    internal static void Plain(string path)
    {
        for (string? p = Path.GetFullPath(path); p is not null; p = Path.GetDirectoryName(p))
            if ((File.Exists(p) || Directory.Exists(p)) && (File.GetAttributes(p) & FileAttributes.ReparsePoint) != 0)
                throw Failure("UnsafePath", "Basis paths must not traverse reparse points.");
    }
    internal static void RequireObjectId(string id)
    {
        if (id.Length is not (40 or 64) || id.Any(c => !char.IsAsciiHexDigit(c)))
            throw Failure("GitBasisUnsupported", "Only complete object IDs are accepted.");
    }
    private static bool Same(string a, string b) => string.Equals(Path.GetFullPath(a), Path.GetFullPath(b),
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    internal static WorkspaceException Failure(string code, string message) => new(code, message);
    internal static ProcessStartInfo StartInfo(string common, string[] args)
    {
        Plain(common);
        var info = new ProcessStartInfo("git") { UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
        info.StandardOutputEncoding = new System.Text.UTF8Encoding(false, true);
        foreach (string key in info.Environment.Keys.Where(k => k.StartsWith("GIT_", StringComparison.OrdinalIgnoreCase)).ToArray()) info.Environment.Remove(key);
        info.Environment["GIT_NO_REPLACE_OBJECTS"] = "1"; info.Environment["GIT_NO_LAZY_FETCH"] = "1";
        foreach (string arg in new[] { "--no-replace-objects", "--git-dir=" + common }.Concat(args)) info.ArgumentList.Add(arg);
        return info;
    }
    internal static async Task<string> Command(string common, string[] args, CancellationToken ct, bool allowMissing = false,
        string failureCode = "GitBasisUnavailable")
    {
        using var process = Process.Start(StartInfo(common, args)) ?? throw Failure("GitBasisUnavailable", "Git did not start.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(30));
        try
        {
            Task<string> output = process.StandardOutput.ReadToEndAsync(timeout.Token), error = process.StandardError.ReadToEndAsync(timeout.Token);
            process.StandardInput.Close(); await process.WaitForExitAsync(timeout.Token);
            string result = await output; await error;
            if (process.ExitCode != 0 && !(allowMissing && process.ExitCode == 1))
                throw Failure(failureCode, "The fixed object store could not complete the Git operation.");
            return result.TrimEnd('\r', '\n');
        }
        catch (OperationCanceledException)
        { if (!process.HasExited) process.Kill(); process.WaitForExit(); throw Failure("GitBasisUnavailable", "The fixed-basis Git operation timed out or was cancelled."); }
    }
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed record Receipt([property: JsonRequired] int Version, [property: JsonRequired] string OwnerId,
        [property: JsonRequired] SnapshotReference Snapshot, [property: JsonRequired] string Revision, [property: JsonRequired] string Tree,
        [property: JsonRequired] string Reference, [property: JsonRequired] bool Pinned, [property: JsonRequired] bool Released);
}
