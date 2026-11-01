using System.Text;
using Splinter.Core;

namespace Splinter.Runtime;

public sealed partial class JournalDeltaStore
{
    private sealed record FrozenCaptureControl(string Id, string Directory, MetadataTreeRoot Control, string Digest);
    private sealed class CaptureControlWriter(PagedMetadataStore store) : Stream
    {
        private readonly byte[] buffer = new byte[60000]; private int held; private long chunk;
        public MetadataTreeRoot Root { get; private set; } = store.Empty;
        private void CommitChunk() { if (held == 0) return; Root = store.Set(Root, (chunk++).ToString("x16"), buffer.AsSpan(0, held)); held = 0; }
        public MetadataTreeRoot Complete() { CommitChunk(); return Root; }
        public override void Write(ReadOnlySpan<byte> bytes)
        { while (!bytes.IsEmpty) { int count = Math.Min(buffer.Length - held, bytes.Length); bytes[..count].CopyTo(buffer.AsSpan(held)); held += count; bytes = bytes[count..]; if (held == buffer.Length) CommitChunk(); } }
        public override void Write(byte[] bytes, int offset, int count) => Write(bytes.AsSpan(offset, count));
        public override bool CanWrite => true; public override bool CanRead => false; public override bool CanSeek => false;
        public override long Length => throw new NotSupportedException(); public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { } public override int Read(byte[] b, int o, int n) => throw new NotSupportedException();
        public override long Seek(long o, SeekOrigin s) => throw new NotSupportedException(); public override void SetLength(long v) => throw new NotSupportedException();
    }
    private sealed class CaptureControlReader : Stream
    {
        private readonly IEnumerator<MetadataEntry> chunks; private byte[] current = []; private int at; private long expected;
        public CaptureControlReader(PagedMetadataStore store, MetadataTreeRoot root) => chunks = store.Open(root).Enumerate().GetEnumerator();
        public override int Read(Span<byte> bytes)
        {
            int total = 0;
            while (!bytes.IsEmpty)
            {
                if (at == current.Length)
                {
                    if (!chunks.MoveNext()) break;
                    if (chunks.Current.Key != (expected++).ToString("x16") || chunks.Current.Child is not null || chunks.Current.Value.Length is < 1 or > 60000) throw PagedInvalid();
                    current = chunks.Current.Value.ToArray(); at = 0;
                }
                int count = Math.Min(bytes.Length, current.Length - at); current.AsSpan(at, count).CopyTo(bytes); at += count; bytes = bytes[count..]; total += count;
            }
            return total;
        }
        public override int Read(byte[] bytes, int offset, int count) => Read(bytes.AsSpan(offset, count));
        public override bool CanWrite => false; public override bool CanRead => true; public override bool CanSeek => false;
        public override long Length => throw new NotSupportedException(); public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { } public override void Write(byte[] b, int o, int n) => throw new NotSupportedException();
        public override long Seek(long o, SeekOrigin s) => throw new NotSupportedException(); public override void SetLength(long v) => throw new NotSupportedException();
        protected override void Dispose(bool disposing) { if (disposing) chunks.Dispose(); base.Dispose(disposing); }
    }
    private static bool CaptureId(string id) => id.Length == 24 && id.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
    private static JournalSourceCapture LoadFrozenCapture(PagedMetadataStore store, MetadataEntry record)
    {
        if (!CaptureId(record.Key) || record.Child is null || record.Value.Length != 64) throw PagedInvalid();
        using var input = new CaptureControlReader(store, record.Child); var capture = SourceCaptureTree.DeserializeControl(input);
        if (capture.Id != record.Key || capture.Digest != Encoding.ASCII.GetString(record.Value.Span) || capture.Digest != SourceCaptureTree.Digest(capture)) throw PagedInvalid();
        return capture;
    }
    private FrozenCaptureControl StoreFrozenCapture(JournalSourceCapture capture)
    {
        using var output = new CaptureControlWriter(pagedIndex!); SourceCaptureTree.SerializeControl(output, capture); var root = output.Complete(); pagedIndex!.Flush();
        return new(capture.Id, pagedIndexDirectory!, root, capture.Digest);
    }
    private void ApplyCaptureControl(Mutation mutation)
    {
        if (segments is null && !segmentShadow || mutation.Sequence != sequence || mutation.Entry != nodes[paths[""]].Entry) throw PagedInvalid();
        if (mutation.Operation == "capture-complete")
        {
            string id = mutation.CaptureId ?? throw PagedInvalid(); if (!CaptureId(id)) throw PagedInvalid();
            captureRoot = pagedIndex!.Remove(captureRoot!, id); captureProtectionGeneration++; return;
        }
        var control = mutation.FrozenCapture ?? throw PagedInvalid(); if (!CaptureId(control.Id) ||
            !WorkspaceStore.SamePath(Path.GetDirectoryName(control.Directory)!, Path.GetDirectoryName(pagedIndexDirectory!)!) || !IndexName(Path.GetFileName(control.Directory))) throw PagedInvalid();
        MetadataTreeRoot root = control.Control;
        if (control.Control.StoreId != pagedIndex!.StoreId || !WorkspaceStore.SamePath(control.Directory, pagedIndexDirectory!))
        {
            if (!segmentShadow) throw PagedInvalid(); ValidateIndexOwner(control.Directory, Identity);
            using var old = new PagedMetadataStore(control.Directory);
            root = pagedIndex.Build(old.Open(control.Control).Enumerate());
        }
        var record = new MetadataEntry(control.Id, Encoding.ASCII.GetBytes(control.Digest), root); var capture = LoadFrozenCapture(pagedIndex, record);
        if (capture.Sequence != sequence || !SourceCaptureTree.SameIdentity(capture.Identity, Identity)) throw PagedInvalid();
        var existing = pagedIndex.Open(captureRoot!).FindEntry(control.Id);
        if (existing is not null)
        { if (!existing.Value.Span.SequenceEqual(record.Value.Span)) throw new WorkspaceException("SourceCaptureRequestConflict", "Retain the first accepted frozen source request."); return; }
        captureRoot = pagedIndex.Set(captureRoot!, control.Id, record.Value.Span, root); captureProtectionGeneration++;
    }
    public void CompleteSourceCaptureRequest(string id)
    {
        using (EnterGate(mutation: true))
        {
            Check(); if (pagedIndex!.Open(captureRoot!).FindEntry(id) is not { } record) return;
            var capture = LoadFrozenCapture(pagedIndex, record);
            SourceCaptureTree.RequireMaterialized(Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(pagedIndexDirectory!)!)!, "captures", id), capture);
            FlushPendingWrites(); Commit(new(sequence, "capture-complete", nodes[paths[""]].Entry, CaptureId: id), []);
        }
    }
    public void RecoverSourceCaptures(string directory)
    {
        if (segments is null || sourceFilePins is null) return;
        foreach (var capture in EnumerateFrozenCaptures())
        {
            string target = Path.Combine(directory, capture.Id); RepairFrozenCapture(target, capture);
            if (File.Exists(Path.Combine(target, "materialized.json")))
            { SourceCaptureTree.Complete(target, sourceFilePins); CompleteSourceCaptureRequest(capture.Id); }
            else SourceCaptureTree.EnsurePins(target, capture, sourceFilePins);
        }
        SourceCaptureTree.RecoverPins(directory, sourceFilePins);
    }
    private IEnumerable<JournalSourceCapture> EnumerateFrozenCaptures()
    {
        PagedMetadataStore reader; MetadataTreeRoot root;
        using (EnterGate()) { pagedIndex!.Flush(); root = captureRoot!; reader = new(pagedIndexDirectory!); }
        using (reader) foreach (var record in reader.Open(root).Enumerate()) yield return LoadFrozenCapture(reader, record);
    }
    private static void RepairFrozenCapture(string destination, JournalSourceCapture capture)
    {
        Directory.CreateDirectory(destination); string path = Path.Combine(destination, "capture.json");
        if (File.Exists(path))
        { var current = SourceCaptureTree.ReadCapture(destination, verifyPagedRoots: false); if (current.Digest != capture.Digest) throw PagedInvalid(); }
        else SourceCaptureTree.Publish(path, capture);
    }
}
