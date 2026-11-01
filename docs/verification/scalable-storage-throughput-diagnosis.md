# Scalable-storage throughput diagnosis

The subsequent user-authorized implementation and bounded verification are tracked in the [throughput follow-up](scalable-storage-throughput-followup.md). The original observations below remain historical evidence.

[한국어](../ko/verification/scalable-storage-throughput-diagnosis.md) · [SCAL qualification](scalable-storage.md) · [Machine report](../../reports/benchmarks/scalable-storage-slowpoints-20261101.json)

The actual 100,000-entry tiny-file inputs exposed **unacceptably long preparation, writes, capture and planning for an interactive development workflow**. Removing aggregate limits has not established usable throughput. These are recovered functional runs with concurrent host load and mixed frozen stage binaries, not matched benchmarks or predictions for an ordinary large project.

Status: **Throughput concern open; verification reduced at the user's request.** This record uses existing evidence and code inspection only. It adds no runtime change, profile, experiment or permission to resume verification. SCAL-03, SCAL-07 and SCAL-08 remain open in the [owning ledger](scalable-storage.md).

## Input and timing boundaries

Each fixed Git/None/Plastic input contains 10 directories and 99,990 regular 32-byte files: 100,000 entries, 3,199,680 aggregate file bytes, and 16 initial payload variants. The basis therefore has only **512 unique logical payload bytes**. This intentionally stresses file metadata, proof and ownership work; it does not qualify mostly unique large assets or payload bandwidth.

All timestamps below are 2026-11-01. Artifact timestamps locate observational intervals, not exclusive function timers. Recovered calls, subphase observations and overlapping process samples must not be added into one lifecycle total. Historical RI/GCO/MX timings do not provide a matched baseline for this candidate.

| ID | Existing observation | Seconds and outcome |
| --- | --- | --- |
| OBS-N1 | None: eight drained workers write all 99,990 files and set fixed modification times | 2,944.167689; 3,199,680 bytes written |
| OBS-N2 | None: first `capture-1` | 3,869.3736515; immutable result `60864f835b308dccc6c2ec96`, fixed source sequence 299,970 |
| OBS-N3 | None: first read-only `integrate --plan` | 3,945.0931925 before explicit cancellation at 15:36:54 +09:00; no completed plan response or original publication |
| OBS-G1 | Git: same B preparation after the record-timer correction | 2,476.9713499; recovered functional preparation succeeded |
| OBS-G2 | Git: final canonical B check | Whole command 766.1348281; its incremental source inspection 760.2953989, 99,990 files reused, zero payload/Git-hash input |
| OBS-G3 | Git: actual fixed-capture CLI-loss recovery | 7,221.1475198; same request/root/digest recovered, independent result and later edit preserved |
| OBS-P1 | Plastic: A same-key preparation including recovery/revalidation | 10,488.1015401; succeeded after revalidating 58,332 previously admitted nodes; not a fresh nominal trial |
| OBS-P2 | Plastic: saved READY through A response | First READY observation 14:37:42 +09:00; process return about 15:20:02 +09:00, roughly 42 minutes. This is not an exclusive timer for metadata revalidation. |

Raw sources: None [writes](C:/Users/olive/AppData/Local/Temp/sscal1n/evidence/bulk-edit-metrics.json), [capture](C:/Users/olive/AppData/Local/Temp/sscal1n/evidence/capture-1.process-metrics.json), [plan](C:/Users/olive/AppData/Local/Temp/sscal1n/evidence/apply-1-plan.process-metrics.json); Git [B preparation](C:/Users/olive/AppData/Local/Temp/sscal1g/evidence/ensure-b-initial-r18-git.process-metrics.json), [canonical command](C:/Users/olive/AppData/Local/Temp/sscal1g/evidence/ensure-b.process-metrics.json), [inspection records](C:/Users/olive/AppData/Local/Temp/sscal1g/h/records/b87030261d7b7141d5bc66f2/source-inspections.jsonl), [recovery](C:/Users/olive/AppData/Local/Temp/sscal1g/evidence/native-cli-loss-recovered-continuation-metrics.json); Plastic [A preparation](C:/Users/olive/AppData/Local/Temp/sscal1p/evidence/ensure-a-same-request-resumed-r19.process-metrics.json) and [slow-phase handoff](C:/Users/olive/AppData/Local/Temp/sscal1p/evidence/slow-phase-handoff-r19.json). These local raw files are machine-specific; the checked-in machine report retains their exact paths.

## Where the long capture intervals occurred

**None, OBS-N2**, +09:00:

| Existing boundary | Time |
| --- | --- |
| CLI process start | 13:26:39.6417783 |
| Fixed capture control | 13:26:40.4788448 |
| Generation basis header | 13:27:46.6939741 |
| Preserved-range/adoption map, 99,990 rows | 13:56:36.4086447 |
| Generation header | 13:57:01.4549226 |
| Workspace result state | 13:57:57.7433953 |
| Source materialized marker | 14:30:06.1286749 |
| Response file | 14:31:09.1516585 |

The measured store mutation pause was 0.0833245 seconds and callback pause 0.0862686 seconds; they overlap. The hour-long capture occurred outside that short freeze. The workspace-state-to-materialization observation spans about 1,928 seconds, but it is not solely a flush timer. See the [None handoff](../../artifacts/scalable-storage-tests/native-none-slowpoint-handoff-20261101.md) and [materialization observation](C:/Users/olive/AppData/Local/Temp/sscal1n/evidence/capture-1-source-materialized-observation.json).

**Git, OBS-G3**, UTC:

| Existing boundary | Time |
| --- | --- |
| Recovery CLI start | 04:25:29.8438305Z |
| Generation basis header | 05:04:34.2288105Z |
| Owned result-sort output observed gone | 06:08:09.2391176Z |
| Result / generation receipt | 06:24:28.1574743Z / 06:24:28.4375879Z |
| Recovery response | 06:25:51.078383Z |
| Wrapper confirms fixed bytes, retry and later edit | 06:26:30.6125413Z |

The sort observation is not an instrumented sort-end boundary. Store/callback pauses were 0.1068701 / 0.1104373 seconds, also overlapping. The actual CLI-loss case passed: one exact actor interruption, the original request/root/digest, independently retained fixed bytes, a completed-request retry and the later workspace edit. A harness alias-binding failure occurred before the recovery call and was retained; the actor or later write was not repeated. [Completion proof](C:/Users/olive/AppData/Local/Temp/sscal1g/evidence/native-cli-loss-complete.json) · [owned-output progress](C:/Users/olive/AppData/Local/Temp/sscal1g/evidence/native-cli-loss-owned-artifact-progress-2.json).

## Confirmed defects already corrected

These corrections have focused qualification in the [integrated report](../../reports/benchmarks/scalable-storage-integrated-20261101.json); they do not prove whole-lifecycle performance preservation.

| ID | Confirmed cause and bounded correction |
| --- | --- |
| FIX-01 | R16-cache: `Dictionary.First` did not provide recency when removed slots were reused; eviction also scanned the global metadata cache. Explicit 16-handle LRU and direct scope/container invalidation replace that work. A held directory cursor now defers live GC before an expensive copy/replay attempt, retaining the final reader guard and same intent. |
| FIX-02 | R19 Plastic: the actual R16 managed stack placed repeated server heartbeats in full manifest/catalog verification. An already admitted immutable reader now holds exact Native identity/path/length and write/delete fences; scoped heartbeats inspect physical bindings, while ordinary observation and full `VerifyReadBoundary` remain. |
| FIX-03 | R18 Git: a clean, data-scaled status walk can produce no output for more than 30 seconds. Its record stream incorrectly inherited the fixed-object blocked-read timer. Only that inappropriate record timer was removed; cancellation, owned-child/stderr drain, record validation and fixed-object reader deadlines remain. |
| FIX-04 | R19 GC recovery: startup segment retirement changed the segment catalog before a pending live repack resumed. The same pending repack now recovers first. Reconciliation permits only proven absent empty normal headers after all other roots/counters/ACK seals match; nonempty removal or other proof mismatch still refuses. An actual killed-child case and the same native request recovered. |

For FIX-02, the same existing 100k snapshot measured full observation 5.5830412 seconds, full scope admission 5.4950772 seconds, **100 scoped observations together 0.9958472 seconds**, full boundary 4.6148969 seconds, and later full observation 5.0921995 seconds. Full-manifest-validation counts were 0 → 1 → 2 → 2 → 3 → 4. Eight fences bound 21,390,437 **logical metadata bytes**, with zero fence handles after disposal. This is one [component probe](../../artifacts/scalable-storage-tests/native-plastic/r19-same-snapshot-scope-component.json), not physical allocation, server throughput, isolated RAM or a whole-lifecycle gain.

## Remaining work found in code and existing counters

| ID | Evidence level and remaining cost |
| --- | --- |
| COST-01 | **Code-confirmed:** first `FileWorkspaceProvider.PreserveGenerationAsync` scans the basis when its header is absent and scans the complete fixed result. `ScanTree` streams and hashes every regular file. Later fixed basis headers avoid that first basis rebuild, but complete result enumeration/hash work remains. The Git artifact intervals locate this path; they do not apportion its CPU or I/O. |
| COST-02 | **Code-confirmed; timing inferred:** `FileInspectionProof.RevalidateAsync` performs a full metadata-only workspace scan and disk-index root comparison for the editorless response. This is a plausible major part of Plastic's READY-to-return interval, not a measured exclusive attribution. Git's zero-payload incremental inspection still took 760.2953989 seconds, demonstrating substantial non-payload cost. |
| COST-03 | **Code-confirmed; exact plan attribution unknown:** original planning verifies the retained generation, builds basis/result/selection indexes, acquires source protection, observes the original namespace and reads selected target versions. The None plan was canceled before publishing a plan; its elapsed time is a lower bound on completion time, not a failed file publication. |
| COST-04 | **Counted and code-confirmed:** range adoption validates frozen bytes against canonical content and durably commits matching live ranges. None recorded 99,990 `adopt-range` commits, 361.542 cumulative operation seconds and 282.786 flush seconds. These counters cannot explain the entire roughly 1,928-second backend observation interval. |
| COST-05 | **Static candidate only:** paged Git inspection uses 256-file summary batches. After restart, the optional initial-content proof may be unavailable, yet a null summary does not stop later batch requests. A full 99,990-file pass could therefore issue about 391 summary RPCs before/alongside fallback reads. This was not counted or timed in the native run, and no capability/negative-result optimization was implemented. |

Code: [generation scan/preservation](../../src/Splinter.SourceControl.Files/FileSourceGenerations.cs), [response proof](../../src/Splinter.SourceControl.Files/FileInspectionProof.cs), [original planning](../../src/Splinter.SourceControl.Files/FileOriginalApply.cs), [range adoption](../../src/Splinter.Runtime/JournalRangeAdoption.cs), [paged Git inspection](../../src/Splinter.SourceControl.Git/GitPagedSourceFingerprint.cs), [optional summary](../../src/Splinter.Runtime/JournalInitialContent.cs).

None's persisted counters also contain 99,990 commits each for write, resize and metadata, 91,627 packing-state commits, 91,571 range relocations, seven completed packing jobs, 583,167 total commits and 573,403 flushes. Operation and flush counters are cumulative and overlap other work; they are not disjoint wall-time components or counts of automatically unnecessary operations.

The 32MiB page-cache peak is not whole metadata memory. During None capture, sampled peaks were 156,463,104 CLI private bytes, 508,272,640 tracked-process private bytes and 401,986 tracked handles; `samplingErrors=[]`. These 100ms samples are lower bounds and include the tracked process group. Native ownership/whole-target protection can scale with file count outside the cache budget.

After None materialization, source-private payload was zero and the journal was 671 bytes while the immutable result remained retained. This establishes adoption/compaction of that source, not zero total-home allocation. No new post-bulk allocation scan was run after the user reduced verification.

## Storage lifetime is a separate cost

Managed None preparation moves the original basis and builds indexes without copying its payload. The original must later become ordinary mutable NTFS again, so it cannot replace independently immutable result storage. The first independent retention can copy each unique basis version into canonical CAS; A/B and later results share those objects. Basis/CAS coexistence is additional lifetime-separation overhead, not proof that all duplication was removed or all those bytes are unavoidable.

Count preexisting basis Native identities separately from new CAS identities and metadata/controls. The 512-byte unique initial payload in this fixture makes it unsuitable for measuring retention of mostly unique large assets; new edited versions can require additional immutable bytes. No phase allocation or percentage saving is inferred from logical payload size.

## Prioritized follow-up questions

These are unimplemented questions, **not new execution authorization**:

1. **Q-01:** Can fixed basis/result metadata and verified canonical identities eliminate duplicate scans or hashes under the same reader lease and generation proof, while retaining full publication-boundary validation?
2. **Q-02:** Can ordered, bounded adoption/packing batches reduce per-row metadata/flush work with exact durable recovery and unchanged accepted-write ACK semantics?
3. **Q-03:** Which full original-observation/index passes are repeated, and can streamed comparisons/independent bounded I/O remove repetition while retaining whole-target/ancestor locks, conditional publication and companion/ownership checks?
4. **Q-04:** Can the optional initial-summary capability be rejected once per exact provider lifetime after restart, rather than once per batch, without treating unavailable proof as verified content?
5. **Q-05:** Which active-container proof/page operations dominate metadata-only work after the qualified cache fixes? A future small targeted attribution must preserve Native replacement detection, immutable-reader fences and full hash proof; no cache-only availability shortcut is justified here.

## User-requested stopping point

The two expensive read-only CLIs were terminated by exact PID/start identity: None plan and Plastic B ensure. Seven owned providers received one authenticated normal Stop each: Git A/B, None A/B/original, Plastic A/B. All reported process/mount absent. Tasks, bases, private deltas, immutable results, request identities and owned evidence remain; normal Stop is not release, discard or managed-original restoration.

Git retained the passed CLI-loss result and later edit; its 100k bulk body was never started. None retained its one capture with zero original applies; the empty `apply-1.json` is an explicit harness barrier, not a CLI response. Plastic A preparation passed and B was canceled after 427 admitted nodes, source ACK sequence zero and no results.

No further 100k phase, selected/publication/query matrix, real 17GiB case, matched ABBA, allocation sweep or latest whole build gate was launched after steering. The three small actual Unity lifecycles and separate cleanup are separate completed evidence; they do not fill these missing large-input or performance gates.

Closeout: [Git](C:/Users/olive/AppData/Local/Temp/sscal1g/evidence/minimal-closeout-normal-stop-r19.json), [None](C:/Users/olive/AppData/Local/Temp/sscal1n/evidence/user-closeout-stopped-preserved.json), [Plastic](C:/Users/olive/AppData/Local/Temp/sscal1p/evidence/minimum-closeout-r19.json). No remaining experiment or fixture deletion is automatically authorized by this document.

