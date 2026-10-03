# Unity 병렬 Editor의 실제 에셋·테스트·반영 검증

2026-10-03의 작은 Windows x64/NTFS·Git/Unity 입력에서 Splinter S군 A/B의 **MCP 대상 선택, 텍스처·씬 편집과 재로드, 현재 파일 테스트의 실패 격리, 전체·선택 경로 반영과 소유 자원 회수**를 `scoped-complete-with-limitations`로 마감했다. 반영한 원본을 실제 Editor에서 로드·테스트한 뒤 원본 94개 tracked 파일과 기존 HEAD를 복원했고, 두 독립 결과와 시험 branch를 보존했다. Codex 독립 worktree C군의 아래 다섯 항목은 모두 `NotRun`이다. 앞선 코드 수정·수명주기 비교의 통과를 이번 에셋/도구/반영 자격으로 승계하지 않는다.

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
| UP-01: MCP의 정확한 A/B routing | Passed with limitations. A/B 각 3회, 총 6개 요청이 올바른 반환 root/PID에 도달했고 invalid selector는 거부됨. MCP 내부 동시 실행 자격은 별도 | NotRun | [`mcp-routing-concurrent.json`](../../artifacts/unity-parallel-validation-20261003/mcp-routing-concurrent.json) |
| UP-02: 텍스처 편집·현재 파일/loaded 값 | Passed. 같은 상대 경로의 64×64 PNG를 A/B의 다른 색·import 설정으로 저장하고 raw PNG와 loaded texture를 대조 | NotRun | [`mcp-texture-edited-and-loaded.json`](../../artifacts/unity-parallel-validation-20261003/mcp-texture-edited-and-loaded.json), [`mcp-scenes-current-loaded-after-reconnect.json`](../../artifacts/unity-parallel-validation-20261003/mcp-scenes-current-loaded-after-reconnect.json) |
| UP-03: 씬·material 편집·저장·재로드 | Passed with limitations. A/B root·위치·Camera/Light·PNG material 연결을 확인하고 scale을 다시 편집·저장·reload. 첫 거부/부분 생성의 명시적 재개를 기록 | NotRun | [`mcp-scene-create-owned-partial-resume.json`](../../artifacts/unity-parallel-validation-20261003/mcp-scene-create-owned-partial-resume.json), [`mcp-scene-edited-reload.json`](../../artifacts/unity-parallel-validation-20261003/mcp-scene-edited-reload.json), `s-a-scene-scale-actual.json`·`s-b-scene-scale-actual.json` |
| UP-04: 현재 파일 테스트·실패 격리·복원 | Passed with limitations. 최초 MCP A/B 각각 3/0, B 현재 marker만 `B-v2`로 바꿔 A 3/0·B 2/1, `B-v1` 복원 뒤 B 3/0. MCP runner 순차 처리와 CLI 호출 겹침은 구분 | NotRun | [`mcp-test-results-initial.json`](../../artifacts/unity-parallel-validation-20261003/mcp-test-results-initial.json), [`s-a-tests-parallel-negative.json`](../../artifacts/unity-parallel-validation-20261003/s-a-tests-parallel-negative.json), [`s-b-tests-parallel-negative.json`](../../artifacts/unity-parallel-validation-20261003/s-b-tests-parallel-negative.json), [`s-b-tests-current-state-restored.json`](../../artifacts/unity-parallel-validation-20261003/s-b-tests-current-state-restored.json) |
| UP-05/06: 전체·부분 원본 반영과 완전 정리 | Passed with limitations. A 전체 14파일 반영·release 중 B 지속, B 선택 2파일의 AI/Git 반영·A 14파일 보존, 원본 L2의 실제 load/test 3/0, A/B 같은 작업 회수 및 원본·기존 작업·MCP 상태 복원 대조 완료. 첫 seal 거부와 명시적 재시도를 유지 | NotRun | [`s-a-integrated-all-paths-verified.json`](../../artifacts/unity-parallel-validation-20261003/s-a-integrated-all-paths-verified.json), [`s-b-selected-paths-applied-verified.json`](../../artifacts/unity-parallel-validation-20261003/s-b-selected-paths-applied-verified.json), [`l2-integrated-assets-scene-actual.json`](../../artifacts/unity-parallel-validation-20261003/l2-integrated-assets-scene-actual.json), [`final-native-cleanup-and-restoration.json`](../../artifacts/unity-parallel-validation-20261003/final-native-cleanup-and-restoration.json) |

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

현재 다섯 항목의 C 비교는 모두 `NotRun`이며, C/S 성능 우열도 판정하지 않는다. 이 실행은 작은 고정 Git/Unity 에셋 입력의 절차 검증이다. 대형 프로젝트·새 패키지/Editor 버전·MCP runner 동시성·Play/build·다른 VCS·ARM/OS 장애·전체 물리/RAM peak는 자격 범위 밖이다. 공유 VSS의 작업별 귀속과 전체 peak는 `unknown`이며, 이전 C: 회수 보류도 이번 T: 성공으로 해소했다고 읽지 않는다.

## 문서와 현재 계약의 회귀 확인

같은 제품 후보 `52d9099b2df3bdd786defbabe8e0b04d3ea0cdf3`에서 필수 `pwsh -NoProfile -File scripts/build.ps1`를 실행해 locked restore·Release 빌드(오류/경고 0)·906 passed / 0 failed / 1 optional ReFS skip을 확인했다. [`required-build-validation.json`](../../artifacts/unity-parallel-validation-20261003/required-build-validation.json)과 고정한 TRX를 보존한다. 제품 코드/schema 변경은 없으며 이 자동 회귀를 추가 Unity·VSS 지원 행렬의 실행으로 해석하지 않는다. 최종 UTF-8·HTML 링크와 실제 브라우저 표시 대조는 `report-validation.json`을 따른다.

필수 빌드는 위 실제 Unity 실행 이후에 수행했다. 이때 나온 Runtime SHA-256 `aea683d3004e1c6224548aeb6b0a8f09aebaa9217b1b15103b9d72af9c21c5b7`은 빌드 검증의 별도 산출물 identity이며, 앞선 실행 파일과 바이너리가 같거나 이 새 산출물로 Native 행렬을 다시 실행했다는 뜻이 아니다.
