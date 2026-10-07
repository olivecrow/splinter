<a id="plastic-사용-환경의-작업공간"></a>
# Workspaces in Plastic environments

[한국어](ko/plastic-compatibility.md)

The exact historical C: A/B were completed as released/discarded after separately authorized removal of blocking snapshots. [VR-01](verification/vss-and-mcp-remediation.md) distinguishes the historical pending state from current state; general unowned-snapshot protection guards remain.

Splinter provides parallel workspaces. Unity VCS/Plastic SCM integration is a compatibility boundary that isolates original metadata, registrations, and locks without additional checkout/copy costs. [Verification records](verification/plastic-compatibility.md) own current-candidate automated/actual qualification; the [plan](plans/plastic-compatibility.md) owns this scope.

The separate `plastic-snapshot` referencing recorded changesets follows the [project basis](plastic-project-basis.md) and [current verification](verification/plastic-project-basis.md). Default snapshot selection, service, and VSS reclamation qualification below concern existing `ntfs-snapshot`.

<a id="선택과-입력"></a>
## Selection and inputs

Specify `--source plastic` in the initial request. Current state v2 fixes project/key/id/home, source/application, basis, and digest. For `plastic`, default application is `unity`, basis is `snapshot`, and only NTFS tasks are supported. `--application none` serves the common lifecycle for Editor-free inputs. There is no fallback to another source.

```powershell
pwsh ./scripts/splinter.ps1 doctor --project <Plastic original> --source plastic --application unity
pwsh ./scripts/splinter.ps1 schema workspace.ensure
pwsh ./scripts/splinter.ps1 workspace ensure --project <Plastic original> --key <same task key> --source plastic --application unity --base snapshot --storage ntfs-snapshot --home <registered short home>
```

Read-only checks inspect local `cm` version, exact workspace root/GUID, and `regular/static` type. Admission fixes the limited content hash of root `.plastic/plastic.workspace` and owned request. Confirm the same management identity at the actual capture's verified read location before fixing the file basis. No arbitrary changeset/branch is checked out to create it; actual capture-time files are the basis. Subsequent original-file edits remain allowed.

Original must be the exact Plastic workspace root. Partial/Gluon, dynamic/plasticfs, mixed/nested VCS, and paths inheriting another workspace are rejected. Home is outside the original at a location **overlapping no Plastic registration and inheriting no Git/Plastic or other management metadata**. Product-repository `.local` inherits that repository's Git and must not serve as Plastic worker home. Do not delete/move arbitrary registrations to satisfy requirements. Windows/NTFS, WinFsp, registered restricted service, supported ordinary files, and space conditions retain the [NTFS contract](ntfs-workspaces.md).

<a id="관리-정보와-결과"></a>
## Management information and results

Exclude original `.plastic` from the worker basis tree. Overlay identity's `deniedTopLevel` blocks `.plastic`, `.git`, `.hg`, and `.svn` roots from reads/enumeration and creation/rename destinations. The same delta fixes this policy for reconnect/replay. Git paths retain their separate existing policy excluding original `.git` and creating a private linked-worktree pointer.

A/B are **file workspaces unregistered with Plastic**. They neither share/change original Plastic registration, server locks, user settings, or credentials, nor create per-task Plastic checkout/wktree/index/cache copies. Before explicit source checks/collection, verify local workspace listings and absent management areas in home/workers. Failed observation, corruption, and unsupported types are preserved/rejected. This provides neither a security sandbox for arbitrary external tools nor an exclusive lock against concurrent global VCS-registration changes.

`checkpoint` preserves file manifests and actual independent basis/result payload regardless of VCS commit/check-in. Payload is not hydrated/copied before explicit collection. Required payload storage during collection is separate from creation/read/delta costs. Original management identity for results/discard remains in `plastic/admission.json`/`plastic/snapshot.json`; common file ownership/basis/results remain in `files/`. Their schemas are `urn:splinter:v1:plastic-source-admission`, `plastic-source-receipt`, and `file-source-manifest`.

Release/discard retain intent, seal, independent-result, original/peer, and owned-resource reclamation guards from [common file results](vcs-independent-workspaces.md) and [explicit discard](workspace-discard.md). The privilege service verifies only request-bound receipts/payload; it does not use `cm`, original Plastic metadata, or user authentication settings as SYSTEM. A current service package is required; ordinary CLI does not automatically elevate or fall back to a helper.

Protection of unowned snapshots/shared source-volume storage can defer reclamation. A release returns `reclaim-pending`; explicit discard returns an Accepted/nonterminal `discard-reclaim-pending` response and same-intent next actions. Neither is completion. Final snapshot reclamation for separate small T: A/B was boundedly verified, distinct from preceding C: deferral and Unity CLI A/B discovery failure. [PC verification](verification/plastic-compatibility.md) fixes exact input/qualification.

Subsequent [Unity CLI handoff verification](verification/unity-cli-handoff.md) used new A/B on the same registered T: input. It checked exact-path commands for workers absent from listings, loaded code, B work after A's normal shutdown, both results, and reclamation. It did not fix CLI list discovery or automatically qualify MCP/other inputs/versions. Actual usage follows [exact target selection](alpha-workspaces.md#exact-target-selection-with-unity-cli).

Original application for legacy `ntfs-snapshot` Plastic, Plastic check-in/update/merge, and new VCS registration are not provided. Full/selected local-file application from recorded `plastic-snapshot` follows a [separate protection boundary](plastic-original-apply.md). Editor refresh, compilation, Play/Test/build, and external-tool connection remain AI/existing-tool responsibilities; Splinter readiness/checkpoint do not claim their success.
