# Implementation scope

Splinter supports the continuing-task workflow on Windows NTFS. A task has a stable writable path, can preserve an independent source result and can publish selected changes to the original after conflict checks. Unity tasks can also run an owned Editor. [Product requirements](../../plan.html) · [Architecture](../foundation.md)

## Remaining work

Qualify large native Git, Plastic and plain-folder lifecycles, peak memory, reclamation cost and OS-failure behavior under the bounded [scale and fault plan](scalable-storage.md). Diagnose any phase that exceeds its budget with a small representative case before resuming the original operation. The primary application-level performance check is the [Unity maze comparison](../verification/parallel-workspace-performance.md).

## Agent procedures and exclusions

The [start guide](../agent-start.md) covers setup and explicit completion. The [agent skill](../../skills/splinter/SKILL.md) owns command use, workspace handoff and scheduling independent edits. Agents select external tools for compilation, scene editing, Play, tests and builds. Splinter owns workspace identity, storage and resource lifetime. New application adapters and platform support require native verification.
