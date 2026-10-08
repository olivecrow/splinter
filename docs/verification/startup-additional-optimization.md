# Additional startup optimization after US

[한국어](../ko/verification/startup-additional-optimization.md)

> **Current timing reference:** the record below preserves its original candidate/stage measurements and then-unexecuted boundaries. The subsequent combined `116fd44` candidate is measured in [UF-01](unity-final-lifecycle.md): **18m 03.0s** complete workflow, **3m 36.1s** Editor readiness, **2m 52.0s** A/B preservation/release and **2m 06.0s** Library restoration. UF does not isolate each optimization or qualify the separate unexecuted scenarios below.

SA-01–05 implement the remaining Library, Close, source-proof, publication and inherited Bee-graph opportunities from [US-01](unity-startup-remeasurement.md). The source baseline is `49d8679`; the comparison binary is the frozen `9c09f04` CLI bundle from US. **New full Unity A/B readiness, ILPP timeout reduction, checkpoint/release and original Library restoration times are NotRun.** US's 349.7933 seconds remains historical evidence, not this candidate's time.

## Changes and guards

| ID | Change | Retained proof and scope |
| --- | --- | --- |
| SA-01 | Pin the owned Library root and every ancestor for the reader lifetime; replace repeated path queries with the held lifetime proof. | Ordinary directory identity/reparse admission, immutable file handles and content proof remain. Reads run outside the lifetime gate; every concurrent close drains admitted reads. Read errors poison availability instead of reporting success. Unchanged-directory restore transfers the root to its moving handle with the same volume/file identity; interruption/replacement preserves and rejects. |
| SA-02 | Linked/unknown Close checks local health before deciding that reclamation is unnecessary; published SJL4 root validation and indexing share one read handle. | Disposed/poison/journal-length checks and external mutation serialization remain. Actual unlinked reclamation still checks the basis. Root checksum, owner, namespace, metadata, length, range and generation validation remain. Each mutation keeps its own durable flush and acknowledgement. |
| SA-03 | On `git-snapshot` inspections with a source-change tracker, compute raw SHA256 and Git blob hash in one full stream when Git clean conversion is proven to preserve bytes. | Exact attribute/config interpretation, object format and before/after generation remain. Conservative text/EOL/content checks admit only identity conversion; filters, encoding, ident, safecrlf or unproved cases use the existing Git path. No payload cache or timestamp-only content proof. |
| SA-04 | Reuse the first completed source inspection for the same new editorless ensure response. | Single-use, operation-local capability; recheck registration, workspace/snapshot identity, policy, HEAD, index, membership, attributes/config and provider epoch/sequence. No persistent proof. Existing tasks, audit, missing capability and Editor-start responses retain ordinary inspection. |
| SA-05 | Before a new explicitly Library-grafted task's first Editor launch, invalidate only ordinary top-level `Library/Bee/*.dag` files. | Exact Library ID/project association and separate original/workspace paths; whole-set path/reparse validation before deletion and per-file recheck. Prior receipt, uncertain launch, ordinary checkout and non-Library/whole-project inputs do not select it. JSON, payloads, state, backend info, response files, artifacts, original and peer remain. |

SA-01 adds no path-result cache: the native open handles provide the lifetime proof. Actual payload reads still surface device/read errors. Forced volume removal and OS failure are outside this qualification. A failed restore handoff permanently refuses reuse of that reader; the normal same-plan recovery reopens a new reader. No format, service or mount contract changes.

The access/sharing distinction matters. The immutable Library root denies write/delete sharing. Its ancestors deny deletion but permit ordinary child publication; their pinned descendants keep them nonempty. Mutable journal directories likewise permit child publication, while an extra read handle pins the **existing** `owner.lock` against deletion and keeps the directory nonempty. Native junction-conversion tests verify this boundary. Final handle paths and ancestry attributes are checked after constructing the chain. Restore parents can become empty, so they keep namespace pins **and** actual path/reparse checks at file/directory admission and move. No new marker is created. These tests do not qualify arbitrary filesystem-filter behavior.

SA-03's NUL test is a sufficient automatic-binary condition over the **whole stream**, bounded to the native counter range. It is not Git's diff-prefix heuristic. Explicit text requires the stricter no-CR condition. Tests compare SHA1/SHA256 output with installed Git, including CRLF, ambiguous attribute values and a NUL after the first MiB. The matching primary implementation is [Git for Windows convert.c](https://github.com/git-for-windows/git/blob/v2.45.1.windows.1/convert.c).

SA-04 returns the original inspection metrics. Its guard-only revalidation is not reported as a second full inspection or included in that original duration. Changed/missing generations, membership or interpretation reject the proof and preserve the task; a second use also rejects. Managed Unity writes can legitimately create source metadata, so `ensure --editor` does not reuse this clean proof.

## Bounded Bee experiment

Unity's [6000.3 compilation code](https://github.com/Unity-Technologies/UnityCsReference/blob/6000.3/Editor/Mono/Scripting/ScriptCompilation/EditorCompilation.cs) enables deferred graph verification. A tiny one-action graph was run with the installed Unity **6000.3.22f1** Bee backend, without launching Unity or executing an existing project's graph:

1. Bake the synthetic graph: exit 0, action count 1.
2. Reuse its compiled graph with deferred verification: the action count becomes 2 **before** the verification signal.
3. Move only its compiled `.dag` out of the test path: exit 4 requests a frontend run; action count stays 2.
4. Explicitly rebake using the retained graph JSON: exit 0, action count becomes 3.

The state, graph JSON and payload hashes remain equal across the missing-graph step. Bee itself removes/recreates its derived graph cache. Product code invalidates only `.dag`; it does not rewrite a graph/response file, share a process endpoint or change installed Unity/tools. Graph generation still costs time. This proves the backend's bounded missing-graph behavior, **not** a real Unity frontend rerun or removal of US's approximately 60-second ILPP delay.

## Evidence and limits

The [dataset](../../reports/benchmarks/startup-additional-optimization-20261008.json) records final regression results, frozen runtime hashes, tiny NTFS comparisons and the Bee experiment. Raw logs/fixtures are under ignored `artifacts/startup-additional-optimization-20261008/` and `artifacts/ilpp-dag-metadata-probe-20261008/`.

The required `pwsh -NoProfile -File scripts/build.ps1` passed locked restore, Release build and **1,453 tests / 0 failures / 20 conditional skips**, with zero build warnings/errors. This includes 77 more cases than the prior 1,396-case suite. The final full suite took 4m 55.231s. All 27 unrelated dirty-file hashes and the clean sibling Unity repository status remain unchanged.

Tests cover empty-root rename/reparse protection, overlapping reads and close, restoration interruption/replacement, local Close health, publication handle races and corruption, source interpretation/generation changes, single-use proof, launch receipts and partial graph-deletion retry. The first native test run exposed that attribute-only directory handles did not prevent rename; the correction retains actual rename attempts as regression checks. Conditional external tests remain conditional.

A first read-only sharing correction also blocked normal child-file moves. A bounded access/share matrix and actual `FSCTL_SET_REPARSE_POINT` experiment led to the narrower design above. The final regressions include create/move/replace-with-backup/delete under a live journal guard and publication/child moves under live restore-parent pins. [Windows' sharing contract](https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-createfilew) treats attribute access separately from data/delete access; metadata-only opens are not used as the namespace proof. Both failed attempts remain in the raw evidence.

Cost comparisons use 64 ordinary NTFS files (4MiB), 20,000 availability calls, 20,000 linked Close calls and 512 individual 4KiB appends. Setup and content verification are outside the timers. Every append still flushes; all bytes and reopened seals are checked. Before/after/after/before ordering reduces simple warmup bias but is not a randomized performance study. These are combined reader/journal operation costs, not exclusive attribution or physical I/O measurements.

| Timed operation | Before mean, seconds | After mean, seconds | Observed reduction |
| --- | ---: | ---: | ---: |
| 20,000 Library availability checks | 0.537752 | 0.016376 | 96.95% |
| 20,000 linked Close calls | 0.648384 | 0.025096 | 96.13% |
| 512 × 4KiB appends, one flush each | 3.178398 | 3.090191 | 2.78% |

Each mean uses two runs of its frozen candidate, with the real Windows journal guard enabled. Append ranges overlap (before 3.0873–3.2695s; after 3.0488–3.1316s), so this sample does **not establish a consistent write-time improvement**. The first exploratory cohort partly overlapped read-only UTF-8 validation; it is preserved separately. The final cohort above ran without simultaneous agent tool work.

The original Unity project/Library, installed service and global/external-tool configuration are untouched. The 27 unrelated dirty files are protected by their recorded hashes. No new full Unity cohort, Play/Test/build, support matrix, OS failure or whole-RAM/physical-storage qualification is implied.
