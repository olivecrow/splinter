# 저널 raw 쓰기와 기존 압축 payload

[English](../journal-payload-compression.md)

**사용자 지시로 2026-10-25 쓰기 압축을 폐기했다.** 현재 동작은 [raw 쓰기와 묶음 영속 flush](raw-write-batching.md)가 소유한다. 새 쓰기·checkpoint·새로 만드는 candidate/latest payload는 raw다. 제품의 Brotli 인코더와 압축 선택을 제거했으며 선택적 압축 writer도 없다.

현재 overlay v2·bounded/append SJR2·SJL4·candidate v3 형식은 유지한다. 기존 압축 block은 검증과 decoded-cache 보호를 포함한 [reader 계약](workspace-payload-codec.md)으로 읽는다. 재연결은 기존 데이터를 변환하지 않는다. compact는 기존 압축 frame을 그대로 보존할 수 있고 새로 재구성한 payload는 raw다.

SJR2는 16바이트 frame header·JSON 변경 metadata·저장 payload·32바이트 SHA-256을 유지한다. 기존 압축 frame의 저장/복원 길이는 구분하며 새 raw frame의 두 길이는 같다. fragment 위치는 논리 복원 offset이고 checksum·전체 replay/seal 검증을 유지한다. 미변경 기준본 공유와 private 변경 범위 저장이 공간 정책이다.

동시 SJR2 쓰기는 크기가 제한된 영속 flush를 공유하고 완료 뒤에만 성공한다. 이전의 변경마다 별도 물리 flush 요구를 대체하며 조기 ACK는 없다. SJL4의 capsule 게시 절차는 유지한다. 현재 근거는 [RW 검증](verification/raw-write-batching.md)이다. [JP 검증](verification/journal-payload-compression.md)과 [JP 계획](plans/journal-payload-compression.md)은 과거 압축 writer 근거이며 재구현 지침이 아니다.
