<a id="unity-lab-실제-저장공간메모리-비교"></a>
# Actual Unity L/A/B storage and memory comparison

[한국어](../ko/verification/unity-live-comparison.md)

Completed a small actual Unity comparison in [fixed UC-01–05 scope](../plans/unity-live-comparison.md) and an [HTML report](../../reports/unity-live-comparison-20261003.html). **Measurement, code/assets, isolation/resumption, and B continuity verified; final snapshot reclaim in UC-04 is Partial**. Not expanded to total physical savings/minimum RAM/large/OS support passes.

Partial above is the comparison-time judgment. Subsequently user explicitly authorized preservation moves of 73 closed old records/same A/B release retry. [Parallel-edit follow-up verification](unity-parallel-editor-validation.md) moved them preserving 2,130 file hashes/72 independent-result refs and confirmed prior two snapshots `missing`/`released`. No automatic migration/erasure of initial failure implied; comparison figures/C-worktree reclaim limits unchanged.

<a id="후보입력독립-작업"></a>
## Candidate, input, and independent tasks

Executed 2026-10-03 on Windows 11 Home x64/Ryzen 5 5600X/visible RAM 67,020,072 KiB. Product `development` candidate `52d9099b2df3bdd786defbabe8e0b04d3ea0cdf3`, Runtime SHA-256 `b769a1fe899c982416d62f84325021c282313bf7930bad9c1db96b831c9a13ea`. Product code/schema/service/registrations/volume unchanged.

Original existing registered `T:\ua-source`, basis `f71e4e244ba9b085aa0fc439336e4c60ee3d5d5d`, 94 tracked files/56 Assets files. Includes actual `Target.fbx` 105,120 B/`Target.png` 5,821,546 B/material/scene. Unity `6000.3.22f1`, URP `17.3.0`, installed Unity CLI `1.0.0-beta.5`. User-authorized Pipeline `0.8.0-exp.1` trial basis `23896dfacba2d90d0c49f826444b178fdbe82d2b`, changing only two Packages files.

| Method | A | B |
| --- | --- | --- |
| C: actual Codex app Git worktree chats | `01a10118-d986-7e60-93f4-2a59f0b2cb42`, `.codex/worktrees/1a8c/splinter-unity` | `01a10119-11d9-7960-b631-2c3f2eecc4a1`, `.codex/worktrees/90dd/splinter-unity` |
| S: product NTFS workspace/parallel AI authors | key `uc-s-a-20261003`, id `40cd86f88ef543d7a8418a6e`, digest `9696acb894812daf3a5e0d65` | key `uc-s-b-20261003`, id `fd4c3247d6b0d38e6c7b5fba`, digest `0c97742488cb4c6f2a6b7671` |

S home existing `.local/wl29`, each budget 17,179,869,184 B. C uses no Splinter for creation/edit/results/normal shutdown/archive. OS file/process measurements independent of Splinter assemblies/APIs too. C/S each run L original+A/B as three independent simultaneous Editors, cohorts sequential. Coordination/Q excluded. User-owned C chat/current-chat S subagent handling times not compared as product performance.

Preserved correction of initial C `baseline.txt` LF/CRLF 1 B difference to original bytes. Before 0.8 edits, all 94 C/S A/B bytes/hashes match original. Each exact-path Editor actually loaded mesh vertices 1855/815/331 and texture 2048×2048. C default worktrees without Library; S original warm-Library snapshot, preceded by C preparation/0.8 changes/recompile history. Not a speed comparison of two cold imports.

<a id="uc-0203-실제-수정재개파일과-메모리"></a>
## UC-02–03: Actual edits, resumption, files, and memory

Baseline `Increment(3)/Double(3)/Calculate(3)` 4/6/13. After L edit 4/6/23; A 5/6/13; B 4/9/13. Later original edits did not propagate to A/B. After normal A shutdown/same-task resumption in both methods, A values/B PID/MVID retained. After C A shutdown, B changed to b12 in same Editor. After S A owned mount/delta reclaim, B PID64016/start ticks `639266201197725855` retained; after b9/MVID verification, b12/new MVID `c5fe00c4-7a14-49ee-bb9a-97f2a316785e` executed. Compared compile/update/failed=false/code hashes. No Play/Test/build.

| Stable interval after 0.8 edit/A resumption | C actual allocation B | S actual allocation B |
| --- | ---: | ---: |
| A task-owned files | 2,002,743,336 | 43,085,992 |
| B task-owned files | 1,887,255,864 | 36,901,240 |
| A/B total | 3,889,999,200 | 79,987,232 |
| S common runner increase | Not applicable | 1,155,416 |

C includes source/Library/control/local evidence; S private journal/records/logs. C A/B Library 1,981,165,568/1,868,238,120 B; S journal allocation 42,926,080/36,634,624 B. Compared with S 81,142,648 B including additional runner once, **designated owned-file allocation decreased 97.91%**. Source common Library 2,068,443,280 B, existing C Git 2,391,743,272 B, common evidence/existing runners/records separate. Per-task VSS attribution/total physical savings unknown. Directory/MFT/VHD-container growth/unobserved full peak outside file total.

S creation responses initialPayloadCopiedBytes/readCacheBytes 0; initial journal/payload 755/61 B. Last A observation ~2.62 GB read/readCacheBytes=0; Library reads not hydrated to disk. Normal shutdown/recompile/metadata writes further grow journal. Does not establish absolute minimum resources or storage only equal to one edited script line. `s-created-allocation` measured after Editor start request, not pure ensure cost.

| Task group median | C Private / Working set GiB | S Private / Working set GiB |
| --- | --- | --- |
| L Editor/owned children | 3.86 / 3.25 | 3.74 / 2.07 |
| A Editor/owned children | 3.25 / 2.72 | 3.21 / 2.71 |
| B Editor/owned children | 3.87 / 3.44 | 3.66 / 2.98 |
| Separate S A/B providers | Not applicable | Private total 213.20 MiB |
| All tasks at same time | 10.98 / 9.41 | 10.82 / 8.06 |

Selected C 54 samples/150.94s, S 24 samples/67.76s. Actual median intervals including 2-second wait/collection overhead 2.85/2.94s. Compared PID/exact start ticks/descendants created afterward; other registered roots excluded from parent groups. Duplicate PIDs in selected samples 0. Unobserved roots retained as null/unknown.

Private differences for three independent Editors small; no large memory savings proven. Same L PID working set also fell 3.25→2.07 GiB. Time/cache/Editor history/OS page management differ; working-set sums may duplicate shared pages, so no RAM-saving ratio. Whole-system available/committed medians C 31.80/59.08 GiB, S 31.73/61.01 GiB, including other apps/existing Editors/services/cache. Minimum RAM/unobserved total peak unknown.

<a id="uc-04-결과와-회수-차단"></a>
## UC-04: Results and reclaim blocking

| Task | Independent Git result |
| --- | --- |
| C A | `codex/uc-c-a-20261003` → `40816fb5968da4e79f87b36267055fc272b4b441` |
| C B | `codex/uc-c-b-20261003` → `cb1e869bd642963ae035180d63ce5535efd05160` |
| S A | `refs/splinter/snapshot-results/40cd86f88ef543d7a8418a6e` → `106d51e155e28b3cbd11ddb84fb9607d233455f8` |
| S B | `refs/splinter/snapshot-results/fd4c3247d6b0d38e6c7b5fba` → `8374d1b5dea8dd1e853b64bb9a2c63802dbca9ac` |
| L C / L S | Trial branch `99b75b399aa08839482453bc9efcc4ca8be2253e` / `551c1418da5065831cf8179accc210db7045a2da` |

C A/B app archive rejected by primary-checkout removal restriction. Preserved source/results/evidence; after verifying Editor absence/exact paths/dry-run, used official Unity CLI `projects clean --yes` to clean only Library/Logs. 94 tracked hashes unchanged. Residual roots 21,596,032/19,085,352 B include source/task-local evidence, not successful whole-checkout reclaim.

S A/B checkpoints succeeded in 94.191/97.297s, preserved `stable-journal-v1` independent results/seals. Releases returned `StateCorrupt` after 44.095/44.047s. Owned Editor/provider/mount/Git registration/delta absent; product process/handles/private/working-set observations 0. Residual records allocation 156,104/238,032 B; two snapshots **preserved as available**. Durable phase `releasing`, recovery `preserve/StateCorrupt`. No full release success/VSS 0 claim.

New A/B JSON deserialized correctly with current Core. Final dependency gate in `SnapshotWorkspaces` calls `store.List()` on same-home records; rejected 73 old records missing currently required `sourceKind`/`applicationKind`. Exact JsonException/each SHA recorded read-only; 73 records unchanged through final. No old-version automatic migration/existing-record deletion/external snapshot reclaim/new home/grant workaround. Installed service/current Runtime binaries are separate candidates; blocking occurred in Runtime full-record query before service deletion request. No installation change.

Restored original `codex/library-20260929 @f71e4e2`, initial 94 raw bytes/hashes/clean state. Canonical fixture `development @6a54974` clean. Preserved existing Editor PID68624/children85184/52688 CIM start ticks/user branches/tags/existing result refs. Four Codex internal `refs/codex/turn-diffs` absent from final list, recorded separately with unresolved cause. Existing pending C: snapshot requests 0; global live verification unknown.

<a id="실패와-해석-한계"></a>
## Failures and interpretation limits

Preserved overlap of 0.7 L `0.0.0.0:7800`/existing protected Editor `127.0.0.1:7800` and exact-path catalog failure. C A/B 0.7 code/assets succeeded. After user approval only test targets changed to 0.8; manifest changes/resolve/reload/normal restart distinguished. Port overlap does not establish internal cause or generalize all 0.7 behavior as failure.

C B stale descriptor 401/S B post-reload 401 reconnected through new exact-path catalogs. S A/B initial/read/post-reload/read/A-resumption first-read server 5-second timeouts succeeded with one same-read retry. Write/compile not repeated solely on timeout. S A stop subsequent Git observation failed because measurer omitted per-execution XDG trust after Editor already exited normally. Preserved same-task reconciliation cost 49.349+8.675s with existing process-local settings, actual resumption/first-read failure. No global safe.directory/administrator-CLI workaround.

Not all process preparation moments collected from start, true peak unknown. Full Medieval input/previous PV S4 failure/full raw unknown, large/ARM/remote, Play/Test/build/capture, power/reboot/full native failures, general installation/binding remain NotRun. New A/B/full matrices not automatically repeated.

<a id="uc-05-보고서와-증거"></a>
## UC-05: Report and evidence

`artifacts/unity-live-comparison-20261003/` preserves responses/costs/94-file hashes/OS allocation/JSONL/independent results/failure diagnosis. HTML includes reviewed tables/prose from `reviewed.json`, links 28 JSON evidence files suitable for public scope. Raw Editor logs/full command lines potentially containing authentication arguments excluded from report.

Current-candidate required-build result is existing locked restore/Release/906 passed/0 failed/1 skip. Actual Unity verification here changed no product code, not Unity Play/Test/build success. UTF-8/HTML source links/calculations/actual-browser tables/graphs/disclosures separately recorded in `report-validation.json`. Small-screen CSS breakpoints inspected in source; separate device-viewport execution qualification NotRun.
