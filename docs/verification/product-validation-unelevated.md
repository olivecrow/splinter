<a id="library-수명주기와-일반-권한-unity-검증"></a>
# Library lifecycle and ordinary-user Unity verification

[한국어](../ko/verification/product-validation-unelevated.md)

**WF-09–12 and LB-01–05 close as scoped-complete-with-limitations** for small Windows x64/NTFS/Unity `6000.3.22f1` input on 2026-09-29. Executed direct existing-Library snapshot reads, single-cache A/B/A reuse, stopped-cache compaction/regrowth, independent-result preservation/final reclaim. Does not mean full SF-02/03, large-project capacity targets, general cold stability/full NR/default-mode selection complete.

<a id="재현-입력과-후보"></a>
## Reproduction input and candidate

- Product candidate applies these changes to `development @ 183118ba1f9071b19f8e725ba7703db07b53c1f6`. Raw evidence at [`library-20260929`](../../artifacts/storage-foundation/library-20260929/). `candidate-manifest.json` fixes executable hashes; `candidate-source.json`/`candidate-code.patch`/`candidate-source-files/` fix code/contract/test changes. Product binaries not replaced during execution.
- Used `T:\ua-source`, `.local\wl29`, existing NTFS VHD/VSS budget. Preserved existing UA `development`/independent-result refs; prepared trial `codex/library-20260929 @ f71e4e244ba9b085aa0fc439336e4c60ee3d5d5d`. Small input origin `58f81b5bc6ff8a940d8a90872bd3fed858a54cc1`, instrumentation sibling-fixture `6a54974`. Trial-input preparation is not product workspace duplication. Sibling repository `development` unchanged.
- Cold original had no Library. After reclaim ran owned Unity once at original path to create Library, then normally exited. Preparation 123.01s, Library logical 1,837,363,213B/file allocation 1,871,929,336B/31,904 files. Source unchanged. Warm reuses this Library. Input hashes for A 96/B 94 files exactly match between cold/warm respectively.
- Service, with user approval, retained two existing registrations and added project/home identity. Registration is a local SID/path/file/volume identity allowlist, not external publication. Installation package manifest SHA-256 `e6698e38ec2659689bdecb8a0ff745055e8b953689c5a36c4d37216e523aeafb`. One administrator configuration distinguished from later ordinary-user execution.

<a id="실제-실행과-판정"></a>
## Actual execution and judgment

| Gate | Result verified for this candidate |
| --- | --- |
| WF-09 | All 187 ordinary CLI runs `callerElevated=false`. Git/Unity/provider use ordinary-user path; only VSS requested from installed SYSTEM service. 68,928 consent observations with 50ms waits show 0 extra UAC. Service-update UAC separate. |
| WF-10 | Actual code/assets/final refresh/independent results in cold A/B/A and warm A/B/A/post-compaction A, 7 source generations total. Same-trial view path/cache identical. Each generation normally shut down/restarted for preparation/validation. 15 Unity starts total including original Library preparation. |
| WF-11 | Actual `prepare-worker`, same-request repeat, new-CLI same-key/ID/snapshot reconnect verified. After publication editing worker A again to 24 leaves immutable A candidate 23/B 33. Worker result `validationClaimed=false` retained. |
| WF-12 | Cold 7/warm 8 results preserved as bundles/separate bare Git. After owned-snapshot reclaim rechecked `git fsck --full --strict`, objects/code/textures/generation output/original model/PNG objects. No alternates; checks do not read original/snapshot. |
| LB-01 | Warm private-cache journal 0B before Editor start. `mounted-cache-b.json` observes direct snapshot reads 736,942,029B/initial copy 0B/overlay I/O failures 0. Range writes/no full-file copy-up supported by actual path plus automatic 16MiB basis-file/4KiB edit tests. |
| LB-02 | Selected different A/B snapshot candidates while fixing first A snapshot as cache basis. Original Library test-file changes after snapshot creation leave four generations reading only fixed old contents. Even after artifact reclaim, surviving cache blocks worker release with `SnapshotInUse`. |
| LB-03 | Compacted cache after stopped-resource/explicit 12GiB expected-overlap budget/content-equivalence checks. Same-request repeats return same-attempt completion receipt. Actual rerun/regrowth verified. Recovery at 8 interruption boundaries/small-budget rejection automatic, not actual power-failure tests. |
| LB-04 | Recorded same-source cold/existing-Library preparation/restart/validation/preservation/reclaim times/additional allocation. No observed full-payload duplication of existing Library at new paths. Original Library preparation/measurement overhead/shared VSS/sampled-maximum limits separated below. |
| LB-05 | Executed source/candidate preservation/reclaim → cache close → worker reclaim. Final owned processes/memory/handles 0; mounts/providers/Git registrations/deltas/owned VSS absent. Three existing non-owned snapshots/UA refs/trial-basis source/index hashes preserved. |

Each A verified code 23/1024 texture/A-only file; B 33/2048 texture/A file absent. Links 3 model meshes/scene restoration/PID/start time/projectPath/domain/MVID/request/sequence/seal. Prior-generation `Validation*.txt` not carried into new generation. All 530 raw-input/response/subsequent-payload schema checks valid.

640×480 PNGs verified request path/hash/per-pass pixels. Cold first A `[0, 9430]` selected second draw; other validation generations first draw valid. Before/after-compaction A PNG SHA-256 identical `5138d38f72aaa13739b4fb0f302b262db6d4338ce4dde9d5854a591e699e72a2`. Unity internal first-draw cause unestablished.

<a id="library-저장량과-정리"></a>
## Library storage and compaction

Journals below are logical lengths of whole managed cache including Library, separate from immutable original Library/shared VSS/source generations/Git results.

| Stopped boundary | Cold cache journal (B) | Existing-Library cache journal (B) |
| --- | ---: | ---: |
| Before Editor start | 0 | 0 |
| First A | 2,282,881,974 | 145,697,693 |
| B | 2,318,250,764 | 181,050,420 |
| A again | 2,352,051,347 | 216,031,269 |
| Immediately after compaction | Not executed | 108,340,700 |
| A rerun after compaction | Not executed | 142,327,043 |

At same A/B/A completion boundary warm journal **90.82% smaller** than cold. Cache-changed-storage comparison for this input. Original Library ~1.87GB not repeatedly counted as free space/per-workspace savings. Original without Library still needs cold import.

| Warm cache | Before compaction | Immediately after | After A rerun |
| --- | ---: | ---: | ---: |
| Current live private payload (B) | 96,787,797 | 96,787,797 | 96,818,730 |
| Replaced payload (B) | 77,261,698 | 0 | 21,751,456 |
| Frame/metadata (B) | 41,981,774 | 11,552,903 | 23,756,857 |
| Actual journal file allocation (B) | 216,035,328 | 108,343,296 | 142,327,808 |

Compaction reduced journal length 49.85%. Before/after Library logical 1,840,074,382B/31,915 files/live private bytes identical. Compaction CLI 11.10s/internal engine 5.17s/same-request replay 1.70s. Simultaneous previous/new journal allocation 324,378,624B. After final A's two Editor runs, journal grew 33,986,343B. No automatic compaction/live Library deletion/immutable-basis payload copying into new journal.

<a id="준비와-반복-비용"></a>
## Preparation and repeated costs

| Trial/generation | First start request→ready (s) | Validation restart request→ready (s) | Final refresh request→success (s) | First start→source result preserved (s) |
| --- | ---: | ---: | ---: | ---: |
| cold A | 1554.54 | 181.59 | 149.37 | 2100.80 |
| cold B | 194.07 | 164.58 | 60.39 | 521.20 |
| cold A return | 93.13 | 71.92 | 24.64 | 277.63 |
| warm A | 226.85 | 62.46 | 24.64 | 406.58 |
| warm B | 92.18 | 65.76 | 25.60 | 281.52 |
| warm A return | 87.04 | 77.66 | 33.33 | 289.56 |
| A after compaction | 85.20 | 64.93 | 29.06 | 272.68 |

Cold first refresh includes initial focus rejection/same-request retry wait. Trial baseline→final reclaim cold 3285.79s/warm 1626.17s. Warm includes extra A/compaction, but original Library preparation 123.01s separate. Few sequential runs, not performance-superiority tests controlling OS/Unity cache/measurement/focus conditions.

<a id="추가-파일-할당과-한계"></a>
## Additional file allocation and limits

| Stopped boundary | Cold home (MiB) | Warm home (MiB) | Warm cache journal (MiB) |
| --- | ---: | ---: | ---: |
| First A | 2188.346 | 151.002 | 138.949 |
| B | 2231.481 | 194.258 | 172.664 |
| A return | 2273.119 | 237.166 | 206.027 |
| Immediately after compaction | Not applicable | 134.492 | 103.324 |
| A after compaction | Not applicable | 176.430 | 135.734 |
| Final reclaim | 2.221 | 3.122 | 0 |

Homes include worker/candidate/source/cache journals/receipts/logs/TEMP/TMP. Warm baseline retains cold preserved metadata 2.221MiB. Final home files are records; active journals 0. Final evidence/independent-result additional allocation cold 20.738MiB/warm 20.233MiB, original Git increase 45.273KiB/20.703KiB respectively. Details in `evidence-summary.json`/`sample-*.json`.

- **Observed maximum of complete samples**, summing C: home increase/trial-artifact increase/VHD backing increase without overlap: cold 2,426,399,008B/warm 319,740,536B. VHD internal Git/Library/VSS not readded. Different initial trial baselines; warm increase excludes original Library preparation.
- Maximum is not instantaneous physical peak. Actual two-journal compaction sum 324,378,624B alone exceeds warm sampled maximum, proving missed total peak during compaction. Whole physical peak remains unknown.
- Of 891 samples total, trial intervals cold 630/warm 206. Files disappearing during publication/reclaim made 2/1 incomplete respectively, excluded from maxima. Maximum sample gaps 6.93s/8.90s. Measurement cumulative wall time cold 133.79s/warm 601.45s may affect execution. Changing PackageCache temporary files in original Library preparation also preserved as separate incomplete samples.
- T: shared VSS maximum at capture/release boundaries used 1,802,240B/allocated 34,127,872B; both 0 finally. Same-boundary C: maximum used 7,490,551,808B/allocated 8,959,721,472B. Volume-shared observations; workspace attribution/between-boundary maxima unknown. Existing C: snapshots not deleted.
- Original NTFS/VHD/prepared original Library/independent results/raw artifacts preserved. No VHD detach/original Library deletion to adjust storage figures.

<a id="실패복구와-자동-검증"></a>
## Failures, recovery, and automatic verification

- Preserved one cold first-A `EditorFocusRequired`. Activated exact PID/start-time/executable-path Editor as ordinary user; same-request retry. Owned-window activation also recorded before later refreshes. Not fully unattended execution.
- Final cold-A Editor log has 2 heartbeat `File.Replace` IOException records. Subsequent heartbeats/final validation normal; independent result/reclaim verified. Cause unresolved. Other final Editor logs lack same bridge error. Preparation logs may overwrite at restart, so not claiming 0 errors for every interval; not inherited as exact historical IPC contention-cause resolution.
- Development original-Library preparation helper failed once before Unity start due to missing deps/runtimeconfig. Verified no running process/Library; preserved failure directory, completed helper file layout. No product-binary change/privileged fallback.
- Two `SnapshotInUse` responses are expected cache-lifetime dependency rejection. Cache still holds basis snapshot after artifact preservation/reclaim; worker reclaim succeeds only after `close-view`.
- `pwsh ./scripts/build.ps1`: locked restore/Release successful, **588 Passed / 0 Failed / 1 Skipped**. First full-run control-file test timeout separately preserved; 9 standalone tests passed. Isolated test group from other parallel flush loads, retaining 4 concurrent readers/500 publications/30-second deadline/product 1-second retry. Final full execution passed again.

All actual forced-termination/response-loss command boundaries, reboot/power/storage failures, general files/assets/ARM A/B are separate NR gates. Library implementation/measurement/reclaim here closed, but full SF-02/03 Partial; SF-04–06/installation/binding/discard retain existing gates.
