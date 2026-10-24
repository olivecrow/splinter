# Single-workspace lifecycle throughput verification

[한국어](../ko/verification/lifecycle-throughput.md)

Scope is [LT-01–04](../plans/lifecycle-throughput.md): one workspace's preparation, active use and completion, rather than overlapping A/B workspaces. Baseline is `be761bb43032f72fbbd5ca9a3165bd937bd45a6f`; the candidate is the working-tree change. The [dataset](../../reports/benchmarks/lifecycle-throughput-20261024.json) preserves individual runs and hashes; raw evidence is under `artifacts/lifecycle-optimization-20261023/`. Timings below are component evidence, **not a new Unity startup, compile or complete-lifecycle measurement**. ABBA ran the combined experimental candidate; final Library-only confirmation is separate, after withdrawing LT-01.

## Implementation and safety

| ID | Implemented behavior | Validation boundary |
| --- | --- | --- |
| LT-01 | **Evaluated and excluded.** The candidate allowed committed SJR2 reads during each individual durable flush. | Although concurrency/guard tests passed, the no-delay workload saved only 7.659s / 13.0%. Following the requested large-gain priority, its Runtime changes and dedicated tests were removed. Existing journal behavior remains. |
| LT-02 | Capture hashes, admission file opens/hashes and unchanged-directory restoration open/hash/metadata operations use `min(max(logical CPUs, 2), 8)` workers. | Directory maps are completely pinned before concurrent file access. Index publication is serial after all workers join; failures close all partial handles only after draining. File identity, type, hardlink/reparse/final-path, length/content checks, post-move verification and directory-last metadata remain. No copying, hydration or persisted payload cache. |
| LT-03 | Same harness with baseline/current product assemblies, separate serial processes in ABBA order. | Fixed synthetic content and current bounded journal; no other build/probe ran during measurements. OS caches were not cleared. Two repetitions do not establish cold-storage, other-hardware or whole-project speedups. |
| LT-04 | Focused and required full automated regression gates. | Final validation results are recorded below. No original project, running Editor, external tool, old results or service was changed. |

The excluded candidate passed 15 dedicated concurrency cases, including before/after-flush barriers, old compressed/private/base bytes, lazy namespace identity, waiting write/seal/flush/change-observation/disposal, failure poisoning, and WinFsp read/GetFileInfo versus Close. Their source and binaries are retained only in raw evidence. The adopted Library change extends failed/cancelled worker-drain tests to eight workers and retains existing directory replacement, handoff, metadata, journal recovery, original application and reclamation coverage.

The shared owned-directory capture/reader also serves managed `project-snapshot` bases; those callers receive the worker change and retain their existing admission guards. The timing fixture below qualifies Library components only, not a new native project-snapshot lifecycle or another OS.

## Library components

Each invocation creates a dedicated ordinary NTFS Library containing 20,000 files of 65,536 bytes (1,310,720,000 bytes; about 1.22GiB), with identical content and a read-only first file. Setup and full restored-content assertions are outside the timers. Capture and admission still verify all file bytes. Restore includes plan, root transfer/move, fresh identity/content proof, metadata and exact bounded-journal reclamation. It covers the **unchanged original Library** route, not general restoration after original Library mutations.

| Stage | Baseline mean, s | Candidate mean, s | Reduction |
| --- | ---: | ---: | ---: |
| Capture | 4.825 | 2.192 | 54.6% |
| Admission | 2.515 | 0.875 | 65.2% |
| Unchanged restore | 10.088 | 3.711 | 63.2% |
| Sum of these components | 17.428 | 6.778 | 61.1% |

Raw times are `before-native-{1,2}-library.json` and `after-native-{1,2}-library.json`. Extra hash scratch is at most 512KiB for eight 64KiB buffers, versus 128KiB for the earlier admission; capture/restore were serial. Handles were already retained for admission/restore; new per-file arrays and capture records add managed allocation. The measured allocation is cumulative managed allocation, not peak process RAM. The final candidate additionally retains the original per-file normalization error category and repeats the non-Windows path check before opening; its confirmation run is recorded separately.

## Mixed reads and durable writes

The initial 2,048-write probe uses growing one-byte ranges plus concurrent private/base reads and at least 5ms SHA256 work per iteration. Without injected storage delay its means are 25.329s / 20.794s: only 4.535s saved. A separate **requested** 5ms sleep before each flush gives 50.337s / 31.072s. Windows timer scheduling makes that delay longer than 5ms; this is a contention model, not a reproduction of BC-01's mean flush latency or evidence of Editor speed. These results alone do not establish a large ordinary-workload gain.

The follow-up uses **8,192 overwrites of the same byte with no injected storage delay**, simultaneous private/base reads and SHA256 work. Baseline runs are 59.216s / 58.514s; candidate runs are 48.564s / 53.849s. Means are **58.865s / 51.207s**, a **7.659s / 13.0%** reduction. Each run records 8,194 commits and 8,200 flushes, including bounded checkpoints. Writer time varies from 17.440s to 23.869s; reduced lock-wait counters alone are not a large total-workflow gain. **The journal candidate is excluded from the final product change.** Neither workload mounts a filesystem or runs Unity; the callback correctness test is separate from these Runtime timings.

## Validation and reproducibility

The combined experimental candidate's focused native suite passed **513 tests, 0 failures, 16 conditional skips** (529 total). The final Library-only `pwsh -NoProfile -File scripts/build.ps1` passed locked restore and Release compilation with zero warnings/errors, then **1,558 passed / 0 failed / 25 conditional skips** (1,583 total, 531.522s). The final cost test also passed. Its capture/admission/unchanged-restore times were 1.935s / 0.729s / 3.001s (5.665s sum), with every restored byte checked. This separate confirmation uses ordinary user TEMP outside VCS; it is not another matched ABBA pair and is not pooled into the table above. The dataset records final source and assembly hashes.

Build a frozen archive of the baseline, then run `LifecycleThroughputCostTests.LargeLibraryCaptureAdmissionAndUnchangedRestore` with either baseline or current product DLLs and a new output path in `SPLINTER_LIFECYCLE_LIBRARY_COST`. Run serially and preserve old outputs. Product hashes are in the dataset. The removed journal harness/source remain under `rejected-journal-candidate/` in raw evidence, not current entry points. Initial sandbox temp-file rename/final-path failures and the invalid runner-filter invocation are retained. The first full suite incorrectly used a temp root below this Git checkout, causing None/Plastic isolation guards to reject inherited VCS administration; final verification uses the ordinary temp directory outside VCS. Successful NTFS tests run outside the restricted sandbox using dedicated fixtures. Baseline archive restore used locked cached packages with online vulnerability lookup disabled after the restricted network rejected it; the final required gate retains the normal repository restore policy.

This component gate did not measure a full Unity lifecycle. The subsequent [LU-01](unity-single-lifecycle.md) separately measures startup/edit/compile/asset inspection/discard/unchanged-Library restoration under matched inputs, including a common bounded-discard routing repair. Original application, general per-file Library restoration, whole-process peak RAM and OS crash qualification remain **NotRun**. Original publication, seal, stop, independent-result and reclamation algorithms retain their guards; they are not shortened by skipping proof or treating a timeout as completion. The cancelled historical comparison and debugger cleanup are not resumed or reclassified.
