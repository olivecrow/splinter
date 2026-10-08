namespace Splinter.Runtime;

// Optional platform proof for a single current-state object directory. The
// implementation must pin every ancestor and the directory itself against
// replacement and reparse conversion until disposal. A remembered path or
// timestamp is not sufficient. Runtime owns this lease while its writer and
// in-flight reads are alive, or for one complete seal/inventory observation.
// It does not prove file content or replace final head/membership checks.
// Callers without this proof retain full walks.
public interface IJournalDirectoryGuard : IDisposable
{
    string DirectoryPath { get; }
    void EnsureFile(string path);
}
