# Preparation and observation throughput

[한국어](../ko/plans/preparation-throughput.md) · [Verification](../verification/preparation-throughput.md)

2026-10-26 follow-up to the lifecycle review. Baseline: `448f029a31a7c6f29d14d868c5e5f9e11aff5b19`.

## Scope and acceptance

Optimize repeated control-file absence waits and None/recorded Plastic source scans. Start's exclusive client lock excludes competing starts: reuse one observed intent/receipt pair and skip absence delays there. A child may still publish a receipt for an existing intent; missing receipt plus intent remains uncertain, preserves the child, and is reobserved by the same-request retry. Ordinary observers retain publication grace and drain concurrent receipt/intent reads. A live project task checks an absent release receipt immediately until release/discard authorization exists. Task capture/release and basis dependency attach exclude concurrent publishers with their existing locks. Sharing errors, corrupt records, owner/generation mismatch and unresolved launch remain errors/unknown.

Enumerate and bound file membership before hashing with at most eight workers for None. Mounted Plastic scans stay serial because its reader retains one revision proof/stream; concurrent files would repeatedly evict that proof. All workers join on failure/cancellation. None/recorded Plastic initial inspection can return an operation-local, single-use proof only with an authoritative source-change provider. Both the initial full scan and later metadata-only membership scan require equal complete generation barriers. Revalidate owner/basis/mount/request/policy, epoch, sequence and history; Plastic rechecks registration isolation. Editor startup discards the proof. Missing tracker/audit paths retain full inspection. No durable cache or format/schema change.

PR-03 measures actual NTFS/WinFsp CLI, one editorless `project-snapshot`/None task at a time. Use a new disposable 2.125 GiB, 2,112-file source (64 × 32 MiB + 2,048 × 64 KiB), identical content/mtime, one common project basis, fixed eight-byte edit + 64 KiB addition, before/after/after/before, two observations per variant. Measure ensure, edit, checkpoint and separate release; independently prove source/results and final ordinary-NTFS restoration. Freeze both runtime bundles. Acceptance: at least 20% and 10 seconds mean ensure reduction, with matched source/result and owned closure. Approximately 13 GiB per comparison, 26 GiB including retained preliminary fixture/results; no simultaneous build/tests during timing; no forced OS cache clearing. Setup/proofs and common prepare/restore are recorded separately.

| ID | Work / evidence |
| --- | --- |
| PR-01 | Publication exclusion, bounded observation waits, no unresolved-launch or ownership bypass. |
| PR-02 | Bounded full scan and single-use inspection proof; mutation, epoch, metadata, membership, identity, Plastic isolation and fallback tests. |
| PR-03 | Actual matched-input ABBA product timings and exact reclamation/restoration. |
| PR-04 | Locked restore/build/full tests; bilingual docs and lifecycle map/index. |

Independent result payload/durable flush, journal ACK/seals, original apply, discard and cleanup guards remain. This comparison does not qualify Unity startup/compile/Play/Test/build, a live Plastic server, original-apply/discard timing, peak RAM or power-loss behavior. It does not reopen retired backends or prior experiments. Incremental journal checkpoints, restore redesign and cross-operation result-proof caching remain separate work.
