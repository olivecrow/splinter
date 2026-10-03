# 저장소 복제와 실행 자원 회수

ReFS/기준본은 `scripts/splinter-dev.ps1`의 선택적 개발 경로다. 일반 제품 생성은 NTFS만 제공하며 이 문서의 복제·봉인은 프로젝트 내용의 검증 성공을 부여하지 않는다.

이 문서는 **선택적 ReFS 내부 알파**와 공통 프로세스 회수의 계약이다. 일반 workspace에 연결한 NTFS 경로는 [NTFS 작업공간](ntfs-workspaces.md), 실제 NTFS/Git/Unity 증거와 완료한 자원 회수은 [NI 검증](verification/ntfs-workspace-integration.md)을 따른다. 아래 최초 전체 기준본 준비를 NTFS 입력의 필수 단계로 사용하지 않는다. 별도 [SO fixture 증거](verification/snapshot-overlay.md)는 제품 NI 통과와 구분한다.

명시적으로 ReFS 경로를 선택한 반복 작업에는 `refs-cow`를 사용한다. 동일 기준본의 큰 파일은 ReFS 블록을 공유하고 수정한 블록만 별도로 할당한다. `git-checkout`은 최초 기준본 준비와 명시적인 진단용이다. CoW 실패를 일반 checkout이나 전체 파일 복사로 바꾸는 fallback은 없다.

현재 코드·fake/Git 적합성·일반 Windows 프로세스 시험과 **실제 ReFS/Unity 조합의 검증 상태는 별개**다. 실행 증거와 남은 관문은 [자원 검증](verification/storage-and-resources.md)을 따른다.

## 기준본 준비와 재사용

`--home` 아래의 기준본과 작업공간은 같은 ReFS 볼륨에 둔다. 원본 Git common directory는 NTFS에 있어도 되는 구조다. 실제 볼륨은 `storage probe --path <home>`으로 조회한다. probe는 파일시스템과 클러스터 크기를 읽으며 실제 블록 복제 성공 증거를 대신하지 않는다.

```text
<home>/records/<id>/       상태·Editor 로그·프로세스 영수증
<home>/workspaces/<id>/    실제 Git worktree 또는 기준본 준비 작업
<home>/baselines/<id>/tree/ .git 없는 소스와 예열 Library
<home>/baselines/<id>/baseline.json
<home>/runners/<binary-hash>/ 동일 빌드의 작은 감시 실행 파일 묶음
<home>/locks/              짧은 명령의 파일 잠금
```

아래 `splinter`는 저장소에서 `pwsh ./scripts/splinter-dev.ps1`을 뜻한다.

```text
splinter storage probe --path <ReFS-home>
splinter baseline prepare --project <Unity-repository> --base development --home <ReFS-home>
splinter workspace status --id <preparation-id> --home <ReFS-home>
splinter workspace status --id <preparation-id> --home <ReFS-home>
splinter baseline seal --id <preparation-id> --home <ReFS-home>
```

처음 prepare는 정확한 소스 커밋을 한 번 checkout하고 별도 Editor에서 Library를 생성한다. `ready=true` 이후 필요한 외부 검증을 하고 소유 Editor를 정상 종료한 다음 seal한다. seal은 소스 revision과 수명만 확인하며 컴파일 성공을 선언하지 않는다. **실행 중인 원본의 Library는 읽거나 복사하지 않는다.** prepare는 프로젝트·소스 커밋·Unity 버전/브리지 호환 키로 같은 준비 작업을 찾는다. 이미 봉인된 기준본은 다시 예열하지 않는다. 캐시를 명시적으로 회수한 뒤에는 새 세대의 준비 작업을 만든다.

다음 소스 커밋을 준비할 때에는 동일 프로젝트·Unity/브리지 호환 키와 동일 `Packages`·`ProjectSettings` Git snapshot을 가진 봉인 기준본을 먼저 찾는다. 호환 기준본이 있으면 소스와 Library를 CoW로 가져오고 Git이 바뀐 소스만 새 커밋으로 갱신한다. 외부 Unity 도구의 갱신·검증 뒤 봉인을 완료해야 새 커밋의 기준본이 된다. 패키지·프로젝트 설정이 달라지거나 호환 기준본이 없을 때만 cold 준비를 사용한다. 버전 파일의 CRLF/LF는 같은 호환 키로 정규화한다. 에셋마다 임포트 조건을 추정해 검증을 생략하지 않는다.

seal은 정확한 revision의 변경 없는 소스인지 확인하고 소유 프로세스 그룹을 종료한다. 작업 브리지를 제거한 뒤 소스와 Library를 CoW로 봉인하고 준비 worktree를 회수한다. `.git`, Temp, Logs, UserSettings, IDE 생성 파일은 기준본에 넣지 않는다. 예열 과정이나 종료가 추적 소스를 변경하면 봉인을 거부한다. 변경을 별도 소스 커밋으로 검토한 후 새 기준본을 준비한다.

```text
splinter workspace ensure --project <Unity-repository> --key <task-key> --storage refs-cow --baseline <baseline-id> --base <baseline.baseRevision> --editor --home <ReFS-home>
```

Git 어댑터는 `worktree add --no-checkout`으로 독립 관리 영역을 만든다. 기준본을 파일별로 복제한 뒤 `read-tree`와 index refresh로 파일을 다시 쓰지 않고 독립 index를 구성한다. 일반 작업은 요청 revision과 기준본 revision이 같아야 한다. 준비 전용 작업만 호환되는 이전 기준본에서 `read-tree -m -u`로 변경 소스를 갱신한다. `.git`는 공유 파일이나 복제 대상이 아니다. writable hardlink·junction은 사용하지 않는다. 이후 편집·컴파일·결과 통합은 [알파 작업공간](alpha-workspaces.md)을 따른다.

기준본은 내용 hash로 확인하는 재사용 디스크 캐시다. 변경되면 `BaselineChanged`로 보존한다. 작업 회수만으로 캐시를 자동 삭제하지 않는다. 더 필요하지 않으면:

```text
splinter baseline release --id <baseline-id> --home <ReFS-home>
```

준비 worktree와 소비 작업이 모두 `released`일 때만 캐시를 삭제한다. 결과 커밋, 작은 상태 기록, 로그는 회수 후에도 남는다. 이 기록들은 상주 메모리가 아니다.

## 복제량과 제약

`workspace.cloneMetrics`와 `baseline.cloneMetrics`는 `logicalBytes`, `clonedBytes`, `copiedBytes`, `files`, `elapsedMilliseconds`를 반환한다. `clonedBytes`는 성공한 `FSCTL_DUPLICATE_EXTENTS_TO_FILE` 범위의 합이며 `copiedBytes`는 파일마다 최대 한 클러스터인 마지막 부분과 작은 파일의 실제 쓰기량이다. 대상은 sparse로 확장하고 **마지막 부분을 먼저 쓴 후** 나머지를 블록 복제한다. 정렬된 파일도 마지막 클러스터를 먼저 기록해 valid data length 확장에 따른 전체 범위의 재할당을 피한다. 큰 파일 오류에는 전체 복사 fallback이 없다. 논리 크기는 물리 사용량이나 절감량이 아니다. 물리 용량 비교에는 같은 볼륨의 여유 공간과 측정 조건을 따로 기록한다. elapsed는 clone 호출 범위이며 Git/index와 기준본 hash 검사는 별도 비용이다.

위 바이트 계수는 clone 단계만 측정한다. 다음 커밋의 Git delta checkout과 Editor의 후속 임포트·컴파일 쓰기는 포함하지 않는다. 파생 캐시 파일마다 강제 디스크 flush를 수행하지 않으며, 상태와 실행 영수증은 별도로 flush 후 atomic 교체한다. 전원 손실을 포함한 전체 복구 보장은 검증하지 않았다.

같은 ReFS 볼륨의 파일과 디렉터리를 대상으로 하며 sparse인 복제 결과도 다시 복제한다. reparse point, ADS, 압축/암호화 파일, 서로 다른 볼륨은 거부한다. 특수 Git filter/LFS, 별도 integrity 설정과 대형 프로젝트는 별도 검증 대상이다. 기준본 hash 확인은 전체 파일을 제한된 스트림 버퍼로 읽으므로 큰 기준본의 생성 시간에는 이 비용도 포함해 평가해야 한다.

중단된 clone은 파일을 덮어쓰거나 삭제하지 않는다. 완성 영수증이 없으면 `CloneIncomplete`, 부분 기준본은 `BaselineSealIncomplete`를 반환한다. 범용 중단 지점별 자동 복구는 다음 작업이다. 유일한 결과나 알 수 없는 잔여 파일을 강제 제거하지 않는다.

## 프로세스와 메모리

`Splinter.Platform.Windows`의 작업별 감시 프로세스가 Windows Job Object 핸들을 유지한다. Unity를 suspended 상태로 생성하고 job에 배정한 **뒤** 실행하므로 초기 자식 생성의 빈틈을 막는다. breakaway를 허용하지 않는다. CLI와 대화가 끝나도 감시 프로세스와 Editor는 유지된다. Host daemon은 아니다.

감시 프로세스는 호출자의 표준 입출력과 다른 상속 가능한 핸들을 받지 않는다. Editor가 살아 있어도 CLI의 JSON 응답과 출력 스트림은 끝난다. 실행 파일은 빌드 hash별 작은 묶음 하나를 공유하므로 작업마다 복제하지 않고, 실행 중에도 제품을 다시 빌드할 수 있다.

`PrepareRunner`는 현재 7개 실행 파일의 순서별 SHA-256으로 내용 키를 계산한다. 준비 중에만 읽기 전용 원본 핸들을 유지해 hash·복사 사이의 변경을 막고, 새 묶음은 고정 크기 버퍼로 복사한 뒤 디렉터리를 게시한다. 캐시는 길이·내용 hash를 스트림으로 검사하므로 재사용 때 실행 파일 전체를 메모리 배열로 적재하지 않는다. 성공/실패 뒤 원본 핸들을 모두 닫으며 손상·소실 캐시는 보존하고 거부한다. 디스크 재사용과 준비 함수의 관리 할당 근거는 [실행 파일 묶음 검증](verification/runner-bundle-memory.md)을 따른다. 해당 할당을 Editor·전체 시스템 RAM 절감으로 읽지 않는다.

정상 stop은 Unity 브리지의 저장하지 않은 씬·Play 거부와 정상 종료 절차를 따른다. Editor 본체가 종료된 뒤에도 남는 job 구성원은 감시 프로세스가 정리한다. 마지막 구성원 종료를 확인한 후 감시 프로세스도 끝난다. 원본 Editor, 다른 작업자와 이미 실행 중인 공유 서비스는 이 job의 대상이 아니다. 감시 프로세스가 예기치 않게 죽으면 job의 kill-on-close가 작동하지만, 종료 증거가 없으면 정상 회수로 판정하지 않는다.

`application.resources`는 감시 프로세스까지 포함한 소유 프로세스 수와 working set/private bytes의 관측값이다. 실행 중인 값은 OS의 시점별 표본이다. release는 `ownershipVerified=true`, `activeProcesses=0`, `workingSetBytes=0`, `privateBytes=0`을 확인하고 Git 회수 후 실제 경로도 없어야 완료한다. 결과는 `workspace.releasedResources`에 남는다. OS의 파일 캐시·standby 메모리를 강제로 비우거나 전체 시스템 RAM이 실행 전 수치와 같다고 주장하지 않는다.

현재 저널의 압축 준비는 scratch 하나와 선택 결과 배열을 사용하며 작은/빈 compressed read는 chunk와 요청분 staging으로 전체 stream을 검증한다. [ME 계획](plans/memory-completion.md)·[직접 검증](verification/memory-completion.md)은 같은 실제 입력의 동시 pool lease·관리 할당/살아 있는 heap과 별도 소유 provider/Editor·시스템 공유 비용을 구분한다. 임대 반환·소유 프로세스 종료를 전체 OS RAM 초기화로 해석하지 않는다.

실행 영수증에 job 소유권이 없으면 `ProcessOwnershipUnknown`으로 자동 회수를 중단한다. `ProcessCleanupIncomplete`와 함께 경로·결과·프로세스를 보존하고 실제 소유 상태를 확인한다. Editor timeout만으로 소스나 cache를 삭제하지 않는다. release 성공은 현재 `releasedResources` 증거를 요구한다.

## 개발 환경 준비와 시험

`pwsh ./scripts/setup-dev-drive.ps1`은 기본적으로 JSON 계획만 출력한다. `-Create`는 Windows 관리자 권한으로 새 동적 64GiB VHDX를 `.local/dev-drive/splinter.vhdx`에 만들고 기본 `S:`에 Dev Drive를 구성한다. 기존 이미지·드라이브·파티션은 재사용하거나 포맷하지 않는다. 정확한 새 이미지에 대응하는 신규 RAW 디스크인지 확인한 후에만 초기화·포맷한다. 자동 시작 시 mount 등록은 하지 않는다. 시스템 변경과 UAC는 명시적으로 승인한 후 수행한다. 제품 실행에는 이 개발용 helper가 필요하지 않으며 기존 ReFS 경로도 사용할 수 있다.

```powershell
$env:SPLINTER_TEST_REFS_ROOT = 'S:\splinter-tests'
pwsh ./scripts/build.ps1
```

경로를 지정하지 않으면 실제 512MiB ReFS 블록 복제 시험은 **Skipped**다. 일반 Windows Job Object 시험은 별도로 수행한다. fake 저장소와 Git no-checkout 시험을 실제 CoW 또는 Unity 캐시 재사용 통과로 보고하지 않는다.
