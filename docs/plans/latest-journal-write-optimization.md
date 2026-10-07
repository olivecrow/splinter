<a id="현재본-저널의-쓰기-경로-최적화"></a>
# Optimizing the current-state journal write path

[한국어](../ko/plans/latest-journal-write-optimization.md)

This is the user's explicit write-path optimization request of 2026-10-06. Reduce duplicate C writes and serialized file-flush waits in SJL2/SJT2 while retaining original + latest changes and individual mutation durable ACKs. The initial implementation keeps default OFF; do not change commit/push, service/ACL/installation, original/Unity Editors, or existing closed A/B runs.

- The current candidate uses SJL4/root v4 capsules and SJB1 owner headers. Preserve/reject earlier SJL1/2/3 and SJT2/3. Keep overlay/workspace v2, candidate v3/SJR2, and compact intent v3.
- Write C payload and metadata once into one pending capsule; perform one durable flush per individual mutation, then publish to the current root. Move the previous root to an owned backup without copying, and reclaim replaced payload/metadata. Do not group different mutations' flush/ACK.
- Incomplete C/no publication retains B; complete C recovers through the same writer; corruption/unknown/missing data after publication is preserved and rejected. Verify only changed file/process boundaries, candidate/OFF/restore/seal, and normal WinFsp callbacks. Do not open an OS power/Unity/VSS/large failure matrix.
- Compare three ON/OFF runs each on the same 8/32 committed PNGs with repeated B→C 128KiB range inputs. Confirm file hashes, path sequences, metadata, seals, current payload/NTFS allocation, and write latency. Hash/allocation measurement stays outside timed writes; do not overlap benchmarks with build/test.

The intermediate SJL3 candidate wrote payload once and flushed four independent files concurrently, improving time by only 7–8%. Preserve that runner/input/figures and improve with a single-flush capsule. Completion requires recovery evidence for a single payload write, space/time improvements and residual costs for the same input, required `pwsh scripts/build.ps1`, and current contract/capabilities/measurement documentation updates. [Verification](../verification/latest-journal-write-optimization.md) owns inputs, figures, and remaining limits.

<a id="마감"></a>
## Closure

At the user's subsequent request, change initial default for new tasks to ON. Existing tasks and compaction with options omitted retain stored selections; explicit false selects OFF. Verify interruption recovery during identity/seed/root publication of new ON creation, empty/non-empty existing OFF reconnection, repeated saves/compact/OFF transition with default ON, and necessary WinFsp callbacks. Do not change service/installation/original/existing A/B.

The subsequent default change also closed. Confirmed required locked restore, full Release build with 0 warnings/errors, 1,155 passed, 0 failed, 10 opt-in skipped out of 1,165 total, and 1/1 actual WinFsp/fake-basis check plus matching composite seal/accounting for default ON. Distinguish current source/logs in [subsequent verification](../verification/latest-journal-write-optimization.md#default-on-for-new-tasks) from existing performance-input evidence.

2026-10-06 scoped-complete-with-limitations. Connected a single payload/metadata capsule and one durable flush per mutation, incremental writer byte accounting, actual observation/seal/reconnection checks, and previous payload/metadata reclamation. Three runs each of the same real 8/32 PNG files reduced ON write time by 29.5%/30.7%; ON/OFF file-data allocation was -91.75%/-93.61%, and ON allocation stayed constant across all cycles. Confirmed 12 accounting/product seals, original preservation/owned basis release, necessary WinFsp 1/1, and 1,134 passed, 0 failed, 10 opt-in skipped out of 1,144 total. ON costs 5.6–5.8 times OFF, and cumulative managed allocations increased. Default OFF was retained then. Subsequently, the user explicitly selected default ON for new tasks based on absolute individual-write times and reduced file allocation. No RAM savings are claimed. [Verification](../verification/latest-journal-write-optimization.md) preserves before/after figures, raw evidence, and unqualified boundaries.
