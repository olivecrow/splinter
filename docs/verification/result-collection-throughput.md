# File result collection throughput verification

[한국어](../ko/verification/result-collection-throughput.md) · [Scope](../plans/result-collection-throughput.md) · [Machine-readable results](../../reports/benchmarks/result-collection-throughput-20261026.json)

**RT-01–04, 2026-10-26.** Baseline `9b68a82cd89912e404d8acd6dcf7e48ce513a353`; adopted source hashes are in the dataset and lifecycle index. This is a real Windows product **None / project-snapshot / application none** comparison. It is not a Unity Editor lifecycle measurement or a live Plastic-server qualification.

## RT-01–02 — adopted behavior and safety

`FileWorkspaceProvider` and its Plastic wrapper now implement the existing `ISnapshotCheckpointSourceProvider`. Checkpoint/preservation share one source manifest inside the coordinator's complete pre/post-seal interval, replacing three source scans across the old checkpoint, inspection and preservation path. Runtime commits the result revision only after the seals match. The existing task lock, normal application stop, independent-result requirements and post-unmount seal remain.

Local full-payload preservation and independent-object verification use at most eight workers. Distinct hash objects have distinct pending/output paths; every new object still gets its own durable flush, content proof and publication. Existing/interrupted objects and later invocations are freshly verified. All active work drains before failure/cancellation returns, and the original product error is preserved. Recorded Plastic revision acquisition stays serial. Injected verification readers retain their sequential lifetime contract. No journal format, CLI/schema, source-manifest format, write/ACK or original-apply contract changed.

Focused tests passed **68/68**. New cases cover None/Plastic seal change, revision non-publication and same-task retry; corrupt pending object preservation without result publication; and active-worker drain on failure/cancellation. Existing independent-result corruption, deduplication, owner/peer, original apply and checkpoint/release tests remain. Synthetic Plastic tests establish the contract only.

## RT-03 — matched actual-product comparison

One disposable source on local NTFS: **2,281,701,376 bytes (2.125 GiB), 2,112 files**, comprising 64 × 32 MiB and 2,048 × 64 KiB unique raw files. The generator uses a fixed seed and records the complete SHA-256/length/mtime manifest. Product preparation moves this source into one common managed project basis. Each fresh workspace overwrites the same eight bytes in one large file, adds the same 64 KiB file, fixes the same modification times, checkpoints and releases. No Editor is launched.

Both runtime bundles were built with SDK **10.0.114** before timing. Run order is **before → after → after → before**, one workspace at a time, with no concurrent build/tests. Input bytes and modification times are checked before and after each task. All four baseline identities and all four result identities match. After release, the verifier reads and hashes every required independent payload. Each result has **2,114 unique objects / 2,315,321,344 bytes**. All four task mounts/deltas/providers were reclaimed; the common project basis was restored to ordinary NTFS with original bytes/mtime intact. Independent results and receipts are retained.

| Measured phase | Before mean (s) | After mean (s) | Saving (s) | Reduction |
| --- | ---: | ---: | ---: | ---: |
| Workspace ensure | 65.051 | 65.599 | -0.548 | -0.8% |
| Fixed private edit | 0.037 | 0.037 | approximately 0 | no meaningful change |
| Checkpoint | **98.429** | **58.944** | **39.485** | **40.1%** |
| Separate release | 39.788 | 35.326 | 4.463 | 11.2% |
| Sum of these workspace phases | **203.305** | **159.906** | **43.399** | **21.3%** |

Individual checkpoint times, in execution order: **87.991, 47.082, 70.807, 108.867 seconds**. Absolute timing varied substantially; both adjacent before/after comparisons still favor the changed code by 40.910 and 38.060 seconds. The mean clears the predeclared 20% and 10-second checkpoint threshold. The release improvement is small and is not presented as a separate large optimization. These measurements cover the combined change; they do not isolate the causal contribution of source-scan removal versus payload parallelism.

Common preparation took **9.031s**, and final common restoration **12.018s**. Each happened once, outside the ABBA workspace-phase sums; these are not before/after measurements. Setup, input/result proofs and human/agent waits are also excluded. There are only two observations per variant on one machine, OS caches were not cleared, and there is no universal speedup, peak-RAM, power-loss or device-failure claim.

Remaining large costs in this input are creation (**65.599s**), full result collection (**58.944s**) and separate release (**35.326s**). Full baseline/result materialization and fresh later-invocation proofs remain. Git creation/admission overlap, a new incremental internal journal checkpoint, and unchanged-subtree original restoration were not implemented by this slice. No original apply/discard performance, Unity start/compile/Play/Test/build, or live Plastic-server timing was run.

## RT-04 — repository gate and evidence

Required `pwsh -NoProfile -File scripts/build.ps1` passed: locked restore/build, **1,631 total / 1,603 passed / 0 failed / 28 skipped**, **538.906s** command wall time. Current product assemblies match the measured candidate. Documentation validation confirmed all **36** indexed source hashes, **115** symbol line hints and **12** new-document links. The focused build succeeded with no warnings/errors after correcting test cancellation-token analyzer warnings. Sandbox MSBuild failed before the baseline build; both measured variants were then successfully built in the same normal local environment. Those diagnostic failures are retained and are not counted as measured runs.

The subsequent flow-document refresh checked **37** indexed source hashes, **119** symbol line hints, **512** local Markdown links and matching topology for **six diagrams in each language**. The JSON index carries the same C20 phase order and measured command means. Product source/assembly hashes and the recorded gate log/TRX hashes/counters still match; no new runtime measurement or test run was needed for the documentation-only refresh.

Raw evidence: `artifacts/result-throughput-20261026/` contains frozen runtimes/hashes, schemas, `measure.py`, protocol/input manifest, four command/closure records, common prepare/restore, focused/full test logs and final closure. The dataset records command evidence hashes and source/assembly identities. Current contracts, both lifecycle-map languages and the JSON source index were updated.
