<a id="plastic-프로젝트-기준본library-참조의-다음-작업-범위"></a>
# Next scope for Plastic project bases and Library references

[한국어](../ko/plans/plastic-project-basis.md)

The scope fixed on 2026-10-04 connects **a project version recorded in Plastic + immutable original Library basis + per-task range journals** and verifies a small actual Unity A/B. This owns VP-03 and Plastic VP-04/05 of the parent [VCS basis transition](project-scoped-basis-and-reclamation-review.md). User-requested implementation/fixed A/B closed scoped-complete-with-limitations on 2026-10-05; [verification](../verification/plastic-project-basis.md) owns current execution decisions/costs/limits. RAM savings are excluded from pass criteria; record only actual workload costs.

<a id="현재-완료-경계"></a>
## Current completed boundaries

| Path | Current implementation/qualification | Relation to this task |
| --- | --- | --- |
| Common/no VCS | Creation-resumption/isolation/result preservation/release-discard and small editorless original application closed with limited qualification. Basis uses existing VSS path | Do not reimplement basic lifecycle. VCS-free project-basis transition has lower priority by user decision |
| Git | Fixed commit reader/range storage/original Library references/actual MCP-resources-scenes-current-state tests/results-reclamation/Library restoration closed with limited qualification | Reuse common contracts/Library implementation. Preserve unconfirmed RAM savings/lost MCP failure details/preparation-restoration costs/unknown total peak from [GL verification](../verification/git-library-reference.md) |
| Plastic | Regular-static registration/management-root isolation/VSS A-B/independent file results/release-discard closed with limited qualification | Changeset reader/Library connection qualification follows PPB. Full-selected original application has separate POA contracts/qualification, distinct from basis transition |

This Plastic path closed with limited qualification. Do not label closure as completing a new VCS-free basis, Plastic original application, or actual Unity RAM savings. Existing C: VSS pending states retain separate [reclamation evidence](../verification/plastic-compatibility.md#authorized-same-request-reclaim-retry-for-historical-c-tasks-2026-10-04).

<a id="포함하는-구현"></a>
## Included implementation

1. **Fixed Plastic basis reader**: fix exact server/repository identity/changeset/project subtree/file revision-size-content identity in receipts/request digests. No latest-branch or live-original file fallback. Server content access remains normal-user; Core contains only common opaque identities/immutable-tree contracts.
2. **Project-scoped overlay integration**: connect separate `plastic-snapshot` selection to CLI/schema/Runtime/Windows provider. No new VSS requests/full checkouts/payload disk hydration/whole-file copy-up in creation/read/resumption/compaction/result preservation/reclamation. Retain existing `ntfs-snapshot` state/results/pending receipts without automatic migration.
3. **Original Library references**: connect existing `library prepare/status/restore`, graft/dependents/read proofs to Plastic. Reuse user-approved Library-only managed mount; source stays at original NTFS location. Original/A/B read the same immutable Library and write separate range journals; after final task reclamation restore original Library to ordinary NTFS.
4. **Preserve isolation/results/reclamation**: block `.plastic` and other management roots in workers; create no new Plastic registrations/server locks/settings-authentication changes. Use a short home on the same NTFS volume, outside inherited/overlapping VCS management/Plastic registrations. Retain independent file results/seals/same-request-intent recovery/original-peer protection/explicit release-discard/zero-resource observation.
5. **Contracts and usage procedures**: update input-output schemas/fake-conformance/capabilities/current features/AI skill/AGENTS/verification records together. Support one current version; preserve/reject corrupt/unknown/unsupported. Retain Git/no-VCS regressions.

<a id="먼저-해결할-읽기-비용과-입력-조건"></a>
## Reader costs and input prerequisites to resolve first

Installed CLI `11.0.16.9915` provides fixed changeset tree/revision lists through `cm help ls` and raw revision stdout through `cm help cat`. Official [list](https://docs.unity.com/en-us/unity-version-control/uvcs-cli/list) and [getfile](https://docs.unity.com/en-us/unity-version-control/uvcs-cli/getfile) document this access. Current `cat` help specifies no offset/length selection, so accessible content alone does not verify efficient range readers.

Before initial implementation, verify sequential/noncontiguous bounded streaming reads/reconnection/hash-length/binary-CRLF semantics/no failed-buffer publication. Observe whether small reads repeatedly transfer full revisions, and client-server cache/temporary-file/process-memory costs. Distinguish zero Splinter payload storage from network transfer/external client caches. If observed bottlenecks block small actual Unity work, fix only that reader. No whole-file memory/disk cache, VSS, mutable-original bypass; no VCS-free fallback on access/server failures.

Plastic changeset 1 in the existing [pre-probe](project-scoped-basis-and-reclamation-review.md#prechecking-fixed-vcs-contents) has 26 files/70,004B and **no recorded Assets**. Before actual implementation execution, freshly verify current registration/changeset/required-source inclusion read-only. Choose an explicit recorded basis including current original Assets/Packages/ProjectSettings/necessary `.meta` and tool packages. Do not infer Library/dirty-private-source equivalence.

If no recorded basis includes required files, exact input preparation is a Unity-verification prerequisite. Present lists/diffs/needed check-in contents and treat as separately approved preparation. No arbitrary check-in/update/original-file omission/full copying to fit the basis. Common fake/reader conformance can proceed without prepared inputs; actual Unity remains NotRun. No new repositories/changesets/homes were created when scope was fixed. Subsequent implementation request and user-approved exact 37-input check-in fixed existing test repository changeset 2. One actual A/B uses owned `C:\splinter-ppb1004` home and `ppb1004-library` key on the same ordinary C: NTFS; excludes new repositories/original-content changes/additional server-history writes. Add existing verified MCP tool package v10.2.0/revision 30d22075093d1d35dfb0091c1c7550e9ad948577 only in each private A/B workspace.

<a id="실행-순서와-종료-기준"></a>
## Execution order and completion criteria

| ID | Selected unit | Completion criteria | Current status |
| --- | --- | --- | --- |
| PPB-01 | Precheck basis/reader | Fix exact project/repository/changeset/revision/required-source manifest; verify raw-content semantics/range-read transfer-cache-memory costs. No content publication on permissions/missing-corrupt/lost responses | Passed with limitations. Entire cs 2 lengths/hashes verified; external cache/network unknown |
| PPB-02 | Common contracts/Plastic overlay lifecycle | schema-fake/owned receipts/same-request resumption/basis retained after original changes/management-root blocking/result-reclamation integration. Zero new VSS/checkout/hydration/copy-up, Git-none regressions pass | Passed. Final required regressions 961 Passed/1 optional skip |
| PPB-03 | Library integration/small native checks | Same immutable Library/individual journals as Git, dependencies/B continuing during A reclamation/final NTFS restoration. Check interruption-same-request recovery at changed side-effect boundaries/original-peer protection | Passed, limited actual Unity. B writes during checkpoint/tests after release/final NTFS restoration. New native forced-interruption matrix unqualified |
| PPB-04 | One actual Unity A/B run | Exact original-A-B path/PID/start, MCP connections/explicit reload rediscovery, independent texture-importer/scene creation-editing/current-state tests/test-job overlap/B follow-up during A completion. Record failures/timeouts/lost results | Passed with limitations. A/B each 3/3, job overlap 9.84 seconds, B peer 3/3. Long MCP write responses/initial preparation costs preserved |
| PPB-05 | Independent results/discard/final cleanup | A checkpoint-release; explicit B discard after checkpoint preserves needed independent results. Verify no owned Editor-children-provider-handles-mount-delta-basis dependencies; restore original Library/reopen original; preserve registrations-original changes-other tasks-shared server | Passed, limited actual Unity. Independent results/owned reclamation/ordinary NTFS restoration-reopening/original-registrations-existing Editors-server preserved |
| PPB-06 | Costs/final decision | Separate initial/resumption/edit/collection/reclamation/Library restore times and task deltas/shared bases/existing data/results/external cache-transfer. Original-A-B Editor/provider Private Bytes/residency/PSS estimates for identical intervals. Required build/UTF-8/decision records | Measurement/recording complete. No RAM savings requirement; total peak/external cache-transfer/shared VSS attribution unknown |

Connect PPB-01/02 first, verify changed Library boundaries through small native runs, then close PPB-04–06 in **one** new fixed-input actual Unity A/B run. Do not rerun existing Git/VSS/Codex controls/full failure matrices. External MCP/Unity CLI tests current A/B files; Splinter adds no compile/game-verification gates. Preserve already approved first/final normal original Editor shutdown and Library-only exception; no automatic dirty-change discard. Verify targets on existing shared MCP server without automatically changing its lifetime.

Judge `scoped-complete-with-limitations` when scoped implementation/regressions and fixed actual A/B results/cleanup/cost evidence exist. Guarantee no RAM savings; do not turn no-savings/unknown into zero. If material evidence is NotRun/failed, retain state/reason and do not declare actual verification complete. On failures preserve identical id/home/basis/intent/evidence, fix necessary defects only; new inputs/repeated expansion require separate questions/completion criteria.

The fixed single-run decision is the scoped closure above. Preserve initial preparation/Library restoration costs, failed long-MCP-write responses/actual completion, unconfirmed overlap of A release/B test-job execution, and unresolved request-delay causes as verification limitations. Do not repeat tests to erase limits or expand qualification.

<a id="이번-단위-뒤에-남는-작업"></a>
## Work remaining after this unit

- **Plastic original application**: [subsequent fixed scope](plastic-original-apply.md) owns full-selected local publication/three-way comparison/management identity-locks/original Editor protection/partial-publication recovery. Do not inherit PPB independent-result qualification as application success. VCS check-in/merge belong to AI/existing tools.
- **New VCS-free project basis**: lower priority by user decision. Retain existing VSS lifecycle/plain-folder original application. Common Library contracts do not establish current `source=none` integration/qualification.
- **Historical VSS reclamation limits**: existing C: pending/non-owned-snapshot protection are separate issues. Zero VSS usage in new Plastic bases does not mean earlier snapshots reclaimed.
- **Additional support/performance**: exclude Gluon/partial/dynamic/mixed-nested VCS/Plastic server-history changes/dedicated installer-service-grant/full OS-ARM-large/Play-build/general RAM-savings research/new HTML reports. Reselect necessary units when actual-use safety defects/blockers or explicit requests arise.
