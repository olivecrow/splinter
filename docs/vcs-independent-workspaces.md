<a id="vcs-없는-ntfs-작업공간"></a>
# VCS-free NTFS workspaces

Current task state is **v3**; managed basis remains v2. [Repeated original application](repeated-original-apply.md) owns live capture, immutable result versions, per-path publication history and exact external cooperation for a running original. [RI qualification](verification/repeated-original-apply.md) is separate from historical stopped-Editor evidence; older tasks are preserved/refused without migration.

[한국어](ko/vcs-independent-workspaces.md)

The current `--source none` path is [project basis](plain-project-basis.md): `project prepare` → `workspace ensure --storage project-snapshot --source none --project-basis <id>`. It uses the explicitly authorized managed original view, same-volume moves, immutable source/Library sharing and private journals. Full/selected application supports application none/unity, followed by task-reference reclamation and explicit NTFS restoration. See [NP verification](verification/plain-project-basis.md) and [VSS retirement](plans/vss-retirement.md). No legacy-state migration or source fallback.

Splinter provides parallel isolation, range storage, and the basic lifecycle. The product goal is to provide these with or without external VCS, ensuring VCS metadata, registration, locks, shared bases, and added storage/memory costs do not undermine workspace isolation, efficiency, or original/peer protection. Source providers own this compatibility boundary and basis/result preservation.

Product CLI selects a source provider through `--source git|none|plastic`. Omission uses the existing Git path. Git errors, damage, or unknown state are not interpreted as ordinary folders. `none` allows only NTFS tasks for ordinary folders without VCS metadata. `.git`, `.plastic`, `.hg`, `.svn` at the original/ancestor/descendant paths and unsupported file types are preserved/rejected. Management-information/registration isolation in Plastic environments follows a [separate contract](plastic-compatibility.md). [Verification records](verification/vcs-independent-workspaces.md) own actual support status.

<a id="입력과-같은-작업-재개"></a>
## Inputs and same-task resumption

```powershell
pwsh ./scripts/splinter.ps1 schema workspace.ensure
pwsh ./scripts/splinter.ps1 doctor --project <prepared ordinary folder> --source none
pwsh ./scripts/splinter.ps1 workspace ensure --project <prepared ordinary folder> --key <fixed key> --source none --storage project-snapshot --project-basis <prepared-id> --home <home>
```

For `none`, default basis is `snapshot` and application is `none`. Creation/edit/resumption/result preservation/reclamation/discard can be requested without Unity files, Editor, or license. For Unity source, fix `--application unity` in the initial request and request `--editor` or `unity start` only when needed. Every path under application `none` is ordinary data; `Library` and IDE files are not arbitrarily excluded.

Source/application, basis, storage and budget are fixed in the request digest and v3 task state. Resume with identical project/key/id/home. Preserve original/task receipts and legacy state. No service/grant is required and earlier versions or VSS state are not migrated.

<a id="기준검사결과"></a>
## Basis, checks, and results

Creation checks source metadata and uses managed project bases, WinFsp, and range journals. There is no whole-payload copying, read hydration, or whole-file copy-up fallback. `Splinter.SourceControl.Files` fixes a basis manifest by reading ordinary file/directory paths, types, lengths, SHA-256, mtime, and attributes at a verified immutable snapshot path. Subsequent original changes/reconnects do not recompute the basis. The common revision field is the provider's opaque identity; for `none` it is the manifest's `sha256:` identity. Git branches/commits are not required and internal `git init` is not run.

Initial basis/explicit content checks for current ordinary folders read the whole input. Cache is not trusted from mtime alone. Manifest limits are 32,768 entries, 1 MiB total UTF-8 path bytes, and 16 MiB JSON; larger inputs are rejected. Ordinary status continues to observe only resources.

`workspace checkpoint --id <id> --request-key <key> --message <description>` preserves one immutable source generation while the task/Editor continue. Control stores `files/generation-basis.json`, `files/generations/<result-id>/result.json`, generation receipts and content-addressed `objects/<sha256>`. Full independent basis/result payloads are retained only at explicit collection and reused by content across all task generations. `resultStorage` distinguishes logical basis/latest-result bytes from unique retained payload bytes/count; physical allocation and peak remain separate. Same-key retry verifies/returns the first result. Durable metadata/range pins protect frozen reads outside the mutation boundary; final shutdown/unmount retains complete seals. See [the current result contract](repeated-original-apply.md).

Each payload-preservation stage processes only unique hash objects. Within one independent-result verification call, basis/result manifest object sets are combined, hashing each identical object once. Conflicting declared lengths for the same hash are rejected before object reads. Prepublication, postpublication, and subsequent reclamation checks each revalidate; there is no persistent cache trusting earlier success. All manifest path/metadata, identity/owner, and collection checks remain. [Optimization regressions](verification/code-quality-optimization.md) distinguish shared-object read counts from subsequent-corruption rejection.

<a id="반영회수폐기"></a>
File generation collection scans one frozen source manifest outside the mutation boundary; final closure also retains complete pre/post-seals. Local full-payload preservation and independent-object verification run with at most eight workers and drain before returning on failure/cancellation. Existing, pending and later-invocation objects are freshly proved; per-object durable flush and all result/ownership checks remain. See [RT-01–04](verification/result-collection-throughput.md) for the matched-input Windows measurement and its limits.

Full file scans enumerate bounded membership; None hashes with at most eight workers, draining on failure/cancellation. Mounted Plastic scans remain serial because its reader retains a single revision proof/stream. The same initial editorless ensure can reuse one full inspection only after validating complete provider generations around both the original scan and a metadata/membership recheck. Owner, basis, mount, policy, identity and Plastic registration isolation remain checked; a missing tracker or audit uses a full scan. The single-use proof is never persisted or reused after Editor startup/provider restart. [PR-01–04](verification/preparation-throughput.md) owns this preparation/observation optimization and its separate timing limits.

## Application, reclamation, and discard

`workspace integrate --target original --result <id> --plan` compares the exact checkpoint, basis/result and current original. `project-snapshot` supports full/selected application for application none/unity with original mount identity, exact live-Editor cooperation or stopped reservation, and explicit intent/digest. Follow [original application](plain-folder-original-apply.md) and [project basis](plain-project-basis.md) for guards and bounded qualification.

Release retains actual independent-payload/seal verification, normal stop, zero owned resources, post-unmount seals, absent delta and basis-dependency guards. Unknown is neither zero nor complete. Explicit discard reclaims only its own resources with the same intent/seal and completed file-source receipts, preserving earlier independent results, original and peers.
