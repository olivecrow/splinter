<a id="압축-저널-제품-연결-검증"></a>
# Compressed journal product integration verification

[한국어](../ko/verification/journal-payload-compression.md)

Following the 2026-10-03 [fixed scope](../plans/journal-payload-compression.md), product integration of current SJR2/overlay v2/candidate v3, actual T: Plastic A/B, and journal allocation closed as **scoped-complete-with-limitations**. Logical samples from the [independent codec](workspace-payload-codec.md) and previous M0/PC/UH are not inherited as native passes for this format.

| Gate | Status | Evidence |
| --- | --- | --- |
| JP-01 Common frame/fragment and single acknowledgment | Passed | Core stored/restored lengths, logical fragment/literal boundaries, one frame/durable flush/sequence per raw/compressed callback. New `JournalPayloadTests` and existing codec fake/conformance |
| JP-02 Replay/corruption/compaction/candidate/budget | Passed with limitations | Overlap/resize/rename/delete, partial reads/corruption/same intent/candidate/compaction-budget regression, compressed input/ACK in existing actual child-interruption tests. Source fingerprint/four mounted samples identical after actual A compaction/reconnect. New native forced termination/OS/power matrices not run |
| JP-03 Single format/schema/discovery | Passed | SJR2/overlay v2/candidate v3, previous request/storage identities and complete preservation/rejection of unsupported frames. Workspace v2/service protocol retained. Final discovery/40 actual responses validated with current embedded schemas |
| JP-04 Actual T: A/B/independent result/reclaim | Passed with limitations | Same ensure, exact project/PID/ticks/loaded A/B, same B continuing during A compaction/reclaim, subsequent b-two, two independent results/final owned snapshot reclaim. Original/registrations/existing records preserved |
| JP-05 NTFS file allocation/memory cost | Passed with limitations | Actual journal raw comparison with identical metadata/decoded input, file allocation/logical/stored/live/replaced payload, cumulative managed allocation/owned-memory samples disclosed. Total peak/VSS attribution/native codec RAM unknown |
| JP-06 Full regression/documents | Passed | Final locked restore/Release 0 warnings/errors, 881 passed/0 failed/1 skip. 40 actual response/discovery cases valid against current embedded schema. Current contracts/skill/plans/verification tables/UTF-8/local links/diff compared |

First full build succeeded, but 59 regressions assuming previous versions/raw physical lengths failed. Test input/current schemas/development tools were updated without restoring previous-format support. Of initial 229 focused regressions, 3 were changed-input oracle/pre-compression size/budget assumptions. Raw failures preserved in `artifacts/payload-integration-initial.trx`/`payload-integration-focused.log`.

Required build for corrected Runtime candidate, 881 passed/0 failed/1 optional skip, and 64 focused passes remain in `artifacts/payload-integration-build-final.log`/`artifacts/payload-integration-focused-final.xml`. Subsequently, 6 failures from omitted discovery document-path constants in schema were preserved in `artifacts/payload-integration-qualified-build.log`/`artifacts/payload-integration-qualified-failed.trx`, then exact constants corrected. Strict schemas/durability/reclaim gates are not relaxed.

Final build/TRX are `artifacts/payload-integration-qualified-final-build.log`/`artifacts/payload-integration-qualified-final.trx`. `response-schema-validation-final.json` validates 40 actual JSON responses using current embedded catalog. Final source/CLI native-module hashes match execution manifest. Small codec partial reads retain full bounded-block validation/restoration costs; this is not expanded to general support/total storage/RAM savings.

<a id="고정-후보와-실제-수명주기"></a>
## Fixed candidate and actual lifecycle

Evidence root is `artifacts/journal-payload-compression-20261003/`. `manifest.json` fixes requests/budgets/binary hashes. Hashes of 8 native implementation modules still match after later Host/Protocol discovery-information changes, compared by `final-audit.json`. Qualification is not inherited by another Runtime's execution.

| Item | Execution scope |
| --- | --- |
| Environment | Windows x64/NTFS/existing service, Unity `6000.3.22f1`, CLI `1.0.0-beta.5`, Pipeline `0.8.0-exp.1`. No installation/grant/volume/UAC changes; ordinary workspace commands callerElevated=false |
| Original/home | `T:\plastic-fixture-20261002` / `T:\pt1002`, plastic/unity/snapshot/ntfs-snapshot, each journal budget 17,179,869,184B |
| A | `jp-t-a-20261003`, id `fea2876f791abc7be2653c30`, digest `49bb16148b82b7582e6ef09e`, Editor 73380 / ticks `639265613835498043`, snapshot `{25C177CB-3A94-4A31-A8BD-56D09EC3ACBD}` |
| B | `jp-t-b-20261003`, id `45f908d5d62a2508a8320569`, digest `b0afbff5a7013c7063b86b0a`, Editor 89680 / ticks `639265614221381303`, snapshot `{EFA84F28-1AC2-4B9F-9300-9B26F4EA79AB}` |
| Basis | Both `sha256:8a58a18cac8d41149bafa58e886dcd25b78fd8162ea7cd5b19e9533fef0c9e0b`, 62 files / 245,523B. Initial payload/journal/read cache 0; same ensure retains id/root/digest/basis/snapshot |

Authored a-one/b-one in `Assets/SplinterJournalProbe.cs` at returned paths, checked external CLI actual project/PID/exact start ticks/loaded value/MVID. B value/PID/MVID remained during A a-two recompilation. After normal A stop, compaction with 1GiB overlap budget, and same snapshot/provider reconnect, verified mounted samples/source fingerprint. Same B edited/recompiled b-two after A checkpoint/release. B marker includes 2,388B repeated comments, also verifying independent-result collection of compressed source payload. This small sample does not substitute for actual Library-input improvement evidence.

| Results and protection | Evidence |
| --- | --- |
| A result | `sha256:75e008cbcd7b9d002e76a1c8cb25583d760d2705cb257978ec5e74dd90b43d8e`, 64 files / 245,665B, identical a-two bytes |
| B result | `sha256:af8b9127c0e5f8dd9c9341fd66681ad0713fe43c911a235cf6591d413e8e782b`, 64 files / 247,970B, b-two marker hash `5895036a025c924db4219176c3840eddeb335672128871d7d27962784f6b5aad` |
| Reclaim | `21-a-final.json` / `22-b-final.json`: released, exact two snapshots missing, mount/provider/delta false, handles/provider working/private/owned application activeProcesses/working/private all 0. After reclaim `independent-results.json` verifies all baseline/result object SHA-256/lengths/markers |
| Existing protection | Identical bytes/hashes for selected T: original 65/C: original 66 files/management roots; identical 5 Plastic registrations; identical existing T: records 335-file bytes/hashes. Original Editor 68624 / ticks `639265412574256363` retained. Records' absolute/relative paths normalized for comparison; raw before/after evidence retained |

Existing C: pending/non-owned blockers were not reclaim targets. This ordinary observation was without UAC; full protected ledger/C: VSS inventory and per-volume used/allocated were not remeasured. Two owned T: snapshots missing is not described as 0 snapshots across the whole volume or C: pending resolution.

<a id="같은-raw-표현의-실제-공간"></a>
## Actual storage for the same raw representation

Each worker explicitly wrote four existing Bee files totaling 51,859,898B to `Library/SplinterPayloadQualification` using 64KiB streams. New fixed write input, not source clone/read hydration/copy-up fallback. Original read-only; all four actual-mount SHA-256/lengths match original, including after A compaction/replay. Journal also includes Unity's own Library/management-file writes.

The tool read actual stable T: journals into bounded C: measurement artifacts and generated SJR2 raw representations of **identical frame metadata/decoded callback input**. Includes outer checksum/48B codec cost. Encoded/raw comparison used the same C: NTFS; actual journal allocation measured separately on T:. Private replay/state hash/sequence match; fake base forbids payload reads. Actual mount/independent result are separate native evidence. This is not whole-performance comparison of prior binaries.

| Journal | T: actual allocation B | C: encoded comparison B | Same raw comparison B | Encoded comparison reduction |
| --- | ---: | ---: | ---: | ---: |
| A before compaction | 43,778,048 | 43,741,184 | 188,538,880 | 76.80% |
| A after compaction | 38,043,648 | 38,043,648 | 163,287,040 | 76.70% |
| B final checkpoint seal | 116,260,864 | 116,260,864 | 297,238,528 | 60.89% |

Actual A compaction allocation reduction is 5,734,400B. Raw comparison/compaction effects are not combined into a whole-workspace savings ratio. Current raw header is 8B larger per frame than previous format; this is not presented as measured comparison with the historical 83% model/previous journal format.

| Cost B | A after compaction | B final |
| --- | ---: | ---: |
| Cumulative decoded payload | 151,877,141 | 268,742,908 |
| Stored payload | 26,635,524 | 87,765,774 |
| Live logical ranges | 151,856,365 | 172,504,063 |
| Referenced stored blocks | 26,635,516 | 35,283,117 |
| Wholly unreferenced stored payload | 8 | 52,482,657 |
| Header/metadata/outer checksum | 11,406,654 | 28,492,437 |

Partially live compressed blocks retain their whole storage cost; logical live bytes are not subtracted from stored bytes. B replaced payload is disclosed; no second out-of-scope compaction added. Independent payload 1,557,752B within final all-records allocation 3,193,528B is not counted twice; this includes previous results. Shared runner is 14 files/2,294,448B, preserving 7 files from a previous content key. Original Library 1,852,496,112 allocated B/30,748 files is a separate baseline. Evidence about 1.263GB includes 8 failed/successful raw-comparison copies, separated from product workspace cost. File allocation does not claim MFT/directories/kernel cache/instantaneous peak.

<a id="메모리시간실패"></a>
## Memory, time, and failures

Sample writer buffer is 65,536B; A/B cumulative managed allocation 3,866,144/3,955,112B. Elapsed 57,944/7,285ms includes different import/cache/durable flush and is not performance-superiority comparison. Cumulative managed allocation for raw generation/two private replay/state-hash comparisons is A before/after 194,709,360/169,363,424B, B 393,757,088B. Not whole-file buffers or peak.

Maximum observed provider working/private samples are A 140,529,664/107,995,136B, B 148,160,512/115,240,960B. Maximum application-child sum working/private samples are A 3,390,918,656/3,883,597,824B, B 6,494,679,040/7,402,373,120B. Shared working-set pages may double-count; not attributed to total RAM/codec cost. Native Brotli memory, total peak/RAM reduction, and per-task VSS attribution unknown. Codec buffer-bound/pool-return conformance is distinguished from memory savings.

Ordinary-user elapsed ms: ensure A/B 59,092/30,583; A stop/compact/checkpoint/release 35,639/17,907/10,458/13,597; B stop/checkpoint/release 30,965/10,226/13,311. Preparation/observation/external-tool/failure/measurement costs also remain in raw costs; this one set is not a time SLA.

- Initial eval/first eval after reload 5-second main-thread timeouts retried only the same-target read-only query once each. No write/recompile repetition due to timeout.
- Preserved B reload recompile_status connection failure at previous 7802. Completed confirmed through same owned PID/heartbeat/exact-path status; new 7801 not fixed.
- First B comparison detected input changes after stop. First added frame was `Library/ArtifactDB-lock`, payload 0/448B metadata; sequence subsequently increased. Cause unresolved. Failed artifact `b-final-journal` preserved; only comparison bound to normal checkpoint sequence 61,687 / 116,258,211B / hash `9aa877ef0d640cae6122bccb418ef31483605a29746dab577451d7052226f39e` adopted. Release's post-unmount seal guard also passed. Editor termination is not journal immutability.

No new A/B, original integrate/check-in, MCP, Play/Test/build, large/ARM/OS/power failures, flush/group commit, new backend, automatic modes/queues, or total memory optimization executed. Full SF-02/03 Partial/existing C: pending/unknown remain.
