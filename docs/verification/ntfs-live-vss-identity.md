<a id="live-vss-식별재연결-검증"></a>
# Live VSS identity and reconnect verification

[한국어](../ko/verification/ntfs-live-vss-identity.md)

As of 2026-09-22. Execution record for the [LV plan](../ntfs-workspaces.md). Does not mean full NR, actual reboot/power-off/storage exhaustion/ARM A/B passed.

<a id="후보와-자동-검증"></a>
## Candidate and automatic verification

Applied working-tree changes to product `development` `591d53fe5dc66e632eb92cdb8e108df7497b0fb6`. Aggregate for 124 files in the [source manifest](../../artifacts/ntfs-recovery/lv-20260922/source-manifest.json) is `73f590dfc7d9676fc897daeefe00d1dd81da52f08d5c3fe5873905e54774b11a`. Execution evidence is linked to pre-commit working-tree candidate file hashes; product push/deployment excluded. Fixture independent checkpoint commit included in results below.

`pwsh ./scripts/build.ps1` locked restore/Release build had 0 warnings/errors; [final TRX](../../artifacts/ntfs-recovery/lv-20260922/final-build.trx) is **181 total: 180 passed/0 failed/1 test excluded at the time skipped**. No new dependency/lock changes. Initial existing-expectation/capabilities-schema omission/open-journal read-sharing test issues were corrected, then full run repeated. Prior failures preserved as separate artifacts.

| ID | Verified scope | Status |
| --- | --- | --- |
| LV-01 | Capture/live identity, boot/owner/request-generation comparison and access DTO/schema; actual helper values | Passed |
| LV-02 | Rejects other ID/owner/provider/volume/time/flags/device and relative-path escape | Automatic Passed |
| LV-03 | Resumes actual JournalDeltaStore using verified new read root; preserves partial-write hash/sequence/identity/journal bytes | Fake/actual journal Passed |
| LV-04 | Distinguishes prior boot/stale request/failed or incomplete query/inaccessible path/exact ID absence/corrupt record | Automatic Passed |
| LV-05 | Preserves unprovable state; required version 2 information; same-key concurrent ensure; `.new`/request `.tmp` preservation | Automatic Passed |
| LV-06 | Actual product workspace range write/flush/provider termination/administrator Observe/non-administrator same-key reconnect | Passed |
| LV-07 | Binary independent-result preservation/product release/final owned VSS absence; Unity checkpoint/reclaim | Passed |
| LV-08 | Same-candidate Windows x64/Unity 6000.3.22f1 single workspace, 3 validations/reconnect/result/reclaim | Passed |

LV-03 does not test Windows actually reassigning device numbers or OS reboot. LV-02/04/05 failures are deterministic-input/file-I/O tests, not actual power/volume failures. New-candidate ARM Windows execution is NotRun.

<a id="실제-gitvss-재연결"></a>
## Actual Git/VSS reconnect

[Execution evidence](../../artifacts/ntfs-recovery/lv-20260922/evidence/) and reproduction scripts are at `artifacts/ntfs-recovery/lv-20260922/`. Short home is `C:\slv260922`. Uses Windows x64/existing NTFS/WinFsp; no volume/VSS setting or installation change.

Small independent Git fixture workspace is `934420476b7f584ac4266967`; snapshot `{FB351A2F-B273-45FC-9DAD-BF7E4AA7C770}`. Sequence: non-administrator ensure → exact Capture helper → same ensure → repeated 8MiB reads → original `source.txt` edit → 4KiB write/flush → owned provider termination → same ensure Observe request → exact helper → same ensure. Provider termination is a test boundary delegated to the specified adapter by separate development `snapshot-spike unmount`, not newly counted as a workspace product feature.

Measurements: initial payload copy 0, read cache 0, journal/NTFS file-data allocation growth for repeated reads 0, 4KiB write payload growth 4096 bytes/journal growth 4508 bytes. Initial Git pointer journal payload 143 bytes is not original duplication. After reconnect verified same snapshot/delta identity/full-file SHA-256/durable sequence 3. Original retains new contents; workspace reads capture-time `captured`. VSS ID list unchanged across Observe.

Acknowledged write result preserved externally as `binary-acknowledged-output.bin`/hash. Restored only owned test writes, preserved independent Git result of unchanged basis, and ran product release. Cleanup was not assigned Unity validation. Final response: released, owned processes/handles/memory 0, mount/Git registration/delta/exact owned snapshot absent. Separate original changes preserved.

<a id="단일-unity-회귀"></a>
## Single Unity regression

Fixture originally clean `development`, revision `46e7f4dbdb219e4afa602f3598a3de9ff4070fae`, 77 tracked files. Used workspace `bc49f980a40cc853e5ad6638`/snapshot `{7DD62742-5422-477B-9745-15574A74AF72}`. Captured after checking original saved/idle state and coordination.

During preparation the user closed/reopened the original Editor. Further Unity interaction paused during the user's request; after confirming new MCP session/canonical PID 67176, continued the same capture/task. Not counted as OS reboot or product canonical Editor restart test. Prior coordination session was confirmed inactive in the new Editor; new task session registered.

For baseline, added only a comment to trigger actual compilation, then verified value 13/current-fingerprint validation. Changed `+10` to `+17` in the same file, then verified value 20/second validation. Existing product WorkspaceBridge also reports actually loaded assemblyMvid. Distinguished from the fixture's separate signal; instrumentation unchanged.

First `unity stop` returned `EditorStopTimeout`; subsequent status confirmed normal exit/owned processes/memory 0. No forced termination; after provider termination through specified adapter, preserved journal identity/full hash/sequence. Provider recorded 18 existing Unity rename attempts as `Rename destination exists`; compile validation errors 0. Not expanded to all general Unity file APIs. Checkpoint application shutdown also returned the same timeout once; after observing owned resources 0, same checkpoint retry completed. Both timeouts and subsequent exit/resumption evidence preserved; no general shutdown-latency SLA pass claimed.


Before/after reconnect verified identical snapshot/delta identity/full-journal SHA-256/source fingerprint. New Observe updates request generation only; VSS ID list retained. New task Editor PID 62216 had same value 20/assemblyMvid as prior PID 62456, different domain generation. Added reconnect-verification comment; third actual compilation/current-fingerprint validation also passed.

| Validation | Value | PID | First 12 fingerprint digits |
| --- | --- | --- | --- |
| Baseline compilation | 13 | 62456 | `1220d654cd77` |
| Modified compilation | 20 | 62456 | `25f145d77937` |
| Post-reconnect compilation | 20 | 62216 | `bcff3a1f5a5d` |

Final independent result revision `1d7e848fc986aaeb964f97da14b64617fc4b3ca1`; ref `refs/splinter/snapshot-results/bc49f980a40cc853e5ad6638`. Only workspace `Assets/SplinterSnapshotProbe.cs` changed; not integrated into original branch. Product release verified final seal after bridge removal: sequence 59211, 213,808,189 bytes, SHA-256 `257a4355561878910890baca58028bdc745f733d08aa5e3a73b12985bb1695e6`, then reclaimed. This cumulative journal figure is not post-reclaim residue or initial duplication.

[Final result.json](../../artifacts/ntfs-recovery/lv-20260922/result.json) verifies result ref/revision/tree/manifest/seal, both workspaces released, owned app/provider/handles/memory 0, mount/Git registration/delta/owned VSS absent. Original HEAD/clean state/77 tracked-file hashes and 3 existing VSS snapshots retained. After user restart, canonical PID 67176/MCP confirmed final Ready/idle, Console 0 errors/warnings; coordination task session ended. Control receipts, independent results, raw evidence, and short-home reproduction records preserved.

**LV is complete at its defined test level.** Excludes full NR, actual OS reboot/device-path reassignment, power/physical storage failures, ARM A/B, general assets/Play/Test Framework qualification. Next work proceeds from remaining interruption/file boundaries in the parent NR plan.
