# 파일 결과 수집 처리량

[English](../../plans/result-collection-throughput.md) · [검증](../verification/result-collection-throughput.md) · [수명주기 지도](../lifecycle-optimization-map.md#c20)

## 범위와 채택 기준

2026-10-26 수명주기 검토 후속이다. 영속 형식과 독립 결과 보호를 유지하며 `project-snapshot`/None 및 recorded Plastic의 C20을 최적화한다. 기존 checkpoint·inspection·preservation의 세 번 소스 읽기를 coordinator가 소유한 전후 seal 구간의 한 번 manifest 수집으로 합친다. 독립 payload 복사·flush·검증은 최대 8개 worker로 제한한다. Plastic revision 획득은 reader의 동시성을 가정하지 않고 직렬로 유지한다.

RT-03은 실제 Windows CLI·NTFS·WinFsp에서 한 번에 Editor 없는 작업공간 하나, 고정 2.125GiB·2,112개 파일, 같은 private 편집과 공통 project basis를 사용한다. 기준은 `9b68a82cd89912e404d8acd6dcf7e48ce513a353`이며 같은 SDK로 전후 실행 파일을 고정하고 전–후–후–전 순서로 생성·편집·checkpoint·별도 release를 기록한다. 공통 prepare/restore와 입력·독립 결과 검증은 별도다. 평균 checkpoint가 20% 이상이면서 10초 이상 줄고 입력/결과 일치와 소유 회수가 성공해야 채택한다. 시험 입력/결과 저장 예산은 약 13GiB이며 시간 측정 중 build/test를 병행하지 않는다. OS 캐시를 강제로 비우지 않는다.

## 유지하는 경계

- task 잠금, 정상 앱 종료와 소유 프로세스 0 확인 후 수집한다. 성공한 수집은 여전히 전체 저널의 전후 seal 일치를 요구한다.
- 기준과 결과 전체 payload를 task별 hash 주소로 중복 제거해 독립 보존한다. 새 object는 각각 영속 flush와 내용 증명 후 게시한다. 기존/중단 object는 다시 검증하며 호출 간 검증 캐시를 재사용하지 않는다.
- 실패·취소 때 모든 worker의 복사/flush를 drain한다. 고정 collection intent·부분 object·작업공간을 보존하며 수집 실패는 회수를 허용하지 않는다.
- CLI/schema·상태/manifest/저널 버전은 그대로다. Git 수집, 쓰기/ACK, 원본 게시와 폐기 의미를 바꾸지 않는다.

## 추적

| ID | 작업 / 근거 |
| --- | --- |
| RT-01 | 기존 `ISnapshotCheckpointSourceProvider`로 파일 checkpoint/보존을 합치고 Plastic은 격리 확인 후 위임한다. |
| RT-02 | 제한된 payload 병렬 처리, seal 변경 실패/재시도, 손상된 pending payload, worker drain 및 기존 결과/소유/apply 회귀. |
| RT-03 | 동일 입력 실제 NTFS/WinFsp 전후후전 비교와 정확한 소유 회수. |
| RT-04 | 필수 locked restore/build/전체 시험과 양 언어 계약·수명주기 지도/색인 동기화. |

## 나머지 검토 후보

Library admission 중 Git proof 선행 읽기는 cache 수명 실험을 따로 해야 한다. index가 소비하기 전에 proof/payload가 퇴출되면 다시 decode할 수 있으므로 단순한 background 이동으로 활성화하지 않는다. bounded checkpoint 증분 게시와 변경 없는 subtree 복원은 별도 디스크 복구 설계·실패 행렬이 필요하며 이번 변경이 이를 대체하지 않는다. Unity Editor 시작·컴파일·전체 수명주기, 실제 Plastic 서버, 원본 반영 성능과 전원 장애 자격은 RT-03 밖이다. 기존 검증을 재실행하거나 승계하지 않는다.
