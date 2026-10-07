# Lifecycle read and restoration optimization

[한국어](../ko/verification/lifecycle-read-optimization.md)

This document owns **LO-01–05**, implementation following the [UR-01–04 real Unity profiles](unity-lifecycle-remeasurement.md). The comparison baseline is the frozen `38b6803` product used by UR. The candidate is identified by source/binary hashes in the dataset. Raw evidence is retained under ignored `artifacts/lifecycle-read-optimization-20261007/`.

## Bounded Git proof reuse — LO-01

`GitObjectSnapshotTree` keeps a tree-local LRU of exact object-ID/length chunk proofs: **4MiB total, at most 4,096 entries**, including a proof being constructed. It evicts before allocation. A miss still verifies the full Git blob hash and framing before publishing a proof or any requested bytes. A hit verifies reread chunks against that proof. Switching blobs cancels a partial reader and invalidates the single payload chunk; errors/close invalidate all proofs. Payload staging, the 64KiB chunk, no-progress timeout, no disk hydration and the 8GiB production blob limit remain.

Real loose/packed Git tests alternate 1MiB A/B/A inputs. The return to A decodes **65,536 bytes**, rather than the former full-blob proof path's 1,048,576 bytes. This is a bounded byte-counter result, not Unity wall time. Alias paths share one proof. Smaller internal test limits prove byte-budget and entry-count eviction, MRU behavior, allocation-before-budget admission, and no decoding/output for an oversized proof. Corrupting a cached blob after switching files leaves the complete caller buffer unchanged and invalidates the entire cache.

Reader-session pooling and releasing the journal gate during base reads are not implemented. Backward reads can still decode a prefix; the change removes repeated full proofs on cached blob switches, not all Git decoding costs.

## Seal reads and path checks — LO-02

SJL4 capsule validation now contributes its exact streamed bytes to the composite seal, preserving ordinal physical-file order and the original root/name/length/content framing. Capsule/prefix/seed validation, current generation, final head/membership/pending checks and independent before/after/post-unmount seal operations remain. It does not reuse a prior seal. Independent raw-file hash tests cover empty roots and multi-file/retained-prefix generations; payload corruption remains rejected. Captured length governs the full read and final length checks.

`EnsurePlainPath` queries each ancestor's attributes once instead of separate directory-exists, file-exists and attribute queries. It still walks parents of missing children and rejects reparses; access errors fail closed. Windows restoration canonicalizes absolute paths, opens ancestors root-first without following reparses and validates each pinned directory handle, removing repeated full ancestor walks inside that loop. No path-string cache replaces identity protection.

Reclamation still retains its separate inventory, hash, intent and predelete guards. Removing those checks or changing their sharing/conditional-deletion contract is outside this implementation.

## Unchanged file restoration and durable cursor — LO-03

Planning reuses the manifest's final/retained hash only when the fully verified basis is still pinned, there are no private ranges, and base/final/manifest lengths match. Execution requires saved physical identity and a fresh complete content hash, then moves and applies metadata using the same no-write/no-delete handle. This covers unchanged, renamed and metadata-only files, including read-only files. The fresh unchanged path reduces approximately **six payload traversals to two**: verified basis construction and the execution proof. This is a code-path count, not a measured whole-Library I/O ratio.

Changed files keep their range replay and individual durable data flushes. If a retry already matches its final hash, it omits only the duplicate hash under the same exclusive output handle. Read-only pre-clear and post-reopen retained-byte checks remain separate.

Completion cursors advance every **128 files** and at the last file, using the existing plan/cursor format. Initial/final records, creation receipts and payload flushes remain individually durable. A failed batch publication can leave 128 completed effects unrecorded; recovery rechecks identity/content, alongside any in-progress effect. New 260-file tests cover interrupted effects at 127/128/129 files and before/after cursor publication, matching-byte replacement, changed read-only bytes and same-handle write/delete exclusion. Fresh 127/128/129-file tests observe 1/1/2 advancement publications, respectively.

There is no whole-directory restore mode or migration of an already-started intent. Directory-wide rename and broader concurrent-read redesign require separate recovery/identity work.

## Bounded normal-binary costs — LO-04

| Candidate | Run 1, s | Run 2, s | Mean, s |
| --- | ---: | ---: | ---: |
| 38b6803 | 8.1917 | 8.1461 | 8.1689 |
| Current | 4.2431 | 4.2286 | 4.2359 |

Mean restore time decreases **48.15% on this small input**. Each current run observes three cursor advancements and verifies exact final content/owned cleanup. The baseline code publishes per file (260); its binary has no cursor observer. Order is before 1 → after 1 → after 2 → before 2. [Exact observations and candidate hashes](../../reports/benchmarks/lifecycle-read-optimization-20261007.json) are retained.

The restore comparison uses the same existing owned-file fixture under the earlier and current normal assemblies: 260 unchanged files × 65,536 bytes, SJL4, on local NTFS. Fixture preparation and final independent byte/cleanup assertions are outside the timer. File movement, planning, validation, progress publication and reclamation are included. No Unity, service, UAC or original project mutation is involved. Run order and individual observations are retained in the dataset; this small warm comparison does not project a percentage onto UR's 23-minute restoration.

## Qualification and limits — LO-05

`pwsh ./scripts/build.ps1` records **1,224 passed, 0 failed, 19 conditional skips**, with zero build warnings/errors. Twenty new cases cover Git proofs (6), restore/cursors (11), and seal framing/corruption (3). Existing packed/loose/backward/cancellation/no-progress reads, file identity/retained bytes/changed-range recovery and journal interruption/reclamation regressions run in the same suite.

Existing dirty changes and the previous UR evidence/schema correction remain separate. The README's Unity figures and 98.65% allocation result belong to their recorded candidates; no new Unity A/B, Play/tests/build, allocation/RAM/whole-disk peak or OS power-loss matrix was run. Current changes have automated corruption/recovery coverage and bounded real NTFS/Git evidence, not a new large-Unity timing claim.
