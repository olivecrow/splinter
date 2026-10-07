<a id="vcs-없는-ntfs-작업공간"></a>
# VCS-free NTFS workspaces

[한국어](ko/vcs-independent-workspaces.md)

The new VSS-free path uses the [project basis](plain-project-basis.md): `project prepare` → `workspace ensure --storage project-snapshot --source none --project-basis <id>`. It uses the authorized managed view of the original, same-NTFS-volume moves, immutable source/Library sharing, and three private journals. It connects full/selected original publication for application none/unity, task-reference reclamation, and final NTFS restoration. Follow [new-candidate verification](verification/plain-project-basis.md). The `ntfs-snapshot` VSS/service prerequisites and application none original-application restriction below apply only to the existing path. There is no automatic migration/source fallback.

Splinter provides parallel isolation, range storage, and the basic lifecycle. The product goal is to provide these with or without external VCS, ensuring VCS metadata, registration, locks, shared bases, and added storage/memory costs do not undermine workspace isolation, efficiency, or original/peer protection. Source providers own this compatibility boundary and basis/result preservation.

Product CLI selects a source provider through `--source git|none|plastic`. Omission uses the existing Git path. Git errors, damage, or unknown state are not interpreted as ordinary folders. `none` allows only NTFS tasks for ordinary folders without VCS metadata. `.git`, `.plastic`, `.hg`, `.svn` at the original/ancestor/descendant paths and unsupported file types are preserved/rejected. Management-information/registration isolation in Plastic environments follows a [separate contract](plastic-compatibility.md). [Verification records](verification/vcs-independent-workspaces.md) own actual support status.

<a id="입력과-같은-작업-재개"></a>
## Inputs and same-task resumption

```powershell
pwsh ./scripts/splinter.ps1 schema workspace.ensure
pwsh ./scripts/splinter.ps1 doctor --project <registered ordinary folder> --source none
pwsh ./scripts/splinter.ps1 workspace ensure --project <registered ordinary folder> --key <fixed key> --source none --storage ntfs-snapshot --home <registered home>
```

For `none`, default basis is `snapshot` and application is `none`. Creation/edit/resumption/result preservation/reclamation/discard can be requested without Unity files, Editor, or license. For Unity source, fix `--application unity` in the initial request and request `--editor` or `unity start` only when needed. Every path under application `none` is ordinary data; `Library` and IDE files are not arbitrarily excluded.

Source/application kind, basis, storage, and budget are fixed in request digest and state. Reuse the same project/key/id/home and inputs. Only current state v2 is read; earlier v1/states without selection fields are not automatically migrated. Preserve original/task/service receipts. A current restricted-service package that reads the new state and an exact project/home grant are required. Installation/update is a separate administrator Apply under the [existing installation procedure](storage-privilege-service.md#installation-and-discovery).

<a id="기준검사결과"></a>
## Basis, checks, and results

Creation checks source metadata and uses existing VSS, WinFsp, and range journals. There is no whole-payload copying, read hydration, or whole-file copy-up fallback. `Splinter.SourceControl.Files` fixes a basis manifest by reading ordinary file/directory paths, types, lengths, SHA-256, mtime, and attributes at a verified immutable snapshot path. Subsequent original changes/reconnects do not recompute the basis. The common revision field is the provider's opaque identity; for `none` it is the manifest's `sha256:` identity. Git branches/commits are not required and internal `git init` is not run.

Initial basis/explicit content checks for current ordinary folders read the whole input. Cache is not trusted from mtime alone. Manifest limits are 32,768 entries, 1 MiB total UTF-8 path bytes, and 16 MiB JSON; larger inputs are rejected. Ordinary status continues to observe only resources.

`workspace checkpoint --id <id> --message <description>` normally stops the owned application and preserves stable results. Per-task control stores **actual payload** for basis/results in `files/baseline.json`, `result.json`, owner/collection receipts, and `objects/<sha256>`. Identical content is reused within that task. Explicit collection preserves the full basis and full result, so this cost is separate from the private range journal. `resultStorage` reports basis/result logical bytes and unique payload bytes/count. Actual allocation, physical peak, and VSS attribution are observed separately.

Independent results are published only after verifying all files, hashes, and owners and matching before/after `stable-journal-v1` seals. Result verification does not use original/task mounts/VSS. Lost responses for the same collection resume by checking published content and fixed temporary files; other intents or partial/damaged files are preserved/rejected. Results are not automatically overwritten or swept. Checkpoint does not create compile/test acceptance evidence.

Each payload-preservation stage processes only unique hash objects. Within one independent-result verification call, basis/result manifest object sets are combined, hashing each identical object once. Conflicting declared lengths for the same hash are rejected before object reads. Prepublication, postpublication, and subsequent reclamation/service checks each revalidate; there is no persistent cache trusting earlier success. All manifest path/metadata, identity/owner, and collection checks remain. [Optimization regressions](verification/code-quality-optimization.md) distinguish shared-object read counts from subsequent-corruption rejection.

<a id="반영회수폐기"></a>
## Application, reclamation, and discard

VCS-free `workspace integrate --target original --plan` is a read-only plan comparing the exact checkpoint, basis/result/current original. Conditional original application executes only for `application=none` with an explicit intent key and reviewed digest. [Original application](plain-folder-original-apply.md) owns the complete contract, interruption/reclamation guards, metadata, and actual qualification limitations. Unity application, branch targets, and execution without intent are rejected before effects.

Release retains actual independent-payload verification, normal stop, zero owned resources, post-unmount seals, absent delta, and snapshot-dependency/isolation guards. The restricted service also verifies the same fixed owner/basis/result and content. Delayed/unknown reclamation is neither completion nor 0. Explicit discard uses a separate intent, seal, completed file-source receipts, and existing separate authorization, without deleting previously preserved independent results. Only same-intent retries are allowed; original, peer, and shared resources are not deleted.
