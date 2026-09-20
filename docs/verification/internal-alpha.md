# 내부 알파 검증 보고서

검증일: 2026-09-21 (Asia/Seoul). **간단한 스크립트 병렬 편집의 내부 알파 검증을 통과했다.** 환경은 Windows, Unity `6000.3.22f1`, Coplay Unity MCP `10.2.0`, 일반 Git checkout과 작업별 새 `Library`/`Temp`다.

저장소에 포함한 [기계 판독용 증거 요약](internal-alpha.evidence.json)에 revision, 작업 ID, 검증 fingerprint, PID, 비교 결과와 로컬 증거 파일 hash를 기록했다. 상세 원본 응답은 무시된 로컬 `artifacts/alpha-20260921`에 있으며 재클론만으로 복원되지 않는다.

## 확인된 결과

두 `gpt-5.6-luna` / `low` 작업자가 CLI로 독립 GUI Editor와 worktree를 동시에 확보했다. 원본 PID는 `25756`, A/B의 최초 PID는 `42768`/`31980`이며 별도 프로젝트 경로·Git index·Library를 사용했다. A의 오류 주입 전후 B와 원본의 HEAD, 컴파일 어셈블리 SHA256와 수정 시각이 같았고 A 스크립트도 다른 경로에 없었다. 근거는 `isolation-before-error.json`, `isolation-during-error.json`이다.

A는 의도적으로 `CS0103`을 만들었고 `validation-failed`와 실제 오류 메시지를 반환했다. 그 상태에서 B가 자신의 스크립트를 다시 수정해 새 fingerprint로 컴파일에 성공했다. B 성공 시점에도 A의 실패 증거와 오류가 남아 있었다. 근거는 `worker-a-intentional-error.json`, `concurrent-error-and-success.json`이다.

A는 오류를 고친 뒤 컴파일에 성공했다. 두 작업자 모두 다른 대화 턴에서 같은 key로 ensure/status를 재개해 기존 작업공간과 실행 중 PID를 되찾았다. 각 Editor를 stop/start한 뒤에는 새 PID와 같은 소스를 확인하고 재검증했다. 근거는 `worker-a-next-turn.json`, `worker-b-resume.json`, `worker-*-restarted-success.json`, `worker-*-final-ready.json`이다. CLI나 대화 종료에 따른 자동 회수는 없었다.

| 작업 | 작업공간 ID | 결과 커밋 | development 통합 커밋 |
| --- | --- | --- | --- |
| A | `33a67b2e55f7345aee71631f` | `f4bf33ecb6d44bb59f360086b1086dff59a35253` | `06d010846546ec3bbe730d2760a35821fb43f1bb` |
| B | `e3884dc6bfce44f7535bb5e1` | `e87c1e1adda58f28ab9ec5e1e84beed5f552934e` | `83c9e26b514586350f77df7732dcec8de7551aa9` |

fixture 기준은 `9fa59f9ed081336faa639d162b5c7c8f29913c33`이다. 각 작업자가 checkpoint한 소스는 성공 validation과 hash가 같았다. 두 결과는 `development`에 순차 통합됐고 조상 관계로 포함 여부를 확인했다. 이후 두 작업 Editor와 경로·Git worktree 등록을 CLI로 회수했다. 결과 브랜치와 상태·로그는 남는다. 근거는 `worker-a-checkpoint.json`, `worker-b-checkpoint-retry.json`, `worker-a-release-retry.json`, `worker-b-release.json`, `after-workers-released.json`이다.

원본 Editor를 활성화하고 MCP refresh로 통합 소스를 읽힌 뒤 domain reload와 컴파일 종료를 확인했다. 원본에 로드된 어셈블리에서 `Increment(3)=4`, `Double(3)=6`을 실행했다. `original-mcp-refresh.json`, `original-mcp-values.json`이 근거다. Console은 지우지 않았다. 최초 설치의 AssetImportWorker3 오류 1개와 서버 연결 중단 때의 오류 2개가 남지만 새 스크립트 컴파일 오류는 없었다.

`pwsh ./scripts/build.ps1`의 locked restore·Release 빌드·자동 시험은 **46개 성공, 실패 0개, 빌드 경고/오류 0개**다. 실제 Git을 사용하는 긴 경로 회수·부분 삭제 복구와 변경/미수집 파일 보존 시험도 포함한다.

## 실행 중 확인한 경계와 최종 재검증

- 첫 새 Library 임포트에 수 분이 걸렸다. 준비 상태를 기다리는 에이전트 지침을 보완했다. 초기 bridge JSON의 빈 validation 날짜는 검증 증거와 구별해 읽도록 수정했다.
- Unity 종료 시 Mobile URP 에셋의 직렬화 형식이 바뀌었다. 변경을 검토·보존하고 재검증한 뒤 결과에 포함했다. Editor·패키지 버전은 바꾸지 않았다.
- B의 상대 `.NET` 도구 경로가 만든 sentinel 3개는 생성 주체를 확인해 제거했다. CLI 접두사가 도구 상태를 제품 저장소 기준 절대 경로로 정규화하며, 최종 B checkpoint에는 스크립트·meta와 검토한 URP 에셋만 있다.
- Git이 ignored cache를 명시적으로 제외하다 checkpoint를 거부한 경로를 수정했다. 이후 양쪽 checkpoint가 성공했다.
- A 회수는 Windows 긴 경로 때문에 Git 등록만 해제하고 일부 파일을 남겼다. 명령별 `core.longpaths=true`와 남은 소스의 Git blob 일치를 검사하는 복구를 추가했다. A의 동일 release 재시도와 B의 정상 release가 성공했다. 알 수 없거나 변경된 잔여 소스의 보존도 자동 시험으로 확인했다.
- B 통합이 저장소 잠금의 `WorkspaceBusy`를 한 번 반환했으며 같은 명령 재시도로 성공했다.
- Coplay 10.2.0의 종료 콜백이 전역 EditorPrefs의 원본 서버 정보를 사용하는 문제를 확인했다. 소유 작업 브리지에서 그 콜백만 해제하며 패키지·전역 설정은 수정하지 않는다. 원본 `Start Server`를 실행해 정상 연결을 복구한 뒤, 별도 작업공간 `0eca6b307c77a6969574fc74`를 추가로 실행·종료·회수했다. guard가 활성화된 작업 PID `28196`은 종료됐고 원본 Editor와 서버 PID `19096`은 유지됐다. 그 이후 원본 MCP 함수 호출도 성공했다. 근거는 `coplay-guard-proof.json`, 최종 `original-mcp-values.json`이다.

## 검증 범위

외부 조정자가 시작한 Luna low 작업자들이 실제 CLI를 사용한 결과다. 초기 결함 수정과 복구는 조정자가 수행했다. 무인 설치·자동 발견성이나 제품의 `agent.start`/Codex runner를 시험한 것은 아니다. ReFS CoW, 공유 캐시 성능, 씬·프리팹 저작, Play, Unity Test Framework 시험, daemon/SQLite와 전력 장애 복구는 미검증이다. 공개 제품의 48개 시나리오와 FD 전체 조건은 별도이며, 이번 통과 범위는 추적표의 AA 항목이다.
