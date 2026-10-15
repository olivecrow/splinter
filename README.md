# Splinter

**Efficient, isolated workspaces for parallel AI development.**

[한국어](README.ko.md)

Splinter is a local workspace and storage tool for AI agents working on Unity projects and ordinary project folders. Each task gets its own writable project path, while unchanged files are read from a fixed project basis and edits are stored as private byte ranges. Multiple tasks can work independently, the original project stays editable, and results are preserved or integrated explicitly.

**Current status:** an internal alpha for Windows and NTFS, with bounded validation on small Git, Unity Version Control / Plastic SCM, and VCS-free projects. See the [validation guide](docs/verification/README.md) for the tested inputs and remaining limits.

## Why Splinter

Parallel development becomes expensive when every task needs a complete project copy, its own generated data, and an independent application instance. Unity makes this especially visible: source isolation alone does not address the cost of assets, the `Library` directory, and multiple Editors.

Splinter separates the data a task can reuse from the data it changes. Its range journals avoid an initial full project copy, a disk cache of files merely read, and copying an entire base file just to modify part of it. An explicitly prepared Library basis can also share unchanged generated data while keeping each task's writes private.

The intended workflow is simple: use a workspace when a task needs independent execution, continue that same task across conversations, then explicitly preserve, integrate, or discard it. Each agent works in the path returned by Splinter and can use its existing file tools, Git tools, and application tooling there.

<a id="measured-storage-and-time-tradeoff"></a>
## Measured storage and time

The figures below use the SJL4 configuration measured on 2026-10-14. Unity time and storage for the [bounded-journal default](docs/bounded-journal.md) have not yet been measured.

Two independent Unity Editors each make and compile one isolated one-line C# change in a **2.80 GB project with 1,602 tracked files and an existing 3.14 GB Library** (Windows/NTFS, Unity 6000.3.22f1). **Both methods run A/B in parallel.** Ordinary Git worktrees in the Codex task paths build new Libraries; Splinter workspaces reference the original Library.

### Complete workflow comparison

Preparation, editing, compilation, result preservation, combining source results, and cleanup took **13m 39.7s with Codex worktrees and 15m 35.2s with Splinter**. Splinter's complete time was **14.1% longer** in this run.

Reusing one shared Editor to validate the independently written A/B changes sequentially took **1m 42.5s**. That separate [shared-Editor comparison](#parallel-coding-with-one-shared-editor) measures a different workflow with the same existing Library and source changes.

![Workspace and Editor operation time with a shared Editor, ordinary worktrees, and Splinter](docs/images/unity-readme-shared-editor.png)

| A/B stage | Codex worktrees | Splinter |
| --- | ---: | ---: |
| Library basis preparation | Not needed | 1m 00.2s |
| Create A + B workspaces | 13.4s | 2m 48.8s |
| Launch both Editors → compiled readiness | 12m 41.7s | 5m 10.5s |
| One-line edits → recompiled readiness | 9.4s | 31.7s |
| Selected asset inspection | 7.6s | 1.2s |
| Normal shutdown of both Editors | 3.6s | 1m 56.8s |
| Preserve results + reclaim workspace files | 23.3s | 3m 19.5s |
| Combine A/B source results | 0.7s | 0.7s |
| Restore original Library | Not needed | 45.5s |
| **Complete workflow** | 13m 39.7s | 15m 35.2s |

Codex timing covers parallel `git worktree add` in the app's task paths, then Editor work through result preservation and `git worktree remove`. Splinter timing spans Library preparation through restoration. Chat creation, app archive, read-only input checks, and allocation scans are excluded. Two empty Codex task directories could not be removed; their files and Git registrations were reclaimed. Overlapping A/B intervals are counted once, and totals include small command-dispatch gaps. The [measurement report](docs/verification/unity-current-comparison.md) details these boundaries.

Both methods combined A/B into the same conflict-free Git result, preserved separately without changing the original source. Each A/B compilation and asset check, normal Editor exit, owned workspace reclamation, and original Library restoration were verified. The combined result was not recompiled in an Editor, and Play Mode, Unity tests, and a game build were not run.

### Initial readiness and repeated editing

![Initial Editor readiness and edit recompilation with Codex worktrees and Splinter](docs/images/unity-readme-editor-comparison.png)

| A/B interval after workspace creation | Codex worktrees | Splinter |
| --- | ---: | ---: |
| Initial compiled readiness | 12m 41.7s | 5m 10.5s |
| One-line edits → recompiled readiness | 9.4s | 31.7s |

Referencing an existing Library can reduce fresh-workspace import costs, but repeated-edit costs matter too. Continuing a prepared workspace has a different cost from creating a new one. Reusing the same workspace while continuing a task spreads its initial setup and final cleanup costs across more work.

### Storage footprint

**Measured SJL4 configuration, 2026-10-14:** additional file allocation after both edits compiled was **10.52 GiB for Codex and 0.14 GiB for Splinter: 98.64% less**.

![Additional file allocation in the measured Codex worktree and Splinter configuration](docs/images/unity-readme-storage.png)

Task and control files, owned Git registration, and shared runner/Library metadata are counted once. Original source, original Library payload, shared Git objects, and filesystem directory metadata are excluded. This is a stage measurement, not peak disk use.

### Parallel coding with one shared Editor

Two independent Codex agents prepare A/B in separate code worktrees. One Editor then validates A, restores A's source while applying B in the same refresh, validates B, and finally restores and recompiles the original source.

| Measured interval | One shared Editor | Splinter: two Editors |
| --- | ---: | ---: |
| Workspace/Editor workflow, including preservation and cleanup | **1m 42.5s** | **15m 35.2s** |
| Editor launch → initial compiled readiness, after workspace creation | 40.6s | 5m 10.5s |
| Both candidates ready → both compilations and asset checks complete | 29.7s | 32.9s |

B waits **16.0s** for A to release the shared Editor, then takes **13.8s** to validate. Splinter removes that shared-Editor queue, but its parallel validation still takes longer in this small case; preparation and cleanup add further cost. Reusing one Editor is faster for this workload.

The shared-Editor run is new; Splinter's RC-01 measurement is reused after verifying identical source/Library contents and unchanged product/runtime hashes. Totals count workspace and Editor operations, excluding AI reasoning, agent-dispatch gaps, and read-only input checks. This is one observation per workflow, not a long-test or build benchmark. See [SE-01](docs/verification/unity-shared-editor.md) for stage timings and evidence.

### Choosing between short and long tasks

For a small change in an already-ready Editor, creating, preserving, merging, and cleaning up parallel workspaces can cost more than the edit itself. Working directly in the original can be simpler and faster when independent execution is unnecessary.

Code can be written in parallel even when validation uses one shared Editor. The potential saving from independent Editors comes from overlapping **Editor work**—imports, compilation, tests, or builds that would otherwise wait for that Editor. Longer AI reasoning or code-writing time alone does not create this saving.

Once both candidates are ready, Editor operations taking `E_A` and `E_B` require roughly `E_A + E_B` with one Editor or `max(E_A, E_B)` with two, assuming unchanged per-task speed. The overlap, `min(E_A, E_B)`, must outweigh the extra preparation, preservation, merge, cleanup, and resource contention. For example, two 30-minute Editor operations with 10 minutes of extra parallel overhead would take 40 minutes instead of 60. This is a model, not a measured crossover; concurrent imports or builds can slow each other down.

Ordinary worktrees with separate project paths and Libraries can also use independent Unity Editors. Splinter reduces duplicated source and Library storage; it is not required merely to launch two Editors. An application-wide singleton, a shared external-tool session, or an exclusive GPU/device can still serialize work. This compile-and-asset-check benchmark does not establish parallel Play Mode, test, or build performance.

The scripted one-line changes measure workspace and Unity costs, excluding AI reasoning and feature-development time. Each method was measured once on the same original input, without clearing OS caches or randomizing order. See the [measurement report](docs/verification/unity-current-comparison.md) and [exact dataset](reports/benchmarks/unity-current-comparison-20261014.json) for conditions and evidence.

## How it works

A **basis** is the fixed data from which a workspace starts. A **private journal** records that workspace's changed byte ranges and file metadata. Together, they form the project tree exposed through a writable WinFsp filesystem.

```text
Fixed project basis
  + task A's private changes  ->  workspace A
  + task B's private changes  ->  workspace B
```

Edits in A do not change B's files or silently update the original. A checkpoint preserves a stable, independent source result; integration applies that result through the selected source adapter's guards. Releasing a workspace preserves its result while reclaiming resources owned by that task. Discarding is a separate, explicit operation.

The original remains usable during task work. For the optional managed Library or VCS-free project basis, preparation moves existing data within the same NTFS volume without copying it and exposes a writable view at the original path. Preparation and final restoration require the original Editor to save and stop normally; restoration returns the original's current contents to an ordinary NTFS directory.

## Main capabilities

- **Independent task workspaces.** Separate writable paths, private changes, task state, and owned application processes, with protection for the original and other tasks.
- **Storage that follows changes.** Shared immutable data, bounded raw or Brotli payloads, and journal compaction. New workspaces use a [bounded durable journal](docs/bounded-journal.md); explicit compaction can also remove ranges that match the basis again.
- **Work that survives a conversation.** Stable task identities, atomic JSON state, and recovery using the same request after interruption or a lost response.
- **Independent results and guarded integration.** Source checkpoints, Git result integration, and planned full or selected file application for supported plain-folder and recorded Plastic paths. Changed targets and conflicting inputs are rejected for explicit resolution.
- **Explicit resource ownership.** Unity Editor start/stop, provider and mount tracking, dependency guards, and release or discard of the task's own resources.
- **An interface for agents.** One JSON response on stdout, discoverable capabilities, command schemas, structured errors, and a repository-provided [agent skill](skills/splinter/SKILL.md).

Splinter manages workspace storage and resource lifetimes. Agents and their chosen tools perform editing, refresh, compilation, Play Mode, tests, builds, and assessment of the result. A checkpoint establishes source stability; compilation and test results come from those external tools. MCP and Unity CLI are optional integrations selected by the agent.

## Storage options

The source kind is explicit: `git`, `plastic`, or `none`. Choose a storage path for the actual project and read its prerequisites before creating a workspace.

| Storage | Project basis | Preparation |
| --- | --- | --- |
| [`git-snapshot`](docs/git-project-basis.md) | A fixed local Git commit, tree, and blobs | A supported clean repository at the requested commit; WinFsp. No VSS service is required. |
| [`plastic-snapshot`](docs/plastic-project-basis.md) | A fixed recorded Plastic changeset and file revisions | Read access through the qualified installed Plastic client; WinFsp. No new Plastic workspace registration or VSS service is required. |
| [`project-snapshot`](docs/plain-project-basis.md) | A managed basis for a VCS-free project, including its generated data | Explicit `project prepare`, a move within the same NTFS volume, and a writable view at the original path. No VSS service is required. |
| [`ntfs-snapshot`](docs/ntfs-workspaces.md) | A VSS snapshot of a Git, Plastic, or VCS-free project | WinFsp and the restricted storage service with the exact project/home identities registered. |

Git and Plastic tasks can attach an explicitly prepared [Library basis](docs/git-library-reference.md). Git commit bases contain committed raw bytes and exclude ignored files; checkout filters, LFS, and unsupported special files require attention to the route's input restrictions. The recorded Plastic reader currently requires client version `11.0.16.9915`.

## Getting started

Splinter is currently used through the PowerShell CLI included in a source checkout. Create an independent workspace, use your existing editing, Git, and Unity tools in the returned project path, then explicitly preserve, integrate, or discard the result. Continue an ongoing task in the same workspace.

Requirements are Windows/NTFS, PowerShell 7.2 or later, the .NET SDK specified by [global.json](global.json), and [WinFsp](https://github.com/winfsp/winfsp). Git, Plastic, and Unity are needed when used by your project; `ntfs-snapshot` additionally requires the [restricted storage service](docs/storage-privilege-service.md).

Follow the [getting started guide](docs/agent-start.md) for setup and your first workspace, or the [Unity guide](docs/alpha-workspaces.md) for Editor use. The [agent skill](skills/splinter/SKILL.md) provides detailed operating instructions for AI agents.

## Scope and limits

The implementation and recorded validation target Windows/NTFS alpha use with small projects. Large projects, macOS/Linux adapters, comprehensive ARM coverage, and reboot, power-loss, or general OS-failure guarantees remain outside that qualification. The [validation guide](docs/verification/README.md) distinguishes automated contract tests from actual filesystem and Unity runs.

Sharing unchanged data reduces duplication in the storage model. Each Unity Editor still has its own memory and import costs; the current evidence does not establish general reductions in total system RAM or peak disk usage. Unsupported file types and metadata are documented per storage route.

Splinter provides a CLI and agent guidance. Dedicated installation, automatic conversation binding, task scheduling, automatic mode selection, shared-original-Editor orchestration, and an AI execution platform are outside the current implementation. Workspace isolation protects storage and resource ownership; it is not an OS security sandbox for untrusted code. Unsupported or damaged state is preserved and explicitly rejected, without automatic migration.

## Documentation

Explore the [documentation map](docs/README.md) for detailed feature guides. English is the default; [Korean documentation](docs/ko/README.md) is available separately, with language links on each page.

| Document | Contents |
| --- | --- |
| [Getting started](docs/agent-start.md) | Setup, your first workspace, continuing and finishing a task |
| [Unity guide](docs/alpha-workspaces.md) | Working on Unity projects in independent Editors |
| [Glossary](docs/glossary.md) | Bases, private changes, result preservation, and resource reclamation |
| [Architecture](docs/foundation.md) | Splinter's components and how they work together |
| [Validation scope](docs/verification/README.md) | Tested environments, measurement evidence, and support limits |
| [Third-party notices](THIRD_PARTY_NOTICES.md) | Dependency sources, versions, and license conditions |

## Credits

Splinter builds on the work of these projects and their contributors:

- **[WinFsp](https://github.com/winfsp/winfsp)** supplies the writable filesystem views and .NET binding. **WinFsp - Windows File System Proxy, Copyright (C) Bill Zissimopoulos.** Its license is GPLv3 with a conditional exception for linking from free and open source software.
- **[.NET](https://github.com/dotnet/runtime)** and **[System.CommandLine](https://github.com/dotnet/command-line-api)** provide the runtime and CLI infrastructure. Brotli compression is used through .NET's `System.IO.Compression` APIs.
- **[json-everything](https://github.com/json-everything/json-everything)** provides JsonSchema.Net, JsonPointer.Net, and Json.More.Net for schema validation and JSON handling. Its source is MIT-licensed; the pinned NuGet binaries also include an Open Source Maintenance Fee agreement.
- **[Humanizer](https://github.com/Humanizr/Humanizer)** is an indirect runtime dependency of the JSON packages, under the MIT license.
- **[xUnit.net](https://github.com/xunit/xunit)** and **[Microsoft.Testing.Platform](https://github.com/microsoft/testfx)** provide automated testing and TRX reports, under Apache 2.0 and MIT respectively.
- **[Matplotlib](https://matplotlib.org/)** renders the recorded comparison charts through an optional documentation script.

Installed Git, PowerShell, Unity, and Plastic tools retain their own licenses. Exact direct and indirect package versions, upstream license links, attribution conditions, and redistribution considerations are recorded in [Third party notices](THIRD_PARTY_NOTICES.md).
