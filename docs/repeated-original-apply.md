# Repeated original application

[ST-65–68](verification/scalable-storage-throughput-followup.md) adds request-scoped native catalog/subtree proofs with fused byte totals, incremental retained-root updates, proven physical-history coverage for accounting, and independently revalidated composition/dependency proofs across publication. Formats, GC and ACKs remain; 26 focused cases do not qualify large or whole-lifecycle performance.

[한국어](ko/repeated-original-apply.md)

For a fresh `checkpoint --release` request, normal stop and infrastructure removal precede full pre/post seals around one capture. That same operation's sealed generation proceeds into release without a second capture; the post-unmount seal remains mandatory. An already fixed/published request retry cannot reuse a new invocation's seals for its older result. It returns the fixed requested result and separately collects current source for closure. Standalone release also collects current source. See [GCO scope](plans/git-collection-optimization.md).

This document owns continuing-task result generations and file publication to an original project. [Scope](plans/repeated-original-apply.md) and [RI-01–08 qualification](verification/repeated-original-apply.md) distinguish implementation, automated checks and actual Unity inputs. The fixed small Git, ordinary-folder and Plastic Unity original/A/B sequences and final full gate passed; RI-01–08 are scoped-complete with explicit input/tool/OS and cost limits.

File source manifests now use **v4**. Native None/Plastic reference captures keep the complete comparison catalog, but retain payloads only for changed before/after files, their range-basis dependencies and previously retained content. Unrelated unchanged basis files are not archived. These results support explicit application, reversal and restart; they are not standalone exports of every project file. The generation fingerprint binds the retention set and range-descriptor root. Git keeps its protected basis route. v3 manifests are preserved/refused and must be finished using the matching binary; task v4/basis v3/overlay v3 and public command fields are unchanged.

## Lifetimes and identities

Current [scalable-storage candidate](scalable-storage.md) records are **task v4, managed basis v3, product overlay v3**; SCAL qualification remains in progress. RI-01–08 below qualify their historical task-v3/basis-v2 candidate. Preserve/refuse older records and finish them with the matching binary; no migration, reclamation, new basis or task is implied by a CLI update. Use an explicitly selected new-format home for new tasks while preserving older homes; never move, rewrite or sweep old state automatically.

A task keeps its path, ID, request digest, immutable source basis, private journal and Library through repeated captures and publications. A result has its own immutable ID, sequence, request key, message, source sequence, capture digest, content identity and independent reference. Capturing a result does not stop its Editor or prohibit later edits/restarts.

```powershell
pwsh -File <product-root>/scripts/splinter.ps1 schema workspace.checkpoint
pwsh -File <product-root>/scripts/splinter.ps1 workspace checkpoint --id <task> --request-key <capture-key> --message <description> --home <home>
```

Use the returned `data.resultGeneration.id`. Reusing the same capture key/message returns its first result, including after later captures or task release. A different message for that key is refused. A pending capture fixes the key/message; finish it before choosing a new result, compacting or reclaiming.

The provider drains accepted writes, takes the callback/mutation boundary, flushes acknowledged data, and durably records namespace/metadata/range references and backing-file identities. Internal NTFS backing aliases protect those ranges. Payload reads, hashing and independent preservation happen after this boundary. Later writes belong to a later result. Bounded replacement and retained-capsule truncation respect aliases; only a private retained prefix may need a receipt-bound copy. Source project files are never copied up or hydrated into a clone. Recovery reads capture protection before writer replay/reclamation.

Current file/range results share immutable content within the home across sources and generations. Git retains a protected immutable basis plus file/range result manifests; explicit branch integration materializes commit/tree/ref results when needed. Original-file publication uses the common conditional publisher without changing HEAD/index/branch. Every completed generation and publication receipt survives task reclamation. Logical retained-payload accounting is separate from actual NTFS allocation and peak RAM.

## Publish a fixed result

```powershell
pwsh -File <product-root>/scripts/splinter.ps1 schema workspace.integrate
pwsh -File <product-root>/scripts/splinter.ps1 workspace integrate --id <task> --target original --result <result-id> --plan --home <home>
# Review conflicts and the exact full/selected write set, then:
pwsh -File <product-root>/scripts/splinter.ps1 workspace integrate --id <task> --target original --result <result-id> --intent-key <apply-key> --expected-plan-digest <digest> --home <home>
```

Repeat `--path <relative-path>` on both calls for a selection. A new publication explicitly selects the latest collected result; uncollected edits may continue in the worker. Comparison uses the last published state **per path**, initially the fixed basis. Only selected published paths advance that state. Unselected edits, deletions and returning an already published file to its initial content remain meaningful changes. Unity companion closure includes required `.meta` paths.

The plan binds the task/request/basis/result, previous publication digest, target root identity, source administration identity, selected paths, observed original contents/metadata/identities and any live-Editor cooperation identity. Conditional native publication retains existing whole-write-set checks and owned staging/recovery receipts. Divergence, unsupported metadata changes, replaced resources and uncertain ownership are preserved and refused. There is no conflict-overwrite option. An incomplete intent must be resumed with its result/key/digest/selection/cooperation identity; a new capture/publication/reclamation cannot bypass it.

A completed old request returns `data.originalApplyReceipt` and verifies that immutable generation. It performs no original publication or new Editor admission, even after release. It must not overwrite newer original edits. Status distinguishes task lifetime, `results`, `captureIntent`, `originalApplies`, effective `originalAppliedPaths` (including deletion markers), the active intent and its recovery action. A live original recovery also needs a freshly observed receipt for the **same** cooperation identity; a status suggestion alone does not reacquire external-tool counters.

Git `--target original` publishes working-tree files without changing HEAD, index or branch. Git administrative reads/leases and outstanding-lock checks remain. Existing explicit branch-target merge is a separate clean-target operation. Plastic retains fixed revisions, unregistered workers, administration/root/selector/lock protection and read-only server behavior. This feature does not import original changes into a task, commit, check in, switch a worker basis or resolve conflicts automatically.

## Live Unity original: agent-owned cooperation

Splinter does not call MCP/Unity CLI, install/manage a tool package, save assets, compile or judge game behavior. The agent uses existing tools and checks **every response's exact project/PID**, plus native PID/start time. Do not infer an absent Editor from a tool's incomplete instance list. Rediscover after reload; never retain a port as identity.

1. Check Play/busy state and all dirty scenes/assets before saving. Refuse an unnamed dirty scene, Play or failed/uncertain save while preserving edits. Save dirty named scenes and explicitly call `AssetDatabase.SaveAssets`; a tool's `save_all` may save scenes only. Verify dirty state cleared. Capture the task only after this save.
2. Save the exact original similarly. Acquire `AssetDatabase.DisallowAutoRefresh` and, where used, `EditorApplication.LockReloadAssemblies` through the external tool. Persist a request-keyed ownership state in that Editor before returning a cooperation receipt. Re-observe uncertain completion; do not increment counters or resave a partly published source blindly after timeout.
3. Write a receipt matching `live-original-editor-permit.schema.json`: version 1, exact project/PID/native start ticks, result ID, stable cooperation key, saved/observed/expiry timestamps and `importsDeferred: true`. Expiry is no later than five minutes after observation. Pass `--live-original <receipt.json>` on plan/apply. Splinter verifies the actual Unity executable, process start and `-projectPath`, retains the project lockfile requirement, and binds the receipt identity to the plan. Renew observation/expiry for a still-owned deferral using the same identity when needed.
4. After publication, externally inspect affected scenes/assets for **new** dirty state. Release exactly this request's acquired counters once, using its persisted state. If there are new edits, report synchronization incomplete and preserve them; do not force reload. Otherwise refresh, recompile, reload affected clean scenes/assets and verify actual loaded values. Unity may show a scene-external-change reload dialog even when its process remains responsive; handle it using the verified clean-state evidence, then reconnect and verify completion.

Auto-refresh deferral does not block explicit `Refresh`, scene watchers or external writes. Native conditional publication protects bytes. See the [Unity refresh-deferral contract](https://docs.unity.com/en-us/engine/6000.3/script-reference/unityeditor/assetdatabase/disallowautorefresh). Do not unconditionally repeat save, publish, counter release or scene reload after a timeout. CLI/JavaScript number serialization can round 64-bit ticks: return ticks as text from external tools and compare exact native integers when writing the permit.

## Final processing

Save the task, normally stop **only its owned Editor**, capture with a new explicit request key using the same generation engine, and publish that fixed result as above. This includes source writes made during normal shutdown. Keep original/peer Editors running. Then explicitly `workspace release --id <task> --home <home>` to preserve final source, seal and reclaim the owned resources. `checkpoint --release` combines normal stop, the same capture and preservation/reclamation; it does not publish to the original. Release alone does not authorize integration. Original shutdown and final project/Library restore are separate, explicitly authorized cleanup after all dependents are reclaimed.

Current limits remain: bounded ordinary-file manifests and supported metadata; Windows/NTFS/WinFsp native qualification only for recorded RI inputs; no Play/Player build, large-input generalization, other Unity versions/OS, or power-loss qualification. Required RI rows and the final full build gate remain obligations until the verification document records them.

## Implementation ownership

| Boundary | Code |
| --- | --- |
| Generation and live-original contract | [Core generations](../src/Splinter.Core/WorkspaceGenerations.cs), [original plan/state](../src/Splinter.Core/OriginalApply.cs) |
| Task requests, history and final closure | [Runtime generations](../src/Splinter.Runtime/WorkspaceGenerations.cs), [original application](../src/Splinter.Runtime/WorkspaceOriginalApply.cs), [state store](../src/Splinter.Runtime/WorkspaceStore.cs) |
| Durable range capture and reclamation protection | [Journal capture](../src/Splinter.Runtime/JournalSourceCapture.cs), [NTFS bridge](../src/Splinter.Platform.Windows/NtfsSourceGenerations.cs), [backing aliases](../src/Splinter.Platform.Windows/WindowsJournalFilePins.cs) |
| Independent payloads and per-path publication | [File generations](../src/Splinter.SourceControl.Files/FileSourceGenerations.cs), [file publication](../src/Splinter.SourceControl.Files/FileOriginalApply.cs), [Git guard](../src/Splinter.SourceControl.Git/GitOriginalPublicationGuard.cs) |
| External cooperation admission | [Unity policy](../src/Splinter.Application.Unity/UnityProjectPolicy.cs), [native Editor identity](../src/Splinter.Platform.Windows/WindowsOriginalEditorIdentityVerifier.cs) |

The [lifecycle map](lifecycle-optimization-map.md) and [structured index](lifecycle-optimization-map.json) locate C20/A20/A30 and K32–K35; they retain earlier performance evidence separately.
