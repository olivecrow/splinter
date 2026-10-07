<a id="library-범위-payload-압축의-후보-선정"></a>
# Candidate selection for Library range payload compression

[한국어](../ko/verification/library-block-compression-feasibility.md)

On 2026-10-03, read-only analysis of Unity CLI A/B cost and 4 Bee files in a Plastic fixture selected **independent block compression of bounded range payloads**. This document owns prerequisite candidate-model evidence; product journal/NTFS allocation was unmeasured at the time. Subsequent independent codec follows [separate verification](workspace-payload-codec.md), and actual product integration follows [JP](journal-payload-compression.md). The candidate model is not inherited as total product savings/support qualification.

<a id="기존-비용에서-고른-대상"></a>
## Target selected from existing costs

Product candidate is `development @ 6edc3de`; program source was unchanged from prerequisite UH `4e262b7`. Recalculated values below from preserved JSON of the [UH execution](unity-cli-handoff.md). final is cumulative storage through immediately before reclaim, not current file allocation.

| Sample | Cumulative journal B | Cumulative payload B | Frame/metadata B | Frame/metadata share | Average stored payload B per write |
| --- | ---: | ---: | ---: | ---: | ---: |
| A ready | 123,356,406 | 116,220,215 | 7,136,191 | 5.79% | 8,405.9 |
| B ready | 123,151,262 | 116,035,959 | 7,115,303 | 5.78% | 8,420.6 |
| A final | 136,298,076 | 123,511,979 | 12,786,097 | 9.38% | 4,897.8 |
| B final | 136,120,355 | 123,349,610 | 12,770,745 | 9.38% | 4,898.5 |

Payload makes up most cumulative storage, so payload was selected before frame-metadata reduction. The per-write average is not the original callback size distribution. It averages bytes stored after current private prefix/suffix reuse; this alone cannot establish that all writes are 4KiB. Internal write times for A/B final are 124.92/128.22 seconds, with flush shares 99.08/98.86%. These nested measurements are not added to Editor wall time, nor is individual durable flush changed for storage work.

At ready, provider working sets were A/B 121,761,792/120,512,512B; private bytes 80,338,944/79,257,600B. Owned Editor process-group working sets were 3,290,206,208/3,072,770,048B; private bytes 3,736,121,344/3,588,870,144B. These are group samples at different times, not simultaneous peak/system physical RAM/reduction rates. Storage priority is retained; memory/Editor concurrency were not selected for this implementation.

Post-reclaim records, independent results, and shared-runner cost follow UH's existing ledger. Result payload already included in records is not added again. UH journals were reclaimed, so current worker live Library data/replaced-record share cannot be calculated. [Prior actual Library classification](resource-efficiency-followup.md) of 106,388,937B live Library and large Bee paths guides candidate discovery; those are not inherited as the same UH values.

<a id="고정한-읽기-전용-입력"></a>
## Fixed read-only input

The original is the current Library at `T:/plastic-fixture-20261002`. Only the following 4 were read, distinguished from prior worker callback traces, journals, and independent-result payloads. No full source/Library was duplicated.

| Path under `Library/Bee/` | Logical bytes | First 12 input SHA-256 digits |
| --- | ---: | --- |
| `1900b0aE.dag` | 23,432,080 | `a9cd54908001` |
| `1900b0aE.dag.json` | 17,762,603 | `1a32020ca67b` |
| `1900b0aE.dag.payloads` | 7,154,032 | `565f29aa13f5` |
| `1900b0aE-inputdata.json` | 3,511,183 | `3b483045762b` |
| Total | 51,859,898 | Full hashes in raw measurement |

Questions, input, and exit criteria were first fixed in `plan.json` of new local evidence directory `artifacts/storage-foundation/library-payload-20261003/`. `Program.cs`/`Probe.csproj` are analysis materials for this input, not new product commands/general harnesses. Ran **24 file/profile combinations** with .NET `10.0.12` BCL `BrotliEncoder`, 4KiB/64KiB/1MiB independent blocks, quality 1/4, and window 22. No dictionary/cache retained between blocks.

Read-only `FileShare.Read` handles for all 4 files were retained through all profiles, rejecting opens that would coexist with other writes/replacement. For every file/profile, verified full SHA-256, matching length/mtime, and exact restoration of every compressed block. Reparse files were rejected. Original payload copies and compressed-payload-file storage were 0B; workspace, Editor, VSS, service/grants, and registrations unchanged. These briefly held analysis file handles are not a product original-write-lock policy.

<a id="모델-크기와-후보-선택"></a>
## Modeled size and candidate selection

The model **assumes an additional 64B metadata per compressed block**, selecting compression only when the sum is smaller than raw. Existing common frame overhead is excluded on both sides. 64B is not an actual new product-format size. The following totals are logical payload sizes across the four inputs.

| Independent block | quality | Modeled stored B | Modeled payload reduction | encode ms | decode ms |
| --- | ---: | ---: | ---: | ---: | ---: |
| 4KiB | 1 | 8,650,872 | 83.32% | 332.93 | 213.84 |
| 4KiB | 4 | 7,839,787 | 84.88% | 2,636.34 | 236.62 |
| 64KiB | 1 | 5,038,085 | 90.29% | 113.12 | 95.86 |
| 64KiB | 4 | 4,154,551 | 91.99% | 756.84 | 114.31 |
| 1MiB | 1 | 2,230,458 | 95.70% | 60.37 | 106.27 |
| 1MiB | 4 | 1,587,525 | 96.94% | 505.76 | 81.37 |

Each row sums CPU intervals executed once. It excludes reads/hashes, Editor waiting, and durable flush; it is not a performance comparison controlling profile order/cache/native codec costs. High ratios for large blocks cannot be applied to actual small callbacks. Product reads may need full-block restoration even for a small range; read amplification is a separate gate.

Quality 1 met the question-stage selection criterion of **at least 25% reduction in the 4KiB model aggregate**. Quality 4 gained only 1.56 percentage points in this model, so the next unit begins with the quality 1 candidate. This criterion is not a product acceptance threshold/time SLA. Raw fallback selects storage for blocks where compression would be larger; it is unrelated to full-file copy-up or snapshot hydration.

Explicit reusable byte arrays totaled 3,145,990B. Profile-loop thread cumulative managed allocation totaled 16,640B for initial 4KiB/q1 and 0B for later combinations. Initial array allocation, native codec state, pool/residency/total RAM are excluded, so this is not memory-saving evidence. The next unit must separately verify the product's request-sized-buffer boundary.

<a id="판정과-재사용-한도"></a>
## Judgment and reuse limits

**The read-only candidate-selection unit is closed.** `measurement.json` preserves full input hashes/times and 24 roundtrips/size/CPU samples; `summary.json` preserves hashes/recalculation of 4 raw UH JSON files and probe/binary hashes. `summarize.py` checked profile count, all restorations, and selection criteria. Product code/schema/state and journal format unchanged.

Distinguishes subsequent [independent codec](workspace-payload-codec.md) and [product JP integration](journal-payload-compression.md). Actual journal integration was NotRun in this candidate analysis; partial reads, replay/compaction/candidate/seal, budgets, interruption, and original/peer protection in the new format are not passed by this model. Only separate JP direct evidence supplies bounded qualification. Retains SF-02/03 Partial, large cost/total peak/VSS attribution unknown, and existing C: pending state.
