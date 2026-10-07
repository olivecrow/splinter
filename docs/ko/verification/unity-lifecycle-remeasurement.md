# Unity 수명주기 재측정

[English](../../verification/unity-lifecycle-remeasurement.md)

이 문서는 [CO-01–08](checkpoint-release-optimization.md) 이후 실제 Unity로 진행한 **UR-01–04**를 소유한다. 측정 전에 제품 소스를 `38b6803`으로 커밋했다. 고정한 일반 배포본은 검증한 CO-08 후보이며 실행 중 제품을 다시 빌드하거나 계측 코드를 삽입하지 않았다. 원시 근거는 ignored `artifacts/unity-remeasure-20261007/`와 `.local/ur7/`에 보존한다.

## 입력과 절차 — UR-01

Unity **6000.3.22f1**, Git 소스 `b09fa58`, `git-snapshot`, 기본 최적화 저널과 원본 Library 참조로 독립 A/B 한 묶음을 실행했다. **추적 소스 1,602개 / 2,804,772,822바이트**가 [이전 비교](readme-unity-comparison.md)와 같다. 이전·현재 Library manifest의 모든 항목, payload hash와 기록된 속성·시각도 일치한다. **파일 42,420개, 디렉터리 2,440개, 3,141,660,252바이트**이며 소유 ID·기준본 경로·캡처 시각만 다르다.

ensure 두 번은 순차로 실행하고, 소유 Editor 두 개의 초기화·refresh는 병렬로 실행했다. A는 `value + 1`을 `value + 2`로, B는 `value + value`를 `value + value + value`로 바꿨다. 기존 외부 검증 probe로 정확한 project/PID/시작 시각, 신선한 비컴파일·비갱신 상태, 기대 코드 값, 요청 ID가 맞고 오류 없는 refresh, generation/assembly MVID 변경을 확인한다. 선택 에셋 검사도 완료했다. 설치된 패키지와 과거 fixture probe는 유지했으며 MCP/Unity CLI를 설치하거나 수정하지 않았다.

Editor 정상 종료 뒤 A checkpoint/release, B checkpoint/release 순서로 실행하고 원본 Library를 복원한다. 독립 결과의 기준은 과거 Codex 결과의 tree다. Codex를 다시 실행하지 않았다.

## 관측 시간 — UR-02

| 단계 | 이전 Splinter, 초 | 현재 Splinter, 초 |
| --- | ---: | ---: |
| Library 준비 | 272.9366 | 240.3283 |
| A ensure | 381.1842 | 322.3461 |
| B ensure | 385.2465 | 319.4375 |
| A/B 초기 컴파일 준비 | 1,753.4893 | 1,041.8831 |
| A/B 수정·refresh 후 컴파일 준비 | 554.4164 | 76.9129 |
| A checkpoint | 5,723.6083 | 83.5010 |
| A release | 1,662.4729 | 130.5857 |
| B checkpoint | 2,914.6685 | 81.6405 |
| B release | 1,163.3742 | 125.6494 |
| A/B 보존·회수 구간 | 9,367.0703 | 421.3806 |
| 원본 Library 복원 | 1,473.2436 | 1,404.9756 |

Library 준비부터 복원까지 관측한 전체 구간은 **4,107.9591초 (68.47분)**다. 에셋 검사 2.4186초, 종료, 진행 절차와 보존한 종료 시간 제한·관측을 포함한다. 최종 독립 회수 감사는 이 구간 뒤에 수행한다. 정확한 비용과 바이너리 hash는 [데이터](../../../reports/benchmarks/unity-lifecycle-remeasurement-20261007.json)에 있다.

![기록한 Unity 시간 비교](../../images/unity-lifecycle-remeasurement.png)

이전 **2시간 36분 7초** 보존 구간에는 B와의 중첩, 외부 B 소스 commit, 보존된 거부·복구와 동시 진단이 포함된다. 현재 구간은 A checkpoint/release 후 B checkpoint/release를 순차 실행했다. 실제 대기 비용을 기록한 비교이며 **최적화만의 인과적 배율 실험은 아니다**. 내부 소스·seal 시간을 명령 총시간에 다시 더하지 않는다. 준비, 에셋 검사, Editor 종료, 마지막 Library 복원은 보존·회수와 별도다.

정상 종료 명령 두 개는 약 120초 뒤 `EditorStopTimeout`을 반환했다. 같은 PID/시작 시각을 계속 관측해 **278.3213초** 뒤 정상 종료를 확인했다. stop 반복·강제 종료·Editor 재시작 없이 같은 작업에서 보존을 이어갔다. 실패 응답도 근거로 유지하며, 이 입력에서 2분 제한의 종료 명령 자체가 성공했다고 주장하지 않는다.

## 남은 비용과 개선 후보 — UR-03

| 명령 | 전체, 초 | 소스 검사, 횟수 / 초 | 외부 seal, 횟수 / 초 | delta 회수 구간, 초 |
| --- | ---: | ---: | ---: | ---: |
| s-a-checkpoint | 83.5010 | 2 / 25.4668 | 2 / 43.9624 | — |
| s-a-release | 130.5857 | 1 / 12.8073 | 3 / 55.2250 | 53.6194 |
| s-b-checkpoint | 81.6405 | 2 / 25.0146 | 2 / 42.7075 | — |
| s-b-release | 125.6494 | 1 / 12.4218 | 3 / 53.6273 | 53.3478 |

delta 회수 구간은 durable `release-delta`와 `release-snapshot` event 사이이며 내부 seal을 포함한다. 외부 seal 3회의 계측과 별도다.

소스 검사는 새 checkpoint의 2회 검사, release의 1회 검사 경로를 사용한다. checkpoint는 바뀐 소스 한 파일만 해시하며 이후 검사에서는 내용 hash 1,602개를 모두 재사용한다. payload hash 입력이 0이어도 소스 메타데이터와 Git 보호 검사 비용은 남는다.

외부 `dotnet-trace`로 각 library/workspace CLI의 첫 60초, A checkpoint provider, 초기 읽기 한 구간과 B release 후반 한 구간을 샘플링했다. 추가한 A 작업공간 종료 구간은 A checkpoint 초반과 겹치므로 순수 종료 프로파일로 해석하지 않는다. Library 복원에도 후반 구간이 있다. 계측 부하는 실측 시간에 포함한다. 샘플에는 여러 스레드의 실행·대기가 겹친다. 누적 체류는 CPU 시간이나 합산 가능한 벽시계 시간이 아니며, 제한된 구간으로 명령 전체의 원인 비율을 계산하지 않는다.

1. **경로 검증 반복은 관측된 checkpoint/release 개선 대상이다.** B release 후반 구간에서 `WorkspaceStore.EnsurePlainPath`의 누적 샘플 체류는 약 **41초**다. seal 생성과 회수에서 파일마다 상위 경로를 반복 확인한다. 연산 범위의 부모·root identity 증명으로 공유할 수 있으나, 문자열만 캐시하면 경로 대체·reparse 보호가 약해진다. 고정 identity, 구성원 검사와 재시작 검증을 유지해야 한다. [경로 검사](../../../src/Splinter.Runtime/WorkspaceStore.cs), [seal 생성](../../../src/Splinter.Runtime/LatestJournalStorage.cs), [회수](../../../src/Splinter.Runtime/LatestJournalReclamation.cs)를 참고한다.
2. **seal 안의 읽기 통합도 가능하지만 payload 해시만의 문제는 아니다.** seal마다 capsule을 재구성·검증하고 물리 파일을 다시 읽어 composite digest를 만든다. 회수에서도 파일 해시가 여러 경계에 반복된다. 한 연산 안에서 읽기를 통합하되 정확한 seal framing, frame별 checksum, 전후·unmount 후 경계, 마지막 head/구성원/pending 검사와 같은 intent 복구를 유지해야 한다. 현재 fence는 delete sharing을 허용하므로 조건부 파일 identity 보호 없이 삭제 직전 검사를 없애면 안 된다.
3. **초기 로딩에는 별도의 읽기·잠금 병목이 있다.** A provider의 60.02초 구간에서 `GitObjectSnapshotTree.Read`는 약 **59.91초**, Monitor 진입은 **433.15 스레드·초**가 잡혔으며 주로 파일 정보·읽기·열기 callback이다. 코드는 기준본 읽기 동안 저널 gate를 잡는다. 누락·인라인 frame 때문에 정확한 Monitor 객체를 trace만으로 단정할 수는 없다. Git reader는 한 객체의 증명만 보관하므로 A→B→A 읽기에서 A 전체를 다시 증명할 수 있고, 역방향 읽기는 앞부분부터 다시 decode할 수 있다. 제한된 메타데이터 전용 proof LRU가 첫 후보이며 제한된 reader 세션과 별도 읽기 계획·gate 설계가 뒤따른다. miss의 전체 blob 증명, 재읽기의 chunk 검사, 손상 시 무게시, disk hydration 금지를 유지한다. 반환한 논리 `readBytes`로 decode 증폭량을 계산할 수 없으므로 proof/prefix/restart 계수가 필요하다. [Git reader](../../../src/Splinter.SourceControl.Git/GitObjectSnapshotTree.cs)와 [저널 읽기 gate](../../../src/Splinter.Runtime/JournalDeltaStore.cs)를 참고한다.
4. **변경 없는 원본 Library도 일반 파일별 복원 경로를 탄다.** 원본 provider의 durable mutation은 0이지만 전체 파일 계획을 만들고 파일별 이동·검증과 durable cursor를 수행한다. 변경 없는 파일도 코드상 전체 payload를 여러 번 읽는다. 원본 delta가 비었음을 증명한 경우 소유 디렉터리 전체를 같은 볼륨에서 이동해 복원하는 경로가 가능하다. 정확한 root/target identity, dependent/reader 부재, 전후·unmount seal, 외부 파일 없는 대상, durable intent와 중단 복구를 유지해야 한다. 이는 코드로 확인한 개선 후보이며 이번 측정에서 구현·검증하지 않았다. [복원](../../../src/Splinter.Platform.Windows/ManagedDirectoryRestore.cs), [기준본 수명](../../../src/Splinter.Platform.Windows/ManagedDirectoryBasis.cs)을 참고한다.

이번 측정에서는 추가 제품 최적화를 구현하지 않았다.

heartbeat sequence가 0이라는 사실만으로 디렉터리 이동 경로를 허용할 수는 없다. 완전히 검증한 stopped journal과 하위 항목의 identity·내용 증명이 필요하며 root identity만으로 충분하지 않다. 논리 mutation 없이 물리 last-access 메타데이터가 바뀔 수도 있다. O(N) 검증·메타데이터 순회는 남을 수 있다. 이미 시작한 일반 복원 intent를 새 빠른 경로로 바꾸면 안 된다.

일반 복원 경로의 더 작은 대안은 복구 가능한 진행 cursor를 제한된 묶음으로 게시하는 것이다. 기존 복구는 파일 이동 뒤 cursor가 전진하기 전 중단을 이미 처리한다. 128개씩 묶으면 약 42,420회 게시가 332회로 줄지만, 중단 뒤 완료 파일 최대 127개를 추가 검증한다. 신규 파일 소유 영수증, 범위 데이터 flush, 최종 검증과 보존 바이트·identity guard는 유지해야 한다. 이는 게시 횟수의 설계상 계산이며 실측 속도 향상이 아니다. 채택 전 중단 시험이 필요하다. 복원 후반 프로파일에서는 반복 경로 검사와 durable flush가 모두 관측됐다.

## 회수 완료와 한계 — UR-04

두 작업은 `released`이며 독립 결과 tree가 과거 Codex A/B 결과와 정확히 일치한다. 각 작업은 의도한 소스 한 파일만 바꾼다. 소유 Editor/provider 프로세스·mount·handle·메모리·delta·Git 작업공간 등록을 회수했다. 원본 소스 1,602개의 hash, HEAD/branch와 기존 ref는 유지됐다. 원본 Library를 같은 볼륨의 일반 NTFS로 복원했고 임시 local Git 설정을 제거했으며 전역 설정은 그대로다. UAC·권한 상승·서비스 변경은 없었다.

고정 배포본의 응답 schema는 실제 CLI 응답 12개 중 8개를 허용하고 checkpoint/release 응답 4개를 거부했다. 최종 unlinked close가 이미 `forget-unlinked`를 출력하지만 두 계측 schema가 연산을 6개로 제한하고 해당 enum을 누락했기 때문이다. 측정 뒤 두 schema가 기존 연산 7개를 허용하도록 수정했다. 실제 저널 회귀 시험으로 7개 연산 모두의 유효성과 unknown 이름 거부를 확인했다. 종료 실패 두 개를 포함한 기록 응답 12개는 수정된 schema를 통과한다. 측정 바이너리는 그대로이며 성능 변경과 구분되는 계약 수정이다.

앞서 완료한 필수 빌드는 고정 제품 후보에서 **통과 1,203개, 실패 0개, 건너뜀 19개**를 기록했다. 별도 schema 수정 뒤 `pwsh ./scripts/build.ps1`은 **통과 1,204개, 실패 0개, 건너뜀 19개**, 빌드 경고·오류 0개다. 기록 응답 검증기도 응답 12개 모두를 통과했다. 고정 배포본의 실제 실행 근거와 측정 뒤 계약 회귀의 자격을 구분한다. 후보별 한 묶음이고 cache 초기화·통계적 구간·일반 속도 보장은 없다. **새 Unity Play Mode·Unity 테스트·게임 빌드·물리 저장 할당·RAM/전체 디스크 peak·서비스/UAC 작업·Codex 재실행은 NotRun**이다. 이전 공간 절감은 역사적 근거이며 새 할당량 측정이 아니다.
