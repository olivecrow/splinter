<a id="한정-마감-vcs-없는-일반-폴더의-안전한-원본-반영"></a>
# Scoped closure: safe original application for VCS-free plain folders

[한국어](../ko/plans/plain-folder-original-apply.md)

This is the next program development unit fixed on 2026-10-02. **Explicitly apply independent `source=none` results to the exact original, protecting subsequent original edits, peer tasks, and preserved results.** This completes Splinter's own parallel-task lifecycle; external VCS compatibility is the next separate unit.

The preceding plain-folder lifecycle closed its [scoped verification](../verification/vcs-independent-workspaces.md). The user's implementation request connects the [current original-application contract](../plain-folder-original-apply.md); the [original-application matrix](validation-matrix.md#vcs-free-original-application) owns execution status. After user return, approved service Update/registration and one small editorless product A/B run closed scoped-complete-with-limitations. Distinguish automated file/interruption-injection tests from actual product qualification; do not expand to excluded full native failure/Unity/Plastic qualification.

<a id="선택-이유와-고정-범위"></a>
## Selection rationale and fixed scope

At scope selection, independent work and result storage were possible, but no product path safely returned results to the original without Git; that missing boundary was selected. Preserve existing Git protection/regressions. The [priority](storage-priority-and-editor-modes.md#follow-up-development-priorities) remains storage → memory → parallel stability. Limit/observe extra copying, temporary space, and memory costs caused by application; do not also begin unselected Library/Editor optimizations.

| Item | Fixed scope |
| --- | --- |
| First execution environment | Windows x64/local NTFS/registered small VCS-free plain folder, `source=none`/`application=none`/`ntfs-snapshot` task |
| Target | Task's fixed `ProjectPath` and original path identity. No export/apply to arbitrary other folders or substitution with Git branches |
| Applied content | Exact task checkpoint basis/result manifests and actual independent payload. Reject if current task contents differ from collected result |
| File operations | Regular-file content changes/creation/deletion, necessary directory creation/confirmed empty-directory deletion. Compare rename as old-path deletion/new-path creation; claim no multipath atomicity |
| Verification boundary | Paths/types/lengths/actual-content hashes/ownership/current original identity. No match declaration from mtime alone. Preserve existing file-type/attribute/path/manifest limits; preserve/reject unsupported metadata changes |
| Lifecycle | checkpoint → review application plan/explicit execution → same-intent resume or preserve → explicit release/discard. Application does not approve automatic reclamation/discard |

Initial actual qualification is limited to editorless plain folders. Do not break common application/owned-Editor contracts or existing Unity paths, but do not inherit this pass for actual original Unity Editor/Library/generated-source application. Separate import/application of already released results is also excluded.

<a id="포함할-구현과-안전-계약"></a>
## Included implementation and safety contracts

1. **VCS-neutral application contract and explicit target.** Define fixed workspace ID/request digest/basis-result identity/original identity/application intent in Core/fakes. Clarify source-specific targets/defaults of existing `workspace integrate`, read-only application plans, execution inputs/responses. `none` requires neither Git branches nor default `development`. Finalize actual ports/fields/options during contract design alongside input/output schemas.
2. **Basis/result/current-original comparison.** Verify independent payload hash/owner and current original; select only changed paths according to rules below. Inspect the entire change set before application; write nothing if any conflict/unsupported/unknown ownership appears. Return target/basis-result/changed paths/conflicts/fresh original guards in plans. Preserve original modifications/new files outside the change set; do not reset whole original to basis. Unrelated original edits do not require a clean whole folder.
3. **Conditional publication and external-writer protection.** Serialize application against the same original. Recheck actual file/parent identities/types/contents/path escape/fresh guards immediately before each publication. Use verified filesystem boundaries preventing intervening external writes/replacements from being overwritten. Splinter locks alone do not block arbitrary external writers. Preserve/reject sharing-lock/path-type changes where safe publication cannot be verified. Other original paths and B work must stay editable.
4. **Durable intent and partial-application recovery.** Publish exact plan/intent before original writes; record per-path execution/observation receipts. Reconcile lost responses/publication-record failures/same-version process restart only through identical ID/home/intent/basis-result/target guard. Do not rewrite verified completed paths; process only remaining paths. If original changes again after interruption or execution cannot be verified, preserve partial state/results and stop. Block release/discard completion and retain recovery evidence while unresolved application intents/owned temporary publication resources remain. Discard does not undo already applied original changes. No automatic rollback/original reset/new-intent overwrite/temporary-file sweeps. Claim neither whole-multipath atomicity nor OS/power-failure qualification.
5. **Preserve scope and costs.** Retain existing VSS/WinFsp range journals/ACK flush/seals/independent result-reclamation guards. Create no whole-original backup/clone. Distinguish changed-file temporary publication payload needed for explicit application from workspace creation/hydration/copy-up; observe actual extra allocation. Stream fixed-size payloads; add no whole-file/project in-memory paths.
6. **Composition and guidance.** Connect needed Runtime/file provider/CLI-Protocol boundaries; keep Windows publication/identity handling at platform boundaries. Add no Git/Unity/Win32-specific APIs to Core. Update input-output schemas/capabilities/fake-conformance/current features/start guide/source skill/AGENTS/verification matrix together. Retain single-current-format policy and old/corrupt-state preservation-rejection; add no automatic migration/compatibility fallback.

<a id="경로별-비교-규칙"></a>
### Per-path comparison rules

| Basis/result/current original | Decision |
| --- | --- |
| Task result equals basis | Leave path untouched; preserve subsequent original changes |
| Task changed; current original equals basis | Candidate for conditional result application. Creation only if absent from basis/current; deletion only if current exactly equals basis |
| Current original equals result | Verify without extra writes. This alone does not infer earlier execution success; distinguish durable evidence of identical intent |
| Original and task changed differently | Conflict. Before-start detection rejects all application before writes; detection after partial application preserves completed/incomplete paths and stops |
| File-directory type conflicts/non-owned paths/nonempty deleted directories/VCS management information/unsupported-corrupt-unknown | Preserve/reject. No recursive deletion/plain-folder fallback bypass |

Conflict resolution/content merging belong to AI/existing tools. Do not automatically discard original changes or adopt latest original as new basis to remove conflicts. If original changes after plan review, do not unconditionally apply existing execution inputs.

<a id="검증량과-종료-기준"></a>
## Verification volume and completion criteria

The question is **whether A changes alone can safely apply to a Git-free plain-folder original, B fixed basis/edits remain, original/independent results survive conflicts/interruption, and resources can explicitly reclaim**.

- Contract/fake and actual-file-provider automated tests cover only changed boundaries. Check creation/modification/deletion, empty-directory/type conflicts, unrelated original edits/same-path conflicts, concurrent original writes/replacements, identical/changed intents, content corruption, response/receipt failures, partial execution resumption. Do not substitute fake passes for concurrent-writer/actual-publication verification. Create no new whole failure harness/boundary-combination matrix.
- Actual verification is limited to **one small NTFS editorless A/B run**: create A/B/resume identical A → edit unrelated original path → B continues editing during A checkpoint/application/explicit release → B overlapping-change checkpoint/application conflict rejection and original/B result preservation → explicit B completion/owned-resource reclamation. No additional native forced-termination matrix for failure/recovery or full Unity/MCP handoff.
- Before actual execution, verify final candidate/fixed source-home/A-B keys/file set-hashes/existing service-registrations/non-owned resources. Prefer an already registered appropriate small input. If new registration/service updates/external-original writes are needed, prepare reviewable execution plans and relevant approval first. Do not expand earlier fixture-specific administrator Apply approval to other paths/settings.
- Pass `pwsh ./scripts/build.ps1` locked restore/Release/existing Git-Windows/file-contract regressions and documentation UTF-8/link/diff checks. Do not weaken Git refs/worktree/dirty-result-peer guards or existing plain-folder release/discard qualification.
- Record extra file allocation/temporary publication payload/independent results/journals separately from managed/process memory. Unmeasured whole physical peak/shared VSS attribution are unknown. Add no fixed savings/time SLA/large performance completion conditions.

Completion is **scoped-complete-with-limitations** with required [AP-01–06](validation-matrix.md#vcs-free-original-application) gates and evidence/limits for this run. First block affected paths/fix defects for acknowledged-write loss or original/peer damage. Otherwise preserve failures/resources and reproduce/regress only the necessary same cause. After success, do not expand repeated new matrices/inputs or reopen M0.

<a id="제외와-이후-순서"></a>
## Exclusions and subsequent order

Exclude Plastic environment compatibility/other VCS-server-check-in features, Git redesign, automatic content merging/conflict resolution, arbitrary-target export/released-result import, original Unity Editor/generated-source application qualification, full external-tool A/B handoff, Library/view expansion, large/ARM/remote/OS/power-failure tests, installer/automatic binding/queues-mode selection/Runner-DAG.

After this unit closes, separately select management-data/registration/lock/shared-basis and extra storage-memory costs for the actual Plastic environment. Completion criteria remain Splinter parallel isolation/original-peer protection/efficiency. External-tool handoff and observed storage-memory bottlenecks are selected in their owning plans.
