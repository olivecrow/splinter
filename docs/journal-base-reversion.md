<a id="저널-정리의-선택적-기준본-복귀"></a>
# Optional base reversion during journal compaction

[한국어](ko/journal-base-reversion.md)

```powershell
pwsh ./scripts/splinter.ps1 schema workspace.compact
pwsh ./scripts/splinter.ps1 workspace compact --id <id> --home <home> --revert-base-ranges true
```

The initial default for new tasks is **ON**. Existing tasks and compaction with the option omitted retain the stored choice; explicit `false` selects OFF. Current `true` selects the [latest-state journal ON](latest-only-journal.md), retaining only current private payload/metadata in subsequent writers and reconnects. `--optimize-journal` is the same option. Explicit compaction compares current private ranges with the immutable basis fixed at task start and removes private payload where bytes match. It does not compare against the currently mutable original. Individual flush/ACK for each save remains; basis comparison is not added to every save. Verification figures for the historical append-only implementation are limited to that candidate.

Basis comparison occurs only within the current file incarnation's `BasePath` and `BaseLength`. Reads stay within 64KiB and the workspace transfer limit. A 4KiB unit, or a short final unit, is removed only if all its bytes match. If any byte differs, that unit remains; this is not an algorithm that produces a minimal representation of every matching byte. Partial reversion/mixed writes are supported. A tail extended after truncation and new or deleted/recreated files are not restored from old basis content. Private files without a basis remain.

Even when file-write order is interleaved, comparison proceeds in logical-offset order within the same `BasePath`; remaining ranges are sorted back into original journal physical order before candidate creation. The Git reader retains only one blob proof and a 64KiB chunk. Remaining stdout after a partial read is terminated by closing the exact owned pipe; discarded content is neither drained nor cached. Full blob hashing, requested-chunk verification, and the 30-second no-progress limit remain. This change's [comparison scope](plans/journal-base-reversion-optimization.md) and before/after measurements are separate from existing real A/B qualification.

Matching content does not discard file ID, path/rename, length, attributes/UnixMode, timestamps, deletion/recreation, hidden state, or changed path/sequence. An SJL4 candidate directly records current nodes, remaining private ranges, and last path sequences, without obsolete write frames. No original-payload fallback reintroduces removed ranges. ON differs from OFF's smaller-frame selection and costs; some inputs may require more metadata, CPU, or managed allocations. Limits or fragment-metadata limits preserve the original and reject the operation.

<a id="검증과-같은-요청-복구"></a>
## Verification and same-request recovery

Distinguish the original journal's structural hash from the **expected structural hash** after removing verified matching ranges. The expected hash includes the same file entries, hidden/path changes, sequence, and boundaries/content of private ranges to retain. Removed-range content equivalence is checked through fixed-basis reads/codec validation. Hashing whole files or unchanged basis data does not create copy-up, full copies, read hydration, or payload disk caches.

Publish atomically only after checking the whole new candidate's checksum, flush, replay, expected structural hash/sequence, and before/after source seals. Previous generations are not reclaimed before reconnect and publication-seal verification. Absent/damaged/unknown bases, unexpected short reads, and damaged intents/candidates preserve data and reject the operation. The existing contract allowing a complete write without a response to survive replay remains.

Current `compaction-intent.json` supports **v3 only**, fixing original/expected hashes, selected bool, compared/removed quantities, and both composite seals. Older v1/v2 intents are preserved/rejected without automatic migration. Workspace/overlay v2, OFF SJR2, ON SJL4/root v4, and independent candidate v3 are used. CLI request `revertBaseRanges` is an optional boolean; retries and new compact calls with the option omitted use the stored choice. A different choice during an operation is `CompactionIntentMismatch`. The same completed seal/choice is a read-only retry; explicitly choosing a different option after completion can begin a separate compaction.

Response `snapshot.compaction` fields `revertBaseRanges`, `baseComparedBytes`, and `revertedPayloadBytes` respectively report the choice, compared bytes returned by the basis reader, and removed logical private bytes. Read `cost` stages `last-write-index`, `base-range-compare`, and `expected-state-hash` together with existing stages. Stage `readBytes` sums journal reads and returned basis bytes for that stage; it is not total Git internal blob decoding or OS physical I/O. Logical removal does not measure NTFS allocation or VSS savings. Old/new generations coexist during compaction, and metadata/checksums remain.

Follow the [fixed comparison/numerical criteria](plans/journal-base-reversion.md) and [actual comparison, recovery, and default decision](verification/journal-base-reversion.md). A smaller final payload alone does not establish improved whole-task speed, total RAM, or Unity import costs.
