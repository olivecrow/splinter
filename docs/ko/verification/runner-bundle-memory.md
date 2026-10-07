# 실행 파일 묶음의 재사용과 준비 메모리

[English](../../verification/runner-bundle-memory.md)

이 단위는 **같은 home/바이너리의 디스크 재사용을 유지하면서 실행 파일 준비 함수의 관리 할당을 줄이는 범위**다. 종료 조건은 기존 내용 키·동일 파일 재사용·손상/소실 보존·동시 게시·실제 Windows 감시 프로세스 회귀와 고정 probe의 전후 대조다. Editor 준비 시간 단축·새 cache 회수/자동 정리·Unity/VSS 시험을 포함하지 않는다.

## 범위와 입력

- 기반은 `development @ 2aae895`와 현재 우선순위 문서다. 변경된 제품 코드는 `WindowsProcessWorkspaceProvider.PrepareRunner` 한 곳이며 공통 schema·상태/IPC·권한 서비스·프로세스 소유권 계약은 변경하지 않는다.
- 기존 제품은 이미 같은 home에서 6개 파일의 순서별 SHA-256을 결합한 내용 키로 묶음을 공유한다. 과거 [정리 비용](journal-compaction-qualification-and-cost.md)의 runner 할당 증가를 현재 동일 바이너리의 반복 복사 결함으로 승계하지 않는다.
- 전후 Release 실행 파일과 동일 개발 probe를 `artifacts/storage-foundation/runner-20261001-223559-965/`에 보존한다. `plan.json`·`before-binaries/`·`after-binaries/`, `probe/`·`before.json`·`after.json`과 필수 빌드 로그/TRX가 근거다. probe는 제품에 추가한 명령/모듈이 아니다.
- probe는 새 소유 cache를 한 번 만들고 같은 후보의 준비를 8회 재사용한다. 각 호출 안의 `GC.GetAllocatedBytesForCurrentThread` 차이와 실제 `FILE_STANDARD_INFO.AllocationSize`를 관측한다. 실행 파일 전체 내용 hash·파일 수·같은 경로와 전후 파일 할당도 확인한다.

## 현재 동작과 확인 조건

현재 파일/Git 소스 DLL을 포함한 원본 8개는 읽기 전용 핸들로 열어 hash·복사가 끝날 때까지 쓰기/교체를 막는다. hash 키의 구성 순서는 그대로이며 신규 복사와 cache hash 검사는 고정 크기 스트림을 사용한다. 반환/예외 뒤 원본 핸들을 닫는다. cache가 손상되거나 파일이 없으면 그 디렉터리를 보존하고 거부한다. 기존 cache를 자동 덮어쓰거나 옛 빌드 디렉터리를 삭제하지 않는다.

`RunnerBundleTests`는 기존 키·같은 경로/mtime와 bounded 관리 할당, 8개 동시 준비의 완전한 단일 게시, 8개 각각의 같은 길이 손상 거부, 소실 파일의 보존과 성공/실패 뒤 원본 config 핸들 해제를 확인한다. 실제 감시 실행/소유 자식 정리와 비소유 sentinel 보호는 기존 `WindowsProcessTests`를 사용한다. 새로운 Unity/전원/전체 native 장애 행렬을 열지 않는다.

## 결과와 제한

아래 실제 할당 probe는 당시 6개 후보의 근거다. 현재 8개 묶음의 자동 회귀와 구분하며 현재 후보의 직접 메모리 수치로 승계하지 않는다.

같은 probe의 8회 재사용 결과다. 전후 바이너리의 전체 내용 hash는 각각 보존한 manifest/JSON을 따른다.

| 관측 | 변경 전 | 변경 후 |
| --- | ---: | ---: |
| 6개 bundle의 논리 bytes | 1,001,668 | 1,001,156 |
| 생성 직후 bundle 파일 할당 (B) | 1,012,056 | 1,012,056 |
| 8회 재사용 뒤 bundle 파일 할당 (B) | 1,012,056 | 1,012,056 |
| 준비 함수 호출당 관리 할당 평균 (B) | 2,024,404 | 20,976 |

현재 함수의 재사용 관리 할당은 **98.96% 감소**했다. 변경 후 8회는 모두 20,976B였다. 기존 디스크 재사용은 유지됐고 물리 공간의 추가 절감은 이 변경의 결과로 주장하지 않는다. 바이너리 논리 길이의 512B 차이는 전체 물리 공간 최적화로 해석하지 않는다.

필수 `pwsh ./scripts/build.ps1`의 locked restore·Release는 경고/오류 0, **총 754 / 성공 753 / 실패 0 / 당시 제외 시험 건너뜀 1**이다. 최종 TRX에서 새 `RunnerBundleTests` 9개와 기존 `WindowsProcessTests.SupervisorSurvivesClientAndCleansDescendantThatOutlivesRoot`의 성공을 대조했다. 같은 묶음에서 실제 감시 프로세스가 실행되고 소유 자식/메모리 0과 비소유 sentinel 생존을 확인한 근거이며 새로운 Unity 자격은 아니다.

이 준비 메모리 단위는 **scoped-complete-with-limitations**로 마감한다. `source-before.cs`·`source-after.cs`·`source-and-binaries.json`·`result.json`과 최종 `automated.trx`를 보존한다. 서비스 설치/업데이트·fixture 변경·새 NTFS workspace·Editor·VIVOBOOK 실행 없이 완료했으며 기존 cache/원시 결과를 삭제하지 않았다.

계측은 함수의 현재 스레드 관리 할당이며 게임·다른 프로세스의 RAM이 포함되지 않는다. 누적 할당 감소를 상주 메모리/working set 또는 Unity Editor·전체 시스템 RAM 절감률로 해석하지 않는다. 파일 할당은 이 6개 bundle만 대상으로 하며 디렉터리/MFT·VSS·OS cache·workspace 전체 peak를 포함하지 않는다. 전후 바이너리 자체의 크기 차이는 코드 변경 비용이고 재사용 절감과 구분한다.

현재 후보의 검증은 준비 함수와 실제 Windows process group 경계에 한정한다. 기존 M0·Unity/WinFsp/VSS의 이전 후보 기록을 이번 바이너리 전체의 새 native 통과로 승계하지 않으며 게임 중인 시스템의 전체 메모리 비교도 수행하지 않는다.
