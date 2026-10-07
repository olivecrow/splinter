# 개발용 저널 중단 시험

[English](../journal-recovery.md)

`scripts/test-journal-recovery.ps1`은 제품 `JournalDeltaStore`를 쓰는 결정적 fake 불변 트리의 개발 시험이다. 기본값은 읽기 전용 계획이며 `-Execute`만 새 증거 디렉터리와 합성 private 저널을 만든다. 기존 디렉터리를 덮어쓰거나 결과를 삭제하지 않는다. 먼저 `pwsh ./scripts/build.ps1`로 Release 시험 바이너리를 준비한다.

```powershell
pwsh ./scripts/test-journal-recovery.ps1 -OutputPath artifacts/ntfs-recovery/<new-run-id>
pwsh ./scripts/test-journal-recovery.ps1 -OutputPath artifacts/ntfs-recovery/<new-run-id> -Execute
```

실행은 Release 시험의 `JournalInterruptionTests.EvidenceRun`을 호출한다. 시험용 자식은 alpha 64KiB와 beta 4KiB를 생성·쓰기하고 승인 sequence 4의 독립 내용 oracle을 외부 파일에 flush한다. write/rename/replace/truncate/create/delete를 하나 더 시도한 뒤 선언한 경계에서 멈춘다. 부모는 요청 nonce·경계·PID·시작 시각을 대조한 정확한 자식만 강제 종료하고 종료를 확인한 뒤 같은 identity의 저널을 다시 연다. 일반 workspace 명령, VSS·WinFsp·Git·Unity와 서비스 설치는 호출하지 않는다.

| 경계 | 대상 |
| --- | --- |
| append 전, header 후, metadata 후, checksum 후, durable flush 전/후, index 적용 후, 호출 반환 후 외부 ACK 전/후 | 6개 연산 각각 9건 |
| payload 절반 후, payload 전체 후 | write 2건 |
| 명시적 `Flush()` 전/후 | 이미 승인된 prefix 2건 |

총 58건이다. payload가 없는 연산의 payload 경계를 중복 시험으로 세지 않는다. `commit.before-flush`와 checksum 후는 같은 파일 상태라도 다른 호출 위치의 중단이다. flush 호출 내부의 OS 명령어, 부분 header/checksum 바이트별 종료와 전원 손실은 포함하지 않는다.

소유 자식의 종료 확인 뒤 저널의 guard/open만 공유 위반 32/33에서 최대 5초 재관측한다. 기존 writer의 teardown을 기다리는 개발 harness 규칙이며 실행별 재시도 횟수를 남긴다. 접근 거부·부재·손상이나 replay 오류를 재시도로 숨기지 않는다.

공통 `IJournalMutationObserver`는 명시적 생성자 인수로만 연결한다. 일반 Runtime·Windows composition에는 observer가 없다. 관측 callback은 저널의 읽기·Length 조회·Flush·Dispose를 하지 않는다. 개발 경계를 위해 payload를 둘로 나누는 것은 observer를 연결한 경우뿐이며 일반 쓰기는 기존 단일 payload append를 유지한다. callback 실패는 store를 reconnect-required로 잠가 부분 append 뒤 같은 인스턴스의 추가 쓰기를 막는다.

모든 정상 transaction은 기존대로 checksum append와 `Flush(true)` 뒤 index를 적용하고 반환한다. 미완성 마지막 frame만 재연결에서 잘라낼 수 있다. checksum·sequence·JSON이 손상된 완전 frame은 기존 `DeltaCorrupt` 계약으로 보존/거부한다. 반환하지 않은 완전 transaction은 프로세스 종료 후 생존할 수 있지만 혼합 namespace나 부분 적용은 허용하지 않는다. durable flush 이후와 외부 ACK 이후의 새 내용은 반드시 생존해야 한다. 명시적 flush 시험의 prefix는 이미 개별 commit에서 승인됐다.

요청·gate·trial 결과 형식은 `contracts/v1/journal-recovery-probe-{request,signal,result}.schema.json`이 소유한다. gate/ACK 파일은 같은 디렉터리의 새 임시 파일을 flush한 뒤 덮어쓰기 없는 move로 게시한다. 기존 trial에 이어 쓰지 않는다. 종료 후 재생 전 journal의 원문 복사, 전후 hash/길이, sequence, namespace·길이·내용 hash와 두 번째 재생 불변을 남긴다. 초기 payload 복사·read cache·base payload 읽기는 모두 0이어야 한다. 이 값은 fixture의 합성 private 쓰기에만 해당한다.

실제 candidate hash와 통과/실패·제한은 [NR 복구 검증](verification/ntfs-recovery.md)이 소유한다. 내부 저널 경계 시험을 실제 mount callback 응답·mmap flush·VSS·재부팅·전원·물리 공간 장애의 통과로 승계하지 않는다.
