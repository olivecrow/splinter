# Actual Unity parallel workspace performance

The unchanged paged Library restoration changes **do not meet the below-half-public-time target**. On the fixed real Unity input, mean accounted time is **446.00s current versus 439.76s public**, or **1.4% longer**. Restoration takes **42.14s versus 40.66s**. All four final product lifecycles pass the specified compile/scene/Play, result and original-restoration checks. Host activity and sample ordering limit causal conclusions.

## Candidate, input and method

Measured on Windows on 2026-11-04 (Asia/Seoul). GitHub's default `development` tip was rechecked through the connector during this work and remained [c4fa5063964e7b8456de4ea7735c042fb9af8930](https://github.com/olivecrow/splinter/commit/c4fa5063964e7b8456de4ea7735c042fb9af8930). Its [verification index](https://github.com/olivecrow/splinter/blob/c4fa5063964e7b8456de4ea7735c042fb9af8930/docs/verification/README.md) records 1,691 Windows passes and 24 skips. Different test inventories do not establish a performance ratio.

Current is the unchanged paged restoration candidate following bbb3a85. All ten frozen Release runtime DLL hashes match both final current samples. The subsequent full regression rebuild matches all ten hashes; no product code changed after final measurement. Public uses its separately built matching-format binary. This compares the same workflow across different storage formats, rather than isolating one optimization commit.

The dedicated input uses Unity **6000.3.27f2**, URP **17.3.0**, **75 source files**, and **17,863 genuinely generated Library files totaling 1,568,757,165 bytes**. Its fixed local input commit is 7ee96cd995bcb77b345f0028cdcdd351a7a34417. Cold import and the user-approved template material upgrade precede the frozen input. No retained large fixture or existing game project was resumed.

Each sample starts from the same complete source/Library SHA-256 and length maps, clean Git HEAD and ordinary Library, with a fresh case/home identity. Final comparison samples are fast-published-1, fast-published-2, final-current-6, final-current-7. Earlier current candidates were interleaved during development, but later safety changes required new current samples: **the final comparison is public/public/current/current, not causal ABBA**. No final candidate sample was omitted because of its duration.

Preparation runs once per pair. A/B creation, complete source reads, Editor startup, code work, scene work, Play and final preservation/reclamation run concurrently. Normal Editor stop requests run A then B in both versions because an earlier concurrent-stop trial encountered a user-global layout sharing dialog. This remains agent scheduling through existing commands.

Both Editor PIDs/project paths match launch receipts. A compiles values (11,20), B (10,22), with changed assembly MVIDs. They save distinct scene markers/positions, run at least 15 runtime Update frames each, exit Play and recheck isolation. Expected source hashes are fixed before final stop. Product restoration and lifecycle dispatch gaps are included.

Excluded work is fixture copying/schema discovery, initial out-of-band input proof, post-release independent-result proof, and final out-of-band original hash/Git proof. These checks still execute and must pass. The measured pipeline body is byte-identical to the preceding actual Unity driver: SHA-256 ec371b6624ac918684d1487bcf3a94ee83875156a707cad13ecba4b85cb1f2a1. Only excluded setup was re-estimated before this block. There is no OS-cache reset, concurrent regression or timing trace during measurement.

The host has 12 logical processors and about 64GiB RAM. An unrelated existing Unity Editor remained running; its activity and other host activity were uncontrolled. A read-only snapshot during the first regression attempt, before the final current samples, showed 98% host CPU and that unrelated Editor consuming about 4.7 cores. This does not attribute a measured delay to that process. Two observations per version are not statistical or general throughput qualification.

## Measurements

| Accounted lifecycle | Public | Current |
| --- | ---: | ---: |
| First final sample | 398.64s | 512.58s |
| Second final sample | 480.88s | 379.43s |
| Arithmetic mean | **439.76s** | **446.00s** |

The half-public-mean threshold is **219.88s**. Neither current sample meets it. Restoration alone misses its half-public threshold of **20.33s**: public samples are 43.93s / 37.39s, current 43.43s / 40.86s.

| Phase, mean of two | Public | Current |
| --- | ---: | ---: |
| Library preparation | 53.70s | 47.23s |
| Concurrent A/B creation/admission | 57.15s | 33.61s |
| Concurrent complete source reads | 0.33s | 1.12s |
| Two Editors ready | 91.46s | 126.65s |
| Code refresh/compilation | 16.26s | 20.21s |
| Scene save | 2.66s | 1.77s |
| Play | 13.06s | 9.42s |
| Final source observation/proof | 1.34s | 0.88s |
| Sequential normal Editor stop requests | 102.98s | 132.41s |
| Concurrent result preservation/reclamation | 60.16s | 30.56s |
| Original Library restoration | 40.66s | 42.14s |

Creation saves 23.53s and preservation/reclamation saves 29.59s; restoration adds 1.48s. Editor readiness plus normal stop totals **259.05s** for current, exceeding the whole-workflow half target of 219.88s. A restoration-only gain cannot meet that target while the other observed costs remain.

Final current samples' excluded complete original hash proofs take 7.96s / 7.11s, versus 6.58s public mean. An earlier candidate sample reached 24.50s alongside slower capture/restore. These observations establish variation, not its precise CPU, disk, antivirus or native-lock cause. They do not justify dropping a slower completed final sample.

## Restoration changes and remaining cost

The preceding candidate routed unchanged originals above 1,024 manifest items into the general paged planner before unchanged-directory selection. It built GUID file records, name/range trees and durable per-file progress despite a zero-sequence journal and an intact root. Public already selected its unchanged-directory route. This unnecessary general planning explains a structural regression; the previous actual Unity block measured current restoration at 238.12s mean.

The current paged plan has an unchanged variant selected only after complete streamed namespace, metadata, base-path, length and empty-range equality. It retains one bounded cleanup/native-identity catalog and no GUID file/name/range trees. Existing fully hashed reader handles supply planning identities. Restoration transfers the exact root, reopens and pins every native leaf, hashes all bytes, proves complete membership, reapplies all metadata and durably records verification before exact journal reclamation. Restart repeats physical proof; a verified cursor never authorizes skipping it. Native ancestor protection spans reader handoff. See [Library lifetimes](../git-library-reference.md).

Independent leaf opens/hashes and metadata writes run in bounded batches with up to eight workers, joining before ownership can close. Held directory handles are reused for metadata. General paged planning bulk-builds names through bounded sort/spool, reuses one parent prefix only while the exact logical root and immutable native-read epoch remain valid, and merges sorted per-directory membership instead of repeatedly resolving each leaf from the root. Eviction, write-sharing, disposal and logical-root changes revoke derived prefix proof. There is no project-sized decoded metadata cache or unchanged-payload copy.

On the same 1,025-file deep native reproduction, restore-only time fell from **6.743824s to 2.377591s**, with every byte, native file/root identity and ancestor-movement assertion retained. The final 35-case focused selection passed in 107.685s. A deliberately failing handoff test demonstrated an ancestor-protection gap before the explicit handoff lease was added; it passes afterward. The added parent-prefix direct-lookup reproduction also failed before its separator fix and passes afterward. This component improvement does not qualify the complete Unity workflow.

The new actual restoration mean of 42.14s is descriptively 82.3% below the preceding block's 238.12s. Those blocks differ in host activity and ordering, so this is not an isolated causal estimate. Against public in this block, current is **3.6% slower**.

Post-hoc plan/cursor timestamps give approximate wall intervals, not exclusive function profiling:

| Restoration interval, mean | Public | Current |
| --- | ---: | ---: |
| Start through plan publication | 22.58s | 17.93s |
| Plan through verified cursor | 14.85s | 23.81s |
| Remaining completion | 3.22s | 0.40s |

| Own CLI CPU, mean | Public | Current |
| --- | ---: | ---: |
| Library preparation | 37.28 CPU-s | 55.23 CPU-s |
| A/B creation sum | 16.66 CPU-s | 17.66 CPU-s |
| A/B preservation/reclamation sum | 19.91 CPU-s | 41.16 CPU-s |
| Library restoration | 41.30 CPU-s | 73.99 CPU-s |

Current restoration averages about **1.76 own-CLI cores** despite its eight-worker limit. Ordered catalog production/traversal, metadata decoding, native opens and durable coordination remain costs around the parallel leaf work. Complete journal comparison occurs across distinct physical reader scopes; sharing a logical seal across those scopes would not preserve backing proof. Adding workers alone has not established a gain. CPU counters exclude child/provider processes and do not establish whole-host utilization, peak RSS, disk saturation or a particular wait owner. The complete pipeline is not proven optimal.

## Correctness, failures and limits

All final samples preserve separate A/B results, compare every expected source hash, prove absent task providers/mounts/deltas and zero owned counters, and restore all original source/Library hashes, clean original HEAD, one Git worktree and ordinary NTFS Library. Public results are independently read through Git archive bytes. Current results use the complete paged file catalog against expected hashes; product capture/reclamation performs physical backing proof. No result was applied to the original.

An earlier development attempt (final-current-2) failed B capture with WorkspaceMaintenancePending / MetadataMaintenanceIO during atomic range-maintenance replacement. The exact external holder was not identified. The exact returned maintenance key was resumed in 0.62s, the same checkpoint completed in 4.99s, complete result proof passed, restoration completed in 37.85s and complete original proof passed in 7.20s. That failed attempt/recovery is excluded from normal timing means and remains retained. No blanket I/O retry, force-kill, timeout increase or state edit was introduced.

Phase budgets were fixed before measurement: excluded fixture setup 90s estimate / 180s stop, capture/reclamation 180s / 360s, restoration 240s / 480s, sequential normal stops 120s / 240s. These estimates come from preceding observed aggregate costs. Product normal stop retains its separate two-minute bound. The earlier 90-second restoration limit was an external harness stop, not a product deadline; its overrun remains a failure in historical evidence. Larger harness budgets are not optimization gains.

All eight final Editor logs have zero C# compiler errors and normal cleanup. All eight contain Unity's internal Search database startup indexing exception. The code/scene/Play probes pass; this is not clean-Console or Search qualification. Post-measurement process inspection found no experiment-owned or gate test processes. No in-progress write was force-killed and no original/task/basis/result/request-key/digest/intent/protection state was edited or deleted to escape a timeout.

## Regression and evidence

The mandatory complete `pwsh ./scripts/build.ps1` gate passes: locked restore, Release compilation with zero warnings/errors, **2,618 discovered = 2,582 passed + 36 conditional skips**, zero failures/errors/timeouts/aborts. Whole-command time is 671.32s against a 1,080s estimate / 2,160s stop. The checked-in eight-class schedule, complete inventory, individual cancellation/timeout bounds and large-catalog opt-out are unchanged. All ten rebuilt runtime DLL hashes match both final current Unity samples.

The preceding complete attempt failed: two existing ten-second methods (sparse forced packing and sixteen-reference proof reuse) timed out, producing one additional cleanup failure. Its terminal summary records 2,619 results including that synthetic cleanup, and its TRX reporter also failed. The raw log remains evidence; an empty TRX is not a passing gate. Three isolated repetitions with unchanged inputs, proof counts and timeouts passed all nine cases in 23.59s total. Host contention was observed, but its precise contribution was not isolated. Neither test was deleted, serialized, resized or given a larger timeout. The failed 1,019.38s attempt remains failed; the subsequent gate supplies the completed result above.

Player builds, Unity Test Runner suites, general Plastic/None native performance, large existing games, peak RSS/allocation, disk saturation and OS/power-loss behavior remain unqualified. Large-catalog opt-in was disabled and retained 100k/17GiB fixtures were not resumed. See [validation scope](README.md).

Ignored evidence is under artifacts/unchanged-library-20261104/: summary.json, matched-plan.json, summarize.py, focused/full-gate logs and the four final case directories. Failed and superseded candidates remain diagnostic evidence, including final-current-2/recovery-proof.json; none contributes to final means. Complete-gate TRX is under artifacts/test-results/. Fixture homes/bases/results and matching binaries remain under .local/; generated-output status does not authorize deleting lifecycle state.

To reproduce from the retained frozen input, use the measurement driver under artifacts/unchanged-library-20261104/measure-unity.py with `--runtime <absolute-splinter.dll>` and `--name <fresh-name>`, read that binary's schemas and retain returned identities. Re-estimate before a new block, use fresh case identities and stop safely at the declared boundary. This does not authorize integration, deletion or blind retry of uncertain writes.
