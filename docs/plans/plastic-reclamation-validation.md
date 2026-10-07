<a id="plastic의-별도-볼륨-회수-검증"></a>
# Plastic reclamation verification on a separate volume

[한국어](../ko/plans/plastic-reclamation-validation.md)

On 2026-10-02 the user requested committing current changes and executing the next task. Proceed with the previously raised single A/B reclamation verification on a separate T: fixture within the scope below. Preserve pending tasks and non-owned snapshots from [existing C: execution](../verification/plastic-compatibility.md).

The question is **whether B continues working while A preserves its independent result/releases on a small T: Plastic project with no earlier snapshots, then finishes explicit B discard and final owned-snapshot reclamation**. Exclude full failure matrices/new harnesses/MCP-CLI discovery fixes/original application/check-in. Use existing product CLI from current candidate `4e262b7`.

<a id="고정-입력과-쓰기"></a>
## Fixed inputs and writes

- New fixture `T:\plastic-fixture-20261002`, separate home `T:\pt1002`. Confirmed neither path existed. Do not move the user's original.
- Content-hash verify existing 61 sources/245,487 bytes from the user's C: project; copy only as test inputs. Do not copy `.plastic`, Library, generated solutions/projects, or subsequent original markers. This small fixture preparation is not a product creation/read copy fallback. Actual A/B use identical NTFS/VSS/WinFsp range storage.
- Explicitly use observed selector repository `splinter-unity-plastic/splinter-unity-plastic@20066124403170@cloud` to register new regular/static Plastic workspace `splinter-plastic-reclamation-20261002`. No new server repository/account, checkout/update/check-in, or moving/deleting existing registrations. New fixture registration prepares source; do not register A/B.
- Unity `6000.3.22f1`, Plastic `11.0.16.9915`, collab-proxy `2.12.4`, Pipeline `0.8.0-exp.1`. Prepare Library through the new fixture's owned batch Editor and capture after normal quit. Do not stop the user's C: Editor.
- Read-only administrator pre-inventory confirmed zero T: snapshots, diff-area used/allocated 0, limit 10,737,418,240 bytes, T: free 15,123,443,712 bytes. Do not change VSS limits/volume; retain current space guard immediately before capture. Preserve six C: snapshots.
- New source Plastic GUID `fc4fce46-cb20-4ad8-b474-cdb0000f9866`, source/home identities `22B349E4:0003000000009637` / `22B349E4:00030000000096A1`. Fixed current-candidate service package `artifacts/plastic-reclamation/package-20261002-01`: 14 files/manifest SHA-256 `c4976b440884bc2feaae31883e693632f0c15fb20756da7f93d0304688cb6179`, grant `grants.json` SHA-256 `37c49c8ae3d9fce605f7793aa8f1f5527459b1446bdc417545bc0806f07400bb`, setup SHA-256 `b7b3fcc7946f1e57aa098fdcb483edcef0d3c06faa4dec4b31bf5218f35e9bc6`. Confirm six existing grants + one new registration in read-only `service-update-plan.json`, then Update existing restricted service. Verify protected ledger/existing registrations/non-owned snapshots before/after. Normal cm/CLI/Unity remain unelevated; only installation helper uses UAC.
- A key `plastic-t-a-20261002`, B key `plastic-t-b-20261002`, source plastic/application unity/base snapshot/ntfs-snapshot/default 16 GiB journal budget. Edit only new small probes in returned paths and a new original marker.

<a id="종료-기준"></a>
## Completion criteria

1. Preserve existing registrations/original/C: tasks-snapshots/service grants-protected ledger. Record registration GUID/source-home identities/seed/per-task basis-digest-snapshot.
2. Observe zero initial payload/readCache at A/B creation, isolation, identical requests, exact owned Editors. Separate creation/Library/delta/independent results/file allocation/memory costs from unknown total peak/VSS attribution.
3. Confirm A normal stop/checkpoint/release, B continued writes with same Editor, A independent bytes/hash, and zero mounts/providers/deltas/snapshots/owned resources.
4. Explicitly discard dirty B using exact intent/digest. Do not mark B result preserved; verify final owned-resource/snapshot reclamation and original/peer/non-owned protection.
5. Observe/preserve failures/pending states at identical id/home/intent. Do not force passes through fresh keys, non-owned snapshot deletion, service-permission-volume bypasses, or forced Editor termination. A different input pass does not resolve C: pending states.

Raw evidence is in `artifacts/plastic-reclamation/`; decisions are recorded as separate candidate/input in [Plastic verification](../verification/plastic-compatibility.md#separate-t-input-reclaim-qualification). This single run confirmed A released/B discarded/owned snapshots-resources reclaimed and preservation of six existing C: snapshots/1,418 protected records, closing **scoped-complete-with-limitations**. Preserve C: pending states and unqualified full external CLI/MCP handoff.
