# Git payload retention verification

[한국어](../ko/verification/git-payload-retention.md) · [Fixed scope](../plans/git-payload-retention.md)

This record owns GR-01–03. The tested candidate changed retained-payload capacity and overflow-safe eviction arithmetic; it preserved warm ordering, decoder concurrency and durability. It was rejected after **2.111s (4.2%)** mean creation savings with greater provider memory; the product contract retains **256MiB / 4,096 entries**. The [machine-readable report](../../reports/benchmarks/git-payload-retention-20261027.json) retains exact observations. Local raw evidence is `artifacts/git-retention-followup-20261027/`; `protocol.json` fixes the cohort, `candidate-source.json` and `candidate.patch` bind the experimental candidate, and `before-runtime.json` / `after-runtime.json` identify the frozen measured runtimes.

## GR-01 — Native creation protocol

The measured baseline was `bea4acdb95f3ea2a914587c7f5e5293b57940e7d`, with **256MiB / 4,096 entries**. The experimental candidate used **1.5GiB / 32,768 entries**, retaining **2 decoder sessions / 128MiB proof read-ahead**. These larger limits are not a supported product option. This baseline already included BO-01–05 and is not a pre-BO comparison. [BD-01](unity-lifecycle-bottleneck-diagnosis.md) supplies the repeated-decode/unused-eviction motivation; its 391.3053613s GUI lifecycle is not the baseline timing for GR-01. The discarded cache-wait bypass remains excluded.

| Protocol item | Fixed boundary |
| --- | --- |
| Input | Commit `20967120692904985ea6320ce01f93239ba136ff`; 1,602 source files / 2,804,769,296 bytes; 42,038 Library files / 3,105,800,655 bytes. This whole input differs from BD-01. |
| Setup | Dedicated `.local/gr27-source` experiment clone and copied old Library cache; `.local/gr27-home` owns the measured tasks. External setup is outside timing and is not a product full-copy path. |
| Order | Before → after → after → before; one editorless workspace at a time, two observations per variant. |
| Timed boundary | Fresh `workspace ensure` elapsed on one monotonic stopwatch. Common Library prepare/restore, complete input verification, each private-edit/isolation check and explicit discard are separately timed. The isolation interval includes Git status and original-byte comparison; it is not edit-only latency. |
| Integrity / closure | Same complete source/Library bytes, modification times and attributes; private edit/original isolation and exact owned discard/closure checks. No silent file exclusion or replacement task after failure. |
| Observability | G1 enabled for both variants, bounded state sampler, Windows provider lifetime peak working set/private commit queried after ensure. |
| Failure | Stop the series; retain exact key/home/id/intent and evidence without automatic replacement or forced stop. |

All four fresh ensure calls completed. Each row uses `ensure-cost.json`, its matching G1 preparation intervals and the owned provider's Windows lifetime peak working set. Warm and refresh overlap inside attach, which is itself inside ensure; these durations must not be added.

| Trial | Variant | Ensure (s) | Warm (s) | Index refresh (s) | Provider peak working set (MiB) |
| --- | --- | ---: | ---: | ---: | ---: |
| 1 | Before | 51.371 | 21.108 | 24.547 | 710.2 |
| 2 | Candidate | 48.650 | 20.495 | 21.700 | 2,841.5 |
| 3 | Candidate | 48.449 | 20.330 | 21.368 | 2,827.9 |
| 4 | Before | 49.950 | 21.086 | 23.244 | 671.4 |
| Mean | Before → candidate | **50.661 → 48.550** | 21.097 → 20.412 | 23.896 → 21.534 | **690.8 → 2,834.7** |

The exact mean ensure saving was **2.11080385s (4.16656%)**. Mean attach changed **25.130 → 22.770s**. Re-decoding after a completed proof fell **532,746,522 → 11,628,972 bytes (97.8%)**, and unused read-ahead eviction fell **442,171,392 → 0 bytes**, averaged across each variant. Every trial still performed **1,563 full blob proofs / 2,787,029,670 first-proof decoded bytes**. Thus substantially less repeated decoding produced only a small creation-time saving.

Mean provider lifetime peak working set rose **724,361,216 → 2,972,444,672 bytes (0.675 → 2.768GiB)**, approximately **4.10×**. Peak private commit means were **740,626,432 → 3,006,611,456 bytes**. These are peaks of the provider process alone, excluding decoder children and the rest of the system. They are not retained-cache or whole-system RAM limits.

All four tasks were explicitly discarded and their owned providers exited. Complete source/Library bytes, modification times, attributes and source Git metadata matched after every trial and final restoration to ordinary NTFS. Common Library preparation took 40.585s and restoration 22.621s, each once outside the ensure comparison. The sampler reported no read errors, failure or limit exhaustion. Candidate source/tests are archived locally and removed from the product; all final source/test files match `bea4acd`. The restored solution rebuilt with zero warnings/errors, and focused Git cache/reader/read-ahead/diagnostic tests passed **56 / 0 failed / 1 conditional skip**. The earlier BO full gate belongs to that unchanged source; the 1,734-test gate belongs to the rejected candidate.

## GR-02 — Safety and regression evidence

On the experimental candidate, the required `pwsh -NoProfile -File scripts/build.ps1` passed **1,734 tests / 0 failures / 30 conditional skips / 1,764 total**. `build-and-test.txt` and `candidate-tests.trx` retain that result; it does not establish adoption or replace validation of the final retained source. Skipped opt-in/native cases are not newly qualified by this gate.

- The candidate's `GitPayloadCacheTests` exercised its default constructor's empty cache and small-fixture allocation bound, reported byte/entry/reader/window limits, alias reuse and complete disposal. This verified on-demand construction without allocating the full 1.5GiB in the test.
- Invalid negative, overflow-wrapped 2GiB, above-limit and maximum-integer byte capacities, and invalid entry counts were rejected before Git access. The candidate's payload cap was exactly **1,610,612,736 bytes**, not 2GiB. Eviction compared incoming bytes with remaining capacity before allocation.
- Small explicit budgets exercised entry-driven eviction with a retained full proof, non-chunk-aligned byte limits and correct re-decoding. Cached-byte corruption was also checked with the candidate capacity configured. Existing loose/packed reads, whole-proof failure, proof eviction, health/poison, peer failure and close/drain coverage remained.

Independent read-only source review found no demonstrated safety defect. It checked integer headroom, pre-allocation eviction, pending first-proof chunks sharing the same budget, unchanged hash/health/publication/drain paths, and the scope of the new boundary tests; it ran no build or tests. This review is separate from the measured experiment and does not qualify whole-process memory. The safety contract still requires complete object/framing proof before publication, a current chunk hash on every cache hit, exact immutable identity and health checks, failure publication exclusion and all admitted-reader drain. The separate 4MiB proof budget, mandatory index refresh, two decoders and 128MiB window are unchanged. Journal flush and durable ACK are unchanged.

## GR-03 — Decision and limits

**Rejected; retain 256MiB / 4,096 entries.** The **2.111s (4.2%)** mean creation saving did not meet the requested substantial time-saving criterion, while provider memory rose markedly. The passing candidate gate and fewer decoded bytes did not justify adoption. Preserve the experiment as evidence rather than a selectable cache mode; the final cleanup/source checks remain distinct from this performance decision.

The candidate byte limit covered retained payload per tree, with a separate entry count bounding small-file metadata growth; neither was a whole-process RAM cap. Proof/index metadata, decoder children, request buffers, GC retention and temporary allocations remained outside it. Sampled cache peaks and Windows lifetime process peaks cover different intervals and resources; do not add them or claim a measured whole-system maximum. The candidate reserved no large payload buffer at startup. Consumption-order/LRU policy changes remain separately unmeasured; no such algorithm trial or automatic follow-up was performed.

This is one machine with two samples per variant and OS caches left intact. The Library input is an old cache, not a proved warmed cache for this source. Editor startup/compilation/Play/Test/build, original application, power loss, other backends and whole Unity lifecycle gains are unqualified. Earlier WC and BD records retain their own inputs, measured boundaries and decisions; this experiment neither replaces their numbers nor automatically resumes those workflows.
