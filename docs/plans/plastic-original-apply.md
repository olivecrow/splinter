<a id="plastic-결과의-원본-반영"></a>
# Applying Plastic results to the original

[한국어](../ko/plans/plastic-original-apply.md)

<a id="범위와-종료-기준"></a>
## Scope and completion criteria

The user-approved follow-up applies independent `plastic-snapshot` results to existing regular/static originals, either fully or only to explicitly selected paths. Reuse plain-folder conditional NTFS publication, fixed intent/digest, same-request recovery after interruption, and reclamation blocking for incomplete intents. Do not change Git integration or legacy VSS Plastic tasks.

- Verify exact original registration GUID/management files/repository/selector and current lock observations. Do not apply management information or Unity Library; provide no server checkout/add/check-in/update/lock changes.
- Fix selected paths, necessary parents, and Unity `.meta` in the plan. Compare pre-change basis/result/current original and reject third-party changes before writing. The original Editor must have stopped normally.
- Contract/fake tests confirm selection, registration/lock rejection, conflicts, intent recovery, and reclamation blocking; required build passes.
- Real input is changeset 2 of existing test original `splinter-unity-plastic` and one new `poa1005-A/B` run. A preserves and selectively applies `Assets/SplinterApplyQualification1005/Selected.txt`, `Withheld.txt`, and `Assets/Readme.asset` comment changes. B preserves `B.txt` in the same test folder, confirms an owned test conflict in the original, then applies fully. Fix `.meta` and parents together. Do not change server history/registrations.
- Preserve B basis reads and independent results while A applies/reclaims. After checking original application results, compare contents and clean only test-owned files; restore only the approved test changes in Readme. Confirm A release/B discard and exact owned mount/provider/API helper absence.

Completion is limited to the contracts above and observations from one run. Remote lock observations are not server reservations; claim no distributed atomicity between server state and local publication. Do not inherit this pass for lost responses to long MCP writes, actual time overlap of release and peer tests, Play/build/large/OS failure matrices. A new VCS-free basis, partial/dynamic Plastic, and reimport of released results have lower priority.

<a id="진행-상태"></a>
## Progress

The above scope closed with limited qualification. Implementation, actual execution, and candidate-specific limitation evidence are recorded in `docs/verification/plastic-original-apply.md`. Preserve existing C: pending reclamation from non-owned VSS protection and independent results. Do not automatically repeat a new actual run.
