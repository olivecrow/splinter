using System.CommandLine;
using System.CommandLine.Parsing;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Splinter.Core;
using Splinter.Protocol;
using Splinter.Runtime;
using Splinter.SourceControl.Files;
using Splinter.Platform.Windows;

namespace Splinter.Cli;

internal static class WorkspaceReadCommands
{
    public static void Add(Command workspace, ContractCatalog contracts, Func<ParseResult, string> home,
        Action<string> select, Action<ApiResponse> respond, CancellationToken ct)
    {
        var read = new Command("read", contracts.Find("workspace.read")!.Description);
        var id = new Option<string>("--id") { Required = true };
        var collection = new Option<string>("--collection") { Required = true };
        var result = new Option<string>("--result"); var plan = new Option<string>("--plan-id");
        var request = new Option<string>("--request-key"); var storeScope = new Option<string>("--store-scope");
        var cursor = new Option<string>("--cursor"); var size = new Option<int?>("--page-size");
        read.Options.Add(id); read.Options.Add(collection); read.Options.Add(result); read.Options.Add(plan); read.Options.Add(cursor); read.Options.Add(size);
        read.Options.Add(request); read.Options.Add(storeScope);
        workspace.Subcommands.Add(read);
        read.SetAction(async parse =>
        {
            const string command = "workspace.read"; select(command);
            var input = new { id = parse.GetValue(id)!, collection = parse.GetValue(collection)!, result = parse.GetValue(result),
                planId = parse.GetValue(plan), requestKey = parse.GetValue(request), storeScope = parse.GetValue(storeScope), cursor = parse.GetValue(cursor), pageSize = parse.GetValue(size) ?? 128 };
            var validation = contracts.Validate(contracts.Find(command)!.InputSchema, JsonSerializer.SerializeToElement(input, ApiJson.Options));
            if (!validation.IsValid)
            { respond(ApiResponse.Failure(command, "SchemaValidationFailed", "Arguments do not match the page query contract.", validation)); return; }
            var store = new WorkspaceStore(home(parse)); using var gate = await store.LockAsync(input.id, ct);
            var record = store.Get(input.id); string control = store.ControlPath(input.id);
            object page = input.collection switch
            {
                "results" => ReadSequence(record.Id, "results", Hash(record.ResultHistory), record.Results.Count, record.Results.Enumerate,
                    input.cursor, input.pageSize),
                "applies" => ReadSequence(record.Id, "applies", Hash(record.OriginalApplyHistory), record.OriginalApplies.Count, record.OriginalApplies.Enumerate,
                    input.cursor, input.pageSize),
                "plan" => ReadPlan(record, control, input.planId, input.cursor, input.pageSize),
                "conflicts" => ReadPlan(record, control, input.planId, input.cursor, input.pageSize, conflicts: true),
                "selection" => ReadSelection(record, control, input.planId, input.cursor, input.pageSize),
                "preserved-scratch" => ReadScratch(record, control, input.cursor, input.pageSize, input.storeScope, home(parse), ct),
                "maintenance-residuals" => ReadMaintenance(record, control, input.requestKey, input.storeScope, input.cursor, input.pageSize),
                "deferred-reclamation" => ReadDeferred(record, control, home(parse), input.storeScope, input.cursor, input.pageSize, ct, input.requestKey),
                "files" => ReadFiles(record, control, input.result, input.cursor, input.pageSize),
                "applied-paths" => FileWorkspaceProvider.ReadAppliedPathsPage(control, record.AppliedPaths, record.AppliedPathNames, input.cursor, input.pageSize),
                _ => throw new WorkspaceException("CursorInvalid", "Choose a supported collection.")
            };
            respond(ApiResponse.Success(command, new { id = record.Id, collection = input.collection, page }));
        });
    }
    private static object ReadFiles(WorkspaceRecord workspace, string control, string? result, string? cursor, int size)
    {
        if (result is null || !workspace.Results.Any(value => value.Id == result))
            throw new WorkspaceException("ResultNotReady", "Select an immutable result belonging to this workspace.");
        return FileWorkspaceProvider.ReadResultPage(control, result, cursor, size);
    }
    private static object ReadPlan(WorkspaceRecord workspace, string control, string? digest, string? cursor, int size, bool conflicts = false)
    {
        if (digest is null) throw new WorkspaceException("CursorInvalid", "Supply the fixed plan digest with --plan-id.");
        var plan = OriginalApplyPages.ReadPlan(control, digest);
        if (plan.OwnerId != workspace.Id || plan.RequestDigest != workspace.RequestDigest)
            throw new WorkspaceException("OriginalApplyStateInvalid", "The fixed plan belongs to another request.");
        if (conflicts) return ReadSequence(workspace.Id, "conflicts", digest, plan.ConflictCount,
            start => OriginalApplyPages.EnumerateConflicts(control, plan, start).Select(value => new { message = value }), cursor, size);
        return ReadSequence(workspace.Id, "plan", digest, plan.ChangeCount, start => Changes(plan, start), cursor, size);
    }
    private static object ReadScratch(WorkspaceRecord workspace, string control, string? cursor, int size, string? scope, string home, CancellationToken ct)
    {
        MetadataCursor? decoded = null;
        if (cursor is not null)
        {
            try { decoded = JsonSerializer.Deserialize<MetadataCursor>(Convert.FromBase64String(cursor), ApiJson.Options) ?? throw new FormatException(); }
            catch (Exception ex) when (ex is FormatException or JsonException)
            { throw new WorkspaceException("CursorInvalid", "The scratch cursor is malformed."); }
        }
        if (scope == "content")
        {
            var contents = new OwnedContentStore(home, new WindowsJournalFilePins());
            var contentPage = contents.ReadPreservedScratchPage(decoded, size, ct);
            return new { root = "sha256:" + (contentPage.Root ?? new MetadataTreeRoot(1, "", null)).ContentHash, count = contentPage.Root?.Count ?? 0, contentPage.Entries,
                nextCursor = contentPage.NextCursor is null ? null : Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(contentPage.NextCursor, ApiJson.Options)) };
        }
        if (scope == "ranges")
        {
            using var ranges = new OwnedRangeContentStore(home, new WindowsJournalFilePins());
            var rangePage = ranges.ReadPreservedScratchPage(decoded, size, ct);
            return new { root = "sha256:" + (rangePage.Root ?? new MetadataTreeRoot(1, "", null)).ContentHash, count = rangePage.Root?.Count ?? 0, rangePage.Entries,
                nextCursor = rangePage.NextCursor is null ? null : Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(rangePage.NextCursor, ApiJson.Options)) };
        }
        if (scope is not (null or "workspace-original")) throw new WorkspaceException("CursorInvalid", "Choose content, ranges or workspace-original for a preserved scratch query.");
        if (workspace.OriginalApply is not { } apply) throw new WorkspaceException("OriginalApplyStateInvalid", "No publication owns a scratch inventory.");
        var page = OriginalApplyPages.ReadPreservedScratchPage(control, apply, decoded, size);
        return new { root = "sha256:" + page.Root.ContentHash, count = page.Root.Count, page.Entries,
            nextCursor = page.NextCursor is null ? null : Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(page.NextCursor, ApiJson.Options)) };
    }
    private static object ReadMaintenance(WorkspaceRecord workspace, string control, string? request, string? scope, string? cursor, int size)
    {
        if (request is null || scope is null) throw new WorkspaceException("CursorInvalid", "Select --request-key and --store-scope from the fixed maintenance receipt.");
        MetadataCursor? decoded = null;
        if (cursor is not null)
        {
            try { decoded = JsonSerializer.Deserialize<MetadataCursor>(Convert.FromBase64String(cursor), ApiJson.Options) ?? throw new FormatException(); }
            catch (Exception ex) when (ex is FormatException or JsonException)
            { throw new WorkspaceException("CursorInvalid", "The maintenance cursor is malformed."); }
        }
        var page = FileWorkspaceProvider.ReadMaintenanceResiduals(workspace, control, request, scope, decoded, size);
        return new { root = "sha256:" + page.Root.ContentHash, count = page.Root.Count, page.Entries,
            nextCursor = page.NextCursor is null ? null : Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(page.NextCursor, ApiJson.Options)) };
    }
    private static object ReadSelection(WorkspaceRecord workspace, string control, string? digest, string? cursor, int size)
    {
        if (digest is null) throw new WorkspaceException("CursorInvalid", "Select --plan-id for the fixed selection.");
        var plan = OriginalApplyPages.ReadPlan(control, digest);
        if (plan.OwnerId != workspace.Id || plan.RequestDigest != workspace.RequestDigest)
            throw new WorkspaceException("OriginalApplyStateInvalid", "The plan belongs to another workspace.");
        MetadataCursor? decoded = null;
        if (cursor is not null)
        {
            try { decoded = JsonSerializer.Deserialize<MetadataCursor>(Convert.FromBase64String(cursor), ApiJson.Options) ?? throw new FormatException(); }
            catch (Exception ex) when (ex is FormatException or JsonException)
            { throw new WorkspaceException("CursorInvalid", "The selection cursor is malformed."); }
        }
        var page = OriginalApplyPages.ReadSelectionPage(control, plan, decoded, size);
        return new { root = "sha256:" + page.Root.ContentHash, count = page.Root.Count,
            entries = page.Entries.Select(path => new { path }).ToArray(),
            nextCursor = page.NextCursor is null ? null : Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(page.NextCursor, ApiJson.Options)) };
    }
    private static object ReadDeferred(WorkspaceRecord workspace, string control, string home, string? scope, string? cursor, int size, CancellationToken ct, string? requestKey)
    {
        if (scope == "journal-packing") return ReadPacking(workspace, control, cursor, size, ct);
        if (scope == "journal-indices") return ReadIndexResiduals(workspace, control, cursor, size, ct, requestKey);
        if (scope == "read-only-replay")
        {
            if (workspace.Snapshot?.Reference is null)
                throw new WorkspaceException("CursorInvalid", "The selected workspace has no owned journal identity.");
            var rows = JournalDeltaStore.EnumeratePreservedReadOnlyReplays(control, NtfsSnapshotWorkspaceBackend.Identity(workspace))
                .Select(value => JsonSerializer.SerializeToUtf8Bytes(new { value.Directory, value.Request, value.Reason }, ApiJson.Options));
            return ReadResidualRows(workspace.Id, "read-only-replay", rows, cursor, size, ct);
        }
        MetadataCursor? decoded = null;
        if (cursor is not null)
        {
            try { decoded = JsonSerializer.Deserialize<MetadataCursor>(Convert.FromBase64String(cursor), ApiJson.Options) ?? throw new FormatException(); }
            catch (Exception ex) when (ex is FormatException or JsonException)
            { throw new WorkspaceException("CursorInvalid", "The deferred reclamation cursor is malformed."); }
        }
        if (scope == "ranges")
        {
            using var ranges = new OwnedRangeContentStore(home, new WindowsJournalFilePins());
            var page = ranges.ReadDeferredReclamationPage(decoded, size, ct);
            return DeferredPage(page.Root, page.Entries, page.NextCursor);
        }
        if (scope is "content" or "content-references")
        {
            var contents = new OwnedContentStore(home, new WindowsJournalFilePins());
            var page = contents.ReadDeferredReclamationPage(scope == "content-references", decoded, size, ct);
            return DeferredPage(page.Root, page.Entries, page.NextCursor);
        }
        throw new WorkspaceException("CursorInvalid", "Select content, content-references, ranges, journal-packing, journal-indices or read-only-replay for deferred reclamation.");
    }
    private static object ReadPacking(WorkspaceRecord workspace, string control, string? cursor, int size, CancellationToken ct)
    {
        if (workspace.Snapshot?.Reference is null)
            throw new WorkspaceException("CursorInvalid", "The selected workspace has no owned journal identity.");
        var rows = JournalDeltaStore.EnumeratePreservedPacking(control, NtfsSnapshotWorkspaceBackend.Identity(workspace))
            .Select(proof => JsonSerializer.SerializeToUtf8Bytes(new { proof.Path, proof.Request, proof.Reason }, ApiJson.Options));
        return ReadResidualRows(workspace.Id, "journal-packing", rows, cursor, size, ct);
    }
    private static object ReadIndexResiduals(WorkspaceRecord workspace, string control, string? cursor, int size, CancellationToken ct, string? requestKey)
    {
        if (workspace.Snapshot?.Reference is null) throw new WorkspaceException("CursorInvalid", "The selected workspace has no owned journal identity.");
        var identity = NtfsSnapshotWorkspaceBackend.Identity(workspace);
        if (requestKey is not null)
        {
            var selected = JournalDeltaStore.EnumeratePreservedJournalIndices(control, identity).SingleOrDefault(value => value.Request == requestKey)
                ?? throw new WorkspaceException("CursorInvalid", "Select a preserved journal index request returned by its summary page.");
            MetadataCursor? decoded = null;
            if (cursor is not null)
            {
                try { decoded = JsonSerializer.Deserialize<MetadataCursor>(Convert.FromBase64String(cursor), ApiJson.Options) ?? throw new FormatException(); }
                catch (Exception error) when (error is FormatException or JsonException) { throw new WorkspaceException("CursorInvalid", "The preserved index cursor is malformed."); }
            }
            using var index = new PagedMetadataStore(selected.InventoryDirectory); var page = index.Open(selected.Entries).ReadPage(decoded, size);
            return new { root = "sha256:" + page.Root.ContentHash, count = page.Root.Count,
                entries = page.Entries.Select(value => new { path = value.Key, details = JsonSerializer.Deserialize<JsonElement>(value.Value.Span, ApiJson.Options) }).ToArray(),
                nextCursor = page.NextCursor is null ? null : Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(page.NextCursor, ApiJson.Options)) };
        }
        var indices = JournalDeltaStore.EnumeratePreservedJournalIndices(control, identity)
            .Select(value => JsonSerializer.SerializeToUtf8Bytes(new { kind = "preserved-index", index = value }, ApiJson.Options));
        var attempts = JournalDeltaStore.EnumerateUnpublishedIndexResidualControls(control, identity)
            .Select(value => JsonSerializer.SerializeToUtf8Bytes(new { kind = "unpublished-control", value.Path, value.Request, value.Reason }, ApiJson.Options));
        var pending = JournalDeltaStore.EnumeratePendingJournalIndexPreservations(control, identity)
            .Select(value => JsonSerializer.SerializeToUtf8Bytes(new { kind = "pending-preservation", value.Path, value.Request, value.Reason }, ApiJson.Options));
        var unverified = JournalDeltaStore.EnumerateUnverifiedJournalIndexFiles(control, identity, new WindowsJournalFilePins())
            .Select(value => JsonSerializer.SerializeToUtf8Bytes(new { kind = "unverified-file", value.Path, value.Request, value.Reason }, ApiJson.Options));
        return ReadResidualRows(workspace.Id, "journal-indices", indices.Concat(attempts).Concat(pending).Concat(unverified), cursor, size, ct);
    }
    private static object ReadResidualRows(string workspace, string collection, IEnumerable<byte[]> values, string? cursor, int size, CancellationToken ct)
    {
        using var scratch = new TemporaryMetadataDirectory();
        using var pages = new PagedMetadataStore(scratch.Path, true);
        IEnumerable<MetadataEntry> Rows()
        {
            foreach (byte[] data in values)
            {
                ct.ThrowIfCancellationRequested();
                IEnumerable<MetadataEntry> Chunks()
                { for (int at = 0, index = 0; at < data.Length; at += 32768, index++) yield return new(index.ToString("x8"), data.AsMemory(at, Math.Min(32768, data.Length - at))); }
                var chunks = pages.Build(Chunks(), ct);
                yield return new(Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(data)), ReadOnlyMemory<byte>.Empty, chunks);
            }
        }
        var root = pages.Build(MetadataExternalSort.Order(Rows(), scratch.Path, ct), ct); pages.Flush();
        IEnumerable<JsonElement> Entries(long start)
        {
            long ordinal = 0;
            foreach (var row in pages.Open(root).Enumerate())
            {
                if (ordinal++ < start) continue;
                using var value = new MemoryStream();
                foreach (var chunk in pages.Open(row.Child!).Enumerate()) value.Write(chunk.Value.Span);
                value.Position = 0; using var document = JsonDocument.Parse(value); yield return document.RootElement.Clone();
            }
        }
        return ReadSequence(workspace, collection, "sha256:" + root.ContentHash, root.Count, Entries, cursor, size);
    }
    private static object DeferredPage<T>(MetadataTreeRoot? root, T[] entries, MetadataCursor? cursor) => new
    {
        root = "sha256:" + (root ?? new MetadataTreeRoot(1, "", null)).ContentHash, count = root?.Count ?? 0,
        countKind = "indexed-candidates", entries,
        nextCursor = cursor is null ? null : Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(cursor, ApiJson.Options))
    };
    private static IEnumerable<OriginalApplyChange> Changes(OriginalApplyPlan plan, long start)
    { for (long index = start; index < plan.ChangeCount; index++) yield return plan.ChangeAt(index); }
    private static string Hash(WorkspaceHistoryReference reference) => reference.Root?.ContentHash ?? new MetadataTreeRoot(1, "", null).ContentHash;
    private static object ReadSequence<T>(string workspace, string collection, string root, long count,
        Func<long, IEnumerable<T>> enumerate, string? cursor, int size)
    {
        string scope = workspace + "\n" + collection + "\n" + root + "\n"; long start = 0;
        if (cursor is not null)
        {
            try
            {
                string decoded = new UTF8Encoding(false, true).GetString(Convert.FromBase64String(cursor));
                if (!decoded.StartsWith(scope, StringComparison.Ordinal) || !long.TryParse(decoded.AsSpan(scope.Length), NumberStyles.None,
                        CultureInfo.InvariantCulture, out start) || start < 0 || start > count) throw new FormatException();
            }
            catch (Exception ex) when (ex is FormatException or DecoderFallbackException)
            { throw new WorkspaceException("CursorInvalid", "The cursor is malformed, stale or belongs to another collection root."); }
        }
        T[] entries = enumerate(start).Take(size).ToArray(); long next = checked(start + entries.LongLength);
        return new { root, count, entries, nextCursor = next == count ? null : Convert.ToBase64String(Encoding.UTF8.GetBytes(scope + next.ToString(CultureInfo.InvariantCulture))) };
    }
}
