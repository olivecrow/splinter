<a id="선택적-기준본-복귀-정리-검증"></a>
# Optional basis-reversion compaction verification

[한국어](../ko/verification/journal-base-reversion.md)

Record dated 2026-10-06, Windows/NTFS, implementation `25f4fa1` after `development @ d97bd3c`. Follows [fixed scope/assessment criteria](../plans/journal-base-reversion.md)/[current contract](../journal-base-reversion.md). **Default is OFF**. ON reduced post-compaction storage but increased total measured time/managed allocation for actual inputs on this candidate. Distinct from later [multi-file timing optimization](../plans/journal-base-reversion-optimization.md) binaries/measurements. No automatic-compaction/append-cost/Unity-readiness improvement claims.

Evidence root `artifacts/journal-base-reversion-20261006/`. Ignored local evidence retained; passes from other candidates not inherited.

<a id="입력과-비교-방법"></a>
## Inputs and comparison method

- Actual original: sibling `splinter-unity`, clean `development @ b09fa58e9d40a560d4fe1af7a213f8ac23e8e3b1`. Tracked 1,602 / 2,804,769,928B, Assets 1,567 / 2,804,669,568B, existing Library 42,420 / 3,141,660,252B (`input.json`). Selected 4KiB/1MiB/1MiB ranges in three actual Medieval PNGs (1,155,235 / 1,177,669 / 1,226,681B). Half last range retained different bytes.
- Fixed raw Git-blob reader/current SJR2/same codec-limits; each 32 B→C→base cycles. Synthetic seed 61006 random/repeated bytes/distributed 8 ranges. Independent OFF/ON directories; 3 rounds OFF/ON→ON/OFF→OFF/ON. Full bytes/entries/change sequences/compaction replay checked.
- Actual repeated comparison uses owned-product recorded Git-basis reader/**actual NTFS journal files** for offline compaction/replay. Fresh readers each compaction/replay, no seed-stage blob-proof reuse. Like product, snapshot health checked once/observation reused. Repeated WinFsp callback/restart performance/Unity cold import NotRun. Separate product A/B checks actual WinFsp lifecycle preservation/coexistence/reclamation.
- OS cache not emptied. Initial/subsequent cache differences/other evidence collection effects remain. Table time is median sum of write+compaction+fresh-reader replay per independent run, excluding source preparation/full CLI connection-checkpoint-release/Unity readiness. Faster replay alone not overall improvement.
- Initial actual trial rechecking live health on every read excluded as methodological error. Only exact owned verification child ended; original directory/`excluded-native-trial.json` retained. Final figures use only `native-offline-qualified`. Synthetic pre-64KiB-improvement `benchmark` retained; final numbers `benchmark-final`.

<a id="offon-결과"></a>
## OFF/ON results

Final NTFS journal allocation identical across three runs each. Times ms, write+compaction+replay total. Savings are post-compaction allocation of **selected journals**, not full 2.8GB project/VSS savings (`comparison.json`).

| Input | OFF allocation B | ON allocation B | Allocation decrease | OFF total ms | ON total ms | Total change |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| random 4KiB reversion | 8,192 | 4,096 | 50.00% | 246.56 | 233.47 | -5.31% |
| random 1MiB reversion | 1,052,672 | 4,096 | 99.61% | 931.30 | 906.09 | -2.71% |
| repeated 1MiB reversion | 4,096 | 4,096 | 0% | 445.42 | 441.65 | -0.85% |
| mixed distributed 8×4KiB | 131,072 | 110,592 | 15.63% | 1,747.15 | 1,749.51 | +0.14% |
| Three actual PNGs | 2,109,440 | 532,480 | 74.76% | 4,712.48 | 7,040.90 | **+49.41%** |

Actual-input OFF totals 4,651.46–4,820.60ms, ON 7,036.59–7,250.65ms. Compaction median 2,266.65→4,647.39ms, replay 1,146.07→1,185.62ms, writing 1,322.89→1,310.05ms. Actual cost increase exceeds repeat variation. Basis comparison needs fresh fixed-blob reading/validation; pure payload savings do not offset it.

Actual compaction cumulative managed allocation median 11,342,824→20,441,432B, not resident RAM/instantaneous total peak. Pre-compaction journal allocation both 202,403,840B. Old+new allocation samples OFF 204,513,280B / ON 202,936,320B: most pre-compaction accumulation remains; ON does not remove pre-compaction peak.

Final payload/frame breakdown (`native-off-frame-ledger.json`, `native-on-frame-ledger.json`):

| Item | OFF B | ON B |
| --- | ---: | ---: |
| Final logical journal | 2,106,633 | 529,092 |
| Live private / stored payload | 2,101,248 | 524,288 |
| Metadata/frame/checksum | 5,385 | 4,804 |
| Obsolete stored payload | 0 | 0 |
| Comparison bytes returned by basis reader | 0 | 2,101,248 |
| Logical private bytes removed | 0 | 1,576,960 |

Repeated 1MiB bytes already compress to 238B payload OFF, so no NTFS-allocation improvement. Required repeated-input improvement/no actual total-time regression above 10% unmet; ON not default. Use explicit option when storage needed. Returned-basis bytes are not total Git decoding/OS disk I/O.

<a id="보존과-중단-복구"></a>
## Preservation and interruption recovery

| Item | Qualification | Evidence |
| --- | --- | --- |
| Original/expected hash, same bytes/entry-ID-rename-time-attributes, truncate/extend/delete-recreate/missing basis | Passed, automated/fake only | `JournalBaseReversionTests`, coordinator/schema/fake tests |
| Actual child forced termination: append-flushed, intent, candidate-flushed, prepared, published, before-previous-delete, previous-deleted | 7/7 Passed, synthetic immutable basis/actual NTFS | `process/result.json` |
| Same 7 boundaries, actual large recorded Git basis | 7/7 Passed, offline compaction/actual NTFS | `native-process/result.json` |
| Same options/identity, external ACK/seal-bytes-metadata, previous-generation preservation/reclamation | Passed, fixed boundaries above | Per-stage PID/start/exit/ACK/receipt |
| Actual WinFsp product A/B, independent results/owned reclamation | Passed including initial A failure/explicit resumption history, editorless only | `product/result.json`, history below |

Each forced termination verified exact own-child nonce/PID/start. Existing contract retaining completely flushed pre-ACK writes during replay confirmed; no claim all pre-ACK writes discarded. Development observer not attached to ordinary provider. Not inherited as live WinFsp-provider kill/power-OS failures/full NR matrix.

<a id="제품-ab-이력"></a>
## Product A/B history

Owned home `.local/jr1006/home`; A `461062084c0a7b6c1b482b58` / key `jr1006-native-a`; B `3566d48668b4feecdfc239b9` / key `jr1006-native-b`. `--source git --application none --storage git-snapshot`, same fixed base. No original Library transition/Unity Editor/import. Raw-byte requirements prompted local `core.autocrlf=input` only during both ensures; try/finally restored original local unset. Global settings unchanged. Initial rejection retained. Preparation returned base reads ~8.65/8.76GB are not full-copy/disk-hydration quantities.

Initial A ON command verified/published new journal, then runner temporary-directory `Directory.Move` access denial caused `InputUnavailable` at `compact-connect`. New/previous journals/8 runner files preserved; hashes matched build at failure investigation. No ACL/attribute/service/installation changes/manual temp-directory-move bypass. Automatic approval review rejected first retry; user informed/explicitly approved **retry/completion of same owned task**. Official `workspace compact` omitting option then succeeded with saved ON/same ID-home (`a-reconnect-approved.json`). Initial failure not changed to pass/access-denial cause not declared resolved. First helper in-process call differs from official standalone CLI resumption; later development helper changed to standalone CLI. No new native execution claimed for helper fix.

B 64×128KiB new files all durable-flushed/read-hash checked, 64 retained markers matched. Concurrent completion during A command qualifies overlap with whole compact stop/preparation, not separately payload-comparison phase. B OFF compaction includes extra 8MiB, so this one product A/B final-allocation difference is not matched performance comparison.

Final `product/result.json`: 63 markers completed during first A compact command/all 64 preserved. UTC/monotonic anchors/command start-end retained. After A resumption/B OFF compaction full bytes/timestamps of three assets matched; same B ensure returned identical ID. After A checkpoint/release, B maintained mount/provider ownership/handles 0 and reread assets/64 files. B checkpoint/release also succeeded; **both phases released**. Independent Git revisions of both results reverified asset bytes/B's 64 markers. Results A `80ae31c2bede6d7b4e308d3fd765583967c28ef6`, B `3f11fc4dde1d1e4af21c32f3f67689b16f19ad04`, retained in `refs/splinter/snapshot-results/<id>`/owned result branches. No original integration.

`original-and-ownership-after.json`: original development/HEAD/clean/actual PNG bytes retained, local autocrlf unset restored, existing **88 non-owned refs**/original worktree registration retained. Both owned Git-basis refs/mounts/deltas/providers/handles/processes-memory 0/absent; returned snapshot reclamation released. Original Unity Editors 0 before/after. Original files/other tasks/installation-service-ACL unchanged.

Actual full checkpoint command times A 272.49s / B 272.92s, release A 91.41s / B 89.58s are single observations. Extra files/action order differ, so not causal OFF/ON comparison. These source-collection/owned-reclamation costs are outside offline write+compaction+replay figures above.

Actual post-reclamation samples `storage-before-after.json`: owned A records 391,720B/B 395,816B, delta generations absent. Shared runner allocation 1,544,536→4,633,608B; first-failure temporary bundle retained. Shared Git(common/results) allocation 2,392,353,848→2,393,277,504B. Retained record/runner/independent-result costs not omitted from final-journal savings to claim total-space improvement. Shared-root totals not divided A/B/double-counted with original baseline. System samples separate `system-before-peer.json`/`system-after-peer.json`; shared OS variation is not option RAM effect.

<a id="한계와-확인"></a>
## Limits and checks

Total physical instantaneous peak/per-workspace VSS attribution/total RAM instantaneous peak/native-codec heap unknown. Git-basis route does not use VSS. Few provider/system observations are resident/shared values, not peak/workspace attribution/causal ON-OFF effects. Actual Unity compile/Play/Test/build/cold import, large original-Editor coexistence, other OS/ARM/new VSS failures NotRun. Checkpoint preserves stable source independently, not game-verification pass.

Required `pwsh ./scripts/build.ps1`: locked restore/0 build errors-warnings, total 1,082 / Passed 1,073 / Failed 0 / Skipped 9 (`build-final.log`, `full-final.trx`). Later development helper/old-intent rejection/response-schema tests added-corrected, test-project rebuild 0 errors-warnings/final selected tests 11/11 Passed (`test-final-build.log`, `final-selected.log`). After native closeout, solution build including final CLI-usage metadata 0 errors-warnings (`build-metadata-final.log`), current-schema validation of both release responses/CLI-option discovery 2/2 Passed (`final-responses.log`). Native-write environment variables removed; only saved responses read, not native A/B rerun. Explicit comparison/forced-termination figures are separate evidence. Earlier build failure while owned CLI used DLLs retained; only success after exit accepted as final.

Fixed unit closes **scoped-complete-with-limitations**, retaining default OFF/initial failure-official resumption history/NotRun-unknown boundaries. No automatic actual A/B/Unity import/full failure-matrix repetition without new real-use storage shortfall/preservation defect/explicit input-support expansion request.

After final solution build, all 8 runner files/hashes matched native-investigation values (`qualified-final-binary-comparison.json`). Runtime/Core/Windows/Git reader passes not inherited from old binaries. UTF-8 of 29 changed files, parse of 65 contract JSONs, relative links in 3 new owned documents, `git diff --check` checked. Implementation repository changes uncommitted; no push/merge/deploy.
