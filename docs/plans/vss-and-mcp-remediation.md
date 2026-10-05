# 기존 VSS 회수와 MCP 응답 복구

2026-10-05 사용자가 두 문제의 해결 작업을 지시했다. 기존 정리 승인과 이번 지시를 실행 승인으로 사용한다. 질문은 (1) 과거 C: Plastic A/B를 비소유·활성 snapshot 손실 없이 회수할 수 있는가, (2) 동일 HTTP 서버의 정확한 Editor 대상에서 긴 실행 응답과 reload 이후 상세 테스트 결과를 보존할 수 있는가다.

## 고정 범위

- 기존 `T:\pc1002` A `c050da8fd660e350f0ef2756` release와 B `1ac08fe356c9603d0f13bf83` discard, 기존 intent/digest를 사용한다. 구형 상태를 현재 형식으로 임의 변환하지 않는다.
- C:의 차단 ID `{78AD7813-DF73-4845-8513-F14CC6E87331}`, `{15182498-4A5B-4E95-B85F-FBE2EC57AE15}`, `{A16A6070-1AAE-445F-8561-08C86B00A115}`, `{0A89C286-0E9C-43EB-87AA-4CD068E98E7E}`의 현재 존재·소유 기록·의존성을 먼저 읽는다. 타 작업의 결과와 활성 자원을 보존한다. 격리 guard 제거, 전역 삭제, VSS 한도 변경은 해결책으로 사용하지 않는다.
- 관리자 작업은 고정 evidence에 대한 inventory와 검증된 소유 회수를 수행한다. 설치된 서비스·원본·peer를 유지한다. 조사 후 사용자가 “기존 차단 snapshot 3개 제거 허용”으로 `{15182498-4A5B-4E95-B85F-FBE2EC57AE15}`, `{A16A6070-1AAE-445F-8561-08C86B00A115}`, `{0A89C286-0E9C-43EB-87AA-4CD068E98E7E}`의 삭제를 별도 승인했다. 이 고정 예외 외 비소유 snapshot은 삭제하지 않는다. 최신 DirectX 복원 지점을 보존한다.
- Coplay `10.2.0`의 고정 revision `30d22075093d1d35dfb0091c1c7550e9ad948577`에 재현 가능한 외부 도구 패치를 관리한다. `Library/PackageCache`나 uv cache를 직접 수정하지 않는다. Splinter Core/Runtime에 Unity 작업 API를 추가하지 않는다.
- Codex HTTP 연결과 정확한 per-call target을 사용한다. 기존 공유 서버는 같은 8080/version의 정상 재시작만 하며 로그는 Library 밖에 둔다. Stdio/HTTP 혼재를 별도 연결 문제로 정리한다.
- 검증은 기존 Git 원본 Editor의 read-only 긴 코드 한 건, 작은 선택 테스트 및 명시적 reload 한 번, 대상/현재 loaded 패치·상세 결과 동등성·원본/다른 Editor 보존을 확인한다. 새 Splinter A/B·새 지원 행렬·Play/build는 만들지 않는다. 필요한 fixture 파일은 해시와 경로를 고정하고 정리한다.

## 종료 기준

1. VSS의 최신 inventory와 차단 ID의 소유·수명·의존성을 대응시키고, 회수 가능한 소유 자원만 오래된 순서로 회수한다. 정확한 A/B의 종결·독립 A 결과 보존과 다른 snapshot/peer 보호를 관측한다. 외부 사용 자원이 blocker라면 회수 완료를 주장하지 않는다.
2. 긴 명령이 실패/null 응답 뒤 몰래 완료되는 경로를 수정하고, 실제 30초를 넘는 read-only 실행에서 한 번의 명령과 확인 가능한 최종 결과를 받는다. 중복 실행을 timeout 재시도로 숨기지 않는다.
3. 동일 Editor의 완료한 선택 테스트 상세 결과를 reload 후 같은 job ID로 다시 읽고 전체 결과 hash가 일치한다. A/B namespace 격리는 기존 자격을 유지하며 신규 병렬 Editor 검증으로 확대하지 않는다.
4. 패치 적용·고정 버전·서버/Editor 재시작·Codex 설정의 원본 보존과 복구 경로를 문서화한다. 단순 timeout 증가만으로 무제한 실행/취소/중복 안전성을 주장하지 않는다.
5. 변경된 경계의 자동 시험, 필수 `pwsh scripts/build.ps1`, UTF-8/diff 검사를 수행한다. 원시 근거는 `artifacts/vss-mcp-remediation-20261005/`, 판정은 별도 verification이 소유한다. HTML·push·서비스 자동 sweep·VCS 없는 기준본은 포함하지 않는다.

## 마감과 후속 경계

[VR/MR 검증](../verification/vss-and-mcp-remediation.md)으로 고정 범위를 한정 마감한다. A released/B discarded와 독립 A 결과·최신 DirectX 보존, 긴 실행의 한 번 실행/중복 차단 및 reload 뒤 상세 결과를 확인했다. 전역 Codex 설정 변경 중 env 누락으로 대화 제출을 막은 결함과 사용자 별도 복구를 기록했으며 앞으로 설정 조회 검사를 필수 절차로 둔다. Codex 재실행 뒤 기본 binding은 HTTP로 전환됐지만 외부 Editor/서버가 예기치 않게 사라져 같은 버전·포트를 독립 실행했다. 이후 사용자 Connect 뒤 기본 HTTP 도구의 정확한 대상·원본 PID/start·로드된 패치·read-only code 결과·Console/idle 상태를 확인해 연결 확인 항목도 마감했다. 이전 예기치 않은 종료 원인은 미확정이며 추가 앱 재시작 생존 시험은 NotRun이다. 신규 A/B/Play/build·일반 VSS 삭제 안전성·Editor/OS 장애의 임의 코드 복구를 자동 확대하지 않는다.
