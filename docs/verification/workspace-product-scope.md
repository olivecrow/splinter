<a id="제품-범위와-명령-발견의-검증"></a>
# Product-scope and command-discovery verification

[한국어](../ko/verification/workspace-product-scope.md)

The first version's bounded assessment follows the [M0 record](m0-delivery.md); follow-up ownership follows the [implementation plan](../plans/implementation-plan.md); current commands follow the [foundation](../foundation.md) and [development CLI](../development-cli.md). This document owns current discovery/rejection/result-preservation contracts limited to workspace management and the limits of automated regression.

<a id="현재-계약"></a>
## Current contract

- Default `scripts/splinter.ps1` provides NTFS workspace creation/resumption, observation, range-journal compaction, checkpoint, integrate, release/discard, owned Editor start/stop, and read-only service status. Diagnostic checkout, view, and spike/probe are excluded from default discovery/creation.
- `scripts/splinter-dev.ps1` is a separate entry point for optional storage routes and development tests. `view --input` manages immutable source generations, a single Library cache, and explicit preservation/reclamation dependencies. It provides no automatic mode, queue, or Unity task judgment.
- Project refresh, compilation, Play/Test/build, and game acceptance judgments belong to AI/external tools. CLI accepts only supported commands/actions/current schemas and rejects invalid requests before state/provider side effects.
- Checkpoint preserves stable current source as an independent result regardless of Editor presence or compilation results. NTFS requires normal stop, 0 owned resources, bridge removal, and the same journal seal before/after and after unmount. Integrate checks current revision/fingerprint, the exact independent result, and original target/dirty guards. It claims no compilation/test success.
- Tasks/candidates/cache/receipts follow current storage-lifetime contracts. Unknown workspace-state fields and unsupported view-ledger request/response/generation fields are preserved and rejected. Current view contracts/regression evidence follow [storage-view contract verification](storage-view-contract.md).
- DAG, AI Runner, daemon/DB, general Unity authoring/Play/Test Runner, product installer, and automatic conversation binding are excluded from implementation. There is no `plan validate` or plan-request schema. Discard is implemented; `unavailable` lists only installation/binding.

<a id="자동-회귀와-한도"></a>
## Automated regression and limits

`ContractAndCliTests` checks default/development command discovery, NTFS-only ensure schema, rejection of unsupported commands before side effects, and current response structure. `ViewLifecycleStateTests`/`ViewWorkflowTests` confirm 0 durable-request/operation/provider effects for unsupported actions.

`WorkspaceCoordinatorTests`, `GitWorkspaceProviderTests`, and `GitSnapshotWorkspaceTests` collect/integrate current source without internal compilation evidence and check original/dirty guards, metadata-inclusive fingerprints, and independent results. `SnapshotWorkspaceCoordinatorTests`/`WorkspaceDiscardTests` continue blocking source/delta changes after results, missing independent results, and owned-resource/cache/snapshot dependencies. The Unity bridge's Coplay shutdown-hook protection qualification at that time is limited to the historical candidate. [Separate verification](external-tool-boundaries.md) checks the current harness's initialization, heartbeat, normal shutdown, and preservation of other quitting callbacks independent of external-package absence/internal API changes.

Required execution is locked restore, Release build, and full automated regression through `pwsh ./scripts/build.ps1`. Current candidate results/hashes are recorded below. This automated regression is not a pass for actual VSS/WinFsp/Unity A/B, external MCP/Unity CLI connection, ARM, reboot/power/physical-space failure. Existing M0/WF/LB records do not qualify this reduced candidate natively. Installation/grant, original fixture, and remote environments are outside this change.

<a id="현재-자동-실행-근거"></a>
## Current automated execution evidence

The current storage-view contract candidate on 2026-10-06 completed locked restore/Release build with **0 warnings, 0 errors** and full automated regression with **1,064 Passed, 0 Failed, 9 Skipped**. Nine conditional external/child-run tests, native NotRun boundaries, and exact source/result evidence follow [storage-view contract verification](storage-view-contract.md).
