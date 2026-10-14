# Full Unity remeasurement after RAM and preparation optimization

[한국어](../ko/verification/unity-time-memory.md)

**UT-01** repeats the actual UF Unity workload at `7630970`. The full interval is **14m 35.2s**, versus **18m 03.0s**, an observed **19.19%** reduction. [Exact dataset](../../reports/benchmarks/unity-time-memory-20261014.json).

Unity 6000.3.22f1, Git `b09fa58`, 1,602 tracked source files / 2,804,772,822 bytes and 44,860 Library entries / 42,420 files / 3,141,660,252 bytes. Source hashes and the original RB Library content/metadata all match. No assets were removed. A/B make the same one-line edits, proving compilation by exact project/PID/start, new generation/MVID, a successful matched refresh request and expected values.

| Stage | Previous UF | Current UT | Observed reduction |
| --- | ---: | ---: | ---: |
| Library basis prepare | 1m 55.9s | 1m 06.6s | 42.52% |
| A/B workspace creation cohort | 5m 32.7s | 2m 32.9s | 54.06% |
| Editor launch to compiled readiness | 3m 36.1s | 4m 02.7s | -12.35% |
| A/B one-line edits to recompiled readiness | 0m 29.9s | 0m 48.1s | -60.91% |
| Selected asset inspection | 0m 02.4s | 0m 01.4s | 41.54% |
| A/B normal shutdown | 1m 27.9s | 1m 45.5s | -20.07% |
| A/B checkpoint + release | 2m 52.0s | 3m 22.7s | -17.85% |
| Original Library restore | 2m 06.0s | 0m 55.0s | 56.31% |
| Complete workflow | 18m 03.0s | 14m 35.2s | 19.19% |

Individual A/B creation times are 151.244s / 152.851s and overlap; do not add them. UF was sequential, UT is parallel. This comparison combines RAM caches, Library path validation and concurrent dispatch; it does not isolate their causal contributions. Preparation, Editor shutdown and original restoration are separate from checkpoint/release.

Editor readiness, recompilation and shutdown were slower in this run. A logs show initial script compilation of 103.350s → 128.857s, initial Tundra build of 55.75s → 74.21s, and post-edit Tundra build of 9.74s → 19.14s. These locate observed slower work; one cohort cannot attribute it to the cache changes or OS state.

| Operation | UF | UT |
| --- | ---: | ---: |
| A checkpoint | 37.671s | 44.592s |
| A release | 49.392s | 57.716s |
| B checkpoint | 37.371s | 43.893s |
| B release | 47.585s | 56.520s |

Against the first RB post-Editor measurements, preservation/release changes from 156m 07.1s to 3m 22.7s, and restoration from 24m 33.2s to 0m 55.0s. Original commands overlap and include diagnostics/recovery and an external B commit; do not sum them or invent an original full preparation interval.

Verified all 1,602 original files, unchanged HEAD/branch/preexisting refs/global settings, restored temporary local Git configuration, independent result trees equal to historical expectations, owned Editor/provider exit, mount/delta/Git-registration reclamation and ordinary NTFS Library restoration. All 12 CLI responses pass the current schema; all frozen runtime file hashes match before/after. One cohort, without clearing OS caches or randomizing order. No additional Play/Test/build, service/VSS change, whole-process peak RAM or allocation comparison was performed. The existing project qualification fixture and installed dependencies are retained, without new optional external-tool qualification.
