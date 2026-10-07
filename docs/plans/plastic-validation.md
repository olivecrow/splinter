<a id="plastic-실제-ab-실행-계획"></a>
# Actual Plastic A/B execution plan

[한국어](../ko/plans/plastic-validation.md)

Execute only one run of the [compatibility development scope](plastic-compatibility.md). User-provided source is `C:\Users\olive\Desktop\Development\splinter-unity-plastic`, Unity `6000.3.22f1`/Plastic `11.0.16.9915`/regular-static GUID `f3e87fff-e66c-4820-aec7-ebe2a633962f`. Current original Editor belongs to the user; do not stop it. Use Pipeline `0.8.0-exp.1`; add no Coplay/MCP.

| Item | Reviewable execution values/writes |
| --- | --- |
| home/requests | New empty `T:\pc1002`, A `plastic-a-20261002` / B `plastic-b-20261002`; source plastic/application unity/base snapshot/storage ntfs-snapshot/default 16 GiB budget |
| New package | `artifacts/plastic-compatibility/package-20261002-01`, 14 explicit files, manifest SHA-256 `f82dbe455d3667f7b89cd446ef458985a383693b1272d94163cd95b543feffbe` |
| grant | `artifacts/plastic-compatibility/grants.json`, SHA-256 `ef7e14d401e880ee333a483b4c2e9f4a173c5571634b09173ac4f43cc2eedd1f`. Preserve five existing path/volume identities; add one source/home registration |
| source/home identity | `B0C924D3:011900000010DC5F` / `22B349E4:000A000000008A2F`, source volume `\\?\Volume{dcb1e12b-4d05-4c46-8279-c89e02953fc3}\` |
| Administrator writes | One existing restricted-service Update/normal restart. Compare read-only `service-update-plan.json`, exact package/grant/setup hashes, existing protected configuration/grants/ledger hashes and snapshots before/after. Stop/preserve if inputs differ |
| Preservation | Original Editor/existing source-management data/three other Plastic registrations-original registration/Git fixture/five existing grants/ledger/non-owned snapshots-refs-results |
| File writes | New small top-level probes such as `SplinterPlasticProbe.txt` in returned A/B paths. New separate original marker after capture confirms continued editing/isolation. No direct editing of existing Assets/settings/Plastic management regions |
| Costs | Separate zero initial creation/read payload-management metadata/index copies from delta/explicit file results/owned working set/stage time. Instantaneous physical peak/shared VSS workspace attribution unknown |
| Completion | Stable independent A checkpoint/release and B continued bounded file writes/owned Editor. Discard dirty B with explicitly test-owned intent. Verify exact result bytes/hash, mount-provider-delta-owned snapshot/resource reclamation and existing resource preservation |

Prepare/proceed according to the user's request authorizing subsequent work including UAC. Normal CLI/Unity/cm remain unelevated; only administrator helper performs installation Update. Do not change VSS limits/volume/existing registrations-server/original Editor. Preparation observed C: free 282,410,065,920 bytes, T: free 15,125,950,464 bytes; retain current space guard immediately before capture.

Check original save/import state immediately before capture. This is filesystem crash consistency; unsaved Editor memory is outside the basis. Verify failures/lost responses through same key/id/home observations, performing only permitted next actions. No bypass through fresh keys/file deletion/permission-settings changes.

This fixed plan executed on 2026-10-02. Preserving existing grants/protected ledger/original/registrations, observed A/B creation/independent Editors/same requests/independent results/B continuation/explicit discard. **A/B mounts/providers/deltas/owned processes were reclaimed, but snapshot reclamation remained pending due to C: non-owned-snapshot protection**. The [verification record](../verification/plastic-compatibility.md) owns exact task/snapshot IDs/blockers/costs/external CLI discovery failure. Subsequently, at user request, the [separate fixed T: plan](plastic-reclamation-validation.md) closed with limited qualification through final reclamation; retain these C: task/snapshot pending states.
