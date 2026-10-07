<a id="저널-기본값-메타데이터의-물리-저장-개선"></a>
# Improving physical storage of journal default metadata

[한국어](../ko/plans/journal-metadata-storage.md)

The question is **whether omitting semantically equivalent defaults in current SJR2 can reduce actual NTFS journal allocation for new writes and explicit compaction**. Product goals are the original NTFS location, an original that stays editable, no initial full copy/read hydration/whole-file copy-up, and per-task range storage. Preserve the physical space → memory → parallel stability order. This document fixes the user-requested implementation/verification unit; it does not reopen closed JP/M0 or new Unity A/B runs.

JM-01–04 closed as scoped-complete-with-limitations according to [direct verification, costs, and limits](../verification/journal-metadata-storage.md). The following fixes scope and completion criteria; it does not instruct further execution.

<a id="선정-근거"></a>
## Selection evidence

Read-only inspection covered JP's preserved real input `a-after-compact/encoded` and `b-collected-journal/encoded` tied to a checkpoint seal. All external checksums and whole-file SHA-256 values match existing records. Defaults can omit 854,111B from A's 10,268,190B of JSON across 23,718 frames, and 2,194,498B from B's 25,531,461B across 61,687 frames. A is already compacted, allowing assessment separate from replaced-payload removal. Existing explicit compaction owns B's 52,482,657B of fully replaced payload. Exclude that effect from this improvement.

<a id="포함과-완료-조건"></a>
## Inclusion and completion criteria

| ID | Scope | Completion criteria |
| --- | --- | --- |
| JM-01 | Omit Offset=0, Destination=null, Replace=false in private Mutation serialization | Literal frames confirm omitted and explicit defaults have identical current-reader meaning. Preserve non-default offset/rename destination/replace=true, raw/Brotli content, individual ACK/flush |
| JM-02 | Reduce metadata for retained explicit compaction/candidate frames | Select reserialized decoded current mutations only when smaller than the original representation. Reuse payload blocks; preserve fragment/skip selection, sequence/path changes, state hash, expected/actual lengths, overlap budgets, and same-intent resumption |
| JM-03 | Before/after file allocation for identical real journals | Compact new owned copies of the fixed A/B journals once each with fixed predecessor and candidate Runtimes. Record FILE_STANDARD_INFO.AllocationSize, previous generation/overlap, residuals after compaction, payload/frame costs, and time/managed allocations separately. Original evidence hashes unchanged; identical private state hash/sequence; zero basis payload reads |
| JM-04 | Regression and documentation | Required build regressions for existing compaction/candidate publication interruption, checksum/partial tail/ACK, and original/peer/reclamation guards; UTF-8/diff/current contract/map/plan/verification synchronized |

Contracts remain overlay v2, SJR2, candidate v3, and workspace v2. The three omitted fields are private JSON fields already read as defaults by the current reader. Add no new version/old-version reader or automatic migration. Do not rewrite stored journals on ensure/read; use smaller current representations only in acknowledged writes and explicit compaction/candidate publication. Do not change input/response schemas, Core payload contracts, service/permissions, or CLI semantics.

<a id="실행-자원과-제외"></a>
## Execution resources and exclusions

Fix `development @ 7f07cb7` as the predecessor Runtime, preserving new comparison copies, binary hashes, and raw results in `artifacts/journal-metadata-storage-20261003/`. Existing JP artifacts are read-only. The fake snapshot provides private replay only and rejects basis payload reads. This is an offline comparison of journals from real Unity inputs; it does not qualify actual WinFsp/VSS/Unity lifecycle execution for the new candidate.

Additional full data copies are the cost of measurement journal copies, not product creation/read hydration. Reclaim only owned copies' previous journals/intents through verified CompleteCompaction; do not modify the original, existing results, snapshots, or other tasks. Separate original Library/Git/independent results/temporary evidence, shared VSS, and whole-workspace physical peak from journal file allocation. Whole-workspace savings, VSS attribution per task, and total RAM/native codec memory are unknown.

Exclude a new harness matrix, additional A/B/Editors, original application/check-in, installation/grant/volume/UAC, large/ARM/remote/OS failures, flush policy, new codecs/backends, automatic compaction/queues, and comprehensive memory optimization. Preserve evidence and fix only this boundary if growth/content/recovery failures occur. Close only this unit as scoped-complete-with-limitations when direct evidence exists for all four criteria.
