# NTFS workspace 통합 검증

상태: **NI-01~08의 1차 내부 알파 구현·자동 및 실제 fixture 검증·회수 완료**. 날짜: 2026-09-21. [확정 범위](../plans/ntfs-workspace-integration.md)와 [현재 계약](../ntfs-workspaces.md)을 따른다. 실제 dirty 원본의 통합 차단·원본/결과 보존도 확인했다. 공개 제품 전체 지원으로 확대하지 않는다. SO/AA/SR 결과는 별도 증거다.

## 환경과 증거 식별

- 실행 당시 제품은 `development`의 착수 HEAD `17cee45190c9ac6800fd1737321ea4286a8696df` 위에 변경을 적용한 후보였다. 검증 대상은 아래 소스 입력 hash로 식별한다. 코드·계약·브리지·스크립트·빌드 입력 117개의 SHA-256 목록은 `evidence/source-manifest.json`에 있다. 정렬한 path/hash JSON의 SHA-256은 `58d097a406d76cd09f580903ddab2714a399800142ab74e75e35e4c71338ee85`다. 문서는 이 hash에 포함하지 않는다. 출시 SHA의 qualification은 아니다.
- Unity fixture는 `C:/Users/olive/Desktop/Development/splinter-unity`, `development`, `46e7f4dbdb219e4afa602f3598a3de9ff4070fae`, Unity `6000.3.22f1`이다. 원본 Editor PID `25756`은 계속 실행했다. 캡처 전 MCP에서 compile/import/Play/scene dirty가 모두 false임을 확인했다.
- 아래 상대 증거 경로의 기준은 제품 저장소의 `artifacts/ntfs-workspace/ni-20260921/`다. `home/records/<id>`에는 지속 상태와 소유권 영수증, `evidence/`에는 CLI 응답·측정·TRX가 있다. 로컬 artifacts는 Git 추적 대상이 아니다.
- 일반 `scripts/splinter.ps1 workspace ensure --storage ntfs-snapshot`으로 생성했다. 캡처/해제만 CLI가 발행한 정확한 VSS helper를 UAC로 실행했다. 일반 CLI와 Unity는 관리자 프로세스로 실행하지 않았고 SO 실험 mount 요청을 제품 생성에 대신 사용하지 않았다.

| 작업 | key / workspace ID | 캡처한 VSS ID | 독립 결과 revision |
| --- | --- | --- | --- |
| A | `ni-20260921-a` / `d3950bd69315ea337cfee26e` | `{D07DDDA4-771B-4B8A-A750-16CB849265A4}` | `02ad95182c794954bfa40239e925a15b2d77d102` |
| B | `ni-20260921-b` / `aecc7c5deec607ac2d927e9a` | `{1472A734-EF7A-4684-81C8-D072BA425797}` | `9828f6fa5e90aa4090562d42623f62e43418ffcb` |
| 바이너리 | `ni-20260921-range` / `a078ea4e572e501c7aeccdb1` | `{A2801D3B-61AE-44D8-9F2E-8300E80A4374}` | `379d03b065a142235fcb7a345fa9511cc5656650` |

각 결과는 해당 원본 저장소의 `refs/splinter/snapshot-results/<id>`에 있다. 바이너리 입력은 run 아래 별도 소유 Git fixture이며 Unity 에셋으로 임포트하지 않았다. 결과 ref와 tree 객체는 mount/delta 회수 후 원본 저장소에서 다시 확인했다.

## NI 상태

| 항목 | 현재 상태 | 근거와 남은 실행 |
| --- | --- | --- |
| NI-01 계약·상태·CLI | Passed: 자동/fixture | storage/schema/capabilities, 권한 부족 nextActions, 실제 캡처→Git 등록→mount→Ready, unknown 관측 구분 |
| NI-02 중복·부분 실행 | Passed: 자동/대표 재개 | fake의 동시 같은 key·입력 충돌·단계별 응답 유실·released key 보호, 실제 새 CLI/제공자 재연결과 released key 동시 재호출·입력 충돌. 모든 실제 I/O 경계 중단은 후속 |
| NI-03 저장 계약 | Passed: 합성 fixture | 비희소·비압축 16MiB/256MiB/1GiB, 최초 payload/hydration 0, 반복 읽기 무할당 증가, 같은 4KiB 쓰기 계측 |
| NI-04 원본/A/B Unity | Passed: 스크립트 fixture | 원본 33, A 오류 중 B 203, 수정 후 A 104/B 204 실행. 다른 `.meta` 변경으로 이전 validation 무효화 |
| NI-05 재연결 | Passed: 대표 fixture | 앱 종료 후 제공자 종료·동일 snapshot/delta 재연결, Editor stop/start와 새로운 domain의 loaded-code. 재부팅 미실행 |
| NI-06 결과·공간 보호 | Passed: 자동/fixture | 독립 결과, 미보존 변경 거부, quota disk-full, snapshot 소실 보호. 실제 dirty development 통합은 GitDirtyWorktree로 차단하며 HEAD/index/diff/result ref 보존 |
| NI-07 회수 | Passed: fixture | 앱/자식/provider·소유 메모리·핸들 0, mount/경로/Git 등록/delta/소유 VSS 부재. 독립 결과·원본·기존 복원 지점 보존 |
| NI-08 최종 후보·문서 | Passed: 이번 내부 알파 범위 | locked restore·Release build·97 passed/0 failed/1 ReFS skip, 같은 source hash의 실제 run·추가 통합 차단·회수 증거, 현재 문서·UTF-8 동기화 |

`pwsh ./scripts/build.ps1`은 경고/오류 0으로 완료했다. `evidence/final-build.trx`는 **총 98개, passed 97, failed 0, not executed 1**, 시험 실행 약 63초다. 제외된 것은 `SPLINTER_TEST_REFS_ROOT`가 필요한 선택적 ReFS 시험이다. fake나 일반 Git 디렉터리 시험을 실제 VSS/WinFsp/Unity 증거로 집계하지 않는다. 실제 NTFS ADS/hardlink와 mount-root/내부 junction 거부 시험도 자동 시험에 포함된다.

## 원본과 두 Unity 작업의 독립성

A/B는 동일 clean Git checkpoint에서 서로 다른 시점의 VSS를 캡처했다. 최초 journal은 각각 868바이트(전용 Git pointer payload 98, metadata 770)였고 전체 payload 복사와 읽기 cache는 0이다. 두 작업의 최초 fingerprint는 `cbce5d1bad6c2a16c62072d4f4786e644f3b436de07a62053769c6c660957b8c`로 같았다.

원본 probe를 +10에서 +30으로 바꿔 원본 MCP에서 33을 관측했다. A에는 의도한 C# 구문 오류를 넣었으며 `a-error-status.json`이 CS1525/CS1519와 `validation-failed`를 반환했다. 그동안 B는 별도 Editor에서 +200 코드의 203과 성공한 validation을 보였다. 원본 쓰기 잠금이나 Editor 종료를 요구하지 않았다.

B의 다른 파일 `SplinterAlphaB.cs.meta`를 변경하자 전체 source fingerprint가 달라져 `source-changed`, `validation=null`이 됐다(`b-meta-invalidates-success.json`). 이 변경을 복원하고 +201로 수정한 B는 204, 수정한 A는 +100에서 103, 다시 +101에서 104를 실제 로드된 assembly에서 반환했다. `.meta`까지 포함하는 제품 fingerprint 검증과 추가 runtime probe를 함께 사용했다.

| 최종 실행 | source fingerprint | 새 코드 증거 |
| --- | --- | --- |
| A 104 | `99569eb500ce60827eb04b4377a5ea10ef891fe8fa15cfe159f447cba779aaae` | `a-104-status.json`, `a-104-probe.json`의 PID/domain/assembly MVID |
| B 204 | `8ad3278cd0b3e5e441bd5b5f250be2d64b4034d9e35d4e32f7c8999792866157` | `b-204-status.json`, `b-204-probe.json`의 PID/domain/assembly MVID |

원본의 시험용 변경은 저장해 둔 원본 바이트로 복원했다. 종료 직전 원본 MCP는 probe 13, compile/import/Play/scene dirty false였으며 Git HEAD·clean 상태와 원래 worktree 등록을 유지했다. 원본 Editor와 협업 패키지는 보존했고 Editor access/작업 lease는 종료했다.

## 저장량과 비용

`evidence/range-measurement.json`은 일반 제품 workspace에서 측정했다. 원본 3개 파일은 난수 데이터이며 native `FILE_STANDARD_INFO.AllocationSize`가 각각 논리 길이와 같고 Sparse/Compressed 속성이 없다. 초기 delta의 journal 922바이트와 identity 674바이트에 할당된 file-data는 총 8,192바이트다. 세 파일을 두 번씩 전체 읽어 SHA-256 일치를 확인한 뒤에도 할당량은 **8,192바이트**, read cache는 **0**이었다.

| 원본 파일 | 쓰기 범위 | journal 증가 | 실제 delta file-data 할당 증가 | 쓰기 시간 |
| --- | --- | --- | --- | --- |
| 16MiB | offset 2,101,247의 4,096바이트 | 4,521바이트 | 4,096바이트 | 25ms |
| 256MiB | 동일 offset·길이 | 4,525바이트 | 8,192바이트 | 17ms |
| 1GiB | 동일 offset·길이 | 4,529바이트 | 0바이트 | 17ms |

각 쓰기의 payload 증가는 정확히 4,096바이트다. 실제 할당은 NTFS 할당 단위의 영향으로 한 번에 증가하며 세 쓰기의 합계 증가는 12,288바이트다. 1GiB 쓰기의 할당 증가 0은 이미 할당된 공간을 사용한 것이며 저널 증가 0이 아니다. 이 값에 MFT/VSS/Git 비용을 섞지 않는다.

최초 집계 스크립트의 ordered dictionary 합산 오류로 aggregate가 null이었다. 파일별 native 관측값은 그대로 보존하고 합계만 고쳤다. 무효 집계는 `range-measurement-aggregate-invalid.json`, 수정된 집계는 `range-measurement.json`이다. 새 실측을 했다고 표시하지 않는다. SO의 `measurement-v1-invalid`도 계속 사용하지 않는다.

일반 Git/fingerprint 검증은 데이터를 읽는다. 최초 준비 시 provider read counter는 4,077,651,938바이트였고 명시한 반복 읽기 후 6,812,993,506바이트였다. 첫 전체 읽기는 16MiB 355ms / 256MiB 5,500ms / 1GiB 21,982ms, 두 번째는 459 / 5,987 / 22,913ms였다. 읽기 hydration 0을 읽기 I/O 0 또는 빠른 시작 SLA로 해석하지 않는다. fixture 준비 시 `.git/objects` 논리 파일량 1,359,368,072바이트는 입력 준비 비용이며 workspace 최초 복사량과 별개다.

| 작업 | VSS capture 호출 시간 | 마지막 seal journal | 관측한 앱 working set 최대 | 관측한 앱 private bytes 최대 |
| --- | --- | --- | --- | --- |
| A | 2,551ms | 263,116,253바이트 | 6,497,574,912 | 7,490,023,424 |
| B | 2,402ms | 188,916,161바이트 | 3,181,006,848 | 3,692,883,968 |
| 바이너리 | 2,965ms | 1,045,399바이트 | Editor 실행 안 함 | Editor 실행 안 함 |

메모리는 상태 조회 표본 중 최댓값이며 연속 계측 peak가 아니다. Unity Library/metadata 재작성은 실제 delta에 포함된다. 캡처 시간에 UAC 응답 대기·전체 Git 검증·Unity 시작 시간은 포함하지 않는다. 최초 A Editor 준비는 3분을 넘겼다. 단계별 CLI 시간은 `evidence/*-timing.json`, 실행/준비 상태는 해당 명령 응답과 `home/records/<id>`에 남긴다.

A/B/바이너리 캡처 직후 C: VSS UsedSpace는 각각 9,214,996,480 / 9,428,889,600 / 9,755,701,248바이트, AllocatedSpace는 11,163,103,232 / 11,376,291,840 / 11,702,333,440바이트였다. MaxSpace는 39,985,348,608바이트였다. 이는 다른 복원 지점과 프로젝트 밖 쓰기를 포함하는 **볼륨 전체 값**이며 작업별 증가량으로 귀속하지 않는다. 최종 해제 직후 UsedSpace는 10,049,417,216바이트, AllocatedSpace는 11,702,333,440바이트였고 C: 여유 공간은 305,596,284,928바이트였다. 기존 snapshot과 동일 볼륨의 다른 작업의 영향을 분리하지 않았으므로 이 차이를 시험 자원의 절감량으로 사용하지 않는다. 설정과 한도는 바꾸지 않았다.

`evidence/result-storage.json`은 기준 commit에 없는 결과 Git 객체를 계측한다. A는 logical 2,861 / Git stored 1,769바이트, B는 2,847 / 1,763바이트다. 바이너리는 원상 복원된 기준 commit을 보존하므로 새 결과 객체가 없다. Git stored 크기는 NTFS allocation이나 저장소 전체 크기가 아니다. 결과 ref/commit과 작은 복구 기록, 합성 입력 저장소는 보존했다.

## 재연결·실패 보호

- **미보존 결과:** A의 checkpoint 전 release는 앱을 안전하게 종료한 뒤 `UncollectedChanges`로 거부했고 delta를 유지했다(`a-uncollected-release.json`).
- **제공자 종료:** 앱/자식 0을 확인하고 소유 provider PID `47192`만 종료했다. 새 CLI ensure가 같은 snapshot·delta를 새 provider로 재연결했고 source fingerprint가 같았다. 근거는 `a-before-provider-crash.json`, `a-provider-crash.json`, `a-provider-down.json`, `a-provider-reconnect.json`이다.
- **Editor 재시작:** A가 새 PID `47256`과 새 domain에서 기존 assembly의 103을 반환했다. 이후 104 수정은 새 MVID/domain과 성공 validation으로 확인했다(`a-restarted-103-probe.json`, `a-104-probe.json`).
- **quota disk-full:** 1MiB journal 상한에서 225개의 4KiB 쓰기, 총 921,600바이트가 승인된 뒤 Windows 오류 112와 `DeltaFull`을 반환했다. 승인 데이터와 나머지 읽기 내용, snapshot/delta 보존을 확인했다. 원본과 같은 바이트를 사용해 source는 clean 상태를 유지했다. 물리 host 볼륨 고갈 시험은 아니다(`quota-protection.json`).
- **snapshot 소실:** B의 독립 result ref/tree, 전체 journal seal, 앱/자식 0을 확인한 다음 정확한 B VSS ID만 제거했다. 다른 5개 snapshot은 유지됐다(`b-loss-result.json`). status/ensure는 `SnapshotMissing`이며 새 캡처로 대체하지 않았다. 이후 독립 결과와 전체 seal이 같은 경우에만 mount/Git/delta 회수가 진행됐다. 독립 결과 없음·수집 뒤 늦은 쓰기 거부는 자동 시험으로 보강했다.
- **부분 unmount:** A에 실제 읽기 핸들을 연 채 release하면 `OverlayStopPending`이었다. 핸들을 닫고 같은 release로 재개해 제공자 종료·Git/delta 회수까지 진행했다(`a-held-handle-release.json`, `a-release-request.json`).
- **원본 통합 차단:** 별도 승인된 실제 NTFS 작업에서 검증·checkpoint를 완료한 뒤 원본 README에 미커밋 변경을 넣어 `workspace integrate --target development`를 호출했다. `GitDirtyWorktree`로 거부됐고 HEAD·인덱스·staged/unstaged diff·README 바이트·worktree 목록·결과 ref가 전후 동일했다. 아래 추가 실행 기록을 따른다. 기존 `original-dirty-integration-precondition.json`은 최초 미실행 당시의 읽기 전용 기록으로만 유지한다.

## 최종 회수 상태

`evidence/final-state.json`에서 A/B/바이너리 모두 **`released / released`**다. 각각 앱/자식 process 0, working set/private bytes 0, provider 종료 및 provider 메모리 0, open handles 0을 관측했다. 실제 mount·workspace 경로·Git 등록·delta·소유 VSS는 없다. 독립 결과 ref/tree는 존재하며 원본 HEAD·clean 상태·원본 worktree·실행 중인 원본 Editor를 보존했다. 누적 journal counter는 회수 전의 이력이며 남은 공간을 뜻하지 않는다.

`evidence/final-snapshot-release.json`은 사용자 UAC 승인 후 정확한 소유 A/바이너리 VSS만 해제하고 이미 사라진 B의 영수증을 완료한 증거다. 일반 CLI의 `a-final-release.json`, `b-final-release.json`, `binary-final-release.json`이 최종 사후조건을 확인했다. 같은 helper로 전역 VSS 삭제나 저장 공간 설정 변경을 수행하지 않았다.

소유하지 않은 기존 VSS 3개 `{E82CEB2E-AC2B-4FDF-ABE9-E6E4041FB724}`, `{8D150A60-DBB3-4A18-A509-DF3E7648C50C}`, `{1B6D14D7-E5FA-4951-B3B6-9E62F96F194D}`는 해제 전후 모두 존재했다. `release-owned-snapshots.ps1`은 CLI가 발행한 Release nextActions·helper hash·결과/의존성/경로 부재를 검사한 로컬 시험 wrapper이며 일반 제품의 새 명령이 아니다. 시험 목적으로 종료된 자원을 재생성하지 않는다.

회수 후 같은 A 요청의 CLI 두 개를 실제 겹치는 시간에 실행했으며 둘 다 같은 ID와 `released`를 반환했다. snapshot 영수증 hash가 같고 mount 경로는 재생성되지 않았다. 같은 key의 journal 한도만 바꾸면 `RequestKeyConflict`였다(`evidence/released-request-check.json`, `released-duplicate-1.json`, `released-duplicate-2.json`, `released-input-conflict.json`).

## 추가 원본 통합 차단 시험

사용자가 실제 원본 통합 보호 시험을 승인한 뒤 별도 run `artifacts/ntfs-workspace/ni-guard-20260921/`을 실행했다. 이 절의 증거는 해당 run의 `evidence/`, 지속 상태는 제품 저장소의 `.local/ni-guard/records/1594868f46bf6d41c1d79cd8`에 있다. 기존 회수된 작업을 되살리지 않고 새 key `ni-20260921-integration-guard`를 사용했다.

- 동일 원본 SHA에서 VSS `{62E72EC3-4450-400C-B905-A690FE4CDE5F}`를 캡처하고 일반 CLI로 작업을 준비했다. 원본 MCP가 PID 25756, probe 13, compile/import/Play/scene dirty false임을 확인했다(`original-before-mcp.json`).
- 작업 Editor PID 62264에서 probe를 +301로 수정했다. `validated-status.json`은 fingerprint `be16c585e557e3a758e49599df4c0561bcc793c9bd77245865c459daca8c5e21`과 성공한 validation을, `probe-304.json`은 실제 반환값 304와 assembly MVID `f9b6f13a-4bad-4b17-9cfc-27e165dd695b`를 기록한다.
- checkpoint는 앱/자식 종료를 확인하고 commit `8f12880e5c96cc8f615a6c09e622838323555fd5`, `refs/splinter/snapshot-results/1594868f46bf6d41c1d79cd8`을 독립 보존했다. 원본에는 이 결과를 merge하지 않았다.
- 원본 README에만 시험 marker를 추가한 뒤 실제 CLI integrate를 요청했다. `integration-rejected.json`은 원본 경로를 지목한 `GitDirtyWorktree`다. `dirty-original-before.json`과 `dirty-original-after.json`의 HEAD·branch·status·인덱스·staged/unstaged diff·README hash·probe hash·worktree 목록이 같고 MERGE_HEAD는 없었다. `integration-guard-result.json`에서 원본과 결과 ref 보존을 확인한다.
- marker가 자신의 변경과 정확히 일치함을 확인하고 저장한 원본 바이트만 복원했다. `original-restored.json`은 원래 SHA의 clean 상태, `original-after-mcp.json`은 원본 probe 13과 idle 상태다. Editor 접근/작업 lease도 종료했다.
- `vss-Release.json`, `final-release.json`, `final-state.json`에서 이 작업도 `released`이며 앱/자식/provider·소유 메모리·핸들 0, mount·Git 등록·delta·소유 VSS 부재를 확인했다. 기존 VSS 3개와 독립 결과는 보존했다. 회수 전 journal은 181,925,663바이트이며 회수 뒤 남은 공간을 뜻하지 않는다.

이번 추가 시험은 제품 코드 변경 없이 수행했다. `candidate-inputs.json`은 위의 117개 파일 source hash와 기존 최종 TRX를 연결한다. 같은 후보의 97 passed/0 failed/1 ReFS skip 결과를 재사용하며 빌드를 다시 실행한 것으로 기록하지 않는다. 이 추가 시험에서 clean 원본에 대한 merge 성공이나 두 NTFS 결과의 충돌 merge를 실행한 것은 아니다. 실제 dirty 통합 차단과 기존 자동 Git/계약 시험의 범위를 구분한다.

짧은 home에서 해당 패키지 DLL 경로는 244자였고 파일 존재와 초기 로그의 `DirectoryNotFoundException`/`PathTooLongException` 부재를 확인했다(`short-home-observation.json`). 이는 이 fixture의 관측이며 긴 경로/일반 패키지 전체 지원을 뜻하지 않는다.

## 검증 한계

깊은 run home에서 `Library/PackageCache/com.unity.visualscripting@4edfcc09512f/Editor/VisualScripting.Core/Dependencies/YamlDotNet/Unity.VisualScripting.YamlDotNet.dll` 경로가 270자가 되어 Unity Mono의 초기 assembly validation에 `DirectoryNotFoundException`이 나타났다. .NET에서는 파일 존재가 확인됐고 두 Editor의 준비·C# 컴파일·위 loaded-code 검증은 완료했다. 이것을 모든 패키지가 정상 로드됐다는 증거로 확대하지 않는다. 후속 실환경 시험은 짧은 `--home`을 사용하고 긴 경로/일반 패키지 호환성을 별도로 검증한다.

재부팅·전원 손실·실제 볼륨 고갈·모든 I/O 경계 강제 중단·일반 dirty/LFS/특수 파일 지원·에셋/패키지 변경·Play/Unity Test Framework·대형 프로젝트 성능·공개 48개 전체는 미검증이다. 구매 에셋은 이번 NI 범위에 필요하지 않다. 원격 게시와 공개 릴리스 검증은 이번 범위에 포함하지 않는다.
