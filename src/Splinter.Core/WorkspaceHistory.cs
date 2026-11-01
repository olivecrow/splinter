using System.Collections;
using System.Text.Json.Serialization;

namespace Splinter.Core;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record WorkspaceHistoryReference([property: JsonRequired] int Version,
    [property: JsonRequired] long Count, [property: JsonRequired, JsonIgnore(Condition = JsonIgnoreCondition.Never)] MetadataTreeRoot? Root);

// Persistence is supplied by the composition. A newly constructed record has
// an empty transient history; persisted records bind their immutable disk root.
public interface IWorkspaceHistoryStorage<T>
{
    long Count { get; }
    T Read(long index);
    T? Find(string key);
    void Append(T item);
    IEnumerable<T> Enumerate(long start = 0);
}

public sealed class WorkspaceHistory<T> : IEnumerable<T>
{
    private readonly List<T> initial = [];
    private IWorkspaceHistoryStorage<T>? storage;
    public long Count => storage?.Count ?? initial.Count;
    public T this[long index] => storage is null ? initial[checked((int)index)] : storage.Read(index);
    public T Last() => Count > 0 ? this[Count - 1] : throw new InvalidOperationException("The history is empty.");
    public T? LastOrDefault() => Count == 0 ? default : this[Count - 1];
    public void Add(T value) { if (storage is null) initial.Add(value); else storage.Append(value); }
    public T? Find(string key, Func<T, string> identity) => storage is null
        ? initial.SingleOrDefault(value => identity(value) == key) : storage.Find(key);
    public IEnumerable<T> Enumerate(long start = 0) => storage?.Enumerate(start) ?? initial.Skip(checked((int)start));
    public void Bind(IWorkspaceHistoryStorage<T> backend)
    {
        if (storage is not null) return;
        if (backend.Count != 0 && initial.Count != 0)
            throw new WorkspaceException("StateCorrupt", "Preserve conflicting memory and disk histories.");
        storage = backend;
        foreach (T item in initial) backend.Append(item);
        initial.Clear();
    }
    public IEnumerator<T> GetEnumerator() => Enumerate().GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
