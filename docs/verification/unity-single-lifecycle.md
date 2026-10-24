# One-workspace Unity lifecycle comparison

[한국어](../ko/verification/unity-single-lifecycle.md)

**LU-01, 2026-10-24:** the [requested comparison](../plans/lifecycle-throughput.md) completed one real Unity workspace at a time, in before → after → after → before order. The observed complete-workflow mean fell from **359.5s to 300.0s: 59.4s / 16.5%**. Library preparation, workspace creation and restoration consistently account for **50.6s** of that difference. Editor startup varies enough that its mean difference is not established as a causal benefit of this change.

The [dataset](../../reports/benchmarks/unity-single-lifecycle-20261024.json) contains individual times, frozen assembly hashes, source proofs, sampled counters and closure receipts. Local raw evidence and the external controller are in `artifacts/unity-single-lifecycle-20261024/`. No overlapping A/B Editors or shared-Editor comparison is included.

## Fixed conditions and accounting

- Windows/NTFS, Ryzen 5 5600X (6 cores / 12 logical processors), 64GB RAM, Unity **6000.3.22f1**, GUI mode using product `unity start` and product-owned normal termination.
- Dedicated experiment source commit `fd611140aa0c22d66782c19b007f1a68c00af2a6`, using the existing MCP-free real-asset fixture. All **1,602 source files / 2,804,769,932 bytes** and **42,038 Library files / 3,105,800,655 bytes**, including every file's modification time, match before each run and after restoration. There are no excluded PID/cache files.
- All four initial loaded assemblies have MVID `e188b8a8-3bef-44f9-b3ac-161e371ee53c`. Each workspace's initial 1,602 source-content proof hashes match the independently hashed input. The journal is the current default bounded SJR2; the rejected LT-01 read/flush prototype is absent.
- The baseline is frozen `be761bb` **plus the common bounded-discard correctness repair below**. The candidate adds LT-02 Library worker changes. Both use the same repair; this is not a comparison against an entirely unmodified released binary.
- Time starts at `library prepare` and ends at `library restore`. One expression changes `Increment(3)` from 4 to 5. Explicit refresh, compilation callbacks, a fresh generation/MVID, current PID/path and ready state prove compilation. Selected model/texture/scene inspection and actual rendering follow. All four PNGs have SHA-256 `df94cefbc5f0d2fa6b46b878badb8848bc0221d66130778347349725a47b632d`; the rendered model was inspected.
- The completion branch is **explicit discard**, including normal owned Editor exit, source registration/delta/basis reclamation, then original Library restoration. Original source publication and independent-result checkpoint/release are not timed by LU-01.
- Experiment clone/backup creation and full input/closure hash proofs are outside timing. Captured-manifest equality checks inside the envelope are separately excluded: about **0.15–0.16s** per run. Remaining accounting differences are **0.006–0.009s**. Editor-start dispatch is inside readiness; exit milestones are inside discard. Neither is added twice. No builds/tests ran during the measured series.

## Results

| Stage | Before mean, s | After mean, s | Saved, s |
| --- | ---: | ---: | ---: |
| Library preparation | 60.9 | 43.8 | 17.1 |
| Workspace creation | 81.7 | 74.3 | 7.4 |
| Editor initial readiness | 105.6 | 95.8 | 9.7, variable |
| Code edit through confirmed compilation | 18.8 | 19.4 | -0.6 |
| Selected asset/scene/render inspection | 1.1 | 1.2 | -0.1 |
| Discard, including normal Editor shutdown | 43.7 | 43.9 | -0.2 |
| Original Library restoration | 47.7 | 21.6 | 26.1 |
| **Complete accounted lifecycle** | **359.5** | **300.0** | **59.4** |

Before totals are **372.9 / 346.0s**; after totals are **297.9 / 302.1s**. Library preparation is 61.0 / 60.7s versus 43.9 / 43.7s; workspace creation is 81.9 / 81.5s versus 74.3 / 74.3s; restoration is 47.2 / 48.1s versus 21.4 / 21.8s. The latter improves **54.7%**. These three stages consistently save **50.6s** together, unlike the startup range: **91.8–119.3s before**, **93.5–98.2s after**. The final baseline starts faster than either candidate run. The overall 16.5% is an observation of this matched series, not a universal or entirely isolated causal percentage.

## Remaining large costs

| Priority | Current evidence | Next investigation boundary |
| --- | --- | --- |
| Editor initialization and storage work | Candidate readiness **93.5–98.2s**. Initial refresh **80.7–85.2s** contains script compilation **33.0–33.9s** and domain reload **39.3–42.2s**, including InitializeOnLoad processing **23.5–27.2s**. | Locate expensive initialization reads/writes and callbacks before selecting a cache or algorithm change. Nested timings must not be summed. More Library workers do not directly parallelize Unity callbacks. |
| Git attachment and initial source verification | `attach-intent → ready` remains **48.3–48.6s** in all four runs. Approximately **2.805GB** has been read from the immutable basis before Editor launch. `GitOverlayRegistration.AttachAsync` performs index refresh; the subsequent source response reuses 1,602 admitted hashes and takes about 5s. | Profile the index-refresh/full-content path; evaluate bounded parallel validation and larger immutable RAM caches while preserving full content, generation and ownership proofs. The entire 48s envelope has not been attributed to one function. |
| Durable writes inside startup/use/shutdown | Candidate providers record about **26,400 flushes** and **54.3–60.8s** cumulative durable-flush time, mainly about **24,000 write mutations**. | This overlaps the lifecycle stages above. CPU/thread increases alone do not remove durable storage latency. Do not skip individual durable ACKs, revive the retired native Bee backend or attribute LT-01's synthetic result to Unity. |
| Normal shutdown | Exact process exit is observed **34.6–34.8s** after discard dispatch, inside its **43.7–43.9s** total. | Identify Unity-owned shutdown work separately if needed. No new debugger investigation was run; exit/handle guards remain. Source metadata capture is about 5.7s and individual seal reads about 0.04–0.06s, so those smaller operations are lower priorities. |

Workspace creation and Editor readiness together occupy about **170s** of the candidate's 300s lifecycle. Provider counters are sampled cumulative observations with publication/poll lag. Lock and mutation waits aggregate overlapping threads; they are not additional elapsed time or a guaranteed recoverable saving. The first baseline's flush cost was substantially higher than the later baseline's despite the same binaries, reinforcing the startup-variability boundary.

## Diagnostic correction and actual discard repair

The initial `00-before` diagnostic used freshly checked-out source modification times with the older Library. Its initial scan was **277.7s**, and readiness **469.9s**. It is excluded in full. Before the matched series, all source times were restored from the content-matching prior input and the frozen Alpha A/B timestamp records; Library bytes/times stayed fixed. Matched initial scans are **0.91–1.03s**. This demonstrates why byte equality alone did not preserve this warm-cache condition; it is not an additional product optimization result.

The external observer also initially compared GUI `renderPipelineBefore` against the batch reference's `uninitialized` value. Actual compilation/render succeeded, and the correct existing GUI reference matched. The response/failure are retained; no product guard was relaxed.

Actual discard then exposed a separate product defect: after normal exit and source removal, `JournalReclamation` routed bounded storage through append reclamation and rejected `delta.journal.bounded.lock` as unknown. It preserved the task at `discard-delta`. The repair dispatches to existing `BoundedJournalReclamation`, retaining the exact seal/inventory, pending publication, writer fence, Windows namespace proof and same-intent recovery. Mixed mode receipts are refused. The same task/id/home/intent/digest completed reclamation in **2.8s** after this repair; the diagnostic's original Library was fully restored. No file was manually deleted to bypass the refusal.

Both matched variants include that repair. The previous adapter regression matrix covered append/latest only; it now also covers bounded success, journal/guard removal, pending intent, foreign files, changed journal and unknown resources. The focused adapter suite passed **20/20**, and the existing inventory/reclamation suites passed **53/53** before two additional conflicting-mode cases were added to the full gate. The final required `pwsh -NoProfile -File scripts/build.ps1` completed locked restore and Release compilation with **0 warnings / 0 errors**, then **1,568 passed / 0 failed / 25 conditional skips (1,593 total)** in 8m 40.5s. These tests ran after the measured series.

All four measured Editors exit normally with code 0; all owned providers, mounts, private deltas and task registrations are absent. Original Library restoration and exact source/Library/refs/configuration are verified after each run. The canonical `splinter-unity` project remains clean at `b09fa58e9d40a560d4fe1af7a213f8ac23e8e3b1`; the old debugger attempt and unrelated resources were not reclaimed or reclassified.

After final source/Library verification and confirmation that the prior fixture retains the exact source commit and Library backup, the dedicated `.local/lu24-source` experiment clone was removed. This teardown is outside product timing. Frozen runtimes, raw evidence and task receipts remain; `fixture-cleanup-preflight.json` and `fixture-cleanup.json` record the exact target and completion.

**Limits:** two observations per version on one machine; OS caches are not cleared. One small code edit and selected assets do not qualify general gameplay, large code changes, Play/Test/build, original application, changed-original-Library restoration, cold storage, peak disk/RAM or external-tool support. The adopted change remains a substantial preparation/restoration reduction, not a several-fold whole-Editor acceleration.
