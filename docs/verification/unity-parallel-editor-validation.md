<a id="unity-병렬-editor의-실제-에셋테스트반영-검증"></a>
# Actual asset, test, and apply verification for parallel Unity Editors

[한국어](../ko/verification/unity-parallel-editor-validation.md)

On the small Windows x64/NTFS/Git/Unity input of 2026-10-03–04, **MCP target selection, texture/scene editing and reload, failure isolation in current-file tests, full/selected-path apply, and cleanup** for Splinter group S A/B and independent Codex worktree group C A/B were closed as `scoped-complete-with-limitations`. C did not use Splinter at all. The approved follow-up also confirmed actual MCP test-frame overlap in C/S, result preservation/reconnection after reload, and complete removal of C checkouts/Git registrations/parent folders. Earlier sequential execution, initial failures, and cleanup Partial at the time remain as their original evidence below; the current verdict follows the final follow-up section. Full physical peak/per-workspace VSS attribution/Editor RAM reduction remain undetermined. The earlier C: Plastic snapshot hold retains its actual blocker in separate [PC verification](plastic-compatibility.md).

Raw evidence is preserved in [`artifacts/unity-parallel-validation-20261003/`](../../artifacts/unity-parallel-validation-20261003/). External MCP/Unity CLI performed edits, refresh, compilation, tests, and actual-value verification; Splinter handled workspace/owned Editor/independent-result/apply/reclaim boundaries. The bounded qualification of [Unity workspaces](../alpha-workspaces.md) and the [current support tracking table](README.md) remains.

<a id="고정-환경과-대상"></a>
## Fixed environment and targets

| Item | Actual execution scope |
| --- | --- |
| Original/home | Existing `T:\ua-source`, `source=git`, `application=unity`; existing registered home `C:\Users\olive\Desktop\Development\splinter\.local\wl29`. Existing 7 grants/service retained; no new installation/version added |
| Basis | A/B both base/reference/revision `23896dfacba2d90d0c49f826444b178fdbe82d2b`; key/id/digest and returned root of one request retained |
| Tools | Unity `6000.3.22f1`, existing Pipeline `0.8.0-exp.1` in the test baseline, existing Coplay MCP `10.2.0`. Approved 0.8 test baseline applied, then original existing 0.7 configuration restored after completion. Unity CLI read the target's current catalog/schema and used the exact project path |
| A | key `up-s-a-20261003`, id `d02a34b32ea4ca395677b89c`, digest `aed6eefc257aa8e49913d554`; returned root `C:\Users\olive\Desktop\Development\splinter\.local\wl29\workspaces\d02a34b32ea4ca395677b89c`; Editor PID `21200`, start ticks string `639266284202579734` |
| B | key `up-s-b-20261003`, id `cc032fded7ad2ee04b79be9b`, digest `835baab97c73401f945d2da8`; returned root `C:\Users\olive\Desktop\Development\splinter\.local\wl29\workspaces\cc032fded7ad2ee04b79be9b`; Editor PID `51944`, start ticks string `639266284355334240` |
| L | Original `T:\ua-source` Editor PID `14256`, start ticks string `639266281791419086`. Actual project/PID were compared separately during A/B execution; immediately before original apply, it was stopped by approved normal window close after a clean-scene guard |
| Apply-verification L2 | New owned Editor for the same original, PID `25756`, start ticks string `639266314384909281`. Actual loaded values/current-state tests verified after full A/selected B apply, followed by normal window close after a clean-scene guard |
| Approved scope | Actual A/B parallel work, assets/scenes/tests, full/partial original apply, and complete cleanup. The user separately gave explicit approval for preserving/moving old terminal-record content and same-id release retries of existing A/B |

Returned roots from [`s-a-ensure.json`](../../artifacts/unity-parallel-validation-20261003/s-a-ensure.json)/[`s-b-ensure.json`](../../artifacts/unity-parallel-validation-20261003/s-b-ensure.json) were bound to file tools and command working directories. `Accepted`/`editor-starting` from `start` was not interpreted as ready; subsequent catalog and actual eval project/PID were verified. Large ticks were recorded as strings to avoid numeric rounding. Credentials/full process command lines are not included in this document.

<a id="up-추적표"></a>
## UP tracking table

| ID / Check | Current S verdict | C comparison | Main evidence |
| --- | --- | --- | --- |
| UP-01: Exact MCP A/B routing | Passed with limitations. 3 exact routings per A/B and invalid rejection. Per-call targets/actual overlapping tests also verified in follow-up | Passed with limitations. C also verified 6 exact routings, invalid rejection, and follow-up overlap. Initial port collision and explicit bridge reconnection after reload required | [`mcp-routing-concurrent.json`](../../artifacts/unity-parallel-validation-20261003/mcp-routing-concurrent.json), follow-up below |
| UP-02: Texture editing/current-file and loaded values | Passed. The same relative-path 64×64 PNG saved with different A/B colors/import settings; raw PNG compared with loaded texture | Passed. Same PNG bytes/colors/importer values as S; distinct C GUIDs/loaded values verified | [`mcp-texture-edited-and-loaded.json`](../../artifacts/unity-parallel-validation-20261003/mcp-texture-edited-and-loaded.json), [`mcp-scenes-current-loaded-after-reconnect.json`](../../artifacts/unity-parallel-validation-20261003/mcp-scenes-current-loaded-after-reconnect.json) |
| UP-03: Scene/material edit/save/reload | Passed with limitations. A/B root/position/Camera/Light/PNG material link verified; scale edited again, saved, and reloaded. Explicit resume after first rejection/partial creation recorded | Passed. Each C scene created/edited/saved/Single-reloaded; same position/scale and URP Lit reference verified | [`mcp-scene-create-owned-partial-resume.json`](../../artifacts/unity-parallel-validation-20261003/mcp-scene-create-owned-partial-resume.json), [`mcp-scene-edited-reload.json`](../../artifacts/unity-parallel-validation-20261003/mcp-scene-edited-reload.json), `s-a-scene-scale-actual.json`/`s-b-scene-scale-actual.json` |
| UP-04: Current-file tests/failure isolation/restoration | Passed with limitations. Initial 3/0, B marker failure/restoration, and sequential execution at the time retained. Follow-up MCP each 4/0, actual frames overlap 8,761ms | Passed with limitations. Initial job failure/loss/explicit rerun and CLI failure isolation retained. Follow-up MCP each 4/0, actual frames overlap 7,869ms | [`mcp-test-results-initial.json`](../../artifacts/unity-parallel-validation-20261003/mcp-test-results-initial.json), [`s-b-tests-current-state-restored.json`](../../artifacts/unity-parallel-validation-20261003/s-b-tests-current-state-restored.json), follow-up below |
| UP-05/06: Full/partial original apply and complete cleanup | Passed with limitations. Full A/selected B apply, original load/test, B continuity, and existing protection rejection/retry retained. Follow-up S also verified independent results/resources 0 | Passed with limitations. Full A+selected B apply, original load/test/raw preservation, followed by complete removal of both checkouts/Git registrations/parent folders | [`s-a-integrated-all-paths-verified.json`](../../artifacts/unity-parallel-validation-20261003/s-a-integrated-all-paths-verified.json), [`s-b-selected-paths-applied-verified.json`](../../artifacts/unity-parallel-validation-20261003/s-b-selected-paths-applied-verified.json), follow-up below |

`Passed` means a boundary actually verified on this fixed input. The five S items are closed within scope; they do not declare general support for other tools/projects/versions.

<a id="mcp-대상-선택과-병렬의-관측-범위"></a>
## MCP target selection and observed scope of parallel execution

MCP instances discovered original L and A/B together, then selected one explicitly for every request. A/B × 3 nonce reads in `mcp-routing-concurrent.json` returned actual roots and PIDs `21200`/`51944`. Invalid `uv-invalid@deadbeef` was rejected as instance-not-found, without fallback execution on original L. The identical baseline assembly MVID is evidence of the same loaded basis code, not identical Editor identity.

Overlapping MCP request submission and concurrent Editor work are distinct. The initial MCP EditMode runner ran **sequentially**, with A job ending at `1791033063840`ms and B job starting at `1791033064442`ms. This run does not establish concurrent MCP test-runner execution. The native Unity CLI exact-path A/B negative-test call windows overlapped by about 2.448 seconds as shown below. This observes overlap of two CLI calls, not measurements of individual test frames executing at the same instant.

| Call | Start / End (KST) | External elapsed / Result |
| --- | --- | --- |
| A CLI negative companion execution | `22:13:17.0661571` → `22:13:20.6515068` | 3,577ms, CLI exit 0, tests 3 passed / 0 failed |
| B CLI negative execution | `22:13:18.2034894` → `22:13:21.7889601` | 3,577ms, CLI exit 0, tests 2 passed / 1 failed |

Times and ordinary-privilege status follow raw records in [`s-a-tests-parallel-negative-cost.json`](../../artifacts/unity-parallel-validation-20261003/s-a-tests-parallel-negative-cost.json)/[`s-b-tests-parallel-negative-cost.json`](../../artifacts/unity-parallel-validation-20261003/s-b-tests-parallel-negative-cost.json). CLI transport success/exit 0 is not interpreted as test success in the intentional failure test.

<a id="실제-텍스처와-씬의-분리"></a>
## Isolation of actual textures and scenes

Both workspaces use the same input paths: `Assets/ParallelQualification/Shared.png`, `Shared.mat`, `Shared.unity`, and `Marker.txt`. Each task owns its values and new asset GUIDs. Textures are readable=true, compression=Uncompressed; raw PNG size/pixels, AssetDatabase loaded values, and meta GUID links were verified together.

The importer settings supplied to the initial `manage_texture(create)` appeared as `importSettings={}` in the response, and actual `isReadable=false`. This request was not counted as a settings success. After explicitly changing TextureImporter through `execute_code` on the same exact MCP target, `isReadable=true` and actual pixels were verified. The internal cause of argument loss remains undetermined; the tool's initial behavior and actual Unity state after correction are distinguished. Evidence is [`mcp-texture-create.json`](../../artifacts/unity-parallel-validation-20261003/mcp-texture-create.json) and `mcp-texture-edited-and-loaded.json`.

| Current value | A | B |
| --- | --- | --- |
| PNG dimensions / RGBA | 64×64 / `(255,64,32,255)` | 64×64 / `(32,64,255,255)` |
| filter / wrap | Point / Clamp | Bilinear / Repeat |
| PNG SHA-256 | `c22f129db1e087779d4ff0dad52bb85bc0b81c0093a8b497607e3f7a71f4dea4` | `5c0843d32867de66a507074c0d9ea2f281268c720df82112750eb754e6069da8` |
| Scene root / Position | `UP_A` / `(1,2,3)` | `UP_B` / `(4,5,6)` |
| Root scale reloaded after additional edit | `(2,1,1)` | `(1,3,1)` |
| marker | `A-v1` | `B-v1` |

The scene has Camera/Light, and the root's MeshRenderer material references that workspace's `Shared.png` as mainTexture. The saved scene was reopened in Single mode and its loaded root/position/texture link verified. Actual reloaded values after the later scale edit are in [`s-a-scene-scale-actual.json`](../../artifacts/unity-parallel-validation-20261003/s-a-scene-scale-actual.json)/[`s-b-scene-scale-actual.json`](../../artifacts/unity-parallel-validation-20261003/s-b-scene-scale-actual.json). Asset-state observation does not mean final screen rendering/Play/build succeeded.

<a id="현재-파일을-읽는-세-테스트"></a>
## Three tests reading current files

[`write-current-state-tests.ps1`](../../artifacts/unity-parallel-validation-20261003/write-current-state-tests.ps1) verifies the current ensure's exact root/key/id/base, then creates the same Editor-only `UP.CurrentState.Tests` assembly/test class in each task. Expected constants differ for A/B. The three tests cover marker file, saved-scene root/material links, and texture dimensions/pixels/import settings. Before every test, they read current files and output project/PID/start, marker, test-assembly MVID, and file hashes.

External recompile was observed as `completed`, `failed=false`, `compilationFailed=false` for both A/B. Initial MCP tests were each 3 passed / 0 failed. With only B's marker file changed to `B-v2` and test source unchanged, CLI A/B execution failed only B's marker test with `Expected: B-v1 / But was: B-v2`; B's scene/texture tests and all three A tests passed. Restoring B's file to `B-v1` returned it to 3 passed / 0 failed. This intentional failure test verified that current-file changes reach the gate without relying on recompilation of test code.

| identity | A | B |
| --- | --- | --- |
| test assembly MVID | `a686f47c-bdb3-404f-a9cf-98b5f810ba4d` | `aa76aeb6-491c-4596-8f11-b28afc950ce0` |
| Final actual marker | `A-v1` | `B-v1` |
| Final actual PID | `21200` | `51944` |

[`s-b-marker-negative-state.json`](../../artifacts/unity-parallel-validation-20261003/s-b-marker-negative-state.json)/[`s-b-marker-restored.json`](../../artifacts/unity-parallel-validation-20261003/s-b-marker-restored.json) record `testSourceChanged=false` and marker hashes for both changes. Final actual identity was compared using [`s-a-tests-source-identity-final.json`](../../artifacts/unity-parallel-validation-20261003/s-a-tests-source-identity-final.json)/[`s-b-tests-source-identity-final.json`](../../artifacts/unity-parallel-validation-20261003/s-b-tests-source-identity-final.json). Whole-game correctness and Play/build are `NotRun`.

<a id="전체부분-반영과-실제-원본-검증"></a>
## Full/partial apply and actual original verification

A's stable independent result `c95c6870495bd907553ae9c856fcdf46aa2451a5` was applied in full using product `integrate` to the original's explicit test branch `codex/up-source-20261003`. Original HEAD is `5123d9d8c874af2e0bb213c408bfd0f1bd27fdcb`; each of 14 original source/meta files was compared against A-result blob/SHA-256. [`s-a-full-integrate.json`](../../artifacts/unity-parallel-validation-20261003/s-a-full-integrate.json) records Succeeded / integrated; [`s-a-integrated-all-paths-verified.json`](../../artifacts/unity-parallel-validation-20261003/s-a-integrated-all-paths-verified.json) establishes equality of 14 files and no changes to other basis files.

A release was Succeeded / released, observing mount/provider/delta/snapshot absence, and 0 openHandles/provider working/private bytes/released application-group working/private bytes. Meanwhile, B read its current marker/texture/scene on the same PID `51944`. After A release, B's new `SelectedB.png` was edited again to `(48,240,80,255)` and current tests verified at 3 passed / 0 failed. See [`s-b-live-during-a-release.json`](../../artifacts/unity-parallel-validation-20261003/s-b-live-during-a-release.json), [`s-b-new-resource-write-after-a-release.json`](../../artifacts/unity-parallel-validation-20261003/s-b-new-resource-write-after-a-release.json), and [`s-b-tests-after-a-release.json`](../../artifacts/unity-parallel-validation-20261003/s-b-tests-after-a-release.json).

B checkpoint in [`s-b-checkpoint-required-message.json`](../../artifacts/unity-parallel-validation-20261003/s-b-checkpoint-required-message.json) returned Succeeded / collected, with stable-journal-v1 independent result `03b5c6271900b004a062dbafb686f4a24a557f6a`. Only explicitly selected `SelectedB.png`/`.meta` were applied through the AI/Git exact-path restore/commit procedure in this verification. **This does not qualify a product partial integrate API.** At original HEAD `091b0b0e377563f8a19264181546c6913b72a543`, the two selected files match B blob/SHA, while A's 14 file hashes remain. The excluded 14 B files are preserved in the independent result. Selection/exclusion lists and actual verification are in [`s-b-partial-apply-plan.json`](../../artifacts/unity-parallel-validation-20261003/s-b-partial-apply-plan.json)/[`s-b-selected-paths-applied-verified.json`](../../artifacts/unity-parallel-validation-20261003/s-b-selected-paths-applied-verified.json).

B's first release was rejected with `DeltaChangedAfterCollection`. [`s-b-checkpoint-stable-after-release-refusal.json`](../../artifacts/unity-parallel-validation-20261003/s-b-checkpoint-stable-after-release-refusal.json) returned the already collected result and previous seal (sequence 39,232 / journal 55,618,778 bytes). **This is not recorded as successful new collection.** [Runtime checkpoint](../../src/Splinter.Runtime/WorkspaceCoordinator.cs) returns the existing view when current source revision/independent result match and nothing changed; this path with an existing result does not collect anew.

An explicit release retry on the same task performed collection after infrastructure removal and stable/unmount seal comparison in the [current release implementation](../../src/Splinter.Runtime/SnapshotWorkspaces.cs), ending Succeeded / released. The final seal in [`s-b-release-explicit-quiescent-retry.json`](../../artifacts/unity-parallel-validation-20261003/s-b-release-explicit-quiescent-retry.json) is sequence 39,234 / journal 55,619,588 bytes / SHA-256 `5f7fd72800e5c443087582804066796b8a4ffefffcd34bee87aa447660ea49f2`. It observed snapshotAvailability=missing, mount/provider/delta absence, and 0 openHandles/provider/application-group working/private bytes. First independent revision `03b5c6271900b004a062dbafb686f4a24a557f6a` is also preserved in additional `refs/splinter/qualification-results/up-b-first-collected-20261003`.

The applied original was actually opened in L2, verifying `UP_A` position `(1,2,3)`/scale `(2,1,1)`, A Shared PNG/GUID, B selected PNG `(48,240,80,255)`/GUID, and marker `A-v1`. compiling/updating/failed=false and current-state EditMode tests were 3 passed / 0 failed. Actual project in [`l2-integrated-assets-scene-actual.json`](../../artifacts/unity-parallel-validation-20261003/l2-integrated-assets-scene-actual.json)/[`l2-integrated-current-state-tests.json`](../../artifacts/unity-parallel-validation-20261003/l2-integrated-current-state-tests.json) is `T:\ua-source`, PID `25756`. `Marker.txt.meta` created by actual Unity import was recorded separately with its hash and preserved at test-branch HEAD `106dbf97cf6b82ea164a2bb1a97c058c78532f3a`. See [`l2-final-trial-source-preserved.json`](../../artifacts/unity-parallel-validation-20261003/l2-final-trial-source-preserved.json).

<a id="준비-단계의-보존과-같은-작업-회수"></a>
## Preparation-stage preservation and same-task reclaim

Current Core does not automatically migrate earlier-format terminal records. The existing home's 73 old terminal records (72 released / 1 discarded) had no workspace/delta and reclamation was released; full-record enumeration returned `StateCorrupt`. Following explicit user approval, their **unchanged content** was moved for preservation to `.local/wl29-archive-20261003-uv`. Before/after hash verification for 2,130 files and comparison of 72 independent-result refs are preserved. Old states were not converted to current states, and independent results were not deleted.

Evidence is [`terminal-record-archive-plan.json`](../../artifacts/unity-parallel-validation-20261003/terminal-record-archive-plan.json), [`archive-result-refs.json`](../../artifacts/unity-parallel-validation-20261003/archive-result-refs.json), and [`archive-executed.json`](../../artifacts/unity-parallel-validation-20261003/archive-executed.json). Archive result is `count=73`, `files=2130`, `verified=true`. Existing 7 grants/service package were reused unchanged.

Next, approved release retries were performed on the **same** earlier-comparison A id `40cd86f88ef543d7a8418a6e`/B id `fd4c3247d6b0d38e6c7b5fba`. Both responses were Succeeded / released, observing `snapshotAvailability=missing`, mounted/providerAlive/deltaPresent=false, reclamation disposition=released. Independent-result revisions in [`old-a-explicit-release.json`](../../artifacts/unity-parallel-validation-20261003/old-a-explicit-release.json)/[`old-b-explicit-release.json`](../../artifacts/unity-parallel-validation-20261003/old-b-explicit-release.json) are preserved as `106d51e155e28b3cbd11ddb84fb9607d233455f8`/`8374d1b5dea8dd1e853b64bb9a2c63802dbca9ac`. This did not bypass protection through other-snapshot deletion, new grants, or automatic old-version recovery.

<a id="실패재연결반영-전-경계"></a>
## Failure, reconnection, and pre-apply boundaries

| Observation | Handling and preserved limitations |
| --- | --- |
| No Pipeline instance in first A catalog | Response before descriptor readiness preserved; catalog checked again after readiness on the same returned root. Not judged as Editor absence |
| First 5,000ms main-thread timeout in CLI bridge start/status read | Failure JSON and subsequent actual bridge state preserved. Lost write response not guessed as success/failure; writes not automatically repeated on timeout alone |
| Initial MCP blocked-pattern rejection for scene creation | Pre-execution rejection response preserved and code corrected. Next additive scene creation failed due to unsaved untitled scene; after verifying the already-created owned material, resumed through explicit replacement of the clean untitled scene |
| Missing MCP instance / bridge=false after reload | Actual state read at exact CLI project/PID, then that task's bridge reconnected. Current instance selected from new discovery; no L/default-resource fallback or permanently fixed port |
| First A checkpoint `DeltaChangedAfterCollection` | Overlapping writes/seal failure preserved. Explicit recollect of stable source returned Succeeded / collected, independent revision `c95c6870495bd907553ae9c856fcdf46aa2451a5`. First attempt not changed to passed |
| Namespace error in B bridge-stop eval | Eval compilation rejected nonexistent namespace before stop execution. Corrected request after verifying current actual API namespace and failure JSON preserved; not expanded into Unity project source-compilation failure |
| B checkpoint missing `--message` | InvalidArguments input rejection in `s-b-checkpoint-final.json` retained. Separate explicit request `s-b-checkpoint-required-message.json` with current schema's required message succeeded as collected |
| PowerShell precedence error in B selected-path stage guard | Incorrect comparison after selected restore failed the guard. Actual two staged paths read and verified against expected two; restore not repeated. `s-b-partial-staged-guard-observed.json` preserved |
| First B release `DeltaChangedAfterCollection` | First rejection/checkpoint returning existing result preserved. Explicit same-id release retry verified final 39,234 seal match and successful reclaim; not marked as successful checkpoint recollection |
| Evidence-save typo for normal shutdown of original L | `CloseMainWindow` called once after exact project/PID/start and clean-scene (`dirty=false`) guard. PowerShell false-literal typo after the call failed evidence saving, but processPresent=false in the observation file confirmed PID 14256 exit. No repeated call/forced termination |

Original scene failures are in [`mcp-scene-create-save-reopen.json`](../../artifacts/unity-parallel-validation-20261003/mcp-scene-create-save-reopen.json)/[`mcp-scene-create-save-reopen-corrected.json`](../../artifacts/unity-parallel-validation-20261003/mcp-scene-create-save-reopen-corrected.json); actual file/loaded values after reconnection are in `mcp-scenes-current-loaded-after-reconnect.json`. During A checkpoint, B maintained `B-v1`, pixel `2040FFFF`, and the relevant Shared scene on the same PID `51944`. The L shutdown boundary follows [`l-clean-scene-before-owned-close.json`](../../artifacts/unity-parallel-validation-20261003/l-clean-scene-before-owned-close.json)/[`l-owned-window-close-observed.json`](../../artifacts/unity-parallel-validation-20261003/l-owned-window-close-observed.json).

<a id="최종-정리복원과-자격의-한계"></a>
## Final cleanup/restoration and qualification limits

The observation at `2026-10-03T22:41:45.3333474+09:00` in [`final-native-cleanup-and-restoration.json`](../../artifacts/unity-parallel-validation-20261003/final-native-cleanup-and-restoration.json) confirmed these completion boundaries. Independent results, terminal receipts, shared runner cache, and raw evidence are intentionally preserved. Complete cleanup means reclaim of owned execution resources/temporary state, not deletion of preserved results.

| Final comparison | Actual result |
| --- | --- |
| Original | Existing branch `codex/library-20260929`, HEAD `f71e4e244ba9b085aa0fc439336e4c60ee3d5d5d` in `T:\ua-source`, clean. All initial 94 tracked-file hashes match; test `ParallelQualification` assets absent |
| Test apply result | `codex/up-source-20261003` HEAD `106dbf97cf6b82ea164a2bb1a97c058c78532f3a` retained. Full A/selected B apply and actual local import result preserved |
| Canonical fixture / Existing Editor | `splinter-unity` development HEAD `6a54974a9c655031aeb2be17fb7622fc5d641c52` clean. Protected Editor PID `68624`, start ticks `639265412574256360`, same identity alive |
| This A/B execution's resources | Workspace/delta/Git registration/owned-provider/Editor PIDs absent, snapshotAvailability=missing. Provider handles/working/private bytes and application-group activeProcesses/working/private bytes all 0 |
| Original-verification Editors | L `14256`/L2 `25756` and all direct children absent. Only normal window close used; no forced Editor termination |
| Preserved records | All hashes of archive 73 records / 2,130 files unchanged; existing 72 independent refs unchanged. This A/B's independent-result refs also preserved |
| Snapshot protection | Only this run's owned snapshots reclaimed. nonOwnedSnapshotDeleteRequests=0. Existing holds on other volumes not deleted or bypassed |

[`mcp-owned-discovery-cleanup-verified.json`](../../artifacts/unity-parallel-validation-20261003/mcp-owned-discovery-cleanup-verified.json) removed only two owned discovery files of closed A/B and restored the legacy discovery file's initial bytes. Existing 7 discovery-file hashes remain; no owned port files remain. [`l2-mcp-preferences-owned-restore.json`](../../artifacts/unity-parallel-validation-20261003/l2-mcp-preferences-owned-restore.json) returns restored port=6400, HTTP=true, and absent resume key from the actual original project/PID. Global MCP files/other tasks were not cleaned in bulk.

The final actual MCP `mcpforunity://instances` read also returned `instance_count=0`, `instances=[]`. It is preserved in [`final-mcp-discovery.json`](../../artifacts/unity-parallel-validation-20261003/final-mcp-discovery.json) and is not evidence that the separately protected HTTP Editor is absent.

The C follow-up below adds actual comparison of five functions; it does not newly rank C/S performance. Both runs verify procedures on small fixed Git/Unity asset input. Large projects, new package/Editor versions, MCP runner concurrency, Play/build, other VCS, ARM/OS failures, and full physical/RAM peak are outside qualification scope. Per-workspace shared-VSS attribution and full peak are `unknown`; earlier C: reclaim holds are not considered resolved by this T: success.

<a id="codex-독립-worktree의-실제-후속"></a>
## Actual follow-up with independent Codex worktrees

At the user's request to execute remaining verification, existing app-created C A/B were reused once. Only ordinary Git/Unity CLI/Coplay MCP/OS observation were used; no Splinter commands/providers/services/mounts/Editor helpers or Coordination were used. The same base `23896dfacba2d90d0c49f826444b178fdbe82d2b`, Unity/Pipeline/Coplay/Test Framework versions, and S's small PNG/scene/test input were retained. Raw evidence is in [`artifacts/unity-codex-parallel-validation-20261003/`](../../artifacts/unity-codex-parallel-validation-20261003/). Existing HTML remains a record of S execution before C follow-up; these follow-up results are added only to this document and conversation text.

| Target | Actual project / Editor identity / MCP |
| --- | --- |
| C A | `C:\Users\olive\.codex\worktrees\1a8c\splinter-unity`; PID `91988`, .NET start ticks `639266331161540380`; `splinter-unity@bb813d67`, stdio 6401 / Pipeline 7802 |
| C B | `C:\Users\olive\.codex\worktrees\90dd\splinter-unity`; PID `29848`, .NET start ticks `639266331164797634`; `splinter-unity@71652d88`, stdio 6402 / Pipeline 7803 |
| C L / Apply L2 | `T:\ua-source`; PID `80412` / `55012`, .NET ticks `639266331168278078` / `639266340563469396`. L MCP `ua-source@a28d347c`, stdio 6400 / Pipeline 7801 |

Unity logs for each A/B initial cold Library import report 198.035/197.805 seconds. These are times from this initial Refresh log, not total preparation/CLI open cost or a fair import-speed comparison with S warm Library. External elapsed for `open` also includes pipe lifetime maintained until Editor exit, so it is not used as startup time.

[`mcp-routing-pairs.json`](../../artifacts/unity-codex-parallel-validation-20261003/mcp-routing-pairs.json) returns actual project/PID/start/nonce 3 times per A/B. `cv-invalid@deadbeef` is rejected, with default selection retained at L. Initial `Start()` returned bridge=false due to a port 6400 collision; read observation subsequently confirmed each running on 6401/6402 after the installed bridge's delayed fallback. Start writes were not repeated. After compilation/reload, bridge=false was verified, then only the relevant owned bridge was explicitly reconnected. Global HTTP=true was retained for other Editors.

C PNG SHA-256 in [`mcp-current-assets-scene.json`](../../artifacts/unity-codex-parallel-validation-20261003/mcp-current-assets-scene.json) matches S A/B PNG above respectively. Colors/64×64/readable/Uncompressed/filter/wrap/scene root/position/scale also match. C GUIDs differ: A `182d9603478f0644484122a1898b1cd1`, B `fc095651f139dcf4a995bfe85fb3ec57`. Following scene creation/save/reopen, separate scale edit/save/Single reopen verified loaded values and the URP Lit material's texture reference. An explicit Unity API procedure accounted for importer/clean Untitled restrictions found in the first S run; equal per-tool first-attempt cost is not claimed.

The three NUnit test bodies/asmdef are identical to S. Actual C assembly MVIDs are A `d8684be1-31c5-4f3d-b617-0d4b201d7ded`, B `69fc36cb-0a2a-4413-bfee-e37c141b7c84`. Reload after initial MCP job creation disconnected the bridge. After reconnection, A job failed with initialization timeout; B job was `Unknown job_id`, and B SessionState had no job record. [Initial result query](../../artifacts/unity-codex-parallel-validation-20261003/mcp-tests-results-after-reconnect.json) and [actual state before rerun](../../artifacts/unity-codex-parallel-validation-20261003/mcp-stable-test-retry-pre-observed.json) are preserved. Internal causes of initialization/loss remain undetermined; CLI passes do not replace those failures. After observing failure/idle state, the same tests were explicitly rerun once with init_timeout=60000, comparing each 3/0 in [`mcp-stable-tests-retry-results.json`](../../artifacts/unity-codex-parallel-validation-20261003/mcp-stable-tests-retry-results.json) with **actual C project/PID/start/MVID/file hashes in every test's output**. The MCP runner was sequential, with A ending at `1791037007673` and B starting at `1791037008144`; no concurrent MCP-test qualification is claimed.

Independent native CLI initially returned A/B each 3/0, then A 3/0 and B 2/1 after only B's marker file changed to `B-v2`, then B 3/0 after restoring `B-v1`. Current-file failure was observed without changing test source/MVID. Call windows in [`a-cli-negative-peer-tests-cost.json`](../../artifacts/unity-codex-parallel-validation-20261003/a-cli-negative-peer-tests-cost.json)/[`b-cli-negative-peer-tests-cost.json`](../../artifacts/unity-codex-parallel-validation-20261003/b-cli-negative-peer-tests-cost.json) were UTC `14:17:22.5992175`→`14:17:25.9169903` and `14:17:20.7150020`→`14:17:23.9365592`, respectively, overlapping by about **1.337 seconds**. Actual individual test-frame concurrency was not measured. CLI exit remained 0 despite B failure, so actual Summary/Results determined the verdict. Returned MCP/CLI JSON was preserved separately for each task instead of the default `TestResults.xml` path in Unity logs.

| Change preservation / Apply | Actual result |
| --- | --- |
| Full C A | `codex/uc-up-a-20261003` → `33e3b2d07a7c0e0f869a947b2dfd4f919e696b05`; only 15 new paths committed. Compared with S's 14 paths, A had already created and included Marker.txt.meta |
| C B independent result | `codex/uc-up-b-20261003` → `2821b2e1e14593cce5df68820b7b4df00ac905a6`; only 17 new paths committed. After A cleanup, SelectedB pixel edited to `(48,240,80,255)` on the same B PID; tests 3/0 |
| Test original | `codex/uc-up-source-20261003` → `5343f8324749b4c7551892bfead9240276e1864a`; ordinary Git fast-forwarded full A, then applied only B SelectedB.png/meta paths. Existing 109 original-file hashes unchanged; source/target hashes of 2 selected files match |
| Actual local load/test | L2 verified A root/scale/PNG/scene hashes/GUID and selected B 32×32 pixel/GUID. Current-state tests 3/0; new local test MVID `0a379901-0c63-47eb-a7b5-3fa41344aa3c` |

Apply evidence is [`b-selected-local-applied.json`](../../artifacts/unity-codex-parallel-validation-20261003/b-selected-local-applied.json), [`l2-integrated-assets-actual.json`](../../artifacts/unity-codex-parallel-validation-20261003/l2-integrated-assets-actual.json), and [`l2-integrated-current-tests.json`](../../artifacts/unity-codex-parallel-validation-20261003/l2-integrated-current-tests.json). Existing uncommitted raw baseline.txt bytes were not staged/restored/deleted. Automatic approval review rejected the restore command because of risk of losing changes; work continued by handling only new verification paths. Although Git diff had no semantic difference, status `M` and existing bytes were preserved unchanged.

L/A/B/L2 all exited normally through `CloseMainWindow` after clean guards/exact PID/start comparisons. No forced termination occurred; each owned process/direct child was confirmed absent. Official Unity CLI cache cleanup reported A 1,833,629,250 B / B 1,833,731,146 B of Library/Logs **logical bytes**, and both Libraries were confirmed absent. This is not new measured physical-allocation/RAM reduction. The existing app's primary-checkout archive rejection was neither bypassed by forced deletion nor retried for the same reason, so full C checkout reclaim is Partial.

[`source-and-old-worktree-restored.json`](../../artifacts/unity-codex-parallel-validation-20261003/source-and-old-worktree-restored.json) restores/compares old C A/B branch/HEAD `40816fb`/`cb1e869` and each set of 94 tracked raw hashes, plus T original `f71e4e2` and 94 raw hashes. Existing C baseline.txt state was preserved, and new test branches/commits retained. The earlier 72 independent refs in [`protected-independent-results-unchanged.json`](../../artifacts/unity-codex-parallel-validation-20261003/protected-independent-results-unchanged.json) are unchanged. Canonical development `6a54974` clean and protected Editor PID68624/CIM ticks `639265412574256360` alive were verified.

[`mcp-owned-cleanup-verified.json`](../../artifacts/unity-codex-parallel-validation-20261003/mcp-owned-cleanup-verified.json) removed only two exact closed C A/B discovery files, restored legacy-file initial bytes, and confirmed matching hashes for 7 existing files. L2 actually verified port=6400, HTTP=true, and absent resume key after the change-ownership guard. Final stdio instances are 0; this does not establish absence of the separate HTTP Editor. This C functional verification does not change full physical/RAM peak unknown or other unexecuted support items in the earlier storage/memory comparison.

<a id="문서와-현재-계약의-회귀-확인"></a>
## Regression verification for documentation and current contracts

Required `pwsh -NoProfile -File scripts/build.ps1` ran on the same product candidate `52d9099b2df3bdd786defbabe8e0b04d3ea0cdf3`, confirming locked restore/Release build (0 errors/warnings)/906 passed / 0 failed / 1 skip. [`required-build-validation.json`](../../artifacts/unity-parallel-validation-20261003/required-build-validation.json) and frozen TRX are preserved. There were no product code/schema changes; this automatic regression is not execution of additional Unity/VSS support matrices. Final UTF-8/HTML link/actual browser-display comparisons follow `report-validation.json`.

The required build ran after the actual Unity execution above. Its Runtime SHA-256 `aea683d3004e1c6224548aeb6b0a8f09aebaa9217b1b15103b9d72af9c21c5b7` is a separate build-validation artifact identity. It does not mean the binaries match the earlier executed files or that the native matrix reran using this new artifact.

After closing C follow-up, the required build ran again from documentation commit `0529a0e87f48f1f9348ce6fb88d93af227be2d06`. locked restore/Release (0 warnings/errors)/906 passed / 0 failed / 1 skip are preserved in the [separate product-regression record for C follow-up](../../artifacts/unity-codex-parallel-validation-20261003/required-build-validation.json) and frozen TRX. Runtime SHA-256 is `e15a295cfc20a75d66c8651f20824d8f8c2134549ecf0b12df7c44365b6b72e3`; this binary did not rerun S native verification, and C did not use Splinter. Follow-up documents were compared for UTF-8/diff/evidence links/actual results; HTML was not changed.

<a id="승인한-연결비용전체-정리-후속-2026-10-04"></a>
## Approved connection, cost, and full-cleanup follow-up (2026-10-04)

The three unfinished areas specified by the user were executed within the [fixed scope](../plans/unity-parallel-editor-validation.md#approved-connection-cost-and-cleanup-follow-up). Product source baseline is `e61270b`; tools are Unity `6000.3.22f1`, Pipeline `0.8.0-exp.1`, Coplay `10.2.0`, and Unity CLI `1.0.0-beta.5`. C used only the two existing Codex worktrees and ordinary Git/MCP/CLI/OS measurement. S connected the same task input to separate snapshots/private journals without changing product code/installation/versions. Evidence is preserved in [`unity-followup-20261003`](../../artifacts/unity-followup-20261003/); HTML is not updated.

| Target | Current task identity / Result |
| --- | --- |
| C A/B | Existing `1a8c/splinter-unity`/`90dd/splinter-unity`; actual test PIDs `67732`/`75348`; preserved commits `44ca7db18b1271149bcfea4cad4bb9238715c974`/`b2fc1733a43723b235f29457da9a8d0d4910153e` |
| S A | key `uf-s-a-20261004`, id `a11706b9134d80aaa36df46f`; corrected PID `32116`, ticks `639266373431877788`; independent result `7cf07dacbefc0473a383ebb188fb35741845d2f9` |
| S B | key `uf-s-b-20261004`, id `1861e89ca17b018d19177fba`; corrected PID `50316`, ticks `639266373480692699`; independent result `1a2328367ceea023c5106e31390952e383abe7ba` |
| L | Test branch `codex/uf-source-20261003` in `T:\ua-source`, base `5343f8324749b4c7551892bfead9240276e1864a`; PID `29268`, ticks `639266363920309963`. Original `codex/library-20260929`/`f71e4e2` and 94 raw files restored after completion |

This S run's first input copy mistakenly used directory `Copy-Item`, nesting `c-a-task-input`/`c-b-task-input` under existing folders. Duplicate `UP.CurrentState.Tests` asmdefs caused compilation errors and a Safe Mode prompt on first Editor start. After the user closed with Quit, exact owned nested files/meta were removed and copied again using per-file relative paths. The same id/home was used without new tasks; C task-file hashes were verified for [A](../../artifacts/unity-followup-20261003/s-a-closed-input-equality.json)/[B](../../artifacts/unity-followup-20261003/s-b-closed-input-equality.json), then restarted. Final actual read showed compile/import/failed=false and a clean scene; MCP tests also passed. Initial journals/failed-start costs are not excluded or recorded as first-attempt success. S A retains the original basis's small additional `SelectedB.png`/meta, so exact equality of all Assets/Library input is not claimed.

<a id="mcp-연결과-실제-테스트-겹침"></a>
### MCP connection and actual test overlap

One Editor-only `OverlapProbe`, checking the current marker every frame for 12 seconds, was added to the three existing marker/scene/texture tests. Each call fixed `unity_instance`; tests recorded actual project/PID/start/MVID/start and end Unix ms. Each task's current marker remained while two separate Editors tested concurrently. C/S A/B each returned **4 passed / 0 failed**.

| Actual test frames | A start→end (Unix ms) | B start→end (Unix ms) | Actual overlap |
| --- | --- | --- | --- |
| C | `1791040010833`→`1791040022863` | `1791040006599`→`1791040018702` | 7,869ms |
| S | `1791040996101`→`1791041008188` | `1791040999427`→`1791041011429` | 8,761ms |

[C detailed results](../../artifacts/unity-followup-20261003/c-mcp-concurrent-test-results-first.json)/[S detailed results](../../artifacts/unity-followup-20261003/s-mcp-concurrent-test-results-first.json) were compared with `c-a/b-test-window.json`/`s-a/b-test-window.json`. Mere external-call overlap was not changed into actual concurrent testing; earlier sequential records for the three tests remain. This `init_timeout=60000` is a bounded budget used after actual compile/import completion and exact-target verification. PlayMode/build/other versions/large input are NotRun.

According to [installed-source evidence](../../artifacts/unity-followup-20261003/mcp-installed-source-investigation.json), when HTTP settings are enabled, `StdioBridgeReloadHandler` stops manual stdio before reload and clears resume intent. On actual S A, detailed results were preserved externally, then one explicit domain reload was requested; bridge=false and idle/compiled were verified. After explicit reconnection on the same A, **the same job ID restored as succeeded, but result=null**. The [actual post-reload job](../../artifacts/unity-followup-20261003/s-a-job-after-explicit-reload.json) matches source behavior where `TestJobManager` stores metadata for only the latest 10 jobs in SessionState and omits detailed results. B retained the same PID/clean scene.

The source's default initialization gate is 15 seconds; it fails a job on timeout when there are still no total tests and compile/import is not in progress. This does not establish why initialization did not begin historically. Unknown job_id from `GetTestJob` means the selected Editor's manager lacks the ID; the exact earlier loss path remains undetermined. This result=null is not claimed as reproduction/resolution of Unknown job. Actual readiness → per-call target → explicit reconnection → external-result preservation before reload was reflected in [current AI instructions](../../skills/splinter/SKILL.md) and the [Unity procedure](../alpha-workspaces.md). Global HTTP is not disabled to disturb peers, and writes/tests are not automatically repeated on timeout alone.

After S B started, its log recorded two `File.Replace` IOExceptions during owned Splinter heartbeat publication. The [error stack/hash](../../artifacts/unity-followup-20261003/s-heartbeat-publication-error.json) is preserved; subsequent actual readiness/MCP tests/normal reclaim passed. The publication-error cause is undetermined; successful later observations do not erase the errors or qualify a new failure matrix.

<a id="물리-파일공유-vss실제-메모리"></a>
### Physical files, shared VSS, and actual memory

Files were counted only at ordinary physical roots using read-only Win32 `FileStandardInfo.AllocationSize`. Logical files in S mounts were not counted as copied physical storage. Processes were observed after verifying PID/start/actual child trees at nominal 2-second intervals; administrator read-only VSS/volume observations were at nominal 5-second intervals, with query cost included in actual intervals. The [final cost aggregation](../../artifacts/unity-followup-20261003/cost-summary-complete-samples.json) connects both raw VSS formats using actual volume identity. Only 3/2 memory samples fell in the C/S overlapping-test intervals; all values are sampled observations.

| Observation scope | C | S / Interpretation |
| --- | --- | --- |
| Per-task file-allocation snapshot | A 1,891,682,584 B + B 1,894,403,824 B = **3,786,086,408 B** | A 38,285,776 B + B 35,877,448 B, plus 7 actually loaded runner files 1,155,416 B shared once = **75,318,640 B**. About 98.01% less additional file allocation at that point |
| Cumulative journal immediately before termination | No separate journal | A 38,065,785 B/B 41,210,123 B. Includes additional writes during stop/collection, showing that the allocation snapshot above is not lifetime peak |
| Original/shared cost | Full L 2,090,943,536 B, Library 2,068,885,632 B | S reads the prepared L Library snapshot. Original/shared Git/VSS/installed .NET/MFT/directories/evidence backups excluded from per-task reduction ratio |
| A+B Editor-tree private median during test overlap | 9,864,130,560 B | 9,944,137,728 B; **Editor RAM reduction not confirmed**. Separate S providers 228,839,424 B |
| A+B Editor-tree private sampled maximum | 10,074,521,600 B | 11,637,444,608 B. C reimported an absent Library; S used a warm snapshot, so this is not a causal performance comparison |
| L+A+B+provider private sampled maximum | 16,732,868,608 B | 17,960,079,360 B. Maximum simultaneous tree total; distinct from instantaneous peak/full-system RAM |
| Full-system committed sampled maximum | 60,077,449,216 B | 62,206,164,992 B. Includes existing Editors/other tasks; per-task attribution unknown |
| C: shared VSS first→last used / sampled maximum allocated | 34,397,179,904→34,854,719,488 B / 35,212,394,496 B | Includes 6 existing snapshots. Not attributed to C worktrees/S or counted twice |
| T: shared VSS sampled maximum used / allocated / final | 3,063,808 B / 34,258,944 B / 0 B | After final S snapshot reclaim, both used/allocated 0; workspaceAttribution remains unknown |
| Volume free first→last | C: 268,480,024,576→268,025,610,240 B | T: 12,998,754,304→12,999,430,144 B. Global changes include evidence/shared resources/other tasks, not pure task cost |

The 98.01% above compares **the defined file-allocation snapshots**. It is not expanded into full physical peak/per-task VSS attribution/full RAM reduction. The initial measurement file that incorrectly classified installed .NET—the provider executable's directory—as the runner bundle is preserved. Scope was corrected using the Splinter DLL path in actual provider arguments and [7 file hashes](../../artifacts/unity-followup-20261003/s-loaded-runner-files.json). Other runner versions in the whole home were excluded from this cost. After S correction, initial AssetDatabase refresh was about 222 seconds for A/219 seconds for B; failed-start/reload/stop/collection/reclaim costs are included too. The absence of RAM reduction did not prompt implementation of unverified new Editor-internal optimization or automatic modes.

<a id="전체-회수와-원본기존-작업-보호"></a>
### Full reclaim and protection of the original and existing tasks

The app archive primary restriction belongs to that tool. The user had already approved cleanup of the exact comparison group. Automatic approval review rejected the first `git worktree remove --force` as deletion bypassing the primary restriction with a force option. Subsequently, original/current tracked raw files and required `.local` were independently hash-preserved; the index was refreshed after confirming that the normalized Git blob of baseline.txt matched HEAD while raw bytes remained unchanged. B's untracked `.local` was also moved for independent preservation. **`git worktree remove -- <exact path>` without a force option** passed. [Both checkouts/Git registrations removed](../../artifacts/unity-followup-20261003/c-checkouts-removed.json) and [0-byte app-name files preserved/empty parents removed](../../artifacts/unity-followup-20261003/c-empty-parent-cleanup.json) were verified. C cleanup did not use Splinter. No separate approval question was added.

The cost of C's first open helper waiting throughout Editor lifetime and then continuing to the next B open, reopening B, is preserved. The exact owned launcher was stopped, and clean B exited normally through `EditorApplication.Exit(0)`. Transport failure of the exit request was not treated as authorization to rerun; actual process/child absence determined the outcome. S's incorrect first input ended through user Quit; corrected final A/B used product normal stop/checkpoint/release. Raw cost files preserve external elapsed A stop 35,128ms/checkpoint 71,744ms/release 51,271ms and B stop 34,711ms/checkpoint 70,281ms/release 46,466ms. During A reclaim, B's same actual PID/clean scene was read; B normal stop was requested about 40 seconds after A release began. Earlier UP qualification of B writes after A completed is separate from this observation.

[Final native protection comparison](../../artifacts/unity-followup-20261003/final-native-protection.json) confirms S A/B phase=released, independent results present, mount/provider/delta/snapshot absent, handles/owned-application processes/working set/private bytes 0, and absence of all observed C/S/L child identities. [Final record allocation](../../artifacts/unity-followup-20261003/s-final-retained-allocation.json) is 497,960 B total for A/B plus shared reusable runner 1,155,416 B. Independent Git results/evidence/reusable cache/terminal receipts intentionally remain; they are not reported as resource leaks or file bytes 0.

Original L's previous branch/head/94 raw hashes were [restored](../../artifacts/unity-followup-20261003/l-source-restored-final.json), and canonical `development`/`6a54974` remained clean. Of 81 pre-existing refs, only the two C probe-result branches changed to approved new commits; the other 79 are unchanged. Existing Editors `68624`/`85184`/`52688` retain identical CIM creation times, and all 6 C: snapshots are retained. [MCP cleanup](../../artifacts/unity-followup-20261003/mcp-cleanup-verified.json) removes only discovery owned by closed test Editors and compares current hashes of the 7 baseline files preserved/restored in earlier verification. Shared HTTP remains enabled; existing servers were not stopped. Owned samplers exited normally through stop files.

Actual same-request reclaim retries for earlier C: Plastic A/B belong to [PC follow-up](plastic-compatibility.md#authorized-same-request-reclaim-retry-for-historical-c-tasks-2026-10-04). Current-format rejection and the unowned-snapshot isolation blocker verified using original-contract CLI are retained. This is not this S run's reclaim failure or a lack of user cleanup approval; it was not bypassed by deleting external tasks' snapshots.

The follow-up [required regression record](../../artifacts/unity-followup-20261003/required-build-validation.json) reports locked restore/Release (0 warnings/errors)/906 passed / 0 failed / 1 skip. The initial restricted-environment restore failure, permitted-environment success log, and frozen TRX are preserved together. Build Runtime hash and hashes of the 7 runner files loaded in the earlier actual S run are separate identities; the native matrix did not rerun with new binaries. Document UTF-8/diff/evidence links/actual counts/overlap/reclaim values were compared; product code/schema and HTML were not changed.
