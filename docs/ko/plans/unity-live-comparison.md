# Unity 실제 공간·메모리와 Codex worktree 비교 범위

[English](../../plans/unity-live-comparison.md)

사용자는 최신 구현에서 실제 Unity 저장공간·메모리를 검증하고 기존 Codex Git worktree 방식과의 차이를 HTML 보고서로 요청했다. 추가 조건은 **로컬 원본 L + 독립 작업 A/B, 총 세 작업의 동시 진행·필요한 만큼의 자원 사용·상호 간섭 부재**다. Codex 비교군은 Splinter를 전혀 사용하지 않는다.

## 고정 비교

| 구분 | C: 기존 Codex 방식 | S: Splinter 방식 |
| --- | --- | --- |
| A/B 생성 | Codex 앱의 실제 Git worktree 작업 두 개 | 현재 제품 CLI의 독립 NTFS snapshot workspace 두 개 |
| L | 같은 기존 원본 `T:\ua-source`의 소유 Unity Editor | 같은 원본의 소유 Unity Editor |
| 실행/검증 | 기존 Unity CLI·일반 Git·파일 도구만 사용. Splinter CLI/서비스 요청/provider/bridge/Coordination 사용 금지 | Splinter는 workspace/Editor 수명·결과/회수만 담당. 실제 코드·에셋 확인은 같은 Unity CLI |
| 계측 | 별도 읽기 전용 OS 파일 할당·프로세스 계측 | 동일 계측으로 실제 records/delta/provider를 추가 분리 |

공통 입력은 기존 등록 Git 원본의 clean `f71e4e244ba9b085aa0fc439336e4c60ee3d5d5d`, Unity `6000.3.22f1`·URP `17.3.0`·기존 Pipeline `0.7.0-exp.1`, 실제 Qualification FBX/2048 텍스처/재질/씬이다. Medieval 전체 팩과 기존 PV의 실패/전체 raw 동등성 unknown은 이번 작은 입력의 통과로 해소하지 않는다. C worktree용 저장소에 이 실제 commit/ref만 가져오며 canonical `development`와 기존 refs/작업은 보존한다. Editor 시작 전 두 방식의 추적 source 파일 bytes/hash를 원본과 직접 대조한다.

S는 기존 등록 home `C:\Users\olive\Desktop\Development\splinter\.local\wl29`, 새 keys `uc-s-a-20261003`·`uc-s-b-20261003`, Git/Unity/NTFS와 고정 journal budget을 사용한다. C는 Library 없는 기본 Codex worktree, S는 원본의 기존 warm Library를 snapshot으로 읽는 현재 사용 방식이다. 이 의도된 출발 차이를 밝히고 원본 Library와 cache 이력을 기록한다. 동시 세 Editor 조건은 같으며 C/S cohort는 순차 실행한다. 새 서비스/등록/설치·VSS 예산/볼륨·전역 Git 설정을 변경하지 않는다.

## 질문·실행과 종료 기준

| ID | 질문/실행 | 완료 증거 |
| --- | --- | --- |
| UC-01 | 현재 후보·환경·동일 입력과 실제 C 생성 | Runtime hash·원본/기존 작업/Editor·Git refs/등록·파일 allocation·OS RAM 기준선. 실제 앱 C의 경로/작업 identity/생성 시간과 원본 byte/hash 일치 |
| UC-02 | L/A/B 실제 사용과 격리 | 세 실제 project/PID/start/MVID와 코드 baseline, FBX mesh·texture 실제 load. L/A/B 서로 다른 스크립트 수정·reload/실행 값, 원본의 이후 수정이 A/B 기준에 전파되지 않음 |
| UC-03 | 필요한 저장/메모리와 지속 | 생성·첫 ready·수정 후·정상 stop/동일 작업 재개·세 작업 idle의 allocation과 2초 간격 process 표본. C source/Library와 S delta/records/runner, 기존 공통 Git·원본 Library와 증거 비용 분리. 바이트/할당과 private/working set·시스템 사용량을 서로 대체하지 않음 |
| UC-04 | A 종료 중 L/B 지속 | A 독립 결과 보존·정상 stop/회수, 같은 L/B의 후속 코드 실행·해시·identity 유지. B 결과 보존·회수. 시험 소유 원본 변경만 독립 보존 후 기준 source 복원; 기존 원본 작업/프로세스는 건드리지 않음 |
| UC-05 | 실제 근거와 HTML 보고서 | 같은 성공한 코드/에셋 범위·실측 시간/공간/메모리·비용 원인/제약·raw JSON 연결과 브라우저 검증. 미완료/실패는 그대로 표시하고 0/통과로 대체하지 않음 |

원시 증거는 `artifacts/unity-live-comparison-20261003/`, [HTML 보고서](../../../reports/unity-live-comparison-20261003.html)와 [직접 검증](../verification/unity-live-comparison.md)이 결과와 종료 상태를 소유한다. 이번 한 번의 작은 실제 입력 L/A/B 묶음을 각 방식에서 수행하고 종료한다. 생성·파일 읽기/수정·Editor 재개·결과 보존/회수는 요청된 검증의 일부다. C는 보존된 Git 결과 뒤 recoverable archive를 요청한다. 앱이 primary worktree 보관을 거부한 경우 source/결과/증거를 유지하고 소유 Editor 부재·정확한 경로·dry-run을 확인한 공식 Unity CLI로 Library/Logs만 정리한다. 해당 제한과 잔여/삭제 비용을 기록하며 전체 checkout 삭제로 우회하지 않는다. S는 같은 요청/id/home·seal·독립 결과 뒤 정확한 소유 자원만 release한다. 실패·보류·기존 C: snapshot은 보존한다.

원본 반영/merge·discard·Play/Test/build·큰 모델/텍스처 교체·대형/ARM/원격/전원/전체 장애 행렬은 제외한다. 몇 분의 준비 비용 자체를 새 최적화/큐 도입 이유로 삼지 않는다. 프로세스 합계를 물리 RAM 절감률로 표현하거나 전역 VSS/OS cache의 귀속을 추측하지 않는다. 전체 머신/미관측 순간 peak와 기본 입력을 넘는 지원은 별도다.

## 관측 후 승인한 도구 버전 변경

기존 Pipeline `0.7.0-exp.1`의 로컬 L은 `0.0.0.0:7800`, 보존 대상 기존 `0.8.0-exp.1` Editor는 `127.0.0.1:7800`을 점유했다. 정확한 L 경로의 CLI catalog 연결도 실패했다. 주소 겹침 관측과 실패를 보존하며 내부 인과를 확정하지 않는다. 사용자는 시험 대상만 Pipeline `0.8.0-exp.1`로 올리고 검증을 계속하되 0.7의 실패를 기록하도록 명시적으로 승인했다. 이 변경은 L 및 C/S A/B의 같은 입력에 한정하고 기존 Editor·프로젝트는 유지한다. 0.7 준비·변경 비용과 0.8 실제 성공 구간을 분리하며 변경 후 source bytes와 실제 Editor identity를 다시 대조한다. Unity Editor/다른 패키지·서비스·grant·볼륨은 변경하지 않는다.

## 종료 경계

실행한 비교·HTML 납품은 마감하며 UC-04의 마지막 회수는 Partial로 남긴다. 같은 home의 과거 기록이 현재 필수 필드를 누락해 의존성 조회를 차단한 실제 원인·새 A/B의 독립 결과·snapshot 보존·소유 mount/delta 회수는 검증 문서가 소유한다. 구형 기록을 자동 이관·삭제하거나 새로운 home/grant·서비스 설치로 이번 시험을 우회하지 않는다. 그 회수 후속을 진행할 때는 기존 기록의 보존·같은 작업 identity·승인한 변경 경계와 종료 기준을 먼저 확정한다. 이번 한 묶음의 실패를 해결하려고 대형/PV/새 A/B 전체 시험을 자동 반복하지 않는다.
