# 구조적 처리 시간 시제품

[English](../../plans/structural-throughput-prototypes.md)

사용자가 승인한 이 실험은 [TP-06](../verification/unity-throughput-optimization.md)의 후속이다. 전체 Unity 작업을 다시 실행하기 전에 작은 개발 전용 구현 두 가지를 비교한다. 제품의 새 backend를 선정하거나 현재 저널 형식·작업공간 수명·기본 내구성 정책을 바꾸지 않는다.

## 고정 범위

| ID | 실험 | 합격 근거 |
| --- | --- | --- |
| SX-01 | 소유 `Library/Bee`만 일반 NTFS로 노출하고 소스·나머지 Library는 실제 범위 overlay 유지 | 실제 mount 파일 작업, 정확한 target identity·경로 보호, 원본·peer 격리, 독립 소스 export, 정상 소유 회수, 실제 Bee 재생이 가능하면 동일 그래프 결과 |
| SX-02 | RAM 현재 범위 인덱스와 세대 checkpoint를 갖는 제한 로그 | 매 쓰기 성공 응답 전 flush, 고정 로그 상한, 현재 범위 전체 복구·독립 결과, 불완전 tail·손상·소유권·게시 중단 검사 |
| SX-03 | 동일한 작은 시간·저장 비용 비교 | 동일 작업·입력, 빌드·시험과 겹치지 않는 직렬 실행. 최초 준비·checkpoint·정리·최종 파일 할당·표본 임시 파일 bytes·쓰기 증폭을 구분하며 표본 bytes를 NTFS 최대 할당으로 읽지 않음 |
| SX-04 | 더 큰 검증의 진행 여부 판단 | 구성요소의 큰 개선과 한정 안전성 검사를 함께 만족해야 확대하며, 그렇지 않으면 전체 프로젝트 재실행이나 Unity 속도 주장 없이 종료 |

Bee 시험은 새 격리 입력을 사용한다. 보존된 Unity 6000.3.22f1의 assembly 세 개·package 0개 빌드 입력을 읽기 전용으로 사용하여 실제 빌드 그래프 생성기를 재생한다. 생성 그래프에 IL 후처리 노드가 없으면 최초·미변경·소스 한 번 변경의 Csc 빌드도 실행하고 컴파일된 상수를 확인한다. IL 후처리·Editor 준비·Play 시간은 측정하지 않는다. 과거 evidence와 형제 Unity 저장소는 편집하지 않는다. native Bee는 기존 payload cache 없이 시작하며 작은 명시적 빌드 입력만 양쪽에 제공한다. 원본 프로젝트·전체 Library를 복사·이동·변환하지 않는다.

native target과 namespace는 실험이 소유한다. mount-point 시제품은 OS 보안 sandbox가 아니다. native 파일 핸들과 mapped image는 별도 종료 확인이 필요하며 overlay context 0만으로 충분하지 않다. unknown 핸들이나 정상 종료 실패는 강제 삭제하지 않고 출력을 보존한다.

로그 시험은 고정 생성 입력, 현재 private 약 48MiB, 논리 객체 20,000개, 4–16KiB의 제한 변경을 사용하며 안전성 시험은 축소한다. 현재 SJL4와 명시적 상한까지만 이전 범위를 보존하는 로그를 비교한다. checkpoint는 현재 private 범위만 기록하고 불변 기준본은 기록하지 않으며, 임시 checkpoint 중복과 그 시간을 포함한다. 이 실험은 매 ACK마다 최신본만 남기는 제품 계약과 달리 제한된 이전 기록을 허용한다. RAM에만 저장한 뒤 성공 응답하는 것은 허용하지 않는다. Group commit은 명시적으로 측정하지 않는 한 별도 미구현 후속이다.

## 실행과 한계

구현과 읽기 전용 입력 조사는 병렬로 진행할 수 있다. 시간 측정은 정확성 검사 뒤 직렬 실행한다. 새 출력은 기존 디렉터리를 거부한다. 측정 전 소스·runtime identity를 고정하고 원시 결과·실패 시도를 보존한다. 통합 뒤 저장소 필수 빌드는 한 번 실행하고 비용 측정과 겹치지 않는다.

복구 시험은 소유 자식 프로세스 중단과 외부에서 관측한 성공 쓰기를 사용한다. OS 전원 장애 내구성은 자격 범위가 아니다. 제품 소스·결과 guard, 원본·peer 보호, 명시적 정리, 외부 도구 경계는 유지한다. Editor pool·외부 package 수정·서비스 변경·ReFS backend·원본 프로젝트 벤치마크는 포함하지 않는다.

작은 시험의 성공은 구성요소 결과다. 이전 공유 Editor 전체 작업은 313.1508초였으며, 이후 동일 작업의 최초 준비·통합·보존·복원을 포함한 정상 전체 실행만 완전한 속도 우위를 입증할 수 있다.

기록한 결과: 시도한 native Csc backend는 graft를 통한 상태 원자 게시에서 `ERROR_NOT_SAME_DEVICE`로 실패했다. 따라서 최종 같은 조건의 Bee 시간 비교는 그래프 생성에 한정한다. `--compile`은 진단 재현이며 지원하는 빌드 절차가 아니다. [검증 기록](../verification/structural-throughput-prototypes.md)을 따른다. 복사·삭제는 게시 보장을 바꾸며 두 번째 namespace 구조는 미구현이다.
