# Remaining lifecycle optimization verification

[한국어](../ko/verification/remaining-lifecycle-optimization.md) · [Active scope](../plans/remaining-lifecycle-optimization.md) · [Machine-readable summary](../../reports/benchmarks/remaining-lifecycle-optimization-20261028.json)

Earlier row paragraphs retain status at the time of their evidence; the current row table and final MX10 closeout own final status. The MX-01–10 program is complete. MX-01 closed diagnostic attribution; MX-02–04, MX-06/07, MX-08B and MX-09 were not adopted. MX-05 A/guarded B2 and MX-08A are retained. The final full gate and 4 matched Git/Unity apply/discard runs passed correctness, but no large whole-lifecycle improvement or individual causal saving was established.

**Initial MX-01 record and then-current progress (historical summary):** **MX-01, 2026-10-28: diagnostic attribution completed.** The captured initialization scope is dominated by Rider and Burst, with concrete product investigation paths assigned to MX-02/04/08. This is not a completed performance fix or a before/after improvement. **MX-02, both MX-03 candidates and MX-04 were measured, rejected and removed. MX-05 is complete: A and publication-count-guarded B2 were adopted after final ordinary-NTFS restore comparisons and the full gate. Unconditional B1 rejection and earlier failures remain preserved. MX-06 is complete as not adopted; MX-07A and MX-07B were measured, rejected and removed; MX-07 is complete. MX-08A remains provisional and MX-08B is rejected and removed. MX-09 is complete as rejected and removed after the actual Git/Unity finalization ABBA failed the predeclared material-gain screen. MX-10 remains the final combined verification. The whole program is active.** The complete program remains active.

## Input, capture and interpretation

Program starting commit is `24956f4975a8c9ae57a21f4c52a656b9f8b51ea3`; the frozen measured product source is `bea4acdb95f3ea2a914587c7f5e5293b57940e7d`. One Windows/NTFS GUI workspace used Unity **6000.3.22f1** and dedicated fixture base `f121a5adc9c4248994774dea03843504c949f09a`. An external launch decorator supplied supported profiler options; product launch contracts and installed packages were unchanged. Additional fixture profiler code was warmed on ordinary NTFS before capture. Instrumentation and fixture work can affect timing.

The raw recording is **567,910,230 bytes**, SHA256 `8e6ca48b7d382f3f3abaf52a616f90fb5168580dba1f04b41855bb2e0a260a49`. Large raw data remains ignored under `artifacts/remaining-optimization-20261028/profile-01/`. The tracked JSON retains sanitized hashes, scoped results and limits rather than raw samples, local account paths, launch arguments or credentials.

The first postorder export reached **512MiB**, retaining 1,356,547 of 15,628,788 inventoried samples before writing the long initialization ancestors. The small emitted methods did not bound the missing cost. A first separate analysis completed frame 0 Main Thread, then stopped at an unnamed marker in another thread. Its evidence remains preserved. Corrected `summary-export-02` traversed **all 15,628,788 loaded samples**, two frames and 216 thread views, and verified the raw unchanged. Its normal exit code 2 reports a partial summary rather than a complete capture.

| Corrected summary coverage | Result |
| --- | ---: |
| Visited / inventoried samples | 15,628,788 / 15,628,788 |
| Individual rows retained | 4,019 |
| GC.Alloc samples aggregated without individual metadata | 15,076,698 |
| Below-threshold details omitted | 536,742 |
| Eligible large rows rejected/evicted from the bounded reservoir | 11,329 |
| Unknown marker-identity samples / buckets | 434 / 245 |
| Unknown marker samples in initialization context | 0 |
| Hierarchy errors / unknown self calculations / marker-name conflicts | 0 / 0 / 0 |

**Summary status remains partial; original capture loss remains unknown.** Full traversal does not prove absence of pre-capture, buffer, domain-reload or cross-frame recording loss. The 13 requested ancestors, 14 initialization markers and 1,944 initialization direct children match all original fields in the earlier main-thread analysis: **1,971 identical target rows**. Two unrelated invalid-ID labels formerly shown as `OverlayCanvas.Initialize` are corrected to unknown; they are not used for class attribution.

Self time excludes complete direct children but includes waits and uninstrumented work; it is not CPU utilization. Named-marker totals in the corrected export combine threads/frames and cannot be treated as elapsed startup time. Main-thread aggregates below come from the earlier analysis that completed exactly frame 0 Main Thread's 5,844,396 samples, before visiting another view, validated against the identical raw and corrected named-marker identities. No nested or cross-thread durations are added. Historical BD-01's 49.364s and MX-01's initialization time are **not an optimization pair**.

## Initialization and I/O findings

| Disjoint part of main ProcessInitializeOnLoadAttributes | Inclusive seconds |
| --- | ---: |
| RiderScriptEditor static constructor | **12.503079936** |
| BurstLoader static constructor | **9.06021888** |
| All remaining children and wrapper/outer overhead | **1.163828224** |
| **Complete outer scope** | **22.72712704** |

Rider and Burst are direct siblings and account for **21.563298816s / 94.87912298834934%** of this scope. Their child/self durations must not be added again. The 288 retained non-GC direct children total 22.722389288s, only **0.0000367s** below all wrapper direct children. The owned WorkspaceBridge constructor is **0.080975696s** (0.066170396s self); optimizing that constructor alone cannot produce a large saving.

Rider's source connects missing-solution initialization to `ProjectGeneration.Sync`, per-project-file `FileIOProvider.WriteAllText`, `LastWriteTracker.UpdateLastWriteIfNeeded`, and the persisted-state `LastWrite` setter's `Save(true)`. Corrected profile metadata confirms **135** repeated `Library/com.unity.ide.rider/PersistedState.asset` operations / **3.4662601s inclusive** under Rider. The preserved main-only detail contains 136 / 3.4721689s plus nested writes and closes; some smaller details were displaced by other-thread waits in the corrected reservoir. These are overlapping scopes, not additional seconds. Rider's **8.842435832s self** is not wholly attributed to solution generation, I/O or flush.

Burst's two largest direct `JitCompilerService.CompileInternal` calls take **5.785662976s** and **2.420623872s**. Compiler-service management commands share entrypoints, so their names do not prove that all this time compiled user Burst functions. No installed package or private callback was changed or disabled.

Frame 0 Main Thread records **2,640 File.Open calls / 12.951948684s self** and **1,993 File.Close calls / 0.8690874s self**. Earlier exact-path prefix evidence assigns **5.048252412s** to workspace ScriptAssemblies and **4.740115272s** to PackageCache opens; these subsets are not extrapolated. The corrected all-thread File.Open total is **16.856342584 thread-seconds**, not a main-thread or lifecycle duration. Open cost has not been divided into provider admission, stat/proof, kernel/cache/antivirus or loader behavior; Close is not synonymous with flush.

The retained BD-01 Bee input comparison separately accounts for all **292 executed actions**: 67 existing ILPP responses plus configuration change only their session pipe, while the 68th ILPP action is the new bridge. All 74 old Csc main responses remain identical and old Csc actions are reused. The bridge's new source/asmdef requests compilation; it does not serially block all existing ILPP work. Existing asset-import line items are small, and their individual invalidation causes remain unknown. This is same-run dependency attribution, not a speedup experiment or permission to reuse stale process-bound graphs.

## Product routing and closeout

| Follow-up | Evidence-supported scope | Preserved boundary |
| --- | --- | --- |
| MX-02 | Test whether an owned precompiled Editor-only bridge avoids unnecessary compilation/reload caused by source/asmdef insertion. | DLL import can also require reload; neither the compiler scope nor 22.72712704s initializer scope is guaranteed to disappear. Measured and rejected below; no material benefit or adoption. |
| MX-04 | Correlate expensive virtual-Library opens with provider operations, then optimize a proved product cost. | Keep namespace/content/identity proofs and reader/context lifetime. No unproved gate removal or revived mapped-cache/native Bee path. The safe non-final Close candidate was measured and rejected below; no Open optimization was retained. |
| MX-08 | Measure product write/metadata/replacement handling against the observed generated-project and repeated persisted-state workload. | Keep ordered mutation, durable ACK, bounded drain and exact recovery. No package patch, global IDE setting change, fake generated state or durability relaxation. |

The exact diagnostic workspace exited normally with code 0, was explicitly discarded, and restored identical source/Library bytes, modification times and attributes. An external observer initially confused root Editor and asset-worker state; the same task was recovered, with **103.72708079998847s** separately recorded recovery. The original external server/preferences were restored and health-checked. Package-free analysis processes preserved the raw and external state. These are agent-owned diagnostics and recovery, not a product server-management capability or an uninterrupted timed lifecycle.

MX-01 closes diagnostic attribution only. It establishes no clean before/after gain, general startup multiplier, final apply/discard comparison, new Play/Test/build qualification, peak-memory reduction or power-loss behavior. The separate measured rejections of MX-02/03/04 follow; remaining candidate work belongs to MX-05–09, and MX-10 must verify the retained combination across the requested complete workflows.

<a id="mx-02-precompiled-bridge-rejected"></a>

## MX-02: precompiled bridge rejected

**Disposition: completed, rejected and removed.** The prototype compiled the same owned bridge against Unity 6000.3.22f1 public reference assemblies, with an Editor-only importer, no implicit references and a prototype v3 binary/importer ownership marker. It removed one initial Csc and one ILPP action but did not avoid the remaining compilation pipeline or reload. No new product mode, ownership format, binary distribution or Unity-version support was retained.

The fixed-input order was **before / after / after / before**, two runs per variant. Both builds record commit `24956f4975a8c9ae57a21f4c52a656b9f8b51ea3`; baseline product source is `bea4acdb95f3ea2a914587c7f5e5293b57940e7d`. The candidate changed the Unity application assembly and its dependent workflow/CLI assemblies; other runtime/storage binaries were identical. Inputs were fixture `f121a5adc9c4248994774dea03843504c949f09a`, **1,607 source files / 42,101 Library files**, with full bytes/modification times/attributes checked before every run. The initial loaded runtime MVID was identical in all four runs: `a827452c-9e24-4558-855e-22035ade4dd1`.

One continuous stopwatch covered Library preparation, workspace creation, GUI readiness, one source edit/explicit refresh/loaded-code verification, asset inspection, normal stop, explicit discard, owned reclamation and Library restoration. Initial readiness is a stage interval, not the `unity-start` command's return. Stage counters overlap or leave controller gaps and are not summed to derive the total. Timing runs used **no Unity profiler or product write diagnostics**. Server observation and bounded Bee capture after normal stop remain inside the continuous total, with no subtraction.

| ABBA run | Initial ready (s) | One edit/compile (s) | Continuous total (s) |
| --- | ---: | ---: | ---: |
| 01-before | 102.91423130 | 26.19117240 | 307.97620330 |
| 02-after | 103.63832160 | 27.38300990 | 305.78099690 |
| 03-after | 102.69305180 | 30.39486990 | 298.89506030 |
| 04-before | 101.29306420 | 26.18818050 | 292.96616340 |
| **Before mean** | **102.10364775** | **26.18967645** | **300.47118335** |
| **After mean** | **103.16568670** | **28.88893990** | **302.33802860** |
| After minus before | +1.06203895 | +2.69926345 | **+1.86684525** |

The mean total increased **0.62130592%**. This small four-run observation establishes no material saving; it does not prove a universal regression. The predeclared acceptance target was at least **15s and 10%** lower mean initial readiness, lower whole lifecycle time, and no materially offsetting edit/exit/memory/compatibility cost. It was not met. Mean included server observation was **1.87500995→1.81646510s**; included post-stop Bee capture was **1.51918565→1.47541795s**. These costs are disclosed, not used to manufacture a corrected speedup.

Initial logs consistently show **Csc 1→0 / ILPP 69→68**. The remaining pipeline/reload and approximately 22s InitializeOnLoad segment still occur. These action counts belong to this fixture and must not be combined with MX-01 or historical BD-01 counts/times as one causal comparison.

The candidate passed **37 focused tests, 0 failures, 0 skipped**. Separate package-free ordinary-NTFS first/restart smoke checks verified exact identity, rejection of an incorrect stop token, Editor-only/no-implicit-reference import, normal exit code 0 and unchanged DLL/importer. Their readiness times were **11.604772499995306s / 4.023874499951489s**, and normal-stop times **0.8016131999902427s / 0.6014807999599725s**; these are correctness checks, not the GUI benchmark's performance result.

All four GUI runs completed normal exit code 0, explicit discard, exact source/Library bytes/modification times/attributes and source metadata restoration, and owned resource reclamation. No trial Editor remained. The original external server and captured preferences were restored exactly and health-checked; the external restart receipt exited 0. This remains agent-owned experiment cleanup, not product server management.

The changed product/source/test files were restored byte-exactly. The rejected patch and compiled binary/importer remain only under ignored local evidence; their hashes and the protocol/runtime/analysis receipts are in the tracked JSON. The unmeasured distribution and additional Unity-version qualification burden provides no reason to retain a candidate with no material measured gain. This closes MX-02 as rejected. It does not complete the remaining program, the final apply/discard combination, Play/Test/build, other Unity versions, peak-memory or power-loss qualification.

<a id="mx-03a-first-consume-retention-rejected"></a>

## MX-03A: first-consume retention rejected

**Disposition: measured, rejected and removed.** On the first successful use of a speculatively prefetched entry, the candidate kept its insertion age rather than promoting it to MRU. Successful publication marked consumption with diagnostics on or off; later hits kept normal MRU behavior. The baseline and candidate both retained **2 readers, 1 warm worker, a 256MiB/4,096-entry payload cache and a 128MiB first-proof window**, with unchanged full proofs, memory bounds and failure/cancellation drain. This was a policy experiment, not a rerun of the rejected capacity-only GR candidate. Only `Splinter.SourceControl.Git.dll` and its PDB differed in the frozen runtime comparison.

The same fixture base `f121a5adc9c4248994774dea03843504c949f09a` supplied **1,607 source / 42,101 Library files**. Each fresh editorless `workspace.ensure` had its own monotonic stopwatch. The common Library prepare/restore, each private edit/isolation check and explicit discard were timed separately. Full source/Library bytes, modification times, attributes and Git metadata checks before and after each run were outside timing. Both variants enabled G1 with a bounded **0.5s** sampler; memory figures are exact-process provider lifetime peaks, not whole-system peak RAM. The initial two-reader baseline remained product `bea4acdb` under build commit `24956f4`.

The first diagnostic attempt's observer compared physical CRLF helper files with normalized LF Git blobs. All **1,607 Git blobs** were independently verified; the four line-ending-only differences were preserved. This was an observer mismatch, not a product content failure. The exact diagnostic task was normally discarded and Library restored to ordinary NTFS; its **43.48075650003739s** recovery is excluded from performance results. A new fixed cohort, `mx03a-measured`, ran the same runtimes/source/Library in before/after/after/before order after recovery. The original failed check, difference record, amendment and closure remain preserved.

| Fresh editorless run | Ensure (s) | Explicit discard (s) | Provider peak working set (bytes) |
| --- | ---: | ---: | ---: |
| 01-before | 51.02398000 | 9.27977430 | 737,333,248 |
| 02-after | 50.74656150 | 8.77316820 | 741,326,848 |
| 03-after | 50.31551940 | 8.31865440 | 745,742,336 |
| 04-before | 51.55086200 | 8.26941030 | 741,146,624 |
| **Before mean** | **51.28742100** | **8.77459230** | **739,239,936** |
| **After mean** | **50.53104045** | **8.54591130** | **743,534,592** |

Mean ensure saved **0.75638055s / 1.47478765%**, below the predeclared **at least 10s AND 15%** reduction with correctness, bounded resources and no offsetting lifecycle/maintenance cost. Mean private isolation checks were **0.26199120→0.26230280s**. Common Library preparation was **39.84370919992216s** and final restoration **22.63769159989897s**; these single common operations are not per-variant lifecycle measurements and are not folded into the ensure saving. Provider lifetime peak private commit averaged **753,795,072→757,583,872 bytes**, also not a whole-system peak.

Proved-read re-decoded bytes fell **305,237,082→11,628,972 (96.19018373%)**, and unused read-ahead eviction fell **227,704,832→0 bytes**. Full-proof decoded bytes remained **2,787,059,626** per-variant mean. The attractive counter reduction did not establish a material elapsed benefit. It does not justify a 96% speedup claim, adding savings from nested counters, or retaining the candidate as an optional policy.

Focused tests passed **55**, failed **0**; **1 conditional cost test** (`RepeatedPackedBackwardReadCost`) was not executed. The full build gate was not run for this rejected scope. All four measured tasks were explicitly discarded, owned providers exited, task paths/private deltas were absent, and both before/after proofs confirmed full source/Library bytes/modification times/attributes and Git metadata. Final Library restoration matched the input. The diagnostic task's closure is separate from these four successful closures. No Unity, MCP/server, package or global-setting operation was performed by this editorless comparison.

At MX-03A closeout, its product source and dedicated test file were restored byte-exactly; the patch and raw evidence remain ignored. MX-03B is a separate measured candidate below, not an adopted extension of A. Two observations per variant on one Windows/NTFS machine with uncleared OS caches do not qualify GUI startup/compile, apply, Play/Test/build, full lifecycle or power-loss behavior.

<a id="mx-03b-reader-warm-concurrency-rejected"></a>

## MX-03B: reader/warm concurrency rejected

**Disposition: measured, rejected and removed. MX-03 is complete with both A and B rejected.** B used four bounded decoder sessions and up to three initial-large-file warm workers alongside mandatory index refresh. It retained the 256MiB/4,096-entry payload and 4MiB proof limits, reducing the per-reader proof read-ahead window from **128MiB to 64MiB**; the largest two blobs exceed that new window. A's retention policy was absent. Full hash/alias proofs, bounded resources and failure/cancellation drain remained required. This measures the candidate combination, not an isolated causal effect of reader count.

The first setup stopped before any native run or home creation because it incorrectly required `Platform.Windows` to remain byte-identical. The candidate's internal Git constructor/reference metadata change was followed by dependent rebuilds: **eight DLL/PDB files** across Git, Platform.Windows, Workflows.Windows and CLI changed; the remaining files in the **45-file** runtime inventory matched, with no added/removed files. The original frozen outputs and setup failure remain preserved. A follow-up comparison found equal method-name/raw-signature/raw-IL/MaxStack/LocalVariablesInitialized rows for **1,703 / 374 / 380 methods** in the three dependent DLLs. It did not compare resolved token targets, exception regions, local-variable signatures, all metadata/resources/PDB/native code, and does **not** prove whole-PE or behavioral equivalence. Dependency rebuild is consistent with the source/reference change, not a compiler trace proving every changed byte. This qualification was read-only and reused the completed comparison; it executed no target DLL or new native trial.

The fresh `mx03b-measured` cohort used the same **1,607 source / 42,101 Library files**, input proof, editorless timing boundaries and **10s AND 15%** mean-ensure acceptance threshold as A. Both variants enabled G1 and the 0.5s observer. No Unity, original server, package or global setting was operated. The A and B cohorts are separate comparisons with separate before runs; their means must not be combined into one gain.

| Fresh editorless run | Ensure (s) | Valid reader samples | Sampled maximum active readers | Samples with ≥3 readers |
| --- | ---: | ---: | ---: | ---: |
| 01-before | 60.68203910 | 33 | 2 | 0 |
| 02-after | 60.98883890 | 35 | 4 | 6 |
| 03-after | 61.23225740 | 35 | 4 | 7 |
| 04-before | 52.97522150 | 28 | 2 | 0 |

Mean ensure was **56.82863030→61.11054815s**, **4.28191785s / 7.53478982% slower**, so acceptance failed. Concurrent use of four readers is observed, but maxima are sampled, not absolute peaks: roughly **1s** provider publication and a **0.5s** observer can miss intervals. Process-start totals are not concurrency counts; zero ≥3 samples alone does not establish absence.

Warm mean shortened **22.69030445→9.29738000s**, while index refresh lengthened **26.72628210→32.84063510s**. Their mean overlap was **22.67312135→9.28021300s**. They run concurrently; subtracting the warm saving from ensure or adding those durations would double-count overlap. Mean re-decoded proved-read bytes increased **1,266,507,990.5→3,396,886,562**, and unused read-ahead eviction **988,282,880→2,523,725,824 bytes**; full-proof decoded bytes remained **2,787,059,626**. These show additional work, not a measured partition of the elapsed regression.

Mean provider lifetime peak working set was **736,632,832→748,523,520 bytes**, and private commit **750,837,760→762,107,904 bytes**; neither is whole-system peak RAM. Mean separate discard was **9.43170930→8.92076205s**. Common Library preparation **42.31323570001405s** and restoration **22.951573599944822s** are separate, not per-variant whole-lifecycle results.

The candidate build reported **0 warnings**, and focused tests passed **80**, failed **0**, with **1 conditional cost skip**. No full build gate was run for this rejected scope. All four pre/post input proofs preserved exact source/Library bytes/modification times/attributes and Git metadata. Every task was explicitly discarded, providers exited, task/private-delta paths were absent and final Library restoration matched. All **seven source/test files** were restored byte-exactly at B closeout; the rejected patch and evidence remain ignored. No reader mode or candidate policy was retained. These two observations per variant with uncleared OS caches provide no actual Unity/full-lifecycle/apply/Play/Test/build/power-loss qualification.

<a id="mx-04-nonfinal-close-rejected"></a>

## MX-04: non-final Close separation rejected

**Disposition: completed, rejected and removed.** The candidate decremented an already non-final context under a small context lock without acquiring the global mutation gate. A possible final Close acquired the mutation gate and rechecked the context before final release. Atomic Open/stat/retain, rename/delete/replace, durable forget, cleanup purge and reader lifetime protections remained. Simply removing Open's outer gate without an atomic lookup/retain contract would leave a deletion/replacement race. Runtime Stat still takes the journal gate and performs its full check; the current Library check uses a pinned handle and does not hash file contents. This row does not establish another removable full-file proof or the entire MX-01 File.Open interval as a saving.

The baseline retained the original all-Close mutation gate and added the same opt-in Close/Stat observation as the candidate. The archived common-instrumentation manifest proves that removing those wrappers reproduces the original source; its “not built” note describes artifact generation, before the parent's subsequent build. Each variant used the **same test DLL**, SHA256 `b44eaf844f40c69a3c6047937c4b9b0b2f395b3742da5daf3104f3117ba4c536`. The runtime inventory has **129 files**; **six DLL/PDB files** differ across Platform.Windows and its dependent Workflows.Windows/CLI assemblies, with no added/removed files. Only Platform product source changed for the candidate and common diagnostics; other product sources did not. This is source/runtime provenance, **not whole-PE or behavior equivalence**. The tracked JSON records exact hashes and evidence paths.

Native cases used **WinFsp MountEx with 8 dispatcher threads**, a deterministic immutable eight-file memory basis, a real bounded **16MiB** journal, **4KiB** files, and **1,000 open/read/close operations per reader**. Readers used distinct files except the same-ID anchor/transient pair. The mixed case added **512 synchronous 512-byte WriteThrough writes** and one final `Flush(true)` without artificial delays. Anchor/writer opens and the ready barrier were preparation; worker activity, normal closes and context drain were timed. Post-workload flush, normal unmount, replay and setup were recorded separately. One stopwatch measured each complete workload; the table sums five **sequential** workload stopwatches, not concurrent callbacks or the full product lifecycle.

| Native ABBA run | Five-workload sum (s) | External process elapsed (s) |
| --- | ---: | ---: |
| 01-before | 5.87151450 | 6.89947690 |
| 02-after | 5.56908080 | 6.63975800 |
| 03-after | 5.42757730 | 6.41605430 |
| 04-before | 5.37714620 | 6.37993600 |

| Native workload | Before mean (s) | After mean (s) | Saved (s) |
| --- | ---: | ---: | ---: |
| Sequential single handle | 0.86534285 | 0.87995220 | -0.01460935 |
| Same-ID anchor and transient | 0.23160400 | 0.20495600 | 0.02664800 |
| Two concurrent readers | 0.89727235 | 0.88197580 | 0.01529655 |
| Eight concurrent readers | 1.28414960 | 1.23754220 | 0.04660740 |
| Eight readers and durable writer | 2.34596155 | 2.29390285 | 0.05205870 |
| **Sequential sum** | **5.62433035** | **5.49832905** | **0.12600130** |

The mean saving was **2.24028982%**, below the predeclared **10 seconds AND 15%** component screen, which would only permit a later matched real Unity trial. No such trial was run. The mixed case saved **0.05205870s / 2.21907729%**. The anchor case had **1,000 actual non-final closes in every run**, so eligibility was demonstrated, but its saving was only **26.648ms**. Candidate mixed runs each classified **8,001 closes**: only **4** took the fast non-final path; **7,961 / 7,980** were final and **36 / 17** were non-final only after waiting and rechecking. Multiple native handles therefore cannot be assumed to provide a large eligible fraction.

A separate direct-callback ABBA used **4,096 total reader iterations** per case and, when enabled, **256 synchronous 1KiB peer writes** to a real journal. These are additional component observations, not seconds to add to the native result.

| Callback readers / anchor / writer | Before mean (s) | After mean (s) | Saved (s) |
| --- | ---: | ---: | ---: |
| 1 / no / no | 0.05387970 | 0.05785250 | -0.00397280 |
| 1 / yes / no | 0.02708575 | 0.02770980 | -0.00062405 |
| 2 / yes / no | 0.03092805 | 0.03042445 | 0.00050360 |
| 8 / yes / no | 0.02922030 | 0.03208575 | -0.00286545 |
| 1 / no / yes | 0.59291610 | 0.58359345 | 0.00932265 |
| 1 / yes / yes | 0.56843345 | 0.56915665 | -0.00072320 |
| 2 / yes / yes | 0.57074180 | 0.56483050 | 0.00591130 |
| 8 / yes / yes | 0.57562165 | 0.57099385 | 0.00462780 |
| **Sequential sum** | **2.44882680** | **2.43664695** | **0.01217985** |

Callback aggregate saving was **0.49737491%**. Close classification elapsed, Open/Security Stat child elapsed and gate waits overlap across scopes and threads; none is additive removable time. The baseline label `close-rechecked-nonfinal` means its original gate-held non-final branch, not an extra baseline recheck. Compare it with both candidate non-final categories. The `close` mutation bucket counts gate-taking Close calls, not all candidate closes; class totals supply the complete count. Native mixed observations still show final-Close gate waiting and substantial Security Stat elapsed. Stat elapsed does not separate internal journal-lock wait, proof execution, kernel or scheduler effects, so it cannot by itself justify gate removal or causal time attribution.

Both builds reported **0 warnings / 0 errors**. Focused tests passed **84**, failed **0**, with **3 conditional cost skips**, in **15.552s**. The skips were the direct Close cost, native Close cost and existing telemetry cost entrypoints; the first two were then explicitly exercised by the separate ABBA runs. All **20 native cases** drained workers, closed/unmounted normally, reached **zero handles and retained contexts**, and replayed all **nine expected file hashes** exactly across variants for the same case. Private journal identities and timestamps naturally differ; this is content/lifetime verification, not byte-identical journal files or power-loss testing.

The three modified existing source/test files were restored byte-exactly and four candidate-only test files removed. No candidate lock, diagnostic hook or optional mode remains. Rejected source/patch, test definitions and raw runs remain ignored evidence. At this row's closeout, existing `bin/Release` output still contained the rejected candidate and required a rebuild before use; source restoration alone does not refresh binaries. No original project, Unity Editor, external server, package or global setting was operated for MX-04. Two observations per variant on one machine with uncleared OS caches do not establish a general Unity saving. **MX-04 is closed as rejected.**

<a id="mx-05a-fixed-topology-restore-provisional"></a>

## MX-05A: earlier fixed-topology root-restoration candidate

**Historical candidate: material component benefit led to provisional retention; the separate final-candidate adoption follows below.** A transfers the intact Library root once, preserves stable files in place, stages exact deleted leaves, and applies same-path private ranges, truncation, extension and new leaves. Directory rename/delete/new-directory support belongs to B; its bulk and leaf observations are recorded separately below. The identified control-receipt publication interruption is fixed: a plain regular `.GUID.tmp` with an exact canonical control-file stem is ignored and preserved, never adopted as authority or deleted; unknown stems, malformed names, directories and reparses still refuse recovery. Focused cancellation/resume tests passed on the subsequent source. New-leaf creation before its identity receipt remains a separate preserve/refuse boundary. The separate final-candidate comparison and full-gate completion are recorded below. The held stable-proof reuse idea is recorded as an unimplemented proposal, not an additional measured gain.

The sequential ordinary NTFS comparison used **10,000 × 1,024-byte files** (**10,240,000 input bytes**) with fixed metadata, **four 1KiB overwrites** (**4KiB total**), a truncation to **17 bytes**, an extension to **2,049 bytes** with a three-byte tail write, one new sparse **2,049-byte** leaf and one deleted leaf. The seven resulting mutable/new files receive durable range completion; no directory topology change is in this input. A real bounded journal backs the fixture. The complete `ManagedDirectoryRestore` call includes plan publication, content/identity proofs, metadata, range flush, final proof and reclamation. Fixture construction and independent final assertions are outside its one stopwatch; external process elapsed is not the same interval.

| Ordinary NTFS ABBA run | Complete restore (s) | Cumulative managed allocation (bytes) | Root moves | Surviving-file moves |
| --- | ---: | ---: | ---: | ---: |
| 01-before | 60.5269537 | 312,879,848 | 0 | 9,999 |
| 02-after | 21.1528534 | 340,946,856 | 1 | 0 |
| 03-after | 39.3249137 | 358,742,640 | 1 | 0 |
| 04-before | 99.1948066 | 312,902,408 | 0 | 9,999 |

Mean complete restore was **79.86088015→30.23888355s**, saving **49.62199660s / 62.13554935%**. This passes the predeclared elapsed screen of **10 seconds AND 15%**, subject to correctness/recovery and resource review. Both variants show substantial run-to-run variation, with only two observations per variant and OS caches not cleared. Mean cumulative managed allocation rose **312,891,128→349,844,748 bytes**. That is allocation during the restore interval, not live memory, peak process/system RAM, or a CPU measurement. The result is not a general speed multiplier or a Unity lifecycle reduction.

All four runs used the same test DLL, SHA256 `47f5321c6738bb3b0c3e9760b2493962d3dbb90b975d354dd613cedf6be79e5f`. Frozen runtime inventories contain **129 files** each; **six DLL/PDB files** differ across Platform.Windows and dependent Workflows.Windows/CLI, with no added/removed files. Runtime, Git and Unity adapter DLLs match. The archived source snapshot and runtime hashes describe the measured A version before the subsequent publication-gap fix and B implementation; they do not qualify those later changes or prove whole-PE equivalence.

The full logical input hash is `56e86c20804ff26d4439596813b380eeacbde19712e23f93c2169f7235bc23fc`; the expected result hash is `c78f0fdf65744a8f3d5db8d15e988c7449477c85fc7f1cb7de799b53d58bc66d`. Each completed run independently compared every final file's bytes/length, attributes and creation/access/write times, exact tree membership, surviving-file NTFS identities, and absence of the old basis, private delta and deleted-leaf stages. Product initial payload copied bytes remained zero. Candidate runs additionally observed original root/directory identities preserved, while baseline runs created new directories; directory-identity equality is therefore not a baseline requirement. All four cost tests passed and permitted their owned fixtures to be cleaned up.

The pre-fix focused run passed **228**, failed **0**, with **2 conditional cost skips**. An initial invalid filter selected no tests; it supplied no validation and was corrected before the recorded focused run. The skips were the sparse-restore and root-delta-restore opt-in cost tests; only the latter was subsequently exercised in this ABBA. The recorded focused run predates the newly identified publication-gap fix. A was provisional after publication-gap repair and focused revalidation; final-candidate adoption follows the separate result below. No original user project, mounted provider, Unity Editor, external server, package or global setting was operated for this component trial. Power-loss behavior, final combined performance and complete apply/discard workflows remain unqualified.

Raw protocol, runtime/source inventories, four results/TRX files, closure and analysis remain ignored under `artifacts/remaining-optimization-20261028/mx05a-cost/` and its parent. The [tracked report](../../reports/benchmarks/remaining-lifecycle-optimization-20261028.json) records exact hashes, the earlier provisional decision and the separate final adoption; existing raw observations are preserved.

<a id="mx-05-safety-progress"></a>

## MX-05 historical safety progress before final comparison

The A publication-temporary fix is implemented and included in the completed focused rerun. An unpublished plain `.GUID.tmp` is accepted only as an ignored, preserved file with an exact canonical control-file stem; its contents never become restore authority and it is not reclaimed by name. Cancellation after a completion write preserves that temporary and same-intent resume with fresh cancellation succeeds for the tested journal modes. Unknown stems, malformed GUIDs, directories and reparse entries remain preserved/refused. The earlier A ABBA still describes its archived pre-fix candidate; these safety results do not retroactively retime it.

| Separate diagnostic/validation run | Source phase | Passed | Failed | Conditional cost skips | Runner elapsed (s) |
| --- | --- | ---: | ---: | ---: | ---: |
| Initial focused | B before sharing/fixture corrections | 277 | 68 | 3 | 211.296 |
| Narrow2 | B root sharing and native-parent hardening | 21 | 0 | 0 | 21.399 |
| Focused2 | A temporary fix and B hardening; before new-leaf helper | 358 | 0 | 3 | 277.072 |
| Newleaf3 | Subsequent A/B exact-parent new-file helper | 18 | 0 | 0 | 24.175 |

These selections overlap and use different sources; do not add them into a unique passing-test count. The **361-test** focused2 selection is 358 passes and three opt-in cost skips. The reinforced Release build took **51.39s**, and the new-leaf Release build **45.42s**, each with **0 warnings / 0 errors**. Neither build duration is a performance saving or proof of the required full gate, whose later result is recorded below. The initial failure runtime (`runtime-mx05b-failed1`), TRX/logs and source snapshots remain preserved. Its 68 failures group by message into **14** stopped-reader-contract errors, **53** owned-file sharing errors and **1** obsolete fresh-route expectation; this is a message classification, not 68 independently established root causes. Old general-route tests explicitly seed a current-v2 plan from a stopped read-only journal so fresh B routing cannot bypass general recovery/cursor coverage. The corrections and failed diagnostics are excluded from the clean cost cohort.

Narrow2's native messages establish the following, rather than assuming all reparse changes are prevented by sharing:

| Injection point and access | Cases | Actual result | Observed protection |
| --- | ---: | --- | --- |
| Empty final root; GENERIC_WRITE `0x40000000` | 3 | Win32 **32**, sharing violation | Injection blocked; ordinary restore completes |
| Empty final root; FILE_WRITE_ATTRIBUTES `0x100` | 5 | **0**, junction conversion succeeds | Product refuses; outside tree and original files/stages/journal preserved |
| Immediately before parent-relative topology install/create; either access above | 4 | **0**, junction conversion succeeds | Product refuses; outside tree and exact plan/intent/cursor preserved |
| Newleaf3: A existing empty parent and B receipted empty parent; FILE_WRITE_ATTRIBUTES `0x100` | 2 | **0**, junction conversion succeeds | Exact-parent new-file creation does not redirect into outside tree; no file/write/verified receipt is posted |

`PinDirectoryAgainstDataWrites` now describes data-write/GENERIC_WRITE exclusion; it does **not** claim to block attributes-only conversion. Topology rename and directory creation use the exact retained parent handle. A/B new files now use `CreateOwnedFile`: one leaf relative to that parent, `FILE_CREATE` with no replacement, share **0**, type constraints and `FILE_CREATED` confirmation. Parent type/identity/path are checked before and after the native call; the returned same file handle becomes the stream. Successful creation still executes `Flush(true)` before its identity receipt. A failure closes the handle without deleting a newly created unreceipted entry; retry does not adopt that entry. Occupied-target tests preserve the foreign bytes and identity. The new hooks inject after the parent check immediately before native creation, closing the specific absolute `RequireAbsent`→`CreateNew` gap in the earlier source.

The outside-tree comparisons cover exact membership, kinds, attributes, creation/write times, NTFS identities and file hashes, not last-access times. The native topology injection cases preserve exact plan/intent/cursor bytes; the final-root cases prove original file identities/length/content and stage/journal presence without asserting every control byte or whole-journal hash. These are tested deterministic boundaries, not proof of all hostile interleavings. Unreceipted nodes remain preserved/refused, and the tests do not promise successful automatic recovery after an unknown node is created. There is no new power-loss or Unity qualification.

The ignored `mx05b-tested2-source.json` identifies **18 archived files** for focused2. The later `mx05b-cost/sources.json` identifies **19 archived files**, including the new-leaf helper/tests and the leaf-cost fixture. Both initial attempted cost runtime manifests contain **129 files**; they share test DLL SHA256 `622a6d35145912c88d51c9eabc34dc6d621d94ddee8aa9cc634ef81eff75e05f`, and six Platform.Windows/Workflows.Windows/CLI DLL/PDB files differ. Exact manifest/source hashes and selected runtime hashes are recorded in the [tracked report](../../reports/benchmarks/remaining-lifecycle-optimization-20261028.json). Archive source hashes and the frozen test/Platform DLLs were checked without rescanning the large active cost payload. This is not whole-PE equivalence or a claim that the earlier focused source equals the later candidate.

The first bulk cost attempt (`mx05b-cost/01-before`) failed in fixture construction with `MissingMethodException`: the test identity helper referenced the new five-argument `DirectoryHandle` signature absent from the frozen older baseline. **Restore was never entered; no candidate was run and no comparison time exists.** Its stdout/stderr, TRX and external process-cost record remain preserved as setup diagnostics. The common test fixture was corrected to use the same native identity measurement against both binaries; the separate frozen cost2 cohort below completed normally. Do not classify this as a product restore failure, count its process duration as restore time, or mix it into the normal ABBA. The runtime hashes above describe that first attempted cohort and are preserved rather than overwritten.

This safety stage alone does not declare final support. AGENTS, implementation contracts and lifecycle maps were subsequently reconciled with the final adoption.

<a id="mx-05b1-bulk-topology-screen"></a>

## MX-05B1: normal bulk comparison passes the elapsed screen

The separate `mx05b-cost2` cohort uses the corrected common native identity fixture and the same frozen product DLLs as the failed setup attempt, with a new shared test assembly. All four product restores completed. The old `mx05b-cost/01-before` setup failure is preserved and excluded; its process duration is not a restore observation. The common-fixture Release build recorded **21.78s**, **0 warnings / 0 errors**, not a full-gate result.

The fixed ordinary NTFS input contains **10,000 × 1,024-byte files**, **10,240,000 bytes**, with an initially **9,993-file subtree** (9,991 direct plus two nested leaves). Nested and independent exceptions are extracted before transport; the retained main unit contains **9,989 files**. The workload also reparents directories, moves/deletes individual leaves and a subtree, creates nested directories/files, and performs overwrite/truncate/extend with fixed metadata. One stopwatch covers the complete product restore, including plan/proofs, durable range writes, final verification and reclamation. Fixture construction and independent final assertions are excluded; external process duration is separate.

| Normal B1 ABBA run | Complete restore (s) | Cumulative managed allocation (bytes) | Root moves | Unit stage/install moves | Surviving-file moves |
| --- | ---: | ---: | ---: | ---: | ---: |
| 01-before | 82.4396773 | 312,928,224 | 0 | 0 / 0 | 9,997 |
| 02-after | 73.0181388 | 496,938,616 | 1 | 9 / 6 | 0 |
| 03-after | 68.5058357 | 496,930,456 | 1 | 9 / 6 | 0 |
| 04-before | 88.6892374 | 312,917,440 | 0 | 0 / 0 | 9,997 |

Mean complete restore was **85.56445735→70.76198725s**, saving **14.80247010s / 17.29978844%**, above the predeclared **10 seconds AND 15%** elapsed screen. Mean cumulative allocation rose **312,922,832→496,934,536 bytes**, **184,011,704 bytes more**. This is cumulative managed allocation within the restore interval, not peak RAM, live retained memory or CPU usage. Stage counts explain which route ran; they are not durations, additive savings or independent causal measurements. B1 posted two new-directory identity receipts per run; final proof and exact reclamation remained inside the timed call.

All four post-restore checks matched final file bytes/lengths, attributes and creation/access/write times, exact membership/spelling and surviving file identities. Basis, delta and stages were absent; initial payload-copy count remained zero. Candidate runs additionally preserved surviving directory identities, while the general baseline created new directories. The common logical input hash is `d20337772adb801b36dbdd52956ca24caaf341587a72f05d3bdf14beb4fd6b5c`; expected final result hash is `3d3c71d0404bc5d25ce35eb4c8d61cf3759a67ce25611e2797d8ab0707b7f631`. Each result/TRX passed before fixture cleanup was permitted.

Both frozen runtime inventories contain **129 files**, share test DLL SHA256 `5f48c958907790828c211ded8fe85cda491a6f3a3aa4db4e6b086dbb3eeaa948`, and differ in six Platform.Windows/Workflows.Windows/CLI DLL/PDB files. The **19-file** candidate source archive and four result/runtime identities are fixed separately from the earlier 18-file focused source. The [tracked report](../../reports/benchmarks/remaining-lifecycle-optimization-20261028.json) preserves the canonical analysis, protocol, runtime/source manifests, all four result/TRX hashes and closure. These records do not assert whole-PE equivalence or retime the earlier A candidate.

**This is B1's material component screen, not final adoption.** Two observations per variant, one machine and uncleared OS caches limit generalization. The input combines a large shared subtree with exceptions and does not establish the cost of independently renamed leaves; the completed separate ABBA below rejects unconditional topology routing on the same frozen B1 runtime. B2 targets repeated ancestor checks in inventory construction and shared forest proof with up to eight full-hash workers; at this historical B1 stage the earlier prototype had passed a build and narrow safety run. The strengthened guard and its final performance were validated later, as recorded below. B2 must retain exact membership/identity/content proof, bounded workers and failure drain. No original user project, Editor, external server, package or global setting was operated for this cost cohort; no mounted Unity, peak-memory/CPU, all-topology, power-loss or final combined improvement is claimed.

<a id="mx-05b1-leaf-regression-and-guard"></a>

## MX-05B1 leaf regression: unconditional topology routing rejected

A separate fixed **1,024 × 1,024-byte** ordinary NTFS input (**1,048,576 bytes**) independently renames every surviving file within its existing parent. It uses the exact frozen B1 bulk-cohort test/product runtimes, with no concurrent build/trial. One stopwatch measures complete restore through final proof/reclamation; setup and independent assertions remain outside. It is not another bulk sample or a Unity timing.

| Independent-leaf ABBA | Complete restore (s) | Cumulative managed allocation (bytes) | Unit stage/install moves | General file moves |
| --- | ---: | ---: | ---: | ---: |
| 01-before | 8.2841113 | 37,304,136 | 0 / 0 | 1,024 |
| 02-after | 47.7502525 | 145,578,168 | 1,024 / 1,024 | 0 |
| 03-after | 40.1972167 | 145,639,496 | 1,024 / 1,024 | 0 |
| 04-before | 8.8615848 | 37,296,312 | 0 / 0 | 1,024 |

Mean restore increased **8.57284805→43.97373460s**, **35.40088655s / 412.94195749% slower**. Mean cumulative managed allocation increased **37,300,224→145,608,832 bytes** (**108,308,608 bytes more**, not peak memory). Candidate runs used one root move plus 1,024 stage and 1,024 install moves, each with its topology cursor publication; baseline runs moved 1,024 files and recorded eight batched general cursor boundaries. These counts support the structural explanation, but they are not separately timed causal contributions and are not added to elapsed time.

All four result/TRX files passed exact final content, metadata, membership and surviving-file identity checks, with basis/delta/stages reclaimed before successful fixture cleanup. Logical input hash: `62fe2c29b5945703d52c0f08609bc2755db6d3b9fa6f0327d0e9ab77e2568521`; expected result hash: `347d6edbd2d53e380c1fb500e3f4611fe26662f9b11ca756bbaa618add236e52`. The runtime manifest matches cost2, SHA256 `560389d6ca8cbc02c58410eb0b5a18518ff6f022ccec4f81a2c62997eaa869da`. This normal regression is distinct from the earlier fixture-binding failure and the successful bulk cohort. Two observations per variant, warm caches, cumulative-not-peak allocation and no Unity/power-loss qualification remain explicit.

**Unconditional B1 topology selection is rejected.** The first B2 follow-up added `PreferFresh` only at the initial route; its move-only rule is superseded by the validated publication bound below: topology must use fewer preserving moves than general restore, with `1 + 2 × live non-root units + deleted non-root units < surviving source-backed files`. Equality or more selects general. The leaf input is rejected by this floor (**2,049 versus 1,024**); the bulk input remains eligible (**16 versus 9,997**). This is a source-derived routing prediction, not a measured guarded result or total-cost guarantee. Already-published topology plans dispatch to `ResumeAsync` before this preference; `Eligible`, plan validation and same-intent recovery do not impose the new cost rule.

The scoped read-only B2 review found no confirmed blocking defect in the inspected changes: inventory syntax/path projection is still followed by actual type/path/identity/full-content proof; original and receipt forests retain separate proof roots; receipt-root share-write permits control publication while pinned controls keep that root nonempty; deleted directories retain DELETE access; the original root is borrowed rather than closed. Directory maps freeze before at most eight full-hash workers, per-index handles join before failure cleanup, and final membership/TreePins remain. This is source review, not an executed test. Attributes-only reparse acceptance and exact-parent/current-type protection remain relevant. The eight 64KiB hash buffers do not bound all plan/proof/handle memory.

The earlier B2 build and narrow safety run are recorded below; the strengthened guard completed focused tests, the full gate and the separate matched fixed/bulk/leaf comparison below. The original B1 results are preserved rather than overwritten by final results. A and guarded B2 are adopted; the real matched Unity apply/discard comparison remains MX-10. MX-05 is complete. MX-06 is complete as not adopted; MX-07A and MX-07B were measured, rejected and removed; MX-07 is complete. MX-08 is in progress, MX-09 is pending and MX-10 remains the final combined verification. The whole program is active. The [tracked report](../../reports/benchmarks/remaining-lifecycle-optimization-20261028.json) records the four raw result hashes, closure, sixteen B1 component limits and the B2 review source hashes.


<a id="mx-05b2-validation-progress"></a>

## MX-05B2: earlier safety passes and stronger publication-guard validation

The first B2 build failed with **2 xUnit1051 analyzer errors**, **0 warnings**, in **28.36s**; it ran no tests and is preserved separately in `mx05b2-build.log`. After the test cancellation-token correction, `mx05b2-build2.log` records a successful Release build with **0 warnings/errors in 19.64s**. `mx05b2-narrow.trx` and its log record **14 passed, 0 failed, 0 skipped in 5.034s**. They cover the earlier move-only fresh preference, existing-intent resume, rejected-input general routing, and worker failure/cancellation join. They are source-specific, overlap other suites and must not be added to a unique test count or used to qualify a later guard revision.

The first required full gate completed on the **older move-only-guard runtime** with **1,958 total, 1,924 passed, 1 failed and 33 skipped**. `mx05-full-gate1.trx` and `mx05-final-build-gate.log` report **25m 31.649s** of test execution; that is not an isolated restore benchmark or the whole build-command duration. The sole failure is `SparseDirectoryRestoreTests.ContentAndTopologyChangesSelectTheirOwnRootRestorePlans(change: "remove-directory")`: the old route expectation conflicts with general selection for **F=0, T=2**. The failed execution and `runtime-mx05b2-gate1/` plus its hash manifest are preserved. This required gate did **not** pass; the separate successful final-candidate rerun follows.

The subsequent publication-guard/test build passed with **0 warnings/errors in 48.38s**, recorded in `mx05b2-publication-build.log`. Focused RootTopology/selection/forest/Sparse/Unchanged tests completed with **234 passed, 0 failed, 0 skipped in 107.228s**, recorded in `mx05b2-publication-focused.trx` and its log. These source-specific tests overlap earlier suites and are not summed into a unique total. The second full gate passed: **1,974 total, 1,941 passed, 0 failed and 33 skipped**, with **10m 34.813s** of test execution in `mx05-full-gate2.trx` and `mx05-final-build-gate2.log`. This is correctness evidence, not restoration performance; selections overlap earlier suites and are not additive. Earlier 14-test results remain a separate revision.

A further source review found an unmeasured weakness in the first move-only guard: **499** independent leaf renames among **1,000** surviving source files yield **T=999 < F=1000**, but **998** topology operation cursors versus **8** general completion batches. This is a structural cost risk, not a measured slowdown for that input. The proposed strengthened fresh-only preference is:

`T < F && O + N <= G`, with `G = ceil(F / 128)`.

Here `F` counts surviving source-backed files, `T` counts preserving moves including the root, `O` counts topology operations and `N` counts new-directory receipts. The bulk input has **F=9997, T=16, O=17, N=2, G=79** and passes the formula; this arithmetic does not establish elapsed benefit. The first guard's previously recorded successful checks remain historical and do not validate the strengthened rule. Existing topology intents keep their original recovery protocol; the revision changes fresh routing alone. The strengthened rule passed the build, focused tests, second full gate and separate matched fixed/bulk/leaf comparison below. A and guarded B2 are adopted, MX-05 is complete. MX-06 is complete as not adopted; MX-07A and MX-07B were measured, rejected and removed; MX-07 is complete. MX-08 is in progress, MX-09 is pending and MX-10 remains the final combined verification. The whole program is active.


A read-only host observation after the first full gate and before final timing sampled an unrelated background workload for **5.0606903s**: **33.109375 CPU-seconds** on **12 logical processors**, approximately **54.5%** of aggregate CPU capacity; free RAM was **35,249,604KiB** and free C: space **161,543,368,704 bytes**. This observation is recorded in `mx05-host-load.json`, not causal attribution for test duration, a performance comparison or a peak resource measure. The user explicitly requested timing after stopping the unrelated background workload. The controller subsequently confirmed the unrelated background workload was absent and completed the frozen fixed/bulk/leaf ABBA; no result is inferred from this host sample. Acceptance remains the predeclared material elapsed criterion, exact safety/closure, acceptable resource costs and a successful final-candidate full gate. Old A/B1 times are unchanged; no B2 benefit or Unity saving is inferred from the host sample. Adoption follows the separate final restore results.


The final-candidate timing comparison closes separately below. The first and second gate durations are not compared as product speedups; intermediate safety/resource observations are not added to the final restore stopwatch.


<a id="mx-05-final-adoption"></a>

## MX-05 final: A and guarded B2 adopted

**MX-05 is complete.** The final A root-delta route and B2 topology route with the publication-count fresh guard are retained. Fixed and bulk pass the predeclared **at least 10s AND 15% mean savings** criterion with exact correctness, a successful full gate and acceptable resource costs for these inputs. Leaf uses general restoration and shows no material elapsed change. Unconditional B1 remains rejected. **MX-06 is complete as not adopted; MX-07A and MX-07B were measured, rejected and removed; MX-07 is complete. MX-08 is in progress, MX-09 is pending and MX-10 remains the final combined verification. The whole program is active.**

The final source is the ten uncommitted product changes over base `24956f4975a8c9ae57a21f4c52a656b9f8b51ea3`; the [map JSON](../lifecycle-optimization-map.json) fixes their exact hashes. `mx05-final-cost/` freezes **22 source/test files** and **129 runtime files per variant**, with **6 Platform/Workflows/CLI DLL/PDB differences**. The baseline is the frozen `24956f4 / bea4acd` product. Both variants use test DLL SHA256 `87d6e6dac8405066ee5492ea7543a29cf38f430f595dc3bf2a61358395d64b4b`; Platform SHA256 is before `e28ee738556b59b8ad1a61e91d8f2e7753c9fd61100ac9fe0b258ebdbc9c4990`, after `5acf5c4a2e5c30c56e7a7cb607d5052aee8fee467e221ff3f748e0b27f9f6315`. These inventories establish the compared candidates, not whole-PE equivalence or equivalence to earlier A/B1 source snapshots.

A single external controller ran each fixed input in serial before → after → after → before order, after confirming the unrelated background workload was absent and without competing builds/trials. The stopwatch covers the **complete ordinary-NTFS product restore call**, including proofs, writes/flushes, final verification and owned reclamation. Fixture setup and independent final assertions are outside it. Fixed and bulk each contain **10,000 files × 1,024 bytes (10,240,000 bytes)**; leaf contains **1,024 × 1,024 bytes (1,048,576 bytes)**. Their exact logical input/result hashes match the separate fixed, bulk and leaf records above.

| Final input | Before 1 (s) | After 1 (s) | After 2 (s) | Before 2 (s) |
| --- | ---: | ---: | ---: | ---: |
| Fixed topology | 45.5749939 | 14.0881750 | 15.3804282 | 46.5311454 |
| Bulk topology | 46.0219583 | 14.8832109 | 14.5989551 | 44.7094776 |
| Independent leaves | 4.9901118 | 4.9923640 | 5.0194015 | 5.0507841 |

| Final input | Before mean (s) | After mean (s) | Saved (s) | Saved (%) | Disposition |
| --- | ---: | ---: | ---: | ---: | --- |
| Fixed topology | 46.05306965 | 14.73430160 | 31.31876805 | 68.00582087 | A adopted |
| Bulk topology | 45.36571795 | 14.74108300 | 30.62463495 | 67.50611769 | Guarded B2 adopted |
| Independent leaves | 5.02044795 | 5.00588275 | 0.01456520 | 0.29011754 | General route retained; effectively unchanged |

All **12 runs** exited normally; each per-run TRX has **one passed test, zero failed/skipped**, `verified=true`, matching input/result hashes and the fixture's independent final content/metadata/membership/surviving-file identity checks. Basis/delta/stage reclamation completed normally. Fixed/bulk candidates preserve directory identities; general baseline reconstruction creates new directories. Both leaf variants perform **1,024 general file moves and 8 completion batches**, with **zero root/unit moves**. The second full gate separately passed **1,941 / 0 failed / 33 skipped, 1,974 total**, in **10m 34.813s** of test execution; its counts overlap earlier suites and are not summed with the 234 focused passes or the cost fixtures.

| Final input | Cumulative managed allocation before → after (bytes) | Increase (bytes) |
| --- | ---: | ---: |
| Fixed topology | 312,864,028 → 358,422,964 | 45,558,936 |
| Bulk topology | 312,954,588 → 453,336,828 | 140,382,240 |
| Independent leaves | 37,297,300 → 39,405,908 | 2,108,608 |

| Final input | Mean observed OS-reported peak working set before → after (bytes) | Mean sampled peak Private before → after (bytes) | Whole-process CPU before → after (s) |
| --- | ---: | ---: | ---: |
| Fixed topology | 163,299,328 → 190,105,600 | 123,709,440 → 151,457,792 | 75.7500000 → 51.9609375 |
| Bulk topology | 163,004,416 → 203,534,336 | 122,279,936 → 164,251,648 | 74.5546875 → 54.5546875 |
| Independent leaves | 104,947,712 → 103,886,848 | 56,219,648 → 54,740,992 | 10.7500000 → 10.5625000 |

Cumulative allocation is **not peak RAM**. OS-reported working-set peaks and Private samples are observed for the whole test process at about **250ms**; samples may miss peaks, and process CPU/memory includes fixture setup and independent assertions rather than only restore. Fixed/bulk mean working-set increases of **26,806,272 bytes (~26.8MB)** and **40,529,920 bytes (~40.5MB)**, with lower observed process CPU means, were judged acceptable for their **31.31876805s / 30.62463495s** restore savings on these inputs. This is a bounded engineering tradeoff, not a universal resource cap or isolated proof of CPU attribution.

The retained fresh rule `T < F && O + N <= ceil(F / ManagedDirectoryRestore.CursorBatchSize)` shares the general execution constant **128** and restricts move/publication amplification. It does not guarantee improvement for every eligible input. Existing intent protocols, full identity/content/membership proofs, durable acknowledgements, failure preservation and exact reclamation remain. Whole-project/append SJR2 retain general restoration; the new routes are original Unity Library SJL4/bounded SJR2 only. [Library contract](../git-library-reference.md) and [F10 map](../lifecycle-optimization-map.md#f10) record those boundaries.

The [tracked report](../../reports/benchmarks/remaining-lifecycle-optimization-20261028.json) includes final per-run data, resource means and hashes from `mx05-final-summary.json`, its script, protocol/source/runtime inventories, all 12 result/TRX/process-cost files and all three analyses. Prior A/B1 values, first-gate/analyzer/setup failures and safety corrections remain historical and are not overwritten or compared as isolated causal B2 gains. Two observations per variant, one machine and uncleared OS caches limit inference. **No actual mounted Unity whole-lifecycle/apply/discard or power-loss qualification is added; README RR-01 times remain unchanged.** Do not add these component savings into a predicted whole-Unity percentage.


<a id="mx-06-current-product-cost"></a>

## MX-06: current-product checkpoint attribution

**MX-06 is complete as not adopted.** Eight current-product cost trials and separate standalone cohorts are preserved below; the [final decision](#mx-06-final-disposition) rejects product integration in this scope. MX-06 is complete as not adopted; MX-07A and MX-07B were measured, rejected and removed; MX-07 is complete. MX-08 is in progress, MX-09 is pending and MX-10 remains the final combined verification. The whole program is active. These runs compare the **same unchanged checkpoint implementation with observation enabled or disabled**, not an optimization before and after. The retained MX05 restoration changes are unrelated to these checkpoint timings.

The frozen runtime inventory contains **129 files**. Runtime SHA256 is `285b379b21040344109c2c795ff0b2278b6867f6bed121f7b410b683ae47f812`, test assembly `6915ad5762aa0f44e2f5286f2ec43cc5b1e0534b3a07a9db4dbbebabcb9a3dc7`. The five checkpoint source hashes, protocol/test/controller hashes and all raw result/TRX/resource references are in the [tracked report](../../reports/benchmarks/remaining-lifecycle-optimization-20261028.json), under `MX-06.currentProductCost`; raw evidence remains in ignored `artifacts/remaining-optimization-20261028/mx06-cost/`.

Two closed inputs were frozen and copied outside timing. **DF tail** uses the historical exact three-file fixture: 20MiB cold private payload, 1,792,000B hot content and **10,305 × 112B** ordered individually durable writes. **Private-128m** has **128MiB cold private payload, 1MiB hot content, 4,096 metadata-only leaves**, and **8,192 × 112B** writes. Its separate fixture setup/full logical proof took **27.967163s** and is not a cost sample. Journal input lengths/hashes are respectively **28,788,115B / `4778c7994eab77502f88f893697f305f5e2705ee380fb960f2a076963ed87899`** and **138,073,358B / `7fd16c8d5393c7f37d6daa6e90ea79809a9c5663285cabaf6182c4bc7d15012f`**. Every trial proves the complete input, including file hashes, modification times and attributes.

Order for each profile was **observer → plain → plain → observer**, two trials per condition. The existing observer only retains bounded in-memory timestamps, but a non-null hook also splits each nontrivial payload append into two writes. Both conditions poll the same cheap checkpoint counters. Full input verification precedes the write stopwatch and warms data/cache; OS caches were not reset. No competing build/performance trial ran.

| Input / run | Condition | Write interval (s) | Cumulative flush (s) | Whole automatic checkpoints (s) | Checkpoints | Prefix bytes |
| --- | --- | ---: | ---: | ---: | ---: | ---: |
| df-tail / 01-observer | observer | 24.3909387 | 21.8218068 | 2.4603300 | 3 | 86,386,653 |
| df-tail / 02-plain | plain | 24.2689690 | 21.6967858 | 2.3071578 | 3 | 86,386,653 |
| df-tail / 03-plain | plain | 42.7237793 | 40.0566258 | 3.1790125 | 3 | 86,386,653 |
| df-tail / 04-observer | observer | 32.6202367 | 30.0467886 | 2.4888998 | 3 | 86,386,653 |
| private-128m / 01-observer | observer | 61.4305174 | 57.5486221 | 4.2482368 | 2 | 280,739,076 |
| private-128m / 02-plain | plain | 36.8716730 | 33.0436321 | 3.6960229 | 2 | 280,739,076 |
| private-128m / 03-plain | plain | 21.1357607 | 17.3218044 | 3.6948417 | 2 | 280,739,076 |
| private-128m / 04-observer | observer | 21.2407267 | 17.3650472 | 3.7626646 | 2 | 280,739,076 |

| Input | Mean checkpoints, observer / plain (s) | Mean state output + candidate flush, observer (s) |
| --- | ---: | ---: |
| DF tail | 2.47461490 / 2.74308515 | 0.54136015 |
| Private-128m | 4.00545070 / 3.69543230 | 0.73128705 |

Observed checkpoint sub-interval means are below. These are parts of whole checkpoint/write time; never add them to those totals. The residual includes sizing, source/state hashing, the initial intent flush and small tail/finally work; it is not pure serialization. Proof intervals group several operations, and the output interval includes durable flush.

| Observed checkpoint interval | DF tail mean (s) | Private-128m mean (s) |
| --- | ---: | ---: |
| Before intent + residual | 0.44089855 | 0.67597835 |
| State output + candidate flush | 0.54136015 | 0.73128705 |
| Candidate replay/proof + old-source rehash + prepared intent | 0.77775435 | 1.33206885 |
| Close + atomic publication | 0.00336505 | 0.00231785 |
| Published replay/proof + extent rebinding | 0.59759625 | 0.91313320 |
| Previous hash + delete | 0.11304530 | 0.35023745 |
| Intent delete + complete | 0.00059525 | 0.00042795 |

All **eight trials** passed their single cost test with **zero failures/skips**, with complete content/metadata/sequence verification after reconnect, exact bounded-store reclamation, removed temporary roots, unchanged frozen inputs and **zero basis-read attempts**. No checkpoint/hook samples were dropped. DF records **10,305 commits / 10,314 flushes** per run; Private-128m records **8,192 / 8,198**. The separate setup proof is not a ninth performance sample or a full regression gate.

The write stopwatch includes every successful ACK and automatic checkpoint. Copy/open/input verification, final live verification, reconnect verification, seal and exact reclaim are separately reported; no forced final checkpoint is added. Reclaim ranges are **0.1352157–0.1437103s** for DF and **0.5218845–0.5627663s** for Private-128m. Cumulative allocation is not peak RAM. OS-reported peak working set, approximately **250ms** Private samples and process CPU cover setup and independent assertions; they are not write/checkpoint-only resources and sampled peaks may be missed.

Write/flush variation is large even with the same unchanged product; the plain/observer mean difference is neither an optimization gain nor an isolated observer-overhead estimate. On these inputs, removing the *entire* checkpoint would save only about 2.5–4.0s; removing only repeated state output/candidate flush has a smaller measured scope of 0.54–0.73s. These are optimistic opportunity bounds, not promised savings, and do not meet the predeclared **at least 10s AND 15%** criterion by themselves. The separately implemented fixed-object full/incremental prototype was measured and not adopted; it cannot inherit current-product, full-state/recovery, WinFsp or Unity qualification. No product checkpoint format changed, no candidate is adopted, and no actual Unity or power-loss evidence is added. Historical DF/SX/BL observations and failed/rejected candidates retain their original limits.

<a id="mx-06-standalone-prototype"></a>

## MX-06: initial standalone prototype observations

**These initial observations are preserved; product integration was not adopted after the separate window cohort below.** A separate BCL-only fixed-object model compares full rewriting with immutable-segment reuse while sharing the raw writer, durable ACK, complete metadata/graph proofs, intent publication, recovery and exact reclamation. Its second build had **0 warnings / 0 errors** and its small interruption/preservation self-test passed **44 cases in 8.6984688s**. The preceding **43-case** snapshot, the pre-primer source and the pre-poison source are preserved; these overlapping runs are not added as unique test coverage. The final catch-poison regression prevents an ACK after a state-proof failure. None of these are product full-gate or OS power-loss qualification.

The frozen measured DLL SHA256 is `ec8834956f564fb040b18f3c6b6e872981ef0782b727fed95fd32f004f7aa58d`. The [tracked report](../../reports/benchmarks/remaining-lifecycle-optimization-20261028.json), `MX-06.standalonePrototypeEvidence`, records the exact eight source files, five runtime files, protocol, controller, raw result/closure hashes, both source-history manifests and the read-only review. The original binaries and raw evidence remain in ignored `artifacts/remaining-optimization-20261028/mx06-prototype-cost/`.

Each case ran **full → incremental → incremental → full**, two observations per mode. Cold/hot uses **256 MiB** of initially private cold data, **4 KiB** hot data and **8,192 writes of 256 bytes**. Fragmentation is a separate **32 MiB** control with **18 writes of 1 MiB**, each followed by a checkpoint; it crosses the obsolete-byte consolidation bound. Both modes construct the same full seed, force a common consolidated state, append one unchanged-value metadata primer and reopen in the selected mode. GUID filenames vary while expected logical bytes, metadata and sequence match. Setup and its cache warming are outside the timer; caches were not reset.

| Input / mode in execution order | Whole measured s | Write loop s | Final forced consolidation s |
| --- | ---: | ---: | ---: |
| Cold/hot 01 full | 48.8059669 | 42.7703663 | 3.5270722 |
| Cold/hot 02 incremental | 26.9824026 | 20.9857475 | 3.4524478 |
| Cold/hot 03 incremental | 27.1943104 | 20.8839859 | 3.6459297 |
| Cold/hot 04 full | 56.3562115 | 50.2641567 | 3.5344133 |
| Fragmentation 01 full | 13.1008051 | 12.1239038 | 0.6052183 |
| Fragmentation 02 incremental | 8.1645396 | 7.1113079 | 0.6336120 |
| Fragmentation 03 incremental | 8.2633262 | 7.1155321 | 0.7499371 |
| Fragmentation 04 full | 10.9940462 | 10.0775182 | 0.5348609 |

| Input | Full mean s | Incremental mean s | Observed saving | Standalone screen |
| --- | ---: | ---: | --- | --- |
| Cold/hot | 52.58108920 | 27.08835650 | 25.49273270s / 48.48270184% | Meets 10s AND 15% |
| Fragmentation | 12.04742565 | 8.21393290 | 3.83349275s / 31.82001584% | Below absolute 10s; consolidation/regression control |

The whole measured interval includes the write loop and its automatic checkpoints, final forced consolidation, independent expected-content generation, close/reopen proof and exact reclamation. Cold/hot has **2 automatic checkpoints plus 1 final forced checkpoint**: full consolidates all three while incremental retains segments for the first two. Fragmentation has **18 loop checkpoints plus 1 final forced checkpoint**; incremental consolidates once during the loop and once at the end. All eight runs exited successfully, matched expected logical hashes, proved reconnect/reclamation and removed exactly their owned store. Closure receipts remain. No Unity or WinFsp ran.

**The full observed saving is not isolated checkpoint-algorithm causation.** Cold/hot mean checkpoint time, including final consolidation, was **12.16008485→7.02195740s**, a **5.13812745s** reduction. Its unchanged append-flush path measured **37.74354865→17.33445330s**, a **20.40909535s** difference. Checkpoint I/O interaction, cache/order and host variance were not separated by these two samples per mode. These phase timers are nested in the whole interval; do not add them as separate wall time. Current-product checkpoints measured only about **2.5–4.0s**, with observed state output/flush **0.54–0.73s**, on different inputs above. Model seconds and percentages must not be substituted for product or Unity gains.

Measured checkpoint payload copies, excluding append payload, were **805,318,656→268,439,552 bytes** for cold/hot and **637,612,032→67,117,056 bytes** for fragmentation. Cold/hot final-consolidation-inclusive physical file-length peaks were almost identical at **538,604,472→538,605,552 bytes**; the fragmentation control increased **68,192,641→83,934,459 bytes**. These are cumulative admitted/observed lengths since the fresh selected-mode open, not independently reset phase peaks, allocated clusters or RAM. The earlier full-seed construction overlap belongs only to setup metrics. Instrumented proof bytes also exclude ordinary frame/control parsing and are not physical disk traffic.

| Whole-process resource mean | Cold/hot full → incremental | Fragmentation full → incremental |
| --- | --- | --- |
| OS-reported peak working set, bytes | 69,201,920 → 70,780,928 | 74,115,072 → 78,077,952 |
| Sampled Private maximum, bytes | 35,702,784 → 36,861,952 | 40,372,224 → 44,109,824 |
| Process CPU, s | 56.453125 → 53.484375 | 13.2109375 → 10.7421875 |

These resource observations include common setup and assertions; Private uses approximately 250ms sampling and can miss the true peak. They are not algorithm-only resource costs. The experiment retains its bounded suffix, segment/obsolete limits and final consolidation cost, but has no product namespace/basis/provider/candidate/seal/schema integration or NTFS identity/parent-relative containment. Managed exceptions do not qualify process termination, reboot or power loss. The initial cold/hot result motivated the independent window diagnosis below; its raw values remain unchanged. **Product integration was not adopted.** The optional product phase cost test is archived and removed; the lifecycle map's product hashes are unchanged.

<a id="mx-06-final-disposition"></a>

## MX-06 final: window diagnosis and non-adoption

**MX-06 is complete as not adopted.** The following independent cold/hot ABBA adds identical scalar window observations to both model modes. It does not replace the original eight model trials or the eight unchanged-product attribution trials. The 44-case model self-test belongs to the preceding protocol snapshot; the window-only build passed with **0 warnings / 0 errors in 3.12s**, and its four normal measured closures are separate evidence. No product format or checkpoint code was changed.

The same **256MiB cold / 4KiB hot / 8,192 × 256B writes / seed 41** and common consolidated seed plus one metadata primer were used. Snapshot counters are recorded at completed writes **0, 4,095, 4,096, 8,191 and 8,192**: cumulative automatic checkpoints are **0, 0, 1, 1 and 2**, so interval write counts are **4,095 / 1 / 4,095 / 1**. A fixed five-element value array is allocated before timing; observations read scalar counters and a monotonic clock without timed-loop file I/O, state copies or ACK changes. Both modes follow the same append path before the first checkpoint. Independent static and raw-result reviews confirmed these boundaries.

| Run / mode | Whole measured s | Write loop s | Loop checkpoints s | Loop append flush s | Final forced consolidation s |
| --- | ---: | ---: | ---: | ---: | ---: |
| 01 full | 31.1421934 | 24.9395249 | 7.2867287 | 17.5161976 | 3.6215352 |
| 02 incremental | 27.0137796 | 20.9623484 | 3.5368401 | 17.2975515 | 3.5105223 |
| 03 incremental | 27.0652545 | 21.0771747 | 3.4243205 | 17.5119701 | 3.3756428 |
| 04 full | 36.6497808 | 27.5493863 | 10.0130104 | 17.3971427 | 6.5293515 |

| Mean interval | Full s | Incremental s |
| --- | ---: | ---: |
| Whole measured | 33.89598710 | 27.03951705 |
| Write loop | 26.24445560 | 21.01976155 |
| Loop checkpoints | 8.64986955 | 3.48058030 |
| Loop append flush | 17.45667015 | 17.40476080 |
| Final forced consolidation | 5.07544335 | 3.44308255 |
| Close/reopen proof | 1.53615035 | 1.56518295 |
| Exact reclaim | 1.03979000 | 1.01137705 |

| Window, completed writes in interval | Full elapsed / flush / checkpoint s | Incremental elapsed / flush / checkpoint s |
| --- | --- | --- |
| Before first checkpoint, 4,095 | 8.80167760 / 8.73069335 / 0 | 8.80248340 / 8.73456015 / 0 |
| First checkpoint and one ACK, 1 | 3.53649170 / 0.00300050 / 3.53345925 | 1.72720770 / 0.00304115 / 1.72413745 |
| Between checkpoints, 4,095 | 8.78288360 / 8.71611630 / 0 | 8.73048330 / 8.66412085 / 0 |
| Second checkpoint and one ACK, 1 | 5.12330160 / 0.00686000 / 5.11641030 | 1.75951245 / 0.00303865 / 1.75644285 |

Whole saving is **6.85647005s / 20.22796985%**: the relative **15%** screen passes, the absolute **10s** screen fails. Loop checkpoint saving is **5.16928925s**; append-flush difference is only **0.05190935s**. The first 4,095 ACKs have effectively equal flush time. The earlier **25.49273270s** whole saving and **20.40909535s** common append-flush difference remain valid recorded observations, but the large flush difference did not reproduce and its cause is unresolved. The **1.63236080s** final forced-consolidation difference includes substantial full-mode variation; it is not all assigned to incremental publication. Nested windows/stages are not extra time to add to the whole interval.

All four runs matched the same initial/final expected logical hashes, ended at **sequence 8,453**, recorded **8,192 ACKs / append flushes**, crossed **two** automatic checkpoints and completed the final forced consolidation. Full consolidated **three** times and incremental **once**. Every reconnect/proof and exact reclaim passed; all owned store directories are absent and external closure receipts remain. Raw result/config/closure hashes, source/protocol/controller hashes and independently recomputed means matched. The measured DLL SHA256 is `0c70e0201e2351f79cc9c792358ace21ab0703d68d84b548f426fb541a00d03a`. The [tracked report](../../reports/benchmarks/remaining-lifecycle-optimization-20261028.json), `MX-06.windowDiagnosticEvidence`, pins the separate source/runtime manifests, all four rows, resource observations and independent review. Raw evidence is preserved in ignored `artifacts/remaining-optimization-20261028/mx06-window-cost/`.

Only two observations per mode were taken. Common full-seed setup/proof warms caches outside timing; physical GUID layout and device pressure are uncontrolled. Process CPU/working set include setup/assertions, and 250ms Private samples are not a guaranteed peak. The model omits product namespace/basis/provider/candidate/seal integration and NTFS identity/parent-relative race protection. No product, Unity, process-kill or power-loss qualification follows. The independent window cohort cannot identify the cause of the earlier cohort's flush divergence.

The current-product checkpoint opportunity (plain means **2.74308515s / 3.69543230s**, observed output plus candidate flush **0.54136015s / 0.73128705s**) and the model's non-reproduced large gain do not justify a new multi-file persisted format, graph recovery, Library restore and exact-reclamation contract in this scope. Product integration was not implemented. `mx06-removal.json` records the optional cost test's byte-exact archive/removal and five unchanged product source hashes; the archive SHA256 is `7a487f43e31bfee9188505e3b286ceb85bcad415cc0a5a9ea1769812038eb58c`. Prototype sources/binaries, product-connection design and all raw cohorts remain ignored historical evidence, not an optional product backend or automatically resumed work. README RR-01 and lifecycle-map product hashes remain unchanged. **MX-07A and MX-07B were measured, rejected and removed; MX-07 is complete. MX-08 is in progress, MX-09 is pending and MX-10 remains the final combined verification. The whole program is active.**


<a id="mx-07a-source-cache-rejected"></a>
## MX-07A: cross-invocation source cache rejected

**MX-07A was measured, rejected and removed.** MX-07B and the final row disposition are recorded below. The source-cache candidate saved **8.45773340s / 15.47910650%** on the fixed editorless input, below the predeclared **at least 10s AND 15%** mean whole-workspace gate. This does not qualify a whole Unity improvement; B has separate local evidence below.

The candidate reused only source content hashes for None/recorded Plastic, with a bounded checksummed current-version cache. Every call still scanned full namespace/metadata and checked complete generation history, owner/basis/provider/mount identity before and after work. Cache-capable reads used a pinned native volume path; independent preserved payloads retained fresh full proof. History backfill, corrupt/pending audit recovery, valid foreign receipts and changed identity stayed fail-closed. These candidate contracts describe the archived experiment, not current product support after removal.

The actual measured fixture used **source none / application none / project-snapshot** on NTFS/WinFsp: **2,112 files / 2,281,701,376 bytes (2.125GiB)**, comprising 64 × 32MiB and 2,048 × 64KiB files, deterministic seed 261026, fixed mtime and attributes. The edit overwrote eight bytes of one large file and added 64KiB with fixed metadata. ABBA ran serially, one workspace at a time, without inserted status loops. One monotonic timer covered ensure dispatch, edit, checkpoint, separate release, response validation and controller evidence writes; per-command intervals below are nested and cannot be added to that total. Setup, full input proofs, post-release independent proof and common basis prepare/restore are separate. Full preverification warms caches; OS caches were not cleared and common observer overhead is not claimed to be zero.

| Order / variant | Whole workspace (s) | Ensure (s) | Checkpoint (s) | Release (s) |
| --- | ---: | ---: | ---: | ---: |
| 1 before | 61.99037280 | 20.90413850 | 31.17722710 | 9.82596920 |
| 2 after | 33.69321480 | 17.00396150 | 10.83320760 | 5.77979260 |
| 3 after | 58.67066730 | 17.59344670 | 35.51189250 | 5.48544230 |
| 4 before | 47.28897610 | 21.10313050 | 16.01507530 | 10.03636720 |
| Mean before | 54.63967445 | 21.00363450 | 23.59615120 | 9.93116820 |
| Mean after | 46.18194105 | 17.29870410 | 23.17255005 | 5.63261745 |

Common preparation was **3.69286070s** and final restoration **12.16613020s**, outside the primary comparison. Checkpoint differed greatly within both variants; its cause was not instrumented. Do not attribute that variation to flush, cache, or the candidate algorithm. The relative gate passed but the absolute gate failed, so the candidate was not retained. Two observations per variant on one machine/device do not establish general speed, peak RAM, actual Plastic-server behavior, original apply, power-loss safety or Unity lifecycle gains. Historical PR/RT inputs informed fixture design; their old timings are not baseline samples for this cohort.

Qualification records are source-specific and overlapping; **do not sum them into a unique test count**:

| Evidence / source stage | Result and scope |
| --- | --- |
| Build 1 / build 2 | Preserved compile failures: missing Runtime imports for WorkspaceStore, then JournalDeltaStore in a test. No successful qualification is claimed for either snapshot. |
| Build 3–6 | Each passed with 0 warnings / 0 errors; final build 6 took 9.12s. These are builds, not full-suite test gates. |
| Focused 1 | 169 total: 168 passed / 1 fixture failure. CopyHashFixture applied a file mtime API to a directory; the directory API correction was preserved separately. |
| Native probe 1 / focused 2 | The first actual device-root enumeration failed because .NET trimmed its trailing separator. The corrected namespace probe and corrected fixture passed 2 selected tests. Only double-trailing-separator GLOBALROOT spelling passed all probed native operations. |
| Native guard 2 | 11 passed / 0 failed / 0 skipped on its recorded source. |
| Cache focused 3 | 36 total: 35 passed / 1 new fixture failure. An ordinary NTFS extended path rejected an interior doubled separator with error 123; this is separate from actual WinFsp GLOBALROOT behavior. |
| Focused 4 / final build 6 | 2 passed: the corrected ordinary-Win32 fake-root test and one actual WinFsp Files full/metadata scan test. The fake test alone does not qualify GLOBALROOT. The native scan compares public and pinned full scans plus metadata scans, before and after private overwrite/create. |

Candidate source inventory contains **327 files** and its CLI runtime **45 files**; baseline contains **323 source/test files / 45 runtime files**. Both record starting HEAD `24956f4975a8c9ae57a21f4c52a656b9f8b51ea3` with the source inventories fixing the uncommitted accepted MX-05 baseline and rejected MX-07A candidate. Dependency rebuilding changed 22 DLL/PDB artifacts; no whole-PE equivalence is asserted. The raw failures, before-fix files, source/runtime manifests and trial evidence remain ignored under `artifacts/remaining-optimization-20261028/`. The [tracked summary](../../reports/benchmarks/remaining-lifecycle-optimization-20261028.json), `MX-07.sourceCacheCandidate`, records sanitized exact hashes and each test scope. No full gate was run for adoption after this failed performance screen.

All four trials proved source bytes/mtime/attributes, baseline/result oracle equality, full independent payload hashes after release, exact provider exit and absent task mount/delta. Each retained result proof covered **2,114 unique objects / 2,315,321,344 bytes**. The common basis was restored to ordinary NTFS with exact full input and no basis payload/delta/provider; independent results remain retained. Removal restored six existing files byte-exactly and removed four archived candidate-only files. At MX-07A removal, all **323** source/test files matched the MX-07 baseline hashes. No source-cache mode, guard API, CLI change or candidate tests remain in product source. README RR-01 and the lifecycle map are unchanged. The separate result-proof candidate and final MX-07 disposition follow below.

<a id="mx-07b-result-proof-rejected"></a>

## MX-07B: retained result proof rejected; MX-07 closed

The candidate retained at most **4,096** readonly independent object handles for one command, hashed the exact basis/result union once, and reread control records plus workspace/revision/control identity before reusing that proof. P0 avoided the second union hash after result publication; P1 held the first proof across unmount to source unregister. Missing capability or an over-cap set retained full verification. The candidate did not add an on-disk format or public option.

Focused Files, coordinator and native lease correctness passed **50 tests, 0 failed/0 skipped, 6.051s**. This covered fresh controls, foreign/stale/disposed binding, fallback, failure/cancellation disposal, drain, unmount/reclamation order and native identity/reparse protection. The first build failed on **4** test cancellation-token analyzer errors; corrected build2 passed with **0 warnings/errors in 9.56s**. The first focused filter was invalid and executed **0** tests; focused2 is the passing run. These setup failures remain recorded rather than counted as product runtime failures. The separate opt-in local cost test passed **1 test in 6.698s**, not a whole-lifecycle test.

The read-only cost input was an already released independent result from MX-07A: **2,114 objects / 2,315,321,344 bytes**. The same build exercised existing `VerifyResultAsync` + `UnregisterAsync` and optional acquire-proof + unregister-with-proof paths. One common full proof warmed the input outside timing; the final fresh full proof and unchanged control hashes passed after all leases were disposed.

| Run | Path | Seconds |
| --- | --- | ---: |
| Before 1 | Repeated full proof | 1.25475270 |
| After 1 | Held proof | 1.49346260 |
| After 2 | Held proof | 1.40144320 |
| Before 2 | Repeated full proof | 1.10463180 |
| Mean before → after | | 1.17969225 → 1.44745290 |

The held path was **0.26776065s / 22.69750013% slower** in this warmed local observation. It supplied no material benefit, so it was rejected without an additional global gate or whole-workspace trial. There are only two observations per path on one host. Source scan, unmount, preparation, Editor and full lifecycle are outside this timer; do not extrapolate this result to full source/Unity or real Plastic performance.

`mx07b-removal.json` records 6 existing files restored byte-exactly and 8 candidate-only files archived then removed. The archive contains **14 source/test files and 45 runtime files**; accepted MX-05 remains, and concurrent MX-08 edits were untouched. This is a bounded removal proof, not a claim that the whole source tree still matches the pre-MX-08 baseline. The [tracked report](../../reports/benchmarks/remaining-lifecycle-optimization-20261028.json) adds `MX-07.resultProofCandidate` with raw hashes; all earlier evidence remains. **MX-07A/B are rejected and MX-07 is complete. MX-08 is active, MX-09 pending and MX-10 remains final combined verification.** No README RR-01 or lifecycle-map performance claim changes.

<a id="mx-08a-combined-resize-local"></a>

## MX-08A: combined resize/metadata local evidence

The change to 4 product files adds `SetLengthAndMetadata`, forwards it through SourceView and uses it in WinFsp Overwrite. SJR2 publishes the final entry in 1 resize commit; unchanged length requires 0/1 metadata commits. Existing clipping/replay and durable ACK boundaries remain. SJL4 retains the separate resize and metadata capsule publications. No new CLI option or journal format is introduced. The candidate has 2 focused/cost test files and is **provisionally retained**, pending the later combined qualification.

Build1 passed with **0 warnings/errors in 33.29s**. Focused correctness passed **30 tests, 0 failed/0 skipped, 1.428s**. The separate opt-in cost test passed **1 test, 0 failed/0 skipped, 263.674s**. These are local checks, not the deferred whole-repository gate.

The same build compared separate resize + metadata and the combined operation on a real file-backed bounded SJR2 journal: **64 files × 128 rounds = 8,192 saves**, with **4,096-byte** refills. Timing includes the mutation/refill loop and final flush. Setup, seal collection and reopened full-content proof are outside timing; all four reopened results matched exactly. The immutable basis is a small synthetic tree; no workspace mount or Unity process ran.

| Run | Path | Seconds | Durable mutations |
| --- | --- | ---: | ---: |
| Before 1 | Separate | 123.00958680 | 24,576 |
| After 1 | Combined | 51.99791950 | 16,384 |
| After 2 | Combined | 35.19008530 | 16,384 |
| Before 2 | Separate | 52.79853000 | 24,576 |
| Mean before → after | | 87.90405840 → 43.59400240 | 24,576 → 16,384 |

The observed mean reduction is **44.31005600s / 50.40729269%**, but the large baseline variation prevents claiming an isolated causal percentage. The test repeats **8,192** saves against **260** historical Unity Overwrite observations (**31.50769231×**); it intentionally amplifies this operation and does not establish representative Unity savings. Fewer durable mutations are a verified mechanism, not permission to scale the timer reduction to a whole lifecycle.

The [tracked report](../../reports/benchmarks/remaining-lifecycle-optimization-20261028.json) adds `MX-08.A` with raw rows and hashes. The parent controller froze **6 source/test files and 45 runtime files** in `mx08a-candidate/manifest.json`; its exact source hashes and manifest hash are linked in the report. This fixes the candidate identity and does not expand qualification. **A remains provisional; MX-08B mixed mutation batching was rejected and removed, MX-09 is pending, and MX-10 is the final matched combined comparison.** No whole-lifecycle adoption or README RR-01 performance change follows from this local observation.

<a id="mx-08b-mixed-batching-rejected"></a>

## MX-08B: mixed batching rejected and removed

The candidate connected Write/nullable metadata/resize to ordered typed batches and the existing WinFsp callback queue. It retained accepted-ticket Flush drain, per-request returned entry state, definite rejection isolation, uncertain-failure stop, bounded copying and the write-only/large-write paths. These candidate APIs, mixed telemetry and candidate-only tests were removed after the local screen; they are not current product capabilities.

Build1 had **8** test property syntax errors. Test-only correction produced build2 with **0 warnings/errors in 7.97s**. Focused1 recorded **45 total / 43 passed / 1 failed / 1 conditional telemetry-cost skip in 3.548s**. The partial-frame replay test wrongly reused a lazy source id; only the test was changed to reopen by path. Build3 passed with **0 warnings/errors in 8.22s**, then only that method's **3 variants passed, 0 failed/0 skipped in 0.263s**. This yields **44 unique passing cases and 1 conditional skip**, not a sum of overlapping runs. The original failure remains preserved, product sources did not change between the focused runs, and these removed-candidate outcomes are not the current product test count.

The ABBA comparison used **129 files per runtime** and the same test DLL. The baseline's **45** A runtime files matched the frozen A manifest. A pre-trial path-separator inventory correction found no hash difference and launched no trial. The timed direct-callback workload used a real durable file-backed bounded SJR2 and fake immutable source: each round performs Write64, SetBasicInfo and Resize32, with one private file per worker. Setup/ready barriers precede timing; all requests plus explicit Flush are timed; reopen and content/source proof are outside. No artificial batching delay, mount, kernel scheduling or Unity process was used.

| ABBA run | Serial: 1 worker × 64 rounds (seconds) | Concurrent: 8 workers × 256 rounds (seconds) |
| --- | ---: | ---: |
| Before 1 | 0.42679080 | 12.33052060 |
| After 1 | 0.43725330 | 3.17787470 |
| After 2 | 0.43728460 | 3.19732090 |
| Before 2 | 0.43332210 | 12.30007430 |
| Mean before → after | 0.43005645 → 0.43726895 | 12.31529745 → 3.18759780 |

Concurrent **6,144 callbacks** saved **9.12769965s / 74.11676159%**; serial **192 callbacks** increased **0.00721250s**. The concurrent result passes **15%** but fails the required absolute **10s**. B was rejected without an amplified repeat. All four trial tests and both workload replay proofs passed. Two observations per variant, one host and uncleared OS caches remain limits; this stress mix is not the historical Unity callback frequency or a whole-lifecycle gain.

`mx08b-removal.json` records **4** existing files restored byte-exactly and **6** candidate-only files archived then removed; the source archive has **10 files**. All **6** A source/test hashes remain exact. The [tracked report](../../reports/benchmarks/remaining-lifecycle-optimization-20261028.json) adds `MX-08.B` with raw results, hashes, test history and removal. At removal, `bin/Release` still contained rejected B and required a current-source rebuild; the later MX09 Release rebuild below replaced it. The frozen A runtime remains valid. **MX-08 implementation and local disposition are complete; A stays provisional, B is complete as rejected, MX-09 remains and MX-10 is the final combined comparison.** The B closeout itself added no post-removal build, global gate, mount or Unity performance qualification; the later MX09 build is recorded separately below.

<a id="mx-09-unpublished-preparation"></a>

## MX-09: removed candidate implementation and local-verification record

**The following preserves the removed candidate's historical contract and local verification, including its then-pending status. It is not the current product path. The final measured disposition below owns current status.**

Only a fresh range-journal task with null `ResultRevision`, a running owned Editor and provider capability overlaps normal `StopAsync` with `PrepareCheckpointAsync`. Preparation may populate existing source-proof caches and verified unreferenced content objects; it publishes no collection/result manifest, index, commit or ref. Files checks complete source generation and owner/basis, then publishes changed payloads only from hash/metadata-verified RAM. If distinct changed payloads exceed **256MiB**, preparation declines. This is not a total-process RAM bound. Git retains existing owner/source/interpretation proofs and path-specific clean filters, writing only changed-file content objects.

Both operations drain even on failure or cancellation. Only joint success continues through `RequireStopped → RemoveInfrastructure → full pre-seal → fresh checkpoint/preserve → full post-seal` and existing publication/reclamation guards. Final collection freshly decides late writes. Existing results, release resumption, editorless work and unsupported providers retain the existing path. MX-08A remains provisional and MX-08B is rejected and removed. MX-09 implementation and local correctness verification are complete; performance, adoption and whole-workflow qualification remain unconfirmed. MX-10 remains the final combined verification. Tests and timings not yet executed are not recorded as passing.

The first Release build of frozen product source/tests passed with **0 warnings and 0 errors in 27.40s** (`mx09-build1.log`). This current-source rebuild replaced the binaries left after MX08B removal; it is not the repository-wide `build.ps1` gate. The completed focused result is recorded separately below; elapsed benefit remains unconfirmed.


Only the **completed focused4 execution** establishes **55 passed / 0 failed / 0 skipped**, xUnit **281.172s** (TRX run wall **281.17478s**). It includes **45 new cases** and **10 existing protections**. This is correctness-test duration, not a shutdown-performance observation.

- Build1 took **27.40s**, Build2 **7.94s**, and Build3 **7.95s**, each with **0 warnings and 0 errors**. Build2 changed a Git test observer from synchronous `GetResult` to async; Build3 replaced the Runtime fixture conditional's target-typed `new` with explicit `PreparationAtomicSource` construction. All **6 product files** retain their Build1 hashes.
- Focused1/2 were interrupted at Starting with no completed count. Verbose focused3 finished **17 Files cases**, then exposed the no-capability fixture creating a separate `PreparingSource` whose preparation barrier had never been released; it was interrupted. Original source/runtime and failed-run records are preserved. The Git observer correction is a separate test-risk improvement, not an established cause of the earlier waits. Partial cases from interrupted runs are not added to the final pass count.
- `mx09-local-final/manifest.json` freezes **9 source/test files and 129 runtime files**. Manifest SHA256 is `b03df13197a7bdd7e284509fa81806f042c5731e5f02c4176abf1f935d0c4260`; final TRX SHA256 is `e25b17cc7db33561af63c95b524619ff6e557608288c66460f2789d18411fe50`. The MX09 block in the [tracked summary](../../reports/benchmarks/remaining-lifecycle-optimization-20261028.json) owns this evidence.

**Implementation and local correctness verification are complete; performance, adoption and whole-workflow qualification remain unconfirmed.** The program and MX09 row remain incomplete. MX08A stays provisional, MX08B is removed, and MX10 is last. This does not claim the repository-wide `build.ps1` gate or an actual Unity shutdown/whole-lifecycle comparison has run.

<a id="mx-09-final-disposition"></a>

## MX-09 actual Git/Unity finalization: rejected and removed

With matched source/Library file bytes, mtime, attributes and initial loaded MVID, 2 ABBA samples per variant measured `workspace checkpoint --release` from an already-running owned Editor through normal shutdown, independent result and owned reclamation. Mean time was **69.76183845→70.28309595s**; signed before-minus-after saving was **-0.52125750s / -0.74719576%**, below the predeclared **at least 10s AND 15%** screen. MX09 is closed as not adopted; candidate product/tests were archived and removed/restored. MX08A remains provisional, MX05 remains retained, and the program and MX10 remain incomplete.

No separate `unity stop` preceded the command. Library restoration is timed separately; continuous observation includes preparation, startup, edit/compile, finalization, restore and observer costs. Initial readiness precedes the new MX09 preparation and varies substantially, so a shorter whole observation is not attributed to MX09. The nested intervals below must not be added.

| Order | Checkpoint/release s | Initial ready s | Library restore s | Continuous observation s |
|---|---:|---:|---:|---:|
| 01-before | 70.59876170 | 175.74334200 | 22.89105400 | 394.47825940 |
| 02-after | 69.99108030 | 114.17107240 | 22.79306420 | 322.22756980 |
| 03-after | 70.57511160 | 100.91895940 | 22.88731850 | 311.21626180 |
| 04-before | 68.92491520 | 119.48113130 | 22.69723040 | 325.83617140 |

All four trials proved normal exit, an independent Git result, owned reclamation and preservation of original source/Library/index/HEAD/config/registrations. Every prior ref was retained; only each exact task branch/result ref was added. The shared Git object database and retained results grow between trials, so the entire repository is not byte-identical input. There is no None/Plastic speed qualification, general Unity gain, complete original-apply/discard lifecycle, peak-RAM or power-loss qualification. The repository-wide `build.ps1` gate and MX10 apply/discard comparison remain last. The current product path is `Stop → RequireStopped → RemoveInfrastructure → full pre-seal → fresh checkpoint/preserve → full post-seal → publish/reclaim`.

`mx09-performance-analysis.json` SHA256 `10f7a13a995b61680b353d9e4bea58cdb9fbef26c2c4cb610b70b905dbb3b84b`; `mx09-removal.json` SHA256 `96621f7841db9792cbf69e8dc3e8a5222a333afc84edb295108b5c539f656e06`. Removal receipt: 4 existing files restored, 5 candidate-only files removed; `retainedMx08AExact=true`, `verified=true`. Raw trials, candidate, 55-case result, builds and interrupted-run records are retained.

<a id="mx-10-final-closeout"></a>

## MX-10 final combination and program closeout

**Program complete; no large whole-time improvement established.** MX-01 closes diagnosis; MX-02–04, MX-06/07, MX-08B and MX-09 are not adopted; MX-05 A/publication-count-guarded B2 and MX-08A are adopted. This record authorizes or starts no further experiment.

| Endpoint | Before whole s | Final whole s | Before−final saving s | Before−final saving % |
|---|---:|---:|---:|---:|
| apply | 338.51645460 | 326.31757210 | 12.19888250 | 3.60363059 |
| discard | 281.90245510 | 286.53377360 | -4.63131850 | -1.64287980 |

Each endpoint has **1 observation per variant** in fixed order `01-apply-before → 02-apply-final → 03-discard-before → 04-discard-final`. Apply was **12.19888250s / 3.60363059%** shorter; discard was **4.63131850s / 1.64287980%** longer. Do not pool distinct endpoints as repeat samples or call this ABBA, a general multiplier or an individual causal effect.

**Final gate and adoption.** One `scripts/build.ps1` locked restore/Release build had **0 warnings / 0 errors**; tests were **2,000 total / 1,966 passed / 0 failed / 34 conditional NotExecuted**, with **9m46.195s** test execution reported by the log. The 34 optional cost/native/evidence and Child/ClientActor entry points were not run; the full gate did not repeat the historical `CombinedOverwriteCost` or root-delta/topology/leaf cost measurements. Do not add the 30 local cases, 55 rejected-candidate cases or earlier suite counts to this result.

The final **313 source/test files** and **45 runtime files** were frozen. All 45 runtime files match the frozen MX08A runtime byte-exactly. MX08A is adopted on local durable-save cost **87.90405840→43.59400240s**, observed **44.31005600s** saving and durable mutations **24,576→16,384**, plus **30** local cases, **1,966** final passes and correctness of **4** actual runs. It adds no format, queue or worker. The local workload's **8,192 saves** amplified the earlier Unity **260 Overwrites** by **31.5077×**, and baseline variation was substantial. The local saving is not inherited as whole-Unity saving or a pure causal percentage.

**Inputs and timing.** The baseline runtime has commit metadata `24956f4975a8c9ae57a21f4c52a656b9f8b51ea3` and product sources `bea4acdb95f3ea2a914587c7f5e5293b57940e7d`, already including earlier BO optimizations. The final candidate is the retained MX05/MX08A combination over that base. Unity **6000.3.22f1**, fixture base `f121a5adc9c4248994774dea03843504c949f09a`, complete source/Library file hashes, lengths, mtime, attributes and initial loaded MVID were verified per run. One `value + 1 → value + 2` edit, successful explicit refresh, changed generation/MVID and actual loaded values **4/6/13→5/6/13** were proved.

Whole time is one unpaused monotonic stopwatch from `library.prepare` dispatch through successful `library.restore` return. The stages below are in seconds, contained within that clock and not additive. Initial ready includes `unity-start`; finalization includes checkpoint/integrate/release or discard plus normal Stop. Stop-only time was not separately instrumented. Input/runtime/schema proof and target-branch preparation precede the clock; independent result/full original proofs, log copies and guarded fixture return follow it. External server lifecycle is separate; workspace observation overhead is included in whole.

| Run | Library prepare | Ensure | Initial ready | Edit/compile | Finalization incl. normal stop | Library restore | Whole |
|---|---:|---:|---:|---:|---:|---:|---:|
| 01-apply-before | 40.08292510 | 49.70547910 | 100.62175830 | 25.11523190 | 97.61364890 | 22.69338090 | 338.51645460 |
| 02-apply-final | 40.44669190 | 49.21571400 | 98.49277190 | 22.66951840 | 90.57529460 | 22.88032810 | 326.31757210 |
| 03-discard-before | 39.91864980 | 50.36644790 | 98.90863560 | 23.87092720 | 44.12045770 | 22.68171050 | 281.90245510 |
| 04-discard-final | 39.67230330 | 52.07913550 | 100.92310290 | 22.97087820 | 44.63291410 | 24.29955000 | 286.53377360 |

| Apply run | Checkpoint | Integrate | Release |
|---|---:|---:|---:|
| 01-apply-before | 63.98219020 | 17.76494740 | 15.86388280 |
| 02-apply-final | 56.55279770 | 18.46311250 | 15.55726580 |

**Result, original and reclamation.** For apply, checkpoint begins with the owned Editor running and owns its normal stop; the independent result is integrated into the actual original's fresh fixed target branch, then released. The full result tree/modes, exact one-line bytes, merge parents, physical original content and unchanged-file metadata were proved. Independent result/task/target refs remain retained. The original Editor was not started or asked to recompile the integrated result. Original HEAD/index legitimately change through authorized branch switch, integration and detached return; initial index bytes/mtime are not claimed unchanged. Index-tree/HEAD/branch/registration semantics and config/exclude were proved, with exact source/Library bytes, mtime and attributes rechecked after the outside-clock return.

Discard starts with the owned Editor running under its exact intent/digest and creates no checkpoint/result. Prior refs, original Git metadata and source/Library are preserved. All four runs proved normal exit **0**, **0** owned resources and mount/delta reclamation. Every prior ref remains; only the exact new task/result/target refs for each apply are allowed. The shared Git object database and retained results grow between runs, so the entire repository is not byte-identical input. The original external server was restarted under its separately approved procedure, with health and captured settings restored.

**Restore selection and remaining time.** Every run had original Library journal durable sequence **0** and route `unchanged-root`. Workspace-private Library writes are not original-Library writes; the MX05 changed-Library A/B2 routes were not selected. These actual runs therefore do not establish MX05 whole-lifecycle saving. Large observed intervals remain Library preparation about **40s**, ensure **49–52s**, initial ready **98–101s**, final apply finalization **90.57529460s** / discard **44.63291410s**, and restore **22–24s**. These are recorded limits, not authorization or a proposal for another trial. Input proof warms caches but does not equalize startup/flush, fixed-order or OS-cache effects. No None/Plastic, simultaneous multi-Editor, Play/Test/player-build, peak-RAM, power-loss or general Unity speed qualification follows. README RR-01 figures stay unchanged.

Exact evidence stays under ignored `artifacts/remaining-optimization-20261028/` and the tracked report. Pending values in frozen source/runtime manifests describe their freeze-time state; this closeout owns subsequent results.

- `mx10/analysis.json`: `42cc1a5d85e2f9a534417f2576da9652c8e8370d4dc8694b9b5f3703492c0293`
- `mx10-final-gate1.json`: `1ac7e98bd11807b9163bca3480bf0e2c0992dc9ae675a9a2222d7f65789c7761`
- `mx10-final-gate1.trx`: `31fb67c0968431d4977522c146147436fd51a09405de1ffb0128f8dd1bda59f4`
- `mx10-final-runtime.json`: `5866b47485b9ff3cbdb1d32f7f55326e860bef404f661fbd3fcca911ec87906d`
- `mx10-server-restoration.json`: `7a617e0c6223207c1256caaf1b60745af057b1649616b4bed3a4f2757b442134`
