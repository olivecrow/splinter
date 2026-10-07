<a id="공통-저장소-1차-구현검증"></a>
# First common-storage implementation and verification

[한국어](../ko/verification/storage-foundation.md)

Executed 2026-09-25, Windows x64/NTFS/Unity 6000.3.22f1. **Overall execution-unit judgment is `FailedPreservation`.** Per-file change verification/storage measurement implemented, bounded tests below passed, but preservation condition for 5 pre-test VSS snapshots unmet. Independent results/original source preserved; owned test resources reclaimed. See [final receipt](../../artifacts/storage-foundation/sf-20260925/final-closeout.json).

2026-09-26 [subsequent cause-reproduction/protection tests](storage-recovery.md) fixed control-JSON publication contention and reproduced cascading loss on latest/middle deletion even in isolated VSS. Non-forced direct API produced same result; actual product guard risky-request blocking/oldest-first reclaim passed defined conditions. Follow-up does not convert historical failure below into success.

| Scope | Judgment | Evidence and remaining conditions |
| --- | --- | --- |
| SF-01 Per-file source verification | Passed: defined source-verification scope | Actual repeated reads/independent oracle/same-size/mtime detection, automatic corruption/race/policy changes, Unity loaded-code linkage |
| SF-02 Measurement/storage invariants | Partial | Initial copy/read hydration 0, range/repeated writes, journal residue/flush/replay/seal measurement. Compression/flush-policy changes/compression-interruption recovery NotRun |
| Real-environment preservation/reclaim | FailedPreservation | 5 owned workspaces reclaimed/results preserved. 5 existing non-owned VSS snapshots lost during B release |
| New VSS-release blocking | PassedAutomated / PassedDefinedLive | 9 fake CIM tests/2026-09-26 isolated tests. 2 risky owned-sentinel requests blocked/12 sequential reclaims. General non-owned-backup coexistence unverified |
| Full SF-03–06/NR/SW | NotRun or existing partial states retained | No single Editor/large PV remeasurement/installation/binding/discard/reboot/power/physical storage exhaustion/ARM A/B |

<a id="기존-vss-보존-실패와-현재-제한"></a>
## Existing VSS preservation failure and current limitations

With user approval increased C: VSS limit 39,985,348,608B→68,719,476,736B(64GiB). [Immediately post-setting receipt](../../artifacts/storage-foundation/sf-20260925/vss-budget/vss-budget.json) verifies preservation of 5 existing IDs. Limit not reduced; still 64GiB.

At B release 23:24:56 KST, Windows recorded `volsnap 95` 5 times; [final VSS query](../../artifacts/storage-foundation/sf-20260925/final-vss.json) found neither existing 5 nor owned test snapshots. [Windows event](../../artifacts/storage-foundation/sf-20260925/volsnap-events.json) messages state oldest copies were deleted to delete later-created copies marked for deletion. Lost IDs:

- `{E82CEB2E-AC2B-4FDF-ABE9-E6E4041FB724}`
- `{8D150A60-DBB3-4A18-A509-DF3E7648C50C}`
- `{1B6D14D7-E5FA-4951-B3B6-9E62F96F194D}`
- `{08B97BC9-DA94-483B-8536-CCC700DA0613}`
- `{EF908FCF-69D0-4BAD-837C-6B7792255316}`

Helper `Remove-CimInstance` input then exact B ID `{E01D2739-E626-4B9E-82EA-951EDB93B04A}`. No global deletion/limit-reduction calls, but exact-ID-only deletion cannot guarantee provider preservation of other snapshots. Event cascade/timing confirmed; full internal-provider cause unresolved. No evidence deleted snapshots restored. Microsoft also states [deleted shadow copies cannot be recovered](https://learn.microsoft.com/en-us/windows-server/administration/windows-commands/vssadmin-delete-shadows). This loss/VSS allocation reduction not counted as storage-saving achievement.

Current helper blocks before deletion with `SnapshotReleaseIsolationRequired` if same volume has other older/same-time/unknown-time snapshots. Unknown volume also blocked; `release-isolation.json` records target/observed IDs/blocking IDs/deletion attempted. If other IDs disappear after permitted exact deletion, stops with `SnapshotCollateralLoss`. Existing snapshots not deleted to pass blocker. Unlike oldest-created A→B reclaim in small A/B, reclaim on volumes with older non-owned snapshots may remain preserved.

Enhancement verifies 0 deletion calls when blocked/exact-ID-only deletion/preservation on observation failure/collateral-loss detection in fake CIM. Subsequent [isolated-volume tests](storage-recovery.md) also verify risky-request blocking/actual oldest-first reclaim. Not verification against all OS automatic deletion/concurrent creation races. Next contract separates late task result preservation from snapshot reclaim time; does not lift blocking or claim arbitrary-order immediate reclaim.

<a id="후보와-자동-시험"></a>
## Candidate and automatic tests

Baseline `development` commit `30ce3075bb377e968931a6bc53dde77c4065673f`, executed uncommitted candidate. Unrelated existing plan changes retained; no commit/push.

- Pre-change baseline: [baseline TRX](../../artifacts/storage-foundation/sf-20260925/baseline.trx), 207 total/206 passed/0 failed/1 test excluded at the time skipped.
- Actual source-cost/final Unity collection candidate: [live manifest](../../artifacts/storage-foundation/sf-20260925/live-candidate-manifest.json)/[live TRX](../../artifacts/storage-foundation/sf-20260925/live-candidate.trx), 218 total/217 passed/0 failed/1 skip. Prior range-storage/initial Unity candidate and resumption/fix process also preserved in raw records.
- VSS-blocking candidate at 2026-09-25 closure: [candidate manifest](../../artifacts/storage-foundation/sf-20260925/candidate-manifest.json)/[final build](../../artifacts/storage-foundation/sf-20260925/implementation-build-14.log)/[TRX](../../artifacts/storage-foundation/sf-20260925/candidate.trx), 227 total/226 passed/0 failed/1 skip. Locked restore/Release/automatic tests executed. No actual VSS run for added guard candidate then; later verification follows [separate record](storage-recovery.md).

Automatic tests include same-size/mtime dirty detection/actual committed content, unchanged large-file reuse while locked against opens, independent raw oracle, source set/attributes/EOL changes, filter rejection, `.meta` validation invalidation, path/provider-generation/corruption/unknown/races, exact audit-recovery commands, existing collection-preservation regression.

<a id="반복-소스-읽기와-초기-비용"></a>
## Repeated source reads and initial costs

Test source 5 files: random 16/256/1,024MiB files, 10B code, 30B attributes, total raw 1,358,954,536B. Separate fixture/Git object preparation not product initial payload duplication. After code changed retaining same size/mtime, explicit verification ran 3 times.

| Observation | Before | After |
| --- | --- | --- |
| 3 command elapsed times | 41.306 / 33.636 / 36.097s | 3.618 / 3.624 / 3.697s |
| Provider-path read increase | Each 1,359,348,024B | 1,252 / 1,134 / 1,134B |
| Raw payload rehash | Whole source each time | 10 / 0 / 0B |
| Canonical Git hash input | No separate count | 10 / 0 / 0B |
| Same-size/mtime code dirty decision | Incorrect clean | `hasChanges=true`, `changedPaths=[code.cs]` |

Raw evidence: [before](../../artifacts/storage-foundation/sf-20260925/baseline-source-cost.json), [after](../../artifacts/storage-foundation/sf-20260925/candidate-final-source-cost.json), [independent Python full-read oracle](../../artifacts/storage-foundation/sf-20260925/candidate-final-oracle.json). All raw fingerprints identical `179d39064f8a0b9a0a6b55d3e92e57c5adbbc259bf877333824269bdf1e4641d`. Subsequent 0B is source-payload rehash, not 0 attributes/control reads/Git metadata enumeration. Actual [release rejection of uncollected changes](../../artifacts/storage-foundation/sf-20260925/same-mtime-release-refusal.json) verified too.

Initial verification cost not reduced. Same ensure after capture helper: pre-change 91.581s; new final-source-candidate task 95.856s. First new-task manifest cost raw/Git hash inputs each 1,358,954,536B, 57.424s, manifest 1,470B. Even with second pre-response check 0B, full cost retained in [initial inspection record](../../artifacts/storage-foundation/sf-20260925/retained-records/final-cold/source-inspections.jsonl). Explicit audit/provider restart also require full reads. Slow intermediate-candidate audit not substituted for final-candidate initial cost.

3 samples each, not benchmark controlling background Unity activity/OS cache. Times are observations, not general speedup guarantees. Independent oracle uses different read sizes; its time not compared as product initial-read performance.

<a id="범위-저장누적-저널"></a>
## Range storage and cumulative journal

[Actual mount range tests](../../artifacts/storage-foundation/sf-20260925/range-exercise.json)/`allocation-range-*.json` distinguish logical payload/journal length/FILE_STANDARD_INFO allocation.

| Stage | Cumulative payload B | Journal logical B | Delta file allocation B |
| --- | ---: | ---: | ---: |
| Before writes / after repeated reads | 68 / 68 | 2,419 / 2,419 | 4,096 / 4,096 |
| 4KiB write in 16MiB | 4,164 | 6,937 | 8,192 |
| 4KiB write in 256MiB | 8,260 | 11,459 | 16,384 |
| 4KiB write in 1,024MiB | 12,356 | 15,985 | 16,384 |
| 100 additional writes to same 4KiB | 421,956 | 468,695 | 471,040 |
| Full 16MiB rewrite | 17,199,172 | 17,252,743 | 17,256,448 |
| After truncate / rename / delete | 17,199,172 | 17,253,157 / 17,253,580 / 17,253,991 | Each 17,256,448 |

Creation payload copy/read cache 0. 100 writes add payload 409,600B, 601.717ms, commit +100/flush +200(including caller flush). Deletion/truncate do not reclaim earlier journal storage. Existing durability/flush policies retained.

After operator stopped provider, reconnected same snapshot/delta/rechecked acknowledged ranges. Preserved [replay-byte verification](../../artifacts/storage-foundation/sf-20260925/range-replay-bytes.json)/original hash match. Controlled reconnect, not power interruption/reboot/actual disk exhaustion.

Whole-journal reads for seal/reclaim verification remain. Preserved `storage-reads.jsonl` totals: synthetic candidate 6 calls/204,305,838B, Unity A 7/1,041,877,059B, B 8/2,088,153,166B. Cumulative with retries; B epoch counters across provider restart not simply subtracted. [Full ledger](../../artifacts/storage-foundation/sf-20260925/final-closeout.json) links raw paths/times.

<a id="추가-할당-장부의-범위"></a>
## Additional-allocation ledger scope

Final-source-candidate new synthetic workspace vs immediately pre-creation: control/delta +68,288B, runner +631,128B, shared Git +1,032B. Same-stage volume VSS used +84,656,128B/allocated +423,968,768B includes other tasks/original writes, not attributed to this workspace. Whole-home runners from repeated old candidates not counted as new-workspace costs.

Original Unity Library 1,874,456,304B excluded baseline. After reclaim, control records 887,000B/runner 1,254,064B/shared Git +18,360B from baseline remain. Independent-result/record preservation cost, not delta/Editor residue. Library writes in delta, not readded.

`unity-both-edited` sample overlaps A checkpoint start, not used as same stopped-endpoint comparison. All samples sequential per-directory observations, not atomic instantaneous sums; MFT/kernel cache/between-sample maxima/logs/temp files outside measured roots not fully measured. `totalAttributableBytes`/total savings remain null. Volume usage reduction after existing VSS loss particularly not normal reclaim/savings evidence.

<a id="실제-unity-ab와-회수"></a>
## Actual Unity A/B and reclaim

Used separate `C:\sf25\u` prepared from existing small fixture `2e85e3fe1b3be036cb826749b59159c0ff2737da`. Source 90 files/6,235,231B including model/texture/scene; original basis HEAD `58f81b5bc6ff8a940d8a90872bd3fed858a54cc1`. No large Medieval pack/C/Q cohorts/Coordination.

| Task | Actual loaded-code value | Final independent result |
| --- | --- | --- |
| A `657cf6764c59f9cea7b132de` | 13 → 23 → 24 after new-CLI resume | `3dfb5fe0bb185038b1c45810d1d461ca8e47d2e2` |
| B `0d0eb2cd8851a4f98ead1b54` | 13 → 33 → 34 edited after A reclaim | `7fcbf2d1b91b432056d53655723d96d22e7b2498` |

Each value links exact PID/task path/domain generation/assembly MVID/current validation fingerprint. After A reclaim, B continued editing before checkpoint, then final collection. Actual independent-ref code rechecked A +21/B +31. [Dirty-original integration rejection](../../artifacts/storage-foundation/sf-20260925/dirty-original-guard.json) preserved HEAD/index/dirty contents/independent refs; restored only test dirty contents to original bytes.

First B release failed `OverlayFlushUncertain`. Provider `UnauthorizedAccessException`/completed flush-result temporary file preserved; exact location/cause unresolved. Reconnected via same-key ensure/original VSS-ID Observe/journal replay, verified same result/fingerprint, resumed reclaim. Replay 261,019,985B/source rebuild raw/Git each 6,235,231B. Operational recovery not unassisted success.

5 workspaces—baseline synthetic/changed synthetic/final initial-cost/Unity A/B—all released, independent refs readable. Product receipts verify owned processes/memory/handles 0, mount/delta/owned snapshots absent. [OS exit check](../../artifacts/storage-foundation/sf-20260925/process-closeout.json) confirms owned Editors/providers/measurement helpers exited. Both test originals clean, one original worktree registration each. Sibling canonical Unity repository unchanged. **Owned-resource reclaim success and existing non-owned VSS preservation failure are separate judgments.**

Current contract follows [change-based source verification](../source-verification.md); remaining scope [first common storage](../plans/storage-priority-and-editor-modes.md)/[NR support gates](../plans/ntfs-recovery-and-support.md).
