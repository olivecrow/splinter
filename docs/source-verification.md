# 변경 기반 소스 검증과 저장 계측

제품 `ntfs-snapshot`은 파일 단위 raw SHA-256과 Git 내용 hash를 같은 snapshot·provider 세대·소스 변경 이력에 묶어 재사용한다. 파일 크기·수정 시간·Git stat cache만으로 내용이 같다고 판단하지 않는다. 전체 fingerprint의 경로 정렬과 `source-fingerprint-v1` 값은 유지한다. ReFS/진단 backend는 기존 전체 raw 읽기 경로다.

## 검증 흐름과 소유 경계

Core의 `ISourceChangeProvider`/`SourceChangeView`는 workspace ID, snapshot ID, provider epoch, 마지막 소스 sequence, 경로별 마지막 변경 sequence를 제공한다. Runtime의 `JournalDeltaStore`는 acknowledged journal 기록에서 이 집합을 만들고 재생 시 다시 구성한다. 새 provider의 epoch는 항상 다르다. Core는 Git·Unity·Win32 API를 참조하지 않는다.

Windows 어댑터는 `overlay/source-query.json`과 `source-response.json`의 새 request ID를 사용한다. 요청 전후에 정확한 provider PID·시작 시각·mount·snapshot identity를 확인한다. 주기적인 `state.json`의 과거 소스 세대를 검증 장벽으로 사용하지 않는다. 15초 안에 해당 응답을 받지 못하면 `SourceObservationUncertain`으로 보존한다.

Git 어댑터는 다음 순서로 검사한다.

1. provider의 소스 세대를 확인하고 현재 HEAD·관리 대상 경로·staged 변경 목록과 전체 index 항목을 열거한다. worktree `git status`로 변경 없는 payload를 다시 읽게 하지 않는다.
2. 관리 경로 정책, 유효 Git attributes와 config의 digest를 확인한다. config 원문은 manifest나 계측 로그에 저장하지 않는다. 미지원 filter/LFS·working-tree-encoding은 계속 거부한다.
3. 같은 snapshot/epoch/policy에서 변경되지 않은 파일의 raw hash를 재사용하고, 변경된 파일만 전체 raw 내용을 해시한다. 디렉터리 rename은 하위 경로도 무효화한다. 추가·삭제·`.meta`·truncate·원자적 교체는 경로/세대와 재열거로 검출한다.
4. Git 내용 hash도 재사용하거나 `git hash-object --path`로 계산한다. HEAD의 blob ID와 비교해 `changedPaths`/`hasChanges`를 보완한다. 같은 크기/mtime으로 Git이 clean을 반환해도 내용이 다르면 변경으로 판정한다. attributes/config 해석이 바뀌면 raw hash와 별도로 Git 내용 hash를 다시 계산한다.
5. HEAD·경로 집합·staged 변경 목록·전체 index 항목·해석 digest와 provider 세대를 다시 확인한다. 도중 변경은 `SourceChanged`이며 manifest와 현재 검증 성공을 갱신하지 않는다.

수집 시 변경된 기존 파일에는 `git add --renormalize`를 경로별로 적용해 stat cache가 숨긴 내용도 index에 반영한다. 그 뒤 raw fingerprint와 실제 커밋을 다시 검사한다. 새 파일/삭제는 기존 `git add -A` 경로를 유지한다. 결과 수집의 `stable-journal-v1`, 전후·unmount 후 seal 검사와 독립 결과 검증은 그대로 필요하다.

`Library`, `Temp`, 소유 Unity bridge 등 관리 경로의 쓰기는 소스 세대를 올리지 않는다. 루트 디렉터리의 날짜·속성 변경도 소스 내용 변경이 아니다. 루트의 rename/delete/resize/payload 쓰기는 기존 저장 계약에서 거부한다. 변경 경로 인덱스는 최대 250,000개이며 한도 초과는 acknowledged append 전에 차단한다.

## manifest와 복구

`records/<id>/source-fingerprint.json`은 checksum을 가진 내부 version 2 manifest다. 정확한 `SnapshotReference`, workspace ID, 관리 정책 digest, 해석 digest, epoch/sequence, 경로별 raw/Git hash를 저장한다. payload 사본은 저장하지 않으며 크기 상한은 64MiB다. 임시 파일을 flush한 뒤 교체한다.

- 같은 CLI를 새로 실행해도 살아 있는 provider 세대가 같으면 재사용한다.
- provider 재시작은 새 epoch의 전체 읽기 `rebuild`를 요구한다. 원본의 새 snapshot으로 대체하지 않는다.
- manifest 누락은 전체 읽기 `initial`, 손상·이전 형식은 `SourceCacheRevalidationRequired`다. 자동으로 과거 성공을 재사용하지 않는다.
- `workspace status --audit-source --id <id> --home <home>`은 전체 소스를 읽고 다시 작성한다. 거부된 manifest는 `.rejected-<id>`로 먼저 보존한다. `--verify-source`를 포함하는 동작이다.
- snapshot/provider 관측 unknown, 완결 저널 손상, 앞뒤 소스 세대 불일치는 성공 발행을 차단한다. 불완전한 마지막 journal frame의 재생 규칙과 acknowledged write 보존은 기존 계약을 유지한다.

일반 status는 새 pending validation 응답을 조정할 때만 소스를 검증한다. 명시적 현재 검사는 `--verify-source`를 쓴다. `sourceVerification=verified`, `validationCurrent=true`, `workspace.validation.success=true`를 함께 확인해야 현재 Unity 검증 성공이다. fingerprint 캐시 성공 자체는 Unity 컴파일 성공이 아니다.

## 계측의 의미

| 위치 | 측정 항목 | 해석 경계 |
| --- | --- | --- |
| `source.inspection`, `source-inspections.jsonl` | mode, raw payload bytes, 해시/재사용 파일 수, manifest bytes, 시간, epoch/sequence, `gitHashInputBytes` | Git 값은 내용 hash에 넘긴 파일의 raw 논리 길이다. Git 전체 I/O·물리 디스크 읽기량과 다르다. 초기/감사/재개 비용도 기록한다 |
| `storage.metrics` | provider epoch, lock 횟수/대기 tick, flush 횟수/tick, commit/append bytes, replay bytes/tick, base/journal read bytes | 현재 provider 프로세스의 누적 계수다. 재시작 뒤 다른 epoch끼리 단순 차감하지 않는다. tick은 `stopwatchFrequency`로 환산한다 |
| `storage-reads.jsonl` | seal과 최종 delta 삭제 전 확인의 실제 stream bytes와 해시 시간 | delta 밖의 진단 기록이다. seal 값·삭제 권한의 근거를 대신하지 않는다 |
| `scripts/measure-workspace-storage.ps1` | FILE_STANDARD_INFO 파일 할당, 논리 길이, root별 분류, VSS 볼륨 관측 | 읽기 전용 개발 도구다. mount를 순회하거나 논리 트리를 실제 복제량으로 합산하지 않는다 |

```powershell
pwsh ./scripts/measure-workspace-storage.ps1 -HomePath <home> -Project <original> -OutputPath <evidence.json> -Label after-edit
```

동일 단계의 원본 Library·공유 Git 기준선을 빼고 추가량을 비교한다. Library 쓰기는 delta 안에 이미 들어 있으므로 이중 합산하지 않는다. 접근 불가·reparse 경로는 unknown으로 기록하고 subtotal만 남긴다. 일반 권한에서 VSS를 읽지 못하면 unknown이며 관리자 볼륨 관측을 별도로 확보한다. VSS의 작업별 귀속과 MFT/OS cache 등은 전체 절감률로 추정하지 않는다. 로그·임시 파일·결과 객체도 공간 장부에 포함해야 한다. 단계 표본의 최고치는 실제 순간 최고치를 보장하지 않는다.

같은 영역 반복 쓰기와 삭제/truncate 후에도 실행 중인 기존 journal 할당은 남는다. 결과 수집 전 정지한 NTFS 작업공간에는 명시적 저널 정리를 구현했으며, [작은 Unity 재개·공간 계측](verification/journal-compaction-qualification-and-cost.md)에서 정리 직후 할당 감소와 두 세대가 겹친 관측 최고치를 확인했다. flush 묶음 처리와 기본 실행 방식 전환은 구현하지 않았다. 범위와 검증 수준은 [공통 저장소 1차 계획](plans/storage-foundation-first-milestone.md), 실제 판정과 VSS 보존 실패는 [검증 기록](verification/storage-foundation.md), 현재 회수 차단/지원 경계는 [NTFS 작업공간](ntfs-workspaces.md)을 따른다.

## 계약과 진입점

`contracts/v1/source-change-{request,response,view}.schema.json`, `workspace-status-request.schema.json`, `response.schema.json`, `storage-read-measurement.schema.json`이 입력·출력·계측 계약이다. `capabilities.sourceVerification`에 현재 방식과 감사 명령·계측 위치가 있다. 구현은 `Core/SourceChanges.cs`, `Runtime/JournalDeltaStore.cs`, `SourceControl.Git/GitSourceFingerprint.cs`, `Platform.Windows/WindowsSourceChanges.cs`와 `NtfsSnapshotWorkspaceBackend.cs`에 나뉜다.
