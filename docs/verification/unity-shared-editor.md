# Parallel coding with a shared Unity Editor

[한국어](../ko/verification/unity-shared-editor.md)

The later [FU-01](unity-finalization-rerun.md) runs a new paired comparison with committed `07bc2de` and the current Library. The SE-01 observations below are preserved as recorded.

SE-01 · 2026-10-14. Two independent Codex subagent sessions prepare code in editorless Git worktrees; one existing-project Editor validates their results sequentially. This answers a different question from [RC-01](unity-current-comparison.md), where **both** ordinary worktrees and Splinter run two independent Editors.

## Input and method

The source is `b09fa58e9d40a560d4fe1af7a213f8ac23e8e3b1`, Unity 6000.3.22f1, on RC-01's Windows/NTFS machine. Before launching the shared Editor, all 1,602 source files and 42,420 original Library files (3,141,660,252 bytes) were checked against the RC-01 input. Product source and frozen runtime hashes still match `7630970`; current documentation HEAD is `37cfa01`. Therefore the previously completed RC-01 Splinter cohort is reused. **A new paired Splinter trial was not run.** Execution order is not randomized and OS caches are not cleared.

Worker A changes `Increment(3)` from 4 to 5; B changes `Double(3)` from 6 to 9. Each agent edits and commits only its assigned file in its own Git worktree. These are actual independent subagent coding contexts, not new Codex app chats or app-managed worktrees. The benchmark does not measure AI reasoning, feature implementation, chat creation, or app handoff latency.

The original Editor uses its existing Library. Both candidates are available before a common validation gate. One controller applies A, refreshes, verifies the loaded assembly and selected assets, then restores A's source and applies B in **one** refresh. It checks B independently, restores the original source, recompiles the baseline, and exits normally. Combining the A-to-B switch avoids an unnecessary intermediate baseline compilation. This is the external qualification probe already used in RC-01, with no new package, MCP, Unity CLI, or service configuration. It is separate from the historical Unity Agent Coordination Q-package qualification; no Q-package or adapter success is claimed.

The exact shared Editor was PID 51996, process-start ticks `639275472228263809`. Every successful observation matched that identity and the original project path. A/B result checks required fresh readiness, successful matching refresh responses, no compiler errors, changed assembly MVID/generation, values `(5,6,13)` and `(4,9,13)`, and baseline restoration to `(4,6,13)`. Asset GUIDs, mesh/texture/scene data, dependency hash, and the rendered preview hash match RC-01; request-specific preview paths and render durations naturally differ.

## Measured time

| Shared-Editor stage | Seconds |
| --- | ---: |
| Create two code worktrees concurrently | 13.3905 |
| Candidate editing and Git result preservation: union of active worker intervals | 1.4195 |
| Launch original Editor → initial compiled readiness | 40.5774 |
| A transfer, compilation, asset inspection | 15.9528 |
| Switch to B, compilation, asset inspection | 13.7654 |
| Restore original source and compile baseline | 12.0400 |
| Normal Editor exit | 2.5743 |
| Combine preserved A/B source results | 0.4029 |
| Remove owned code worktrees and registrations | 2.3794 |
| **Accounted workflow total, including small dispatch gaps** | **102.5050** |

The Editor launch-through-exit interval is uninterrupted, **84.9127s**. The total adds worktree creation, active candidate edit/preservation intervals (overlap counted once), merge and reclamation. Agent reasoning/dispatch gaps between those operations and read-only input/final checks are excluded. It is not elapsed conversation time. Original restoration and its actual compilation are included; retained result refs preserve both changes independently.

At the common validation gate, A has no material queue wait. B waits **15.9529s** while A occupies the Editor, then uses it for **13.7654s**. Both validations complete after **29.7182s**. This directly demonstrates the single-Editor queue with independent candidate states; it does not include artificial waits or long-running synthetic work.

| Same one-line workload | Shared Editor: SE-01 | Independent Splinter Editors: RC-01 |
| --- | ---: | ---: |
| Editor launch → initial readiness after workspace creation | 40.6s | 310.5s |
| Both candidates ready → compilation and asset checks complete | 29.7s | 32.9s |
| Accounted workflow through preservation, original restoration and cleanup | **102.5s** | **935.2s** |

Splinter has no exclusive shared-Editor lease to wait for, but its parallel validation is **3.2s slower** in this observation. Its full workflow is **832.7s longer**. This small workload favors reuse of one Editor. These single observations do not isolate filesystem, import, cache, scheduling or tool contributions causally.

## What the comparison means

Parallel code writing is available in both workflows. Only Editor-dependent operations can gain from overlapping Editor use. If those operations take `E_A` and `E_B` at unchanged per-task speed, the serial part is approximately `E_A + E_B` versus `max(E_A,E_B)` in parallel. The overlap must outweigh additional preparation, preservation, merge, cleanup and resource contention. Longer AI reasoning or code writing by itself does not meet that condition. No long-operation crossover was measured here; tests/builds may scale differently and may contend for CPU, memory, disk, GPU or external services.

Ordinary Unity worktrees with separate project paths and Libraries already ran independent Editors concurrently in RC-01. Splinter is therefore not a prerequisite for launching two Unity Editors. Its measured storage benefit concerns reducing duplicated source/Library data while keeping independent state. An application-wide singleton or shared external-tool control channel is a separate constraint that isolated storage does not automatically remove.

The [official Codex worktree guidance](https://learn.chatgpt.com/docs/environments/git-worktrees) recommends Local handoff when verification needs the local checkout, including applications limited to one instance. It does not state that every Unity project must share one Editor. It separately describes worktree file/dependency/cache disk costs; the page does not identify storage as the reason for that single-instance wording. Unity's [command-line documentation](https://docs.unity3d.com/6000.3/Documentation/Manual/EditorCommandLineArguments.html) distinguishes a project already open in an Editor from a different project path.

## Preservation and limits

A result `3fc085588117a2d52093a19c868ce959b26e7993` and B result `79ac646af81f70d057b42acb48805bf4c0fdca6b` remain reachable under `refs/splinter-benchmark/20261014-shared/`. The combined result has the same tree as RC-01, `756e0a926577c9bcff036c823b4816b3b2d5fb19`. The original branch/HEAD and all 1,602 source-file bytes are unchanged; baseline compilation and normal Editor exit passed. Both newly created code worktrees and registrations were removed, and existing refs/worktrees were preserved.

The original Library remains an ordinary NTFS directory. The shared Editor normally regenerated its cache; those cache bytes were **not** rolled back. Future exact-input comparisons must capture that current Library rather than assume it still matches the historical RC-01 manifest. There were no package, service, global-setting or administrator changes.

Play Mode, Unity tests, game build, combined-result compilation, new shared-Editor storage/peak RAM measurement, and long-task crossover are **NotRun**. RC-01's 98.64% additional-allocation reduction compares two independent-Editor methods; it is not a measured storage reduction against this one-Editor workflow.

[Dataset](../../reports/benchmarks/unity-shared-editor-20261014.json). The dataset records local raw evidence paths/hashes, exact timing intervals, input/runtime verification, result refs and closure.
