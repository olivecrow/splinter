<a id="제품-vsscli-쓰기-중단-개발-시험"></a>
# Development product VSS/CLI write-interruption tests

[한국어](ko/product-write-recovery.md)

`scripts/test-product-write-recovery.ps1` uses a registered clean source/home and the installed restricted-privilege service. It pauses an external client in the VSS/WinFsp/Git workspace of ordinary product `workspace ensure` and terminates only the exact owned provider. Ordinary providers are not connected to development observers. CLI, Git, and client run as a standard user; service installation/grants, volumes/budgets, Unity, and VIVOBOOK are not changed.

The default is a read-only plan and creates no source/home/evidence. `-Execute` permits only new evidence, preserves existing workspace/home records, and uses a new key for each case. Prepare Release binaries with `pwsh ./scripts/build.ps1`.

```powershell
pwsh ./scripts/test-product-write-recovery.ps1 -Project <registered-source> -HomePath <registered-home> -OutputPath artifacts/ntfs-recovery/<new-id> -BaseReference <recorded-base>
# After checking these inputs and execution conditions, add -Execute. Partial runs use -Cases '<operation>:<boundary>'.
```

Fixed fixture paths are `Assets/SplinterQualification/Target.fbx` and `Assets/TutorialInfo/Icons/URP.png`. Beta is preserved without text normalization through the repository's `*.png binary`. Only small byte oracles for the two tracked files are retained in evidence; this is not initial product copying or hydration. Check actual snapshot reads and matching original hashes, externally acknowledge the prefix after writing/flushing alpha's first 4KiB, then proceed to the boundaries below. Gamma is `Recovery-target.fbx` in the same Qualification folder; an existing file fails the oracle. Source files and `.meta` files are unchanged, and Unity import/asset support is not evaluated. Normalization under Git text/eol declarations is separate from raw file hashes; this test does not change that policy.

| Operation | Client boundary | Count |
| --- | --- | --- |
| write/rename/replace/truncate/explicit file flush | Before the operation, after completed oracle and external ACK | 2 each, 10 total. |
| write | After actual Write return, before explicit file Flush | 1. |
| mmap | Before view Flush, after view/file Flush completion and external ACK | 2. |

Write targets 4KiB at position 8192; truncate sets a 4KiB length; replace replaces alpha with beta. Publish mmap ACK only after both view and file Flush complete. An unacknowledged mmap page before explicit Flush may be absent or survive in full. Normal Write return follows the product's individual durable-journal contract, so new content must survive even before explicit file Flush. This is not an oracle for callback internals, OS flush internals, or power loss.

Request/signal/result use `product-write-recovery-probe-{request,signal,result}.schema.json`, fixing the exact workspace/snapshot/token/generation/PID/start time. Prefix/ACK/signal are published by flushing a new temporary file and moving without overwrite. Failure terminates only the client and preserves the product provider/workspace/snapshot and delta. Immediately after termination, only journal sharing errors 32/33 are re-observed for up to 5 seconds; corruption, access denial, and unknown are not concealed.

After exact provider termination and mount absence, the same ensure must continue the same snapshot/delta/Git registration. Check new current VSS read verification and a new provider generation; a second ensure must retain that same new provider and journal. After actual kernel reads and a new 4KiB write to beta, explicitly preserve an independent Git result through `workspace checkpoint`, without awarding compile success. Verify ref/blob bytes and ordinary release's source seal, mount/Git registration/delta/last-snapshot reclamation, and zero processes/handles/memory. `reclaim-pending` is not completion.

Compare original HEAD/index/tracked hashes, existing refs/worktrees, and service capture/release-isolation snapshot IDs. A standard user's failed global VSS query is unknown, not 0. Use only restricted-service guards and receipt evidence; do not directly delete unowned snapshots. [NR verification](verification/ntfs-recovery.md) owns execution, failures, current candidates, and support limitations. Do not inherit qualification from earlier fake/kernel candidates, Unity, ARM, or reboot/power/space failures. Notify the user before running VIVOBOOK tests.
