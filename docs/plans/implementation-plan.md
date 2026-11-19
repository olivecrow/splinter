# Implementation scope and remaining work

[Requirements](../../plan.html) · [Current architecture](../foundation.md) · [Qualification](../verification/README.md)

The scoped first practical-use workflow is implemented: create/resume a task, work through its returned path, capture independent results, explicitly publish when requested, then release or discard owned resources.

## Remaining work

- Complete [SCAL qualification](scalable-storage.md) only within an explicitly approved bounded execution scope. Large-input usability, full lifecycle/resource measurements and OS-failure qualification remain incomplete.
- Investigate demonstrated bottlenecks with small reproductions before resuming large operations. Keep durable ACK, native ownership, reader, conflict and same-request recovery protections.
- Qualify new platforms, application adapters, client versions and external-tool workflows when actually needed; compilation and fake tests do not imply native support.

## Unity workflow timing priorities

The original Unity 6000.6.8f1 maze comparison reached playable / clean in 6m45s / 7m03s with the pre-opened shared FIFO Editor and 31m53s / 32m10s with three Splinter workspaces. The [revised agent schedule](../../skills/splinter/SKILL.md#schedule-independent-work-and-finish-ready-workspaces) and script verification workflow reached 19m51s / 20m23s on a matched replay, including interruption and recovery. The five checks below were exercised. The [verification guide](../verification/parallel-workspace-performance.md#november-19-maze-workflow) records the measured scope, slower mounted edits and abnormal Unity shutdowns.

| Priority | Work | First check |
| --- | --- | --- |
| 1. Diagnose readiness before restarting | The controller requested a normal stop during a progressing first refresh. A's initial refresh took 161s, including 81s of compilation. The second launch was requested 6m28s after the first and reached verified readiness 2m26s later. The log shows progress and leaves the first launch's uninterrupted ready time unknown. | Start one workspace on the same warm input. Record compile, assembly reload, initial refresh, Pipeline availability and exact owned readiness separately. Set the phase estimate and stop deadline from comparable evidence before launch. On a diagnostic poll timeout, inspect that same process, its log and any blocking dialog. At the phase stop deadline, request a safe stop and verify group exit before considering a restart. |
| 2. Start Editors during authoring | All three modules were staged by 2m50s. A's first launch was requested at 3m42s and C's at 17m02s. | Prepare the Library once at kickoff and request each needed Editor as soon as its workspace and shared dependencies allow. Compare one and two staggered starts on a small case. Record time to first verified module and time until both are verified, including any disk or shared-helper contention. |
| 3. Publish and release each ready result | The first module passed at 14m47s. Planning the first original update began at 29m07s. Reviewing and applying all three results took 22s. | On two independent edits, checkpoint the first saved and verified result while its peer is working. Measure verification to first applied result. Serialize writes to the original, then release each finished task as authorized. Verify both published changes and the final combined behavior. |
| 4. Locate the cost of normal shutdown | Three sequential final stops spanned 5m29s. In the earlier A stop, the CLI timed out before the owned group was subsequently confirmed empty. | On one task, time the stop request, bridge response, Unity root exit, empty process group, preservation and release receipt. Repeat with a peer and overlap independent work after shared dependencies clear. Use `workspace checkpoint --release` when final preservation and release can proceed together. Keep Library restore after the last dependent exits and releases its reference. |
| 5. Isolate import and compile cost on the mount | Source publication through module verification summed to 300s across the three workspaces and 61s on the shared Editor. The implementations and concurrent activity differed. | Apply the same one-script edit to identically warmed native and mounted projects. Time refresh, Bee compilation, reload and the module check separately, then repeat with a second writer. Use provider read, write and flush measurements to select a runtime change. The [590-node Bee replay and prior lifecycle comparisons](../verification/parallel-workspace-performance.md#isolating-graph-generation) provide existing controls. |

For a subsequent storage-runtime A/B, hold the revised schedule, package versions, warm Library, game requirements and Play-to-clear checks fixed. Validate the controller's Library key, Git byte handling, CLI commands and output decoding on a small fixture before timing. Exclude common tool and package setup. Record the shared Editor's pre-opened state and the separate original Editor launch after Splinter Library restoration. Keep one elapsed clock from kickoff to verified playable and another from kickoff to clean, including interruptions and recovery. Report overlapping spans separately from summed per-task durations. Verify clean source state, retained results, released resources, native Library restoration and an empty shared queue where applicable. Record each Editor's native exit code separately from resource cleanup.

## Agent procedures and exclusions

The [start guide](../agent-start.md) covers setup and explicit completion. The [agent skill](../../skills/splinter/SKILL.md) owns tool selection and same-task handoff. Agents and existing Unity/Git/MCP/CLI tools perform editing, target selection, compilation, Play/Test/build and acceptance.

Dedicated installers, automatic conversation binding, scheduling/automatic mode selection, general lease takeover, shared-original-Editor orchestration, a general Unity API, AI Runner/DAG and daemon/DB are excluded. VSS and ReFS bases are retired. No new compatibility layer or automatic old-state migration is planned.
