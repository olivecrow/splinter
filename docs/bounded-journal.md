# Bounded durable journal

The [bounded journal probe](plans/structural-throughput-prototypes.md) exercises a single SJR2 journal with a current-state prefix and a bounded append suffix. The [product workspace journal](scalable-storage.md) uses raw SJR2 segments under an SJS1 control root.

## Selection and limits

The initial `state-bounded` frame records the format, overlay identity, generation and suffix limits. Before another mutation would exceed 16 MiB of appended bytes or 4,096 frames, the writer publishes a new prefix. The journal budget can impose a smaller limit. Old and new generations coexist during publication. Suffix length alone understates peak disk use.

## Publication and recovery

The writer fences the journal, captures the exact old seal and writes an identity-bound checkpoint intent. It flushes and replays a candidate containing the current private ranges, metadata, whiteouts and retained open-file identities. After atomic replacement on the same volume, it verifies the published state before reclaiming the owned previous generation or acknowledging another mutation.

Retry uses the same identity and generation seals. Corruption or unknown files block cleanup. Multiple ordered mutations can share a durable flush. [Raw writes](raw-write-batching.md)

## Results and qualification

An internal prefix checkpoint preserves journal state. A user-requested `workspace checkpoint` preserves an independent source result. Component timings and interruption checks apply to the selected journal and input. [Validation scope](verification/README.md)
