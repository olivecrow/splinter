<a id="부분-private-쓰기작은-버퍼editor-시작"></a>
# Partial private writes, small buffers, and Editor startup

[한국어](../ko/verification/resource-efficiency-followup.md)

Execution date: 2026-10-02. Scope is **reuse of matching prefixes/suffixes in private overwrites, request-sized buffers, and separation of startup from current-source verification**. Current journal format, schema, common ports, versions, and installed service remain. This does not expand to full Library/Editor RAM, large, cold, remote, or OS-failure support. Owning contracts are [NTFS](../ntfs-workspaces.md#repeated-writes-to-the-same-private-range) and [source verification](../source-verification.md).

Raw evidence is at `artifacts/storage-foundation/resource-followup-20261002-020617-014/`. Preserves `plan.json`, fixed before/after CLI/probe bundles/source, `before-fixed/result.json`, `after-fixed/result.json`, `comparison.json`, build logs/TRX, and separate `unity-plan.json`/CLI responses/monitors. The probe and execution adapter are fixed development inputs, not new product commands/general harnesses.

<a id="고정-입력의-공간과-대여-버퍼"></a>
## Fixed-input storage and rented buffers

Initially wrote 64KiB to `Library/Bee/cache` on the same immutable fake basis, then issued 16 full requests changing 8 middle bytes. Existing/current Runtime ran in separate processes/new owned directories. The initial new write stores full payload; later writes reuse only matching prefixes/suffixes of a wholly private range. One existing `write` frame, modification time, path sequence, and individual durable flush remain. The probe fails if it reads snapshot payload.

| Observation B | Before | After |
| --- | ---: | ---: |
| Recorded payload | 1,114,112 | 65,664 |
| Journal logical length | 1,122,136 | 73,752 |
| NTFS journal file allocation | 1,122,304 | 77,824 |
| Durable sequence / flush count | 20 / 20 | 20 / 20 |
| Actual rented buffer for a 4KiB Read callback | 1,048,576 | 4,096 |
| Cold-thread managed allocation for the same callback | 1,050,048 | 5,568 |

File allocation is `FILE_STANDARD_INFO.AllocationSize`. Reduction for this input is 1,044,480B, about 93.1%; both executions have identical final content hashes/replay results. Initial creation, length extension, changes to every byte, and base/hole are not optimized. Library matching-prefix/suffix proportions and overall savings were not measured. Total physical peak, MFT/directories, and VSS/Git/evidence costs are excluded from the table.

Memory samples call the same WinFsp Read callback with a 1MiB maximum transfer setting on a new managed thread. Actual ArrayPool rental events and cumulative managed allocation on the current thread were observed. This supports smaller rentals for small callback requests; it is not a pool-retention/provider-residency/Editor/whole-system RAM reduction rate. Chunk/acknowledgment boundaries for large requests and return after success/failure remain.

Both executions separately verified 64KiB file creation, full overwrite with a 1-byte change, flush/read, and replay after normal unmount on an actual local WinFsp mount. The immutable basis is fake; this does not replace VSS/Git/Unity/forced termination/power verification. Automatic regression covers private extents/64KiB boundaries, first/last changes, replay after interrupted frames/flush, 0/1/4KiB/16KiB/128KiB callbacks, and constrained EOF.

<a id="시작-응답과-현재-검증"></a>
## Startup responses and current verification

The post-start View for `unity start` and `ensure --editor` uses execution observation from ordinary status. Without a new pending validation response, source is not inspected and is `not-checked`. New validation-result reconciliation, ensure without an Editor, refresh, explicit current/audit checks, checkpoint/integrate, and seals before/after collection/after unmount remain. Source-check failures during import are not hidden as generic automatic retry or converted to validation success.

Common fake regression verified execution acceptance when a post-start source check would return `SourceChanged`, reuse of the same task/Editor by a new coordinator, existing refresh rejection, rejection of changed source by new pending validation, and checkpoint blocking. This does not establish the exact OS/file-change cause in the earlier [Unity comparison](unity-duplicate-private-writes.md).

Current Unity execution is one new owned task `3cb811fcc192df22638cabbb` on registered `T:/ua-source`, `.local/wl29`, `6000.3.22f1`. Initial start and restart after normal stop were `Accepted`/`not-checked` at about 2.05 seconds/2.88 seconds respectively; preparation was awaited separately. Actual loaded values 13→113→114, each Editor PID/project/MVID, current source verification, and compile success were checked. Same ensure retained ID/digest/snapshot/provider; normal restart changed only Editor PID.

First checkpoint and release succeeded. Result `0fad9415052bc4233af2a1abb3cfaf5c6bc535c6` and script blob `42fdea2ed8529f4352d918c9ddc0824206ab0147` remain independently after reclaim. Actual mount/Git registration/delta and exact snapshot are absent/reclaimed; owned processes/memory/handles are 0. Preserved original 94 tracked files, HEAD/clean state, 72 existing workspace records, existing refs, and 3 original Editor/process identities. 33 CLI runs had 0 failures/no elevation; additional consent samples were 0. Original integration, installation/grant/volume, remote execution, and new failure matrices were excluded.

Owned journal file allocation immediately before termination was 201,719,808B. A ledger comparing the product checkpoint's exact `stable-journal-v1` seal and checksum shows Library at 106,388,937B of 115,933,762B valid private data. This demonstrates why the synthetic input's 93.1% cannot be inherited as total actual Library savings. Task path, snapshot, generated contents/callback sequence differ from the earlier Unity comparison; this single execution is not a before/after comparison of total storage improvement. Shared VSS attribution, total physical peak, and Editor/system RAM savings are unknown/undetermined. No unnecessary verification failure during actual start/restart was observed here, but removal of all retries or establishment of the OS cause is not claimed.

<a id="자동-회귀와-적용-한계"></a>
## Automatic regression and application limits

Final required `pwsh ./scripts/build.ps1` locked restore/Release had 0 warnings/errors, **776 of 777 passed, 0 failed, 1 test excluded at the time skipped**. `build-final.log` and `automated.trx` are preserved. The initial full-run compaction-ratio failure also remains in `build.log`. That test intended full overwrites but changed only the first byte, so new reuse reduced pre-compaction cost. Input was corrected to change every byte; existing compaction ratios and content/reclaim judgments were not weakened.

Runtime/Windows hashes of the current fixed probe and actual product provider match the final required build. `source-identity.json` fixes hashes of 3 uncommitted implementation changes on `development @ e7b1c49`. No full native pass of a rebuild/new candidate is claimed; judgments remain separated by input, binary, and execution boundary. **These three improvement units are closed as scoped-complete-with-limitations.** Remaining new/fully changed Library burden, actual provider/Editor RAM, and other SourceChanged/late collection write boundaries require separate follow-up decisions.
