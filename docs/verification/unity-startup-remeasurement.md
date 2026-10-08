# Unity startup remeasurement after SU

[한국어](../ko/verification/unity-startup-remeasurement.md)

Implementation follow-up: [SA-01–05](startup-additional-optimization.md) now covers the additional Library/source/publication/Bee changes with bounded evidence. The measurements and analysis below describe the historical US candidate; they are not current-candidate timings.

**US-01:** the two real Unity Editors reached initial readiness in **349.7933 seconds (5m 49.8s)**, compared with [UP-01](unity-startup-profile.md)'s 766.9024 seconds (12m 46.9s): an observed **417.1091-second / 54.39% reduction**. Scanning fell from about 447s to 16s. CompileScripts remained about 210s and did not improve. This is one cohort with a recovered launch failure, not an isolated causal percentage. The [dataset](../../reports/benchmarks/unity-startup-remeasurement-20261008.json) preserves timings, runtime hashes, failures and profiling boundaries.

## Candidate and equivalent input

[SU-01–05](startup-profile-optimization.md) was committed as `95c813537b5bdc698f8f48e2bca63c7ae99f1d30` before measurement. Both first `unity start` commands failed with `SnapshotIdentityMismatch` **before launching an Editor**: the persisted-workspace reader did not admit the source-metadata suffix that `ensure` correctly saved. Commit `9c09f04c38f5b9cc8f65f159298744f1c79d833d` fixes that admission guard, retaining exact hash/source/provider/revision checks. The required build passed **1,376 tests / 0 failures / 20 conditional skips**, with no build warnings or errors.

The same A/B records and mounted providers were resumed without state edits or repeated preparation. Providers retained the frozen `95c8135` runtime; subsequent CLI commands used frozen `9c09f04`. Decoder/journal algorithms did not change in the correction. Consequently US is a **resumed mixed-runtime cohort**, not a fresh lifecycle entirely on `9c09f04`. Initial failed responses remain failures in the evidence.

Unity **6000.3.22f1**, source `b09fa58e9d40a560d4fe1af7a213f8ac23e8e3b1`, **1,602 tracked files / 2,804,772,822 bytes**, two independent `git-snapshot` Editors and the original Library reference match UP. All **44,860 Library entries** have identical paths, content hashes, lengths, attributes and timestamps. Manifest hashes differ only through capture/ownership wrappers. Source bytes were checked before and after; no source edits, refresh experiment, Play Mode, tests or game build were performed.

Readiness comes from the unchanged external fixture: each exact returned path and owned PID/start identity, fresh `ready` observation, `isCompiling=false`, `isUpdating=false`, and code values A=4 / B=6 / original=13. Splinter heartbeat is not compile evidence. No UAC, service, global configuration or installed external-package change was used.

## Preparation time

| Operation | US seconds | Duration |
| --- | ---: | ---: |
| Prepare original Library reference | 150.4625 | 2m 30.5s |
| Prepare workspace A | 242.8974 | 4m 02.9s |
| Prepare workspace B | 251.8003 | 4m 11.8s |
| Both Editors initially ready, concurrent start | 349.7933 | 5m 49.8s |
| Selected setup + readiness operations | 994.9535 | 16m 35.0s |

The last row is an **operation sum**, excluding failed starts and the diagnosis/build/commit pause. It is not a continuous preparation-to-ready measurement. The actual interval was **1,554.7921s (25m 54.8s)**, including a **559.8147s (9m 19.8s)** gap between the last ensure and resumed launch. The dataset retains these UTC intervals. Successful `unity start` command time is already inside the readiness interval and is not added again.

| Unity phase | UP A / B, seconds | US A / B, seconds |
| --- | ---: | ---: |
| Scan | 446.9486 / 447.5970 | 16.0673 / 16.3939 |
| CompileScripts | 189.7586 / 192.4327 | 209.6090 / 209.4719 |
| ImportInProcess | 11.8075 / 11.7899 | 10.1738 / 10.3722 |

Unity phases are nested and overlap; **do not sum them**. A's initial refresh took 330.307s. Its Bee trace records only **one Csc action**, compiling the new `Splinter.Workspace.Editor` assembly in **13.0127s**. It also records 68 IL post-processing actions with **51.4177s interval union**, graph construction **41.8336s**, and graph `Backend.Write` **40.7893s**, including JSON serialization **3.2112s**. These are overlapping elapsed scopes, not exclusive CPU costs. The initial verification also includes **60.1323s in ILPP-Configuration**; see the additional diagnosis below. CompileScripts should not be described as 210 seconds of C# compiler computation or dozens of C# recompiles.

## Remaining opportunities

### 0. A 60-second ILPP task runs before an obsolete graph is rejected

A's first Bee verification took **72.91s**, versus UP's 14.58s. The trace shows ILPP-Configuration starting at relative **10.134492s**, `CheckDagSignatures` at **10.155641s**, and the main thread's `JoinBuildThread` at **10.162926s**. With deferred DAG verification, the old graph's task began about 21ms before verification. After discovering changed input data, Bee waited for the already-started worker: ILPP **60.132280s** overlaps Join **60.131790s**. B shows the same pattern, with ILPP **60.152s**. The regenerated graph's ILPP tasks finish in **1.332s / 1.197s**; UP had no first-verification ILPP task.

The captured basis graph's response payload contains an older ILPP endpoint than the regenerated A/B graphs. Long-running Trigger processes use only about **3.734s / 3.891s sampled CPU**, each with **210,420 read bytes** and about **60,000 other I/O operations**. This supports a stale-endpoint retry/timeout hypothesis. However, the actual first invocation's response-file copy, Trigger stdout and internal stack are absent, so its exact connection target and timeout cause remain **unconfirmed**. Timestamp reuse making that old task runnable is also an inference, not an isolated causal result.

The next bounded investigation should establish that first invocation and a supported way to reject process-specific stale work before execution. Splinter already removes captured `Library/ilpp.pid` for independent process startup; it does not rewrite Bee DAG/response files. Sharing an original endpoint, weakening dependency checks, or editing caches speculatively is not justified. This is a potentially significant additional delay to investigate, not a proven 60-second saving.

### 1. Repeated Library availability checks and ordinary Close

The first 60-second provider trace contains **243.9419 sampled filesystem-callback thread-seconds**. Monitor entry accounts for **156.5559s**; `Directory.Exists` leaf samples account for **41.3988s**. All of those directory-existence samples include `OwnedDirectorySnapshotTree.EnsureAvailable`. The call runs through `JournalDeltaStore.Check` and `GraftedSnapshotTree.EnsureAvailable`, including Open, List, GetFileInfo and Close. The trace is labelled `early-scan` by the harness but overlaps scan/compile preparation; it is not a pure Scan interval.

A promising change is to pin the owned Library root and ancestors for the reader lifetime, then use that identity proof to avoid redundant path queries. Replacement, rename, reparse, lease/reclamation, content and I/O-error guards must remain. A cached boolean or time-based existence check is insufficient.

Ordinary linked-file Close also calls `ReleaseUnlinkedFile`, which performs full availability checking before deciding that no unlink reclamation is needed. A narrow fast return could retain local disposed/poison/journal-length checks under the gate and keep complete availability/serialization for actual reclamation. Removing the external retention lock without a lifetime proof is unsafe.

**150.1149s of Monitor samples omit named product frames**, so the exact lock cannot be established for those samples. Callback stacks and source suggest journal-gate contention for List/GetFileInfo/GetSecurity; Open/Close can also take the external mutation gate. These waits overlap across threads and are not removable wall-clock seconds.

### 2. Workspace preparation repeats source work

A's initial source inspection took **86.2119s**: 1,602 files, 2,804,769,928 raw payload bytes hashed, then the same byte count supplied to seven Git hashing processes. The response inspection took **12.2740s**, reused all 1,602 hashes and read **zero payload bytes**. Both observe the same fingerprint, provider epoch and sequence 2. Together they account for **98.4859s of A's 242.8974s preparation**, but not all of that work is removable.

`GitSourceFingerprint` computes raw SHA256 and Git's clean-content hash separately. A single pass can be considered only for files whose Git attributes/EOL/ident/filter semantics prove an identity transform; other files still require Git. An operation-scoped completed proof may also avoid the response's repeated metadata walk and manifest publication after checking unchanged HEAD/index/membership/interpretation/provider generation. Replacing content proof with timestamps or HEAD blob identity alone would weaken the contract.

The separate 30-second A-prepare trace includes `HashRangeSourceAsync` for 20.8479 sampled thread-seconds and plain-path checking for 5.0704s. File opens and repeated ancestor checks are further candidates for pinned-handle reuse. These inclusive sampled times overlap. Registration's `update-index --refresh` cost was not isolated.

### 3. Journal publication and output I/O during compilation

In the 60-second compile trace, Write accounts for **38.4287 / 44.4705 callback thread-seconds**. Monitor entry is only **0.4899s**. Publication has **14.2740s exclusive sampled residence / 20.5180s inclusive**, durable flush **9.1426s**, and file opening/handle closing about 4.9s/4.6s. The previous repeated journal ancestry-walk hotspot is no longer prominent.

`LatestJournalStorage.Publish` validates with `ReadFrame`, then `Index` reopens the same root. Reusing one validated handle is a narrow candidate while preserving namespace/block/hash/length/generation checks, atomic publication and each acknowledged mutation's durable flush. Samples do not establish that all Publish time is `File.Replace`, and no publication retry-sleep samples were observed. Group flush or weaker acknowledgement is not justified by this evidence.

### 4. Git reads are now a secondary target

Startup provider transfer reads decreased from **69.39 / 71.52GiB** to **28.15 / 24.75GiB**, and observed Git process identities from **839 / 832** to **49 / 48**. Win32 transfer counters include pipes, cache and device traffic; identities include launcher/child observations and are not decoder-start counts. The early trace contains about 3.28s of Git Read residence with no observed Git Monitor entry.

Retaining the first requested chunk during initial full-blob proof, preferring completed idle decoders, or moving reader retirement outside the shared gate may help specific patterns. Each needs bounded payload/proof memory and disposal/recovery tests. This input's estimated chunk-proof upper bound is about 1.36MiB, below the existing 4MiB / 4,096-entry limit; increasing the cache is not the leading explanation for current cost.

## Closure and measurement limits

Both normal stop commands exceeded the existing **120-second** limit. The same Editors exited normally after **287.4339s (4m 47.4s)** without another stop request or forced termination. This is slower than UP's 226.6012s and remains a separate problem.

A/B checkpoint took **59.8131s / 58.2084s**, release **102.0764s / 101.3695s**; the sequential preservation/release cohort took **321.4743s**. Original Library restoration took **114.7703s (1m 54.8s)**. The actual preparation-through-restoration interval was **2,279.6423s (37m 59.6s)**, including the diagnosis/build/commit gap; it is not a clean uninterrupted performance baseline. Stop failures and launch-admission failures are retained alongside successful subsequent operations.

Both tasks are `released`, their independent result trees match the unchanged source tree, and exact owned Editors/providers, mounts, task deltas and Git worktree registrations are absent. All 1,602 original tracked files are byte-verified; HEAD/branch/preexisting refs are unchanged. Library is an ordinary NTFS directory on its original volume. Fixture-local Git settings are restored, global settings are unchanged, both frozen runtime bundles retain their hashes, and all **27 unrelated dirty files** retain their pre-task hashes. **All 14 actual CLI responses**, including four failures, pass the current-schema validator (one targeted test passed). Conditional skips from the required build remain conditional, not newly qualified external inputs.

Raw evidence is in ignored `artifacts/unity-startup-remeasurement-20261008/` and `.local/us8/`. Provider samples were taken every ten seconds and owned process samples every two seconds. Two nonoverlapping 60-second provider EventPipe windows and one 30-second A-prepare CLI window used 16MiB buffers. Sampled thread residence, nested Unity/Bee scopes and CPU/I/O counters must not be added as exclusive wall time. The first trace's label does not qualify it as scan-only.

Cache state, scheduling and profiler overhead were not controlled by randomized repeats. The repair pause can affect cache state. A/B are parallel tasks, not statistical repetitions; this run does not isolate individual SU changes or establish a general Unity speedup. No extra full cohort was run to conceal the failed admission or slow stop. The remaining candidates above are analysis, not implemented or measured improvements.
