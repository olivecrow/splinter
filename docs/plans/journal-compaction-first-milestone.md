# SF-02 저널 공간 정리 1차 범위

상태: **명시적 정리 명령 구현·제한된 검증 완료, 작은 Unity 스크립트 관문 Partial, SF-02 전체 Partial**. 대형 Unity 프로젝트의 작업공간 용량을 먼저 줄이기 위한 실행 단위다. 기존 append-only `delta.journal`은 같은 4KiB를 100회 덮어써도 이전 payload를 유지했다. [기존 실측](../verification/storage-foundation.md#범위-저장누적-저널)의 해당 단계는 누적 payload 421,956B, 저널 논리 468,695B, delta 파일 할당 471,040B였다. 이번 [격리 NTFS 실행](../verification/journal-compaction.md)에서는 별도 fixture의 할당 감소를 확인했다. 두 실행의 수치를 같은 입력의 전후 값으로 합치거나 100GB Unity 프로젝트의 예측치로 사용하지 않는다.

## 목표와 실행 경계

명시적 `workspace compact --id <id> [--home <state-root>]`는 **결과 수집 전의 NTFS 작업공간 하나**에서 더 이상 살아 있지 않은 덮어쓰기 범위와 메타데이터 기록을 정리하고, 같은 snapshot·작업 ID·Git 등록으로 편집을 재개한다. 자동 sweep이나 release의 대기 snapshot 회수를 이 명령에 결합하지 않는다.

명령은 소유 Editor·작업 프로세스와 열린 mount 핸들이 없고 **같은 작업공간**의 다른 수명주기 명령이 진행 중이지 않을 때만 provider를 정지·unmount한다. 다른 작업공간은 계속 사용할 수 있어야 한다. 관측이 unknown이거나 정지할 수 없으면 기존 상태를 보존하고 필요한 조치를 반환한다. 작업자가 편집 중인 파일이나 사용자가 연 원본 Editor를 강제로 닫지 않는다. 정리 완료 뒤에는 동일 기준의 mount를 다시 연결하고, 정리 전 검증 응답은 현재 결과로 재사용하지 않는다. `checkpoint`는 최종 결과 수집이므로 정리용 중간 checkpoint로 호출하지 않는다.

## 포함 범위와 완료 조건

| 단위 | 완료 조건 |
| --- | --- |
| 저널 세대와 원자적 전환 | 살아 있는 범위·파일 메타데이터·whiteout·rename·truncate·`.meta`·소스 변경 경계를 보존하는 새 세대를 별도 파일에 작성한다. chunk 단위로 읽고 써서 파일 전체 payload를 메모리에 올리지 않는다. flush·checksum·재생 검증 후에만 새 세대를 발행하며, 이전 세대는 새 세대의 영속성과 재연결을 확인할 때까지 보존한다. |
| 중단·손상 복구 | 새 세대 작성 전/중, flush 뒤, 발행 전/후, 이전 세대 정리 중의 중단을 재시작으로 시험한다. 확인된 이전 또는 새 세대를 정확히 선택하거나 `preserve`로 차단한다. 완결 프레임 손상·identity 불일치·증거 부족은 조용히 잘라내거나 다른 snapshot으로 대체하지 않는다. 이미 성공을 응답한 쓰기는 어느 지점에서도 잃지 않는다. |
| 제품 명령과 재개 | `workspace compact`의 입력/출력 schema, fake, CLI 계약, 중복 요청·새 CLI 재시도를 연결한다. provider 정지·재연결 전후에 같은 작업의 파일 내용·Git source와 source-validation 세대를 대조한다. 새 쓰기와 최종 독립 결과 수집·release가 계속 가능해야 한다. 다른 작업과 원본은 그대로 둔다. |
| 공간·시간 장부 | 같은 입력/종료점의 기존 저널과 정리 후 저널에 대해 논리 길이, NTFS 실제 할당, 임시 세대 최고치, 처리 시간·읽기·flush·재생·seal 비용을 분리한다. 4KiB 동일 영역 100회 덮어쓰기 fixture에서 정리 후 delta 실제 할당이 정리 전보다 작아야 한다. 16MiB 전체 재쓰기와 delete/truncate도 측정하되, 실제 살아 있는 데이터보다 작은 저장을 약속하지 않는다. VSS 사용/할당은 볼륨 공유 관측으로 표기하고 workspace별 절감량으로 귀속하지 않는다. |
| 제한된 실제 회귀 | fake A/B에서 B 정리 중 A의 편집 지속과 상호 격리를 확인한다. 격리 NTFS/Git의 제품 mount에서 정리→같은 작업 재개→추가 편집→독립 결과→소유 자원 회수를 실행한다. 기존 작은 Unity 스크립트 한 작업의 로드/재개를 확인하고 대용량 PV C/S/Q나 100GB 시험으로 집계하지 않는다. 실패·운영 개입과 남은 자원을 그대로 기록한다. |

새 세대가 기존 `MaxJournalBytes`와 계산 가능한 임시 공간 상한을 넘거나 물리 여유 공간이 부족하면 시작 전에 거부하고 이전 세대를 보존한다. 저장량이 줄어도 임시 최고치나 공유 VSS 비용 때문에 전체 추가 공간이 줄지 않을 수 있으므로, 이 실행 단위는 **최종 저널 할당 감소와 전체 공간 비용을 별도 판정**한다. 공간 절감이 확인되지 않으면 SF-02 저장 목표를 통과로 표시하지 않는다.

구현·자동 중단 재개, 합성/Unity 저널의 실제 할당 감소, 동일 snapshot 재연결, 독립 결과와 소유 자원 회수는 [실행 기록](../verification/journal-compaction.md)을 따른다. 작은 Unity Editor는 로드와 재시작 로드를 확인했으나 `Library/ScriptAssemblies` 예외로 브리지 준비·스크립트 검증을 완료하지 못했다. 전체 작업공간 peak·실제 전원/공간 장애와 일반 지원도 남아 있어 위 완료 조건 전체를 Passed로 올리지 않는다.

## 유지할 계약과 제외 범위

- NTFS 원본 위치·파일시스템을 유지한다. 최초 전체 복제, 읽기 hydration, 파일 전체 copy-up fallback, 원본 쓰기 차단, 새 VSS 캡처는 허용하지 않는다.
- 결과 수집의 `stable-journal-v1` 전후 및 unmount 후 seal, 독립 Git 결과, `reclaim-pending`의 같은 ID 명시적 release 재시도를 유지한다. 정리 후 새 저널의 seal/세대를 검증하고 이전 validation을 무조건 재사용하지 않는다.
- 이번 범위는 **대체된 범위의 공간 정리**다. 모든 payload의 범용 압축, 쓰기마다 `Flush(true)`하는 내구성 정책 변경·묶음 처리, 자동 정리/daemon, 비소유 snapshot 삭제, 단일 Editor 후보 전환, 설치·binding·discard, NR 전체 장애 지원은 포함하지 않는다.
- Splinter 제품 Unity 회귀에는 Coordination을 연결하지 않는다. Q 비교군과 기존 PV 대용량 시험은 재개하지 않는다.
- 기존 [SF 실행](../verification/storage-foundation.md)의 `FailedPreservation`, SF-01의 제한된 Passed, SF-02 전체 Partial, SF-03~06 NotRun을 유지한다. 이 범위의 실제 판정과 미실행 조건은 [SF-02 정리 검증](../verification/journal-compaction.md)을 따른다.

관련 Core/Runtime/Windows·CLI 계약과 schema, fake·적합성 시험, [현재 NTFS 계약](../ntfs-workspaces.md), [검증 추적표](validation-matrix.md), 실행 증거를 함께 관리한다. 설치·권한 서비스, 자동 binding, 다른 프로젝트 변경은 이 범위 밖이다.
