<a id="범위-payload-codec"></a>
# Range payload codec

[한국어](ko/workspace-payload-codec.md)

`IWorkspacePayloadCodec` defines an independent byte representation and partial-read contract for one changed range. `BrotliWorkspacePayloadCodec` is provided with Core/fake conformance tests and used as the storage representation of the [current compressed journal](journal-payload-compression.md). The codec itself has no file, VCS, snapshot, or Editor access or durability policy. [JP verification](verification/journal-payload-compression.md) covers product integration; [codec verification](verification/workspace-payload-codec.md) covers independent samples. There is no new codec execution command.

<a id="입력출력-계약"></a>
## Input/output contract

- `MaxDecodedBytes` is the maximum decoded length of one range. Runtime accepts a fixed limit from 1 to 1,048,576B, defaulting to 1MiB, regardless of whole-file length.
- `GetMaxEncodedBytes(n)` returns the required destination-buffer length, including a raw representation. Negative or over-limit `n` produces `PayloadBlockLimit`.
- `Encode(decoded, destination)` writes into a separate caller-provided buffer and returns the initialized prefix length. The source span and result buffer must not overlap. An insufficient destination is rejected before writing; bytes beyond the returned prefix remain unchanged.
- `Read(encoded, offset, destination)` validates the entire block's length, format, checksum, and decoded result before publishing requested bytes. Offset must be nonnegative; reads at or beyond EOF return 0. Empty/EOF reads also validate. Failure leaves destination unchanged.

Input is a memory span kept stable by the caller for the duration of the call. The codec neither reads nor retains files, VCS, snapshots, or Editors. Raw selection also represents only the supplied range, without whole-file copy-up or snapshot-payload comparison reads. Encoded input and output must not overlap.

## Runtime block schema v1

Codec identity is `splinter-range-brotli-v1`. Only the **single current binary representation** below is read. Other magic/encoding values are preserved and rejected with `PayloadBlockUnsupported`. This is not a contract published as a CLI input/output JSON schema or durable workspace format.

| Position | Length | Value/check |
| --- | ---: | --- |
| 0 | 4B | ASCII `SPB1`. |
| 4 | 4B | Decoded length as a little-endian signed int, from 0 to the fixed limit. |
| 8 | 4B | Stored payload length as a little-endian signed int. |
| 12 | 1B | 0 raw, 1 Brotli. |
| 13 | 3B | All 0; reserved values are rejected. |
| 16 | stored length | Raw bytes or one independent Brotli stream. |
| 16 + stored length | 32B | SHA-256 of the first 16B and entire stored payload. |

Total length is `48 + stored`. For raw, stored=decoded. Compression is attempted with quality 1/window 22 and selected only when payload is at least 64B smaller. Otherwise the same representation uses raw. **Both raw and compressed representations have 48B overhead.** This does not mean they are always smaller than bare payloads or current journal frames. Existing metadata and duplicate costs must be recalculated at actual journal integration.

The checksum protects length, encoding, and payload together. Decoder input consumption and decoded length are checked exactly. Trailing bytes, truncation, checksum mismatch, excessive declared lengths, and invalid reserved values produce `PayloadBlockCorrupt`. Buffer/offset input rejection uses `PayloadBufferTooSmall`, `InvalidPayloadBuffer`, and `InvalidPayloadRange`. Failure is not converted into partial-decoding success or a raw fallback.

<a id="버퍼와-수명"></a>
## Buffers and lifetime

Common Encode rents scratch matching the caller's input length and copies only the beneficial encoded prefix into the result. Inefficient compression selects raw; scratch is returned on every path. Journal-internal Encode does not rent an encoded buffer for raw fallback. It rents only the compression capacity that can satisfy whole-block savings and creates an exact result array. The common API, format, quality/window, bytes, and selection criteria remain unchanged.

Raw read accesses the span directly after checksum verification. For compressed reads with decoded length above 16KiB and a request no larger than half that length, one scratch buffer holds **a 16KiB chunk + actual request length**. The whole stream is decoded sequentially; only the requested bytes are retained after the scratch chunk. Output is published after checking decoder completion and exact input consumption/decoded length. Empty/EOF reads need only the chunk. Small blocks/large requests use scratch for the full declared decoded length. Re-signed trailing data, excess decoded output, and truncation are also rejected before output publication; leases return after success/failure. The codec retains no decoded blocks.

ArrayPool may provide an array larger than requested. Power-of-two requests at the default 1MiB limit keep rented sizes within that limit, but an arbitrary smaller configured limit is not a byte-exact pool-allocation cap. Caller-provided input/encoded/read buffers, pool retention, and Brotli native state are separate memory costs. Even a small partial read may require decoding the whole block; read amplification must be reported. There is no global decoded cache/dictionary or persistent codec state.

[Codec verification](verification/workspace-payload-codec.md) owns common fake/conformance tests, malformed streams, lease return/cumulative managed allocation, and preceding evidence from actual Bee inputs. The [ME plan](plans/memory-completion.md) and [ME verification](verification/memory-completion.md) own the current memory path's scope and direct evidence. Journal replay/compaction/candidates/independent results, budgets, native interruptions, and NTFS physical savings follow separate JP verification.
