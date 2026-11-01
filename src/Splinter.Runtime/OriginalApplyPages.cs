using System.Buffers.Binary;
using System.Text;
using Splinter.Core;

namespace Splinter.Runtime;

// Fixed plans and sparse progress share immutable metadata containers. The
// control record publishes only roots after their pages have been flushed.
public static class OriginalApplyPages
{
    private static readonly UTF8Encoding Utf8 = new(false, true);
    private static string Location(string control) => Path.Combine(control, "original-pages");
    public static OriginalApplyPlan Create(OriginalApplyPlan template, IEnumerable<OriginalApplyChange> changes, string control,
        CancellationToken ct = default, IJournalFilePins? filePins = null, IEnumerable<string>? selection = null)
    {
        using var store = new PagedMetadataStore(Location(control), true, filePins: filePins);
        var paths = new PagedPathInterner(store, template.PathNamesRoot);
        using var spool = new MetadataSpool(Location(control)); long number = 0;
        foreach (var change in changes)
        { ct.ThrowIfCancellationRequested(); spool.Append(new(Key(number++), EncodeChange(change))); }
        paths.Import(spool.Read().Select(entry => DecodeChange(entry.Value.Span).Path), ct);
        selection ??= template.SelectionRoot is null ? template.SelectedPaths : template.EnumerateSelection();
        MetadataTreeRoot? selectedRoot = null; string[]? selectedPreview = null;
        if (selection is not null)
        {
            using var selected = new MetadataSpool(Location(control)); long ordinal = 0;
            foreach (string path in selection)
            { if (!OriginalApplyState.ValidSelectionPath(path)) throw Invalid("The selected publication path is invalid."); selected.Append(new(Key(ordinal++), Utf8.GetBytes(path))); }
            if (ordinal == 0) throw Invalid("A selected publication scope cannot be empty.");
            paths.Import(selected.Read().Select(entry => Utf8.GetString(entry.Value.Span)), ct);
            IEnumerable<MetadataEntry> SelectionEntries()
            {
                foreach (var entry in selected.Read())
                { string path = Utf8.GetString(entry.Value.Span); yield return new(PagedPathInterner.LookupIdentity(path), Convert.FromHexString(PagedPathInterner.Reference(path))); }
            }
            selectedRoot = store.Build(MetadataSort.Order(SelectionEntries(), Location(control), ct), ct);
            selectedPreview = SelectionPreview(SelectionPaths(store, selectedRoot, paths));
        }
        IEnumerable<MetadataEntry> Entries()
        {
            foreach (var entry in spool.Read()) yield return new(entry.Key, EncodeChange(DecodeChange(entry.Value.Span), paths));
        }
        var root = store.Build(Entries(), ct);
        IEnumerable<MetadataEntry> Names()
        {
            long index = 0;
            foreach (var entry in store.Open(root).Enumerate())
            {
                var change = DecodeChange(entry.Value.Span, paths);
                byte[] number = new byte[8]; BinaryPrimitives.WriteInt64LittleEndian(number, index++);
                yield return new(PagedPathInterner.LookupIdentity(change.Path), number);
            }
        }
        var names = store.Build(MetadataSort.Order(Names(), Location(control), ct), ct); store.Flush();
        var plan = template with { Version = 3, Changes = [], ChangesRoot = root, PathsRoot = names, PathNamesRoot = paths.Root,
            SelectionRoot = selectedRoot, SelectedPaths = selectedPreview, Digest = "" };
        plan = plan with { Digest = plan.ComputeDigest() }; Bind(plan, control, filePins); return plan;
    }
    public static IEnumerable<OriginalApplyChange> OrderChanges(IEnumerable<OriginalApplyChange> changes, string control, CancellationToken ct = default)
    {
        IEnumerable<MetadataEntry> Encoded()
        {
            foreach (var change in changes)
            {
                int phase = change.After?.Kind == "directory" ? 0 : change.Before?.Kind == "directory" ? 2 : 1;
                int depth = change.Path.Count(c => c == '/');
                int rank = phase == 2 && change.After is null ? int.MaxValue - depth : depth;
                yield return new(phase.ToString() + rank.ToString("x8") + "/" + change.Path, EncodeChange(change));
            }
        }
        foreach (var entry in MetadataSort.Order(Encoded(), Location(control), ct)) yield return DecodeChange(entry.Value.Span);
    }
    public static OriginalApplyPlan WithConflicts(OriginalApplyPlan plan, IEnumerable<string> conflicts, string control, IJournalFilePins? filePins = null)
    {
        using var store = new PagedMetadataStore(Location(control), true, filePins: filePins); var preview = new List<string>(100);
        var paths = new PagedPathInterner(store, plan.PathNamesRoot);
        using var spool = new MetadataSpool(Location(control)); long number = 0;
        foreach (string conflict in conflicts)
        {
            if (preview.Count < 100) preview.Add(ConflictPreview(conflict));
            spool.Append(new(Key(number++), Utf8.GetBytes(conflict)));
        }
        paths.Import(spool.Read().Select(entry =>
        {
            string text = Utf8.GetString(entry.Value.Span); int split = text.IndexOf(": ", StringComparison.Ordinal); return split < 0 ? "" : text[..split];
        }));
        IEnumerable<MetadataEntry> Entries()
        { foreach (var entry in spool.Read()) yield return new(entry.Key, EncodeConflict(Utf8.GetString(entry.Value.Span), paths)); }
        var root = store.Build(Entries()); store.Flush();
        var result = plan with { ConflictsRoot = root, PathNamesRoot = paths.Root, Conflicts = preview.ToArray(), Digest = "" };
        result = result with { Digest = result.ComputeDigest() }; Bind(result, control, filePins); return result;
    }
    public static void SavePlan(OriginalApplyPlan plan, string control)
    {
        Bind(plan, control);
        string directory = Path.Combine(control, "original-plans"); WorkspaceStore.EnsurePlainPath(directory); Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, plan.Digest[7..] + ".json"); WorkspaceStore.EnsurePlainPath(path);
        if (File.Exists(path))
        {
            // A plan can be rebuilt into a different physical page address while
            // retaining the same logical digest. Keep the first owned artifact.
            _ = ReadPlan(control, plan.Digest); return;
        }
        string pending = path + "." + Guid.NewGuid().ToString("N") + ".tmp"; WorkspaceStore.EnsurePlainPath(pending);
        using (var output = new FileStream(pending, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        { System.Text.Json.JsonSerializer.Serialize(output, plan, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)); output.Flush(true); }
        AtomicFilePublication.Publish(pending, path);
    }
    public static OriginalApplyPlan ReadPlan(string control, string digest)
    {
        if (digest.Length != 71 || !digest.StartsWith("sha256:", StringComparison.Ordinal) || digest[7..].Any(c => c is not (>= '0' and <= '9' or >= 'a' and <= 'f')))
            throw Invalid("Supply the exact fixed plan digest.");
        string path = Path.Combine(control, "original-plans", digest[7..] + ".json"); WorkspaceStore.EnsurePlainPath(path);
        using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var plan = System.Text.Json.JsonSerializer.Deserialize<OriginalApplyPlan>(input, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)) ?? throw Invalid("The fixed plan artifact is empty.");
        if (plan.Digest != digest || plan.ComputeDigest() != digest) throw Invalid("The fixed plan artifact identity changed.");
        Bind(plan, control); return plan;
    }
    public sealed record SelectionPage(MetadataTreeRoot Root, string[] Entries, MetadataCursor? NextCursor);
    public static SelectionPage ReadSelectionPage(string control, OriginalApplyPlan plan, MetadataCursor? cursor = null, int pageSize = 128)
    {
        using var pages = new PagedMetadataStore(Location(control)); var root = plan.SelectionRoot ?? pages.Empty;
        var page = pages.Open(root).ReadPage(cursor, pageSize); var names = new PagedPathInterner(pages, plan.PathNamesRoot);
        return new(page.Root, page.Entries.Select(entry =>
        {
            if (entry.Value.Length != 32 || entry.Child is not null) throw Invalid("A selection page contains malformed path data.");
            string path = names.Resolve(Convert.ToHexStringLower(entry.Value.Span));
            if (entry.Key != PagedPathInterner.LookupIdentity(path) || !OriginalApplyState.ValidSelectionPath(path)) throw Invalid("A selection page contains another path identity.");
            return path;
        }).ToArray(), page.NextCursor);
    }
    public static bool MatchesSelection(OriginalApplyPlan plan, string control, IEnumerable<string>? paths)
    {
        // Once intent and digest are fixed, absence of repeated CLI paths asks
        // to recover that exact scope. New intents still plan null as full scope.
        if (paths is null) return true;
        if (plan.SelectionRoot is null) return plan.SelectedPaths is not null && plan.SelectedPaths.Order(StringComparer.Ordinal)
            .SequenceEqual(paths.Order(StringComparer.Ordinal), StringComparer.Ordinal);
        using var work = new TemporaryMetadataDirectory(Location(control)); using var pages = new PagedMetadataStore(work.Path, true);
        IEnumerable<MetadataEntry> Entries()
        {
            foreach (string path in paths)
            {
                if (!OriginalApplyState.ValidSelectionPath(path)) throw Invalid("A retry selection contains an invalid path.");
                yield return new(PagedPathInterner.LookupIdentity(path), Convert.FromHexString(PagedPathInterner.Reference(path)));
            }
        }
        try
        {
            var root = pages.Build(MetadataSort.Order(Entries(), work.Path));
            return root.Count == plan.SelectionRoot.Count && root.ContentHash == plan.SelectionRoot.ContentHash;
        }
        catch (WorkspaceException error) when (error.Code is "MetadataStoreInvalid" or "PathIndexInvalid") { return false; }
    }
    public static OriginalApplyState Initialize(OriginalApplyPlan plan, string intentKey, string control, IJournalFilePins? filePins = null)
    {
        using var store = new PagedMetadataStore(Location(control), true, filePins: filePins); store.Flush();
        var state = new OriginalApplyState { Plan = plan, IntentKey = intentKey, Steps = [], StepsRoot = store.Empty };
        Bind(state, control, filePins); return state;
    }
    public static void Bind(OriginalApplyPlan plan, string control, IJournalFilePins? filePins = null)
    {
        if (plan.ChangesRoot is null) return;
        if (plan.Version != 3 || plan.PathsRoot is null || plan.PathNamesRoot is null || plan.PathsRoot.Count != plan.ChangesRoot.Count)
            throw Invalid("The fixed publication name index is missing or mismatched.");
        if (plan.ConflictsRoot is not null)
        {
            if (plan.Conflicts.Length != Math.Min(100L, plan.ConflictsRoot.Count)) throw Invalid("The conflict preview differs from its immutable root.");
            using var pages = new PagedMetadataStore(Location(control)); long index = 0;
            var paths = new PagedPathInterner(pages, plan.PathNamesRoot);
            foreach (var entry in pages.Open(plan.ConflictsRoot).Enumerate().Take(100))
            {
                if (entry.Key != Key(index) || ConflictPreview(DecodeConflict(entry.Value.Span, paths)) != plan.Conflicts[index++])
                    throw Invalid("The conflict preview differs from its immutable pages.");
            }
        }
        else if (plan.Conflicts.Length != 0) throw Invalid("A paged plan must retain its full conflict root.");
        if (plan.SelectionRoot is not null)
        {
            using var pages = new PagedMetadataStore(Location(control));
            if (plan.SelectionRoot.Count == 0 || plan.SelectedPaths is null || !plan.SelectedPaths.SequenceEqual(
                SelectionPreview(SelectionPaths(pages, plan.SelectionRoot, new(pages, plan.PathNamesRoot))), StringComparer.Ordinal))
                throw Invalid("The bounded selection preview differs from its fixed root.");
        }
        else if (plan.SelectedPaths is not null) throw Invalid("A paged publication selection requires a fixed root.");
        if (plan.PageReader is not PlanReader current || current.Directory != Location(control) || current.Root != plan.ChangesRoot || current.Names != plan.PathsRoot || current.PathNames != plan.PathNamesRoot ||
            current.SelectionRoot != plan.SelectionRoot || filePins is not null && current.FilePins is null)
            plan.PageReader = new PlanReader(Location(control), plan.ChangesRoot, plan.PathsRoot, plan.PathNamesRoot, plan.SelectionRoot, filePins);
    }
    public static void Bind(OriginalApplyState state, string control, IJournalFilePins? filePins = null)
    {
        Bind(state.Plan, control, filePins);
        if (state.StepsRoot is not null && (state.PageProgress is not Progress current || !current.BelongsTo(state, Location(control)) || filePins is not null && !current.HasFilePins))
            state.PageProgress = new Progress(Location(control), state, filePins);
    }
    public static IDisposable BeginPublication(OriginalApplyState state)
    {
        if (state.Plan.PageReader is not PlanReader reader || state.PageProgress is not Progress progress) return NoScope.Instance;
        return new PublicationScope(reader, progress);
    }
    public static void SaveSteps(OriginalApplyState state, IReadOnlyList<KeyValuePair<long, OriginalApplyStep>> steps)
    {
        if (steps.Count > 64 || steps.Select(item => item.Key).Distinct().Count() != steps.Count ||
            steps.Any(item => item.Key < 0 || item.Key >= state.Plan.ChangeCount))
            throw Invalid("A publication progress batch must contain at most 64 distinct fixed-plan entries.");
        if (state.PageProgress is Progress progress) state.StepsRoot = progress.SaveBatch(steps);
        else foreach (var step in steps) state.SaveStep(step.Key, step.Value);
    }
    private sealed class PublicationScope : IDisposable
    {
        private readonly PlanReader reader; private readonly Progress progress; private readonly PagedMetadataStore pages;
        internal PublicationScope(PlanReader reader, Progress progress)
        {
            this.reader = reader; this.progress = progress;
            pages = new PagedMetadataStore(reader.Directory, true, filePins: reader.FilePins); reader.Shared = pages; progress.Shared = pages;
        }
        public void Dispose() { reader.Shared = null; progress.Shared = null; pages.Dispose(); }
    }
    private sealed class NoScope : IDisposable { internal static readonly NoScope Instance = new(); public void Dispose() { } }
    private sealed class PlanReader(string directory, MetadataTreeRoot root, MetadataTreeRoot names, MetadataTreeRoot pathNames,
        MetadataTreeRoot? selectionRoot, IJournalFilePins? filePins) : IOriginalApplyPlanReader
    {
        internal string Directory => directory;
        internal IJournalFilePins? FilePins => filePins;
        internal MetadataTreeRoot Root => root;
        internal MetadataTreeRoot Names => names;
        internal MetadataTreeRoot PathNames => pathNames;
        internal MetadataTreeRoot? SelectionRoot => selectionRoot;
        internal PagedMetadataStore? Shared { get; set; }
        public OriginalApplyChange? Find(string path)
        {
            using var owned = Shared is null ? new PagedMetadataStore(directory) : null; var store = Shared ?? owned!;
            var raw = store.Open(names).Find(PagedPathInterner.LookupIdentity(path));
            if (raw is null) return null;
            if (raw.Length != 8) throw Invalid("A publication path ordinal is malformed.");
            return At(BinaryPrimitives.ReadInt64LittleEndian(raw));
        }
        public OriginalApplyChange At(long index)
        {
            if (index < 0 || index >= root.Count) throw Invalid("Publication index is outside the fixed root.");
            using var owned = Shared is null ? new PagedMetadataStore(directory) : null;
            var store = Shared ?? owned!;
            var raw = store.Open(root).Find(Key(index)) ?? throw Invalid("The fixed publication entry is missing.");
            var change = DecodeChange(raw, new PagedPathInterner(store, pathNames)); CheckName(store, change, index); return change;
        }
        public IEnumerable<OriginalApplyChange> Enumerate()
        {
            using var owned = Shared is null ? new PagedMetadataStore(directory) : null;
            var store = Shared ?? owned!; long index = 0;
            foreach (var entry in store.Open(root).Enumerate())
            {
                if (entry.Key != Key(index)) throw Invalid("Publication page ordinals are incomplete.");
                var change = DecodeChange(entry.Value.Span, new PagedPathInterner(store, pathNames)); CheckName(store, change, index++); yield return change;
            }
            if (index != root.Count) throw Invalid("Publication root count is inconsistent.");
        }
        public IEnumerable<string> Selection()
        {
            if (selectionRoot is null) yield break;
            using var owned = Shared is null ? new PagedMetadataStore(directory) : null; var pages = Shared ?? owned!;
            foreach (string path in SelectionPaths(pages, selectionRoot, new(pages, pathNames))) yield return path;
        }
        private void CheckName(PagedMetadataStore store, OriginalApplyChange change, long index)
        {
            var value = store.Open(names).Find(PagedPathInterner.LookupIdentity(change.Path));
            if (value is null || value.Length != 8 || BinaryPrimitives.ReadInt64LittleEndian(value) != index)
                throw Invalid("Publication name identity differs from its ordinal index.");
        }
    }
    private static IEnumerable<string> SelectionPaths(PagedMetadataStore pages, MetadataTreeRoot root, PagedPathInterner names)
    {
        foreach (var entry in pages.Open(root).Enumerate())
        {
            if (entry.Value.Length != 32 || entry.Child is not null) throw Invalid("A selected path reference is malformed.");
            string path = names.Resolve(Convert.ToHexStringLower(entry.Value.Span));
            if (!OriginalApplyState.ValidSelectionPath(path) || entry.Key != PagedPathInterner.LookupIdentity(path)) throw Invalid("The selected name identity differs from its fixed path.");
            yield return path;
        }
    }
    private static string[] SelectionPreview(IEnumerable<string> entries)
    {
        var result = new List<string>(100); int characters = 0;
        foreach (string path in entries)
        { if (result.Count == 100 || path.Length > 4096 - characters) break; result.Add(path); characters += path.Length; }
        return result.ToArray();
    }
    private sealed class Progress(string directory, OriginalApplyState state, IJournalFilePins? filePins) : IOriginalApplyProgress
    {
        internal bool HasFilePins => filePins is not null;
        internal bool BelongsTo(OriginalApplyState other, string path) => ReferenceEquals(state, other) && directory == path;
        internal PagedMetadataStore? Shared { get; set; }
        public IEnumerable<KeyValuePair<long, OriginalApplyStep>> Enumerate()
        {
            using var owned = Shared is null ? new PagedMetadataStore(directory) : null;
            var store = Shared ?? owned!;
            foreach (var entry in store.Open(state.StepsRoot!).Enumerate())
            {
                if (entry.Key.Length != 16 || !long.TryParse(entry.Key, System.Globalization.NumberStyles.HexNumber,
                    System.Globalization.CultureInfo.InvariantCulture, out long index) || index < 0 || index >= state.Plan.ChangeCount)
                    throw Invalid("Progress contains an entry outside the fixed publication.");
                yield return new(index, DecodeStep(entry.Value.Span));
            }
        }
        public OriginalApplyStep Get(long index)
        {
            if (index < 0 || index >= state.Plan.ChangeCount) throw Invalid("Progress index is outside the fixed plan.");
            using var owned = Shared is null ? new PagedMetadataStore(directory) : null;
            var store = Shared ?? owned!;
            var raw = store.Open(state.StepsRoot!).Find(Key(index));
            return raw is null ? new() : DecodeStep(raw);
        }
        public MetadataTreeRoot Save(long index, OriginalApplyStep step)
        {
            if (index < 0 || index >= state.Plan.ChangeCount) throw Invalid("Progress index is outside the fixed plan.");
            using var owned = Shared is null ? new PagedMetadataStore(directory, true, filePins: filePins) : null;
            var store = Shared ?? owned!;
            var root = store.Set(state.StepsRoot!, Key(index), EncodeStep(step)); store.Flush(); return root;
        }
        internal MetadataTreeRoot SaveBatch(IReadOnlyList<KeyValuePair<long, OriginalApplyStep>> steps)
        {
            using var owned = Shared is null ? new PagedMetadataStore(directory, true, filePins: filePins) : null;
            var store = Shared ?? owned!;
            var root = store.SetBatch(state.StepsRoot!, steps.Select(item => new MetadataEntry(Key(item.Key), EncodeStep(item.Value))).ToArray());
            store.Flush(); return root;
        }
        public MetadataTreeRoot PreserveScratch(string path, string? observedIdentity, string reason)
        {
            using var owned = Shared is null ? new PagedMetadataStore(directory, true, filePins: filePins) : null; var store = Shared ?? owned!;
            using var memory = new MemoryStream(); using var writer = new BinaryWriter(memory, Utf8, true);
            var paths = new PagedPathInterner(store, state.ScratchNamesRoot);
            writer.Write((byte)2); writer.Write(Convert.FromHexString(paths.Intern(path))); writer.Write(observedIdentity is not null);
            if (observedIdentity is not null) WriteString(writer, observedIdentity); WriteString(writer, reason);
            var root = store.Set(state.PreservedScratchRoot ?? store.Empty, PagedPathInterner.LookupIdentity(path), memory.ToArray()); store.Flush();
            state.ScratchNamesRoot = paths.Root; return root;
        }
    }
    public sealed record PreservedScratchPage(MetadataTreeRoot Root, PreservedPublicationScratch[] Entries, MetadataCursor? NextCursor);
    public static PreservedScratchPage ReadPreservedScratchPage(string control, OriginalApplyState state, MetadataCursor? cursor = null, int pageSize = 128)
    {
        using var pages = new PagedMetadataStore(Location(control));
        var page = pages.Open(state.PreservedScratchRoot ?? pages.Empty).ReadPage(cursor, pageSize);
        var paths = new PagedPathInterner(pages, state.ScratchNamesRoot);
        return new(page.Root, page.Entries.Select(entry => DecodeScratch(entry, paths)).ToArray(), page.NextCursor);
    }
    public static IEnumerable<PreservedPublicationScratch> EnumeratePreservedScratch(string control, OriginalApplyState state)
    {
        if (state.PreservedScratchRoot is null) yield break;
        using var pages = new PagedMetadataStore(Location(control));
        var paths = new PagedPathInterner(pages, state.ScratchNamesRoot);
        foreach (var entry in pages.Open(state.PreservedScratchRoot).Enumerate())
        {
            yield return DecodeScratch(entry, paths);
        }
    }
    private static PreservedPublicationScratch DecodeScratch(MetadataEntry entry, PagedPathInterner paths)
    {
        try
        {
            using var memory = new MemoryStream(entry.Value.ToArray(), false); using var reader = new BinaryReader(memory, Utf8);
            if (reader.ReadByte() != 2) throw Invalid("Unsupported preserved scratch record.");
            string path = paths.Resolve(ReadHash(reader)); string? observed = ReadBool(reader) ? ReadString(reader) : null; string reason = ReadString(reader);
            if (memory.Position != memory.Length || entry.Key != PagedPathInterner.LookupIdentity(path) || !path.Split('/').Last().StartsWith(".splinter-apply-", StringComparison.Ordinal))
                throw Invalid("A preserved scratch record has another path or trailing data.");
            return new(path, observed, reason);
        }
        catch (Exception ex) when (ex is EndOfStreamException or DecoderFallbackException or ArgumentException)
        { throw Invalid("A preserved publication scratch record is malformed."); }
    }
    public static IEnumerable<(long Index, OriginalApplyStep Step)> EnumerateProgress(OriginalApplyState state, string control)
    {
        if (state.StepsRoot is null)
        { for (int i = 0; i < state.Steps.Length; i++) yield return (i, state.Steps[i]); yield break; }
        using var store = new PagedMetadataStore(Location(control));
        foreach (var entry in store.Open(state.StepsRoot).Enumerate())
        {
            if (entry.Key.Length != 16 || !long.TryParse(entry.Key, System.Globalization.NumberStyles.HexNumber,
                System.Globalization.CultureInfo.InvariantCulture, out long index) || index < 0 || index >= state.Plan.ChangeCount)
                throw Invalid("Progress contains an entry outside the fixed publication.");
            yield return (index, DecodeStep(entry.Value.Span));
        }
    }
    private static string Key(long index) => index.ToString("x16", System.Globalization.CultureInfo.InvariantCulture);
    private static byte[] EncodeChange(OriginalApplyChange change, PagedPathInterner? paths = null)
    {
        using var memory = new MemoryStream(); using var writer = new BinaryWriter(memory, Utf8, true);
        writer.Write(paths is null ? (byte)1 : (byte)3);
        if (paths is null) WriteString(writer, change.Path); else writer.Write(Convert.FromHexString(PagedPathInterner.Reference(change.Path)));
        WriteVersion(writer, change.Before); WriteVersion(writer, change.After);
        writer.Write(change.Expected is not null);
        if (change.Expected is { } expected) { WriteString(writer, expected.Identity); WriteVersion(writer, expected.Version); }
        writer.Write(change.AlreadyMatches); return memory.ToArray();
    }
    private static OriginalApplyChange DecodeChange(ReadOnlySpan<byte> bytes, PagedPathInterner? paths = null)
    {
        try
        {
            using var memory = new MemoryStream(bytes.ToArray(), false); using var reader = new BinaryReader(memory, Utf8);
            if (reader.ReadByte() != (paths is null ? 1 : 3)) throw Invalid("Unsupported publication entry format.");
            string path = paths is null ? ReadString(reader) : paths.Resolve(ReadHash(reader)); var before = ReadVersion(reader); var after = ReadVersion(reader);
            OriginalFileObservation? observed = ReadBool(reader) ? new(ReadString(reader), ReadVersion(reader) ?? throw Invalid("An observed version is missing.")) : null;
            bool already = ReadBool(reader); if (memory.Position != memory.Length) throw Invalid("Trailing publication entry bytes.");
            return new(path, before, after, observed, already);
        }
        catch (Exception ex) when (ex is EndOfStreamException or DecoderFallbackException or ArgumentException or OverflowException)
        { throw Invalid("Publication entry bytes are malformed or truncated."); }
    }
    private static string ConflictPreview(string value) => value.Length > 256 ? value[..256] + "..." : value;
    private static byte[] EncodeConflict(string text, PagedPathInterner paths)
    {
        int split = text.IndexOf(": ", StringComparison.Ordinal); string path = split < 0 ? "" : text[..split], reason = split < 0 ? text : text[(split + 2)..];
        using var memory = new MemoryStream(); using var writer = new BinaryWriter(memory, Utf8, true);
        writer.Write((byte)1); writer.Write(Convert.FromHexString(PagedPathInterner.Reference(path))); WriteString(writer, reason); return memory.ToArray();
    }
    public static IEnumerable<string> EnumerateConflicts(string control, OriginalApplyPlan plan, long start = 0)
    {
        if (start < 0 || start > plan.ConflictCount) throw Invalid("Conflict cursor is outside the fixed root.");
        if (plan.ConflictsRoot is null) { foreach (var value in plan.Conflicts.Skip(checked((int)start))) yield return value; yield break; }
        using var pages = new PagedMetadataStore(Location(control)); var paths = new PagedPathInterner(pages, plan.PathNamesRoot);
        foreach (var entry in pages.Open(plan.ConflictsRoot).Enumerate(start == 0 ? null : Key(start - 1))) yield return DecodeConflict(entry.Value.Span, paths);
    }
    private static string DecodeConflict(ReadOnlySpan<byte> bytes, PagedPathInterner paths)
    {
        try
        {
            using var memory = new MemoryStream(bytes.ToArray(), false); using var reader = new BinaryReader(memory, Utf8);
            if (reader.ReadByte() != 1) throw Invalid("Unsupported conflict record.");
            string path = paths.Resolve(ReadHash(reader)), reason = ReadString(reader);
            if (memory.Position != memory.Length) throw Invalid("Trailing conflict bytes.");
            return path.Length == 0 ? reason : path + ": " + reason;
        }
        catch (Exception ex) when (ex is EndOfStreamException or DecoderFallbackException or ArgumentException)
        { throw Invalid("Malformed conflict bytes."); }
    }
    private static string ReadHash(BinaryReader reader)
    { byte[] bytes = reader.ReadBytes(32); if (bytes.Length != 32) throw Invalid("Truncated path identity."); return Convert.ToHexStringLower(bytes); }
    private static void WriteVersion(BinaryWriter writer, OriginalFileVersion? version)
    {
        writer.Write(version is null ? (byte)0 : version.Kind == "directory" ? (byte)1 : (byte)2);
        if (version is null) return;
        writer.Write(version.Length); writer.Write(version.Attributes); writer.Write(version.ModifiedUtcTicks);
        if (version.Kind == "file") writer.Write(Convert.FromHexString(version.Hash!));
        writer.Write(version.UnixMode is not null); if (version.UnixMode is { } mode) writer.Write(mode);
    }
    private static OriginalFileVersion? ReadVersion(BinaryReader reader)
    {
        byte tag = reader.ReadByte(); if (tag == 0) return null; if (tag > 2) throw Invalid("Invalid publication entry type.");
        long length = reader.ReadInt64(); int attributes = reader.ReadInt32(); long ticks = reader.ReadInt64();
        byte[]? hash = tag == 2 ? reader.ReadBytes(32) : null;
        if (hash is not null && hash.Length != 32) throw Invalid("Truncated publication digest.");
        uint? mode = ReadBool(reader) ? reader.ReadUInt32() : null;
        if (mode is > 0xFFF) throw Invalid("Invalid publication permission metadata.");
        return new(tag == 1 ? "directory" : "file", length, hash is null ? null : Convert.ToHexStringLower(hash), attributes, ticks, mode);
    }
    private static readonly string[] Phases = ["pending", "staging", "staged", "retired", "published", "done"];
    private static byte[] EncodeStep(OriginalApplyStep step)
    {
        int phase = Array.IndexOf(Phases, step.Phase); if (phase < 0 || step.StagedBytes < 0) throw Invalid("Invalid publication progress.");
        using var memory = new MemoryStream(); using var writer = new BinaryWriter(memory, Utf8, true);
        writer.Write((byte)2); writer.Write((byte)phase); writer.Write(step.StagedBytes); writer.Write(step.StagedIdentity is not null);
        if (step.StagedIdentity is not null) WriteString(writer, step.StagedIdentity);
        writer.Write(step.StageNonce is not null); if (step.StageNonce is not null) WriteString(writer, step.StageNonce); return memory.ToArray();
    }
    private static OriginalApplyStep DecodeStep(ReadOnlySpan<byte> bytes)
    {
        try
        {
            using var memory = new MemoryStream(bytes.ToArray(), false); using var reader = new BinaryReader(memory, Utf8);
            if (reader.ReadByte() != 2) throw Invalid("Unsupported publication progress format.");
            int phase = reader.ReadByte(); long length = reader.ReadInt64(); string? id = ReadBool(reader) ? ReadString(reader) : null;
            string? nonce = ReadBool(reader) ? ReadString(reader) : null;
            if (phase >= Phases.Length || length < 0 || memory.Position != memory.Length) throw Invalid("Malformed publication progress.");
            return new() { Phase = Phases[phase], StagedBytes = length, StagedIdentity = id, StageNonce = nonce };
        }
        catch (Exception ex) when (ex is EndOfStreamException or DecoderFallbackException or ArgumentException)
        { throw Invalid("Publication progress bytes are malformed or truncated."); }
    }
    private static void WriteString(BinaryWriter writer, string value)
    { byte[] bytes = Utf8.GetBytes(value); writer.Write(bytes.Length); writer.Write(bytes); }
    private static string ReadString(BinaryReader reader)
    {
        int length = reader.ReadInt32();
        if (length is < 1 or > 8 * 32767 * 4 + 65536 || length > reader.BaseStream.Length - reader.BaseStream.Position)
            throw Invalid("Invalid publication string length.");
        return Utf8.GetString(reader.ReadBytes(length));
    }
    private static bool ReadBool(BinaryReader reader) => reader.ReadByte() switch { 0 => false, 1 => true, _ => throw Invalid("Invalid publication boolean.") };
    private static WorkspaceException Invalid(string message) => new("OriginalApplyStateInvalid", message);
}
