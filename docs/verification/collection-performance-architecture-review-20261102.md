# Collection performance and storage architecture review — 2026-11-02

Structural implementation follow-up: [ST-65–68](scalable-storage-throughput-followup.md#st-6568-shared-catalog-proofs-persistent-retention-and-independent-composition-proofs) adds native-fenced catalog summaries/totals, persistent retained additions, proven physical-history coverage and independently revalidated composition proofs. The 26 selected small checks qualify only those boundaries; the initial inventory, first hashes, fallback paths and large-input questions remain.

Implementation follow-up: ST-61–64 in [the owner verification](scalable-storage-throughput-followup.md) implements bounded backing/slice proof reuse and removes redundant retained-root sorting/accounting sort for small inputs. Remaining candidates are explicitly listed there; the analysis below preserves its observation time.

[한국어](../ko/verification/collection-performance-architecture-review-20261102.md)

## Astra re-review — superseding assessment

This section independently rechecks current code and preserves the earlier analysis below. Its updated status supersedes the initial 01:59 “in progress/unverified” wording. The executing model is GPT-6 Astra; the reasoning-effort setting was not independently observable through a tool. The goal was actually reactivated with `create_goal`. No product/performance run was made. Main now reports a successful small lifecycle, capture 16.50→12.77 s and whole workflow 85.08→69.45 s, one run per side. This review did not independently audit its measurement files. An initially observed failed command does not negate the later success report. Shared owner documentation is still being edited; implementation observation and reported validation remain distinct.

### Corrections and precision

- **Alias cleanup moved; not all GC.** `FileSourceGenerations.cs:129` no longer calls pruning during capture. `SnapshotWorkspaces.cs:203–205` prunes on release and forces metadata maintenance. However, `WorkspaceGenerations.cs:100` → `WorkspaceMetadataMaintenance.cs:22` → `FileMetadataMaintenance.cs:51–67` still performs conditional maintenance before returning capture. Any workspace/shared/content/ranges store with pending collection or at least 4 MiB appended metadata can trigger scope `all`. A tiny input below this threshold proves no absence of large-input maintenance. Preserve same-request recovery for pending intents; evaluate deferral only for new collections.
- **Accounting still walks history.** `FileSourceGenerations.cs:124–129` opens historical manifests. `FileWorkspaceProvider.cs:455–477` enumerates their `ContentReferences`, sorts/deduplicates, and separately sums all basis/result entry lengths. Pruning removal does not remove this work. Caller omission of ct and the sort's default token also deserve cancellation-latency review. Root-bound aggregate reuse is a candidate; naive addition double-counts shared chunks.
- **Retention is cumulative, not just the latest delta.** `FileReferenceResults.cs:16–48` unions the previous retained set with current before/after/dependency payloads. `RequiredBasisPayloads:98–104` and `VerificationPayloads` process that accumulated set. If each generation adds d unique payloads, H(g)=O(gd); accounting that enumerates historical sets can take O(dg²) input rows. This is conditional arithmetic, not measured behavior of every workload. A latest logical hash set does not automatically cover every old descriptor's physical raw/whole backing, so deleting historical traversal needs a separate closure proof.
- **64-entry publication is not capture-wide rollback.** `ResultFileContentStore.PreservationBatch:51–77` preserves published prefixes and never publishes pending roots on Dispose. A failure on entry 65 must preserve the first 64 and resume the same request; the capture receipt follows completion of all required batches and validation. The earlier wording about batch ACK does not imply discarding prior batches. Single-iterator private drain already exists at `FileSourceGenerations.cs:392–404`; it is not a new optimization proposal.

### More concrete residual costs

| ID/priority | Code, multiplier and assessment | Protection and smallest next check |
|---|---|---|
| A1 / high | `JournalPagedCapture.cs:187–250`: each 64 KiB read repeats availability/resolve/node/floor; each segment-backed extent opens, verifies native identity/length and closes backing. Sequential reads of one large extent can therefore open roughly ceil(B/64KiB) handles. A sequential extent cursor and bounded handle cache are candidates. | Bind cache to exact capture root/native identity/reader lifetime, preserve close drain/GC fence/replacement detection. Count opens/reads/pages on a small 2 MiB sequential file and multiple extents, with rename/deletion/replacement. Counts are arithmetic, not measurement. |
| A2 / high | `OwnedRangeContentStore.cs:49–114,214–231`: `VerifySource/HashRange` rereads a range hashed during raw preparation. Whole backing hashes use an 8-entry proof LRU, but slice hashes repeat per request, before `AlreadyReferenced`. Existing raw objects may be rehashed by `OpenLocked` under the shared catalog lock. These are different trust boundaries. | Compare handle/offset/length/hash-bound proof transfer against current rereads; test source change, LRU eviction, existing object corruption and lost response. Whole backing hash does not establish a requested slice hash automatically. |
| A3 / high | Accounting/cumulative H and separate whole-N raw/descriptor walks remain. `ScanTree` already reuses unchanged hashes; saying every file is always content-hashed is wrong. | Count N/R/H/read bytes/sort rows with zero/one/multiple-generation changes. Incremental catalog updates must cover descendant renames, deletes, metadata-only and same-byte overwrites. |
| A4 / medium | `PagedMetadataCompactProof.cs:32,41,65–71` performs logical traversal, another reference traversal/sort, and explicit physical reread/hash. Cache can reduce some reads; ST-53 eliminates copying, not all proof cost. | Evaluate a flushed builder page set combined with full graph/native proof under one fence. Preserve unreachable tails, child/unknown/pending-collection and corruption detection; writer assertions alone are insufficient. |
| A5 / medium | `NtfsSourceGenerations.cs:68–113` live completion polls complete-source IPC every 100 ms and verifies provider every ten attempts. This differs from ST-55 source-change polling. Stopped mode proves owned absence, directly adopts, completes capture, then calls `MaintainLiveStorage(collectSegments:true)`. | Measure IPC/adoption/live maintenance separately. Preserve conditional later-write replacement, final flush, idle-reader release and same-request live-repack/segment recovery. Event wake still needs lost-wake request lookup. |
| A6 / low | `FileRawRangePreparation.cs:58–74` allocates 1 MiB arrays and round-trips JSON rows; `JournalPagedCapture.cs:229` allocates/decodes whole encoded blocks. Distinguish new raw writes from retained encoded data. | Measure allocation/GC first; pooling must preserve lifetime/concurrency/exception return. Reduced allocation does not prove reduced disk time. |

### Resources, concurrency and representation

Raw source verification occurs before the shared catalog lock, but the same instance's `proofGate` serializes preservation batches. More workers alone cannot remove that bottleneck. Compare bounded parallel independent reads/proofs feeding an ordered publisher. Fixed directory depth is only initial distribution; skew requires dynamic work stealing, byte-bounded tasks and deterministic merge. Backends without concurrent-read permission retain serial fallback. Pipelining must stop new work and drain failures, with flush before ACK.

Permission to use about 4 GiB is headroom, not a need to fill it. The current target is one-eighth of available memory and may be smaller. `MetadataMembership.cs:17,27–46` is bounded by both shared scratch and SortRunBytes, at most 128 MiB. For a 65-character raw/whole hash key and 8-byte value, its accounting charges 650 bytes per entry, allowing roughly 206k entries at the maximum run budget before spill. This is its accounting model, not measured managed memory or an invariant spill threshold. It may fall back despite total process headroom; rebalance only after observing cache hits, peak/GC and concurrent spills. No upfront large allocation is needed.

A common immutable root model must also examine the full-file SHA-256 identity contract. After a partial edit, ordinary SHA-256 generally cannot be reconstructed from the previous digest and changed-range digest; full-file reads can remain. A Merkle extent/chunk root with whole-file hash only at required API/publication boundaries would change identity, canonical chunking, equivalent-content and recovery contracts. “Pin the persistent root” alone does not remove all work. Reusing an already calculated full hash and immutable reader proof within the same capture is the smaller first candidate.

Updated priority is repeated byte proofs/handle and metadata lookup (A1/A2), cumulative accounting/full scans (A3), conditional maintenance/proofs (A4/A5), then temporary serialization/allocation (A6). This is a static cost-path priority, not a measured exclusive-time ranking. All five question groups are answered by the original checklist plus these corrections; that is not proof every function is micro-optimized. Actual utilization and exact run-specific syscall/flush decomposition remain unmeasured. Large execution is not needed to establish these code boundaries.


## Scope and conclusion

At 01:59 KST on 2026-11-02, this review read HEAD `1b33995955f4e7b77b1ce416c8a99be7e6e80116` and the changing uncommitted tree. Main chat `01a19644-3877-7a20-a47c-65d095af8880` was checked read-only at the start and before completion. Its latest message reports 14 of 15 focused checks passing, a corrected test handle lifetime, and a 32-item before/after lifecycle in progress; A later command was marked failed at the final snapshot; its contents were not inspected. This is not a final pass. Line numbers below refer to that moving tree and may drift. At the final snapshot `contracts/v1/file-source-manifest.schema.json` was also modified by the main work. No product run, large fixture, build, or test was performed for this review.

The implementation is **not demonstrated to be fully optimized**. Reducing redundant traversal, bytes and proofs deserves measurement before a general page-format rewrite. No candidate's end-to-end saving is established. A snapshot of final workspace state is distinct from overwriting the original: preserving concurrent original and peer edits requires basis/result/current conflict decisions. Git is also a snapshot model; recording every edit is not the only option.

The main chat is implementing narrower retained payloads, publication in groups of 64, moving reference cleanup out of capture response, and avoiding basis reads fully covered by private ranges. Treat them as **in progress, not verified complete**. ST-53–55 compact proof, membership and polling changes are present in code and the existing report. The residual candidates below do not claim those as new discoveries. See [follow-up](scalable-storage-throughput-followup.md), [diagnosis](scalable-storage-throughput-diagnosis.md), and [measurements](../../reports/benchmarks/capture-maintenance-20261102.json).

## Call graph and work

Let N be files and directories, D directories, R private ranges, B bytes read, H retained-result references and P metadata pages. These are code-level scaling relationships, not observed time estimates.

| Stage and location | Repetition and boundary | Status |
|---|---|---|
| `WorkspaceGenerations.cs:90` → `FileSourceGenerations.cs:25–121` | Fixed request/retry → basis/result → preserve → independent verification → boundary → result/receipt publication → verification. Retry checks the same identity. | Observed |
| `FileSourceGenerations.cs:47,69–84,158–205`; `JournalPagedCapture.cs:115–147` | First basis scan is complete; later fixed basis reused. Result scan is a single DFS over N, with directory sort/merge and per-file layout/basis lookup. Unchanged frozen layouts or verified summaries reuse hash; otherwise content is read and hashed. Changed-path index exists but does not replace this scan. | Residual; actual syscalls/bytes unknown |
| `FileRawRangePreparation.cs:37–115` | Walks result entries, splits private ranges at up to 1 MiB, reuses full-file hash only for an exact whole-file range; otherwise reads/hashes. Sorts by physical backing identity/hash/offset, serializes JSON scratch rows, batches existing-reference and raw preservation, then sorts/builds a path/offset index. Backing grouping already exists. | R, B and sort/spill costs; not measured |
| `FileSourceGenerations.cs:299–407`; `ResultFileContentStore.cs:72–105,153–184,211` | Enumerates result files again, validates ranges, spools adoption rows and constructs descriptors. Existing and new descriptors are composed and file-hash verified. Up to 64 changed roots per flush/publication is in-progress main work. Adoption separately sorts/builds/flushes. | Reconstruction bytes, pages and durable flushes remain |
| `CapturedRangeAdoptionStore.cs:21–35`; `PagedMetadataCompactProof.cs:12–67` | After build/flush and durable artifact publication, verifies all root/native ownership and exact physical coverage. Only compact flat trees avoid copying; pending collection/unknowns fall back or are preserved/rejected. Proof itself costs P and sorting. | ST-53 confirmed; not automatically dispensable |
| `FileSourceGenerations.cs:87–121,140–157`; `FileWorkspaceProvider.cs:479` | Independent reader verifies selected payloads before publication; `VerifyGenerationCoreAsync` verifies them again after result/receipt publication. Each can read selected bytes; actual duplicate bytes/cache hits unknown. Git has a distinct Git verification path. | Candidate; failure and retry boundaries differ |
| `JournalRangeAdoption.cs:10–81,106–122` | Each row verifies fixed block/length, reads frozen and preserved bytes in 64 KiB pieces and compares; batches conditional live replacements under gate, then flushes/root/segment and releases idle readers. Comparison loop is serial; nonconcurrent basis has a gate. | Later writes protected; redundant comparison is a hypothesis |
| `FileSourceGenerations.cs:426–482`; `MetadataMembership.cs:17–56` | Cleanup drains live/pending/basis/current and historical references to build membership, then examines aliases; budget overflow falls back to sorted disk tree. Recheck capture versus explicit maintenance after main finishes. | ST-54 present; critical-path move in progress |

## Detailed findings and safe boundaries

1. **Traversal, detection and retention.** Repeating 100k iterations alone does not explain tens of minutes. The code adds tree lookups, per-directory sorts, conditional reads/hashes, root construction and durable publication. A fixed-depth partition can be badly imbalanced by a large directory or file. A dynamically fed directory/large-file queue plus deterministic path-policy merge is a candidate. `ScanTree` is serial DFS; the dynamic partitioner in `BoundedParallel.cs:37` does not automatically parallelize it. Measure directory sizes, read bytes, queue depth and wait first. A change index can replace full inventory only with proof of complete fixed-sequence coverage, both sides of rename, deletion, parent metadata, restart tails and external bypass writes. Gaps/unknowns require a full observation or rejection. Dirty, size and mtime alone miss same-size edits, restored times and outside writes. Namespace and content need the same fixed fence. Identical immutable directory roots may skip subtrees; `FileReferenceResults.cs:51` uses that idea for comparison, not proof that capture skips its full scan. Pin dependencies needed to read/apply/undo after workspace release. Main's v4 retention selection including rename and partial-write dependencies is still being verified.

2. **Raw and descriptors.** Physical grouping and batches already exist. Per-range reads/hashes, JSON rows, two sorts, scratch tree and descriptor reconstruction remain. A verified immutable backing/range proof tied to offset, length, hash and reader lease may avoid repeated reads. Coalesced adjacent I/O and bounded parallel independent backings need measurements of storage, locks and memory. Basis identity/length/visible length plus private chunk offsets express a reconstructed file without a full copy, but many tiny ranges increase keys/pages/hashes/spools. Merge adjacent ranges only where backing and verification boundaries agree; binary temporary rows are a candidate, with effect unknown until byte-exact and recovery checks. Even when a descriptor is reused, its input iterator must be drained to catch a bad last range and preserve/adopt all rows. Avoiding descriptor construction while streaming validation/adoption once is a candidate, not established behavior.

3. **Publication and independent verification.** Equal logical root hashes do not alone prove that an independent reader can open intact, owned native backing. Under the same immutable root, reader lease, native identity and ownership fence, reuse of verified immutable page/payload proofs is a candidate. At publication, recheck request/root binding, durable data-before-root, actual receipt publication, mutable availability/ownership and external change. Do not blindly merge the whole pre- and post-publication checks. A sealed proof from the independent pre-reader followed by published root/receipt and fence-continuity checks could remove repeated content hashing if those premises are proved. Parallel subtree/range verification must bound concurrency, drain all failures and combine deterministically; shared reader/lock concurrency needs proof. Main's 64-file publication requires all-or-nothing batch ACK/retry checks.

4. **References and aliases.** Membership makes lookup cheaper but still enumerates references. Per-workspace immutable reference roots, generation indices, incremental aggregates or root diffs may avoid repeated full enumeration. Before deletion, exact live/pending/historical membership and conflicting lengths, pinned roots and crash-safe updates remain necessary. Parallel enumeration with local sets and merge is possible but shared locks/merge may dominate. Moving GC/repack outside response reduces latency without necessarily reducing total work, and adds retained space and maintenance delay. Resume an already started collection under the same request first. Verify results and peer references survive without immediate GC and observe backlog.

5. **Backend and transition.** Per-row frozen-versus-preserved comparison defends against corruption and wrong references. If both sides already carry a proven immutable backing/slice identity, repeated byte comparison may be avoidable, but a matching hash alone is insufficient: bind native identity, offset, length, hash, fence and original proof. A bounded prepare/verify pipeline and independent backing batches must preserve publication ordering and drain failures. Live writes can occur during comparison, so only matching block token/offset slices may be replaced under the gate. Stopped processing can consider a simpler boundary only after exact owned stop/no-writer proof. `WindowsSourceChanges.cs:51–88` polls at 20 ms for the first 100 ms, then 100 ms, with one-second long-wait provider proofs. Event wake is a candidate requiring lost-response/process-identity tests; polling dominance is not measured. Current Git subprocess counts, Plastic serial revision costs and backend lock waits remain unmeasured.

## Architecture and priority

| Priority | Candidate and minimal validation | Relation and unknowns |
|---|---|---|
| 1 | Share fixed-reader pre/post content proof; small corruption, replacement, receipt failure and same-request retry tests; count reads/hash bytes. | Re-evaluate after main changes; independent availability after release is mandatory. |
| 2 | Exact root diff/change journal to reduce scans; small rename/delete/restart/gap/external write, same size/time and concurrent original/peer cases with fallback. | Existing changed index and retention selection are groundwork, not full replacement. |
| 3 | Count raw sorts/JSON/repeated reads and descriptor fragmentation independently; small multi-backing, overlap, bad last row and interrupted retry byte-exact checks. | Do not repeat existing backing batching or drain. |
| 4 | Dynamic directory/large-file queue and parallel independent verification; skewed input, 1/2/4 workers, nonconcurrent fallback, peak memory, I/O queue, CPU and wait. | Worker count is not utilization evidence. |
| 5 | Per-workspace reference root/diff and separate maintenance; two results, later live writes, interrupted same-request collection and peer references; measure response and total lifecycle I/O/space. | Separate main's latency move from unproven total-work reduction. |
| 6 | Compare a common immutable chunk/sealed-segment plus persistent file-state root, publishing durable data then pinning root and dependencies. | Range references, immutable roots and shared backing already exist. Missing proof is safe pinning of a live root as an independent result integrated with ownership, GC, recovery and original comparison; no format change is proposed here. |

A common root could remove portions of scan → conversion → descriptor → adoption map → live reference replacement, but must preserve availability after reclamation, copy-on-write later writes, original/peer protection, basis/result/current comparison, same-request retry and data-before-root durability. Complete independent payload copies are an implementation choice, not an intrinsic product goal. Keeping changed/previous content and rename/partial-write dependencies with immutable references elsewhere requires proof of reference lifetimes, original changes and final restore. Main is implementing part of this direction; this review does not claim completion.

A directory copy approximately performs N namespace operations and B bytes read/written. Depending on path, Splinter adds another N scan, selected B hashing/preservation and two independent verifications, two-sided R range reads, metadata sort/pages/durable flush and reference inventories. These do not all execute on every path. As arithmetic only, 1 ms of extra serial work per 100k files is 100 seconds; it is neither an observed measurement nor a current prediction. Required protection does not justify accepting tens of minutes as inevitable.

ST-52's simple canonical byte-greedy model showed both bulk savings and sparse-update amplification; it does not disprove every B+ design. Eliminate avoidable whole scans, duplicated bytes and proofs first, then measure remaining metadata before comparing packed-page bulk/sparse writes, reads, flushes and recovery. Do not automatically restart format redesign or large tests.

## Resources and evidence limits

`StorageResources.cs:7–18` sets a demand-based target up to 4 GiB, metadata up to 2.5 GiB, shared scratch up to 512 MiB and at most 32 workers. This is neither a 4 GiB upfront allocation nor an OS RSS or multi-process aggregate cap. Dynamic partitioning applies only where invoked; serial DFS, raw sort/iterator, adoption comparison, root publication and shared locks remain. CPU saturation, disk bandwidth/random I/O, lock waits, peak RSS and memory efficiency were not measured. Proposed counters: stage opens/reads/bytes/hash, sort spills, page reads/writes/cache hits, flush counts/latency, subprocess/lock/queue waits, worker utilization, per-process RSS and scratch peak. Distinguish nested timings and duplicate bytes.

The existing ST-53 small 31-file/992-byte, six-generation baseline subphase probe records adoption build/flush 0.031 s, immediate collection 1.413 s, live reference observation 2.335 s and reference index 0.524 s. These can overlap and must not be added. ST-41–51 small ABBAs do not establish current large-input time or improvement. Historical 100k/17 GiB tens-of-minutes-to-hours records mix recovery, concurrent load and binaries and are neither a normal baseline nor a basis for extrapolation. Large inputs, real Git/Plastic/Unity, the full gate and hardware saturation remain unverified. This review used read-only source and existing evidence only.

## Coverage checklist

- [x] Inventory, opens/reads/hashing/tree/durability, external processes and waits, dynamic partitioning, dirty/fence/rename/delete/restart/external writes and retention dependencies: call table and item 1. Backend-specific actual syscall counts/times unknown.
- [x] Raw range validation, backing grouping, parallelism, descriptor, redundant rows/spools/flush and drain: call table and item 2. Duplicate bytes unmeasured.
- [x] Adoption/pages/reclamation, pre/post independent verification, proof sharing, parallelism and batched publication: call table and item 3. New boundary is a candidate.
- [x] Historical/shared references, membership/disk fallback, locking, GC separation versus total work, same-request recovery: item 4.
- [x] Backend comparison/reads/hashing, basis reads, polling/locks, pipeline, live/stopped, later writes/readers/ACK: call table and item 5. Backend costs unmeasured.
- [x] Snapshot versus original conflict, common immutable root, existing references and missing contracts, comparison with copying, B+ versus removing work: architecture and priority.

## Re-review source observation

2026-11-02T02:15:31.019413+09:00; HEAD `1b33995955f4e7b77b1ce416c8a99be7e6e80116`.

```text
524bbe99b15ce7b33678d7920bfcc0415052fa288ed2b221d2c1599ca0109a41  src/Splinter.SourceControl.Files/FileSourceGenerations.cs
dec5cf2a1aed569eda3bf570cb9d76f06d656848e594f2db01729daf5ba5da29  src/Splinter.SourceControl.Files/FileReferenceResults.cs
7e8f06394175af6f9a6cc59e16179ae9eb99704b2cce456c5a89e25f33839f73  src/Splinter.SourceControl.Files/FileWorkspaceProvider.cs
02e01c81b5bdd094703d98a0f154581d82e9b604739b1216086113fa1476ae77  src/Splinter.SourceControl.Files/FileRawRangePreparation.cs
c1e52f7c38d58ec10ed3b596facf3ac430e37a621bc1e78ce768066ae7681fed  src/Splinter.SourceControl.Files/FileMetadataMaintenance.cs
725437a3d1d2623a7e2eb944cd6fbe5615ff5e9b2fa0951ec3c3e9eab933a6a6  src/Splinter.Runtime/ResultFileContentStore.cs
f6f688a7ed271caf72f6b6b25f5ff75f36b0feb1a199df188175f79af2451ca5  src/Splinter.Runtime/JournalPagedCapture.cs
d51cf3d6036be6efe9c8010010360ccb896d95f79739a7ba0b6dd8e7249e18c8  src/Splinter.Runtime/JournalRangeAdoption.cs
48d24cd46ac381635797493b37c23c7804daa99ca0328e74fd58b9926b1f0ce4  src/Splinter.Runtime/OwnedRangeContentStore.cs
33c3b0809d90eeeca9a1c4cb4d9a52e10565104a04bca3bd2e14ce153a0d68e8  src/Splinter.Runtime/WorkspaceGenerations.cs
85aab04079e7bd722389fb09fca5fe3cec04b6a292f2269c2089317f3202a2a1  src/Splinter.Runtime/PagedMetadataCompactProof.cs
4beb5b987c316784f6652ad452a758becd3351e48152701635c73a6536307867  src/Splinter.Platform.Windows/NtfsSourceGenerations.cs
862045ca726bdefce057a8a1fb97eb2dea5bfb6bfb2478ad753ffb6eb51b9d43  src/Splinter.Runtime/MetadataMembership.cs
```

Observed working tree (only the two review documents were edited by this review):

```text
 M AGENTS.md
 M contracts/v1/file-source-manifest.schema.json
 M docs/README.md
 M docs/foundation.md
 M docs/ko/README.md
 M docs/ko/foundation.md
 M docs/ko/lifecycle-optimization-map.md
 M docs/ko/repeated-original-apply.md
 M docs/ko/scalable-storage.md
 M docs/ko/verification/scalable-storage-throughput-followup.md
 M docs/lifecycle-optimization-map.json
 M docs/lifecycle-optimization-map.md
 M docs/repeated-original-apply.md
 M docs/scalable-storage.md
 M docs/verification/scalable-storage-throughput-followup.md
 M src/Splinter.Platform.Windows/WindowsSourceChanges.cs
 M src/Splinter.Runtime/CapturedRangeAdoptionStore.cs
 M src/Splinter.Runtime/JournalPagedCapture.cs
 M src/Splinter.Runtime/PagedMetadataStore.cs
 M src/Splinter.Runtime/ResultFileContentStore.cs
 M src/Splinter.Runtime/SharedMetadataPoolMaintenance.cs
 M src/Splinter.Runtime/WorkspaceStore.cs
 M src/Splinter.SourceControl.Files/FileMetadataMaintenance.cs
 M src/Splinter.SourceControl.Files/FilePagedQueries.cs
 M src/Splinter.SourceControl.Files/FileSourceGenerations.cs
 M src/Splinter.SourceControl.Files/FileWorkspaceProvider.cs
 M tests/Splinter.Tests/RawBasisReuseTests.cs
?? docs/ko/verification/collection-performance-architecture-review-20261102.md
?? docs/verification/collection-performance-architecture-review-20261102.md
?? reports/benchmarks/capture-maintenance-20261102.json
?? reports/benchmarks/reference-capture-20261102.json
?? src/Splinter.Runtime/MetadataMembership.cs
?? src/Splinter.Runtime/PagedMetadataCompactProof.cs
?? src/Splinter.SourceControl.Files/FileReferenceResults.cs
?? tests/Splinter.Tests/CaptureMaintenanceTests.cs
?? tests/Splinter.Tests/ReferenceCaptureTests.cs
```
