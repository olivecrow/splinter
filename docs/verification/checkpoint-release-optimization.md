# Checkpoint and release optimization

[한국어](../ko/verification/checkpoint-release-optimization.md)

This document owns **CO-01–08**, the bounded 2026-10-07 optimization following [LP profiling](checkpoint-release-profiling.md). The first [measurements and candidate hashes](../../reports/benchmarks/checkpoint-release-optimization-20261007.json) distinguish the original normal distribution from the CO-01–04 optimized distribution, both based on `74518fd`. CO-05–08 below measure a further candidate against that optimized distribution. The earlier Unity timing remains historical evidence.

Subsequent real Unity costs, profiles and cleanup are recorded separately in [UR-01–04](unity-lifecycle-remeasurement.md). Large-Unity NotRun statements in the CO records below describe those earlier tiny measurements.

## Changes and safety

- **CO-01:** Replace 50-path Git attribute subprocesses with UTF-8 NUL stdin batches targeting 256 KiB. Preserve current/cached filter and encoding checks, before/after interpretation digest bytes, HEAD/index/path membership and provider-generation checks. For 1,602 paths fitting one input batch, attribute process count per full inspection changes from 132 to 4; this is a code-derived count, not a new large-project timing.
- A fresh range-journal checkpoint avoids initial and final response-only source scans. Resource readiness and stopped application ownership still gate the operation; the source adapter verifies checkpointing, followed by sealed independent collection. Existing-revision retries still inspect current source. The final response accurately says `sourceVerification=not-checked` and omits `source`; verified result revision/fingerprint/seal remain in `workspace.snapshot.result`.
- An unchanged release reuses its stable independent result only with an exact saved pre-seal, one fresh clean revision/fingerprint inspection and unchanged post-seal. Independent ref/object validation and post-unmount seal remain. Changed generated data/infrastructure takes full collection; source mismatch, corrupt results or concurrent writes preserve the task.
- **CO-04:** Index already-open reclamation fence paths with a HashSet, using the same full-path normalization and OS case comparison as `WorkspaceStore.SamePath`. Retain file existence checks, all handles, both inventory validations, hash-before-delete, durable receipts and same-intent recovery. Lookup work becomes expected linear rather than quadratic. No journal format, flush/ACK policy or inventory observation cache changed.

## Bounded before/after evidence

**CO-03:** One ordered run per normal binary on the same source commit: **64 tracked files / 5,510 B**, **256 synthetic Library files / 65,536 B**, `git-snapshot`, Unity directory policy and default SJL4. Each task changes one source line and one byte in 128 private Library files. No Unity Editor or real asset import runs. Both use separate owned homes and identical original source/Library bytes; build and full tests finished before the after measurement. No OS cache clearing or extra profiling scopes. Process startup and up to 100 ms driver polling are included; each command has a 240-second observation bound without forced termination/retry.

| Operation | Before, s | After, s | Reduction | Source inspections before → after |
| --- | ---: | ---: | ---: | ---: |
| Checkpoint | 42.3045 | 31.8774 | 24.65% | 7 → 5 |
| Release | 20.6558 | 14.5439 | 29.59% | 2 → 1 |
| Combined | 62.9604 | 46.4213 | 26.27% | 9 → 6 |

Source inspection totals were 33.8403 → 24.2087 seconds for checkpoint and 10.0437 → 4.7185 seconds for release. Checkpoint retained 2 outer seal measurements; release retained 4. These nested times are included in command totals. The result source fingerprints match across candidates.

The CO-04 one-pass microprobe used the same 1,024 real one-byte NTFS files and open streams for the old then indexed lookup, after a 32-file warmup. It measured **760.3767 → 63.2675 ms**, including index construction. Comparisons changed from **524,800** to **1,024 indexed lookups**, plus 1,024 index insertions. This is lookup evidence, not whole release time; order/cache/JIT variation prevents extrapolating to the historical Unity delay.

## Regression, closure and limits

**CO-02:** `pwsh ./scripts/build.ps1` passed locked restore, Release build with **0 warnings / 0 errors**, and **1,160 passed / 0 failed / 19 skipped** tests. The 26 added cases cover NUL/Unicode batching across the byte limit and literal argv equivalence; current/cached unsupported attributes; response truthfulness; unavailable/interrupted-release readiness; source/revision/fingerprint/result changes; writes overlapping verification; changed-journal full collection; multi-file interrupted reclamation, foreign-file preservation and Windows writer fences. The existing recollection interruption test now explicitly changes the journal before injecting its preservation write. Ten actual successful before/after CLI responses also passed the existing response-schema validator in a separate single-test run.

Initial before Library preparation failed under the filesystem sandbox before provider readiness. The same identity/request resumed successfully at ordinary user permissions; this setup refusal is retained and excluded from checkpoint/release totals. The build script also required ordinary-user execution outside the sandbox after restore failed there.

Both tasks reached `released`, retained the one-source-file independent result, and observed owned mount/provider/handles/memory/delta absent. Original source hashes (64 files), Library hashes (256 files), clean HEAD and removal of owned basis refs were verified after each run; Library returned to ordinary NTFS. The existing Unity project and its earlier results were untouched. Raw requests/responses, binary copies, receipts, test evidence and measurement scripts remain in ignored `artifacts/lifecycle-optimization-20261007/` and `.local/lifecycle-optimization-20261007/`.

This is a single tiny workload per candidate, not a median or SLA. **New large Unity lifecycle timings, Editor/import/compile/Play/Test/build, VSS, OS interruption, whole-disk peak and RAM attribution are NotRun/unknown.** At CO-01–04, per-file source metadata checks, seal/hash passes, ignored-tree traversal on full preservation, periodic journal inventory observations and provider request polling remained. CO-05–08 below address some of those remaining costs. The historical 2h 36m Unity preservation/release sequence is not relabeled as optimized or resolved by these measurements.

New native plain-folder/Plastic, changed-journal fallback, interruption and peer execution are also NotRun for this candidate. Their automatic/fake regressions are distinguished from earlier native qualifications.

## Follow-up: removing repeated work (CO-05–08)

The [follow-up dataset](../../reports/benchmarks/checkpoint-release-followup-20261007.json) records a new baseline using the exact CO-01–04 optimized binaries, an intermediate candidate and the final candidate. The earlier costs above are preserved rather than combined across separate runs.

**CO-05 — checkpoint transaction:** Git checkpoint and independent ref preservation share an operation-local source proof inside Runtime's pre/post seal boundary. Ordinary edits require two full source inspections instead of five. Staging/pinning still checks provider generation and path-change history, HEAD/index/path membership, attributes/config interpretation, managed index exclusions and the exact committed tree. Missing tracked paths retain a full staged inspection, including deleted `.gitattributes` interpretation. Failed pinning, lost replies and changed seals preserve the prior successful manifest and allow the same task to retry. Literal managed roots are excluded from Git's ignored-file traversal; wildcard policies and unowned ignored files still receive their existing guards. No persistent proof or state-format migration is introduced.

**CO-06 — journal and request costs:** volume-size callbacks read the exclusive writer's byte counter; heartbeats use explicitly labeled writer accounting. Reconnect rebuilds it from actual files. Explicit observations, stopped state and seals still inspect files; a poisoned writer reports unknown activity and retains live ownership/handles. SJL4 duplicate-generation and payload-path lookups use sets/dictionaries, while complete checksums, replay, identity and seal content remain. Fresh SJL4 reclamation avoids a duplicate outer seal by fixing the saved expected seal in an owned intent and requiring the inner reclaimer's full fresh seal before deletion. OFF/missing-current-journal and interrupted recovery retain their existing checks. Source/flush/stop publications wake the provider immediately; the one-second heartbeat and missed/unavailable-notification polling remain. Notifications cannot authorize a request. Per-mutation durable flush/ACK is unchanged.

### Offline journal costs

One stopped SJL4 input per size, generated once, measured by separate .NET processes in before→after order. Each private file contains one byte. Physical filename/length/SHA-256 manifests and complete seals remained identical. These are intermediate storage-candidate measurements; subsequent source-router changes leave this journal code unchanged, but the final binaries were not separately microbenchmarked. Constructor and seal timings are separate, and seal includes another constructor. The 16-query column compares the old actual-file `Bytes` getter with `CommittedBytes`, not a whole callback or command.

| Private files / retained backup files | Read-only constructor, ms before → after | Full seal including constructor, ms before → after | 16 byte-count queries, ms before → after |
| --- | ---: | ---: | ---: |
| 512 / 511 | 593.9489 → 535.8092 | 612.3981 → 537.9753 | 274.4681 → 0.1719 |
| 2,048 / 2,047 | 2,418.8014 → 2,068.0361 | 2,178.6386 → 2,033.5423 | 1,081.2229 → 0.1642 |

These are single ordered observations including possible cache/JIT effects. Removing repeated inventory work is substantial for live byte-count queries; full hash/replay costs remain. The small constructor/seal reductions do not establish large-project scaling or a Unity timing estimate.

### Final product measurement (CO-07)

The same fresh fixture contains **64 tracked files / 5,510 B** and **256 synthetic Library files / 65,536 B**, with `git-snapshot`, Unity directory policy and default SJL4. Each candidate edits the same source line and one byte in 128 private Library files. No Editor runs. One ordered run per candidate, separate owned homes, no cache clearing or injected profiler. Each CLI observation is bounded to 120 seconds without forced termination/retry; startup and up to 100 ms driver polling are included. Full tests completed before the final normal-binary run.

| Operation | Prior optimized binary, s | Intermediate without request wakeup, s | Final, s | Reduction versus prior optimized binary |
| --- | ---: | ---: | ---: | ---: |
| Checkpoint | 28.6560 | 21.1214 | 14.0870 | 50.84% |
| Release | 13.6807 | 11.8697 | 9.6596 | 29.39% |

Checkpoint/release combined: **42.3367 → 23.7466 seconds (43.91%)**. Full source inspections changed from 5 to 2 for checkpoint; release retained 1. Separate setup/restoration costs were Library prepare 7.7513 → 7.6478 s, ensure 27.9540 → 24.6339 s, and Library restore 9.3588 → 8.7554 s. Intermediate timings are retained, not added to final command time. The wakeup-only intermediate checkpoint took 17.9085 s and release 9.9637 s; its four source inspections exposed missing capability forwarding in the CLI source router. The final candidate forwards only supported selected-provider operations, retaining the existing None/Plastic paths. The source result fingerprint matches across all candidates.

### Regression and closure (CO-08)

`pwsh -NoProfile -File ./scripts/build.ps1` passed locked restore, Release build with **0 warnings / 0 errors**, and **1,203 passed / 0 failed / 19 skipped** tests. Twenty actual CLI responses passed the existing response-schema validator in one separate test. Added coverage includes source rewrite/revert, rename, provider epoch/unknown, config/attributes, commit-hook tree substitution, literal/wildcard ignored policy, source-router capability selection, after-pin failure and same-task/no-op retry, lost reply with a prior manifest, overlapping seals, accounting/reconnect/poison, duplicate journal generations, failed/partial reclamation, notification publication/replacement and unavailable-notification polling. A transient signal publication sharing failure in the existing interruption harness was corrected by using the existing delete-sharing/bounded IPC reader; final tests passed without changing journal durability behavior.

All four tasks reached `released` with the expected one-source-file independent result, absent mounts/deltas and zero owned process/memory/handle observations. Every original source/Library hash and clean HEAD were verified, owned basis refs were removed, and the fixture Library returned to ordinary NTFS. **No Administrator elevation, UAC, service installation/update or existing Unity-project change was used.** Raw scripts, requests/responses, frozen binaries and closure evidence remain in ignored `artifacts/lifecycle-followup-20261007/`, `.local/lifecycle-followup-20261007/` and `.local/lco7/`.

These are bounded single-run Editor-free observations. The historical large Unity 2h 36m sequence was **not rerun**; per-file source metadata, full seal/hash and independent-object graph checks still cost time. New Unity import/compile/Play/Test/build, native plain-folder/Plastic/peer/interruption matrices, VSS, whole-disk peak and RAM attribution remain **NotRun/unknown**. Automatic/fake protections do not transfer earlier native qualification to this candidate.
