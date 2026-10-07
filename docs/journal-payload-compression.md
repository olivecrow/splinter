<a id="저널-변경-범위-압축"></a>
# Journal changed-range compression

[한국어](ko/journal-payload-compression.md)

`JournalDeltaStore` supports overlay **v2**, OFF's **SJR2**, and the [SJL4 latest-state journal with optimization ON](latest-only-journal.md). It stores the changed range of one write callback, at most 1MiB, as raw or an independent `splinter-range-brotli-v1` block. Older overlays/journals are preserved and explicitly rejected without automatic migration. Workspace state is v2 and independent candidates are v3/SJR2. The frame append/selection description below covers the ordinary OFF SJR2 path; ON uses a single capsule record for payload/metadata, one durable flush per mutation, and publication/reclamation.

<a id="현재-frame"></a>
## Current frame

| Position | Length | Meaning |
| --- | ---: | --- |
| 0 | 4B | ASCII `SJR2`. |
| 4 | 4B | Little-endian metadata length, 1–65,536B. |
| 8 | 4B | Actual stored payload length. |
| 12 | 4B | Decoded payload length after restoration, 0–MaxTransferBytes. |
| 16 | metadata length | Current Mutation JSON. |
| Following | stored length | Raw bytes or the codec's complete SPB1 representation. |
| Last | 32B | SHA-256 of the entire header, metadata, and stored payload. |

`stored=decoded` means raw; `0<stored<decoded` means codec. Metadata operations have both lengths 0. Compression is selected only when **at least 64B smaller than raw, including the codec's entire 48B cost**. Raw gains no codec header/checksum. A new raw frame adds 8B of header compared with the previous frame, so it is not always smaller than previous raw. Encoded data has both outer frame and codec checksums, included in measurements.

Core `WorkspaceJournalPayload` checks stored/decoded lengths and logical-fragment ranges. Extents retain fixed block start, both lengths, and decoded-fragment offset. Partial overwrites and truncation do not calculate physical offsets inside compressed streams. Partial reads of compressed blocks also validate/decode the whole bounded block, without keeping a block cache. Snapshots are read only for uncovered ranges.

Acknowledged writes retain the existing order: frame append, individual durable flush, index application, and callback return. Compression does not split one callback into multiple acknowledgements or add group flush. Private identical-byte/leading-and-trailing-range reuse runs first. Journal budgets use actual header/metadata/stored/checksum lengths. Replay connects logical extents only after validating the outer checksum and matching inner codec/declared lengths. Damaged complete frames are preserved/rejected; only an incomplete final unacknowledged frame is removed under the existing policy.

Mutation JSON omits `Offset=0`, `Destination=null`, and `Replace=false`. The current reader treats explicit defaults and omission identically while preserving nondefault values, Entry, Sequence, Operation, and fragment/skip ranges. Creation/reconnect/read do not rewrite stored journals. Default omission retains format and existing input semantics. Additional CLI/response fields for optional [base-reversion compaction](journal-base-reversion.md) follow a separate contract.

Default compaction/candidate creation collects valid logical fragments into current frames and selects them only when smaller than the original stored frame. More expensive fragment metadata retains the original frame. Content/sequence/source seals, same-intent retries, and explicit overlap budgets remain. Independent-result collection reads decoded content through existing file/source providers, without depending on the journal/VSS to be reclaimed.

Explicit compaction/candidate creation serializes decoded mutations of retained frames into current JSON, selecting metadata only when smaller. Retained raw/encoded payload bytes and both lengths remain; the outer checksum is rewritten for the new metadata. [JM verification](verification/journal-metadata-storage.md) owns actual storage qualification and additional serialization cost of omitted defaults; the [JM plan](plans/journal-metadata-storage.md) fixes the bounded scope.

`PayloadBytes`/`RetainedPayloadBytes` are actual stored lengths, including the codec's 48B. `LivePrivateBytes`/`RetainedLogicalPayloadBytes` are logical lengths. `LiveStoredPayloadBytes` counts each referenced block once; `ObsoletePayloadBytes` sums stored lengths of wholly unreferenced blocks. A partially valid compressed block retains its full storage cost, so logical length is not subtracted from physical length. Frame cost includes header, metadata, and outer checksum, separate from VSS/whole-workspace costs.

Compression preparation rents one scratch buffer only for the maximum stored capacity of a beneficial block (decoded−48−64B), then creates the exact selected encoded array. Raw selection creates no result array. No duplicate decoded+48B encoded temporary buffer is used. Codec/selection/bytes, budgets, and individual durable flush/ACK remain. Small/empty read/replay requests validate the full stream using a 16KiB chunk and requested-range staging, without a decoded cache. Whole requests retain whole bounded scratch.

Internal state hashing and private comparison process one encoded extent with one stored-block read and one pass of 16KiB decoded chunks. Valid slices from the same physical block in compaction/candidate creation are collected by decoding the entire block once, without rereading the whole stream for each 64KiB chunk or slice. Separate extents/validation stages revalidate and retain no decoded cache. Digests/comparisons and unpublished candidates are temporary until header/checksum, complete stream, tail, and declared-length validation finish. Failure publishes neither acknowledged writes nor candidate generations. Public range reads retain existing staging: copy into destination only after validation succeeds. [Automated regressions](verification/code-quality-optimization.md) check block-read counts, bytes for maximum-size/fragment cases, and existing failure-without-publication rules; they do not claim real Unity timing or total-RAM improvement percentages.

Returning pool leases does not remove pool retention, native codec state, or caller request/result/index memory. The [ME scope](plans/memory-completion.md) and [current memory evidence](verification/memory-completion.md) distinguish leases, managed allocations/heap, owned provider/Editor, and shared-system costs. Prior storage/lifecycle qualification remains under [JP verification](verification/journal-payload-compression.md) and [JP exit scope](plans/journal-payload-compression.md).
