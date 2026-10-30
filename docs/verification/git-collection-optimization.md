# Git collection optimization verification

[한국어](../ko/verification/git-collection-optimization.md)

[Scope and exit criteria](../plans/git-collection-optimization.md), 2026-10-30, source `0432721a1ce1f7042994d5f75146a5e8004a1c5b` over `f91e470` (reader implementation `1e20f49af96e6f64c73b98a3021e525a87e7c486`). Commit preparation verified that all 15 source/test files are byte-identical to the qualified full-gate inputs. **GCO-01–04 are scoped-complete and adopted.** [Structured evidence and source/raw hashes](../../reports/benchmarks/git-collection-optimization-20261030.json) record the fixed comparison and final gate. Historical [GCD-01](git-result-collection-diagnosis.md) retains its measurements of the prior reader. No new Unity qualification is claimed.

## Implementation and protection

`ISnapshotTree.VerifyReadBoundary` defaults to the existing availability check. The Git capture composition supplies `GitSnapshotReadScope` under the exact borrowed reader lease. It fences only the owned loose basis ref and basis receipt, rereads current ref/receipt content during reads, and performs full Git observation at publication boundaries. It also verifies the commit on admission and retains tree/blob/chunk proofs. Packed payloads are supported; packed-only basis refs use the original full-observation path. HEAD/index/branches and shared packed refs are not fenced. The scope is disposed before releasing its borrowed lease, including exception paths. Failed validation poisons the scope.

Full boundary checks precede result-manifest publication, Git result-ref publication, generation receipt publication and Runtime result publication. Missing commit objects at a publication boundary are rejected even when already proved payloads remain in the reader. No whole-project copy, hydration, changed-payload buffering policy or journal/durability change was introduced. No wire schema, task v3 or managed-basis v2 change was needed; fake/default readers keep the full availability behavior.

A fresh `checkpoint --release` performs normal stop and infrastructure removal, then one capture between full seals. Its same-operation sealed result proceeds directly into release with retained history checks and the post-unmount seal. A prior fixed/published request or pending capture cannot borrow a new invocation's seals: retries return their original requested generation and collect current source for closure. Standalone release still captures current source. No cross-invocation source-equality shortcut was added.

## Focused evidence

- First focused set: **145 total / 144 passed / 0 failed / 1 subprocess-only skip**. Includes the new leased-reader cases and existing Git/source/application/publication/recovery behavior. The parent subprocess tests execute; the child entrypoint is not an independent qualification row.
- Additional combined-finalization set: **5 passed / 0 failed / 0 skipped**, overlapping the earlier fresh-finalization case. It injects loss after capture freeze and generation publication, changed full pre/post seals, and changes seen after unmount. Older fixed results remain immutable while newer closure source is preserved.
- New reader coverage exercises loose-ref/receipt fencing and fence release, original and peer-ref writes, packed payloads, packed-only ref deletion, wrong/lost read lease, object loss at publication, poisoned-scope behavior, and refusal before result manifest/receipt publication.

Raw reports are `artifacts/git-collection-optimization-20261030/focused.trx` and `combined-recovery.trx`. Test build passed with zero warnings/errors. Full-gate evidence is added at closeout below.

## Fixed small native CLI comparison

One ordered before/after pair on the same original Git source, **27 basis files / 75,034 B**, seed `2d701823dc6fee4b9a47d5d9a734e1b4a93b749b`, application `none`, `git-snapshot`, Windows/NTFS/WinFsp. No Library or Editor was started. Separate before/after homes contain one A/B pair each, with identical project/key values and fresh task records. The source was explicitly reset by removing only two known experiment-created files after complete owned cleanup; no reset/clean/recursive deletion occurred.

| Normal CLI operation | Before, s | After, s | Saved, s | Reduction |
| --- | ---: | ---: | ---: | ---: |
| First A capture | 37.408 | 4.937 | 32.471 | 86.8% |
| Second A capture | 23.694 | 4.780 | 18.914 | 79.8% |
| Separate A release | 29.114 | 10.070 | 19.043 | 65.4% |
| B `checkpoint --release` | 64.023 | 10.042 | 53.981 | 84.3% |

All four independent result trees match between candidates. A publishes two captured results into original files, preserves a later `last.txt` during separate release and retains three generations in both candidates. B writes while A works and again after A is reclaimed. Combined B preserves the same requested final tree with **2 → 1 generations**. Both pairs reach `released` with absent owned provider/mount/delta and valid historical result graphs. An old capture request still returns sequence 1 after release. Original HEAD/index and the unrelated original edit are preserved; current source returns clean after the separate experiment reset.

The predeclared adoption screen of **10 seconds or 30%** reduction is met. The improvement comes from eliminating per-read Git processes under the scoped guards and sharing a fresh combined operation's capture. A separate release still performs current-source capture.

This is one ordered pair, not a randomized/cold-cache experiment. Git objects and retained result refs remain from before, and identical project/key values yield the same task/result IDs in separate homes. Some after results reuse existing immutable refs and avoid `update-ref`; its individual contribution was not isolated. OS caches, process startup and background provider polling are included. The full 30-second-scale savings must not be assigned solely to one microchange or generalized to Unity/large inputs. The post-measurement capabilities text changed descriptively; collection/finalization code did not change afterward.

## Final gate and limits

Final `pwsh ./scripts/build.ps1` passes locked restore, Release build with **0 warnings/errors**, and **2,065 total / 2,029 passed / 0 failed / 36 conditional skips** in **7m 10s 511ms**. Skips remain opt-in cost/native fixtures and subprocess-only children, not the required scoped guard/recovery parents. All **31 actual response envelopes** validate against the current catalog with **0 invalid / 0 excluded**. Bilingual/source-index and whitespace checks pass. The baseline binary, CLI responses, timing events, returned task identities, result-tree hashes, source-reset receipts and event logs are retained in ignored `.local/git-collection-optimization-20261030/` and `artifacts/git-collection-optimization-20261030/`.

An initial driver preparation attempt had a PowerShell function/executable name collision and was interrupted before task creation; it was corrected and excluded. Product guards remained enabled. Existing RI Unity/Plastic/None qualification remains tied to its recorded source. New live Unity refresh/compile/Play/build, large inputs, Plastic/None native expansion, OS crash/power loss, RAM/storage savings and universal speed ratios were not measured.
