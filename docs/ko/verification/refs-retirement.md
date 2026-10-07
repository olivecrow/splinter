# ReFS 제거 검증

[English](../../verification/refs-retirement.md)

범위는 [ReFS 폐기 결정](../plans/refs-retirement.md)의 코드·명령·schema·문서 제거와 기존 스냅샷 계약의 자동 회귀다. 폐기한 기능의 과거 SR 실행은 현재 기능의 자격으로 사용하지 않는다.

2026-10-06 `development` 작업 트리에서 제거를 마감했다. `pwsh ./scripts/build.ps1`의 locked restore·Release build는 경고/오류 0, 전체 **1,067개 중 1,058 Passed·0 Failed·9 Skipped**다. 9개는 외부 실행/자식 전용 시험의 조건이며 폐기한 ReFS 시험은 더 이상 포함하지 않는다. [전체 build 로그](../../../artifacts/refs-retirement-20261006/build-full.log)·[고정 TRX](../../../artifacts/refs-retirement-20261006/full.trx)를 보존한다.

마지막 CLI 도움말 문구까지 반영한 Release 재빌드는 경고/오류 0이며 CLI 계약 **64 Passed·0 Failed·0 Skipped**를 확인했다. [최종 build](../../../artifacts/refs-retirement-20261006/build-final.log)·[CLI 로그](../../../artifacts/refs-retirement-20261006/cli.log)·[CLI TRX](../../../artifacts/refs-retirement-20261006/cli.trx)를 보존한다. 실제 [기본 capabilities](../../../artifacts/refs-retirement-20261006/product-capabilities.json)·[개발 capabilities](../../../artifacts/refs-retirement-20261006/development-capabilities.json)에도 폐기한 저장 모드와 명령이 없다. [최종 소스/증거 manifest](../../../artifacts/refs-retirement-20261006/manifest.json)가 기준 commit과 변경 파일/삭제 파일·검증 결과의 hash를 연결한다.

| 경계 | 확인 내용 |
| --- | --- |
| 기능 발견과 실행 | 기본/개발 CLI 모두 `refs-cow`를 source/state 접근 전에 거부. 개발 CLI의 `baseline prepare/seal/release`·`storage probe` 제거와 schema 발견 부재 |
| 저장 공급자 | ReFS Win32 블록 복제·전체 기준본 수명·Git clone 영수증·공통 복제 계약·Dev Drive 준비·선택적 ReFS 시험 제거. 현재 src/tools/scripts/contracts에 관련 구현 참조 없음 |
| 보존과 재개 | 폐기한 저장 모드·준비 purpose·값이 남은 제거 필드를 보존 후 거부. 현재 v2의 빈 과거 필드는 결과 없는 metadata로만 읽으며 같은 request digest·workspace ID로 재개 |
| 기존 기능 | 전체 계약/fake·Git·Windows 회귀와 기존 snapshot·dirty/target/peer·독립 결과·release/discard 보호 |
| 문서 | 선택적 경로/후속 과제 안내 제거, 과거 시험 수 보존, UTF-8·수정 문서 링크·diff 검사 |

원시 artifacts·과거 ReFS 기계 증거·독립 결과·기존 상태/데이터를 삭제하거나 자동 이관하지 않았다. 실제 Unity A/B·서비스 설치/업데이트·과거 snapshot 회수는 이 제거 작업에서 실행하지 않았다.
