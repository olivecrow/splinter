<a id="plastic-프로젝트-기준본"></a>
# Plastic project basis

[한국어](ko/plastic-project-basis.md)

`--source plastic --storage plastic-snapshot --base <changeset@repository@server>` connects a recorded project version to the WinFsp range journal. It uses no VSS, new checkout/Plastic registration, initial payload copying, or read disk hydration. The original remains editable at its existing NTFS location. Existing `ntfs-snapshot` is a capture-time file basis and is not automatically migrated to this separate path.

<a id="입력과-관리-경계"></a>
## Inputs and management boundaries

Invalid fixed-revision input is `PlasticBasisRequestInvalid`/`correct-request`. Damage or changes to an already fixed listing, receipt, or revision content are distinguished as `PlasticBasisInvalid`, returning `requires-user-action` and workspace `preserve`/`workspace.status`. Preserve the same task/basis/journal without selecting a new basis or bypassing through another VCS.

The original must be an exact regular/static Plastic workspace root. Compare controlled-directory XML for the explicit original local root with the requested changeset listing's canonical repository/server and repository ID. Fix explicit numeric changeset, root revision/GUID, file revision/length/hash, listing digest, and owner in `snapshot/plastic-basis.json`. Do not fall back to latest branch values, mutable originals, or another VCS. Dirty/private working-tree files are not labeled as part of the recorded basis. Unity inputs require recorded Assets, Packages/manifest.json, and ProjectSettings/ProjectVersion.txt.

```powershell
pwsh ./scripts/splinter.ps1 schema workspace.ensure
pwsh ./scripts/splinter.ps1 workspace ensure --project <original> --key <stable-key> --source plastic --application unity --storage plastic-snapshot --base <changeset@repository@server> --home <same-volume-short-home> --library-basis <prepared-id>
```

Home must neither inherit nor overlap any VCS management/Plastic registration. VSS service/grant is not a prerequisite for this recorded-basis path. Installed WinFsp, PowerShell 7, and standard-user `cm` repository-read access are required. The content reader uses verified `11.0.16.9915` libraries in `%ProgramFiles%/PlasticSCM5/client`; other versions are preserved/rejected with `PlasticBasisUnsupported`. Original management roots such as `.plastic` are excluded from the worker and protected against writes/renames. The worker is an unregistered file workspace and provides no Plastic update/check-in/merge or authentication/server configuration. Full/selected local application follows a [separate plan/intent contract](plastic-original-apply.md).

VCS management entries are rejected at every depth of the recorded tree. Runtime tool queries for nonexistent nested `.git`/`.plastic` paths are ordinary not-found. Permitting those queries does not permit recorded management entries or writes to top-level worker management roots.

<a id="읽기와-비용"></a>
## Reads and costs

The fixed listing is `cm ls --tree` XML. Content is read through private stdin/stdout of a standard-user PowerShell child owned by the tree. `PlasticRevisionBridge` uses only the fixed client libraries' revision-segment reader and decoder, creating no HTTP listener, mutation API, or payload files. It does not fall back to binary/BOM conversion through `cat --raw` or historical unauthenticated `cm api`. The helper remains local to its process; on tree shutdown, stdin closes and only its own child is terminated if still present.

Helper decoded scratch is 4MiB; returned frames are at most 64KiB. The owner accepts one stream from the same tree, with a 120-second body limit. Failed, oversized, or interrupted bodies terminate that child to avoid mixing with the next revision. The first content request verifies the full revision length and server MD5 content identity, staging only requested bytes. Later requests compare against verified SHA-256 chunk proofs. Errors, corruption, and incomplete streams do not publish into the request buffer. MD5 is Plastic's existing content identifier, not a new security authentication/signature feature. The library adapter is an internal fixed-version boundary, not advertised as a stable public vendor SDK.

For one revision, retain at most 4MiB proof, a 64KiB chunk, and up to 1MiB request staging; file limit is 8GiB. Sequential reads reuse the open stream/chunk. Returning to an earlier position or another file may require a new stream and prefix reads/full proof through the same owned helper. No whole-payload memory/disk cache bypass is used. External-client caches, transfer, and native memory differ from this managed-buffer limit and need separate measurement.

`snapshotAvailable` means the owned fixed manifest is valid. Tree open validates identity/digest and all metadata; a read handle blocks manifest writes/replacement. Metadata callbacks do not reread/hash the same manifest each time; path protection and reader fences remain. Server access to each revision is checked separately during content reads. A local manifest alone does not establish read success when server permissions/content are lost or unavailable. Open reader leases block reclamation; release ends only local owner/intent. It neither deletes server history/shared caches nor creates server pins preventing revision deletion.

<a id="library결과회수"></a>
## Library, results, and reclamation

Use the same [original Library lifecycle](git-library-reference.md) as the Git path. `library prepare` moves only original Library into an immutable base on the same volume and provides an original overlay at the existing path; fix the returned id in `--library-basis`. Original/A/B changes use separate journals. Initial/final transitions require normal original-Editor shutdown; Splinter does not automatically stop the original Editor or shared MCP server.

Independent results use existing file manifests and `files/objects`. Creation/ordinary reads store no payload; explicit checkpoint result collection preserves actual basis/result payload. Results can be verified without server, original, or Library basis. Result-preservation cost is separate from task delta.

Release/discard checks seals, normal stop/unmount, and absent owned provider/handles/processes/delta. Remove that task's Library dependent only after completing the source basis's local release intent. If dependent removal fails after basis reclamation, continue the same request. After final peer reclamation, `library restore` applies only final original ranges and restores ordinary NTFS Library. Other tasks, existing VSS pending states, original registration, and independent results remain.

[Security-boundary verification](verification/security-boundaries.md) owns current-reader automated/fixed binary/BOM-read qualification. Actual Unity A/B in historical [PPB verification](verification/plastic-project-basis.md) and the [PPB plan](plans/plastic-project-basis.md) is evidence for the then-current API reader, not qualification of the new reader for Unity or large segmented inputs.
