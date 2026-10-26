<a id="vcs-없는-ntfs-작업공간"></a>
# VCS-free NTFS workspaces

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

Source/application, basis, storage and budget are fixed in the request digest and v2 state. Resume with identical project/key/id/home. Preserve original/task receipts and legacy state. No service/grant is required and earlier versions or VSS state are not migrated.

<a id="기준검사결과"></a>
## Basis, checks, and results

Creation checks source metadata and uses managed project bases, WinFsp, and range journals. There is no whole-payload copying, read hydration, or whole-file copy-up fallback. `Splinter.SourceControl.Files` fixes a basis manifest by reading ordinary file/directory paths, types, lengths, SHA-256, mtime, and attributes at a verified immutable snapshot path. Subsequent original changes/reconnects do not recompute the basis. The common revision field is the provider's opaque identity; for `none` it is the manifest's `sha256:` identity. Git branches/commits are not required and internal `git init` is not run.

Initial basis/explicit content checks for current ordinary folders read the whole input. Cache is not trusted from mtime alone. Manifest limits are 32,768 entries, 1 MiB total UTF-8 path bytes, and 16 MiB JSON; larger inputs are rejected. Ordinary status continues to observe only resources.

`workspace checkpoint --id <id> --message <description>` normally stops the owned application and preserves stable results. Per-task control stores **actual payload** for basis/results in `files/baseline.json`, `result.json`, owner/collection receipts, and `objects/<sha256>`. Identical content is reused within that task. Explicit collection preserves the full basis and full result, so this cost is separate from the private range journal. `resultStorage` reports basis/result logical bytes and unique payload bytes/count. Actual allocation, physical peak, and historical VSS attribution are observed separately.

Independent results are published only after verifying all files, hashes, and owners and matching before/after `stable-journal-v1` seals. Result verification does not use original/task mounts/VSS. Lost responses for the same collection resume by checking published content and fixed temporary files; other intents or partial/damaged files are preserved/rejected. Results are not automatically overwritten or swept. Checkpoint does not create compile/test acceptance evidence.

Each payload-preservation stage processes only unique hash objects. Within one independent-result verification call, basis/result manifest object sets are combined, hashing each identical object once. Conflicting declared lengths for the same hash are rejected before object reads. Prepublication, postpublication, and subsequent reclamation checks each revalidate; there is no persistent cache trusting earlier success. All manifest path/metadata, identity/owner, and collection checks remain. [Optimization regressions](verification/code-quality-optimization.md) distinguish shared-object read counts from subsequent-corruption rejection.

<a id="반영회수폐기"></a>
File checkpoint collection uses one source manifest inside the coordinator's complete pre/post-seal interval. Local full-payload preservation and independent-object verification run with at most eight workers and drain before returning on failure/cancellation. Existing, pending and later-invocation objects are freshly proved; per-object durable flush and all result/ownership checks remain. See [RT-01–04](verification/result-collection-throughput.md) for the matched-input Windows measurement and its limits.

## Application, reclamation, and discard

`workspace integrate --target original --plan` compares the exact checkpoint, basis/result and current original. `project-snapshot` supports full/selected application for application none/unity with original mount identity, normal Editor shutdown, and explicit intent/digest. Follow [original application](plain-folder-original-apply.md) and [project basis](plain-project-basis.md) for guards and bounded qualification.

Release retains actual independent-payload/seal verification, normal stop, zero owned resources, post-unmount seals, absent delta and basis-dependency guards. Unknown is neither zero nor complete. Explicit discard reclaims only its own resources with the same intent/seal and completed file-source receipts, preserving earlier independent results, original and peers.
