# Source verification and storage observations

A source inspection binds its result to the workspace ID, immutable basis, provider epoch and source-change sequence. On Windows, the control request and response must match the owned provider PID, start time, token and mount. Capture drains accepted writes and flushes pending source paths before fixing the source sequence. A flush failure preserves the dirty entry for retry. [Kernel recovery](kernel-recovery.md)

Generated paths and the owned Unity bridge are accounted for separately. Git verification interprets paths with the repository's attributes and filters. The provider can reuse previously verified raw hashes only while the same basis, generation, path changes and interpretation still support them. A raw file hash alone cannot establish the Git blob when a clean filter changes bytes. Explicit audits read and verify the required source. A provider restart requires evidence for the new epoch. Corrupt or incomplete proofs preserve the task for inspection.

An intermediate checkpoint preserves accepted metadata and private ranges against a fixed basis. Subsequent edits can continue. Before releasing the task, Splinter validates independent results and the native backing and compares the journal seals across stop and unmount. [Result generations and publication](repeated-original-apply.md)

## Meaning of measurements

`initialPayloadCopiedBytes` reports bytes copied during workspace admission. `readBytes`, private payload size, total journal size, logical basis size and NTFS allocation answer different questions. Count a shared basis once across its dependent workspaces. Observe owned Editor and provider working sets and private bytes separately from Windows cache and other processes. Compare cumulative operation counters only within the same provider epoch.

Use `scripts/measure-workspace-storage.ps1` on the intended workspace IDs and generated-directory IDs. Use the [Unity maze benchmark](verification/parallel-workspace-performance.md) for elapsed time through actual gameplay and cleanup.
