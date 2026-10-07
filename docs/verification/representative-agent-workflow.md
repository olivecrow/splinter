<a id="대표-ai-ab-사용과-비용-판단"></a>
# Representative AI A/B usage and cost assessment

[한국어](../ko/verification/representative-agent-workflow.md)

One bundle ran repeated editing, error isolation, same-task resumption, preservation/application/reclamation, and failed-task discard with independent Editors A/B on a registered small Windows x64 Unity fixture. This unit closes as **scoped-complete-with-limitations**. Product code/contracts/installation/grants unchanged. Includes first-checkpoint/refresh failures and same-task retry costs; does not extend to general projects/large inputs/OS-failure support.

<a id="입력과-증거"></a>
## Inputs and evidence

- Product candidate: `development` `4c7bd6c47f1001827a16f4884b9901108ce71c72`, same execution code as previous `9e10b4a`, only minimal AI skill/guidance added.
- Original: `T:/ua-source`, clean `codex/library-20260929`, HEAD `f71e4e244ba9b085aa0fc439336e4c60ee3d5d5d`, 94 tracked files. Existing warm Library observed file allocation 1,871,929,368 bytes.
- Existing registered home `.local/wl29`; existing restricted VSS service/Unity `6000.3.22f1`/Coplay `10.2.0`. New integration fixture branch `codex/m0-20261001-195330-423`, no replacement of existing branch/ref.
- A: key `m0-20261001-195330-423-a`, ID `1f96f4d47b8cb65bfe9854d5`, snapshot `{16D3B086-973A-4FD5-AD79-576752633DA2}`. B: same prefix `-b`, ID `79ad32c9178fca5938210399`, snapshot `{CCE9951F-5B2C-4DDF-902B-60A8D3A8EAB3}`.
- Raw evidence `artifacts/m0-workflow/m0-20261001-195330-423/`: `plan.json`, `before/after.json`, A/B bindings storing returned paths/identities, per-CLI JSON/stderr/monitors, actual loaded code, journal-overlap frames, stage storage measurements, `result.json`, `build.log`/`automated.trx`. Binding/HTTP-session files are local test material, not new product contracts.

<a id="실제-사용-흐름"></a>
## Actual usage flow

| Stage | Checked behavior |
| --- | --- |
| A/B creation/Editor usage | Work at returned absolute paths. Original NTFS location retained; both ensure `initialPayloadCopiedBytes=0`. Each Git-pointer payload 61 bytes/read cache 0. No separate Codex worktree/Coordination |
| Error isolation | A edited to `M0MissingA`, current-source `CS0103` validation failure confirmed. B separate Editor current validation/actual loaded assembly confirms `Calculate(3)=213` |
| A correction/same-task resumption | A corrected/validated 113. New ensure retains key/id/home/path/digest/basis/snapshot/provider PID/start/Editor. B continues/validates 214 |
| Independent-result preservation | After normal A Editor stop, first checkpoint rejects journal change. Same A retry collects `stable-journal-v1` independent manifest/ref. Result `5d54cbeededd50a29e547ee710041cd3963c4826` |
| Original-integration guard | Owned marker dirties original; integrate returns `GitDirtyWorktree`. Original HEAD/index/marker hash/independent result ref unchanged; remove only marker. Integrate into clean new test branch, revision `119d69ede8199cc23d07d001b82c1109da33ec7a` |
| B continues during A shutdown | B edited to 215 overlapping preserving A release. Same ID/provider/Editor PID 30936, new MVID `5c6c7338-5935-4dcd-976c-a0d21e39edfa`, actual 215/current successful validation. A release Succeeded |
| Failed-task discard | B edited to `M0MissingB`, current `CS0103` failure/no result revision checked. Discard with exact intent/digest/id/home including normal Editor stop Succeeded, `discarded`/`complete` |
| Reclamation/original preservation | Both owned Editors/providers stop; actual mounts/Git registrations/deltas absent; exact snapshots service-observed `missing`/`released`; owned processes/memory/handles 0. Returning original branch retains 94 hashes/HEAD/clean state/68 existing record hashes/153 refs. Only A result branch/ref/new integration branch added; B changes absent from original |

Verification reads `sourceVerification=verified`, `validationCurrent=true`, successful/failed validation, and actual-project/PID loaded assembly together. Stale initial `snapshot-probe.json`/pre-error assembly values do not determine success. Integrated trial branch source/ref inclusion checked; no separate original-Editor game-behavior validation after integration.

All 38 ordinary CLI calls were non-administrator on Windows; 50ms consent-process observations 0. User-started HTTP MCP targeted another original `C:/Users/olive/Desktop/Development/splinter-unity`, PID 76076/instance `splinter-unity@983ac2308e6c044d`. After A/B shutdown, new read-only HTTP session confirmed same project/instance/ready. Previous HTTP session returned `Session not found`, so queried using new session; not guaranteed continuous-session lifetime. No original-MCP editing/refresh/shutdown requests/additional Unity CLI installation.

<a id="실패와-재시도의-경계"></a>
## Failure and retry boundaries

Even after owned Editor shutdown/resources 0, first A checkpoint seal changed from 189,968,241 to 189,977,313 bytes, returning `DeltaChangedAfterCollection`. Extra 9,072 bytes frame checksums verified; sequence 41188/41189 were respective 4,096-byte writes to `Library/ArtifactDB-lock`/`Library/SourceAssetDB-lock`. Delayed Library-lock writes after Unity exit observed; broader OS internal causes not established.

Failure preserved independent Git revision/delta/snapshot and rejected manifest publication/reclamation. After confirming no Editor/open handles 0/stable same-journal sequence/length, explicit checkpoint retry with **same ID/home** succeeded. Release recollects after management-file removal/checks post-unmount seal. First collection immediately after normal shutdown is not qualified as always succeeding; seal/flush protection unchanged/no automatic retry added.

B final-error refresh rejected once with `EditorFocusRequired`; same-command retry produced current failure response. Editor log also records bridge/fixture-observation-file `File.Replace` sharing errors. Subsequent current verification/shutdown/reclamation succeeded, but past publication-error causes/full elimination unclaimed. Finished without forced-process termination/new-key bypass/global-setting or service-permission expansion.

<a id="같은-실행의-비용"></a>
## Costs of the same run

Times calculated from monitor CLI start/end and validation `completedAt`. Editor readiness is an **observed upper bound** through first ready query, including polling/agent waiting/analysis. Full run 36 minutes 57 seconds includes waiting/measurement/failure investigation/retries, not sum of product-processing times/SLA.

| Item | Observation |
| --- | --- |
| New workspace ensure | A 42.41 seconds, B 40.94 seconds |
| Editor start response / first-ready observed upper bound | A 7.90 seconds / about 223 seconds, B 10.86 seconds / about 176 seconds |
| Same A ensure resumption | 11.19 seconds; existing Editor/provider/snapshot retained |
| Successful refresh request start → validation completion | A correction 32.56 seconds; B 213/214/215 respectively 38.80/39.02/54.64 seconds |
| A checkpoint | Initial rejection 85.51 seconds, same-task retry 41.77 seconds. Investigation/stability-observation waiting separate |
| A integrate / release | Dirty rejection 13.01 seconds, clean integration 20.10 seconds, release 95.84 seconds |
| B error refresh / discard | Focus rejection 8.86 seconds, retry start → failed validation completion 14.25 seconds, discard 37.80 seconds |

Initial baseline subtracted from complete `measure-workspace-storage.ps1` file-allocation observations. Control/delta, runner, Git common, existing validation roots summed once; original Library/test evidence excluded. Library private writes already in delta, not double-counted.

| Stage | Additional file allocation of observed product roots |
| --- | --- |
| Both Editors/first correction | 336,118,296 bytes (about 320.55 MiB) |
| A/B current validation/near A shutdown start | 351,355,544 bytes (about 335.08 MiB), largest among stage samples |
| Only B after A release | 173,370,184 bytes (about 165.34 MiB) |
| After A release/B discard/original return | 1,850,672 bytes (about 1.77 MiB), includes preserved logs/records/runner cache/Git result objects |

A/B logical payload immediately before shutdown respectively 171,028,633/159,550,568 bytes; raw journals 189,980,670/176,382,435 bytes. Actual delta logical amounts differ from file allocation/VSS physical space. First-ready owned-process-group working sets A about 3.03 GiB/B about 4.27 GiB; private bytes about 3.61/5.39 GiB. Sum is not total system physical memory/peak.

Original Library allocation identical start/end. Global VSS CIM measurement **unknown** due to initialization failure; per-workspace shared-VSS attribution/total physical peak/total savings unreported. File-allocation samples exclude MFT/directories/OS cache and may miss peaks between shutdowns. Logs/test evidence separate costs; `storage-ended` evidence-file allocation 758,776 bytes differs from final evidence size after document/automated-test evidence copies.

Selected small warm input supports two independent Editors/explicit shutdown without initial full Library copying. However preparation takes minutes, both Editors substantial memory, and result collection needs retry. **Adopt only for ongoing work needing independent execution**; short edits retain direct route. No automatic default Editor-mode selection/achievement of large-space goals/general performance improvement. No new comparison group/repeated benchmarks for this cost assessment.

<a id="자동-회귀와-다음-경계"></a>
## Automated regression and next boundary

Required `pwsh ./scripts/build.ps1` locked restore/Release had 0 warnings/errors, **745 total / 744 passed / 0 failed / 1 test excluded at that time skipped**. No current code changes; existing seal/recollection regressions detecting delayed writes retained, no new harness/contracts/simulation tests.

M0 representative usage/cost unit closes. Normal usage does not qualify native forced-interruption passes. Current recovery regression/existing source-comparison/evidence-reuse boundaries/bounded-version closeout follow separate [delivery assessment](m0-delivery.md); entry/support-input/service prerequisites/next actions follow [start guide](../agent-start.md). No automatic new failure matrix/general installation/automatic binding/VIVOBOOK/large PV.
