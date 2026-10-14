# RAM·준비 최적화 후 실제 Unity 전체 재측정

[English](../../verification/unity-time-memory.md)

**UT-01**은 `7630970`에서 이전 UF와 같은 실제 Unity 작업을 다시 수행했다. 전체 시간은 **14m 35.2s**, 이전 **18m 03.0s** 대비 **19.19%** 단축이다. [정확한 데이터](../../../reports/benchmarks/unity-time-memory-20261014.json).

Unity 6000.3.22f1, Git `b09fa58`, 추적 소스 1,602개·2,804,772,822바이트, Library 44,860항목·42,420파일·3,141,660,252바이트다. 원본 소스 hash와 최초 RB Library의 내용·메타데이터가 모두 같다. 에셋은 줄이지 않았다. A/B는 이전과 같은 각 한 줄을 수정하고 정확한 project/PID/start, 새 generation/MVID, 성공한 refresh 요청과 예상 값으로 컴파일을 확인했다.

| 단계 | 이전 UF | 현재 UT | 관측 단축률 |
| --- | ---: | ---: | ---: |
| Library 기준본 준비 | 1m 55.9s | 1m 06.6s | 42.52% |
| A/B 작업공간 생성 묶음 | 5m 32.7s | 2m 32.9s | 54.06% |
| Editor 시작 → 초기 컴파일 준비 | 3m 36.1s | 4m 02.7s | -12.35% |
| A/B 한 줄 편집 → 재컴파일 완료 | 0m 29.9s | 0m 48.1s | -60.91% |
| 선택 에셋 확인 | 0m 02.4s | 0m 01.4s | 41.54% |
| A/B 정상 종료 | 1m 27.9s | 1m 45.5s | -20.07% |
| A/B checkpoint + release | 2m 52.0s | 3m 22.7s | -17.85% |
| 원본 Library 복원 | 2m 06.0s | 0m 55.0s | 56.31% |
| 전체 작업 | 18m 03.0s | 14m 35.2s | 19.19% |

A/B 생성 개별 시간은 151.244s / 152.851s지만 서로 겹치므로 합산하지 않는다. 이전은 순차, 현재는 병렬이다. 이 비교는 RAM cache·Library 경로 검증·병렬 호출을 함께 적용한 결과이며 각 변경의 인과적 절감률을 분리하지 않는다. 준비·Editor 종료·원본 복원은 checkpoint/release와 별도 구간이다.

이번 Editor 준비·재컴파일·종료는 이전보다 길었다. A 로그의 초기 script compilation은 103.350s → 128.857s, 초기 Tundra build는 55.75s → 74.21s, 편집 후 Tundra build는 9.74s → 19.14s다. 이 값은 관측된 느려진 구간이며, 단일 실행만으로 cache 변경이나 OS 상태가 원인이라고 단정하지 않는다.

| Operation | UF | UT |
| --- | ---: | ---: |
| A checkpoint | 37.671s | 44.592s |
| A release | 49.392s | 57.716s |
| B checkpoint | 37.371s | 43.893s |
| B release | 47.585s | 56.520s |

최초 RB의 편집·컴파일·Editor 종료 후 보존/회수는 156m 07.1s → 3m 22.7s, 원본 복원은 24m 33.2s → 0m 55.0s다. 최초 개별 명령은 일부 겹치고 진단·복구·B 외부 commit을 포함하므로 합산하거나 최초의 전체 준비 시간을 추정하지 않는다.

원본 1,602파일 재검증, 기존 HEAD/branch/ref·전역 설정 유지, 임시 local Git 설정 복원, 독립 결과 tree의 과거 예상값 일치, 소유 Editor/provider 종료, mount/delta/Git 등록 회수와 일반 NTFS Library 복원을 확인했다. CLI 응답 12개가 현재 schema를 통과했다. 같은 고정 runtime의 전체 파일 hash를 전후 확인했다. 단일 표본이며 OS cache를 비우거나 순서를 무작위화하지 않았다. 추가 Play/Test/build·서비스/VSS 변경·전체 RAM peak·저장 할당 비교는 수행하지 않았다. 이전의 프로젝트 검증 fixture와 설치된 의존성을 그대로 사용했으며 선택적 외부 도구의 새 자격을 주장하지 않는다.
