# Bee storage with atomic publication

[한국어](../ko/plans/bee-native-storage-design.md)

This design follows the committed structural prototypes (`a8eb778`). The [SX record](../verification/structural-throughput-prototypes.md) owns their measured results and failures. The bounded product journal is committed separately as `d0ac0a2`. The user subsequently authorized implementation and real performance measurement of this design. The first implementation is a development-only, single-volume native-backing candidate; product default activation and a whole Unity Editor benchmark are separate qualification boundaries.

## Recommendation

Keep one WinFsp project volume and evaluate **internal native backing for newly generated Bee files**. Do not expose that backing through a junction. Keep the logical namespace, file identity, source tracking and resource ownership in the provider. Buffered Bee writes would be a separately declared generated-data durability policy, not a change to source write acknowledgements.

The first single-volume candidate is implemented and measured in [BN-01–05](../verification/bee-internal-native.md). Its small namespace/source/mapping/recovery checks pass, but its performance gate fails: the ABBA mean accounted lifecycle grows from 11.18s to 18.88s and retained allocation from 0.92MiB to 2.13MiB. Keep it development-only; it is not selected for product adoption. A physical NTFS root with source overlays remains unimplemented and would require the larger source-boundary redesign below.

## Evidence and comparison

The existing junction lets the graph generator run, but actual Bee cannot publish `TundraBuildState.state` and `tundra.digestcache`. Exact `MoveFileExW` calls through relative and absolute view paths fail with `ERROR_NOT_SAME_DEVICE` (17); direct native target paths succeed with the same file ID. Copy-enabled success changes the ID and does not establish atomic rename. The exact WinFsp internal branch responsible was not isolated. A missing custom reparse resolver or a host option is not an established fix.

| Boundary | One virtual volume, native backing inside provider | Physical NTFS project root, source overlays |
| --- | --- | --- |
| Bee rename and replacement | Delivered within one virtual namespace; provider must atomically update its namespace and keep replaced open identities alive | Bee stays on NTFS and its native rename is the promising part already supported by the direct-path diagnostic |
| Source and root files | Existing virtual namespace remains; backing changes must not bypass source generation or result guards | Root files, newly created source directories and temporary-to-source replacement need a new routing/protection model |
| Existing Library reuse | Retain immutable reads and range changes; new native output must not require materializing inherited Bee files | Bee can be cold and private, but other Library/source subtrees still need sharing and cross-boundary semantics |
| I/O cost | Removes selected payload encoding, journal rewrite and per-write durable flushing; WinFsp callbacks, buffer copies, namespace work and virtual cache costs remain | Can avoid WinFsp callbacks for Bee; source callbacks and directory-junction boundary costs remain |
| Mapping and shutdown | Virtual file contexts still own mappings/images; native backing lifetime must follow them | Bee mappings are native, but source overlays and native backing each need ownership and closure proof |
| Implementation scope | New backing policy plus coherent namespace, flush, recovery, accounting and reclamation integration | Workspace layout, arbitrary source/root editing, Git/Plastic boundaries, identity, results and lifecycle redesign |

The graph-only 67.0% reduction is evidence for the old junction's small graph workload. It predicts neither alternative's compilation or Editor time. Buffered full native output may use more storage than compressed private ranges; the SX graph allocation was already larger. Measure time and storage together.

## Design A: one virtual namespace

### Namespace and metadata

`WinFspOverlayFileSystem` already delegates ordinary file operations to `IWorkspaceDeltaStore` and reports a stable logical `DeltaEntry.Id`. Preserve that boundary rather than adding an unrelated path-based write bypass. No `Library/Bee` reparse point is presented to clients. A proposed backing implementation can hold private native payload by opaque logical file identity, while one namespace owner maps paths to identities. A rename then publishes one logical namespace change; it need not physically copy or rename payload merely because its logical name changes.

For replacement, the destination name changes atomically, the source identity survives, and existing handles to the replaced destination retain its previous content until their final close. Directory moves, case-only moves, share/delete semantics, EOF/allocation size, append/constrained writes, attributes and timestamps need the same behavior across backing kinds. Updating both a journal namespace and a separate native path tree without an interruption protocol would introduce two competing authorities.

The existing [source/cache transfer implementation](../../src/Splinter.Runtime/SourceViewDeltaStore.cs) is a useful contract example, not a ready-made native implementation. It preserves identity and recovers an interrupted private-file transfer. Its tests include sparse private data moving from `Temp` to `Assets` and replacement through an already open identity. A blanket rejection of all Bee-to-source moves would narrow the ordinary journal namespace contract.

### Preserve the no-copy rule

Native backing is initially empty. A newly created file contains only bytes written by the application; merely reading an inherited file must not create native payload. A partial write to an inherited Bee file must retain its immutable base plus private changed ranges. No whole-file copy-up, read hydration or original-Library copy is permitted.

The conservative first design keeps inherited files range-backed, while new files can use native backing. A new temporary output can replace an inherited name by namespace publication without copying the inherited bytes. Truncation-to-zero could later permit a backing transition, but only after its identity, open-handle and interruption semantics are proven; it must not be inferred from a filename extension. Explicitly starting with an empty Bee cache is a different application policy, with rebuild costs, and is not an automatic fallback for a missing or damaged inherited cache.

### Durability and crossing the generated/source boundary

Source and ordinary project files retain durable acknowledgement, dirty detection and independent results. A buffered Bee callback may acknowledge ordinary NTFS-buffered data only under an explicit regenerable-output policy. `Flush` must reach every relevant backing file and report failure without publishing success. Atomic visibility of a rename is distinct from crash/power-loss durability.

A file created in Bee and renamed into source must become part of the source generation and receive source durability before successful publication. Future writes through an existing handle must follow its new classification. Source moved into Bee must not silently lose already acknowledged source bytes. Until a safe transition exists, a narrow development prototype may explicitly reject these moves, but it cannot qualify as a transparent replacement for the product filesystem.

Persist the selected policy, workspace/basis identity, owned backing root and native file identities in current-version state. Unknown files, owner mismatch or interrupted state are preserved and rejected, with same-request recovery where defined. The generated-data policy belongs in the application adapter contract; do not hardcode Unity paths into Core or change all applications' write policy.

### Handles, mappings and ownership

Keep `FlushAndPurgeOnCleanup` and the current distinction between handle cleanup and final file-context close. An open count of zero is not proof that mapped data or executable-image contexts have drained. The native payload handle belongs to the logical identity, and must remain valid while reads, writes, deleted/replaced identities or retained virtual contexts need it. Backing disposal and unlink reclamation must drain those operations.

Do not allow a direct path to the backing tree to become the ordinary client path. Pin the task-owned root and ancestors, validate leaf identity/final path, reject unexpected reparse/hardlink/unsupported entries, and do not share writable native files across A/B or with the original. These are ownership safeguards, not an OS sandbox. They do not prove absence of arbitrary foreign data mappings. Native buffering may add NTFS cache pages while virtual cached pages still exist; no RAM reduction follows automatically.

## Product integration points

| Current code | Required design work |
| --- | --- |
| [Core/SnapshotOverlay.cs](../../src/Splinter.Core/SnapshotOverlay.cs), `DeltaEntry` and `IWorkspaceDeltaStore` | Keep stable identities and CRUD semantics. Define explicit observation of native bytes and durability; do not report a source-journal-only value as total storage. Keep native APIs outside Core. |
| [WinFspOverlayFileSystem.cs](../../src/Splinter.Platform.Windows/WinFspOverlayFileSystem.cs), `Write`, `Cleanup`, `Close`, `Rename`, listing and `Flush` | Route all operations coherently; retain overwritten/unlinked identities, honor cache flush, and avoid releasing backing at `Cleanup`. The global mutation gate and transfer-buffer copies remain costs unless independently changed and tested. |
| [ApplicationProjectPolicy.cs](../../src/Splinter.Core/ApplicationProjectPolicy.cs), [UnityProjectPolicy.cs](../../src/Splinter.Application.Unity/UnityProjectPolicy.cs) | The current generated descriptor identifies `Library`, not a native Bee policy. Add a generic policy boundary only if this design is selected, with a Unity-owned exact subtree selection. |
| [WinFspOverlayServer.cs](../../src/Splinter.Platform.Windows/WinFspOverlayServer.cs), store composition and source query | Current source queries select `SourceViewDeltaStore` or cast to `JournalDeltaStore`. A wrapper must preserve generation/content proof, stop/flush failure preservation and complete resource accounting. |
| [NtfsSnapshotWorkspaceBackend.cs](../../src/Splinter.Platform.Windows/NtfsSnapshotWorkspaceBackend.cs), `SealAsync`, `CompactAsync`, `RemoveDeltaAsync` | Current seals, size accounting and reclaim inventory are journal-specific. Bind any separate backing inventory to ownership and recovery; do not delete native files based only on the source journal's seal. Preserve pre/post/unmount checks. |
| [JournalDeltaCandidates.cs](../../src/Splinter.Runtime/JournalDeltaCandidates.cs), [JournalCandidatePublication.cs](../../src/Splinter.Runtime/JournalCandidatePublication.cs) | Candidate publication reads journal ranges and checks its source seal. Every source-classified byte must still become an independent result; a retained native-path reference is not independent. |
| [ManagedDirectoryBasis.cs](../../src/Splinter.Platform.Windows/ManagedDirectoryBasis.cs) | Keep original/peer dependencies and restoration guards. First evaluate task-private Bee only; changing the managed original Library's representation also changes restore planning and is a separate scope. |

## Why not start with a physical project root

An ordinary NTFS root could contain private Bee output and junctions to virtual `Assets`, `Packages`, `ProjectSettings` and other source trees. This may eliminate Bee's virtual dispatch, but those three names are not the complete editable project namespace. Root-level files, arbitrary new directories and source/VCS management files still need correct private storage and protection. Writable hardlinks to the basis or links to the mutable original are invalid shortcuts.

Editors and code tools can create a temporary file outside its eventual destination directory and replace a source file with it. Such an operation would cross NTFS and the virtual source volume. The present graft failure demonstrates why this needs direct atomic-rename proof, not a .NET copy-fallback test. It does not prove every reverse-layout operation will fail, but removes any basis for assuming transparent compatibility. Multiple source submounts also require same-volume identity and cross-subtree tests.

Representing every possible root file with a separate link does not solve arbitrary creation, enumeration, directory moves or link replacement. A complete solution would need a new namespace interception/layout contract. Initial full copy, ReFS, external-tool patches and an assumption that tools always write temporary files beside their destination are excluded. This alternative should remain deferred unless the smaller single-volume approach fails a meaningful correctness or cost gate.

## Authorized implementation and small validation sequence

The fixed experiment uses the existing three-assembly Unity 6000.3.22f1 Bee input, without starting an Editor or changing the original Unity project. `overlay` uses the current default bounded SJR2 journal. `internal-native` keeps that namespace and journal, but adds empty, opaque NTFS payloads only for newly created Bee files. Both variants retain cleanup purge. The historical junction mode is diagnostic evidence only.

| ID | Acceptance boundary |
| --- | --- |
| BN-01 | Stable namespace IDs, no client reparse point, native no-copy rename/replacement, old destination handles, inherited range writes, source durability and original/peer protection |
| BN-02 | Exact owner/policy/inventory, explicit flush, same-owner recovery and failure preservation; no power-loss qualification |
| BN-03 | Native mapped-file and image lifetime; normal stop with zero retained contexts; no filename-specific cleanup workaround |
| BN-04 | Actual cold/unchanged/incremental Bee builds and constants `42 → 43`; serial ABBA whole small lifecycle versus current bounded storage, with separate time/storage/RAM boundaries |
| BN-05 | Required build, reviewed implementation and paired documentation; failed runs remain evidence and cannot become successful timing samples |

Generated file promotion must preserve source durability and route subsequent existing-handle writes through the source journal. Unsupported directory moves with native descendants must fail before any namespace mutation. That limitation and the absence of product lifecycle integration prevent this candidate from being advertised as a transparent product backend.

1. **Namespace proof before timing:** on an empty owned Bee fixture, use native `MoveFileExW` with copy fallback disabled for new-name rename and replacement, both relative and absolute project paths. Check unchanged file identity, old destination handles, content, case/directory moves, failure preservation and original/peer sentinel hashes. A .NET move alone is insufficient.
2. **File and mapping behavior:** zero/short/multi-chunk and offset writes, holes, truncation, metadata, concurrent read/rename/delete, memory-mapped writes and executable-image lifetime. Preserve the callback contracts in `WinFspTransferTests` and `WinFspCleanupConcurrencyTests`; use a real mount for cache/mapping evidence. Require normal provider exit and zero retained contexts, not a filename-specific reopen workaround.
3. **No-copy and source safety:** inherited sparse/large file read and small overwrite without materialization; new-file replacement; source-boundary transfer with existing handles; independent result after backing/basis removal; dirty and target refusal; A release while B and the original remain intact. Reuse the behavioral coverage in `ImmutableCandidateTests`, `CandidateSourceProjectionTests` and `CandidatePublicationTests`.
4. **Recovery and flush:** interrupt owned children at namespace publication, flush, source promotion and reclaim boundaries. Check explicit buffered-cache versus durable-source expectations, exact inventory and same-request recovery. This is process-interruption evidence, not power-loss evidence.
5. **Actual small Bee:** only after those gates, replay the existing three-assembly input. Require successful cold, unchanged and one-source-change backend builds, compiled constants `42 → 43`, atomic state/digest-cache publication and normal closure. Then perform a bounded serial ABBA timing with preparation, graph, compile, flush, preservation and cleanup separately. Preserve failed attempts. No Editor, ILPP or large-project claim follows.

Measure native and journal allocation separately, including namespace metadata and temporary replacement files. Report logical bytes, allocated bytes, buffering/RAM observations and timing boundaries; final allocation is not peak. A useful result must beat the current complete small backend lifecycle, not merely its graph phase. No numeric speedup is predicted by this review.

## Decision after the first candidate

[BN-01–05](../verification/bee-internal-native.md) rejects product adoption on measured cost, while retaining the working atomic-publication and safety evidence. A next structural candidate would place the native association in the same authoritative namespace mutation/replay record, reducing the cross-store per-file intents without removing owner, source durability or recovery proofs. The current extra control-publication cost is not individually timed, and this alternative is unimplemented; no speedup is predicted. Dirty-only native flushing may remove repeated clean-file work, but cannot replace the final flush after retained contexts drain.
