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
| `storage.metrics` | provider epoch, lock 횟수/대기 tick, flush 횟수/tick, commit/append bytes, replay bytes/tick·프레임·읽기 호출·단계별 tick, base/journal read bytes | 현재 provider 프로세스의 누적 계수다. 재시작 뒤 다른 epoch끼리 단순 차감하지 않는다. tick은 `stopwatchFrequency`로 환산한다 |
| `storage-reads.jsonl` | seal과 최종 delta 삭제 전 확인의 실제 stream bytes와 해시 시간 | delta 밖의 진단 기록이다. seal 값·삭제 권한의 근거를 대신하지 않는다 |
| `scripts/measure-workspace-storage.ps1` | FILE_STANDARD_INFO 파일 할당, 논리 길이, root별 분류, VSS 볼륨 관측 | 읽기 전용 개발 도구다. mount를 순회하거나 논리 트리를 실제 복제량으로 합산하지 않는다 |
| `scripts/measure-overlay-journal.py` | 정지한 저널의 frame checksum·SHA-256·sequence, 기록 경로별 누적/유효/대체 payload, 현재 경로별 유효 payload | 제품 replay·seal로 확인한 입력의 개발 장부다. 정리된 `write-compacted`·`skip-range`도 처리하며, 물리 할당·제품 검증을 대체하지 않는다 |

```powershell
pwsh ./scripts/measure-workspace-storage.ps1 -HomePath <home> -Project <original> -OutputPath <evidence.json> -Label after-edit
python ./scripts/measure-overlay-journal.py <stopped-delta.journal> --output <new-accounting.json>
```

할당 계측은 현재 저널·정리 후보·이전 세대를 별도 category로 나눈다. 선택적 `-EvidencePath <run-directory>`는 검증 자료를 `verification-evidence` root(`scope=measurement-overhead`)로 따로 관측한다. 이를 제품 작업공간 공간에 합산하지 않고 여러 작업이 공유하는 자료는 한 번만 센다. 표본 파일 자신이 기록되기 전의 관측이므로 마지막 표본 파일까지 포함한 절대 상한은 아니다.

저널 장부는 Windows에서 읽기 전용 핸들에 read sharing만 허용해 살아 있는 writer와 파일 삭제를 거부한다. payload를 한 frame씩 읽어 checksum·전체 hash를 계산하며 원본 snapshot 데이터는 읽지 않는다. 불완전한 tail·손상·sequence 불일치에서 입력을 고치지 않고 실패한다. 출력은 기존 파일을 덮어쓰지 않는다. 현재 경로 비교는 ASCII 대소문자 무시만 검증했고 비ASCII 경로는 명시적으로 거부한다. `unclassifiedBytes=0`은 저널의 payload/프레임 바이트 장부가 맞는다는 뜻이며 MFT·볼륨의 미계측 공간이 0이라는 뜻이 아니다.

`groups`는 기록 당시 경로군별 누적 쓰기와 그중 아직 참조되는 payload를, `livePayloadByCurrentGroup`은 rename 후 현재 경로군의 유효 payload를 나타낸다. 두 분류를 더하지 않는다. `obsoletePayloadBytes`에는 더 이상 참조하지 않는 payload가 들어가지만, 정리 형식의 metadata 비용·범위 수 제한 때문에 그 전부가 실제 할당 절감으로 이어진다고 보장하지 않는다. 장부의 hash·sequence를 같은 경계의 제품 seal과 대조한다. 계측기의 독립 기대값·손상 보존·Windows writer 거부 시험은 `python -m unittest discover -s tests/scripts -p test_measure_overlay_journal.py -v`다.

Runtime의 replay는 수명 한정 64KiB 버퍼로 저널만 순차 읽는다. 완결 frame의 SHA-256·메타데이터·sequence 검증을 생략하지 않고 payload나 원본을 별도 파일에 hydration하지 않는다. `replayReadCalls`/`replayReadTicks`는 실제 journal 파일 읽기 호출, `replayValidationTicks`는 checksum·decode·sequence 확인, `replayIndexTicks`는 메모리 index 적용, `replayFrames`는 적용한 frame 수다. `replayTicks`에는 버퍼 복사·기타 루프 비용도 포함하므로 세 단계 합과 같지 않다. OS cache를 거친 파일 API 읽기량이며 물리 디스크 I/O가 아니다. 이전 기록에서 새 계수가 없으면 null/unknown이며 0으로 소급하지 않는다. 쓰기 flush와 불완전 tail·완결 손상 보존 계약은 기존과 같다.

동일 단계의 원본 Library·공유 Git 기준선을 빼고 추가량을 비교한다. Library 쓰기는 delta 안에 이미 들어 있으므로 이중 합산하지 않는다. 접근 불가·reparse 경로는 unknown으로 기록하고 subtotal만 남긴다. 일반 권한에서 VSS를 읽지 못하면 unknown이며 관리자 볼륨 관측을 별도로 확보한다. VSS의 작업별 귀속과 MFT/OS cache 등은 전체 절감률로 추정하지 않는다. 로그·임시 파일·결과 객체도 공간 장부에 포함해야 한다. 단계 표본의 최고치는 실제 순간 최고치를 보장하지 않는다.

같은 영역 반복 쓰기와 삭제/truncate 후에도 실행 중인 기존 journal 할당은 남는다. 결과 수집 전 정지한 NTFS 작업공간에는 명시적 저널 정리를 구현했으며, [작은 Unity 재개·공간 계측](verification/journal-compaction-qualification-and-cost.md)에서 정리 직후 할당 감소와 두 세대가 겹친 관측 최고치를 확인했다. flush 묶음 처리와 기본 실행 방식 전환은 구현하지 않았다. 범위와 검증 수준은 [공통 저장소 1차 계획](plans/storage-foundation-first-milestone.md), 실제 판정과 VSS 보존 실패는 [검증 기록](verification/storage-foundation.md), 현재 회수 차단/지원 경계는 [NTFS 작업공간](ntfs-workspaces.md)을 따른다.

`replayIndexOperations`는 관측된 연산별 `operation`·`frames`·`applyTicks` 배열이며 최대 9개다. `create`, `metadata`, `resize`, `write`, `write-compacted`, `rename`, `delete`, `skip-write`, `skip-range`만 허용한다. 합계는 각각 `replayFrames`와 `replayIndexTicks`에 대응하고 현재 provider의 신규 쓰기는 포함하지 않는다. `skip-range`도 한 frame이므로 frame 수는 durable sequence 증가량과 다를 수 있다. 새 provider의 빈 배열은 replay한 frame이 없음을 뜻하며, 과거 누락/null은 unknown이다. 반환 배열 수정은 내부 계수에 영향을 주지 않는다.

rename의 index 적용은 기존의 경로·whiteout 순회를 유지하며 같은 하위 경로 접두사를 rename당 한 번 만들어 재사용한다. 범위 index·저널 형식·지속 cache를 추가하지 않는다. [Offline probe](replay-profiling.md)로 합성 비용을 구분하며, 그 결과를 실제 Unity 준비 시간에 곧바로 적용하지 않는다.

## 계약과 진입점

`contracts/v1/source-change-{request,response,view}.schema.json`, `workspace-status-request.schema.json`, `response.schema.json`, `overlay-observation.schema.json`, `storage-read-measurement.schema.json`이 입력·출력·계측 계약이다. `capabilities.sourceVerification`에 현재 방식과 감사 명령·계측 위치가 있다. 구현은 `Core/SourceChanges.cs`, `Runtime/JournalDeltaStore.cs`, `SourceControl.Git/GitSourceFingerprint.cs`, `Platform.Windows/WindowsSourceChanges.cs`와 `NtfsSnapshotWorkspaceBackend.cs`에 나뉜다.
