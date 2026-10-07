<a id="vcs-프로젝트-단위-기준본-전환-계획"></a>
# VCS project-scoped basis transition plan

[한국어](../ko/plans/project-scoped-basis-and-reclamation-review.md)

User-fixed scope: resolve the mismatch of creating volume-wide VSS for work on one project. First reuse project versions already recorded in Git/Plastic; give new VCS-free basis methods lower priority. Costs of outside-project changes or existing snapshot reclamation may motivate this transition but do not replace definition of the first problem.

Preserve [product requirements](../../plan.html): original NTFS location/filesystem, no initial full copies/read hydration/whole-file copy-up fallback, per-task range storage, continuously editable original. Do not delete/automatically migrate existing `source=none` features, earlier VSS tasks/independent results/pending receipts. Existing scoped PS/ME/SP/M0 closure does not qualify new backends.

<a id="최초-전체-복제에-대한-정정"></a>
## Correction about initial full copies

An initial full copy is not a snapshot prerequisite. A copy-on-write basis can reference unchanged existing storage while preserving necessary previous ranges **before** original modification. Copying new files after overwrite or only processing watcher notifications cannot protect already-lost previous bytes. Copying whole files for small original changes is distinct from current range-storage requirements.

Existing commits/changesets already own previous contents on this VCS route, so original-write interception is not an initial-implementation prerequisite. First reference exact immutable VCS objects/revisions. Separately review live-original-read optimizations only when identical-version proof holds during reads; do not rely on mtime/length or mutable-source basis fallback. VCS-free write-before range preservation/kernel filters/initial seed costs have lower priority, not mandatory research blocking this transition.

<a id="이번-구조와-입력-경계"></a>
## Architecture and input boundaries

Workspaces comprise **fixed project version + per-task range journals + separately managed generated caches**. Adapters own Git commit/tree/blob and Plastic changeset/file revision; Core holds only opaque basis identity/immutable-tree contracts.

| Boundary | Implementation target |
| --- | --- |
| Project scope | Fix only project-root/needed-subtree file lists/immutable content IDs. Request no C:-style volume snapshots or capture of other projects |
| Git | Read shared existing local trees/blobs and preserve owned basis references. Connect range journals without full checkout/cloning/read hydration. Branch movement/original edits cannot change fixed bases |
| Plastic | Reference explicit repository/fixed changeset/revisions. Create no new registered checkouts/worker VCS management data; read under normal-user permissions. Separately check server-transfer/CLI-cache/temporary-storage costs and side effects |
| Uncommitted/unregistered source | Do not silently omit needed source absent from recorded bases or inconsistent original contents. Initial scoped paths reject or expose explicit input semantics starting at recorded versions. No automatic commit/check-in/update |
| Generated data | Do not assume VCS-external Unity Library is committed. Separate new private generated ranges/usage costs; no mutable original Library sharing or hidden VSS capture. Warm original Library reuse/cost equivalence need separate new-path qualification |
| Existing lifecycle | Preserve identical project/key/id/home/basis-digest-owner, individual durable flushes, seals, independent results, original-peer-target-dirty guards, explicit reclamation/discard |

Selecting recorded versions differs from point-in-time capture of a whole current folder. Commits/changesets do not preserve ignored/private files. Retain rejection of unsupported Git transforms/CRLF/LFS-filters/submodules-special files and Plastic transformations/partial workspaces; verify new-route qualification separately. Payload storage required for results independent of original/VCS is distinct from creation/read costs.

Current product `ntfs-snapshot` uses VSS. Git-only `git-snapshot` connects the [current contract](../git-project-basis.md), with [original Library references](../git-library-reference.md) and actual Unity/MCP closed with limited qualification. Plastic-only `plastic-snapshot` also connects [current-contract](../plastic-project-basis.md) fixed changeset/blob readers and common Library. [PPB verification](../verification/plastic-project-basis.md) owns current automated/actual qualification. Guarantee no actual RAM savings; user excluded them from Plastic pass criteria. Do not change bases of existing commands or historical VSS state.

<a id="구현-순서와-종료-기준"></a>
## Implementation order and completion criteria

2026-10-04 subsequent approval: implement Git first. Execution question covers `git-snapshot` recorded-commit reading/basis retained during original-branch changes/range journals/same-request resumption/owned-ref protection/independent results/reclamation. Do not expand this qualification to Plastic readers/VCS-free transition/Unity Editor-Library reuse/MCP/full native failure matrices.

Fixed real input is a regular Git repository in new owned `.local/gb1004/source`. Create 1MiB binary/small text-ignore files/ignored Library samples; record with `core.autocrlf=false`. Create only one editorless A/B pair in `.local/gb1004/home`. Confirm original changes/subsequent commit, A 4KiB changes/resumption/explicit compaction/independent results-release while B reads-writes, explicit B discard after independent checkpoint. Reclaim only own fixture Git refs/registrations/journals-mounts-owned providers; preserve original fixture/independent results/metadata receipts/evidence. No installation/grant/volume settings/historical VSS reclamation. On failures retain identical key/id/home, fix cause/resume. Completion requires no owned refs/registrations/deltas/mounts/providers and both independent results; no judgment of whole physical peak/Unity RAM savings.

| ID | Included unit | Completion criteria | Current status |
| --- | --- | --- | --- |
| VP-01 | Common basis contracts/fake conformance | Define project scope/immutable identity/bounded reads/owned basis references/missing-unknown rejection/same-request recovery. Verify original edits/basis movement-corruption/other-owner protection | Git common contracts/fakes/protection automated tests Passed |
| VP-02 | Git basis reader/owned references | Exact project trees/blobs only. No full checkout/initial payload copies/read disk cache; identical bytes after original-branch changes; preserve-reject object loss. Protect original index-branch/peer-result refs | Git reader/owned refs connected; small editorless A/B verified. Subsequent Unity/Library closed through GL; large/general RAM unqualified |
| VP-03 | Plastic basis reader/fixed identity | Exact changeset-revision lists/content reads; no new Plastic registration/check-in by provider. Check required-source inclusion/content transformations/server-cache costs/failures-lost responses; protect original/peer | Fixed reader/full cs 2 lengths-hashes/automated protection passed. Distinguish approved exact-input check-in from read-only product contract; follow PPB |
| VP-04 | Windows overlay/Runtime-CLI integration | Connect WinFsp/range journals/common immutable readers. Zero new VSS calls during creation/resumption/read/compaction/collection/reclamation. Update current input-output schemas/fakes/capabilities/features-AI docs together | Git/Plastic integration/schema-fakes/required regressions complete. Actual Plastic lifecycle follows PPB status |
| VP-05 | Scoped actual inputs/A-B/cost decisions | One necessary small editorless A/B per VCS: fixed basis during original edits, changed-volume storage, same-request resumption, B continuing during A completion, independent results/dedicated-resource reclamation. Separate file allocation/transfer-cache/owned memory/residual shared data | Git A/B and separate PPB Plastic Unity A/B confirmed basis-Library isolation/resumption/peer continuation/independent results-reclamation/original restoration-costs. Retain MCP response/release-test overlap limits and external cache-total peak unknown |

Connect Git reader first, then Plastic reader to the same contracts. Run actual workspaces within units with fixed inputs/questions/completion criteria. Do not automatically include full Unity Editor/Library reuse/MCP/Play-Test-build in VP-05. Exclude VCS-free folder transition/original-write kernel drivers/new installer-service-grant-volume settings/server-history changes/full failure-ARM-large matrices.

Git VP-01/02 and VP-04/05 portion closed scoped-complete-with-limitations through [dedicated verification](../verification/git-project-basis.md): automated 919 Passed/1 optional NotRun and fixed actual editorless A/B. Subsequent Git Unity/Library closed separately in [GL verification](../verification/git-library-reference.md). Plastic VP-03 and related integration/actual inputs also [closed with limited qualification](../verification/plastic-project-basis.md) under [PPB scope](plastic-project-basis.md), automated 961 Passed/1 optional skip/fixed Unity A/B. Retain large/whole-peak qualification gaps and VCS-free transition; RAM savings are not Plastic pass criteria. No automatic repeated A/B/full failure matrices without new questions.

<a id="고정-vcs-내용의-사전-확인"></a>
## Prechecking fixed VCS contents

The 2026-10-04 [read-only probe](../../artifacts/vcs-project-basis-20261004/probe.py) and [record](../../artifacts/vcs-project-basis-20261004/basis-probe.json) checked existing VCS project-version lists/content availability without full checkout/new VSS/original writes. Each content sample streamed 85B from `ProjectSettings/ProjectVersion.txt`, preserving only hashes. Manifest JSON stored identifiers/file lists, no file payloads. CLI-internal cache writes unmeasured. This does not prove zero product creation/read hydration or provider/Unity support.

| Input | Fixed basis | List | Content sample/limits |
| --- | --- | --- | --- |
| Git `splinter-unity` | commit `6a54974a9c655031aeb2be17fb7622fc5d641c52` | 1,602 files/logical 2,804,769,314B | `cat-file` fixed-blob 85B read. No whole-content export/checkout |
| Plastic existing test repository | changeset 1 | 26 files/logical 70,004B | `cm ls --tree`/`cm cat` reads. **No recorded Assets; cannot use as equivalent basis for the whole current Unity folder** |

During prechecks, an unquoted PowerShell Git revision expression was requoted/requeried; installed Plastic 11.0.16.9915 `ls` format rejected `{tab}`, so same-version XML output was used. Restricted `getworkspacefrompath` access also failed; switched to normal-user server-version reads. No workspace registrations/file-permission changes. Probe invokes read-only commands; their timeouts do not justify repeating mutation requests.

Do not automatically check in current Plastic private Assets or create a whole copy to satisfy prerequisites. First actual Plastic run needs an explicit changeset containing required source and prior comparison with current original. List/content access success does not qualify actual A/B.

Official references: [Git objects](https://git-scm.com/book/en/v2/Git-Internals-Git-Objects), [cat-file](https://git-scm.com/docs/git-cat-file), [Plastic list](https://docs.unity.com/en-us/unity-version-control/uvcs-cli/list), [getfile](https://docs.unity.com/en-us/unity-version-control/uvcs-cli/getfile), [Unity Asset Database](https://docs.unity3d.com/6000.0/Documentation/Manual/asset-database-contents.html). Distinguish commit/changeset content accessibility from Splinter adapter implementation/cost qualification.

<a id="별도-보존하는-두-번째-문제"></a>
## Separately preserved second problem

The user selected only the first problem for this task; do not automatically rerun independent reclamation constraints of existing snapshots. [Reclamation reproduction/protection](../verification/storage-recovery.md) and [existing C: Plastic pending](../verification/plastic-compatibility.md) own evidence. New VCS bases do not automatically make historical VSS safe or reclaimed. Retain current isolation guards/same id-home-intent retries/non-owned-other-task snapshot protection.
