# VCS 독립 수명주기 검증

범위는 [확정 계획](../plans/implementation-plan.md#마감한-선행-개발-단위-vcs-없는-기본-수명주기), 현재 구현은 [일반 폴더 계약](../vcs-independent-workspaces.md), 관문 상태는 [VCS 추적표](../plans/validation-matrix.md#vcs-없는-기본-수명주기)가 소유한다. 기존 Git 기반 M0의 완료를 이번 후보에 승계하지 않는다.

`FileWorkspaceTests`는 실제 파일 공급자와 **fake snapshot backend**를 사용한다. fixture 복사는 시험의 작은 합성 기준 준비이며 제품 NTFS의 복제/범위 쓰기 자격이 아니다. A/B·원본 이후 변경·같은 key 재개·checkpoint payload의 실제 내용과 source/snapshot/mount 없는 확인·dirty discard·기존 결과 유지·peer 보호·capture/connect/unmount/delta/snapshot 응답 유실·손상 payload 보존·seal 변화 차단·VCS 관리 정보 거부·Git 오류 fallback 부재·이전 상태 거부를 검증한다. 모든 일반 폴더 단계에서 Git adapter 호출 0을 확인한다.

`ContractAndCliTests`는 일반 폴더 입력/schema와 Git/Unity 없는 doctor를 확인한다. 기존 Git 수명주기·merge/dirty guard와 Windows 자원/저널 회귀는 필수 `scripts/build.ps1`에 포함한다. 새 `Splinter.SourceControl.Files.dll`이 runner 묶음과 서비스 패키지에 포함된다. runner는 현재 7개 파일이며 과거 6개 후보의 실제 메모리 수치를 현재 후보 수치로 재사용하지 않는다.

최종 `pwsh ./scripts/build.ps1`의 locked restore·Release는 경고/오류 0, **795개 중 794개 통과·선택 ReFS 1개 Skipped**다. TRX는 `artifacts/vcs-independent/native-fix-final.trx`, build 출력은 `native-fix-build.log`다. 앞선 794개 시험의 `automated-final.trx`·`verification.json`, 초기 schema/fixture 실패 증거는 그대로 보존하며 최종 통과로 덮어쓰지 않는다. 실제 회수에서 재현한 경로 결함은 아래의 같은 요청 복구와 실제 authority pin 자동 회귀로 확인했다.

## 실제 A/B 한 묶음의 판정

2026-10-02에 [고정 native 계획](../plans/vcs-independent-validation.md)을 사용자 승인으로 실행했다. 판정은 **scoped-complete-with-limitations**다. 원시 증거는 `artifacts/vcs-independent/`, 최종 요약은 `native-execution.json`이다. 기존 Git fixture와 Editor·결과·보호 상태를 보존했다.

| 경계 | 실제 관측과 증거 |
| --- | --- |
| 입력/권한 | `.git` 없는 `T:\vcs-none-20261002`, home `.local/vcs1002`, `source=none`, `application=unity`, `base=snapshot`, journal budget 16 GiB. 최초 import가 만든 설정/lock을 포함해 불변 기준을 고정. 기존 grant 3개와 새 grant 1개를 승인된 설치 도구로 적용. 첫 업데이트 때 보호 기록 1,317개, 수정 업데이트 때 1,345개가 유지됐으며 일반 CLI의 `callerElevated=false` |
| A/B 생성/재개 | A `87465748b51e98555de4313f`, B `968940e262731f6de9410814`, 서로 다른 실제 VSS/WinFsp mount. `a-resume-02.json`에서 새 CLI의 동일 A id/digest/basis/snapshot 유지. 원본을 `original-after-capture`로 변경해도 A/B는 `basis`를 읽음 |
| 소유 Editor/외부 도구 | Unity `6000.3.22f1`, A PID 45772/UTC ticks 639265202652845900, B PID 25072/639265202897372343. 정확한 경로 지정 Unity CLI/Pipeline의 포트 7801/7802와 loaded `basis`·A `a-ok!`를 확인. A 외부 컴파일 성공, B 의도적 `CS1525` 실패. Splinter heartbeat는 컴파일 성공 증거로 사용하지 않음 |
| 결과/peer | `a-checkpoint-04.json`의 stable-journal 수집·정상 Editor 종료·독립 payload. `b-after-a-release.json`에서 A의 unmount/delta 회수 뒤에도 B가 자기 경로/실패 소스로 외부 도구에 응답. 원본 Editor PID 76076/기존 시작 시각, 원본 Pipeline 7800 ready 유지 |
| 회수/폐기 | A의 수정 후 동일 release는 `released`. B는 성공 checkpoint 없이 고정 intent/digest로 폐기, 먼저 만든 A snapshot 때문에 `discard-reclaim-pending`을 거친 뒤 같은 intent로 `discarded`; 중복 요청도 동일 상태. 최종 두 mount/delta/snapshot 부재, 소유 Editor/provider/process/handle/메모리 0, ownership verified. A 결과는 유지되고 B의 `files/result.json`은 없음 |
| 회수 후 내용 | `independent-payload-after-reclaim.json`: 기준/결과의 49개 file 항목·26개 object의 길이/SHA-256 검증. 보존 `basis`·`a-ok!`와 수정된 원본을 별도로 읽음. 검사에는 원본/snapshot/mount를 결과 입력으로 사용하지 않음 |

최초 생성·Editor·checkpoint는 서비스 패키지 `package-20261002-01`로 실행했다. A release의 `StoragePathRejected`는 content-object 경로의 `objects/<hash>`가 Windows authority에 비정규 경로로 전달된 결함이었다. `FileWorkspaceProvider`의 내부 경로만 OS 구분자로 만들고 authority guard를 유지했다. 최종 자동 회귀와 새 패키지 `package-20261002-02`(manifest SHA-256 `57a91e572c7be16e61a3a6f6f2901bfc9c8e3d18c6297ffd26f3f88edfcd2571`)를 적용한 뒤 **동일 A release/B discard 기록을 재개**해 마쳤다. 첫 실패·B 지연 회수 증거를 보존했다. 수정 패키지로 전체 A/B를 새로 생성해 반복한 것은 아니며 이전 후보의 실행을 새 후보의 별도 통과로 표시하지 않는다.

Unity CLI `1.0.0-beta.5`의 `status`/`pipeline list`는 A/B를 누락했고 정확한 `command --project-path`는 연결됐다. 소유 경로 두 개의 Hub 임시 등록도 목록 누락을 해결하지 않았으며 종료 후 추가한 등록만 제거했다. 향후 인계에는 목록만으로 부재를 추정하지 않고 target path·포트·PID/시작 시각을 대조한다. 이번 범위의 읽기/eval·recompile 연결이며 일반 MCP·Coplay 병렬 worker·전체 외부 도구 인계 자격으로 확대하지 않는다.

## 비용과 남은 제한

두 초기 storage 관측의 전체 payload 복제·journal·read cache는 0이다. A marker는 offset 61의 **5바이트 FileStream 쓰기**로 변경했다. 기존 범위 저장/개별 ACK flush·seal 회귀를 유지했고, Editor 종료 때 A journal은 13,982,199 bytes, B는 13,225,733 bytes였다. 이는 Unity Library/관리 파일 쓰기를 포함하며 marker 비용이나 전체 파일 복제량으로 읽지 않는다. 최초 Editor import 로그는 A 76.715초, B 76.034초이며 전체 준비 시간이 아니다.

A 독립 보존은 basis 논리 52,937 bytes, result 논리 54,069 bytes, 고유 실제 payload 54,140 bytes/26개다. 파일 할당 표본은 payload 92,184 bytes, 수집 단계 두 delta 28,180,480 bytes, 공유 7개 runner 1,057,112 bytes다. 최종 delta 할당은 없고 retained control/evidence는 따로 남는다. 원본 Library 45,594,344 bytes는 기준선이며 합산하지 않는다. 계측은 `-SourceKind none`으로 Git 조회 없이 수행했고 file-result category는 control의 부분이므로 이중 합산하지 않는다. 공유 VSS의 workspace 귀속과 전체 물리 peak는 **unknown**이다.

일반 Editor 없는 `application=none`은 자동 적합성 범위이며 별도 native Editor 없는 한 묶음은 실행하지 않았다. 일반 폴더 original-apply·Plastic 사용 환경의 호환 처리·대형/ARM/원격/OS·native 강제 중단 행렬도 **NotRun/범위 제외**다. 기존 Git M0·과거 runner 메모리 수치를 새 후보의 전체 지원으로 승계하지 않는다.
