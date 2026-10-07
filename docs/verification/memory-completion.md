<a id="메모리-단계의-직접-검증"></a>
# Direct verification of the memory stage

[한국어](../ko/verification/memory-completion.md)

The [fixed ME-01–05 scope](../plans/memory-completion.md) is **closed as scoped-complete-with-limitations.** It verifies internal journal buffer reduction, same-input semantics/failure contracts, and owned provider/Editor cost/reclaim on current Windows x64 NTFS alpha. Next general development is stable parallel work. Cumulative allocation, pool leases, live heap, process working set/private bytes, and shared system usage are not interchangeable.

<a id="고정-대조"></a>
## Fixed comparison

Baseline Runtime is `d100d2331fa059dc13ecadbd18f574e3b49b7371a5d1009cefe47ddfb8e69d17` from physical-storage completion commit `2bb3d06`. The same artifact probe ran in separate baseline/candidate child processes, holding actual preserved Unity A/B journal/identity under read-only handles. Source evidence is not rewritten/compacted. `artifacts/memory-completion-20261003/` contains raw evidence.

Current Runtime hash is `90d37ae0a1fd4a77542e4fb15425e9e8f8da92d06ebb3b9a78333d9d17e39cb4`. CLI/service, Core/schema/codec identity, overlay v2/SJR2/candidate v3, quality 1/window 22, compression selection criteria, and individual durable flush/ACK remain. This is an internal Runtime change retaining common APIs/fakes; no new command/module/installation.

<a id="me-0103-같은-입력과-bounded-buffer"></a>
## ME-01–03: Same input and bounded buffers

The same probe's separate child processes ran once per input. This is offline replay comparing private state of actual preserved journals with fixed identity/fake immutable basis, distinguished from live VSS/full original-state verification. Original hash, identity, sequence, private state/usage, node/extent/hidden/path-change counts, and base payload read 0 matched before/after. Raw/encoded selection and selected bytes/hashes for small fixed function inputs also matched. Automatic tests compared 112B boundaries through maximum 1MiB and raw/compressed/selection boundaries.

| Direct observation | Baseline → current |
| --- | --- |
| Concurrent Shared pool leases in journal 1MiB compression preparation | 3,145,728 → 1,048,576B, 66.7% decrease. Caller input/selected-result arrays separate |
| Concurrent leases in journal 64KiB compression preparation | 196,608 → 65,536B. Raw selection also has no duplicate encoded buffer |
| Empty/EOF validation lease for codec 1MiB block | 1,048,576 → 16,384B |
| Actual leases for 7B/4KiB reads of the same block | 1,048,576 → 32,768B. Requested size is 16,384+count, including pool rounding |
| Full-block read lease | Remains 1,048,576B. Full validation before output publication and bounded limit retained |
| Maximum concurrent pool lease in actual A/B replay | Each 2,162,688 → 1,130,496B, 47.7% decrease. All rent/return counts match |

Chunking validates the whole stream; it does not remove read amplification. Verified 16KiB boundaries/EOF/int.MaxValue, repeated/concurrent calls, resigned trailing/declared lengths/truncation, excess restoration, and unchanged caller output/lease return on failure. No decoded/persistent cache or forced product GC added.

| Actual preserved input | A | B |
| --- | ---: | ---: |
| journal bytes / durable sequence | 37,188,067 / 27,970 | 53,212,331 / 61,687 |
| Fixed journal SHA-256 | `279faf6afe24070916732d4ab9fe7a81542bbb7ee64610133931e31f84cba1db` | `147b0ffdac9e38bdbfa02973e38dca91cd6edb01f419b59a2723661f73352ad2` |
| Replay cumulative managed allocation, baseline → current B | 106,260,984 → 104,698,920 | 174,994,000 → 173,451,112 |
| Heap after probe GC, baseline → current B | 6,788,208 → 5,228,776 | 8,845,968 → 7,293,760 |
| index nodes / extents | 982 / 20,880 | 2,188 / 33,252 |
| Replay time, baseline → current ms | 673 → 718 | 1,063 → 1,132 |
| Process private bytes, baseline → current B | 42,799,104 → 43,798,528 | 43,765,760 → 42,934,272 |

GC ran only in the probe. Heap includes shared-pool retention/probe overhead as well as indexes; differences above are not index-reduction rates. Managed allocation decreased about 1.5MiB; index counts unchanged. Private bytes increased for A. Timing is a single observation of about 6–7% increase, not an SLA/significance judgment. Function lease reduction is not claimed equivalent to process/total RAM reduction.

<a id="me-04-현재-후보의-제품-ab"></a>
## ME-04: Product A/B with the current candidate

Used existing `T:\plastic-fixture-20261002`/`T:\pt1002`, Plastic/Unity/snapshot/NTFS, budget 17,179,869,184B, Unity `6000.3.22f1`, existing CLI `1.0.0-beta.5`/Pipeline `0.8.0-exp.1`, and restricted service with ordinary-user tokens. All command-cost callerElevated records are false.

| Task | Fixed key / id | Loaded values and independent result |
| --- | --- | --- |
| A | `mem-t-a-20261003` / `90390d3a165005fb166e6a15` | `ME-a-one`, Editor PID 60668/exact start ticks. Result `sha256:467b1862df8372ba72647c9ef0e81f37a78281c789022c40cc525464076aef27` |
| B | `mem-t-b-20261003` / `309595a36609254dfcb64c8c` | `ME-b-one` → `ME-b-two` after A release, PID 12744/same start ticks/new MVID. Result `sha256:81eed32b46d4ae1a078e57cfa583572126cd69caecd66729e76a3c17b8ba2777` |

After both creations/64KiB original sample reads, initial payload/read cache/payload/sequence were 0. Explicitly wrote the same four fixed PS Bee files totaling 51,859,898B to each new Library path using 64KiB streams; reverified mounted hashes after normal stop/same ensure. This is not original-read hydration/full clone. Read each worker's 128KiB repeating-string source/marker and preserved independent file results. Same-ensure id/path/key/digest/base/snapshot remained unchanged.

Initial catalog during preparation returned `No Pipeline instance found`; after observing reload, connected to the same-path catalog. First A/B eval and first B eval after subsequent reload timed out on the main thread at 5 seconds. Each raw failure was preserved; only the same read query was retried once to verify actual project/PID/ticks/value/MVID and compiling/updating/failed=false. B recompile was requested once; thereafter only completion state was read. After reload B port changed 7802→7801, so exact path was retained per command. Discovery/first-response limitations are not marked resolved.

| Owned-cost point samples | A | B |
| --- | ---: | ---: |
| Provider private / working set B after creation | 25,677,824 / 64,184,320 | 27,197,440 / 65,798,144 |
| Provider private / working set B after loaded | 94,478,336 / 136,040,448 | 95,522,816 / 135,811,072 |
| Owned Editor group private / working set B after loaded | 3,932,237,824 / 3,497,758,720 | 3,725,754,368 / 3,188,903,936 |
| Normal stop command ms | 60,710 | 31,110 |
| Provider/Editor group processes/handles/private/working set after release | 0 | 0 |

Group is an observed sum of root/children/supervisor, not physical RAM sum. Retains the observation that Editor cost exceeds provider cost; Editor count/validation-view selection remains AI procedure. Tens-of-minutes stalls blocking work were not observed for this input. Installation, pools/queues, automatic modes, and original Editor sharing are not added as memory features.

Separately recorded shared system available physical/committed/commit limit/cache by stage. After creation available=41,087,598,592B/commit=50,550,927,360B; after loaded 34,919,989,248B/58,339,336,192B; after reclaim 42,497,175,552B/48,774,729,728B. Includes other apps/original Editors/OS cache, so no task attribution or before/after reduction ratio is derived. Lifetime peak, point samples, and whole instantaneous peak are distinguished.

Final two owned snapshots are missing; mount/provider/delta and owned resources are 0. After reclaim, fully verified object lengths/hashes for each result's 66 files/376,897 logical B and baseline's 62 files/245,523B; marker/128KiB source also matched. Preserved bytes/hashes of 65 original source/admin files and 738 existing record files, 5 Plastic registrations, and creation identities of original Unity PIDs 68624/85184/52688. Original Editor loaded-project/code qualification is not claimed from PID presence alone. No write/reclaim requests were sent to existing pending C: or non-owned snapshots.

<a id="me-05-회귀와-종료"></a>
## ME-05: Regression and closure

Verified `pwsh ./scripts/build.ps1` locked restore/Release with 0 warnings/errors, **900 passed / 0 failed / 1 skipped (901 total)**. 16 added cases cover codec chunk/nonpublication boundaries and journal selection/byte comparison. Four failures in initial focused execution were duplicate output directories in test input; corrected with Distinct, then required full execution passed. Failure evidence is also preserved.

Validated schemas of **44** actual responses using current product `ContractCatalog`. UTF-8 checks for 92 files, 414 document links, and diff checks passed. Final counts/judgment for current source/binary evidence are fixed in `completion-final.json`. Current memory implementation and this single set are closed; final A/B or new benchmarks/harnesses are not automatically repeated.

Whole instantaneous peak, Brotli native memory, and workspace attribution of pool retention/system RAM are unknown. Large/cold/ARM/remote/OS/full concurrent-count boundaries, new actual development view/cache qualification, and previous SF/NR states are not inherited. Reopening requires actual new memory shortage/leak/persistent growth, work blockage, or an explicit support-expansion request with input/exit criteria.
