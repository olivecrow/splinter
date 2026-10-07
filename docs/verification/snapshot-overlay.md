<a id="ntfs-snapshot-overlay-실행-증거"></a>
# NTFS snapshot-overlay execution evidence

[한국어](../ko/verification/snapshot-overlay.md)

**SO-01–06 fixture verification completed** on 2026-09-21 with Windows 11 Home 10.0.26200, NTFS C:, .NET 10.0.112, WinFsp 2.2.26215 Beta4, Unity 6000.3.22f1. Range storage, actual Git/Unity execution, same-basis reconnect, result preservation, and owned-resource reclamation were confirmed without initial full copying/original relocation/read hydration. This does not mean product-workspace backend support, reboot/power-loss/general Unity support.

Raw evidence remains in repository artifacts/snapshot-overlay/. Below S is run-20260921-a/, U unity-20260921/, setup installation/approved VSS-setting receipts. Source/delta/mount reclaimed. Independent results/small journal-failure samples/logs/JSON retained.

| Gate | Status | Actual evidence |
| --- | --- | --- |
| SO-01 | PassedFixture | Nonsparse/uncompressed random 16MiB/256MiB/1GiB, original C: retained, VSS capture 16,031ms, initial A/B journal/payload 0. S/source-files.json, S/01-initial.json, snapshot-receipt volume/provider. Unity clean HEAD/independent indexes U/git-*-initial.json |
| SO-02 | PassedFixture | Full repeated SHA-256 matches. Corrected measurement: B reads 2,717,908,992 bytes, delta allocation 8,192→8,192 bytes. Engine read cache 0. S/02-reads.json |
| SO-03 | PassedFixture | Same 4KiB writes to 16MiB/256MiB/1GiB: journal +4,521/+4,525/+4,529 bytes, total allocation +12KiB. mmap boundary, extension after truncate 0, successful rename/replacement. S/03-physical-small-writes.json, S/03-filesystem-operations.json |
| SO-04 | PassedFixture | A/B retain t0 during original writes/deletes/renames; independent private markers; C observes new t1. Original Editor runs 33, A/B 103/203. S/04-source-and-private-writes.json, S/04-new-snapshot.json, U/original-live-probe-33.json, U/unity-*-resumed-probe.json |
| SO-05 | PassedFixture | GUI A/B with original open, independent Git indexes, child I/O. After further changes A=104/B=204, new requests/fingerprints/domain/assembly IDs checked. Create/change/rename/delete notifications, sharing lock 32, byte-range lock 33. U/unity-*-final-validation.json, U/unity-*-final-probe.json, S/05-notifications-locks-child-io.json |
| SO-06 | PassedFixture | Same snapshot/delta resumption, provider termination during write/flush, quota disk-full, torn/checksum corruption, actual owned-snapshot loss, export reconciliation, reclamation waiting on open handles, final process/memory/mount/Git/delta/snapshot reclamation |

<a id="unity와-공간메모리-비용"></a>
## Unity and storage/memory costs

Actual independent fixture: C:/Users/olive/Desktop/Development/splinter-unity. Capture basis is clean development at c0a60da2a889f03cf514536658fded605dc5b68d and saved/compilation-idle original. Original Editor PID 25756 remained open. VSS capture 16,843ms, 33,500 files, logical 1,956,139,527 bytes, summed file-data allocation 1,995,396,232 bytes. Logical Library 1,939,726,219 bytes. Full metadata measurement itself took 237,538ms, excluded from creation latency.

| Item | A | B |
| --- | ---: | ---: |
| Journal / private pointer payload after Git connection before Editor | 851 / 81 bytes | 851 / 81 bytes |
| Delta file-data allocation before Editor | 4,688 bytes | 4,688 bytes |
| Final running delta-allocation observation | 600,375,888 bytes | 618,725,968 bytes |
| Final journal after shutdown/result preservation | 603,464,860 bytes | 619,874,195 bytes |
| Cumulative Library-write payload | 537,143,970 bytes | 552,750,892 bytes |
| Last provider OS peak working set | 130,629,632 bytes | 127,500,288 bytes |
| Final running application Job active processes | 21 | 19 |
| Application Job working set at same time | 5,474,381,824 bytes | 5,356,871,680 bytes |
| Application Job private bytes at same time | 6,244,192,256 bytes | 6,071,443,456 bytes |
| Final owned application/provider memory | 0 | 0 |

Running allocation was immediately before shutdown, differing from final journal including shutdown writes. Costs include two Editor runs/source compilation/bridge updates/generated IDE files/repeated logs. Library payload is cumulative Unity-requested writes, not initial original copying. Hydration 0/no compaction. Application Job figures are point-in-time totals, not continuous peak across all runs. Shared OS system-cache resident 1,187,045,376 bytes recorded separately. Before/after C/D/E/G free space in snapshot receipts/measurement JSON includes external-write noise.

Actual resumed Editor PIDs: A 74364, B 7100. First start caused substantial Unity imports; readiness around 4–6 minutes observed. Resumption tracked by unity-*-launch-2.json/unity-*-resumed-probe.json times. Final incremental compilation completed within about 40 seconds after request; actual loaded assembly MVID matched generated DLL. Fast Editor starts/general Unity performance advantages are not established.

<a id="장애와-발견한-호환성-문제"></a>
## Failures and compatibility issues discovered

- 06-held-handle-and-reconnect.json: unmount waits on open handle. After closure, normal release/same-basis reconnect preserves acknowledged data.
- 06-provider-kill-during-write.json: exact owned provider terminated during write-through child/flush. All externally recorded acknowledged 1,835,008 bytes recovered. Additional persisted but unresponded final transaction permitted.
- 06-quota-disk-full.json: Windows 112 at 64KiB journal budget; acknowledged payload 57,344 bytes confirmed after reconnect. Actual host-volume exhaustion NotRun.
- 06-journal-recovery.json: only incomplete 19-byte tail removed, acknowledged journal hash retained. Complete checksum corruption returns DeltaCorrupt/preserves corrupt journal byte-for-byte. Only test-injected bytes restored from verified backup, then same basis reconnected. Not automatic general-corruption recovery.
- 06-snapshot-loss-observed.json: only independent-result-preserved C/fault/quota t1 actually deleted. C snapshot-missing, read/write error 21, remount SnapshotMissing. A/B/existing restore points retained without original bypass/new capture.
- Result export stopped on hash-handle sharing error; already-written files reverified against overlay/manifest completed. Initial failure lacked final acknowledged manifest. General arbitrary partial-file automatic recovery unimplemented.

First Unity execution found snapshot Library/ilpp.pid pointing to original helper PID; Unity terminated original helper. Original Editor/code results remained, but isolation was defective. Changed to **whiteout only task-specific ephemeral PIDs/ports** before execution; reran A/B with same snapshot/delta, confirmed original new helper PID 1924 alive. Original full Library/global MCP settings were not deleted.

First WinFsp provider counted kernel-cache contexts as open user handles, leaving reclamation waiting after application exit. After confirming both application Jobs 0, exact owned provider terminated/resumed with same journal. Current Cleanup/Close separation observes openHandles/retainedFileContexts. Final reclamation completed with normal unmount/provider exit.

Existing probe's initial callback report did not update after later reload. Compiler success alone was insufficient; actual loaded functions were called through ready bridge, confirming 104/204/assembly MVID. First forced full-script-compilation incomplete records remain in U/first-run-* and are not successes.

<a id="독립-결과와-최종-회수"></a>
## Independent results and final reclamation

| Result | Preserved location |
| --- | --- |
| A source | Fixture branch codex/so-20260921-a, commit bfaabd522ab5cdcf366d29ba41f25e3bcc8b80b2, ref refs/splinter/snapshot-results/so-unity-a |
| B source | Fixture branch codex/so-20260921-b, commit 87eb6177c3541699c42608297d3c0c03818d96cd, ref refs/splinter/snapshot-results/so-unity-b |
| Synthetic binary results | S/results/{a,b,c,fault,quota,compat}/result.json/SHA-256-verified files. About 2.70GB, result-preservation costs separate from initial workspace copies |
| Original restoration | U/original-restored-probe.json: PID 25756, Calculate(3)=13, compiling/updating false. Only reusable settings/documentation commits retained |

S/06-reclamation.json/U/06-reclamation.json record owned process 0, memory 0, open handles 0, no mount/Unity Git registration, delta 0 after checking all current manifests/durable sequences/hashes/refs. final-owned-temporary-cleanup.json records removal of random test source/immutable runner cache. Raw logs/small failure samples/independent results intentionally remain.

final-vss.json confirms absent owned t0 {14D604AB-4D25-4DF4-84D5-BE4ED8379CE8}, t1 {B00DEE89-0C00-4B02-846A-7B02706207DF}, Unity {AB041222-44EC-408F-BF25-03C9E100FB36}; 4 existing restore points preserved. Approved WinFsp installation remains. C: VSS limit increased 39,986,397,184→68,719,476,736 bytes, not automatically reduced. Final used 40,111,124,480 / allocated 42,959,630,336 bytes are volume totals including existing points/writes outside project. No immediate post-deletion VSS allocation shrinkage/exact per-task preserved-byte claims.

<a id="자동-검증과-남은-범위"></a>
## Automated verification and remaining scope

pwsh scripts/build.ps1: locked restore, 0 build warnings/errors, 68 passed/1 test at that time skipped out of 69. Eleven snapshot fakes check ranges/overlap/read/replay/loss/result-release conditions; two mount-observation regressions check that missing memory fields in failed states are not treated as 0 and check unstarted state. Fakes do not replace actual evidence. Preparation/combined Unity-preservation wrappers added to reproduce verified configuration commands; no evidence of rerunning full wrapper on a new fixture. Syntax checks passed for 21 PowerShell files/journal-accounting Python.

Initial allocation-measurement error is recorded in S/measurement-v1-invalid/invalidity.json. That bundle/earlier filesystem-operations physical-allocation fields are invalid. Hash/operation results remain valid; current 02/03 physical measurements corrected with FILE_STANDARD_INFO.AllocationSize.

Reboot/power loss, host physical-space exhaustion, forced termination at every mount/flush/release-command boundary, ADS/hardlink/reparse/EFS/ACL, dirty/LFS originals, packages/arbitrary asset changes, PlayMode/Unity Test Framework are NotRun or Unsupported. Actual 1TB dataset did not run. Integration assessment: **retain candidate/proceed with product-coordinator design**; commercial support/performance conclusions withheld.

<a id="현재-개발-script의-자동-회귀-경계"></a>
## Current development-script automated-regression boundary

`SnapshotScriptLifecycleTests` checks source preservation → provider shutdown → stopped-journal measurement → Git-registration reclamation with external-command substitutes/own temporary files. Unmount/measurement failure must preserve independent-source receipt/journal/registration and stop subsequent reclamation. Internal I/O-module import does not change caller path variables/types; tests check JSON publication/range-write/read results at two explicit run paths.

Current helper Windows-allocation query checked only matching logical/allocated totals for one owned 8,192B file. Evidence: `artifacts/code-efficiency-fixes/helper-allocation/report.json`, not new qualification for VSS/Unity/whole-workspace costs. Journal-measurement regressions: `artifacts/code-efficiency-fixes/journal-accounting-tests.log`; required locked restore/Release build 0 warnings/errors, automated regressions 1,051 Passed / 0 Failed / 10 conditional Skipped out of 1,061. Logs/current TRX: `artifacts/code-efficiency-fixes/required-build.log`/`required-build.trx`. Existing SO-01–06 native execution remains evidence for its historical candidate; actual Unity/VSS rerun of this wrapper/helper candidate is NotRun.
