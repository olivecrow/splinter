<a id="os와-앱의-확장-경계"></a>
# OS and application extension boundaries

[한국어](ko/extensibility-boundaries.md)

The current execution adapters and CLI support Windows/NTFS and `unity|none`. Mount, process, and publication adapters for macOS/Linux, and adapters for Blender/Unreal/Godot, are not implemented. Synthetic tests of common contracts do not establish actual support for those environments. See [extension-contract verification](verification/extensibility-boundaries.md) for the verification scope.

`ISnapshotTree.SupportsConcurrentReads` defaults to `false`. A provider may opt in for its entire lifetime only when simultaneous payload reads are safe; it keeps any decoder synchronization internally. Grafts require both providers to opt in, and ownership wrappers forward the capability. The journal otherwise retains its serial basis-read gate. In both cases it captures private bytes and fixed basis coordinates under its mutation gate, checks completion, and drains all active reads before releasing ownership. See [startup-time verification](verification/startup-time-optimization.md).

<a id="경로와-파일-metadata"></a>
## Paths and file metadata

`SnapshotReference.PathPolicy` is part of immutable snapshot identity. `WorkspacePathPolicy` provides path normalization, comparison, and parent/child classification. Journal indexing, moves, hiding, replay, compaction, candidates, and grafting use the same policy. An omitted value retains the semantics of the current Windows contract. A case-sensitive policy is fixed as a separate identity; the same delta cannot reconnect under another policy, and trees with different policies cannot be grafted together. Unknown policies are rejected.

The optional `UnixMode` on `SnapshotEntry` and `DeltaEntry` carries Unix permission bits. Unknown permissions are null and are not inferred from Windows attributes. Git regular-file and executable-file modes are also distinguished. Journal metadata updates and candidate storage preserve this information. Windows attributes/timestamps, Unix permissions, and actual OS support for ACLs, owners, and special files are separate concerns. Carrying this information does not implement a Unix filesystem.

<a id="앱별-프로젝트-정책"></a>
## Application project policies

Core's `IApplicationProjectPolicy` supplies application identity, compatibility keys, generated-directory descriptors, original-access reservations, and companion-file scope for selected source files. `ApplicationProjectPolicies` selects the exact policy in the composition layer. An unregistered application is not substituted with Unity or none.

`UnityProjectPolicy` owns `Temp/UnityLockfile`, parents and `.meta` pairs under `Assets`, Unity/bridge compatibility keys, and the `Library` descriptor. The Files provider handles common parent processing, three-way comparison, fixed intents, and conditional publication. It also protects companion files returned by the application policy as unchanged guards. Plastic registration, selector, and server-lock checks remain at the existing VCS boundary. Original reservations are released when their scope ends or a check fails; another process's lock or Temp contents are not deleted.

For a managed original, Windows first acquires the exact mount identity lease, then passes only that root to the application policy. This permission does not extend to other reparse paths or arbitrary mounts. The application policy only checks/reserves normal shutdown; it does not decide whether compilation, Play/Test, or builds succeeded.

<a id="생성-디렉터리"></a>
## Generated directories

`ManagedDirectoryBasis` uses the injected policy's `GeneratedDirectoryDescriptor(Kind, RelativePath)` and compatibility key. The current common graft supports a single top-level child directory; nested or multiple generated directories are unsupported. Unity's public commands remain `library prepare/status/restore` and `--library-basis`. No public options or commands for other applications have been added.

A whole-project basis uses a separate `Project` descriptor. The current version of managed-basis state and preparation intent is **v2**, fixing application, directory kind, path, and compatibility key. v1 records are preserved and explicitly rejected, without automatic conversion. Workspace v2, overlay v2/SJR2, candidate v3, and restore plan v2 remain unchanged. Same-volume moves, original/peer protection, reader dependencies, fixed restore plans, and explicit final restoration follow the existing [Library](git-library-reference.md) and [whole-project](plain-project-basis.md) contracts.
