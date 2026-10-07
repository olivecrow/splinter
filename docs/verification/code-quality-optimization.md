<a id="코드-최적화의-자동-회귀"></a>
# Automatic regression for code optimization

[한국어](../ko/verification/code-quality-optimization.md)

Only the six reviewed items were changed on top of the existing safety-fix commit `development @ 8873ba6`. Exit criteria were removal of repeated full searches/block restoration, preservation of arguments for the exact next action, existing seal/ownership/content and failure-without-publication contracts, and required automatic regression. No new dependency, schema/state version, command, or persistent cache is added. Actual Unity/VSS, service installation/updates, new native A/B runs, and closed verification matrices are not executed.

<a id="확인한-경계"></a>
## Verified boundaries

| Item | Verification method | Limitation |
| --- | --- | --- |
| Shared ownership of next actions | 8 None/Plastic full/selected plan/resume cases and 3 Git/Plastic Library/None project basis ensure cases in `WorkspaceActionTests`. Serialized arguments validated against existing input schemas | Commands not rerun against actual product resources |
| File rename search | A file moves only its corresponding node. Existing replacement/lazy/whiteout/Unicode/case regressions and 2 new replay cases with deep child files and unrecorded parents | Directories retain boundary-prefix search because replay nodes may lack parents. Large-input before/after timing not measured |
| Restore extent lookup | Restoring the first/middle/last 4B among 512 sparse extents uses at most 10 search probes and visits 1 extent. Rejects holes and a different seal | Automatic tests with private ranges/fake basis; actual project final restore not rerun |
| Unique-object verification | 32 identical files and the same object in basis/result require 1 object open per verification call. Subsequent corruption rejected in a new call. A different declared length rejected before payload reads | Pre/post-publication and subsequent verification stages each reread. Total file-collection time reduction not measured |
| Original plan search | Nested selections, boundary siblings `dir1`/`dir10`, blocked deletion of external children, rejection of missing selections, and existing full/selected/Unity companion regression | Current original metadata/content observations and physical identity/publication guards retained. Native publication lifecycle qualification remains limited to the historical candidate in the owning documents |
| Compression traversal | A same write of private data up to 1MiB reads only the stored payload size from the journal. Checked compaction bytes/sequence for maximum blocks and partial-overwrite fragments, and read counts for `before-state-hash`/`candidate-scan` | No permanent dedup across extents/stages. Public small/empty-read staging, corrupt-tail rejection, and pool return remain existing codec regression. Native codec/Editor/total RAM and actual Unity timing not measured |

State hashing/comparison restores once per extent, and compaction groups slices of the same block for one restoration. Existing header/checksum and tail/declared-length validation are not omitted. Temporary digests/comparisons/candidates are used/published only after successful verification; public read destinations are not modified before full verification. Ownership, seal, individual flush of authorized writes, and reclaim guards are unchanged.

<a id="실행-근거"></a>
## Execution evidence

`artifacts/code-quality-optimization/` preserves initial/final focused-test TRX files and required build logs/full TRX. The initial focused run passed 293 of 294 tests, with 1 failure. The failure was a Plastic fixture input error using an arbitrary string instead of the recorded base schema; it was corrected to the actual recorded format. The original failure result is also preserved.

Final focused tests were **299 passed, 0 failed/skipped**. Added regressions total 20: 11 next-action cases, 4 journal search/block traversal cases, 2 rename replay cases with unrecorded parents, and 3 object/original plan cases. Required `pwsh ./scripts/build.ps1` locked restore and Release build had 0 warnings/errors; **1,024 of 1,025 passed, 0 failed, 1 test excluded at the time skipped**, with the full test run taking about 4 minutes 35 seconds. `build-final.log`, `full-final.trx`, and `source-manifest.json` for changed C# files are preserved. 119 documents passed UTF-8 checks. This document records automatic regression; actual-input qualification of historical RI/JP/ME/SP and original apply is not inherited as native success for the current candidate.
