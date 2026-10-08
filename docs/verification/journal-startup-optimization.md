# Startup journal memory and lookup optimization

[한국어](../ko/verification/journal-startup-optimization.md)

> **Current timing reference:** the record below preserves its original candidate/stage measurements and then-unexecuted boundaries. The subsequent combined `116fd44` candidate is measured in [UF-01](unity-final-lifecycle.md): **18m 03.0s** complete workflow, **3m 36.1s** Editor readiness, **2m 52.0s** A/B preservation/release and **2m 06.0s** Library restoration. UF does not isolate each optimization or qualify the separate unexecuted scenarios below.

**JS-01–04** addresses concrete storage-path findings from the [UC-01 Unity startup review](unity-checkpoint-release-remeasurement.md). The prior Unity readiness measurement remains **848.3379 seconds** on its recorded candidate. This change has small Editor-free qualification, not a new Unity startup timing.

## Retired metadata lifetime — JS-01

A payload location previously retained its complete capsule `Frame`. After metadata retirement, the physical footer and the canonical frame metadata were removed, but payload locations still referenced the old frame, serialized footer and deserialized item arrays. Appending blocks could retain successive growing node metadata snapshots after their disk retirement.

`LatestJournalStorage.Location` now contains only physical path/offset/length and optional current metadata. Metadata reads resolve the canonical frame by path. Payload locations never retain past frames. This preserves current metadata validation and avoids a new payload cache or storage-format change. A weak-reference regression proves retired footer/item arrays become collectible while the writer and original payload remain live; bytes, per-mutation flush and reconnect seals remain correct.

## Affected-file lookup and temporary allocation — JS-02

A file-to-object membership index replaces full-object scans when planning affected capsule retention and moving the old root. Seed loading, ordered replay, replacement, deletion and publication update both maps. Reconnect reconstructs the index from validated storage; it is not persisted or used instead of ownership/content validation.

Footer comparison checks every byte using at most 64KiB of pooled scratch and checks file length before and after. Metadata hashing uses the validated current array directly rather than cloning it. The existing complete hash/checksum checks, one durable flush per mutation, publication/reclamation order, poison handling and recovery boundaries remain. The current SJL4/root v4, overlay v2 and candidate v3 formats are unchanged; no command/schema change is introduced.

## Bounded normal-binary measurement — JS-03

The [dataset](../../reports/benchmarks/journal-startup-optimization-20261008.json) fixes source and binary hashes and all six runs. A normal Release probe creates 128 unrelated private files, then appends 4KiB blocks to one file 256 or 512 times on local NTFS. Setup and final retained-reference/content/seal/reconnect inspection are outside the append timer. Every sample proves exact payload, unchanged unrelated files and one flush per append. Before/after have identical logical put counts and current journal sizes for each input.

The frozen baseline is the `168081c` runtime used by UC-01. The candidate is based on `4989f27` and identified by source/runtime hashes. Order is before256 → before512 → after512 → after256 → after512 repeat → before512 repeat; caches are not cleared. The table uses the two 512-write samples per candidate:

| Metric | Before | Current | Observed reduction |
| --- | ---: | ---: | ---: |
| Reachable footer/item byte arrays | 66.956 MiB | 0.523 MiB | 99.22% |
| Cumulative calling-thread allocation | 554.501 MiB | 383.912 MiB | 30.76% |
| 512 append mutations, mean | 3.880 s | 3.740 s | 3.61% |

The exact retained-array totals are **70,208,305 → 548,599 bytes**. This counts distinct footer/item byte arrays reachable through storage frames/locations, excluding object overhead and total provider/system RAM. Calling-thread allocation is cumulative allocation during synchronous writes, not live heap size. The 256-write pair retains **17,981,848 → 413,218 bytes** and takes **1.7956 → 1.7651 seconds**.

The large retention improvement is established for this append pattern. The small elapsed reduction is an ordered warm observation, not a general speed estimate. Complete current-node metadata is still serialized: the 512-write logical put total remains **31,906,237 bytes**. Logical put counters are not physical disk I/O. Broader metadata representation changes and the original full-workload write amplification remain separate work.

## Validation and remaining boundaries — JS-04

`pwsh -NoProfile -File ./scripts/build.ps1` passed locked restore and Release build with **0 warnings/errors**, then **1,277 passed, 0 failed, 19 conditional skips**. Four new cases cover live-writer retired-reference collection; mixed append/metadata/truncation/partial overwrite/rename/unlinked cleanup and writable reconnect; and footer corruption past the first 64KiB chunk or at its last byte. Existing interrupted-publication/reclamation, corruption, candidate and seal regressions also ran.

An initial sandbox execution rejected `File.Replace` with `UnauthorizedAccessException`. Its incomplete fixture remains preserved. The exact owned failed probe was stopped after PID/start/command verification; successful samples used fresh paths under normal user permissions. No UAC/elevation, service changes or Unity operations were performed. Raw runs, the probe and build results remain under ignored `artifacts/journal-startup-optimization-20261008/`. Existing unrelated dirty files are unchanged.

New Unity readiness/compilation, overall provider/system memory, peak disk and OS failure qualification are **NotRun**. The 448-second Unity scan and Bee graph regeneration remain separate investigation targets. Source timestamps and the raw-Git byte contract were not changed: the 27 reimported source assets differ by original CRLF versus committed LF, so blindly copying original timestamps would be unsound. No current Editor-time improvement is claimed from this small fixture.
