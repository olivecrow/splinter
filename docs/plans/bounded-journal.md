# Product bounded-journal implementation

[한국어](../ko/plans/bounded-journal.md)

The user authorized committing the existing experiments, applying the bounded log to the real product while preserving original/result safety, and reviewing an atomic-publication-capable Bee architecture. The preceding experiment is committed as `a8eb778`. The [product contract](../bounded-journal.md) owns the new provider default; [Bee design](bee-native-storage-design.md) was review-only in this scope. The subsequent [BN experiment](../verification/bee-internal-native.md) was measured separately, then retired and removed after its time/storage gate failed. The successful bounded product path remains.

| ID | Scope and completion evidence |
| --- | --- |
| BL-01 | Real SJR2 mutation/state engine with a persisted 16MiB/4,096-frame suffix limit; individual durable ACK; full metadata, rename/delete/resize, open-unlinked identity, cache and no original payload read during checkpoint. |
| BL-02 | Same-owner initialization/publication recovery, malformed/unknown generation preservation, corruption rejection, writer poison, exact inventory and independent result survival. Separate exception injection/process termination from unrun OS failure. |
| BL-03 | Windows provider default, explicit/omitted compaction selection, schema/capability/fake observation and safe reclaim/unchanged-Library restoration. Small editorless actual A/B with source unchanged, peer continuity, independent results and owned closure. |
| BL-04 | Serial same-input SJL4/product-bounded component measurement; include initialization, automatic checkpoint, verification/reopen/result/cleanup costs. Record RAM/storage distinctions, with no whole-Unity speed claim. |
| BL-05 | Required locked restore/full Release build/tests and paired English/Korean contract/qualification updates; preserve historical reports. |

Use small new owned inputs. Do not move the existing Unity Library, start Editors, change services/UAC/ACL or external packages, rerun the complete maze, or implement native Bee storage. Timing must not overlap builds/tests. No group commit, RAM-only ACK, background checkpoint writer, source hydration or compatibility migration is included.

The suffix bound allows obsolete bytes until checkpoint and temporary private-state duplication. State-prefix obsolete bytes are not bounded to 16MiB. No original bytes are copied to fill gaps. The current format remains self-identifying and unknown/older incompatible records are preserved/rejected. New defaults must not silently change existing tasks. [Verification](../verification/bounded-journal.md) records actual completion and limitations.

2026-10-15: scoped-complete-with-limitations. BL-01–05 are complete within the [verification record](../verification/bounded-journal.md): actual editorless A/B, 12 process-termination boundaries, 1,691 passing tests and the matched component pair. Whole Unity and Bee native backing remain outside this completion.
