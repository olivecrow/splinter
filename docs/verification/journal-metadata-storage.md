<a id="저널-기본값-메타데이터-저장-검증"></a>
# Journal-default metadata storage verification

[한국어](../ko/verification/journal-metadata-storage.md)

[JM scope](../plans/journal-metadata-storage.md) ran on 2026-10-03, Windows x64/C: NTFS. Against the product goal of range storage without full original copying, omissible defaults in current SJR2 were selected for improvement. This is offline compaction comparison of JP's actual Unity journals using new owned copies. It does not claim actual VSS/WinFsp/Unity lifecycle or whole-workspace storage/RAM qualification for the new candidate.

<a id="고정-입력과-현재-계약"></a>
## Fixed inputs and current contract

Evidence root is `artifacts/journal-metadata-storage-20261003/`. [Measurement source](../../artifacts/journal-metadata-storage-20261003/probe/Program.cs) compares private state/sequence with a fake snapshot rejecting basis-payload reads and measures `FILE_STANDARD_INFO.AllocationSize`. Both inputs are stored journals tied to JP's [actual execution/checkpoint seals](journal-payload-compression.md); original evidence was fixed read-only with handles denying write/delete sharing.

| Input | SHA-256 | Sequence / frame count |
| --- | --- | ---: |
| A, `a-after-compact/encoded` | `97ec3db788345d2a558b9d5a16f386cc6afcdfa3b9e7e217af603f88c89c389c` | 27,970 / 23,718 |
| B, `b-collected-journal/encoded` | `9aa877ef0d640cae6122bccb418ef31483605a29746dab577451d7052226f39e` | 61,687 / 61,687 |

Preceding Runtime is `development @ 7f07cb7`, SHA-256 `4f841493c324cbca64fc88595dc14e607e6a3498962a299ffde49bf06886bb43`; candidate is `d100d2331fa059dc13ecadbd18f574e3b49b7371a5d1009cefe47ddfb8e69d17`. Core is identical in both. Each input/Runtime combination was compacted once; identical original/identity hashes checked before/after. Failed sandbox A comparison is preserved in `baseline-a`; accepted tables use only ordinary-user `*-qualified` runs.

Changes omit private Mutation `Offset=0`, `Destination=null`, `Replace=false` and select smaller current JSON during explicit compaction/candidate preparation. Reader semantics already allowing defaults, overlay v2/SJR2/candidate v3/workspace v2, Core/CLI/schema, raw/Brotli stored lengths, per-callback acknowledgment/individual durable flush remain. Stored journals are not rewritten on read/ensure. This is a smaller representation of the same version; no older-version migration/fallback.

<a id="같은-입력의-실제-파일-할당"></a>
## Actual file allocation for the same inputs

| NTFS journal allocation B | Original input | After preceding Runtime compaction | After candidate compaction | Additional decrease here |
| --- | ---: | ---: | ---: | ---: |
| A, already compacted | 38,043,648 | 38,043,648 | 37,191,680 | 851,968 (2.24%) |
| B, checkpoint seal | 116,260,864 | 54,697,984 | 53,215,232 | 1,482,752 (2.71%) |

A decoded/stored payload is identical before/after. Logical journal length 38,042,178→37,188,067B; frame costs 11,406,654→10,552,543B. After preceding/candidate B compaction, stored payload is 35,242,050B in both, live logical ranges 172,504,063B, fully unreferenced stored payload 24B. Preceding/candidate frame costs 19,453,670→17,970,281B; logical journal 54,695,720→53,212,331B. The difference is metadata and is not double-counted with existing payload removal.

Additional journal-allocation decrease totals **2,334,720B** for A/B. Existing B compaction's 61,562,880B decrease is not this change's effect. Original-input defaults 2,194,498B differ from final B logical decrease 1,483,389B because existing compaction merges skip frames. This sample promises no general Library saving rate.

Immediately after publication, previous/current journal file-allocation sums: A 76,087,296→75,235,328B; B 170,958,848→169,476,096B. After reclaiming verified owned previous generations/intents with `CompleteCompaction`, remaining journal allocation matches candidate-compacted values above. Predicted overlap A 75,230,245B, B 169,470,542B matches **sum of two journal logical lengths** respectively. This budget excludes NTFS allocation, control files, directories/MFT, VSS, system cache, and instantaneous maxima between observations.

[Preceding A](../../artifacts/journal-metadata-storage-20261003/baseline-a-qualified/measurement.json), [candidate A](../../artifacts/journal-metadata-storage-20261003/candidate-a-qualified/measurement.json), [preceding B](../../artifacts/journal-metadata-storage-20261003/baseline-b-qualified/measurement.json), [candidate B](../../artifacts/journal-metadata-storage-20261003/candidate-b-qualified/measurement.json) preserve full seals, private state, payload/frame costs, and stage costs. Comparison copies, failed generations, binaries/logs are separate measurement artifacts, not added to product-workspace costs. Original Library/Git/independent results/shared VSS are outside this comparison; total physical peak/per-workspace VSS attribution are unknown.

<a id="정확성비용실패"></a>
## Correctness, costs, and failures

A private-state hash is `2e28ef2352868301e4fa2ae033404077ca93bbc11ebe003bc39f6c9380e3f043`; B is `690e9fd91225c876a610ab54f3202cd9ed32a32a6d041debee0bec45b2d053b0`, identical in both Runtimes/before and after compaction. These Runtime state hashes include content/metadata, logical extents, path changes, and sequence. Basis-payload reads 0, unchanged original journal/identity hashes, matching original seal in previous generation, and completed reclamation confirmed. This does not replace new execution evidence for actual mount/independent results.

| Compaction cost | Preceding A | Candidate A | Preceding B | Candidate B |
| --- | ---: | ---: | ---: | ---: |
| elapsed ms | 5,570 | 5,027 | 7,905 | 15,827 |
| Cumulative managed allocation B | 270,208,800 | 303,181,296 | 669,452,680 | 751,697,112 |

Additional serialization increased cumulative managed allocation. This is not managed-heap peak/total RAM/native-codec memory. Candidate B overlapped required full build, so timing conditions differ. These are few single samples; no time improvement, stall cause, SLA, or memory savings claimed. Accepted as a bounded storage-first improvement without further timing repetition.

- First sandbox A `File.Replace` access denial preserved candidate/intent. Completed in a new owned comparison folder with ordinary-user permissions, without deletion/attribute-change/administrator-installation bypass.
- Test project's default MSBuild run failed without diagnostics in child reference target. `baseline-test-build.log` remains; ordinary `-m:1` build prepared focused fixtures. Required build used the original script outside isolation under ordinary-user permissions.
- [Before-fix tests](../../artifacts/journal-metadata-storage-20261003/baseline-tests.trx): 2 failed out of 3; already-omitted literal current frame passed. [After-fix focused tests](../../artifacts/journal-metadata-storage-20261003/after-focused-tests.trx): 23 passed. Checks independent literal SJR2, explicit/omitted defaults, nondefault offset/replace rename, raw/encoded content, individual flush, immutability during read, compaction size/budget, and previous-generation preservation.

<a id="판정"></a>
## Assessment

Required `pwsh ./scripts/build.ps1` completed locked restore/Release with 0 warnings/errors and **884 passed, 0 failed, 1 skipped**. [Build log](../../artifacts/journal-metadata-storage-20261003/build.log) and [same-run TRX](../../artifacts/journal-metadata-storage-20261003/automated.trx) remain. Existing candidate/compaction publication interruption, same-intent resumption, corrupt-state preservation, individual ACK/flush, checksum/tail, and original/peer/reclamation-guard regressions are included. Only current private JSON representation changes; public schema/Core/fake contract formats do not. UTF-8 checks for 88 files, local links in changed documents, and diff checks passed. [Final audit](../../artifacts/journal-metadata-storage-20261003/final-audit.json) compares input/binary/current-source hashes, before/after content/sequence, owned previous-generation reclamation, file allocation, and full tests.

JM-01–04 close as **scoped-complete-with-limitations**. Acceptance rests on smaller actual NTFS file allocation for two actual journals including already-compacted A and content/recovery regressions. Additional cumulative managed allocation/single timing sample limitations remain disclosed; this is not memory/time improvement. New Unity/VSS A/B, installation/grant/volume, original application, large/ARM/remote/OS failures, total peak/VSS attribution/total RAM remain NotRun or unknown. Existing JP/M0, SF-02/03 Partial, and C: pending states remain unchanged.

Those NotRun boundaries concern JM's offline unit execution. Subsequent actual T: A/B with the same JM Runtime, current approved writes/compaction, independent results/reclamation were checked separately through [PS-03–05 direct execution](physical-storage-completion.md). JM before/after file comparison/managed-allocation values are not converted into PS total-space/RAM improvement.
