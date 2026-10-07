<a id="실행-파일-묶음의-재사용과-준비-메모리"></a>
# Executable bundle reuse and preparation memory

[한국어](../ko/verification/runner-bundle-memory.md)

This unit is scoped to **reducing managed allocation in the executable preparation function while retaining disk reuse for the same home/binaries**. Exit criteria are existing content keys, identical-file reuse, corruption/loss preservation, concurrent publication, actual Windows supervisor process regression, and before/after comparison with a fixed probe. It excludes Editor preparation time reduction, new cache reclaim/automatic cleanup, and Unity/VSS testing.

<a id="범위와-입력"></a>
## Scope and input

- Based on `development @ 2aae895` and the current priority document. Product changes are limited to `WindowsProcessWorkspaceProvider.PrepareRunner`; common schema, state/IPC, privilege service, and process ownership contracts remain unchanged.
- The existing product already shares bundles in the same home using a content key combining ordered SHA-256 hashes of 6 files. Historical runner allocation growth in [compaction cost](journal-compaction-qualification-and-cost.md) is not inherited as a repeated-copy defect for current identical binaries.
- Before/after Release executables and the identical development probe are preserved at `artifacts/storage-foundation/runner-20261001-223559-965/`. Evidence includes `plan.json`, `before-binaries/`, `after-binaries/`, `probe/`, `before.json`, `after.json`, and required build logs/TRX. The probe is not a product command/module.
- The probe creates one new owned cache and reuses preparation of the same candidate 8 times. It observes `GC.GetAllocatedBytesForCurrentThread` differences within each call and actual `FILE_STANDARD_INFO.AllocationSize`. It also checks full executable content hashes, file count, identical paths, and before/after file allocation.

<a id="현재-동작과-확인-조건"></a>
## Current behavior and verification conditions

The current 8 original files, including file/Git source DLLs, are opened with read-only handles that prevent writes/replacement until hashing/copying finishes. Hash key component order is unchanged; new copies and cache hash verification use fixed-size streaming. Original handles close after return/exception. If the cache is corrupt or a file is missing, its directory is preserved and rejected. Existing caches are not automatically overwritten and old build directories are not deleted.

`RunnerBundleTests` verifies existing keys, identical paths/mtimes and bounded managed allocation, complete single publication from 8 concurrent preparations, rejection of same-length corruption in each of the 8 files, preservation of missing files, and release of the original config handle after success/failure. Existing `WindowsProcessTests` covers actual supervisor execution/owned-child cleanup and non-owned sentinel protection. No new Unity/power/full native failure matrix is opened.

<a id="결과와-제한"></a>
## Results and limitations

The actual allocation probe below is evidence for the historical 6-file candidate. It is distinguished from automatic regression of the current 8-file bundle and is not inherited as a direct memory measurement for the current candidate.

These are 8 reuse results from the same probe. Full content hashes of before/after binaries follow the preserved manifest/JSON.

| Observation | Before | After |
| --- | ---: | ---: |
| Logical bytes of the 6-file bundle | 1,001,668 | 1,001,156 |
| Bundle file allocation immediately after creation (B) | 1,012,056 | 1,012,056 |
| Bundle file allocation after 8 reuses (B) | 1,012,056 | 1,012,056 |
| Average managed allocation per preparation call (B) | 2,024,404 | 20,976 |

Reuse managed allocation in the current function **decreased by 98.96%**. All 8 after-change runs were 20,976B. Existing disk reuse was retained; no additional physical-storage savings are claimed from this change. The 512B binary logical-length difference is not interpreted as total physical-storage optimization.

Required `pwsh ./scripts/build.ps1` locked restore and Release had 0 warnings/errors, **754 total / 753 passed / 0 failed / 1 test excluded at the time skipped**. Final TRX confirmed success of 9 new `RunnerBundleTests` and existing `WindowsProcessTests.SupervisorSurvivesClientAndCleansDescendantThatOutlivesRoot`. This is evidence of actual supervisor execution from the same bundle, owned child/memory 0, and surviving non-owned sentinel; it is not new Unity qualification.

This preparation-memory unit is closed as **scoped-complete-with-limitations**. `source-before.cs`, `source-after.cs`, `source-and-binaries.json`, `result.json`, and final `automated.trx` are preserved. It completed without service installation/updates, fixture changes, new NTFS workspaces, Editors, or VIVOBOOK execution; existing caches/raw results were not deleted.

Measurement is managed allocation on the function's current thread, excluding RAM of games/other processes. Cumulative allocation reduction is not interpreted as a residency/working-set or Unity Editor/whole-system RAM reduction rate. File allocation covers only this 6-file bundle, excluding directories/MFT, VSS, OS cache, and whole-workspace peak. Binary size differences themselves are code-change cost and are distinguished from reuse savings.

Current-candidate verification is limited to the preparation function and actual Windows process-group boundary. Prior M0 and Unity/WinFsp/VSS candidate records are not inherited as new native success for these binaries in full, and no whole-memory comparison on a system running a game is performed.
