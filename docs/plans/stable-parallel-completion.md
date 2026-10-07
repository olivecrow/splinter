<a id="안정적인-병렬-작업의-확정-범위"></a>
# Fixed scope of stable parallel work

[한국어](../ko/plans/stable-parallel-completion.md)

This is the next development unit after physical storage PS and memory ME. The question is **whether B queries/same-task resumption/file editing progress independently while A commands run, commands for one task serialize, and real lock-path errors are not mistaken for occupancy**. This does not plan a rerun of existing PV/C/S/Q comparisons or Unity handoff qualification.

**SP-01–04 closed as scoped-complete-with-limitations.** [Direct evidence](../verification/stable-parallel-completion.md) owns defect reproduction/fix, actual overlap/64 writes, independent results/reclamation, and unqualified boundaries. Do not automatically add another comparison or full failure matrix.

<a id="포함과-종료-조건"></a>
## Inclusion and completion criteria

| ID | Inclusion | Completion criteria |
| --- | --- | --- |
| SP-01 | Reproduce/fix actual lock errors | Reproduce the current defect treating missing lock directories as occupancy. Retain existing bounded 5-second waits only for confirmed sharing/lock violations; immediately preserve/reject other I/O/access errors as `StateReadUnavailable`. Keep input/state/response formats and retry classification |
| SP-02 | Common lifecycle/lock regression | Same-key commands create/progress once, reuse the same lock after its owner closes. Canceling a wait does not touch owned handles. B status/same ensure finish while A awaits termination. Preserve existing project/basis/snapshot protection |
| SP-03 | Actual command/file I/O overlap | Create new A/B once on the existing registered small T: original. Write distinct source payloads per workspace; overlap A checkpoint/release with B status/ensure/explicit writes in separate processes. Confirm command start/end intervals, in-progress write hashes, identical id/digest/base/snapshot, B further editing after A reclamation, independent results/final reclamation |
| SP-04 | Protection/scoped closure | Preserve original/Plastic registrations/existing records/results/original Editor identity; absence of owned providers/handles/memory/mounts/deltas/both snapshots. Close the selected current-alpha unit after required build/response schema/documentation UTF-8/link checks |

<a id="고정-입력과-실행"></a>
## Fixed inputs and execution

Original `T:\plastic-fixture-20261002`, home `T:\pt1002`, keys `sp-t-a-20261003`/`sp-t-b-20261003`, `--source plastic --application unity --base snapshot --storage ntfs-snapshot`, budget 17,179,869,184B. Publish new evidence in `artifacts/stable-parallel-completion-20261003/`; edit only returned workspace paths. Fix new source files to 128KiB repeated payload and 32 small progress markers per task. Write no files to the original or peer.

This native boundary is **concurrent CLI/file I/O progression of two mounted editorless product tasks**. Earlier independent-Editor/loaded-code qualifications for current Unity/tools remain in their owning verification; this pass does not inherit them. Do not replace overlapping B writes with mere follow-ups after completed A checkpoint/release; require evidence of execution-interval overlap. Protected snapshot-service writer serialization, project-registration locks, and 5-second occupancy responses on one workspace are normal boundaries. Add no new pass criteria for concurrent speedup or preparation-time SLA.

Explicit checkpoint/release of these two new test tasks is part of implementation verification. Preserve failures/unknown with identical identity and evidence; do not reclaim other tasks/non-owned snapshots or bypass existing C: pending states. Exclude original application, discard, Editor/MCP/Unity CLI/Play/Test/build, installation/registration/service/volume changes, large/ARM/remote/forced interruption/new full matrices. Close after fixing the defect/selected question and regressions; do not automatically add another comparison or harness.
