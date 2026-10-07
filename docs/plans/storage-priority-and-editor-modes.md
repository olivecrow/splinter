<a id="저장공간-우선-설계와-editor-실행-방식"></a>
# Storage-first design and Editor execution methods

[한국어](../ko/plans/storage-priority-and-editor-modes.md)

Current default CLI manages NTFS workspace lifetimes/Editor start-stop. Built-in refresh/compilation-focus/internal success gates were removed; checkpoint unifies result preservation. Optional single-view/Library caches/probes moved to [development CLI](../development-cli.md). Existing M0/WF/LB/scenario states below are historical candidate evidence, not automatic native passes for this candidate. Current regression limits follow [scope verification](../verification/workspace-product-scope.md).

Judge large Unity workspace capacity and creation/edit-readiness/resumption times together; Editor concurrency comes afterward. Default CLI manages independent Editors per task. Optional single-Editor source views/Library caches are storage paths under `splinter-dev.ps1 view`. AI/external tools verify projects/select modes; automatic mode selection excluded.

First scoped practical-use [M0](implementation-plan.md) closed **discard/minimal AI connection/representative workflow/cost decisions**. Preserve [representative use](../verification/representative-agent-workflow.md) small warm inputs/preparation-memory costs/unconfirmed total physical space-large efficiency. Library/validation remain existing explicit storage paths. Task ordering/Editor counts/modes/source-return merging are AI procedures; general lease takeover/queues-Coordination/automatic modes/shared original Editors excluded. Responsibilities follow [implementation plan](implementation-plan.md#program-and-agent-responsibilities).

<a id="후속-개발-우선순위"></a>
## Follow-up development priorities

**Current Windows/NTFS alpha physical storage closed scoped-complete-with-limitations under [PS](physical-storage-completion.md)/[direct evidence](../verification/physical-storage-completion.md), memory under [ME](memory-completion.md)/[direct evidence](../verification/memory-completion.md), selected parallel stability under [SP](stable-parallel-completion.md)/[direct evidence](../verification/stable-parallel-completion.md).** Order below guides product decisions. Select next tasks only for new actual shortages/leaks-sustained growth/preservation defects-work blockers/explicit support expansion, with fixed inputs/questions/completion criteria. Retain SF-02/03 Partial/large-OS-ARM NotRun/unknown whole instantaneous peak-VSS-system RAM attribution.

| Priority | Goal | Assessment |
| --- | --- | --- |
| 1: current alpha scoped closure | Physical space reduction | PS confirmed range storage/private reuse-compression/explicit compaction/small metadata/final A-B reclamation-residual costs. Separate allocation/Library/independent results-temporary evidence/shared VSS; support expansion separate |
| 2: current alpha scoped closure | Memory reduction | ME duplicate-write buffers/small-read scratch/same actual inputs-failure contracts/owned costs-reclamation. Separate providers/Editors-caches/pools-managed heap/system values; no whole RAM-reduction rates/new support states |
| 3: selected scoped closure | Stable parallel work | SP error classification/same-other-task locks/B queries-resumption-64 writes during A collection-reclamation/independent results-reclamation. Distinguish editorless boundary/earlier Unity qualification; select next units from actual new blockers-safety defects |

Original/other tasks/acknowledged writes/result preservation are prerequisites for every savings decision. Retain existing SF/NR/SW scope; select only needed real-input boundaries in this order.

User selected [VCS project-scoped basis transition](project-scoped-basis-and-reclamation-review.md) VP-01–05 as next space unit. Resolve project-work/volume-VSS mismatch by reusing fixed Git/Plastic versions in existing range journals. VCS-free new bases/existing independent-snapshot reclamation have lower priority/separate preservation. Closed units do not qualify new backends; no automatic kernel-driver/full Unity verification.

New Editor preparation **within minutes** is acceptable, not high-priority optimization. Agents can plan code/analyze source during preparation. Existing decisions assess workspace usability/Editor ready/current verification success; no new Runner/scheduler. Investigate/improve only observed bottlenecks when **tens of minutes or longer**, repeated stalls, or inability to progress blocks actual work. Keep timings, but a few minutes' reduction does not precede space-memory-stability with new comparisons/repeated benchmarks.

<a id="별도-지원-확대의-남은-범위"></a>
## Remaining separate support expansion

| Unit | Existing execution status | Reduced follow-up responsibility |
| --- | --- | --- |
| SF-01 change-based source verification | Defined source-verification scope Passed | General file-input support separate NR |
| SF-02 journal/storage | Small Library LB closed; overall Partial | Improve only real-input storage bottlenecks. Large targets/instantaneous physical peaks verified when needed, unconfirmed retained |
| SF-03 single Library verification | Existing WF/LB closed; overall Partial | Retain cache/candidate-generation/explicit-transition lifecycle-reclamation. Minimum external-tool connection. Queues/general lease takeover/original sharing excluded; generated-source merging AI-Git |
| SF-04 task resumption/independent results | NotRun | Select only used candidate-worker same-request/result/basis-loss-original-change boundaries. Full special combinations not mandatory |
| SF-05 large-input conformance | NotRun | Select used-input mount-Git/file I/O accuracy only. External tools author assets-packages; retain LFS-filter unsupported rejection |
| SF-06 default method selection | NotRun | AI procedure chooses using existing-path costs. Product automatic selection excluded; whole comparisons only when needed |

Development candidates/current contracts follow [validation view](../source-view.md). [Verification guide](../verification/README.md) records completed small tests/limits; no automatic large PV.

SF-02/03 Partial/SF-04–06 NotRun are evidence states, not unfinished-program counts. AI procedures/excluded implementation do not turn historical unexecuted items into passes. Select one actual bottleneck/completion criteria for storage-memory work; existing caches do not justify task queues/game automation expansion.

<a id="다음-판단에-필요한-근거"></a>
## Evidence needed for next decisions

- [BQ storage](../verification/journal-budget-and-growth-qualification.md): approximately 99.5% of small fixture live payload is Library. Replaced-record compaction alone cannot meet capacity targets.
- [Compaction-size regression](../verification/journal-compaction-size.md): preserve conditions where small-overwrite metadata increases space. Keeping smaller existing frames prevents growth; not whole-large-Library savings.
- [Identical private writes](../verification/duplicate-private-writes.md): compare repeated identical saved-range suppression/individual flush. Actual synthetic-journal allocation savings; actual Library duplicate share/whole savings unmeasured.
- [VC](../verification/immutable-validation-view.md): separate candidate publication ~1.3 seconds/repeated replay 21–29 seconds/Editor ready 75–84 seconds/normal stop 31–33 seconds. Publication time is not overall readiness.
- [JW](../verification/live-journal-write-cost.md): initial-import internal commit 848.16 seconds, flush 828.38. Preceding Windows callback serialization leaves Runtime-only group flush unselected. Callback atomicity/wait-completion/acknowledged-write durability need separate review.
- [WI](../verification/cumulative-write-range-index.md), [RI](../verification/replay-index-cost.md), [RU](../verification/unity-replay-comparison.md): distinguish identical closed journals/synthetic inputs/cumulative Editor resumes. Synthetic improvements/few warm samples are not whole-Editor/Library physical savings.
- [VSS protection](../verification/storage-recovery.md): exact-ID deletion alone does not guarantee other snapshots survive. Retain isolation guards/explicit delayed reclamation.
- [PS closure](../verification/physical-storage-completion.md): latest JM fixed T: A-B/compaction/independent results-reclamation/selected-task residual 1,048,304B. Separate shared runner/existing records-evidence/original Library; whole-space savings/instantaneous peak unknown. No whole-SF promotion to Passed.

<a id="sf-03-제품-연결과-남은-관문"></a>
## SF-03 product integration and remaining gates

Product `validation` CLI/actual Windows-Git-Unity providers/A-B-A across distinct VSS/same path-cache/worker-publication write blocking/independent Git results/product snapshot reclamation dependency checks were selected-scope complete in [WF execution](../verification/product-validation-workflow.md). Retain first cold-import interruption/recovery history. Final candidate two empty-cache repeats/raw response schemas/whole costs/preservation-reclamation judged separately in [WF-06–08](../verification/product-validation-cold-cost.md).

Common lifecycle/actual provider-CLI follow [candidate lifecycle](../view-workflow.md). [WF matrix](validation-matrix.md#sf-03-product-workflow-integration) judges these integration boundaries:

- Actual VSS confirms registered Git-pointer detachment/worker-write blocking/same owner-request publication recovery. Preserve existing `ExportCandidate` `.git` rejection/user-file protection.
- Separate A-B-snapshot-dependent candidate/source generations/view-owned caches. Cache retains first candidate's Library snapshot. Switch candidates across snapshots only with same project/compatibility/partition/budget/owner; no initial full copies/read hydration.
- Verify independent Git object existence/product snapshot dependency blocking. Test actual CLI interruption/lost responses; fake-exception injection passes are not actual-process outcomes.
- Final small actual A-B-A verifies normal stop/no mounts/same-path restart/actual code-assets/result independence/original-other tasks/owned reclamation. Separate whole additional file allocation/peaks/replay-preparation-resumption times.

Retain overall SF-03 Partial. FIFO/Coordination/general lease takeover/shared original Editors/automatic modes/dedicated installer-automatic binding excluded. Generated-source return merging/execution selection are AI procedures. Group flush unadopted; large PV/full NR support only when needed. Scoped M0 is not full SF pass. Retain [UA](../verification/storage-privilege-service.md), historical WF/LB qualifications/current same-request-lost-response-restart recovery.

<a id="다음-범위-판단"></a>
## Selecting the next scope

Preserved real JP journals led to separate JM-01–04 [default-metadata physical storage improvement](journal-metadata-storage.md), closed scoped-complete-with-limitations. Retain current SJR2 meaning/individual flush/results-reclamation guards; reduce default representations only in new writes/explicit compaction-candidates. [Direct evidence](../verification/journal-metadata-storage.md) is offline same-A-B NTFS allocation/extra serialization costs, not new native A-B/whole-workspace-RAM qualification.

Selected space work closed [Bee standalone-block candidate](../verification/library-block-compression-feasibility.md)/[standalone codec](../verification/workspace-payload-codec.md), then [compressed product journals](../verification/journal-payload-compression.md) small T: A-B/same-journal raw control. Distinguish model-standalone logical ratios/actual product file allocation. Retain whole-workspace space-RAM/large-OS/overall SF Partial; select next bottlenecks separately from needed real inputs.

[Partial-write/buffer/start improvements](../verification/resource-efficiency-followup.md) closed matching-prefix-suffix reuse in private overwrites/small I/O buffers/Editor start-current-source verification separation. New/extended/whole-content-change Library costs remain; judgment limited to fixed same-input space-rented-buffer comparisons/small local Unity lifecycle. No inheritance of whole Library savings/Editor-system RAM/readiness SLA/OS failures. Next targets are real new-fully changed Library records; no automatic repeated comparison/large-remote-installation/failure matrices.

[Small warm Unity comparison](../verification/unity-duplicate-private-writes.md) owns two edits/same ensure/normal restart/independent results-release/fixed Runtime before-after allocation on one registered input. Closed pair showed only small pre-completion journal differences; identical-private-write optimization does not achieve Library capacity targets. Most live private data is Library; choose one actual remaining new-changed Library target. No automatic repeats/large-cold-remote/new failure matrices/install-privilege-volume changes/original integration. Synthetic 96.1%/time SLA/memory savings are not actual results.

[NR-01/02 preparation interruption recovery](ntfs-recovery-and-support.md#fixed-scope-nr-0102-preparation-interruption-recovery), fixed after Library closure, closed with retained limitations at [26 actual current-candidate boundaries](../verification/preparation-recovery.md). Confirmed same-request resume/independent results/original-other tasks; unresolved publication-failure causes/large-capacity-mode SF gates retained.

[WF-06–08](../verification/product-validation-cold-cost.md) gives final two-empty-cache A-B-A/owned allocation-time/independent results-reclamation evidence. Rendering setup writes settings, so same generation restarts after configuration saves; extra Editor starts-stops count in repeat costs. Retain first-two-attempt failures/instantaneous physical peaks between observations/shared VSS attribution limits.

Small Library live amount/compaction-regrowth/new-resumed costs closed in [WF/LB](../verification/product-validation-unelevated.md); M0 costs in [scoped delivery](../verification/m0-delivery.md). Select cost follow-ups only from actual-input/path bottlenecks. Preserve [IPC](../verification/control-file-read-races.md) historical t2/unresolved heartbeat publication causes. Large space/full NR-PV only when needed; no preceding new backends/flush policies. Installation is AI procedure/automatic binding excluded.

<a id="범위-payload-codec과-다음-제품-연결"></a>
## Range payload codec and next product integration

Before product integration, **standalone range-codec common contracts/fakes/accuracy/corruption-partial reads/size-buffer boundaries/fixed Bee controls** closed scoped-complete-with-limitations. [Codec verification](../verification/workspace-payload-codec.md) owns implementation/decision. Only standalone state added to development capabilities/schema; default workspaces/current journals/installed service unchanged. Logical codec representation savings are not actual workspace/NTFS savings.

Product integration closed scoped-complete-with-limitations under [fixed scope](journal-payload-compression.md)/[JP verification](../verification/journal-payload-compression.md). Question: **can compressed ranges connect to current append-ACK/independent results/original-peer protection and reduce actual journal file allocation?** Below are closed inclusions/completion criteria, not repeat instructions.

| Inclusion | Completion criteria |
| --- | --- |
| Common frame-range contracts/fake conformance first | Specify encoded physical length/decoded logical length/fragment offset/metadata-checksum costs/single callback acknowledgment-flush. Count codec 48B/existing checksum duplication; raw selection cannot enlarge identical new frames. Retain whole-block restoration costs/buffer bounds for partial reads |
| Runtime write-read-replay/compaction-candidate integration | Retain current private-prefix-suffix reuse/partial overlap-resize-delete-rename/content-sequence-seal/same-request resumption/candidate-compaction overlap budgets/independent results. No initial full copies/snapshot comparison reads/read hydration/whole-file copy-up |
| Single current-version boundary | Update required current formats-versions/schemas-fakes-errors together. Preserve-explicitly reject unsupported old records-candidates-journals without conversion-overwrite. No arbitrary pending-resource-result rewriting-reclamation/old readers-fallback |
| Small actual changed-side-effect run | Compare existing fixed checksum/partial-frame/ACK-replay boundaries first, then fix only one registered T: Plastic/existing-home representative pair. Verify same-task resume/normal stop/independent results/B continuing during A reclamation/original-registrations-existing results/owned reclamation. Preserve exact keys-binaries-bases-budgets before execution |
| Actual space-memory decisions | Separate exact journal allocation/live-replaced payload/extra metadata/buffers-owned memory/failure costs on identical raw-candidate inputs. Unmeasured shared VSS/whole peak/native codec memory are not zero. Required build/schema-docs-UTF-8-diff |

Close only integration unit with contracts/content-recovery/scoped A-B/before-after allocation evidence. Preserve failures/fix only relevant boundaries if figures increase or functional-reclamation failures remain. Candidate 83% model/standalone tables are not product savings.

Product integration fixed/executed separately from standalone codec. Excluded new UAC/service-grant-volume/original integration-check-in/MCP/large-ARM-OS failures/flush-group commit/new backends/modes-queues-schedulers/comprehensive memory optimization. If installation/privileges actually needed, separately draft concrete plans. Existing C: non-owned blockers/pending snapshots unchanged.

<a id="보존한-한정-범위-일반-권한-unity-검증-흐름"></a>
## Preserved scoped unit: normal-user Unity verification workflow

Conditions below explain WF/LB closure/reproduction boundaries. They are not next-task instructions; rerun only needed subconditions if code affects boundaries or actual defects recur.

Unit: **complete small Unity A-B-A preparation/verification/resumption/independent result preservation/reclamation under normal-user permissions on the product including restricted service/IPC fixes, implementing/measuring Library storage lifecycle**. Fixed candidate applied to `development @ 183118b` executed five Library criteria/WF-09–12 and closed scoped-complete-with-limitations under [verification evidence](../verification/product-validation-unelevated.md). Below defines that closure/reverification criteria.

<a id="이번-단계의-library-완료-기준"></a>
### Library completion criteria for this phase

1. First verification view directly reads existing Library from its fixed immutable snapshot; no per-task whole-Library copies/read hydration/whole-file copy-up. Record actual cold imports with missing original Library separately. Keep cache basis snapshot across A-B candidates.
2. Reuse one fixed-path Library across generations/new CLIs. Isolate original-basis changes/inter-task writes, retaining exact snapshot/read proofs/lifecycle dependencies. Cache existence blocks its basis snapshot reclamation.
3. Separate current live ranges/replaced records; provide cache compaction verifying budgets/interruption recovery/content equivalence without Editor-provider-mount. Measure before-after/resumption growth/overlap peaks; reduce old records, never arbitrarily delete necessary Library data.
4. Actual Unity verifies cold/existing-Library reuse/A-B-A/resumption-repeated edits on identical input. Disclose total extra allocation/Library/shared VSS/Git-temporary-results/preparation-resumption-compaction times; check Library reuse did not merely recreate everything at a new path. Small synthetic tests alone cannot complete.
5. Independently preserve source-candidate results, then reclaim cache/basis snapshot in exact order. Update contracts/schemas/fakes/recovery-corruption rejection/capabilities/agent procedures/support matrices together. Unmet criteria mean Library incomplete.

<a id="선택-근거"></a>
### Selection evidence

- WF-06–08 confirmed small two-empty-cache costs/results, but before IPC fixes/privilege service. Not latest-product execution passes.
- IPC-01–03 confirmed actual Windows contention/synthetic-client preservation. New actual `prepare-worker`/Unity integration remain.
- UA-01–04 confirmed normal CLI NTFS-Git A-B/zero additional UAC. No Unity; worker results preserved with `validationClaimed=false`.
- Establish evidence of these changes working together and before-after Library improvement. Prefer identical existing-small-input conditions; no automatic closed-large-PV resumption.

<a id="입력과-자원-경계"></a>
### Input and resource boundaries

- Use Windows x64/NTFS/VSS/WinFsp/existing small `splinter-unity` fixture Unity `6000.3.22f1`. Read sibling AGENTS.md and fix basis revision/input hashes. Reuse existing three meshes/texture-code differences; no paid assets/representative large projects/new ARM Editors.
- Run cold/existing-Library trials sequentially. Each has workers A-B with distinct keys-VSS, one fixed view-cache, A→B→A source generations. Existing-Library trial adds new A generation after compaction to verify regrowth. Record original Library preparation separately; fix identical cold-warm source/Unity. Include required normal stop-restart/retry costs per generation preparation/final verification. One concurrent verification-view Editor; no Codex worktrees/Coordination/new coordinating agent.
- UA project-home grants are reuse candidates, not assumed currently usable. Read-only check installed package/current binaries/protocol/grant identities/service state/Editor occupancy/original-non-owned snapshots/reclamation guards before execution. No unnecessary service update for docs-only changes.
- Fix exact project-home/source volume/free space/journal budgets/measurement locations in manifest. Do not initialize existing C: snapshots/UA T: fixtures/independent results. If approved environment cannot fully reclaim, record blocker/create no new resources. Separate concrete new VHD-format-VSS budgets-grants-service preparation.
- Preserve original location/filesystem/continuous editing. No initial full copies/read hydration/whole-file copy-up bypass. Prerecord/protect original-other-task changes/result refs.

<a id="실행-단위와-완료-기준"></a>
### Execution units and completion criteria

| ID | Included work | Required evidence |
| --- | --- | --- |
| WF-09 | Normal-user current candidate/installed-service connection | Final source-CLI-provider-bridge/installed package hashes/environment manifest, unelevated calling CLI-Git-Unity in actual creation-publication-verification-reclamation/zero normal repeated UAC. Record service SYSTEM/initial administrator configuration separately |
| WF-10 | Actual one-empty-cache A-B-A/per-generation Unity verification | Same view-cache/normal stop-actual mount absence before switches/expected A-B code-models-textures/fingerprint-domain-MVID/request-sequence-seal matches. No A-only files/previous-generation outputs, raw schemas/Accepted follow-up observations |
| WF-11 | Normal unmount/same-task resumption | Observe immediately after actual `prepare-worker` unmount/reinvoke identical owner-request/new-CLI same key-ID-snapshot-delta reconnect. A subsequent edits do not change original candidate-B or inherit verification. With same-snapshot readers, retry after normal stop-unmount |
| WF-12 | Independent results/original-other-task protection/reclamation/whole costs | Preserve candidate/source generations/worker follow-up edits as exact-seal independent Git results. Reclaim dependent artifact→view-cache→worker; explicit same-workspace release retries when necessary. Reverify objects-content-hashes without snapshots/zero-absent owned resources/original-other-task-non-owned snapshots/whole allocation-time-failure costs |

Preview follows existing [fixture contract](../unity-render-qualification.md): first valid PNG from maximum two attempts per request; retain paths/hashes/per-pass pixels. If preparation Editor saves settings, normal stop/restart same generation for final verification. No ordinary-settings exclusions/weakened source-sequence checks.

WF-11 is limited to normal boundaries/same-request reinvocation/new-CLI reconnection. Unobserved transient contention is recorded unreproduced. No claims resolving historical t2 exact causes/all forced-stop-lost-response/reboot recovery. Retain automated regressions that do not hide corruption/access denial/other owners through retries.

<a id="수정-허용-범위와-산출물"></a>
### Permitted fixes and deliverables

Implement Library criteria/fix reproducible blockers in existing Runtime/CLI-Windows workflow/Unity adapters-bridges/IPC/privilege-service integration/tests. Do not weaken timeouts/privileges/preservation without causes. Public changes update schemas/fakes/conformance/current features together. New backend/flush/general lease-queue redesign needs separate scope first.

Preserve raw evidence in `artifacts/storage-foundation/library-20260929/`: raw requests-responses/commands-operational interventions/preparation-restart-verification-resumption-preservation-reclamation times/PNG-TRX/original-task-result hashes/resource observations. Do not overwrite/delete existing UA-WF-IPC artifacts. `docs/verification/product-validation-unelevated.md` owns interpretation/limits, linked from matrix.

Separate extra allocation/observed maxima for worker/candidate/source/Library-cache/Git-independent results/temporary-evidence files. Compare initial/A-B-A/A follow-up/post-reclamation, recording Library regrowth. Shared VSS is separate volume observation; task attribution unknown. File-allocation samples are neither instantaneous physical peaks nor achieved savings targets. No performance winner/default mode selection here.

Run final `pwsh ./scripts/build.ps1` locked restore/Release/tests/docs UTF-8-diff. If executables change, earlier trial passes are not inherited; reverify necessary whole flow on final candidate. Failure/diagnosis attempts count in raw records/whole costs.

<a id="마감과-제외-범위"></a>
### Closure and exclusions

Close only this small unit `scoped-complete-with-limitations` when all WF-09–12 evidence exists. Remaining `reclaim-pending`/unknown/environment blocks/unpreserved results mean incomplete; record exact remaining criteria. No non-owned snapshot deletion/automatic sweeps/forced termination to satisfy completion.

Judge Library completion only from five actual criteria; no expansion to general cold/full SF-SW/large support. Preserve historical SF-SW states/follow AI-excluded-verification classifications. Add no large PV/OS-ARM/installation-publication. Automatic modes/queues/general lease takeover/original sharing excluded; source-return merging/game verification belong to AI/external tools.

Library implementation-verification closure is limited to five criteria. New-input/candidate preservation defects require relevant fixes/verification. No automatic full NR/public-operation runs.

<a id="공통-저장소와-editor-정책"></a>
## Common storage and Editor policy

Workers edit independent sources without Editors/use existing immutable candidates explicitly. Verification views restart at same paths/Library only after normal Editor stop/no mounts. AI selects task order/modes; record actual waits/replay-restart costs. Preserve user original Editor ownership; no product sharing/takeover contracts. Do not block original writes throughout work.

Retain whole logical tree/GUID-meta/package dependencies, storing range reads/actual changes. No selecting only current-scene referenced files/whole-file copying substitute. Tools rewriting whole files accumulate writes; include compaction/independent Git result materialization costs.

First comparison metric is **total extra physical space/peak excluding original**. Separate worker deltas/view-Library/Git-LFS-independent results/temporary files/shared VSS. VSS is shared-volume observation, neither task attribution nor double-counted. Logical length/cumulative managed allocations/working set differ from physical space; unknown stays unknown.

With original-result preservation/input conformance prerequisites, follow physical space → memory → stable parallel work. Evaluate independent Editors when parallel gains justify added costs; preparation within minutes alone is neither adoption failure nor highest-priority bottleneck.
