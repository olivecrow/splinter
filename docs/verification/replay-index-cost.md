<a id="replay-index-비용-검증"></a>
# Replay index cost verification

[한국어](../ko/verification/replay-index-cost.md)

[RI-01–04](../replay-profiling.md) for `ri-20260927` closed as **scoped-complete-with-limitations**. Implemented per-operation index measurement/rename-prefix reuse; verified automatic **300 passed/0 failed/1 test excluded at the time skipped**, and synthetic **27 matching outputs/input preservation** with a common inspection binary. Existing VR raw journal was reclaimed; these figures are not actual Unity reconnect improvement.

<a id="후보와-입력"></a>
## Candidate and input

Baseline is `d48b3b067ac90aa94b50a030dd07e4bbe0877de9`. `baseline` uses its Core/Runtime; `profiled` adds only per-operation measurements; `current` is the working-tree candidate adding measurement/rename-prefix reuse. No task repository or Unity Library copy; prepared only 5 DLL/JSON comparison files per bundle. Sequential execution on Windows 10.0.26200 x64/.NET SDK 10.0.112/12 logical CPUs. Optional CPU-model CIM query denied permission, retained as unknown.

All final comparison `Splinter.ReplayProbe.dll` SHA-256 hashes are `D86947A44A5ED7620374CC82E3A146B8B3B0E8765D7B8E5A8963F86F2919E85B`. Final Runtime SHA-256 is `4948624B8F45FB57AA08CF1EF14303C14875D4B19E60658223EE2C1AB91A6B89`; full binary/source manifests preserved. Discovered full build regenerated probe too; prior 27 runs retained as preliminary, then 27 below separately ran with common inspection DLL.

| Input | Content | Frames | Journal logical B | Internal generation seconds |
| --- | --- | ---: | ---: | ---: |
| append | 8,192 sequential 4KiB writes | 8,194 | 36,505,896 | 0.4723 |
| overlap | Initial 2,048×4KiB + 6,144 overlapping 1KiB writes, shrink/extend | 8,196 | 17,619,290 | 0.1779 |
| file-renames | 35,000 empty files, 4,032 renames | 39,033 | 13,844,139 | 0.2430 |

VR file/rename scale informed this input, but it does not reproduce an actual Unity journal. Journal total 67,969,325B; logical input including receipts/identity 73,553,859B, within 256MiB budget. Internal generation time includes journal/expected-content hash preparation, excluding receipt publication/process startup. Fixed data verified against an independent expected model differing from actual file content.

Input journal SHA-256:

- append: `cd5c877b6c554b1e4f4c60028b86a1a5a79a3b24d029ad2d664d4fe77e40cae9`
- overlap: `8e0fece7ff103a20b1ebf990795f791b1b1fdd76546c32eb53a5c76922081591`
- file-renames: `bb90110b9e409afef1c46ed01c1fb2bcb9ec08999b0fc08fb7e7511a928ba9e1`

<a id="반복-시간과-메모리"></a>
## Repeated timing and memory

Per input, run 1 order baseline/profiled/current; run 2 profiled/current/baseline; run 3 current/baseline/profiled. Separate processes each time; no overlap with build/test. Whole hashes read immediately before runs, so warm OS cache, not cleared. 3 samples; JIT/GC/scheduling/power state not fixed.

| Input / implementation | Replay median seconds (min–max) | Index median seconds | Cumulative managed-allocation median B | Process peak working-set median B |
| --- | ---: | ---: | ---: | ---: |
| append / baseline | 1.4001 (0.9843–1.4022) | 1.2930 | 283,832,872 | 63,496,192 |
| append / profiled | 1.3903 (0.9743–1.4032) | 1.2769 | 283,843,608 | 63,569,920 |
| append / current | 1.4056 (0.9721–1.4105) | 1.2935 | 283,909,096 | 63,332,352 |
| overlap / baseline | 1.8948 (1.8861–1.9235) | 1.8109 | 840,737,488 | 65,384,448 |
| overlap / profiled | 1.8691 (1.4977–1.8901) | 1.7824 | 840,726,888 | 65,343,488 |
| overlap / current | 1.9035 (1.8669–2.0378) | 1.8170 | 840,778,000 | 65,679,360 |
| file-renames / baseline | 3.7077 (3.5239–3.7543) | 3.3447 | 9,635,552,136 | 112,164,864 |
| file-renames / profiled | 3.8709 (3.6103–3.9529) | 3.4674 | 9,635,551,424 | 112,119,808 |
| file-renames / current | 2.4062 (2.3920–2.4104) | 2.0162 | 84,087,952 | 108,072,960 |

Rename dominated index application; per-operation median profiled 3.4263s → current 1.9923s. Baseline per-operation counts unknown because absent. Moved creation of identical `old + "/"` string from each path check to once per rename. Existing path/whiteout traversal, `OrdinalIgnoreCase`, and boundary semantics retained. No persistent index/separate disk cache added.

Final synthetic rename replay median decreased about 35.1% vs baseline; cumulative managed allocation about 99.1%. Does not mean simultaneous 9.64GB usage. Concurrent working-set observation about 112.2MB → 108.1MB, not disk savings. Preliminary 27-run rename median was 3.5039s → 2.9043s, so no particular improvement rate generalized. No optimization for the two range-write inputs; final table makes no separate improvement claim for them.

Rename store-creation median baseline 3.7168s → current 2.4156s; CLI median including hash/content/state validation/process start/exit 4.3280s → 2.9609s. Replay values alone are not whole-task preparation time. Actual mount/Editor ready/stop/first import time not measured.

<a id="저장량과-보존"></a>
## Storage and preservation

Same FILE_STANDARD_INFO measurement code restricted to offline root list. No VSS query/snapshot/mount created. No pre-generation whole-storage baseline or instantaneous peak, so no global maximum additional physical storage claimed.

| Measured root | Logical B | File allocation B |
| --- | ---: | ---: |
| Common input, 9 files | 73,553,859 | 73,565,248 |
| baseline execution bundle | 343,638 | 352,600 |
| profiled execution bundle | 346,710 | 356,696 |
| current execution bundle | 346,710 | 356,696 |
| First rejection-test input / corrected rejection-test input | 2,109 / 3,679 | 8,560 / 13,360 |
| Measurement evidence, qualified sample point | 715,704 | 870,696 |
| New probe bin / obj | 601,214 / 329,395 | 618,840 / 353,712 |

Common input file allocation unchanged before/after; in all 27 runs journal hash/expected contents/internal state hash match across all three implementations for each input. Total allocation for 3 execution bundles is 1,065,992B. profiled bundle copy 0.3062s/current 0.2315s; only initial 2-file baseline Core/Runtime copy measured at 0.0099s. Time to add probe to baseline/replace final common inspection DLL unknown, not summed as total preparation time.

Table excludes MFT/directories, OS cache, existing repository build/restore caches, logs outside roots, and measuring-script files. The sample itself and subsequent document checks are also excluded, distinguishing it from final directory totals. Input/binaries/raw evidence preserved without automatic cleanup. No new Unity/VSS resources in this scope.

<a id="판정과-재현"></a>
## Judgment and reproduction

| ID | Judgment | Evidence |
| --- | --- | --- |
| RI-01 | Passed | 9 operation enums/nullable contract, frame/index totals, new-write exclusion, returned-array independence, CLI/schema regression |
| RI-02 | Passed | 3 bounded inputs, independent content/hash verification, separated generation/file allocation/memory, preserved input in 4 rejection paths |
| RI-03 | Passed | Prefix reuse only changed. Boundary siblings/lazy children/deleted child paths/non-ASCII/case-only rename, open IDs/original bytes/replay regression |
| RI-04 | Passed | Final identical inspection DLL/27 repetitions/same state, 300 automatic passes, locked restore/build/document checks |
| Actual Unity reconnect / first render / large PV | NotRun | No actual source journal/Editor execution for RI candidate here. Prior VR-03 Partial retained |

After correcting nullable test code/capability-schema omission in `pwsh ./scripts/build.ps1`, final 300/0/1 and 0 build warnings/errors verified. First receipt reserialization in rejection tests changed time offset to local, blocking at identity first; that input preserved too. Separate input retaining fixed offset verified 4 rejections/no changes: overwrite, wrong identity, incomplete tail, checksum corruption. Product identity/journal contracts unchanged to achieve passes.

Reproduction instructions are [offline replay measurement](../replay-profiling.md). Evidence at `artifacts/storage-foundation/ri-20260927/`:

- `evidence/build-qualified.log`, `tests-qualified.trx`, `source-manifest.json`, `environment.json`.
- `evidence/bundles-qualified.json`, `qualified-run-*.json`, `qualified-matrix.json`, `qualified-summary.json`, `common-harness-qualification.json`.
- `evidence/generate-*.json`, `profiled-*.json`, `guards.json`, `guard-harness-repair.json`, `space-before-matrix.json`, `space-qualified.json`.
- `run-matrix-qualified.ps1`, `run-guards.ps1`, `measure-space.ps1`, and all input/comparison binaries. Preliminary comparison `run-*.json`, `matrix.json`, `summary.json`, `bundles.json` also preserved.

Remaining timing-improvement candidates are repeated range-extent copy/sort costs and actual small Unity reconnect/first-render verification. This implementation is not expanded to default modes/Coordination integration/flush changes/large Unity support.

Subsequent actual small Unity comparison follows [RU execution](unity-replay-comparison.md). Actual Unity NotRun/synthetic results at this RI execution remain unchanged, distinguished from later real-environment observations.
