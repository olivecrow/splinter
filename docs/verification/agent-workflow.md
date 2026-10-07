<a id="최소-ai-작업-연결-검증"></a>
# Minimal AI workflow integration verification

[한국어](../ko/verification/agent-workflow.md)

CLI discovery, use of the returned path, resumption of the same task, and explicit termination in the [source skill](../../skills/splinter/SKILL.md) were verified with one task on a registered local fixture. This development unit without an Editor is closed as **scoped-complete-with-limitations**. The current separate Editor qualification follows [representative A/B use](representative-agent-workflow.md) and the M0 tracking table. This does not qualify automatic installation, Codex skill discovery, or conversation binding. The full Unity/VSS failure matrix is not reopened to retest the current instructions.

<a id="고정한-입력과-증거"></a>
## Fixed input and evidence

- Product execution code: `9e10b4a92e2c18c38d14de0fccf2979e26be315c` on `development`. This unit only added or modified the source skill and guidance/plan documents; it did not change product APIs or state/schema.
- Instructions: `skills/splinter/SKILL.md`, SHA-256 `8CA432EC0AB2F133519ED4B851FE33A51846E436ABA6ACBA170FFF395E69B3AF`. The agent in the current conversation read and applied the source. No global/project installation or automatic selection was performed.
- Local original: `T:/ua-source`, clean branch `codex/library-20260929`, HEAD `f71e4e244ba9b085aa0fc439336e4c60ee3d5d5d`, 94 tracked files. The existing warm Library, registered home, and restricted service were used.
- home: `artifacts/storage-authority/ua-20260928/home`. New task key `agent-20261001-190502-766`, ID `25bb092e3f55d5dda58aaccb`, request digest `a1d2e87aad35e16b0d548f19`, VSS ID `{D9EC72CE-81A0-49FD-96E0-E1DCA4A42013}`.
- Raw evidence: `artifacts/agent-workflow/agent-20261001-190502-766/`. Preserves `plan.json`, `before.json`, `decision.json`, discovery/schema responses, `binding.json`, edit/resume/discard responses, monitors for each CLI, and `result.json`. The binding is a test handoff record, not a new product state contract.

<a id="관측한-사용-흐름"></a>
## Observed usage flow

| Step | Actual execution and result |
| --- | --- |
| direct decision | Skill/document authoring did not require independent execution, so work proceeded directly in the existing product checkout. Existing fixture task ID list unchanged; 0 new workspaces/Editors and Codex worktrees |
| Command discovery | Existing `capabilities`, `schema workspace.ensure`, `schema workspace.discard`, and read-only service status all Succeeded. Required CLI usage, input, and retry instructions were found in the responses |
| Task creation and first edit | Used the `data.workspace.path` returned by ensure as the shell working directory and absolute file target. Wrote and flushed 44 bytes to a root test file; the file was absent from the original |
| Same-request resumption by a new CLI | Ran ensure in a new process with the saved project/key/base/storage/home. ID, key, home, root/control, digest, base revision, snapshot, provider PID/start time all matched. First-edit hash retained; source changed paths also reported that file |
| Continued editing after resumption | Added and flushed 34 bytes at the same returned path, for a final 78 bytes. Reading an existing code file also produced the original hash. Created no Editor/validation/checkpoint |
| Explicit termination | discard with the saved ID/intent/digest/home Succeeded on the first request with `discarded`/`complete`. It was not presented as ordinary release or successful validation |
| Reclaim and preservation | Actual root/delta absent, corresponding provider exited, Git registrations/refs restored. The service's exact snapshot observation was missing, reclamation released. Owned processes/memory/handles 0; original 94 hashes, HEAD/branch/clean state, 117 existing record files, and previous task IDs unchanged |

All 7 ordinary CLI executions ran without Windows administrator privileges; 0 additional consent processes were observed. This is the scope verified by 50 ms sampling and a path without automatic elevation. Service installation/updates, grant changes, original edits, Unity, and VIVOBOOK were not included.

<a id="제한과-남은-확인"></a>
## Limitations and remaining checks

Actual agent use in explicit/direct cases and resumption by a new CLI were verified. busy/unknown decisions are skill rules; actual occupancy by another author, a new conversation/compaction, and simultaneous conversation connections were not verified in this execution. The workspace CLI's short lock is not declared to provide conversation-exclusive control of arbitrary file edits. Automatic skill discovery/installation and automatic path binding remain follow-up work and are not promoted to full SW qualification.

The full `Win32_ShadowCopy` list could not be observed: access was denied in the sandbox, and initialization failed in a non-administrator query outside it. Global snapshot inventory and comparison of all non-owned snapshots are **unknown**. No new administrator action or service privilege expansion was performed for this purpose. Only the existing service observations for this task and preservation of Git/the original/existing records are qualified in the table above.

Unity Editor, compile errors/fixes, preservation-based termination/integration, dirty guard, B continuing after A terminates, and storage/time adoption decisions were verified in the separate [representative A/B execution](representative-agent-workflow.md). These are not inherited by this Editor-free record. Native forced termination, actual reboot/power loss/storage exhaustion, ARM, and large input are separate support expansions.

<a id="자동-검증"></a>
## Automatic verification

The skill frontmatter/name/description and unfinished placeholders were checked with `skill-creator/scripts/quick_validate.py`. This is structural validation; actual-use qualification comes from the raw responses/file observations above. Required `scripts/build.ps1` locked restore and Release produced 0 warnings/errors; automatic tests were **745 total / 744 passed / 0 failed / 1 test excluded at the time skipped**. The first sandbox restore failure log was preserved, and the same standard build completed outside the sandbox as an ordinary user. `build.log`, `build-escaped.log`, TRX, and `automated.json` are preserved in the corresponding evidence.

All 58 documents and 10 changed files were checked as UTF-8; local document links in changed files and Git diff whitespace checks passed. No product code/contract change or separate instruction-mimicking test was added.
