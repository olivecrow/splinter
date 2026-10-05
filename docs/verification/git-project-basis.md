# Git 프로젝트 기준본 검증

질문은 Git에 이미 기록된 프로젝트만을 불변 기준으로 읽고 VSS 없이 범위 작업공간을 유지/회수할 수 있는가다. [구현 범위](../plans/project-scoped-basis-and-reclamation-review.md)의 VP-01/02·VP-04/05의 Git 부분에 한정한다. 아래 최초 구현 후보의 Unity Editor/Library/MCP는 NotRun이며, 후속 reader 수정과 실제 Unity A/B는 [별도 검증](git-unity-validation.md)이 소유한다. Plastic·VCS 없는 전환·대형/ARM/OS 장애 행렬로 승계하지 않는다.

## 자동 계약과 reader

현재 read 무진행 제한은 `GitProgressReadTests`에서 제어한 clock/부분 stream으로 확인한다. 각 부분 읽기가 20초 안에 끝나는 총 160초 검증은 완료되고 hashing/idle은 reader를 종료하지 않는다. 31초 동안 한 읽기가 멈추면 그 reader의 abort 한 번과 `GitBasisReadTimeout`을 확인한다. 실제 작은 loose/packed Git 읽기·손상 요청 buffer 미게시 회귀는 유지한다. 이 제어 stream 시험은 실제 대형 blob/느린 저장장치나 전체 Unity 준비 성능의 통과가 아니다. `RecoveryContractTests`는 timeout/기준 손상의 `requires-user-action`·`preserve` 일치를 확인한다.

`GitObjectSnapshotTests`는 실제 작은 Git 저장소의 loose/packed object·원본 수정/branch 이동 뒤 고정 range 읽기·ignored 데이터 제외·범위 저널 재생·reader release fence·peer/독립 ref 보호·symbolic/변경 ref 거부·create/delete 응답 유실 조정·blob 소실/손상 시 반환 buffer 미게시·tree hash/필수 receipt 필드 손상 거부를 확인한다. `SnapshotWorkspaceCoordinatorTests.GitProjectModeUsesSharedJournalLifecycleAndSameRequestRecovery`는 공통 fake에서 capture 응답 유실·동일 요청·정리/독립 결과/release를 확인한다. fake를 WinFsp 실행 증거로 읽지 않는다.

기존 RunnerBundle/ContractAndCli·Git/원본/peer·discard/atomic-state 회귀도 유지한다. 첫 한정 실행의 두 회수 시험은 배열의 참조 동일성을 비교해 실패했다. 직렬화된 내용 비교로 수정했으며 제품 실패로 분류하지 않는다. `artifacts/git-basis-focused-classes/`의 실패 TRX를 보존한다. 첫 combined class/method filter는 0개 실행되어 통과로 세지 않는다.

첫 전체 실행은 920개 중 918 Passed·1 Failed·1 선택적 ReFS NotRun이었다. tree 손상으로 Git metadata 명령이 실패할 때 일반 `GitBasisUnavailable`로 분류하던 결함을 보존 필요 `GitBasisObjectInvalid`로 고쳤다. blob/table hash 검사와 requested buffer 미게시 의미는 유지한다. 실패 TRX는 `artifacts/git-project-basis-20261004/full-before-error-classification-fix.trx`에 보존하며 수정 뒤 필수 전체 검사를 다시 실행한다.

최종 `pwsh ./scripts/build.ps1`은 locked restore·Release 빌드·전체 920개 중 919 Passed/0 Failed/1 선택적 ReFS NotRun으로 통과했다. Git reader 11개와 Git mode fake 수명주기·runner/기존 Git·VSS·원본/peer·discard 회귀가 포함된다. TRX는 `artifacts/test-results/Splinter.Tests_net10.0_x64.trx`와 이 실행 evidence의 `full-final.trx`다. 빌드의 4개 MSB3026 경고는 동시에 종료 중이던 소유 CLI의 DLL 잠금으로 한 번씩 복사 재시도 후 완료됐으며 build 오류는 0이었다. 문서 UTF-8과 변경 whitespace 검사도 통과했다. 이 자동/실제 Git 단위를 scoped-complete-with-limitations로 마감하며 아래 NotRun/unknown을 유지한다.

## 실제 입력과 실행

고정 입력은 `.local/gb1004/source`의 1MiB binary·작은 text/ignore와 ignored Library 표본, 정확한 같은 `.local/gb1004/home`의 Editor 없는 A/B다. 실행/JSON은 `artifacts/git-project-basis-20261004/native.ps1`·단계별 응답·`identity.json`에 보존한다. 원본 fixture와 metadata/독립 결과는 삭제하지 않는다.

준비 스크립트의 `Git` 함수가 실행 파일 이름과 충돌해 재귀 호출로 실패했다. 초기 empty source와 오류를 보존하고 `git.exe`로 고쳐 같은 fixture에서 이어갔다. 제한 실행의 A 상태 게시가 AccessDenied/StateWriteIncomplete로 거부됐으며 진단/임시 파일을 보존했다. 같은 project/key/home을 sandbox 밖의 일반 Windows 사용자 권한으로 재개했다. 서비스 설치·grant·UAC elevation·VSS capture로 우회하지 않았다.

B compaction의 `compact-connect`에서 mount는 재연결됐으나 workspace 파일 게시가 Win32 1175/StateWriteIncomplete, statePublication=unknown으로 실패했다. 기존 state·임시 파일을 보존하고 정확한 B id/home의 같은 compact 요청으로 이어가 ready를 확인했다. 대상 파일 삭제/속성 변경이나 새 key·교체 snapshot으로 우회하지 않았다. 이 한 번의 복구를 atomic publication 오류의 일반 원인 확정/해결로 표시하지 않는다.

실제 Editor 없는 Git A/B 한정 실행은 scoped-complete-with-limitations로 마감했다. Git 객체 provider·두 고정 기준·ignored Library 부재를 확인했다. 초기 각 저널 808B, initialPayloadCopiedBytes/readCacheBytes 0을 관측했다. 원본의 후속 commit 뒤 A/B의 이전 text와 기준 commit이 유지됐고 A의 4KiB binary 수정은 원본/B를 바꾸지 않았다. A의 독립 결과/release 뒤 B 후속 쓰기와 소스 검증을 확인했다. B는 최종 오류 분류 수정 runner로 재연결해 같은 후속 내용을 읽고 독립 결과/checkpoint 뒤 명시적 discard를 완료했다. 건강한 기준의 A 수명주기 실행과 최종 오류 분류 회귀를 구분하며 새 A/B를 반복하지 않았다.

`native-result.json`은 A released·B discarded, 각 snapshot/provider/mount/delta 부재·handle/workingSet/privateBytes 0, 소유 basis ref와 Git 등록 부재를 기록했다. 두 독립 결과의 정확한 blob을 회수 뒤 재확인했다. fixture를 참조하는 실제 Git reader/provider 프로세스도 없었다. 실행 callerElevated=false이며 VSS 서비스 설치/권한 상승 없이 끝났다. 필수 전체 회귀의 최종 판정은 아래 완료 결과를 따른다.

`storage-initial.json`의 실제 NTFS 파일 할당 표본은 A control 51,152B, B control 42,960B, shared Git common 62,952B, shared runner 1,347,928B다. A에는 앞선 실패의 보존된 진단/임시 파일 비용도 포함하며 최소 steady 비용으로 해석하지 않는다. 1MiB 기준 binary는 workspace별로 복제하지 않았다. shared roots를 작업별로 중복 합산하지 않는다.

A의 4KiB 쓰기 뒤 저널은 1,262B·stored payload 183B로 private Git pointer와 압축된 변경 범위만 포함했다. 이 반복 바이트 입력의 압축률을 일반 에셋으로 확대하지 않는다. `storage-final.json`의 A/B control 할당 63,984B/88,336B는 소유 delta 없는 상태·결과/회수/실패 metadata이며, shared Git 75,272B에는 기준/원본/두 독립 결과가 남는다. shared runner 4,043,784B/24파일은 이번 수정 단계의 세 내용 키 bundle이다. 기존 bundle을 자동 sweep하지 않는 cache 계약이며 회수되지 않은 mount/저널로 해석하지 않는다. 모든 6개 표본 root의 파일 할당 관측은 complete이고 shared VSS 계측은 unknown이다.

전체 물리 순간 peak·shared Git 객체의 workspace별 귀속·native Git 메모리·실제 Unity RAM 절감은 unknown이다. 작은 raw/packed range 읽기를 대형 Unity 에셋의 준비 시간/메모리 지원으로 확대하지 않는다. 최초 no-payload-copy와 read disk cache 0은 managed scratch·Git 객체 해독·OS cache 비용 0이라는 뜻이 아니다.
