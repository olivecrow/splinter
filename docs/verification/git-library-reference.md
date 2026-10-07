<a id="git-원본-library-참조-검증"></a>
# Git original-Library reference verification

[한국어](../ko/verification/git-library-reference.md)

Scope is GL-01–05 in the [plan](../plans/git-library-reference.md); current contract is [original Library reference](../git-library-reference.md). User-approved route moves only Library within the same volume, supplies a managed mount at the original path, and restores it finally. Previous cold Git A/B/VSS/view results do not qualify the new warm route.

Current v2 restore-plan file-identity/creation-receipt protection is checked with project restoration in common `ManagedDirectoryRestoreTests`. If another file occupies the same path after interruption, even identical bytes preserve/reject target/basis/journal. Same-request resumption after normal interruption/readonly/handle protection also tested. GL execution below qualifies its historical candidate; actual Unity/WinFsp restoration/OS failures for this protection change are NotRun.

| ID | Current assessment | Evidence |
| --- | --- | --- |
| GL-01 | Passed, bounded actual Unity | Original 3,148,601,607B moved within same volume; initial copying/read cache 0. Base 145, original 146, A 147, B 148; base hash unchanged |
| GL-02 | Passed, bounded actual Unity | Fixed restore interruption/same-plan resumption, preparation cancellation/preservation tests, small native A/B. Actual Unity B continues during A reclamation/final ordinary NTFS restoration passes |
| GL-03 | Passed with limitations | Exact original/A/B project/PID/start, new initialization values/HTTP reconnect after explicit A/B reload, textures/scenes/concurrent test jobs. B failure's detailed-result loss remains |
| GL-04 | Measured, savings unconfirmed | A/B Private Bytes about 3.08/3.06GB, similar to cold samples. Provider costs/resident-PSS estimates separate; sharing/total peak unknown |
| GL-05 | Passed, bounded actual Unity | A/B independent results/owned reclamation, original ordinary NTFS Library restoration/reopening, MCP idle/clean, existing refs/config retained, required build/UTF-8 |

Raw evidence `artifacts/git-library-reference-20261004/`. Initial tests exposed File API misuse for Windows directory timestamps and missing new capabilities enum, corrected. All 72 related tests passed rerun; success distinguished from initial failure. No HTML generated.

Current candidate required locked restore/Release/full regression: 941 passed, 0 failed, 1 test excluded at that time skipped out of 942. After fixing duplicate prestart-reader reuse, 79 related tests also passed under ordinary-user permissions. Ten sandbox publication/Git rejections preserved in separate logs. Final required build including new schema completed exit 0 under ordinary-user permissions.

Shared MCP-server log inside original Library retained a write handle; first capture failed InputUnavailable without moving original. After user approved 8080-server restart/external logs, continued same preparation request. Only exact empty root from first failure removed nonrecursively; new preparing intent connected same-request recovery. New server same 10.2.0/8080; full command lines/authentication tokens unrecorded.

Library preparation response 536.8466 seconds; initial failure 96.5645 seconds/server-restart costs separate. A ensure 491.1709 seconds, B 518.2890 seconds; B first WorkspaceBusy 5.9601 seconds followed same id/key/home retry. These differ from Editor usable time. Actual HTTP connection observed after original CLI-list STATUS_NO_INSTANCES/eval 5-second timeout; connection requests not unconditionally repeated.

A/B start→first HTTP connection about 860/796 seconds respectively. Includes compilation from new qualification source/bridge/Unity per-path scans; Library reference is not immediate readiness. Earlier separate cold-import ~88 minutes differs in input/observation boundaries, preventing general speed multiplier. Original Library full-read verification/43,131 protected file handles/metadata costs remain.

<a id="실제-mcp와-현재-상태-검사"></a>
## Actual MCP and current-state checks

Unity 6000.3.22f1, Coplay 10.2.0, original development `6a54974a9c655031aeb2be17fb7622fc5d641c52`. A `68ee8723a88580460666be3c` Editor PID/start `61356/639267103839383275`; B `121b2f293ead60d7e18c3de2` `39716/639267109491871584`. Returned paths/PID/start compared on every write/state observation. New qualification code only connects to existing HTTP, does not start shared server.

A imported/saved/reopened red 16×16 PNG/`Qualification_A` x=1; B blue PNG/`Qualification_B` x=2. Original source unwritten. Explicit reload confirmed same path/PID/start/new `LoadedAt`; both MCP sessions changed. Test jobs A `d8380cfb5b2d42c4aab0bd95c3e187fa`, B `8b932ea9952249f2a8b70b6ac6caaa03` passed all 3 marker/texture/scene tests each. Job interval overlap 59ms; per-method CPU concurrency unmeasured. Tests also compare own `Application.dataPath`/fixed `project.txt`.

After original/A/B range writes, base probe byte 145/initial SHA-256 unchanged. `basis-direct-write-fence.json` records Win32 sharing violation 32 when requesting only a write handle on exact owned base probe. Payload writes 0; no direct immutable-basis-write bypass.

Changing only B marker to `WRONG` detected expected B/actual WRONG failure in B job `d59bd996c550456aae9ae125da952369`; A passed 3/3. After reload, B retained `failed`, 3 completed tests/failure message but entire `result` null. MCP `TestJobManager` intentionally omits full Result during SessionState restoration. Detailed 2/3 pass is not inferred. Restoring marker, B job `eaa36df4591a4b3f8c91dcb4356ba1f7` passed 3/3 including full details. Request timeout/disconnection did not cause unconditional write repetition.

<a id="저장량과-ram"></a>
## Storage and RAM

All 8 roots in `storage-post-task-long-paths.json` complete. First sample preserved 206 Win32 long-path lookup failures as unknown; corrected `\\?\` absolute paths then repeated same read-only measurement. A/B control allocation 42,097,824/41,962,912B, delta journals 41,549,824/41,418,752B. About 97.9% smaller than previous cold 1,950,875,648/1,953,628,160B journals, but task interval/generated-data state not fully identical benchmarks. Common basis 3,211,863,192B allocation is moved original data, not new-copy costs. Basis metadata 15,037,368B/original delta 2,949,120B/shared runner 2,867,888B/existing Git 2,392,301,232B/evidence separate. No new VSS/non-owned-snapshot changes. Shared VSS queries unknown due to permissions; total physical peak unknown.

All three samples for exact 6 PID/start identities in `all-idle-memory.json` complete. Median Private Bytes: original 3,137,077,248B, A 3,078,062,080B, B 3,058,962,432B; original/A/B providers 195,493,888/217,513,984/214,241,280B. A/B non-shareable resident 2,470,907,904/2,458,296,320B, estimated PSS 2,501,681,152/2,487,575,698B. Similar to previous cold Editor Private ~3.10/3.05GB; **RAM savings unconfirmed**. File-basis sharing does not share Unity heap/scenes/graphics. ShareCount limit 7/nonatomic samples/kernel cache/inter-Editor shared attribution unconfirmed. Residency differences between long-idle initial/restarted original are not causal savings/regression evidence. Lifetime peak is not instantaneous peak of this interval.

After A exit, separate `B-owned-group-memory.json` opened exact owned B Job with QUERY and sampled 9 Editor/child/supervisor PID/start identities three times; all complete. First-sample Private sum 3,731,165,184B/estimated PSS sum 2,991,255,552B. Library provider outside Job is separate. Time/scope differ from earlier both-alive Editor/provider samples; values do not replace A's concurrent full-child costs/whole-system peak.

Three plan-writing samples in `restore-cli-memory.json`, selecting only final original-restore CLI by exact command: Private 183,074,816B, working set 171,159,552B, estimated PSS about 132,426,021B. Not whole restore interval/peak/Editor RAM replacements. Command lines neither output nor stored.

All three moving-stage samples in `restore-cli-moving-memory.json` complete: Private 205,967,360B, working set 196,886,528B, estimated PSS about 155,632,201B. Fixed restore plan 43,129 files/2,497 directories/final logical 3,150,139,635B; 14 new files/private-range sum 6,483,757B. Range sum differs from final original payload/whole-command cumulative I/O.

<a id="결과-보존과-회수"></a>
## Result preservation and reclamation

A normal stop 159.9423 seconds, checkpoint 346.3494 seconds, release 125.3945 seconds. Independent result `27bb138f76207dd4448ef2ab303dfbd0f2b479ca`/`releasedResources` verified process/working set/private 0 retained. A mount/provider/handles/delta/basis ref absent; only B dependency in common Library. During A checkpoint/release B remained idle/clean at same PID/start; after A reclamation MCP wrote `after-A.txt`, confirmed blue PNG/probe 148/marker B. B checkpoint including normal stop 402.7276 seconds, release 124.7934 seconds; independent result `59917b317b250f609c506e875b27eabc9fa4099a` retained. Original probe removed from managed-original delta before normal original-Editor shutdown; shared 8080 server kept same PID/start.

Final `library restore` completed 888.9812 seconds. Cursor 43,129/43,129, filesVerified=true, restored state; owned base/original delta/dependencies absent. Original Library ordinary NTFS folder/probe absent. `cleanup-verification.json` reads marker/PNG/scenes/B follow-up file from independent A/B refs and confirms differing hashes; Library/Temp/Logs/obj excluded. Original development HEAD/clean/existing 84 refs/worktree list/config SHA-256 `c46a6b92587ba0498859c4fd8e953417edff90f9ae4095398ef1e1d139ce3a61` compared with initial observations. Reopen-helper reserved-variable-name error failed before execution; corrected variable name/same review procedure succeeded. No original/result-overwrite bypass.

`process-cleanup-verification.json`: all 14 sampled shutdown PID/start identities absent, callerElevated=false. Both durable Job releasedResources verified process/working set/private 0. Shared server `73512/639267092568061834` retained; original reopened with same Unity/path as `34200/639267143482280358`. Initial CLI query COMMAND_FAILED before Pipeline readiness; after initial refresh, read-only catalog/actual project/PID/start checks/one connect-only request succeeded. Final HTTP only original; MCP query confirmed compile/updating/failed/playing=false, clean SampleScene, Library reparse=false, absent probe/original qualification source.

All 8 roots in `storage-final.json` complete. A/B control allocation 569,504/582,048B, Library-basis manifest/restore-plan/receipts 42,534,024B, reusable runner 2,867,888B. Task deltas/basis payload absent; metadata/independent Git results/evidence retained. Existing-Git-inclusive allocation 2,391,897,040B/evidence at observation 1,569,832B separate from new task payload. Restored original Library remains at original location, not summed as reclaimed data/new copy.

Fixed single Git/Unity A/B in GL-01–05 closes **scoped-complete-with-limitations**. Unconfirmed RAM savings, preparation/collection/restoration costs, SessionState loss of failure details, unknown total physical/RAM peak/shared attribution remain. No inherited qualification/automatic repetition for Play/build, Plastic/non-Git, general security descriptors, ARM, OS failures, or large additional inputs.
