# Documentation map

[Project overview](../README.md) · [Technical architecture](foundation.md) · [Validation limits](verification/README.md)

Korean editions are available for the [main README](../README.ko.md) and [technical architecture](ko/foundation.md).

## Start and operate

| Guide | Contents |
| --- | --- |
| [Agent start](agent-start.md) | Setup, create/resume, continue and finish a task |
| [Agent skill](../skills/splinter/SKILL.md) | CLI schemas, exact paths and request handoff |
| [Unity workspaces](alpha-workspaces.md) | Owned Editors and external tool selection |
| [Glossary](glossary.md) | Bases, ranges, seals, results and reclamation |
| [Development CLI](development-cli.md) | Reusable diagnostics and probes |

## Current contracts

| Area | Owning guides |
| --- | --- |
| Architecture and resources | [Foundation](foundation.md), [storage/resources](storage-and-resources.md), [extension boundaries](extensibility-boundaries.md) |
| Project bases | [Git](git-project-basis.md), [Plastic](plastic-project-basis.md), [managed ordinary project](plain-project-basis.md), [Library sharing](git-library-reference.md) |
| Workspace lifecycle | [NTFS workspaces](ntfs-workspaces.md), [VCS-independent workspaces](vcs-independent-workspaces.md), [discard](workspace-discard.md) |
| Current storage | [Scalable storage](scalable-storage.md), [bounded journal](bounded-journal.md), [raw writes and shared flush](raw-write-batching.md), [base reversion](journal-base-reversion.md), [latest-state journal](latest-only-journal.md) |
| Results and publication | [Import into another workspace](workspace-import.md), [repeated original application](repeated-original-apply.md), [plain-folder publication](plain-folder-original-apply.md), [Plastic publication](plastic-original-apply.md), [Plastic boundary](plastic-compatibility.md) |
| Recovery | [Journal](journal-recovery.md), [kernel](kernel-recovery.md), [product writes](product-write-recovery.md), [external tools](unity-mcp-response-recovery.md) |
| Development views | [Workflow](view-workflow.md), [source views](source-view.md), [source verification](source-verification.md), [overlay](snapshot-overlay.md), [Unity preview checks](unity-render-qualification.md) |
| Diagnostics | [Write profiling](write-profiling.md), [replay profiling](replay-profiling.md), [bounded journal probe](plans/structural-throughput-prototypes.md) |

## Active scope and qualification

[Product requirements](../plan.html), [implementation scope](plans/implementation-plan.md), [SCAL plan](plans/scalable-storage.md) and [validation guide](verification/README.md) distinguish implemented behavior from pending support/performance qualification.

[Current parallel workspace performance](verification/parallel-workspace-performance.md) records matched A/B measurements in a dedicated real Unity URP project, compilation/scene/Play checks, Library restoration cost and remaining qualification limits.
