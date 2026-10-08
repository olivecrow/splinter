# Actual Unity startup profile

[한국어](../ko/verification/unity-startup-profile.md)

**UP-01** profiles one real A/B startup on committed candidate `8709ba27574a2de8d0ecaee1a1413f42358d53ec`, after [JS](journal-startup-optimization.md) and [ST](startup-time-optimization.md). Initial readiness is **766.9024 seconds (12m 46.9s)**, versus [UC-01](unity-checkpoint-release-remeasurement.md)'s 848.3379 seconds: an observed **81.4355-second / 9.60%** reduction. Most remaining time is in scanning, not C# compiler computation. The [dataset](../../reports/benchmarks/unity-startup-profile-20261008.json) preserves exact counters, trace qualifications and lifecycle evidence.

## Input and measurement boundary

Unity **6000.3.22f1**, source `b09fa58e9d40a560d4fe1af7a213f8ac23e8e3b1`, 1,602 tracked files / 2,804,772,822 bytes; two independent Editors using `git-snapshot` and the original Library reference. All **44,860 Library entries** match UC exactly, including payload hashes, paths, sizes, attributes and timestamps. Manifest hash differences are exclusively snapshot ownership/path/capture fields. The normal Release Runtime SHA256 is `3661d876c091b774965ecaddcbd9be080ec432378f561ee843647d5519b50537`; binaries were frozen before execution.

The unchanged external fixture verifies each returned path, owned PID/start identity, fresh noncompiling/nonupdating state and expected code values. Splinter's heartbeat is not treated as compilation proof. This run performs initial startup only: no source edits, refresh experiment, Play Mode, tests or game build. The original source and installed packages are unchanged.

Provider observations run every ten seconds; ordinary-user Win32 CPU/I/O/private-byte samples run every two seconds. Three nonoverlapping 60-second EventPipe windows trace A's provider during early scan, late scan and compile preparation, with a 16MiB buffer. Command-line metadata identifies only observed owned processes. No administrator/UAC action or service update is used. Raw evidence is under ignored `artifacts/unity-startup-profile-20261008/` and `.local/up8/`.

UC did not have external EventPipe sampling. Cache state, scheduling and profiling overhead are not controlled by randomized repeats. Thus the difference is **one observed cohort comparison**, not an isolated causal percentage or a general Unity guarantee. A/B are two tasks, not two independent benchmark repetitions.

## Where the startup time goes

| Measurement | UC A / B, seconds | UP A / B, seconds |
| --- | ---: | ---: |
| Both Editors initially ready | 848.3379 cohort | 766.9024 cohort |
| Scan | 448.3302 / 448.1171 | 446.9486 / 447.5970 |
| CompileScripts | 250.8740 / 251.5398 | 189.7586 / 192.4327 |
| ImportInProcess | 17.544 / 16.869 | 11.8075 / 11.7899 |

Unity's phases are nested and sometimes overlap; **do not sum this table**. A's initial AssetDatabase refresh is 732.187 seconds. Its Bee log separates initial verification (**14.58s**), graph construction (**68s**, rounded log duration), and backend execution (**97.26s**). Historical A graph construction/backend execution were 90s/131s. CompileScripts includes graph generation, I/O, post-processing and waits, not just C# compiler CPU.

## 1. Git reads serialize the scan

Each scan trace contains about 480 aggregate filesystem-callback seconds across concurrent threads. Monitor entry accounts for **419.399s / 419.811s**, or **87.35% / 87.44%** of that callback residence. The nearest visible product frame is `GitObjectSnapshotTree.Read` for **98.54% / 99.93%** of those waits. Source inspection maps this to the Git tree's own `readGate`; ST's independent Library reads do not remove that gate.

This is overlapping **thread residence**, not CPU utilization or 87% removable wall time. Matching windows observe approximately 12 provider CPU seconds and 5 CPU seconds in sampled Git children per 60 seconds. Both corresponding inner provider intervals contain **zero writes, flushes and journal appends**. Journal serialization is not the cause of these scan windows.

The active Git path spends approximately 30–32 sampled seconds in `ReadByte`/header handling, 18–21s in buffered reads, 3–4s in process start and 2–3s each in hashing and reader retirement. Header residence can include waiting for Git to produce output; batching header reads cannot be credited with saving all of that time.

Startup provider read transfers total **69.39GiB / 71.52GiB**, while logical basis reads total **4.416GiB / 4.349GiB** in the bounded provider intervals. Transfers include pipes, devices and cached file reads; they are not physical disk bytes or the missing `DecodedBlobBytes` counter. The code's backward-prefix decoding and two-session eviction policy are concrete amplification candidates. The 839/832 sampled Git identities include launcher/child pairs and missed short processes: they are **not decoder-start counts**.

## 2. Build preparation still waits on storage

Bee's finer A trace records **67.550s BuildProgram**, including **66.500s Backend.Write**. The graph-building child uses about **1 CPU second** despite that wall duration. `JsonToString` is nested (3.94s), not an additional stage. Observed provider CPU is 307.1s/299.4s during startup, including 217.0s/213.5s kernel CPU. These sampled lower bounds and Bee traces point to substantial storage/system-call work rather than expensive graph computation alone. A compiler-server process may serve both tasks, so child compiler CPU is not a per-Editor compilation total.

The compile-preparation trace contains 192.341 callback-thread seconds; 105.190s is Monitor entry. Security/open/close/cleanup/metadata callbacks wait while mutation work proceeds. Non-Monitor residence includes approximately **21.874s flush**, **16.701s `EnsurePlainPath`**, **12.606s `Publish`**, **6.133s `ReadLatest`**, and **2.913s Brotli**. These are sampled leaf contexts, not additive wall-clock stage costs. The repeated `PathOf` → `EnsurePlainPath` ancestry walk is a concrete next target; publication also includes more than one internal operation.

Initial logical journal puts fall from about **3.854GiB to 1.596GiB**, and A write work from **251.1s to 182.7s**. These are bounded live counters, not physical disk allocation. The remaining flush/ownership/publication costs explain why reducing metadata bytes alone does not proportionally reduce elapsed time.

## 3. Separate necessary invalidation from avoidable work

The historical 27 reimported source assets differ only through CRLF conversion: 17 terrain layers, five animations and five controllers. Their individual import lines total about two seconds; all seven original C# source files already equal their raw Git blobs. Those 27 assets do not explain the 447-second scan or broad script preparation.

The captured original/current Bee inputs differ in 74 assembly paths, one added Editor source (`WorkspaceBridge.cs`) and the private IL post-processor endpoint. B's actual DAG payload comparison finds **216 changed payloads**: 147 differ only by original-to-task project-root substitution, 68 only by the owned ILPP pipe, and one adds the bridge to the Editor response file. Both tasks still execute 147 WriteText, 68 WriteResponseFile, 67 ILPostProcess, two Csc and four CopyFiles actions. Changed payload counts and executed action counts are different measurements.

The original Editor's endpoint must never be reused by another task. These are real cache-input changes, not evidence of 67 changed package source assemblies. Per-action Bee "why" records were not captured, so the runtime Csc trigger is not attributed solely to the Editor bridge. Bee diagnostics are preserved locally; external package/compiler/cache internals are not patched.

## Follow-up priorities

1. **Git decoder scheduling and reuse.** Measure exact offset/restart/decoded-byte counters, then separate short proof/LRU bookkeeping from per-reader operations so the existing two readers can progress independently. Preserve in-flight proof/payload budgets, same-object serialization, corruption refusal and close/reset draining. Retaining a verified requested chunk across same-object restarts may help header/backward probes; it cannot remove the first full proof and can hurt other access patterns. A bounded single-read header comparison is a smaller candidate, with benefit still unmeasured.
2. **Content-proven source metadata.** A durable binding of raw Git blob identity, eligible original timestamps and Library basis could prevent unnecessary rechecks for matching files. Current Git entries use snapshot capture time. Do not blindly copy mutable-source timestamps or apply them to CRLF-different bytes. This needs a provenance contract and stale/changed-source guards; the entire scan is not promised removable.
3. **Repeated journal path verification and no-op cleanup waits.** Reuse an ownership/ancestry proof only while exact root/ancestor handles and the relevant generation stay pinned. `WinFspOverlayFileSystem.Cleanup` currently takes the mutation gate even without `CleanupDelete`, when its only remaining work is interlocked handle bookkeeping; conditional acquisition is a narrow candidate with handle/close/stop ordering tests. Its 18.556 callback-thread seconds of waiting overlap other waits. Keep replacement/reparse/identity refusal, actual delete/context serialization and durable publication/ACK boundaries; simply deleting checks or broadly removing locks/flushes does not follow from this evidence.
4. **Isolate the generated Editor bridge.** A dedicated Editor-only assembly with automatic references disabled may reduce bridge-triggered predefined-assembly compilation; Unity documents this behavior in [assembly references](https://docs.unity3d.com/6000.3/Documentation/Manual/assembly-definitions-referencing.html). It does not eliminate independent Editor endpoints, path changes, Git scanning or all package post-processing. A warm restart of the same workspace is a separate, unmeasured workload.

These are analysis findings, not implemented or measured follow-up speedups. The first three directions target user-mode work and do not require a UAC/service change.

## Closure

| Lifecycle stage | Seconds |
| --- | ---: |
| Original Library preparation | 155.6209 |
| A / B workspace ensure, sequential | 237.1078 / 233.1627 |
| Concurrent Editor initial readiness | 766.9024 |
| Same normal Editor shutdown | 226.6012 |
| A checkpoint / release | 59.9193 / 103.8614 |
| B checkpoint / release | 58.7054 / 100.1621 |
| Original Library restoration | 113.9665 |
| Complete preparation through restoration | 2,057.5467 |

Thus fresh Library/workspace setup adds **625.8914s (10m 25.9s)** before the Editor timer. The full startup-only lifecycle takes **34m 17.5s**. It excludes UC's later source-edit/refresh/asset-inspection workload; do not compare whole-lifecycle totals as identical workloads. Offline analysis and diagnostic copying also overlap post-startup cleanup, whose timing is contextual rather than an isolated optimization benchmark.

Both tasks are `released`; independent result trees equal the unchanged source tree. Owned Editors/providers, mounts, task deltas and Git registrations are absent. The original 1,602 source files are byte-verified, source HEAD/branch/preexisting refs remain unchanged, and Library is ordinary NTFS on its original volume. Temporary fixture-local Git configuration is restored and global settings are unchanged.

Both stop commands return **EditorStopTimeout** at approximately 120 seconds; the same normal exit completes at 226.6012s. Failed responses remain failed, with no repeated stop or forced termination. All **12 actual CLI responses** pass the existing current-schema validator (one targeted test passed). Before this run, the committed candidate passed the required build with **1,315 passed, 0 failed, 19 conditional skips**. All 27 unrelated dirty files are preserved. New Play/Test/build, physical disk peak, whole-machine RAM peak and OS-failure qualification remain **NotRun**.

Current implementation follow-up belongs to [SU-01–05](startup-profile-optimization.md). The candidate, inputs and timings recorded here do not qualify that new implementation.
