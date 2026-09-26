# 반복 UAC 없는 저장소 권한 경계

사용 목표는 최초 설치/권한 구성 후 정상적인 workspace 생성·재연결·회수마다 UAC를 누르지 않는 것이다. 현재 제품은 각 VSS helper를 별도로 승격하는 내부 알파이며 이 목표는 아직 미구현이다. UAC 자체를 끄거나 CLI·Unity·에이전트 전체를 관리자로 실행하는 방식은 기본 경로로 삼지 않는다.

## 목표 구조

Windows 저장소 어댑터에 좁은 권한 서비스를 둔다. 설치/업데이트/제거처럼 머신 구성이 바뀌는 시점의 관리자 승인과, 이미 승인된 범위의 반복 작업을 구분한다. 일반 CLI·Git·Editor는 현재 사용자 권한으로 실행하며 서비스는 VSS capture/observe/release에 필요한 일만 처리한다. 이는 Runtime을 daemon/DB writer로 전환하거나 작업을 자동 배정하는 계획이 아니다. Core는 공급자 중립 계약을 유지한다.

서비스 실행 파일/등록과 권한 있는 상태는 일반 사용자가 바꿀 수 없는 위치에 둔다. 요청자 사용자 identity, 등록 프로젝트·볼륨·workspace 소유권, 고정 명령과 입력 schema, 요청 ID/재시도, 독립 결과/마지막 의존성, 실제 snapshot identity를 서비스가 검증한다. 일반 사용자가 넘긴 임의 PowerShell·실행 파일·파일 경로를 관리자 권한으로 실행하는 인터페이스는 제공하지 않는다. IPC 접근 제어·상대 인증·reparse/경로 대체·다른 사용자 요청·중단 후 재개를 별도 검증한다.

서비스가 없거나 호환되지 않으면 설치/복구가 필요한 상태를 반환한다. 자동으로 반복 UAC를 띄우거나 관리자 CLI로 우회하지 않는다. 현재 정확한 helper 방식은 진단/수동 복구 경로로 구분한다. snapshot 삭제 차단과 `reclaim-pending`은 권한 서비스가 있어도 그대로 적용하며, 권한 획득을 타 작업 삭제 승인으로 해석하지 않는다.

| 검증 단위 | 완료 기준 | 상태 |
| --- | --- | --- |
| UA-01 설치/수명 | 최초 승인 후 제한된 서비스 등록, 재시작 발견, 활성 결과를 보존하는 업데이트/제거 | NotRun |
| UA-02 요청 경계 | 사용자·소유권·정확한 ID·schema·경로 검증, 임의 실행과 타 작업 요청 거부 | NotRun |
| UA-03 실제 반복 작업 | 일반 사용자 CLI의 A/B 생성·재연결·회수를 추가 UAC 없이 수행, 정확한 결과/자원 증거 | NotRun |
| UA-04 장애/복구 | 서비스 중단·응답 유실·재시작·호환성 불일치에서 중복 capture와 거짓 완료 없음 | NotRun |

이 작업은 [대화별 설치/사용 계획](session-workflow-and-installation.md)의 권한 UX 요구사항이다. 현재 지연 회수 시험에서 사용한 제한된 관리자 세션은 소유한 두 fixture snapshot의 회수만 처리하고 종료하는 개발용 운영 도구다. 제품 서비스 설치나 UA 통과 증거가 아니다.

설계의 Windows 근거는 Microsoft의 [관리자 broker 모델](https://learn.microsoft.com/en-us/windows/win32/secauthz/administrator-broker-model)과 [OS 서비스 모델](https://learn.microsoft.com/en-us/windows/win32/secauthz/operating-system-service-model)이다. 이 문서는 구현 범위 후보와 완료 기준이며 서비스 설치를 수행하지 않는다.
