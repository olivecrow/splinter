<a id="on의-현재-변경본-저장과-범위-회수"></a>
# Current changed-state storage and range reclamation with ON

[한국어](../ko/plans/latest-only-journal.md)

This is the implementation scope following the user's corrected intent on 2026-10-06, after `f67f217`, which only provided basis reversion. ON retains the immutable original basis + current private changed ranges + recovery information for one unfinished write. Completion means the existing individual mutation durable-flush/success-response boundary. Do not infer completion of an entire Unity task or change original/Library/Editor/service. Keep default OFF.

Explicitly applying existing compact selection ON makes subsequent writers/reconnections use current-state storage. Its displayed name is journal optimization; existing `--revert-base-ranges` input remains an alias for the same current command. Compare basis content only during explicit compact, not every write. Distinguish current OFF SJR2 from ON current-root representation; preserve/reject old intent/root formats. Do not automatically change existing state during ensure.

ON uses bounded private payload blocks, current metadata per file ID, and path buckets. Write only changed blocks/nodes/buckets anew. Flush one SJT2 redo first, then individually flush and atomically publish the corresponding file and small SJL2/root v2; reclaim only unreferenced previous payload. Without complete redo, retain B; with durable redo, interruption before C root publication can still finish as C. Partially replaced blocks resave only remaining private slices as bounded blocks, so small edits can incur rewriting the corresponding private block. Do not fill by reading original bytes or materialize a whole changed basis file. Remove only known unfinished tails; do not sweep unknown/corrupt/different-identity files. Support only compaction intent v3.

Verify same-version lost responses/recovery at each publication boundary, latest bytes/metadata/path/sequence/seal, rename/delete/truncate/new incarnation, partial overlap/compression/raw, one writer, and peer protection. Connect the new representation to checkpoint/candidate/restore/seal/observation and OFF transition; do not inherit earlier native qualification.

Fixed measurements use existing repeated interleaved 128KiB saves across 8/32 real PNG files and new small partial overwrites. Record storage at multiple points within cycles; compare OFF history growth against ON's bound on current changes. Sum reachable indexes/payload, pending data, and actual NTFS allocation, not only the root. Initial copies/read caches are zero; total physical peak/RAM and shared attribution remain unknown. Do not repeat existing closed A/B/full failure matrices; select only changed-writer file/process boundaries and necessary actual WinFsp fixtures.

Completion requires common Runtime/CLI/Windows integration and schema/fake regressions, interruption recovery of exact owned files, space/time figures for real repeated many-file saves, `pwsh scripts/build.ps1`, and documentation/verification records. This user request excludes additional commit/push, installation/ACL, non-owned process termination, and original project transition.

Subsequent current-writer changes belong to [write-path optimization](latest-journal-write-optimization.md). Closure/figures below record the SJL2/SJT2 candidate at that time.

<a id="마감"></a>
## Closure

2026-10-06 **scoped-complete-with-limitations**. Connected current-state storage/individual durable redo/previous payload reclamation and selection retention, Last Close/candidate/OFF transition, restoration/reclamation, and schema/CLI. Confirmed three OFF/ON runs each across 8/32 real PNG files, necessary local WinFsp normal save/delete/reconnect, three exact-owned-child boundaries, and automated recovery. Required locked restore/Release build had 0 warnings/errors; 1,118 passed, 0 failed, 10 explicit opt-in skipped out of 1,128 total. [Verification](../verification/latest-only-journal.md) owns figures and failure/fix history. Confirmed -93.74% final allocation for 32 files and constant current payload within cycles, but median writes of OFF 3.998s/ON 33.351s failed default ON conditions. Keep default OFF and do not open a new Unity/VSS/full failure matrix.

Subsequent default decision: OFF in this record is the choice for that candidate. The current SJL4 write path and default ON decision for new tasks follow the [current contract](../latest-only-journal.md) and [subsequent verification](../verification/latest-journal-write-optimization.md). Preserve historical inputs, figures, and unqualified boundaries.
