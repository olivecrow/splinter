# Splinter

**Isolated workspaces that share unchanged data for parallel AI development.**

[한국어](README.ko.md)

Splinter is a local workspace and storage tool for AI agents working on Unity projects and ordinary project folders. Each task gets its own writable project path, while unchanged files are read from a fixed project basis and edits are stored as private byte ranges. Multiple tasks can work independently, the original project stays editable, and results are preserved or integrated explicitly.

**Current status:** a research alpha for Windows and NTFS, with bounded validation on small Git, Unity Version Control / Plastic SCM, and VCS-free projects. The source and recorded evidence are available for others to inspect, reproduce, and extend. See the [validation guide](docs/verification/README.md) for tested inputs and remaining limits.

The current [scalable-storage candidate](docs/scalable-storage.md) writes task v4/basis v3/overlay v3 with metadata-store format 2 (`SPM2`), replacing aggregate metadata/journal quotas with disk pages and streamed processing. Earlier `SPM1` stores are preserved and refused; finish them with their matching binary. SCAL-01–08 qualification is still in progress; no earlier small-input timing or support result is inherited by this candidate.

The user curtailed large verification on 2026-11-01. Recorded slow phases include multi-hour 100k tiny-file operations. Large-project usability, the remaining full lifecycles and matched performance preservation are unqualified; retained fixture state is documented separately.

## Why Splinter

Parallel development becomes expensive when every task needs a complete project copy, its own generated data, and an independent application instance. Unity makes this especially visible: source isolation alone does not address the cost of assets, the `Library` directory, and multiple Editors.

Splinter separates the data a task can reuse from the data it changes. Its range journals avoid an initial full project copy, a disk cache of files merely read, and copying an entire base file just to modify part of it. An explicitly prepared Library basis can also share unchanged generated data while keeping each task's writes private.

New changed payloads are stored uncompressed. Concurrent writes can share a durable flush before success; retired encoded payloads are preserved and rejected. See the [current write policy and bounded verification](docs/raw-write-batching.md).

Separate Editors also let each task choose when to apply changes, compile and validate. For example, character, enemy-AI and UI work can use separate project/scene states: validating one feature need not replace the source under another task's active scene, Play session or debugging work. Source edits and compilation errors stay within the task's workspace until explicitly integrated.

Writing code in a separate code-only worktree does not itself disturb a shared Editor. Coordination becomes necessary when that change is applied to the shared project for compilation or validation. Independent sessions reduce those handoffs and interruptions; they do not make a task immune to its own compilation/domain reload, nor eliminate contention for CPU, memory, disk or shared external tools. This is a structural workflow benefit, not a measured long-term productivity gain.

The intended workflow is simple: use a workspace when a task needs independent execution, continue that same task across conversations, then explicitly preserve, integrate, or discard it. Each agent works in the path returned by Splinter and can use its existing file tools, Git tools, and application tooling there.

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
- **Storage that follows changes.** Shared immutable data, bounded raw changed payloads, and journal compaction. New workspaces use a [bounded durable journal](docs/bounded-journal.md); explicit compaction can also remove ranges that match the basis again.
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

Git and Plastic tasks can attach an explicitly prepared [Library basis](docs/git-library-reference.md). Git commit bases contain committed raw bytes and exclude ignored files; checkout filters, LFS, and unsupported special files require attention to the route's input restrictions. The recorded Plastic reader currently requires client version `11.0.16.10484`.

## Getting started

Splinter is currently used through the PowerShell CLI included in a source checkout. Create an independent workspace, use your existing editing, Git, and Unity tools in the returned project path, then explicitly preserve, integrate, or discard the result. Continue an ongoing task in the same workspace.

Requirements are Windows/NTFS, PowerShell 7.2 or later, the .NET SDK specified by [global.json](global.json), and [WinFsp](https://github.com/winfsp/winfsp). Git, Plastic, and Unity are needed when used by your project.

Follow the [getting started guide](docs/agent-start.md) for setup and your first workspace, or the [Unity guide](docs/alpha-workspaces.md) for Editor use. The [agent skill](skills/splinter/SKILL.md) provides detailed operating instructions for AI agents.

## Scope and limits

The implementation and recorded validation target Windows/NTFS alpha use with small projects. Large projects, macOS/Linux adapters, comprehensive ARM coverage, and reboot, power-loss, or general OS-failure guarantees remain outside that qualification. The [validation guide](docs/verification/README.md) distinguishes automated contract tests from actual filesystem and Unity runs.

Sharing unchanged data reduces duplication in the storage model. Each Unity Editor still has its own memory and import costs; the current evidence does not establish general reductions in total system RAM or peak disk usage. Unsupported file types and metadata are documented per storage route.

Splinter provides a CLI and agent guidance. Dedicated installation, automatic conversation binding, task scheduling, automatic mode selection, shared-original-Editor orchestration, and an AI execution platform are outside the current implementation. Workspace isolation protects storage and resource ownership; it is not an OS security sandbox for untrusted code. Unsupported or damaged state is preserved and explicitly rejected, without automatic migration.

## Documentation

Explore the [documentation map](docs/README.md) for detailed feature guides. English is the default; Korean is provided only for the main README and technical architecture.

| Document | Contents |
| --- | --- |
| [Getting started](docs/agent-start.md) | Setup, your first workspace, continuing and finishing a task |
| [Unity guide](docs/alpha-workspaces.md) | Working on Unity projects in independent Editors |
| [Glossary](docs/glossary.md) | Bases, private changes, result preservation, and resource reclamation |
| [Architecture](docs/foundation.md) | Splinter's components and how they work together |
| [Validation scope](docs/verification/README.md) | Tested environments, measurement evidence, and support limits |
| [Third-party notices](THIRD_PARTY_NOTICES.md) | Dependency sources, versions, and license conditions |

## License

Splinter's own code and documentation are available under the [MIT license](LICENSE). Third-party components retain their own licenses and conditions; see [Third-party notices](THIRD_PARTY_NOTICES.md).

## Credits

Splinter builds on the work of these projects and their contributors:

- **[WinFsp](https://github.com/winfsp/winfsp)** supplies the writable filesystem views and .NET binding. **WinFsp - Windows File System Proxy, Copyright (C) Bill Zissimopoulos.** Its license is GPLv3 with a conditional exception for linking from free and open source software.
- **[.NET](https://github.com/dotnet/runtime)** and **[System.CommandLine](https://github.com/dotnet/command-line-api)** provide the runtime and CLI infrastructure. Changed payloads are stored uncompressed.
- **[json-everything](https://github.com/json-everything/json-everything)** provides JsonSchema.Net, JsonPointer.Net, and Json.More.Net for schema validation and JSON handling. Its source is MIT-licensed; the pinned NuGet binaries also include an Open Source Maintenance Fee agreement.
- **[Humanizer](https://github.com/Humanizr/Humanizer)** is an indirect runtime dependency of the JSON packages, under the MIT license.
- **[xUnit.net](https://github.com/xunit/xunit)** and **[Microsoft.Testing.Platform](https://github.com/microsoft/testfx)** provide automated testing and TRX reports, under Apache 2.0 and MIT respectively.
- **[Matplotlib](https://matplotlib.org/)** renders the recorded comparison charts through an optional documentation script.

Installed Git, PowerShell, Unity, and Plastic tools retain their own licenses. Exact direct and indirect package versions, upstream license links, attribution conditions, and redistribution considerations are recorded in [Third party notices](THIRD_PARTY_NOTICES.md).
