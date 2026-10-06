# Unity 병렬 Editor의 실제 에셋·테스트·반영 검증

2026-10-03~04의 작은 Windows x64/NTFS·Git/Unity 입력에서 Splinter S군 A/B와 Codex 독립 worktree C군 A/B의 **MCP 대상 선택, 텍스처·씬 편집과 재로드, 현재 파일 테스트의 실패 격리, 전체·선택 경로 반영과 정리**를 `scoped-complete-with-limitations`로 마감했다. C에서는 Splinter를 전혀 사용하지 않았다. 승인한 후속에서 C/S의 실제 MCP 테스트 프레임 겹침과 reload 뒤 결과 보존/재연결, C의 checkout·Git 등록·부모 폴더 전체 제거까지 확인했다. 과거 실행의 순차 처리·초기 실패와 당시 정리 Partial은 아래 원래 근거로 남기며, 현재 판정은 마지막 후속 절을 따른다. 전체 물리 peak/VSS 작업별 귀속·Editor RAM 절감은 미확정이며, 과거 C: Plastic snapshot 보류는 별도 [PC 검증](plastic-compatibility.md)의 실제 blocker를 유지한다.

원시 근거는 [`artifacts/unity-parallel-validation-20261003/`](../../artifacts/unity-parallel-validation-20261003/)에 보존한다. 외부 MCP·Unity CLI가 편집·refresh·컴파일·테스트와 실제 값 검증을 수행했으며, Splinter는 workspace·소유 Editor·독립 결과·반영/회수 경계를 담당한다. [Unity 작업공간](../alpha-workspaces.md)과 [현재 지원 추적표](README.md)의 한정 자격을 유지한다.

## 고정 환경과 대상

| 항목 | 실제 실행 범위 |
| --- | --- |
| 원본/home | 기존 `T:\ua-source`, `source=git`, `application=unity`; 기존 등록 home `C:\Users\olive\Desktop\Development\splinter\.local\wl29`. 기존 grant 7개·서비스 유지, 새 설치/버전 추가 없음 |
| 기준 | A/B 모두 base/reference/revision `23896dfacba2d90d0c49f826444b178fdbe82d2b`; 한 요청의 key/id/digest와 반환 root를 유지 |
| 도구 | Unity `6000.3.22f1`, 시험 baseline의 기존 Pipeline `0.8.0-exp.1`, 기존 Coplay MCP `10.2.0`. 승인한 0.8 시험 baseline을 적용했으며 종료 뒤 기존 0.7 원본 구성으로 복원. Unity CLI는 대상의 현재 catalog/schema를 읽고 exact project path를 사용 |
| A | key `up-s-a-20261003`, id `d02a34b32ea4ca395677b89c`, digest `aed6eefc257aa8e49913d554`; 반환 root `C:\Users\olive\Desktop\Development\splinter\.local\wl29\workspaces\d02a34b32ea4ca395677b89c`; Editor PID `21200`, start ticks 문자열 `639266284202579734` |
| B | key `up-s-b-20261003`, id `cc032fded7ad2ee04b79be9b`, digest `835baab97c73401f945d2da8`; 반환 root `C:\Users\olive\Desktop\Development\splinter\.local\wl29\workspaces\cc032fded7ad2ee04b79be9b`; Editor PID `51944`, start ticks 문자열 `639266284355334240` |
| L | 원본 `T:\ua-source` Editor PID `14256`, start ticks 문자열 `639266281791419086`. A/B 실행 중 실제 project/PID를 따로 대조했고, 원본 반영 직전에 clean scene guard 뒤 승인된 정상 창 닫기로 종료 |
| 반영 검증 L2 | 같은 원본의 새 소유 Editor PID `25756`, start ticks 문자열 `639266314384909281`. A 전체/B 선택 반영 후 실제 loaded 값과 current-state 테스트를 검증하고 clean scene guard 뒤 정상 창 닫기 수행 |
| 승인 범위 | 실제 A/B 병렬 작업, 에셋·씬·테스트, 원본 전체/부분 반영과 완전 정리. 구형 종료 기록의 내용 보존 이동과 기존 A/B의 같은 id release 재시도는 사용자가 별도로 명시 승인 |

[`s-a-ensure.json`](../../artifacts/unity-parallel-validation-20261003/s-a-ensure.json)·[`s-b-ensure.json`](../../artifacts/unity-parallel-validation-20261003/s-b-ensure.json)의 반환 root를 파일 도구와 명령 working directory에 바인딩했다. `start`의 `Accepted`/`editor-starting`을 준비 완료로 읽지 않고 후속 catalog와 실제 eval의 project/PID를 확인했다. 큰 ticks는 숫자 반올림을 피하도록 문자열로 기록했다. 인증 정보·전체 프로세스 명령행은 문서에 싣지 않는다.

## UP 추적표

| ID / 확인 항목 | S군 현재 판정 | C군 비교 | 핵심 근거 |
| --- | --- | --- | --- |
| UP-01: MCP의 정확한 A/B routing | Passed with limitations. A/B 각 3회 exact routing·invalid 거부. 후속의 호출별 target과 실제 겹치는 테스트도 확인 | Passed with limitations. C도 6회 exact routing·invalid 거부와 후속 겹침 확인. 초기 포트 충돌과 reload 후 명시적 bridge 재연결 필요 | [`mcp-routing-concurrent.json`](../../artifacts/unity-parallel-validation-20261003/mcp-routing-concurrent.json), 아래 후속 |
| UP-02: 텍스처 편집·현재 파일/loaded 값 | Passed. 같은 상대 경로의 64×64 PNG를 A/B의 다른 색·import 설정으로 저장하고 raw PNG와 loaded texture를 대조 | Passed. S와 같은 PNG 바이트/색·importer 값, 서로 다른 C GUID·loaded 값 확인 | [`mcp-texture-edited-and-loaded.json`](../../artifacts/unity-parallel-validation-20261003/mcp-texture-edited-and-loaded.json), [`mcp-scenes-current-loaded-after-reconnect.json`](../../artifacts/unity-parallel-validation-20261003/mcp-scenes-current-loaded-after-reconnect.json) |
| UP-03: 씬·material 편집·저장·재로드 | Passed with limitations. A/B root·위치·Camera/Light·PNG material 연결을 확인하고 scale을 다시 편집·저장·reload. 첫 거부/부분 생성의 명시적 재개를 기록 | Passed. C 각 씬 생성·편집·저장·Single 재로드와 같은 위치/scale·URP Lit 참조 확인 | [`mcp-scene-create-owned-partial-resume.json`](../../artifacts/unity-parallel-validation-20261003/mcp-scene-create-owned-partial-resume.json), [`mcp-scene-edited-reload.json`](../../artifacts/unity-parallel-validation-20261003/mcp-scene-edited-reload.json), `s-a-scene-scale-actual.json`·`s-b-scene-scale-actual.json` |
| UP-04: 현재 파일 테스트·실패 격리·복원 | Passed with limitations. 최초 3/0·B marker 실패/복원과 당시 순차 처리 유지. 후속 MCP는 각 4/0, 실제 프레임 8,761ms 겹침 | Passed with limitations. 초기 job 실패/유실·명시적 재실행, CLI 실패 격리 유지. 후속 MCP는 각 4/0, 실제 프레임 7,869ms 겹침 | [`mcp-test-results-initial.json`](../../artifacts/unity-parallel-validation-20261003/mcp-test-results-initial.json), [`s-b-tests-current-state-restored.json`](../../artifacts/unity-parallel-validation-20261003/s-b-tests-current-state-restored.json), 아래 후속 |
| UP-05/06: 전체·부분 원본 반영과 완전 정리 | Passed with limitations. A 전체/B 선택 반영·원본 load/test·B 지속·기존 보호 거부/재시도 유지. 후속 S도 독립 결과와 자원 0 확인 | Passed with limitations. A 전체+B 선택 반영·원본 load/test·raw 보존에 이어 후속에서 두 checkout·Git 등록·부모 폴더 전체 제거 | [`s-a-integrated-all-paths-verified.json`](../../artifacts/unity-parallel-validation-20261003/s-a-integrated-all-paths-verified.json), [`s-b-selected-paths-applied-verified.json`](../../artifacts/unity-parallel-validation-20261003/s-b-selected-paths-applied-verified.json), 아래 후속 |

`Passed`는 이 고정 입력에서 실제로 확인한 경계다. S군 다섯 항목은 한정 마감했으며 다른 도구/프로젝트/버전의 일반 지원 선언은 아니다.

## MCP 대상 선택과 병렬의 관측 범위

MCP instance는 원본 L과 A/B를 함께 발견한 뒤 각 요청에 명시적으로 선택했다. `mcp-routing-concurrent.json`의 A/B × 3회 nonce read는 실제 root와 PID `21200`/`51944`를 반환했다. 잘못된 `uv-invalid@deadbeef`는 instance-not-found로 거부됐고 원본 L로 대체 실행되지 않았다. 동일한 baseline assembly MVID가 나온 것은 같은 기준 코드를 로드했다는 근거이며, Editor identity가 같다는 뜻은 아니다.

MCP 요청을 겹쳐 제출한 사실과 Editor 작업의 동시 처리를 구별한다. 최초 MCP EditMode runner는 A job 종료 `1791033063840`ms 뒤 B job 시작 `1791033064442`ms로 **순차 처리**됐다. 이 실행으로 MCP test runner의 동시 수행을 주장하지 않는다. 반면 native Unity CLI의 exact-path A/B negative test 호출 창은 아래와 같이 약 2.448초 겹쳤다. 이는 두 CLI 호출의 겹침 관측이며, 개별 테스트 프레임이 같은 순간 실행됐다는 계측은 아니다.

| 호출 | 시작 / 종료 (KST) | 외부 경과 / 결과 |
| --- | --- | --- |
| A CLI negative 동반 실행 | `22:13:17.0661571` → `22:13:20.6515068` | 3,577ms, CLI exit 0, 테스트 3 passed / 0 failed |
| B CLI negative 실행 | `22:13:18.2034894` → `22:13:21.7889601` | 3,577ms, CLI exit 0, 테스트 2 passed / 1 failed |

시간과 일반 권한 여부는 [`s-a-tests-parallel-negative-cost.json`](../../artifacts/unity-parallel-validation-20261003/s-a-tests-parallel-negative-cost.json)·[`s-b-tests-parallel-negative-cost.json`](../../artifacts/unity-parallel-validation-20261003/s-b-tests-parallel-negative-cost.json)의 원시 기록을 따른다. CLI 명령의 transport 성공/exit 0을 의도한 실패 시험의 테스트 성공으로 읽지 않는다.

## 실제 텍스처와 씬의 분리

두 workspace의 입력 경로는 `Assets/ParallelQualification/Shared.png`, `Shared.mat`, `Shared.unity`, `Marker.txt`로 같다. 값과 새 asset의 GUID는 각 작업이 소유한다. 텍스처는 readable=true, compression=Uncompressed이며 raw PNG의 크기·픽셀과 AssetDatabase의 loaded 값, meta GUID 연결을 함께 확인했다.

최초 `manage_texture(create)`에 전달한 importer 설정은 응답에서 `importSettings={}`였고 실제 `isReadable=false`였다. 이 요청을 설정 성공으로 세지 않았다. 같은 정확한 MCP 대상에서 `execute_code`로 TextureImporter를 명시적으로 수정한 후 `isReadable=true`와 실제 픽셀을 확인했다. 인자 소실의 내부 원인은 확정하지 않았으며, 도구 하나의 초기 동작과 수정 후 실제 Unity 상태를 구분한다. 근거는 [`mcp-texture-create.json`](../../artifacts/unity-parallel-validation-20261003/mcp-texture-create.json)과 `mcp-texture-edited-and-loaded.json`이다.

| 현재 값 | A | B |
| --- | --- | --- |
| PNG 크기 / RGBA | 64×64 / `(255,64,32,255)` | 64×64 / `(32,64,255,255)` |
| filter / wrap | Point / Clamp | Bilinear / Repeat |
| PNG SHA-256 | `c22f129db1e087779d4ff0dad52bb85bc0b81c0093a8b497607e3f7a71f4dea4` | `5c0843d32867de66a507074c0d9ea2f281268c720df82112750eb754e6069da8` |
| 씬 root / 위치 | `UP_A` / `(1,2,3)` | `UP_B` / `(4,5,6)` |
| 추가 편집 후 reload한 root scale | `(2,1,1)` | `(1,3,1)` |
| marker | `A-v1` | `B-v1` |

씬에는 Camera와 Light가 있고, root의 MeshRenderer material은 해당 workspace의 `Shared.png`를 mainTexture로 참조한다. 저장된 씬을 Single로 재열고 loaded root·위치·texture 연결을 확인했다. 이후 scale 편집의 실제 재로드 값은 [`s-a-scene-scale-actual.json`](../../artifacts/unity-parallel-validation-20261003/s-a-scene-scale-actual.json)·[`s-b-scene-scale-actual.json`](../../artifacts/unity-parallel-validation-20261003/s-b-scene-scale-actual.json)에 있다. 에셋 상태 관측은 최종 화면 렌더/Play/build 성공을 의미하지 않는다.

## 현재 파일을 읽는 세 테스트

[`write-current-state-tests.ps1`](../../artifacts/unity-parallel-validation-20261003/write-current-state-tests.ps1)은 현재 ensure의 exact root/key/id/base를 확인한 뒤 각 작업에 같은 Editor-only `UP.CurrentState.Tests` assembly와 같은 테스트 class를 생성한다. expected 상수는 A/B마다 다르다. 테스트는 marker 파일, 저장된 씬의 root·material 연결, 텍스처의 크기·픽셀·import 설정의 세 항목이다. 매 테스트 전에 현재 파일을 읽고 project/PID/start·marker·test assembly MVID·파일 hash를 출력한다.

외부 recompile은 A/B 모두 `completed`, `failed=false`, `compilationFailed=false`로 관측했다. 최초 MCP 테스트는 A/B 각각 3 passed / 0 failed였다. B의 marker 파일만 `B-v2`로 변경하고 테스트 소스는 유지한 채 CLI A/B를 실행했을 때 B marker test만 `Expected: B-v1 / But was: B-v2`로 실패했고, B의 scene/texture test와 A의 세 test는 통과했다. B 파일을 `B-v1`로 복원한 후 3 passed / 0 failed로 돌아왔다. 테스트 코드 재컴파일에 기대지 않고 현재 파일 변화가 관문에 반영됨을 확인한 의도한 실패 시험이다.

| identity | A | B |
| --- | --- | --- |
| test assembly MVID | `a686f47c-bdb3-404f-a9cf-98b5f810ba4d` | `aa76aeb6-491c-4596-8f11-b28afc950ce0` |
| 최종 실제 marker | `A-v1` | `B-v1` |
| 최종 실제 PID | `21200` | `51944` |

[`s-b-marker-negative-state.json`](../../artifacts/unity-parallel-validation-20261003/s-b-marker-negative-state.json)·[`s-b-marker-restored.json`](../../artifacts/unity-parallel-validation-20261003/s-b-marker-restored.json)은 두 변경에서 `testSourceChanged=false`와 marker hash를 기록한다. 마지막 actual identity는 [`s-a-tests-source-identity-final.json`](../../artifacts/unity-parallel-validation-20261003/s-a-tests-source-identity-final.json)·[`s-b-tests-source-identity-final.json`](../../artifacts/unity-parallel-validation-20261003/s-b-tests-source-identity-final.json)으로 대조했다. 게임 전체의 정합성이나 Play/빌드는 `NotRun`이다.

## 전체·부분 반영과 실제 원본 검증

A의 안정된 독립 결과 `c95c6870495bd907553ae9c856fcdf46aa2451a5`를 제품 `integrate`로 원본의 명시적 시험 branch `codex/up-source-20261003`에 전체 반영했다. 원본 HEAD는 `5123d9d8c874af2e0bb213c408bfd0f1bd27fdcb`이며, 원본의 14개 source/meta 파일 각각을 A 결과의 blob/SHA-256과 대조했다. [`s-a-full-integrate.json`](../../artifacts/unity-parallel-validation-20261003/s-a-full-integrate.json)은 Succeeded / integrated, [`s-a-integrated-all-paths-verified.json`](../../artifacts/unity-parallel-validation-20261003/s-a-integrated-all-paths-verified.json)은 14파일 일치와 다른 기준 파일 변경 없음의 근거다.

A release는 Succeeded / released이며 mount/provider/delta/snapshot 부재, openHandles·provider working/private bytes와 released application group working/private bytes 0을 관측했다. 그 동안 B는 같은 PID `51944`에서 현재 marker/texture/씬을 읽었다. A release 뒤 B의 새 `SelectedB.png`를 `(48,240,80,255)`로 다시 편집하고 현재 테스트 3 passed / 0 failed를 확인했다. [`s-b-live-during-a-release.json`](../../artifacts/unity-parallel-validation-20261003/s-b-live-during-a-release.json), [`s-b-new-resource-write-after-a-release.json`](../../artifacts/unity-parallel-validation-20261003/s-b-new-resource-write-after-a-release.json), [`s-b-tests-after-a-release.json`](../../artifacts/unity-parallel-validation-20261003/s-b-tests-after-a-release.json)을 따른다.

B checkpoint는 [`s-b-checkpoint-required-message.json`](../../artifacts/unity-parallel-validation-20261003/s-b-checkpoint-required-message.json)에서 Succeeded / collected, stable-journal-v1 독립 결과 `03b5c6271900b004a062dbafb686f4a24a557f6a`를 반환했다. 이번 검증에서 명시적으로 선택한 `SelectedB.png`와 `.meta`만 AI/Git의 exact path restore·commit 절차로 반영했다. **제품의 partial integrate API 자격은 아니다.** 원본 HEAD `091b0b0e377563f8a19264181546c6913b72a543`에서 선택 두 파일은 B blob/SHA와 같고 A의 14파일 hash는 유지됐다. 제외한 B의 14파일은 독립 결과에 보존된다. 선택/제외 목록과 실제 검증은 [`s-b-partial-apply-plan.json`](../../artifacts/unity-parallel-validation-20261003/s-b-partial-apply-plan.json)·[`s-b-selected-paths-applied-verified.json`](../../artifacts/unity-parallel-validation-20261003/s-b-selected-paths-applied-verified.json)에 있다.

B의 첫 release는 `DeltaChangedAfterCollection`로 거부됐다. [`s-b-checkpoint-stable-after-release-refusal.json`](../../artifacts/unity-parallel-validation-20261003/s-b-checkpoint-stable-after-release-refusal.json)은 이미 collected인 결과와 이전 seal(sequence 39,232 / journal 55,618,778 bytes)을 반환했다. **새 수집 성공으로 기록하지 않는다.** [Runtime checkpoint](../../src/Splinter.Runtime/WorkspaceCoordinator.cs)는 현재 소스 revision과 독립 결과가 같고 변경이 없으면 기존 view를 반환하며, 결과가 이미 있는 이 경로는 새 collection을 하지 않는다.

같은 작업의 명시적 release 재시도는 [현재 release 구현](../../src/Splinter.Runtime/SnapshotWorkspaces.cs)의 infrastructure 제거 뒤 collection과 stable/unmount seal 대조를 수행해 Succeeded / released로 끝났다. [`s-b-release-explicit-quiescent-retry.json`](../../artifacts/unity-parallel-validation-20261003/s-b-release-explicit-quiescent-retry.json)의 최종 seal은 sequence 39,234 / journal 55,619,588 bytes / SHA-256 `5f7fd72800e5c443087582804066796b8a4ffefffcd34bee87aa447660ea49f2`다. snapshotAvailability=missing, mount/provider/delta 부재, openHandles·provider와 application group working/private bytes 0을 관측했다. 첫 독립 revision `03b5c6271900b004a062dbafb686f4a24a557f6a`는 추가 `refs/splinter/qualification-results/up-b-first-collected-20261003`에도 보존했다.

반영된 원본을 L2에서 실제로 열어 `UP_A` 위치 `(1,2,3)` / scale `(2,1,1)`, A의 Shared PNG와 GUID, B의 선택 PNG `(48,240,80,255)`와 GUID, marker `A-v1`을 확인했다. compiling/updating/failed=false이고 current-state EditMode test는 3 passed / 0 failed였다. [`l2-integrated-assets-scene-actual.json`](../../artifacts/unity-parallel-validation-20261003/l2-integrated-assets-scene-actual.json)·[`l2-integrated-current-state-tests.json`](../../artifacts/unity-parallel-validation-20261003/l2-integrated-current-state-tests.json)의 actual project는 `T:\ua-source`, PID는 `25756`이다. Unity의 실제 import가 생성한 `Marker.txt.meta`를 hash와 함께 별도 기록하고 시험 branch HEAD `106dbf97cf6b82ea164a2bb1a97c058c78532f3a`에 보존했다. [`l2-final-trial-source-preserved.json`](../../artifacts/unity-parallel-validation-20261003/l2-final-trial-source-preserved.json)을 따른다.

## 준비 단계의 보존과 같은 작업 회수

현재 Core는 이전 형식의 종료 기록을 자동 이관하지 않는다. 기존 home의 구형 종료 기록 73개(72 released / 1 discarded)는 workspace/delta가 없고 reclamation이 released인 상태였으며, 전체 기록 열거에서 `StateCorrupt`를 발생시켰다. 사용자 명시 승인 뒤 **내용을 그대로** `.local/wl29-archive-20261003-uv`로 보존 이동했다. 2,130개 파일의 전후 hash 검증과 72개 독립 결과 refs 대조를 보존했으며, 구형 상태를 현재 상태로 변환하거나 독립 결과를 삭제하지 않았다.

근거는 [`terminal-record-archive-plan.json`](../../artifacts/unity-parallel-validation-20261003/terminal-record-archive-plan.json), [`archive-result-refs.json`](../../artifacts/unity-parallel-validation-20261003/archive-result-refs.json), [`archive-executed.json`](../../artifacts/unity-parallel-validation-20261003/archive-executed.json)이다. archive 결과는 `count=73`, `files=2130`, `verified=true`다. 기존 grant 7개와 서비스 패키지를 그대로 사용했다.

그 다음 앞선 비교의 **같은** A id `40cd86f88ef543d7a8418a6e` / B id `fd4c3247d6b0d38e6c7b5fba`에 승인된 release 재시도를 수행했다. 두 응답은 Succeeded / released이고 `snapshotAvailability=missing`, mounted/providerAlive/deltaPresent=false, reclamation disposition=released를 관측했다. [`old-a-explicit-release.json`](../../artifacts/unity-parallel-validation-20261003/old-a-explicit-release.json)·[`old-b-explicit-release.json`](../../artifacts/unity-parallel-validation-20261003/old-b-explicit-release.json)의 독립 결과 revision은 각각 `106d51e155e28b3cbd11ddb84fb9607d233455f8` / `8374d1b5dea8dd1e853b64bb9a2c63802dbca9ac`로 보존됐다. 다른 snapshot 삭제·새 grant·구버전 자동 복구로 우회한 결과가 아니다.

## 실패·재연결·반영 전 경계

| 관측 | 처리와 보존한 제한 |
| --- | --- |
| 첫 A catalog에 No Pipeline instance | descriptor 준비 전 응답을 보존하고 같은 반환 root의 준비 후 catalog를 다시 확인. Editor 부재로 판정하지 않음 |
| CLI bridge 시작/상태 읽기의 첫 5,000ms main-thread timeout | 실패 JSON과 후속 actual bridge 상태를 보존. 쓰기 응답 유실을 성공/실패로 추정하거나 타임아웃만으로 쓰기를 자동 반복하지 않음 |
| scene 생성의 첫 MCP blocked-pattern 거부 | 실행 전 거부 응답을 보존하고 코드를 정정. 다음 additive scene 생성은 unsaved untitled scene 때문에 실패했고 이미 생성한 owned material을 확인한 뒤 clean untitled scene의 명시적 대체로 재개 |
| reload 뒤 MCP instance 누락 / bridge=false | 정확한 CLI project/PID에서 실제 상태를 읽고 해당 작업의 bridge를 다시 연결. 새 discovery로 현재 instance를 선택; L/default resource로 fallback하거나 포트를 영구 고정하지 않음 |
| 첫 A checkpoint의 `DeltaChangedAfterCollection` | writes overlap과 seal 실패를 보존. 안정된 소스의 명시적 recollect가 Succeeded / collected, 독립 revision `c95c6870495bd907553ae9c856fcdf46aa2451a5`를 반환. 첫 시도 통과로 바꾸지 않음 |
| B bridge stop eval의 namespace 오류 | eval 코드 컴파일에서 존재하지 않는 namespace를 거부하여 stop 실행 전 실패. 현재 실제 API namespace를 확인한 수정 요청과 실패 JSON을 보존; Unity 프로젝트 소스 컴파일 실패로 확대하지 않음 |
| B checkpoint의 누락 `--message` | `s-b-checkpoint-final.json`의 InvalidArguments 입력 거부를 유지. 현재 schema의 필수 message를 넣은 별도 명시적 요청 `s-b-checkpoint-required-message.json`에서 collected 성공 |
| B 선택 경로 stage guard의 PowerShell precedence 오류 | selected restore 뒤 비교식이 잘못되어 guard 실패. 실제 staged 두 경로를 읽어 expected 두 경로와 일치함을 확인하고 restore를 반복하지 않음. `s-b-partial-staged-guard-observed.json` 보존 |
| B 첫 release의 `DeltaChangedAfterCollection` | 첫 거부와 기존 결과를 반환한 checkpoint를 보존. 같은 id의 명시적 release 재시도에서 최종 39,234 seal 일치와 회수 성공을 확인; checkpoint 재수집 성공으로 표시하지 않음 |
| 원본 L 정상 종료의 증거 저장 typo | exact project/PID/start 및 clean scene(`dirty=false`) guard 후 `CloseMainWindow`를 한 번 호출. 호출 후 PowerShell false literal typo로 evidence 저장이 실패했으나 관측 파일의 processPresent=false로 PID 14256 종료를 확인. 재호출·강제 종료 없음 |

씬 실패의 원문은 [`mcp-scene-create-save-reopen.json`](../../artifacts/unity-parallel-validation-20261003/mcp-scene-create-save-reopen.json)·[`mcp-scene-create-save-reopen-corrected.json`](../../artifacts/unity-parallel-validation-20261003/mcp-scene-create-save-reopen-corrected.json), 재연결 후 실제 파일/loaded 값은 `mcp-scenes-current-loaded-after-reconnect.json`에 있다. A checkpoint 중 B는 같은 PID `51944`에서 `B-v1`, pixel `2040FFFF`, 해당 Shared 씬을 유지했다. L 종료 경계는 [`l-clean-scene-before-owned-close.json`](../../artifacts/unity-parallel-validation-20261003/l-clean-scene-before-owned-close.json)·[`l-owned-window-close-observed.json`](../../artifacts/unity-parallel-validation-20261003/l-owned-window-close-observed.json)을 따른다.

## 최종 정리·복원과 자격의 한계

[`final-native-cleanup-and-restoration.json`](../../artifacts/unity-parallel-validation-20261003/final-native-cleanup-and-restoration.json)의 `2026-10-03T22:41:45.3333474+09:00` 관측에서 아래 종료 경계를 확인했다. 독립 결과·terminal receipts·공유 runner cache·원시 근거는 의도적으로 보존한다. 완전 정리는 소유 실행 자원과 임시 상태의 회수이며 보존 결과의 삭제를 뜻하지 않는다.

| 최종 대조 | 실제 결과 |
| --- | --- |
| 원본 | `T:\ua-source`의 기존 branch `codex/library-20260929`, HEAD `f71e4e244ba9b085aa0fc439336e4c60ee3d5d5d`, clean. 최초 94개 tracked 파일의 모든 hash 일치, 시험 `ParallelQualification` assets 부재 |
| 시험 반영 결과 | `codex/up-source-20261003` HEAD `106dbf97cf6b82ea164a2bb1a97c058c78532f3a` 유지. A 전체·B 선택 반영과 실제 로컬 import 결과 보존 |
| canonical fixture / 기존 Editor | `splinter-unity`의 development HEAD `6a54974a9c655031aeb2be17fb7622fc5d641c52` clean. 보호한 Editor PID `68624`, start ticks `639265412574256360` 동일 identity 생존 |
| 이번 A/B 실행 자원 | workspace·delta·Git registration·소유 provider/Editor PID 부재, snapshotAvailability=missing. provider handles/working/private bytes와 application group activeProcesses/working/private bytes 모두 0 |
| 원본 검증 Editor | L `14256`과 L2 `25756`, 직접 자식 모두 부재. 정상 창 닫기만 사용, 강제 Editor 종료 없음 |
| 보존 기록 | archive 73 records / 2,130 files 모든 hash 동일, 기존 독립 refs 72개 불변. 이번 A/B 독립 result refs도 보존 |
| snapshot 보호 | 이번 소유 snapshot 회수만 수행. nonOwnedSnapshotDeleteRequests=0. 다른 볼륨의 기존 보류를 삭제하거나 우회하지 않음 |

[`mcp-owned-discovery-cleanup-verified.json`](../../artifacts/unity-parallel-validation-20261003/mcp-owned-discovery-cleanup-verified.json)에서 닫힌 A/B의 소유 discovery 파일 두 개만 제거하고 legacy discovery 파일을 최초 bytes로 복원했다. 기존 discovery 파일 7개의 hash가 그대로이며 owned port files는 남지 않았다. [`l2-mcp-preferences-owned-restore.json`](../../artifacts/unity-parallel-validation-20261003/l2-mcp-preferences-owned-restore.json)은 port=6400, HTTP=true, resume key 부재의 복원 값을 실제 원본 project/PID에서 반환한다. MCP 전역 파일/다른 작업을 일괄 정리하지 않았다.

최종 MCP의 실제 `mcpforunity://instances` 읽기도 `instance_count=0`, `instances=[]`였다. [`final-mcp-discovery.json`](../../artifacts/unity-parallel-validation-20261003/final-mcp-discovery.json)에 보존하며, 보호한 별도 HTTP Editor의 부재를 주장하는 근거로 사용하지 않는다.

아래 C 후속은 다섯 기능의 실제 비교를 추가하며 C/S 성능 우열을 새로 판정하지 않는다. 두 실행은 작은 고정 Git/Unity 에셋 입력의 절차 검증이다. 대형 프로젝트·새 패키지/Editor 버전·MCP runner 동시성·Play/build·다른 VCS·ARM/OS 장애·전체 물리/RAM peak는 자격 범위 밖이다. 공유 VSS의 작업별 귀속과 전체 peak는 `unknown`이며, 이전 C: 회수 보류도 이번 T: 성공으로 해소했다고 읽지 않는다.

## Codex 독립 worktree의 실제 후속

사용자의 남은 검증 실행 요청으로 기존 앱 생성 C A/B를 한 번 재사용했다. 일반 Git·Unity CLI·Coplay MCP·OS 관측만 사용했고 Splinter 명령/provider/service/mount/Editor helper·Coordination은 사용하지 않았다. 동일 base `23896dfacba2d90d0c49f826444b178fdbe82d2b`, Unity/Pipeline/Coplay/Test Framework 버전과 S의 작은 PNG·씬·테스트 입력을 유지했다. 원시 증거는 [`artifacts/unity-codex-parallel-validation-20261003/`](../../artifacts/unity-codex-parallel-validation-20261003/)에 있다. 기존 HTML은 C 후속 전 S 실행의 기록으로 유지하고 이번 후속 결과는 이 문서와 대화의 텍스트에만 추가한다.

| 대상 | 실제 project / Editor identity / MCP |
| --- | --- |
| C A | `C:\Users\olive\.codex\worktrees\1a8c\splinter-unity`; PID `91988`, .NET start ticks `639266331161540380`; `splinter-unity@bb813d67`, stdio 6401 / Pipeline 7802 |
| C B | `C:\Users\olive\.codex\worktrees\90dd\splinter-unity`; PID `29848`, .NET start ticks `639266331164797634`; `splinter-unity@71652d88`, stdio 6402 / Pipeline 7803 |
| C L / 반영 L2 | `T:\ua-source`; PID `80412` / `55012`, .NET ticks `639266331168278078` / `639266340563469396`. L MCP `ua-source@a28d347c`, stdio 6400 / Pipeline 7801 |

각 A/B 초기 cold Library 임포트의 Unity 로그는 198.035/197.805초다. 이는 이번 초기 Refresh 로그의 시간이며 전체 준비/CLI open 비용이나 S warm Library와 공정한 임포트 속도 비교로 환산하지 않는다. `open`의 외부 경과는 Editor 종료까지 유지된 파이프 수명도 포함하므로 startup 시간으로 사용하지 않는다.

[`mcp-routing-pairs.json`](../../artifacts/unity-codex-parallel-validation-20261003/mcp-routing-pairs.json)은 A/B 각 3회 실제 project/PID/start/nonce를 반환한다. `cv-invalid@deadbeef`는 거부되고 기본 선택은 L에 유지됐다. 초기 `Start()`는 port 6400 충돌 때문에 bridge=false를 반환했고, 읽기 관측에서 설치된 bridge의 지연 fallback 이후 각각 6401/6402 running을 확인했다. 시작 쓰기를 반복하지 않았다. 컴파일/reload 뒤에는 bridge=false를 확인한 다음 해당 소유 bridge만 명시적으로 재연결했다. 전역 HTTP=true는 다른 Editor를 위해 유지했다.

[`mcp-current-assets-scene.json`](../../artifacts/unity-codex-parallel-validation-20261003/mcp-current-assets-scene.json)의 C PNG SHA-256은 위 S A/B의 PNG와 각각 동일하다. 색·64×64·readable/Uncompressed·filter/wrap·씬 root/위치/scale도 같고, C GUID는 A `182d9603478f0644484122a1898b1cd1`, B `fc095651f139dcf4a995bfe85fb3ec57`로 서로 다르다. 씬 생성·저장·재열기 이후 별도 scale 편집·저장·Single 재열기로 loaded 값과 URP Lit material의 texture 참조를 확인했다. 첫 S 실행에서 발견한 importer/clean Untitled 제한을 반영한 명시적 Unity API 절차를 사용했으며 도구별 최초 시도 비용의 동등성을 주장하지 않는다.

세 NUnit 테스트 본문/asmdef는 S와 동일하다. 실제 C assembly MVID는 A `d8684be1-31c5-4f3d-b617-0d4b201d7ded`, B `69fc36cb-0a2a-4413-bfee-e37c141b7c84`다. 최초 MCP job 생성 후 reload로 bridge가 끊겼다. 재연결 후 A job은 초기화 시간 초과 실패, B job은 `Unknown job_id`였고 B SessionState에는 job 기록이 없었다. [`초기 결과 조회`](../../artifacts/unity-codex-parallel-validation-20261003/mcp-tests-results-after-reconnect.json)와 [`재실행 전 실제 상태`](../../artifacts/unity-codex-parallel-validation-20261003/mcp-stable-test-retry-pre-observed.json)를 보존한다. 초기화/유실의 내부 원인은 미확정이며 CLI의 통과로 해당 실패를 대체하지 않는다. 실패와 idle 상태를 관측한 뒤 init_timeout=60000으로 같은 테스트를 명시적으로 한 번 재실행해 [`mcp-stable-tests-retry-results.json`](../../artifacts/unity-codex-parallel-validation-20261003/mcp-stable-tests-retry-results.json)의 각 3/0과 **각 테스트 출력의 실제 C project/PID/start/MVID·파일 hash**를 대조했다. MCP runner의 A 종료 `1791037007673` 뒤 B 시작 `1791037008144`로 순차 처리됐으므로 MCP 테스트 동시 수행 자격은 주장하지 않는다.

독립 native CLI는 초기 A/B 각각 3/0, B marker 파일만 `B-v2`로 변경한 뒤 A 3/0·B 2/1, `B-v1` 복원 뒤 B 3/0이었다. 테스트 소스/MVID를 바꾸지 않고 현재 파일의 실패를 관측했다. [`a-cli-negative-peer-tests-cost.json`](../../artifacts/unity-codex-parallel-validation-20261003/a-cli-negative-peer-tests-cost.json)과 [`b-cli-negative-peer-tests-cost.json`](../../artifacts/unity-codex-parallel-validation-20261003/b-cli-negative-peer-tests-cost.json)의 호출 창은 각각 UTC `14:17:22.5992175`→`14:17:25.9169903`, `14:17:20.7150020`→`14:17:23.9365592`로 약 **1.337초 겹쳤다**. 실제 개별 테스트 프레임의 동시성을 계측한 것은 아니다. B 실패에도 CLI exit는 0이므로 실제 Summary/Results를 판정에 사용했다. Unity 로그의 기본 `TestResults.xml` 경로 대신 각 작업별 MCP/CLI 반환 JSON을 분리해 보존했다.

| 변경 보존 / 반영 | 실제 결과 |
| --- | --- |
| C A 전체 | `codex/uc-up-a-20261003` → `33e3b2d07a7c0e0f869a947b2dfd4f919e696b05`; 새 15경로만 commit. S의 14경로에 비해 Marker.txt.meta를 A에서 이미 생성해 포함 |
| C B 독립 결과 | `codex/uc-up-b-20261003` → `2821b2e1e14593cce5df68820b7b4df00ac905a6`; 새 17경로만 commit. A 정리 후 같은 B PID에서 SelectedB 픽셀을 `(48,240,80,255)`로 수정하고 테스트 3/0 |
| 시험 원본 | `codex/uc-up-source-20261003` → `5343f8324749b4c7551892bfead9240276e1864a`; 일반 Git으로 A 전체 fast-forward 후 B SelectedB.png/meta 두 경로만 반영. 기존 원본 109파일 hash 불변, 선택 2파일 source/target hash 일치 |
| 실제 로컬 load/test | L2의 A root·scale·PNG/scene hash·GUID와 선택한 B 32×32 픽셀/GUID 확인. 현재 상태 테스트 3/0, 새 로컬 test MVID `0a379901-0c63-47eb-a7b5-3fa41344aa3c` |

반영 근거는 [`b-selected-local-applied.json`](../../artifacts/unity-codex-parallel-validation-20261003/b-selected-local-applied.json), [`l2-integrated-assets-actual.json`](../../artifacts/unity-codex-parallel-validation-20261003/l2-integrated-assets-actual.json), [`l2-integrated-current-tests.json`](../../artifacts/unity-codex-parallel-validation-20261003/l2-integrated-current-tests.json)이다. baseline.txt의 기존 미커밋 raw 바이트는 stage·복원·삭제하지 않았다. 복원 명령은 자동 승인 검토가 변경 유실 위험으로 거절했고, 새 검증 경로만 다루는 방식으로 계속했다. Git diff의 의미상 차이는 없었지만 상태의 `M`과 기존 바이트를 그대로 보존했다.

L/A/B/L2는 모두 clean guard와 정확한 PID/start 대조 후 `CloseMainWindow`로 정상 종료했다. 강제 종료는 없었으며 각 소유 프로세스와 직접 자식 부재를 확인했다. 공식 Unity CLI의 cache 정리는 A 1,833,629,250 B / B 1,833,731,146 B의 Library/Logs **논리 바이트**를 보고했고 두 Library 부재를 확인했다. 새 물리 할당/RAM 절감 실측으로 해석하지 않는다. 기존 앱 primary checkout archive 거부 제한을 강제 삭제로 우회하거나 다시 같은 이유로 재시도하지 않았으므로 전체 C checkout 회수는 Partial이다.

[`source-and-old-worktree-restored.json`](../../artifacts/unity-codex-parallel-validation-20261003/source-and-old-worktree-restored.json)에서 기존 C A/B branch/HEAD `40816fb`/`cb1e869`와 각 94개 tracked raw hash, T 원본 `f71e4e2`와 94개 raw hash를 모두 복원/대조했다. 기존 C baseline.txt의 상태도 보존했고 새 시험 branch/commit은 유지했다. [`protected-independent-results-unchanged.json`](../../artifacts/unity-codex-parallel-validation-20261003/protected-independent-results-unchanged.json)의 이전 독립 refs 72개도 불변이다. Canonical development `6a54974` clean과 보호 Editor PID68624/CIM ticks `639265412574256360` 생존을 확인했다.

[`mcp-owned-cleanup-verified.json`](../../artifacts/unity-codex-parallel-validation-20261003/mcp-owned-cleanup-verified.json)은 정확한 닫힌 C A/B discovery 두 파일만 제거하고 legacy 파일의 최초 bytes를 복원해 기존 7파일의 hash 일치를 확인했다. L2에서 변경 소유 guard 후 port=6400, HTTP=true, resume key 부재를 실제 확인했다. 최종 stdio instances는 0이며 별도 HTTP Editor의 부재로 해석하지 않는다. 이번 C 기능 검증으로 앞선 저장·메모리 비교의 전체 physical/RAM peak unknown이나 다른 지원 미실행 항목을 바꾸지 않는다.

## 문서와 현재 계약의 회귀 확인

같은 제품 후보 `52d9099b2df3bdd786defbabe8e0b04d3ea0cdf3`에서 필수 `pwsh -NoProfile -File scripts/build.ps1`를 실행해 locked restore·Release 빌드(오류/경고 0)·906 passed / 0 failed / 1 skip을 확인했다. [`required-build-validation.json`](../../artifacts/unity-parallel-validation-20261003/required-build-validation.json)과 고정한 TRX를 보존한다. 제품 코드/schema 변경은 없으며 이 자동 회귀를 추가 Unity·VSS 지원 행렬의 실행으로 해석하지 않는다. 최종 UTF-8·HTML 링크와 실제 브라우저 표시 대조는 `report-validation.json`을 따른다.

필수 빌드는 위 실제 Unity 실행 이후에 수행했다. 이때 나온 Runtime SHA-256 `aea683d3004e1c6224548aeb6b0a8f09aebaa9217b1b15103b9d72af9c21c5b7`은 빌드 검증의 별도 산출물 identity이며, 앞선 실행 파일과 바이너리가 같거나 이 새 산출물로 Native 행렬을 다시 실행했다는 뜻이 아니다.

C 후속 마감 뒤 문서 커밋 `0529a0e87f48f1f9348ce6fb88d93af227be2d06`를 기준으로 필수 빌드를 다시 실행했다. locked restore·Release(경고/오류 0)·906 passed / 0 failed / 1 skip을 [`C 후속의 별도 제품 회귀 기록`](../../artifacts/unity-codex-parallel-validation-20261003/required-build-validation.json)과 고정 TRX에 보존했다. Runtime SHA-256은 `e15a295cfc20a75d66c8651f20824d8f8c2134549ecf0b12df7c44365b6b72e3`이며 이 바이너리로 S native 검증을 재실행하거나 C에서 Splinter를 사용한 것은 아니다. 후속 문서는 UTF-8·diff·근거 링크와 실제 결과 값을 대조했고 HTML은 변경하지 않았다.

## 승인한 연결·비용·전체 정리 후속 (2026-10-04)

사용자가 지정한 세 미완료 영역을 [고정 범위](../plans/unity-parallel-editor-validation.md#승인한-연결비용정리-후속)로 실행했다. 제품 소스 기준은 `e61270b`, 도구는 Unity `6000.3.22f1`·Pipeline `0.8.0-exp.1`·Coplay `10.2.0`·Unity CLI `1.0.0-beta.5`다. C는 기존 Codex worktree 두 곳과 일반 Git/MCP/CLI/OS 계측만 사용했다. S는 같은 작업 입력을 별도 snapshot/private journal에 연결했으며 제품 코드·설치·버전을 변경하지 않았다. 증거는 [`unity-followup-20261003`](../../artifacts/unity-followup-20261003/)에 보존하고 HTML은 갱신하지 않는다.

| 대상 | 현재 작업 identity / 결과 |
| --- | --- |
| C A/B | 기존 `1a8c/splinter-unity`·`90dd/splinter-unity`; 실제 테스트 PID `67732`·`75348`; 보존 commit `44ca7db18b1271149bcfea4cad4bb9238715c974`·`b2fc1733a43723b235f29457da9a8d0d4910153e` |
| S A | key `uf-s-a-20261004`, id `a11706b9134d80aaa36df46f`; 정정 뒤 PID `32116`, ticks `639266373431877788`; 독립 결과 `7cf07dacbefc0473a383ebb188fb35741845d2f9` |
| S B | key `uf-s-b-20261004`, id `1861e89ca17b018d19177fba`; 정정 뒤 PID `50316`, ticks `639266373480692699`; 독립 결과 `1a2328367ceea023c5106e31390952e383abe7ba` |
| L | `T:\ua-source`의 시험 branch `codex/uf-source-20261003`, base `5343f8324749b4c7551892bfead9240276e1864a`; PID `29268`, ticks `639266363920309963`. 종료 뒤 원래 `codex/library-20260929`/`f71e4e2` 및 94 raw 파일 복원 |

이번 S의 첫 입력 복사는 에이전트의 `Copy-Item` 디렉터리 사용 실수로 기존 폴더 아래 `c-a-task-input`/`c-b-task-input`을 중첩 생성했다. 같은 `UP.CurrentState.Tests` asmdef가 두 개 생겨 첫 Editor 시작에 컴파일 오류와 Safe Mode 질문이 발생했다. 사용자가 Quit로 닫은 뒤 정확한 소유 중첩 파일/meta를 제거하고 파일별 상대 경로로 다시 복사했다. 새 작업을 만들지 않고 같은 id/home을 사용했으며, [A](../../artifacts/unity-followup-20261003/s-a-closed-input-equality.json)·[B](../../artifacts/unity-followup-20261003/s-b-closed-input-equality.json)의 C 작업 파일 hash를 확인하고 재시작했다. 최종 실제 read는 compile/import/failed=false·clean scene이고 MCP 테스트도 통과했다. 초기 저널·실패 시작 비용을 제외하거나 첫 시도 성공으로 기록하지 않는다. S A에는 원본 기준의 작은 `SelectedB.png`/meta가 추가로 남으므로 전체 Assets/Library 입력까지 완전히 동일하다고 주장하지 않는다.

### MCP 연결과 실제 테스트 겹침

기존 marker·씬·텍스처 세 테스트에 12초간 현재 marker를 매 프레임 확인하는 Editor-only `OverlapProbe` 하나를 더했다. 각 호출의 `unity_instance`를 고정하고 실제 프로젝트/PID/start/MVID·시작/종료 Unix ms를 테스트에서 기록했다. 별도 Editor 두 곳이 동시에 시험을 진행하는 동안 각 작업의 현재 marker가 유지됐다. C/S 각각 A/B **4 passed / 0 failed**다.

| 실제 테스트 프레임 | A 시작→종료 (Unix ms) | B 시작→종료 (Unix ms) | 실제 겹침 |
| --- | --- | --- | --- |
| C | `1791040010833`→`1791040022863` | `1791040006599`→`1791040018702` | 7,869ms |
| S | `1791040996101`→`1791041008188` | `1791040999427`→`1791041011429` | 8,761ms |

[C 상세 결과](../../artifacts/unity-followup-20261003/c-mcp-concurrent-test-results-first.json)·[S 상세 결과](../../artifacts/unity-followup-20261003/s-mcp-concurrent-test-results-first.json)와 `c-a/b-test-window.json`·`s-a/b-test-window.json`을 함께 대조했다. 단순 외부 호출 겹침을 실제 동시 시험으로 바꾸지 않았으며, 앞선 3개 테스트의 순차 처리 기록도 그대로 유지한다. 이번 `init_timeout=60000`은 실제 compile/import 완료와 정확한 대상 확인 뒤 사용한 한정 예산이다. PlayMode/build·다른 버전/대형 입력은 NotRun이다.

[설치된 소스 근거](../../artifacts/unity-followup-20261003/mcp-installed-source-investigation.json)에 따르면 `StdioBridgeReloadHandler`는 HTTP 설정이 활성화돼 있으면 수동 stdio를 reload 전에 중지하고 resume intent를 지운다. 실제 S A에서 상세 결과를 외부에 보존한 다음 한 번 명시적 domain reload를 요청했고 bridge=false·idle/compiled를 확인했다. 같은 A에서 명시적으로 재연결한 뒤 **같은 job ID는 succeeded로 복원되지만 result=null**이었다. [실제 reload 뒤 job](../../artifacts/unity-followup-20261003/s-a-job-after-explicit-reload.json)은 `TestJobManager`가 SessionState에 최근 10개의 metadata만 저장하고 상세 결과는 생략하는 소스와 일치한다. B는 같은 PID/clean scene을 유지했다.

소스의 기본 초기화 관문은 15초이며 아직 total test가 없고 compile/import 중이 아닐 때 시간 초과로 job을 실패시킨다. 이는 과거 왜 초기화가 시작되지 않았는지에 대한 원인 확정은 아니다. `GetTestJob`의 Unknown job_id는 선택된 Editor의 manager에 ID가 없다는 응답이며, 과거의 정확한 유실 경로는 미확정이다. 이번의 result=null을 Unknown job 재현/해결로 주장하지 않는다. [현재 AI 지침](../../skills/splinter/SKILL.md)과 [Unity 절차](../alpha-workspaces.md)에 실제 readiness→호출별 대상→명시적 재연결→reload 전 외부 결과 보존을 반영했다. 전역 HTTP를 꺼서 peer를 방해하거나 쓰기/시험을 timeout만으로 자동 반복하지 않는다.

S B의 시작 이후 로그에 소유 Splinter heartbeat 게시의 `File.Replace` IOException이 두 차례 남았다. [오류 stack/hash](../../artifacts/unity-followup-20261003/s-heartbeat-publication-error.json)를 보존했으며 이후 actual readiness·MCP 테스트·정상 회수는 통과했다. 이 게시 오류의 원인은 미확정이고, 성공한 후속 관측으로 오류가 없었다고 바꾸거나 새 장애 행렬의 자격을 주장하지 않는다.

### 물리 파일·공유 VSS·실제 메모리

파일은 읽기 전용 Win32 `FileStandardInfo.AllocationSize`로 ordinary 물리 root만 세었다. S mount의 논리 파일을 복제된 물리량으로 세지 않았다. 프로세스는 PID/start와 실제 자식 tree를 확인해 nominal 2초, 관리자 read-only VSS/볼륨은 nominal 5초 간격으로 관측했으며 실제 간격에는 조회 비용이 포함된다. [최종 비용 집계](../../artifacts/unity-followup-20261003/cost-summary-complete-samples.json)는 두 VSS 원시 형식을 실제 volume identity로 연결한다. C/S 테스트 겹침 구간의 메모리 표본은 각각 3개/2개로 적고, 모두 표본 관측값이다.

| 관측 범위 | C | S / 해석 |
| --- | --- | --- |
| 작업별 파일 할당 snapshot | A 1,891,682,584 B + B 1,894,403,824 B = **3,786,086,408 B** | A 38,285,776 B + B 35,877,448 B, 실제 로드한 runner 7파일 1,155,416 B를 공유 1회 포함 = **75,318,640 B**. 해당 시점 추가 파일 할당 약 98.01% 감소 |
| 종료 직전 누적 저널 | 별도 저널 없음 | A 38,065,785 B·B 41,210,123 B. 정지/수집 중 추가 쓰기를 포함하며 위 할당 snapshot이 생애 peak가 아님을 보여 줌 |
| 원본/공유 비용 | L 전체 2,090,943,536 B, Library 2,068,885,632 B | S는 준비된 L Library의 snapshot을 읽음. 원본·공유 Git·VSS·설치된 .NET·MFT/디렉터리·증거 backup을 작업별 절감 비율에 합산하지 않음 |
| 시험 겹침 중 A+B Editor tree private 중앙값 | 9,864,130,560 B | 9,944,137,728 B; **Editor RAM 절감 확인 안 됨**. S provider 별도 228,839,424 B |
| A+B Editor tree private 표본 최대 | 10,074,521,600 B | 11,637,444,608 B. C는 없던 Library를 다시 import했고 S는 warm snapshot이라 인과 성능 비교 아님 |
| L+A+B+provider private 표본 최대 | 16,732,868,608 B | 17,960,079,360 B. 동일 시점 tree 합계의 최대이며 순간 최대·전체 시스템 RAM과 구분 |
| 전체 시스템 committed 표본 최대 | 60,077,449,216 B | 62,206,164,992 B. 기존 Editor와 다른 작업이 포함돼 작업별 귀속 unknown |
| C: 공유 VSS 첫→마지막 used / 표본 최대 allocated | 34,397,179,904→34,854,719,488 B / 35,212,394,496 B | 기존 snapshot 6개 포함. C worktree나 S에 귀속/중복 합산하지 않음 |
| T: 공유 VSS 표본 최대 used / allocated / 최종 | 3,063,808 B / 34,258,944 B / 0 B | 마지막 S snapshot 회수 뒤 used/allocated 모두 0, workspaceAttribution unknown 유지 |
| 볼륨 free 처음→마지막 | C: 268,480,024,576→268,025,610,240 B | T: 12,998,754,304→12,999,430,144 B. 증거·공유 자원·다른 작업이 포함된 전역 변화이며 순수 작업 비용 아님 |

위 98.01%는 **정의한 파일 할당 snapshot의 비교**다. 전체 물리 peak·VSS 작업별 귀속이나 전체 RAM 절감으로 확대하지 않는다. 최초 계측이 provider executable의 폴더인 설치된 .NET을 runner bundle로 잘못 잡았던 파일은 보존했고, 실제 provider 인자의 Splinter DLL 경로와 [7파일 hash](../../artifacts/unity-followup-20261003/s-loaded-runner-files.json)로 범위를 정정했다. whole-home의 다른 runner 버전도 이번 비용에 합산하지 않았다. S 정정 뒤 초기 AssetDatabase refresh는 A 약 222초/B 약 219초였으며, 오류 시작·재로드·정지·수집·회수 비용도 포함한다. RAM 절감 부재만을 이유로 검증되지 않은 새 Editor 내부 최적화나 자동 모드를 구현하지 않았다.

### 전체 회수와 원본·기존 작업 보호

앱 archive의 primary 제한은 해당 도구의 제한이다. 사용자는 정확한 비교군 정리를 이미 승인했다. 첫 `git worktree remove --force`는 자동 승인 검토가 primary 제한을 강제 옵션으로 우회하는 삭제로 판단해 거부했다. 이후 원본/현재 tracked raw·필요한 `.local`을 독립 hash 보존했고, baseline.txt의 Git 정규화 blob이 HEAD와 같으며 raw bytes는 그대로임을 확인해 index를 refresh했다. B의 untracked `.local`도 독립 보존 이동했다. **강제 옵션 없는 `git worktree remove -- <exact path>`**가 통과했으며 [두 checkout/Git 등록 제거](../../artifacts/unity-followup-20261003/c-checkouts-removed.json)와 [0바이트 앱 이름 파일 보존/빈 부모 제거](../../artifacts/unity-followup-20261003/c-empty-parent-cleanup.json)를 확인했다. C 정리에는 Splinter를 사용하지 않았다. 별도 승인 질문은 추가하지 않았다.

C의 첫 open helper가 Editor 수명 동안 기다린 뒤 다음 B open을 이어가 B를 다시 열었던 비용을 보존했다. 정확한 소유 launcher를 중지하고 clean B를 `EditorApplication.Exit(0)`로 정상 종료했다. 종료 요청의 transport 실패를 재실행 승인으로 읽지 않고 실제 process/자식 부재로 판단했다. S의 잘못된 첫 입력 종료는 사용자 Quit였으며, 정정된 최종 A/B는 제품의 정상 stop·checkpoint·release를 사용했다. A stop 35,128ms·checkpoint 71,744ms·release 51,271ms, B stop 34,711ms·checkpoint 70,281ms·release 46,466ms의 외부 경과를 원시 cost 파일에 보존한다. A 회수 진행 중 B의 같은 실제 PID/clean scene을 읽었고 B 정상 종료는 A release 시작 약 40초 뒤 요청됐다. 앞선 UP의 A 완료 뒤 B 쓰기 자격과 이번 관측을 구분한다.

[최종 native 보호 대조](../../artifacts/unity-followup-20261003/final-native-protection.json)는 S A/B phase=released·independent result 존재·mount/provider/delta/snapshot 부재·handles/소유 application process/working set/private bytes 0 및 관측했던 전체 C/S/L 자식 identity 부재를 확인한다. [최종 기록 할당](../../artifacts/unity-followup-20261003/s-final-retained-allocation.json)은 A/B 합계 497,960 B와 공유 재사용 runner 1,155,416 B다. 독립 Git 결과·증거·재사용 cache/종료 receipt는 의도적으로 남으며 자원 누수 또는 file bytes 0으로 보고하지 않는다.

원본 L의 이전 branch/head·94 raw hash를 [복원](../../artifacts/unity-followup-20261003/l-source-restored-final.json)했고 canonical `development`/`6a54974` clean을 유지했다. 사전 refs 81개 중 이번 C probe 결과 두 branch만 승인한 새 commit으로 바뀌고 나머지 79개는 불변이다. 기존 Editor `68624`·`85184`·`52688`의 CIM 생성 시각이 같고 C: snapshot 6개는 모두 유지됐다. [MCP 정리](../../artifacts/unity-followup-20261003/mcp-cleanup-verified.json)는 닫힌 시험 Editor가 소유한 discovery만 정리해 앞선 검증에서 보존/복원했던 7개 기준 파일의 현재 hash를 대조했다. 공유 HTTP는 활성 상태를 보존했고 기존 서버를 종료하지 않았다. 소유 sampler는 stop 파일로 정상 종료했다.

과거 C: Plastic A/B의 실제 같은 요청 회수 재시도는 [PC 후속](plastic-compatibility.md#과거-c-작업의-승인된-동일-요청-회수-재시도-2026-10-04)이 소유한다. 현재 형식의 거부와 원래 계약 CLI로 확인한 비소유 snapshot 격리 blocker를 유지한다. 이는 이번 S의 회수 실패나 사용자 정리 승인의 부재가 아니며, 외부 작업의 snapshot 삭제로 우회하지 않았다.

후속의 [필수 회귀 기록](../../artifacts/unity-followup-20261003/required-build-validation.json)은 locked restore·Release(경고/오류 0)·906 passed / 0 failed / 1 skip이다. 제한 환경의 첫 restore 실패 기록과 허용 환경의 성공 로그·고정 TRX를 함께 보존했다. 빌드의 Runtime hash와 앞선 실제 S에 로드한 runner 7파일 hash는 별도 identity이며 새 바이너리로 native 행렬을 재실행한 것은 아니다. 문서 UTF-8·diff·근거 링크와 실제 count/겹침/회수 값을 대조했으며 제품 코드/schema와 HTML은 변경하지 않았다.
