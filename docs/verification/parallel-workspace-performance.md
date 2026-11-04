# Current parallel workspace performance

Measured on 2026-11-04 (Asia/Seoul; UTC run date 2026-11-03). Fresh editorless native CLI A/B workspaces take 53.6% less time with concurrent preservation/reclamation and 54.7% less with serial preservation/reclamation than the published version. Both means are below half the published time. The complete regression gate passed 2,565 cases with 36 conditional skips and zero failures. Actual Unity Editor and large-project throughput remain unqualified.

## Versions and published evidence

GitHub's default `development` tip was rechecked through the connector at 2026-11-03 18:19:41 UTC as [`c4fa5063964e7b8456de4ea7735c042fb9af8930`](https://github.com/olivecrow/splinter/commit/c4fa5063964e7b8456de4ea7735c042fb9af8930). The current working tree is based on `4ebb59d`, 35 commits beyond that tip. This compares the same user workflow across versions; it is not an isolated optimization-commit ablation.

The [published verification index](https://github.com/olivecrow/splinter/blob/c4fa5063964e7b8456de4ea7735c042fb9af8930/docs/verification/README.md) records 1,691 Windows passes, 24 skips and zero failures. Test inventories differ and do not establish a speed ratio.

The latest published actual Unity A/B record, [TP-06](https://github.com/olivecrow/splinter/blob/c4fa5063964e7b8456de4ea7735c042fb9af8930/docs/verification/unity-throughput-optimization.md), failed experimental cache-retention reclamation. Its completed phases total 1,022.1s; a separate shared-Editor workflow completed in 313.2s. The former is a subtotal. Cache retention was withdrawn before publication, and no successful whole-Unity timing for the final public product supports a historical whole-Unity ratio.

## Matched native measurement

Both versions use Windows 11 Home 10.0.26300, Ryzen 5 5600X (6 cores / 12 threads), 68,628,553,728 bytes RAM and NTFS. The public source was extracted from the verified commit and built separately with locked restore, SDK 10.0.112 and Release configuration. Build time is excluded; each run retains its runtime DLL hashes.

Each fresh project has 64 committed 1KiB source payloads, four source/configuration files and an ignored 128-file / 128KiB Library. All eight inputs have Git commit `eccc9dbbee8e882c8b1b31ed1315a1805b180382` and identical source/Library SHA-256 maps. No Editor runs.

The workflow prepares a Library basis, creates A/B concurrently under one home, reads/verifies all source input, then concurrently writes distinct source results and 128 one-byte Library changes per workspace, flushing every write. Complete changed Library bytes and peer exclusion are checked. Each result is preserved and reclaimed, independently verified, and followed by original Library restoration. Final original hashes/HEAD/clean status, one remaining original Git worktree, absent task providers/mounts/deltas and zero owned resource counters are asserted.

Two modes use the same input: serial A-then-B preservation/reclamation, and concurrent preservation/reclamation. Each mode runs public → current → current → public with fresh homes and no concurrent regression suite. No OS-cache reset is performed. Two samples per version are observations, not statistical or hardware-saturation qualification; every final sample is included.

Schema/fixture setup, status diagnostics, post-release independent-result proofs and final out-of-band original hash/HEAD checks are excluded from the accounted pipeline. Product restoration and ordinary lifecycle dispatch gaps remain included. Each CLI's exit-time CPU accounting is recorded in both versions; Git children and providers are excluded from those CPU totals. Product timing logs and tracing are disabled in matched runs. Each cohort has a two-minute estimate and four-minute stop; a command exceeding the harness's 60-second response bound stops new stages and preserves state for same-request recovery. The eight-cohort batch took 606.04s against a ten-minute estimate and twenty-minute stop.

## Final measurements

Each cell is the arithmetic mean of two complete `round5-final-*` observations. All eight input maps, lifecycle proofs and per-version runtime hashes passed verification. All ten current runtime DLL hashes still matched the measured binaries after the full gate rebuilt them. Earlier candidates that missed the target are excluded because their code differs; no earlier public sample is substituted into this block.

| Preservation/reclamation | Public mean | Current mean | Reduction | Public half target | Margin below half |
| --- | ---: | ---: | ---: | ---: | ---: |
| Concurrent A/B | 83.93s | 38.96s | 53.6% | 41.97s | 3.01s |
| Serial A then B | 107.78s | 48.87s | 54.7% | 53.89s | 5.02s |

Concurrent public samples were 87.01s / 80.85s and current samples 39.23s / 38.68s. Serial public samples were 107.02s / 108.54s and current samples 48.65s / 49.10s. Every current sample is below its mode's half-public-mean target; the small sample count does not establish a general speed guarantee.

| Concurrent lifecycle phase | Public mean | Current mean |
| --- | ---: | ---: |
| Library preparation | 8.55s | 2.66s |
| A/B creation and initial admission | 35.64s | 16.79s |
| A/B reads and durable writes | 3.69s | 1.16s |
| A/B preservation and reclamation | 35.19s | 16.86s |
| Original Library restoration | 0.86s | 1.48s |

Serial preservation/reclamation fell from 61.74s to 27.30s. Creation/admission and preservation/reclamation still account for about 86.4% of current concurrent pipeline time. Restoration remains about 0.62s slower than public; its cause is not isolated by these cohort timings.

## Implemented efficiency and retained verification

- Independent read-only Git membership queries overlap in bounded groups: four initial queries and two final queries, with all children joined and original failure precedence retained. Final HEAD/index/interpretation observations remain after the final query pair. Fixed commit/tree IDs use one Git command, while complete raw commit/root/subtree proof remains mandatory.
- Comparison-only source membership consumes the complete sorted/deduplicated input into the existing checksummed bounded spool. A framed private SHA-256 compares that spool; the paged lookup catalog is constructed only when lookup is needed. Empty input allocates neither scratch nor a catalog. Path, collision, UTF-8, length and page-limit rejection remains before comparison can succeed. Persisted fingerprints and product formats do not change.
- Directory-record transforms use the same pure encoder/decoder and validation without opening empty writable metadata stores solely to encode records. Immutable source scans retain bounded CPU-sized hash workers and overlap enumeration, hashing and catalog input. Spool completion precedes the home-wide metadata writer lock; durable catalog/index publication remains serialized.
- Retained Git read admission obtains access through the existing fenced scope, removing a duplicate pre-fence observation. Complete native ancestry/name/ref/object proofs remain inside that scope, and later publication/release boundaries retain fresh observations. Read-only page/index proof sharing is limited to the exact fenced call.
- One backend observation reuses the project observation already obtained inside its control-before/after proof. Generated Library availability stays separate. Preliminary ownership states that do not enter that proof still obtain a fresh project observation. No observation is cached across calls.
- Capture/materialization polling retains full provider proof immediately before publication and periodically while waiting. It avoids immediately repeating that proof on the first empty response, uses five 20ms initial waits followed by 100ms waits, and keeps response token/generation/error/adoption checks. Under the exact managed-basis gate, known-absent control files avoid an unnecessary publication grace; real native sharing/lock conflicts still wait for publication.
- Reader/collector admission and teardown retain exact-generation verification, native ancestry guards and all enclosing fences until admission release. Concurrent peers keep their original source/result ownership. No durable flush, complete byte/hash/page/inventory proof, result seal or recovery boundary is skipped.

An unpublished runner-cache directory can briefly fail its atomic publication rename while a child file has an ordinary reader without delete sharing. A one-file native reproduction proved Windows error 5 while held and immediate success after release. Publication now retries only that same completed temporary directory to the absent target for at most one second on native errors 5/32/33; persistent denial preserves the source and normal complete hash validation remains. It neither recopies the bundle nor changes permissions, overwrites a target or increases an operation/test timeout. The original intermittent external holder was not identified.

Public command fields and current persisted formats are unchanged. See [Git basis](../git-project-basis.md), [storage](../scalable-storage.md), [workspace observations](../ntfs-workspaces.md), [Library preparation](../git-library-reference.md), [runner resources](../storage-and-resources.md) and [result lifetimes](../repeated-original-apply.md).

## CPU, waits and remaining cost

| Own CLI CPU, concurrent mode | Public mean | Current mean |
| --- | ---: | ---: |
| A/B creation sum | 12.73 CPU-s | 11.16 CPU-s |
| A/B preservation/reclamation sum | 13.66 CPU-s | 17.61 CPU-s |

Completion uses more own-CLI CPU while its wall time falls from 35.19s to 16.86s. Creation also removes repeated work. These counters exclude Git children and native providers: they do not measure whole-host utilization, prove every core is saturated or identify kernel/disk limits. Worker pools remain bounded by existing CPU/I/O budgets; increasing all concurrency limits was not assumed to improve performance.

Small 4/8-file reproductions retained the same ownership, full-byte, independent-result and restore proofs. A source-query-only change improved its measured source inspection but did not improve overall wall time, so it was not presented as a lifecycle win. The final small reproduction took 34.92s before the fresh matched 64/128 block. A separate 512-read / 16KiB handle-options probe showed no consistent gain from changing the asynchronous-open flag at concurrency one/eight; that change was rejected.

Small diagnostic stage timings still put independent retained Git admission around 1.5s, retained/result release proof around 0.9s each and metadata maintenance around 1.1s per task. These nested timings are excluded from matched runs and must not be added to cohort wall time. Remaining creation cost includes provider/process startup and initial physical admission; completion retains independent basis/result verification and serialized metadata publication. This measurement does not establish that every remaining path is optimal. Required physical proof, durable publication and ownership cannot be replaced with cross-call caches or omitted checks.

## Regression and failure handling

Focused checks cover the new comparison spool/lazy lookup and exact invalid-input rejection, empty-source index-only changes, retained Git read admission, capture/lifetime proofs, basis-gated publication reads, project/Library observation separation and temporary/persistent runner-reader conflicts. The final focused selections passed 51, 59 and 45 cases respectively; they overlap and are not a combined case count. Two expected-red invalid-path cases exposed changed exception type/code and were corrected before the final nine-case membership selection passed.

The required complete `pwsh ./scripts/build.ps1` gate passed on the measured candidate: locked restore, Release build with zero warnings/errors, and 2,601 discovered cases (2,565 passed, 36 conditional skips). The final TRX records zero failures, errors, timeouts and aborted cases; all fifteen added cases passed. The test runner took about 10m34s (TRX execution span 633.38s), and the whole command took 641.37s (10m41s). Its thirteen-minute estimate was based on the previous 777.66s complete gate and the added cases; the stop boundary was twenty-six minutes. No individual test timeout or verification strength was relaxed. No gate-owned test/CLI/probe processes remained after exit.

An earlier candidate's runner preparation failed before either workspace was created. The exact project/key/home/basis was recovered with the fix, then normally restored in 2.69s; complete original hashes/HEAD/clean state and absent workspace/provider ownership passed. Its unpublished eight-file nonce cache remains retained. No original/task/basis/result/request/digest/intent state was edited or deleted to escape a failure. The two analyzer-failed builds during that minimal fix exhausted its declared preparation window; that overrun is retained as a stop-policy miss, not a passing phase. A subsequent minimal two-case runner reproduction and the focused selections passed before matched measurement resumed.

Earlier interrupted requests remain subject to same-request recovery and their original preservation obligations; successful current measurements do not identify historical external I/O holders or turn canceled/overrun runs into passes. All eight final cohorts completed normally, restored their original state and proved absent owned resources before the full regression started.

## Reproduction and scope

Ignored final evidence is under `artifacts/parallel-ab-20261104/`: `summary.json`, `final-batch-run.json`, `round5-final-*.log`, the matched driver/summarizer and final evidence checker, focused TRX files, `full-regression.log` / `full-regression-run.json` and `post-gate-processes.json`. The reusable local harness and raw `round5-final-*` per-run schemas/responses/hash maps/runtime hashes/CPU records are under `artifacts/parallel-ab-20261103/`. The full gate TRX is under `artifacts/test-results/`. The public build and fresh fixture homes are under `.local/parallel-ab-20261103/`. Independent results and matching-format binaries remain retained; generated evidence is not product state authority.

Use `python artifacts/parallel-ab-20261103/measure-cpu.py --runtime <absolute-splinter.dll> --name <new-name> --files 64 --library-files 128 --completion serial|parallel`. Read each version's own schemas, use returned workspace paths and keep request identities on recovery. The tool does not authorize integration, deletion or retries of uncertain writes.

Retained 100k/17GiB fixtures and real Unity projects/Editors were not resumed. Actual Unity compilation/Play/build, Plastic/None native matrices, peak RSS/physical allocation, disk saturation and OS/power-loss behavior remain unqualified. The complete default functional gate does not qualify those workloads.
