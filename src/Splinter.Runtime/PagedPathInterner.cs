using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Splinter.Core;

namespace Splinter.Runtime;

// Immutable component chains separate path spelling from indexes ordered by
// fixed-size identities. A deeply nested valid Windows path may exceed a
// logical page in UTF-8; each component still fits its filesystem name limit.
public sealed class PagedPathInterner
{
    public static string EmptyPath => new('0', 64);
    private static readonly UTF8Encoding Utf8 = new(false, true);
    private readonly PagedMetadataStore store;
    public MetadataTreeRoot Root { get; private set; }
    internal long ImportBuilds { get; private set; }
    public PagedPathInterner(PagedMetadataStore store, MetadataTreeRoot? root = null)
    { this.store = store; Root = root ?? store.Empty; _ = store.Open(Root); }
    public string Intern(string path)
    {
        string identity = EmptyPath;
        foreach (var component in Components(path))
        {
            identity = component.Key; byte[]? prior = store.Open(Root).Find(identity);
            if (prior is null) Root = store.Set(Root, identity, component.Value.Span);
            else if (!prior.AsSpan().SequenceEqual(component.Value.Span)) throw Invalid("Preserve a colliding path component identity.");
        }
        return identity;
    }
    public static string Reference(string path)
    { string identity = EmptyPath; foreach (var component in Components(path)) identity = component.Key; return identity; }
    public void Import(IEnumerable<string> paths, CancellationToken ct = default)
    {
        var previous = Root;
        IEnumerable<MetadataEntry> Entries()
        {
            foreach (string path in paths)
                foreach (var entry in Components(path)) { ct.ThrowIfCancellationRequested(); yield return entry; }
        }
        using var additions = MetadataExternalSort.Order(Entries(), store.WorkingDirectory, ct).GetEnumerator();
        using var merged = new MetadataSpool(store.WorkingDirectory);
        bool more = additions.MoveNext(), changed = false;
        MetadataEntry? last = null;
        void Advance()
        {
            last = additions.Current;
            while ((more = additions.MoveNext()) && additions.Current.Key == last.Key)
            {
                ct.ThrowIfCancellationRequested();
                if (!last.Value.Span.SequenceEqual(additions.Current.Value.Span)) throw Invalid("Preserve a colliding path component identity.");
            }
        }
        if (!more) return;
        // A merge cursor validates collisions once. Unchanged imports keep the
        // old root without N separate lookups through Build(previousRoot).
        foreach (var prior in store.Open(previous).Enumerate())
        {
            ct.ThrowIfCancellationRequested();
            while (more && StringComparer.Ordinal.Compare(additions.Current.Key, prior.Key) < 0)
            { merged.Append(additions.Current); changed = true; Advance(); }
            if (more && additions.Current.Key == prior.Key)
            {
                if (!prior.Value.Span.SequenceEqual(additions.Current.Value.Span)) throw Invalid("Preserve a colliding path component identity.");
                Advance();
            }
            merged.Append(prior);
        }
        while (more) { ct.ThrowIfCancellationRequested(); merged.Append(additions.Current); changed = true; Advance(); }
        if (changed) { Root = store.Build(merged.Read(), ct, previous); ImportBuilds++; }
    }
    private static IEnumerable<MetadataEntry> Components(string path)
    {
        if (path.Length == 0) yield break;
        if (path.Length > 32767 || path.Contains('\\') || path.Contains('\0') || path.Contains(':')) throw Invalid("The path exceeds the Windows filesystem path contract.");
        string parent = EmptyPath;
        foreach (string component in path.Split('/'))
        {
            if (component.Length == 0 || component is "." or "..") throw Invalid("Only ordinary path components can be interned.");
            byte[] name = Utf8.GetBytes(component), value = new byte[checked(36 + name.Length)];
            Convert.FromHexString(parent).CopyTo(value, 0); BinaryPrimitives.WriteInt32LittleEndian(value.AsSpan(32), name.Length); name.CopyTo(value, 36);
            parent = Convert.ToHexStringLower(SHA256.HashData(value)); yield return new(parent, value);
        }
    }
    public string Resolve(string identity)
    {
        if (!Hash(identity)) throw Invalid("A path component reference is malformed.");
        var components = new Stack<string>(); int length = 0;
        while (identity != EmptyPath)
        {
            byte[] value = store.Open(Root).Find(identity) ?? throw Invalid("A referenced path component is missing.");
            if (value.Length < 37 || identity != Convert.ToHexStringLower(SHA256.HashData(value))) throw Invalid("A path component identity changed.");
            int bytes = BinaryPrimitives.ReadInt32LittleEndian(value.AsSpan(32));
            if (bytes != value.Length - 36) throw Invalid("A path component length is invalid.");
            string name;
            try { name = Utf8.GetString(value.AsSpan(36)); }
            catch (DecoderFallbackException) { throw Invalid("A path component has malformed UTF-8."); }
            if (name.Length == 0 || name is "." or ".." || name.Contains('/') || name.Contains('\\') || name.Contains('\0') || name.Contains(':'))
                throw Invalid("A component reference contains a non-ordinary name.");
            length = checked(length + name.Length + (components.Count == 0 ? 0 : 1));
            // Derived from the actual Windows path boundary, not a project
            // count or aggregate path-size quota. Also bounds a corrupt cycle.
            if (length > 32767) throw Invalid("A path component chain is cyclic or exceeds the filesystem path contract.");
            components.Push(name); identity = Convert.ToHexStringLower(value.AsSpan(0, 32));
        }
        return string.Join('/', components);
    }
    public static string LookupIdentity(string path, bool caseSensitive = false) =>
        Convert.ToHexStringLower(SHA256.HashData(Utf8.GetBytes(caseSensitive ? path : path.ToUpperInvariant())));
    private static bool Hash(string value) => value.Length == 64 && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
    private static WorkspaceException Invalid(string message) => new("PathIndexInvalid", message);
}
