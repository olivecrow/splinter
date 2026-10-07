<a id="첫-미리보기-검사-검증"></a>
# First preview inspection verification

[한국어](../ko/verification/first-render-qualification.md)

[Fixed FR-01–04 scope](../unity-render-qualification.md) closed as **scope complete with limitations retained (`scoped-complete-with-limitations`)**. Raw evidence is `artifacts/storage-foundation/fr-20260927/`, aggregation `qualification-summary.json`; preservation/reclaim/reverification after VHD detach in `closeout.json`/`final-environment-check.json`. The small URP fixture's inspection-success contract closed; cause of Unity's internal first-draw difference unresolved.

| ID | Status | Evidence |
| --- | --- | --- |
| FR-01 | Passed, bounded cause separation | Baseline cold first/subsequent PNGs and warm-domain API/shader/consecutive-draw comparison. Initial diagnostic-image interpretation corrected; native cause unresolved |
| FR-02 | Passed, fixture scope | First valid image selected within one request, maximum 2 attempts, per-request publication/pixels/hash. New cold `[0,9457]` recorded; valid PNG returned |
| FR-03 | Passed, fixture scope | 3 first requests for new cold/reload/normal restart, 3 failure injections/3 normal recoveries, original scene/previous PNG preservation |
| FR-04 | Passed | 2 independent results, 90 original files/25 existing refs/other-volume snapshot preserved, owned resources 0, results reverified after VHD detach |

<a id="기준과-공통-실행-파일"></a>
## Basis and common executables

Product task basis `a1c3f91`, canonical fixture basis `ae89b9f`. New execution original is clean `58f81b5bc6ff8a940d8a90872bd3fed858a54cc1` of existing small Unity, with 90 tracked files/25 existing refs/no Library. Existing-result file/ref counts not reused for new basis. Independent `fr27-baseline`/`fr27-candidate` deltas on same new owned snapshot; Editors sequential. Unity 6000.3.22f1/URP 17.3.0.

Both actual tasks use the same CLI/Windows/Git/Core/Runtime executables fixed in JW (`binary-manifest.json`). Built before JW commit, so informational version retains `091a5fc`. Rebuild after `a1c3f91` has different PE hashes, but method-body/field-value hashes of 8 common managed DLLs match (`post-commit-version-stamps.json`, `post-commit-method-body-comparison.json`). Not a whole-PE hash equivalence claim.

Standard `scripts/build.ps1` locked restore/Release build/tests: **303 passed, 0 failed, 1 test excluded at the time skipped**. Initial restricted-environment restore exit 1 without detailed diagnosis; permitted rerun of same entrypoint passed. Both logs/TRX preserved. Baseline Unity initial preparation partly overlapped product automatic tests, so initial preparation time not used for performance comparison.

<a id="재현과-수정-근거"></a>
## Reproduction and fix evidence

Baseline new-Library first ready 1,084.00 seconds. First PNG from same Editor PID 22880 showed only background, but existing response was `success:true`; next PNG showed model. 3 meshes/2048×2048 texture/compile ready did not guarantee a valid image.

Then compared first draw after each domain reload with new code value/MVID in same Editor. **All final PNGs and saved intermediate-pass PNGs for 9 diagnostics showed the same model image**, covering `Camera.Render`, `Camera.Render` after support check, `SingleCameraRequest`, explicit asynchronous-shader options, and consecutive draws. SHA-256 `e208015437e22a9b9df0fa800e90dd0f57f61e238b4f667ad9c9fcd3dfe396dc`, same as baseline second PNG. Pipeline observed URP before/after; asynchronous option observed false. Diagnostics in already prepared Editor could neither reproduce cold blank image nor distinguish API/shader variants.

Intermediate visual classification of some diagnostics as blank/pass differences was corrected by final raw-image/hash comparison (`diagnostic-evidence-reconciliation.json`). Raw PNGs unchanged. API replacement/shader settings therefore not established causes. Actual blank-image evidence is baseline cold first PNG and modified cold request foreground pixels `[0,9457]`; Unity internal cause unresolved.

Verified fixture defect: **judging compile-ready, loaded meshes, and saved PNG as screen success**. New inspection uses fixed solid background/separate camera scene/manual URP request, clears GPU target first, then verifies actual pixels. Selects first valid image within at most 2 attempts, recording each count. Fails when both blank or references/background/publication invalid. Publishes new request-ID-bound PNG/SHA-256; preserves prior results. Current contract follows [preview inspection](../unity-render-qualification.md).

First inspection loading fix into same baseline Editor as new code 63/MVID verified model, 9,457 foreground pixels, 1 render pass, original scene restoration. Does not replace separate new-Library verification. First baseline shutdown CLI returned `EditorStopTimeout` after 31.78 seconds. Subsequently rechecked natural Editor exit/owned processes/memory 0, without forced termination. Preserved independent bundle/repository of baseline result `8f409c11f23bc146d1df56670d9595d6ea77de20`; unmounted/unregistered Git.

<a id="수정본의-실제-첫-요청과-실패-경로"></a>
## Actual first requests and failure paths of the fix

Verified no Library in separate candidate delta; fixed common probe/render helper/meta SHA-256. Same three files checked in canonical fixture/two independent results too. Canonical Editor not opened here.

Reusable fixture local commit `c154a7f7e38c09fb7f16373f8437e6549772af8b`. Immediately before commit, verified blobs of 3 staged implementation files match actual candidate independent result; test code values 43/44/63 not applied to canonical default 13.

| Condition | Editor PID / loaded code | Ready or change validation | Foreground pixels per pass of first request | Render/cleanup time |
| --- | --- | ---: | --- | ---: |
| New private Library cold | 22580 / 43 | First ready 1,057.09s | `[0,9457]` | 2,893.67ms |
| Code change/domain reload | 22580 / 44 | Edit→validation complete 21.24s | `[9457]` | 49.10ms |
| Restart after normal shutdown | 46932 / 44 | First ready 63.38s | `[9457]` | 88.85ms |

Each condition verified new request ID/actual projectPath/PID/generation/MVID. Reload MVID changed `e7b70a7d-527f-4639-8c5c-33ed0df86e32`→`0bcfdb1c-7437-4679-b700-5da0f62651f5`; restart verified same code/MVID/new PID/generation. All three PNGs 640×480/model visible/SHA-256 `df94cefbc5f0d2fa6b46b878badb8848bc0221d66130778347349725a47b632d`/`sceneStateRestored:true`. First valid image selected within a single request; cold first draw 0 not hidden. Raw responses `candidate-{cold,reload,warm}-first-response.json`; PNG content/hashes `preview-analysis.json`.

Restart normally stopped/started only Editor while provider remained mounted. Stop 29.78 seconds, not mount/replay comparison. Final candidate stop returned `EditorStopTimeout` after 31.77 seconds; after checking logs/actual PID absence, same-lease stop recheck confirmed owned processes/working set/private bytes 0. Two baseline/candidate timeouts/initial failures preserved; no forced termination.

| Actual failure injection | Result | Recovery/preservation |
| --- | --- | --- |
| Owned scene camera culling mask 0 | Two passes `[0,0]`, `contains no visible geometry`, no PNG path | Valid PNG after restoring raw scene bytes/import, temporary scene restored |
| Owned scene model GUID changed to missing reference | `Scene mesh references missing`, no PNG path | Valid PNG after restoring raw scene bytes/import, temporary scene restored |
| Existing PNG placed at new-request output path | `Preview destination already exists` before scene open, no PNG path | Existing hash preserved; only owned collision file removed, then new request succeeded |

Three recovery PNGs match normal-image hash. `failure-checks.json` verifies original scene hash, pre-injection PNG preservation, 0 temporary publication files. Output collision rejects before scene work; `sceneStateRestored:false` in its failure response is not interpreted as scene-restoration failure. Not every shader/render pipeline/asset error tested.

<a id="시간과-저장량"></a>
## Time and storage

Initial preparation distinguished from restart/render. Baseline 1,084.00s/candidate 1,057.09s have different fixture code; baseline import partly overlapped product automatic tests. Small-sample preparation observations, not performance-improvement ratios.

| Category | Observation |
| --- | --- |
| Baseline delta/control maximum | 2,401,734,552B |
| Candidate delta/control maximum | 2,294,144,368B |
| Owned-file allocation maximum, both tasks+snapshot receipts | 4,695,916,248B, about 4.696GB; whole test preserving two independent cold Libraries |
| Owned-file allocation after reclaim | 0B |
| Shared Git including existing results | Before/after reclaim 6,145,760B; immediately pre-creation baseline unknown, not used as this increase |
| C: execution bundle/PNG/bundle/independent Git/closed control evidence | Post-reclaim observation 33,018,224B; subsequent documents/receipts may add |
| V: shared VSS immediately before reclaim | Used 27,426,816B / allocated 100,663,296B |
| V: shared VSS after reclaim | Used/allocated 0B |

File figures are actual allocation observations in `space-*.json`; instantaneous peak unmeasured. VSS is volume-shared, not split between tasks or double-counted with file values. Original had no Library; no full Library copy/read hydration/journal rewind. Does not prove physical Library savings/large-project capacity targets.

<a id="독립-결과와-회수"></a>
## Independent results and reclaim

| Task | Independent result | Preservation |
| --- | --- | --- |
| baseline | `8f409c11f23bc146d1df56670d9595d6ea77de20` | `baseline-result.bundle`/`baseline-result.git`, final modified code 63 |
| candidate | `6b94595b402683ca6028de95fd73888d13ee2c5b` | `candidate-result.bundle`/`candidate-result.git`, final modified code 44 |

Each result changes only 4 files: probe/helper/meta/loaded code. Baseline result is final fix installed after diagnostics; original old inspection/diagnostic sources separately preserved in `diagnostic-source/`/responses/PNGs. Bundles/bare repositories created without alternates; fsck/refs/changed blobs/bundle hashes checked before reclaim/after VHD detach.

Reclaimed 3 owned Editor runs/2 providers/mounts/Git registrations/deltas/owned snapshot. Open handles/I/O failures in provider-exited state 0. Preserved original clean HEAD/90 tracked-file raw hashes/25 existing refs/other-volume snapshot `{78AD7813-DF73-4845-8513-F14CC6E87331}`. 4 owned-result refs additionally retained. Restricted administrator session ended; verified VHD detached/V: absent/independent results again. Existing VHD file/original fixture preserved.

<a id="운영-기록과-판정-경계"></a>
## Operational record and judgment boundaries

- Preserves initial restricted product restore failure/permitted rerun of same entrypoint, common frozen binaries of two actual runs, and post-commit version-stamp difference.
- One separate observation `Get-Content` encountered sharing violation on live provider state. Requeried with `FileShare.ReadWrite|Delete`; provider I/O failures 0. Actual watcher/inspectors use shared reads (`observer-sharing-retry.json`).
- Retains two baseline/candidate stop timeouts/natural-exit rechecks and corrected intermediate diagnostic PNG interpretation. Previous VC/VR/RU/WI/JW blank PNGs/VR-03 Partial unchanged.
- First-request/failure-recovery contract for fixed small Unity 6000.3.22f1/URP 17.3.0. Excludes native Unity first-draw cause, generic image-semantic judgment/all assets/materials/platform support/product workspace validation API.
- Boundary prepared for full code-quality review/refactoring/optimization. Product Runtime/flush/default modes/large PV/physical Library savings not inherited from this result.
