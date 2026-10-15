# Bounded durable journal

[한국어](ko/bounded-journal.md)

New ordinary Windows workspace providers use a bounded SJR2 journal. Each mutation appends a checksummed raw/Brotli frame and individually flushes it to disk before returning success. A current-state checkpoint replaces repeated per-mutation object publication and reclamation. Original files remain immutable inputs to a workspace; no initial payload copy, read hydration, or whole-file copy-up is introduced.

## Selection and limits

The first `state-bounded` frame fixes format version 1, the full overlay identity hash, generation, a **16MiB appended-byte limit**, and **4,096 appended frames**. The byte limit is also capped by the task's existing `MaxJournalBytes`. Before the next mutation would exceed either limit, the writer saves the current private ranges as a new state prefix. Existing append/SJL4 journals retain their saved format on reconnect. Independent candidate v3/SJR2 remains a separate representation.

These are suffix limits, not total storage limits. A prefix contains current private bytes and indexed metadata. A later overwrite can make prefix bytes obsolete until the next checkpoint. During publication, old and new state temporarily coexist. The existing maximum journal budget and checkpoint space admission still apply. Exact NTFS allocation, process RAM and whole-workspace peak must be measured separately.

`journalStorageMode` reports `bounded`, `latest`, or `append`. Bounded observations additionally report `boundedLogBytesLimit`, `boundedLogFrameLimit`, `boundedLogAppendedBytes`, and `boundedLogAppendedFrames`. `journalOptimizationEnabled` continues to identify SJL4's latest-only behavior; `false` alone does not mean bounded storage is disabled. `journalBytesBasis` still distinguishes live writer accounting from actual file inspection.

An omitted `workspace compact` mode preserves the stored mode. Explicit `--optimize-journal true` / `--revert-base-ranges true` selects [SJL4 latest-only storage](latest-only-journal.md); explicit `false` selects append storage. Mode transitions require the existing stopped/unmounted compaction and same-intent recovery. There is no automatic migration of existing tasks or new CLI mode selector.

## Publication and recovery

The writer holds its lifetime guard across all generation changes. Under the journal gate it:

1. Captures the exact old seal and current logical state, including metadata, Unix mode, whiteouts, path sequences, projection guards and deleted files still held open.
2. Flushes an identity-bound checkpoint intent and writes a new state using only private ranges. Each frame retains its checksum and payload proof.
3. Flushes and independently replays the candidate, verifies the complete logical state, and durably records both generation seals.
4. Atomically replaces the current journal on the same volume, retaining the previous generation. It reopens and proves the published state before changing live extent addresses.
5. Verifies and reclaims the previous generation, removes the intent, then admits the next mutation. Each subsequent mutation still gets its own durable flush and acknowledgement.

Interrupted publication resumes only with the same identity and exact generation evidence. Corruption, changed owners, ambiguous generations and unknown files are preserved and rejected. An uncertain writer is poisoned until a new verified connection. Initialization has its own flushed intent; incomplete creation cannot silently become another format.

Recorded open-unlinked file identities survive a checkpoint. Read-only base lookups are a disposable index: they remain usable by live handles but do not enter the durable state hash unless a mutation records that identity. Restart reconstructs untouched entries from the immutable basis. Their final close durably retires the retained state without inventing a source mutation. Reads retain the existing captured-state and in-flight-read drain rules. The decoded-payload RAM cache keeps its 64MiB / 4,096-entry bound and checks current encoded bytes on every hit; it is not RAM-only storage or a whole-process memory cap.

## Results and qualification

Internal state checkpoints are not `workspace checkpoint`: the latter still collects an independent source result and checks seals before/after collection and after unmount. Release requires the matching retained result, stopped owned processes/handles, exact file inventory and conditional reclamation. An unchanged original Library can use the same identity-checked directory restoration with a bounded journal's exact reclaim inventory. The unchanged-directory restoration plan is v2 and fixes `JournalMode` (`latest` or `bounded`); missing/older plans are preserved and rejected without migration. The existing per-file v2 restoration plan is separate.

The [implementation/validation scope](plans/bounded-journal.md) and [verification record](verification/bounded-journal.md) distinguish automated recovery, small real product A/B, component timing and unrun scenarios. Earlier fixed-object log timings are [prototype evidence](verification/structural-throughput-prototypes.md), not product or Unity performance. Full Unity Editor/maze timing and OS power-loss qualification require separate evidence.
