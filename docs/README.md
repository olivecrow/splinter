# Documentation map

The [Unity maze benchmark](verification/parallel-workspace-performance.md) compares Splinter with Unity Agent Coordination and records the timing boundaries and the work each agent could perform independently.

## Start and operate

| Task | Guide |
| --- | --- |
| Choose a source and create or resume a workspace | [Agent start](agent-start.md), [agent CLI skill](../skills/splinter/SKILL.md) |
| Open an Editor, check the exact target and end a session | [Unity workspaces](alpha-workspaces.md), [external tool response recovery](unity-mcp-response-recovery.md) |
| Preserve, share or apply results | [Workspace import](workspace-import.md), [original publication](repeated-original-apply.md), [explicit discard](workspace-discard.md) |

## Storage and ownership

| Area | Guides |
| --- | --- |
| Architecture | [Foundation](foundation.md), [glossary](glossary.md), [extension boundaries](extensibility-boundaries.md) |
| Bases | [Git](git-project-basis.md), [Plastic](plastic-project-basis.md), [ordinary folder](plain-project-basis.md), [shared Unity Library](git-library-reference.md) |
| Lifecycle | [NTFS workspaces](ntfs-workspaces.md), [processes and resources](storage-and-resources.md) |
| Live storage | [Source and generated partitions](scalable-storage.md), [raw writes and flush](raw-write-batching.md), [compaction and base reversion](journal-base-reversion.md) |

## For contributors

| Area | Guides |
| --- | --- |
| Verification | [Regression suite and remaining qualification](verification/README.md) |
| Tools and views | [Development CLI](development-cli.md), [view workflow](view-workflow.md), [source verification](source-verification.md), [Unity render fixture](unity-render-qualification.md) |
| Recovery checks | [Journal](journal-recovery.md), [kernel](kernel-recovery.md), [product writes](product-write-recovery.md) |
| Component probes | [Write profiling](write-profiling.md), [replay profiling](replay-profiling.md), [bounded journal probe](plans/structural-throughput-prototypes.md), [bounded journal](bounded-journal.md), [latest-state journal](latest-only-journal.md) |
| Scope and remaining work | [Product requirements](../plan.html), [implementation scope](plans/implementation-plan.md), [storage qualification gates](plans/scalable-storage.md) |

Korean editions are available for the [main README](../README.ko.md) and [technical architecture](ko/foundation.md).
