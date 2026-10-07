<a id="한정-m0-납품-판정"></a>
# Bounded M0 delivery judgment

[한국어](../ko/verification/m0-delivery.md)

**The development CLI/source-skill version for the registered small Windows x64 NTFS/Git/Unity environment is judged deliverable as scoped-complete-with-limitations.** Start at the [AI start guide](../agent-start.md); execution status is in the [M0 tracking table](../plans/validation-matrix.md). General installation, automatic binding, large-project efficiency, ARM A/B, and full OS-failure support are excluded.

Candidate is `development @ 95b48b4`; `plan.json` fixes the full Git SHA. This unit changed only start guidance and evidence/scope documents; product code, schemas, dependencies, and installed service were unchanged. Execution code also remains the same after [representative use](representative-agent-workflow.md) `4c7bd6c`. All historical-binary native tests are not presented as passes for a new candidate.

Raw evidence is at `artifacts/m0-delivery/delivery-20261001-205002-403/`. Preserves `plan.json`, `recovery-source-audit.json`, `current-recovery-tests-audited.json`, `automated-audited.json`, `automated.trx`, `build.log`, read-only CLI responses/monitors, and document checks. Source-hash comparison determines applicability boundaries of evidence; it does not replace execution/durability tests.

<a id="현재-후보의-m0-06-근거"></a>
## M0-06 evidence for the current candidate

Required `pwsh ./scripts/build.ps1` locked restore/Release had 0 warnings/errors, **745 total / 744 passed / 0 failed / 1 test excluded at the time skipped**. TRX confirmed 170 successes under the fixed conditions below. No new tests/harnesses were added; synthetic/actual-file/child-termination evidence below is not full native VSS failure qualification.

| Current automatic regression | Selected successes | Verified boundary |
| --- | --- | --- |
| `PreparationInterruptionTests.KilledClient…` | 19 | Actual child CLI termination, durable fake preparation receipts, same-request resumption, duplicate identity/side-effect prevention |
| `JournalInterruptionTests`/`JournalReplayTests` | 69 | Actual child termination with synthetic immutable trees, acknowledged prefix/incomplete tail, replay/corruption rejection. Not VSS/WinFsp callback/power tests |
| `RecoveryContractTests`/`StateWriteDiagnosticTests` | 27 | Preservation/unknown for corrupt/locked records and path mismatches, actual Windows replace/append errors and publication state, resumption of the same owned diagnostic Git CLI |
| Snapshot coordinator response-loss/unknown/seal regression | 10 | Same-identity reconciliation after lost capture/release, unknown preservation, rejection of writes before/after result collection. Backend is fake |
| Storage authority response/intent regression | 13 | Same-request reuse by a new processor, pending publication, partial/forged request rejection, deferred reclaim. Native operation is fake |
| Discard/Git/delta/separate authority gate regression | 32 | Same-intent resumption after lost stop/unmount/source/delta/snapshot responses, rejection of unknown/changed requests, durable publication of resource absence, interrupted actual temporary Git/file deletion resumption, helper authorization checks |

Additionally, [current representative A/B](representative-agent-workflow.md) verified stable recollection/independent result/release of **the same A** after initial collection rejection due to a late Library write, and discard of uncommitted compile-failure B. [Discard after service application](workspace-discard.md) verifies same-intent resumption after initial authorization rejection, repeated completed requests, peer preservation, and actual VSS reclaim. No new false completion, lost acknowledged writes, or original/peer damage was observed on supported paths. Unresolved state-publication errors remain limitations in their records.

M0-06 closes with **current-contract preservation, same-request retry, corruption/unknown rejection, and the bounded execution conditions above**. All native forced termination, VSS internal calls/OS flush, reboot/power loss/physical storage exhaustion for current binaries are neither added as completion criteria nor guaranteed. Full NR InProgress/unexecuted boundaries remain.

<a id="과거-native-근거의-재사용-경계"></a>
## Reuse boundaries for historical native evidence

Recorded raw source hashes were compared with current files. Numbers are total/matching/changed/removed files in each manifest; they do not imply equivalence of new files or the whole environment. The 4 removed files belong to excluded DAG/plan APIs, not lost execution evidence.

| Existing evidence | Hash comparison | Current judgment |
| --- | --- | --- |
| [26 preparation CLI cases](preparation-recovery.md), scoped 22 in `nr02-20260930d/code-and-environment.json` | 22 / 14 / 8 / 0 | Mount/client/server/backend/existing preparation test files match. Coordination/diagnostic/command contracts changed, so not inherited as current 26 native passes |
| [58 common journal cases](ntfs-recovery.md#nr-03-actual-process-termination-at-common-journal-boundaries) | 229 / 198 / 27 / 4 | `JournalDeltaStore`, `JournalReplayReader`, and atomic-publication implementation match. Reuses only that synthetic-journal evidence; historical WinFsp differs from current code, so no current kernel expansion |
| [19 local kernel cases](ntfs-recovery.md#nr-03-local-winfsp-callback-mmap-and-new-provider-connection) | 238 / 208 / 26 / 4 | Journal/actual WinFsp filesystem/client/server/mount implementations match. Not a full product native-interruption guarantee including changed coordinator/authority |
| [13 product client cases](ntfs-recovery.md#nr-03-client-boundaries-for-product-vss-and-cli) | 244 / 214 / 26 / 4 | Native read/journal/mount implementations match. Discard/authority/coordination/schema changes and unresolved publication failure prevent inheritance as current 13 end-to-end passes |
| [State diagnostics](state-write-diagnostics.md) | 247 / 218 / 25 / 4 | Atomic publication/diagnostic tests match. `WorkspaceStore` changes are discard-intent read checks; `9ec88e6..HEAD` comparison shows existing publication/diagnostic policy unchanged. Current full regression verifies actual file/CLI conditions |
| [Post-fix native discard](workspace-discard.md), scoped 24 in `source-after-observation-fix.json` | 24 / 24 / 0 / 0 | Recorded discard code/contracts unchanged. Retained as same-intent resumption/duplicates/normal reclaim evidence, not actual forced termination during discard qualification |

Current changes are document-only; changed discard boundaries were verified by existing actual service application/resumption, representative Unity failed-task discard, and current automatic regression, so the full native matrix was not repeated. Identical source alone does not inherit provider/volume/boot/environment qualification as a current full pass.

<a id="시작-안내와-납품의-실제-경계"></a>
## Actual start-guide and delivery boundaries

Read-only checks of `capabilities`, `schema doctor`, service status, and doctor used existing registration `T:/ua-source`. Service is running; caller is not elevated. doctor reads Git/Unity `6000.3.22f1` metadata but retains `readyForExecution=false` and execution `checksNotRun`. This is not described as initial preparation/Editor/grant certification.

First doctor was `GitInspectionFailed` because the installer-owned fixture's process-scoped Git trust was absent in the new evidence environment. Original failure was preserved; only exact `T:/ua-source` was configured in this child's `XDG_CONFIG_HOME`, then queried again. Original/global Git settings, ownership, and service grants were unchanged. 5 CLI observations ran without Windows administrator privileges; additional consent-process samples were 0. No new product NTFS workspace/Unity Editor on the registered fixture, original MCP, or VIVOBOOK was used. Owned temporary Git/fake tasks in automatic regression above are distinguished from this observation.

Deliverables are the existing development checkout build/CLI, [source skill](../../skills/splinter/SKILL.md), and [start guide](../agent-start.md). General-distribution installer, global/project skill installation, and automatic conversation binding are not yet provided. No new machine service/driver registration, public release/upload, tag/release was created.

<a id="유지할-제한과-후속-판단"></a>
## Retained limitations and follow-up decisions

Late Library writes during first result collection, Editor focus retries, unresolved causes of bridge/fixture observation-file publication errors, and minute-scale preparation/owned-memory costs are owned by [representative use](representative-agent-workflow.md). Shared VSS attribution/total physical peak are unknown. General file/asset combinations, large capacity, ARM A/B, reboot/power/physical storage failures remain unfinished NR/SF support expansion.

The bounded version closes M0; full remaining NR/SF/SW is not reintroduced as the development-progress denominator. Follow-up selects only the changed boundary once an actual project/input or installation need is fixed. Support checks for new inputs, general installation/automatic binding, default Editor modes, and large optimization are not automatically included in this judgment.
