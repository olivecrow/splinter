# Glossary

| Term | Meaning in Splinter |
| --- | --- |
| Original | The user's project at its existing path. Publishing to it requires an explicit result and conflict check. |
| Task or workspace | An independently writable project view with a stable ID, task key, state home and assigned path. |
| Basis | The fixed source a workspace reads beneath its changes. This can be a Git commit, Plastic changeset or managed NTFS directory. |
| Library basis | A separately prepared Unity cache that Git or Plastic workspaces can share for unchanged reads. |
| Delta | Task-private data describing writes, lengths, metadata, hidden names and renames over a basis. |
| Extent | A byte range and its backing location. Uncovered ranges can read from the immutable basis. |
| Seal | An identity for a fixed storage state. Operations also check the required backing files, inventories and ownership. |
| Checkpoint or result generation | Immutable source output identified by its result ID. An intermediate checkpoint allows the task to continue. |
| Plan digest | Identity of the reviewed target, changes, selection and conflicts used to authorize a particular publication. |
| Request or intent key | Caller-chosen stable identity for an operation and its retries. Keep the same input when resuming. |
| Release | Preserve the task's independent result and reclaim its owned runtime resources. |
| Discard | Explicitly reclaim the selected task, including unpreserved changes. Requires its intent and matching request digest. |
| Restore | Return a managed original project or Library to native NTFS after dependents and readers close. |
| View and candidate | Development workflow objects for selecting a fixed source generation and running it with a separately owned cache. |

[Start a task](agent-start.md) · [Storage](scalable-storage.md) · [Original publication](repeated-original-apply.md)
