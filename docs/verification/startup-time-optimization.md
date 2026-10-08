# Startup time: independent reads and append metadata

[한국어](../ko/verification/startup-time-optimization.md)

**ST-01–04** targets storage work on the preparation path identified in [UC-01](unity-checkpoint-release-remeasurement.md). That historical Unity run took 848.3379 seconds to initial readiness; its A log records 448.330154 seconds in Scan and 250.873981 seconds in CompileScripts. These are Unity phase measurements, not isolated compiler CPU time. New real Unity readiness is **NotRun** for this candidate.

## Independent immutable reads — ST-01

`ISnapshotTree.SupportsConcurrentReads` is a lifetime-fixed opt-in, defaulting to false. Git synchronizes its own decoder/proof state; the owned NTFS directory uses immutable indexes and pinned, position-independent handles. Both opt in. A graft opts in only when both leaves support it, and both production ownership wrappers forward it. This lets Library reads progress while a Git read is blocked. Unknown providers keep the serial basis gate.

Private bytes and immutable coordinates are captured under the journal gate before concurrent basis I/O. Admission, poison/length/availability checks on completion, and draining every in-flight read before all close callers return are unchanged. Tests include production wrappers and an actual pinned NTFS Library, unchanged source/private bytes, conservative fallback, concurrent mutations and closes, and read/availability/poison failures. They prove independent progress, not a measured reduction of Unity's 448-second scan. Git's two sessions, 256KiB retained chunks and full blob/chunk validation remain; backward reads beyond those windows can still restart decoding.

## Reuse completed append metadata — ST-02

The current SJL4/root v4 and initialization v1 now require **`NodeFormat=2`**. Nodes use **SJN2** and immutable 32-extent metadata pages use **SJP1** within ordinary SJB1 blocks. Missing/older format discriminators are rejected before initialization publication or pending recovery/reclamation, including empty old roots. Earlier SJN1 state is preserved without migration. Overlay/workspace v2, OFF SJR2 and independent candidate v3 remain unchanged.

A true append publishes at most one newly completed page and keeps the unfinished suffix inline. Earlier pages remain referenced without rewriting their full extent/hash arrays or copying their payload. Pages count as metadata, not private user payload. Ownership, exact references, hashes, complete range coverage and total object inventory are checked on reconnect. Each mutation still durably flushes once before publication, retirement and ACK.

Changing a paged prefix retires the invalid suffix pages and persistently disables live paging for that node. Existing capsule retirement can carry each affected page capsule's retained append payload once; this is not a zero-copy claim for arbitrary overwrites. Disabling repaging prevents repeated page/payload accumulation. The retained prefix stays reusable; explicit stopped compaction can rebuild independent page seeds. Range payloads, deletion with open contexts, final unlinked reclamation, candidate export, OFF/ON compaction and interrupted publication retain their guards. No snapshot read, whole-file copy-up or new read cache is introduced.

## Reuse identical owned Unity infrastructure — ST-03

Starting the same workspace verifies the complete existing bridge owner, content hash, allowed descendants and plain paths. If embedded bridge content is identical, neither the C# file nor its marker is rewritten. File/directory timestamps and Unity-created `.meta` files remain. Different verified owned code follows the existing update path; changed ownership/content and unexpected/reparse entries are rejected. Launch tokens and control records remain fresh. This avoids a same-workspace restart invalidation; it does not eliminate first-start compilation.

Original source timestamps are not substituted. Git-clean input can still differ from committed raw bytes through newline conversion, and existing Library receipts do not bind stable source metadata to raw blob identities. Safe timestamp reuse needs a separate durable content/metadata provenance contract. Bee inputs/caches and external tools were not modified.

## Bounded measurement and validation — ST-04

The [dataset](../../reports/benchmarks/startup-time-optimization-20261008.json) fixes source/binary hashes and four samples. The same frozen JS probe executable runs against normal Release Core/Runtime assemblies on local NTFS: 128 unrelated private files, then 1,024 appends of 4KiB to one file. Setup and final byte/peer-file/seal/reconnect inspection are outside the timer. Order is before1 → after1 → after2 → before2, without cache clearing; no benchmark sample overlaps the full test suite. The baseline already includes the preceding JS memory fix.

| Metric | Before | Current | Observed reduction |
| --- | ---: | ---: | ---: |
| Append elapsed, sample 1 / 2 | 7.9540 / 7.9697 s | 5.9865 / 6.4040 s | — |
| Append elapsed, mean | 7.9619 s | 6.1953 s | 22.19% |
| Logical put bytes | 123,240,924 | 9,872,201 | 91.99% |
| Per-append durable flushes | 1,024 | 1,024 | unchanged |

All samples verify exact payload, unchanged unrelated files, current seals and read-only reconnect. Logical put counts are not physical disk I/O and exclude storage-internal carried objects; the append regression separately checks that every append capsule contains exactly one new user payload. Current journal bytes are 4,953,346 → 4,894,541. This is a fixed warm append workload, not a Unity startup percentage or arbitrary-overwrite qualification. Substantial fixed per-mutation work remains despite the lower metadata volume.

Final `pwsh -NoProfile -File ./scripts/build.ps1` passed locked restore and Release build with **0 warnings/errors**, then **1,315 passed, 0 failed, 19 conditional skips**. The 38 new cases cover independent reads (10), owned bridge reuse (10), and metadata pages/current-format refusal (18). Existing large-footer corruption coverage remains through an input that deliberately disables paging. A new interrupted-publication case exposed the old root handle blocking its own backup truncation; recovery now closes that handle while retaining writer/object guards, then reopens the current root. The initial failure is retained in the raw evidence. An overlapping build attempt hit a test-DLL lock; the final build/test run completed alone, before measurement.

Raw runs/build logs and probe evidence remain under ignored `artifacts/startup-time-20261008/`. All 27 unrelated dirty files remain unchanged; the sibling Unity repository is clean. English/Korean contracts and navigation are synchronized. No administrator/UAC action, installed service change, original Unity edit or full Unity run occurred. New real Editor/compile time, overall RAM/disk peaks, installed-service compatibility and OS power-loss qualification remain **NotRun**. Previous-format journals are preserved and rejected; they are not automatically upgraded.
