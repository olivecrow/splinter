<a id="일반-폴더-원본-반영의-제품-ab-준비"></a>
# Preparing product A/B for plain-folder original application

[한국어](../ko/plans/plain-folder-original-apply-validation.md)

These are the fixed inputs/approvals/stop criteria used in one AP-05 run of the [confirmed scope](plain-folder-original-apply.md). After the user's return, execution including UAC was approved; service Update/new registrations and actual product A/B ran. **The scoped unit closed; do not automatically rerun it.** The [verification record](../verification/plain-folder-original-apply.md) owns candidate-specific execution/costs, first UAC cancellation, state-publication failure, and same-request resumption.

| Item | Fixed candidate and preparation state |
| --- | --- |
| Product candidate | Original-application code/input-output contracts/pending-apply service guard at `development @ fa1efea`. Final code/binary hashes and regression evidence preserved in `artifacts/plain-folder-apply/candidate.json` |
| Original | `T:\plain-apply-20261002`, small VCS-free editorless plain folder. Registration identity `22B349E4:000E000000008A2D`. Original preserved after verification |
| home | `C:\Users\olive\Desktop\Development\splinter\.local\ap1002`, registration identity `B0C924D3:00BD00000008911E`. Both independent results/owned receipts preserved |
| seed | `artifacts/plain-folder-apply/native-seed` and `native-seed-manifest.json`: `data.txt=basis`, `obsolete.txt=delete-me`, `marker.txt=marker-before`. No VCS/Unity/Library. Compare seed hashes before/after actual source transfer |
| Requests | A `ap-editorless-a-20261002`, B `ap-editorless-b-20261002`; `--source none --application none --base snapshot --storage ntfs-snapshot`; journal budget 16MiB |
| Service change | `artifacts/plain-folder-apply/package-20261002-01`, 14 explicit files, manifest SHA-256 `850e65db3dc59e16ea015cd2afa51ff65dbb25a5bc92d70891f29d92584e5877`. Compare/preserve four existing protected registrations/ledger during Update adding one source/home. Check installed hash |
| Read-only status/plan | `service-update-plan.json`: Update/Apply=false/five registrations, exact path identities in `grants.json`. Distinguish from initial four-registration package-only plan |
| Approval | Do not expand prior-fixture approval. Execution including UAC approved after user return; same Update reapproved after first cancellation. Preserve `service-apply-outcome.json` and configuration/ledger evidence |
| Exclusions | Unity/original Editor/MCP/additional full failure harness/volume-VSS budget changes/existing source-workspace-snapshot deletion/remote-ARM tests |

<a id="준비와-승인-경계"></a>
## Preparation and approval boundary

Under normal privileges, build product and write new package/manifest/seed within evidence; prepare only new source/home. Read path identity with `storage-grant`; prepare final grants preserving existing four and default read-only Update plan. Do not run `setup-storage-service.ps1 -Apply`, SCM restart, or elevation during preparation.

Administrator Apply is fixed to the reviewed setup script/package manifest/grant hash after user approval. Reject differences between protected configuration and plan before service stop/copy. Compare existing ledger file hashes and installed package before/after Update; do not remove registrations or clean ledger/snapshots. Subsequent CLI remains normal-user. Do not bypass service/grant rejection through elevated CLI/automatic fallback.

<a id="한-묶음과-종료-조건"></a>
## One run and completion criteria

1. Confirm final candidate/installed package hashes, new grant, and normal-user CLI. Record existing non-owned processes/snapshots, free space, VSS headroom. Preserve unknown and other tasks.
2. Create A/B; resume identical A request in a new CLI. Preserve exact ID/home/request digest/snapshot/basis and both returned paths. Change original `marker.txt` to `marker-after`, confirming continued original editing independent of immutable basis.
3. In A set `data.txt=a-result`, `added.txt=a-created`, delete `obsolete.txt`. Continue separately editing B `data.txt=b-result` and `b-only.txt=b-private`. After A checkpoint, verify change set/zero conflicts/digest from `--target original --plan`; apply with explicit intent `ap-a-apply-20261002`.
4. Verify only exact A contents in original. Retain `marker-after` and B basis/edits. Continue reading/editing B during explicit A release; verify A independent result and actual owned-resource absence. reclaim-pending is incomplete; retry only the identical exact request.
5. After B checkpoint, verify conflicts at identical original paths in read-only plan. Explicit execution must reject as `OriginalApplyConflict` before original writes, retaining A original and B independent result. Explicit B release preserves independent result; verify final owned snapshot/mount/delta/process/handle/memory reclamation.
6. Judge AP-05/06 by separating raw CLI responses/schemas/file hashes/independent results/owned resource observations from journal/result/stage allocation. Keep VSS workspace attribution/whole physical peak unknown. Open no new input/full matrix after this run.

Original/peer damage or acknowledged-write loss on supported paths first blocks that route and requires a fix. Other sharing/permission/ownership/corruption/reclamation-isolation failures preserve raw evidence and tasks; perform only necessary same-request recovery. Do not manufacture success through non-owned snapshot deletion, fresh-key recapture, automatic rollback/sweep.
