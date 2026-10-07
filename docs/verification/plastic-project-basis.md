<a id="plastic-프로젝트-기준본-검증"></a>
# Plastic project-basis verification

[한국어](../ko/verification/plastic-project-basis.md)

Historical C: pending state and long MCP-response limits from this execution remain. Current bounded resolution of same C: A/B reclamation and detailed external Coplay long-request/reload results follows [VR/MR](vss-and-mcp-remediation.md).

Scope follows [PPB-01–06](../plans/plastic-project-basis.md); current contract follows [Plastic project basis](../plastic-project-basis.md). User excluded RAM savings from acceptance criteria; only actual costs are observed. Raw evidence remains in `artifacts/plastic-project-basis-20261004/`.

PPB native/Unity/cost qualification below is limited to the cm API reader used at execution. Content migration/automated tests for current private stdio reader follow [SB-01](security-boundaries.md); actual Unity/large segmented revisions for new reader are NotRun. Historical full traversal/A/B do not qualify new reader passes.

| ID | Current state | Evidence/remaining conditions |
| --- | --- | --- |
| PPB-01 | Passed with limitations | Full lengths/content hashes of 60 files/244,715B in approved cs 2 checked. Binary/BOM reader defects fixed. External cache/network costs unknown |
| PPB-02 | Passed, bounded native/automated | Fixed basis, actual range overlay, same A/B resumption, management isolation, independent result/release/discard connected. Required regression 961 Passed/1 optional skip |
| PPB-03 | Passed, bounded actual Unity | Separate original/A/B Library probes/journals, B writes during A checkpoint, B tests after release, no final dependents, ordinary NTFS restoration |
| PPB-04 | Passed with limitations | Exact A/B MCP/CLI, rediscovery after reload, texture/importer, scene creation/editing, current-state 3/3 tests/job overlap. Failed-response/actual-completion limits for long writes remain |
| PPB-05 | Passed, bounded actual Unity | A release/B discard, independent-result verification after Library reclamation, owned resources 0, ordinary NTFS original reopening, source/registration/existing Editor/shared-server preservation |
| PPB-06 | Measurement/recording complete | Distinguish task delta/common Library/retained results/runner, actual Editor/provider/API helper RAM, preparation/collection/restore times. Total peak/shared VSS attribution unknown |

<a id="reader입력-확인"></a>
## Reader/input checks

`cm` in restricted execution returned authentication/management-path access denial. Identical reads under ordinary-user permissions confirmed CLI 11.0.16.9915, original root/GUID/regular/static, changesets 0/1. No administrator installation/service/grant/original/server-history-change bypass.

Actual `ReaderProbe` read changeset 1's largest file ProjectSettings/ProjectSettings.asset, 25,496B. Fixed listing 905ms; first 4KiB request verified all 25,496B in one stream, 807ms. Tail/previous-position requests within the same 64KiB chunk took 1/0ms respectively, with 0 additional decoded bytes/streams. Payload storage 0; this small one-chunk-file result is not large/nonsequential-file transmission efficiency. Actual network bytes/native-client cache writes unknown. Probe is not product A/B execution qualification.

Changeset 1 lacks Assets; differences from current original are 37 added/changed files, 201,141B, 0 deletions. Exact path/size/SHA-256 in `input-review.json`. User approved only this list/necessary parents for recording in changeset 2. Plastic MD5 of all approved 37 contents among 60 recorded files matches reviewed original bytes. Packages/existing private `SplinterPlasticOriginalAfterCapture.txt`/ignore.conf unchanged. First check-in reported no changes for changed-but-not-checked-out files; no server history confirmed (cs 0/1). Applying `--all --applychanged` only to identical hashes/paths created cs 2. Both runs, original state, and 37 revision/hash proofs remain.

<a id="자동실제-결과"></a>
## Automated/actual results

`PlasticRevisionTests` checks bounded binary range proof/sequential reuse, corruption/length mismatch/unpublished buffer, rejection after original changes/different owner/fixed-tree changes, reader fence/same release intent, unsupported paths, source/storage/schema. Independent-result tests with file-mount doubles cover verification without server/original and resuming same-basis reclamation after dependent removal fails during discard. Their file materialization is not product WinFsp/no-copy native evidence.

Initial focused run failed on returned-array reference equality and capabilities-schema build-time differences. Corrected to content comparison/rebuild with current schema. Next run failed existing discovery expectations lacking new storageMode, updated to current contract. Subsequent new tests' PascalCase fixture JSON/wrong recovery-assertion property name were corrected. Initial/subsequent logs/TRX remain, not product content loss or first-run passes. Final candidate `pwsh ./scripts/build.ps1`: locked restore/Release build 0 warnings/errors, full 962 tests: 961 Passed, 0 failed, 1 test excluded at that time skipped. Raw logs/results: `build-verified-candidate.log`/`build-verified-candidate.trx`.

<a id="실제-입력에서-발견한-수정"></a>
## Fixes found with actual inputs

First A creation rejected cs 2 PNG as PlasticBasisInvalid because parser excluded XML type `bin`. Regular binary linked to fixed-revision contract/XML reproduction regression added. Same A resumption failed original-repository comparison because installed Windows cm 11 `cm ls / --selector` returned `/ is not in a workspace`. Corrected comparison to controlled-directory XML of explicit original local root; regressions added for ignoring private children/rejecting different root/repository. Existing / + --tree retained for fixed server-tree content reads. Both failures preserved without payload publication/new VSS/registration; resume with same key/home/cs 2/Library.

Original Library preparation 522.15 seconds, initialPayloadCopiedBytes 0. Moved data: 31,646 files/1,903,620,373B; initial metadata/hash/reader-fencing costs distinguished from actual copying. All 6 root measurements in `storage-prepared.json` complete; total VSS attribution unknown.

Raw stdout traversal of cs 2 failed for one PNG/two BOM C# files out of 60. Actual PNG 24,069B became stdout 44,536B/different hash; each C# lost 3B BOM. Named-pipe output probe also returned 0B/exit 1 with client error. No payload-disk-cache bypass. Official local cm API blob probe retained lengths/BOM/hashes of same three revisions and confirmed its own process termination. New tree-owned API reader full cs 2 traversal: 60 files/244,715B/60 streams, 0 errors, 0 payload disk storage. External-client internal cache/server transfers/native full buffering unknown.

Development unmount verification found conflict between existing overlay schema VSS consistency else/separate Plastic condition. Plastic added to provider-specific consistency chain; actual-request schema regression added. After normal unmount, identical A reconnected with new reader, preserving previous failures/responses/delta/basis/Library dependencies.

Initial import exposed repeated full manifest verification/reconstruction at every metadata callback; changed to full tree-open validation/pinned manifest read handle. Automated checks confirm write/replace rejection during 1,000 Stat calls/reclamation after reader closure. Unity queries for nonexistent nested `.git`/`.plastic` return ordinary not-found; recorded-entry management-path rejection remains. Initial provider's 6 Unsupported source path errors remain; final A/B provider epochs have 0 I/O failures each. Normal stop/unmount/reconnect used same id/home/base/Library; no additional cohort.

<a id="실제-unitymcp현재-상태-검사"></a>
## Actual Unity/MCP/current-state checks

Original: `splinter-unity-plastic`, regular/static registration GUID `f3e87fff-e66c-4820-aec7-ebe2a633962f`, cm `11.0.16.9915`, fixed changeset `2@splinter-unity-plastic/splinter-unity-plastic@20066124403170@cloud`. Root revision 196/repository ID 3301056 fixed. Unity `6000.3.22f1`/Pipeline `0.8.0-exp.1`; Coplay MCP `10.2.0`/revision `30d22075093d1d35dfb0091c1c7550e9ad948577` only in A/B private Packages. Original lacks MCP package, so original MCP NotRun; status checked with CLI. Existing Git Editor/shared MCP 8080 server retained.

| Target | Task id | Final Editor PID / UTC start ticks |
| --- | --- | --- |
| Original managed Library | basis `2257c2d6c05d07f59589b19f` | `59500 / 639267199358336563` |
| A | `bc32507b0419d443add291a6` | `58548 / 639267217111827072` |
| B | `245ed2e8b3b6de691bfe5cf5` | `31864 / 639267220495578750` |

First-import start→first MCP connection: A about 25 minutes 5 seconds, B about 25 minutes 38 seconds; C# compilation 487.056/485.459 seconds. Two normal-stop requests timed out at 120 seconds; actual exit observed/confirmation requests closed them. No forced Editor kills/automatic write retries on timeout. Final same A/B reader reconnect ensures: 92.16/119.04 seconds; warm start→MCP about 4 minutes 14 seconds/3 minutes 1 second; compilation 32.023/30.356 seconds. Original Library reference does not guarantee instant readiness; cache/import/concurrent loads differ, preventing speed-multiplier attribution to one cause.

Every MCP write specified exact `unity_instance`; returned project/PID/start matched owned Editor. Initial original/A/B CLI evals hit 5-second main-thread timeouts; explicit read-only observations confirmed exact targets/idle/clean state. Ports not fixed after reload. Private test source edited, scripts refresh requested once per Editor, then MCP instances rediscovered. B HTTP session changed/both exact identities observed; repeated manual connection unnecessary. Distinct MVIDs of each `Library/ScriptAssemblies/PlasticQualification.EditMode.dll` and actual IL `get_localScale`/`get_filterMode` prove edited tests loaded.

A imported red/B blue readable 16×16 PNGs with distinct `.meta`. Each `Parallel.unity` has one root `Qualification_A/B`, x=1/2. Follow-up edit saved Point filter/scale 2/3. Both creation requests returned failure/null, but actual Editor history showed each successful 58.26-second operation; both edits similarly showed failure/null responses then about 31.6-second success each. File hashes/history/follow-up tests prove application; identical writes were not retransmitted. Direct `manage_scene` load/hierarchy/return all succeeded (about 1.4–2.1 seconds), root names confirming isolation. Long CodeDOM-write response timeout is not marked resolved.

Final concurrent test jobs A `2e6d700b96ef47fc976c4d1453cd0ca7`, B `b27d7a37e6344e69982e32bb9a4056c6`: marker/exact project/Library probe/texture/importer/scene/scale **3/3 Passed** each. Job-lifecycle interval overlap **9.84 seconds**; per-method CPU concurrency unmeasured. A-job query from B `Unknown job_id` is expected namespace-separation evidence. During A checkpoint, B wrote/imported `B-after-A.txt`. This also returned null failure, but successful history/current file/final independent result confirmed it; no retransmission. B test request sent with A release took 62.07 seconds to respond; job `4ac077cc82a5428cba95c90c0db2bc23` detailed results 3/3 Passed. Actual job start 15:05:09.638Z followed A release durable completion 15:04:54.179Z. Therefore actual B writes during checkpoint/current-state passes after release are confirmed, but release/test-job execution overlap is unproven. Request-delay cause unconfirmed; no automatic repetition.

<a id="저장량과-ram"></a>
## Storage and RAM

All 7 roots in `storage-work.json` complete. Task-control allocation A **71.90MiB**, B **72.21MiB**, mostly private-range journals including Library writes. Common Library allocation **1865.82MiB** includes **1849.86MiB** moved existing original payload, about **11.02MiB** protected metadata, about **4.94MiB** original delta. Runner **5.63MiB**/evidence **11.17MiB** at same observation are separate. This is not full 1.9GB Library copies per task. This sample is neither total instantaneous peak nor equivalent Codex comparison remeasurement; no general saving rate. No new source/Library VSS requests; total shared VSS costs unknown due to query permissions; historical C: pending remains.

`memory-work.json`: all 8 targets across three exact-PID/start samples complete. Editor-root Private Bytes: A **2995.5MiB**, B **3057.1MiB**, original **3207.2–3209.6MiB**; estimated PSS **2374.9/2439.7/2011.7–2012.0MiB** respectively. A/B provider Private about **180MiB**, original Library provider **164.4MiB**, tree-owned cm API helpers **54.8/55.4MiB**. Root samples do not replace whole Editor-child totals/system peak. ShareCount bounds/nonatomic sample times/kernel-cache/network/external-client native-buffer attribution unknown. RAM savings are not acceptance criteria.

<a id="결과-보존과-회수"></a>
## Result preservation and reclamation

A checkpoint including normal stop **103.07 seconds**, release **30.00 seconds**. B checkpoint **105.65 seconds**, explicit discard with same id/digest/intent **5.92 seconds**. `releasedResources` records verified process/working-set/private 0 for both owned Editor/child groups; confirms absent mount/provider/handles/delta/local basis released. Final Library dependents absent. No automatic sweeping/original/peer/non-owned snapshot deletion.

Independent result A `sha256:23e1c347e3749f490dc33354a2ecf1469fb59ae8003e71f9f095ffd9f89d16e0`; B `sha256:132eee8fcb4af5b305b6d9c2851982bbe36e89611434ab0825f365ad8d661b34`. Product `VerifyResultAsync` checked owner/baseline/result/collection/all payloads with both mounts absent. No original/Library basis/Plastic server content read. A/B unique payload **278,291/278,493B**; creation/read 0 distinguished from explicit result-preservation costs. Modified tests/PNG/scene/importer/B follow-up file also in results. Auxiliary verifier's initial output failed on disposed JsonDocument deferred enumeration; final array-materialized `independent-results-final-verified.json` succeeded. Not product-verifier failure/first-run pass.

All 7 roots in `storage-after-tasks.json` complete; A/B control allocation **552,696/594,080B**. Task deltas absent; metadata/independent results/evidence remain.

Normal Exit after checking exact original path/PID/start/compile/updating/dirty returned CLI Invalid response format. Disconnecting shutdown response preserved/not retransmitted; same PID/start absence observed. Only verification original Library probe removed after content/exact-path comparison. `original-final-source-case-aware-proof.json` records matching approved 37 SHA-256s, two Packages files, `.plastic/plastic.workspace` admission hash, original GUID/regular/static/no home registration. Initial auxiliary-observation path-string case-comparison failure corrected to Windows path semantics, without changing original/registration. All 14 initial/final Editor/provider/API helper/supervisor PIDs in `owned-processes-after-tasks.json` absent. Only existing Git Editor MCP connection remains.

Restore plan: 31,007 files, 2,411 directories, final logical 1,869,236,248B, 15 new files, sum of final private ranges 22,377,109B. Range sum differs from total restore I/O/journal storage. Three exact restore-CLI samples in `memory-restore-moving.json` complete: Private 186,331,136B, working set 173,518,848B, estimated PSS about 132,163,291B. Not full restore peak.

Final `library restore` succeeded in **883.94 seconds**. Cursor **31,007/31,007, filesVerified=true**, state restored; mount/base/delta/dependents/original Library provider absent. Original Library ordinary NTFS folder, probe absent. All 7 roots in `storage-final.json` complete. A/B control unchanged; Library manifest/restore-plan/receipt allocation **32,396,920B**, reusable runner **5,907,808B**, evidence at that observation **13,613,384B** remain. Restored 1.87GB logical Library is current data at original location, not summed as removed payload/new copy.

Original reopened at same Unity/path as **6440/639267243826229521**. First refresh 8.775 seconds; successful exact-path CLI identity confirmed compile/updating/failed/playing=false, clean SampleScene, Library reparse=false, no probe/qualification source. After reopening, 37 SHA-256s/Packages/Plastic registration identity/hash/no home registration still matched. `final-cleanup-proof.json` records retained existing Git Editor **34200/639267143482280358**, 8080 server **73512/639267092568061834**, callerElevated=false. `independent-results-after-library-restore.json` records successful product verification of both independent results without Library basis.

The fixed single Plastic/Unity A/B of PPB-01–06 closes as **scoped-complete-with-limitations**. Initial preparation/collection/restore costs, failed long-MCP-write responses/actual completion, unconfirmed release/B-test execution overlap/request-delay cause, absent original MCP package, unknown external cache/transfers/total physical-RAM peaks remain. Plastic original application, new VCS-free basis, partial/dynamic, server-history changes, Play/build, ARM/OS/large additional inputs are unqualified here. Historical C: VSS pending remains; neither same A/B nor full matrices repeat automatically without a new question.
