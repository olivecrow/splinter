<a id="검증-안내와-보존-근거"></a>
# Verification guide and preserved evidence

[한국어](../ko/verification/README.md)

GitHub CI runs the full regression suite on Windows and checks locked restore/compilation on Linux. Linux runtime support is not qualified. The [first public Linux test run](https://github.com/olivecrow/splinter/actions/runs/38688426300) at `fc33088` recorded 1,301 passes, 101 failures and 313 skips, including Windows file-sharing/locking expectations and process-start timestamp identity checks. These failures remain unresolved for Linux; compilation-only CI must not be read as a Linux runtime pass. The local Windows publication check passed 1,691 tests with 24 skips and no failures.

[Retired native Bee backing](bee-internal-native.md) owns historical BN-01–05: the single-volume candidate passed bounded safety checks but was slower and larger, and its implementation and commands were removed. Measurements and recovery evidence remain; they do not qualify a current backend or full Unity Editor speed.

[Product bounded journal](bounded-journal.md) owns BL-01–05: actual journal metadata/lifecycle/recovery, bounded component costs and the new Windows-provider default. Full Unity and OS power-loss qualification remain separate.

[Structural throughput experiments](structural-throughput-prototypes.md) owns SX-01–04: historical native NTFS Bee and retained bounded durable range-log tooling on isolated small inputs. The rejected Bee code is removed; component timing, extra RAM/temporary space and source/result protection remain historical evidence, separate from unmeasured full Unity speed.

[Unity throughput optimization](unity-throughput-optimization.md) TP-01–06: Git proof reuse and bounded journal improvements; the cache-retention experiment was withdrawn after a real Unity reclamation failure. Component evidence, recovery and shared-Editor comparison are separated.

[Multi-stage Unity maze workflow](unity-maze-workflow.md) owns MW-01: six feature stages and three integrated playable mazes pass in both methods. The observed 1,501.9s Splinter / 787.5s shared-Editor workflows include recovery and do not establish a clean throughput ratio. Normal preparation/integration costs and original/resource closure are recorded separately.

[Unity workflow with parallel combined completion](unity-finalization-rerun.md) owns FU-01: `07bc2de` completes the actual Unity workflow in 768.3s, versus RC-01's 935.2s. A fresh paired shared-Editor run takes 94.0s. Parallel `checkpoint --release` includes normal Editor shutdown; input differences from history and the one-cohort limitation remain explicit.

[Finalization and decoded-payload reuse](lifecycle-finalization-optimization.md) owns LF-01–05. Its bounded measurements remain separate from FU-01 and the historical RC-01/SE-01 Unity results below.

**Shared-Editor comparison:** [SE-01](unity-shared-editor.md) measures parallel coding followed by one Editor's serial validation: 102.5s accounted workflow, including original-source recovery and cleanup; B waits 16.0s. It reuses RC-01's 935.2s Splinter result after input/runtime verification. Code writing is parallel in both methods; long Editor-operation crossover and shared-Editor storage savings are unmeasured.

**Current comparison:** [RC-01](unity-current-comparison.md) records ordinary Codex/Git worktrees at 819.7s and Splinter at 935.2s with current additional file allocation on the same source input. Both run A/B concurrently and preserve identical combined results. Input/allocation observation and chat startup exclusions, plus retained empty Codex roots, remain explicit.

**Previous Unity timing:** [UT-01](unity-time-memory.md), `7630970`, **14m 35.2s**, versus UF's 18m 03.0s. Same original source/Library and edits, now with overlapping A/B creation. Preparation/restoration improved while Editor and preservation stages grew; one run does not isolate each change's effect. The UF record below remains historical.

[Time and RAM optimization](time-memory-optimization.md) owns TM-01–04: scoped Library admission, Git/journal RAM caches and independent parallel preparation. Its small Editor-free fixtures and current regression evidence do not replace the recorded UF-01 full Unity timings.

**Previous full-workflow baseline: [UF-01](unity-final-lifecycle.md).** Committed `116fd44` measured 18m 03.0s total, 3m 36.1s Editor readiness, 2m 52.0s preservation/release and 2m 06.0s Library restoration. Original source/Library and A/B edits match; normal shutdown and original/owned closure pass without stop timeouts. RB/UR/UC/UP/US figures below are historical candidate evidence.

[External-tool coupling removal](external-tool-boundaries.md) owns automatic regression of bridge initialization/normal shutdown independent of missing MCP/internal API changes, and package run-state preservation. MCP/Unity CLI names/versions/patches/connection success are not product prerequisites. Tool-specific records below are historical candidate/input facts, not inherited as current support/installation requirements.

[Latest-only write-path optimization](latest-journal-write-optimization.md) owns current SJL4 capsule single payload/metadata recording, one durable flush per mutation, exact previous payload/metadata reclaim, and ON write -29.5%/-30.7% and ON/OFF file allocation -91.75%/-93.61% for identical repeated 8/32 PNG inputs. ON is 5.6–5.8× slower than OFF; cumulative managed allocation increased. The user selected default ON at that time based on 1,536 writes totaling 22.389s/~14.6ms average and file-allocation reduction. [BL](bounded-journal.md) owns the current bounded default for new Windows providers; existing stored selections remain unchanged. SJL2 historical costs/SJL3 intermediate 7–8% improvement preserved separately. File/owned-process interruption/WinFsp fake-basis qualification, not inherited Unity/VSS/OS-power/full-peak qualification.

[Security boundary verification](security-boundaries.md) covers only changed Plastic private-reader/MCP authentication/service ACL/lock/reception-limit boundaries. Historical Unity A/B qualification retained; current-reader Unity execution/installed-service updates not automatically passed. MCP v2 records historical external candidate removed from current scope.

[C: reclaim/MCP response recovery](vss-and-mcp-remediation.md) owns historical same-A release/B discard closure and external Coplay patch execution/duplicate prevention/reload details. C: pending reclaim closed; initial-failure reproduction conditions/ownership/recovery evidence remain in that verification. Historical MCP candidate results distinguished from current authentication/transport unexecuted boundaries in [security verification](security-boundaries.md).

[Plastic original apply](plastic-original-apply.md) separately qualifies recorded-basis full/selected publication, registration/lock/Editor protection, same-intent recovery.

[README Unity comparison](readme-unity-comparison.md) owns RB-01–05: actual Codex app worktrees versus the then-current default-ON Splinter candidate with the original Library basis, two independent Editors per method, identical A/B edits, additional file allocation and separately measured setup/Editor time. The single run reduced edited-ready allocation by 98.65% with longer Editor preparation and recompilation. Failures, cleanup, one-cohort/cache limits, unmeasured RAM/whole-disk peak, and unexecuted Play/Test/build remain explicit.

[Checkpoint/release profiling](checkpoint-release-profiling.md) owns LP-01–04: reused Unity phase records, tiny Editor-free Git/Library tasks with scratch-only timing scopes, and a bounded fence-membership microprobe. It identifies repeated verification/recollection and quadratic reclamation; no optimization or expanded support pass is claimed.

[Checkpoint/release optimization](checkpoint-release-optimization.md) owns CO-01–08: attribute batching, result reuse, combined checkpoint/preservation, live accounting, indexed inventory and request wakeup. The follow-up tiny comparison reduced checkpoint 50.84% and release 29.39% against the already optimized binary. Guard/schema regression and original/owned cleanup passed; CO remains tiny-fixture qualification; subsequent real Unity remeasurement is recorded separately in UR-01–04. Expanded support remains NotRun.

[Unity lifecycle remeasurement](unity-lifecycle-remeasurement.md) owns UR-01–04: identical source/Library input, A/B checkpoint/release 421.3806s, initial compilation 1,041.8831s and edit recompilation 76.9s. Stop-command timeout, same-task continuation, profiler overhead, scheduling differences, original restoration and further opportunities remain explicit.

[Lifecycle read/restoration optimization](lifecycle-read-optimization.md) owns LO-01–11: bounded Git proof/session reuse and separated base-read gating, fused seal reads, path checks and unchanged restore/cursor batching, whole-Library restoration and immutable listing reuse following UR. It records small real NTFS costs and full corruption/recovery regression, not its own Unity timing qualification; later combined timings are UF-01.

[Historical Unity checkpoint/release measurement](unity-checkpoint-release-remeasurement.md) owns UC-01: committed candidate `168081c`, unchanged real source/Library input, 304.9s for sequential A/B checkpoint/release and 180.9s for original Library restoration. Independent results, original protection, owned resource cleanup and 12 response schemas pass. Stop commands still time out before normal exit; the earlier profiled/current unprofiled single cohorts do not establish an isolated causal speedup.

[Startup journal optimization](journal-startup-optimization.md) owns JS-01–04: retired footer references, affected-file membership lookup and bounded metadata verification. It records small normal-binary append costs and byte/seal/reconnect/corruption regression; it does not replace UC-01 with an unmeasured Unity startup speedup.

[Startup time optimization](startup-time-optimization.md) owns ST-01–04: independent immutable reads, SJN2 append metadata pages and identical owned bridge reuse. It records the historical SJN2 format boundary, bounded time comparison and concurrency/recovery regressions; UC-01 remains historical.

[Actual Unity startup profile](unity-startup-profile.md) owns UP-01: the committed JS/ST candidate reaches initial readiness in 766.9s on the same source and Library entries. It separates unchanged Git-read-dominated scanning from faster compile/output work, records sampled CPU/I/O and exact Bee input differences, and verifies owned reclamation/original restoration. One profiled cohort does not establish an isolated speedup; further optimizations are analysis only.

[Startup profile follow-up](startup-profile-optimization.md) owns SU-01–05: implementation and bounded cost/guard verification for the Git read gate, repeated journal path checks, no-op Cleanup, bridge assembly and source timestamps found in UP. The tiny actual Unity assembly check and read-only original capture cost remain separate from the subsequent US-01 cohort.

[Unity startup remeasurement after SU](unity-startup-remeasurement.md) owns US-01: 349.8s initial readiness versus UP's 766.9s, 16s scanning and about 210s compile preparation. It preserves the initial admission failure, committed correction, same-workspace mixed-runtime resumption, repair pause and slow stop. ILPP verification delay, Library availability, source proof and journal publication are remaining analysis targets. All 14 responses and original/owned closure pass; one cohort is not an isolated causal speedup.

[Additional startup optimization](startup-additional-optimization.md) owns SA-01–05: pinned Library lifetimes and restoration handoff, local Close checks, single-handle publication, content-proven Git hashing, operation-local ensure proof and first-launch inherited Bee graph invalidation. Bounded native/Git/Bee evidence does not qualify new full Unity readiness or a 60-second ILPP saving.

[Shutdown and preparation costs](lifecycle-cost-optimization.md) owns LC-01–03: small-write metadata scaling, client root-metadata initialization and scoped seal/reclamation checks. It distinguishes bounded NTFS costs/recovery from historical US times; subsequent combined Unity execution is UF-01.


[Actual Unity C/S comparison](unity-live-comparison.md)/[HTML report](../../reports/unity-live-comparison-20261003.html) compare designated-input/file-allocation/Editor/provider memory/isolation/resumption in Codex app worktrees/product workspaces. Missing historical home fields/authorized follow-up reclaim distinguished in owning verification. Total physical/VSS attribution/minimum RAM unknown; no full SF/PV support expansion.

[Unity parallel-edit verification](unity-parallel-editor-validation.md) owns bounded C/S A/B exact MCP targets/resources/scenes/current-state tests/original apply/reclaim. Actual test overlap/reload-result preservation/reconnect/owned reclaim/initial failure/recovery costs follow text verification. [HTML](../../reports/unity-parallel-editor-validation-20261003.html) records through S. Editor RAM savings unconfirmed/full physical peak/VSS attribution unknown retained.

[Physical-storage stage final verification](physical-storage-completion.md) judges fixed-JM-candidate T: A/B/initial duplication/read hydration 0/compaction/independent results/reclaim/residual file allocation/ledgers/regression scoped-complete-with-limitations. Separate SF/NR support expansion/shared VSS/full instantaneous peak unknown retained.

[Memory-stage final verification](memory-completion.md) closes current Runtime compressed-write/partial/empty-read buffers/byte/nonpublication/replay semantics/same actual journals/new product A/B owned costs/independent results/reclaim scoped-complete-with-limitations. Total RAM/native codec/instantaneous peak unknown retained.

[Selected parallel-stability unit](stable-parallel-completion.md) closes incorrect busy guidance for lock-path errors, B observation/same-request/64 writes during actual A checkpoint/release, independent results/reclaim scoped-complete-with-limitations. Editor-free CLI/file-I/O boundary; global VSS list unknown; prior Unity/PV/large/OS qualification not inherited.

Current plain-folder original-apply candidate follows [separate verification](plain-folder-original-apply.md). Ordinary-user file tests distinguished from bounded closure of small Editor-free actual VSS/WinFsp A/B.

Current default CLI provides NTFS workspace lifecycle/Editor start/stop/checkpoint. No own refresh/compile/focus handling/internal success gate. Optional view/Library cache/probes use [development CLI](../development-cli.md). M0/WF/LB/scenario states below are historical-candidate evidence, not automatically native passes under changed contracts. Current automatic-regression limits follow [scope verification](workspace-product-scope.md). Status meanings in [glossary](../glossary.md#responses-and-verification-status).

<a id="현재-변경의-검증"></a>
## Verification of current changes

[Historical SJL2 latest-only journal](latest-only-journal.md) owns SJL2/root v2/SJT2/v3 compact intent repeated multi-file writes/current-payload reclaim. Actual PNG 8/32-file OFF/ON 3 runs each: final allocation -91.81%/-93.74%; ON constant every cycle. 32-file write median OFF 3.998s/ON 33.351s, so default OFF then. Current defaults follow subsequent write-path optimization. Required WinFsp normal close/reconnect/owned-child interruption/composite seal/accounting/full regression distinguished; Unity/VSS/large qualification not inherited.

[Optional basis-reversion compaction](journal-base-reversion.md) owns previous append/v2-intent candidate actual 2.8GB Unity-project recorded Git input/synthetic OFF/ON 3 runs each/fixed 7 actual-child-termination boundaries for 2 bases/editorless WinFsp A/B independent results/owned reclaim. Historical final allocation -74.76%/whole measured time +49.41% recorded together, retaining default OFF. Initial runner access denial/explicit same-task resume/unconfirmed Unity import/total instantaneous peak preserved, distinguished from new SJL2 qualification.

[Multi-file time optimization](journal-base-reversion-optimization.md) owns separate actual PNG 8/32 repeated edits/36 before-after stopped NTFS journals' compaction time/hash/metadata/sequence. ON compaction -79.47% for 8 files/-90.19% for 32, preserving original/refs/final allocation. No-reversion total costs/write variation recorded together, retaining historical default OFF; previous Unity/WinFsp qualification not inherited by new candidate.

[OS/application extensibility contracts](extensibility-boundaries.md) own automatic conformance of immutable path policy/permission metadata/injected application companions/original reservations/generated-directory descriptors. Actual macOS/Linux/other apps/new Unity/VSS A/B NotRun.

[Code-optimization regression](code-quality-optimization.md) is contract/file/fake-journal testing of six reviewed items above `8873ba6`. Verifies exact next-action basis/selection arguments/shared-object reverification/restore search/rename replay/block traversal cost. Excludes new Unity/VSS/service execution/full-support qualification of closed stages.

[VCS-free project basis](plain-project-basis.md) owns candidate-specific qualification of new `project-snapshot`, authorized whole-project original view, source/Library range separation/NTFS restore, small files/actual Unity MCP/scenes/resources/current-state tests/costs. Existing VSS plain-folder/Git/Plastic success not inherited.

[Git project basis](git-project-basis.md) owns contracts connecting recorded commit/tree/blob to range journals without VSS and small Editor-free Git A/B qualification. Separates initial payload duplication/read disk cache 0 from shared objects/runner/full-blob decoding costs; Plastic/Unity Library/Editor/MCP/large/ARM/OS failures not inherited by this path.

[Actual Git Unity verification](git-unity-validation.md) owns separately authorized current `splinter-unity` new Git-basis A/B/cold Library/exact HTTP MCP/resources/scenes/current-state tests/owned reclaim/observed preparation bottlenecks. Distinguishes ~88-minute cold import/reload disconnect/lost failure details/unconfirmed RAM savings from prior VSS qualification.

[Git original Library reference](git-library-reference.md) owns authorized Library-only managed mount/fixed original/A/B basis/range writes/final ordinary NTFS restore/new warm Unity MCP/RAM verification. Cold execution/small-file interruption tests not inherited as actual warm Unity qualification.

[Plastic project basis](plastic-project-basis.md) owns fixed changeset/blob reader/VSS-free range overlay/shared original Library/fixed actual Unity A/B automatic/actual qualification. Exact authorized input check-in/first import/tool timeouts/ordinary-user revision helper/external cache/transfer unknown distinguished; RAM savings excluded from acceptance.

[Journal default metadata](journal-metadata-storage.md) owns implementation retaining current SJR2 semantics/individual flush and offline NTFS before-after comparison with preserved actual Unity A/B journals. Existing payload-compaction effects/additional serialization cost/new native lifecycle unexecuted/full workspace/RAM unconfirmed distinguished.

[Compressed journal product integration](journal-payload-compression.md) owns bounded overlay v2/SJR2/candidate v3 regression/small T: Plastic A/B/actual NTFS allocation for same-input journal raw comparison/independent results/reclaim. Distinguishes whole storage/RAM/large/OS qualification from independent codec logical samples/prior native qualification.

[Unity CLI A/B handoff](unity-cli-handoff.md) closes exact-returned-path catalog/loaded edits/A stop/release then subsequent B compile/two independent results/final snapshot reclaim/original protection scoped-complete-with-limitations on existing CLI/Pipeline/small T: Plastic. Procedure supplemented without program changes; list omission/first eval timeouts/MCP/other-input/Play/Test/build unqualified remain.

[Plastic compatibility](plastic-compatibility.md) separately tracks automatic conformance of regular/static original management/registration isolation/independent file results and fixed-candidate actual T: A/B/final snapshot reclaim. External CLI worker discovery failure/full handoff unqualified retained. Current C: reclaim closure follows VR-01 above.

[VCS-independent lifecycle](vcs-independent-workspaces.md) distinguishes plain-folder automatic file/fake/Git regression from actual VSS/Unity A/B/post-fix same-request reclaim scoped-complete-with-limitations. Candidate-specific execution/list omission/storage/unexecuted boundaries retained. New state v2, no automatic previous-state migration.

[Bounded M0 delivery judgment](m0-delivery.md) closes bounded existing development CLI/source skill/[AI start guide](../agent-start.md) version scoped-complete-with-limitations. Compared current recovery regression/historical-native source hashes/change boundaries, not inherited as all current native interruption passes. Remaining full NR/SF/SW is follow-up support expansion, not M0 progress denominator.

[Representative AI A/B use/cost](representative-agent-workflow.md) bounded closure covers independent Editor error/fix/same-task resumption/independent result/dirty guard/integration/B during A release/current compile-failure B discard/actual reclaim. Includes first seal/focus rejections/same-task retries/minute-scale preparation/memory costs. Physical peak/VSS attribution unknown; native forced termination/OS failures/ARM/large input excluded.

[Explicit discard verification](workspace-discard.md) distinguishes automatic qualification from approved service Update/local Editor-free dirty A/B actual VSS discard/same-request resume/peer continuity. Execution itself scoped-complete-with-limitations, distinct from current M0 representative Unity qualification. Full SW-09 Partial/forced termination/SW-10 unexecuted retained.

[Minimal AI workflow integration](agent-workflow.md) verified direct source-skill read/CLI discovery/returned absolute-path editing/new-CLI same-task resumption/explicit/direct/explicit discard. Editor-free unit scoped-complete-with-limitations; full SW-03/06 Partial. Current Editor qualification follows representative run above, not expanded to automatic discovery/installation/binding/actual busy/unknown occupancy/new/simultaneous conversations.

Standard entrypoint `pwsh ./scripts/build.ps1`: locked restore/Release/automatic tests; results in `artifacts/test-results/`. Current contracts require VSS v2 capture identity/full overlay verification tuple/`stable-journal-v1` results/required measurement fields. Unsupported formats preserved/rejected without automatic migration.

Historical test/exclusion counts come from their TRX. Old exclusion counts for retired backend tests are not current execution conditions/remaining required verification. Distinguishes [ReFS retirement decision](../plans/refs-retirement.md)/[current removal verification](refs-retirement.md).

Automatic tests cover current-format creation/reads/incomplete-input rejection/checkpoint interruption retries/preservation under unknown ownership/snapshot loss/response loss. Development request-generation script tests use only synthetic receipts/files, not substitutes for actual VSS/WinFsp mounts/Unity execution.

<a id="지원-경계와-남은-관문"></a>
## Support boundaries and remaining gates

First actual-use version exit criteria in [M0](../plans/validation-matrix.md#first-practical-use-version-m0). NR/SF/SW/48-case catalog below preserve existing support evidence/unfinished conditions. Full support expansion not M0 completion denominator; existing subordinate successes not inherited as passes for new representative A/B. [Product-scope verification](workspace-product-scope.md) covers excluded task-graph discovery/rejection contracts/automatic regression.

| Area | Verified scope | Remaining work/interpretation limits |
| --- | --- | --- |
| NI/LV product NTFS | Small-fixture VSS/Git/Windows x64 Unity/reconnect/independent results/reclaim | Full NR support unfinished. Actual reboot/power/physical storage failures/all command boundaries separate |
| NR | State/journal corruption protection/file-attribute rejection/current-boot live identity/ARM single workspace/[26 preparation CLI interruptions](preparation-recovery.md) | NR-02 scoped-complete-with-limitations. Publication-failure cause unresolved; explicit resume/reclaim vs fresh-key repeats distinguished. Other files/assets/write/result/reclaim interruption/ARM A/B/OS failures unfinished |
| NR-03 common journal | [58 actual child terminations on fake immutable basis](ntfs-recovery.md#nr-03-actual-process-termination-at-common-journal-boundaries), acknowledged prefix/post-flush transaction/tail/second replay | Common unit scoped-complete-with-limitations. Actual WinFsp callbacks/mmap/VSS/concurrent readers/writers/OS failures unexecuted; full NR-03 InProgress |
| NR-03 local kernel | [19 actual WinFsp callback/mmap/new-provider cases of separate candidate](ntfs-recovery.md#nr-03-local-winfsp-callback-mmap-and-new-provider-connection), acknowledged data/same-path reconnect/pre-opened other-file Read/resource reclaim | Local unit scoped-complete-with-limitations. Initial reader exit timeout/new open-close simultaneous responses unqualified retained. Actual VSS server/CLI/ARM/OS failures NotRun; full NR-03 InProgress |
| NR-03 product client | [13 separate-candidate actual VSS/CLI external boundaries](ntfs-recovery.md#nr-03-client-boundaries-for-product-vss-and-cli), same snapshot/delta/Git registration/new read proof/provider/independent result/normal reclaim | External product-boundary unit scoped-complete-with-limitations. Preparation-state publication cause unknown/Git text-fixture failure preserved. VSS internal callbacks/OS flush/concurrent I/O/Unity/ARM/reboot/power/storage NotRun; full NR-03 InProgress |
| NR-01 state-write diagnostics | [Failure stages/publication state/machine errors/16 actual-file tests](state-write-diagnostics.md) | Diagnostic unit scoped-complete-with-limitations. Actual Windows I/O/new owned diagnostic Git CLI/common/schema/fake scope. Prior NR-02/03 publication-failure causes unknown/full NR-01 InProgress. Separate from VSS/WinFsp/power/storage gates |
| PV-01/02 | Representative fixture/user-selected comparison scope scoped-complete-with-limitations | Not full PV-02 Passed. S4 FailedInputQualification/few samples/full-pack raw equivalence unknown |
| SF-01/02 | Change-based source checks/subordinate explicit compaction/overlap budget/measurement | Initial common-storage FailedPreservation retained. Full SF-02 Partial/large capacity goal unmet |
| SF-03/VC/VR | Development small A/B/A/normal cold import/reconnect | Full SF-03 Partial/VR-03 Partial retained. Queue/general lease takeover excluded; source-return merge AI procedure. Current product integration distinguished as WF |
| SF-03/VW | [Common lifecycle](../view-workflow.md) durable state/receipt reconciliation/fake conformance | Synthetic files/receipts, not actual VSS/WinFsp/Unity support evidence. Automatic execution VW/product integration WF |
| SF-03/VG | [Registration-proven management-file separation](../source-view.md#separating-management-files-with-registration-evidence) actual temporary Git registration/synthetic immutable-tree conformance | Candidate v2 publication automatic-test scope. Actual-provider execution separately judged by WF current-candidate evidence |
| SF-03/VP | [Same-request publication recovery](../source-view.md#same-request-publication-recovery) durable intent/partial-journal prefix/completion receipt reconciliation | Synthetic tree/actual temporary files/exception injection. Actual CLI interruption WF; reboot/power/storage separate NR gates |
| SF-03/WF | [Product integration/recovery](product-validation-workflow.md), [two empty caches](product-validation-cold-cost.md), [current-candidate ordinary-user cold/existing Library](product-validation-unelevated.md)/independent results/reclaim | Scoped-complete-with-limitations. Small-fixture preparation/validation restarts/focus retries/heartbeat-error limits retained. No large capacity/general cold stability/full NR proof |
| LB-01–05 | [Actual Library snapshot reuse/compaction/regrowth/reclaim](product-validation-unelevated.md) | Fixed-candidate Library lifecycle scoped-complete-with-limitations. Whole instantaneous physical peak/VSS attribution unknown/full SF-02/03 Partial |
| RI/RU/WI | Bounded replay comparison of synthetic or same closed journal | Warm cache/few samples. Not converted to cumulative whole-Editor improvement/physical Library savings |
| JW/FR | Fixed-write measurement/small-URP per-request valid-PNG inspection | Windows prior serialization/durability retained. Unity internal first-draw cause unresolved/group flush unselected |
| SW-09 discard | [Local dirty A/B service application/same-request resumption](workspace-discard.md)/[representative failed Unity worker stop/discard](representative-agent-workflow.md) | Subordinate executions scoped-complete-with-limitations/full SW-09 Partial. Native forced termination/SW-10 unexecuted |
| M0, SW-03/06 | [Minimal AI instructions/direct](agent-workflow.md)/[representative Editor use/termination/cost](representative-agent-workflow.md)/[current recovery regression/bounded delivery](m0-delivery.md) | Bounded M0 closed, not all current native/OS failures passed. Full SW-03/06 Partial; busy/unknown occupancy/new/simultaneous conversations/automatic installation/binding unexecuted |
| SF-04–06, SW-01/02/04/05/07/08/10 | Preserved scenarios/follow-up responsibility classification | NotRun retained. Large/special support checked when needed; mode/installation/handoff AI procedure; automatic modes/dedicated installer/binding excluded. Full conversation/Editor flow unqualified |
| UA-01–04 | [Restricted privilege service](storage-privilege-service.md) actual installation/ordinary CLI A/B additional UAC 0/reclaim/service restart/response loss | Registered Windows x64 NTFS/Git fixture Passed. No expansion to general Unity/ARM/actual power/reboot/forced termination during native VSS calls |

Historical records below are not inherited as new-candidate passes before required real-environment tests. GUI Unity/VSS/isolated-disk tests run after verifying defined scope/owned resources. Preserve current original/other tasks/existing result refs/non-owned snapshots.

[WF-09–12/LB-01–05](product-validation-unelevated.md) closed through actual execution of fixed candidate in that record. [NR-02](preparation-recovery.md) separate final-candidate preparation boundaries, no automatic prior Unity/Library inheritance. Execution/reverification criteria owned by [storage-priority plan](../plans/storage-priority-and-editor-modes.md#preserved-scoped-unit-normal-user-unity-verification-workflow); remaining general support/large capacity/installation gates by [tracking table](../plans/validation-matrix.md).

<a id="후속-작업에-필요한-기록"></a>
## Records needed for follow-up

These materials preserved as reproduction conditions/failure evidence/comparison baselines, not a changelog. Read each candidate/input/date/limitation together. Raw artifacts may be local; documentation cleanup does not delete them.

| Record | Preservation reason |
| --- | --- |
| [Bounded M0 delivery](m0-delivery.md) | Current recovery-contract regression/existing native-source comparison/reuse limits/actual start-command observations/bounded development-CLI completion |
| [Representative AI A/B use/cost](representative-agent-workflow.md) | Small warm-input error isolation/same-task resume/independent result/dirty guard/integration/failure discard, post-exit Library lock writes/focus rejection/same-task retry/time/allocation limits |
| [Product scope](workspace-product-scope.md) | Excluded task-graph command/schema discovery/error-response contracts/current automatic vs real-environment qualification |
| [Restricted privilege service](storage-privilege-service.md) | Completion judgment separating protected state/IPC/initial installation/repeated UAC/service interruption/response loss |
| [VSS preservation failure](storage-foundation.md), [deletion blocking/recovery](storage-recovery.md), [deferred reclaim](deferred-snapshot-reclamation.md) | Exact-ID deletion collateral effects/isolation guard/explicit same-task retries/reclaim order |
| [NTFS product integration](ntfs-workspace-integration.md), [live identity](ntfs-live-vss-identity.md), [NR recovery](ntfs-recovery.md) | Actual Windows/ARM combinations/unfinished failure/file gates/environment-specific reproduction |
| [Preparation CLI interruption recovery](preparation-recovery.md) | 26 actual same-final-candidate boundaries/concurrent resume/child generation/independent result/original preservation/reclaim/publication-cause unresolved/explicit recovery |
| [Parallel comparison](parallel-workflow.md) | C/S/Q same-input conditions/result refs/Library/queue/transition cost/S4 input failure/sample limits |
| [Initial overlay tests](snapshot-overlay.md) | Bounded actual NTFS file API/ownership evidence, not combined as full NTFS support |
| [Historical retired ReFS evidence](storage-and-resources.md), [removal verification](refs-retirement.md) | Distinguishes preserved SR evidence/current feature-contract removal automatic regression |
| [Journal compaction](journal-compaction.md), [Unity/compaction costs](journal-compaction-qualification-and-cost.md), [budget/regrowth](journal-budget-and-growth-qualification.md) | Interruption recovery/two-generation overlap/whole-allocation peak/live Library size/post-resume regrowth |
| [Journal compaction size regression](journal-compaction-size.md) | Conditions where small overwrites enlarge journal/existing frames retained/same-synthetic-input NTFS allocation comparison |
| [Identical private writes](duplicate-private-writes.md) | Byte equivalence/whole-private-range duplicate suppression/metadata/flush/budget/recovery/small local WinFsp client completion |
| [Small Unity identical-write comparison](unity-duplicate-private-writes.md) | Actual product CLI/VSS/WinFsp/Unity warm edit/restart pair/journal/Library/same result/reclaim/observation differences |
| [Partial writes/buffer/start improvements](resource-efficiency-followup.md) | Private matching-prefix/suffix reuse/request-sized buffer fixed-input comparison/local start/restart/current verification/preservation/reclaim |
| [Library payload compression candidate selection](library-block-compression-feasibility.md) | Recent UH cumulative costs/exact restoration/size model of 4 Bee files/24 combinations. Next bounded-codec candidate, product/physical savings unqualified |
| [Independent range codec](workspace-payload-codec.md) | Core/fake/raw/Brotli/corruption/trailing rejection/partial reads/lease return/concurrent callers/actual Bee representation comparison. Product journal not integrated |
| [Common Library A/B/A](immutable-validation-view.md), [preparation/replay](validation-view-readiness-and-replay.md) | Separates immutable candidates/validation generations; normal stop/remount/restart costs/initial-import limits |
| [Library lifecycle/ordinary-user execution](product-validation-unelevated.md) | Fixed-snapshot Library/range writes/compaction/regrowth/cold comparison/15 independent results/reclaim/failure-recovery limits |
| [Product validation workflow](product-validation-workflow.md) | Actual CLI integration/different VSS/one view/cache/code/assets/PNGs/generation separation/CLI interruption recovery/independent results/post-VHD-detach verification |
| [New-cache repetitions/full costs](product-validation-cold-cost.md) | Final-candidate two-empty-cache A/B/A/2-minute normal-exit wait/preparation-validation restarts/raw response schemas/allocation including failures/16 independent results/owned reclaim |
| [Control-file read contention](control-file-read-races.md) | Windows concurrent Replace/guard/open absence/sharing errors/bounded read recovery/persistent failure/corruption/owner preservation. Distinct from actual WinFsp/VSS/Unity requalification |
| [Replay index](replay-index-cost.md), [actual Unity comparison](unity-replay-comparison.md), [range index](cumulative-write-range-index.md) | Fixed-input/candidate hashes/stage replay/actual Editor-cost comparison baselines |
| [Live writes](live-journal-write-cost.md) | Internal flush cost/callback serialization/wait/completion boundaries/future durability decisions |
| [First preview](first-render-qualification.md) | First-draw cause unresolved/cold/reload/restart/failure injection/PNG-hash interpretation baseline |

Scenario execution/follow-up responsibility follow [verification tracking table](../plans/validation-matrix.md), narrowed work/exit criteria [implementation plan](../plans/implementation-plan.md), cost judgment [storage-priority plan](../plans/storage-priority-and-editor-modes.md). Exclusions not converted to Passed/all NotRun not read as development backlog. Per-stage completion plans/repeated CHANGE LOG not maintained.
