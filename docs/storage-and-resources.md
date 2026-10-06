# 실행 자원과 메모리 회수

작업별 프로세스 소유권·실행 파일 묶음·메모리 관측과 정상 회수의 공통 계약이다. 저장소 수명은 [NTFS 작업공간](ntfs-workspaces.md)과 선택한 스냅샷 기준본 계약을 따른다.

## 프로세스와 메모리

`Splinter.Platform.Windows`의 작업별 감시 프로세스가 Windows Job Object 핸들을 유지한다. Unity를 suspended 상태로 생성하고 job에 배정한 **뒤** 실행하므로 초기 자식 생성의 빈틈을 막는다. breakaway를 허용하지 않는다. CLI와 대화가 끝나도 감시 프로세스와 Editor는 유지된다. Host daemon은 아니다.

감시 프로세스는 호출자의 표준 입출력과 다른 상속 가능한 핸들을 받지 않는다. Editor가 살아 있어도 CLI의 JSON 응답과 출력 스트림은 끝난다. 실행 파일은 빌드 hash별 작은 묶음 하나를 공유하므로 작업마다 복제하지 않고, 실행 중에도 제품을 다시 빌드할 수 있다.

`PrepareRunner`는 현재 8개 실행 파일의 순서별 SHA-256으로 내용 키를 계산한다. 준비 중에만 읽기 전용 원본 핸들을 유지해 hash·복사 사이의 변경을 막고, 새 묶음은 고정 크기 버퍼로 복사한 뒤 디렉터리를 게시한다. 캐시는 길이·내용 hash를 스트림으로 검사하므로 재사용 때 실행 파일 전체를 메모리 배열로 적재하지 않는다. 성공/실패 뒤 원본 핸들을 모두 닫으며 손상·소실 캐시는 보존하고 거부한다. 디스크 재사용과 준비 함수의 관리 할당 근거는 [실행 파일 묶음 검증](verification/runner-bundle-memory.md)을 따른다. 해당 할당을 Editor·전체 시스템 RAM 절감으로 읽지 않는다.

정상 stop은 Unity 브리지의 저장하지 않은 씬·Play 거부와 정상 종료 절차를 따른다. Editor 본체가 종료된 뒤에도 남는 job 구성원은 감시 프로세스가 정리한다. 마지막 구성원 종료를 확인한 후 감시 프로세스도 끝난다. 원본 Editor, 다른 작업자와 이미 실행 중인 공유 서비스는 이 job의 대상이 아니다. 감시 프로세스가 예기치 않게 죽으면 job의 kill-on-close가 작동하지만, 종료 증거가 없으면 정상 회수로 판정하지 않는다.

`application.resources`는 감시 프로세스까지 포함한 소유 프로세스 수와 working set/private bytes의 관측값이다. 실행 중인 값은 OS의 시점별 표본이다. release는 `ownershipVerified=true`, `activeProcesses=0`, `workingSetBytes=0`, `privateBytes=0`을 확인하고 Git 회수 후 실제 경로도 없어야 완료한다. 결과는 `workspace.releasedResources`에 남는다. OS의 파일 캐시·standby 메모리를 강제로 비우거나 전체 시스템 RAM이 실행 전 수치와 같다고 주장하지 않는다.

현재 저널의 압축 준비는 scratch 하나와 선택 결과 배열을 사용하며 작은/빈 compressed read는 chunk와 요청분 staging으로 전체 stream을 검증한다. [ME 계획](plans/memory-completion.md)·[직접 검증](verification/memory-completion.md)은 같은 실제 입력의 동시 pool lease·관리 할당/살아 있는 heap과 별도 소유 provider/Editor·시스템 공유 비용을 구분한다. 임대 반환·소유 프로세스 종료를 전체 OS RAM 초기화로 해석하지 않는다.

실행 영수증에 job 소유권이 없으면 `ProcessOwnershipUnknown`으로 자동 회수를 중단한다. `ProcessCleanupIncomplete`와 함께 경로·결과·프로세스를 보존하고 실제 소유 상태를 확인한다. Editor timeout만으로 소스나 cache를 삭제하지 않는다. release 성공은 현재 `releasedResources` 증거를 요구한다.
