# VSS 저장 경로 폐기

[English](../../plans/vss-retirement.md)

결정: `ntfs-snapshot`과 Splinter 소유의 모든 VSS 실행 경로를 제거한다. 남은 제품 모드는 `git-snapshot`, `plastic-snapshot`, `project-snapshot`이다. VSS를 개발 선택지나 fallback으로 남기지 않는다. 공통 불변 snapshot 계약, NTFS/WinFsp 범위 저장, 공유 Library 기준본, 독립 결과, 소유권·복구 보호는 유지한다.

## 프로젝트 기준본으로 전환한 이유

사용자의 2026-10-04 결정은 범위 불일치를 지적했다. 작업 대상은 프로젝트 하나인데 VSS는 C:와 같은 볼륨 전체를 캡처한다. 따라서 [프로젝트 단위 기준본 계획](project-scoped-basis-and-reclamation-review.md)은 기존 Git/Plastic 버전을 먼저 활용하기로 했다. 프로젝트 밖 변경과 볼륨 전체 보존은 개별 작업이 필요로 하는 저장 범위를 벗어난다. 볼륨 snapshot 내부의 프로젝트 경로를 선택해도 snapshot 자체가 프로젝트 단위가 되지는 않는다.

별도의 회수 제약도 있다. [2026-09-26 격리 재현](../verification/storage-recovery.md#vss-연쇄-삭제)은 나중 snapshot을 삭제한 뒤 삭제 대상이 아닌 더 오래된 snapshot이 소실되는 현상을 관측했다. `bForceDelete=false`인 직접 VSS API에서도 재현했다. 보호 구현은 더 오래되거나 같은 시각이거나 시각을 모르는 snapshot이 있으면 삭제를 거부했다. 한정 보호 시험에서 다른 snapshot을 보존했지만, 임의 작업 완료 순서와 즉시 독립 물리 회수를 동시에 보장하지 못했다. 다른 작업이나 백업 프로그램의 snapshot을 지워 회수를 우회하도록 허용한 적은 없다. 정확한 드라이버 임계값은 미확정이며, 기록된 환경의 근거를 모든 VSS 공급자의 일반 법칙으로 확대하지 않는다.

폐기 근거는 프로젝트/볼륨 범위 불일치와 회수 제약이다. **VSS 속도 벤치마크가 기준에 미달했다는 주장이 아니다.** `d97bd3c`의 ReFS 폐기는 대형 프로젝트의 최초 전체 기준본 준비 부담에 따른 별도 결정이다. [ReFS 결정](refs-retirement.md)을 혼동하지 않는다.

## 유지하는 구조

| 모드 | 고정 프로젝트 기준본 | 기존 구현 시점 |
| --- | --- | --- |
| `git-snapshot` | 로컬 commit/tree/blob, 소유 reachability ref와 reader fence | `d401677`, 2026-10-04 |
| `plastic-snapshot` | 기록된 changeset과 raw 파일 revision, 일반 사용자 reader | `7d90b3a`, 2026-10-05 |
| `project-snapshot` | 명시적 관리형 VCS 없는 프로젝트 기준본, 같은 볼륨 이동과 writable 원본 view | `3781769`, 2026-10-05 |

세 경로 모두 VSS 서비스가 필요 없다. Git/Plastic은 별도 준비한 Library를 참조할 수 있고 전체 프로젝트 관리형 기준본은 생성 데이터를 포함한다. source/Library 준비·복원은 정확한 identity, 의존성, 원본 Editor 정상 종료와 명시적 restore 조건을 유지한다. 최초 전체 프로젝트 복제, 읽기 hydration, 파일 전체 copy-up fallback, 변경 가능한 원본 fallback을 추가하지 않는다.

## 제거와 보존 경계

- VSS 공급자·reader, capture/observe/release PowerShell, StorageAuthority 서비스·SCM·IPC·client와 서비스 전용 보호, 설치·패키지·grant·status 진입점, 서비스 schema·capability, VSS 전용 시험·harness를 제거한다. 남은 구현에 필요한 서비스 전용 패키지 의존성은 없다.
- 공통 경로·identity 보호, 원자 게시, 읽기 임대, 개별 durable ACK, seal, 독립 결과, 원본·peer·dirty·target 보호, 지원 모드의 같은 버전 중단 복구를 유지한다. 개발 candidate/cache reader는 공통 기준본 조합을 사용하며 실제 자격은 별도다. 개발 Git view는 표현할 수 없는 Library graft를 거부하고 일반 task의 Library 공유는 유지한다.
- `ntfs-snapshot` 생성과 저장 상태를 거부한다. Runtime은 `StorageModeRetired`, 현재 요청 schema는 모드 거부를 반환한다. 구형 VSS overlay는 provider 시작·mount 전에 `SnapshotProviderUnsupported`로 거부한다. 자동 이관, 새 캡처, 결과 재작성, release, discard, ledger 정리는 하지 않는다.
- 기존 작업 상태, 독립 결과, 보호 ledger, 원시 evidence, 보고서, VHDX를 보존한다. 과거 검증은 당시 후보·입력의 기록으로 유지한다. 과거 문서의 서비스·CLI 예시는 현재 실행 지침이 아니다.
- Windows VSS, 시스템 복원점, 백업 프로그램, 설치 서비스 설정, snapshot은 변경하지 않는다. 시스템 정리는 이번 소스 변경 범위 밖이다.

시작 작업 트리는 `2201579`에서 깨끗했다. 이 커밋에 앞선 요청 범위 StorageAuthority 경로 pin 수정과 수명 시험, UA-05 문서가 이미 포함되어 있었다. 서비스 전용 구현은 이번에 폐기하고 공통 WinFsp·저널·관리형 기준본 경로 방어는 유지한다. 취소된 서비스 재설치와 새 실제 VSS 검증은 재개하지 않으며 `artifacts/storage-authority-idle-20261023/`를 실행하거나 변경하지 않는다.

## 완료 근거

[폐기 검증](../verification/vss-retirement.md)에 필수 빌드, 유지한 회귀 범위, 폐기 진입점 검사, 문서·UTF-8 검사를 기록한다. 새 VSS·Unity·대형 프로젝트·재부팅/전원 장애·물리 공간/RAM 자격을 주장하지 않는다. 이전 한정 결과는 각 검증 원본에 유지한다.
