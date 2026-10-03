# 범위 payload codec

`IWorkspacePayloadCodec`는 한 변경 범위의 독립적인 byte 표현과 부분 읽기 계약이다. `BrotliWorkspacePayloadCodec`를 Core/fake 적합성과 함께 제공하며 [현재 압축 저널](journal-payload-compression.md)의 저장 표현으로 사용한다. codec 자체에는 파일·VCS·snapshot·Editor 접근과 내구성 정책이 없다. 제품 연결 자격은 [JP 검증](verification/journal-payload-compression.md), 독립 표본은 [codec 검증](verification/workspace-payload-codec.md)으로 구분한다. 새 codec 실행 명령은 없다.

## 입력·출력 계약

- `MaxDecodedBytes`는 한 범위의 최대 복원 길이다. Runtime은 1~1,048,576B 사이의 고정 상한을 받고 기본값은 1MiB다. 파일 전체 길이와 무관하다.
- `GetMaxEncodedBytes(n)`은 raw 표현을 포함한 필요한 목적 버퍼 길이를 반환한다. 범위를 초과하거나 음수인 `n`은 `PayloadBlockLimit`이다.
- `Encode(decoded, destination)`는 caller가 제공한 별도 버퍼에 쓰고 초기화한 prefix 길이를 반환한다. 원본 span과 결과 버퍼는 겹칠 수 없다. 목적 버퍼가 부족하면 쓰기 전에 거부하며 반환 prefix 뒤의 bytes는 그대로 둔다.
- `Read(encoded, offset, destination)`는 전체 block의 길이·형식·checksum·복원 결과를 검증한 뒤 요청 bytes를 게시한다. offset은 음수일 수 없으며 EOF 이상의 읽기는 0이다. 빈/EOF 읽기도 검증한다. 실패 시 destination을 변경하지 않는다.

입력은 caller가 호출 동안 고정한 메모리 span이다. codec은 파일·VCS·snapshot·Editor를 읽거나 유지하지 않는다. raw 선택도 받은 범위만 표현하며 파일 전체 copy-up이나 snapshot payload 비교 읽기를 하지 않는다. encoded와 출력은 겹칠 수 없다.

## Runtime block schema v1

codec identity는 `splinter-range-brotli-v1`이다. 아래 **현재 binary 표현 하나만** 읽으며 다른 magic/encoding은 보존 후 `PayloadBlockUnsupported`로 거부한다. CLI 입력/출력 JSON schema나 durable workspace 형식으로 공개한 계약이 아니다.

| 위치 | 길이 | 값/검사 |
| --- | ---: | --- |
| 0 | 4B | ASCII `SPB1` |
| 4 | 4B | little-endian signed int의 decoded 길이, 0~고정 상한 |
| 8 | 4B | little-endian signed int의 stored payload 길이 |
| 12 | 1B | 0 raw, 1 Brotli |
| 13 | 3B | 모두 0, 예약 값은 거부 |
| 16 | stored 길이 | raw bytes 또는 하나의 독립 Brotli stream |
| 16 + stored 길이 | 32B | 첫 16B와 stored payload 전체의 SHA-256 |

총 길이는 `48 + stored`다. raw는 stored=decoded다. 압축은 quality 1/window 22로 시도하며 payload가 적어도 64B 작을 때만 선택한다. 그 외에는 같은 표현의 raw를 사용한다. **48B overhead는 raw와 compressed 양쪽에 모두 있다.** bare payload나 현재 journal frame보다 항상 작다는 뜻이 아니다. 실제 journal 연결에서 기존 metadata와 중복 비용을 다시 계산해야 한다.

checksum은 길이·encoding·payload를 함께 보호하며 decoder가 소비한 bytes와 복원 길이를 정확히 확인한다. trailing bytes·잘림·checksum 불일치·지나친 선언 길이·잘못된 reserved 값은 `PayloadBlockCorrupt`다. buffer/offset 입력 거부는 `PayloadBufferTooSmall`·`InvalidPayloadBuffer`·`InvalidPayloadRange`다. 실패를 부분 복원 성공이나 raw fallback으로 바꾸지 않는다.

## 버퍼와 수명

공통 Encode는 caller 입력 길이만큼의 scratch를 대여하고 유익한 encoded prefix만 결과로 옮긴다. 압축이 비효율적이면 raw로 선택하며 모든 경로에서 scratch를 반환한다. journal의 내부 Encode는 raw fallback용 encoded buffer를 빌리지 않고 전체 block 절감 기준을 만족할 압축 용량만 대여해 정확한 결과 배열을 만든다. 공통 API/형식/quality/window/바이트/선택 기준은 유지한다.

raw read는 checksum 검사 뒤 span을 직접 읽는다. compressed read에서 decoded가 16KiB보다 크고 요청 길이가 그 절반 이하이면 **16KiB chunk + 실제 요청 길이**의 scratch 하나를 사용한다. 전체 stream을 순차 복원하며 요청분만 scratch 뒤에 보존하고 decoder 완료·정확한 입력 소비/복원 길이를 검증한 뒤 출력에 게시한다. 빈/EOF 읽기는 chunk만 필요하다. 작은 block/큰 요청은 declared decoded 길이의 전체 scratch를 사용한다. 재서명된 trailing/초과 복원/잘림도 출력 게시 전에 거부하고 성공/실패 뒤 임대를 반환한다. decoded 블록을 codec에 저장하지 않는다.

ArrayPool은 요청보다 큰 배열을 줄 수 있다. 기본 상한 1MiB의 power-of-two 요청에서는 대여 크기도 그 상한 안이지만 임의의 더 작은 configured 상한을 byte 단위의 pool 할당 상한으로 해석하지 않는다. caller가 제공한 input/encoded/read 버퍼·pool 잔류·Brotli native state는 별도 메모리 비용이다. 작은 부분 읽기에도 블록 전체 복원이 필요할 수 있어 read amplification을 숨기지 않는다. 전역 decoded cache/dictionary·persistent codec state는 없다.

공통 fake/적합성, malformed stream·임대 반환/누적 관리 할당과 실제 Bee 입력의 선행 근거는 [codec 검증](verification/workspace-payload-codec.md)을 따른다. 현재 메모리 경로의 범위/직접 근거는 [ME 계획](plans/memory-completion.md)·[ME 검증](verification/memory-completion.md)이 소유한다. journal replay/compaction/candidate/독립 결과·budget·native 중단/NTFS 물리 절감은 별도 JP 검증을 따른다.
