using Splinter.Core;

namespace Splinter.Runtime;

// The source journal stores only canonical chunk references after adoption.
// Each cached stream keeps the exact backing read fence until it is evicted.
public sealed class OwnedCapturedContentReader : ICapturedContentReader, IDisposable
{
    private readonly WorkspaceRecord workspace;
    private readonly string control;
    private readonly OwnedContentStore whole;
    private readonly OwnedRangeContentStore raw;
    private readonly LinkedList<(string Hash, bool Raw, Stream Input)> streams = new();
    private readonly object gate = new();
    private bool disposed;
    private bool scoped;
    internal long ProofAdmissions { get; private set; }
    public OwnedCapturedContentReader(string home, string control, WorkspaceRecord workspace, IJournalFilePins pins)
    { this.workspace = workspace; this.control = control; whole = new(home, pins); raw = new(home, pins); }
    public long GetLength(string hash, bool rawRange)
    { lock (gate) { ObjectDisposedException.ThrowIf(disposed, this); return scoped ? OpenCached(hash, rawRange).Length : rawRange ? raw.LengthOf(workspace, control, hash) : whole.LengthOf(workspace, control, hash); } }
    // Only adoption under the fixed capture/workspace operation owns this
    // scope. Its retained request protects references; streams fence the exact
    // canonical native bytes. Do not carry it into another operation.
    internal IDisposable BeginAdoptionScope()
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (scoped) throw new InvalidOperationException("Nested content proof scopes are unsupported.");
            ReleaseIdleReaders(); scoped = true; return new Scope(this);
        }
    }
    private sealed class Scope(OwnedCapturedContentReader owner) : IDisposable
    { public void Dispose() { lock (owner.gate) { owner.ReleaseIdleReaders(); owner.scoped = false; } } }
    private Stream OpenCached(string hash, bool rawRange)
    {
        var found = streams.First;
        while (found is not null && (found.Value.Hash != hash || found.Value.Raw != rawRange)) found = found.Next;
        if (found is null)
        {
            Stream input = rawRange ? raw.Open(workspace, control, hash) : whole.Open(workspace, control, hash);
            ProofAdmissions++;
            // The adoption operation owns at most one bounded batch of native
            // fences. Sixteen recurring hashes must not thrash an eight-entry
            // reader cache on every file. Normal reads retain the smaller LRU.
            if (streams.Count == (scoped ? 64 : 8)) { streams.First!.Value.Input.Dispose(); streams.RemoveFirst(); }
            found = streams.AddLast((hash, rawRange, input));
        }
        else { streams.Remove(found); streams.AddLast(found); }
        return found.Value.Input;
    }
    public int Read(string hash, bool rawRange, long offset, Span<byte> destination)
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            var input = OpenCached(hash, rawRange);
            if (offset < 0 || offset > input.Length) throw new WorkspaceException("CapturedContentInvalid", "The canonical source read is outside the preserved chunk.");
            input.Position = offset;
            return input.Read(destination[..(int)Math.Min(destination.Length, input.Length - offset)]);
        }
    }
    public void Dispose()
    {
        lock (gate) { if (disposed) return; disposed = true; foreach (var value in streams) value.Input.Dispose(); streams.Clear(); raw.Dispose(); }
    }
    public void ReleaseIdleReaders()
    { lock (gate) { ObjectDisposedException.ThrowIf(disposed, this); foreach (var value in streams) value.Input.Dispose(); streams.Clear(); } }
}
