<a id="plastic-원본-반영-검증"></a>
# Plastic original-apply verification

[한국어](../ko/verification/plastic-original-apply.md)

Follows the [fixed scope](../plans/plastic-original-apply.md) and [current contract](../plastic-original-apply.md). One small recorded Plastic A/B bundle on Windows x64/NTFS, cm 11.0.16.9915, Unity 6000.3.22f1 closes as **scoped-complete-with-limitations**. Task Editors did not run; original Editor shutdown, applied-resource import, restoration of test changes, and reopening were checked through actual Unity CLI. This is distinct from PPB parallel-Editor/MCP qualification.

| ID | Question | Status |
| --- | --- | --- |
| POA-01 | Full/selected plans, parents/Unity meta, schema, existing digest preservation | Passed, contract/file tests and actual plans |
| POA-02 | Reject unknown registration/selector/locks, original Editor, conflicts | Passed, fake rejection of registration/lock observations, actual management handles/original Editor/file conflicts. Actual external-server lock creation NotRun |
| POA-03 | Same-intent recovery, management-handle exclusion, incomplete-reclamation blocking | Passed, injected response loss in actual NTFS publication/recovery of same selected intent. Full WinFsp/OS failure injection NotRun |
| POA-04 | New actual A/B selected/full application and peer isolation on existing cs2 | Passed, fixed A/B of actual publication candidate. Final addition rejecting incomplete asset/meta deletion is automated file tests; new actual republication bundle for final candidate NotRun |
| POA-05 | A release/B discard, restore only test changes in original, coexistence | Passed, actual product/current state/results/original Unity checks |

Windows file tests with fake cm/mount do not qualify actual Plastic server or WinFsp A/B. Distributed atomicity of remote locks/local publication, partial/dynamic, large inputs, OS/power failure, ARM, Play/build, and importing released results are unqualified. Historical C: VSS pending and MCP execution-time limits remain.

<a id="고정-입력과-실제-결과"></a>
## Fixed inputs and actual results

Original: `C:\Users\olive\Desktop\Development\splinter-unity-plastic`; base `2@splinter-unity-plastic/splinter-unity-plastic@20066124403170@cloud`; GUID `f3e87fff-e66c-4820-aec7-ebe2a633962f`. Home `C:\splinter-poa1005`; no new registration/server writes/VSS. Raw evidence remains in `artifacts/plastic-original-apply-20261005/` and PPB folder `original-poa1005-*`/`poa1005-*source.json`.

| Task | id / digest | Independent result | Final state |
| --- | --- | --- | --- |
| A | `572060a3e4de722d02289c90` / `ecd0b666472170eae068b378` | `sha256:5bb52b9e293b6168929c4215e02ea457667895a500b6988449660b0b77b5f48d` | released, selected apply completed |
| B | `57c8eb493bde0a6defd690aa` / `c2420bef2608d104a3bb9099` | `sha256:220de59757d3a32c5493f77e8e98c2af76a401625d6e9fbdc3c381c724df3cf6` | discarded, full apply completed |

A full plan had 8 items; `--path Assets/Readme.asset --path Assets/SplinterApplyQualification1005/Selected.txt` plan included 6 changes/guards. Selected-plan digest `sha256:15aba30665919dc63ef3abfe11db81e0f2a9cfade4ed83b346995cab5d60cea7`, intent `poa1005-A-selected`. Only Selected/asset/folder meta and Readme comment were published; Withheld/meta remained only in independent objects. B read previous Readme/its own resources even after A comment appeared in original.

B's owned test conflict was rejected with `OriginalApplyConflict` before intent/original writes. After removing only that content-matching file, a 4-item full plan `sha256:b25645c2e4eff7b7bfe5d0a571a57fe7661aa7644592da3fa98e6e8efda79e95`/`poa1005-B-full` published. A selected result remained. Actual server-lock observations had empty inventory; affirmative rejection of Locked/Retained/unknown is parser/fake testing.

First plan with original Editor open was rejected as `PlasticOriginalUnavailable`, with no intent. Initial deferred-exit callback did not stop Editor. Direct normal Exit after rechecking state recorded both `Invalid response format` and actual PID exit, without forced termination. This was not repeating the same write on timeout.

Actual original Readme/metadata was Archive+NotContentIndexed(8224), recorded basis Archive(32), causing first-plan conflict despite matching content. Corrected same A plan preserving the original indexing bit had 0 conflicts and successful actual publication. Final candidate additionally rejects deleting only one half of asset/meta, verified by automated file tests. New A/B publication was not automatically repeated or marked as native qualification of this added rejection.

<a id="정리unity비용"></a>
## Cleanup, Unity, and costs

`final-cleanup-proof-verified.json` confirms absent mount/provider/delta, local basis released, both applies completed, matching SHA-256 for all baseline/result objects, and absence of providers 34352/55572/direct children. Historical mount receipts are retained ownership records, not actual mount presence. Initial measurement's wrong field/event names remain as failure evidence in `final-cleanup-proof.json`; current observations rechecked them.

A actual releasing event was UTC 16:45:59.768–16:46:02.160; **2 of B's 25 reads** actually ran in that interval and matched. Full release-command 21.16 seconds is distinguished from physical-reclamation event interval. This overlaps file reads; it does not resolve previous PPB's unconfirmed MCP test/release execution overlap.

Reopened original 89888/639267293612476820 matched Selected/B TextAsset content, GUIDs `27e34c93796a40d3a99cb11e68001005`/`533281f1093d469ba6b28301bc001005`, folder GUID `953efc528fb74d71bf6a4ed958001005`. Withheld absent; compile/updating/failed=false. After normal shutdown, only matching-hash owned test files were removed and Readme bytes restored. Existing 37 SHA-256s, 2 Packages files, registration GUID/hash all matched. Final original 77676/639267294741020073: clean/nonplaying, compile/updating/failed=false, no test resources, ordinary NTFS Library. Initial final query's .NET hash API was rejected during Unity eval compilation; query changed to a supported API without modifying project files.

Existing Git Editor 34200/639267143482280358 and shared 8080 server 73512/639267092568061834 retained identical PID/start times. Initial payload copying/read cache both 0 for A/B. Preparation A/B 41.10/39.14 seconds, checkpoint 65.61/48.88 seconds, A selected apply 33.33 seconds, B full apply 25.60 seconds, A release 21.16 seconds, B discard 8.13 seconds. Deltas at collection: A 6690B (allocated 8192B)/B 3039B (allocated 4096B); final delta 0, control allocation 422936/427056B, retained objects 314656/310376B. Shared runner/evidence/original Library are separate. Total instantaneous peak/shared VSS attribution unknown; RAM savings are not required by this scope.

Final `pwsh scripts/build.ps1`: locked restore/Release 0 warnings/errors, **978 Passed + 1 Skip / 979**, 0 failed. `final-build.trx`/`final-candidate.json` preserve current regressions/source hashes. UTF-8/diff checks are checked separately.
