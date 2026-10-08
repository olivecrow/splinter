# Current Unity checkpoint and release remeasurement

[한국어](../ko/verification/unity-checkpoint-release-remeasurement.md)

> **Current timing reference:** the record below preserves its original candidate/stage measurements and then-unexecuted boundaries. The subsequent combined `116fd44` candidate is measured in [UF-01](unity-final-lifecycle.md): **18m 03.0s** complete workflow, **3m 36.1s** Editor readiness, **2m 52.0s** A/B preservation/release and **2m 06.0s** Library restoration. UF does not isolate each optimization or qualify the separate unexecuted scenarios below.

**UC-01** records one real Unity A/B cohort after committing the [LO-01–11 optimizations](lifecycle-read-optimization.md) as `168081c349d87d9767d44ce0a741ec61279c2706`. The tested normal Release binaries were frozen before execution. Source and Library inputs match the earlier [UR-01–04 run](unity-lifecycle-remeasurement.md). Raw evidence remains under ignored `artifacts/unity-checkpoint-release-20261008/` and `.local/uc8/`; the [public dataset](../../reports/benchmarks/unity-checkpoint-release-20261008.json) retains exact timings, hashes and qualification boundaries.

## Same input and bounded procedure

Unity **6000.3.22f1**, Git source `b09fa58e9d40a560d4fe1af7a213f8ac23e8e3b1`, **1,602 tracked files / 2,804,772,822 bytes**, `git-snapshot`, default SJL4 and the original Library basis. All **42,420 Library files / 2,440 directories / 3,141,660,252 bytes** have exactly the same manifest metadata and payload hashes as UR. Only basis ownership/path/capture fields differ.

A changes `value + 1` to `value + 2`; B changes `value + value` to `value + value + value`. The existing external fixture confirms each exact project/PID/start identity, fresh noncompiling/nonupdating state, request-matched error-free refresh, expected code values and changed assembly MVID/generation. Installed packages and the probe are unchanged. Two ensures run sequentially, Editor initialization/refresh concurrently, then normal Editor stop, A checkpoint/release, B checkpoint/release, and Library restoration. Checkpoint itself makes no compilation claim.

## Checkpoint and release

These command times begin **after compilation and normal Editor exit**. They include independent result preservation and owned resource reclamation, but exclude setup, Editor shutdown and original Library restoration.

| Operation | Prior UR, s | Current UC, s |
| --- | ---: | ---: |
| A checkpoint | 83.5010 | 66.7514 |
| A release | 130.5857 | 88.2598 |
| B checkpoint | 81.6405 | 65.1359 |
| B release | 125.6494 | 84.7432 |
| A/B checkpoint + release | 421.3806 | 304.8956 |

The sequential preservation interval is **5m 04.9s**, versus **7m 01.4s** previously: 116.5 seconds (27.64%) shorter as observed. This is one cohort per candidate without cache clearing or randomized repeats. **UR included external EventPipe profiling; UC does not**, retaining built-in source/seal/events and ten-second read-only provider samples. The difference is not an isolated causal optimization percentage. A/B results are two independently verified tasks, not a statistical sample of arbitrary Unity projects.

## Separate preparation, shutdown and restoration costs

| Stage | Prior UR, s | Current UC, s |
| --- | ---: | ---: |
| Library preparation | 240.3283 | 153.1848 |
| A ensure | 322.3461 | 238.4172 |
| B ensure | 319.4375 | 232.4926 |
| Initial compiled readiness | 1041.8831 | 848.3379 |
| Edit to compiled readiness | 76.9129 | 65.2544 |
| Normal Editor shutdown | 278.3213 | 262.0236 |
| Library restoration | 1404.9756 | 180.9024 |
| Complete prepare-to-restore interval | 4107.9591 | 2286.9492 |

Initial compiled readiness is **14m 08.3s** and the complete preparation-to-restoration interval is **38m 06.9s**. Initial readiness remains a substantial cost; it includes scanning/importing/compilation, not independently measured compiler CPU time.

Both `unity stop` commands again return **`EditorStopTimeout` at about 120 seconds**. The same requested Editors exit normally after **262.0236 seconds**, without another stop request, forced termination or restart. Failed responses remain evidence. The four checkpoint/release commands and final Library restoration all succeed; this does not qualify the stop command's two-minute limit as successful.

The original Library retains durable sequence zero. Its new directory plan fixes **44,860 root/descendant identities**, and full identity/content/membership checks and metadata restoration precede reclamation. The directory mode is actually used and restoration takes **180.9024 seconds (3m 00.9s)**, versus 23m 25.0s previously. This single unchanged-Library run does not qualify changed-Library directory restoration or OS power-loss recovery.

## Built-in cost attribution

| Command | Source inspections, s (count) | Outer seals, s (count) | Delta reclamation interval, s |
| --- | ---: | ---: | ---: |
| A checkpoint | 25.218 (2) | 27.887 (2) | — |
| A release | 12.393 (1) | 31.912 (3) | 34.215 |
| B checkpoint | 24.484 (2) | 27.280 (2) | — |
| B release | 12.148 (1) | 32.076 (3) | 34.726 |

These are nested wall-clock measurements already included in command totals. Reclamation includes its inner seal and is separate from the outer seal measurements. Checkpoints hash only their changed source file (A 143 bytes, B 152 bytes); release hashes no source payload. Source metadata/Git guards still take roughly 12 seconds per inspection. No additional CPU/disk trace or code instrumentation was inserted.

## Closure and evidence limits

Both tasks are `released`; independent result trees exactly match the historical Codex A/B oracles and each changes only its intended source file. The two owned Editors and all three providers are absent by PID/start identity. Providers are unmounted with zero open handles; task deltas, mounts and owned Git registrations are absent. Original source hashes/HEAD/branch/preexisting refs remain unchanged. Library is ordinary NTFS on its original volume; the fixture-local temporary Git setting is restored, and global configuration is unchanged.

All **12 actual CLI responses**, including the two stop failures, pass the current response schema through the existing validator. The premeasurement required suite recorded **1,273 passed, 0 failed, 19 conditional skips**; frozen binaries remain unchanged. Existing unrelated dirty files are preserved. No new Unity Play Mode, Unity tests, game build, Codex run, allocation/RAM/whole-disk peak, UAC/service change or OS-failure matrix was performed. Historical allocation savings remain historical.
