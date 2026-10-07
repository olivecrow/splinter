<a id="폐기한-refs-기준본의-과거-검증"></a>
# Historical verification of the retired ReFS basis

[한국어](../ko/verification/storage-and-resources.md)

> **Historical record of a retired feature.** Do not use it as current functionality, a development route, or a follow-up task. Reasons and scope follow the [decision source](../plans/refs-retirement.md).
>
> This is internal-alpha evidence under conditions where the initial full ReFS basis was prepared. It does not pass SO gates for no initial NTFS copying, no read copying, or snapshots with concurrent original edits. About 297MiB is A-creation growth after basis preparation.

Internal-alpha verification from 2026-09-21. It checked avoidance of repeated physical copying of large files and absence of owned processes/memory after completed worktree reclamation. It does not pass all public scenarios or performance SLAs. A small machine-readable result bundle is in [execution evidence](storage-and-resources.evidence.json). Raw JSON is stored in ignored `artifacts/resource-alpha/`.

<a id="환경과-소스"></a>
## Environment and source

- Windows 11 Home build 26200, .NET SDK `10.0.112`, Unity `6000.3.22f1`, Coplay `10.2.0`.
- Original fixture/common Git directory: NTFS `../splinter-unity`. Original development is `66820470038c85319bc4d47d1ff2859cab82e928`; HEAD, index, and working files were preserved.
- A user-approved new dynamic 64GiB VHDX was created at `.local/dev-drive/splinter.vhdx` and attached as **S: ReFS Dev Drive**. Cluster size is 4096 bytes. Existing partitions were unchanged; no startup mount was registered.
- Test home is `S:\splinter-resource-alpha`; large automated tests use `S:\splinter-tests`. Product baseline commit, modified-source digest, and executable/TRX hashes at execution are in machine-readable evidence.
- Test commit `79a367c27c3419d37758af67d454652f901dc58c`, differing by only one script, was created in separate `splinter/resource-cache-seed` ref. Original checkout/index/development/main were unchanged.

<a id="결과"></a>
## Results

| ID | Test | Status | Evidence and scope |
| --- | --- | --- | --- |
| SR-01 | Reclaim child outliving root and supervisor | Passed | Actual Windows job, PowerShell root/64MiB child/separate sentinel. Reobservation shows owned processes/memory 0; sentinel alive |
| SR-02 | Block worktree deletion with remaining child/unknown ownership | Passed | Runtime fake. Source release not called; reclaimed after state recovery |
| SR-03 | Block full-copy fallback when CoW unavailable | Passed | Actual NTFS probe/fake Runtime. Rejected before Git creation |
| SR-04 | No-checkout worktree, independent index, incremental source application | Passed | Actual temporary Git + fake clone. Only `.git` before creation; no duplicate clone on reconnect; only changed files updated for new revision |
| SR-05 | Basis validation/change detection/consumer protection | Passed | Fake/file tests and actual active seed `BaselineInUse` rejection. Sealing without valid verification and deletion of changed cache blocked |
| SR-06 | Actual large ReFS clone/nested clone/write isolation | Passed | 512MiB+3 bytes, aligned files, 0-byte files/empty directories. B cloned from A also preserved; hashes/additional-allocation upper bound checked |
| SR-07 | Two concurrent GUI Editors with warm Library | Passed | Distinct A/B PIDs, GUI windows, control paths. During A editing/compilation, B/original/basis source and B/original assembly hashes retained. B validation also succeeded |
| SR-08 | Reclaim actual Unity process group/worktree | Passed | Each A/B has releasedResources evidence of 0; all 40 observed owned processes exited; both folders/Git registrations removed. Original Editor/MCP retained; actual calls succeeded |
| SR-09 | Reuse basis for changed-source commit/seal/explicit GC | Passed | Actual changed-source commit prepared with refs-cow, compiled, sealed. Preparation Editor/folder reclaimed/new cache deleted; requesting existing basis does not warm again |
| SR-10 | CLI exit/output completion while Editor remains alive | Passed | Final supervisor implementation A/B start responses 1,979/2,054ms. Includes actual Windows regression closing inheritable anonymous pipes |

`pwsh ./scripts/build.ps1` with `SPLINTER_TEST_REFS_ROOT=S:\splinter-tests` completed locked restore, 0 warnings/errors, and **56 passed / 0 failed / 0 skipped**. Runs omitting the ReFS path at that time treated SR-06 as Skipped. Those tests/environment-variable wiring are now removed. Actual GUI verification is outside this automated-test count. Previous ordinary-checkout [AA verification](README.md) and this CoW evidence are also distinct.

<a id="저장소-측정"></a>
## Storage measurements

After initial cold preparation was verified and stopped, source/Library were sealed in basis `51f7ee1d3abfc7c859cee60b`. The original's running Library was not used.

| Item | A | B |
| --- | ---: | ---: |
| Logical file bytes | 2,017,545,775 | 2,017,545,775 |
| Successful native clone bytes | 1,963,737,088 | 1,963,737,088 |
| Tail/small-file copy bytes | 53,808,687 | 53,808,687 |
| File count | 41,604 | 41,604 |
| Clone-call time | 51,741ms | 63,199ms |
| Total ensure time | 78,885ms | 91,113ms |
| Total normal release time | 13,395ms | 12,226ms |

About **97.3% of file-content ranges** used native block cloning. This is not a physical-disk saving rate. S: free-space decrease before/after A creation was **311,017,472 bytes (about 296.6MiB)**. Beyond about 1.88GiB of logical files, file/directory metadata and small writes have costs. B measurement overlapped A Editor startup, so it is not an independent sample for disk-growth comparison.

Quiet measurement in the large synthetic test showed **557,056 bytes** of additional allocation for two clones totaling about 1GiB. The same test in the final build also passed physical-allocation bounds/write isolation. Values by execution time are separated in evidence JSON. Free-space figures immediately after deletion include ReFS deferred reclamation and do not mean the VHDX file shrinks immediately. Preparation time is one fixture observation; no speed multiplier over ordinary copying or large-project SLA is claimed.

The basis was sealed before discovering a CRLF/LF compatibility-key issue during development. After checking that the old key matched the exact version/bridge content, only this test cache's metadata key was normalized. Tree content/manifest were unchanged. New implementation generates normalized keys initially. This one-time test-data correction is not evidence of general cache-migration support.

<a id="종료-상태와-다음-범위"></a>
## End state and next scope

A/B end results are `ownershipVerified=true`, `rootRunning=false`, `activeProcesses=0`, `workingSetBytes=0`, `privateBytes=0`. This neither clears OS file cache/standby RAM nor equalizes whole-PC usage. Original Editor PID `25756` and MCP PID `19096` were retained; MCP confirmed `Increment(3)=4`, `Double(3)=6` after reclamation.

All four worktrees—initial preparation, A/B, incremental preparation—are `released`; all paths and owned roots/supervisors are absent. Final home workspaces are empty; only the original is registered in Git. Incremental basis `b285120ac7846a909ecc09c7` cache was explicitly deleted. Requesting the initial basis returns its existing ID.

At test end, one initial basis and small records/logs/result refs remained on disk. Current state after the user's subsequent cleanup request reclaimed the basis/test VHDX is in the [cleanup record](README.md). The basis itself has no resident process. Cache deletion at that time was an independent contract of a separate command. Derived files created during execution are not forcibly cleaned together with user results.

Product NTFS range-storage, original/A/B Unity, reconnect/failure-protection evidence, actual dirty-original integration blocking, and completed final VSS reclamation are in [NI verification](ntfs-workspace-integration.md). SO-01–06 remain separate fixture evidence. SR results are preserved for their narrow scope at execution.
