# NTFS snapshot-overlay 실행 증거

[English](../../verification/snapshot-overlay.md)

2026-09-21 Windows 11 Home 10.0.26200, NTFS C:, .NET 10.0.112, WinFsp 2.2.26215 Beta4, Unity 6000.3.22f1에서 **SO-01~06 fixture 검증을 완료했다.** 최초 전체 복제·원본 이동·읽기 hydration 없이 범위 저장과 실제 Git/Unity 실행, 같은 기준 재연결, 결과 보존과 소유 자원 회수를 확인했다. 제품 workspace backend 지원·재부팅·전원 손실·범용 Unity 지원을 뜻하지 않는다.

원시 증거는 저장소 artifacts/snapshot-overlay/에 보존한다. 아래 S는 run-20260921-a/, U는 unity-20260921/, setup은 설치·승인된 VSS 설정 영수증이다. source/delta/mount는 회수했다. 독립 결과·작은 journal 장애 표본·로그·JSON은 보존했다.

| 관문 | 상태 | 실제 증거 |
| --- | --- | --- |
| SO-01 | PassedFixture | 비희소·비압축 난수 16MiB/256MiB/1GiB, 원본 C: 유지, VSS capture 16,031ms, A/B 최초 journal/payload 0. S/source-files.json, S/01-initial.json, snapshot receipt의 볼륨/공급자. Unity clean HEAD·독립 index는 U/git-*-initial.json |
| SO-02 | PassedFixture | 전체 반복 SHA-256 일치. 수정 측정에서 B 2,717,908,992 bytes 읽기에도 delta allocation 8,192→8,192 bytes. engine read cache 0. S/02-reads.json |
| SO-03 | PassedFixture | 16MiB/256MiB/1GiB에 동일 4KiB 쓰기: journal +4,521/+4,525/+4,529 bytes, 합계 allocation +12KiB. mmap 경계, truncate 후 확장 0, rename/교체 성공. S/03-physical-small-writes.json, S/03-filesystem-operations.json |
| SO-04 | PassedFixture | 원본 쓰기·삭제·rename 중 A/B t0 유지, private marker 독립, C는 새 t1 관측. 원본 Editor 33, A/B 103/203 실행. S/04-source-and-private-writes.json, S/04-new-snapshot.json, U/original-live-probe-33.json, U/unity-*-resumed-probe.json |
| SO-05 | PassedFixture | 원본을 연 채 GUI A/B 실행·독립 Git index·자식 I/O. 추가 변경 후 A=104/B=204, 새 request/fingerprint와 domain·assembly ID 확인. 생성/변경/rename/삭제 알림, 공유 잠금 32, byte-range 잠금 33. U/unity-*-final-validation.json, U/unity-*-final-probe.json, S/05-notifications-locks-child-io.json |
| SO-06 | PassedFixture | 같은 snapshot/delta 재개, write/flush 중 provider 종료, quota disk-full, torn/checksum 손상, 실제 소유 snapshot 소실, export 재조정, 열린 핸들 회수 대기, 최종 process/메모리/mount/Git/delta/snapshot 회수 |

## Unity와 공간·메모리 비용

실제 독립 fixture는 C:/Users/olive/Desktop/Development/splinter-unity다. 캡처 기준은 c0a60da2a889f03cf514536658fded605dc5b68d의 clean development와 저장 완료·컴파일 idle 원본이다. 원본 Editor PID 25756은 계속 열어 두었다. VSS capture 16,843ms, 파일 33,500개, 논리 1,956,139,527 bytes, file-data allocation 합계 1,995,396,232 bytes였다. Library 논리 1,939,726,219 bytes였다. 전체 metadata 계측 자체에 237,538ms가 걸렸으므로 생성 지연에 섞지 않는다.

| 항목 | A | B |
| --- | ---: | ---: |
| Editor 전 Git 연결 뒤 journal / private pointer payload | 851 / 81 bytes | 851 / 81 bytes |
| Editor 전 delta file-data allocation | 4,688 bytes | 4,688 bytes |
| 최종 실행 중 관측 delta allocation | 600,375,888 bytes | 618,725,968 bytes |
| 종료·결과 보존 뒤 최종 journal | 603,464,860 bytes | 619,874,195 bytes |
| 누적 Library 쓰기 payload | 537,143,970 bytes | 552,750,892 bytes |
| 마지막 제공자의 OS peak working set | 130,629,632 bytes | 127,500,288 bytes |
| 최종 실행 중 앱 Job active processes | 21 | 19 |
| 같은 시점 앱 Job working set | 5,474,381,824 bytes | 5,356,871,680 bytes |
| 같은 시점 앱 Job private bytes | 6,244,192,256 bytes | 6,071,443,456 bytes |
| 최종 소유 앱/제공자 메모리 | 0 | 0 |

실행 중 allocation은 종료 직전 값이며 종료 시 추가 write까지 포함한 최종 journal과 시점이 다르다. 비용은 두 차례 Editor 실행·소스 컴파일·bridge 갱신·생성 IDE 파일·반복 로그를 포함한다. Library payload는 Unity가 요청한 누적 쓰기이며 최초 원본 복사량이 아니다. hydration 0이고 compaction은 없다. 앱 Job 수치는 관측 시점 합계이며 모든 실행의 연속 peak가 아니다. 공유 OS system cache resident 1,187,045,376 bytes는 별도 기록했다. C/D/E/G의 전후 free space는 snapshot receipt와 measurement JSON에 있으며 외부 쓰기 잡음이 포함된다.

실제 재개 Editor PID는 A 74364, B 7100이다. 첫 시작에 상당한 Unity 임포트가 발생했고 약 4~6분 수준의 준비가 관측되었다. 재개는 unity-*-launch-2.json과 unity-*-resumed-probe.json의 시각으로 추적한다. 최종 incremental compile은 요청 뒤 약 40초 내 완료했으며 실제 로드된 assembly MVID가 생성 DLL과 일치했다. 빠른 Editor 시작이나 일반 Unity 성능 우위까지 확정하지 않는다.

## 장애와 발견한 호환성 문제

- 06-held-handle-and-reconnect.json: 열린 핸들에서는 unmount 대기. 닫은 뒤 정상 해제와 같은 기준 재연결로 승인 데이터를 보존했다.
- 06-provider-kill-during-write.json: write-through child와 flush 중 정확한 소유 provider를 종료했다. 외부에 기록한 승인 1,835,008 bytes를 모두 복구했다. 추가 영속된 미응답 마지막 transaction은 허용한다.
- 06-quota-disk-full.json: 64KiB journal budget에서 Windows 112, 승인 payload 57,344 bytes를 재연결 뒤 확인했다. host 볼륨 실제 고갈은 NotRun이다.
- 06-journal-recovery.json: 미완성 tail 19 bytes만 제거하고 승인 journal hash를 유지했다. 완결 checksum 손상은 DeltaCorrupt, 손상 journal을 byte-for-byte 보존했다. 시험에서 주입한 byte만 검증된 backup으로 복원한 뒤 같은 기준을 재연결했다. 일반 손상의 자동 복구가 아니다.
- 06-snapshot-loss-observed.json: 결과가 독립 보존된 C/fault/quota의 t1만 실제 삭제했다. C는 snapshot-missing, 읽기/쓰기 오류 21, 재마운트 SnapshotMissing. 원본 우회·새 캡처 없이 A/B와 기존 복원 지점을 유지했다.
- 결과 export 중 hash handle 공유 오류로 중단된 뒤 이미 쓴 파일을 overlay와 재검증하고 manifest를 완성했다. 첫 실패에는 최종 승인 manifest가 없었다. 일반 임의 partial-file 자동 복구는 미구현이다.

첫 Unity 실행에서 snapshot의 Library/ilpp.pid가 원본 helper PID를 가리켜 Unity가 원본 helper를 종료했다. 원본 Editor와 코드 결과는 유지됐지만 격리 결함이었다. 실행 전 **작업별 ephemeral PID/port만 whiteout**하도록 수정한 뒤 A/B를 같은 snapshot/delta로 다시 실행했고 원본의 새 helper PID 1924 생존을 확인했다. 원본 Library 전체나 전역 MCP 설정을 삭제하지 않았다.

첫 WinFsp 제공자는 커널 cache context를 열린 사용자 핸들로 집계해 앱 종료 뒤 회수가 대기했다. 두 앱 Job 0을 확인한 뒤 정확한 소유 provider를 종료하고 같은 저널로 재개했다. 현재 Cleanup/Close를 분리하여 openHandles와 retainedFileContexts를 관측한다. 최종 회수는 정상 unmount와 제공자 종료로 완료했다.

기존 probe의 초기 callback 보고서는 후속 reload 뒤 갱신되지 않았다. compiler 성공만으로 통과하지 않고 ready bridge에서 실제 로드된 함수를 호출하여 104/204와 assembly MVID를 확인했다. 첫 강제 전체 script compile의 미완료 기록은 U/first-run-*에 보존하며 성공으로 세지 않는다.

## 독립 결과와 최종 회수

| 결과 | 보존 위치 |
| --- | --- |
| A 소스 | fixture branch codex/so-20260921-a, commit bfaabd522ab5cdcf366d29ba41f25e3bcc8b80b2, ref refs/splinter/snapshot-results/so-unity-a |
| B 소스 | fixture branch codex/so-20260921-b, commit 87eb6177c3541699c42608297d3c0c03818d96cd, ref refs/splinter/snapshot-results/so-unity-b |
| 합성 binary 결과 | S/results/{a,b,c,fault,quota,compat}/result.json과 SHA-256 검증 파일. 약 2.70GB, 최초 workspace 복사와 별도 결과 보존 비용 |
| 원본 복원 | U/original-restored-probe.json: PID 25756, Calculate(3)=13, compiling/updating false. 재사용 설정과 문서 commit만 유지 |

S/06-reclamation.json과 U/06-reclamation.json은 모든 current manifest·durable sequence/hash/ref 확인 뒤 소유 process 0, memory 0, open handle 0, mount 없음, Unity Git 등록 없음, delta 0을 기록한다. final-owned-temporary-cleanup.json은 시험 난수 source와 immutable runner cache 제거를 기록한다. raw 로그·작은 장애 표본·독립 결과는 의도적으로 남긴 산출물이다.

final-vss.json에서 owned t0 {14D604AB-4D25-4DF4-84D5-BE4ED8379CE8}, t1 {B00DEE89-0C00-4B02-846A-7B02706207DF}, Unity {AB041222-44EC-408F-BF25-03C9E100FB36} 부재와 기존 복원 지점 4개 보존을 확인했다. 승인된 WinFsp 설치는 유지한다. C: VSS 상한은 39,986,397,184→68,719,476,736 bytes로 증가했고 자동 축소하지 않았다. 최종 used 40,111,124,480 / allocated 42,959,630,336 bytes는 기존 지점과 프로젝트 밖 쓰기를 포함한 볼륨 합계다. 삭제 후 VSS allocation 즉시 축소나 작업별 정확한 preserved bytes는 주장하지 않는다.

## 자동 검증과 남은 범위

pwsh scripts/build.ps1: locked restore, 빌드 경고/오류 0, 69개 중 68 통과·당시 시험 1개 skip. snapshot fake 11개는 범위/겹침/읽기/재생/소실/결과 해제 조건을 검사하며, mount 관측 회귀 2개는 실패 상태의 메모리 필드 누락을 0으로 간주하지 않는지와 미시작 상태를 검사한다. 실제 증거를 fake로 대체하지 않았다. 준비와 Unity 복합 보존 wrapper는 검증된 구성 명령을 재현하기 위해 추가했으며 전체 wrapper를 새 fixture로 다시 실행한 증거는 없다. PowerShell 21개와 journal accounting Python의 구문 검사도 통과했다.

초기 allocation 측정 오류는 S/measurement-v1-invalid/invalidity.json에 기록했다. 그 묶음과 이전 filesystem-operations의 물리 allocation 필드는 무효다. hash·operation 결과는 유효하며 현재 02/03 physical 측정은 FILE_STANDARD_INFO.AllocationSize로 고쳤다.

재부팅·전원 손실, host 물리 공간 고갈, 모든 mount/flush/release 명령 경계의 강제 종료, ADS/hardlink/reparse/EFS/ACL, dirty/LFS 원본, 패키지·임의 에셋 변경, PlayMode/Unity Test Framework는 NotRun 또는 Unsupported다. 실제 1TB 데이터셋을 실행하지 않았다. 통합 판단은 **후보 유지 및 제품 coordinator 설계 진행 가능**이며 상용 지원/성능 확정은 보류한다.

## 현재 개발 script의 자동 회귀 경계

`SnapshotScriptLifecycleTests`는 외부 명령 대역과 자기 임시 파일에서 source 보존 → provider 종료 → stopped journal 계측 → Git 등록 회수 순서를 검사한다. unmount/계측 실패는 독립 source 영수증과 journal·등록을 보존하고 후속 회수를 중단해야 한다. 내부 I/O 모듈의 import는 caller 경로 변수와 타입을 변경하지 않으며 명시적 두 run 경로의 JSON 게시와 범위 쓰기/읽기 결과를 검사한다.

현재 helper의 Windows 할당 조회는 자기 8,192B 파일 한 개의 logical/allocated 합계 일치만 확인했다. 근거는 `artifacts/code-efficiency-fixes/helper-allocation/report.json`이며 VSS·Unity·전체 workspace 비용의 새 자격이 아니다. 저널 계측기 회귀는 `artifacts/code-efficiency-fixes/journal-accounting-tests.log`, 필수 locked restore/Release 빌드는 경고·오류 0이며 자동 회귀는 총 1,061개 중 1,051 Passed / 0 Failed / 10 conditional Skipped다. 로그와 현재 TRX는 `artifacts/code-efficiency-fixes/required-build.log`·`required-build.trx`를 따른다. 기존 SO-01~06 native 실행은 당시 후보의 근거로 유지하며 이번 wrapper/helper 후보의 실제 Unity/VSS 재실행은 NotRun이다.
