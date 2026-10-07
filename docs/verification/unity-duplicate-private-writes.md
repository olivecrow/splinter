<a id="작은-unity-반복-작업의-동일-쓰기-공간-비교"></a>
# Duplicate-write storage comparison for small repeated Unity work

[한국어](../ko/verification/unity-duplicate-private-writes.md)

Execution date: 2026-10-02. One pair of editing, same-task resumption, normal Editor restart, independent result/release runs used an existing warm fixture. **Journal-allocation difference immediately before shutdown for this input is small: 256KiB, about 0.16%.** The [synthetic repeated-input 96.1%](duplicate-private-writes.md) is not inherited as actual Library savings. This unit is scoped-complete-with-limitations; overall SF-02 Partial and M0's bounded-completion boundary remain.

<a id="고정-입력과-실행-경계"></a>
## Fixed inputs and execution boundaries

- Original is registered `T:/ua-source`, clean `codex/library-20260929 @ f71e4e244ba9b085aa0fc439336e4c60ee3d5d5d`. Existing Library file allocation is 1,871,929,368B, identical at start/end. Home is existing `.local/wl29`; Unity `6000.3.22f1`.
- Runtime from `93080be` and `05f946a` were fixed. Only `Splinter.Runtime.dll` differs among CLI-bundle executables. Actual provider bundles were also compared: `7AAC910D9542CF632B1397C6A4E8FF6F83D7A5B1790044E6D13E9D6CD9F7CD78` / `58FF65F386965EC329101C2081AB3334D4515E389B50762CEF12A7FC8446B1BC`.
- At each returned task path, `SplinterSnapshotProbe.Calculate` offset 10→110→111 was applied. Current-source verification, successful validation, actual loaded-code values 13→113→114, project/PID/MVID checked. Same ensure retained ID/digest/snapshot/provider/running Editor. Normal stop/start changed Editor PID while retaining provider.
- Two Runtimes ran **sequentially**; each result was preserved then released. No new service/installation/grant/volume configuration, original integration, forced termination, VIVOBOOK, large/cold inputs, or new failure matrix. Product code/contracts were unchanged in this comparison.

Raw evidence: [plan](../../artifacts/storage-foundation/unity-space-20261002-004005-467/plan.json), [fixed file hashes](../../artifacts/storage-foundation/unity-space-20261002-004005-467/binaries.json), CLI JSON, monitors, loaded code, per-stage accounting, and [final assessment](../../artifacts/storage-foundation/unity-space-20261002-004005-467/result.json). Execution/measurement adapters in this directory are test material for this input, not new product commands/general harnesses.

Native fixed DLLs are Release builds before their changes were committed, informational versions `+b526646` / `+93080be`. This task's required rebuild is `+05f946a`, Runtime hash `7107163364D74A17C87F192900C4D24E7B69B2AB3F4AA16571354F596A08F42D`. Duplicate-write execution source hash `C63AD2F4B8BCA736D1872F11C54C55CD5B668D1725D576929831997B0DC04A90` is identical, but binaries differ; [build identity](../../artifacts/storage-foundation/unity-space-20261002-004005-467/runtime-build-identity.json) and [qualification manifest](../../artifacts/storage-foundation/unity-space-20261002-004005-467/qualification.json) distinguish native qualification from rebuilt automated regression. The rebuild does not automatically inherit native passes.

<a id="실제-파일-할당과-library-분류"></a>
## Actual file allocation and Library classification

Owned journal files were observed using `FILE_STANDARD_INFO.AllocationSize`. Existing Library is not summed as an additional physical copy; private Library writes are already in the journal.

| Observation B | Existing Runtime | Duplicate-write reuse Runtime |
| --- | ---: | ---: |
| First-ready journal-file allocation | 129,892,352 | 128,909,312 |
| Journal-file allocation after first edit | 139,984,896 | 143,392,768 |
| Journal-file allocation after restart | 149,291,008 | 149,225,472 |
| Logical journal length immediately before shutdown | 161,642,716 | 161,414,505 |
| Journal-file allocation immediately before shutdown | 161,742,848 | 161,480,704 |
| Recorded Library payload, logical | 132,522,134 | 132,308,082 |
| Live Library private ranges, logical | 96,298,264 | 96,189,510 |
| Total obsolete payload, logical | 37,623,220 | 37,508,986 |

[Existing journal classification](../../artifacts/storage-foundation/unity-space-20261002-004005-467/before-journal-accounting.json) and [current classification](../../artifacts/storage-foundation/unity-space-20261002-004005-467/after-journal-accounting.json) compare frame checksum/sequence, before/after size/mtime, and **exact stable-journal-v1 seal preserved by the product**. A read-only adapter permits the owned provider's write handle and rejects differing seal/hash. No snapshot-payload copying/read hydration occurred. Both ensures' initial payload copying/read cache are 0.

File allocation immediately before shutdown decreased by 262,144B, but current-run allocation was larger after first editing. This is not a consistent decrease across stages. Even with identical code inputs, snapshot, task path, time, Editor/cache state, generated files, and actual callback streams differ. Live private Library size also differs slightly; **0.16% is an observed difference in this single pair**, not exact causal savings from one optimization or a general-project number. New management-frame timestamps/sequences and individual-flush contracts follow existing automated/small WinFsp verification.

Of current live private payload 104,799,904B, Library is 96,189,510B (about 91.8%). Large live paths include `Library/Bee/1900b0aE.dag`, `.dag.json`, `.dag.payloads`, `-inputdata.json`, and post-processed DLLs. Duplicate-private-write reuse alone did not greatly reduce Library burden for this input. Next storage improvements should target remaining new/changed Library records; duplicate-write proportion/partial-change savings potential remain unmeasured. Logical obsolete payload is not actual compaction savings.

<a id="보존회수와-실패-비용"></a>
## Preservation, reclamation, and failure costs

Existing task `80b9144554356e112e354869` result is `cb3a2c0cabf468dedbff5e26575bc33c41ec7db7`; current task `7c3c8e2114a7639208f3de8d` result is `4e512796b57d3e0750844427e691e3b1a2f3f603`. After reclamation, both result script blobs are `42fdea2ed8529f4352d918c9ddc0824206ab0147`, retaining offset 111. Each result branch/ref remains. Actual mount/Git registration/delta absent; exact snapshots missing/released; owned process/working set/private bytes/handles 0. Original 94 tracked files/HEAD/clean state, existing workspace records/refs, and original Editor identity were preserved.

Existing Runtime's initial start and both Runtime restarts returned `SourceChanged`. Already-published launch receipts/owned Editors were checked and observed/retried under the same IDs, with no new-task/forced-termination bypass. Additional current-source checking after the existing initial start also preserved an access-denied `InternalError` in the restricted tool-execution environment, then rechecked in ordinary Windows-user execution. Source fingerprints matched, but in-progress journal changes do not establish a broader OS cause. Initial start/restart is not qualified as always succeeding on one attempt.

First-edit owned-file measurement had one path-absence error. Editing/validation was not repeated; failed evidence and a new measurement remain. Current restart's entire control accounting also recorded allocatedBytes=unknown/observed subtotal because `state.json`/temporary paths were absent. Owned journal sample/final accounting are complete; unknown was not read as 0. All 67 CLI monitors were non-elevated; 50ms consent-process observation found 0.

After reclamation, additional allocation across product roots versus baseline: records 335,008B, reusable runner bundles for two Runtimes 2,024,112B, Git common results 9,904B. Existing validation roots/original Library match. Test evidence has separate costs. Shared VSS in [final accounting](../../artifacts/storage-foundation/unity-space-20261002-004005-467/storage-ended.json) is unknown; no claims for MFT/directories, OS cache, instantaneous whole-system peak, or total physical-space savings. Stage times/failures/retries remain in monitors and are not preparation SLAs or game/system-memory savings.

Required `pwsh ./scripts/build.ps1` completed locked restore/Release with 0 warnings/errors and **764 passed, 0 failed, 1 test excluded at that time skipped out of 765**. [Build log](../../artifacts/storage-foundation/unity-space-20261002-004005-467/build.log) and [TRX](../../artifacts/storage-foundation/unity-space-20261002-004005-467/automated.trx) remain. This comparison is closed; further repetition/support expansion does not start automatically.
