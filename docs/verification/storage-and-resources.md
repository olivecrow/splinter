# 저장소·메모리 보강 검증

> 이 기록은 최초 전체 ReFS 기준본이 준비된 조건의 내부 알파 증거다. NTFS 최초 복제 없음·읽기 복제 없음·원본 동시 수정 스냅샷의 SO 관문 통과를 뜻하지 않는다. 약 297MiB는 기준본 준비 이후 A 생성의 증가량이다.

2026-09-21의 내부 알파 검증이다. 큰 파일의 반복 물리 복사를 피하고, worktree 회수 완료 시 소유 프로세스와 메모리를 남기지 않는 조건을 검사했다. 전체 공개 시나리오·성능 SLA의 통과를 뜻하지 않는다. 재현 계약은 [저장소·자원 관리](../storage-and-resources.md), 작은 기계용 결과 묶음은 [실행 증거](storage-and-resources.evidence.json)에 있다. 원시 JSON은 무시된 `artifacts/resource-alpha/`에 보관한다.

## 환경과 소스

- Windows 11 Home build 26200, .NET SDK `10.0.112`, Unity `6000.3.22f1`, Coplay `10.2.0`.
- 원본 fixture/common Git directory는 NTFS의 `../splinter-unity`. 원본 development는 `66820470038c85319bc4d47d1ff2859cab82e928`이며 HEAD·index·작업 파일을 보존했다.
- 사용자가 승인한 새 동적 64GiB VHDX를 `.local/dev-drive/splinter.vhdx`에 만들고 **S: ReFS Dev Drive**로 연결했다. 클러스터는 4096 bytes. 기존 파티션은 변경하지 않았고 자동 시작 mount도 등록하지 않았다.
- 시험 home은 `S:\splinter-resource-alpha`, 대용량 자동 시험은 `S:\splinter-tests`다. 실행 당시 제품 기준 commit과 수정 소스 digest·실행 파일/TRX hash는 기계용 증거에 기록한다.
- 스크립트 한 파일만 다른 시험 commit `79a367c27c3419d37758af67d454652f901dc58c`는 별도 `splinter/resource-cache-seed` ref에 만들었다. 원본 checkout·index·development/main은 바꾸지 않았다.

## 결과

| ID | 시험 | 상태 | 근거와 범위 |
| --- | --- | --- | --- |
| SR-01 | 루트보다 오래 사는 자식과 감시 프로세스 회수 | Passed | 실제 Windows job, PowerShell 루트/64MiB 자식/별도 sentinel. 재관측 후 소유 프로세스·메모리 0, sentinel 생존 |
| SR-02 | 자식 잔류·소유권 불명 상태에서 worktree 삭제 차단 | Passed | Runtime fake. source release 미호출, 상태 회복 후 회수 |
| SR-03 | CoW 불가 시 전체 복사 fallback 차단 | Passed | NTFS 실제 probe와 fake Runtime. Git 생성 전 거부 |
| SR-04 | 무-checkout worktree·독립 index·증분 소스 반영 | Passed | 실제 임시 Git + fake clone. 생성 직전 `.git`만 존재, 재접속 중복 clone 없음, 새 revision의 변경 파일만 갱신 |
| SR-05 | 기준본 검증·변경 감지·소비자 보호 | Passed | fake/파일 시험 및 실제 활성 seed의 `BaselineInUse` 거부. 유효한 검증 없는 봉인과 변경 캐시 삭제 차단 |
| SR-06 | 실제 대용량 ReFS clone·중첩 clone·쓰기 격리 | Passed | 512MiB+3 bytes, 정렬 파일, 0바이트·빈 디렉터리. A에서 복제한 B도 보존, hash 검사, 추가 할당량 상한 검사 |
| SR-07 | 예열 Library로 두 GUI Editor 동시 실행 | Passed | A/B의 서로 다른 PID·GUI 창·제어 경로. A 변경·컴파일 중 B/원본/기준본 소스와 B/원본 assembly hash 유지. B도 검증 성공 |
| SR-08 | 실제 Unity 전체 프로세스·worktree 회수 | Passed | A/B 각각 releasedResources의 0 증거, 관측한 소유 프로세스 40개 모두 종료, 두 폴더·Git 등록 제거. 원본 Editor/MCP 유지 및 실제 호출 성공 |
| SR-09 | 소스 변경 커밋의 기준본 재사용·봉인·명시적 GC | Passed | 실제 변경 소스 커밋을 refs-cow로 준비·compile·봉인. 준비 Editor/폴더 회수와 새 캐시 삭제, 기존 기준본 재요청 시 재예열 없음 |
| SR-10 | Editor가 살아 있는 동안 CLI 종료·출력 완료 | Passed | 최종 감시 구현의 A/B start 응답 1,979/2,054ms. 상속 가능한 익명 pipe를 닫는 실제 Windows 회귀 시험 포함 |

`SPLINTER_TEST_REFS_ROOT=S:\splinter-tests`로 실행한 `pwsh ./scripts/build.ps1`은 locked restore, 경고/오류 0, **56 passed / 0 failed / 0 skipped**다. ReFS 경로를 생략한 실행은 SR-06을 Skipped로 처리한다. 실제 GUI 검증은 이 자동 시험 수에 포함하지 않는다. 이전 일반 checkout의 [AA 검증](internal-alpha.md)과 이번 CoW 증거도 구분한다.

## 저장소 측정

최초 cold 준비를 검증·종료한 뒤 기준본 `51f7ee1d3abfc7c859cee60b`에 소스와 Library를 봉인했다. 원본의 실행 중인 Library는 사용하지 않았다.

| 항목 | A | B |
| --- | ---: | ---: |
| 논리 파일 bytes | 2,017,545,775 | 2,017,545,775 |
| 성공한 native clone bytes | 1,963,737,088 | 1,963,737,088 |
| 꼬리·작은 파일 copy bytes | 53,808,687 | 53,808,687 |
| 파일 수 | 41,604 | 41,604 |
| clone 호출 시간 | 51,741ms | 63,199ms |
| ensure 전체 시간 | 78,885ms | 91,113ms |
| 정상 release 전체 시간 | 13,395ms | 12,226ms |

파일 내용의 약 **97.3% 범위**가 native block clone이었다. 이는 물리 디스크 절감률이 아니다. A의 생성 전후 S: 여유 공간 감소는 **311,017,472 bytes(약 296.6MiB)**였다. 약 1.88GiB의 논리 파일과 별도로 파일·디렉터리 메타데이터와 작은 쓰기의 비용이 있다. B 측정에는 A Editor 시작이 겹쳤으므로 디스크 증가량 비교의 독립 표본으로 사용하지 않는다.

대용량 합성 시험의 조용한 측정에서는 약 1GiB의 두 clone을 만든 추가 할당량이 **557,056 bytes**였다. 최종 빌드의 동일 시험에서도 물리 할당 상한과 쓰기 격리가 통과했다. 실행 시점별 값은 증거 JSON에 구분한다. 삭제 직후 여유 공간 수치는 ReFS의 지연 회수를 포함하며 VHDX 파일이 즉시 작아진다는 뜻도 아니다. 준비 시간은 단일 fixture 관측이며 일반 복사 대비 속도 배수나 대형 프로젝트 SLA를 주장하지 않는다.

기준본은 개발 중 CRLF/LF 호환 키 문제를 발견하기 전에 봉인했다. 기존 키가 정확한 버전·브리지 내용과 일치함을 검사한 후 이 시험 캐시의 메타데이터 키만 정규화했다. 트리 내용과 manifest는 바꾸지 않았다. 새 구현은 처음부터 정규화된 키를 생성한다. 이 한 번의 시험 데이터 보정은 일반 캐시 migration 지원의 증거가 아니다.

## 종료 상태와 다음 범위

A/B의 종료 결과는 `ownershipVerified=true`, `rootRunning=false`, `activeProcesses=0`, `workingSetBytes=0`, `privateBytes=0`이다. OS의 파일 캐시·standby RAM을 비우거나 전체 PC 사용량을 동일하게 맞춘 결과가 아니다. 원본 Editor PID `25756`과 MCP PID `19096`은 보존했고, 회수 후 MCP에서 `Increment(3)=4`, `Double(3)=6`을 확인했다.

최초 준비, A/B, 증분 준비의 네 worktree가 모두 `released`이며 모든 경로와 소유 root/supervisor가 사라졌다. 최종 home의 workspaces는 비어 있고 원본만 Git에 등록돼 있다. 증분 기준본 `b285120ac7846a909ecc09c7`의 캐시도 명시적으로 삭제했다. 최초 기준본 재요청은 기존 ID를 반환했다.

이 시험 종료 시점에는 최초 기준본 하나와 작은 기록·로그·결과 ref를 디스크에 유지했다. 이후 사용자의 정리 요청에 따라 기준본과 시험 VHDX를 회수한 현재 상태는 [정리 기록](storage-cleanup.md)에 있다. 기준본 자체에는 상주 프로세스가 없다. 캐시 삭제는 `baseline release`의 독립 계약이다. 실행 과정에서 만들어진 파생 파일을 사용자 결과와 함께 강제로 정리하지 않는다.

다음 작업은 [NTFS 스냅샷·변경분 검증](../plans/ntfs-snapshot-overlay-spike.md)의 SO-01~06이다. 기존 clone/seal 복구 확장보다 최초 전체 복제 없는 저장소와 원본 동시 수정의 증거를 우선한다. SR 결과는 실행 당시의 좁은 범위로 보존한다.
