namespace Splinter.Runtime;

// Optional native proof: every reused ancestor must remain ordinary, pinned
// against rename and nonempty/reparse conversion until disposal. Inspect the
// current leaf on every call; return false outside the proved parent set.
// This is not a content, ownership, or mutable namespace membership proof.
public interface IRequestPathGuard : IDisposable
{
    bool TryEnsurePlainPath(string path);
}
