<a id="git-기준의-실제-unity-검증"></a>
# Actual Unity verification with a Git basis

[한국어](../ko/verification/git-unity-validation.md)

Input/exit criteria owned by the [execution plan](../plans/git-unity-validation.md). No prior VSS/Unity qualification inherited. Fixed product implementation commit `d401677`, original `splinter-unity` development `6a54974a9c655031aeb2be17fb7622fc5d641c52`, 1,602 tracked files/2,804,769,314 bytes, Unity 6000.3.22f1/Coplay 10.2.0/original Pipeline 0.7. Verified actual original Editor PID 81892/start ticks 639266922811331760/HTTP `splinter-unity@983ac2308e6c044d`.

New home `.local/gitu1004/home`; A `4a559ff02453d25916bf9a4b`, B `1132a8831629b7e773a16a27`. Original Library not captured. Original repo local `core.autocrlf=input` used temporarily, then original config restored; global settings/checkout files preserved. Independent raw materials `artifacts/git-unity-validation-20261004/`. Existing stdio connector instance 0 distinguished from user's HTTP server actual original connection.

<a id="확인한-준비-결함과-복구"></a>
## Preparation defects verified and recovery

Pre-fix A/B mounts connected, but each `git update-index --refresh` exceeded existing 10-minute operation deadline. A progressed on owned provider PID 55556 through 385MB/1,021 reads, 570MB/1,251 reads, 1.11GB/2,451 reads, then reached deadline. Requested-byte counts not interpreted as full blob-decoding volume. Initial payload duplication/read disk cache 0; private journal then `.git` pointer payload 98 bytes/792 bytes total. Failed-command JSON/state/journal preserved.

Fixed two observed boundaries. First full blob hash fixes 64KiB SHA-256 proofs, retaining only one blob's proof (maximum 4MiB/8GiB blob), one verified chunk, bounded requested staging. Continues sequential decoder; backward reads/other blobs restart required streams. Checks new requested bytes against originally verified fixed contents; destination entirely unpublished if full requested validation fails. All subtrees hash-verified in one bounded batch stream. No full blob-corruption audit per request/unbounded native Git RAM claimed.

After each failed command exited, compared exact owner/request/token/client handles 0 and normally unmounted provider through existing private stop protocol. Empty `index.lock` in same registered `.git/worktrees/<id>` verified by exclusive open/content/creation UTC, moved for preservation to `preparation-recovery-A/B/owned-stale-index.lock`. A recovery-script PowerShell using syntax/local-vs-UTC comparison/final-phase naming errors distinguished as execution/guard failures; no additional resources deleted after normal provider stop. No new A/B/original/peer/VSS cleanup workaround.

First corrected build failed because still-running B CLI PID 43932 locked output DLLs; log preserved. After B deadline exit/same owned stop, required locked restore/build/regression: 922 Passed/1 test excluded at the time NotRun; selected Git reader tests 14/14 Passed.

Resumed same A/B preparation with corrected reader; completed registration/mount/source checks/Editor launch. Observed about 8.54GB cumulative source reads/~704,000 callbacks during preparation, including initial index reads/raw SHA-256/Git hashing input, not initial duplication. Separately changed full SHA-256 range-source input reads from existing 4KiB to up-to-1MiB pooled buffers. Content hash/cancellation/journal barrier/manifest publication semantics retained. After normal A Editor exit, full audit verified 1,620 files/2,804,781,822 bytes SHA-256/same Git hashing amount in 131.494s. Across command provider read callbacks grew 17,418/baseRead 6,094,751,628 bytes/I/O failures 0. Includes Git inspection/index observation, not SHA-256-only callbacks/overall speedup.

Started A Editor PID 69124/start ticks 639266955196373287, B PID 81688/start ticks 639266955572189536. Both `unity start` returned `Accepted`/running=true/ready=false, not compile success. After about 30-minute cold PackageCache preparation, verified package-registration completion logs in both Editors. Journals grew to about A 795MB/B 838MB during this interval; I/O failures 0. Individual durable flush cost observed; no ACK/flush combining/omission.

Same program-source build completed without warnings/errors; full regression overlapping cold import was 920 Passed/2 Failed/1 test excluded at the time NotRun. Failures: existing preparation child not reaching boundary within 30 seconds, storage-setup child 30-second exit wait cancellation. `full-under-cold-import.trx`/logs preserved. After A/B reclaim, required `pwsh ./scripts/build.ps1` locked restore/Release/full 923 tests: 922 Passed/0 Failed/1 test excluded at the time NotRun. Test time 4 minutes 42.447 seconds; prior two deadline tests passed. Product/test deadlines not extended/failing tests not excluded. Final candidate evidence uses `build-quiet-final.log`/`full-quiet-final.trx`/UTF-8/whitespace results.

GUI Editor cold InitialRefreshV2 completed A 5,297.249s (88.3min), B 5,263.066s (87.7min). Imported actual Medieval village textures/models/URP shader includes/material/terrain/prefab input; first script compilation A 124.635s/B 197.441s, Editor assembly initialization about 73s. Some original texture Crunch recorded about 38s. FBX duplicate name/type and terrain shader dependency/support warnings preserved. After completion verified compiling/updating/compilationFailed/playing=false at exact project/PID. Not fast-preparation/warm-Library qualification.

Unity Private Bytes in `texture-import-memory.json` then about A 3.10GB/B 3.05GB/original 3.04GB. Process-group private sums A ~3.67GB/B ~3.66GB; original group also includes shared HTTP/licensing resources. Sample did not demonstrate Editor RAM savings. Resident working-set variation/group sums not total actual RAM attribution/instantaneous peak. Separate from Git-basis initialPayloadCopiedBytes/readCacheBytes 0.

<a id="실제-mcp-편집현재-상태-테스트"></a>
## Actual MCP editing and current-state tests

Discovered original+A+B via user's HTTP server; private bootstrap started only each Editor's HTTP connection. Original server/global preferences unchanged. Exact instance specified per call; C# checked actual project full path/PID/start ticks. First identity-code variable `process.StartTime` triggered MCP's `Process.Start` string check and was rejected; verified with read request changing only variable name. Safety checks retained.

Ran A/B `execute_code` in parallel, creating red/blue 16×16 textures respectively at identical `Assets/GitQualification/parallel.png`, verified by Unity importer. Same `Parallel.unity` path created/saved through Unity API with A `Qualification_A`/x=1, B `Qualification_B`/x=2. No qualification path in original. Opened original SampleScene in each Editor and confirmed dirty=false for additive scene-test loads.

Authored marker/scene/texture tests in identical `GitQualification.EditMode` assembly on A/B; each returned 3/3 success. Initial A job `1d5d002d332d439ca5a835d5df0e708f`/B `978d150c172f4dd4924370fdb0449fda` job start/end intervals overlapped 775ms. Individual test-method intervals unmeasured. Changed only B marker to `wrong-B`: A again 3/3, B explicit negative job `1f961c4c622447bfa6bd565319c0ef1e` completed=3/total=3 with one marker failure. Final restored-B job `7598902cd11e489d88565e5065b0d2f4` again 3/3.

First `run_tests` requests after B negative/restoration/subsequent file authoring each disconnected during domain reload. After actual reload/same project/PID HTTP reconnect, read-only SessionState checks verified no current job/no new job for that request, then explicit execution. No automatic write retry based only on timeout. Negative-job failure list persisted, but `result` detail was null on both queries. Restored success details returned. Thus target isolation/reconnect verified, without claiming complete request/failure-detail preservation stability during reload. Mono Process RAM API 0 replaced with external process observations, not read as memory cost 0.

<a id="독립-결과peer-지속과-회수"></a>
## Independent results, peer continuity, and reclaim

A checkpoint preserved `72b19165b4cd5dcf133d8afdd9608d9e7993254a` at `refs/splinter/snapshot-results/4a559ff02453d25916bf9a4b`. Only 18 qualification source/meta paths added; Library excluded. Command elapsed checkpoint 295.304s/release 117.684s. Verified owned A application processes/handles/mount/provider/delta/basis ref/linked-worktree registration absent; independent result retained.

During A reclaim queried B same PID/start ticks/idle/clean scene; after reclaim `after-A.txt` authoring/blue texture read succeeded. After first subsequent test request reload disconnect/no-job verification, new job `98c175445ef04515abb1eb7bb30633a3` passed all three tests. After normal B Editor exit reverified original PID/start ticks/SampleScene dirty=false/compiling/updating/failed/playing=false/HTTP connection. B full audit verified 1,622 files/2,804,782,008 bytes SHA-256/same Git hash amount in 135.053s.

B checkpoint preserved `d152ef87b611a46577aec6425654b415ef2fbc42` at `refs/splinter/snapshot-results/1132a8831629b7e773a16a27`; completed explicit discard with same `gu1004-discard-B` intent/request digest. `cleanup-verification.json` verifies A released/B discarded, absent mount/provider/handles/delta/basis refs/Git registration, result markers/scenes/different PNG hashes/B subsequent file, result exclusion of Library/Temp/Logs/obj. Compared original development HEAD/clean state/16 existing refs/worktree list to initial observations; local-config hash/global autocrlf exactly restored/retained. `process-cleanup-verification.json` found all 78 A/B owned PID/start ticks from previous resource samples absent; callerElevated=false. Final HTTP list only original; same original PID/start ticks/clean SampleScene/idle retained. Independent results/control/evidence/content-key runner caches preserved, not product-owned task-resource residue.

<a id="비용-표본"></a>
## Cost samples

All 7 root allocation observations in `storage-after-authoring.json` complete. A/B control 1,952,709,928/1,955,405,712 bytes; delta 1,950,875,648/1,953,628,160 bytes thereof. Delta includes new Library/PackageCache/private source/metadata, not all final Library logical data. Shared runner three content keys/24 files/4,051,976 bytes. Existing shared Git 2,392,262,208 bytes/original Library 3,211,753,520 bytes not per-task initial duplication, not double-counted. Each initial journal started at 8,192-byte file allocation; initialPayloadCopiedBytes/readCacheBytes 0. Journals include range storage/compression of cold-generated data/repeated changes, without warm-basis reuse.

All 7 `storage-final.json` root allocation observations also complete. A/B control 1,765,536/1,835,576 bytes/no delta. Source manifests/owned receipts/Editor logs/recovery/reclaim metadata remain. Distinguishes shared runner 4,051,976 bytes, existing shared Git including independent results 2,391,857,504 bytes, evidence at observation 5,130,992 bytes. A/B process groups in `final-memory.json` each 0; original group retained.

Whole instantaneous peak/shared VSS/system RAM per-workspace attribution unknown. Git path invoked no new VSS capture/reclaim; existing non-owned snapshots not deleted. Unconfirmed RAM savings/cold preparation/result-collection cost not turned into no-payload-copy failure or cost 0.

<a id="실행-상태"></a>
## Execution status

| Question | Status |
| --- | --- |
| GU-01 Actual import/compile/same-task resumption | Passed, includes ~88-minute cold import and preparation failure/fix/same-request resumption |
| GU-02 Exact HTTP A/B handoff | Passed with limitations, preserves reload reconnect/request disconnect/lost failure details |
| GU-03 Texture/scene/current-state tests | Passed, parallel authoring/A/B 3/3/B intended failure/restoration verified; test-method simultaneous execution timing unmeasured |
| GU-04 Independent result/B continuing during A release/B discard | Passed, independent results/peer subsequent work/both owned resources 0/original/config/existing-ref protection |
| GU-05 Storage/RAM/preparation cost | Passed with limitations, stage allocation/reclaim/RAM samples/cold cost verified. RAM savings unconfirmed/total peak unknown |

Warm Library composition, Play/build/original integration/new VCS/ARM/OS matrices not qualified here. User Library-composition requirements/project-level immutable-basis need preserved in execution plan.

Fixed one A/B GU-01–05 execution closes as scoped-complete-with-limitations. Verified actual source audit/Unity edits/tests/owned reclaim of new Git reader; 88-minute cold preparation/MCP reload request/failure-detail preservation limits/unconfirmed RAM savings retained for follow-up decisions. Not expanded to fast warm preparation/general support.
