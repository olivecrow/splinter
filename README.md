# Splinter

**Independent workspaces for parallel development on Windows.**

[한국어](README.ko.md)

Splinter gives each task its own writable project path. Tasks share a fixed project basis and store their changes as private byte ranges, while the original stays editable. The same workspace can be resumed across conversations, and each Unity task can run its own Editor.

## How it works

A WinFsp mount presents the shared basis and the task's changes as one project tree. Splinter creates the workspace without a full project copy, a persistent cache of every file read, or a whole-file copy for a partial edit. Unity tasks can also share unchanged data from a prepared `Library` basis.

1. **Create or resume.** Use a stable task key and work in the path Splinter returns. For Unity, start an Editor for that workspace when needed.
2. **Checkpoint and share.** Preserve a fixed source result and keep editing. Another workspace can [import the whole change or selected paths](docs/workspace-import.md). Use existing tools to compile, run Play Mode, test, and build.
3. **Finish explicitly.** Apply a reviewed result to the original, release the workspace and retain its result, or discard the task. Application checks the current target for conflicts.

## Project sources

| Source | Storage mode | Basis |
| --- | --- | --- |
| [Git](docs/git-project-basis.md) | `git-snapshot` | A fixed local commit from a supported clean repository |
| [Unity Version Control / Plastic SCM](docs/plastic-project-basis.md) | `plastic-snapshot` | A recorded changeset read through a supported installed client |
| [Folder without version control](docs/plain-project-basis.md) | `project-snapshot` | A managed basis prepared on the original NTFS volume |

Git and Plastic tasks can attach a separately [prepared Unity Library](docs/git-library-reference.md). Managed project and Library preparation move existing data within the same NTFS volume and expose a writable view at the original path. For Unity projects, preparation and final restoration require the original Editor to save and exit normally.

## Quick start

Use a source checkout on Windows with NTFS, WinFsp, PowerShell 7.2+, and the [.NET SDK pinned in global.json](global.json). Install Git, Plastic, or Unity as required by your project.

From the Splinter checkout, build the CLI and inspect the workspace schema. This example uses a clean Git repository containing a Unity project. Set the workspace home outside the original project.

```powershell
pwsh ./scripts/build.ps1
pwsh ./scripts/splinter.ps1 capabilities
pwsh ./scripts/splinter.ps1 schema workspace.ensure

$project = 'C:/projects/MyGame'
$workspaceHome = 'C:/sp/mygame'
$base = git -C $project rev-parse HEAD

pwsh ./scripts/splinter.ps1 workspace ensure --project $project --home $workspaceHome `
    --key lighting-pass --source git --application unity --storage git-snapshot --base $base
```

After `workspace ensure` succeeds, use `data.workspace.path` for file edits and tool commands. Keep the same project, home, key, and base to resume. Follow the guides for [setup and completion](docs/agent-start.md), [Unity Editor use](docs/alpha-workspaces.md), and [agent handoff](skills/splinter/SKILL.md).

## Status

Splinter is a Windows/NTFS research alpha. Validation covers specific small workloads. Large-project lifecycles, general performance and memory gains, and OS-failure durability remain unqualified. Each Unity Editor still has its own memory and import costs. Isolation is limited to project storage and resource ownership. [Validation scope](docs/verification/README.md)

## Documentation and license

- [Documentation map](docs/README.md) for storage, publication, recovery, and development tools.
- [Technical architecture](docs/foundation.md) for components, data formats, and ownership boundaries.
- [Product requirements](plan.html) for supported workflows and project scope.

Splinter's code and documentation are licensed under [MIT](LICENSE). Dependency versions and license conditions are in the [third-party notices](THIRD_PARTY_NOTICES.md).

WinFsp - Windows File System Proxy, Copyright (C) Bill Zissimopoulos. [WinFsp source](https://github.com/winfsp/winfsp)
