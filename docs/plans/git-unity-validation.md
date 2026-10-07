<a id="git-기준본의-실제-unity-검증"></a>
# Actual Unity verification of the Git basis

[한국어](../ko/plans/git-unity-validation.md)

User approval: after committing existing Git implementation, verify actual Unity execution of new `git-snapshot` using currently open `splinter-unity` and `http://127.0.0.1:8080/mcp`. Do not inherit existing Unity/VSS qualification; fix this input and completion criteria. Add no HTML.

<a id="입력과-보호-경계"></a>
## Inputs and protection boundaries

- Use Git implementation commit `d401677` on product `development` and current build. Required automated checks were 919 Passed/1 then-excluded test NotRun.
- Original is `C:/Users/olive/Desktop/Development/splinter-unity`, development commit `6a54974a9c655031aeb2be17fb7622fc5d641c52`. Use recorded 1,602 files/approximately 2.8GB as basis without a separate full checkout/copy. Preserve existing input restrictions for the full Medieval pack.
- Actual original uses Unity 6000.3.22f1, Coplay 10.2.0, Pipeline 0.7.0-exp.1. Confirmed original PID 81892/start ticks 639266922811331760, HTTP instance `splinter-unity@983ac2308e6c044d`. Preserve original Editor/scenes/server/branch/files/existing result refs.
- To meet raw Git basis contracts, temporarily set `core.autocrlf=input` only in the original repository. Keep global true unchanged. Confirm clean state without rewriting current checkout files. Record initial/temporary config and original file hashes; restore local settings at completion under an identical-content guard. Do not overwrite other changes.
- New owned home `.local/gitu1004/home`, keys `git-unity-A`/`git-unity-B`. Fix source=git/application=unity/storage=git-snapshot/base=development in the initial request. Do not use/reclaim existing tasks/VSS pending/other snapshots.
- Do not capture/copy original Library. Separate each task's cold generated Library/journal/shared Git/runner/Editor/provider/native Git and original resource costs. Zero initial payload copies/read disk cache and overall space/RAM reductions are different claims.

<a id="한-묶음의-실행과-종료"></a>
## One run and completion

| ID | Question | Completion evidence |
| --- | --- | --- |
| GU-01 | Open actual Unity A/B on Git bases and continue identical tasks? | Exact workspace/id/base/digest, owned Editor PID/start, actual import/compile completion. Do not judge readiness from responses/lists alone |
| GU-02 | Exact tools connect to current A/B Editors? | Current HTTP discovery, per-call exact instance matching actual project/PID. CLI uses exact paths when necessary. Protect original/shared server |
| GU-03 | Texture/scene work at identical relative paths and current-state tests isolated? | Distinct A/B pixels/markers/transforms, actual import/save/reload, each current file evaluated by the same test class. Intentional failure by changing only B marker, then pass after restoration; A unchanged |
| GU-04 | B continues during A result preservation/release; final owned resources reclaimed? | A independent result then release; B subsequent resource/test and independent result then explicit discard. No owned Editor/provider/Git reader/mount/registration/delta/basis ref; independent results retained, original/config/server protected |
| GU-05 | Actual costs/limits distinguished? | File allocation, preparation time, generated Library/journal, process samples, failure/recovery evidence. Keep total instantaneous peak/shared attribution unknown |

New original integration, original branch changes, Play/build, other VCS/ARM/OS failure matrices are outside completion criteria. Use previously approved Pipeline 0.8 only in private A/B Packages when tool connection needs it; preserve original 0.7. Add no arbitrary packages/Editor versions. If preparation stalls for tens of minutes, investigate/fix only observed bottlenecks and continue with identical id/home. Do not repeat writes/scenes/tests on timeout alone or hide failures with new A/B.

Raw execution, identity, original/settings/resource costs remain in `artifacts/git-unity-validation-20261004/`. Explicit completion reclamation approves preservation/release/discard of this A/B; do not reinterpret cleanup instructions as missing approval. Preserve failed tasks/results/evidence and shared caches.

Fixed GU-01–05 execution and final required regressions closed as scoped-complete-with-limitations in the [verification record](../verification/git-unity-validation.md). Do not automatically repeat final A/B or new matrices. Library basis reuse below is separate subsequent design/implementation, outside this completion.

<a id="관측한-준비-병목의-수정"></a>
## Fixing the observed preparation bottleneck

Before the fix, A/B exceeded the existing 10-minute limit at `git update-index --refresh` before Editor start. Entire blobs were redecoded on every partial read, and subtree verification started a Git process per object. Fix only boundaries observed on this input. Use 64KiB chunk proofs established by initial whole-blob hashes, one-blob-bounded memory and sequential decoder reuse, and bounded subtree batch verification. Add no original-content fallback/disk hydration/skipped verification/deadlines. After normal owned-provider unmount, preserve/move the empty index lock belonging to the exact registration as evidence; resume identical id/key/home/base/digest. Regressions include small forward/backward reads, no buffer publication on reread corruption, and nested tree corruption rejection.

The user's Library composition question is a separate follow-up design request. Existing VCS-free `ntfs-snapshot` uses VSS-fixed original Library and private ranges. Git already uses the same range journal/compression for new Library private changes. The missing boundary is existing Library basis reuse. Git source and Library basis can be composed, but attaching VSS bases reintroduces volume snapshots. Development common view/cache is single-writer; do not connect parallel A/B to the same mutable cache. Review a project-scoped immutable Library generation as shared read basis and separate private ranges for each A/B Library write. Define Unity/package/source compatibility keys, owner/peer reclamation protection, and initial-generation preparation costs before integration. Current Git verification retains cold Library creation costs; do not present mutable original Library sharing or reuse as implemented.
