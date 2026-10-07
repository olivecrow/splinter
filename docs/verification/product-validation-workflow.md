<a id="제품-validation-workflow-검증"></a>
# Product validation-workflow verification

[한국어](../ko/verification/product-validation-workflow.md)

[Product CLI/actual provider](../view-workflow.md) WF-01–05 assessed **selected scope complete with limits retained (`scoped-complete-with-limitations`)**. Candidates published from independent VSS workers A/B validated A/B/A on same Unity view/cache; candidate/source-generation/worker results preserved as independent Git objects, then owned resources reclaimed. Small fixture includes interrupted/recovered first cold import, not overall SF-03/large-capacity goals/uninterrupted cold stability.

Raw evidence [`artifacts/storage-foundation/wf-20260928/`](../../artifacts/storage-foundation/wf-20260928/); final code is work committed with this document after baseline `c1e3cc4`. File/executable/result hashes in [verification manifest](../../artifacts/storage-foundation/wf-20260928/verification-manifest.json), full automated tests in [final TRX](../../artifacts/test-results/wf-product-20260928.trx). Pre-fix failures/final runs not mixed to inherit prior success.

<a id="입력과-실제-실행-경계"></a>
## Inputs and actual execution boundaries

- Windows x64 Unity `6000.3.22f1`, small URP fixture `V:/sf-compaction-20260926/unity` on existing isolated NTFS VHD. Original HEAD `58f81b5bc6ff8a940d8a90872bd3fed858a54cc1`, 90 tracked files/29 protected refs/no initial original Library.
- Home/independent-result bundle on C:. Product `workspace ensure`/`validation` commands, no Codex worktree/Unity Agent Coordination added to S route.
- User-approved reopened administrator helper only capture/observe/release of two fixed workers/detach VHD. Ordinary CLI/Git/Unity non-administrator. Thirteen helper requests/shutdown in `session-*.json`/`response-*.json`.
- A worker `34e1837b50676e1afe7269b9` snapshot `{080F1F21-9E08-4630-BE98-7B1F65D9C7C5}`; B `8eed731485f92952225b7636` `{C79E3EDC-9DE0-4F67-A92F-EE688496B221}`. Not one snapshot presented as A/B.
- Retained view `unity-view`, cache `7fafcaf0d0eab504c3e762b7`, mount `home/validation-mounts/db2fbef9a6296ae0e1c17abf`. Three selections distinct source generations. Current probe/render helper only in each validation source; original/immutable candidates unchanged (`instrumentation-*.json`).

Canonical fixture short-PNG-path fix is `splinter-unity` `development` commit `6a54974`. Matches render file compiled in actual generations; no canonical Editor run.

<a id="실제-기능-판정"></a>
## Actual functionality assessment

| Gate | Observation/evidence |
| --- | --- |
| WF-01 publication | Exact Git registration verified/source-only A/B candidates published separating only `.git`. A journal 8,410 bytes. After publication A worker changed to 24 while candidate stays 23; same-request retransmission returns original receipt (`candidate-*.json`, `publish-a-lost-response-replay-1.json`, `live-isolation.json`). |
| WF-02 switching | Final A1/B/A2 same mount/cache/normal stop-unmount-restart, loaded code/assets/per-generation generated files checked. A2 lacks source outputs from A1/B (`*-started.json`, `ready-*.json`, `qualified-*.json`). |
| WF-03 evidence | Actual refresh/stop records success against candidate seal/source generation/output seal. User source change after B validation rejected `ValidationSourceChanged`; same pending observe succeeds only after restart/new validation (`observe-b-changed.json`, `observe-b-recovered-1.json`). |
| WF-04 preservation/reclamation | A release with dependent candidate/source returns `SnapshotInUse`. Replacing active B-view proof via worker reconnect returns `SnapshotReadersActive`, existing proof hash matches/mount retained/I/O errors 0. Eight independent results/cache-worker-VSS reclamation/object-content reverification after VHD detach completed. |
| WF-05 recovery | Separate publish CLI PID 56460 actually terminated after durable workflow intent/before response. Same owner/request recovers candidate, retransmission confirms nonduplicate receipt (`interrupted-publish-process.json`, `interrupted-publish-ledger.json`, `recover-a-interrupted-1.json`, `replay-a-interrupted-1.json`). |

Actual owned sentinel write in original confirms continued writability/absence in workers-candidates. Only sentinel cleaned; all original 90 SHA-256s/HEAD/29 protected refs/clean Git status retained (`source-before.json`, `source-after.json`). Result refs added to original Git object store; checkout/index not replaced with results.

<a id="최종-코드에셋png"></a>
### Final code, assets, and PNGs

| Selection | Editor PID | `Calculate(3)` | texture max | CandidateA file | Foreground pixels in first valid pass |
| --- | ---: | ---: | ---: | --- | ---: |
| A1 (`a1-stable`) | 52096 | 23 | 1024 | Present | `[9430]` |
| B (`b-final`) | 66692 | 33 | 2048 | Absent | `[9457]` |
| A2 (`a2`) | 53252 | 23 | 1024 | Present | `[9430]` |

All confirm 3 meshes/3,001 vertices/2,441 triangles/0 missing meshes/scene restoration. Request-ID PNG paths/SHA-256/640×480/per-pass pixels rechecked; images opened/model confirmed. First pass within up to 2 attempts valid in all. A1/A2 SHA-256 `5138d38f72aaa13739b4fb0f302b262db6d4338ce4dde9d5854a591e699e72a2`, B `df94cefbc5f0d2fa6b46b878badb8848bc0221d66130778347349725a47b632d`. `png-verified-*.json`/`preview-*.png` retained.

A1 retains diagnosis-created `Validationa1*.txt` in same generation; B `Validationb.txt`/`Validationbfinal.txt`; A2 only `Validationa2.txt`. Retry-generated files not deleted to fabricate clean first runs. Worker A follow-up 24 not promoted to candidate-A validation success; `preserve-worker` response `validationClaimed=false`.

<a id="비용과-저장량"></a>
## Costs and storage

| Measurement | Observation | Interpretation |
| --- | --- | --- |
| A/B ensure | 14.28 seconds each | Product CLI time after already-approved helper receipts |
| A/B publish | 2.62 / 2.55 seconds | Small source-only candidates, separate from Editor preparation |
| Final A1/B/A2 process start→observed ready | 57.58 / 47.26 / 75.38 seconds | Recovered/warm cache/few samples; A1/B diagnosis restarts included, not new-cost comparison |
| Normal stop | 8.68 / 8.62 / 9.15 seconds | Explicit final-generation shutdown |
| Independent source preservation | 7.64 / 6.10 / 6.00 seconds | Separate candidate/worker preservation times |
| View-related file allocation | A1 2,339,465,032 → B 2,494,604,320 → A2 2,639,363,600 bytes | Cache/generation-source/control total, not logical payload/working set |
| Post-reclamation view-related allocation | 882,704 bytes | Retained metadata such as receipts/logs; active source/cache journals 0 |

`storage-*-stopped.json` distinguishes six roots; common Git/original Library separate. A2 view observation about 2.46 GiB, maximum at observation, not instantaneous peak of all temporary files. `stranded-a1-files` preserved outside mount during interruption/independent bundles-repositories/raw artifacts excluded from home active-storage total. No total additional physical-space-goal achievement claim.

Ordinary VSS measurements unknown/not workspace-attributed. Separate administrator probe: V: shared VSS intermediate used 1,277,952/allocated 33,980,416 bytes, final both 0. C: VSS storage/snapshots not allocated/summed here. No NTFS original relocation/initial full-data copying/read hydration/full-file copy-up fallback added.

<a id="실패복구와-남는-제한"></a>
## Failures, recovery, and remaining limits

1. Initial-start mount-parent creation/missing WinFsp-ownership observation corrected. First cold import then hit provider-state `File.Replace` `0x80070497`; Unity exited after unmount. Some test readers lacked delete sharing. Readers corrected to allow it; Windows IPC retries 32/33/1175 errors for up to 1 second only with **same flushed temporary file**. No delete-first/attribute bypass. No single cause established/new uninterrupted-cold pass.
2. Library/Logs/Packages Unity left in ordinary directory after unmount preserved by moving exact whole mount path to `stranded-a1-files` after processes 0 confirmed. Manifest/interrupted logs retained; same candidate/generation/cache resumed.
3. Original `58f81b5` old preview did not meet current FR contract; current canonical probe only injected into validation source. Long-control-path Mono temporary PNG limit reproduced; fixture final name shortened to `preview-<request SHA>.png`, same-directory temp independent short GUID. Failed responses/previous PNG retained.
4. Disappeared Unity `~UnityDirMonSyncFile~<32 hex>~` counted as ordinary source changes, rejecting normal refresh. Only exact name directly under Assets/ProjectSettings absent from both candidate/current source excluded from sequence. Entire journal/seal retained; ordinary user-file change actually rejected in final B.
5. Initial source-release locked-journal-directory rename rejected on Windows. Changed to unlink exact file with seal-verified journal handle retained, retry through durable reclamation intent/metadata hash. Same release-request resumption/actual reclamation of three sources/three candidates/cache succeeded.
6. Twenty execution responses failed schema because Accepted follow-up command/status required null fields missing. Raw responses retained. Reserialization of 57 actual results by final formatter, 15 status follow-up payloads/50 actual inputs all passed (`schema-audit-final.json`). **Serialization reverification**, not VSS/Unity rerun. Actual status after VHD detach with final binary also conforms.

Final `pwsh -NoProfile -File scripts/build.ps1`: locked restore/Release **0 warnings/errors**, automated **511 Passed / 0 Failed / 1 Skipped**. Skip records then-conditions of now-retired backend. Distinguish final A/B/A before serializer fix, actual subsequent reclamation fixes, final serializer automated/record reverification. No new cold repeats/all command-boundary interruptions/actual reboot-power-physical-space failures/general assets-large projects-ARM A/B in these passes.

<a id="독립-결과와-회수"></a>
## Independent results and reclamation

Eight refs in `independent-results.json` preserved in C: `independent-results.bundle`/bare repository. Bundle SHA-256 `521abd4e6b25b75324b0de2840fe27e429b2f35f68a064a0ca248feb66d992c3`. After VHD detach, checked each code/texture/CandidateA/per-generation output without missing commits-trees-blobs and commit SHA-256s of six validation results.

| Result | Ref suffix ID | Verified code |
| --- | --- | ---: |
| source A1 | `23b692554774a2717d752ee4` | 23 |
| source B | `003f01e68336b69cb2f1a53d` | 33 |
| source A2 | `654741e9950e2174ecdf6ad9` | 23 |
| candidate A | `3e66cf70e3b4776c1b08dfa2` | 23 |
| candidate B | `7f69a4baf776ed9fda80e9c9` | 33 |
| candidate A follow-up | `d98090f6834345971d32039d` | 24 |
| worker A | `34e1837b50676e1afe7269b9` | 24 |
| worker B | `8eed731485f92952225b7636` | 33 |

Six results prefix `refs/splinter/validation-results/`; two workers `refs/splinter/snapshot-results/`. Final evidence `closeout-after-detach.json`, `response-final-space.json`, `final-environment-confirmed.json`. Owned application/provider processes-memory-handles-mounts-Git registrations/source-cache-worker deltas/V: snapshots all 0/absent; one original-repository worktree registration retained. Existing C: `{78AD7813-DF73-4845-8513-F14CC6E87331}`/other C: snapshot created externally during testing retained. Test VHD detached, not deleted; administrator helper ended.

Product default-mode selection/general lease takeover/generated-source return merging/FIFO-Coordination/installation-binding-discard-privilege service remain in [separate plan](../plans/storage-priority-and-editor-modes.md). Product CLI/VSS/Unity routes connected here are not reclassified as unimplemented follow-ups.
