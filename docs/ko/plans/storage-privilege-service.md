# 반복 UAC 없는 저장소 권한 경계

[English](../../plans/storage-privilege-service.md)

사용 목표는 최초 설치/권한 구성 후 정상적인 workspace 생성·재연결·회수마다 UAC를 누르지 않는 것이다. [서비스 코드와 설치 도구](../storage-privilege-service.md)는 연결했으며 실제 완료 판정은 [UA 검증](../verification/storage-privilege-service.md)을 따른다. UAC 자체를 끄거나 CLI·Unity·에이전트 전체를 관리자로 실행하는 방식은 기본 경로로 삼지 않는다.

## 목표 구조

Windows 저장소 어댑터에 좁은 권한 서비스를 둔다. 설치/업데이트/제거처럼 머신 구성이 바뀌는 시점의 관리자 승인과, 이미 승인된 범위의 반복 작업을 구분한다. 일반 CLI·Git·Editor는 현재 사용자 권한으로 실행하며 서비스는 VSS capture/observe/release에 필요한 일만 처리한다. 이는 Runtime을 daemon/DB writer로 전환하거나 작업을 자동 배정하는 계획이 아니다. Core는 공급자 중립 계약을 유지한다.

서비스 실행 파일/등록과 권한 있는 상태는 일반 사용자가 바꿀 수 없는 위치에 둔다. 요청자 사용자 identity, 등록 프로젝트·볼륨·workspace 소유권, 고정 명령과 입력 schema, 요청 ID/재시도, 독립 결과/마지막 의존성, 실제 snapshot identity를 서비스가 검증한다. 일반 사용자가 넘긴 임의 PowerShell·실행 파일·파일 경로를 관리자 권한으로 실행하는 인터페이스는 제공하지 않는다. IPC 접근 제어·상대 인증·reparse/경로 대체·다른 사용자 요청·중단 후 재개를 별도 검증한다.

서비스가 없거나 호환되지 않으면 설치/복구가 필요한 상태를 반환한다. 자동으로 반복 UAC를 띄우거나 관리자 CLI로 우회하지 않는다. 현재 정확한 helper 방식은 진단/수동 복구 경로로 구분한다. snapshot 삭제 차단과 `reclaim-pending`은 권한 서비스가 있어도 그대로 적용하며, 권한 획득을 타 작업 삭제 승인으로 해석하지 않는다.

| 검증 단위 | 완료 기준 | 상태 |
| --- | --- | --- |
| UA-01 설치/수명 | 최초 승인 후 제한된 서비스 등록, 재시작 발견, 활성 결과를 보존하는 업데이트/제거 | Passed: 설치·동일 패키지 업데이트·활성 상태 제거 거부·재시작 |
| UA-02 요청 경계 | 사용자·소유권·정확한 ID·schema·경로 검증, 임의 실행과 타 작업 요청 거부 | Passed: 자동 경계 및 설치된 SYSTEM 서비스의 6종 거부·보호 경로·SCM peer |
| UA-03 실제 반복 작업 | 일반 사용자 CLI의 A/B 생성·재연결·회수를 추가 UAC 없이 수행, 정확한 결과/자원 증거 | Passed: 전용 NTFS/Git A/B·동일 snapshot 재연결·독립 결과·회수·추가 UAC 0 |
| UA-04 장애/복구 | 서비스 중단·응답 유실·재시작·호환성 불일치에서 중복 capture와 거짓 완료 없음 | Passed: 일반 CLI 정지 오류·재개, Observe 응답 본문 유실 후 재시작/동일 응답, 버전 거부; capture 중단은 자동 시험 |

이 판정은 [UA 검증](../verification/storage-privilege-service.md)의 Windows x64·등록된 프로젝트/home·작은 fixture 범위다. native VSS 호출 도중 강제 종료와 실제 재부팅·전원 장애, 일반 Unity·ARM 지원은 확대하지 않는다. 이 작업은 [대화별 설치/사용 계획](session-workflow-and-installation.md)의 권한 UX 요구사항이며 제품 전체 설치와 자동 binding은 별도다. 과거 지연 회수 시험의 제한 관리자 세션은 제품 서비스 증거로 합산하지 않는다.

설계의 Windows 근거는 Microsoft의 [관리자 broker 모델](https://learn.microsoft.com/en-us/windows/win32/secauthz/administrator-broker-model)과 [OS 서비스 모델](https://learn.microsoft.com/en-us/windows/win32/secauthz/operating-system-service-model)이다. 이 문서는 완료 기준이며 실제 설치는 명시적 setup 진입점에서 수행한다.
