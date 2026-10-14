# Unity throughput optimization

[한국어](../ko/verification/unity-throughput-optimization.md)

This record follows [MW-01](unity-maze-workflow.md) and the [implementation scope](../plans/unity-throughput-optimization.md). TP-01–04 are retained. The experimental kernel-cache candidate failed normal reclamation in the actual Unity maze workflow and was withdrawn. Its small-fixture passes and faster individual phases do not qualify the final product or establish a complete Unity speed advantage.

| ID | Change | Current evidence |
| --- | --- | --- |
| TP-01 | Configuration plus per-path Git attribute interpretation; version 3 source manifest | 62 focused tests pass. Adding 73 paths hashes only their 720 bytes while an unchanged 1MiB file is held against reads. Global configuration and affected-path attributes still invalidate conversion hashes. Unsupported versions remain unchanged even under audit. |
| TP-02 | Reuse the first verified immutable-content summary during initial Git admission | 46 focused tests pass across initial summaries, proof reuse, decoding and source-change contracts. The initial inspection reuses four files with zero payload reads after the required index refresh; audit reads all 1,048,608 bytes. The final actual-attach ordering and changed-source regression pass in the candidate follow-up below. |
| TP-03 | Commit-scoped capsule handle/footer reuse and same-handle retirement | 82 focused tests pass, including preparation pins, corruption, publication, recovery and read concurrency. At most 64 Windows handles are retained during preparation; other platforms/overflow use fresh reads. Pins close before publication. |
| TP-04 | Capture compressed private bytes before decoding outside the mutation gate | Initial targeted run: 54 pass, two optional cost probes skipped. Generation preservation, concurrent retirement, poisoned writer and close drainage pass. A subsequent 35-test concurrency/recovery run passes, including the 64-fragment overflow regression. |
| TP-05 | Experimental Windows file-cache retention | **Withdrawn.** Small native/helper and editorless CLI checks pass, but each actual Unity workspace retains eleven linked Burst executable-image contexts after normal Editor exit. Normal completion returns `OverlayStopPending`. Product cleanup purge remains enabled; the experimental helper and tests are preserved as raw evidence, not product code. |
| TP-06 | Same multi-stage maze workflow versus one shared Editor | Feature generation, saved assets and integrated Play checks pass on the candidate. Normal Splinter completion fails; exact-ID recovery restores the original. Recovery is separate from throughput qualification. The shared replay uses the same corrected scene handoff. |

## Bounded costs

Two runs per frozen assembly, in before/after/after/before order, produce these mean times. Each run performs 768 individually flushed mutations, retains exactly 195 current files / 289,879 bytes and passes full content plus reconnect-seal checks.

| Writer fixture | Before | After |
| --- | ---: | ---: |
| Whole-file overwrite | 3.766s | 3.661s |
| Metadata replacement | 3.553s | 3.359s |
| Page-prefix overwrite | 5.141s | 5.216s |
| Sum | 12.460s | 12.236s |

The preparation portions decrease 32–50% and cumulative managed allocation 65–72%; total time changes only about 1.8%, with a page-prefix regression within these few observations. This is not a large workflow improvement. Raw data: `artifacts/unity-throughput-optimization-20261015/storage-probe-summary.json` and its four writer samples.

The separate eager-shard prototype reduces 2,048 physical files to 128 or 32 and makes a full read verification faster. It rewrites 16–64 times as many private bytes per mutation and slows the measured mutations. It does not implement multi-shard atomicity or production recovery and is not selected for the product.

The ordinary-user native cache prototype (`.local/wc15b/result.json`) measures eight repeated read rounds on a small synthetic basis. Mean time is 0.1571s with cleanup purge and 0.0374s with retention; basis reader bytes fall from 16,384,000 to 9,043,968. Retention alone leaves five contexts after normal unmount. The separate retention-plus-notification trial drains in 11.3ms, reaches zero contexts and preserves the seal across unmount. Root `FlushFileBuffers` succeeds in all trials. This is not a Unity result or proof of every dirty/mapped-write boundary.

## Preserved guarantees and RAM cost

The journal format, final physical-byte checks, independent results, ownership and per-mutation durable flush/ACK remain. TP-04 holds at most 4MiB of rented encoded buffers and 64 fragments **per read**, plus ordinary fallback scratch; simultaneous reads multiply this bound. It uses the existing 64MiB decoded cache under a separate cache lock, still checks current encoded bytes, and drains active reads before close. It is not a total-process RAM bound.

Windows kernel cache is managed by the OS, without a product per-volume byte budget. The withdrawn experiment required exact-volume flush, admission fencing and actual context drainage; a timeout or Editor exit was insufficient. Production retains the existing cleanup-purge policy. The native measurements below describe only the rejected candidate.

The `.local/wc15c/result.json` helper run verifies native generation identity and the public raw WinFsp notification API. A live mapped view rejects stop with `OverlayContextsRemain`; the same view remains writable and flushable. Closing it permits final drain in 61.4ms with zero contexts before/after unmount and equal independently reopened content/seal. Its elapsed read times are not used as performance evidence because other tests ran concurrently. Long and unpaired-surrogate names retain exact UTF-16 code units; ordinary notification batches are at most 16KiB and a single long record is at most 64,016 bytes.

## Small real product A/B

`artifacts/unity-throughput-optimization-20261015/current/result.json` records a fresh editorless Git project with 64 payload files and a 128-file managed Library on the frozen candidate. A/B each reuse 68 initial source files with zero payload rehash. Blocking A's flush-response publication and B's source-response publication retains the same live provider/mount; removing only the injected empty response directory lets the same request ID complete. No sticky state is cleared. Parallel `checkpoint --release` preserves two independent Git results, excludes the peer's source, reaches `released`, and confirms mount/provider/delta absence and zero handles/working set/private bytes. Library restore verifies every original file hash, original HEAD/clean state and one original worktree. Timings are correctness records only because the full regression suite ran concurrently.

## Actual Unity failure and safe recovery

The frozen candidate is identified by `artifacts/unity-throughput-maze-20261015/candidate.json`; it contains TP-01–05 and differs from the final TP-01–04 product. Both owned Editors exit normally with code 0. Combined completion first reaches the Editor deadline, then the same-ID retry reports `OverlayStopPending`. Exact-process read-only heap observation finds eleven linked DLL contexts per workspace: two Burst runtime DLLs and nine Burst JIT DLLs, with no deleted contexts. Root flush and name notifications had not drained these executable images. The editorless CLI check above did not cover this actual failure.

A small image fixture verifies that an existing-file `FILE_WRITE_DATA` open rejects a live executable image with sharing error 32, and succeeds after its release without any write. The installed WinFsp implementation uses `MmFlushImageSection(MmFlushForWrite)` on that open path ([official source](https://github.com/winfsp/winfsp/blob/fde790d/src/sys/create.c)). Recovery pins the exact owned root, opens only the identified existing DLLs without writing, and verifies DLL contents, metadata and complete delta inventories before releasing the root. A preserves 20,015 delta files / 50,349,918 bytes and sequence 49,331; B preserves 17,464 files / 41,627,284 bytes and sequence 45,242. Both providers then unmount naturally with zero handles and contexts. No process is killed, counter changed, file overwritten or global cache purged.

The same workspace IDs subsequently reach `released`, preserve their independent results and restore the original Library. All original source files and Library hashes, existing refs/worktree registrations and temporary local Git configuration are verified. Recovery evidence and the rejected source snapshot are preserved under the same artifact directory. The candidate's elapsed failure and diagnostic recovery are not a successful whole-workflow measurement.

## TP-06 time comparison

[Dataset](../../reports/benchmarks/unity-throughput-maze-20261015.json). The same 2.80GB committed source and a byte-verified 42,422-file / 3,142,530,954-byte Library precede both methods. A builds and saves the maze generator/scenes; B builds the controller, input and stage logic. Six cumulative stages and three integrated playable mazes produce equivalent results. AI authoring, input inventories and evidence export are excluded; preparation, normal exit, preservation and original cleanup are included when completed.

| Phase | Splinter experimental candidate | One shared Editor |
| --- | ---: | ---: |
| Library preparation | 69.7s | Not required |
| A/B workspace creation | 105.5s | 14.3s |
| Initial Editor readiness | 272.7s | 24.1s |
| Six feature stages | 355.3s, parallel | 136.2s, sequential |
| Integration and Play validation | 218.9s | 113.3s |
| Successful whole workflow | **Not available — normal reclamation failed** | **313.2s (5m 13.2s)** |

Splinter's completed phases already total **1,022.1s (17m 02.1s)** before completion/restoration; this is a subtotal, not a successful total. The failure is returned at 1,296.1s. After diagnosis, same-ID completion takes 43.2s for the A/B cohort and Library restoration 52.0s; these recovery measurements cannot be inserted into a hypothetical uninterrupted total. The partial-phase/whole-shared ratio is 3.26, not an end-to-end speed ratio.

Against MW-01's historical Splinter stages, workspace creation decreases from 169.9s to 105.5s, while initial Editor readiness is 267.8s versus 272.7s. Feature stages decrease from 382.9s to 355.3s and integration from 239.8s to 218.9s. That historical Library has six fewer files and 35 differing files, so these are observed comparisons, not isolated causal effects. MW-01's 787.5s shared total includes a modal incident and must not be used as a clean baseline.

The measured candidate does not meet the speed acceptance criterion: even its feature-development phase is slower than sequential Editor access. The final code retains TP-01–04 and withdraws TP-05; it has no new successful whole-Unity timing claim. No larger repetitive rerun or general long-task crossover is inferred. This is one ordered pair with no OS cache reset or statistical repetitions.

## Regression verification

`pwsh -NoProfile -File ./scripts/build.ps1` passes locked restore and Release build: 1,621 passed, zero failed, 25 conditional external tests skipped out of 1,646. After the final Git attach-order adjustment, 37 follow-up regressions pass in an isolated output whose product DLL hashes match the frozen runtime; the real CLI pair above also uses that candidate. The complete gate preceded this last sequencing adjustment. Raw evidence: `artifacts/unity-throughput-optimization-20261015/validation-summary.json` and `full-regression.trx`.

After withdrawing cache retention, the final isolated Release build has zero warnings/errors. A further 35 focused tests pass, and the small real `final-purge` A/B repeats initial zero-rehash admission, response-publication failure preservation, parallel completion, independent results and full original/resource restoration. `artifacts/unity-throughput-optimization-20261015/final-validation.json` pins this final source and runtime and records the gate boundaries. The raw small-run scope string inherited the earlier cache wording; this final run uses cleanup purge, as the pinned code confirms. Its concurrent-test timings are not performance evidence. The final TP-01–04 product has no new successful full Unity timing qualification.

The default `scripts/splinter.ps1` Release output is rebuilt after withdrawal; all ten product DLL hashes match the final native-tested output (`default-entrypoint-proof.json`). The actual response-schema check also passes after selecting response objects separately from array-shaped benchmark records.
