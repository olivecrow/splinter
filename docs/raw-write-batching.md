# Raw writes and shared durable flush

Durable source journals store changed byte ranges as raw payloads. Stored and decoded lengths must match. In Git and Plastic Unity workspaces, disposable generated data has a separate buffered write path. Tracked source takes precedence over generated-path rules. [Storage layout](scalable-storage.md)

## Ordered durability

The journal applies an ordered batch under its writer gate. Raw SJR2 frames retain checksums. The batch returns success after the owning journal's shared durable flush. A failure may leave an unacknowledged prefix. An uncertain writer requires verified replay. Single-journal SJL4 capsules retain individual publication barriers.

## Windows admission

The WinFsp adapter admits concurrent range and supported metadata callbacks before the mutation gate. A synchronous caller leads the next FIFO group. A group can cover up to 64 callback/chunk units and 16 MiB of requested staging. A larger write is split and waits for every segment. Append positions and constrained lengths reflect earlier writes. Native buffers and handle leases remain valid until the callbacks drain.

Explicit flush drains admitted writes. It may reuse a successful flush on the same open SJR2 journal handle at the same length after integrity checks. Reconnection, changing journals and uncertain execution require a new check. For cached source I/O, capture also drains dirty kernel file handles and propagates flush errors. Memory-mapped writers must finish both view and file flush before a checkpoint can rely on those bytes. [Kernel flush and recovery](kernel-recovery.md)

## Measurement

The number of journal flushes depends on the admitted callback overlap. Measure the complete session when reporting session totals. Keep callback time, journal flush time and end-to-end wall time separate. [Unity workload and timing boundaries](verification/parallel-workspace-performance.md)
