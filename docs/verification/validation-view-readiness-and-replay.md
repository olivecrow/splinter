<a id="첫-준비재연결-시간-검증"></a>
# First-preparation/reconnect timing verification

[한국어](../ko/verification/validation-view-readiness-and-replay.md)

Execution ID `vr-20260927` closed [VR-01–04](../source-view.md) as `scoped-complete-with-limitations`. Starting product baseline `5c5d94f`/VR candidate above it verified. Normal first import/repeated reconnect/preservation/reclamation confirmed, but first render blank, so overall VR-03 Partial. Overall SF-03/general Unity support not promoted to Passed.

Existing isolated NTFS VHD small Unity `V:\sf-compaction-20260926\unity`/new owned `V:\vr27`. Snapshot `{FFE06A10-BF98-4846-B91B-C573B1D53E2A}`, original basis `58f81b5bc6ff8a940d8a90872bd3fed858a54cc1`. No original Library/new cache confirmed. Baseline including shared Git allocation 6,112,144B before worker creation preserved.

Raw evidence `artifacts/storage-foundation/vr-20260927/`: previous binary bundle/manifest, new binaries/tests, individual timings, source/candidate hashes, allocation samples/VSS volume observations separated. Earlier VC failure/recovery/blank previews not overwritten. Summary `measurement-summary.json`; final owned resources/detach `closeout.json`/`final-environment-check.json`.

<a id="판정과-자동-회귀"></a>
## Assessment and automated regression

| Gate | Assessment | Evidence/limits |
| --- | --- | --- |
| VR-01 | Passed, defined scope | 64KiB journal-read buffer, all frame checksum/metadata/sequence validation, read/validation/index counters. Boundary corruption/incomplete tails/historical null-unknown compatibility checked. |
| VR-02 | Passed, defined scope | Wait for latest mounted state/actual mount with same PID/start. Automated failure/generation mismatch/limits/cancellation tests and all 3 actual new reconnects succeed. Three existing-implementation cases returned `OverlayStartUncertain` before readiness. |
| VR-03 | Partial | Empty-cache first import/explicit compilation completion/actual code-asset checks/normal stop passed. One explicit refresh retry on same Editor needed after `EditorFocusRequired`. First PNG only background; same Editor second PNG model. Cause unconfirmed. |
| VR-04 | Passed, defined scope | Same closed source/cache journals, 3 existing/new runs each, hash preservation/actual-source reads/I/O errors 0. Independent Git result/original preservation/file allocation/shared VSS observation/final owned-resource reclamation checked. |

`pwsh ./scripts/build.ps1` locked restore/build/automated tests **296 passed, 0 failed, 1 test excluded at that time skipped**, in `build-contract-final.log`/`tests-296-passed.trx`. Added `JournalReplayTests`, `OverlayReadinessTests`, actual-observation-format `overlay-observation` schema conformance. Ordinary-response/development-overlay schemas both allow new nullable counters; missing historical counters unknown.

First-run sandbox restore restrictions/new test-code error hashing with writer open remain in `build.log`/`build-retry.log`; test writer closed before rerun. Final success not retroactive to failures. Cold Editor Runtime/Core/Windows hashes identical to final comparison; only `Splinter.Protocol.dll` changed from final schema regressions. Distinguished in `code-under-test-final.json`/two current manifests.

<a id="실제-준비와-첫-화면"></a>
## Actual preparation and first image

Worker mount 1.116 seconds, immutable-candidate publication 0.731 seconds, first-view mount 1.835 seconds observed. First two use previous-binary direct CLI, view mount includes PowerShell wrapper, so not speed-improvement comparisons across short stages. Candidate journal 8,038B, initial payload copying/read cache 0.

Unity `6000.3.22f1` start→first ready heartbeat **1,027.510 seconds (17 minutes 7.5 seconds)**, completed without forced termination/cache replacement/import recovery. Separate refresh preserved initial window-activation failure/retried same Editor; completed `success=true`, errors 0 with matching request ID `e8cd93c7e52740288fa1d3980de694c4`/fingerprint. Ready alone did not replace completed validation.

Actual loaded value original 13→candidate **23**, texture `1024×1024`/maxTextureSize 1024, 3 meshes/3,001 vertices/2,441 triangles. First/second observations same PID 2312, Assembly MVID `cc254943-165a-4955-859a-49002423133b`, asset dependency hash. Visual inspection: first `view-a1-preview.png` background only; `view-a1-second-preview.png` model. No intervening code/asset changes/Editor restart. Persistent missing assets not reproduced, but specific cause such as asynchronous shader preparation unproven. First-render success unmet.

Near compilation completion, cache observations record 319,572 flushes/cumulative 726.068 seconds. Cumulative function time at that observation, not independent decomposition of same interval as first-ready wall time. Acknowledged-write flush policy unchanged. Normal Editor stop **30.081 seconds**, subsequent unmount 1.658 seconds. No claim this change reduced first-import writing costs.

<a id="동일-저널의-재연결-비교"></a>
## Same-journal reconnect comparison

After normal stop/unmount, cache `2,268,054,588B`/330,665 frames and source `9,442,536B` fixed. Same mount/control/request/snapshot used for 3 `baseline → current` pairs, both direct `dotnet ... snapshot-spike`. No Editor start/cache copies. Each reads actual code/texture settings/candidate files, unmounts, then verifies both journal hashes unchanged.

| Round | Existing cache replay | New cache replay | Existing actual-mount readiness observation | New actual-mount readiness observation |
| --- | ---: | ---: | ---: | ---: |
| 1 | 23.025 seconds | 17.736 seconds | 24.336 seconds | 18.837 seconds |
| 2 | 19.032 seconds | 14.901 seconds | 19.972 seconds | 15.878 seconds |
| 3 | 19.464 seconds | 14.691 seconds | 20.480 seconds | 15.749 seconds |
| Median | **19.464 seconds** | **14.901 seconds** | **20.480 seconds** | **15.878 seconds** |

Observed replay median decreased **23.44%**, actual-mount readiness median **22.47%**. Existing CLI returned `OverlayStartUncertain` at 16.71–16.99 seconds; measurement continued observing same provider readiness. New CLI succeeded actually mounted at 15.73–18.82 seconds. Timeout-return time is not compared as actual usable time.

New implementation reads entire identical journal in 34,608 calls each run. Median reading 0.613 seconds/validation 2.342 seconds/index application 11.748 seconds; most remaining time index construction. OS cache not emptied, before/after hashes each round, fixed order. Only three small warm-OS-cache repeated observations, not physical disk reads/arbitrary-system improvements/full Unity restart timing evidence.

<a id="저장량독립-결과회수"></a>
## Storage, independent results, and reclamation

Across 185 file-allocation samples, highest observed owned files **2,280,951,448B (about 2.281GB)**. Excludes sample itself/growth after last report/MFT/OS cache/instantaneous-peak bounds.

| Owned path immediately before final preservation | File allocation |
| --- | ---: |
| worker | 8,752B |
| immutable candidate | 12,848B |
| source generation | 9,446,008B |
| common cache | 2,268,058,152B |
| control | 3,388,360B |
| snapshot receipt | 37,328B |

Shared Git allocation **6,112,144 → 6,117,288B**, increase 5,144B. Original Library not created. Measurement-evidence allocation immediately after reclamation 10,617,136B separate, may grow with later document/summaries. Library not double-counted with journal. Isolated VSS volume before reclamation used 27,099,136B/allocated 100,663,296B; after last owned snapshot reclamation 0. Shared-volume observations, not per-workspace attribution.

Read-only checksum accounting: cache live payload 1,829,199,588B/superseded 283,706,123B; source live 8,438,867B/superseded 733B. Logical accounting not converted to physical allocation. No journal compaction in scope.

Independent result retained in fixture Git `codex/vr27/view-a1`, **`fc16f38bb21188c92d52f43fb5471ed7f2f23828`**. Code 23/texture settings 1024/CandidateA file/result ref/object `git fsck` verified before/after VSS reclamation. Original HEAD/development/clean/code-texture-meta hashes match initial values.

Normal-stop owned processes/working-private bytes 0; per-PID/start exit/open handles 0 for worker/first view/6 comparison/final-preservation providers; delta reclaimed after mount/Git-registration removal. Only snapshot `{FFE06A10-BF98-4846-B91B-C573B1D53E2A}` released; other-volume existing `{78AD7813-DF73-4845-8513-F14CC6E87331}` retained. Helper state unreadable in restricted environment corrected to invalid/unknown/actual exit rechecked. Four auxiliary processes ended/VHD detached; existing VHD file/original-independent Git results inside preserved. Not host physical VHD shrinkage.

<a id="남은-범위"></a>
## Remaining scope

First-render initialization cause, detailed index-construction bottlenecks, initial-import writing costs remain. Next timing-improvement candidate is bounded decomposition of measured large index application; implementation/execution NotRun. No grouped-flush changes/online compaction/new default mode/queue-Coordination/source-return merge/large PV resumption.
