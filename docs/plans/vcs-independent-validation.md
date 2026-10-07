<a id="일반-폴더의-실제-ab-실행-계획"></a>
# Actual plain-folder A/B execution plan

[한국어](../ko/plans/vcs-independent-validation.md)

This execution plan fixes actual gates for the [implementation scope](implementation-plan.md#closed-preceding-development-unit-vcs-free-basic-lifecycle). Executed with user administrator Apply/A/B approval; closed **scoped-complete-with-limitations** in the [verification record](../verification/vcs-independent-workspaces.md). After actual-path defect fixes, locked restore/Release had 794 of 795 tests passed/1 then-excluded test Skipped. The verification record owns candidate/failures/same-request recovery for one run; do not expand to full failure/installation qualification.

<a id="고정-입력과-변경할-머신-구성"></a>
## Fixed inputs and machine configuration changes

| Item | Review values |
| --- | --- |
| New source | `T:\vcs-none-20261002` — small four-file Unity input created without changing existing Git fixture. No `.git`/Plastic management data |
| Fixed keys/requests | A `vcs-none-a-20261002`, B `vcs-none-b-20261002`; `source=none`, `application=unity`, `base=snapshot`, `storage=ntfs-snapshot`, journal budget 16 GiB |
| New home | `C:\Users\olive\Desktop\Development\splinter\.local\vcs1002` — new NTFS home outside original |
| Unity/tools | Installed `6000.3.22f1`, Unity CLI `1.0.0-beta.5`, fixture Pipeline `0.7.0-exp.1`. Discover needed external commands through installed CLI help/schema; use exact project path in every execution |
| Service | Update/normal restart of existing `SplinterStorageAuthority`. Normal workspace/Unity/Git remain unelevated |
| Packages | First run `artifacts/vcs-independent/package-20261002-01` — manifest SHA-256 `82eee942d8732f57afd8ee419dd283ee32289f573ff64783ce102bd87074f21d`. After reclamation-path fix, `package-20261002-02` — `57a91e572c7be16e61a3a6f6f2901bfc9c8e3d18c6297ffd26f3f88edfcd2571`. Fourteen explicit files each; preserve both plans/Apply/failure evidence |
| grant | `artifacts/vcs-independent/grants.json` — retain existing three, add one source/home. Read/compare current directory/volume IDs for four scopes |
| Read-only plan | `artifacts/vcs-independent/service-update-plan.json` — `Action=Update`, `Apply=false` validation succeeded. Installation path `%ProgramFiles%/Splinter/StorageAuthority`, existing protected ledger `%ProgramData%/SplinterStorageAuthority` preserved |
| Preserve | Existing Editor/import-worker/service observations in `protected-processes.json`. Do not use original `splinter-unity` Editor/files, existing `T:\ua-source`, or existing workspaces/results/snapshots as test targets or terminate/delete them |
| Space observations | At preparation C: free 284,184,903,680 bytes, T: free 15,174,967,296 bytes. Recheck current state/volume/VSS headroom immediately before actual capture. Keep VSS attribution/whole physical peak unknown |

Administrator Apply rechecks current protected service configuration/ledger and new package. Preserve/reject existing configuration if extra unknown grants or changed identities appear. Do not migrate existing v1 state into new v2 workspaces. Do not change installation/grants/volume settings as normal-CLI failure fallback.

The approved administrator task is the update below. Use default installation-tool checks, atomic file publication, existing grant/protected-state preservation, and SCM restart. Preserve separate packages/failure evidence; claim no automatic installed-file backups.

```powershell
pwsh -NoProfile -File ./scripts/setup-storage-service.ps1 -Action Update -PackagePath ./artifacts/vcs-independent/package-20261002-01 -GrantFile ./artifacts/vcs-independent/grants.json -Apply
```

<a id="한-묶음의-실행과-종료"></a>
## One run and completion

1. After service application, check status/unelevated CLI/new grant. Prepare initial import through an owned Unity process only for the new fixture; stop normally. Fix source manifest/private-generated boundaries before capture. Seed hashes in `fixture-seed.json` are distinct from post-import basis.
2. Create A/B with `--source none --application unity --base snapshot --storage ntfs-snapshot`, distinct fixed keys. Preserve returned project paths/id/digest/snapshot/basis; resume identical requests in new CLIs. Subsequently change original marker and verify immutable A/B bases.
3. Verify each owned Editor by exact project path/PID/start time. Change A source and collect necessary content/compilation observations through existing external tools; B preserves failing source. Do not terminate/reselect peer/original/shared server. Splinter liveness does not mean compile success.
4. Verify A normal stop/checkpoint/stable seal/actual basis-result payload. Check B continues at its own path during A release; after reclamation verify result contents independent of original changes. If snapshot reclamation delays, record `reclaim-pending` and actual remaining resources unchanged.
5. After normal B stop, explicitly discard with intent/digest; verify same-intent retry, reclamation without requiring results, original/A result protection, zero actual mount/delta/owned resources. Final snapshot reclamation follows existing exact-ID/dependency/isolation guards.
6. Distinguish zero initial journal payload copying/hydration/copy-up and range-write/acknowledged-flush observations from logical/actual allocation costs of explicit result preservation. Do not invent zero or workspace attribution for whole physical peak/shared VSS. Update VCS matrix with results/failures/limits.

Verify only this run and changed side-effect boundaries in available environment. Preserve evidence/resources if permission/ownership/corruption/isolation guards block. Do not expand into automatic source-mode changes, fresh-key recapture, non-owned snapshot deletion, new full harnesses/matrices, or large/remote/OS/ARM tests. Plain-folder original apply and Plastic environment compatibility are follow-ups.
