# Shutdown and preparation cost optimization

[한국어](../ko/verification/lifecycle-cost-optimization.md)

> **Current timing reference:** the record below preserves its original candidate/stage measurements and then-unexecuted boundaries. The subsequent combined `116fd44` candidate is measured in [UF-01](unity-final-lifecycle.md): **18m 03.0s** complete workflow, **3m 36.1s** Editor readiness, **2m 52.0s** A/B preservation/release and **2m 06.0s** Library restoration. UF does not isolate each optimization or qualify the separate unexecuted scenarios below.

LC-01–03 follow the remaining lifecycle costs in [US-01](unity-startup-remeasurement.md), using source baseline `76f8f7f` and its frozen normal runtime in `.local/sa8-runtime`. The changes target small-write metadata, Library initialization and seal/reclamation work. New full Unity startup, shutdown, checkpoint/release and Library restoration timings are **NotRun**. Historical US times are not current-candidate results.

## Input evidence

US's normal Editor exit took 287.4339s. The owned bridge reached `exiting` about one second after each stop request. Near-stop provider samples record 10,311 / 10,306 write mutations, 1,172,606 / 1,165,926 payload bytes and 5,962,591,600 / 5,962,585,870 logical object-put bytes. Write scopes total 275.7885s / 271.4713s, including 150.6798s / 147.4325s in durable flush. These scopes overlap between A/B and must not be added. The sample window extends several seconds beyond exit. Logical put bytes exclude storage-internal carried objects and are neither physical disk I/O nor retained allocation. The exact Unity file and per-path cause remain unconfirmed.

The previous SJN2 implementation stopped live paging after a changed prefix and serialized the growing remaining extent/block table on each write. This is a code-backed scaling problem consistent with the observed increasing put size. The bounded comparison below isolates storage behavior without launching Unity.

Fresh Library preparation previously hashed its payload for capture, client initialization and provider admission. Each A/B workspace hashed the Library again in its client and provider. For the US Library's 3,141,660,252 bytes, that is seven whole-input traversals. Removing the three client traversals avoids 9,424,980,756 logical hash-input bytes on that code path; this calculation is not a measured physical-I/O or time saving.

## Current boundaries

- **LC-01 — small-write metadata:** SJN3 reuses unchanged logical pages after arbitrary overwrites and combines a changed short prefix with the next unchanged page within 32 extents. SJP2/SJM2 metadata follows user payload; replacing metadata preserves the unchanged private prefix in place. Retained prefixes bind the original header hash and validate every complete block, while node/page checksums and exact inventory still prove current ownership. Full seal content, one durable flush per mutation and same-journal recovery remain. Root/initialization require NodeFormat=3; earlier NodeFormat=2 is preserved and rejected without migration. The page-reference array still grows with page count. The current representation and unsupported-format boundary are owned by [latest-only journals](../latest-only-journal.md).
- **LC-02 — client initialization:** the client verifies fixed manifest/read-proof/owner identity and holds the reader lease/root namespace while initializing only the journal's root metadata. Its restricted tree rejects payload reads, listing and child lookup. The provider still opens and content-verifies every immutable file before mount publication. Client initialization is not payload qualification. Existing initialization intent and before-effect/after-record boundaries remain; mismatched/corrupt inputs are preserved and refused.
- **LC-03 — seal/reclamation:** Windows seals and reclamation inventory validation use a scoped pinned object-directory proof instead of walking all ancestors for each file. Every leaf, content checksum, current head and final membership/pending check remains. Reclamation validates saved plan names before opening fences and validates remaining content after all fences, avoiding an additional full pre-fence hash pass. Delete-sharing fences still require the existing immediate predelete hashes. Partial reclamation without `owner.lock` retains complete path walks; it never recreates the owner. All pins close before owned-directory deletion.

No service, UAC, Unity package, global configuration or external tool is changed. The original Unity project and previous evidence/results remain untouched. No journal payload cache, full-file copy-up, acknowledgement batching or timestamp-only content proof is introduced.

## Validation

The [dataset](../../reports/benchmarks/lifecycle-cost-optimization-20261008.json) records source/frozen-runtime hashes, raw evidence hashes, limits and the scope correction below. The required `pwsh -NoProfile -File scripts/build.ps1` passed locked restore, Release build and **1,498 tests / 0 failures / 20 conditional skips** (1,518 total; 5m 20.306s), with zero build warnings/errors. All 27 unrelated dirty-file hashes and the clean sibling Unity repository are unchanged.

Coverage includes random and descending writes across reconnect, current payload without metadata-induced prefix copying, independently computed seals, prefix damage/partial blocks/missing blocks, four interruption boundaries, final retirement-target corruption, old-format refusal, client root-only initialization and full provider corruption rejection, graft admission, pinned seal namespaces and partial reclamation with/without the owner lock. Each write retains its own durable flush/commit. The earlier focused run's five failures were test-reader sharing-mode errors; the corrected focused run passed 113 cases before the final additional retirement test and full suite. Failed fixtures/logs remain preserved.

## Bounded time comparison

All probes use fresh ordinary-user NTFS fixtures and each runtime's native format. Setup and content/reopened-seal checks are outside the timers. The two small-operation baseline runs are reused from the initial before/after/after/before exploration; the two final-candidate runs occurred after the read refinements, so this is **not a new balanced cohort**. No build or other benchmark ran concurrently with the final probes. These are small samples, not a general performance study.

| Operation | Before, seconds | After, seconds | Observed change |
| --- | ---: | ---: | ---: |
| Library client initialization: 256 files / 16MiB, mean of two | 0.205016 | 0.072486 | 64.64% shorter |
| Subsequent full provider payload admission, mean of two | 0.138438 | 0.148922 | 7.57% longer |
| Stopped seal: 256 files / 1MiB, mean of two | 0.213477 | 0.119339 | 44.10% shorter |
| Owned reclamation of that stopped fixture, mean of two | 0.628538 | 0.328238 | 47.78% shorter |
| 2,048 growing-range 64B writes, one pair | 17.210355 | 14.125682 | 17.92% shorter |
| 2,048 fully-private 64B overwrites, mean of two | 13.608212 | 13.708671 | 0.74% longer |
| 8,192 fully-private 64B overwrites, one pair | 53.800992 | 55.661433 | 3.46% longer |
| 2,048 append-control writes, mean of two | 11.293879 | 11.398550 | 0.93% longer |

The provider's full content check remains mandatory and has no intended code reduction; its observed 10.48ms increase is retained in the table. Client initialization excludes provider launch, mount, workspace registration and Editor preparation. The seal/reclamation figures are those operations alone, not complete checkpoint/release commands. Append sample ranges overlap; these samples do not establish a write-time gain for appends or fully-private overwrites.

### Corrected range-growth input

The first probes filled the entire file with 128 contiguous private chunks. Inspection of the preserved stopped journals showed **128 extents after both 2,048 and 8,192 writes**, in both runtimes: partial writes repack and merge contiguous private ranges. Increasing the call count therefore did not reproduce growing extent metadata. Those results remain valid overwrite-overhead measurements, including their regressions; the raw `scaling8192` key retains its historical name.

The additional bounded pair uses a 256KiB logical file initialized with holes, 128 spaced 64B seeds, then 2,048 shuffled 64B writes at 128B spacing. It asserts **128 initial / 2,048 final / 2,048 reopened extents**, exact final bytes, 2,048 individual flush/commits and matching reopened seals. Its logical put bytes fell **490,134,535 → 32,151,320 (93.44%)**, while elapsed time fell 17.92%. The unchanged-extent overwrite probes separately fell **64,419,073 → 21,065,407 (67.30%)** at 2,048 writes and **309,962,047 → 135,101,755 (56.41%)** at 8,192 writes, without time improvements.

Logical puts exclude storage-internal carried objects and are neither physical writes nor retained disk allocation. The first frozen candidate's overwrite slowdown motivated reading carried objects once and calculating full/prefix retirement hashes in one stream; final predelete hash checks remain. Old-format probes, sandbox publication failures and the harness's removed-helper API failure remain in ignored `artifacts/lifecycle-cost-followup-20261008/`; the helper was replaced with equivalent lease/tree composition only in the probe.

The original Unity Library/project, installed service and global/external-tool settings are untouched. The actual Unity file causing shutdown's write pattern remains unconfirmed. Full Unity shutdown/startup, checkpoint/release, restoration, whole-RAM/physical-storage and OS/power-loss qualification remain **NotRun**. These bounded reductions cannot be applied directly to US's 287-second shutdown or the user's earlier full-stage percentages.
