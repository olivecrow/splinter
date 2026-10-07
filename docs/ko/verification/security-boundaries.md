# 보안 경계 검증

[English](../../verification/security-boundaries.md)

SB-02는 제거된 외부 패치 기능의 과거 인증 후보 기록이다. 현재 제품에 외부 MCP 토큰·고정 버전·후보 준비/연결을 요구하지 않으며 패치 제작/운영은 제공하지 않는다. 기존 후보·설치·서버·evidence는 보존한다. 현재 책임은 [외부 도구 경계](../unity-mcp-response-recovery.md)를 따른다.

선택 범위는 다른 로컬 Windows 계정의 Plastic/MCP 접근, 서비스 자식 파일의 쓰기 ACL, 전역 회수 잠금 선점, 미완성 IPC 수신이다. 원본/peer·snapshot·서비스 ledger·기존 외부 후보를 보존한다. 설치/grant 변경·VSS 회수·Unity A/B·새 악성 프로젝트·전체 공격 행렬을 실행하지 않는다. 원시 확인은 `artifacts/security-fixes/`다.

| ID | 경계 | 확인과 제한 |
| --- | --- | --- |
| SB-01 | Plastic private revision reader | 공개 cm API helper를 제거하고 private stdin/stdout의 고정 라이브러리 adapter로 연결. 기존 고정 cs 2의 PNG 24,069B·BOM C# 6,677B/302B 길이·MD5 일치. 자동 frame 시험은 binary/BOM·oversize·불완전/실패·미지원 버전·중단 body의 폐기를 확인. Unity·대형 segmented revision·다른 client 버전은 NotRun |
| SB-02 | 외부 MCP v2 인증 | 새 후보 `candidate-security-v2-03`의 10개 패치 파일 해시 확인. 인증/형식/중복 header/Origin 5개, 실제 FastMCP HTTP·custom route·WebSocket/CLI 3개, 기존 code job 전달 7개 통과. CLI 토큰의 다른 host 전송도 거부. Editor의 실제 새 토큰 연결·계정 간 침투·기존 서버 전환은 NotRun. 기존 runtime의 Starlette TestClient deprecation warning은 시험 의존성의 근거이며 product 의존성을 변경하지 않음 |
| SB-03 | 서비스 ACL | runtime/설치/script 회수의 동일한 synthetic ACL에서 Users·별도 user의 `InheritOnly` Modify 거부, SYSTEM/Admin·상속 전용 CREATOR OWNER 허용. 실제 서비스 경로/파일 ACL은 변경하지 않음. 설치/update 실연은 NotRun |
| SB-04 | 회수 잠금 | 실제 임시 파일 핸들의 독점 획득·두 번째 획득 거부·dispose 뒤 재획득·파일 보존을 승인된 ACL double 아래 확인. 전역 mutex 이름과 무관. VSS 삭제·설치된 서비스의 실제 회수는 NotRun |
| SB-05 | IPC 수신 | 실제 자기 named pipe의 미완성 header/body가 별도 1초 제한으로 종료되고 다음 client의 정상 frame이 수신됨. 인증된 operation의 기존 3분 제한·결과 불명확 보존은 유지. 반복 등록 사용자 남용에 대한 일반 rate limit이나 새 queue를 구현한 것이 아님 |

필수 `pwsh ./scripts/build.ps1`은 locked restore·Release 빌드 경고/오류 0, 총 **1,036개 중 1,026 Passed / 0 Failed / 10 conditional Skipped**로 통과했다. skip은 당시 제외 시험와 별도 실행 입력이 필요한 evidence/child 진입점이다. 최종 원시 근거는 `artifacts/security-fixes/required-build-complete.log`와 `required-build-complete.trx`이며 일반 사용자 권한으로 실행했다. 제한 토큰에서는 기존 named-pipe 시험도 연결을 거부하므로 그 실패를 제품 실패로 합산하지 않는다. 중간 전체 실행은 누락된 capabilities schema와 새 pipe 시험의 읽기 전 쓰기 대기를 발견해 중단했고 해당 로그를 보존한다. 이를 고친 pipe 시험 4개를 먼저 확인한 뒤 최종 전체 회귀를 완료했다.

요청·상태·workspace v2는 기존 계약을 유지하며 capabilities의 Plastic reader 설명을 응답 schema에 함께 추가한다. 의존성/lockfile 변경은 없으며 bridge는 vendor 라이브러리의 안정된 공개 SDK로 주장하지 않는 고정 버전 내부 adapter다. helper는 4MiB decoded scratch와 최대 64KiB frame을 유지하며 vendor packet/native allocation·외부 cache/통신·전체 RAM peak는 별도 미계측이다.

보안 수정의 현재 코드와 설치·실행 중인 환경은 구분한다. 서비스 수정의 실행 환경 반영은 새 서비스 패키지의 명시적 Update와 별도 자격을 따른다. 과거 외부 MCP 후보 연결은 현재 제품 설치 요구가 아니다. v1 후보·원래 manifest/lock·공유 서버와 다른 Editor를 자동 교체하지 않는다. 과거 PPB/MR의 실제 Unity 자격은 당시 reader/후보의 근거로 보존하며 새 후보의 Passed로 승계하지 않는다.
