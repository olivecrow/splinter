# 수명주기 병목 후속

[English](../../plans/lifecycle-bottlenecks.md) · [검증](../verification/lifecycle-bottlenecks.md)

## 고정 범위

생존 경로와 내용이 같은 원본 Library 복원에서 파일별 작업을 줄인다. 제한된 계측으로 callback/queue/flush와 Git 준비 비용을 구분하고 현재 Unity 시작 자료를 보존한다. 입력은 RR-01 이후 현재 코드이며 과거 전체 시간은 이 변경의 전후 측정이 아니다.

| ID | 구현·증거 경계 |
| --- | --- |
| BO-01 | 최종 상태가 정확히 같으면 양수 seal sequence도 기존 디렉터리 경로를 허용한다. metadata 변경·기존 leaf 삭제는 별도 sparse 디렉터리 계획을 사용한다. 미지원 구조/내용 변경과 시작한 파일 계획은 일반 복원을 유지한다. |
| BO-02 | 같은 고정 10,000개 일반 NTFS 파일에서 leaf 하나 삭제, 검증·회수까지 전체 복원. 고정 baseline과 후보의 직렬 ABBA이며 Editor·mount provider 자격은 아니다. |
| BO-03 | 제한된 WinFsp admission/batch/store-call·mutation 시간 관측. 기존 영속 처리 뒤 성공 ACK와 명시적 drain/실패 처리를 유지한다. OFF/ON callback 비용은 Unity와 별도다. |
| BO-04 | 제한된 Git reader/proof/cache 관측과 명시적 attach 구간 계측. reader/cache 예산 증가나 proof 생략은 없다. |
| BO-05 | 정확한 task/provider/Editor identity, 제한된 Bee 자료/로그와 before/live 연계를 사용하는 읽기 전용 개발용 캡처. Editor 시작/종료·자동 profiler 주입은 없다. |

## 복원 계약

최초 sparse 경로는 Unity Library의 latest/bounded 저널에 한정한다. 디렉터리 집합, 생존 파일의 원래 경로/길이가 같고 private range·신규 파일이 없으며 metadata patch·기존 leaf 삭제만 허용한다. 효과 전에 기준본·정확한 seal·원본/delta 경로·모든 NTFS identity·최종 metadata·정렬된 삭제 목록을 새 계획에 고정한다. root/조상 보호를 유지하고 root 이동 뒤 하위 핸들을 다시 열어 생존 내용과 identity를 전부 검증한다.

계획은 같은 고정 핸들로 읽고 hash를 계산하며 정확한 sealed 저널의 metadata-only replay와 최종 효과를 대조한다. 최종 검증과 staged leaf 회수가 끝날 때까지 읽기 전용 저널을 유지한다. 변경 없는 경로에도 적용하며 양수 sequence 자체는 최종 일치의 증명이 아니다.

삭제 leaf는 고정한 기존 receipt root 안의 예정된 이름으로 이동한다. 정확한 목록 검증·최종 metadata 적용·계획에 결합한 검증 영수증 게시 뒤에만 보관 데이터를 파괴한다. 별도 회수 영수증은 중단된 삭제 재개 시 이미 없는 staged 항목을 허용한다. 생존 파일의 최종 pin은 계속 유지하므로 같은 증명이 유지되는 동안 세 번째 전체 scan은 필요 없다. 저널 회수는 기존 정확한 inventory와 seal을 사용한다. 전체 basis·이동 후 내용 증명과 생존 파일 metadata 적용은 유지한다. 줄어드는 것은 생존 파일별 이동·영수증·cursor 게시이며 전체 입력 검증을 생략하지 않는다.

기존 일반/unchanged-directory 계획은 해당 경로로 재개한다. 충돌·고아 계획, 외부 대상, 교체 identity, 변경된 내용/seal은 보존·거부한다. 이전 계획을 이관하지 않는다. 현재 재시도는 계획 metadata를 다시 적용하므로 중단 뒤 모든 외부 metadata-only 편집을 보존한다는 약속은 아니다.

## 계측과 남은 판단

`writeAdmission`과 `gitReads`는 선택적으로 켜는 provider 내부 진단이며 복구 입력·정확성 증명이 아니다. 시작 프로세스에서 `SPLINTER_WRITE_DIAGNOSTICS=1` 및/또는 `SPLINTER_GIT_PREPARATION_DIAGNOSTICS=1`을 설정하며 이미 실행한 provider는 시작 당시 설정을 유지한다. 기본 상세 관측은 null이다. monotonic ticks/frequency와 누적 counter를 사용하고 동시·중첩 합계를 전체 시간에 더하지 않는다. WinFsp ring은 크기를 제한하고 덮어쓴 표본 수를 보고한다. Runtime delta는 store-call 구간이며 관리용 flush가 포함될 수 있다.

진단 CLI 프로세스에 `SPLINTER_GIT_PREPARATION_DIAGNOSTICS=1`을 설정하면 `control/git-preparation.json`을 게시한다. attach 효과 전에 이전 기록을 새 attempt ID·`running` marker로 교체하며 이 최초 게시가 실패하면 해당 명시적 진단 시도는 실행하지 않는다. 완료는 `succeeded`·`failed`·`cancelled`, 고정 UTC/monotonic anchor, 요청 identity, 제한된 검증/read-tree/warm/refresh/final-head-status/admission 중첩 구간을 기록한다. 완료 게시 실패는 실제 연산 결과를 유지하고 해당 시도를 미완료로 남긴다. 기본 실행은 진단 파일을 생성·갱신하지 않는다. attempt/시각/요청 identity를 대조하며 이전 기록이나 미완료 marker를 현재 attach 성공으로 읽지 않는다.

[시작 캡처 명령](../development-cli.md)은 회수 전에 현재 증거를 보존한다. 캡처 오버헤드·fixture 비용·과거 RR-01 관측을 구분한다. Unity profiler는 자동으로 켜지 않으며 고정한 profiler 실험은 이후 외부 에이전트 절차다.

독립 callback이 실제로 누적될 때만 후속 queue scheduling을 선택한다. 동기 호출자는 현재 영속 ACK 전 다음 종속 요청을 보낼 수 없다. 미사용 퇴출·재decode·대기가 클 때만 Git demand 우선 선행 읽기를 선택한다. reader만 늘리면 현재 proof window가 줄고 byte만 늘리면 chunk entry 제한이 남는다. bridge DLL 전환과 작은 metadata commit 절감은 보류한다. 조기 ACK·전체 복사 cache·stale endpoint/DAG 조작·외부 도구 수정·폐기 backend는 도입하지 않는다.

## 종료 기준

후속 [BD-01 실제 Unity 진단](../verification/unity-lifecycle-bottleneck-diagnosis.md)은 고정 GUI 작업공간 하나의 폐기·정확한 Library 복원까지 측정했다. 전체 decoder 대기는 0.000837초뿐이어서 시험한 캐시 전용 reader 우회 후보를 제외했으며 후보의 속도 개선을 주장하지 않는다. 큰 비용의 후속은 순차 영속 쓰기·소스 캐시의 생산/소비 간 보존·시작 reload다. Close/Open 대기 합계는 원인 분석 단서이며 더할 수 있는 절감 시간이 아니다. 최종 context의 unlink/rename 회수·health 검사 때문에 잠금만 제거할 수 없다. 캐시 예산 확대·비동기 close 큐·내구성 완화·새 Unity backend는 채택하지 않았다. 원본 반영·후보 GUI 전후 비교·세부 초기화 원인 분리는 미자격이다. 별도 [GR 용량 실험](../verification/git-payload-retention.md)은 Editor 없는 생성 절감이 작아 큰 cache 후보를 제외했다.

중단·외부 데이터·metadata·identity의 의미 있는 시험, 필수 build, 제한된 계측 시험과 같은 조건의 일반 NTFS 복원 증거를 확인한다. 구성 요소 절감을 새 Unity 전체 절감으로 표현하지 않는다. 후속 Unity 실행은 source/전체 Library/초기 assembly 상태와 동일한 연속 시작·종료 스톱워치를 고정하고 정상 종료·소유 복구/정리를 포함해야 한다. 적용과 폐기는 종료 기준이 다르며 복원 시간만으로 승계하지 않는다.
