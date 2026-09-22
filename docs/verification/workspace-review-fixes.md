# 목표·구조 검토 후 수정 검증

기준일: 2026-09-22. `development`의 `6e8976c` 이후 작업 트리에서 확인한 수집 경쟁 조건, 상태 조회의 전체 소스 읽기, Git/Unity의 중복 mount 접근 규칙을 수정했다. 실행 후보는 `artifacts/workspace-review-fixes/source-manifest.json`의 파일별 SHA-256으로 식별한다. 이전 LV/NI의 실환경 증거를 이 후보의 시험 결과로 승계하지 않는다.

| 항목 | 검증과 결과 | 증거 수준 |
| --- | --- | --- |
| 수정 전 재현 | 기존 180개 통과·ReFS 1 skip. 추가한 회수/체크포인트 경쟁 조건 2건, status 반복 읽기 1건, 다른 backend 이름의 mount 접근 1건이 모두 실패 | `before.trx`: 총 185, 실패 4 |
| 안정된 결과 수집 | 소스 검사·ref 보존 전후 seal 비교. 중간 쓰기에 manifest 발행/회수 차단, 기존 manifest 보존, 새 coordinator에서 재시도. 브리지 제거의 정상 쓰기는 비교 구간 전에 완료 | fake source/backend/app, 영속 JSON |
| 이전 기록 | protocol 없는 manifest는 살아 있는 원래 기준에서 재수집. snapshot 소실과 이미 시작한 release 단계는 `SnapshotResultRevalidationRequired`·preserve, 자원 미삭제. 오류의 사용자 조치 분류와 schema 호환 확인 | fake + JSON/schema |
| 상태 조회 비용 | pending 결과 대기 중 10회 및 반영 후 10회의 새 coordinator 조회에서 source inspection 추가 호출 0. 새 결과 반영 때 1회. 수정 후 checkpoint는 이전 validation으로 진행하지 않음 | adapter 호출 계수, 영속 상태 |
| 실제 Git/CLI | 작업 파일을 수정하고 독점 잠금한 상태에서도 일반 status 성공. 잠금 해제 후 `--verify-source`는 변경 검출·이전 성공 무효화. not-checked 응답에 현재 검증 여부를 넣는 모순은 schema가 거부 | 임시 실제 Git worktree와 파일 I/O, Unity 없음 |
| 공통 mount 규칙 | Git/Unity가 같은 Core 규칙 사용. 10개 identity/관측 조합, NTFS 및 대체 이름의 어댑터 경로, 미확인 root·내부 링크 거부, 외부 파일 보존 | Core + Windows junction/Git fixture. WinFsp 실행 증거 아님 |

최종 진입점은 `pwsh ./scripts/build.ps1`이다. locked restore와 Release build는 경고/오류 0, 자동 시험은 **총 205개 중 204 통과·0 실패·선택적 ReFS 1 skip**이다. `final-build.log`, `final.trx`, 125개 소스/시험/계약/실행 파일의 후보 `source-manifest.json`은 `artifacts/workspace-review-fixes/`에 보존한다. ReFS 시험은 `SPLINTER_TEST_REFS_ROOT`를 지정하지 않아 건너뛰었다. UTF-8 66개 파일, 변경 문서의 로컬 링크 대상 152개, 기획서 HTML의 표 구조·ID·내부 앵커 및 `git diff --check`를 통과했다.

현재 계약은 [NTFS 작업공간](../ntfs-workspaces.md), [공통 Unity 흐름](../alpha-workspaces.md), [기반 구조](../foundation.md)에 있다. source fingerprint와 journal seal의 전체 읽기는 명시 검증·결과 수집 경계에 여전히 존재한다. 상태 poll의 불필요한 읽기를 제거한 것이며 전체 작업의 대형 프로젝트 성능이나 증분 해시 최적화 완료를 뜻하지 않는다.

이번에는 live VSS/WinFsp mount·Unity·원격 ARM·재부팅·전원·물리 공간 고갈 시험을 실행하지 않았다. 수정한 회수 절차의 실환경 회귀와 실제 AI A/B·단계별 비용은 다음 [PV-01/02](../plans/ntfs-recovery-and-support.md#제한된-병렬-작업과-비용-관문)와 기존 NR 지원 관문에 남긴다. NR-01/05/07 자동 회귀 보강을 NR 전체 완료나 공개 48개 통과로 표시하지 않는다.
