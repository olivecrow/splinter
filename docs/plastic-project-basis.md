# Plastic project basis

`--source plastic --storage plastic-snapshot --base <changeset@repository@server>` connects a recorded project version to the WinFsp range journal. It uses no VSS, new checkout/Plastic registration, initial payload copying, or read disk hydration. The original remains editable at its existing NTFS location. Legacy `ntfs-snapshot` is retired and preserved/rejected without automatic migration.

<a id="입력과-관리-경계"></a>
## Inputs and management boundaries

Invalid fixed-revision input is `PlasticBasisRequestInvalid`/`correct-request`. Damage or changes to an already fixed listing, receipt, or revision content are distinguished as `PlasticBasisInvalid`, returning `requires-user-action` and workspace `preserve`/`workspace.status`. Preserve the same task/basis/journal without selecting a new basis or bypassing through another VCS.

The original must be an exact regular/static Plastic workspace root. Compare controlled-directory XML for the explicit original local root with the requested changeset listing's canonical repository/server and repository ID. Fix explicit numeric changeset, root revision/GUID, file revision/length/hash, listing digest, and owner in `snapshot/plastic-basis.json`. Do not fall back to latest branch values, mutable originals, or another VCS. Dirty/private working-tree files are not labeled as part of the recorded basis. Unity inputs require recorded Assets, Packages/manifest.json, and ProjectSettings/ProjectVersion.txt.

```powershell
pwsh ./scripts/splinter.ps1 schema workspace.ensure
pwsh ./scripts/splinter.ps1 workspace ensure --project <original> --key <stable-key> --source plastic --application unity --storage plastic-snapshot --base <changeset@repository@server> --home <same-volume-short-home> --library-basis <prepared-id>
```

Home must neither inherit nor overlap any VCS management/Plastic registration. VSS service/grant is not a prerequisite for this recorded-basis path. Installed WinFsp, PowerShell 7, and standard-user `cm` repository-read access are required. The content reader uses verified `11.0.16.10484` libraries in `%ProgramFiles%/PlasticSCM5/client`; other versions are preserved/rejected with `PlasticBasisUnsupported`. Original management roots such as `.plastic` are excluded from the worker and protected against writes/renames. The worker is an unregistered file workspace and provides no Plastic update/check-in/merge or authentication/server configuration. Full/selected local application follows a [separate plan/intent contract](plastic-original-apply.md).

VCS management entries are rejected at every depth of the recorded tree. Runtime tool queries for nonexistent nested `.git`/`.plastic` paths are ordinary not-found. Permitting those queries does not permit recorded management entries or writes to top-level worker management roots.

<a id="읽기와-비용"></a>
## Reads and costs

The fixed listing is streamed `cm ls --tree` XML into an immutable paged catalog. Content is read through private stdin/stdout of a standard-user PowerShell child owned by the tree. `PlasticRevisionBridge` uses only the fixed client libraries' exact revision readers and decoder, creating no HTTP listener, mutation API, or payload files. Its source travels over the private pipe to avoid the Windows command-line length limit. It does not fall back to binary/BOM conversion through `cat --raw` or historical unauthenticated `cm api`. The helper remains local to its process; on tree shutdown, stdin closes and only its own child is terminated if still present.

Helper decoded scratch is 4MiB; returned frames are at most 64KiB. The owner accepts one stream from the same tree, with a 120-second timeout on each blocked frame/body read, disabled during caller hashing. Failed, oversized, or interrupted bodies terminate that child to avoid mixing with the next revision. The first content request verifies the full revision length and server MD5 content identity, staging only requested bytes. Later requests compare against verified SHA-256 chunk proofs. Errors, corruption, and incomplete streams do not publish into the request buffer. MD5 is Plastic's existing content identifier, not a new security authentication/signature feature. The library adapter is an internal fixed-version boundary, not advertised as a stable public vendor SDK.

One revision retains disk-backed SHA-256 chunk proof metadata, a 64KiB chunk, and up to 1MiB request staging; there is no aggregate file-size or segment-count quota. Sequential reads reuse the open stream/chunk. Small-file lookahead uses at most 64 exact revision IDs and 4MiB total decoded payload through the same serialized helper and the qualified client's `GetSmallFilesData` call. Every returned ID, segment, last marker, length and MD5 must match the requested batch; missing, duplicate or foreign records reject the whole batch before exposure. The tree still performs its ordinary full MD5 and durable SHA-256 chunk proof. The helper retains only that bounded batch; larger files keep segmented reads. This is neither original-file substitution nor project hydration. External-client packet buffers, this payload batch, native handles and private memory are measured separately from the metadata cache.

The SCAL candidate changes task/basis/overlay formats to v4/v3/v3 without migrating old state. Its qualification remains in progress. A read-only fixed 64-revision protocol check is recorded separately from whole-lifecycle and Unity performance; historical RI/PPB measurements do not qualify the new candidate.

`snapshotAvailable` means the owned fixed manifest is valid. Tree open validates identity/digest and all metadata; a read handle blocks manifest writes/replacement. Metadata callbacks do not reread/hash the same manifest each time; path protection and reader fences remain. Server access to each revision is checked separately during content reads. A local manifest alone does not establish read success when server permissions/content are lost or unavailable. Open reader leases block reclamation; release ends only local owner/intent. It neither deletes server history/shared caches nor creates server pins preventing revision deletion.

<a id="library결과회수"></a>
## Library, results, and reclamation

Use the same [original Library lifecycle](git-library-reference.md) as the Git path. `library prepare` moves only original Library into an immutable base on the same volume and provides an original overlay at the existing path; fix the returned id in `--library-basis`. Original/A/B changes use separate journals. Initial/final transitions require normal original-Editor shutdown; Splinter does not automatically stop the original Editor or shared MCP server.

Independent results use existing file manifests and `files/objects`. Creation/ordinary reads store no payload; explicit checkpoint result collection preserves actual basis/result payload. Results can be verified without server, original, or Library basis. Result-preservation cost is separate from task delta.

Continuing `checkpoint --request-key` uses the [common immutable-generation engine](repeated-original-apply.md): source state is fixed under a short mutation boundary and scan/payload preservation follows outside it. Final shutdown retains complete seals. Local payload/proof work uses at most eight workers where the tree supports concurrent reads; recorded revision acquisition remains serial. Worker failure/cancellation drains all active operations, and every new object still flushes durably before publication. RT-01–04 is the historical collection-throughput qualification; its native timing uses None input and is not a Plastic-server performance claim.

Release/discard checks seals, normal stop/unmount, and absent owned provider/handles/processes/delta. Remove that task's Library dependent only after completing the source basis's local release intent. If dependent removal fails after basis reclamation, continue the same request. After final peer reclamation, `library restore` applies only final original ranges and restores ordinary NTFS Library. Other tasks, existing VSS pending states, original registration, and independent results remain.

Security-boundary verification records private-reader qualification; RI-07 owns the current-client segmented/BOM reads and actual Unity original/A/B sequence. Historical PPB verification and the PPB plan remain evidence for their then-current API reader and do not qualify another candidate.
