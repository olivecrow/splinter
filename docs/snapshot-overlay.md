# NTFS 스냅샷과 범위 저장 실험

현재 명령은 [별도 개발 CLI](development-cli.md)에서 사용한다. 자체 compile/validation 요청은 제거했으며 storage 후보·source/cache·seal/회수 계약만 유지한다.

**독립 저장소 실험 진입점**의 계약이다. 같은 VSS/WinFsp/저널 구현을 사용하는 일반 제품 backend는 [NTFS 작업공간](ntfs-workspaces.md)에 별도로 연결되어 있다. Windows/NTFS 한 환경의 SO-01~06 fixture 증거는 [SO 기록](verification/snapshot-overlay.md), 제품 NI 상태는 [NI 기록](verification/ntfs-workspace-integration.md)으로 구분한다. ReFS 준비나 원본 이동은 요구하지 않는다.

## 구성과 저장 계약

`candidate-export`·등록 증거가 필요한 `candidate-source-export`와 `overlay-serve-request.validationView`의 불변 후보·분리된 source/cache 저널은 [고정 검증 view 계약](validation-view.md)을 따른다. 게시 입력은 고정 `publication.requestKey`를 요구하며 같은 요청의 intent·원본 seal로만 중단을 재조정한다. 정상 Editor 종료·provider 종료 뒤 같은 경로에서 후보를 바꾸는 개발용 실험이며 제품 workspace 모드로 표시하지 않는다. 정의한 A/B/A와 생성·재개·전환 비용의 실행 증거는 [VC 기록](verification/immutable-validation-view.md)에 분리한다.

공유 VSS helper는 같은 볼륨에 다른 오래된/동일 시각/시각 불명 snapshot이 있으면 `SnapshotReleaseIsolationRequired`로 해제를 차단하고 `release-isolation.json`을 남긴다. 정확한 ID만 삭제해도 다른 snapshot이 소실된 [실제 실패](verification/storage-foundation.md)가 있으므로 기존 snapshot을 삭제해 차단을 우회하지 않는다. 현재 guard는 [격리된 소유 snapshot](verification/storage-recovery.md)에서 검증했다. 일반 비소유 백업 공존·외부 동시 삭제의 보존 보장은 미완료다. 새 소스 변경 세대·저널 I/O 계수는 [공통 소스 검증과 계측](source-verification.md)을 따른다.

| 구성 | 현재 책임 |
| --- | --- |
| Splinter.Core/SnapshotOverlay.cs | 플랫폼 중립 snapshot/tree/delta/mount ports, 관측과 독립 결과·의존성 해제 조건 |
| Splinter.Runtime/JournalDeltaStore.cs | SHA-256 append-only 범위/메타데이터 저널, 동기 flush, 범위 합성, truncate/rename/delete, 동일 identity 재생 |
| VssSnapshotTree.cs | persistent differential 시스템 VSS device만 읽기, 원본 fallback 금지, 소실 시 실패 |
| WinFspOverlayFileSystem.cs / WinFspOverlayServer.cs | Windows 경로의 파일·디렉터리·mmap I/O, 제공 프로세스, 핸들·메모리·저널 상태 |
| WinFspOverlayClient.cs / WinFspOverlayMountProvider.cs | 공통 mount port, immutable runner, PID/시작 시각·launch generation, 같은 snapshot/delta 재연결, flush/unmount |
| GitOverlayRegistration.cs | no-checkout linked worktree, 독립 index, 결과 commit/ref, 정확한 소유 등록 회수 |
| OverlayExperimentCommands.cs | snapshot-spike JSON CLI의 schema 검증 및 Git/Unity/Windows 조립 |
| scripts/vss-snapshot.ps1 | 관리자 capture/observe/owned-ID release helper. VssSnapshotProvider가 제품용 영수증·권한 재개 경계로 연결 |

생성 시 source payload를 복사하지 않는다. 읽기는 snapshot과 기존 저널 범위를 합성하며 영구 캐시를 만들지 않는다. 일부 쓰기는 해당 범위만 저장한다. 승인한 프레임은 Flush(true)한다. 재생은 불완전한 마지막 프레임만 제거한다. 완결 프레임의 checksum·sequence·JSON·경로/범위 메타데이터 오류와 identity JSON 손상은 파일을 보존하고 DeltaCorrupt로 실패한다. 임의 손상 복원·자동 재캡처·전체 복사 fallback은 없다.

원본은 작업 중 계속 쓸 수 있다. VSS ClientAccessible은 writer 없는 filesystem crash consistency이며 미저장 Unity 메모리까지 포착하지 않는다. 동일한 캡처 시점의 Library를 공유하고 작업별 실제 쓰기만 저널에 남긴다. snapshot ID와 device 경로를 고정하며 재연결 중 새 캡처로 바꾸지 않는다.

## 명령과 상태

`scripts/invoke-snapshot-spike.ps1 -OverlayRequestPath <persisted-request> -Action <action>`은 `snapshot-spike --input` JSON CLI의 wrapper다. 자동 캡처·설치를 하지 않는다. 입력은 contracts/v1/snapshot-spike-request.schema.json과 overlay-serve-request.schema.json, 제공자 상태는 overlay-observation.schema.json을 따른다.

| action | 조건과 결과 |
| --- | --- |
| mount / status / unmount | 동일 identity·delta·경로 사용. 앱/클라이언트가 있으면 회수 대기. PID·시작 시각·token 확인 |
| git-prepare | clean checkpoint와 새 branch, -ReceiptPath, -Revision 필요. checkout 없이 등록하며 mount보다 먼저 수행 |
| git-attach | .git 기준 읽기를 제외한 mount에 전용 pointer와 독립 index 연결. 원본 index를 복제·공유하지 않음 |
| unity-start / unity-status | 정확한 Unity 버전, 전용 Job Object와 소유 bridge. fresh heartbeat로 ready 판단 |
| unity-stop | 정상 종료 후 Editor·자식 전부 종료, 소유 메모리 0 관측 |
| preserve | 앱 종료 후 Git source 결과 commit와 refs/splinter/snapshot-results/overlay-id 보존. bridge·생성 cache 제외 |
| git-release | mount 부재와 보존 revision 일치 후 정확한 Git 등록만 제거. 결과 ref와 branch 유지 |

control/state.json의 phase는 replaying, mounted, snapshot-missing, snapshot-unknown, unmounted, failed다. 실행 중 provider와 CLI는 같은 snapshot probe를 사용한다. snapshotAvailability는 available/missing/unknown이며 unknown을 snapshot-missing으로 저장하지 않는다. CLI는 processAlive, snapshotAvailable, mountPresent를 함께 반환하고 unknown boolean은 null이다. launch intent와 receipt generation이 다르면 OverlayStartUncertain으로 중복 실행을 막는다. 일치하는 새 launch가 살아 있지만 이전 세대 state만 남아 있으면 starting으로 관측하며 자원 수치를 0으로 추정하지 않는다. mount/reconnect는 정확한 PID·시작 시각, 실제 mount, 5초 이내 mounted 상태를 최대 2분 기다린다. 한도 초과·취소 뒤에도 같은 provider와 저널을 보존한다. stop.json과 flush.json은 같은 token을 요구한다. unmounted 뒤에도 실제 프로세스와 경로 부재를 확인한다.

WinFsp Cleanup에서 사용자 핸들 종료를 집계한다. 커널 retainedFileContexts는 Close까지 남을 수 있어 별도로 관측한다. 정상 unmount 뒤 제공자가 종료되면 그 메모리도 회수된다. 열린 사용자 핸들이 있으면 unmount를 강행하지 않는다.

## Unity 경계

선택 모델·텍스처·PNG의 추가 검사는 형제 fixture의 [미리보기 검사 계약](unity-render-qualification.md)을 따른다. `qualification-command.json`의 `inspect`는 fixture의 명시적 코드/렌더 검사다. 제품은 refresh나 합격 판정을 제공하지 않는다. PNG 경로는 같은 요청 응답의 `assets.previewPath`를 읽는다.

.git만 기준 읽기에서 제외한다. Library를 제외하거나 삭제해 전체 재생성하지 않는다. 실행 전 private overlay의 Library/ilpp.pid, Library/Pipeline/.unity-pipeline-port, Library/MCPForUnity/RunState/mcp_http_*.pid만 whiteout한다. 원본 프로세스 주소를 재사용하면 원본 compiler helper에 간섭할 수 있기 때문이다.

실험의 소유 bridge도 실행 관측과 정상 종료만 처리한다. refresh·컴파일·로드 코드/렌더 증거는 외부 도구와 fixture의 명시적 inspect 절차에서 확인한다. 초기 fixture 출력이나 Editor ready를 최신 코드의 합격으로 재사용하지 않는다.

## 재현 순서

설치·VSS 설정 변경은 별도 승인 범위다. setup-snapshot-overlay.ps1은 기본 Plan이며 -Action InstallWinFsp는 공식 SHA-256과 NAVIMATICS 서명을 확인한 WinFsp 2.2.26215 Beta4 Core만 설치한다. 자동 재부팅은 없다. set-snapshot-test-budget.ps1은 기본 Plan이며 승인된 -Apply는 C: 상한을 64GiB로 증가만 시킨다. 캡처 전 diff-area 여유 8GiB를 검사한다. 실험 뒤 자동 축소하지 않는다.

새 경로에서 실행한다. 과거 증거 디렉터리의 source/delta/snapshot은 회수되어 재실행 입력으로 사용할 수 없다.

```powershell
pwsh scripts/build.ps1
$run = Join-Path $PWD 'artifacts/snapshot-overlay/new-run'
pwsh scripts/prepare-snapshot-overlay-test.ps1 -Action Source -RunDirectory $run
# 승인된 관리자 PowerShell에서 Capture 수행
pwsh scripts/vss-snapshot.ps1 -Action Capture -SourcePath "$run/source" -OwnerId so-new-run -ReceiptDirectory "$run/snapshot-t0"
pwsh scripts/prepare-snapshot-overlay-test.ps1 -Action Overlays -RunDirectory $run -SnapshotReceipt "$run/snapshot-t0/snapshot.json"
foreach ($name in 'a','b') {
  pwsh scripts/invoke-snapshot-spike.ps1 -OverlayRequestPath "$run/control-$name/request.json" -Action mount
}
pwsh scripts/test-snapshot-overlay-io.ps1 -RunDirectory $run
pwsh scripts/measure-overlay-small-writes.ps1 -RunDirectory $run
pwsh scripts/test-snapshot-overlay-concurrency.ps1 -RunDirectory $run
pwsh scripts/test-snapshot-overlay-compatibility.ps1 -RunDirectory $run
```

원본 변경 후 새 snapshot-t1을 캡처하고 prepare-snapshot-overlay-test.ps1 -Action Overlays -Names c로 C를 준비한다. C에서 새 marker·namespace를 확인한다. test-snapshot-overlay-faults.ps1은 t1로 fault/quota를 만들어 핸들 유지·제공자 강제 종료·quota 112를 재현한다. preserve-snapshot-test-results.ps1은 a/b/c/fault/quota의 변경 결과를 별도 파일·SHA-256 manifest로 보존한다. export는 최초 workspace 복사와 별도 비용이다. test-overlay-journal-recovery.ps1은 보존 후 A에만 incomplete/checksum 장애를 주입한다. test-owned-snapshot-loss.ps1은 독립 보존 검증 후 관리자 InjectLoss로 소유 t1만 삭제한다. 정확한 인자는 각 script의 param 블록을 따른다.

Unity는 별도 run에 실제 fixture를 캡처한다. -Action Overlays -Unity로 .git 제외·16GiB journal 한도의 A/B 요청을 만든다. 각 요청에 git-prepare -Branch <new-codex-branch> -Revision <checkpoint-SHA> -ReceiptPath <run/git-a.json> → mount → git-attach → unity-start를 실행한다. 원본/A/B를 서로 다르게 편집하고 대상 Editor의 새로고침·새 코드 실행을 확인한다. measure-snapshot-overlay.ps1로 allocation·provider/OS cache·볼륨을, unity-status로 앱 Job 자원을 기록한다.

결과 확정 뒤 unity-stop, preserve-snapshot-overlay-unity.ps1 -RunDirectory <run> -Name a 및 b를 실행한다. 이 wrapper는 source 보존·journal accounting·unmount·Git 등록 회수·manifest를 연결한다. 합성 fixture도 모든 mount를 정상 해제한다. reclaim-snapshot-overlay-test.ps1은 기본 read-only 검증이며 -Reclaim은 결과 hash/ref·동일 durable sequence·프로세스/핸들/mount/Git 등록 부재를 확인한 뒤 delta만 지운다. 중단 후 이미 회수한 delta는 일치하는 reclaim intent로 재조정한다. 마지막 관리자 release-snapshot-overlay-test.ps1 -RunDirectories <runs> -OutputPath <report>는 해당 snapshot ID만 회수하고 기존 ID 보존을 검증한다. 난수 원본과 실행기 cache는 snapshot 회수 후 명시적으로 정리한다.

## 비용과 미검증 범위

journal 한도는 maxJournalBytes, node 250,000개, extent 1,000,000개, whiteout 250,000개다. 한 transaction은 최대 1MiB다. VSS 읽기 핸들 32개와 metadata/negative cache 32,768개를 유지한다. 파일 내용 cache는 0이다. WinFsp callback 8개와 각각 최대 1MiB buffer를 사용한다. 한도 초과는 DeltaFull/OverlayMetadataLimit이며 자동 삭제·copy-up·compaction은 없다. 반복 쓰기의 과거 payload도 비용에 포함한다.

initialPayloadCopiedBytes, journalBytes, payloadBytes, metadataBytes, readBytes, readCacheBytes는 엔진 계수다. measure-overlay-journal.py는 실제 요청 경로별 Library/Temp/Logs 등의 승인 쓰기를 집계한다. 실제 file-data allocation은 FILE_STANDARD_INFO.AllocationSize다. MFT·디렉터리 전체 비용은 포함하지 않으며 볼륨 free space·VSS 사용/할당량도 함께 본다. 공유 OS cache를 작업별 영구 복제나 소유 프로세스 메모리로 계산하지 않는다.

ADS, hardlink identity, reparse/EFS/압축/희소, 개별 ACL 변경, 일반 dirty/LFS checkpoint, 전원 손실, 재부팅 후 snapshot identity 검증은 미지원 또는 미검증이다. reparse/EFS/압축/희소 속성은 기준 읽기와 create/overwrite/속성 callback에서 변경 전에 거부한다. callback 자동 시험은 실제 mount의 전체 파일 API 지원 증거가 아니다. host 물리 디스크 고갈은 하지 않았고 quota로 Windows disk-full 112를 검증했다. snapshot은 프로젝트 밖 쓰기와 기존 복원 지점 비용도 포함한다. 이 실험은 보안 sandbox가 아니다.

## 개발 요청의 필수 증거

`prepare-snapshot-overlay-test.ps1`, `test-snapshot-overlay-faults.ps1`, `test-snapshot-overlay-compatibility.ps1`은 `scripts/snapshot-overlay-request.ps1`의 공통 생성 함수를 사용한다. 현재 v2 capture receipt와 같은 요청의 live observation으로 `readAccess`, `snapshotReceiptDirectory`, `snapshotOwnerId`를 모두 채운다. mount는 제품과 같은 VSS identity·현재 boot·소유권 검증을 수행하며 경로 존재 확인만으로 우회하지 않는다. 새 조회가 필요하면 정확한 receipt의 `vss-snapshot.ps1 -Action Observe` 절차를 먼저 수행한다. 요청 생성은 capture·mount·원본 변경을 수행하지 않는다.