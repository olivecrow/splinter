# Latest-state journal

SJL4 is a journal representation exercised by the [bounded journal probe](plans/structural-throughput-prototypes.md). The [product workspace journal](scalable-storage.md) uses segmented storage.

## Storage and publication

The journal root identifies an owned generation directory and its reachable nodes and payload blocks. Initialization fixes the identity and attempt before publishing the seed and root. A mutation writes raw changed bytes and the necessary index records, verifies the candidate and publishes the root atomically. Each capsule has its own publication barrier. Readers keep the backing files needed for their captured state until they finish.

A reconnect checks the identity, generation order, previous-state hash, checksums and object bounds. An incomplete owned pending publication can be resolved against the exact current root. Unknown files, damaged objects or ambiguous generations block reclamation. An open unlinked file remains addressable by its handle until its final close.

## Measurement

Use the probe's `sjl4` method for component comparisons. Record live data size, allocated space, publication overlap, flush cost and reader retention separately. [Raw writes and shared flush](raw-write-batching.md)
