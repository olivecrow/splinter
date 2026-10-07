<a id="splinter-첫-실사용-버전-계획"></a>
# Splinter first practical-use version plan

[한국어](../ko/plans/implementation-plan.md)

**The first scoped practical-use version M0 for registered small Windows x64 NTFS/Git/Unity environments has closed.** [Delivery decision](../verification/m0-delivery.md) is scoped-complete-with-limitations; [AI start guide](../agent-start.md) lists existing development CLI/source-skill prerequisites and next actions. Follow-ups divide into workspace-management program implementation, AI usage procedures, and support verification when needed. Do not recast all NR/SF/SW as unfinished program features or the denominator for M0 progress.

<a id="목적과-완료-경계"></a>
## Purpose and completion boundary

Splinter is **a tool helping AI agents efficiently create, continue, and discard parallel workspaces when desired, in environments such as Unity where project/Editor occupancy constrains independent work**. The conversation's AI and existing development tools judge task meaning, write code, and verify game features.

**Splinter provides parallel work with or without external VCS and must prevent storage, memory, and parallel-work problems when using Git, Unity VCS/Plastic SCM, and similar tools.** Parallel isolation/basic lifecycle are Splinter features; VCS-specific handling maintains them safely/efficiently. Closed M0 assumed Git. Plain-folder lifecycle/existing-Git protection and one small actual A/B run closed. Small editorless plain-folder original application also closed with limited qualification. Plastic compatibility and a separate small T: A/B closed through final snapshot reclamation. Preserve preceding C: pending/discovery failures in [PC verification](../verification/plastic-compatibility.md) as historical evidence; current C: closure follows [VR-01](../verification/vss-and-mcp-remediation.md#vr-01-exact-existing-c-plastic-reclamation).

The first practical-use version completes the bounded M0 environment/workflow below. General Unity authoring/testing platforms and qualification for every NTFS failure condition are not completion criteria. [plan.html](../../plan.html) owns product requirements, [foundation](../foundation.md) current implementation, [verification matrix](validation-matrix.md) execution status.

```text
Assess need → create independent task/resume same task → edit/use needed Editor
                                                     → keep preserved
                                                     → preserve result/explicitly apply, then reclaim
                                                     → explicitly discard
```

Conversation/Editor ending, timeout/lease expiry/disk pressure do not express application/discard intent. Ending A retains B basis/changes/Editor and original. No separate coordinating AI/task-graph submission/subagent Runner.

<a id="프로그램과-에이전트의-책임"></a>
## Program and agent responsibilities

The user-fixed [VCS project-scoped basis transition](project-scoped-basis-and-reclamation-review.md) reuses recorded Git/Plastic versions as project-only bases with per-task range storage. Git `git-snapshot`, original Library references, and actual Unity/MCP [closed with limited qualification](../verification/git-library-reference.md); actual RAM savings were unconfirmed. Plastic [PPB-01–06](plastic-project-basis.md) reader/Library/fixed actual Unity-MCP A-B/independent results-reclamation/original restoration also [closed with limited qualification](../verification/plastic-project-basis.md). Preserve initial preparation-restoration/overlap limits/unknown whole peak; user excluded RAM savings from pass criteria. Historical MCP response limits were subsequently resolved through [MR verification of a fixed Coplay patch](../verification/vss-and-mcp-remediation.md), covering long execution/duplicate prevention/reload details/default Codex HTTP connection. Do not inherit qualification as patch verification for a new full parallel A/B. [Full-selected Plastic original publication](plastic-original-apply.md) closed under separate POA scope/candidate-specific qualification. A new VCS-free basis subsequently connects the user-approved [managed project original](../plain-project-basis.md), preserving existing plain-folder features/results/historical VSS pending evidence. Earlier native qualification does not pass new readers.

Splinter owns workspace creation/maintenance/resumption/independent results/explicit application-reclamation-discard/owned Editor lifetimes. Do not add AI policies/workflows achievable with current CLI/file tools/Git/MCP/Unity CLI as program features.

| Area | Responsibilities retained in Splinter | AI/existing-tool responsibilities |
| --- | --- | --- |
| Task connection | Stable key/id/home/basis/returned paths, same-request resumption, duplicate-wrong-target rejection | Assess need, handoff records/requery, coordinate writers to one path. Automatic Codex binding/fork/global-last-task guessing excluded |
| Editor | Start/observe/stop exact project, ownership/resource reclamation | Select optional tools/connect targets/shared-server coexistence-recovery; edit/refresh/compile analysis/Play/tests/builds/capture/judgment. External-tool install-downgrade-patching/modified-tool operation/general relay APIs excluded |
| Results/integration | Stable seals/independent results/latest target-dirty guards/integration serialization/original-peer protection | Verification contents/pass decisions/Git conflict resolution-integration strategy/review-return-merge Unity generated sources/post-integration game verification |
| Storage/execution method | Range storage/Library-cache lifetimes/explicit compaction/cost-state observation | Choose independent Editor or development source-view route/task ordering/concurrent Editor count. Automatic modes/queues-schedulers/shared original Editor/general lease takeover excluded |
| External VCS coexistence | Isolate management data-registrations-locks-shared bases/protect original-peer/limit-observe extra copies-index-cache-results-memory costs | Ordinary history/check-in/conflict policy through existing VCS tools. Judge compatibility by actual Splinter parallel work-reclamation/efficiency |
| Installation/discovery | Executables/current contracts/diagnostics/explicit restricted-service-driver configuration tools | Obtain requested distribution/build-dependencies/skill connection/update-removal. Dedicated project-global installer/Codex-settings framework excluded |

Acknowledged-write durability/ownership/original-peer-shared-resource protection that AI judgment cannot replace remain program responsibilities. CLI locks do not claim exclusive control of arbitrary file writes. Retain ownership/seals/independent results/reclamation/target guards/failure records. Project compile/test pass judgment belongs to AI/external tools.

<a id="남길-후속-작업과-종료-기준"></a>
## Retained follow-ups and completion criteria

Current Windows/NTFS alpha **physical storage closed under [PS-01–05](physical-storage-completion.md), memory under [ME-01–05](memory-completion.md), selected parallel stability under [SP-01–04](stable-parallel-completion.md).** SP covers lock errors/editorless concurrent CLI-file I/O. Select new ordinary tasks from actual blockers/preservation defects/new input-support requests. Do not count separate SF/NR support expansion or unknown shared VSS-instantaneous peak-total RAM attribution as unfinished features or automatically rerun them.

Existing C: VSS pending reclamation/MCP response limits also [closed through VR/MR](../verification/vss-and-mcp-remediation.md). Historical C: pending/MCP failure wording below is evidence from that time, not currently unfinished work. User excluded investigating earlier unexpected Editor termination causes. Subsequently approved whole-project managed-original mount implemented [VCS-free project bases](plain-project-basis.md), closing file/actual Unity A-B/full-selected application/NTFS restoration/full regressions with limited qualification. New `project-snapshot` qualification and newly observed external Coplay shared-server shutdown ownership defect follow [NP verification](../verification/plain-project-basis.md). Shared-server recovery is neither code repair nor uninterrupted-coexistence success. Do not automatically add support expansion to closed units; select large/ARM/OS-failure work after fixing necessary inputs/support scope.

| Category | Retained work | Completion criteria/limits |
| --- | --- | --- |
| Plain-folder original application: scoped closure | [Safe VCS-free original application](plain-folder-original-apply.md): explicit target/three-way content comparison/conditional publication/same-intent-partial execution recovery | [AP-01–06](validation-matrix.md#vcs-free-original-application) and one small editorless NTFS A/B. Original-peer/independent results/efficiency/existing Git regressions retained. Distinguish automated file-interruption injection from AP-05 product qualification. [Decision](../verification/plain-folder-original-apply.md): scoped-complete-with-limitations |
| VCS independence: scoped closure | [VCS-free basic lifecycle](#closed-preceding-development-unit-vcs-free-basic-lifecycle) common contracts/plain-folder path/existing-Git protection-regressions | [Scoped A/B decision](../verification/vcs-independent-workspaces.md): scoped-complete-with-limitations. Retain original-peer/range storage/reclamation guards. Original application uses [separate AP](../verification/plain-folder-original-apply.md); Plastic qualification separate PC |
| Unity CLI handoff: scoped closure | [Unity CLI A/B handoff](session-workflow-and-installation.md#follow-up-scope-unity-cli-ab-handoff) on existing CLI-Pipeline/small T: Plastic: exact targets/loaded editing/normal-shutdown coexistence/independent results-reclamation | [UH verification](../verification/unity-cli-handoff.md): scoped-complete-with-limitations. Connect through returned-path commands without program changes. Retain list omissions/first eval timeout/unqualified MCP-other inputs-Play-Test-build. No separate full handoff for existing Git candidate |
| Result preservation/task verification boundary: implemented | Removed built-in refresh-compilation/focus handling/internal compile gates/duplicate preserve-worker | External verification followed by basic checkpoint; retain stable seals/independent results/target guards without compile-success claims. Current regressions in [scope verification](../verification/workspace-product-scope.md); new native handoff selected per tool |
| Storage-memory: compression integration scoped closure | After [UH costs/Bee selection](../verification/library-block-compression-feasibility.md)/[standalone codec](../verification/workspace-payload-codec.md), connect [compressed journals](journal-payload-compression.md) frame-fake/ranges-replay/budget-results | SJR2/overlay v2/candidate v3 and [JP verification](../verification/journal-payload-compression.md) same-input NTFS allocation/small T: A-B/independent results-reclamation/required regressions closed scoped-complete-with-limitations. Whole workspace space-RAM/large-OS unmeasured; do not automatically precede with new backend/flush-queues/full comparison |
| AI usage procedures | Improve discovery/creation-resumption/optional-tool selection/preservation-application-discard skill/examples | Agents can use existing schemas/returned paths/identical identities. Documented behaviors do not require new program features |
| Physical storage: default metadata scoped closure | [Fixed JM scope](journal-metadata-storage.md): current SJR2 defaults omitted/smaller explicit compaction-candidate representations | [Direct JM evidence](../verification/journal-metadata-storage.md): fixed actual-journal allocation before-after/content-sequence/extra managed allocations/required regressions closed scoped-complete-with-limitations. Separate replaced-payload removal by existing compaction; no new Unity-VSS/whole workspace-RAM claims |
| Physical storage phase: scoped closure | [Fixed PS scope](physical-storage-completion.md): SJR2 accounting/exact task allocation/product A-B/compaction-independent results-reclamation | [PS verification](../verification/physical-storage-completion.md) owns decisions/costs. Ordinary space follow-ups closed; retain large-OS-ARM/VSS attribution-whole instantaneous peak qualification gaps |
| Memory phase: scoped closure | [Fixed ME scope](memory-completion.md): compression write-read buffers/same-input semantics/owned provider-Editor-system costs/A-B reclamation | [ME verification](../verification/memory-completion.md) owns decisions/comparison inputs/costs. Total RAM/native codec-instantaneous peak unknown and separate support states retained |
| Parallel stability: selected scoped closure | [Fixed SP scope](stable-parallel-completion.md): lock-error classification/other-task progression/B query-resume-file writes during A collection-reclamation | [SP verification](../verification/stable-parallel-completion.md) owns decisions/concurrency-reclamation evidence. Editorless boundary; retain existing Unity-tools-PV/global VSS unknown. Add no repeats without new defects-inputs-support questions |
| Support verification when needed | Select mount-Git/interruption-reclamation boundaries/reproduced safety defects for used input-environment only | Fix support conditions/candidate/question/completion criteria first. Whole OS failures/ARM/LFS-filters/all files-packages/48-scenario catalogue are not blanket mandatory tasks |

MCP/Unity CLI are optional external tools chosen by agents as needed from user-installed tools. Splinter storage isolation/bases/Library-unchanged-file references/range changes/independent results/safe original application/owned-resource lifetimes work without them. Specific tool names/versions/internal APIs are not product prerequisites/gates. Agents choose/connect tools and refresh/compile/Play-Test-build/capture/judge results. External-tool installation/downgrade/modification/separate modified-tool creation-operation are excluded from Splinter scope. Historical exact-path/fixed-MCP handoff qualification is evidence for specified candidates/inputs. Current owned bridges do not manipulate external callbacks/servers/PID files. checkpoint/integrate do not judge compile/test success. Follow [external-tool boundaries](../unity-mcp-response-recovery.md) and [decoupling verification](../verification/external-tool-boundaries.md).

<a id="한정-마감한-원본-반영"></a>
## Scoped closure of original application

**Safe original application for VCS-free plain folders**, fixed on 2026-10-02, closed scoped-complete-with-limitations. One small editorless A/B verified applying only changed paths from exact independent results while retaining subsequent original edits/B editing/preserved results. [Fixed scope](plain-folder-original-apply.md) owns inclusions-exclusions/conditional publication-interruption recovery/costs/completion criteria.

First actual qualification is one small `source=none`/`application=none` Windows x64-NTFS A/B. Closed AP-01–06 in [AP matrix](validation-matrix.md#vcs-free-original-application) with [execution evidence](../verification/plain-folder-original-apply.md). Plastic qualification uses separate PC; full Unity handoff/original Editor application remain follow-ups and do not inherit AP.

<a id="마감한-선행-개발-단위-vcs-없는-기본-수명주기"></a>
## Closed preceding development unit: VCS-free basic lifecycle

Additional user requirements on 2026-10-02 changed preceding Unity CLI handoff priority. Fixed scope: **complete plain-folder creation/same-task resumption/independent editing-Editors/result preservation/reclamation/discard without Git, retaining Splinter isolation/efficiency/original-peer protection when Git is used**. Common contracts/file-source providers/CLI-Windows integration and approved service update/actual A-B closed with limited qualification. Current state v2 and `--source git|none`/`--application unity|none` contracts follow [feature documentation](../vcs-independent-workspaces.md); candidate-specific automated regressions/actual execution/same-request resumption after fixes/limits follow [verification](../verification/vcs-independent-workspaces.md). [VCS matrix](validation-matrix.md#vcs-free-basic-lifecycle) owns execution status/gates.

<a id="현재-의존성과-목표-책임"></a>
### Current dependencies and target responsibilities

At scope refixing, CLI always composed Git/Git discard providers; Core/result preservation centered on commits/revisions. Current implementation separates `SourceKind`/`ApplicationKind`, opaque basis-result identities, provider checkpoints/independent preservation/discard. Common storage owns parallel isolation; VCS providers own management-data/basis/result/reclamation compatibility. Existing Git linked worktree/commit-ref handling is a current-path implementation choice; retain local `.git`/HEAD-base/clean/branch-ref-object guards. Verify new plain-folder actual qualification separately; Git remote registration is unnecessary.

| External VCS environment | Common Splinter goal | Environment-specific protection/current status |
| --- | --- | --- |
| Git | Creation/resumption/independent Editors/content checks/independent results/reclamation/discard | Existing commit-ref/linked worktree/integration-dirty guards protect management data. Separate shared objects-registrations/per-task changes-results costs; retain original-peer at reclamation. Preserve current scoped qualification/regressions |
| No VCS | Same | Immutable file bases/content hashes/independent-result basic lifecycle closed with limited qualification. No external VCS installation-initialization. Safe original application distinguishes separate AP automated-file tests/small editorless product qualification |
| Unity VCS / Plastic SCM | Same | Excluded from preceding plain-folder unit. Current [compatibility contracts](../plastic-compatibility.md) separately implement management-data-registration protection/independent file results; follow [PC](../verification/plastic-compatibility.md) scoped actual qualification/reclamation-handoff limits |

Separate parallel editing/result preservation from original integration. Independent work/result storage must work without VCS; no successful Git commit/check-in prerequisites. Original application compares starting basis/preserved result/latest original to protect subsequent changes. No simple overwrite/automatic `git init`/required user Git registration substitute.

<a id="이번에-포함할-구현"></a>
### Included implementation

- Starting with Core/fakes, clarify basis identity/change-content verification/independent-result preservation/explicit-original-application boundaries. Distinguish file bases/VCS revisions; no Git SHA-branch-commit requirement in common lifecycle. Finalize actual ports/fields/commands in contract design; these requirements are not current-schema guidance.
- Connect plain folders to Runtime/CLI-Host/Windows source selection-doctor/result-discard composition. Fix VCS selection in requests-state-digests; resume identical key-id-home/basis-contents. No fallback to no VCS on Git failure/corruption/unknown. First plain-folder input contains no VCS management; do not copy Git-Plastic management as ordinary source or share identical management state across tasks. Retain Git checks-refs/original-peer protection.
- Identify plain-folder bases from exact immutable snapshots with supported paths-file types/content hashes. No forced Git clean-untracked semantics; metadata-mtime do not establish content matches. Specify temporary-Editor management ownership; isolate subsequent original edits/A-B bases-changes.
- `checkpoint` provides independent results unrelated to compile success/VCS commits. Own actual reconstruction contents/immutable bases, not only manifests; results remain readable/verifiable after original edits/workspace-delta-snapshot reclamation. Results dependent on original paths/reclaimable VSS are not independent. Separately measure payload-basis storage needed for explicit preservation; no bypass of forbidden creation-read hydration/whole-file copy-up.
- Connect result-preserving release/result-free explicit discard to plain folders. Retain exact intent/content-owner/normal stop-unmount/seals/owned-resource-shared-dependency guards/same-request interruption resumption. Do not disguise VCS-free cleanup as Git worktree/branch deletion.
- Adapt Git paths to common contracts, retaining locked restore/automated regressions. Update input-output schemas/fakes/conformance/capabilities/features/AI skill-start guide/matrix together. If formats change, declare current format/preserve-reject existing records-results; no automatic migration/multi-version fallback.

<a id="실행-관문과-완료-기준"></a>
### Execution gates and completion criteria

Actual-test question: **can A/B work independently on a small Git-free NTFS folder without whole-original copying, resume identical tasks, and safely finish A independent results/reclamation/B explicit discard?** No new full failure harness; judge changed-contract conformance and one small representative A/B.

1. Verify zero Git calls/required installation dependencies for product commands in plain folders without `.git`. No hidden Git initialization. Confirm zero whole payload copying/hydration during creation-read/range storage for small writes/original location-content protection.
2. Verify returned A-B paths/distinct keys-IDs/bases-changes; resume identical requests in new CLIs. Later original edits cannot change fixed A-B bases. Compare owned Editor paths/PID-start with actual required external-tool results. Do not restore built-in Splinter compilation.
3. After A normal stop/independent checkpoint/explicit release, B editing-Editor continues and A result verifies without original/VSS. Explicitly discard B changes without commits/verification success. Verify collection before-after/unmount seals/independent results/original-peer protection/exact owned reclamation; report unknown/reclaim-pending separately.
4. Necessary lost-response/same-intent recovery/corrupt-wrong-target rejection use changed-boundary fake/automated regressions. Pass existing Git regressions; declare Git-free result application unavailable then/reject before side effects. No inheritance of earlier Git native/M0 evidence.

Inputs are Windows x64/local NTFS/supported regular files. Editorless basic tasks require no Unity project metadata/Editor/license; common conformance checked rejection-discovery boundaries. Actual owned-Editor gate used a small VCS-free fixture with exact Unity `6000.3.22f1`. Fix new fixture/exact project-home/basis contents/existing registrations-free space before execution; leave Git fixture `.git` unchanged. [Native-plan](vcs-independent-validation.md) administrator Apply executed under separate user approval. Installation/external-project changes for other inputs/configurations are not automatically included in this closure.

Preceding closure reached **VCS-neutral original-application contracts/blocking only**. Actual apply is separate AP below, without inherited passes; it was outside preceding scope. Automatic conflict merging/Plastic management-server-checkout/check-in/dedicated adapter/full independent Unity CLI coexistence/large-remote-OS failures/development view expansion were also outside AP. Plastic adapter follows separate PC scope below. Prioritize fixes for supported-path write loss/original-peer damage; otherwise record selected-unit success-failure/limits and close.

<a id="이후-순서와-원본-반영-원칙"></a>
### Subsequent order and original-application principles

| Order | Development unit | Completion boundary |
| --- | --- | --- |
| 1: scoped closure | VCS-neutral contracts/plain-folder basic lifecycle/existing-Git protection | Scoped actual A-B/independent results-reclamation/Git regression decision. Excludes separate AP-PC original application/Plastic qualification |
| 2: scoped closure | [Safe VCS-free original application](plain-folder-original-apply.md), existing Git protection retained | Explicit targets/three-way comparison/conditional publication/same-intent-partial execution recovery/one small editorless NTFS A-B/AP-01–06. Automatic content merging belongs to AI/existing tools |
| 3: scoped closure | [Parallel isolation/efficiency compatibility in Unity VCS / Plastic SCM](plastic-compatibility.md) | [PC verification](../verification/plastic-compatibility.md) owns T: A-B bases/same requests/original-peer protection/independent results-owned reclamation/costs-follow-up limits. C: reclamation closed in [VR-01](../verification/vss-and-mcp-remediation.md#vr-01-exact-existing-c-plastic-reclamation); external CLI discovery limits/full handoff unqualified retained |

Original application does not reset whole original to task-start state. Apply when affected paths match basis; if already matching result, verify as same-intent reconciliation evidence; preserve/reject paths changed differently by original/task and new-file/deletion/type conflicts. Retain unrelated original changes. [Scoped plan](plain-folder-original-apply.md) fixes actual publication/partial recovery; [feature document](../plain-folder-original-apply.md) owns current input-output schemas/publication-recovery contracts. AP-05 product A-B/AP-06 costs-regressions closed, following [execution evidence/limits](../verification/plain-folder-original-apply.md). Do not inherit native forced interruption/Unity-Plastic/large-OS support. Use VCS commands/APIs only as needed for ownership/isolation/basis-results/reclamation boundaries. Ordinary VCS conflicts/check-in policy belong to existing tools/AI. Compatibility completion means Splinter safety/efficiency, not VCS feature counts/check-in successes.

<a id="첫-버전의-지원-조건"></a>
## First-version support conditions

Below are closed Git-based M0 conditions. Plain-folder scoped qualification follows [VCS verification](../verification/vcs-independent-workspaces.md), Plastic [PC verification](../verification/plastic-compatibility.md). Neither inherits the other's native qualification.

| Item | Fixed scope |
| --- | --- |
| Environment | Windows x64/local NTFS/Git/registered project-home/existing restricted VSS service. Confirm actual Unity fixture from exact patch `6000.3.22f1` |
| Input | Verified clean Git/regular files-directories. Retain current unsupported file features/LFS-filters/dirty rejection. If primary projects require them, fix separate minimal support scope first |
| Storage | Original NTFS location-filesystem/continuously editable original/no initial full copies-read hydration-whole-file copy-up fallback/range changes |
| Execution | Separate independent source editing/verification. Run only needed Editors; choose default independent Editors or development storage views when needed. No automatic modes/queues/shared original Editor |
| AI entry | Development distribution/existing CLI/minimum guidance/stable task keys-workspace IDs-absolute paths-identical homes. No global-last-task guessing or required specific Codex session APIs |
| Guarantees | Supported-path original-other-task isolation/acknowledged-write preservation/exact owned resources/same-version normal resumption-request retransmission. No whole OS-failure/security-sandbox claims |

This is not a table promoting current implementation to supported completion. Record new-candidate execution qualification/support limits together. Diagnostic Git checkout is no NTFS fallback and outside first-version support expansion.

<a id="제품-작업-순서"></a>
## Product task order

| Order | Deliverable | Completion criteria/fix limits |
| --- | --- | --- |
| 1 | Explicit `discard` contracts/implementation: integration-local unit closed | Dirty tasks/same-intent resumption-duplicates/peer protection/actual reclamation. Unity compile failure-Editor stop included in representative flow at 3, no separate failure matrix |
| 2 | Minimal AI task connection/guidance: editorless unit closed | [Skill source](../../skills/splinter/SKILL.md), actual explicit-direct/returned-path editing/new-CLI same key-id-home resumption/discard. Installation connection is AI procedure; dedicated installer/automatic binding excluded |
| 3 | Representative A-B usage: scoped closure | [Representative use](../verification/representative-agent-workflow.md) errors-fixes/same-task resumption/dirty guards/preservation-integration-release/failed-task discard. Include first collection-focus rejection/same-task retry limits |
| 4 | Space-time adoption decision: scoped closure | Scoped adoption for sustained independent work using same-small-warm-input observed allocation/preparation-editing-completion costs. VSS attribution/physical peak/large efficiency unknown; no new comparison-repeat gates |
| 5 | Scoped version delivery: closed | [Start guide](../agent-start.md)/[current recovery-delivery decision](../verification/m0-delivery.md). Scoped version assumes existing development CLI/registered service, not general installation/large-public support |

[Per-conversation plan](session-workflow-and-installation.md#completion-preservation-and-discard) owns detailed discard intent/shared resources/interruption conditions. First implementation targets exact worker workspaces. Explicitly block active candidate/source/cache dependencies and return dependency cleanup actions. Do not automatically stop/delete original Editors/other tasks/shared views. If snapshot reclamation delays, distinguish logical completion/physical-reclamation wait; no hidden backup/reclamation-complete claims.

<a id="한정-납품-경계와-후속-선택"></a>
### Scoped delivery boundary and follow-up selection

M0 delivers existing development-checkout CLI/source skill/registered service-driver environment. [Representative usage/costs](../verification/representative-agent-workflow.md) owns execution/failures/same-task retries; [delivery decision](../verification/m0-delivery.md) current regressions/historical native reuse limits/excluded failure conditions. Do not read as general installer/automatic binding/public release-distribution completion.

Select follow-ups by actual user requirements within responsibility/completion tables. New projects/inputs target relevant input-registration-mount boundaries; installation targets AI procedures/existing configuration tools; observed costs blocking practicality target input bottlenecks. Before needs are fixed, launch no new harness/full failure matrix/VIVOBOOK/large PV/p95 repeats. Fix newly confirmed supported-path acknowledged-write loss/original-peer damage first.

Development order is **physical space reduction → memory reduction → stable parallel work**; PS/ME and selected SP closed with limited qualification. Select next units from new real-use blockers/safety defects/needed support inputs. [Storage-first plan](storage-priority-and-editor-modes.md#follow-up-development-priorities) owns acceptable preparation costs/decision methods. New Editor preparation within minutes is acceptable and can overlap AI planning/analysis, so preparation-time reduction has low priority. Retain existing scope/closed M0 decision.

<a id="유지할-구현과-제거-경계"></a>
## Retained implementation and removal boundaries

| Decision | Current boundary |
| --- | --- |
| Retain | Core ports/schemas/atomic state-locks/Git linked worktrees/VSS-WinFsp range journals/acknowledged-write flush/result seals/process supervision/ownership-reclamation guards |
| Retain | Unity start-status-stop execution/resource observation/original-peer owned-resource protection. External tools/AI verify projects |
| Remove | Built-in Unity refresh/compile events-domain reload evidence/focus retries/internal compile-success gates/duplicate preserve-worker |
| Retire | [ReFS basis path](refs-retirement.md). Initial full-basis storage-time burden for large projects led to code-command-test removal. Use current snapshots |
| Separate development path/stop expansion | Diagnostic checkout/single view-Library cache/spikes-probes. Create-select only through `splinter-dev.ps1`; retain protection regressions/reproduction evidence. Excluded from default product discovery |
| Exclude implementation | DAG-AI Runner/daemon-DB/general Unity authoring-Play-Test Runner/dedicated installer-Codex auto binding/automatic modes-queues-general lease takeover-shared original Editor/conflict resolution-generated-source return merging/game-verification platforms |

Internal OS child-process supervisors differ from task-splitting AI Runners even if module names contain `runner`. Do not delete product-path/reproduction features by name alone. No arbitrary state-record format changes/old-version migration-fallback. Raw evidence/independent result refs/snapshots/other tasks are outside cleanup.

<a id="첫-버전-검증-m0"></a>
## First-version verification M0

Keep execution status only in [M0 matrix](validation-matrix.md#first-practical-use-version-m0). Reference existing NI/LV/WF/LB/NR sub-evidence without automatically inheriting whole-new-workflow passes.

| ID | First-version completion criteria |
| --- | --- |
| M0-01 | Exact project-home A-B creation/range storage for small writes without initial full payload copies-hydration. Original location-content/peer isolation |
| M0-02 | Actual AI uses correct paths-Editors; same task keys-bases-changes across CLI-conversation resumption. Zero new tasks when unnecessary |
| M0-03 | B progresses while faulty A is fixed/preserved. Exact Unity code-change-compile evidence/resumption of one execution path |
| M0-04 | Explicit discard without verification success-checkpoint, duplicate discard/same-intent interruption recovery. Protect original-other tasks-shared dependencies |
| M0-05 | Distinguish explicit result preservation/application/reclamation. B continues after A ends; reject dirty-changed originals/preserve results |
| M0-06 | No false success/duplicate side effects/wrong deletion on supported-path lost requests-responses/owned-process interruption/corrupt-unknown. Retain durability contracts |
| M0-07 | Confirm owned Editor-provider-mount-Git-delta/final snapshot reclamation at same path. Report shared-reclaim-pending separately; unknown is not zero |
| M0-08 | Adoption-limits judged through fixed-input creation/full editing readiness/resumption/repeated changes/completion times/extra storage. Unobserved physical peak/shared VSS attribution unknown |

<a id="검증량과-종료-규칙"></a>
## Verification volume and completion rules

- `pwsh ./scripts/build.ps1` locked restore/Release/existing automated tests required. Do not gain progress by reducing discard-deletion/acknowledged-write/original-peer protection tests.
- Contract changes update schema-fake-conformance together. Select actual tests for **changed side-effect boundaries or reproduced defects**, ending with one representative first-version A-B flow. Documentation/command-discovery changes do not rerun all historical Unity-VSS tests.
- Preserve failures; bounded same-cause reproduction/fix/regression. On success close selected follow-up; no automatic M0 reopening. Write questions/completion criteria before new harnesses/full combinations/increased repeats.
- Fixed-input costs distinguish initial/existing-task resumption/repeated edits. Promise no measurement multiplier/savings/time SLA. Do not automatically resume large C-S-Q PV; p95/20 repeats are not first-version gates.
- Reproduced supported-path acknowledged-write loss/original edits/other-task termination-deletion blocks affected route and requires fixing first. Past failures remain unknown until cause confirmed.

<a id="별도-지원-확대"></a>
## Separate support expansion

[NR](ntfs-recovery-and-support.md) OS failures/ARM/file combinations and [SF](storage-priority-and-editor-modes.md) large capacity are support verification when needed. [SW](session-workflow-and-installation.md) installation/conversation handoff/tool selection/game verification are AI procedures. Excluded automatic binding/queues-mode selection do not remain unfinished development items. Preserve `NotRun`/`Partial`/failure-unknown as evidence states, never relabel Passed/Unsupported. Product owns ordinary asset I/O mount correctness; external tools own asset authoring/Play-Test commands.

Earlier P0–P7/F phases and 48-item catalogue are not first-version progress denominators. Preserve long-term IDs/failures/reproduction conditions in [verification matrix](validation-matrix.md), distinct from scoped M0 closure. Connect external VCS compatibility only through selected-unit common contracts/fakes/actual inputs. Do not preimplement other VCS/OS/application expansions.
