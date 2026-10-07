<a id="불변-후보고정-검증-view-검증"></a>
# Immutable-candidate/fixed validation-view verification

[한국어](../ko/verification/immutable-validation-view.md)

On 2026-09-27, `vc-20260926` VC-01–04 synthetic/small Unity A/B/A execution is **scoped-complete-with-limitations**. Immutable-candidate publication, code/asset reversion using same Library, independent results/owned-resource reclamation checked. Includes first-import failure/forced shutdown/resumption after fix, blank first-A preview, mount-response rechecks for long cache replay, unmeasured initial Git allocation. Overall SF-03 Partial; product-mode adoption/large Unity conformance incomplete. Current format/commands follow [candidate contract](../source-view.md).

Raw evidence in `artifacts/storage-foundation/vc-20260926/`: `closeout.json`, `cost-summary.json`, `results-verification.json`, `pre-reclamation-audit.json`, individual CLI responses/timings/Unity logs/validation responses/journal accounting/allocation observations. Some control evidence contains local IPC tokens; do not use as public outputs.

<a id="범위와-입력"></a>
## Scope and inputs

- Starting baseline: changes after `development` `4ffed3f`. Previous BQ work preserved in that commit.
- Existing isolated 16GiB NTFS VHD/small Unity `V:\sf-compaction-20260926\unity`, basis `58f81b5bc6ff8a940d8a90872bd3fed858a54cc1`, Unity 6000.3.22f1.
- No original Library. One new snapshot, editorless workers A/B, fixed `V:\vc26\view`/one common cache. No large PV/canonical Editor/Coordination.
- Existing Anvil model/texture fixture; no new asset purchases/full original copies. One snapshot `{7DEE6BAD-3B2F-4429-BC99-416548BB4C71}`, per-generation `source-a1`/`source-b`/`source-a2`, common `cache`.
- A: `Calculate(3)=23`, texture max 1024, A-only text/.meta. B: code 33, texture max 2048. Returning to A confirmed actual 23/1024/A file.
- After candidate A publication, worker reedited to code 24; independent Git result `ab36d65f7f3534985b18e822e1ff00e9dd2bb492` retained. Candidate seal unchanged; Unity A still loaded 23.
- Owned temporary file created in original/absence in view checked/only that file deleted. No blocking original writes during work. Final original HEAD/clean/code-.meta hashes/no Library reconfirmed.

<a id="관문별-판정"></a>
## Per-gate assessment

| Gate | Assessment/evidence |
| --- | --- |
| VC-01 | Passed, defined synthetic/small fixture: candidate export with 0 original-payload reads, worker follow-up editing/candidate immutability/read-only seal, concurrent-writer/corrupt/incomplete publication rejection |
| VC-02 | Passed, defined synthetic/fixture: source/cache separation, single writer, per-generation source preservation, rejection of source reuse for another candidate, recovery at 5 private-extent-move interruption points |
| VC-03 | Passed, defined loaded-code/asset A/B/A/preservation/reclamation. First A import succeeded after correction/operational intervention. First-A blank preview remains unresolved, not all-A/B/A render pass |
| VC-04 | Measurement scope complete with limited storage/timing accounting: actual figures/unknown below retained. Normal cold-import time, new Git allocation immediately before creation, unobserved peak/equal-input comparison savings unconfirmed |

Final `pwsh ./scripts/build.ps1`: **282 passed, 0 failed, 1 test excluded at that time skipped**, 0 build warnings/errors, `build-transfer-retry.log`/`tests-282-passed.trx`. Synthetic tests check add/delete/rename/truncate/.meta, reediting after candidates, cache resumption, independent source outputs, moving only live 4KiB in 1GiB sparse file, same-generation recovery at 5 interruption points/wrong-generation blocking, replacement/roundtrip of open file IDs. Includes candidate/selection schemas, changed-source Git attach, retained index.lock. Synthetic recovery does not replace actual reboot/power/space failures.

<a id="unity-결과와-운영-개입"></a>
## Unity results and operational intervention

| Observation | A1, resumed after fix | B | Return to A2 |
| --- | --- | --- | --- |
| Actual code value | 23 | 33 | 23 |
| texture width/height/max | 1024/1024/1024 | 2048/2048/2048 | 1024/1024/1024 |
| Assembly MVID | `fa902747-0138-48ef-b4e0-274669b3b57a` | `136fff76-8dfd-430f-aa3d-35d10b735e45` | Same as A1 |
| A-only file | Present | Absent | Present |
| Explicit refresh | Success, 0 errors | Success, 0 errors | Success, 0 errors |
| Model mesh/vertices/triangles | 3/3001/2441 | 3/3001/2441 | 3/3001/2441 |
| Missing scene mesh references | 0 | 0 | 0 |
| Saved render preview | Blank sky/ground, cause unconfirmed | Model visible | Model visible |

All three responses retain texture GUID `60e4466064901d24bbb38040780c6bed`. Refresh acceptance/completion distinguished; each `requestId`/fingerprint/success/0 errors matched. A1/A2 fingerprints also identical. A1 inspect ran after acceptance/before completion confirmation; A2 after confirmation. This difference alone does not establish blank-preview cause. First A image not reclassified as pass; original PNG retained.

First A import hit `ViewPartitionBoundary` when moving new temporary .meta from Temp to source. Implementation then rejected all boundary moves. Normal `unity-stop` timed out after 35.957 seconds/window closing incomplete; only owned Editor forcibly terminated after exact PID/start/project/candidate-seal confirmation. Existing candidate/source/cache/snapshot retained. Durable intent transmitting only live extents of private files without original ranges/same-generation recovery implemented, automated tests passed/resumed same Library. After recovery A1/B/A2 observed I/O failures 0; private moves 5/792B, 4/236B, 4/236B respectively. Initial failure logs/forced-shutdown records remain.

B/A2 mount CLI returned `OverlayStartUncertain`. Cache replays 29.249/21.107 seconds respectively; same provider later mounted confirmed. No duplicate providers/recapture. Initial mount return cannot be readiness without recheck. Pre-reclamation measurement-script Git-command-name conflict failed before data reclamation/corrected/reran. Native-observation process-permission retries do not count as product-performance improvement.

<a id="시간"></a>
## Time

| Stage | Actual observation |
| --- | ---: |
| Worker A mount on prepared snapshot | 1.6734 seconds |
| Worker B mount | 1.6403 seconds |
| Existing worker A resumption | 1.5973 seconds |
| A candidate publication | 1.3049 seconds |
| B candidate publication | 1.2941 seconds |
| VSS helper internal capture | 0.355 seconds, initial payload copying 0B |
| First A launch → first ready, including failure/fix/intervention | 2,323.351 seconds |
| A recovery launch → ready | 544.985 seconds |
| B / A2 launch → ready | 84.120 / 75.008 seconds |
| A1 recovery / B / A2 cache replay | 7.428 / 29.249 / 21.107 seconds |
| A1 / B / A2 refresh-call start → completion | 32.203 / 25.464 / 25.001 seconds |
| A1 / B / A2 normal Editor stop | 30.863 / 31.642 / 33.065 seconds |
| A1 / B / A2 independent-result preservation | 2.389 / 2.236 / 2.316 seconds |
| A1 / B / A2 unmount | 1.572 / 1.486 / 1.423 seconds |
| A1 / B / A2 Git-registration reclamation | 1.952 / 1.776 / 1.800 seconds |
| A1 stop start → B ready | 180.643 seconds |
| B stop start → A2 ready | 400.352 seconds |

A/B candidate journals 8,010B/2,863B. Internal journal preparation/verification 0.0613/0.0589 seconds; totals including receipt publication/CLI startup use table. Separate snapshot capture/Git-registration times in receipts. Small experimental-worker values do not represent product `workspace ensure`/large-original full verification.

Last two values include operational waiting between measurements/tool calls. Not automatic-switch latency/pure directional-performance difference. `cost-summary.json` includes start times/individual calls for separating costs. Ready is owned heartbeat polled each second, separate from validation success. Initial A not clean cold-import sample. Small mounts/internal candidate exports on prepared snapshots do not represent full product `workspace ensure` preparation. Initial-source verification/permission preparation/full CLI operational delay separate.

<a id="저장량"></a>
## Storage

Same-time file accounting after normal A2 shutdown/result preservation/unmount/Git-registration reclamation. Allocation read with `FILE_STANDARD_INFO.AllocationSize`; per-path payload shares not converted to physical space.

| Storage location | Logical file length B | File allocation B |
| --- | ---: | ---: |
| Worker A | 12,259 | 12,848 |
| Worker B | 3,417 | 4,656 |
| Two candidates, including receipts | 13,955 | 21,600 |
| Validation source A1 | 9,461,409 | 9,462,392 |
| Validation source B | 9,425,736 | 9,429,624 |
| Validation source A2 | 9,425,782 | 9,429,624 |
| Common cache | 2,309,202,812 | 2,309,206,568 |
| Control records | 5,696,274 | 5,801,392 |
| Snapshot receipts/helper | 26,802 | 37,328 |
| Shared Git, including retained results | 6,056,835 | 6,112,144 |
| Original Library | 0 | 0 |

A/B candidate journals themselves 8,010B/2,863B. Common-cache journal 2,309,202,263B: live payload 1,829,089,908B, superseded 312,771,103B, metadata/frames 167,341,252B. Current live paths: Library 1,829,015,829B/other cache 74,079B. Each source-journal live payload 8,438,867/8,437,864/8,437,910B, mostly Unity-generated `.csproj`. Logical-range accounting, not subdivisions of file allocation.

**Highest observed owned worker/candidate/source/cache/control/snapshot file allocation 2,345,400,736B**. Same-time measurement-evidence allocation separate 4,826,392B. Of 365 10-second/stage observations, owned roots complete in 364; 1 overlapping reclamation incomplete. First sample after worker/candidate creation/during import, so pre-creation Git baseline unknown. Final entire Git allocation is not pure new execution cost. Raw evidence/closed control-receipts immediately after last reclamation allocation 11,146,120B; later documentation/summaries excluded.

Isolated-volume VSS first/last data-stage observations: used 25,051,136/27,131,904B, allocated both 100,663,296B. After snapshot reclamation 0/0. Shared-volume figures not per-workspace attributed/double-counted with allocation. File allocation excludes MFT/directories/kernel cache, may miss peaks between samples. Preserved VHD host-allocation reduction unclaimed. No independent-Library equal-input comparison here; saving rates/speed multipliers not calculated.

<a id="독립-결과보존회수"></a>
## Independent results, preservation, and reclamation

| Result branch | Revision | Content |
| --- | --- | --- |
| `codex/vc26/worker-a` | `ab36d65f7f3534985b18e822e1ff00e9dd2bb492` | Follow-up code 24 after candidate publication |
| `codex/vc26/view-a1` | `48ec1758a02e2a791329b83e86e50e6b9f99c2f0` | A code 23/generated source |
| `codex/vc26/view-b` | `9b75f64c7ae54f5981dfd2f2145f74b9472316ce` | B code 33/no A-only file |
| `codex/vc26/view-a2` | `51db3be32bf32d1c129e736e56d1bcfe5ced22f5` | Return A code 23/separate generated source |

Common experimental-result ref updates on next collection, so each branch/revision retained. Before/after snapshot deletion checked `git fsck --full --no-reflogs`, branch identity/code/.meta/A-file content/original HEAD-clean-hash. Results remain in existing isolated VHD Git/VHD detached. No original application/Unity-generated-source merge into worker.

After owned application-group process/working set/private bytes 0, provider exit/open handles 0/no mount/Git-registration removal confirmed, reclaimed only exact trial subpaths of `V:\vc26`. Provider PID reused by separate MCP Python process distinguished by start time; that process preserved. Only owned snapshot finally released; existing C-volume snapshot `{78AD7813-DF73-4845-8513-F14CC6E87331}` retained. Restricted administrator session/measurement ended/V: absent confirmed. Unknown not replaced with 0.

<a id="판단과-다음-경계"></a>
## Assessment and next boundary

Minimal route worked: no Library copies in workers/immutable candidates, one Library reused A/B/A. However 2.31GB journal replay 21–29 seconds, Editor readiness 75–84 seconds, shutdown 31–33 seconds dominate repeated work. Normal cold import/first A render still need verification. Therefore not justification for default-mode switch/large PV resumption.

Next bounded candidate: **normal first-import/first-render reproduction and repeated reconnect measurement using same cache**. Match long-replay preparation responses to actual mounted times; distinguish journal reading/validation/index reconstruction/flush/Editor readiness before choosing improvements. Follow-up not run yet. Full original copying/read hydration/flush-policy changes/online compaction/general lease interruption-recovery/source-return merge/queue-Coordination not included.
