<a id="병렬-작업과-비용-비교-방법"></a>
# Parallel workflow and cost comparison method

[한국어](../ko/plans/parallel-workflow-qualification.md)

This document preserves methods for aligning inputs/C-S-Q conditions/tasks/measurements in later comparisons. User-selected execution closed; this is no new run instruction. Decisions/remaining limits follow [verification guide](../verification/README.md).

<a id="확정한-목표"></a>
## Fixed goal

**Verify whether independent conversations A/B on the current NTFS internal alpha can repeatedly edit/verify/resume scripts and continue identical B after A first applies to original/reclaims. Compare against Codex default Git-worktree independent Editors and Coordination single-Editor sequential verification on the same Unity project with real 3D models/textures; choose next improvements through time/read/storage/memory/call/operational-intervention costs.**

Preserve existing PV inputs/execution-completion conditions within [product plan](../../plan.html#next). This is no automatic next task; full selected [NR](ntfs-recovery-and-support.md) verification/[SW](session-workflow-and-installation.md) AI procedures are not prerequisites. Scoped results do not establish public support/full NR-SW completion.

<a id="제품-목표구조와-현재-도달점"></a>
## Product goal, architecture, and current progress

Each conversation's AI obtains its development environment when needed/continues same tasks. Retain Windows-NTFS original location/filesystem, immutable starting snapshots/per-task range changes. No initial full-data copies/read hydration/whole-file copy-up fallback; original stays editable during work. Explicit application/preservation/discard are distinct user intents.

Architecture is **CLI → Runtime → Core provider contracts → Git/Unity/Windows adapters**. CLI composes implementations; Runtime coordinates atomic JSON/task-repository locks/results-reclamation. Core has no Unity/Win32/Git/Codex APIs. Host is read-only in-process; small Editor bridges communicate with per-task Unity instead of product .NET assemblies. Retain these boundaries. [Current foundation](../foundation.md#structure-and-dependencies) lists actual references/responsibilities.

<a id="포함하는-작업"></a>
## Included work

<a id="1-실행-준비와-관측-고정"></a>
### 1. Fix execution preparation and observations

- Default local Windows x64/existing NTFS-WinFsp/Unity fixture. Record actual OS-tools/source-Library file counts-sizes/free space-RAM/original Editor-MCP/existing ownership before execution. ARM A-B/reboot are separate NR.
- All Splinter conversations share one short home; A-B use distinct keys/returned absolute paths. Verify actual short-path write permissions/file tools/CLI/Editor targets. Codex controls are separate, never nested/shared ownership with Splinter.
- New captures require clean repository root/saved-imported-compiled source. Do not block subsequent original editing. Needed administrator helpers follow exact `nextActions`/existing privilege procedure.
- A-B each edit/verify/resume/finish in their conversation. Record per-conversation project/key/home/workspace/path as evidence, not automatic binding. No coordinating AI/automatic Runner. Create no conversations/workers while drafting this plan.
- **No Unity Agent Coordination in Splinter S or independent Codex C**. A-B work concurrently in independent instances. Only user-added Q uses parallel Codex coding/single-Editor sequential Coordination. Identify original/task Editors by exact paths/PID-start; record counts per method. Q candidate switches/shared leases apply only during Q.

<a id="2-pv-01-여러-차례-작업과-한쪽-선행-완료"></a>
### 2. PV-01: repeated work and one task finishing first

| Step | Case | Completion evidence |
| --- | --- | --- |
| 1 | Create A-B from same clean original; first script edit-verification after readiness | Distinct keys/workspaces/Git indexes/PID-start/snapshot-delta identities/request fingerprints/actual loaded code |
| 2 | Insert A compilation error; B completes second edit-verification; fix A on subsequent request | A failed/B successful intervals/request IDs. No errors-changes propagated to peer-original |
| 3 | After A-B creation, original edits-verifies separate test script; A-B retain earlier basis | New original content/loaded code/unchanged A-B bases-file hashes. Original test-owned changes explicitly committed before later integration for clean state |
| 4 | Resume existing tasks via new CLI/subsequent same-conversation requests; further edit-verify | Same project-key-home-workspace-snapshot-delta/existing changes/no duplicate workspaces-Editors. No actual app restart-context compaction means no such recovery pass |
| 5 | Explicit A completion: final verification → checkpoint → integrate → original Unity check → release; B retained | A result ref-tree-manifest/stable seal/original applied-execution values/owned reclamation. B PID-start-snapshot-delta/changes retained/original Editor-MCP retained |
| 6 | B edits-verifies again after A reclamation; separate completion: checkpoint → latest-original integrate → Unity check → release | B advances without new workspace; original includes A-B-original changes/two independent results/zero-absent test-owned resources |

Limit A-B-original changes to distinct scripts/nonconflicting locations. No combined conflict-resolution/uncommitted inheritance/new integration-candidate development. Preserve unexpected original changes/stop integration.

**Current checkpoint is final-result collection, not an intermediate editing save point.** `WorkspaceCoordinator` rejects start/refresh after collection as `ResultAlreadyCollected`. Perform repeated edits/resumes before checkpoint; do not prematurely checkpoint B. Do not add resumed editing from already collected results.

Current `integrate` checks verified result/clean original-target branch then Git-merges. **No product feature Unity-verifies a latest-original merged candidate before application.** Existing PV observes post-integration original refresh/loaded code; preserves failures/results-state, no automatic rollback/fake prevalidation. AI uses Git/external Unity procedures for precandidates/post-integration game verification. Splinter integration platforms excluded; gates retained.

Loaded code connects PID/project/time/domain generation/assembly MVID/execution values. Common Qualification fixture new `inspect` response must match request IDs; initial `snapshot-probe.json` alone is not fresh evidence. Product heartbeat does not call fixtures. Observe actually changed A-B functions through existing probes; augment fixture only when insufficient. Original executes via existing MCP. Assembly MVID alone does not confirm unexecuted functions.

<a id="3-pv-02-codex-기본-worktree와-실제-에셋-프로젝트-직접-비교"></a>
### 3. PV-02: direct Codex-default-worktree comparison with actual assets

**Direct Codex default-worktree comparison/Unity projects containing actual models-textures are required PV-02 scope.** Splinter-only measurements/synthetic files cannot finish PV-02. Distinguish methods C-S-Q from A-B conversations within each.

<a id="비교-방식과-공정한-출발점"></a>
#### Comparison methods and fair starting points

| Method | Workspace/Unity preparation | Comparison boundary |
| --- | --- | --- |
| C: Codex default worktree | Actual app creates independent worktree conversations A-B from same clean commit; run Unity per path | Include app default checkout/configured setup/Editor-Library preparation/resumption/results-cleanup. `git worktree add` alone is no Codex-session comparison |
| Q: Codex worktree + Unity Agent Coordination | Parallel coding in actual worktrees A-B; shared-Editor queue serializes verification | No Splinter. No worker Editors-Library; hand candidates to shared Editor. Include candidate commits/checkouts-transfers/queue wait/compile-import/original return/shared Library rewrite/two source-checkout space |
| S: Splinter NTFS | Each conversation creates A-B via `workspace ensure --storage ntfs-snapshot` from same commit-original; run Unity | Include snapshots/range journals/Git registration/verification-results-reclamation/helpers. Observe actual original-Library reuse/rewrite costs |

User explicitly added Q during 2026-09-23 execution. Previously removed Coordination was original preparation, not Q evidence. Restore previously used `v0.3.0` in Q-only basis; record resolved revision. Common sources-assets-tasks match C-S; disclose Coordination/single Editor differences. Without global skill, explicitly reference package-repository skill. Apply no skill/package to S.

Q connects coding completion/queue registration-acquisition/candidate transfer-checkout/compile-import/loaded code-screen/lease-return times. Do not assume Coordination copies worktree edits. Exact-SHA verification uses committed candidates/clean shared checkout; precommit asset work uses general access leases. Actual transport API handles states-leases-end; no direct state-file edits. Preserve original changes under separate refs/record return ref after candidates. Include A-error-to-B-switch delays. Q has no dual-Editor success criterion; compare identical final results/preservation/wait-inclusive whole time.

Q intermediate edits also use commit-optional FIFO; final results exact-SHA. Integrator prepares shared checkout at known basis; workers transfer/verify/reclaim only their changed files inside leases. Hash-verify/preserve generated `.meta`/scene changes in own worktrees; restore only temporary transfer copies. Integrator switches original refs without leases/records time. Disclose single Editor cannot retain original-A-B execution states simultaneously.

- Match PC/NTFS devices/Unity patches/common packages/build target/assets-import settings/two conversations/task contents/completion conditions. Q-only Coordination/serialized Editor intended. Match Codex versions/models/reasoning/common tools/original Editor conditions. Record path-length/volume differences; separate variables if needed.
- Official Codex managed-worktree guidance starts with Git checkout, allows additional ignored-file copying through `.worktreeinclude`/local setup. Fixture `Library` ignored/no `.worktreeinclude`. Verify app version/settings/files-Library presence immediately after creation. [OpenAI Worktrees](https://learn.chatgpt.com/docs/environments/git-worktrees), [Local environments](https://learn.chatgpt.com/docs/environments/local-environment).
- Main control is requested default flow. No hidden pre-copy/shared-Library optimization. Disclose existing such settings/distinguish defaults. Any Library-copy auxiliary control includes full copy-time-space-consistency costs.
- Original asset acquisition/import/Git-basis preparation is common environment cost, separately reported. Main interval runs new-task creation→actual edit readiness. Record starting Library; Splinter basis-Library reuse is verified behavior. Record Q-package preparation separately. Hide no preparation outside timers.
- Do not run cohorts simultaneously competing for disk-RAM. C-S A-B Editors parallel; Q code parallel/Editor serial. Interleave orders C→S→Q/S→Q→C/Q→C→S, recording actual orders/exceptions. Record OS caches/Unity Accelerator/package caches; no arbitrary global-cache deletion. Uncontrolled conditions are observations of that environment.
- Existing-task resumes reuse prepared workspaces. Q distinguishes next-candidate verification/same shared-Editor-Library restart. Measure key creation-readiness/repeated tasks at least three times per method; report individual/median/range. Full error-asset-first-finisher flow runs representative cases; fix common core work for other repeats in advance. Fewer samples are limited observations, no definitive improvement rates.

<a id="실제-에셋과-시험-규모"></a>
#### Actual assets and test scale

Retain small script fixtures as controls; add representative profiles with **importable real 3D models/textures/materials/referencing verification scenes**. Unity creates separate import artifacts/Library Asset Database; measure source Assets bytes separately from Library generation-rewrites. [Unity 6.3 Asset Database](https://docs.unity3d.com/6000.3/Documentation/Manual/asset-database-contents.html).

| Profile | Composition | Decision |
| --- | --- | --- |
| Basic | Existing scripts/current packages/common designated models-textures-scenes. Exclude full Medieval village pack | Required C-S-Q small-project fixed-cost control |
| Representative assets | User Medieval village actual models/1K-2K-4K textures/materials-scene references. Initial total source target 2–5GiB; fix actual composition | Required direct comparison. Manifest actual sizes-counts/mesh vertices-triangles/texture resolutions-formats/import settings/Library size. No 8K source textures in package, no 8K qualification |
| Expanded | After representative results/free space-RAM, diverse 10–20GiB assets | Optional observations. NotRun cannot become representative pass/general large support |

- Use user-imported Medieval village. Original folder has 1,512 files/2,798,526,062 bytes (2.61GiB), 151 FBX/textures-materials-prefabs-scenes. Preserve package originals; designated test copies for edits. No additional purchases/downloads assumed.
- No padding/meaningless model duplicates/arbitrary bytes renamed as images to meet size. Include large/many small-medium files; verify actual scene mesh-texture references/display. Disk presence alone is not Unity asset completion.
- Give methods identical asset and `.meta` hashes-GUIDs/common Git bases. Exclude currently rejected LFS-filters. Measure binary Git-object growth/checkout costs; shared Git counted once. Remote disclosure of paid-private assets/large fixtures excluded.
- Budget actual expected allocation for original Assets-Git-Library/C two checkouts-Library/S VSS-deltas-results/measurement files, not logical asset size alone. If insufficient, record reduced scale/unexecuted scope; no real disk-exhaustion expansion.

<a id="공통-작업-시나리오"></a>
#### Common work scenarios

| Case | Common goal | Main comparison |
| --- | --- | --- |
| New A-B | Create two same-basis tasks → Unity ready → verify scene models-textures loaded | Creation/first import-cache reuse/whole time-extra space-RAM until C-S dual Editors or Q serial verification usable |
| Assets unchanged/code only | Keep large assets, run PV-01 repeated changes/error isolation/resumption | Unchanged asset reads-copies/Library reuse/compile-reload/actual code costs |
| Same-task resumption | C-S reopen existing work-Library/continue edits; Q verifies next candidate in same conversation/shared Editor | Separate initial/repeated costs/same-task connection. Queue reuse is not Editor restart |
| Texture changes | Modify one designated valid texture; separately one import-setting change | Actual reimport/resolution-format/display/source-Library writes/journal-allocation growth |
| Model changes | Replace designated model with valid different revision/check referencing scene | Actual mesh-reference changes/reimport/whole-file rewrite-range costs |
| One finishes first | Preserve-apply-verify-clean A, B further work then completion | Whole successful-result time/application-cleanup/residual storage/peer-original protection |

Asset-only changes cannot substitute external MCP-CLI compilation/domain reload for import success. No Splinter built-in refresh-compilation evidence. Collect fixture import completion/Asset Database/actual meshes-textures/scene references/screens. Any extra code-verification changes use matching steps in controls, separately timed from pure reimport. Scoped observations do not implement general asset APIs/Play-Test. [Unity refresh procedure](https://docs.unity3d.com/6000.3/Documentation/Manual/AssetDatabaseRefreshing.html).

<a id="단계별-계측과-해석"></a>
#### Per-stage measurement and interpretation

Add observations to identical PV-01 candidate, connecting common C-S intervals/product-specific substeps. Do not force Codex to run absent `status`/seal commands; compare actual cost of same user goal. Unity-external synthetic storage remains auxiliary cause isolation.

| Interval | Required distinctions |
| --- | --- |
| Creation/Editor preparation | Common initial environment/per-task C-S preparation/checkout or VSS-metadata-Git registration-mount/import-readiness times/source-Library counts-sizes/initial payload-metadata |
| Normal status | Ten repeats without new pending responses; separate pending waits/new-result reconciliation. Per-call time/read/sourceVerification, legitimate source verification during reconciliation |
| Explicit checks/refresh | `--verify-source`/requests/compile-reload/reconciliation time-reads. Match `workspace.validation.success`/current fingerprint |
| checkpoint/integrate/release | Whole source-journal reads/result commits-refs/seals/original application-verification/unmount-Git-delta-VSS reclamation time-storage-calls |
| Runtime resources | Original-A-B Editors/children/providers working set-private bytes-handles/time samples. Sample maxima are not unmeasured whole-run peaks |
| Storage-scale controls | Same 4KiB writes/repeated reads on nonsparse-uncompressed synthetic 16MiB/256MiB/1GiB files. Record small-tree sizes-counts to separate metadata-payload |
| User/AI operation | Per-stage-conversation CLI-tool/retry-query counts/one-time privileges/manual keys-copying-Editor reconnection/interventions. Count normal edits/explicit completions separately. Record tokens-billing only if available, separate from device costs |

- Zero initial full payload copying/read hydration/small-write range storage are required contracts. Source fingerprint/journal seal read I/O is not zero.
- Specify counter origins/target processes/time intervals for read bytes. Distinguish logical/cache/physical I/O; unknown/reasons where inseparable. Add small development instrumentation if absent, not product backends/resident services.
- Separately record journal payload-metadata-file lengths/actual NTFS allocation/VSS diff areas/Git objects/actual Library rewrites. Volume VSS includes original-other process writes; no arbitrary per-task allocation. Unsupported values stay unknown.
- Synthetic storage does not replace actual model-texture import evidence. Compare two parallel C-S tasks; without single-task serial control, do not call differences parallelization speedup. Report stage absolutes/differences-ratios/whole time. Brief delays/single samples cannot claim general superiority.
- Record physical allocation immediately after creation/first ready/repeated edits/both running/after A reclamation/final reclamation. Separate preserved results/Codex recovery snapshots/shared caches/unreclaimed resources; disclose preservation costs. Assume no RAM savings/import skipping.
- Measurement completion differs from winning performance. Both methods need same results-preservation/complete evidence for PV-02. Report slower/higher-cost Splinter intervals unchanged for decisions. No arbitrary speed SLA/reduction pass criteria.

<a id="4-발견-결함의-수정-한도"></a>
### 4. Limits on fixing discovered defects

Obtain reproduction evidence/fix affected boundaries if current lifecycle cannot execute cases or may damage originals/other tasks/sole results. Ownership: Runtime state-result coordination/Git registration-integration/Windows snapshot-mount-observation/Unity verification connection. Core-Protocol changes update schemas/fakes/conformance/current docs together.

No optimizations before measurements/results. Installation-handoff are SW AI procedures/automatic binding excluded. Existing discard/new-environment boundaries follow owners. If fixes change candidate, preserve previous runs/reconnect affected cases/A-B preservation-reclamation on final candidate.

<a id="산출물과-완료-기준"></a>
## Deliverables and completion criteria

Later raw runs go to `artifacts/parallel-workflow/<runId>/`; verification to `docs/verification/parallel-workflow.md`, preserving actual records/final scoped decisions/failures/input-sample limits. Include environment/source-binaries/assets manifests/methods-profiles-repeat-cache conditions/actual Codex conversation-request-task connections/commands-responses/source-loaded code/import-display/results ref-tree-seal/stage costs/start-end resources/NotRun reasons. Use short home separate from evidence.

Completion requires all:

1. Actual identical-final-candidate whole PV-01: independent conversations/original edits/error isolation/resumption/A first then B continuation/final original behavior.
2. Separate lightweight status/explicit current-source verification; successful validation/current fingerprint/actual loaded code refer to identical changes.
3. PV-02 directly compares new-repeated work/completion-reclamation in basic/representative profiles across actual Codex independent C/Coordination single Q/Splinter S; designated texture-model changes in representative profile. Connect required storage contracts/stage time-read-storage-memory/calls-interventions/identical results to raw evidence. Missing controls/assets/required values mean no overall PV-02 Passed; separate observed subcases/unknown-unexecuted.
4. Preserve independent A-B results; verify zero owned processes-memory-handles/no mounts-Git registrations-deltas/final owned snapshots. Retain B/shared dependencies during A reclamation. Unknown is not zero.
5. Preserve original Editor-MCP/non-owned tasks/existing snapshots/user changes. Retry existing identities-receipts; no arbitrary forced deletion/new capture hiding failures.
6. Code changes complete standard build/relevant real-environment regressions/matrix-map-current-contract-UTF-8 synchronization. Docs-only changes distinguish document validation.

Judge PV-01/02 independently; one pass cannot fill another's gaps. Future proposals identify measured bottlenecks/interventions/evidence, not endless optimization.

<a id="이번에-포함하지-않는-범위"></a>
## Excluded scope

- SW installation/handoff/tool selection/new discard qualification. Automatic binding/writer takeover unimplemented.
- Whole NR preparation-reclamation interruption matrices/actual reboot-forced power loss-physical disk exhaustion-offline/ACL-special files-long path/ARM A-B.
- General asset-package authoring/Play/EditMode-PlayMode beyond representative import-limited edits; dirty-LFS-linked-worktree inheritance/preintegration candidate verification.
- Optional Runner-DAG/daemon-SQLite/premeasurement optimization/paid asset purchases/unlimited whole real-user projects/public releases/remote push.

Select only NR needed for input-environments claiming general support. Preserve PV NotRun-failures; whole SW-NR are not next mandatory tasks. Follow [reduced implementation plan](implementation-plan.md#retained-follow-ups-and-completion-criteria) responsibilities/actual costs/completion criteria.
