# SF-02 명시적 저널 공간 정리 검증

실행일: 2026-09-26, Windows x64/NTFS. 범위는 [확정한 1차 계획](../plans/journal-compaction-first-milestone.md)의 결과 수집 전 명시적 압축이다. 기존 [공통 저장소 실행](storage-foundation.md)의 `FailedPreservation`은 유지한다. SF-02 전체는 계측·압축 하위 범위의 증거와 flush 정책·장애 범위를 나눠 판정한다.

## 자동 계약과 중단 재개

`JournalDeltaStore`는 기존 acknowledged frame을 replay한 뒤 살아 있는 payload 범위만 새 후보에 chunk 단위로 쓴다. 16MiB 전체 재쓰기와 truncate 뒤의 live 4MiB, `.meta`, rename·delete, 변경 경로·sequence를 fake snapshot으로 대조하고 시험 볼륨의 저널 할당 감소를 검사했다. 후보 flush·재생·상태 hash와 전후 seal을 확인한 후 `File.Replace`로 전환한다. intent 기록, 후보 flush, 준비 영수증, 게시 직후의 네 지점에서 중단을 주입해 같은 identity의 재시도를 실행했다. 손상된 후보는 원본 저널을 바꾸지 않고 거부한다. 재연결된 provider가 저널 핸들을 열고 있을 때도 검증 후 이전 세대를 정리한다. 단일 Editor/프로세스/핸들 관측, 다른 workspace의 독립 상태, 반복 compact의 같은 attempt 반환은 coordinator fake로 검사했다. 전체 `pwsh ./scripts/build.ps1`의 locked restore/Release 빌드는 경고·오류 0이었다. Unity 첫 임포트와 겹친 시험의 프로세스 감독 1건은 시간 초과했으나, Editor와 VHD를 회수한 뒤 `pwsh ./scripts/build.ps1 -NoRestore` 전체 재실행에서 **263건 중 262 통과·0 실패·선택적 ReFS 1 skip**이었다.

이 검증은 프로세스 중단을 흉내 낸 결정적 경계 시험이다. 실제 강제 전원 종료·물리 공간 고갈·재부팅의 저널 세대 복구를 통과로 표시하지 않는다. 저널의 기존 쓰기별 `Flush(true)` 정책은 변경하지 않았다.

## 격리 NTFS/Git 제품 경로

기존 시험 소유의 16GiB 동적 NTFS VHDX를 정확한 이미지·disk ID·볼륨 경로로 다시 연결했다. `V:\sf-compaction-20260926` 아래에 새 1MiB 합성 Git fixture와 작은 Unity fixture를 만들었다. 원본 C:의 VSS 설정은 변경하지 않았다. 제한 관리자 세션은 두 소유 ID와 helper hash를 고정해 Capture/Observe/Release만 수행하며 일반 CLI는 비관리자로 실행한다. 이것은 제품의 권한 서비스 구현이나 UAC 제거 증거가 아니다.

합성 작업공간 `7a660f001fa171305b92d76e`의 실제 WinFsp mount에서 같은 4KiB 구간을 100회 덮어썼다. 원본 파일 SHA-256은 `D0B5218892F200A1CDD8AA5642FEAD94F8173F67CE19B0B74899D9F0FC6861CB`로 유지됐고 mount hash는 별도로 바뀌었다. 처음 compact는 provider 정지 뒤 새 VSS 관측이 필요해 `SnapshotPrivilegeRequired`를 반환했다. 같은 ID의 Observe helper 후 동일 명령을 재시도해 다음 결과를 얻었다.

| 저널 세대 | 논리 B | NTFS 할당 B | durable sequence |
| --- | ---: | ---: | ---: |
| 정리 전 | 453,147 | 454,656 | 102 |
| 정리 후 | 48,138 | 49,152 | 102 |

전환 시점의 두 세대 할당 합계는 503,808B였다. 이 값은 저널 두 파일의 겹침이며 작업공간 전체의 실제 peak가 아니다. 논리 감소 405,009B와 할당 감소 405,504B를 구분한다. 새 mount의 파일 hash가 정리 전 작업 view hash와 같고 원본 hash는 그대로였다. 이전 저널·intent는 재연결 확인 뒤 사라졌다. 같은 요청 재호출은 attempt와 seal을 바꾸지 않았다. 이후 새 편집·Git 커밋 `70dc0deebcf65c5f21bb2817c49bc52e7377ba2e`을 만들었고, 독립 결과 ref를 보존한 뒤 이 소유 작업의 snapshot/mount/delta를 회수했다. [압축 응답](../../artifacts/storage-foundation/compaction-20260926/compact-1.json), [재연결 대조](../../artifacts/storage-foundation/compaction-20260926/reconnected-check.json), [release](../../artifacts/storage-foundation/compaction-20260926/release-source-1.json)를 따른다.

두 번째 Unity 작업공간은 다른 ID와 snapshot으로 동시에 연결됐다. 합성 작업을 압축한 뒤 Unity mount에서 별도 파일 쓰기·읽기가 가능했고 원본 Unity Git 소스에는 그 파일이 생기지 않았다. 이는 다른 작업의 mount 존속과 격리를 보여 주며, 두 Editor의 동시 성능이나 대형 PV 비교를 뜻하지 않는다.

## 작은 Unity 저널과 재개

별도 소유 작업공간 `d97e7152babb9e543ea02737`은 Unity 6000.3.22f1의 작은 fixture를 실제 WinFsp mount에서 열었다. 첫 import의 Editor 로그에는 `Project Loading completed in 1183.380 seconds`와 실제 scene·texture·FBX 임포트가 있다. Editor가 로드한 뒤에도 패키지 추가 임포트가 계속돼 Splinter 브리지 heartbeat가 발행되지 않았다. 제품 `unity stop`은 `EditorStopTimeout`을 반환했다. 정확한 소유 PID·시작 시각·Editor 실행 파일·해당 작업공간 경로를 대조한 뒤 소유 Editor만 종료했고, 제품 상태에서 자식 프로세스 0·mount 핸들 0을 확인했다. 이 운영 개입을 정상 종료 통과로 집계하지 않는다.

첫 `workspace compact`는 `SnapshotPrivilegeRequired`에서 멈췄고, 소유 snapshot의 제한 Observe 후 같은 명령을 재시도했다. 후보 게시 후 첫 연결은 `OverlayStartUncertain`을 반환했으나 status에 같은 snapshot·새 세대 provider가 살아 있음을 확인하고 동일 명령으로 완료했다.

| Unity 저널 세대 | 논리 B | NTFS 할당 B | durable sequence |
| --- | ---: | ---: | ---: |
| 정리 전 | 2,347,144,214 | 2,347,147,264 | 422,868 |
| 정리 후 | 1,721,650,294 | 1,721,651,200 | 422,868 |

논리 감소는 625,493,920B, 실제 저널 할당 감소는 625,496,064B다. 세대 전환 시 두 저널의 할당 합계는 4,068,798,464B로, 이 순간의 저널 파일 두 개만 포함한다. Library·Git·control·VSS 공유 공간의 최고치가 아니며, 작은 합성 저널의 절감률과 합산하지 않는다. 재연결 후 이전 세대와 intent는 사라졌고, 원본과 다른 작업공간의 snapshot ID는 유지됐다. [압축 결과](../../artifacts/storage-foundation/compaction-20260926/unity-compact-3.json)와 [정리 전 할당](../../artifacts/storage-foundation/compaction-20260926/unity-allocation-before.json)을 따른다.

압축된 mount에서 동일 Editor를 다시 열었고 `ProjectInfo`의 `projectLoad=65.454`초와 같은 Unity 버전·원본 snapshot·작업 ID를 확인했다. 재시작 로그의 `Library/ScriptAssemblies` `FileNotFoundException`은 script compilation 경로에서 발생했으며 브리지 heartbeat가 없어 `application.ready=false`였다. 따라서 Unity 스크립트 검증·정상 종료는 실패/미완료로 둔다. 같은 소유 PID·시작 시각·경로를 대조해 Editor를 종료했고, 프로세스·핸들 0을 확인했다. 압축 후 mount에서 `Assets/CompactionPeer.txt`와 `.meta`가 유지됨을 읽었고 두 파일만 소유 브랜치에 커밋했다. Unity validation을 통과한 제품 checkpoint라고 표시하지 않는다.

## 마감 장부와 남은 경계

| 관문 | 관측과 판정 |
| --- | --- |
| 합성 제품 mount 정리·재개·독립 결과 | Passed: 같은 snapshot의 후속 편집·Git 결과 `70dc0deebcf65c5f21bb2817c49bc52e7377ba2e`, release 완료 |
| 작은 Unity 실제 로드·정리·재시작 | Partial: 첫 로드와 재시작 로드, 625,496,064B 저널 할당 감소·동일 sequence 확인. 브리지 ready/스크립트 validation은 실패 |
| Unity 독립 결과·소유 자원 | Passed (운영 개입): 에셋 두 파일의 Git 결과 `9d6335bf6f6b1efbfb6d7924b6ec5dc1ab5f3389`와 `stable-journal-v1` ref를 보존하고 소유 프로세스·핸들·mount·delta·snapshot 0 확인. Editor는 정확한 소유 신원 대조 후 종료 |
| 격리 볼륨 종료 | Passed: 두 작업 모두 `released`, 제한 관리자 세션의 두 소유 VSS ID 0 확인 후 VHD 분리. `V:` 부재 확인 |

Unity 원본 Git HEAD `58f81b5bc6ff8a940d8a90872bd3fed858a54cc1`은 유지됐고 결과 ref `refs/splinter/snapshot-results/d97e7152babb9e543ea02737`은 `9d6335bf6f6b1efbfb6d7924b6ec5dc1ab5f3389`를 가리킨다. [Unity release 응답](../../artifacts/storage-foundation/compaction-20260926/unity-release-2.json), [snapshot 해제](../../artifacts/storage-foundation/compaction-20260926/response-release-unity.json), [VHD 분리](../../artifacts/storage-foundation/compaction-20260926/response-finish.json)가 근거다.

VSS Used/Allocated는 볼륨 공유 관측이므로 작업별 절감액으로 귀속하지 않는다. 첫 임포트의 Library·control·Git·독립 결과까지 포함한 전체 peak는 측정하지 못했다. 저널 scan·후보 쓰기·flush·seal 각각의 직접 시간/읽기 바이트도 분리 계측하지 않았다. provider의 재연결 replay는 status metrics에 있으나 전체 압축 비용으로 대체하지 않는다. 전원 장애·비소유 백업 공존·임의 순서의 즉시 snapshot 회수·100GB 프로젝트와 단일 Editor 후보도 이 시험 범위 밖이다. 기존 [공통 저장소 실행](storage-foundation.md)의 `FailedPreservation`은 변경하지 않는다.
