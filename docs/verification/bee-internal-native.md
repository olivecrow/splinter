# Internal native Bee backing verification

[한국어](../ko/verification/bee-internal-native.md)

**2026-10-15: small correctness gates passed; complete performance gate failed.** This record owns BN-01–05 under the [atomic Bee storage design](../plans/bee-native-storage-design.md). The bounded product journal is committed as `d0ac0a2`. This candidate implements the selected single-volume structure in `tools/Splinter.BeeProbe`; it does not activate native Bee storage in ordinary product workspaces. The previous junction experiment and its atomic-rename failure remain in the [SX record](structural-throughput-prototypes.md).

The new structure completes actual Bee compilation and preserves source/results, but the mean measured phase sum rises from **11.184s to 18.883s (+68.8%)**, and retained allocation rises from **0.918MiB to 2.128MiB (2.318×)**. This candidate does not justify product adoption. [Exact data, hashes and provenance](../../reports/benchmarks/bee-internal-native-20261015.json) retain all four successful runs separately from the failed pilot.

## Evidence status

| ID | Evidence | Status |
| --- | --- | --- |
| BN-01 | Single namespace, file identities, no-copy inherited writes, source promotion and original/peer protection | Passed within the small mounted/direct-store boundaries below |
| BN-02 | Durable source, native flush, exact owner/inventory and same-owner process recovery | Passed: 10 interruption boundaries and ownership guards |
| BN-03 | Mapped data and executable images, generic cache drainage, normal stop | Passed on `native-safety-06`; live mapping/image stops are refused |
| BN-04 | Actual cold/unchanged/incremental Bee builds, constants `42 → 43`, serial ABBA time/storage/RAM | Correctness passed; complete performance acceptance failed |
| BN-05 | Required locked restore, Release build/full regression and paired documentation | Passed within the stated development-candidate scope |

## Implemented candidate

One WinFsp project volume owns every logical name and file identity. Newly created `Library/Bee` files receive opaque private NTFS payloads; no Bee junction or native client path is exposed. The bounded journal remains the namespace authority and stores source data. Reads and partial writes of inherited Bee files continue to use immutable basis ranges plus private journal ranges. Neither reading inherited data nor partially overwriting it materializes a native copy.

Native generated payload uses ordinary NTFS buffering. Explicit flush reaches all native payloads and the journal; source writes retain per-mutation durable acknowledgement. Moving a newly generated file into source first imports and verifies its application-written bytes in the durable journal under the same identity, then publishes its source path. Later writes through an already open handle use the journal. Source files moved into Bee remain journal-backed. This is a durability tradeoff for regenerable data, not an equal-durability comparison or a source ACK relaxation.

Owned roots/ancestors, opaque payload identities, policy, associations and current-version recovery intents are checked. Unknown files, wrong ownership, unsupported pending transitions and hardlinks are preserved and rejected. A native payload has a separate byte budget; journal observations are explicitly journal-only. Native control files and payloads are accounted separately. Promoted native payloads remain as owned historical input until reclamation, so their bytes can temporarily coexist with durable source journal bytes.

## Namespace and source safety: BN-01

The final mounted fixture is `artifacts/bee-internal-20261015/native-safety-06/`. It passes 15 check rows and normal closure with zero open handles and retained contexts. The checks include native `MoveFileExW` with copy fallback disabled for relative and absolute paths, both a new name and replacement. The source file index survives publication. Directory and case-only renames preserve identity and contents.

Real replacement tests close the destination first; real directory moves close descendant handles first, matching the tested ordinary Windows operations. Old replaced native/inherited identities and moved descendant identities are separately verified through the direct delta contract until explicit release. These results do not claim POSIX replacement/unlink semantics. A delete-pending name may remain visible until the final handle closes.

A 64MiB inherited sparse file is read and patched with 4KiB at offset 17,825,915. Native payload/file growth and initial copied payload remain zero; the journal grows 760B for this compressible patch and metadata. A newly generated file can replace an inherited name without copying inherited bytes. Zero/short/multi-chunk/offset writes, zero-filled holes, truncation/re-extension and timestamps are also checked.

Source transfer retains identity and writes through the existing handle. Both independently exported 27-byte results are verified after unmount, and again while all basis/native inputs are denied read access by owned exclusive handles. Original fixture hashes and a separate peer sentinel remain unchanged. This is a small fixture with an independent peer sentinel, not a new product A/B lifecycle or Git/Plastic original-apply qualification.

A directory containing unpromoted native descendants cannot cross into source in this candidate. The direct-store request is rejected before namespace publication and keeps its contents. This deliberate limitation prevents claiming transparent product filesystem compatibility.

## Recovery and ownership: BN-02

`artifacts/bee-internal-20261015/recovery-02/recovery-result.json` records **10/10** exact owned-child interruption cases: two creation boundaries, a native payload flush, namespace rename, four source-promotion boundaries and two payload-reclamation boundaries. Each reconnect uses the same identity, preserves an acknowledged durable source sentinel, completes the operation and reopens a second time. Promoted source exports remain readable after the store is disposed; original input bytes remain unchanged.

The guard checks preserve and refuse unknown files, another journal owner, a mismatched pending owner, a pending association with the wrong native file identity, an unrelated same-owner pending operation and a hardlink. The active native root cannot be replaced. Pending documents are validated against their semantic owner/identity and allowed transition before replacement; a valid checksum alone does not authorize overwriting the existing generation. Stopped proof checks exact inventory, payload identity/content and owner/association/promotion control contents.

These tests terminate physical-store children, not a mounted provider or Editor. Reparse injection is explicitly **NotRun** in this recovery fixture. OS crash, power loss, hostile concurrent mutation and an exhaustive filesystem attack matrix are not qualified.

## Cache and mapping lifetime: BN-03

The candidate keeps `FlushAndPurgeOnCleanup`. Its development-only stop barrier verifies the exact mounted root/volume, fences new opens while admitting only its own root flush handle, flushes the kernel cache and sends WinFsp cache invalidation notifications for currently linked retained identities. Deleted identities are never notified through a reused path. Success still requires real final `Close` callbacks and zero handle/context counts; the barrier neither resets counters nor forces unmount.

The mounted safety check closes the original file handle while retaining a writable data mapping. Stop is refused, and the mapping remains readable/writable afterward. A loaded executable image likewise refuses stop and remains readable. After unloading that image, the same generic barrier drains normally and preserves the output file. No filename-specific reopen or deletion is used for these passing checks. This does not prove absence of arbitrary foreign mappings.

The initial `bee-pilot-01` completed all three actual Bee builds but timed out at normal closure with zero open handles and one retained context. Its provider and outputs were preserved. A separately recorded recovery reopened the exact `1900b0aE.dag_derived` file with write-data/read-attributes access and closed it without altering its content, metadata or protected inventories. The provider then exited normally and preserved independent source results. That recovery is historical failed-run handling: its final `passed` provider receipt does **not** turn the failed timing sample into a successful cohort. The current measurement script contains no such filename-specific cleanup.

## Actual Bee measurement: BN-04

The fixed input is the preserved three-assembly Unity 6000.3.22f1 fixture: no packages, Editor, IL post-processing or Play. The script checks frozen input and runtime hashes, then runs the installed graph generator and Bee/Csc backend without patching external tools. Each fresh view starts with empty generated output and performs one graph generation plus cold, unchanged and one-source-change builds. Compiled `RuntimeSentinel.Value` must read `42`, `42`, then `43`. State/digest files must be published within the same virtual volume, with no leftover temporary publication file; native rename telemetry must retain logical identity.

The final `artifacts/bee-internal-20261015/bee-abba-01/` comparison runs bounded-overlay / internal-native / internal-native / bounded-overlay serially after all builds and tests stop. All four runs pass the compiled constants, equal normalized graph/payload, unchanged frozen inputs/runtime, independent source results, normal unmount and zero handle/context gates. Each native run publishes the state and digest cache three times with preserved logical identity. The earlier failed pilot contributes correctness/diagnostic evidence only.

Means of two fresh views per variant:

| Measured phase | Current bounded overlay | Internal native Bee |
| --- | ---: | ---: |
| Provider preparation | 0.344s | 0.419s |
| Graph generation | 1.207s | 0.720s |
| Cold generated-output compile | 7.063s | 11.746s |
| Unchanged build | 0.422s | 0.416s |
| One-source-change build | 1.359s | 1.441s |
| Assembly constant inspection | 0.488s | 0.565s |
| Input publication and source edits | 0.070s | 0.120s |
| Normal completion | 0.231s | 3.455s |
| **Measured phase sum** | **11.184s** | **18.883s** |
| **Provider start → normal exit, wall time** | **11.573s** | **20.911s** |

The phase sum increases **68.8%** and wall time increases **80.7%**. Native cold compilation increases **66.3%**; unchanged and incremental work do not show a substantial gain. Graph mean time decreases 40.3%, but the first overlay graph takes 1.688s while its later graph takes 0.727s, close to the native 0.730s and 0.710s. OS and compiler-server caches are not reset, and the fixed order has two samples per variant. “Cold” means empty generated output in that view, not cold machine caches. The graph ratio is not an isolated causal estimate or a whole-workflow benefit.

| Serial run | Variant | Graph | Cold compile | Phase sum | Wall time |
| --- | --- | ---: | ---: | ---: | ---: |
| 0 | Bounded overlay | 1.688s | 7.822s | 12.900s | 13.289s |
| 1 | Internal native | 0.730s | 14.389s | 23.606s | 25.834s |
| 2 | Internal native | 0.710s | 9.103s | 14.160s | 15.988s |
| 3 | Bounded overlay | 0.727s | 6.303s | 9.468s | 9.857s |

Both native cold compilations and complete lifecycles are slower than both overlay runs. This is sufficient to reject this candidate against its bounded performance gate; it is not a statistical claim about all Unity workloads.

Provider completion subphases are included in normal completion above and must not be added again:

| Mean provider subphase | Bounded overlay | Internal native |
| --- | ---: | ---: |
| Fence, kernel flush and context drain | 0.014s | 0.731s |
| Explicit post-drain store flush | 0.003s | 0.743s |
| Seal, original check and independent preservation | 0.049s | 0.186s |
| Unmount, stopped reopen/proof and cleanup | 0.026s | 1.628s |

The closure wall interval also includes request polling, final receipt publication and process exit. Native runs retain 178 payloads and report 356 payload flushes: the root kernel flush and explicit post-drain flush each visit the payload set. Their accumulated native flush times are 2.257s and 0.655s respectively; these counters overlap the completion subphases. No dirty-only flush optimization or saving was measured. New-file creation additionally publishes three durable JSON controls, an empty payload flush and journal namespace metadata. Those control-publication costs are not individually profiled, so they remain a structural follow-up hypothesis rather than a quantified explanation of the cold-compile increase.

| Mean retained resource / observed process maximum | Bounded overlay | Internal native |
| --- | ---: | ---: |
| Final regular-file NTFS allocation | 0.918MiB | 2.128MiB |
| Provider sampled maximum working set | 62.723MiB | 65.084MiB |
| Provider sampled maximum private bytes | 29.992MiB | 36.053MiB |
| Direct-client sampled maximum working set | 46.213MiB | 45.859MiB |

The allocation increases **131.8%**. Native outputs use 358 backing files per run (178 payloads, 178 association records and two owner/lock controls), plus the three journal files. The mean delta allocation alone is 200KiB, but the combined mean is 2,231,384B; presenting only the smaller journal would hide the storage increase. Phase-sampled allocation maxima are 958,464B and 1,052,672B for the overlays, and 3,681,368B and 3,210,328B for native. They are observations at selected boundaries, not peaks.

Final allocation uses recursive `GetFileInformationByHandleEx(FileStandardInfo).AllocationSize` over the owned delta and native directories, including nested journal objects and native control files. It excludes the shared fixture, tools, independent source export, receipts, evidence copies and filesystem directory metadata. Intermediate allocation values are phase-boundary samples, not a temporary peak. Native payload logical length is not physical allocation.

RAM samples cover the provider and directly launched clients at approximately 100ms intervals. Compiler descendants, NTFS/system cache and complete machine memory are **NotMeasured**. Per-process observed maxima are not a simultaneous process-tree peak. Memory sampling is inside client timing intervals; phase allocation scans and evidence capture are outside named operation sums but inside the reported lifecycle wall interval.

## Regression and remaining boundary: BN-05

The final required `pwsh -NoProfile -File ./scripts/build.ps1` completed locked restore and Release compilation, then **1,691 passed / 0 failed / 24 conditional skips (1,715 total)** in **12m 18.103s**, with exit code 0. Raw log: `artifacts/bee-internal-20261015/build-final.log`. Native safety/recovery checks are separate development-probe evidence and are not silently included in that product test count. The final cohort records the executable/runtime identities independently from the source commit.

This candidate does not wire an application policy, native backing inventory, source classification and recovery into the ordinary product provider/CLI/seal/compaction/reclamation lifecycle. Original Library representation/restoration is unchanged. A complete Unity Editor workflow, ILPP, Play/Test/build through an Editor, large-project throughput, full memory/peak storage and sequential-Editor crossover remain **NotRun**. No service, UAC, global setting, external-tool or original Unity project change is part of this experiment. README's historical whole-workflow figures must not be replaced by these component timings.
