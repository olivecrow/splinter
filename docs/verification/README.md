# Validation scope

The current candidate uses task v4, managed basis v3, product overlay v3, file-result manifest v4 and metadata-store format 2 (`SPM2`). Current storage is described in [scalable storage](../scalable-storage.md). Earlier candidate results do not qualify current code automatically.

## Reproducible checks

Default xUnit execution uses eight concurrent classes with conservative scheduling. Tests reserving the entire process-wide scratch budget use an exclusive collection. Existing per-test timeout/cancellation bounds are unchanged. The default paged-store/catalog matrix uses 32 and 1,024 entries across each shape; graph-copy tests force disk reachability/relocation with a zero memory budget. `SPLINTER_LARGE_CATALOG_TESTS=1` explicitly adds 32,769 / 262,145 / 1,048,576-entry capacity inputs and needs a separate estimate/stop scope. Default-suite success does not qualify those sizes.

Run `pwsh ./scripts/build.ps1` for locked restore, Release compilation and automated regression. `-NoRestore` reuses an existing locked restore. CI runs the Windows suite and Linux compilation; Linux runtime support is not qualified. Focused tests, interruption harnesses and reusable diagnostics remain under `tests/`, `scripts/` and `tools/`.

Set estimates and stop boundaries before execution, following [AGENTS.md](../../AGENTS.md). Stop safely when a phase exceeds twice its estimate; do not run large fixtures to validate routine edits. A focused pass is not a full gate.

## Remaining qualification and retained state

- [Current parallel workspace measurement](parallel-workspace-performance.md) compares the published tip with the current code on identical fresh native A/B inputs. The small current concurrent-completion pilot hit a shared metadata lease conflict; exact-request recovery passed. Matched serialized-completion measurements are 18.7% slower overall and do not qualify whole-Unity performance.
- SCAL-01–08 qualification is incomplete. Full SCAL native lifecycle/fault qualification, 17GiB, comprehensive 100k original publication/reclamation, matched general performance, peak RSS and OS/power-loss behavior remain unqualified.
- Small historical Git/None/Plastic and Unity runs were candidate/input-specific. They do not establish universal support, speedup, memory saving, parallel Play/Test/build performance or long-session break-even.
- Large verification was curtailed after slow 100k phases. A separately authorized None recheck completed one 99,990-file write/capture/read-only-plan sequence; it did not publish original files, create a fresh causal ABBA comparison or complete the three-provider matrix.
- The three retained 100k providers were normally stopped. Original/task/basis/result/request/digest/intent/private state was retained; the managed original was not restored. Do not automatically resume or delete these fixtures. Their local state and matching historical binaries are outside disposable report cleanup.
- A previous current-source focused selection exceeded its 20-second limit at 26.835 seconds. Removing its historical report does not turn that overrun into a pass or waive the stop rule.
- Unsupported prior formats and encoded journal payloads are preserved and refused. No automatic migration, reclamation or old-format decoder is provided. Existing fixtures requiring an earlier binary must use that exact binary.

## Evidence policy

Keep new verification notes in English and record the candidate, input, command, outcome, timing limit and material unexecuted scope. Prefer reproducible tests and a concise current limitation over copied logs, historical screenshots, repeated test counts or parallel English/Korean reports. Earlier reports are available through Git history; they are not current qualification claims.

## Regression maintenance (2026-11-03)

The failure reproductions identified these issues without requiring a runtime behavior change:

- Observation/response schemas retained a 32MiB ceiling for process-wide metadata counters while the current policy allows up to 2.5GiB. Both schemas and the discovery description now match that policy. The regression checks 32MiB+1, the current ceiling and invalid values.
- Scratch-pressure packing cases reserved the entire shared budget while unrelated sorts/spools used it. Only those two pressure/recovery cases now run exclusively. Repeated schema compilation uses immutable class catalogs. Stopped-reclamation cases reuse one real immutable Git basis while retaining fresh journals, intents, receipts, native readers and ownership/seal verification within their unchanged ten-second limits.
- Killed-reader recovery now drains child output and proves native file leases have released before asserting reclamation. The wait is bounded to one second and retries only actual sharing/lock errors. A deliberately retained reader also proves deferral. Native directory identity, complete source bytes and runtime preservation guards remain checked. The precise external owner of the intermittent full-load I/O conflict was not identified; temporary tracing was removed.
- Eight source-fingerprint files previously formed one serialized partial test class. Independent behaviors now have separate classes and private mutable Git fixtures. Discovery remains exactly 2,579 cases after splitting the classes. A mixed reproduction confirmed scratch exhaustion still runs exclusively. An experimental collection-priority order did not improve full-suite throughput and was removed; native samples did not establish a benefit from changing the bounded eight-class default.
- A paged-restore selection test created 32,769 private files for a current 1,024-entry planning boundary. Its default now creates 1,025 files in bounded metadata batches and retains actual paged-plan, restored-file and reclamation assertions. The native reproduction passed in 14.442s; 32,769 remains explicit capacity qualification.

Removed obsolete or duplicate checks include retired 250k-node/1m-extent quotas built through reflection, a repeated 100,001-entry catalog limit probe, the old serial-admission implementation used only for benchmarking, an exact internal page-byte-cost assertion, an old comparison-only publication codec, and former compression-threshold-only cases. Current raw-byte roundtrips, persisted history, page/cursor/reopen, forced disk fallback, corruption/refusal, native ownership, cancellation and interruption/recovery checks remain. Reference adoption still covers unique, duplicate and repeated proofs with fewer redundant repetitions. Unsupported-state refusal is a current preservation contract, not backward-compatible execution.

The complete `pwsh ./scripts/build.ps1` gate passed on Windows on 2026-11-03: locked restore, Release build with zero warnings/errors, and 2,579 discovered cases (2,543 passed, 36 conditional skips). The final TRX records zero failures, errors, timeouts and aborted cases. Test duration was 12m49s; the whole command finished in about 13m04s within its separately declared 16-minute stop. All ten stopped-reclamation cases, the real killed-reader recovery case, both scratch-pressure cases and the metadata-counter boundary regression passed. No test processes remained afterward.

Earlier canceled runs are not completed gates. Some diagnostic cancellations missed their declared deadlines (up to 29 seconds); those overruns are not passes. The earlier five-minute whole-suite estimate underestimated aggregate cost. Correcting a phase estimate does not establish a performance improvement or increase any individual test timeout. No retained 100k/17GiB fixture was resumed. Large/native/cost qualifications described above remain separate from this default functional regression result.
