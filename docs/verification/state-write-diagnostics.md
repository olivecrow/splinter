<a id="workspace-상태-쓰기-실패-진단-검증"></a>
# Workspace state-write failure diagnostic verification

[한국어](../ko/verification/state-write-diagnostics.md)

Reference date: 2026-10-01. This diagnostic unit follows [NR-01's fixed scope](../plans/ntfs-recovery-and-support.md#fixed-scope-nr-01-state-write-failure-diagnostics) and the [current workspace contract](../ntfs-workspaces.md). The final candidate after `10eaad7` is **scoped-complete-with-limitations**. Overall NR-01 is **InProgress**; actual VSS/WinFsp/Unity, service/grant/installation, VIVOBOOK, reboot/power/physical-space failures are **NotRun** for this candidate.

`qualified-source.json`, `qualified-candidate.json`, and `final-audit.json` in `artifacts/ntfs-recovery/state-write-diagnostics-20261001/` fix a candidate of 247 source files and 20 managed binaries/JSON files. Source hashes, existing installed-service/WinFsp file hashes, and the sibling fixture's clean state were confirmed unchanged. `qualified-build.log`/`qualified-full.trx` own locked restore, Release build with 0 warnings/errors, and **714 Passed / 0 Failed / 1 Skipped test excluded at that time out of 715**. UTF-8/diff checks also passed.

| Gate | Count | Results and limits |
| --- | --- | --- |
| Common data/response schema | 9 | Conformance of 8 stages/publication states and rejection of contradictions, native-error pairs/Win32 facility, missing diagnostics, and extra path fields. Synthetic responses Passed |
| Actual local file I/O | 5 | Replacement sharing error 32, read-only replacement denial 5, event-append sharing error 32, file blocker at record path, destination-directory preservation. Windows x64 Passed |
| Combined diagnostics for original operation error and failure recording | 1 | Retains fake-source `GitCommandFailed` together with actual locked state-file stage/native error. Resumption/reclamation of the same fake task after unlocking Passed |
| Ordinary CLI/new owned diagnostic Git fixture | 1 | Normal ensure → actual release-intent replacement lock → JSON diagnostics/existing record, Git registration/complete tmp checked → same ensure/release. Original HEAD/index/tracked inputs/tmp preserved; final released Passed |

**All 16 new tests Passed**. Actual file tests with denied replacement retained previous content and complete temporary JSON; a new Save for the same task neither deleted nor automatically adopted failed tmp files. Event-append blocking confirmed both failure response and `statePublication=completed`, where new JSON was already visible. The event log remained unchanged until access recovered, then recording resumed with the same Save. Publication-error responses retain `unknown` for replacement success. Access recovery means closing handles opened by the test, not product attribute-changing bypass or automatic retry. All inputs were new owned temporary directories; existing registered source/home and non-owned snapshots were not used.

Actual internal open/write/flush/close failures, partial event append, and recurrence during VSS preparation are **NotRun**. Passing synthetic stage responses does not qualify those actual failure gates or previous kernel/VSS/Unity candidates. The diagnostic Git test is not NTFS snapshot/Unity validation, and validation-project ledger `ValidationStateWriteIncomplete` is outside this unit.

<a id="보존한-시험-실패"></a>
## Preserved test failures

`first-build.log`/`candidate-source.json` preserve the initial candidate that failed before tests on the xUnit2031 analyzer. It was corrected to use Assert.Single's filter-argument overload. Subsequent `final-build.log`, `failed-full.trx`, `final-source.json`, `failed-test-candidate.json`, and `failed-binaries/` preserve **712 Passed / 1 Failed / 1 Skipped**. The CLI fixture attached an NTFS-only preparation observer to diagnostic Git mode, so failure injection did not operate and returned normal success instead of the expected failure. This is not assessed as resolving a product defect. Subsequent tests used an ordinary release state-file lock and separate fake original-error preservation; product observer, storage/publication, retry/flush policies were unchanged. The final full suite reran with the corrected new candidate.

[NR-02 state-publication failure](preparation-recovery.md#preserved-failures-and-requalification-conditions) and [NR-03 product-test state-publication failure](ntfs-recovery.md#preserved-failures-and-revalidation-conditions) preserve original candidates, responses, and temporary files. Those responses lack lower-level errors/stages, so their causes are **unknown**. New diagnostics do not retroactively establish sharing violation, access denial, or insufficient space for past errors. If the same actual task reproduces the failure, first collect new diagnostics, original request, and stage records; do not change retry/flush policy by guesswork.
