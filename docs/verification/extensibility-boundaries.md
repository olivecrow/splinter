# 확장 계약 검증

입력은 `ef97330` 위의 확장 경계 수정과 저장소의 자동 fixture다. 실제 다른 OS·앱의 구현이나 새로운 Unity/VSS/서비스/제품 A/B를 실행하는 범위는 포함하지 않는다. 현재 계약은 [OS와 앱의 확장 경계](../extensibility-boundaries.md)를 따른다.

| 경계 | 증거 | 자격 |
| --- | --- | --- |
| 경로 정책 | 대소문자 구분 fake tree의 독립 경로·이동·숨김·재생·후보와 정책 불일치 거부 | 자동 적합성; 실제 POSIX mount NotRun |
| 권한 metadata | Git 실행 파일 mode·저널 metadata·후보 roundtrip | 자동 적합성; 실제 Unix ACL/소유자/게시 NotRun |
| 앱 선택/원본 보호 | 주입한 가짜 정책의 별도 동반 파일 guard·원본 접근 거부·lease 회수, 미등록 정책 거부 | fake 정책 + 일반 권한 Windows 파일 게시 시험; 다른 앱 NotRun |
| 기존 Unity 동작 | 기존 Plastic 원본 반영 시험의 `.meta`·UnityLockfile·고정 intent·등록/잠금 보호, 임시 reservation 소유 정리 | 자동 파일 회귀; 새 Unity Editor 실행 NotRun |
| 생성 디렉터리 | 주입한 Cache descriptor·고정 앱/종류·v1 보존 거부, 기존 prepare/restore 회귀 | 자동 파일 적합성; 새 native mount/A/B NotRun |

2026-10-05의 `pwsh ./scripts/build.ps1`은 locked restore·Release build를 경고/오류 0개로 완료했다. 전체 회귀는 **1,052개: 통과 1,042·실패 0·조건부 건너뜀 10**이며 시험 실행 시간은 4분 44초다. TRX는 `artifacts/test-results/Splinter.Tests_net10.0_x64.trx`다. 건너뜀은 당시 backend·외부 실행/자식 전용 진입점의 조건이며 새 지원 통과로 표시하지 않는다.

집중 검사는 파일시스템/Git/후보 56개, 생성 디렉터리/프로젝트/복원 34개, 앱 정책/Plastic 원본 반영 21개와 CLI 계약 59개를 확인했다. 이 집합은 전체 회귀와 겹치므로 합산하지 않는다. 최종 전체 회귀에는 추가한 잘못된 projection 경로·손상 권한 metadata 사례와 응답 capabilities schema도 포함한다.

UTF-8 문서 검사와 `git diff --check`를 통과했다. 기존 verification의 native 자격을 이번 후보의 실제 실행 통과로 승계하지 않는다.
