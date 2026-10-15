# 제한된 durable 저널

[English](../bounded-journal.md)

새 일반 Windows workspace provider는 제한된 SJR2 저널을 사용한다. 각 mutation은 checksum이 있는 raw/Brotli frame을 추가하고 성공 반환 전에 개별 디스크 flush를 수행한다. 매 mutation의 object 게시·회수 대신 현재 상태 체크포인트를 사용한다. 원본 파일은 작업공간의 불변 입력으로 유지하며 초기 payload 복사·읽기 hydration·파일 전체 copy-up을 추가하지 않는다.

## 선택과 한계

첫 `state-bounded` frame이 형식 버전 1, 전체 overlay identity hash, generation, **추가 바이트 16MiB** 및 **추가 frame 4,096개** 제한을 고정한다. 바이트 제한은 작업의 기존 `MaxJournalBytes` 이하로도 제한한다. 다음 mutation이 어느 한계를 넘기 전에 현재 private 범위를 새 상태 prefix로 저장한다. 기존 append/SJL4 저널은 재연결 시 저장된 형식을 유지한다. 독립 후보 v3/SJR2는 별도 표현을 유지한다.

이 제한은 suffix에 적용하며 전체 저장 공간 제한이 아니다. prefix에는 현재 private 바이트와 index metadata가 있다. 이후 덮어쓰기로 불필요해진 prefix 바이트는 다음 체크포인트까지 남을 수 있다. 게시 중에는 이전/새 상태가 일시적으로 공존한다. 기존 전체 저널 예산과 체크포인트 공간 허용 검사를 유지한다. 정확한 NTFS 할당·프로세스 RAM·전체 작업공간 peak는 따로 측정해야 한다.

`journalStorageMode`는 `bounded`, `latest`, `append`를 구분한다. bounded 관측에는 `boundedLogBytesLimit`, `boundedLogFrameLimit`, `boundedLogAppendedBytes`, `boundedLogAppendedFrames`를 추가한다. `journalOptimizationEnabled`는 계속 SJL4의 현재본 전용 동작을 뜻하며 `false`만으로 bounded가 꺼졌다고 해석하지 않는다. `journalBytesBasis`는 계속 writer 계수와 실제 파일 검사를 구분한다.

`workspace compact`의 mode를 생략하면 저장된 형식을 유지한다. 명시적 `--optimize-journal true` / `--revert-base-ranges true`는 [SJL4 현재본 저장](latest-only-journal.md), 명시적 `false`는 append를 선택한다. 전환은 기존 stop/unmount compaction과 같은 intent 복구를 따른다. 기존 작업의 자동 이관이나 새 CLI mode 선택기는 없다.

## 게시와 복구

writer는 세대 교체 동안 수명 guard를 유지한다. 저널 gate 안에서 다음을 수행한다.

1. metadata·Unix mode·whiteout·path sequence·projection guard·열린 채 삭제된 파일을 포함한 현재 논리 상태와 정확한 이전 seal을 고정한다.
2. identity에 묶인 checkpoint intent를 flush하고 private 범위만으로 새 상태를 쓴다. 각 frame의 checksum과 payload 증명을 유지한다.
3. 후보를 flush하고 독립 replay로 전체 논리 상태를 검증한 뒤 양쪽 generation seal을 durable 기록한다.
4. 같은 볼륨에서 이전 generation을 남기며 current를 원자 교체한다. 다시 열어 게시된 상태를 증명한 뒤 live extent 위치를 바꾼다.
5. 이전 generation을 검증·회수하고 intent를 제거한 뒤 다음 mutation을 허용한다. 이후 mutation도 각각 durable flush와 ACK를 수행한다.

게시 중단은 같은 identity와 정확한 generation 증명으로만 복구한다. 손상·owner 변경·불명확한 세대·unknown 파일은 보존·거부한다. 불확실한 writer는 새 검증 연결까지 poison 상태다. 초기화도 별도의 flush된 intent를 사용하며 미완료 생성이 다른 형식으로 바뀌지 않는다.

기록된 파일의 열린 unlink identity는 체크포인트를 넘어 보존된다. 읽기 전용 기준 파일 조회는 일시적 인덱스로, 살아 있는 핸들에서 계속 사용하되 mutation이 그 identity를 기록하기 전에는 영속 상태 hash에 넣지 않는다. 재시작하면 변경 없는 항목을 불변 기준본에서 재구성한다. 마지막 close는 소스 mutation을 만들지 않고 보존 상태를 durable 회수한다. 읽기는 기존 고정 상태와 진행 중 읽기 drain을 유지한다. decoded-payload RAM cache는 64MiB / 4,096개 제한과 hit마다 현재 encoded 바이트 검사를 유지하며 RAM 전용 저장이나 전체 프로세스 메모리 한계가 아니다.

## 결과와 자격

내부 상태 체크포인트는 `workspace checkpoint`와 다르다. 후자는 여전히 독립 소스 결과를 수집하고 수집 전후·unmount 뒤 seal을 대조한다. release에는 일치하는 독립 결과·소유 프로세스/핸들 종료·정확한 파일 inventory·조건부 회수가 필요하다. 변경 없는 원본 Library도 bounded의 정확한 회수 inventory를 통해 기존 identity 검증 디렉터리 복원을 사용할 수 있다. 변경 없는 디렉터리 복원 plan은 v2이며 `JournalMode`(`latest` 또는 `bounded`)를 고정한다. 누락/이전 plan은 이관 없이 보존·거부한다. 기존 파일별 v2 복원 plan은 별도다.

[구현·검증 범위](plans/bounded-journal.md)와 [검증 기록](verification/bounded-journal.md)은 자동 복구·작은 실제 제품 A/B·구성 요소 시간·미실행 경계를 구분한다. 이전 고정 object 로그 시간은 [시제품 근거](verification/structural-throughput-prototypes.md)이며 제품이나 Unity 성능이 아니다. 전체 Unity Editor/미로 시간과 OS 전원 장애 자격은 별도 근거가 필요하다.
