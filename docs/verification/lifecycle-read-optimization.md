# Lifecycle read and restoration optimization

[한국어](../ko/verification/lifecycle-read-optimization.md)

This document owns **LO-01–08**, implementation following the [UR-01–04 real Unity profiles](unity-lifecycle-remeasurement.md). LO-01–05 compare against the frozen `38b6803` product used by UR and were committed as `0e707ed`; LO-06–08 below use `0e707ed` as their baseline. The candidate is identified by source/binary hashes in the dataset. Raw evidence is retained under ignored `artifacts/lifecycle-read-optimization-20261007/`.

## Bounded Git proof reuse — LO-01

`GitObjectSnapshotTree` keeps a tree-local LRU of exact object-ID/length chunk proofs: **4MiB total, at most 4,096 entries**, including a proof being constructed. It evicts before allocation. A miss still verifies the full Git blob hash and framing before publishing a proof or any requested bytes. A hit verifies reread chunks against that proof. In that candidate, switching blobs cancels a partial reader and invalidates the single payload chunk; errors/close invalidate all proofs. Payload staging, the 64KiB chunk, no-progress timeout, no disk hydration and the 8GiB production blob limit remain.

Real loose/packed Git tests alternate 1MiB A/B/A inputs. The return to A decodes **65,536 bytes**, rather than the former full-blob proof path's 1,048,576 bytes. This is a bounded byte-counter result, not Unity wall time. Alias paths share one proof. Smaller internal test limits prove byte-budget and entry-count eviction, MRU behavior, allocation-before-budget admission, and no decoding/output for an oversized proof. Corrupting a cached blob after switching files leaves the complete caller buffer unchanged and invalidates the entire cache.

LO-01 did not include reader-session pooling or releasing the journal gate during base reads. The LO-06–08 follow-up below adds them. Reads outside the bounded retained chunks can still decode a prefix; neither change removes all Git decoding costs.

## Seal reads and path checks — LO-02

SJL4 capsule validation now contributes its exact streamed bytes to the composite seal, preserving ordinal physical-file order and the original root/name/length/content framing. Capsule/prefix/seed validation, current generation, final head/membership/pending checks and independent before/after/post-unmount seal operations remain. It does not reuse a prior seal. Independent raw-file hash tests cover empty roots and multi-file/retained-prefix generations; payload corruption remains rejected. Captured length governs the full read and final length checks.

`EnsurePlainPath` queries each ancestor's attributes once instead of separate directory-exists, file-exists and attribute queries. It still walks parents of missing children and rejects reparses; access errors fail closed. Windows restoration canonicalizes absolute paths, opens ancestors root-first without following reparses and validates each pinned directory handle, removing repeated full ancestor walks inside that loop. No path-string cache replaces identity protection.

Reclamation still retains its separate inventory, hash, intent and predelete guards. Removing those checks or changing their sharing/conditional-deletion contract is outside this implementation.

## Unchanged file restoration and durable cursor — LO-03

Planning reuses the manifest's final/retained hash only when the fully verified basis is still pinned, there are no private ranges, and base/final/manifest lengths match. Execution requires saved physical identity and a fresh complete content hash, then moves and applies metadata using the same no-write/no-delete handle. This covers unchanged, renamed and metadata-only files, including read-only files. The fresh unchanged path reduces approximately **six payload traversals to two**: verified basis construction and the execution proof. This is a code-path count, not a measured whole-Library I/O ratio.

Changed files keep their range replay and individual durable data flushes. If a retry already matches its final hash, it omits only the duplicate hash under the same exclusive output handle. Read-only pre-clear and post-reopen retained-byte checks remain separate.

Completion cursors advance every **128 files** and at the last file, using the existing plan/cursor format. Initial/final records, creation receipts and payload flushes remain individually durable. A failed batch publication can leave 128 completed effects unrecorded; recovery rechecks identity/content, alongside any in-progress effect. New 260-file tests cover interrupted effects at 127/128/129 files and before/after cursor publication, matching-byte replacement, changed read-only bytes and same-handle write/delete exclusion. Fresh 127/128/129-file tests observe 1/1/2 advancement publications, respectively.

There is no whole-directory restore mode or migration of an already-started intent. Directory-wide rename requires separate recovery/identity work.

## Bounded normal-binary costs — LO-04

| Candidate | Run 1, s | Run 2, s | Mean, s |
| --- | ---: | ---: | ---: |
| 38b6803 | 8.1917 | 8.1461 | 8.1689 |
| Current | 4.2431 | 4.2286 | 4.2359 |

Mean restore time decreases **48.15% on this small input**. Each current run observes three cursor advancements and verifies exact final content/owned cleanup. The baseline code publishes per file (260); its binary has no cursor observer. Order is before 1 → after 1 → after 2 → before 2. [Exact observations and candidate hashes](../../reports/benchmarks/lifecycle-read-optimization-20261007.json) are retained.

The restore comparison uses the same existing owned-file fixture under the earlier and current normal assemblies: 260 unchanged files × 65,536 bytes, SJL4, on local NTFS. Fixture preparation and final independent byte/cleanup assertions are outside the timer. File movement, planning, validation, progress publication and reclamation are included. No Unity, service, UAC or original project mutation is involved. Run order and individual observations are retained in the dataset; this small warm comparison does not project a percentage onto UR's 23-minute restoration.

## Qualification and limits — LO-05

The LO-01–05 `pwsh ./scripts/build.ps1` run records **1,224 passed, 0 failed, 19 conditional skips**, with zero build warnings/errors. Twenty new cases cover Git proofs (6), restore/cursors (11), and seal framing/corruption (3). Existing packed/loose/backward/cancellation/no-progress reads, file identity/retained bytes/changed-range recovery and journal interruption/reclamation regressions run in the same suite.

Existing dirty changes and the previous UR evidence/schema correction remain separate. The README's Unity figures and 98.65% allocation result belong to their recorded candidates; no new Unity A/B, Play/tests/build, allocation/RAM/whole-disk peak or OS power-loss matrix was run. Current changes have automated corruption/recovery coverage and bounded real NTFS/Git evidence, not a new large-Unity timing claim.


## Separate immutable reads from journal metadata — LO-06

The ordinary `JournalDeltaStore` now captures immutable file metadata, copies private bytes and records uncovered base intervals under its existing gate. It then reads only those immutable intervals under a separate serialized reader gate. There is no extra payload copy/cache: private bytes already sit in the caller's request buffer, and the interval list is bounded by the existing extent index. A concurrent overwrite, truncate, rename or final unlink can reclaim the old private capsule without changing the captured read's bytes. Basis reads remain serialized; this does not introduce unbounded concurrent Git decoders.

Completion rechecks writer poison, journal length and basis availability. Read failure always retires its lifetime admission. Close rejects new operations, including flush/accounting/restore APIs, and every concurrent close waits for admitted reads before closing journal handles and ownership fences. The owner still disposes the snapshot/lease after the store. `baseReadBytes` is atomic; common `lockWaitTicks` excludes the separate reader gate and OS/Git waits.

Eight deterministic tests cover SJR2 and SJL4 reads blocked inside the basis while metadata and mutations finish, preserved pre-mutation private bytes, truncate/rename/unlink, successful/failed reads during two simultaneous closes, retained writer exclusion, poisoned writes, serialized payload readers and a nested immutable candidate. The tests prove progress while the basis is explicitly held; they are not native WinFsp timing or Unity throughput measurements. Development `SourceViewDeltaStore` still has its outer gate.

## Bounded Git reader sessions — LO-07

Two object-keyed LRU sessions keep their decoder positions and two verified 64KiB chunks each: **256KiB of managed payload buffers**, compared with 64KiB previously. At most two owned `cat-file` readers remain active. A third object reassigns the oldest session: a fully consumed batch reader is reused; a partial reader has its exact stdout closed and child terminated before replacement. This avoids both interleaved decoder churn and a new process per small file. Proof eviction also retires the corresponding session. Errors and close clean both sessions and all proofs, even if one cleanup fails.

The 4MiB/4,096-entry proof bound (including an in-flight proof), full-object hash/framing before first publication, chunk proofs, whole-request staging, no-progress timeout and no disk hydration remain. Previously verified retained bytes may be reused without reading a subsequently changed object store; they still equal the fixed object. This is not a fresh corruption audit on every hit. Backward reads outside the two retained chunks and evicted partial sessions can still restart and decode a prefix. Git child/pipe/internal packed-object memory is additional to the managed buffer limit; whole-process RAM was not measured.

Eight real Git cases cover loose/packed interleaving and backward reads, old-range restarts, multi-chunk staging, session/proof eviction, alias reuse, corrupted rereads with no partial request publication, all-owned-child exit, and 12 distinct small blobs without per-file process creation.

## Fixed-input comparison and validation — LO-08

The same two 1MiB blobs are read from actual loose and packed object stores under the frozen `0e707ed` and current normal assemblies. Initial full proofs read **2,097,152 bytes in both candidates**. Four A/B rounds request 16 bytes at offsets 0, 65,536, 131,072 and 196,608; then both files are read backwards at 131,072. The raw byte/PID observations and candidate hashes are in the [dataset](../../reports/benchmarks/read-concurrency-20261008.json); ignored raw evidence is under `artifacts/read-concurrency-20261008/`.

| Metric, identical in loose and packed cases | 0e707ed | Current |
| --- | ---: | ---: |
| Decoded bytes in forward rounds, excluding full proofs | 1,310,720 | 524,288 |
| Distinct payload reader PIDs through proofs + forward rounds | 8 | 2 |
| Additional decoded bytes for nearby backward reads | 393,216 | 0 |
| Additional reader starts for nearby backward reads | 2 | 0 |
| Peak active payload readers | 1 | 2 |

Forward decoding is **60% lower** and observed reader starts are **75% lower** on this pattern. Each run checks every returned byte and verifies all observed reader PIDs exited on close. Warm forward-round elapsed times were 0.6057428 → 0.003358 seconds (loose) and 0.5899848 → 0.0031412 seconds (packed), excluding initial proofs/setup. There is one observation per candidate/store, run baseline loose → baseline packed → current loose → current packed without cache clearing. These tiny warm durations do not estimate real Unity startup savings.

New Unity/Editor/compile profiling, native WinFsp concurrency, whole-project RAM and OS crash qualification are **NotRun**. The prior **17m 22s** initial-readiness value remains UR's earlier candidate measurement. No original Unity project, installed service or UAC setting was changed.

Final `pwsh -NoProfile -File ./scripts/build.ps1`: locked restore passed; **1,240 passed, 0 failed, 19 conditional skips**, with **0 build warnings/errors**. The 16 new cases also passed in the 38-case focused run. Source and measured Git binary hashes match the final build.
