<a id="선택적-외부-도구-경계-검증"></a>
# Optional external-tool boundary verification

[한국어](../ko/verification/external-tool-boundaries.md)

The 2026-10-06 change is limited to the `codex/remove-external-tool-coupling` worktree started from `development @ d97bd3cf9c7aaf7d5657d60271881538953ae8fd`. Current responsibilities follow the [external-tool guidance](../unity-mcp-response-recovery.md) and [Unity lifecycle](../alpha-workspaces.md). MCP/Unity CLI installation, names, versions, internal APIs, and connection success are not product prerequisites or gates.

| ID | Changed boundary | Verification and limits |
| --- | --- | --- |
| ET-01 | Owned Editor initialization and normal shutdown | Runs two synthetic implementations in the existing Unity API stub harness linking the actual `WorkspaceBridge.cs`: no external package, and a shutdown class present without the old private function. Verifies 0 external assembly resolver calls, ready/compiling/ready/exiting heartbeats, project/PID/start time/token, and preservation of other quitting callbacks. Does not qualify actual Unity/external-package execution |
| ET-02 | Normal shutdown protection and launch preparation | Verifies wrong-token rejection, blocked shutdown during compile/import, and request-identity-bearing refusal for Play/unsaved scenes. Actual temporary-file tests remove only Unity compiler/Pipeline addresses and preserve MCP/other-tool run-state, Library artifacts, and original files. Existing normal-shutdown wait/timeout/cancellation tests and owned-resource/lock guards retained |
| ET-03 | Removal of external patch production/operation | Removes prepare/verify/server, authentication correction, C# job, and dedicated test sources under `integrations/unity-mcp/`. Searches product/development CLI, Unity bridge, and scripts for absence of Coplay/MCPForUnity/PID patterns, tokens, and patch execution references. Updates README, requirements, AGENTS, skill, start/foundation/usage plans, and owning documents to optional-tool boundaries |
| ET-04 | Existing preservation/reclaim contracts | Required locked restore, Release build, and full automatic regression verify seal, independent result, original/peer, dirty/target, and owned-resource guard regression. Input/public response schemas, fake common contracts, workspace v2, and journal formats unchanged. Removed heartbeat field `coplayShutdownGuard` was an internal bridge observation field unused by public schemas or gates |

<a id="현재-자동-실행-근거"></a>
## Current automatic execution evidence

Required `pwsh ./scripts/build.ps1` passed locked restore and Release build with **0 warnings / 0 errors**, and **1,069 total: 1,060 Passed / 0 Failed / 9 conditional Skipped**. Tests took about 7 minutes 24 seconds. Skips require explicit execution conditions such as separate evidence input or child entrypoints; they are not counted as new native success. The two synthetic cases for the changed bridge and external run-state/original file preservation tests also Passed.

The final log is `.local/external-tool-build.log` in this worktree; raw results are [TRX](../../artifacts/test-results/Splinter.Tests_net10.0_x64.trx). The first restricted restore ended with exit 1 without detailed diagnostics; the same required command completed in full in the same worktree's ordinary-user execution environment. No installation, administrator Editor, or service change was used as a workaround. Dependencies/lockfiles are unchanged. UTF-8, 552 local document links, and `git diff --check` also passed.

<a id="남은-경계와-보존한-근거"></a>
## Remaining boundaries and preserved evidence

An external package terminating a shared server from its own quitting callback is the responsibility of that tool and agent procedure. Splinter does not remove the callback or block normal shutdown on external connection state. The procedure to verify the target/connection and decide recovery before the next external request is separate from Splinter result/reclaim judgment. Other original/peer processes, services, and snapshots are not terminated/deleted as a workaround.

Actual Unity A/B, MCP, Unity CLI, service installation/Update, and native matrices are **NotRun**. Already running Editors/servers, installed packages, and global Codex settings were unchanged. Automatic regression of the new source is not taken as application/compatibility success in those environments. Storage isolation is not an OS sandbox for arbitrary external code and does not guarantee every failure case during external-tool initialization/shutdown.

[UH](unity-cli-handoff.md), [UP](unity-parallel-editor-validation.md), [VR/MR](vss-and-mcp-remediation.md), [SB-02](security-boundaries.md), and [NP external shutdown observations](plain-project-basis.md#external-mcp-shutdown-defect-and-recovery) are preserved as facts for their historical versions/candidates/inputs. Successful historical callback protection/fixed patches are not inherited as current product requirements or success. Generated external modifications, raw evidence, independent results, original/sibling fixtures, and other tasks' WIP were unchanged. No original-project apply, push, PR, or deployment was performed for verification. Integration of product source into `development` is a separate boundary.
