# 코드 품질 검토의 다섯 결함 수정

기준일은 2026-09-27이며 `development`의 `c341a2d` 이후 수정 후보를 대상으로 한다. 개발용 validation view의 빈 파일 왕복, 정리 실패 응답, 제품 heartbeat의 fixture 의존성, Git 해시 프로세스 수와 작은 범위 읽기의 탐색 비용을 수정했다. 실제 제품/실험의 경계는 유지한다.

| 항목 | 수정과 검증 | 증거 수준 |
| --- | --- | --- |
| 빈 private 파일 왕복 | `Temp` → `Assets` → 길이 0 → `Temp`에서 재사용 ID의 이전 extent를 지운다. 정상과 intent/staged/published/source-removed 중단 5건 모두 같은 source/cache 재연결, ID·빈 내용·확장 후 zero-fill과 receipt 완료를 검사한다 | 실제 Runtime 저널·임시 파일, fake snapshot. WinFsp/Unity 실행 아님 |
| 정리 실패의 복구 계약 | `CompactionGenerationMismatch`, `CompactionStateMismatch`, `CompactionCandidateMismatch`, `CompactionIntentCorrupt`, `CompactionIntentMismatch`를 Core 공통 정책으로 보존한다. 실패 응답은 `requires-user-action`과 같은 id/home의 `workspace.status`, 새 coordinator의 status는 `preserve`이며 delta/snapshot 미삭제를 확인한다 | fake backend와 영속 상태·Protocol, 5건 |
| heartbeat와 fixture 분리 | 제품 브리지에서 `SplinterSnapshotProbe.Calculate` reflection 호출을 제거했다. 같은 `Assembly-CSharp`의 예외를 던지는 함수를 둔 별도 프로세스에서 incremental 0/1, ready/compiling/ready와 reload 뒤 validation 전달·fixture 호출 0을 확인한다 | 제품 `WorkspaceBridge.cs`를 직접 링크한 .NET harness와 외부 Unity API stub, 2건. 실제 Unity 컴파일/Editor 시험 아님 |
| Git hash 배치 | 필요한 파일을 최대 256개씩 `hash-object --stdin-paths`로 처리한다. 공백·한글 경로, leading dash, binary/text·CRLF와 autocrlf false/true를 검사한다. 520개 새 텍스트 파일을 포함한 fixture의 초기/epoch 재시작/audit는 각각 hash 프로세스 3회, 변경 없음은 0회다. raw fingerprint와 경로별 단독 Git hash가 일치한다 | 실제 임시 Git worktree, 2건. provider는 fake이며 Unix 전용 이름 분기는 이번 Windows 실행에서 미실행 |
| 범위 읽기 index | 정렬된 extent에서 offset으로 이진 탐색하고 요청 끝에서 멈춘다. 512개 sparse extent의 앞/뒤/경계/hole/EOF/빈 요청에서 내용과 요청당 탐색 비교 최대 10·순회 항목 최대 3을 확인한다. truncate→확장의 zero-fill과 재연결도 확인한다 | 실제 Runtime 저널과 fake base, live/reconnect 2건. 기존 랜덤 겹침·압축 회귀도 함께 실행 |
| 계측 호환성 | `source.inspection.gitHashProcesses`, `storage.metrics.readRangeIndex`는 선택 필드다. 기존 누락/null은 unknown이며 음수는 schema가 거부한다. 응답과 overlay 관측의 read-range schema가 일치한다 | DTO 역직렬화·실제 CLI 응답·JSON Schema |

## 실행 기록

수정 전에는 기존 303개가 통과했고 추가한 빈 파일/복구 분류 10개가 모두 실패했다. `artifacts/review-fixes/before.trx`는 **총 314, 통과 303, 실패 10, 선택적 ReFS skip 1**이다. 빈 파일은 range hash 불일치로, 정리 오류는 `correct-request` 분류로 실패했다.

첫 수정 실행은 **총 320, 통과 318, 실패 1, skip 1**이다. 다섯 수정의 새 회귀는 모두 통과했고, 추가한 schema 시험이 일반 `git-checkout`의 hash 프로세스 수를 1로 잘못 기대해 실패했다. 이 경로의 기존 전체 검사는 `hash-object`를 사용하지 않으므로 기대값을 0으로 정정했다. 해당 결과는 `artifacts/review-fixes/first-fixed-run.trx`에 보존한다.

최종 표준 진입점 `pwsh ./scripts/build.ps1`의 locked restore와 Release 빌드는 경고/오류 0, 자동 시험은 **총 320, 통과 319, 실패 0, 선택적 ReFS skip 1**이다. ReFS는 `SPLINTER_TEST_REFS_ROOT`를 지정하지 않아 건너뛰었다. `artifacts/review-fixes/final-build.log`, `final.trx`, 각 실행의 counter/hash인 `test-summary.json`, 소스·계약·lock·시험 입력과 빌드 어셈블리의 `source-manifest.json`을 보존한다. 의존성 버전은 유지하고 Protocol→Core 및 테스트 harness 참조에 해당하는 lock만 갱신했다.

## 변경하지 않은 지원 경계

이번에는 live VSS/WinFsp mount, 실제 Unity Editor, ARM, 재부팅·전원·물리 공간 고갈과 대형 PV를 실행하지 않았다. 이전 FR/JW/WI/RU 결과를 이번 바이너리의 실행 증거로 승계하지 않는다. 논리 탐색량·hash 프로세스 수 개선을 전체 Editor 준비 시간, 디스크 할당 절감 또는 Library 용량 목표 달성으로 환산하지 않는다.

저널 형식·acknowledged write flush·원본 hydration 금지·소유 자원 회수 정책은 유지한다. loaded-code 증거는 기존 공통 fixture의 새 `inspect` 응답을 쓰도록 제품 문서와 형제 `splinter-unity`의 사용 문서를 맞췄다. 형제 저장소의 소스·에셋·설정과 canonical Editor는 변경하지 않았다.

현재 계약은 [validation view](../validation-view.md), [소스 검증·계측](../source-verification.md), [NTFS workspace](../ntfs-workspaces.md), [기반 구조](../foundation.md)에 있다.
