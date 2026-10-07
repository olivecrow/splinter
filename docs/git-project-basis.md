<a id="git-프로젝트-기준본"></a>
# Git project basis

[한국어](ko/git-project-basis.md)

`--storage git-snapshot --source git` uses the tree/blobs of a recorded project commit as its basis. The original stays at its existing NTFS location and can be edited or advanced to another branch state while work proceeds. Creation, reads, resumption, compaction, and reclamation invoke neither VSS nor the storage-privilege service. Existing `ntfs-snapshot` uses VSS and is not automatically converted.

```powershell
pwsh ./scripts/splinter.ps1 schema workspace.ensure
pwsh ./scripts/splinter.ps1 workspace ensure --project <Git-root> --key <task-key> --source git --application none --base development --storage git-snapshot --home <NTFS-home>
```

Edit at returned `data.workspace.path` and retain project/key/id/home, storage, and base/digest when resuming the same request. Initial input requires a repository root whose clean checkout HEAD matches the requested commit. `.git` must be an ordinary directory inside the original; only local objects and regular files are used. Dirty/staged/untracked content, skip-worktree/assume-unchanged, submodules/symlinks, LFS/filters/working-tree-encoding, partial clones/shallow repositories/alternates are rejected. Because raw blobs are exposed, only false or input is allowed for `core.autocrlf`; checkout transformations are not run. This mode does not capture the entire current folder at a point in time.

<a id="소유권과-저장"></a>
## Ownership and storage

Git commit/tree/blob stream verification applies a 30-second no-progress limit to each blocking read, rather than a 30-second whole-operation limit. Verification continues beyond 30 seconds while partial reads keep completing; hashing/idle time is not timed. On no progress, terminate only that stream's exact owned reader and preserve basis/journal with `GitBasisReadTimeout`. Failure retains request-buffer nonpublication, proof disposal, and fixed-basis protection; writes are not automatically repeated. Existing metadata-command limits and the 8GiB blob/proof limit remain.

`GitObjectSnapshotProvider` fixes the exact commit/tree and owner in `git-basis.json` and compare-and-creates `refs/splinter/git-bases/<id>`. Effect/response loss after intent publication is reconciled with the same request. A missing/moved completed ref, symbolic ref, or damaged receipt is preserved and rejected, without creating a new basis. After initial basis selection, mutable source/index/branch are not reread as the basis.

`GitObjectSnapshotTree` reads fixed-tree metadata and raw blobs through `git cat-file`. It verifies commit/root-tree object hashes and hashes of all subtrees read in one batch stream. The first blob read verifies the entire object hash and builds SHA-256 proofs in 64KiB units before returning only the requested range. Later reads publish only proof-matching chunks into requested-range staging. A tree-local LRU retains at most 4MiB of SHA-256 chunk proofs across 4,096 blobs, keyed by exact object ID and fixed length. The in-flight proof shares that byte budget: eviction happens before allocation, and a new proof is published only after complete blob/hash/framing verification. Blob switches invalidate the single payload chunk and partial decoder while retaining metadata proofs. Errors and close discard all proofs. Sequential reads continue the decoder; backward reads reopen the stream. It still retains only one verified 64KiB payload chunk and up to 1MiB of requested-range staging; the blob limit is 8GiB. See [bounded optimization verification](verification/lifecycle-read-optimization.md). Payload is neither cached on disk nor checked out. It guarantees requested bytes from verified chunks match fixed content; it does not audit current corruption of the whole blob on every request. Git's internal packed-object memory, process costs, and nonsequential decoding are not bounded by this managed-buffer limit. Empty/EOF requests return 0 bytes. Windows path-case collisions, ADS/reserved names, and special entries are rejected. Mutable files outside Git are not read as fallback.

When abandoning a partial read to switch blobs or close, first close the exact owned stdout pipe to cancel write waits for the unread suffix. Then close stdin, retaining the existing 1-second exit limit, owned-child termination, and stderr observation. Requested bytes are returned only after full-blob or chunk-proof verification; a canceled suffix is not treated as a successful read. This timing optimization belongs to the [separate many-file comparison](plans/journal-base-reversion-optimization.md) and does not inherit earlier actual Unity/WinFsp qualification.

The mode uses WinFsp and the existing overlay v2/SJR2 range journal. Only private changed ranges are stored, without initial basis-payload copying, read hydration, or whole-file copy-up. Git linked worktrees use the existing no-checkout registration/index contract; the `.git` pointer is management data written into the private journal. Original index/branch and peer registrations are unchanged. The basis reachability ref is separate from the result branch.

<a id="결과와-회수"></a>
## Results and reclamation

Existing checkpoint, source guards, before/after/unmount seals, independent results, and integration target/dirty guards remain. Release verifies the exact independent result and compare-and-deletes the basis ref only after owned application/provider/handles/mount/registration/delta are absent. An open reader fence or changed ref blocks deletion. Explicit discard also follows the same identity/intent and zero-resource boundaries. If a reclamation response is lost, continue only the same release/discard intent. Original/peer/independent-result refs and shared objects are preserved; Git GC is not run.

<a id="입력과-검증-한계"></a>
## Inputs and verification limitations

Ignored/private files and Unity Library are excluded from the commit basis. Generated data is created as new private data inside the workspace; Library private writes use the same range journal/compression. Reuse the original Library through `library prepare` and `workspace ensure --library-basis <id>` under the [explicit separate basis](git-library-reference.md). Original Library conversion/restoration requires normal Editor shutdown and user authorization. [Git Unity verification](verification/git-unity-validation.md) owns bounded real Unity/HTTP MCP, resource/scene/current-state test execution and cold costs. [Separate verification](verification/git-library-reference.md) evaluates actual warm-path preparation, MCP, and RAM costs. Plastic and VCS-free projects use separate reader/lifetime contracts in the [Plastic basis](plastic-project-basis.md) and [managed project basis](plain-project-basis.md), without inheriting Git qualification. This path does not reclaim snapshots of other VSS tasks or use development VSS views/caches as supported Git-basis paths.

The current contract connects `workspace.ensure`, `overlay-serve-request`, `git-project-basis-receipt`, response schemas, Core snapshot ports and `WorkspaceStorage.HasRangeJournal`, and Windows `WorkspaceSnapshotBasis` composition. The runner adds the Git DLL and uses a content key for the current 8 files, retaining earlier bundles. [Verification](verification/git-project-basis.md) distinguishes automated/fake boundaries from real Editor-free A/B.
