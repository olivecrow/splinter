# SF-02 Unity 재개와 작업공간 공간·정리 비용 검증

실행일: 2026-09-26. 범위와 판정 기준은 [QJ-01~04 계획](../plans/journal-compaction-qualification-and-cost.md)을 따른다. Windows x64, NTFS 격리 16GiB VHD, Unity `6000.3.22f1`, 실제 VSS/WinFsp/Git 제품 경로에서 작은 Unity fixture와 1MiB 합성 fixture를 사용했다. 소유 ID는 Unity `7610f94472b806a477ac8b53`, 합성 `19e0131aff5bda4ffad7e911`이다. 두 결과를 원본 브랜치에 통합하지 않았다. 기존 [SF-02 1차 기록](journal-compaction.md)과 [공통 저장소 FailedPreservation](storage-foundation.md)의 판정은 유지한다.

| 관문 | 이번 실행의 판정 |
| --- | --- |
| QJ-01 Unity 로드·파일 API | **Passed, 원인 귀속 제한**: 제품의 빈 하위 디렉터리 Win32 열거 결함을 재현·수정했다. 동일 snapshot의 정리 후 재개에서 예외 없이 브리지 준비와 스크립트 컴파일을 확인했다. 첫 예외 직전 Unity의 디렉터리 삭제·재생성도 있어서 그 예외를 단일 원인으로 귀속하지 않는다. |
| QJ-02 추가 물리 공간 장부 | **Partial**: Unity의 생성 전 기준·222개 완전한 10초 표본·정리 중 관측 최고치·후속 작업·release와 합성의 단계 표본을 보존했다. 합성 Git common의 생성 직전 할당은 계측하지 못해 초기 증가분이 unknown이다. 표본 사이 최고치와 NTFS 디렉터리/MFT 메타데이터도 unknown이다. |
| QJ-03 직접 비용 장부 | **Passed: 정의한 단일 실행**. 동일 compaction attempt의 원본 replay, scan, 후보 쓰기/flush, 후보 replay/seal, 게시, provider 재연결·이전 세대 정리의 tick·읽기/쓰기 바이트·flush 수와 CLI 경과를 보존했다. 쓰기별 flush 정책은 변경하지 않았다. |
| QJ-04 재개·결과·회수 | **재개 후 정상 경로 Passed, 첫 로드 종료는 운영 개입**. 수정 후 새 코드 14·현재 validation·제품 `unity stop`·독립 결과·소유 자원 0을 확인했다. 수정 전 첫 로드의 제품 stop 시간 초과는 소유 PID를 확인한 강제 종료로 기록한다. |

## 빈 디렉터리와 Unity 로드

정리 전 첫 import는 `ProjectInfo.projectLoad=1041.348`초 뒤에도 브리지 `application.ready=false`였다. Editor의 `DeleteUnusedAssemblies`에서 `Library/ScriptAssemblies` `FileNotFoundException`이 났다. [원본 저널 경로 이력](../../artifacts/storage-foundation/qj-20260926/script-assemblies-journal.json)은 이 디렉터리가 생성된 뒤 오류 무렵 삭제·재생성된 사실을 보인다. 같은 짧은 mount 경로에서 빈 디렉터리가 존재하고 `Directory.GetFiles`는 0개를 반환했지만, Win32 `FindFirstFileW(<dir>\*)`는 `ERROR_FILE_NOT_FOUND(2)`를 반환했다. 일반 NTFS의 빈 대조 폴더는 성공했다. [수정 전 파일 API](../../artifacts/storage-foundation/qj-20260926/empty-enumeration.json)을 따른다.

`WinFspOverlayFileSystem.ReadDirectoryEntry`와 단일 이름 조회가 하위 디렉터리에 실제 `.`·`..` 정보와 marker 순서를 제공하도록 고쳤다. 이는 저널에 가짜 파일을 기록하지 않는다. 수정된 provider로 같은 snapshot을 재연결한 뒤 두 폴더 모두 `FindFirstFileW`가 성공했다([수정 후 파일 API](../../artifacts/storage-foundation/qj-20260926/empty-enumeration-after.json)). 자동 callback 시험은 빈 하위 디렉터리·marker·이름 조회·저널 sequence 불변을 확인한다. 실제 Win32 비교와 Unity 재개가 별도 회귀 근거다.

첫 로드의 `unity stop`은 `EditorStopTimeout`이었다. PID `44924`의 시작 시각·Unity 실행 파일·작업공간 경로를 확인하고 그 소유 Editor만 강제 종료했다. 제품 status는 자식 프로세스 0·mount 핸들 0이었다([종료 응답](../../artifacts/storage-foundation/qj-20260926/unity-stop-before-fix.json), [소유 종료 영수증](../../artifacts/storage-foundation/qj-20260926/unity-owned-stop.json)). 정리 후 재시작 호출은 `SourceChanged`를 반환하면서도 소유 Editor PID `43372`를 시작했다. 파일이 안정된 뒤 `workspace status --verify-source`는 소스 변경 없음과 현재 fingerprint를 확인했고, 같은 Editor는 `ready=true`가 됐다. 재시작 로그에는 같은 `FileNotFoundException`이 없었다. [재시작 상태](../../artifacts/storage-foundation/qj-20260926/unity-status-restart-3.json), [편집 전 로드 코드](../../artifacts/storage-foundation/qj-20260926/unity-probe-before-edit.json)의 `Calculate(3)=13`을 따른다. 한 번의 성공 재개가 삭제·재생성 타이밍의 모든 조합을 보장하지는 않는다.

정리된 동일 snapshot에서 `Assets/SplinterSnapshotProbe.cs`의 `value + 10`을 `value + 11`로 바꿨다. [검증 상태](../../artifacts/storage-foundation/qj-20260926/unity-status-validated-1.json)는 `application.ready=true`, `sourceVerification=verified`, `validationCurrent=true`, `validation.success=true`, 오류 0이다. [로드 코드](../../artifacts/storage-foundation/qj-20260926/unity-probe-after-edit.json)는 같은 Editor PID에서 14와 새 assembly MVID를 기록한다. 수정 전 13을 소스 텍스트만으로 추정하지 않았다. 제품 [stop](../../artifacts/storage-foundation/qj-20260926/unity-stop-after-fix.json)은 성공했고 소유 프로세스 수·메모리는 0이었다.

## 원본을 제외한 추가 물리 공간

읽기 전용 [계측 스크립트](../source-verification.md)는 home의 control/delta·runner 파일 할당, 원본 `.git` common의 기준 대비 증가분을 더한다. `Library` 쓰기는 저널 안에 포함하고 mount의 논리 view를 다시 더하지 않는다. 원본 `Library`는 0인 fixture이며 제외한다. Unity `.git` common 생성 직전 기준은 6,075,552B다. [공간 요약과 모든 표본](../../artifacts/storage-foundation/qj-20260926/space-summary.json)은 각 범주의 논리 크기·NTFS 실제 파일 할당을 연결한다.

| Unity 단계 | 소유 추가 NTFS 파일 할당 B | 저널 파일 할당 B | 설명 |
| --- | ---: | ---: | --- |
| 생성 직전 | 0 | 0 | 원본 Git 기준을 별도 보유 |
| 새 workspace ready | 794,320 | 4,096 | control·runner·Git 등록 증가 포함 |
| 첫 import 후, provider unmount | 1,943,365,608 | 1,941,647,360 | 정리 직전 동일 세대 |
| 정리 중 관측 최고 | **3,590,531,672** | 1,646,452,736(새 세대) + 1,941,647,360(이전 세대) | 10초 표본 `unity-166`; 두 세대 3,588,100,096B와 기타 2,431,576B |
| 정리·재연결 직후 | 1,648,883,736 | 1,646,452,736 | 직전 대비 294,481,872B 감소 |
| 재개·검증·결과 수집 후 | **2,031,857,744** | 2,030,043,136 | 계속된 Unity 쓰기로 정리 전보다 다시 증가 |
| release 후 | 1,799,064 | 0 | 보존한 control/runner/Git 결과; 소유 mount·delta는 없음 |

저널의 정리 전/후 논리 길이는 1,941,645,422B/1,646,450,291B, 영수증의 실제 할당은 1,941,647,360B/1,646,452,736B로, 같은 `durable sequence=298899`를 유지했다([Unity compaction 응답](../../artifacts/storage-foundation/qj-20260926/unity-compact-2.json)). provider가 살아 있을 때의 정리 전 표본은 1,944,134,992B로 unmount 뒤보다 769,384B 컸다. 위 비교는 정리 호출 직전의 unmount 표본과 맞춘 것이다. 222개 10초 표본은 모두 열거 완료였지만, 이는 **관측 최고치**다. 디렉터리/MFT 메타데이터·표본 사이 순간 최고·VHD 동적 파일의 호스트 할당·OS/Unity 공유 캐시는 더하지 않았다.

합성 mount의 같은 4KiB 범위 100회 덮어쓰기에서 원본 SHA-256은 유지됐고 작업 view hash만 달라졌다([입력](../../artifacts/storage-foundation/qj-20260926/source-exercise.json)). 저널 할당은 정리 전 454,656B에서 8,192B로 줄었다. 다만 ready 후 합성 작업공간의 관측 추가 할당은 편집 후 1,223,856B에서 정리 직후 **1,478,984B로 증가**했다. 재연결 runner 파일이 692,568B에서 1,393,328B로 늘어 작은 저널 절감을 상쇄했다. 합성 `.git` common의 생성 직전 기준이 없어 그 최초 증가분은 unknown이며, 표는 ready 시점을 Git 기준으로 삼는다. 작은 fixture의 저널 절감률을 큰 Unity workspace의 전체 절감률로 대체하지 않는다.

격리 `V:` 볼륨의 VSS Used/Allocated는 snapshot 0개일 때 0/0, 두 개 준비 뒤 2,949,120/34,078,720B, 첫 import 뒤 27,754,496/101,187,584B, Unity 정리 직후 27,836,416/101,187,584B, 마지막 소유 release 뒤 0/0이었다([VSS 준비](../../artifacts/storage-foundation/qj-20260926/response-vss-ready.json), [첫 import](../../artifacts/storage-foundation/qj-20260926/response-vss-first-import.json), [종료](../../artifacts/storage-foundation/qj-20260926/response-vss-final.json)). 이는 볼륨 공유 공간 시계열이다. 작업별 절감이나 위 추가 할당에 귀속·합산하지 않는다. 다른 볼륨 `C:`의 VSS 수치는 이 장부에서 제외한다.

## 단계별 compaction 비용

Unity attempt `f3907db09b2f4c9d940af9a27bd3a4e9`의 `stopwatchFrequency=10,000,000` ticks/s, 저널 직접 정리 `totalTicks=489,805,151`(48.981초), 성공한 제품 CLI 호출 벽시계 88.124초다. 이전 `SnapshotPrivilegeRequired`/Observe 대기와 최초 import 시간은 이 88.124초에 포함하지 않는다. 주요 직접 stage는 다음과 같으며 모든 stage의 tick·읽기/쓰기 바이트·flush 수는 [응답](../../artifacts/storage-foundation/qj-20260926/unity-compact-2.json)에 있다.

| stage | 초 | 읽기 B | 쓰기 B | flush 횟수 |
| --- | ---: | ---: | ---: | ---: |
| 원본 replay | 15.331 | 1,941,645,422 | 0 | 0 |
| 후보 scan | 2.808 | 131,438,077 | 0 | 0 |
| 후보 write | 4.927 | 1,661,816,220 | 1,646,450,291 | 0 |
| 후보 flush | 0.441 | 0 | 0 | 1 |
| 후보 replay | 9.946 | 1,646,450,291 | 0 | 0 |
| 후보 seal | 1.856 | 1,646,450,291 | 0 | 0 |
| 게시 세대 확인 | 4.070 | 3,588,095,713 | 0 | 0 |
| provider 재연결 | 12.337 | 1,646,450,291 | 0 | 0 |
| 이전 세대 정리 | 7.802 | 0 | 0 | 0 |

의도 게시 두 번도 각각 `Flush(true)` 한 번으로 기록했다. `readBytes`는 stage에서 실제 읽은 저널 바이트이므로 여러 검증 읽기를 중복 합산해 고유 데이터량으로 해석하지 않는다. 첫 import 후 provider status의 `commitCount=298885`, `flushCount=298924`, 누적 `flushTicks=8524175572`(852.4초)는 현행 쓰기별 flush의 관측값이다([첫 import 상태](../../artifacts/storage-foundation/qj-20260926/unity-status-first-ready.json)). 이는 정책 개선의 성능·내구성 검증이 아니다.

## 독립 결과와 회수

Unity checkpoint는 변경 파일 `Assets/SplinterSnapshotProbe.cs`만 결과 커밋 `ccfa3a99a02c8b5e2991f1477d33e73451007ae4`로 수집했다. 최종 release manifest의 `stable-journal-v1` seal은 sequence 341656·저널 길이 2,030,016,783B·SHA-256 `78ea5f32ed51bccf8dc8439a1367c4262fe2ad725f5d1f514ac323fba27c1e9b`다. 합성 결과는 `fdc232a6856461bb277256fa95c2f4f7a1fdb5e9`와 sequence 105의 독립 ref로 남았다. 두 원본 Git 작업 트리는 clean이고 HEAD는 각각 기존 `58f81b5...`/`2aa147d...`를 유지한다([Unity checkpoint](../../artifacts/storage-foundation/qj-20260926/unity-checkpoint.json), [Unity release](../../artifacts/storage-foundation/qj-20260926/unity-release-2.json), [합성 release](../../artifacts/storage-foundation/qj-20260926/source-release-2.json)).

합성의 오래된 소유 snapshot을 먼저, Unity의 최신 소유 snapshot을 나중에 명시적으로 해제했다. 두 제품 상태는 `released`, 소유 Editor/자식·mount·delta·VSS ID는 0이고 `V:`의 VSS Used/Allocated도 0이다. 제한 관리자 세션은 정확한 두 ID만 처리했으며 [종료 영수증](../../artifacts/storage-foundation/qj-20260926/response-finish.json) 뒤 격리 VHD `Attached=false`, `V:` 부재를 확인했다. 일반 비소유 백업 공존이나 임의 순서 즉시 회수의 증거가 아니다.

전체 `pwsh ./scripts/build.ps1`의 locked restore와 Release 빌드는 경고·오류 0, 자동 시험 **265건 중 264 통과·0 실패·선택적 ReFS 1 skip**이었다. 비용 응답의 `response.schema.json` 계약도 자동 시험에서 검증했다. 실제 mount와 Unity 회귀는 위 별도 영수증으로 확인했다. SF-02 전체는 flush 정책과 전원/재부팅/공간 장애가 남아 **Partial**이다. 작은 Unity fixture에서 정리 중 최고 추가 공간이 정리 전보다 크게 늘었고, 계속 작업한 뒤 최종 추가 공간도 정리 전을 넘었다. 따라서 대형 Unity 프로젝트의 작업공간 용량 부담 해소를 달성했다고 판정하지 않는다.
