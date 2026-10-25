# 현재 압축 payload reader

[English](../workspace-payload-codec.md)

쓰기 압축은 폐기했다. [현재 raw 쓰기 계약](raw-write-batching.md)은 제품 인코더를 제거한다. `IWorkspacePayloadReader`와 `BrotliWorkspacePayloadReader`는 현재 형식으로 이미 저장된 block만 검증하고 읽는다. 파일·VCS·snapshot·Editor 접근은 없다.

- `MaxDecodedBytes`: 1–1,048,576바이트, 기본 1MiB.
- `GetMaxEncodedBytes(n)`: 48바이트 부가 정보를 포함한 유효 표현의 최대 크기. 음수/상한 초과 복원 길이는 거부한다.
- `Read(encoded, offset, destination)`: 전체 block과 정확한 stream 소비를 검증한 뒤 요청 bytes를 게시한다. 빈/EOF 읽기도 검증한다. 잘못된 길이·checksum·예약 필드·추가/잘린 데이터·겹치는 버퍼를 거부하며 실패 시 출력을 변경하지 않는다.
- `Encode` 메서드와 제품 압축 선택 경로는 없다. 시험은 압축 fixture를 독립적으로 구성한다.

유지하는 `splinter-range-brotli-v1` 표현은 `SPB1` magic·복원/저장 little-endian 길이·raw/Brotli 구분·예약 0바이트·저장 payload·SHA-256으로 구성된다. 기존의 현재 표현이며 이전 버전 reader나 이관을 추가하지 않는다.

16KiB보다 큰 압축 입력의 작은 요청에는 16KiB decode chunk와 요청분 staging을 사용해 전체 복원 scratch를 피한다. 나머지 읽기는 크기가 제한된 scratch를 사용하고 모든 lease는 성공/실패 뒤 반환한다. 저널의 64MiB / 4,096-entry decoded cache는 hit마다 현재 encoded bytes를 다시 검증한다. 새 raw payload는 이 cache에 넣지 않는다.

이 변경은 [RW 검증](verification/raw-write-batching.md)이 소유한다. [이전 codec 검증](verification/workspace-payload-codec.md)은 과거 인코더/reader 근거이며 현재 인코더 기능이나 새 공간 절감 주장이 아니다.
