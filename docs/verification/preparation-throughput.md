# Preparation and observation throughput verification

[한국어](../ko/verification/preparation-throughput.md) · [Scope](../plans/preparation-throughput.md) · [Dataset](../../reports/benchmarks/preparation-throughput-20261026.json)

**PR-01–04, 2026-10-26.** Baseline `448f029a31a7c6f29d14d868c5e5f9e11aff5b19`; adopted sources and assembly hashes are pinned in the dataset. Actual Windows **None / project-snapshot / application none**, not a Unity Editor lifecycle or live Plastic-server benchmark.

## PR-01 — remove absence waits only with publication evidence

`ReadAsync` still defaults to one second of missing-file publication grace. Sharing violations still retry and throw if unresolved; complete invalid JSON, access errors and reparse/owner mismatches remain failures.

- `StartAsync` holds `client.lock`, so competing starts cannot publish a new intent/receipt pair. A child of an existing intent can still publish its receipt; observing its absence remains uncertain and preserves that child until a same-request retry sees the matching receipt. It reads those records once, reuses them for observation and reconciliation, and skips missing-file delays under that lock. It reads state even when both are absent; prior intent/receipt keeps state-publication grace. An unresolved intent still blocks launch, and a live process without matching receipt is never adopted.
- Ordinary observers overlap intent/receipt reads and join both before interpreting the result; state still has its normal grace. Failed/cancelled reads drain before returning. This does not infer that a provider is absent from a timeout.
- Project task observation reads any existing release receipt immediately, validating its identity. Absence does not wait before release/discard authorization exists; once authorized, it retains publication grace. Shared-basis ownership, read leases and actual availability remain checked.
- Task-locked capture/release and basis-gated dependency attachment skip absence grace for the records they themselves would publish. No flush, ACK, final seal or reclamation prerequisite was removed.

## PR-02 — bounded full scans and one-use response proof

Full file scans enumerate bounded membership first; None hashes distinct files with at most eight workers. Mounted Plastic source scans remain serial: its single-revision reader would repeatedly discard and rebuild its full proof when concurrent files interleave. Length, modification time and attributes must still match the enumerated entry. Output remains ordinally sorted with the existing manifest/metadata format. A failure/cancellation joins active workers before propagation or reclamation.

None/recorded Plastic implement the existing `ISourceInspectionProofProvider` for initial ensure. A full scan is surrounded by authoritative source-change barriers; its single-use proof binds request/source/application/path/base/result/snapshot/mount/policy. Revalidation checks owner and basis again, performs a complete metadata/membership scan between two fresh matching barriers, and returns the prior source result only for the same epoch/sequence/history. Same-size writes with preserved mtime, restart, membership/metadata/policy/identity changes and incomplete history reject reuse. Plastic rechecks registration isolation. A missing tracker or audit keeps full inspection; subsequent commands freshly inspect, and Editor startup discards the response proof. No persisted cache, public CLI/schema or storage-format change.

Focused tests passed **98/98** before native timing. Cases cover no-payload-read reuse with a locked file, single consumption, fallback full reads, mutation/restart/metadata/membership/policy/identity rejection, both scan barriers, incomplete history, Plastic isolation, first-start absence, unresolved launch and existing control-file/reclamation regressions. The final focused run includes release-receipt identity checks without authorization. The full gate additionally tests a child publishing its receipt during locked observation: the first call preserves uncertainty, and the same-request retry sees that receipt without relaunch. Timing tests use the existing nonparallel collection.

## PR-03 — actual matched-input ABBA comparison

**2,281,701,376 bytes / 2.125 GiB / 2,112 files**: 64 × 32 MiB and 2,048 × 64 KiB unique raw files. SHA-256, lengths and modification times match before/after every task. The same eight-byte overwrite and 64 KiB addition use fixed mtimes. One common project basis and its original provider remain in place across all trials; each task uses its selected frozen runtime, SDK **10.0.114**, with bounded raw SJR2. No concurrent build/tests or Editor, and no forced OS cache clearing.

| Phase | Before mean (s) | After mean (s) | Saved (s) | Reduction |
| --- | ---: | ---: | ---: | ---: |
| Ensure | 65.258 | 20.650 | 44.608 | 68.4% |
| Fixed private edit | 0.059 | 0.037 | 0.022 | no meaningful change |
| Checkpoint | 38.916 | 24.524 | 14.392 | 37.0% |
| Separate release | 35.071 | 9.578 | 25.492 | 72.7% |
| Sum of these workspace phases | 139.304 | 54.790 | 84.515 | 60.7% |

| Execution order | Ensure (s) | Checkpoint (s) | Release (s) |
| --- | ---: | ---: | ---: |
| 1 before | 65.392 | 43.103 | 35.098 |
| 2 after | 20.657 | 14.528 | 9.544 |
| 3 after | 20.644 | 34.520 | 9.613 |
| 4 before | 65.124 | 34.730 | 35.043 |

The two adjacent ensure pairs improve by **44.735s** and **44.480s**. The mean clears the predeclared 20% and 10-second ensure threshold. Control-wait changes, scan parallelism and response-proof reuse were measured together; the table does not attribute a separate causal saving to each. Command times include enclosing observation/state overhead. The small edit difference is not a meaningful throughput regression or improvement.

All four baseline identities and all four result identities match. Each retained independent result contains **2,114 unique objects / 2,315,321,344 bytes**; every required payload was read and hash-verified after release. All four task mounts/deltas/providers were reclaimed. Common prepare (**8.598s**) and final restore (**11.965s**) ran once each outside the workspace sums; original bytes/mtime were preserved and its path is ordinary NTFS again. Setup, input/result proofs and human/agent waits are excluded. Results, receipts and raw evidence remain; approximately 13 GiB was budgeted for the disposable source and results.

Remaining measured command costs are ensure **20.650s**, full result collection **24.524s**, and separate release **9.578s**. Full source/admission proof, independent baseline/result materialization and later-invocation result revalidation remain. Their inner costs were not individually timed. Further result-proof reuse would require pinned object identity/lifetimes; incremental bounded-journal checkpoints and restore redesign need separate recovery work. This slice does not remove those protections.

Only two observations per variant on one local NTFS drive. No universal multiplier, Unity start/compile/Play/Test/build, live Plastic server, original-apply/discard timing, peak RAM, power-loss or device-failure qualification. Earlier RT/LU measurements remain historical comparisons, not an additional sample in this dataset.

The final checkpoint samples vary substantially: **14.528s and 34.520s** after versus **43.103s and 34.730s** before. The latter adjacent pair is nearly equal; the mean is an observation for this workload, not a guaranteed checkpoint saving. No individual cause for that variation was instrumented.

The preliminary candidate and its passing full gate are retained in `artifacts/preparation-throughput-20261026/`. Final review kept mounted Plastic scans serial to avoid repeatedly evicting its single-revision proof. Both final runtime bundles were frozen again and the complete matched-input ABBA experiment was rerun in `artifacts/preparation-throughput-final-20261026/`. Preliminary measurements are excluded from every final mean. Approximately 13 GiB was budgeted per comparison, 26 GiB including retained preliminary source/results.

## PR-04 — repository gate and evidence

Required `pwsh -NoProfile -File scripts/build.ps1` passed: locked restore/build, **1,646 total / 1,618 passed / 0 failed / 28 skipped**, **550.621s** command wall time. The raw log, gate metadata and copied TRX are retained in `artifacts/preparation-throughput-final-20261026/`. The candidate's product sources are unchanged since native timing. Two intermediate test fixture nullability compile errors were corrected before the passing focused run; they are not benchmark runs. The first sandbox baseline build could not write the SDK sentinel; both measured bundles were built successfully in the same normal local environment.

Raw evidence: `artifacts/preparation-throughput-final-20261026/` holds runtime bundles/hashes, command schemas, protocol, input manifest, measurement/summary scripts, four command/closure records, common prepare/restore, focused/full gate logs and closure. The dataset records evidence and source/assembly hashes. English/Korean contract, documentation map and lifecycle map/index are updated together.

Documentation validation confirmed **39** indexed source hashes, **132** symbol line hints, **514** local Markdown links, **19** stages and matching topology for **six diagrams per language**. Final source/evidence hashes and every current product runtime file match the final measured candidate. The dataset records gate counters and log/TRX hashes.
