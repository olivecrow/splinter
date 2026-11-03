# Current parallel workspace performance

Measured on 2026-11-03. The bounded editorless A/B workflow is slower overall than the published version, despite faster preparation and working I/O. Concurrent final preservation/reclamation also exposed a shared-metadata lease failure. This result does not qualify actual Unity Editor throughput.

## Versions and published evidence

GitHub's default `development` tip was verified through the connector as [`c4fa5063964e7b8456de4ea7735c042fb9af8930`](https://github.com/olivecrow/splinter/commit/c4fa5063964e7b8456de4ea7735c042fb9af8930). The measured current code is `6d1dfd5` (repository cleanup and regression maintenance), 34 commits beyond that tip. Differences cannot be attributed solely to the cleanup commit.

The [published verification index](https://github.com/olivecrow/splinter/blob/c4fa5063964e7b8456de4ea7735c042fb9af8930/docs/verification/README.md) records a local Windows gate of 1,691 passes, 24 skips and zero failures. The current gate passed 2,543 cases with 36 conditional skips and zero failures/timeouts. Different test inventories do not establish performance differences.

The latest published actual Unity A/B record is [TP-06](https://github.com/olivecrow/splinter/blob/c4fa5063964e7b8456de4ea7735c042fb9af8930/docs/verification/unity-throughput-optimization.md): its experimental cache-retention candidate failed normal reclamation. Its completed phases total 1,022.1s; the separate shared-Editor workflow completed in 313.2s. The former is a subtotal, not a successful end-to-end result. Cache retention was withdrawn before publication, and the final published product has no new successful whole-Unity timing. A current-to-historical whole-Unity speed ratio would therefore be invalid.

## Matched native repository measurement

Both actual versions were measured afresh on Windows 11 Home 10.0.26300, Ryzen 5 5600X (6 cores / 12 threads), 68,628,553,728 bytes RAM and NTFS. The published version was extracted from the verified Git commit and built separately with locked restore, the same SDK 10.0.112 and Release configuration. Build time is excluded. Runtime DLL hashes are retained with each result.

Each fresh synthetic project has 64 committed 1KiB source payloads, four source/configuration files and an ignored 128-file / 128KiB Library. Every compared run has the same Git source commit `eccc9dbbee8e882c8b1b31ed1315a1805b180382` and identical source/Library SHA-256 maps. No Editor is launched.

The workflow prepares a Library basis, creates A/B concurrently under one home, reads and verifies the complete input in both workspaces, then concurrently creates a distinct source result and performs 128 one-byte Library changes per workspace. Each write is explicitly flushed. Full changed Library bytes and peer-source exclusion are checked. After preservation/reclamation, both independent results are verified, the original Library is restored, all original hashes/HEAD/clean status are checked and only the original Git worktree remains. Product responses confirm absent task providers/mounts/deltas and zero owned resource counters.

Because the current concurrent-completion pilot failed, **both versions use serial A then B final preservation/reclamation in the matched timing table**. Creation and file work remain parallel. This is a controlled serialized-completion comparison, not a successful fully parallel current lifecycle.

Run order was published → current → current → published, with fresh homes/projects and no concurrent regression suite. No OS-cache reset was performed. Means have only two samples per version and are observations, not statistical qualification. Each cohort was estimated at two minutes with a four-minute stop; all four completed within it. Schema/fixture preparation, status diagnostics and post-release result proofs are excluded explicitly; all other dispatch gaps and normal lifecycle costs remain included.

| Cohort wall stage | Published mean | Current mean | Change |
| --- | ---: | ---: | ---: |
| Library preparation | 8.72s | 3.97s | −54.4% |
| Concurrent A/B creation | 38.58s | 29.35s | −23.9% |
| Concurrent read/write/full-content checks | 1.95s | 1.30s | −33.2% |
| Serial A/B final preservation + reclamation | 63.66s | 98.76s | +55.1% |
| Original Library restoration | 0.83s | 1.65s | +97.9% |
| **Accounted workflow** | **113.74s** | **135.03s** | **+18.7%** |

Individual totals: published 113.31 / 114.18s; current 133.57 / 136.50s. The similar current repeats support investigating the completion regression before a larger workload. Restoration's large percentage is only an additional 0.82s here.

## Concurrent completion failure

A preceding fresh 4-payload / 8-Library-file pilot ran A/B creation, file work and `checkpoint --release` concurrently. Both workspaces created successfully; B completed finalization in 30.99s. A returned `InputUnavailable`, `retryDisposition=correct-request`, with a sharing violation on `<home>/metadata-store/writer.lock`. A had already fixed one independent result and remained `releasing`. No state/lock files were removed or edited.

After B completed, the exact same A workspace/home/request key/message resumed successfully. Both tasks had one result, reached `released`, and the complete fixture source/Library was restored and verified. Recovery time is not inserted into a hypothetical uninterrupted success. The published version completed the equivalent small concurrent pilot normally (85.12s accounted total). This is one observed current failure, not a measured failure rate; its preservation/recovery succeeded but fully concurrent current completion did not.

The immediate mechanism is an incompatible lease on the home-shared metadata store, rather than two commands intentionally sharing one workspace ID. `PagedMetadataStore` acquires `writer.lock` with `FileShare.Read`; writable acquisitions are exclusive against other writers. `SharedMetadataPool` has a separate logical pool lock, while compact-tree certification also opens a write-denying lease. Per-workspace operation locks alone cannot coordinate all shared-store lifetimes. The failure does not identify the exact competing holder or managed call stack: audit pool-lock coverage, publication/verification fence lifetimes and shared collection/certification before choosing a fix. Preserve single-writer, physical identity/hash/page proof and durable publication guarantees. Deleting the lock or weakening file sharing is not a fix.

The CLI's `correct-request` classification also fails to describe this observed peer-contention boundary usefully. Any future retry behavior must retain the same fixed result/request and distinguish a confirmed sharing conflict from missing, damaged or unowned input.

## Bottlenecks and next investigation

1. **Shared metadata lease coordination blocks reliable concurrent completion.** Address the small two-process reproduction before returning to a large A/B workflow. The passing ordinary regression gate does not cover this observed cross-process lifecycle interleaving.
2. **Final preservation/reclamation dominates elapsed time:** 98.76s, or 73.1% of the current accounted total. Preparation and work together save about 14.6s, but completion adds 35.1s. A separate completed same-request retry on current A took 6.03s and returned the exact same sole result with no provider. Fresh completion and receipt verification are different paths; subtracting their times would not isolate a particular internal phase. Add bounded stage attribution for retained-basis verification, source capture/retention publication, seal verification, Git unregistration and physical reclamation before optimizing redundant work.
3. **Small-input fixed costs outweigh payload I/O.** All current initial admissions reuse 68 source files with zero payload rehash. At ready-state observation, cumulative provider flush time is 0.303–0.316s each, versus 0.858–0.880s in the published version. Current capture freeze counters are roughly 0.08–0.18s each. These counters cover narrower/different intervals and must not be summed as cohort wall time; they do not explain a roughly 49s per-task completion by themselves. Preserve verification/ACK strength while measuring the remaining process and lifecycle costs.

The two task providers' summed working set at the ready observation averages 152.1MiB published versus 187.4MiB current (+23.2%). This excludes the original Library provider, CLI processes and OS caches; it is neither peak RSS nor total workflow RAM. No physical-allocation, disk-saturation or large-input scaling claim follows from it.

## Reproduction and retained evidence

Local ignored evidence is under `artifacts/parallel-ab-20261103/`: `measure.py`, `summary.json`, `machine.json`, per-run schemas/responses/hash maps, the failed pilot and its exact-request recovery, and the separate receipt retry. The baseline source/build and fresh fixture homes are under `.local/parallel-ab-20261103/`. Independent results and their matching binaries remain retained; no benchmark provider/CLI/Git process remained afterward.

The local harness accepts `--runtime <absolute-splinter.dll> --name <new-name> --files 64 --library-files 128 --completion serial`; use the same inputs/order and distinct new names. `--completion parallel --files 4 --library-files 8` selects the observed contention pilot. The harness reads each version's own command schemas, uses returned workspace paths and preserves state rather than force-killing a timed-out CLI.

No previous 100k/17GiB fixture, real Unity project/Editor or original managed Library was resumed. Actual Unity compilation/Play/build, Plastic/None matrices, power-loss behavior, peak resources and large-project performance remain unqualified. The observed failure and serialized timings must not be advertised as successful current full-parallel Unity throughput.
