# 검증 안내와 보존 근거

## 현재 변경의 검증

표준 진입점은 `pwsh ./scripts/build.ps1`이다. locked restore, Release 빌드, 자동 시험을 수행하며 결과는 `artifacts/test-results/`에 기록한다. 선택적 ReFS는 `SPLINTER_TEST_REFS_ROOT`를 지정한 경우에만 실행한다. 현재 계약은 VSS v2 capture identity, 완전한 overlay 검증 tuple, `stable-journal-v1` 결과, 필수 계측 필드를 요구한다. 지원하지 않는 형식은 자동 이관하지 않고 보존 후 거부한다.

자동 시험은 현재 형식의 생성·읽기, 불완전한 입력 거부, checkpoint 중단 후 재시도, 소유권 불명·snapshot 소실·응답 유실에서의 보존을 다룬다. 개발 스크립트의 요청 생성 시험은 합성 영수증과 파일만 사용한다. 실제 VSS·WinFsp mount·Unity 실행을 대신하지 않는다.

## 지원 경계와 남은 관문

| 영역 | 확인한 범위 | 남은 작업·해석 제한 |
| --- | --- | --- |
| NI/LV 제품 NTFS | 작은 fixture의 VSS·Git·Windows x64 Unity·재연결·독립 결과·회수 | NR 전체 지원은 미완료. 실제 재부팅·전원·물리 공간 장애와 모든 명령 경계는 별도 |
| NR | 상태/저널 손상 보호, 파일 속성 거부, 현재 boot의 live identity, ARM 단일 workspace | 나머지 파일·에셋 경계, ARM A/B와 실제 장애는 미완료 |
| PV-01/02 | 대표 fixture와 사용자 선택 비교 범위는 scoped-complete-with-limitations | PV-02 전체 Passed 아님. S4 FailedInputQualification, 소수 표본, 전체 팩 raw 입력 동등성 unknown |
| SF-01/02 | 변경 기반 소스 검증, 명시적 정리·중첩 예산·계측의 하위 범위 | 공통 저장소 최초 실행의 FailedPreservation은 유지. SF-02 전체 Partial, 대형 용량 목표 미달성 |
| SF-03/VC/VR | 개발용 작은 A/B/A, 정상 cold import와 재연결 | SF-03 전체 Partial. 큐·일반 임대 복구·source 반환 병합 미구현. VR-03 Partial. 현재 제품 연결은 WF로 구분 |
| SF-03/VW | [공통 수명주기](../validation-workflow.md)의 지속 상태·영수증 재조정·fake 적합성 | 합성 파일/영수증이며 실제 VSS·WinFsp·Unity 지원 증거가 아님. 자동 실행 상태는 VW, 제품 연결 자격은 WF를 따름 |
| SF-03/VG | [등록 증거가 있는 관리 파일 분리](../validation-view.md#등록-증거가-있는-관리-파일-분리)의 실제 임시 Git 등록·합성 불변 트리 적합성 | 후보 v2 게시의 자동 시험 범위. 실제 provider 실행은 WF의 현재 후보 증거로 별도 판정 |
| SF-03/VP | [같은 요청 게시 복구](../validation-view.md#같은-요청의-게시-복구)의 영구 intent·부분 저널 prefix·완료 receipt 재조정 | 합성 트리·실제 임시 파일·예외 주입 범위. 실제 CLI 중단은 WF, 재부팅·전원/공간 장애는 NR의 별도 관문 |
| SF-03/WF | [제품 연결·복구](product-validation-workflow.md)와 [최종 후보의 두 빈 cache A/B/A·전체 비용](product-validation-cold-cost.md), 독립 결과·회수 | scoped-complete-with-limitations. 준비 종료 후 검증 재시작 비용을 포함한 작은 fixture 범위. 이전 실패·복구를 보존하며 대형 용량·일반 cold 안정성·NR 전체를 증명하지 않음 |
| RI/RU/WI | 합성 또는 동일 닫힌 저널의 제한된 replay 비교 | warm cache·소수 표본. 누적 Editor 전체 개선·Library 물리 절감으로 환산하지 않음 |
| JW/FR | 고정 쓰기 계측과 작은 URP fixture의 요청별 유효 PNG 검사 | Windows 선행 직렬화·내구성 경계 유지. Unity 내부 첫 draw 원인 미확정, group flush 미선정 |
| SF-04~06, SW-01~10, UA-01~04 | 계획 | NotRun. 대형 입력·모드 채택·설치/binding/discard·권한 서비스 미구현 |

새 후보에서 필요한 실제 환경 시험을 실행하기 전에는 아래 기록을 현재 후보의 통과로 승계하지 않는다. GUI Unity·VSS·격리 디스크 시험은 정의된 범위와 소유 자원을 확인한 뒤 실행한다. 현재 원본·다른 작업·기존 결과 refs와 비소유 snapshot을 보존한다.

## 후속 작업에 필요한 기록

아래 자료는 변경 이력이 아니라 재현 조건·실패 근거·비교 기준으로 보존한다. 각 파일의 후보·입력·날짜와 제한을 함께 읽는다. 원시 artifacts는 로컬에 있을 수 있으며 문서 정리로 지우지 않는다.

| 기록 | 보존 이유 |
| --- | --- |
| [VSS 보존 실패](storage-foundation.md), [삭제 차단과 복구](storage-recovery.md), [지연 회수](deferred-snapshot-reclamation.md) | exact-ID 삭제의 타 snapshot 영향, 격리 guard, 같은 작업의 명시적 재시도와 회수 순서 |
| [NTFS 제품 통합](ntfs-workspace-integration.md), [live identity](ntfs-live-vss-identity.md), [NR 복구](ntfs-recovery.md) | 실제 Windows/ARM 조합과 미완료 장애·파일 관문, 환경별 재현 조건 |
| [병렬 비교](parallel-workflow.md) | C/S/Q의 동일 입력 조건·결과 refs·Library/큐/전환 비용, S4 입력 실패와 표본 한계 |
| [초기 overlay 시험](snapshot-overlay.md), [ReFS·프로세스](storage-and-resources.md) | 파일 API·소유권·선택적 backend의 제한된 실환경 증거. NTFS 전체 지원으로 합산하지 않음 |
| [저널 정리](journal-compaction.md), [Unity와 정리 비용](journal-compaction-qualification-and-cost.md), [예산과 재증가](journal-budget-and-growth-qualification.md) | 중단 복구, 두 세대 겹침·전체 할당 peak와 Library 유효량, 재개 후 재증가 |
| [공통 Library A/B/A](immutable-validation-view.md), [준비와 replay](validation-view-readiness-and-replay.md) | 불변 후보와 검증 세대의 분리, 정상 stop/remount/restart 비용·최초 import 제한 |
| [제품 validation workflow](product-validation-workflow.md) | 실제 CLI 연결, 서로 다른 VSS와 한 view/cache, 코드·에셋·PNG·세대 분리, CLI 중단 복구, 독립 결과와 VHD 분리 후 검증 |
| [새 cache 반복과 전체 비용](product-validation-cold-cost.md) | 최종 후보의 두 빈 cache A/B/A, 2분 정상 종료 대기, 준비/검증 재시작 비용, 원시 응답 schema·실패 포함 할당·16개 독립 결과·소유 자원 회수 |
| [Replay index](replay-index-cost.md), [실제 Unity 비교](unity-replay-comparison.md), [범위 index](cumulative-write-range-index.md) | 고정 입력·후보 hash·단계별 replay와 실제 Editor 비용을 비교하는 기준 |
| [Live 쓰기](live-journal-write-cost.md) | flush 내부 비용, callback 직렬화·대기/완료 경계와 향후 내구성 판단 |
| [첫 미리보기](first-render-qualification.md) | 첫 draw 미확정 원인, cold/reload/재시작과 실패 주입, PNG 해시 기반 판독 기준 |

미완료 시나리오의 소유자는 [검증 추적표](../plans/validation-matrix.md), 다음 제품 설계 판단은 [저장공간 우선 계획](../plans/storage-priority-and-editor-modes.md)을 따른다. 완료 단계별 계획과 반복 CHANGE LOG는 유지하지 않는다.
