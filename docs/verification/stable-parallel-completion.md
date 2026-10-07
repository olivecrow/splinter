<a id="안정적인-병렬-작업의-직접-검증"></a>
# Direct verification of stable parallel work

[한국어](../ko/verification/stable-parallel-completion.md)

The [fixed SP-01–04 scope](../plans/stable-parallel-completion.md) is **closed as scoped-complete-with-limitations.** It covers lock-error classification and concurrent CLI/file I/O for Editor-free product A/B on the current Windows x64 NTFS alpha. Existing Unity/external-tool handoff and PV comparison qualifications remain with their candidates/verifications and are not inherited by this pass.

<a id="sp-0102-결함과-공통-회귀"></a>
## SP-01–02: Defects and common regression

Current directory child-index regression in `JournalRenameReplayTests` indexes 300 other folders, then verifies listings and bytes for file replacement, new-file deletion, subdirectory moves, lazy children, ordinary replay, and compaction replay. Lookup cost follows the corresponding folder's child count; the index remains within current limits. Large Unity latency and whole-memory cost are neither measured nor declared passed by these automatic tests. The SP actual A/B below qualifies the candidate at the time.

On basis `1dd9f12`, a test removing the actual lock directory returned `WorkspaceBusy` after waiting 5 seconds. It incorrectly described a path problem as safe occupancy retry. A test with an existing directory at the lock location returned raw `UnauthorizedAccessException`. Both pre-fix failures and three occupancy-test passes are preserved in `artifacts/stable-parallel-completion-20261003/lock-before.trx`.

Runtime applies existing 100ms/5-second waiting only to confirmed sharing/lock violation HRESULTs. Other I/O/access failures are immediately rejected as existing `StateReadUnavailable`/`requires-user-action`. It does not modify files/permissions or bypass through a new lock directory. Verified same-task occupancy, cancellation, owned-handle preservation and reuse after close, independent locks for other keys, and completion of B status/same ensure while A waits for normal stop. Focused tests: 18 passed, 6 new regressions. Existing tests comparing only creation counts for different keys were supplemented with actual completion-order boundaries.

Core/provider APIs, fake contracts, input/output schemas, state v2, and overlay v2/SJR2/candidate v3 remain unchanged. This corrects internal classification of already provided error codes. It does not reduce project registration/apply, basis/shared snapshot locks, or protected service-writer serialization. No conversation-exclusive arbitrary file-edit lock, queue, automatic binding, or lease takeover is provided.

<a id="sp-03-실제-겹침"></a>
## SP-03: Actual overlap

Current Runtime SHA-256 is `b769a1fe899c982416d62f84325021c282313bf7930bad9c1db96b831c9a13ea`. Used existing original `T:\plastic-fixture-20261002`, home `T:\pt1002`, Plastic/Unity/snapshot/NTFS, and 17,179,869,184B journal budget. All CLI calls used ordinary-user tokens. Raw commands/responses, UTC start/end times, and per-write hashes are at `artifacts/stable-parallel-completion-20261003/`.

| Task | key / id | Independent result |
| --- | --- | --- |
| A | `sp-t-a-20261003` / `a7c28dec5242ed3d77bfe043` | `sha256:2a6395c243727eee88b30e21581e5da20415ecaf0524170276156b01f6b560c7`, 63 files / 376,595 logical B |
| B | `sp-t-b-20261003` / `26f2f8a586f5f8d60d376237` | `sha256:9bb3f661fb35c41cb58485989b524107075ac14afc9de4de9d2e28911dcd233a`, 127 files / 377,633 logical B |

Immediately after creation, initial payload/read cache/private payload/sequence were each 0. Different 128KiB source payloads were explicitly written to each returned path; the B writer and A commands ran in separate processes. The same original's basis is 62 files / 245,523 logical B, fingerprint `sha256:8a58a18cac8d41149bafa58e886dcd25b78fd8162ea7cd5b19e9533fef0c9e0b`.

| Interval with A running | A command ms | B writes completed within interval | B status / same ensure ms |
| --- | ---: | ---: | ---: |
| checkpoint | 9,187 | 32 | 2,285 / 3,391 |
| release | 12,714 | 32 | 2,070 / 3,174 |

B observation/resumption completed before each A command ended. All 64 mounted write hashes and B independent result manifest/object hashes were checked. A result contains no B marker. B id/path/key/digest/base/snapshot remained identical across resumption; after A reclaim, B payload was changed again and independently collected. This is one actual timing observation, not parallel speedup/SLA or qualification of every command combination. Editor, MCP/Unity CLI, original integration, discard, and forced termination were not executed here.

<a id="sp-04-보호회수회귀와-한계"></a>
## SP-04: Protection, reclaim, regression, and limits

After reclaim, all baseline/result file payloads in both results were verified by length/hash. Both are `released`; product snapshot observation is `missing`; owned provider/app group processes, handles, private/working set are 0; mount/delta/path are absent. Bytes/hashes of 65 original/management files and 944 previous records/results files, 5 Plastic registrations, and start times of original Unity PIDs 68624, 85184, and 52688 were preserved. No release/discard requests were sent to existing pending C: tasks.

An additional global `Win32_ShadowCopy` list query failed initialization under an ordinary-user token. Failure logs were preserved; permissions, service/registrations, and volume were unchanged. Global list/new live observations of existing pending snapshots are unknown. Absence of the two owned snapshots here is verified observation from their product releases, distinguished from claiming the whole volume/VSS is 0. Historical C: pending, SF Partial/NotRun, and unqualified large/ARM/OS/other-tool boundaries remain.

Completed required `pwsh ./scripts/build.ps1` locked restore/Release build, 906 passed/0 failed/1 skip, and current `ContractCatalog` schema validation of 20 actual JSON responses. Document UTF-8/local links/diff and final source/binary/evidence linkage are preserved in `completion-final.json`. This selected unit is closed; new parallel work opens only with questions and exit criteria for actual progress blockage/preservation defects, new input, or support requests. A few minutes of new Editor preparation cost alone does not automatically initiate a queue/new optimization/full comparison.
