# Replay index 비용 검증

`ri-20260927`의 [RI-01~04](../plans/replay-index-cost.md)를 **scoped-complete-with-limitations**로 마감했다. 연산별 index 계측과 rename 접두사 재사용을 구현했고 자동 **300 통과·0 실패·선택적 ReFS 1 skip**, 공통 검사 바이너리의 합성 **27회 출력 일치·입력 보존**을 확인했다. 기존 VR raw journal은 회수했으므로 이번 수치를 실제 Unity 재연결 개선으로 해석하지 않는다.

## 후보와 입력

기준은 `d48b3b067ac90aa94b50a030dd07e4bbe0877de9`다. `baseline`은 해당 Core/Runtime, `profiled`는 연산별 계측만 추가한 Runtime, `current`는 계측과 rename 접두사 재사용을 추가한 작업 트리 후보다. 작업 저장소나 Unity Library를 복사하지 않고 각 비교용 DLL/JSON 5개만 준비했다. Windows 10.0.26200 x64, .NET SDK 10.0.112, 논리 CPU 12개에서 순차 실행했다. 선택적 CPU 모델 CIM 조회는 권한 거부여서 unknown으로 남겼다.

최종 비교의 모든 `Splinter.ReplayProbe.dll` SHA-256은 `D86947A44A5ED7620374CC82E3A146B8B3B0E8765D7B8E5A8963F86F2919E85B`다. 최종 Runtime SHA-256은 `4948624B8F45FB57AA08CF1EF14303C14875D4B19E60658223EE2C1AB91A6B89`이며 전체 바이너리·소스 manifest를 보존했다. 전체 빌드가 probe도 다시 생성한 점을 발견해 앞선 27회는 예비 결과로 보존하고 공통 검사 DLL로 아래 27회를 별도 실행했다.

| 입력 | 내용 | frame 수 | journal 논리 B | 내부 생성 초 |
| --- | --- | ---: | ---: | ---: |
| append | 8,192회 순차 4KiB 쓰기 | 8,194 | 36,505,896 | 0.4723 |
| overlap | 초기 2,048회 4KiB + 6,144회 겹치는 1KiB 쓰기, 축소/확장 | 8,196 | 17,619,290 | 0.1779 |
| file-renames | 빈 파일 35,000개, rename 4,032회 | 39,033 | 13,844,139 | 0.2430 |

VR의 파일/rename 규모를 참고했지만 실제 Unity 저널의 재현은 아니다. journal 합계는 67,969,325B, 영수증·identity까지 포함한 입력 논리 합계는 73,553,859B로 256MiB 한도 안이다. 내부 생성 시간은 journal·기대 내용 hash 준비까지이며 영수증 게시와 프로세스 시작을 제외한다. 고정 데이터는 실제 파일 내용과 다른 독립 기대 모델로 검증한다.

입력 journal SHA-256:

- append: `cd5c877b6c554b1e4f4c60028b86a1a5a79a3b24d029ad2d664d4fe77e40cae9`
- overlap: `8e0fece7ff103a20b1ebf990795f791b1b1fdd76546c32eb53a5c76922081591`
- file-renames: `bb90110b9e409afef1c46ed01c1fb2bcb9ec08999b0fc08fb7e7511a928ba9e1`

## 반복 시간과 메모리

각 입력에서 1회차는 baseline/profiled/current, 2회차는 profiled/current/baseline, 3회차는 current/baseline/profiled 순서다. 매번 별도 프로세스이며 build/test와 겹쳐 실행하지 않았다. run 직전 전체 hash를 읽으므로 warm OS cache이고 cache를 비우지 않았다. 3회 표본이며 JIT·GC·스케줄링·전력 상태를 고정하지 않았다.

| 입력 / 구현 | replay 중앙값 초 (최소~최대) | index 중앙값 초 | 누적 관리 할당 중앙값 B | 프로세스 peak working set 중앙값 B |
| --- | ---: | ---: | ---: | ---: |
| append / baseline | 1.4001 (0.9843~1.4022) | 1.2930 | 283,832,872 | 63,496,192 |
| append / profiled | 1.3903 (0.9743~1.4032) | 1.2769 | 283,843,608 | 63,569,920 |
| append / current | 1.4056 (0.9721~1.4105) | 1.2935 | 283,909,096 | 63,332,352 |
| overlap / baseline | 1.8948 (1.8861~1.9235) | 1.8109 | 840,737,488 | 65,384,448 |
| overlap / profiled | 1.8691 (1.4977~1.8901) | 1.7824 | 840,726,888 | 65,343,488 |
| overlap / current | 1.9035 (1.8669~2.0378) | 1.8170 | 840,778,000 | 65,679,360 |
| file-renames / baseline | 3.7077 (3.5239~3.7543) | 3.3447 | 9,635,552,136 | 112,164,864 |
| file-renames / profiled | 3.8709 (3.6103~3.9529) | 3.4674 | 9,635,551,424 | 112,119,808 |
| file-renames / current | 2.4062 (2.3920~2.4104) | 2.0162 | 84,087,952 | 108,072,960 |

rename는 index 적용 대부분을 차지했고 연산별 중앙값은 profiled 3.4263초 → current 1.9923초다. baseline의 연산별 계수는 없으므로 unknown이다. 기존 루프가 각 경로 검사마다 같은 `old + "/"` 문자열을 생성했던 부분을 rename당 한 번으로 옮겼다. 경로·whiteout의 기존 순회, `OrdinalIgnoreCase`와 경계 의미는 유지했다. 지속 index·별도 디스크 cache는 추가하지 않았다.

최종 rename 합성 replay 중앙값은 baseline 대비 약 35.1% 감소했고 누적 관리 할당은 약 99.1% 감소했다. 이는 동시에 9.64GB를 점유했다는 뜻이 아니다. 동시 working set 관측은 약 112.2MB → 108.1MB이며 디스크 절감량도 아니다. 예비 27회의 rename 중앙값은 3.5039초 → 2.9043초였으므로 특정 개선률을 일반화하지 않는다. 범위 쓰기 2개에는 최적화를 넣지 않았고 이번 최종 표에서도 별도 개선 주장을 하지 않는다.

rename의 store 생성 중앙값은 baseline 3.7168초 → current 2.4156초, hash·내용/상태 검증·프로세스 시작/종료를 포함한 CLI 중앙값은 4.3280초 → 2.9609초다. replay 수치만 작업 전체 준비 시간으로 사용하지 않는다. 실제 mount/Editor ready·stop·첫 import 시간은 이번에 측정하지 않았다.

## 저장량과 보존

동일 FILE_STANDARD_INFO 계측 코드를 offline root 목록으로 제한했다. VSS 조회·snapshot·mount를 생성하지 않았다. 생성 직전 전체 공간 기준과 순간 peak는 없으므로 추가 물리 공간의 전역 최고치를 주장하지 않는다.

| 측정 root | 논리 B | 파일 할당 B |
| --- | ---: | ---: |
| 공통 입력 9개 파일 | 73,553,859 | 73,565,248 |
| baseline 실행 묶음 | 343,638 | 352,600 |
| profiled 실행 묶음 | 346,710 | 356,696 |
| current 실행 묶음 | 346,710 | 356,696 |
| 첫 거부 시험 입력 / 수정한 거부 시험 입력 | 2,109 / 3,679 | 8,560 / 13,360 |
| 측정 증거, qualified 표본 시점 | 715,704 | 870,696 |
| 새 probe의 bin / obj | 601,214 / 329,395 | 618,840 / 353,712 |

공통 입력은 전후 파일 할당이 같았고 27회 모두 journal hash·기대 내용·내부 state hash가 각 입력의 세 구현에서 일치했다. 총 3개 실행 묶음의 할당은 1,065,992B다. profiled 묶음 복사는 0.3062초, current는 0.2315초였으며 baseline의 초기 Core/Runtime 2개 복사만 0.0099초로 계측했다. baseline에 probe를 추가한 시간과 최종 공통 검사 DLL 교체 시간은 unknown이므로 전체 준비 시간으로 합산하지 않는다.

표는 MFT/디렉터리·OS cache·기존 저장소 build/restore cache·루트 밖 로그와 측정 스크립트 파일을 포함하지 않는다. 해당 표본 자신과 이후 문서 검사 자료도 표본에 포함되지 않으므로 최종 디렉터리 총량과 구별한다. 입력·바이너리·원시 증거를 보존하고 자동 정리하지 않았다. Unity/VSS 자원은 이번 범위에서 새로 만들지 않았다.

## 판정과 재현

| ID | 판정 | 근거 |
| --- | --- | --- |
| RI-01 | Passed | 9개 연산 enum·nullable 계약, frame/index 합계, 신규 쓰기 제외, 반환 배열 독립성, CLI/schema 회귀 |
| RI-02 | Passed | 3개 한정 입력, 독립 내용/hash 검증, 생성·파일 할당·메모리 분리, 4개 거부 경로의 입력 보존 |
| RI-03 | Passed | prefix 재사용만 변경. 경계 형제·lazy child·삭제 하위 경로·비ASCII·case-only rename, 열린 ID와 원본 byte, 재생 회귀 |
| RI-04 | Passed | 최종 동일 검사 DLL·27회 반복과 동일 state, 300 자동 통과, locked restore·build·문서 검사 |
| 실제 Unity 재연결 / 첫 렌더 / 대형 PV | NotRun | 이번 RI 후보의 실제 소스 journal·Editor 실행은 하지 않음. 이전 VR-03 Partial을 유지 |

`pwsh ./scripts/build.ps1`에서 nullable 시험 코드와 capability schema 누락을 각각 수정한 뒤 최종 300/0/1, build 경고/오류 0을 확인했다. 거부 시험의 첫 영수증 재직렬화가 시각 offset을 로컬로 바꿔 identity 단계에서 먼저 차단됐으므로 그 입력도 보존했다. 고정 offset을 유지한 별도 입력에서 덮어쓰기·잘못된 identity·미완료 tail·checksum 손상의 4개 거부와 무변경을 확인했다. 제품 identity/저널 계약을 바꿔 통과시키지 않았다.

재현 사용법은 [offline replay 계측](../replay-profiling.md)이다. `artifacts/storage-foundation/ri-20260927/`에 다음 증거를 남겼다.

- `evidence/build-qualified.log`, `tests-qualified.trx`, `source-manifest.json`, `environment.json`.
- `evidence/bundles-qualified.json`, `qualified-run-*.json`, `qualified-matrix.json`, `qualified-summary.json`, `common-harness-qualification.json`.
- `evidence/generate-*.json`, `profiled-*.json`, `guards.json`, `guard-harness-repair.json`, `space-before-matrix.json`, `space-qualified.json`.
- `run-matrix-qualified.ps1`, `run-guards.ps1`, `measure-space.ps1`와 모든 입력/비교 바이너리. 예비 비교의 `run-*.json`, `matrix.json`, `summary.json`, `bundles.json`도 보존했다.

남은 시간 개선 후보는 범위 extent의 반복 copy/sort 비용과 실제 작은 Unity의 재연결·첫 렌더 검증이다. 이번 구현을 기본 모드·Coordination 연결·flush 변경·대형 Unity 지원으로 확대하지 않는다.
