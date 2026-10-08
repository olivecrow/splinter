# 현재 Unity checkpoint·release 재측정

[English](../../verification/unity-checkpoint-release-remeasurement.md)

**UC-01**은 [LO-01–11 최적화](lifecycle-read-optimization.md)를 `168081c349d87d9767d44ce0a741ec61279c2706`으로 커밋한 뒤 수행한 실제 Unity A/B 한 묶음이다. 검증한 일반 Release 바이너리를 실행 전에 고정했다. 소스·Library 입력은 이전 [UR-01–04](unity-lifecycle-remeasurement.md)와 같다. 원시는 ignored `artifacts/unity-checkpoint-release-20261008/`·`.local/uc8/`, 정확한 시간·hash·자격 경계는 [공개 데이터](../../../reports/benchmarks/unity-checkpoint-release-20261008.json)에 보존한다.

## 같은 입력과 한정 절차

Unity **6000.3.22f1**, Git 소스 `b09fa58e9d40a560d4fe1af7a213f8ac23e8e3b1`, **추적 파일 1,602개 / 2,804,772,822바이트**, `git-snapshot`, 기본 SJL4와 원본 Library 기준본이다. **Library 파일 42,420개 / 디렉터리 2,440개 / 3,141,660,252바이트**의 manifest 메타데이터와 payload hash가 모두 UR과 정확히 같다. 기준본 소유권·경로·캡처 필드만 다르다.

A는 `value + 1`을 `value + 2`로, B는 `value + value`를 `value + value + value`로 바꾼다. 기존 외부 fixture가 정확한 project/PID/시작 identity, 최신 비컴파일·비업데이트 상태, 요청이 일치하는 오류 없는 refresh, 기대 코드 값과 assembly MVID/generation 변경을 확인한다. 설치 패키지와 probe는 바꾸지 않았다. ensure 두 개는 순차, Editor 초기화/refresh는 동시이며, 이후 정상 종료 → A checkpoint/release → B checkpoint/release → Library 복원이다. checkpoint 자체는 컴파일 성공을 주장하지 않는다.

## Checkpoint와 release

아래 시간은 **컴파일과 정상 Editor 종료 뒤** 시작한다. 독립 결과 보존·소유 자원 회수를 포함하고, 준비·Editor 종료·원본 Library 복원은 제외한다.

| 작업 | 이전 UR, 초 | 현재 UC, 초 |
| --- | ---: | ---: |
| A checkpoint | 83.5010 | 66.7514 |
| A release | 130.5857 | 88.2598 |
| B checkpoint | 81.6405 | 65.1359 |
| B release | 125.6494 | 84.7432 |
| A/B checkpoint + release | 421.3806 | 304.8956 |

순차 보존 구간은 **5분 04.9초**, 이전은 **7분 01.4초**로 116.5초(27.64%) 짧게 관측됐다. 후보별 한 묶음이며 cache 제거·무작위 반복은 없다. **UR은 외부 EventPipe 프로파일링을 포함했고 UC는 사용하지 않았다.** 현재는 내장 source/seal/event와 10초 간격 읽기 전용 provider 표본만 남긴다. 따라서 차이를 코드 변경만의 인과적 절감률로 해석하지 않는다. A/B는 독립 검증한 작업 두 개이며 임의의 Unity 프로젝트에 대한 통계 표본이 아니다.

## 별도의 준비·종료·복원 비용

| 단계 | 이전 UR, 초 | 현재 UC, 초 |
| --- | ---: | ---: |
| Library 준비 | 240.3283 | 153.1848 |
| A ensure | 322.3461 | 238.4172 |
| B ensure | 319.4375 | 232.4926 |
| 초기 컴파일 준비 | 1041.8831 | 848.3379 |
| 변경 후 컴파일 준비 | 76.9129 | 65.2544 |
| 정상 Editor 종료 | 278.3213 | 262.0236 |
| Library 복원 | 1404.9756 | 180.9024 |
| 준비부터 복원까지 | 4107.9591 | 2286.9492 |

초기 컴파일 준비는 **14분 08.3초**, 준비부터 복원까지는 **38분 06.9초**다. 초기 준비 비용은 여전히 크며, 파일 검사·import·컴파일을 포함한다. 컴파일러 CPU 시간만 따로 측정한 값은 아니다.

두 `unity stop` 명령은 이번에도 약 **120초에 `EditorStopTimeout`**을 반환했다. 같은 요청을 받은 Editor들은 **262.0236초** 뒤 정상 종료했다. stop 재요청·강제 종료·재시작은 없고 실패 응답을 보존했다. checkpoint/release 네 명령과 최종 Library 복원은 모두 성공했으나, stop 명령의 2분 제한 통과를 뜻하지 않는다.

원본 Library의 durable sequence는 0이었다. 새 디렉터리 계획은 **root/하위 identity 44,860개**를 고정하고, 전체 identity·내용·구성원 검사와 메타데이터 복원 뒤 회수했다. 실제 디렉터리 복원 경로가 사용됐고 **180.9024초(3분 00.9초)**가 걸렸다. 이전은 23분 25.0초였다. 변경 없는 Library 한 번의 실행이며 변경된 Library의 디렉터리 복원이나 OS 전원 장애 복구 자격으로 확대하지 않는다.

## 내장 기록의 비용 구분

| 명령 | 소스 검사, 초 (횟수) | 외부 seal, 초 (횟수) | delta 회수 구간, 초 |
| --- | ---: | ---: | ---: |
| A checkpoint | 25.218 (2) | 27.887 (2) | — |
| A release | 12.393 (1) | 31.912 (3) | 34.215 |
| B checkpoint | 24.484 (2) | 27.280 (2) | — |
| B release | 12.148 (1) | 32.076 (3) | 34.726 |

모두 명령 총시간 안에 포함된 중첩 wall-clock 값이다. 회수는 자체 내부 seal을 포함하며 외부 seal 측정과 구분한다. checkpoint는 변경한 소스 파일만 hash했다(A 143바이트, B 152바이트). release의 소스 payload hash 입력은 0이다. 소스 메타데이터/Git guard는 검사당 약 12초가 남아 있다. 추가 CPU/disk trace나 제품 코드 계측은 넣지 않았다.

## 종료와 근거 한계

두 작업은 `released`이며 독립 결과 트리가 과거 Codex A/B 정답과 정확히 같고 각각 의도한 소스 파일 하나만 바꾼다. 소유 Editor 두 개와 provider 세 개가 PID/시작 identity 기준으로 모두 종료됐다. provider는 unmounted·열린 핸들 0이며 task delta·mount·소유 Git 등록이 없다. 원본 소스 hash·HEAD·branch·기존 ref는 그대로다. Library는 원래 볼륨의 일반 NTFS이고, fixture의 임시 local Git 설정은 복원했으며 전역 설정은 바꾸지 않았다.

stop 실패 두 건을 포함한 **실제 CLI 응답 12개**가 기존 validator에서 현재 schema를 모두 통과했다. 측정 전 필수 검증은 **통과 1,273개·실패 0개·조건부 제외 19개**이며 고정 바이너리는 변경되지 않았다. 기존 다른 작업의 미커밋 변경도 보존했다. 새 Unity Play Mode·Unity tests·게임 빌드·Codex 실행·할당량/RAM/전체 디스크 peak·UAC/서비스 변경·OS 장애 행렬은 수행하지 않았다. 과거 저장 공간 절감 값은 과거 근거로 유지한다.
