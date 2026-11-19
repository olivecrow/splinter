# Git project basis

`git-snapshot` fixes a clean supported local Git project to a commit and tree. The task keeps its own Git registration and a protected basis ref. The original stays editable. Admission checks repository ownership, HEAD, index, worktree and Git interpretation. Ignored and private source files are outside the commit. For a warm Unity project, attach a separately [prepared Library](git-library-reference.md).

```powershell
pwsh ./scripts/splinter.ps1 schema workspace.ensure
pwsh ./scripts/splinter.ps1 workspace ensure --project <git-root> --key <task-key> --source git --application none --storage git-snapshot --base <commit> --home <home>
```

Use the returned `data.workspace.path`. Keep the fixed commit, task key, ID, home and request digest for resumption. Unsupported repositories, missing pinned objects and unverifiable management state require inspection. Never switch to the mutable original as the source of missing bytes.

## Reads and changes

Git supplies commit, tree and blob identity. The reader verifies complete objects and binds chunk proofs to their object identity. Scoped readers pin owned refs and receipts while checking the needed observations. Packed-object readers and in-memory range caches have bounded lifetimes. Mounting a workspace creates the source journal. Reading a file does not create a persistent copy of it. [Storage](scalable-storage.md) · [Source verification](source-verification.md)

## Results and original publication

Keep the result ID from `workspace checkpoint` with a stable request key. The result and its protected basis survive task release. Original publication requires a reviewed selection, intent and plan digest and checks current contents and applied history. The original keeps its branch, HEAD and index. Git branch materialization or integration uses a separate explicit operation. Release verifies owned registrations and refs, fences and independent results before reclaiming task-private storage. [Publication guide](repeated-original-apply.md)
