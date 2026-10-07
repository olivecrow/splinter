# 개발용 로컬 kernel 복구 시험

[English](../kernel-recovery.md)

`scripts/test-kernel-recovery.ps1`은 제품 `JournalDeltaStore`·`WinFspOverlayFileSystem`을 실제 WinFsp `FileSystemHost`에 연결한다. 기준은 결정적 fake 불변 트리의 alpha 64KiB와 beta 4KiB다. 실제 VSS provider/server·workspace CLI·Git·Unity를 실행하는 시험은 아니다. 기존 로컬 WinFsp 설치를 사용하며 VIVOBOOK이나 설치·서비스·볼륨 설정을 변경하지 않는다.

기본값은 읽기 전용 계획이다. `-Execute`만 새 evidence와 mount home을 만들며 둘은 별도 트리여야 한다. 기존 경로를 재사용하거나 지우지 않는다. `-Cases`는 선언한 case 이름의 부분 실행이며 전체 행렬의 통과로 표시하지 않는다. Release 시험 바이너리는 `pwsh ./scripts/build.ps1`로 준비한다.

```powershell
pwsh ./scripts/test-kernel-recovery.ps1 -OutputPath artifacts/ntfs-recovery/<new-run-id>
pwsh ./scripts/test-kernel-recovery.ps1 -OutputPath artifacts/ntfs-recovery/<new-run-id> -Execute
# 한 case의 개발 재현: 새 run만 허용
pwsh ./scripts/test-kernel-recovery.ps1 -OutputPath artifacts/ntfs-recovery/<new-smoke-id> -Cases 'write:callback.before-effect' -Execute
```

| 연산 | 종료 경계 | 수 |
| --- | --- | --- |
| write / rename / replace / truncate / 명시적 flush | callback 전, 부작용 후 성공 응답 전, 외부 클라이언트 ACK 후 | 각각 3, 합계 15 |
| mmap | view flush 전, 파일 flush callback 전/후, view/file flush 완료와 외부 ACK 후 | 4 |

공통 `IOverlayMutationObserver`는 명시적 개발 생성자 인수로만 사용한다. 일반 provider는 observer 없이 실행한다. callback 전후는 mutation gate와 기존 개별 저널 flush 정책을 유지한다. 관측점에서 callback에 성공을 반환하기 전에 멈출 수 있지만 WinFsp 드라이버/OS 내부 모든 명령어를 중단한 것은 아니다.

외부 클라이언트는 실제 mount에 4KiB prefix를 write/flush하고 독립 oracle과 ACK를 증거 경로에 flush한다. provider는 이 prefix를 관측한 후 gate를 활성화한다. 부모는 요청 digest·nonce·PID·시작 시각이 일치하는 자기 자식만 종료한다. journal 재생과 두 번째 재생의 바이트 불변을 확인한 뒤 실제 mount 부재를 확인하고 같은 경로·동일 delta/identity를 새 provider로 연다. 읽기 검증과 beta의 새 4KiB 쓰기/flush 뒤 정상 stop/unmount와 핸들·retained context·자식 부재를 확인한다. private delta와 결과는 보존한다.

mmap의 view flush만으로 승인하지 않는다. `MemoryMappedViewAccessor.Flush()`와 같은 파일의 `FileStream.Flush(true)`를 모두 끝낸 뒤 외부 ACK를 게시한다. 이 구분은 [Microsoft의 FlushViewOfFile 계약](https://learn.microsoft.com/en-us/windows/win32/api/memoryapi/nf-memoryapi-flushviewoffile)의 view와 파일 metadata/cache flush 분리를 따른다. 실제 시험의 범위는 프로세스 종료이며 전원 손실의 보장이 아니다. 미승인 dirty page는 부재 또는 생존을 허용하고, flush 완료/ACK 뒤의 내용은 유지돼야 한다.

write callback 부작용 후 정지 상태에서 beta의 별도 프로세스 reader가 응답하는지 확인한다. reader는 쓰기 gate 전에 파일을 열고, gate 중 `ReadExactly`의 바이트·hash를 외부에 게시한 뒤 파일을 닫는다. 읽기 응답과 Close/프로세스 종료를 구분한다. 기존 `Cleanup`도 mutation gate를 잡으므로 닫기는 provider 종료 뒤 완료를 확인한다. 새 파일 open의 동시 완료·같은 파일/범위의 동시 쓰기 원자성을 증명하지 않는다. callback before-effect는 새 내용이 없고 after-effect 및 외부 ACK 후는 완료한 새 내용을 요구한다. flush의 경우 이미 승인된 prefix가 대상이다.

요청·signal·trial 결과는 `kernel-recovery-probe-{request,signal,result}.schema.json`을 따른다. 제어·ACK는 같은 디렉터리의 create-new 임시 파일을 flush하고 덮어쓰기 없는 move로 게시한다. 종료 후 저널 guard/open만 공유 위반 32/33에서 최대 5초 재관측하며 접근 거부·부재·손상을 숨기지 않는다. 여전히 mounted/unknown인 경로를 지우지 않는다. 새 host가 이전 mount의 정확한 빈 일반 디렉터리를 제거하는 것은 mount 생성 계약에만 한정한다.

초기 payload 복사와 read cache는 0을 요구한다. 정상 기준 읽기의 `baseReadBytes`·저널 private 쓰기·증거 원문 복사 비용은 구분한다. 후보와 실제 실행·실패·제한은 [NR 복구 검증](verification/ntfs-recovery.md)이 소유한다. 실제 VSS 재연결·ARM/VIVOBOOK·전원/재부팅·물리 공간 장애로 확대하지 않는다.
