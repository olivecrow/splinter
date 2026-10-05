# VCS 없는 프로젝트 기준본 검증

현재 후보의 [관리형 원본 계약](../plain-project-basis.md), [확정 범위](../plans/plain-project-basis.md)에 대한 2026-10-05 기록이다. 과거 VSS/Git/Plastic 성공을 새 reader 자격으로 승계하지 않는다. 파일·Unity A/B와 전체 회귀를 확인했으며 최종 NTFS 복원 관측은 아래 근거로 구분한다.

| ID | 현재 근거 | 상태 |
| --- | --- | --- |
| NP-01 | source·Library의 3 private 범위 fake, 명시적 None/basis schema, capture 응답 유실·같은 요청·VCS 거부·task digest 손상 거부·작업 참조 보호 | 자동 Passed |
| NP-02 | 실제 파일/Unity prepare·dependent restore 거부·최종 NTFS 복원·같은 restore 완료 영수증 | Passed; OS 장애의 native qualification은 NotRun |
| NP-03 | `project prepare/status/restore`, `project-snapshot`, exact basis/source/application·capabilities·owned-directory/SJR2·task receipt | Passed |
| NP-04 | 원본 private 쓰기·A/B frozen 읽기/1-byte 분리·A 전체 게시·A release 중 B 지속·B discard 또는 선택 게시·최종 원본 보존 | 파일/Unity Passed |
| NP-05 | 선택 Unity source의 원본/A/B exact MCP identity·리소스/importer·씬 생성/수정·현재 상태 tests·실제 2.690초 테스트 겹침 | 편집/테스트 Passed; 공유 서버 종료 결함은 제한 |
| NP-06 | warm·A 회수 뒤 정확한 task/common 파일 할당·baseline/독립 결과 분리·필수 전체 회귀와 실제 response schema | Passed; 전체 peak/OS 비용 unknown |

## 공통 복원 보호 회귀

현재 공통 복원 보호는 `ManagedDirectoryRestoreTests`의 로컬 NTFS 파일 시험으로 별도 확인한다. 새 파일의 범위 flush 후 다른 파일로 교체·동일 바이트 교체·생성 영수증 전 중단·보존 영역 파일의 전체 덮어쓰기 후 이동 중단·완료 뒤 교체에서 대상과 기준/저널을 보존한다. 정상 영수증 뒤 재개·readonly 파일 이동/메타데이터·retained bytes 변경 거부 시 readonly 속성 보존·쓰기 중 외부 교체 거부·이전 restore plan 거부도 확인한다. `RecoveryContractTests`는 기준 보호 오류의 응답/복구 정책 일치를 확인한다. 이 변경의 실제 Unity/WinFsp restore와 OS 장애는 NotRun이며 아래 이전 후보의 실행을 현재 보호 변경의 native 통과로 승계하지 않는다.

2026-10-05 최종 `pwsh ./scripts/build.ps1`은 locked restore·Release 빌드 경고/오류 0, 전체 1,005개 중 1,004 Passed/0 Failed/1 선택적 ReFS NotRun이다. 복원/오류 정책·디렉터리 이동/재생·Git 무진행 제한의 새 회귀 20개를 포함한다. 최종 TRX는 `artifacts/code-quality-fixes/full-final.trx`에 보존하며 문서 UTF-8·diff 공백 검사도 통과했다. 이 파일/제어 stream·기존 전체 자동 회귀를 새로운 Unity·VSS·대형/ARM/OS 자격으로 확대하지 않는다.

## Editor 없는 실제 파일 경계

`T:/np-native-project-20261005`, home `T:/np-native-home-20261005`, 기준 `0378ee9322d8ed7e5d6dd18b`, A `07db6ca107068b9ce01ef40f`, B `c866ae90adb0b5679ff72f20`. 고정 3 MiB 파일과 작은 text 입력에서 원본 private text·A/B 1-byte 변경이 서로 분리됐다. A 전체 반영 후 원본의 별도 변경은 보존됐다. A 회수 뒤 B가 추가로 썼고 B를 같은 intent로 폐기한 뒤 일반 NTFS 원본을 복원했다. A byte 11·원본 text와 B 신규 파일 부재를 확인했다. 근거: `artifacts/plain-project-basis-20261005/native-files/`의 각 JSON과 `summary.json`.

초기 파일 계획은 WinFsp의 stream query native 87로 부작용 전에 거부됐다. 관리형 mount의 NamedStreams=false·기준 ADS 거부를 검증한 경로만 처리하고 ordinary NTFS stream guard를 유지했다. 첫 A release는 공통 snapshot을 실제 삭제하는 기존 peer guard와 충돌했다. task-owned reference만 해제하는 경로로 수정한 뒤 같은 A/id/home을 회수했다. 공통 payload는 최종 restore까지 유지했다. 실패와 재시도 근거를 보존했으며 별도 새 A/B로 대체하지 않았다.

## 실제 Unity 입력과 도구

원본 fixture `b09fa58e9d40a560d4fe1af7a213f8ac23e8e3b1`의 선택 **51 files/157,794 logical bytes**를 한 번 준비했다. SampleScene·Settings·Alpha scripts·Packages/ProjectSettings이며 전체 Medieval 팩과 원본 Library는 제외했다. 이 준비 복사는 제품 기준본 복사와 구분한다. 기존 C: fixture의 `.git`와 Editor PID 37600/start `639267352637577053`는 유지했다. 공유 HTTP 8080 서버는 아래 외부 shutdown 결함으로 한 차례 중단·복구했으므로 무중단 공존 통과로 읽지 않는다. 원본 C:의 관리 정보를 제거하지 않았다.

입력 `T:/vcs-none-project-20261005`, home `T:/np-unity-home-20261005`, Unity `6000.3.22f1`, 기존 Pipeline `0.7.0-exp.1`, 고정 Coplay candidate-02. 최초 batch import return 0·CS 오류 없음. 생긴 source/Library **1,841,478,483 logical / 1,875,469,680 allocated bytes**를 같은 볼륨에서 이동했으며 prepare의 초기 payload 복사는 0이다. 준비 시 검증/hash 읽기와 약 2분 준비 비용은 존재한다.

| 대상 | basis/task | 실제 Editor identity | HTTP MCP instance |
| --- | --- | --- | --- |
| 검증 원본 | basis `806908d0bfb0cd58e6f485df` | PID 74044/start `639267765099184522` | `vcs-none-project-20261005@664943a407af9f72` |
| A | `cf9403e4f2d9d43fea984308`, digest `45f9657780dd2c6ca094464b` | PID 54492/start `639267764914133475` | `cf9403e4f2d9d43fea984308@62d76c35f33e2c74` |
| B | `f951c25c9f4b030b2f614a3a`, digest `5e5d7bc206d7b04fc6a4c746` | PID 16956/start `639267765707553341` | `f951c25c9f4b030b2f614a3a@c8a87729089c2b55` |

MCP 결과는 `artifacts/plain-project-basis-20261005/*identity*`에 정확한 project/PID/start를 보존했으며 큰 시작 시각은 문자열로 저장했다. A는 현재 Codex 기본 MCP에서도 확인했다. HTTP per-call instance를 사용해 A/B 세션 기본 대상의 경쟁을 피했다.

원본의 `Assets/OriginalNote.txt`·`Library/NPOriginalMarker.txt` private 쓰기 후 A/B에서 부재를 확인했다. A는 red/Point, B는 blue/Bilinear의 8×8 PNG를 각각 import하고 읽을 수 있었다. 각각 Camera/Light가 있는 새 Scene의 cube 위치를 `(1,3,0)`/`(2,3,0)`으로 수정·저장했다. 공통 경로 `Assets/NPState.txt`는 A 101/B 202를 유지했다. 두 code job의 시작/완료 구간도 겹쳤다. raw job 결과 `a-edit-result.json`, `b-edit-result.json`을 보존했다.

각 Editor에 작성한 선택 EditMode 시험 4개는 현재 파일 값·정확한 PID/path·peer/원본 private 파일 부재, 저장된 씬 위치, 자기 텍스처/importer, 3초 coroutine 동안 현재 상태를 검증한다. A/B 모두 4 Passed/0 Failed다. 최초 동시 job은 실제 coroutine이 직렬 실행됐고 두 번째 선택 쌍도 직렬이었다. A의 실제 테스트 시작을 관측하고 B를 접수한 세 번째 한정 쌍에서 **2,690ms 실제 coroutine 겹침**을 확인했다. 세 시도의 job·테스트별 결과와 `actual-test-overlap.json`을 모두 보존한다. 접수 구간의 겹침만으로 실행 겹침을 주장하지 않는다.

최초 read-only Pipeline eval은 main-thread 5초 timeout이었다. reload 중 포트 변경에 따른 401도 보존한다. 기존 CLI의 exact-path `editor_focus`로 초기 연결 callback을 진행하고 MCP identity를 다시 확인했다. 포트를 고정하거나 timed-out 쓰기를 자동 반복하지 않았다. 승인한 helper는 검증 Editor의 기존 Bridge.StartAsync만 호출했다. helper/시험 assembly는 소유 경로와 hash를 확인해 제거한 뒤 컴파일 완료와 결과 수집을 확인했으며 독립 결과에는 남기지 않았다. Unity 버전/패키지/전역 설정은 변경하지 않았다. 마지막 `codex mcp list --json`과 `codex mcp get unity_mcp_coplay --json`은 유효한 HTTP 8080 설정으로 exit 0이었다.

## 원본 반영과 소유 회수

A checkpoint의 독립 결과는 173,547 logical bytes/58 payload objects다. 검증 원본 Editor를 정확한 PID/start로 정상 종료한 뒤 A 전체 계획의 **8 changes/0 conflicts**를 고정 intent/digest로 게시했다. A release는 active process·RAM·open handles 0, mount/delta 부재와 task 참조 해제를 확인했다. 공통 기준 데이터는 B 때문에 유지했다.

A 회수 뒤 B MCP로 값 202·A 변경/원본 private 파일 부재를 확인하고 B cube를 `(2,5,0)`으로 추가 편집·저장했다. B checkpoint는 173,561 logical bytes/58 objects다. B 전체 원본 계획은 A가 게시한 `NPState.txt`와 meta의 **2 conflicts**를 반환했으며 실행하지 않았다. `Assets/NP_B` 선택 계획의 **6 changes/0 conflicts**만 같은 선택/intent/digest로 게시했다. 원본 값 101·별도 원본 text/Library marker와 A 파일을 보존했다. B release도 소유 자원 0과 mount/delta 부재를 확인했다. 실제 Unity 묶음은 두 독립 결과를 보존했고 명시적 discard 자격은 위 실제 파일 묶음에서 확인했다.

최종 원본 복원은 같은 basis/id/home의 고정 plan/seal/cursor로 **31,362 files**를 처리했고 **약 783.9초(13분 4초)**가 걸렸다. 검증/plan 작성과 파일별 durable cursor 게시·이동·범위 복원이 있는 비용이며 즉시 복원이라고 주장하지 않는다. `unity-final-proof.json`은 ordinary NTFS/reparse=false, 적용한 12개 파일의 독립 결과 hash 일치, 원본 private text/Library marker·값 101, 소유 Editor/provider 0·task mount/delta 및 common base/delta 부재·dependent 0을 확인했다. 임시 helper/시험 assembly도 원본에 없다. 같은 restore 재요청은 `unity-restore-repeat.json`의 완료 영수증을 반환했다. 상태·manifest·restore plan·독립 결과는 의도적으로 보존한다.

## 외부 MCP 종료 결함과 복구

검증 원본 Editor의 정상 종료 때 Coplay `McpEditorShutdownCleanup.OnEditorQuitting` → `StopManagedLocalHttpServer`가 공유 서버 PID 79520까지 종료했다. 전역 EditorPrefs의 기존 handshake를 공유하고, stale/missing pidfile에서 포트 추정으로 내려가는 구현 때문에 이 Editor가 시작하지 않은 서버에 영향을 줬다. `original-editor.log`, `shared-server-recovery.json`과 패키지 소스에 근거를 보존했다. Splinter 소유 종료가 peer Editor까지 종료한 것으로 읽지 않는다. 보존한 Git Editor와 B는 계속 실행됐지만 MCP 연결은 끊겼다.

이전에 승인한 같은 버전/포트·외부 로그 재시작으로 서버를 복구했다. 새 8080 listener는 PID 37048이고 전역 Codex 설정은 수정하지 않았다. stale handshake가 제거된 B 종료 뒤 서버가 유지됨을 관측했다. 마지막 Codex 기본 MCP의 read-only identity job은 기존 Git 원본 PID 37600/start `639267352637577053`, 정확한 path, compiling/updating=false로 성공했다. 근거는 `preserved-editor-native-mcp-final.json`과 `artifacts/vss-mcp-remediation-20261005/np1005-final-instances.json`이다.

이 복구는 패키지의 shutdown 소유권 결함을 수정한 것이 아니다. 따라서 임의의 기존 handshake가 있는 공유 서버 무중단 공존 자격은 제공하지 않는다. 사용자가 선택한 대응은 [스킬의 종료 후 연결 확인](../../skills/splinter/SKILL.md#check-remaining-mcp-connections-after-an-editor-stops)과 [필요한 서버 복구·Editor 재연결 절차](../unity-mcp-response-recovery.md#editor-종료-후-남은-mcp-연결-확인)다. 외부 MCP 코드 수정을 제품의 필수 후속으로 남기지 않으며 새로운 프로젝트 기준본 기능·자동 모드·서버별 URL/전역 설정 변경으로 확대하지 않는다. 이 문서 변경을 새 실제 종료 검증으로 읽지 않는다.

## 저장 비용과 제한

warm `unity-storage-warm.json`은 정확한 A/B·공통 basis/runner **6 roots**를 한 번씩 계측했다. 각 task 할당 **32,677,232 bytes**, 각 private journal **32,440,320 bytes**. 공통 root **1,899,048,272 bytes** 중 이동한 기존 payload는 **1,875,469,680**, manifest 등 **11,720,672**, 원본 private journal **11,857,920**이다. runner는 **3,019,440 bytes**이며 빌드가 다른 cache 두 묶음이다. 합산에서 이동 baseline을 제외한다.

`unity-storage-after-a.json`에서 회수한 A root는 567,968 bytes, 아직 수집 상태의 B는 75,467,824 bytes였다. B delta는 74,907,648 allocated bytes이며 반복 compile/test/편집 비용을 포함한다. 각 독립 결과 payload는 227,696 allocated bytes다. 공통 root 1,927,884,112 bytes 중 기존 payload 1,875,469,680, 원본 journal 40,697,856, metadata 11,716,576이다. 새로운 불변 기준 전체 복제와 구분한다. 최종 회수 뒤 남은 영수증/manifest/restore plan/독립 결과 비용은 최종 계측에 따르며 증거 폴더는 제품 저장 비용에 합산하지 않는다.

`unity-storage-final.json`의 6 roots는 모두 complete다. A/B root는 **567,968/580,256 allocated bytes**이고 각 independent payload 227,696을 포함한다. 공통 root에는 기준 payload/원본 journal 없이 manifest·restore plan·영수증 등 **33,498,680 bytes**가 남는다. 공유 runner **3,019,440 bytes**를 포함해 남은 제품 관리/결과 파일 할당은 **37,666,344 bytes**다. 원래 데이터는 원본 NTFS로 돌아갔으며 추가 복제 baseline에 합산하지 않는다. 기록/독립 결과 보존을 정리 실패로 읽거나 임의로 삭제하지 않는다.

이 backend는 VSS를 호출하지 않는다. 일반 사용자 계측의 시스템 전체 VSS 조회는 unknown이며 시스템 VSS 0이라고 주장하지 않는다. 물리 순간 peak/OS metadata·개별 ACL 의미·대형/ARM/재부팅/전원 장애·다른 Unity 버전은 NotRun이다. RAM 절감은 사용자 결정대로 합격 조건이 아니다.

`full-build-final.log`의 locked restore·Release build는 경고/오류 0, **984 Passed/0 Failed/1 optional ReFS Skipped**다. 이전 전체 회귀에서 신규 mode/capability·command sideEffects의 schema 누락으로 8개가 실패한 로그도 보존했으며 누락을 고친 현재 후보의 결과와 구분한다. `response-schema-validation-complete.json`은 최종 restore/status/동일 restore를 포함한 실제 파일/Unity 수명주기 응답 43개가 현재 제품 `ContractCatalog`의 response schema를 통과했음을 기록한다. 지원 판정은 작은 실제 입력과 자동 회귀의 **scoped-complete-with-limitations**이며 외부 MCP shutdown 제한·unknown·NotRun을 그대로 유지한다.
