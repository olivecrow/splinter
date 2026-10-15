# Structural throughput prototypes

[한국어](../ko/verification/structural-throughput-prototypes.md)

**Current disposition:** both Bee implementations were retired and removed: this junction failed atomic publication, and the later [BN candidate](bee-internal-native.md) failed its time/storage gate. Commit `93a7395` preserves their implementation. The results below remain historical evidence; their Bee commands are unavailable in current code. Bounded-log tooling and the [bounded product journal](../bounded-journal.md) remain.

This development-only experiment follows [TP-06](unity-throughput-optimization.md) and the [fixed SX scope](../plans/structural-throughput-prototypes.md). The product journal format, default storage policy and workspace lifecycle are unchanged. The source baseline is `dd188e1`; executable hashes and raw-result hashes identify the measured candidates separately from later builds.

[Pinned comparison dataset](../../reports/benchmarks/structural-throughput-prototypes-20261015.json). SX-02 passes its bounded content/recovery experiment; SX-01 passes the graph/source-protection experiment but **fails actual Bee compilation**. SX-03 reports those distinct boundaries, and SX-04 does not approve a production backend or claim a whole-workflow speedup.

## Bounded range log: SX-02 and SX-03

The actual current SJL4 implementation and a small bounded WAL use the same deterministic original and mutations: 20,000 fixed logical files, 256 hot files, 48MiB of initial private ranges, then 4,096 incompressible 4/8/16KiB writes. Each successful write follows its own durable flush. The WAL keeps current private ranges in RAM and appends checked range records; a full current-private checkpoint replaces the log at its cap. It never checkpoints the immutable original. The final consolidation and obsolete-file retirement are included in the write interval.

One serial cohort, without overlapping builds or tests:

| Phase / cost | SJL4 | WAL, 4MiB cap | WAL, 16MiB cap |
| --- | ---: | ---: | ---: |
| Initialize and seed private ranges | 70.582s | 21.949s | 11.090s |
| 4,096 writes including final checkpoint | 92.871s | 26.970s | 13.872s |
| Write-time reduction against SJL4 | — | 71.0% | 85.1% |
| Validate current bytes | 1.032s | 0.516s | 0.683s |
| Reopen and validate disk state | 6.893s | 0.737s | 0.746s |
| Export independent result | 0.983s | 0.298s | 0.389s |
| Remove owned store, preserving result | 1.206s | 0.010s | 0.009s |
| Sum of these instrumented phases | 173.566s | 50.480s | 26.790s |
| Process peak working set observed through the write phase | 100.0MiB | 162.0MiB | 162.5MiB |
| Sampled peak owned file bytes | Not measured | 101.4MiB | 113.3MiB |
| Recorded write bytes during measured writes | 104.1MiB | 524.0MiB | 183.9MiB |
| Per-write response p50 / p95 | 20.62 / 35.38ms | 3.40 / 11.13ms | 3.07 / 3.71ms |
| Maximum individual response | 183.3ms | 942.9ms | 349.4ms |

The sum excludes fixture generation and process launch; it is not a product `checkpoint/release` or full Unity time. File-byte peaks are transaction/checkpoint samples, not peak NTFS allocation. Recorded writes exclude filesystem/device overhead. Larger logs reduce checkpoint frequency but allow more obsolete history. Checkpoint pauses explain why the maximum response can increase despite much faster total writes. The 16MiB variant retains 48MiB of private payload in RAM; 16MiB is a log cap, not a memory cap.

The 24-case selftest passes: normal content/reconnect/independent-result checks, twelve WAL and six SJL4 owned-child interruption boundaries with externally durable ACK records, incomplete-header recovery, complete-frame/checkpoint corruption rejection, and wrong-owner/unknown-file preservation. All measured methods recover the same full logical hash, leave the immutable original unchanged, and retain a verified independent archive after their stores are removed. This is process-interruption evidence, not power-loss evidence.

The WAL is a reduced fixed-object content-range implementation. It does not yet implement file creation/deletion/rename, all SJL4 metadata, concurrent writers, live per-read disk-corruption proof, or production lifecycle integration. Live WAL reads use the RAM index; reconnect verifies on-disk state. These ratios therefore qualify a structural component opportunity, not a drop-in product speedup. OS caches were not reset and each cap has one measured sample; the cap-to-cap ratio is not an isolated causal estimate.

Raw results: `artifacts/bounded-log-probe-20261015/measured-{0-sjl4,1-wal4,2-wal16}/result.json`; selftest: `selftest-02/selftest-result.json`. The preceding `full-*` runs overlapped because the coordinator started a second process before confirming completion; they are retained as diagnostics and excluded from this table. The SJL4 raw `inventory` lists root members only; total current stored bytes are obtained from its checked `Observe().journalBytes`, not that partial listing.

## Native Bee: SX-01 and SX-03

The probe exposes only the exact owned `Library/Bee` as native NTFS. Source and other Library files use the current range journal. The fixed Unity 6000.3.22f1 input has three assemblies, no packages and no IL post-processing. It uses actual installed graph generation and Csc tools without an Editor. Original tools, the historical input and the sibling Unity project are not edited. Both variants start with empty Bee output; no original Library payload is copied.

The final **graph-only** cohort runs overlay/native/native/overlay, with three graph generations in each fresh view. Builds and other tests are stopped. Means of two fresh views per variant:

| Small graph experiment | Current overlay | Native Bee graft |
| --- | ---: | ---: |
| First graph, empty generated output | 1.311s | 0.476s |
| Subsequent graph rewrite | 1.559s | 0.494s |
| Mean of all six graphs | 1.476s | 0.488s |
| Preparation | 0.322s | 0.374s |
| Independent preservation and normal closure | 0.460s | 0.153s |
| Instrumented phase sum, three graphs per view | 5.341s | 2.101s |
| Wall interval, provider start through exit | 5.615s | 2.225s |
| Final owned regular-file NTFS allocation | 476KiB | 636KiB |

Graph time decreases **67.0%**, while this small final allocation increases 33.6%. The phase sum includes input/source edits and excludes evidence copying/comparison/dispatch gaps; the wall interval includes those gaps but excludes the final allocation scan. Neither interval includes compilation. Allocation uses `GetFileInformationByHandleEx(FileStandardInfo).AllocationSize` for only delta/native-Bee regular files after exit, excluding fixture, tools, source export, receipts, evidence and filesystem directory metadata. Peak temporary allocation is **NotMeasured**.

All four views have identical normalized graph/payload hashes, unchanged immutable inputs, verified independent source results, normal unmount, zero handles/contexts and no provider errors. The final real mounted `selftest-06` also checks target/ancestor protection, peer preservation, mapped writes, live executable-image rejection and the unsupported atomic-rename boundary without changing its source or creating its destination. These checks do not make compilation supported.

Raw cohort: `artifacts/sx-20261015/bee-measured-02/`. The preceding graph cohort has means 1.507s/0.530s (64.8% reduction), consistent in direction. Its `allocatedBytes` field incorrectly used `GetCompressedFileSizeW`; those values are excluded from physical-space claims. The final harness fixes the meter and repeats the short cohort. OS and compiler caches were not reset; these few observations do not establish statistical or whole-Unity qualification.

The first standalone attempt retained Unity's parent-IPC flags and was rejected before compiling. A second attempt compiled and verified constants `42 → 43`, but its overlay retained one mapped `1900b0aE.dag_derived` context. Exact-process read-only inspection identified a graph-cache data file, not a compiler DLL. Opening that exact existing file with write-data/read-attributes access, without writing bytes or metadata, then closing it allowed natural drainage. File content/metadata and the complete basis/delta inventories were unchanged, and the independent result survived normal unmount. Neither failed pilot is a successful complete timing sample. See `artifacts/sx-20261015/recovery-pilot02/closure.json`.

The following correctness replay first exposed a prototype error: attempting to create an existing protected directory returned access denied instead of name collision. Returning collision only for an existing protected directory fixes recursive `CreateDirectoryW` without relaxing deletion, replacement or rename guards. On the next replay, the native variant executes 163 of 164 build nodes, including Csc, but fails to publish `TundraBuildState.state` and the digest cache. This is a failed build, not a faster successful compilation.

An exact Win32 experiment isolates the remaining structural limitation:

| Same-directory `MoveFileExW` operation | Observed result |
| --- | --- |
| Relative or absolute view path, no copy fallback | Error 17, `ERROR_NOT_SAME_DEVICE`, for new names and replacement |
| View path with `MOVEFILE_COPY_ALLOWED` | Success with a different file ID: copy and delete, not atomic rename |
| Direct absolute native target path | Success with the same file ID |

The full WinFsp project root and its NTFS graft do not provide the atomic rename semantics this actual Bee backend needs. The former `.NET File.Move` selftest cannot establish atomicity because its copy fallback can mask this boundary. No copy fallback or external-tool modification is adopted. Both diagnostic providers and the failed compile provider close normally with original/source preservation. Evidence: `artifacts/sx-20261015/native-rename-diagnostic-exact/rename.json` and `bee-correctness-04/run-0-native-bee-failure.json`.

Native Bee uses ordinary NTFS buffering and a final output flush, rather than SJL4's per-write durable journal ACK. This durability change is limited to regenerable Bee output; source preservation remains journaled. The mount-point experiment is not an OS security sandbox and does not prove absence of arbitrary foreign data mappings.

## Qualification boundary: SX-04

The bounded log provides a substantial component gain with explicit RAM, temporary-space and write-amplification costs. Production adoption requires the missing metadata/lifecycle/recovery semantics. The NTFS graft is rejected for actual Bee compilation despite any graph-generation gain. Merely loosening path guards cannot fix its cross-volume atomic rename failure.

At the SX gate, alternatives were a physical NTFS project root or native Bee payloads inside the same virtual volume. The latter was subsequently implemented and measured in [BN](bee-internal-native.md), then removed after worse time/storage results. The physical-root alternative was not implemented. These historical alternatives are not current optional paths or approved follow-up work.

A whole Unity speed advantage against the previous 313.1508s shared-Editor workflow remains **NotRun**. Do not replace README workflow timings with these component measurements or add the two prototype speedups together.

## Regression verification

`pwsh -NoProfile -File ./scripts/build.ps1` passes locked restore, Release compilation and the complete test gate: 1,615 passed, zero failed and 24 conditional tests skipped (1,639 total). The final probe-only mkdir/rename follow-up is separately rebuilt and checked with the real mounted selftest; product storage code and defaults are unchanged. Full gate evidence: `artifacts/sx-20261015/build.log` and `full-regression.trx`.
