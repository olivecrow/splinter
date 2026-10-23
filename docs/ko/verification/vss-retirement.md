# VSS 폐기 검증

[English](../../verification/vss-retirement.md)

범위: 2026-10-23 `development`의 `2201579`를 기준으로 한 작업 트리에 [폐기 결정](../plans/vss-retirement.md)을 적용했다. 소스·빌드·자동 회귀 근거다. 과거 VSS·Unity 실행의 후보·입력·판정·제한은 그대로 유지한다.

| ID | 확인 | 판정과 경계 |
| --- | --- | --- |
| VT-01 | 폐기 실행 경로 전체 제거 | VSS 전용 소스·서비스·스크립트·schema·시험 37개 파일을 제거했다. VSS provider/reader, StorageAuthority client/SCM/IPC, 서비스 grant/status/install/package, 볼륨 예산 변경·snapshot 손실 주입 실행은 남지 않는다. 남은 스크립트 probe도 VSS를 조회하지 않는다. 서비스 전용 NuGet 의존성은 없었으며 중앙 버전·lockfile은 변경하지 않았다. |
| VT-02 | 구형 입력 보존·거부 | 자동 CLI/Runtime/schema 시험은 새 `ntfs-snapshot` 요청을 거부한다. 구형 Git/Plastic/None 상태의 status/start/checkpoint/compact/release/discard는 `StorageModeRetired`이며 원래 record·독립 결과·ledger·delta·snapshot 표식 파일 바이트를 유지한다. 구형 overlay 요청은 launch/mount 전에 `SnapshotProviderUnsupported`로 거부한다. 합성 보존 시험이며 설치된 상태를 조사하거나 정리한 근거가 아니다. |
| VT-03 | 유지 동작과 필수 빌드 | 시험 전용 임시 `maxParallelThreads=4` 설정으로 통과했다. locked restore·경고/오류 0개 Release 빌드·**1,556개 통과 / 실패 0개 / 24개 건너뜀(전체 1,580개)**이다. 필수 명령은 `pwsh ./scripts/build.ps1`이다. 수정한 집중 시험 184개도 통과했다. 종료 뒤 출력 디렉터리의 임시 runner 설정을 제거했으며 제품·전역 설정은 변경하지 않았다. |
| VT-04 | 탐색과 문서 | 제품 schema/capabilities는 `git-snapshot`, `plastic-snapshot`, `project-snapshot`만, 개발 도구는 추가로 `git-checkout`을 노출한다. 서비스 명령/schema/type/resource는 없다. 영문·국문 현재 절차와 과거 기록 안내를 함께 갱신했다. 로컬 Markdown/HTML 링크와 변경 파일의 UTF-8/대체 문자 검사, 변경한 PowerShell 5개 구문 분석, `git diff --check`가 통과했다. 소스에서 폐기 실행 API/서비스 참조는 발견하지 않았다. |

Git object/ref 소유권, 기록된 Plastic revision과 독립 파일 결과, 관리형 프로젝트/Library 의존성과 복원 guard, 공통 NTFS 경로/파일 identity와 원자 게시, 저널/ACK/seal, view/candidate/result 수명, 원본 반영/dirty/target/peer guard, 지원 경로의 같은 요청 중단 복구 시험을 유지한다. VSS 전용 시험은 제거하고 공통 동작 fixture는 유지하는 provider를 선택하도록 바꿨다. 전환 중 집중 시험 실패는 구형 provider identity, VSS fixture 메타데이터/기대값과 시험 컴파일 진단에서 발생했으며 공통 결과·회수 보호를 약화하지 않고 수정했다.

개발 preparation·외부 client 쓰기 복구 probe는 고정 raw Git 기준본을 사용한다. 결과 schema는 VSS token/전역 snapshot 관측 대신 동일 Git tree proof와 소유 기준본 ref 회수를 확인한다. 새 native 실행은 **NotRun**이다. 개발 view는 공통 Git reader를 쓰며 표현하지 못하는 Library graft를 거부하고 새 native view 자격은 없다. 일반 task Library 공유는 기존 계약을 유지한다. 읽기 전용 공간 계측의 현재 보고서는 VSS CIM 필드 없는 v3이며 과거 v2 보고서는 변환하지 않고 보존한다.

## 근거와 한계

근거: [최종 빌드](../../../artifacts/vss-retirement-20261023/final-build.log), [TRX](../../../artifacts/vss-retirement-20261023/final-tests.trx), [수치/설정](../../../artifacts/vss-retirement-20261023/test-summary.json), [runner 설정](../../../artifacts/vss-retirement-20261023/xunit.runner.json), [저장소 점검](../../../artifacts/vss-retirement-20261023/final-repository-audit.json), [집중 시험](../../../artifacts/vss-retirement-20261023/focused-tests.log). 초기 전환 실패 실행은 `.local/vss-*.log`에 남으며 통과 근거가 아니다.

[기본 전체 실행](../../../artifacts/vss-retirement-20261023/default-full-build.log)은 1,555개 통과·실패 1개·24개 건너뜀이었다. `GraftedReadersOverlapOnlyWhenBothProvidersOptIn(false,false)`가 두 번째 읽기를 10초 대기 안에 관측하지 못했다. 수정하지 않은 해당 클래스는 이후 [별도 실행에서 10개 모두 통과](../../../artifacts/vss-retirement-20261023/read-parallelism-recheck.log)했고 위 병렬 수 4의 전체 실행도 통과했다. 재실행 결과를 얻기 위해 제품 구현·시험 assertion·제한 시간을 바꾸지 않았다. 실행 문맥의 시간 대기 실패를 기록하며 정확한 scheduling 원인을 확정한 근거는 아니다.

실제 VSS 생성/삭제/조회, 서비스 설치/업데이트/제거, 시스템 복원점/백업 변경, Unity 실행/test/build, 새 native A/B, VHDX 조작, 대형/OS 장애 행렬은 실행하지 않았다. 중단한 서비스 재설치는 재개하지 않았다. 기존 ledger·작업 상태·독립 결과·원시 evidence를 이관하거나 정리하지 않았다. `artifacts/storage-authority-idle-20261023/`와 형제 `splinter-unity` checkout을 수정하지 않았다. 이 변경에는 commit/push가 포함되지 않는다.
