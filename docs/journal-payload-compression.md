# Journal raw writes and existing compressed payloads

[한국어](ko/journal-payload-compression.md)

**Write compression was retired on 2026-10-25 at the user's request.** [Raw writes and shared durable flush](raw-write-batching.md) owns current behavior. New write, checkpoint and newly materialized candidate/latest payloads are raw. The production Brotli encoder and compression selection have been removed; there is no optional compression writer.

Current overlay v2, bounded/append SJR2, SJL4 and candidate v3 formats are unchanged. Existing compressed blocks remain readable through the [reader contract](workspace-payload-codec.md), including validation and decoded-cache protections. Reconnect never converts existing data. Compaction can carry an existing encoded frame unchanged; newly reconstructed payload is raw.

SJR2 retains its 16-byte frame header, JSON mutation metadata, stored payload and 32-byte SHA-256. Stored and decoded lengths remain distinct for already encoded frames; new raw frames have equal lengths. Fragment positions are logical decoded offsets. Checksums and complete replay/seal validation remain. Sharing the unchanged basis and recording only changed private ranges remain the storage policy.

Concurrent SJR2 writes can share a bounded durable flush, with success only after completion. This supersedes the former physical flush per mutation; it does not introduce early ACK. SJL4 retains its capsule publication protocol. See [RW verification](verification/raw-write-batching.md). [JP verification](verification/journal-payload-compression.md) and [JP plan](plans/journal-payload-compression.md) preserve historical compressed-writer evidence and are not instructions to restore it.
