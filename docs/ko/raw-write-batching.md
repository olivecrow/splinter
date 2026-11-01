# Raw 쓰기와 묶음 영속 flush

[English](../raw-write-batching.md)

사용자는 2026-10-25 압축을 폐기했다. 새 쓰기·현재 상태 checkpoint·새로 만드는 candidate/latest-state payload는 변경 범위를 raw로 저장한다. 제품의 Brotli 인코더와 압축 선택 경로는 제거했다. 공간 절감은 불변 기준본 공유와 private 변경 범위로 제공하며 최초 전체 복제·읽기 hydration·파일 전체 copy-up은 없다.

현재 SJR2/SJL4 표현에 이미 기록된 압축 payload는 `IWorkspacePayloadReader` / `BrotliWorkspacePayloadReader`로 계속 검증하고 읽는다. 재연결은 기존 내용을 재작성하지 않는다. compact는 더 작은 유효 표현인 기존 압축 frame을 그대로 유지할 수 있지만 새 데이터를 압축하지 않는다. 현재 상태 prefix를 재작성할 때는 raw를 사용한다. 이는 현재 형식의 데이터 보존이며 다른 버전 지원이나 자동 이관이 아니다.

## 순서와 내구성

`IWorkspaceWriteBatch.WriteBatch`는 최대 **64개 범위 쓰기 / 16MiB**를 받는다. 입력 버퍼는 반환까지 호출자가 소유하고 변경하지 않는다. store gate 안에서 요청 순서대로 checksummed SJR2 frame을 추가하고, 하나의 공유 `Flush(true)` 완료 뒤 성공을 반환한다. 진행 중 상태는 다른 호출자에게 노출하지 않는다. 실패하면 미승인 prefix가 남을 수 있고 불확실한 writer는 poison 뒤 replay가 필요하다. 묶음은 롤백 트랜잭션이 아니다. checkpoint 경계에서는 대기 frame을 먼저 flush하며 기존 **16MiB / 4,096-frame** suffix 한도·전체 상태 증명·게시 intent·복구를 유지한다.

단일 `Write` 호출도 영속 flush 뒤 반환한다. SJL4는 기존 capsule 게시 경계를 유지하며 payload는 raw로 바뀌지만 별도 capsule commit을 합치지는 않는다. source/cache view는 연속 쓰기를 해당 소유 저널로 전달하며 서로 다른 partition의 쓰기 순서를 바꾸거나 두 파일을 하나의 물리 flush로 처리했다고 주장하지 않는다.

## Windows 요청 수집

SCAL 처리량 후속은 최대 64개의 동기 metadata 변경을 받는 앱/OS 중립 `IWorkspaceMetadataBatch` 계약도 제공한다. WinFsp는 mutation gate 앞에서 동시 크기/시간 callback을 모으고 소유 저널의 공유 영속 flush 뒤 ACK한다. 명시적 flush/capture는 접수된 metadata와 쓰기 ticket을 모두 drain한다. 실패한 묶음은 불확정 prefix를 보존하고 replay를 요구하며 SJL4는 개별 capsule 경계를 유지한다. [ST-10 검증](verification/scalable-storage-throughput-followup.md)을 참조한다.

WinFsp 어댑터는 mutation gate **앞에서** 동시 쓰기 callback을 모은다. 한 호출자가 FIFO의 다음 묶음을 동기적으로 처리하며 그동안 기다린 호출들은 다음 flush를 공유할 수 있다. 백그라운드 writer·수집을 위한 sleep·조기 성공 응답은 없다. native 입력 버퍼는 callback 반환까지 살아 있다. Staging payload는 commit당 64개 chunk / 논리 16MiB로 제한한다. ArrayPool 반올림/보유와 caller/native 버퍼는 별도 비용이며 전체 RAM 상한이 아니다. 큰 callback은 나눠 처리하고 모든 조각이 완료된 뒤 성공한다. append 위치와 constrained 길이는 같은 묶음의 선행 쓰기를 반영한다. 다른 변경은 같은 gate를 통해 직렬화한다.

명시적 flush는 어댑터가 이미 접수한 쓰기 ticket을 먼저 drain한다. Close·seal·회수의 내구성/소유 경계는 유지한다. 중단된 대기 스레드도 접수한 쓰기가 drain될 때까지 native 버퍼 소유권을 유지한다. 성공을 이미 반환한 callback의 미완료 쓰기는 없다. 이전 완료 뒤에만 다음 요청을 보내는 순차 호출자는 요청 사이를 묶을 기회를 주지 않는다. 하나의 callback에 포함된 여러 chunk는 flush를 공유할 수 있다. flush 스레드를 단순히 늘리는 설계가 아니다.

## 쓰기 경로 후속

명시적 SJR2 flush는 로컬 무결성을 확인하고 접수된 쓰기를 drain한 뒤, 같은 열린 저널 핸들과 같은 길이가 이미 성공한 영속 flush를 통과했을 때만 그 결과를 재사용한다. 새로 열거나 재연결·교체한 핸들은 자체 영속 경계를 확인해야 한다. 변경과 묶음 쓰기의 flush는 성공 반환 전에 계속 수행하며 실패한 writer의 poison도 유지한다. SJL4의 명시적 flush와 capsule 게시는 그대로다.

Raw 현재 상태의 크기 계산은 알려진 payload 길이를 사용해 payload 읽기와 할당을 생략한다. 실제 checkpoint 출력·checksum·전체 상태 증명·replay는 데이터를 계속 검증한다. Raw 덮어쓰기 양끝 탐색 후보는 읽기 감소가 전체 덮어쓰기 시간 개선으로 이어지지 않아 제거했다. 기존 raw/압축 비교·전체 private 범위 확인·앞뒤 4KiB 절감 기준은 유지한다.

[WP-01–05 검증](verification/write-path-throughput.md)은 동일 입력 전후 비용과 남은 checkpoint/flush 병목을 기록한다. 저널 형식·공개 메서드·버전은 바꾸지 않았다.

## 범위

[RW-01–05 검증](verification/raw-write-batching.md)은 raw 표현/reader·순서/실패·callback 시험·동일 입력 구성 요소 비용·실제 WinFsp 근거를 구분한다. Unity의 실제 요청 중첩·전체 종료/수명주기 절감·장치 전원 장애·전체 프로세스 peak RAM 자격으로 확대하지 않는다. 이 작업은 원본 프로젝트·Unity Editor·외부 도구·장치 설정을 변경하지 않는다.
