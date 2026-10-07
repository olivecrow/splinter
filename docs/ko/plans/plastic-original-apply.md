# Plastic 결과의 원본 반영

[English](../../plans/plastic-original-apply.md)

## 범위와 종료 기준

사용자가 승인한 후속은 `plastic-snapshot`의 독립 결과를 기존 regular/static 원본에 전체 또는 명시적으로 선택한 경로만 반영하는 것이다. 일반 폴더의 조건부 NTFS 게시, 고정 intent/digest, 중단 후 동일 요청 복구와 미완료 intent 회수 차단을 재사용한다. Git 통합과 구형 VSS Plastic 작업은 변경하지 않는다.

- 정확한 원본 등록 GUID·관리 파일·repository/selector와 현재 잠금 관측을 확인한다. 관리 정보나 Unity Library를 반영하지 않으며 서버 checkout/add/check-in/update/잠금 변경은 제공하지 않는다.
- 선택 경로와 필요한 부모·Unity `.meta`를 계획에 고정한다. 변경 전 기준/결과/현재 원본을 비교하고 제3의 변경은 쓰기 전에 거부한다. 원본 Editor는 정상 종료한 상태여야 한다.
- 계약/fake 시험에서 선택, 등록·잠금 거부, 충돌, intent 복구와 회수 차단을 확인하고 필수 build를 통과한다.
- 실제 입력은 기존 시험 원본 `splinter-unity-plastic`의 changeset 2와 새 `poa1005-A/B` 한 묶음이다. A는 `Assets/SplinterApplyQualification1005/Selected.txt`, `Withheld.txt` 및 `Assets/Readme.asset` 주석 변경을 보존하고 선택 반영한다. B는 같은 시험 폴더의 `B.txt`를 보존한 뒤 원본의 소유 시험 충돌을 확인하고 전체 반영한다. `.meta`와 부모는 함께 고정한다. 서버 이력·등록은 변경하지 않는다.
- A의 반영/회수 중 B의 기준 읽기와 독립 결과를 보존한다. 원본 반영 결과를 확인한 뒤 시험에서 소유한 파일만 내용 대조 후 정리하고 Readme의 승인 시험 변경만 복원한다. A release/B discard와 정확한 소유 mount/provider/API helper 부재를 확인한다.

완료는 위 계약과 한 묶음의 관측에 한정한다. 원격 잠금 관측은 서버 예약이 아니며 서버 상태와 로컬 게시의 분산 원자성을 주장하지 않는다. MCP 긴 쓰기의 응답 유실, release와 peer 테스트 실행의 실제 시간 중첩, Play/build·대형·OS 장애 행렬은 이 시험의 통과로 승계하지 않는다. VCS 없는 새 기준본, partial/dynamic Plastic 및 released 결과의 재수입은 후순위다.

## 진행 상태

위 범위를 한정 마감했다. 구현·실제 실행과 후보별 제한의 근거는 `docs/verification/plastic-original-apply.md`에 기록한다. 기존 C: 비소유 VSS 보호에 따른 회수 보류와 독립 결과는 유지한다. 새 실제 묶음을 자동 반복하지 않는다.
