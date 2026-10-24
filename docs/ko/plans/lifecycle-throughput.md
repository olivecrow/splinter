# 단일 작업공간 수명주기 처리 시간

[English](../../plans/lifecycle-throughput.md)

요청한 최적화 단위는 기준본 준비 → 소유 Editor 시작 → 코드 변경·컴파일·Editor 사용 → 명시적 원본 반영 또는 폐기 → 소유 자원 회수로 이어지는 작업공간 하나다. A/B 작업공간의 동시 실행이 최적화 목표가 아니다. 작은 상수 조정보다 반복 작업·대기를 구조적으로 줄이는 변경을 우선한다.

## 한정 구현

| ID | 후보 | 채택 경계 |
| --- | --- | --- |
| LT-01 | SJR2 append의 개별 durable flush 대기 중에도 이전 확정 내용을 읽는다. | 단일 writer·미게시 metadata/extent·현재 encoded bytes 검증·개별 durable ACK·실패 poison·checkpoint/seal/close 배타성과 read drain을 유지한다. Runtime과 WinFsp callback에서 시험한다. SJL4와 세대 교체는 기존 gate를 유지한다. |
| LT-02 | 관리형 Library capture·admission 파일 열기/해시·변경 없는 디렉터리 복원에 최대 8개 worker를 사용한다. | 전체 조상 map을 먼저 고정하고 worker가 모두 끝난 뒤 index를 게시한다. identity/type/path/content/metadata 검사와 이동 뒤 새 전체 증명을 유지한다. 실패·취소 worker가 끝난 뒤 핸들을 닫는다. 전체 복사·영속 payload cache를 추가하지 않는다. |
| LT-03 | 동일한 측정 harness를 변경 전/후 제품 assembly로 실행한다. | 프로세스를 ABBA 순서로 직렬 실행하고 파일 수·내용을 맞추며 모든 결과와 assembly hash를 보존한다. 실제 NTFS Library 비용과 명시적인 읽기/쓰기 작업 모델을 구분한다. 모델·구성 요소의 개선을 Editor·전체 수명주기 개선으로 주장하지 않는다. 유의미한 근거가 없는 후보는 제외한다. |
| LT-04 | 필수 locked restore·Release build·전체 자동 시험을 실행한다. | 원본 게시·폐기·독립 결과·복구 guard와 현재 schema/형식을 보존한다. 새 native apply/discard 장애 행렬이나 과거 Unity 비교를 자동 재개하지 않는다. |

기존 BC/BS 관찰은 flush 대기와 Library 준비·복원을 후보로 지목하는 근거이며 이 후보의 실측이 아니다. 스레드 대기 누계는 서로 겹치므로 벽시계 시간처럼 더하지 않는다. 자원 예산 증가만으로 Unity 자체 초기화·reload·종료를 없앨 수 없다. 외부 도구·plugin·Unity 설정·로드된 assembly를 바꾸거나 불필요한 Editor를 유지하거나 내구성을 미루지 않으며 폐기한 Bee/VSS/ReFS 경로를 되살리지 않는다.

채택 후보·비용·남은 자격은 [검증 기록](../verification/lifecycle-throughput.md)이 소유한다. LT-02는 채택하고, LT-01은 추가 저장 지연 없는 부하에서 7.659초·13.0% 절감에 그쳐 제외한다. 원본 Library 변경 뒤 일반 파일별 복원·원본 소스 반영·최종 독립 결과 스캔은 별도 측정 근거가 추가 변경을 정당화하기 전까지 기존 알고리즘을 유지한다.

## 요청한 실제 Unity 비교

**한정 완료:** [LU-01 결과](../verification/unity-single-lifecycle.md)에 동일 입력 네 실행의 359.5 → 300.0초, 공통 폐기 수정·정상 회수·남은 큰 비용을 기록한다. 아래 절차는 완료한 범위의 기록이며 자동 반복 요청이 아니다.

첫 진단 실행은 본 비교 전에 제외한다. 새 checkout의 소스 수정 시각이 바뀌어 warm Library에 추가 검사가 발생했고, 관측기도 GUI 렌더 초기 상태를 batch 기준과 비교했다. 본 비교는 **모든 소스·Library 파일의 수정 시각과 바이트를 함께 고정**하며 기존 GUI 에셋 기준을 사용한다. 진단 중 기존 bounded 폐기 연결 결함도 발견했다. `JournalReclamation`이 bounded guard를 알 수 없는 append 파일로 처리했다. 시간 비교 양쪽에 기존 bounded inventory/fence/Windows 디렉터리 guard를 선택하는 동일한 수정을 적용하고, journal/metadata/guard 삭제 뒤 같은 intent 복구도 유지한다. 충돌하는 모드 영수증은 보존한다. 따라서 비교 기준은 변경 없는 출시본이 아니라 **이 공통 정확성 수정을 더한 `be761bb`**다. 진단 당시 실행 파일·실패·복구는 별도 근거로 남긴다.

LU-01은 채택한 Library 변경만 포함한 후보와 고정한 `be761bb` 기준본을 **변경 전 → 후 → 후 → 전** 순서로 측정한다. 매회 새 작업공간 하나와 제품이 소유하는 GUI Editor(`6000.3.22f1`)를 사용하고, 동일한 실제 에셋 fixture의 추적 소스 1,602개·Library 42,038개 파일(3,105,800,655바이트)에서 시작한다. 전용 실험 clone은 이미 MCP를 제외한 fixture 커밋 `fd611140aa0c22d66782c19b007f1a68c00af2a6`을 사용하며 이전 진단 입력·외부 서버·canonical Unity 프로젝트는 보존한다.

Library 준비 → 작업공간 준비 → Editor 최초 준비 완료 → C# 식 하나 변경/refresh/컴파일 → 선택 모델·텍스처·씬 검사와 렌더 → 소유 Editor 정상 종료·회수를 포함한 명시적 폐기 → 원본 Library 복원을 측정한다. 매회 실행 전과 복원 뒤 소스·Library 전체 바이트를 비교하고 파일 제외나 stale PID 예외를 허용하지 않는다. 컴파일된 반환값·새 generation/MVID·정확한 PID/프로젝트를 확인한다. 일반 NTFS Library 복원·소스/refs/설정 일치·소유 Editor/provider/mount/delta 자원 부재를 확인해야 마감한다.

실험용 clone/backup 준비와 전체 입력/마감 hash 검사는 제품 시간 밖에 둔다. 측정 구간 안의 작은 증명 제외 시간은 따로 기록하고, 겹치는 종료 milestone이나 스레드 대기 누계를 벽시계 시간에 더하지 않는다. OS cache는 지우지 않는다. 버전별 2회 관측은 이 작업량에 한정하며 원본 반영·원본 Library 변경 뒤 일반 복원·Play/Test/build·전체 시스템 RAM은 LU-01 범위 밖이다. 실패 시 정확한 task/intent를 보존하고 측정 시리즈를 중단하며 복구 비용을 별도로 보고한다. 이번에는 새로 요청한 단일 작업공간 비교를 수행하며, 중단된 생성 메서드 비교나 디버거 진단을 재개하지 않는다.
