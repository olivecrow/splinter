<a id="현재-기반"></a>
# Current foundation

[한국어](ko/foundation.md)

See the [glossary](glossary.md) for bases, deltas, seals, intents, and distinctions between completion commands.

MCP and Unity CLI are optional external tools selected as needed by agents from user-installed tools. Splinter storage isolation, bases, Library/unchanged-file references, range changes, independent results, safe original application, and owned-resource lifetime work without them. Specific tool names, versions, or internal APIs are not product prerequisites or gates. Agents handle selection, connection, refresh, compilation, Play/Test/build, capture, and result evaluation. Installing, downgrading, modifying, or creating/operating separate modified external tools is outside Splinter's scope. Follow [external-tool boundaries](unity-mcp-response-recovery.md) and [decoupling verification](verification/external-tool-boundaries.md).

The product path is **NTFS workspace creation/resumption → agent work through external tools → checkpoint → optional integrate → explicit release/discard**. Splinter manages workspaces, owned Editors, range storage, independent results, and safe reclamation. Compilation, tests, and game acceptance belong to agents and external tools such as MCP/Unity CLI.

When external tools are used, perform [exact target selection](alpha-workspaces.md#exact-target-selection-with-unity-cli). Historical [UH](verification/unity-cli-handoff.md)/MCP executions are evidence for their inputs/candidates, not current product tool-version requirements or compilation gates. Splinter's bridge does not manipulate external-package shutdown callbacks, servers, or run-state.

Product starting points are the [AI guide](agent-start.md) and `scripts/splinter.ps1`. Diagnostics, shared-Library views, and spikes/probes are separated into the [development CLI](development-cli.md). Existing verification is evidence for its candidate/input and does not automatically award native qualification to changed contracts.

Splinter's common storage layer provides parallel isolation/range storage. Source providers own the safety boundary between selected VCS metadata and Splinter task bases/results/reclamation. Product CLI explicitly selects Git, VCS-free ordinary folders, or Plastic environments. Source/application kind and opaque basis/result identities are fixed in v2 task state; ordinary folders use immutable file manifests and per-task independent payloads. Failure does not fall back to another source. [VCS-free workspaces](vcs-independent-workspaces.md) own current restrictions/actual qualification. Git remote registration is unnecessary. Management-information/registration isolation and independent file results in Plastic environments follow the [current contract](plastic-compatibility.md), with separate [actual qualification](verification/plastic-compatibility.md). Editor-free ordinary-folder [original apply](plain-folder-original-apply.md) provides plans/intents and conditional Windows publication; actual product qualification is limited to one small Editor-free NTFS A/B set.

<a id="구조와-의존성"></a>
## Structure and dependencies

Runtime `JsonPropertyValidation` commonly owns traversal for duplicate property names inside JSON objects/arrays. Names use Ordinal comparison; each input/state boundary retains size/depth/version/deserialization options and error codes. Development-view responses use temporary JSON inspection only to decide Accepted for `start`; other actions serialize results during final response output.

[OS/application extension boundaries](extensibility-boundaries.md) own path policy fixed in snapshot identity, optional Unix-permission metadata, and application project policies. Unity original locks, companions, and Library descriptors reside in the Unity adapter; Files/Windows use explicitly injected policies. Managed-basis state/preparation intent support only v2 with explicit application/directory identity. No actual other-OS/application adapters exist; [verification](verification/extensibility-boundaries.md) distinguishes automated conformance from real support.

The Plastic content reader runs a fixed client adapter embedded in Files through a standard-user PowerShell child and reads only private stdio frames. It provides neither a public API listener nor new CLI commands. The [current contract](plastic-project-basis.md) owns Windows `cm 11.0.16.9915`, PowerShell 7 prerequisites, and bounded helper segments; [security-boundary verification](verification/security-boundaries.md) owns automated/bounded native reads. Workspace storage isolation is not an OS security sandbox against malicious Git hooks, Unity code, or authenticated MCP code execution.

The [VCS-free project basis](plain-project-basis.md) uses original prepare/restore in Windows `ManagedDirectoryBasis(projectBasis:true)` and task-owned references in `ProjectDirectorySnapshotProvider`. Common `IProjectSourceAccess` holds exact basis/mount and required Editor-shutdown leases during original reads/publication. Files reuses existing independent results/three-way application; Windows publisher permits only the verified original view within a limited boundary. Task reclamation does not delete shared peer payload; final restore owns basis lifetime. No Win32/Unity APIs are added to Core.

Common Library/project restore uses v2 plans, existing file identities, new-file creation receipts, and Windows handle protection. Runtime journal child indexes update on create/move/delete/replay, avoiding full-path searches for directory queries. Git `GitProgressReadStream` limits only the exact owned reader's blocking reads to 30 seconds without interrupting whole-object verification that continues to progress. Core `WorkspaceFailurePolicy` commonly owns error response/recovery classification. Each feature document owns its contract and automated/actual qualification.

| Project | Current responsibility | References |
| --- | --- | --- |
| `Splinter.Core` | Workspace/storage/source/application/process ports; ownership/result/failure contracts. | .NET base libraries |
| `Splinter.Protocol` | Response DTOs, product/development command catalogs, embedded JSON Schema. | Core, JsonSchema.Net |
| `Splinter.Host` | Read-only discovery/diagnostics. | Core, Protocol |
| `Splinter.Runtime` | Workspace lifecycle, atomic JSON state/locks, range journals, candidate/cache lifetime. | Core |
| `Splinter.SourceControl.Files` | Ordinary-folder/Plastic file bases, independent payload, Plastic read-only compatibility boundary, fixed-source routing, discard receipts. | Core, Runtime |
| `Splinter.SourceControl.Git` | Fixed commit/tree/blob reader, owned basis refs/fences, linked worktrees/index, current-source hashes, independent results, guarded merge/reclamation. | Core |
| `Splinter.Application.Unity` | Exact-version/project Editor execution, readiness observations, normal shutdown, owned bridge. | Core |
| `Splinter.Platform.Windows` | VSS/Git immutable-reader composition, WinFsp, changed-range journals, restricted-privilege service, Job Objects. | Core, Runtime, Files, Git, WinFsp.Net; Win32 |
| `Splinter.Workflows.Windows` | Development immutable source views, shared Library caches, independent results, reclamation dependencies. | Runtime and Git/Unity/Windows |
| `Splinter.Cli` | Product-command parsing, adapter composition, JSON/exit codes; common composition for development entry point. | Host, Runtime, Git/Unity/Windows/workflow, System.CommandLine |
| `tools/Splinter.Development` | Separate development-command entry point `splinter-dev`. | CLI |
| `unity/Splinter.Editor` | Liveness/import/compile-progress observations, unsaved-scene/Play protection, normal shutdown. | Unity Editor API; included only in owned workspaces |
| `Splinter.Tests`, `tests/Splinter.UnityBridgeHarness` | Contracts/fakes, actual Git/Windows children, bridge-source conformance. | xUnit v3, MTP; bridge uses Unity API stubs |
| `tools/Splinter.ReplayProbe`, `tools/Splinter.WriteProbe` | Read/write costs and correctness instrumentation for fixed synthetic inputs. | Runtime; development tools |
| `tools/Splinter.BoundedLogProbe` | Bounded range-log [experiments](plans/structural-throughput-prototypes.md) and [product journal measurements](verification/bounded-journal.md). Rejected Bee tooling was removed; its code is preserved at `93a7395`. | Runtime; development tool |

CLI is a short-lived process. Host is a read-only in-process layer, not a daemon/DB writer. Runtime durably publishes intent before effects and same-request receipts. Provider-specific APIs stay in adapters; Core contains no Unity/Win32/Git/Codex APIs.

Runtime `WorkspaceActions` constructs next-action command IDs/arguments together; CLI projects them into responses without reparsing human-readable command strings. Same-ensure project/key/base/storage/source/application and Library/project basis, plus original-application target/intent/digest/selected paths, remain. CLI responses and failure recovery use identical ensure arguments. Existing schemas/durable state formats remain. [Optimization verification](verification/code-quality-optimization.md) owns automated regressions and measurement limits for changed searches, object verification, and compression traversal.

Runtime file locks serialize commands per workspace ID; project/shared-basis/snapshot locks protect required existing effect boundaries. Only confirmed sharing/lock violations receive bounded waits. Lock-path, access, and other I/O failures immediately reject with `StateReadUnavailable`, without interpreting them as occupancy or automatic recovery. [SP verification](verification/stable-parallel-completion.md) owns bounded current concurrent-CLI/file-I/O qualification.

The [read-only storage ledger](source-verification.md#meaning-of-measurements) distinguishes current SJR2 stored/decoded quantities, logically valid ranges, whole referenced-block costs, and exact task/shared-file allocation. [PS scope](plans/physical-storage-completion.md) and [execution verification](verification/physical-storage-completion.md) own physical-storage development exit boundaries/final-candidate qualification. Instrumentation does not replace product replay/seals or VSS ownership/reclamation contracts.

[TP-01–06](verification/unity-throughput-optimization.md) adds per-path Git interpretation in source manifest v3, one-use initial admission from verified immutable content, bounded capsule preparation and private-read snapshots. Full content proof, individual durable acknowledgements and independent-result seals remain. Failed flush or response publication keeps the mounted provider alive. The experimental Windows kernel-cache retention path failed normal Unity reclamation and was withdrawn; cleanup purge remains enabled. Component checks do not establish an end-to-end advantage over a shared Editor.

Workspace state and Windows control IPC publish same-directory flushed temporary files through `File.Replace` or initial nonoverwriting move. Reader delete sharing, bounded sharing-error retries, and preservation of unknown/corrupt/ownership errors follow the [NTFS contract](ntfs-workspaces.md). State-write failure stage/HResult/publication-completion details are in the [diagnostic contract](verification/state-write-diagnostics.md).

The [current compressed journal](journal-payload-compression.md) uses overlay v2, [default bounded SJR2](bounded-journal.md) for new ordinary Windows providers, explicit OFF SJR2, and candidate v3 raw/Brotli ranges. Bounded storage retains an individually flushed 16MiB/4,096-frame suffix and atomically publishes current private state at its limit; [BL](verification/bounded-journal.md) owns its separate qualification. [Optimization ON](latest-only-journal.md) uses SJL4/root v4 capsules retaining only current private payload/metadata, returning success after one durable flush per mutation, publication, and previous-payload/metadata reclamation. Distinguish actual product integration in [JP](verification/journal-payload-compression.md), internal buffers/costs/reclamation in [ME](verification/memory-completion.md), and new latest-state representation in [LJ](verification/latest-journal-write-optimization.md). Compression preparation uses one scratch buffer and selected-result array; small/empty compressed reads publish only after whole-stream verification through chunks and requested-range staging. Range journals retain individual acknowledged-write durable flush and before/after/unmount seals. Identical private bytes and small changes between matching leading/trailing ranges are reused, without initial full copying, read hydration, or whole-file copy-up fallback. Source-fingerprint caches are bound to snapshot/provider generation/change history; initial checks, audits, and reconnects verify full content. [Source verification](source-verification.md) checks stored content, not project acceptance.

Private Mutation default JSON values are omitted; explicit compaction/candidates select smaller current metadata. Inputs/responses/SJR2 meaning are not changed, and reads do not convert stored journals. [JM verification](verification/journal-metadata-storage.md) distinguishes offline file allocation for actual Unity journals from new-candidate native execution.

[Checkpoint/release optimization](verification/checkpoint-release-optimization.md) combines Git checkpoint/preservation inside stable seals, indexes SJL4 inventory lookups and consolidates fresh reclamation seal validation. Live writer accounting is labeled separately from actual-file audits; poisoned accounting remains unknown without terminating the provider. Request-file notifications wake the provider while one-second heartbeat polling and request identity checks remain. Existing independent-result, content hash, ownership and interrupted-recovery guards are retained.

Owned-process supervisors, PID/start time/token and zero child resources, plus actual mount/Git/delta/snapshot reclamation observations, are central to the product. [Explicit discard](workspace-discard.md) uses separate durable intent/authorization. `release` retains independent results and does not classify shared/reclaim-pending/unknown as completed reclamation. Open development candidates/sources/caches remain protected as snapshot dependencies.

Workspace state records storage, source, owned resources, independent results, and application/reclamation intents. View ledgers record candidate publication, source generations, cache dependencies, and per-request receipts. Unsupported fields/formats and damaged state are preserved and explicitly rejected. Agents/external tools perform compilation, Play/Test/build, and game acceptance decisions.

Development storage contracts are owned by Core `ViewWorkflow*`, `ImmutableCandidate`, `SourceViewSelection`; Runtime `ViewWorkflowCoordinator`, `SourceViewDeltaStore`, `CacheSnapshotTree`; Windows `ViewLifecycle`, `WindowsViewProvider`; and Git `GitSourceResult`. [Storage-view contract verification](verification/storage-view-contract.md) owns current schemas/regression evidence.

<a id="에이전트용-명령-계약"></a>
## Agent command contracts

The development-checkout execution prefix is `pwsh ./scripts/splinter.ps1`. Stdout contains one JSON response distinguishing acceptance, success, pending reclamation, and failure. Read `schema <command>` before first use; use returned `data.workspace.path` as the editing/external-tool target.

| Product command | Responsibility |
| --- | --- |
| `capabilities`, `schema`, `doctor` | Current product-contract discovery, inputs/outputs, read-only diagnosis. |
| `workspace ensure/list/status` | NTFS creation/same-request resumption, stored listings, owned-resource observations. |
| `workspace status --verify-source/--audit-source` | Current content-hash verification/full audit. |
| `workspace checkpoint` | Normal owned-Editor stop, stable-source checkpoint, independent results. Compile/test success unnecessary. |
| `workspace integrate` | Exact Git result/clean-branch merge, or ordinary-folder/recorded-Plastic `--target original --plan` and conditional NTFS publication with fixed intent/digest/selected paths. |
| `workspace compact` | Private-journal cleanup after normal stop/unmount, seals, and explicit budgets. [Optimization ON](latest-only-journal.md) subsequently stores only latest state and retains the choice; new ordinary Windows tasks default to bounded, omitted options preserve the stored mode, and explicit true/false selects ON/append. Includes explicit compact [base reversion](journal-base-reversion.md). |
| `workspace release/discard` | Reclamation after independent-result preservation/explicit result discard. Protect peers/shared dependencies. |
| `library prepare/status/restore` | Explicitly authorized same-volume original-Library move, original/A/B range storage, final ordinary-NTFS restoration. [Contract](git-library-reference.md). |
| `project prepare/status/restore` | Explicitly authorized same-NTFS-volume move of a VCS-free whole original, shared source/Library basis, original/A/B range storage, final ordinary-NTFS restoration. [Contract](plain-project-basis.md). |
| `unity start/stop` | That workspace Editor's lifetime. Confirm preparation/shutdown through state observations. |
| `storage service status` | Read-only status of existing restricted VSS service. |

Discover development contracts through `scripts/splinter-dev.ps1 capabilities/schema`. `view` manages immutable candidates, source generations, Library caches, and independent-result lifetime. Separate spike/probe inputs and execution conditions follow the [development CLI](development-cli.md).

<a id="진단의-의미"></a>
### Meaning of diagnostics

`doctor`, stored listings, Editor readiness, source fingerprints, and checkpoint success each prove only their own boundary. They do not mean external-tool connection, game behavior, or compile/test acceptance. Exact external-tool A/B projectRoot and shared-server coexistence require separate usage qualification.

<a id="제공하지-않는-작업-그래프"></a>
### Task graphs not provided

AI Runner, DAG/plan submission, daemon/DB, queue/automatic mode, automatic Codex binding, dedicated installer, and general Unity authoring/Play/Test APIs are not provided. Installation connection, conversation handoff, work order, validation decisions, and conflict resolution are agent procedures.

<a id="저장소와-브랜치-운영"></a>
## Repository and branch operation

`development` is the development/integration basis; `main` is for releases. Sibling `../splinter-unity` is an independent Git/Unity fixture with separate instructions. Document/source changes do not authorize cleanup of existing result refs, snapshots, or other tasks.

<a id="빌드와-시험"></a>
## Build and tests

```powershell
pwsh ./scripts/build.ps1
```

Runs locked restore, Release build, and automated contract/fake/Git/Windows regressions. Maintain central dependency versions/lockfiles. Automated tests do not qualify native VSS/Unity, ARM, or OS failures. [Verification guide](verification/README.md) preserves raw evidence/support limitations.

External recovery/performance measurement/preserved-measurement recovery entry points and child-only `Child`, `ChildWriter`, `ChildProvider`, `ChildClient`, `ChildReader`, `ClientActor`, and `ChildEnsure` are Skipped without specified execution environment variables. Scripts/parent tests invoke those same entry points with the variables. Default automated execution also runs synthetic journal/preparation-interruption regressions invoked by parent tests, separately from external kernel/product-write recovery execution qualification. Behavior/metadata/guard tests use small inputs; only tests validating large-file reuse, chunk boundaries, or storage costs specify required large inputs.

Windows-only file-sharing/native-contract tests are Skipped on other OSes. When standalone fixed-response/error-mapping/instrumentation tests repeat behavior, combine unique assertions into actual create/resume/reject/replay tests. Retain regression protections such as renames beneath unchanged intermediate parents, request digests, individual durable flush, and original/independent-result protection.

<a id="실제-저장소의-git-수명주기-시험"></a>
### Git lifecycle tests in actual repositories

`pwsh ./scripts/test-worktree-lifecycle.ps1 -Repository <path>` is a development test modifying worktree registrations in the specified repository. Distinguish it from Unity/CoW qualification and preserve existing tasks, original, and refs.

<a id="다음-구현-경계"></a>
## Next implementation boundaries

Follow the [implementation plan](plans/implementation-plan.md). VCS-neutral contracts, ordinary-folder basic lifecycle, and protection boundaries for existing Git environments closed with explicit limitations for one small A/B set. Distinguish implementation/automated-file tests for [safe ordinary-folder original application](plain-folder-original-apply.md) from bounded completion of small Editor-free product A/B. The [Plastic compatibility contract](plastic-compatibility.md) was separately connected and boundedly closed through final reclamation of small T: A/B. [PC verification](verification/plastic-compatibility.md) owns preceding C: reclamation deferral/external CLI discovery failure. Recorded Plastic [full/selected original application](plastic-original-apply.md) follows [separate bounded qualification](verification/plastic-original-apply.md). Verify full external-tool handoff separately for needed inputs. Select only actually observed storage/memory bottlenecks. Do not automatically reopen completed Git-based M0, large PV, or the whole failure catalog.

Plastic `plastic-snapshot` connects `Splinter.SourceControl.Files`' fixed-revision reader with the existing Windows overlay/shared Library dependency. [Project basis](plastic-project-basis.md) and [verification](verification/plastic-project-basis.md) own the current contract/qualification. Original application follows the separate contract above; new Plastic registration/check-in and server pins are not provided.
