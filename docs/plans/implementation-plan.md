# Implementation scope and remaining work

[Requirements](../../plan.html) · [Current architecture](../foundation.md) · [Qualification](../verification/README.md)

The scoped first practical-use workflow is implemented: create/resume a task, work through its returned path, capture independent results, explicitly publish when requested, then release or discard owned resources.

## Remaining work

- Complete [SCAL qualification](scalable-storage.md) only within an explicitly approved bounded execution scope. Large-input usability, full lifecycle/resource measurements and OS-failure qualification remain incomplete.
- Investigate demonstrated bottlenecks with small reproductions before resuming large operations. Keep durable ACK, native ownership, reader, conflict and same-request recovery protections.
- Qualify new platforms, application adapters, client versions and external-tool workflows when actually needed; compilation and fake tests do not imply native support.

## Agent procedures and exclusions

The [start guide](../agent-start.md) covers setup and explicit completion. The [agent skill](../../skills/splinter/SKILL.md) owns tool selection and same-task handoff. Agents and existing Unity/Git/MCP/CLI tools perform editing, target selection, compilation, Play/Test/build and acceptance.

Dedicated installers, automatic conversation binding, scheduling/automatic mode selection, general lease takeover, shared-original-Editor orchestration, a general Unity API, AI Runner/DAG and daemon/DB are excluded. VSS and ReFS bases are retired. No new compatibility layer or automatic old-state migration is planned.
