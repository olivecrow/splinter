# Current encoded-payload reader

[한국어](ko/workspace-payload-codec.md)

Write compression is retired. The current [raw-write contract](raw-write-batching.md) removes the production encoder. `IWorkspacePayloadReader` and `BrotliWorkspacePayloadReader` only validate and read already stored current-format blocks. They have no file, VCS, snapshot or Editor access.

- `MaxDecodedBytes`: 1–1,048,576 bytes, default 1MiB.
- `GetMaxEncodedBytes(n)`: maximum valid representation size, including its 48-byte overhead; rejects negative/oversized decoded lengths.
- `Read(encoded, offset, destination)`: validates the complete block and exact stream consumption before publishing requested bytes. Empty/EOF reads also validate. Invalid lengths, checksums, reserved fields, trailing/truncated data and overlapping buffers are rejected. Failure leaves output unchanged.
- There is no `Encode` method or production compression-selection path. Tests construct encoded fixtures independently.

The retained `splinter-range-brotli-v1` representation has `SPB1` magic, decoded/stored little-endian lengths, a raw/Brotli discriminator, reserved 0 bytes, the stored payload and SHA-256. This is the existing current representation; no older-version reader or migration is added.

For compressed inputs larger than 16KiB and small requests, a 16KiB decode chunk plus requested-byte staging avoids a full decoded scratch allocation. Other reads use bounded scratch. All leases return on failure/success. The journal's 64MiB / 4,096-entry decoded cache continues to revalidate current encoded bytes on every hit; new raw payloads do not populate it.

[RW verification](verification/raw-write-batching.md) owns this change. [Earlier codec verification](verification/workspace-payload-codec.md) remains historical encoder/reader evidence, not a current encoder feature or new storage-saving claim.
