<a id="물리-저장-공간-단계의-최종-검증"></a>
# Final verification of the physical storage stage

[한국어](../ko/verification/physical-storage-completion.md)

PS-01–05 in the [fixed scope](../plans/physical-storage-completion.md) are **closed as scoped-complete-with-limitations.** Physical-storage implementation/cost judgment for current Windows x64 NTFS alpha is complete; subsequent memory judgment follows [ME verification](memory-completion.md). This is distinct from full SF/large/OS/ARM support; raw execution is preserved at `artifacts/physical-storage-completion-20261003/`.

<a id="후보와-직접-근거"></a>
## Candidate and direct evidence

Current development JM Runtime SHA-256 is `d100d2331fa059dc13ecadbd18f574e3b49b7371a5d1009cefe47ddfb8e69d17`. Retains workspace v2, overlay v2/SJR2, candidate v3, and service protocol 1. Program changes here are a read-only development measuring tool; Core/public schemas/flush/seal/reclaim guards unchanged. No new dependencies.

| ID | Direct verification | Judgment |
| --- | --- | --- |
| PS-01 | 10 cases covering SJR2 stored/decoded, whole cost of partially live blocks, fragments/skip/rename/resize/delete/recreation, checksum/tail/sequence, and rejection preserving old-format/non-ASCII/writer. Compared payload/live/obsolete/frame ledgers of two existing JP preserved inputs with historical Runtime usage | Passed, stopped ASCII ledger. Brotli content restoration belongs to product replay |
| PS-02 | 3 tests for exact task selection, shared/evidence separation, totals, duplicate roots/missing records, existing-output preservation, and actual hardlink unknown | Passed, Windows file allocation. Excludes MFT/directories and VSS/OS-cache attribution |
| PS-03 | Latest Runtime creation/read/same request/owned Editor/fixed write/compaction/independent result/reclaim in final A/B below | Passed with limitations, one small actual A/B and reclaim of exact two snapshots |
| PS-04 | Per-stage exact-root allocation, two journal generations overlapping, results/runner/evidence, and post-reclaim residue | Passed with limitations, shared VSS attribution/whole instantaneous peak unknown |
| PS-05 | Locked restore/Release 0 warnings/errors, 884 passed/0 failed/1 skipped. Python 13 passed, 35 actual/failed response-schema cases, UTF-8/local links/diff | Passed, current-candidate automatic regression/document audit |

`python-final.log`, `python-qualified.log`, `build.log`, `automated.trx`, and `response-schema-validation.json` are final-candidate execution evidence. Last Python execution includes strict rejection of lowercase task IDs. Initial Python total test found omitted `Measure-Object` summation on OrderedDictionary; fixed with explicit summation. Failure logs and raw rows of pre-fix creation/read samples were preserved. `final-costs.json` independently recalculates the two initial sample totals from immutable root rows and compares later published totals with root/category totals. Prior raw-journal ledger expectations that treated partially live bytes as immediately reclaimable payload were also corrected to current Runtime whole referenced-block semantics.

<a id="최종-제품-ab"></a>
## Final product A/B

Used existing `T:\plastic-fixture-20261002`/home `T:\pt1002`, plastic/unity/snapshot/ntfs-snapshot, each budget 17,179,869,184B. Retained Unity `6000.3.22f1`, existing CLI `1.0.0-beta.5`/Pipeline `0.8.0-exp.1`, and registered restricted service; no installation/grant/volume/UAC changes. Ordinary CLI cost callerElevated=false.

| Task | key / id | Owned Editor |
| --- | --- | --- |
| A | `ps-t-a-20261003` / `328a6239c9f51f15979bdbf3` | PID 45892, start ticks `639266071054259595` |
| B | `ps-t-b-20261003` / `e7b5af5fbad8f669cdf8b013` | PID 69560, start ticks `639266071083365748` |

Both ensures had initial payload/journal/read cache/sequence 0. After reading 64KiB from a basis Bee file at returned paths, journal/sequence/cache remained 0. Same ensure retained each id/path/digest/basis/snapshot. Wrote four fixed Bee files totaling 51,859,898B to each worker's new `Library/SplinterPhysicalQualification` using 64KiB streams; verified mounted lengths/hashes. These are new explicit client writes, not initial duplication/hydration/full-file copy-up. Independent source marker is `Assets/SplinterPhysicalProbe.cs`.

After normal A stop/one compaction with 1GiB overlap budget, reconnected to the same snapshot and verified four mounted samples/source. Before/after measurement copies match product compaction seal bytes/hash/sequence. B retained same PID/start time/b-one value/MVID through A compaction/release. After A reclaim, changed B once to b-two and issued one external CLI recompile, verifying same PID/start time, new MVID/loaded value, and no compile error. CLI port was rediscovered after reload rather than fixed.

B also passed normal stop/one compaction with the same budget/reconnected four samples/source verification. A/B checkpoints each preserved stable sources as independent 64 objects/245,669B; both release/final status are released, exact snapshot missing, mount/provider/delta false, handles/provider working/private and owned application activeProcesses/working/private all 0. After reclaim, verified all baseline/result file object lengths/SHA-256, markers, and result identity. A result is `sha256:9463b77cb342190155044eab6f24b6be5606d94f06369d204b162fb6dcca7f43`; B is `sha256:80dd86dfce8a1112fb5423e76f898b4d21428fd660bb689dc16b2545aa4409d9`. Bee samples are Library/cache input, not automatically included in source results.

`protection-and-reclamation.json` verifies unchanged bytes/hashes of T: original source/management roots' 65 files and existing records' 537 files, plus 5 Plastic registrations. Existing Unity PIDs 68624/85184/52688 retained creation times. Ordinary CIM did not observe project arguments of those existing processes, so this does not expand to original-project loaded-code qualification. Current Runtime and executing runner SHA-256 match.

Initial A/B eval and first eval after B reload had 5-second main-thread timeouts preserved as raw failures. Only the same-target read query was retried once each; writes/recompile/new tasks were not repeated due to timeout. Unity code acceptance is observed by external tools/AI; no Splinter internal gate was reintroduced.

<a id="비용과-종료-판정"></a>
## Cost and closure judgment

| Journal | Allocation before compaction B | Allocation after compaction B | Reduction B | Two-generation allocation during compaction B |
| --- | ---: | ---: | ---: | ---: |
| A | 41,005,056 | 40,488,960 | 516,096 | 81,494,016 |
| B | 42,938,368 | 37,117,952 | 5,820,416 | 80,056,320 |

Combined journal-allocation reduction from both compactions is 6,336,512B. Tasks were compacted at different times; overlap values are not summed as simultaneous peak. A/B after-seal sequences are 26,796/28,118; full copy hashes and Runtime journal/payload/metadata counts were also compared. A after stored/live-stored/obsolete/frame costs are 28,674,875/28,674,867/8/11,813,367B; B 26,650,551/26,650,527/24/10,466,881B. Logical live data is 167,329,435/151,936,765B respectively; storage costs of partially live blocks are not subtracted. The measuring tool does not claim Brotli content restoration; actual mounted reads and product state/hash/seal verification remain separate.

| Stage | Allocation of two selected tasks B | Shared runner/home root allocation B | Independent payload included in tasks B |
| --- | ---: | ---: | ---: |
| After creation/basis sample reads | 111,232 | 3,449,864 | 0 |
| Editor/fixed-input edit sample | 71,360,128 | 3,449,864 | 0 |
| Before A compaction | 76,845,008 | 3,449,864 | 0 |
| A preserved, B continuing | 76,652,376 | 3,449,864 | 310,784 |
| A reclaimed, B subsequent-change sample | 36,423,824 | 3,449,864 | 310,784 |
| Before B compaction | 43,698,656 | 3,449,864 | 310,784 |
| After B preservation | 38,168,936 | 3,449,864 | 621,568 |
| After both tasks reclaimed | 1,048,304 | 3,449,864 | 621,568 |

Post-reclaim two-task 1,048,304B comprises independent payload 621,568B and control/manifest/diagnostic records 426,736B. No journal/previous generation/mount remains. A is 513,472B, B 534,832B; results are not deleted to reduce residue to 0. Current runner is one content key, 7 files/1,155,416B shared by A/B. Two previous keys, 14 files/2,294,448B are also preserved; shared total is 21 files/3,449,864B. This cache already included at creation is not counted twice as new-task cost. All records total 738 files/4,241,832B and include existing materials, so are not added again to the two selected task totals.

Original Library 30,748 files/1,852,496,112 allocated B is a separate baseline. Task-journal logical Library bytes are not added again to file allocation. Final evidence-root sample is 164,623,712B, including four journal copies at 161,542,457 logical B and tests/TRX/failures/responses/measurement evidence. Later audit files are a small amount outside this final sample; this value is not a final absolute upper bound. Raw materials are not deleted. T: free space is preserved as manifest 13,215,363,072B and stage volumeObservations; reclaim of two new owned journals does not provide exact attribution of shared-volume writes.

Retains current range storage, identical private writes/small changed ranges, bounded compression, explicit compaction, small default metadata, and owned reclaim. Compression savings for the same raw input use separate fixed [JP](journal-payload-compression.md) comparison; extra default journal-file savings use [JM](journal-metadata-storage.md). Different Editor activities in this stage are not directly compared with earlier candidates to invent additional savings ratios. Current-candidate lifecycle/compaction/residual cost is explained and data/original/peer protection passed, so the physical-storage development unit closes.

Ordinary VSS usage queries retain failures as unknown. Volume free space is shared observation including other writes/VSS, not converted to file-allocation totals, per-task VSS attribution, or overall storage-saving rate. Compaction's two journal generations are not whole-workspace instantaneous peak. MFT/directories, kernel/OS cache, total physical peak, and RAM improvement are unknown.

Existing C: reclaim-pending/discard-reclaim-pending and non-owned snapshots are not invoked/deleted. Reclaim of the two T: snapshots does not mean C: pending resolution or 0 snapshots across the whole volume. Retains SF-02/03 Partial, SF-04–06 NotRun, large PV/other input/VCS/ARM/OS/storage-exhaustion support status. Subsequent memory is separate ME scope; new storage work opens only for new actual-use shortages/preservation defects/explicit support-expansion requests.
