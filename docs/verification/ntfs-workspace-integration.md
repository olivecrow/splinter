<a id="ntfs-workspace-통합-검증"></a>
# NTFS workspace integration verification

[한국어](../ko/verification/ntfs-workspace-integration.md)

Status: **NI-01–08 first internal alpha implementation/automatic and actual fixture verification/reclaim complete**. Date 2026-09-21. Follows [fixed scope](../ntfs-workspaces.md)/[current contract](../ntfs-workspaces.md). Actual dirty-original integration blocking/original/result preservation also verified. Not expanded to full public-product support. SO/AA/SR results are separate evidence.

<a id="환경과-증거-식별"></a>
## Environment and evidence identity

- At execution, product candidate applied changes to starting `development` HEAD `17cee45190c9ac6800fd1737321ea4286a8696df`. Identified by source-input hashes below. `evidence/source-manifest.json` lists SHA-256 for 117 code/contract/bridge/script/build-input files; sorted path/hash JSON SHA-256 `58d097a406d76cd09f580903ddab2714a399800142ab74e75e35e4c71338ee85`. Documents excluded from hash. Not release-SHA qualification.
- Unity fixture `C:/Users/olive/Desktop/Development/splinter-unity`, `development`, `46e7f4dbdb219e4afa602f3598a3de9ff4070fae`, Unity `6000.3.22f1`. Original Editor PID `25756` kept running. Before capture MCP confirmed compile/import/Play/scene dirty all false.
- Relative evidence below rooted at product repo `artifacts/ntfs-workspace/ni-20260921/`. `home/records/<id>` durable state/ownership receipts; `evidence/` CLI responses/measurements/TRX. Local artifacts not Git tracked.
- Created via ordinary `scripts/splinter.ps1 workspace ensure --storage ntfs-snapshot`. Only capture/release used exact CLI-issued VSS helper via UAC. Ordinary CLI/Unity not administrator processes; SO experimental mount requests not substituted for product creation.

| Task | key / workspace ID | Captured VSS ID | Independent result revision |
| --- | --- | --- | --- |
| A | `ni-20260921-a` / `d3950bd69315ea337cfee26e` | `{D07DDDA4-771B-4B8A-A750-16CB849265A4}` | `02ad95182c794954bfa40239e925a15b2d77d102` |
| B | `ni-20260921-b` / `aecc7c5deec607ac2d927e9a` | `{1472A734-EF7A-4684-81C8-D072BA425797}` | `9828f6fa5e90aa4090562d42623f62e43418ffcb` |
| Binary | `ni-20260921-range` / `a078ea4e572e501c7aeccdb1` | `{A2801D3B-61AE-44D8-9F2E-8300E80A4374}` | `379d03b065a142235fcb7a345fa9511cc5656650` |

Each result at corresponding original repo `refs/splinter/snapshot-results/<id>`. Binary input separate owned Git fixture under run, not imported as Unity asset. Result refs/tree objects rechecked in original repositories after mount/delta reclaim.

<a id="ni-상태"></a>
## NI status

| Item | Current status | Evidence and remaining execution |
| --- | --- | --- |
| NI-01 Contracts/state/CLI | Passed: automatic/fixture | Storage/schema/capabilities, insufficient-privilege nextActions, actual capture→Git registration→mount→Ready, unknown observation distinction |
| NI-02 Duplicates/partial execution | Passed: automatic/representative resume | Fake concurrent same key/input conflict/per-stage response loss/released-key protection; actual new-CLI/provider reconnect/concurrent released-key repeats/input conflict. Interruption at all actual I/O boundaries remains follow-up |
| NI-03 Storage contract | Passed: synthetic fixture | Nonsparse/uncompressed 16MiB/256MiB/1GiB, initial payload/hydration 0, no allocation growth from repeated reads, same 4KiB-write measurement |
| NI-04 Original/A/B Unity | Passed: script fixture | Original 33/B 203 during A error/post-fix A 104/B 204 execution. Other `.meta` changes invalidate prior validation |
| NI-05 Reconnect | Passed: representative fixture | Provider termination after app exit/same snapshot/delta reconnect, Editor stop/start/new-domain loaded code. Reboot unexecuted |
| NI-06 Result/storage protection | Passed: automatic/fixture | Independent results/unpreserved-change rejection/quota disk-full/snapshot-loss protection. Actual dirty development integration blocked with GitDirtyWorktree; HEAD/index/diff/result refs preserved |
| NI-07 Reclaim | Passed: fixture | App/children/provider/owned memory/handles 0; mount/path/Git registration/delta/owned VSS absent. Independent results/original/existing restore points preserved |
| NI-08 Final candidate/docs | Passed: this internal alpha scope | Locked restore/Release build/97 passed/0 failed/1 skip, actual run/additional integration blocking/reclaim with same source hashes, current documents/UTF-8 synchronized |

`pwsh ./scripts/build.ps1` completed with 0 warnings/errors. `evidence/final-build.trx`: **98 total, passed 97, failed 0, not executed 1**, tests ~63s. Excluded case was historical test for now-retired backend. Fake/ordinary Git-directory tests not counted as actual VSS/WinFsp/Unity evidence. Actual NTFS ADS/hardlink/mount-root/internal-junction rejection tests included automatically.

<a id="원본과-두-unity-작업의-독립성"></a>
## Independence of original and two Unity tasks

A/B capture VSS at different times from same clean Git checkpoint. Initial journals each 868 bytes (dedicated Git-pointer payload 98/metadata 770); full-payload copy/read cache 0. Both initial fingerprints `cbce5d1bad6c2a16c62072d4f4786e644f3b436de07a62053769c6c660957b8c`.

Changed original probe +10→+30, observed 33 through original MCP. Inserted deliberate C# syntax error in A; `a-error-status.json` returned CS1525/CS1519/`validation-failed`. Meanwhile separate B Editor showed +200 code value 203/successful validation. No original write lock/Editor shutdown required.

Changing B other file `SplinterAlphaB.cs.meta` changed full source fingerprint to `source-changed`/`validation=null` (`b-meta-invalidates-success.json`). Restored it and edited B +201, returning 204; corrected A +100 returned 103, then +101 returned 104 in actually loaded assembly. Used product fingerprint including `.meta` plus additional runtime probe.

| Final execution | Source fingerprint | New-code evidence |
| --- | --- | --- |
| A 104 | `99569eb500ce60827eb04b4377a5ea10ef891fe8fa15cfe159f447cba779aaae` | PID/domain/assembly MVID in `a-104-status.json`/`a-104-probe.json` |
| B 204 | `8ad3278cd0b3e5e441bd5b5f250be2d64b4034d9e35d4e32f7c8999792866157` | PID/domain/assembly MVID in `b-204-status.json`/`b-204-probe.json` |

Original test changes restored from saved original bytes. Immediately before close original MCP probe 13/compile/import/Play/scene dirty false; original Git HEAD/clean/worktree registration retained. Original Editor/collaboration package preserved; Editor access/task lease ended.

<a id="저장량과-비용"></a>
## Storage and costs

`evidence/range-measurement.json` measured ordinary product workspace. 3 original random files have native `FILE_STANDARD_INFO.AllocationSize` equal to logical lengths, no Sparse/Compressed flags. Initial delta journal 922 bytes/identity 674 bytes allocated 8,192 bytes file-data total. After two whole reads/SHA-256 match per file, allocation still **8,192 bytes**, read cache **0**.

| Original file | Write range | Journal increase | Actual delta file-data allocation increase | Write time |
| --- | --- | --- | --- | --- |
| 16MiB | 4,096 bytes at offset 2,101,247 | 4,521 bytes | 4,096 bytes | 25ms |
| 256MiB | Same offset/length | 4,525 bytes | 8,192 bytes | 17ms |
| 1GiB | Same offset/length | 4,529 bytes | 0 bytes | 17ms |

Each payload increase exactly 4,096 bytes. Allocation grows in NTFS units; sum across 3 writes 12,288 bytes. 1GiB write allocation growth 0 uses already allocated space, not journal growth 0. MFT/VSS/Git costs excluded.

Initial aggregation script ordered-dictionary sum error produced null aggregate. Individual native values preserved; only total corrected. Invalid aggregate `range-measurement-aggregate-invalid.json`, corrected `range-measurement.json`. Not presented as new measurement. SO `measurement-v1-invalid` still unused.

Ordinary Git/fingerprint verification reads data. During initial preparation provider read counter 4,077,651,938 bytes; after explicit repeated reads 6,812,993,506 bytes. First full reads 16MiB 355ms/256MiB 5,500ms/1GiB 21,982ms; second 459/5,987/22,913ms. Read hydration 0 not read I/O 0/fast startup SLA. Fixture-preparation `.git/objects` logical 1,359,368,072 bytes is input-preparation cost, separate from initial workspace copy.

| Task | VSS capture call time | Last seal journal | Observed maximum app working set | Observed maximum app private bytes |
| --- | --- | --- | --- | --- |
| A | 2,551ms | 263,116,253 bytes | 6,497,574,912 | 7,490,023,424 |
| B | 2,402ms | 188,916,161 bytes | 3,181,006,848 | 3,692,883,968 |
| Binary | 2,965ms | 1,045,399 bytes | No Editor | No Editor |

Memory maxima from status-query samples, not continuously measured peak. Unity Library/metadata rewriting included in actual delta. Capture time excludes UAC-response wait/full Git verification/Unity launch. First A Editor preparation exceeded 3 minutes. Per-stage CLI times `evidence/*-timing.json`; execution/preparation state in command responses/`home/records/<id>`.

Immediately after A/B/binary captures C: VSS UsedSpace 9,214,996,480 / 9,428,889,600 / 9,755,701,248 bytes, AllocatedSpace 11,163,103,232 / 11,376,291,840 / 11,702,333,440 bytes; MaxSpace 39,985,348,608 bytes. **Whole-volume values** include other restore points/writes outside project, not per-task increases. Immediately after final release UsedSpace 10,049,417,216 bytes/AllocatedSpace 11,702,333,440 bytes/C: free 305,596,284,928 bytes. Existing snapshots/other same-volume tasks not separated, so difference not test-resource savings. Settings/limits unchanged.

`evidence/result-storage.json` measures result Git objects absent from basis commit. A logical 2,861/Git stored 1,769 bytes; B 2,847/1,763 bytes. Binary preserves restored basis commit, no new result objects. Git stored size not NTFS allocation/whole repository size. Result refs/commits/small recovery records/synthetic input repository preserved.

<a id="재연결실패-보호"></a>
## Reconnect and failure protection

- **Unpreserved result:** A release before checkpoint safely stopped app, rejected `UncollectedChanges`, retained delta (`a-uncollected-release.json`).
- **Provider termination:** Verified app/children 0, terminated only owned provider PID `47192`. New CLI ensure reconnected same snapshot/delta with new provider/same source fingerprint. Evidence `a-before-provider-crash.json`, `a-provider-crash.json`, `a-provider-down.json`, `a-provider-reconnect.json`.
- **Editor restart:** A new PID `47256`/domain returned existing assembly 103. Subsequent 104 edit verified new MVID/domain/successful validation (`a-restarted-103-probe.json`, `a-104-probe.json`).
- **Quota disk-full:** At 1MiB journal limit, 225 acknowledged 4KiB writes/921,600 bytes, then Windows error 112/`DeltaFull`. Verified acknowledged data/remaining reads/snapshot/delta preserved. Original-equivalent bytes leave source clean. Not physical host-volume exhaustion (`quota-protection.json`).
- **Snapshot loss:** After B independent-result ref/tree/full journal seal/app/children 0 verified, removed only exact B VSS ID. Other 5 snapshots retained (`b-loss-result.json`). status/ensure `SnapshotMissing`, no replacement capture. Mount/Git/delta reclaim proceeds only with identical independent result/full seal. Automatic tests supplement missing-result/post-collection-late-write rejection.
- **Partial unmount:** A release with actual read handle open returned `OverlayStopPending`. Closed handle/resumed same release through provider exit/Git/delta reclaim (`a-held-handle-release.json`, `a-release-request.json`).
- **Original integration blocking:** In separately authorized actual NTFS task after validation/checkpoint, inserted uncommitted original README change/requested `workspace integrate --target development`. Rejected `GitDirtyWorktree`; HEAD/index/staged/unstaged diffs/README bytes/worktree list/result ref unchanged. Additional record below. Existing `original-dirty-integration-precondition.json` retained only as initial-unexecuted read-only record.

<a id="최종-회수-상태"></a>
## Final reclaim state

`evidence/final-state.json`: A/B/binary all **`released / released`**. Each app/child process 0, working set/private bytes 0, provider exited/memory 0, open handles 0. Actual mount/workspace paths/Git registrations/deltas/owned VSS absent. Independent-result refs/trees exist; original HEAD/clean/worktree/running original Editor preserved. Cumulative journal counters are pre-reclaim history, not residual storage.

`evidence/final-snapshot-release.json` records exact owned A/binary VSS release after user UAC approval/completion receipt of already missing B. Ordinary CLI `a-final-release.json`, `b-final-release.json`, `binary-final-release.json` verify final postconditions. No global VSS deletion/storage-setting changes via same helper.

3 non-owned existing VSS `{E82CEB2E-AC2B-4FDF-ABE9-E6E4041FB724}`, `{8D150A60-DBB3-4A18-A509-DF3E7648C50C}`, `{1B6D14D7-E5FA-4951-B3B6-9E62F96F194D}` exist before/after release. `release-owned-snapshots.ps1` local test wrapper checks CLI Release nextActions/helper hash/result/dependencies/path absence, not new ordinary product command. Closed resources not recreated for testing.

After reclaim ran two same-A CLI requests with actual overlapping times; both returned same ID/`released`, identical snapshot receipt hash, no mount recreation. Changing only journal limit under same key returned `RequestKeyConflict` (`evidence/released-request-check.json`, `released-duplicate-1.json`, `released-duplicate-2.json`, `released-input-conflict.json`).

<a id="추가-원본-통합-차단-시험"></a>
## Additional original integration-blocking test

After user authorized actual original-integration protection, executed separate `artifacts/ntfs-workspace/ni-guard-20260921/`. Section evidence in its `evidence/`; durable state product `.local/ni-guard/records/1594868f46bf6d41c1d79cd8`. Used new key `ni-20260921-integration-guard`, without reviving reclaimed tasks.

- Captured VSS `{62E72EC3-4450-400C-B905-A690FE4CDE5F}` from same original SHA/prepared via ordinary CLI. Original MCP confirms PID 25756/probe 13/compile/import/Play/scene dirty false (`original-before-mcp.json`).
- Task Editor PID 62264 probe edited +301. `validated-status.json` records fingerprint `be16c585e557e3a758e49599df4c0561bcc793c9bd77245865c459daca8c5e21`/successful validation; `probe-304.json` actual 304/assembly MVID `f9b6f13a-4bad-4b17-9cfc-27e165dd695b`.
- Checkpoint verifies app/children exit/preserves independent commit `8f12880e5c96cc8f615a6c09e622838323555fd5`/`refs/splinter/snapshot-results/1594868f46bf6d41c1d79cd8`. No merge into original.
- Added marker only to original README/requested actual CLI integrate. `integration-rejected.json` is original-path `GitDirtyWorktree`. `dirty-original-before.json`/`dirty-original-after.json` identical HEAD/branch/status/index/staged/unstaged diffs/README hash/probe hash/worktree list; no MERGE_HEAD. `integration-guard-result.json` confirms original/result-ref preservation.
- Verified marker exactly own change/restored only saved original bytes. `original-restored.json` clean original SHA; `original-after-mcp.json` original probe 13/idle. Editor access/task lease ended.
- `vss-Release.json`, `final-release.json`, `final-state.json`: task `released`, app/children/provider/owned memory/handles 0, mount/Git registration/delta/owned VSS absent. 3 existing VSS/independent result preserved. Pre-reclaim journal 181,925,663 bytes, not residual storage.

Additional test without product-code changes. `candidate-inputs.json` links same 117 source hashes/existing final TRX. Reuses same-candidate 97 passed/0 failed/1 skip, not recorded as rebuilt. No clean-original merge success/conflicting merge of two NTFS results tested here. Actual dirty-integration blocking distinguished from existing automatic Git/contracts.

Short-home package DLL path 244 characters; verified file exists/initial logs lack `DirectoryNotFoundException`/`PathTooLongException` (`short-home-observation.json`). Fixture observation, not full long-path/general-package support.

<a id="검증-한계"></a>
## Verification limits

Deep run home makes `Library/PackageCache/com.unity.visualscripting@4edfcc09512f/Editor/VisualScripting.Core/Dependencies/YamlDotNet/Unity.VisualScripting.YamlDotNet.dll` 270 characters; initial Unity Mono assembly validation reports `DirectoryNotFoundException`. .NET verified file existence; both Editor preparation/C# compilation/loaded-code validation above completed. Not evidence all packages loaded correctly. Subsequent real-environment tests use short `--home`, separately verify long-path/general-package compatibility.

Reboot/power loss/actual-volume exhaustion/all I/O-boundary forced termination/general dirty/LFS/special files/assets/packages/Play/Unity Test Framework/large-project performance/all 48 public cases unverified. Purchased assets unnecessary for NI scope. Remote publication/public-release verification excluded.
