using Splinter.Core;

namespace Splinter.Runtime;

// Only sealed capture segments use this cache. Each retained handle excludes
// writes/deletion; active leases survive eviction and Dispose drains readers.
internal sealed class CaptureBackingReaders(string directory, IJournalFilePins pins) : IDisposable
{
    private const int Capacity = 8;
    private readonly object gate = new();
    private readonly LinkedList<Entry> idle = new();
    private readonly Dictionary<CapturedBacking, Entry> entries = new();
    private int active;
    private bool closing;
    internal sealed class Entry(CapturedBacking backing, FileStream stream)
    {
        internal readonly CapturedBacking Backing = backing;
        internal readonly FileStream Stream = stream;
        internal int Users;
        internal LinkedListNode<Entry>? Idle;
    }
    internal sealed class Lease(CaptureBackingReaders owner, Entry entry, bool cached) : IDisposable
    {
        private CaptureBackingReaders? lifetime = owner;
        internal FileStream Stream => entry.Stream;
        public void Dispose() => Interlocked.Exchange(ref lifetime, null)?.Release(entry, cached);
    }
    internal Lease Acquire(CapturedBacking backing)
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(closing, this);
            if (!entries.TryGetValue(backing, out var entry))
            {
                if (entries.Count == Capacity && idle.First is { } oldest)
                {
                    idle.RemoveFirst(); entries.Remove(oldest.Value.Backing); oldest.Value.Stream.Dispose();
                }
                string path = Path.Combine(directory, backing.Name); WorkspaceStore.EnsurePlainPath(path);
                var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1, FileOptions.RandomAccess);
                try
                {
                    pins.Verify(input, backing.Identity);
                    if (input.Length != backing.MinimumLength)
                        throw new WorkspaceException("SourceCaptureInvalid", "A sealed capture backing changed length.");
                    entry = new(backing, input);
                }
                catch { input.Dispose(); throw; }
                if (entries.Count == Capacity)
                { active++; entry.Users++; return new(this, entry, false); }
                entries.Add(backing, entry);
            }
            if (entry.Idle is { } node) { idle.Remove(node); entry.Idle = null; }
            active++; entry.Users++; return new(this, entry, true);
        }
    }
    private void Release(Entry entry, bool cached)
    {
        lock (gate)
        {
            entry.Users--; active--;
            if (!cached) entry.Stream.Dispose();
            else if (entry.Users == 0) entry.Idle = idle.AddLast(entry);
            Monitor.PulseAll(gate);
        }
    }
    public void Dispose()
    {
        lock (gate)
        {
            closing = true;
            while (active != 0) Monitor.Wait(gate);
            foreach (var entry in entries.Values) entry.Stream.Dispose();
            entries.Clear(); idle.Clear();
        }
    }
}
