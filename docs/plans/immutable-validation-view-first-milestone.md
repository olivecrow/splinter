# SF-03 불변 후보와 고정 검증 view 첫 범위

2026-09-26 확정. BQ의 다음 범위를 진행한다. 작업 복사본은 용량뿐 아니라 생성부터 편집 가능까지 걸리는 시간도 평가한다. 첫 준비, 반복 재개, 후보 게시, Unity 준비, A/B/A 전환을 합쳐서 숨기지 않는다.

2026-09-27의 [실행 기록](../verification/immutable-validation-view.md)에서 정의한 개발용 범위를 `scoped-complete-with-limitations`로 마감했다. 첫 import 수정·강제 종료 후 재개, 첫 A의 빈 미리보기, 반복 mount 준비 재확인과 미계측 Git 기준을 유지한다. 전체 SF-03이나 정상 cold import/전체 렌더 지원의 완료가 아니다.

## 구현과 완료 기준

| ID | 범위 | 완료 기준 |
| --- | --- | --- |
| VC-01 | Editor 없는 작업의 불변 범위 후보 | 원본 payload를 복제하지 않고 저널의 유효 범위와 메타데이터를 별도 게시한다. 작업 재편집 뒤에도 후보는 변하지 않는다. 손상·동시 writer·미완료 게시를 거부한다. checkpoint와 별도다. |
| VC-02 | 고정 검증 경로와 캐시 분리 | 후보별 소스 쓰기 저널과 공통 캐시 저널을 분리한다. Editor와 mount를 정상 종료한 뒤에만 다음 세대를 마운트한다. 이전 소스 출력은 보존한다. 캐시의 단일 writer를 강제하고 재연결 시 후보 identity를 확인한다. |
| VC-03 | 작은 A/B/A | 합성 파일의 추가·삭제·rename·truncate·.meta와 작은 Unity fixture의 실제 loaded code 및 에셋을 확인한다. 작업자 Editor 0, 검증 Editor 동시 최대 1. 같은 경로·Library에서 A/B/A를 실행한다. 원본과 후보 및 독립 결과를 보존한다. |
| VC-04 | 시간·용량·회수 | worker 준비/후보 게시/첫 import/반복 mount replay/Editor 재시작·ready/검증/보존·회수 시간을 나눈다. 후보·작업자·검증 소스·Library·기타 캐시·증거·공유 VSS를 구분한다. 논리 길이를 물리 할당으로 환산하지 않는다. |

기존 snapshot-overlay 실험 진입점을 확장한 개발용 spike로 검증한다. 제품 workspace의 기본 실행 방식을 바꾸거나 실험을 제품 capability로 표시하지 않는다. Core의 공통 후보 계약과 fake 시험을 먼저 연결한다. 최초 전체 복사·읽기 hydration·파일 전체 copy-up은 금지한다. 소스 변경이 없는 대형 파일을 후보 게시 때 읽지 않는다. 후보와 검증 출력은 snapshot에 의존하므로 독립 결과 확인 전 회수하지 않는다.

첫 전환 방식은 **정상 Editor 종료 → 열린 핸들 0 및 provider 종료 → 같은 경로에 다음 후보 mount → Editor 재시작**이다. 열린 Editor 아래의 root 교체를 구현하지 않는다. 재시작 비용을 반복 전환 시간에 포함한다. 이전 소스 출력 journal은 세대별로 유지하며 다음 후보의 소스와 혼합하지 않는다. Unity가 생성한 소스의 작업자 반영·충돌 병합, 실행 중 hot switch, 큐/Coordination 연결, package/Unity 버전 간 cache 재사용은 후속 범위다.

실제 첫 import에서 드러난 소스/캐시 경계 rename에는 원본 범위가 없는 새 private 파일의 유효 extent만 전달하는 최소 보완을 포함한다. 파일 전체 복사 fallback은 금지하며 지속 intent·같은 세대 재연결 복구·열린 ID 보존과 중단 지점 시험을 완료 기준에 포함한다. 최초 거부·정상 stop timeout·소유 Editor 종료와 같은 Library 재개는 운영 개입으로 기록한다.

이미 확보한 작은 fixture와 격리 NTFS 시험 볼륨만 사용한다. 큰 PV와 canonical Editor/MCP는 재개하지 않는다. 새 시험의 소유 자원만 회수한다. SF-03 전체(일반 임대 중단 복구·소스 반환 포함), SF-04~06, flush 정책, 온라인 정리, 기본 모드 채택은 이 첫 단위의 성공으로 완료 처리하지 않는다.
